using FSO.Files.Utils;
using System.IO;

namespace FSO.Files.Formats.IFF.Chunks
{
    /// <summary>
    /// This class defines a single family in the neighbourhood, and various properties such as their 
    /// budget and house assignment. These can be modified using GenericTS1Calls, but are mainly
    /// defined within CAS.
    /// </summary>
    public class FAMI : IffChunk
    {
        public uint Version = 0x7;

        public int HouseNumber;
        //this is not a typical family number - it is unique between user created families, but -1 for townies.
        //i believe it is an alternate family UID that basically runs on an auto increment to obtain its value.
        //(in comparison with the ChunkID as family that is used ingame, which appears to fill spaces as they are left)
        public int FamilyNumber;
        public int Budget;
        public int ValueInArch;
        public int FamilyFriends;
        public int Unknown; //19, 17 or 1? could be flags, (1, 16, 2) ... 0 for townies. 24 for CAS created (new 16+8?)
                            //1: in house
                            //2: unknown, but is set sometimes
                            //4: unknown
                            //8: user created?
                            //16: in cas

        public uint[] FamilyGUIDs = new uint[] { };

        /// <summary>
        /// ENG-12: the family's persisted spell block — six 16-bit learned-spell
        /// bitmaps, the port equivalent of native Family::DoStream field 9
        /// (ENG-08 familyspells-decode.md §2: on save the six words are refreshed
        /// from the live magic controller by max-merge, then streamed; on load
        /// they are read back and the family->332 "persisted block" latch is
        /// set). null = no block / latch 0 (every family without learned spells —
        /// this keeps the R252 original-format canon byte-identical: the native
        /// streams field 9 unconditionally, but an all-zero block plus latch is
        /// informationally the no-op the port encodes as absent).
        /// Serialized as six trailing LE shorts AFTER the GUID list on both the
        /// Neighborhood.iff chunk stream and the shared lot-marshal stream
        /// (SAV-10: the marshal's post-FAMI tail is 5 bytes when the block is
        /// absent, so a "read iff >=12 bytes remain" reader never over-reads).
        /// </summary>
        /// <summary>
        /// TRV-05 (native Family::DoStream 0x76d710, field 8): the family's
        /// current Vacation Island rental (lot 40..48) while the family is on
        /// vacation — the port equivalent of native Family+0x13C. Decoded law:
        /// written at travel arrival (cSimsApp::LoadGame 0x1024f7f0,
        /// app-trip-flag 0x109 path: family->0x13c = lot, then an IMMEDIATE
        /// Neighborhood::Save); cleared on return
        /// (Neighborhood::RemoveFromVacation 0xa83e0: +0x13c = 0 plus flag bit
        /// 0x8 of +0x138, then Neighborhood::Save); consumed by GenericTS1Call
        /// mode 27's availability loop (TRV-01: families whose +0x13c equals
        /// the rental make it unavailable); persisted as ONE int AFTER the
        /// GUID list and BEFORE the spell block, gated version > 7 (the
        /// shipped original FAMIs are version 7 — never re-saved by the
        /// Complete engine — so the field is absent there; the native writer
        /// passes version 9 via Family::SaveFamily -> ReconSaveObject).
        /// 0 = no vacation booking.
        /// </summary>
        public int VacationHouseNumber;

        public short[] SpellWords = null;

        public uint[] RuntimeSubset = new uint[] { }; //the members of this family currently active. don't save!

        public void SelectWholeFamily()
        {
            RuntimeSubset = FamilyGUIDs;
        }

        public void SelectOneMember(uint guid)
        {
            RuntimeSubset = new uint[] { guid };
        }

        /// <summary>
        /// Reads a FAMI chunk from a stream.
        /// </summary>
        /// <param name="iff">An Iff instance.</param>
        /// <param name="stream">A Stream object holding a OBJf chunk.</param>
        public override void Read(IffFile iff, Stream stream)
        {
            using (var io = IoBuffer.FromStream(stream, ByteOrder.LITTLE_ENDIAN))
            {
                io.ReadUInt32(); //pad
                Version = io.ReadUInt32(); //0x9 for latest game
                string magic = io.ReadCString(4); //IMAF

                HouseNumber = io.ReadInt32();
                FamilyNumber = io.ReadInt32();
                Budget = io.ReadInt32();
                ValueInArch = io.ReadInt32();
                FamilyFriends = io.ReadInt32();
                Unknown = io.ReadInt32();
                FamilyGUIDs = new uint[io.ReadInt32()];
                for (int i=0; i<FamilyGUIDs.Length; i++)
                {
                    FamilyGUIDs[i] = io.ReadUInt32();
                }
                // no trailing ints: the original FAMI chunk ends after the GUID list
                // (R252), and this reader also runs on shared lot-marshal streams where
                // an over-read consumes the following payload (SAV-10).

                // TRV-05 vacation-rental field (native DoStream field 8):
                // exactly one int between the GUID list and any spell block,
                // read only for version > 7 (originals are version 7; the
                // shared lot-marshal stream is symmetric because the writer
                // emits the int under the same version gate).
                if (Version > 7) VacationHouseNumber = io.ReadInt32();
                else VacationHouseNumber = 0;

                // ENG-12 spell block: read iff the stream is EXHAUSTED by exactly
                // the 6 shorts. An IFF chunk stream is chunk-sized (block present
                // -> exactly 12 remain; absent -> 0), so this is exact there.
                // The >= 12 form of this gate was a P1 (caught in self-check
                // before review): the lot-marshal stream (VMTS1LotState) is SHARED
                // and its post-FAMI tail is ~76 bytes (TutorialObjectID + the
                // Version-40 budget block) — >= 12 would consume tail bytes as a
                // phantom block and desync every spell-less family save load (the
                // SAV-10 corruption class). The marshal now additionally writes
                // its FAMI copy BLOCK-LESS (see VMTS1LotState.SerializeInto), so
                // this reader never sees a block on that stream at all.
                if (stream.Length - stream.Position == 12)
                {
                    SpellWords = new short[6];
                    for (int i = 0; i < 6; i++) SpellWords[i] = io.ReadInt16();
                }
                else SpellWords = null;
            }
        }

        public override bool Write(IffFile iff, Stream stream)
        {
            using (var io = IoWriter.FromStream(stream, ByteOrder.LITTLE_ENDIAN))
            {
                // TRV-05 vacation-rental field: when the family holds a
                // booking, the chunk is promoted to the native Complete
                // writer's version 9 shape — [GUIDs][vacation int][spell
                // block] — so the booking persists exactly like native
                // Family::DoStream field 8. DISCLOSED divergence: the native
                // writer passes version 9 unconditionally (Family::SaveFamily
                // 0x1006d4e0 -> ReconSaveObject(...,9)), so a native v9 chunk
                // also carries the 6 spell shorts when spell-less; the port
                // keeps bookless/spellless families byte-identical to the
                // R252/ENG-12 canon (version 7, no trailing fields) and only
                // promotes the version when there is a vacation booking to
                // persist. Both readers honour the chunk's own version word.
                if (VacationHouseNumber != 0)
                {
                    Version = 9;
                }
                io.WriteInt32(0);
                io.WriteUInt32(Version); // honour the loaded version (7 original; a 9 legacy port FAMI round-trips as 9); was hardcoded 9 (R252)
                io.WriteCString("IMAF", 4);
                io.WriteInt32(HouseNumber);
                io.WriteInt32(FamilyNumber);
                io.WriteInt32(Budget);
                io.WriteInt32(ValueInArch);
                io.WriteInt32(FamilyFriends);
                io.WriteInt32(Unknown);
                io.WriteInt32(FamilyGUIDs.Length);
                foreach (var guid in FamilyGUIDs)
                    io.WriteUInt32(guid);
                if (Version > 7) io.WriteInt32(VacationHouseNumber);

                // R252: the original FAMI chunk has NO trailing zero int32s (data
                // size is exactly 40 + 4*guidCount). The port previously wrote 4
                // trailing zero int32s (16 bytes); that has been removed.

                // ENG-12 spell block: written ONLY when the family has one (see
                // SpellWords) — spell-less families stay byte-identical to the R252
                // canon. Mirrors the reader's "== 12 bytes remain" law (review note:
                // the gate is EXACT — see the Read-side comment for why).
                if (SpellWords != null && SpellWords.Length == 6)
                {
                    for (int i = 0; i < 6; i++) io.WriteInt16(SpellWords[i]);
                }
            }
            return true;
        }
    }
}