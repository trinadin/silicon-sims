using System;
using System.IO;

namespace FSO.Files.HIT
{
    /// <summary>
    /// TRK is a CSV format that defines a HIT track.
    /// </summary>
    public class Track
    {
        private bool TWODKT = false; //Optional encoding as Pascal string, typical Maxis...
        public string MagicNumber;
        public uint Version;
        public string TrackName;
        public uint SoundID;
        public uint TrackID;
        public HITArgs ArgType;
        public HITControlGroups ControlGroup;
        public HITDuckingPriorities DuckingPriority;
        public uint Looped;
        public uint Volume;

        public bool LoopDefined = false;

        //ts1
        public uint SubroutineID;
        public uint HitlistID;

        /// <summary>
        /// AUD-21: the [Track] kSpl / sound-pressure-level column (column 7 of
        /// the .hot [Track] CSV; the native sound-object register 0x39 set at
        /// track load — cHitIniFile::IniFileCallbackTrack FLAT 0x3017F0, decode
        /// receipt evidence/AUD-20/footstep-residuals-decode.md §(c)). Parsed by
        /// Hot.cs via ParseEME so equate labels resolve (kSplNormal=20,
        /// kSplInfinite=0, kSplLoud=10, kSplStereo=10001/0x2711, kSplQuiet=100)
        /// and unresolved symbols (kSplNone, the kSplnfinite typo) read 0.
        /// Default 0x14 = the native default attenuation for sounds with no
        /// track (cBoxX::GetInstanceVolPan FLAT 0x2E1E80: att=0x14 unless a
        /// sound object supplies register 0x39). The TRK binary layout's
        /// matching column is unidentified, so TRK-loaded (TSO) tracks keep the
        /// default — the TSO mix path is unchanged.
        /// </summary>
        public uint kSpl = 0x14;

        /// <summary>
        /// Creates a new track.
        /// </summary>
        /// <param name="Filedata">The data to create the track from.</param>
        public Track(byte[] Filedata)
        {
            BinaryReader Reader = new BinaryReader(new MemoryStream(Filedata));

            MagicNumber = new string(Reader.ReadChars(4));

            if(MagicNumber == "2DKT")
                TWODKT = true;

            int CurrentVal = 8;
            string data;

            if(!TWODKT)
                data = new string(Reader.ReadChars(Filedata.Length));
            else
                data = new string(Reader.ReadChars(Reader.ReadInt32()));
            string[] Values = data.Split(',');

            //MagicNumber = Values[0];
            Version = ParseHexString(Values[1]);
            TrackName = Values[2];
            SoundID = ParseHexString(Values[3]);
            TrackID = ParseHexString(Values[4]);
            if (Values[5] != "\r\n" && Values[5] != "ETKD" && Values[5] != "") //some tracks terminate here...
            {
                ArgType = (HITArgs)ParseHexString(Values[5]);
                ControlGroup = (HITControlGroups)ParseHexString(Values[7]);

                if (Version == 2)
                    CurrentVal++;

                CurrentVal += 3; //skip two unknowns and clsid

                DuckingPriority = (HITDuckingPriorities)ParseHexString(Values[CurrentVal]);
                CurrentVal++;
                Looped = ParseHexString(Values[CurrentVal]);
                LoopDefined = true;
                CurrentVal++;
                Volume = ParseHexString(Values[CurrentVal]);
            }

            Reader.Close();
        }

        public Track() { }

        private uint ParseHexString(string input)
        {
            bool IsHex = false;

            if (input == "") return 0;
            if (input.StartsWith("0x"))
            {
                input = input.Substring(2);
                IsHex = true;
            }
            else if (input.Contains("a") || input.Contains("b") || input.Contains("c") || input.Contains("d") || input.Contains("e") || input.Contains("f"))
            {
                IsHex = true;
            }

            if (IsHex)
            {
                return Convert.ToUInt32(input, 16);
            }
            else
            {
                try
                {
                    return Convert.ToUInt32(input);
                }
                catch (Exception)
                {
                    return 0;
                }
            }
        }
    }
}
