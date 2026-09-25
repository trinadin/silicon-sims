using FSO.Content.Model;
using FSO.Content.TS1;
using FSO.Files.Formats.IFF.Chunks;
using FSO.Files.Formats.IFF;
using FSO.LotView.Model;
using FSO.SimAntics.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using FSO.SimAntics.Marshals;
using FSO.SimAntics.Model.TS1Platform;
using FSO.SimAntics.Marshals.Threads;
using Microsoft.Xna.Framework;
using FSO.SimAntics.Entities;
using FSO.Vitaboy;

namespace FSO.SimAntics.Utils
{
    public class VMTS1ActivatorNew
    {
        private const int OpcodeGotoRelative = 27;
        private const int OpcodeGotoSlot = 45;
        private const int OpcodeGosub = 30;
        private const int OpcodeIdleForInput = 17;

        private class TS1MultitileBuilder
        {
            public string Name;
            public List<short> Objects;
            public List<LotTilePos> Offsets;
            public int Price;
        }

        private VM VM;
        private int Size;
        private bool FlipRoad;
        private short HouseNumber;
        private readonly Dictionary<short, string> StaleThreadContinuations = new Dictionary<short, string>();

        public VMTS1ActivatorNew(VM vm, short hn)
        {
            this.VM = vm;
            HouseNumber = hn;
        }

        private T[] ArrayPad<T>(T[] array, int count)
        {
            if (array.Length >= count)
            {
                return array;
            }
            else
            {
                var result = new T[count];

                for (int i = 0; i < array.Length; i++)
                {
                    result[i] = array[i];
                }

                return result;
            }
        }

        private OBJD GetMasterOBJD(OBJD tile)
        {
            var file = tile.ChunkParent;
            var list = file.List<OBJD>();

            return list.FirstOrDefault((other) => other.IsMaster && other.MasterID == tile.MasterID);
        }

        private VMRoutine GetRoutine(VMStackFrameMarshal frame)
        {
            var content = Content.Content.Get();
            var res = content.WorldObjects.Get(frame.CodeOwnerGUID)?.Resource;

            if (res == null)
            {
                return null;
            }

            VMRoutine routine;
            if (frame.RoutineID >= 8192) routine = (VMRoutine)res.SemiGlobal?.GetRoutine(frame.RoutineID);
            else if (frame.RoutineID >= 4096) routine = (VMRoutine)res.GetRoutine(frame.RoutineID);
            else routine = (VMRoutine)VM.Context.Globals.Resource.GetRoutine(frame.RoutineID);

            return routine;
        }

        private bool IsRestorableStack(VMStackFrameMarshal[] stack, out string invalidFrames)
        {
            var invalid = new List<string>();
            foreach (var frame in stack ?? new VMStackFrameMarshal[0])
            {
                VMRoutine routine = null;
                try
                {
                    routine = GetRoutine(frame);
                }
                catch
                {
                    // A missing code owner or semi-global is itself a stale continuation.
                }

                var instructions = routine?.Instructions;
                if (instructions == null || instructions.Length == 0 || frame.InstructionPointer >= instructions.Length)
                {
                    invalid.Add(frame.RoutineID + ":" + frame.InstructionPointer + "@" + frame.CodeOwnerGUID.ToString("x8")
                        + "(len=" + (instructions == null ? "missing" : instructions.Length.ToString()) + ")");
                }
            }
            invalidFrames = string.Join(",", invalid);
            return invalid.Count == 0;
        }

        private VMArchitectureMarshal ConvertArchitecture(IffFile iff, HOUS hous, int size)
        {
            var arch = new VMArchitectureMarshal();

            arch.Width = Size;
            arch.Height = Size;
            arch.Stories = 5;

            arch.Floors = new FloorTile[5][];
            arch.Walls = new WallTile[5][];

            // Create unused floors
            for (int i = 2; i < 5; i++)
            {
                arch.Floors[i] = new FloorTile[Size * Size];
                arch.Walls[i] = new WallTile[Size * Size];
            }

            arch.Terrain = new VMArchitectureTerrain(size, size);

            TerrainType ttype = TerrainType.GRASS;
            if (!VMTS1Activator.HouseNumToType.TryGetValue(HouseNumber, out ttype))
                ttype = TerrainType.GRASS;
            arch.Terrain.LightType = (ttype == TerrainType.SAND) ? TerrainType.GRASS : ttype;
            arch.Terrain.DarkType = ttype;

            var floorM = iff.Get<FLRm>(1)?.Entries ?? iff.Get<FLRm>(0)?.Entries ?? new List<WALmEntry>();
            var wallM = iff.Get<WALm>(1)?.Entries ?? iff.Get<WALm>(0)?.Entries ?? new List<WALmEntry>();

            var floorDict = VMTS1Activator.BuildFloorDict(floorM);
            var wallDict = VMTS1Activator.BuildWallDict(wallM);

            //altitude as 0
            var advFloors = iff.Get<ARRY>(11);
            var flags = iff.Get<ARRY>(8).TransposeData;
            if (advFloors != null)
            {
                //advanced walls and floors from modern ts1. use 16 bit wall/floor data.
                arch.Floors[0] = VMTS1Activator.RemapFloors(FlipRoad, VMTS1Activator.DecodeAdvFloors(advFloors.TransposeData), floorDict, flags);
                arch.Floors[1] = VMTS1Activator.RemapFloors(FlipRoad, VMTS1Activator.DecodeAdvFloors(iff.Get<ARRY>(111).TransposeData), floorDict, flags);
                //objects as 3
                arch.Walls[0] = VMTS1Activator.RemapWalls(VMTS1Activator.DecodeAdvWalls(iff.Get<ARRY>(12).TransposeData), wallDict, floorDict, arch.Floors[0]);
                arch.Walls[1] = VMTS1Activator.RemapWalls(VMTS1Activator.DecodeAdvWalls(iff.Get<ARRY>(112).TransposeData), wallDict, floorDict, arch.Floors[1]);
            }
            else
            {
                arch.Floors[0] = VMTS1Activator.RemapFloors(FlipRoad, VMTS1Activator.DecodeFloors(iff.Get<ARRY>(1).TransposeData), floorDict, flags);
                arch.Floors[1] = VMTS1Activator.RemapFloors(FlipRoad, VMTS1Activator.DecodeFloors(iff.Get<ARRY>(101).TransposeData), floorDict, flags);
                //objects as 3
                arch.Walls[0] = VMTS1Activator.RemapWalls(VMTS1Activator.DecodeWalls(iff.Get<ARRY>(2).TransposeData), wallDict, floorDict, arch.Floors[0]);
                arch.Walls[1] = VMTS1Activator.RemapWalls(VMTS1Activator.DecodeWalls(iff.Get<ARRY>(102).TransposeData), wallDict, floorDict, arch.Floors[1]);
            }
            //objects as 103
            arch.Terrain.GrassState = iff.Get<ARRY>(6).TransposeData.Select(x => (byte)(127 - x)).ToArray();

            //targetgrass is 7
            //flags is 8/108
            var pools = iff.Get<ARRY>(9).TransposeData;
            var water = iff.Get<ARRY>(10).TransposeData;

            for (int i = 0; i < pools.Length; i++)
            {
                //pools in freeso are slightly different
                if (pools[i] != 0xff && pools[i] != 0x0) arch.Floors[0][i].Pattern = 65535;
                if (water[i] != 0xff && water[i] != 0x0) arch.Floors[0][i].Pattern = 65534;
            }

            arch.Floors[0] = VMTS1Activator.ResizeFloors(arch.Floors[0], size);
            arch.Floors[1] = VMTS1Activator.ResizeFloors(arch.Floors[1], size);
            arch.Walls[0] = VMTS1Activator.ResizeWalls(arch.Walls[0], size);
            arch.Walls[1] = VMTS1Activator.ResizeWalls(arch.Walls[1], size);
            arch.FineBuildableArea = VMTS1Activator.ResizeFlags(flags, size);
            arch.Terrain.GrassState = VMTS1Activator.ResizeGrass(arch.Terrain.GrassState, size);
            arch.Terrain.Heights = Array.ConvertAll(VMTS1Activator.ResizeGrass(VMTS1Activator.DecodeHeights(iff.Get<ARRY>(0).TransposeData), size), x => (short)(x * 10));
            arch.Terrain.RegenerateCenters();
            arch.RoofStyle = (uint)Content.Content.Get().WorldRoofs.NameToID(hous.RoofName.ToLowerInvariant() + ".bmp");

            arch.FloorsDirty = true;
            arch.WallsDirty = true;

            return arch;
        }

        private TTABFlags ConvertFlags(OBJMInteractionFlags flags)
        {
            TTABFlags result = 0;

            if ((flags & OBJMInteractionFlags.PushHeadContinuation) != 0)
            {
                result |= TTABFlags.FSOPushHead;
            }

            return result;
        }

        private VMQueuedActionMarshal ConvertAction(OBJM objm, OBJT objt, OBJMInstance inst, OBJMInteraction action)
        {
            var calleeObj = objm.ObjectData[action.TargetID].Instance;

            return new VMQueuedActionMarshal()
            {
                UID = (ushort)action.UID,
                RoutineID = (ushort)action.ActionTreeID,
                CheckRoutineID = 0, // In TS1, it seems to find this each check with the TTA index.
                Callee = action.TargetID,
                StackObject = action.TargetID,
                CodeOwnerGUID = calleeObj.OBJT.GUID,
                IconOwner = action.Icon,
                Args = action.Args,
                Priority = (short)action.Priority,
                Name = "", // TODO: recover this

                Mode = Engine.VMQueueMode.Normal,
                // TODO: TS1 doesn't save a lot of flags for checks, so we need to make the game fetch it when necessary.
                // Not up for fetching the TTAB from here, so skip permission checks when loading for now.
                Flags = TTABFlags.FSOSkipPermissions | ConvertFlags(action.Flags),
                Flags2 = 0, // This isn't TSO.
                NotifyIdle = action.Flags.HasFlag(OBJMInteractionFlags.UserInterrupted), // Also impacted by priority, but this one is obvious enough.

                InteractionNumber = action.TTAIndex,
            };
        }

        private VMThreadMarshal ConvertThread(OBJM objm, OBJT objt, OBJMInstance inst)
        {
            var thread = new VMThreadMarshal();
            short myID = inst.ObjectData[(int)VMStackObjectVariable.ObjectId];

            thread.Stack = new VMStackFrameMarshal[inst.Stack.Length];
            thread.TempRegisters = ArrayPad(inst.TempRegisters, 20); // FSO has 20 temp registers rather than 8

            for (int i = 0; i < thread.Stack.Length; i++)
            {
                var frame = inst.Stack[i];

                var ownerType = objt.Entries[frame.CodeOwnerObjType - 1];

                thread.Stack[i] = new VMStackFrameMarshal()
                {
                    StackObject = frame.StackObjectID,
                    RoutineID = (ushort)frame.TreeID,
                    CodeOwnerGUID = ownerType.GUID,
                    InstructionPointer = (ushort)frame.NodeID,

                    // FreeSO expects at least 4 arguments in a bunch of places - TS1 is more strict.
                    Args = ArrayPad(frame.Parameters, 4),
                    Locals = frame.Locals,

                    Caller = myID,
                    Callee = myID, // FreeSO specfic thing that probably should have been removed. Sort of restored from use counts for people.

                    ActionTree = false, // This is restored later for person type objects.
                    SpecialResult = Engine.VMSpecialResult.Normal,
                };
            }

            // Expansion installs can replace an object's private/semiglobal BHAVs without
            // rewriting the OBJM continuation stored in an older lot. Restored frames bypass
            // VMThread.Push, so a missing routine or stale instruction pointer would otherwise
            // fault later when an idle child unwinds. A call stack is atomic: if any frame is no
            // longer executable, reject the entire continuation and reset this object once after
            // VM.Load has restored all object/group references. This is deliberately TS1-import
            // only; TSO/network snapshots retain their existing load contract.
            if (!IsRestorableStack(thread.Stack, out var invalidFrames))
            {
                StaleThreadContinuations[myID] = invalidFrames;
                thread.Stack = new VMStackFrameMarshal[0];
                thread.Queue = new VMQueuedActionMarshal[0];
                thread.ActiveQueueBlock = -1;
                thread.ActionUID = 0;
                thread.Interrupt = false;

                if (inst.PersonData.HasValue)
                {
                    var personData = inst.PersonData.Value.PersonData;
                    var priority = (int)VMPersonDataVariable.Priority;
                    var nonInterruptable = (int)VMPersonDataVariable.NonInterruptable;
                    if (personData != null && priority < personData.Length) personData[priority] = 0;
                    if (personData != null && nonInterruptable < personData.Length) personData[nonInterruptable] = 0;
                    var flags = (int)VMStackObjectVariable.Flags;
                    if (inst.ObjectData != null && flags < inst.ObjectData.Length)
                        inst.ObjectData[flags] &= (short)~VMEntityFlags.InteractionCanceled;
                }
                return thread;
            }

            // Original TreeStack::ReconStream stores the generic StackElem+8
            // word. It is a user-event phase only at an actual opcode35 node;
            // private tree IDs alone are not unique across code owners.
            for (int i = 0; i < thread.Stack.Length; i++)
            {
                var phase = inst.Stack[i].PrimitiveState;
                if (phase != 1 && phase != 2) continue;
                var frame = thread.Stack[i];
                var routine = GetRoutine(frame); // validated atomically above
                if (routine.Instructions[frame.InstructionPointer].Opcode == 35)
                    frame.TS1UserEventPhase = (byte)phase;
            }

            if (inst.PersonData != null)
            {
                // Restore interaction queue, use counts
                var person = inst.PersonData.Value;

                int activeCount = person.ActiveInteraction.IsValid() ? 1 : 0;
                thread.ActiveQueueBlock = (sbyte)(activeCount - 1);
                thread.Queue = new VMQueuedActionMarshal[activeCount + person.InteractionQueue.Length];

                short activePriority = person.PersonData[(int)VMPersonDataVariable.Priority];
                if (activeCount != 0)
                {
                    thread.Queue[0] = ConvertAction(objm, objt, inst, person.ActiveInteraction);
                    thread.ActionUID = thread.Queue[0].UID;
                }

                for (int j = 0; j < person.InteractionQueue.Length; j++)
                {
                    thread.Queue[activeCount + j] = ConvertAction(objm, objt, inst, person.InteractionQueue[j]);

                    if (activeCount != 0 && thread.Queue[activeCount + j].Priority > activePriority)
                    {
                        thread.Queue[0].NotifyIdle = true;
                    }
                }

                thread.Interrupt = false; // TODO: Don't really know a good mapping for this...

                // Try to restore callees for the stack and some special state.

                foreach (var use in person.ObjectUses)
                {
                    var frame = thread.Stack[use.StackLength - 1];
                    var inAction = false;

                    if (use.StackLength > 1)
                    {
                        // This is a bit of an adapter. When returning from a stack frame, the parent frame needs to handle the result.
                        // For subroutines, the result of the frame just determines where we go from the subroutine node.
                        // Calling named trees works the same way.
                        // In TS1, certain types of primitive handle this differently. Gosub found action and idle for input will ignore results from interactions.
                        // They also remove the interaction from being active, obviously.
                        // In FreeSO, this is implemented as a SpecialResult flag on the stack frame, but it _should_ be tied to the primitive.
                        // Routing primitives are another one that have special handling for returns from portal functions.
                        // Normally, FreeSO handles these because routing actually occupies space in the stack, and the result code goes into the routing frame.
                        // When restoring TS1 stacks though, it just goes straight back into the calling tree with the routing primitive...
                        // It should rerun in this case, since we don't have any routing state in the save.
                        var callingFrame = thread.Stack[use.StackLength - 2];

                        var routine = GetRoutine(callingFrame);
                        var prim = routine.Instructions[callingFrame.InstructionPointer];

                        if (prim.Opcode == OpcodeGotoRelative || prim.Opcode == OpcodeGotoSlot)
                        {
                            frame.SpecialResult = Engine.VMSpecialResult.Retry;
                        }
                        else if (prim.Opcode == OpcodeGosub || prim.Opcode == OpcodeIdleForInput)
                        {
                            frame.SpecialResult = Engine.VMSpecialResult.Interaction;
                            inAction = true;
                        }
                    }

                    for (int i = use.StackLength - 1; i < thread.Stack.Length; i++)
                    {
                        thread.Stack[i].Callee = use.TargetID;
                        thread.Stack[i].ActionTree |= inAction;
                    }
                }
            }
            else
            {
                thread.ActiveQueueBlock = -1;
                thread.Queue = new VMQueuedActionMarshal[0];
                thread.Interrupt = false;
                thread.ActionUID = 0;
            }

            return thread;
        }

        private VMAnimationStateMarshal ConvertAnimation(string anim, int eventsRun)
        {
            var split = anim.Split(';');

            //name;priority;speed (1/1000ths);frame;weight? (1/1000ths);loop;unk;unk

            var name = split[0];
            var speed = int.Parse(split[2]);
            var frame = int.Parse(split[3]);
            var weight = int.Parse(split[4]);
            var loop = split[5] == "1";

            var anims = Content.Content.Get().AvatarAnimations;

            var res = anims.Get($"{name}.anim");
            int frames = res == null ? 1 : res.NumFrames;

            int end = speed < 0 ? 0 : 1000;

            return new VMAnimationStateMarshal()
            {
                Anim = name,
                Speed = Math.Abs(speed) / 1000f,
                CurrentFrame = frames * (frame / 1000f),
                Weight = weight / 1000f,
                Loop = loop,
                EndReached = frame == end,
                PlayingBackwards = speed < 0,
                // TODO: The original seems to use events count determine this live,
                // rather than the animation queuing events like in FreeSO.
                // This might skip events right now...
                EventQueue = new short[0], 
                EventsRun = (byte)eventsRun,
            };
        }

        // SAV-11: true when the person's Neighbour store record (looked up by the
        // record's own NeighbourId) carries the death flag (person data word 68).
        private static bool IsDeadStoreRecord(OBJMPerson person, NBRS neighbors)
        {
            var pd = person.PersonData;
            if (pd == null || pd.Length <= (int)VMPersonDataVariable.NeighborId) return false;
            try
            {
                if (!neighbors.NeighbourByID.TryGetValue(pd[(int)VMPersonDataVariable.NeighborId], out var rec)) return false;
                var rpd = rec.PersonData;
                return rpd != null && rpd.Length > (int)VMPersonDataVariable.IsGhost
                    && rpd[(int)VMPersonDataVariable.IsGhost] > 0;
            }
            catch { return false; }
        }

        private VMAvatarMarshal ConvertAvatar(OBJMInstance inst, NBRS neighbors)
        {
            var person = inst.PersonData.Value;

            var bodyStrings = inst.OBJD.ChunkParent.Get<STR>(inst.OBJD.BodyStringID);

            var ava = new VMAvatarMarshal()
            {
                Animations = person.Animation == "" ? new VMAnimationStateMarshal[0] : new VMAnimationStateMarshal[]
                {
                    ConvertAnimation(person.Animation, person.AnimEventCount)
                },
                CarryAnimationState = person.CarryAnimation == "" ? null : ConvertAnimation(person.CarryAnimation, 0),

                MotiveChanges = new VMMotiveChange[16],
                MotiveDecay = new VMTS1MotiveDecay(),
                PersonData = person.PersonData,
                MotiveData = person.MotiveData.Select(motive =>
                {
                    return (short)Math.Round(motive);
                }).ToArray(),

                RadianDirection = inst.ObjectData[(int)VMStackObjectVariable.Direction] * (float)(Math.PI / 4.0),

                DefaultSuits = new VMAvatarDefaultSuits(false)
                {
                    Daywear = new VMOutfitReference(bodyStrings, false)
                },
                DynamicSuits = new VMAvatarDynamicSuits(false), // Not used in TS1
                Decoration = new VMAvatarDecoration(),
                BoundAppearances = person.Accessories.Select(accessory => accessory.Name).ToArray(),

                BodyOutfit = person.Body == "" ? null : new VMOutfitReference($"{person.Body},{person.BodyTex}", false),
                HeadOutfit = person.Head == "" ? null : new VMOutfitReference($"{person.Head},{person.HeadTex}", true),
                SkinTone = DiskSkinToAppearance(person.PersonData[(int)VMPersonDataVariable.SkinColor], RecordVersionFor(neighbors, person))
            };

            for (int i = 0; i < 16; i++)
            {
                ava.MotiveChanges[i] = new VMMotiveChange();
                ava.MotiveChanges[i].Motive = (VMMotive)i;
            }

            foreach (var delta in person.MotiveDeltas)
            {
                var target = ava.MotiveChanges[delta.Motive];
                target.MaxValue = (short)delta.StopAt;
                target.PerHourChange = (short)Math.Round(delta.TickDelta * 1800);
            }

            return ava;
        }

        /// <summary>
        /// R252: original-format skin remap (read boundary). The original TS1 disk
        /// encoding for pd[60]/SkinColor is lgt=1, drk=2, med=3 (non-monotonic),
        /// whereas AppearanceType is Light=0, Medium=1, Dark=2. The old code cast
        /// the disk value straight to AppearanceType, so a light sim (disk 1)
        /// rendered as Medium and a medium sim (disk 3) mapped to an out-of-range
        /// enum. Which dialect is in person.PersonData[60] depends on the NBRS
        /// record Version that produced the sim: Version 0x4 (original) stores
        /// 1/2/3, Version 0xA (the port's legacy format) stored 0/1/2. The stored
        /// array is NOT mutated, so a save round-trips the disk value verbatim.
        /// </summary>
        // R252 P0-2: public seam for the disk skin remap so the port's own
        // recordfmt autotest (AutotestCASFlow in Simitone.Client) can assert the
        // production map directly. Mirrors the public SkinToDisk write-side helper
        // in SimitoneNeighbourGenerator.
        public static AppearanceType DiskSkinToAppearance(short disk, int recordVersion)
        {
            if (recordVersion == 0x4)
            {
                // original encoding: 1=light, 2=dark, 3=medium
                switch (disk)
                {
                    case 3: return AppearanceType.Medium;
                    case 2: return AppearanceType.Dark;
                    case 1: default: return AppearanceType.Light;
                }
            }
            // legacy 0xA port encoding: 0=light, 1=medium, 2=dark
            switch (disk)
            {
                case 2: return AppearanceType.Dark;
                case 1: return AppearanceType.Medium;
                case 0: default: return AppearanceType.Light;
            }
        }

        /// <summary>
        /// Resolve the NBRS record Version for an avatar's person data so the skin
        /// dialect can be chosen. The record is looked up by the NeighborId stored
        /// in the person data (VMPersonDataVariable.NeighborId = 31).
        ///
        /// NOTE (R252 P0): pd[31] is 0 on BOTH the shipped original saves AND the
        /// port's own new-format records — the real NeighbourID is a separate NBRS
        /// record field (Neighbour.NeighbourID), never written into the person-data
        /// shorts by the CAS writer (SimitoneNeighbourGenerator.MakePersonData
        /// leaves index 31 at its 0 default; only ImportAddNeighbor writes it, and
        /// runtime spawn also sets it via VMAvatar). So the NeighbourByID lookup
        /// below always misses (key 0 is never a live neighbour id, which start at
        /// 1), and this method ALWAYS takes the fallback. The fallback must
        /// therefore select the ORIGINAL 0x4 dialect — what both the shipped
        /// original saves and everything R252 writes carry — NOT the legacy 0xA
        /// dialect. Otherwise a light sim (disk 1) renders as Medium and a medium
        /// sim (disk 3) as Light, because a legacy-dialect read would interpret
        /// the original 1/2/3 disk values through the 0/1/2 map.
        /// </summary>
        private static int RecordVersionFor(NBRS neighbors, OBJMPerson person)
        {
            try
            {
                var neighborId = person.PersonData[(int)VMPersonDataVariable.NeighborId];
                if (neighbors != null && neighbors.NeighbourByID.TryGetValue(neighborId, out var nb))
                    return nb.Version;
            }
            catch { }
            // R252 P0: pd[31] is always 0 on original + new records (see the note
            // above), so this is effectively the only path taken and it MUST be the
            // ORIGINAL dialect 0x4, not the legacy 0xA.
            return 0x4;
        }

        public Blueprint LoadFromIff(IffFile iff)
        {
            StaleThreadContinuations.Clear();
            var content = Content.Content.Get();
            var simi = iff.Get<SIMI>(1);
            var hous = iff.Get<HOUS>(0);

            var neighbors = content.Neighborhood.Neighbors;

            var fsov = new VMMarshal();

            fsov.TS1 = true;
            fsov.GlobalState = simi.GlobalData.ToArray();

            Size = simi.GlobalData[23];
            var type = simi.GlobalData[35];

            fsov.GlobalState[20] = 255; //Game Edition. Basically, what "expansion packs" are running. Let's just say all of them.
            fsov.GlobalState[25] = 4; //as seen in EA-Land edith's simulator globals, this needs to be set for people to do their idle interactions.
            fsov.GlobalState[17] = 4; //Runtime Code Version, is this in EA-Land.

            var selectedPerson = fsov.GlobalState[3];

            //VM.SetGlobalValue(3, 0); //Selected Sim ID. Default to 0.
            //VM.SetGlobalValue(9, 0); //Active Family ID. Default to 0.

            //VM.SetGlobalValue(10, HouseNumber); //set house number
            //VM.SetGlobalValue(32, 0); //simless build mode
            //VM.SetGlobalValue(33, 2); //machine level

            // Init architecture and other context stuff

            FlipRoad = (hous.CameraDir & 1) > 0;

            var arch = ConvertArchitecture(iff, hous, Size);

            var clock = new VMClockMarshal
            {
                TicksPerMinute = 30,
                Hours = fsov.GlobalState[0],
                DayOfMonth = fsov.GlobalState[1],
                Minutes = fsov.GlobalState[5],
                Month = fsov.GlobalState[7],
                Year = fsov.GlobalState[8]
            };
            clock.MinuteFractions = fsov.GlobalState[6] * clock.TicksPerMinute;

            var context = new VMContextMarshal
            {
                Architecture = arch,
                Clock = clock,
                Ambience = new VMAmbientSoundMarshal(),
                RandomSeed = (ulong)((new Random()).NextDouble() * ulong.MaxValue)
            };

            fsov.Context = context;

            var sims1 = new VMTS1LotState();
            sims1.SimulationInfo = simi;
            sims1.CurrentHouse = HouseNumber;
            // vm is initialized at the end...

            fsov.PlatformState = sims1;

            // Load objects

            var objt = iff.Get<OBJT>(0);
            var objm = iff.Get<OBJM>(1);

            objm.Prepare((ushort typeID) =>
            {
                var entry = objt.Entries[typeID - 1];
                return new OBJMResource()
                {
                    OBJD = content.WorldObjects.Get(entry.GUID)?.OBJ,
                    OBJT = entry
                };
            });

            var objectCount = objm.ObjectData.Count;
            sims1.TutorialObjectID = objm.TutorialObjectID;

            var objects = new List<VMEntityMarshal>();
            var threads = new List<VMThreadMarshal>();
            var groups = new List<VMMultitileGroupMarshal>();
            var groupBuilders = new Dictionary<short, TS1MultitileBuilder>();

            var objsById = objm.ObjectData.Values.OrderBy(obj => obj.Instance.ObjectData[(int)VMStackObjectVariable.ObjectId]);

            foreach (var obj in objsById)
            {
                var inst = obj.Instance;

                if (inst.OBJD == null)
                {
                    if (inst.PersonData.HasValue)
                    {
                        var person = inst.PersonData.Value;
                        var neighborId = person.PersonData[(int)VMPersonDataVariable.NeighborId];

                        if (neighbors.NeighbourByID.TryGetValue(neighborId, out Neighbour neighbor) && inst.OBJT.Name == neighbor.Name)
                        {
                            // Last chance recovery - doesn't tend to succeed.
                            // The unleashed premade families tend to have the wrong GUID doe to having their save copied from another hood.
                            inst.OBJT.GUID = neighbor.GUID;
                        }
                        else
                        {
                            continue;
                        }
                    }
                    else
                    {
                        continue;
                    }
                }

                // SAV-11: a person whose Neighbour store record carries the death flag is
                // dead — re-activating the family must not resurrect them.
                if (inst.PersonData.HasValue && IsDeadStoreRecord(inst.PersonData.Value, neighbors)) continue;

                var master = inst.MultitileData.HasValue ? GetMasterOBJD(inst.OBJD) : null;

                VMEntityMarshal ent;

                if (inst.PersonData != null)
                {
                    var ava = ConvertAvatar(inst, neighbors);

                    ava.PlatformState = new VMTS1AvatarState();

                    ent = ava;
                }
                else
                {
                    var gobj = new VMGameObjectMarshal();

                    gobj.Direction = (Direction)(1 << inst.ObjectData[(int)VMStackObjectVariable.Direction]);
                    gobj.PlatformState = new VMTS1ObjectState();

                    ent = gobj;
                }

                ent.TS1 = true;
                ent.GUID = inst.OBJT.GUID;
                ent.MasterGUID = master == null ? 0 : master.GUID;

                ent.Position = inst.X == -16 && inst.Y == -16 ?
                    LotTilePos.OUT_OF_WORLD :
                    new LotTilePos((short)inst.X, (short)inst.Y, (sbyte)inst.Level);

                ent.Attributes = inst.Attributes;
                ent.MyList = new short[0];
                ent.ObjectData = inst.ObjectData;
                ent.ObjectID = inst.ObjectData[(int)VMStackObjectVariable.ObjectId];
                ent.PersistID = selectedPerson == ent.ObjectID ? 65537 : (uint)ent.ObjectID;
                ent.Container = inst.ObjectData[(int)VMStackObjectVariable.ContainerId];
                ent.ContainerSlot = inst.ObjectData[(int)VMStackObjectVariable.SlotNumber];

                ent.DynamicSpriteFlags = 0;
                ent.DynamicSpriteFlags2 = 0;

                for (int i = 0; i < inst.DynamicSpriteFlags.Length; i++)
                {
                    if (inst.DynamicSpriteFlags[i] != 0)
                    {
                        if (i < 64)
                        {
                            ent.DynamicSpriteFlags |= 1ul << i;
                        }
                        else
                        {
                            // TODO: some objects have more than 128 flags
                            ent.DynamicSpriteFlags2 |= 1ul << (i - 64);
                        }
                    }
                }

                ent.LightColor = Color.White;

                ent.Contained = inst.Slots.Select(x => x.ObjectID).ToArray();

                // Relationships

                ent.MeToObject = inst.Relationships.Select((rel) =>
                {
                    return new VMEntityRelationshipMarshal()
                    {
                        Target = (ushort)rel.TargetID,
                        Values = rel.Values.Select(x => (short)x).ToArray()
                    };
                }).ToArray();

                // FSO persist relationships are not used in TS1.
                ent.MeToPersist = new VMEntityPersistRelationshipMarshal[0];

                objects.Add(ent);
                threads.Add(ConvertThread(objm, objt, inst));

                if (inst.MultitileData == null)
                {
                    groups.Add(new VMMultitileGroupMarshal()
                    {
                        MultiTile = false,
                        Name = inst.OBJT.Name,
                        Objects = new short[] { ent.ObjectID },
                        Offsets = new LotTilePos[] { new LotTilePos() },
                        Price = inst.OBJD.Price,
                        SalePrice = -1,
                    });
                }
                else
                {
                    var mt = inst.MultitileData.Value;
                    short lead = mt.MultitileParentID == 0 ? ent.ObjectID : mt.MultitileParentID;
                    if (!groupBuilders.TryGetValue(lead, out TS1MultitileBuilder builder))
                    {
                        builder = new TS1MultitileBuilder()
                        {
                            Name = master.ChunkLabel,
                            Objects = new List<short>(),
                            Offsets = new List<LotTilePos>(),
                            Price = master.Price,
                        };

                        groupBuilders.Add(lead, builder);
                    }

                    var offset = LotTilePos.FromBigTile((short)mt.GroupX, (short)mt.GroupY, (sbyte)mt.GroupLevel);

                    if (lead == ent.ObjectID)
                    {
                        builder.Objects.Insert(0, ent.ObjectID);
                        builder.Offsets.Insert(0, offset);
                    }
                    else
                    {
                        builder.Objects.Add(ent.ObjectID);
                        builder.Offsets.Add(offset);
                    }
                }
            }

            foreach (var builder in groupBuilders.Values)
            {
                groups.Add(new VMMultitileGroupMarshal()
                {
                    MultiTile = true,
                    Name = builder.Name,
                    Objects = builder.Objects.ToArray(),
                    Offsets = builder.Offsets.ToArray(),
                    Price = builder.Price,
                    SalePrice = -1,
                });
            }

            fsov.Entities = objects.ToArray();
            fsov.Threads = threads.ToArray();
            fsov.MultitileGroups = groups.ToArray();

            VM.Load(fsov);
            VM.UpdateFreeObjectID();

            // Spawn controller objects that are missing from the saved lot.
            // In vanilla TS1, these are spawned automatically and saved into OBJM.
            // If the lot was never opened in vanilla, they won't be in the save,
            // so we need to spawn them here.
            var controllerObjects = content.WorldObjects.ControllerObjects.Select(x => (uint)x.ID).ToList();

            foreach (var controller in controllerObjects)
            {
                // Check if controller already exists in the loaded lot
                var exists = VM.Entities.Any(e => e.Object.OBJ.GUID == controller);
                if (!exists)
                {
                    // Spawn missing controller at OUT_OF_WORLD
                    var group = VM.Context.CreateObjectInstance(controller, LotTilePos.OUT_OF_WORLD, Direction.NORTH);
                    // TRV-04: a phone plugin registers in object category 1. Every shipped
                    // lot's plugin instances carry ObjectData[59]=1, and the phone's
                    // 'Call Plugin' check (PhoneGlobals 8308) enumerates category-SP0(=1)
                    // objects to build its menu, running each one's 'CT - Phone Plugin
                    // Menu' tree (global STR#303[67]), which adds the pie entry with
                    // Param0 = its object id. A controller is a phone plugin iff its
                    // resource carries that named tree — the marker the check itself
                    // invokes, so no GUID list or name heuristic is needed.
                    var plugin = group?.BaseObject;
                    var pluginResource = plugin?.Object?.Resource;
                    if (pluginResource?.TreeByName != null
                        && pluginResource.TreeByName.ContainsKey("CT - Phone Plugin Menu"))
                    {
                        plugin.SetValue(VMStackObjectVariable.Category, 1);
                    }
                }
            }

            // Match the recovery the interpreter previously reached only after throwing, but do
            // it deterministically before the first simulation tick. Controller creation stays
            // ahead of behavior recovery, matching the lifecycle that the old first-tick reset
            // observed. Reset entry points run only for rejected saved continuations.
            VM.TS1State.SanitizedThreadObjectIDs = StaleThreadContinuations.Keys.OrderBy(x => x).ToArray();
            foreach (var stale in StaleThreadContinuations.OrderBy(x => x.Key))
            {
                var entity = VM.GetObjectById(stale.Key);
                if (entity == null) continue;
                entity.Reset(VM.Context);
                if (entity is VMAvatar avatar) avatar.ClearMotiveChanges();
                Console.WriteLine("[TS1StackRepair] object=" + stale.Key
                    + " guid=" + entity.Object.GUID.ToString("x8")
                    + " invalid=" + stale.Value + " recovery=reset");
            }
            if (StaleThreadContinuations.Count > 0)
                Console.WriteLine("[TS1StackRepair] total=" + StaleThreadContinuations.Count);

            // Attempt to recover queue names.
            foreach (var ava in VM.Context.ObjectQueries.Avatars)
            {
                foreach (var action in ava.Thread.Queue)
                {
                    var ttas = action.Callee.TreeTableStrings;
                    //var ttab = action.Callee.TreeTable;

                    if (ttas != null)
                    {
                        action.Name = ttas.GetString(action.InteractionNumber);

                        // TODO: rerun check tree and match param 0 to restore changed action names if possible
                    }
                }
            }

            return VM.Context.Blueprint;
        }
    }
}
