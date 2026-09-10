using FSO.Files.Utils;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FSO.Files.Formats.IFF.Chunks
{
    /// <summary>
    /// R252 original created-record format constants. The port previously wrote a
    /// divergent format (NBRS chunk-data version 0x49, record Version 0xA with a
    /// 256-short PersonData block, PersonMode 9, Name "iffname", skin 0/1/2).
    /// The original Maxis TS1 format uses: chunk-data version 0x3E (0x3F for the
    /// UserData2..8 templates), record Version 0x4 (80-short PersonData), and
    /// PersonMode 5. The skin encoding (lgt=1, drk=2, med=3) is non-monotonic and
    /// lives in SimitoneNeighbourGenerator/VMTS1ActivatorNew (the logical 0/1/2
    /// AppearanceType <-> disk 1/3/2 remap), not here.
    /// </summary>
    public static class NbrsFormat
    {
        public const uint CHUNK_VERSION  = 0x3E; // NBRS chunk-data Version (was 0x49)
        public const int  RECORD_VERSION = 0x4;  // per-record Version (was 0xA)
        public const int  PERSON_MODE    = 5;    // person-data-carrying mode (was 9)
    }

    /// <summary>
    /// This chunk defines all neighbours in a neighbourhood. 
    /// A neighbour is a specific version of a sim object with associated relationships and person data. (skills, person type)
    /// 
    /// These can be read within SimAntics without the avatar actually present. This is used to find and spawn suitable sims on 
    /// ped portals as visitors, and also drive phone calls to other sims in the neighbourhood.
    /// When neighbours are spawned, they assume the attributes saved here. A TS1 global call allows the game to save these attributes.
    /// </summary>
    public class NBRS : IffChunk
    {
        public List<Neighbour> Entries = new List<Neighbour>();
        public Dictionary<short, Neighbour> NeighbourByID = new Dictionary<short, Neighbour>();
        public Dictionary<uint, short> DefaultNeighbourByGUID = new Dictionary<uint, short>();

        public uint Version;

        /// <summary>
        /// Reads a NBRS chunk from a stream.
        /// </summary>
        /// <param name="iff">An Iff instance.</param>
        /// <param name="stream">A Stream object holding a NBRS chunk.</param>
        public override void Read(IffFile iff, Stream stream)
        {
            using (var io = IoBuffer.FromStream(stream, ByteOrder.LITTLE_ENDIAN))
            {
                io.ReadUInt32(); //pad
                Version = io.ReadUInt32(); //0x49 for latest game
                string magic = io.ReadCString(4); //SRBN
                var count = io.ReadUInt32();

                for (int i=0; i<count; i++)
                {
                    if (!io.HasMore) break;
                    var neigh = new Neighbour(io);
                    Entries.Add(neigh);
                    if (neigh.Unknown1 > 0)
                    {
                        NeighbourByID.Add(neigh.NeighbourID, neigh);
                        DefaultNeighbourByGUID[neigh.GUID] = neigh.NeighbourID;
                    }
                }
            }
            Entries = Entries.OrderBy(x => x.NeighbourID).ToList();
            foreach (var entry in Entries)
                entry.RuntimeIndex = Entries.IndexOf(entry);
        }

        /// <summary>
        /// Writes a NBRS chunk to a stream.
        /// </summary>
        /// <param name="iff">An Iff instance.</param>
        /// <param name="stream">A destination stream.</param>
        public override bool Write(IffFile iff, Stream stream)
        {
            using (var io = IoWriter.FromStream(stream, ByteOrder.LITTLE_ENDIAN))
            {
                io.WriteUInt32(0);
                io.WriteUInt32(NbrsFormat.CHUNK_VERSION); // 0x3E original; was 0x49 (R252)
                io.WriteCString("SRBN", 4);
                // R253 (integration-audit P1): the header count MUST equal the
                // number of records actually serialized. The previous code wrote
                // Entries.Count (which includes placeholders/malformed decodes
                // that live only in Entries) but iterated NeighbourByID (the
                // validly decoded, id-keyed records) — a native reader desynced
                // on the tail. Placeholders are runtime-only and are not
                // persisted; the count now matches the emitted live records.
                io.WriteInt32(NeighbourByID.Count);
                foreach (var n in NeighbourByID.Values)
                {
                    n.Save(io);
                }
            }
            return true;
        }

        public void AddNeighbor(Neighbour nb) {
            Entries.Add(nb);
            Entries = Entries.OrderBy(x => x.NeighbourID).ToList();
            foreach (var entry in Entries)
                entry.RuntimeIndex = Entries.IndexOf(entry);

            NeighbourByID.Add(nb.NeighbourID, nb);
            DefaultNeighbourByGUID[nb.GUID] = nb.NeighbourID;
        }

        /// <summary>
        /// Removes a neighbour record and every lookup it participates in. The
        /// native DeleteCharacter sweep of the neighborhood import (r247-fam-import
        /// decode §3, 0xb2510) removes the record from the live Neighbour array;
        /// this is the same invalidation for the NBRS chunk model. Does nothing
        /// when the record is absent.
        /// </summary>
        public void RemoveNeighbor(Neighbour nb) {
            if (nb == null) return;
            Entries.Remove(nb);
            Entries = Entries.OrderBy(x => x.NeighbourID).ToList();
            foreach (var entry in Entries)
                entry.RuntimeIndex = Entries.IndexOf(entry);

            if (NeighbourByID.TryGetValue(nb.NeighbourID, out var byId) && byId == nb)
                NeighbourByID.Remove(nb.NeighbourID);
            if (DefaultNeighbourByGUID.TryGetValue(nb.GUID, out var byGuid) && byGuid == nb.NeighbourID)
                DefaultNeighbourByGUID.Remove(nb.GUID);
        }

        public short GetFreeID()
        {
            //find the lowest id that is free
            short newID = 1;
            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].NeighbourID == newID) newID++;
                else if (Entries[i].NeighbourID < newID) continue;
                else break;
            }
            return newID;
        }
    }

    public class Neighbour
    {
        public int Unknown1 = 1; //1
        public int Version = 0xA; //0x4, 0xA
        //if 0xA, unknown3 follows
        //0x4 indicates person data size of 0xa0.. (160 bytes, or 80 entries)
        public int Unknown3 = 9; //9
        public string Name;
        public int MysteryZero = 0;
        public int PersonMode; //0/5/9
        public short[] PersonData; //can be null
        // R252: the ACTUAL on-disk PersonData byte span this record consumed
        // (measured from the stream position delta during read, NOT re-derived
        // from Version). Exposed so the recordfmt raw scan can assert the written
        // record is genuinely 0xa0 bytes rather than trusting Version==0x4.
        public int PersonDataBytes;

        // R253 (integration-audit P1): opaque legacy person-data tail beyond the
        // 88-short runtime view. Version 0xA records carry 256 shorts on disk but
        // the VM/runtime PersonData view is 88 shorts; the remaining bytes were
        // read and discarded, then re-written as zeros — erasing opaque data on
        // any read+rewrite (e.g. legacy saves round-tripping through the port).
        // This tail is retained and re-emitted verbatim WITHOUT changing the
        // runtime interpretation of PersonData. Null for Version 0x4 records
        // (which carry exactly 0xa0 bytes = 80 shorts, all within the view).
        public short[] LegacyPersonDataTail;

        public short NeighbourID;
        public uint GUID;
        public int UnknownNegOne = -1; //negative 1 usually

        public Dictionary<int, List<short>> Relationships;

        public int RuntimeIndex; //used for fast continuation of Set to Next

        public Neighbour() { }

        public Neighbour(IoBuffer io)
        {
            Unknown1 = io.ReadInt32();
            if (Unknown1 != 1) { return; }
            Version = io.ReadInt32();
            if (Version == 0xA)
            {
                //TODO: what version does this truly start?
                Unknown3 = io.ReadInt32();
                if (Unknown3 != 9) { }
            }
            Name = io.ReadNullTerminatedString();
            if (Name.Length % 2 == 0) io.ReadByte();
            MysteryZero = io.ReadInt32();
            if (MysteryZero != 0) { }
            PersonMode = io.ReadInt32();
            if (PersonMode > 0)
            {
                var size = (Version == 0x4) ? 0xa0 : 0x200;
                var pdStart = io.Position; // R252: measure the real byte span
                PersonData = new short[88];
                var totalShorts = size / 2;
                int pdi = 0;
                if (totalShorts > 88)
                    LegacyPersonDataTail = new short[totalShorts - 88];
                for (int i=0; i<totalShorts; i++)
                {
                    if (pdi < 88)
                    {
                        // runtime view: first 88 shorts
                        PersonData[pdi++] = io.ReadInt16();
                    }
                    else
                    {
                        // opaque legacy tail retained for round-trip (R253 P1)
                        LegacyPersonDataTail[i - 88] = io.ReadInt16();
                    }
                }
                PersonDataBytes = (int)(io.Position - pdStart);
            }

            NeighbourID = io.ReadInt16();
            GUID = io.ReadUInt32();
            UnknownNegOne = io.ReadInt32();
            if (UnknownNegOne != -1) { }

            var entries = io.ReadInt32();
            Relationships = new Dictionary<int, List<short>>();
            for (int i=0; i<entries; i++)
            {
                var keyCount = io.ReadInt32();
                if (keyCount != 1) { }
                var key = io.ReadInt32();
                var values = new List<short>();
                var valueCount = io.ReadInt32();
                for (int j=0; j<valueCount; j++)
                {
                    values.Add((short)io.ReadInt32());
                }
                Relationships.Add(key, values);
            }
        }

        public override string ToString()
        {
            return Name;
        }

        public void Save(IoWriter io)
        {
            io.WriteInt32(Unknown1);
            io.WriteInt32(Version);
            if (Version == 0xA) io.WriteInt32(Unknown3);
            io.WriteNullTerminatedString(Name);
            if (Name.Length % 2 == 0) io.WriteByte(0);
            io.WriteInt32(MysteryZero);
            io.WriteInt32(PersonMode);
            if (PersonMode > 0)
            {
                var size = (Version == 0x4) ? 0xa0 : 0x200;
                int pdi = 0;
                var totalShorts = size / 2;
                for (int i = 0; i < totalShorts; i++)
                {
                    if (pdi < 88)
                    {
                        io.WriteInt16(PersonData[pdi++]);
                    }
                    else if (LegacyPersonDataTail != null && (i - 88) < LegacyPersonDataTail.Length)
                    {
                        // R253 (integration-audit P1): re-emit the retained opaque
                        // legacy tail verbatim instead of zero-filling it.
                        io.WriteInt16(LegacyPersonDataTail[i - 88]);
                    }
                    else
                    {
                        io.WriteInt16(0); // no tail captured; legacy zero-fill
                    }
                }
            }

            io.WriteInt16(NeighbourID);
            io.WriteUInt32(GUID);
            io.WriteInt32(UnknownNegOne);

            io.WriteInt32(Relationships.Count);
            foreach (var rel in Relationships)
            {
                io.WriteInt32(1); //keycount (1)
                io.WriteInt32(rel.Key);
                io.WriteInt32(rel.Value.Count);
                foreach (var val in rel.Value)
                {
                    io.WriteInt32(val);
                }
            }
        }
    }
}
