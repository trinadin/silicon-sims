using FSO.Common;
using System;
using System.Collections.Generic;
using System.IO;

namespace FSO.Client
{
    public class GlobalSettings : IniConfig
    {
        public override string HeadingComment => "FreeSO Settings File. Properties are self explanatory.";
        private static GlobalSettings defaultInstance;

        public static GlobalSettings Default
        {
            get
            {
                if (defaultInstance == null)
                {
                    defaultInstance = new GlobalSettings(Path.Combine(FSOEnvironment.UserDir, "config.ini"));
                    if (defaultInstance.DPIScaleFactor > 4 || defaultInstance.DPIScaleFactor == 0)
                        defaultInstance.DPIScaleFactor = 1; //sanity check
                    if (defaultInstance.ChatWindowsOpacity == 0 || defaultInstance.ChatWindowsOpacity > 1)
                        defaultInstance.ChatWindowsOpacity = 1; //sanity check
                    if (defaultInstance.GameEntryUrl == "http://api.freeso.org")
                    {
                        defaultInstance.GameEntryUrl = "https://api.freeso.org";
                        defaultInstance.CitySelectorUrl = "https://api.freeso.org";
                    }

                    if (defaultInstance.ArchiveClientGUID == "")
                    {
                        defaultInstance.ArchiveClientGUID = GenerateGUID();
                    }

                    if (defaultInstance.ArchiveServerGUID == "")
                    {
                        defaultInstance.ArchiveServerGUID = GenerateGUID();
                    }
                }
                return defaultInstance;
            }
        }

        private static string GenerateGUID()
        {
            return Guid.NewGuid().ToString();
        }


        public GlobalSettings(string path) : base(path) { }

        private Dictionary<string, string> _DefaultValues = new Dictionary<string, string>()
        {
            { "ShowHints", "true"},
            { "CurrentLang", "english" },
            { "ClientVersion", "0"},
            { "DebugEnabled", "false"},
            { "ScaleUI", "false"},
            { "CityShadows", "false"},
            { "ShadowQuality", "2048"},
            { "SmoothZoom", "true"},
            { "AntiAlias", "0"},
            { "EdgeScroll", "true"},
            { "Lighting", "true"},
            { "FXVolume", "10"},
            { "MusicVolume", "10"},
            { "VoxVolume", "10"},
            { "AmbienceVolume", "1"},
            { "StartupPath", ""},
            { "DocumentsPath", ""},
            { "Windowed", "true"},
            { "GraphicsWidth", "1024"},
            { "GraphicsHeight", "768"},
            { "LastUser", ""},
            { "SkipIntro", "true"},
            { "DebugHead", "0"},
            { "DebugBody", "0"},
            { "DebugGender", "true"},
            { "DebugSkin", "0"},
            { "LanguageCode", "1"},
            { "SurroundingLotMode", "2" },

            { "UseCustomServer", "true" },
            { "GameEntryUrl", "http://api.freeso.org" },
            { "CitySelectorUrl", "http://api.freeso.org" },

            { "TargetRefreshRate", "60" },

            { "TTSMode", "1" }, //disable/allow/force
            { "CompatState", "-1" },

            { "TS1HybridPath", "D:/Games/The Sims/" },
            { "TS1HybridEnable", "false" },
            { "TS1IsSteamInstall", "false" },
            { "TS1InstallationConfigured", "false" },

            { "Shadows3D", "false" },
            { "CitySkybox", "true" },

            { "LightingMode", "-1" },
            { "Weather", "true" },
            { "DirectionalLight3D", "true" },
            { "DPIScaleFactor", "1" },
            { "TexCompression", "0" },

            { "ChatColor", "0" }, //uint packed color. 0 means choose random
            { "ChatTTSPitch", "0" }, //-100 to 100
            { "ChatOnlyEmoji", "false" },
            { "ChatShowTimestamp", "false" },
            { "ChatSizeX", "400" },
            { "ChatSizeY", "255" },
            {"ChatLocationX", "20" },
            {"ChatLocationY", "20" },
            {"ChatDeltaScale", "8" },
            { "ChatWindowsOpacity", "0.8" },

            { "ComplexShaders", "false" },
            { "GlobalGraphicsMode", "0" }, //2d, 2d hybrid, 3d
            { "EnableTransitions", "true" },

            { "ArchiveServerGUID", "" },
            { "ArchiveClientGUID", "" },
            { "TS1FreeWill", "true" },

            // R121: the ORIGINAL Play/Graphics options screen states (STR# 145 'optionstrs').
            // FreeWill/EdgeScroll/AntiAlias/volumes above have live engine effect; the rest
            // persist the player's canon-verbatim choice pending engine work (disclosed).
            { "TS1Shadows", "true" },
            { "TS1InterfaceFX", "true" },
            { "TS1TerrainDetail", "2" },
            { "TS1CharacterDetail", "2" },
            { "TS1AutoCenter", "true" },
            { "TS1SimInBackground", "false" },
            { "TS1QuickTips", "true" },
            { "TS1AutoSnapshot", "false" },
            { "TS1LivePIP", "true" },
            { "TS1ExportHTML", "false" },
        };

        public override Dictionary<string, string> DefaultValues
        {
            get { return _DefaultValues; }
            set { _DefaultValues = value; }
        }

        public string CurrentLang { get; set; }
        public string ClientVersion { get; set; }
        public bool CityShadows { get; set; }
        public int ShadowQuality { get; set; }
        public bool SmoothZoom { get; set; }
        public int AntiAlias { get; set; }
        public bool EdgeScroll { get; set; }
        public bool Lighting { get; set; }
        public byte FXVolume { get; set; }
        public byte MusicVolume { get; set; }
        public byte VoxVolume { get; set; }
        public byte AmbienceVolume { get; set; }
        public string StartupPath { get; set; }
        public string DocumentsPath { get; set; }
        public bool Windowed { get; set; }
        private int ConfiguredGraphicsWidth, ConfiguredGraphicsHeight;
        private int? RuntimeGraphicsWidth, RuntimeGraphicsHeight;
        public int GraphicsWidth
        {
            get => RuntimeGraphicsWidth ?? ConfiguredGraphicsWidth;
            set { ConfiguredGraphicsWidth = value; RuntimeGraphicsWidth = null; }
        }
        public int GraphicsHeight
        {
            get => RuntimeGraphicsHeight ?? ConfiguredGraphicsHeight;
            set { ConfiguredGraphicsHeight = value; RuntimeGraphicsHeight = null; }
        }

        // UI layout reads the fitted logical viewport, while config.ini keeps
        // the requested resolution. Persisting fitted dimensions alongside the
        // requested DPI would enlarge the next launch's physical window.
        public void SetRuntimeViewport(int width, int height)
        {
            RuntimeGraphicsWidth = width;
            RuntimeGraphicsHeight = height;
        }

        protected override object GetValueForSave(System.Reflection.PropertyInfo property)
        {
            if (property.Name == nameof(GraphicsWidth)) return ConfiguredGraphicsWidth;
            if (property.Name == nameof(GraphicsHeight)) return ConfiguredGraphicsHeight;
            return base.GetValueForSave(property);
        }
        public string LastUser { get; set; }
        public bool SkipIntro { get; set; }
        public ulong DebugHead { get; set; }
        public ulong DebugBody { get; set; }
        public bool DebugGender { get; set; }
        public int DebugSkin { get; set; }
        public byte LanguageCode { get; set; }

        public bool UseCustomServer { get; set; }
        public string GameEntryUrl { get; set; }
        public string CitySelectorUrl { get; set; }

        public int TargetRefreshRate { get; set; }
        public int TTSMode { get; set; } //disable/allow/force
        public int SurroundingLotMode { get; set; }
        public int CompatState { get; set; }

        public string TS1HybridPath { get; set; }
        public bool TS1HybridEnable { get; set; }
        public bool TS1IsSteamInstall { get; set; }
        public bool TS1InstallationConfigured { get; set; }

        public bool Shadows3D { get; set; }
        public bool CitySkybox { get; set; }

        public int LightingMode { get; set; }

        public bool Weather { get; set; }
        public bool DirectionalLight3D { get; set; }
        public float DPIScaleFactor { get; set; }
        public int TexCompression { get; set; } //first bit on/off, second bit is user defined or auto.

        public uint ChatColor { get; set; }
        public int ChatTTSPitch { get; set; }
        public int ChatOnlyEmoji { get; set; }
        public bool ChatShowTimestamp { get; set; }
        public float ChatSizeX { get; set; }
        public float ChatSizeY { get; set; }
        public float ChatLocationX { get; set; }
        public float ChatLocationY { get; set; }
        public int ChatDeltaScale { get; set; }
        public float ChatWindowsOpacity { get; set; }

        public bool ComplexShaders { get; set; }
        public int GlobalGraphicsMode { get; set; }
        public bool EnableTransitions { get; set; }


        public string ArchiveServerGUID { get; set; }
        public string ArchiveClientGUID { get; set; }

        public bool TS1FreeWill { get; set; }

        // R121: original options screen states (see _DefaultValues note).
        public bool TS1Shadows { get; set; }
        public bool TS1InterfaceFX { get; set; }
        public int TS1TerrainDetail { get; set; }
        public int TS1CharacterDetail { get; set; }
        public bool TS1AutoCenter { get; set; }
        public bool TS1SimInBackground { get; set; }
        public bool TS1QuickTips { get; set; }
        public bool TS1AutoSnapshot { get; set; }
        public bool TS1LivePIP { get; set; }
        public bool TS1ExportHTML { get; set; }

        public static int TARGET_COMPAT_STATE = 2;
    }
}
