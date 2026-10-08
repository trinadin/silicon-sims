using FSO.Files.Utils;
using System.Collections.Generic;
using System.IO;

namespace FSO.Files.Formats.IFF.Chunks
{
    /// <summary>
    /// The GUID translation table ('Gtab' 0x47746162, native GUIDTranslation
    /// Load 0x8a040 / Save 0x89fd0 — r247-fam-import decode §0/§3): the old→new
    /// GUID pairs recorded when an exported family is re-imported (one pair per
    /// recreated member, added via 0x8ae50; "12-byte records"). The neighborhood
    /// import loads it from the .FAM, appends a pair per member and saves it back
    /// into the FAM before the file is moved over Houses/HouseNN.iff.
    /// FORMAT CAVEAT (disclosed): no shipped file carries a Gtab chunk, so the
    /// exact serialization is reconstructed — [pad:4][version:4]["batG":4]
    /// [count:4][records: {u32 oldGUID, u32 newGUID, u32 reserved}]. The 12-byte
    /// record size is instruction-pinned by the decode; the trailing reserved
    /// word is retained raw so a future sample can be diffed. Tutorial.FAM has no
    /// Gtab chunk, so the tutorial import persists an empty (count 0) table.
    /// ROUND-TRIP SCOPE (softened, r248 P2-6): nothing here is sample-pinned —
    /// the header version is a hardcoded constant (Write emits 1, Read stores
    /// whatever it sees) and the record shape itself is the reconstruction;
    /// only the parsed pair list is carried through Write verbatim.
    /// No live port consumer reads Gtab back yet (relationship rows live in NBRS
    /// records); the chunk exists to keep the moved FAM-as-house file faithful.
    /// </summary>
    public class Gtab : IffChunk
    {
        public uint Version;

        public List<GtabPair> Pairs = new List<GtabPair>();

        public override void Read(IffFile iff, Stream stream)
        {
            using (var io = IoBuffer.FromStream(stream, ByteOrder.LITTLE_ENDIAN))
            {
                io.ReadUInt32(); //pad
                Version = io.ReadUInt32();
                string magic = io.ReadCString(4); //batG

                var count = io.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    if (!io.HasBytes(12)) break;
                    Pairs.Add(new GtabPair()
                    {
                        OldGUID = io.ReadUInt32(),
                        NewGUID = io.ReadUInt32(),
                        Reserved = io.ReadUInt32(),
                    });
                }
            }
        }

        public override bool Write(IffFile iff, Stream stream)
        {
            using (var io = IoWriter.FromStream(stream, ByteOrder.LITTLE_ENDIAN))
            {
                io.WriteInt32(0);
                io.WriteUInt32(1);
                io.WriteCString("batG", 4);

                io.WriteInt32(Pairs.Count);
                foreach (var pair in Pairs)
                {
                    io.WriteUInt32(pair.OldGUID);
                    io.WriteUInt32(pair.NewGUID);
                    io.WriteUInt32(pair.Reserved);
                }
            }
            return true;
        }
    }

    public class GtabPair
    {
        public uint OldGUID;
        public uint NewGUID;
        public uint Reserved;
    }
}
