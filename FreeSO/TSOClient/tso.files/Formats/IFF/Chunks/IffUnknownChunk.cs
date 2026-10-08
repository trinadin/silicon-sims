using System.IO;

namespace FSO.Files.Formats.IFF.Chunks
{
    /// <summary>
    /// Generic handler for IFF chunk types the engine does not interpret but whose data the
    /// original game uses (POSI placement, XXXX family metadata, TMPL templates, pers/FAMh/
    /// CATS/Optn/rsmp). Without a registered handler IffFile.AddChunk SKIPS these entirely
    /// and their bytes are lost; registering them here at least preserves and enumerates the
    /// payload so downstream code (and IFF round-trips) can access it.
    /// </summary>
    public class IffUnknownChunk : IffChunk
    {
        public byte[] Data;

        public override void Read(IffFile iff, Stream stream)
        {
            var ms = stream as MemoryStream;
            if (ms != null) Data = ms.ToArray();
            else
            {
                using (var outMs = new MemoryStream())
                {
                    stream.CopyTo(outMs);
                    Data = outMs.ToArray();
                }
            }
        }

        public override bool Write(IffFile iff, Stream stream)
        {
            if (Data == null) return false;
            stream.Write(Data, 0, Data.Length);
            return true;
        }
    }
}
