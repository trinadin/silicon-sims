using FSO.Files.Utils;
using System.Collections.Generic;
using System.IO;

namespace FSO.Files.Formats.IFF.Chunks
{
    /// <summary>
    /// The exported family's character inventories ('FINV' 0x46494e56, native
    /// loader 0xb8950, version 2 — r247-fam-import decode §3): a positional list
    /// parallel to the EXPi member ids — entry i belongs to member i, and the
    /// import re-keys it onto the recreated character's new NeighbourID
    /// ("(s16)*el = nbrID" — decode §3 member loop). Each record is the same
    /// shape as the NGBH inventory tail (Hot Date afterthought, NGBH.cs):
    /// [marker:4 == 1][id:i16][itemCount:4][items: {i32 type, u32 guid, u16 count}].
    /// FORMAT CAVEAT (disclosed): no shipped UserData file carries a FINV chunk
    /// (Tutorial.FAM included), so the record law is reconstructed from the NGBH
    /// inventory serializer of the same save ecosystem; Read is bounds-checked
    /// and yields an empty list rather than throwing on a shape mismatch.
    /// ROUND-TRIP SCOPE (softened, r248 P2-6): Write re-serializes the parsed
    /// form with two RECONSTRUCTED values, not carried-through ones — the
    /// per-record id is invented positionally (i + 1; no shipped sample pins
    /// the native's exported NID law) and the version constant is pinned to the
    /// decode's 2. A Read/Write cycle is faithful only in record/item counts
    /// and item payloads, never in record ids.
    /// </summary>
    public class FINV : IffChunk
    {
        public uint Version;

        /// <summary>Positional inventories; index i = EXPi member i.</summary>
        public List<List<InventoryItem>> Inventories = new List<List<InventoryItem>>();

        public override void Read(IffFile iff, Stream stream)
        {
            using (var io = IoBuffer.FromStream(stream, ByteOrder.LITTLE_ENDIAN))
            {
                io.ReadUInt32(); //pad
                Version = io.ReadUInt32();
                string magic = io.ReadCString(4); //VNIF

                var count = io.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    if (!io.HasBytes(6)) break;
                    if (io.ReadInt32() != 1) { } //record marker, as NGBH
                    io.ReadInt16(); //the exported member's NID (re-keyed on import)
                    var itemCount = io.ReadInt32();
                    var items = new List<InventoryItem>();
                    for (int j = 0; j < itemCount; j++)
                    {
                        if (!io.HasBytes(10)) break;
                        items.Add(new InventoryItem(io));
                    }
                    Inventories.Add(items);
                }
            }
        }

        public override bool Write(IffFile iff, Stream stream)
        {
            using (var io = IoWriter.FromStream(stream, ByteOrder.LITTLE_ENDIAN))
            {
                io.WriteInt32(0);
                io.WriteUInt32(2);
                io.WriteCString("VNIF", 4);

                io.WriteInt32(Inventories.Count);
                for (int i = 0; i < Inventories.Count; i++)
                {
                    io.WriteInt32(1);
                    io.WriteInt16((short)(i + 1)); //positional id; re-keyed by the importer
                    io.WriteInt32(Inventories[i].Count);
                    foreach (var item in Inventories[i])
                    {
                        item.SerializeInto(io);
                    }
                }
            }
            return true;
        }
    }
}
