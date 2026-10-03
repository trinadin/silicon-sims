using FSO.Files.Formats.IFF.Chunks;
using FSO.Files.Utils;
using FSO.SimAntics.Engine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FSO.SimAntics.Primitives
{
    public class VMTS1InventoryOperations : VMPrimitiveHandler
    {
        public override VMPrimitiveExitCode Execute(VMStackFrame context, VMPrimitiveOperand args)
        {
            //todo: check condition
            var operand = (VMTS1InventoryOperationsOperand)args;

            //tragic clown magic growth qualify: hasToken, stack object
            //6 17 45

            //magic person a - check inventory (caller??)
            //7 17 45

            //teleport here check: hasToken
            //6 17 13
            //has wand check:
            //7 17 13

            //get 35 coins:
            //7 3 9
            //get wand, toadsweat:
            //7 1 9

            //spawn purchases test (mode 4)
            //0 180 4
            //5 180 4
            //4 180 4

            //remove token caller get in wagon:
            //1 132 8
            //mode 4 something:
            //1 180 4
            //3 180 4
            //
            //weird remove request with wrong type (0 instead of 8):
            //0x82, 0x09

            //NOTES:
            // - has token and remove token currently ignore the type, and only look for entries with the given GUID.
            //   I haven't seen inventory items appear under multiple types, so this shouldn't affect anything right now
            //   ...but there is probably a flag for it!

            //$FamilyAssets $NameAttrib $NeighborLocal $FameTitleLocal

            //=== from exe ===
            //1. | of count | and index to Temp | . | Returning count to Temp | 0. | starting at index  | stored in Temp | from Stack Object's GUID | with object |
            // (Using Inventory of object in Temp 4)
            //types:
            // SKILL   SOUVENIR    PURCHASE    SIMDATA     DATE    INGREDIENT  MAGIC   GIFT

            var neighbourhood = Content.Content.Get().Neighborhood;
            var inTarget = (operand.UseObjectInTemp4) ? context.VM.GetObjectById(context.Thread.TempRegisters[4]) : context.Caller;
            // ENG-06 review P2-3: with the owner gate corrected to Flags.0x20, the
            // CALLER path (and a stale Temp[4] object id on re-entered frames) can
            // resolve null or to a non-avatar — the native has a null-owner debug-report
            // path here (decode §2.1); the port returns FALSE instead of throwing
            // (observed live: HD 4119 'Spawn Downtown Purchases - TEST' on the
            // post-return re-entry).
            if (!(inTarget is VMAvatar)) return VMPrimitiveExitCode.GOTO_FALSE;
            var target = (VMAvatar)(inTarget);
            var neighbour = target.GetPersonData(Model.VMPersonDataVariable.NeighborId);
            var inventory = neighbourhood.GetInventoryByNID(neighbour);
            // ENG-06 review P2-2: the count SOURCE selector is the corrected CountTemp
            // ((Flags2.0x0C)>>2) — the old Temp[0] hardcode contradicted the corrected
            // property when the selector was nonzero.
            var count = (operand.CountInTemp) ? context.Thread.TempRegisters[operand.CountTemp] : 1;
            var type = operand.TokenType; //type 0 on find of type indicates "any type".
            var index = context.Thread.TempRegisters[operand.IndexTemp];
            
            //type 4: magic town purchasables
            //type 6: vacation purchasables
            var guid = (operand.GUID == 0) ? (uint)context.StackObject.Object.GUID : operand.GUID;
            //var guid = operand.GUID;

            switch (operand.Mode)
            {
                case VMTS1InventoryMode.AddToken:
                    inventory = InitInventory(neighbour, inventory);
                    //if we have an existing item add to that
                    var aitem = inventory.FirstOrDefault(x => x.GUID == guid && x.Type == type);

                    if (aitem == null)
                        inventory.Add(new InventoryItem() { Count = (ushort)count, GUID = guid, Type = type });
                    else
                        aitem.Count += (ushort)count;

                    return VMPrimitiveExitCode.GOTO_TRUE;

                case VMTS1InventoryMode.RemoveToken:
                    if (inventory == null) return VMPrimitiveExitCode.GOTO_FALSE; //can't remove a token that isn't there
                    var ritem = inventory.FirstOrDefault(x => x.GUID == guid && (type == 0 || x.Type == type));
                    if (ritem == null || ritem.Count < count) return VMPrimitiveExitCode.GOTO_FALSE; //can't remove a token that isn't there
                    if (count == -1) count = ritem.Count; //count of -1 means remove all
                    ritem.Count -= (ushort)count;
                    if (ritem.Count == 0) inventory.Remove(ritem);
                    //todo: does this write the index?
                    return VMPrimitiveExitCode.GOTO_TRUE;

                case VMTS1InventoryMode.RemoveTokenAtIndex:
                    //
                    if (inventory == null || index < 0 || index >= inventory.Count)
                        return VMPrimitiveExitCode.GOTO_FALSE; //can't remove a token that isn't there

                    ritem = inventory[index];
                    if (count == -1) count = ritem.Count; //count of -1 means remove all
                    ritem.Count -= (ushort)count;
                    if (ritem.Count == 0) inventory.Remove(ritem);
                    if (operand.NextIndexIntoTemp)
                        context.Thread.TempRegisters[operand.IndexTemp] = (short)(index-1);
                    return VMPrimitiveExitCode.GOTO_TRUE;

                case VMTS1InventoryMode.FindToken:
                    if (inventory == null) return VMPrimitiveExitCode.GOTO_FALSE;
                    var foundindex = inventory.FindIndex(x => x.GUID == guid && (type == 0 || x.Type == type));
                    if (foundindex == -1) return VMPrimitiveExitCode.GOTO_FALSE;
                    var items = inventory[foundindex];
                    var itemcount = (short)(items.Count);
                    // ENG-06 (decode mode-3 law): count-dst = Temp[(Flags2.0x60)>>5],
                    // index-dst = Temp[(Flags.0x18)>>3], BOTH written unconditionally on
                    // success (the old FoundIndexIntoTemp gate + the shared selectors
                    // were the port's guess; zero corpus callers — latent)
                    context.Thread.TempRegisters[(operand.Flags2 & 0x60) >> 5] = itemcount;
                    context.Thread.TempRegisters[(operand.Flags & 0x18) >> 3] = (short)foundindex;
                    return (itemcount > 0) ? VMPrimitiveExitCode.GOTO_TRUE : VMPrimitiveExitCode.GOTO_FALSE;
                    //return (itemcount >= count)? VMPrimitiveExitCode.GOTO_TRUE : VMPrimitiveExitCode.GOTO_FALSE;

                case VMTS1InventoryMode.SetToNextTokenOfType: //ignores guid
                    // ENG-06 (decode §3 mode 4, review P2-1 CORRECTED): the INDEX
                    // passed in is Temp[Flags2 & 3] (the source selector — the shared
                    // `index` read above); the NEXT INDEX is written to
                    // Temp[(Flags.0x18)>>3] (the same index-DST selector mode 3 uses)
                    // and the service's count result to Temp[(Flags2.0x60)>>5];
                    // no next token -> FALSE. Zero corpus callers — latent. The value
                    // written to the count-dst is the FOUND TOKEN's Count (the native
                    // r's exact content is not decoded further — disclosed).
                    if (inventory == null) return VMPrimitiveExitCode.GOTO_FALSE;
                    var items2 = inventory.Where(x => x.Type == type).ToList();
                    var next = items2.FirstOrDefault(x => inventory.IndexOf(x) > index);
                    if (next == null) return VMPrimitiveExitCode.GOTO_FALSE;
                    foundindex = inventory.IndexOf(next);
                    context.Thread.TempRegisters[(operand.Flags & 0x18) >> 3] = (short)foundindex;
                    context.Thread.TempRegisters[(operand.Flags2 & 0x60) >> 5] = (short)next.Count;
                    return VMPrimitiveExitCode.GOTO_TRUE;

                case VMTS1InventoryMode.Temp0NeighborAsAutofollow: //5
                case VMTS1InventoryMode.Temp0NeighborAsFollowHome: //6
                    // ENG-06 (inventory-dispatch-decode divergence 4): the native stores
                    // the NEIGHBOR'S GUID as a real token — RemoveAllTokensOfType(1|3)
                    // then AddToken(type 1|3, guid = GetGUID(FindNeighborByID(Temp[0])),
                    // count = 1); TokenType operand ignored. The old port shape (magic
                    // GUID 10|11, Type 2, Count := the nid) was a stand-in whose readers
                    // (generic calls 19/20) are corrected with it — the token's GUID is
                    // now consumed directly by the spawn. Port guard: an unknown nid
                    // returns FALSE (the native's null-neighbor path is undecoded).
                    {
                        var nType = (operand.Mode == VMTS1InventoryMode.Temp0NeighborAsAutofollow) ? (ushort)1 : (ushort)3;
                        var nRec = neighbourhood.GetNeighborByID(context.Thread.TempRegisters[0]);
                        if (nRec == null) return VMPrimitiveExitCode.GOTO_FALSE;
                        inventory = InitInventory(neighbour, inventory);
                        inventory.RemoveAll(x => x.Type == nType);
                        inventory.Add(new InventoryItem() { Count = 1, GUID = nRec.GUID, Type = nType });
                        return VMPrimitiveExitCode.GOTO_TRUE;
                    }
                default:
                    // ENG-06 (decode): the native TryInventoryAction dispatch ends at
                    // mode 6; every mode > 6 is the common tail — CPState::SetDirty
                    // (0x04000000) + return TRUE, no inventory access. The corpus's
                    // 24/27/32 sites (Masseur/Masseuse/Director/Campfire mains,
                    // dart_board, BeeHive) are intentional native no-ops; this
                    // fall-through IS the law.
                    return VMPrimitiveExitCode.GOTO_TRUE;
            }
        }

        private List<InventoryItem> InitInventory( short neighbour, List<InventoryItem> inventory)
        {
            if (inventory == null)
            {
                var neighbourhood = Content.Content.Get().Neighborhood;
                //set up this neighbour's inventory...
                inventory = new List<InventoryItem>();
                neighbourhood.SetInventoryForNID(neighbour, inventory);
            }
            return inventory;
        }
    }

    public class VMTS1InventoryOperationsOperand : VMPrimitiveOperand
    {
        public VMTS1InventoryMode Mode { get; set; }
        public byte TokenType { get; set; } //token type
        public byte Flags { get; set; } //flags
        //1 - unknown (regularly set for find token, ONLY this is set for add token and remove)
        //2 - count in temp
        //4-8 - temp[num] := count (set to next related)
        //16 - found index into temp (regularly set for find token)
        //32 - ??
        //64 - ??
        //128 - index in temp (set to next)
        public byte Flags2 { get; set; }
        //1-2 - temp[num] := index (1 regularly set, not in set to next? would be index in temp 0)
        //4 - (set in find token)
        //8 - (very regularly set)
        //16 - ??
        //32 - object in temp 4
        //64 - ??
        //128 - mode 8 remove time tokens?
        public uint GUID { get; set; }

        public int CountTemp
        {
            get
            {
                // ENG-06 (divergence 2): the native count-SOURCE selector is
                // (op[3].0x0C)>>2 = Flags2 bits.
                return (Flags2 >> 2) & 3;
            }
        }

        public int IndexTemp
        {
            get
            {
                return Flags2 & 3;
            }
        }

        public bool NextIndexIntoTemp
        {
            get
            {
                return (Flags & 0x80) > 0;
            }
        }

        public bool FoundIndexIntoTemp
        {
            get
            {
                return (Flags & 0x10) > 0;
            }
        }

        public bool CountInTemp
        {
            get
            {
                // ENG-06 (divergence 2): the native count-enable is op[3].0x02 =
                // Flags2.0x02. All 16 corpus sites carry Flags2=0xFD (bit1 clear)
                // — native count = 1 everywhere; the old Flags.0x02 reading made
                // 3 mode-2 sites (Guitar 4097, Souvenir 4179, Newspaper 4097)
                // read a Temp count instead.
                return (Flags2 & 2) > 0;
            }
            set
            {
                Flags2 &= unchecked((byte)(~2));
                if (value) Flags2 |= 2;
            }
        }

        public bool UseObjectInTemp4
        {
            get
            {
                // ENG-06 (inventory-dispatch-decode divergence 1): the native
                // owner gate is op[2].0x20 = Flags.0x20 (PPC prologue 0xebcc0,
                // capstone-verified rlwinm). The old Flags2.0x20 reading put ALL
                // 16 corpus sites on the Temp[4]-owner path; natively only 6
                // (Flags in {0x22,0x23,0x2f,0x33}) use it — the other 10 use the
                // CALLER's inventory.
                return (Flags & 32) > 0;
            }
            set
            {
                Flags &= unchecked((byte)(~32));
                if (value) Flags |= 32;
            }
        }

        #region VMPrimitiveOperand Members
        public void Read(byte[] bytes)
        {
            using (var io = IoBuffer.FromBytes(bytes, ByteOrder.LITTLE_ENDIAN))
            {
                Mode = (VMTS1InventoryMode)io.ReadByte();
                TokenType = io.ReadByte();
                Flags = io.ReadByte();
                Flags2 = io.ReadByte();
                GUID = io.ReadUInt32();
            }
        }

        public void Write(byte[] bytes)
        {
            using (var io = new BinaryWriter(new MemoryStream(bytes)))
            {
                io.Write((byte)Mode);
                io.Write(TokenType);
                io.Write(Flags);
                io.Write(Flags2);
                io.Write(GUID);
            }
        }
        #endregion
    }

    public enum VMTS1InventoryMode : byte
    {
        AddToken = 0, //add
        RemoveToken = 1,
        RemoveTokenAtIndex = 2,
        FindToken = 3, //count in temp0
        SetToNextTokenOfType = 4, //count in temp0. ignores guid.
        Temp0NeighborAsAutofollow = 5, 
        Temp0NeighborAsFollowHome = 6,
        UnusedRemoveTimeData = 8
    }
}
