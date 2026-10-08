using FSO.Files.Utils;
using System.Collections.Generic;
using System.IO;

namespace FSO.Files.Formats.IFF.Chunks
{
    /// <summary>
    /// Duplicate of STR chunk, instead used for simulator constants.
    /// </summary>
    public class FCNS : STR
    {
        //no difference!

        /// <summary>
        /// Structured (name, float) pairs for this table, filled during Read.
        /// The native FloatConstants resource (FCNS) is consumed by name, e.g. the
        /// twelve autonomy constants in Global.iff FCNS id 2 that override the
        /// compiled AutonomyConstants/MotiveConstants defaults at boot
        /// (AutonomyConstantsClient::UpdateConstants 0x112750, r249-freewill-cfg).
        /// </summary>
        public List<string> ConstantNames = new List<string>();
        public List<float> ConstantValues = new List<float>();

        public override void Read(IffFile iff, Stream stream)
        {
            using (var io = IoBuffer.FromStream(stream, ByteOrder.LITTLE_ENDIAN))
            {
                var zero = io.ReadInt32();
                var version = io.ReadInt32(); //2 in tso
                string magic = io.ReadCString(4); //NSCF
                var count = io.ReadInt32();

                ConstantNames.Clear();
                ConstantValues.Clear();
                LanguageSets[0].Strings = new STRItem[count];
                for (int i=0; i<count; i++)
                {
                    string name, desc;
                    float value;
                    if (version == 2)
                    {
                        name = io.ReadVariableLengthPascalString();
                        value = io.ReadFloat();
                        desc = io.ReadVariableLengthPascalString();
                    }
                    else
                    {
                        name = io.ReadNullTerminatedString();
                        if (name.Length % 2 == 0) io.ReadByte(); //padding to 2 byte align
                        value = io.ReadFloat();
                        desc = io.ReadNullTerminatedString();
                        if (desc.Length % 2 == 0) io.ReadByte(); //padding to 2 byte align
                    }

                    LanguageSets[0].Strings[i] = new STRItem()
                    {
                        Value = name + ": " + value,
                        Comment = desc
                    };
                    ConstantNames.Add(name);
                    ConstantValues.Add(value);
                }
            }
        }

        public override bool Write(IffFile iff, Stream stream)
        {
            return false;
        }
    }
}
