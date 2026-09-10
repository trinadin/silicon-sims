using FSO.Files.Utils;
using System.IO;

namespace FSO.Files.Formats.IFF.Chunks
{
    /// <summary>
    /// The exported-family index ('EXPi', native ExpFamilyInfo via ReconLoadObject,
    /// 0xb8250/0xb7490 — r247-fam-import decode §0/§3): famId + the 0-terminated
    /// list of up to 8 member uChr/NBRS ids carried by an exported .FAM file.
    /// File layout (verified against UserData/Tutorial.FAM EXPi id 0, 31-byte
    /// payload): [pad:4][version:u32]["iPXE":4][famId:i16][ids: i16 x 8]
    /// [flag byte:1] — all fields little-endian. The trailing flag byte is the
    /// exporting game's ExpFamilyInfo flag (an echo of native app+0x64, used by
    /// CycleThroughImports for cross-version compatibility).
    /// ROUND-TRIP SCOPE (softened, r248 P2-6): Write re-serializes the PARSED
    /// form only — the pad word is re-zeroed and the version constant is pinned
    /// to the Tutorial.FAM sample (0x3F), so a foreign file's version/pad bits
    /// are NOT preserved; the flag byte and famId/member ids are the only
    /// fields carried through verbatim. Tutorial.FAM decodes as famId 1, ids
    /// [48, 47, 0 x 6], flag 1 (skeptic-corrections.md §3).
    /// </summary>
    public class EXPi : IffChunk
    {
        public uint Version;

        /// <summary>The exported family's requested id (native efi +0x02, s16).</summary>
        public short FamilyID;

        /// <summary>Member uChr/NBRS ids, 0-terminated (native efi +0x04, s16[8]).</summary>
        public short[] MemberIDs = new short[8];

        /// <summary>The exporting game's flag byte (native efi +0x00); raw retention.</summary>
        public byte FlagByte;

        /// <summary>The 0-terminated prefix of MemberIDs, in order.</summary>
        public short[] ActiveMemberIDs
        {
            get
            {
                var count = 0;
                while (count < MemberIDs.Length && MemberIDs[count] != 0) count++;
                var result = new short[count];
                Array.Copy(MemberIDs, result, count);
                return result;
            }
        }

        public override void Read(IffFile iff, Stream stream)
        {
            using (var io = IoBuffer.FromStream(stream, ByteOrder.LITTLE_ENDIAN))
            {
                io.ReadUInt32(); //pad
                Version = io.ReadUInt32();
                string magic = io.ReadCString(4); //iPXE

                FamilyID = io.ReadInt16();
                for (int i = 0; i < 8; i++) MemberIDs[i] = io.ReadInt16();
                FlagByte = io.HasBytes(1) ? io.ReadByte() : (byte)0;
            }
        }

        public override bool Write(IffFile iff, Stream stream)
        {
            using (var io = IoWriter.FromStream(stream, ByteOrder.LITTLE_ENDIAN))
            {
                io.WriteInt32(0);
                io.WriteUInt32(0x3F);
                io.WriteCString("iPXE", 4);
                io.WriteInt16(FamilyID);
                for (int i = 0; i < 8; i++) io.WriteInt16(MemberIDs[i]);
                io.WriteByte(FlagByte);
            }
            return true;
        }
    }
}
