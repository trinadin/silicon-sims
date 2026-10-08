using System;
using System.Collections.Generic;
using System.Linq;
using FSO.Common.Content;
using FSO.Content.Model;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;
using FSO.Files.FAR1;
using System.IO;
using FSO.Content.Framework;
using System.Text.RegularExpressions;
using FSO.Content.Codecs;
using Microsoft.Xna.Framework.Graphics;
using FSO.Common.Utils;

namespace FSO.Content
{
    /// <summary>
    /// Provides access to floor (*.flr) data in FAR3 archives.
    /// </summary>
    public class WorldFloorProvider : IContentProvider<Floor>
    {
        private Content ContentManager;
        private Dictionary<ushort, Floor> ById;

        public Dictionary<ushort, FloorReference> Entries;
        public IContentProvider<IffFile> Floors;
        public Dictionary<string, ushort> DynamicFloorFromID;

        private IffFile FloorGlobals;
        private IffFile BuildGlobals;
        public int NumFloors;

        public WorldFloorProvider(Content contentManager)
        {
            this.ContentManager = contentManager;

            this.Entries = new Dictionary<ushort, FloorReference>();
            this.ById = new Dictionary<ushort, Floor>();
            DynamicFloorFromID = new Dictionary<string, ushort>();
            SoundClassById = new Dictionary<ushort, int>();

        }

        /// <summary>
        /// AUD-20: per-pattern footstep sound class, decoded from the original
        /// PPC engine. FloorGraphicsMgr::GetFloorSound (image 0x101a4060)
        /// looks the pattern up in a std::map and returns the byte at
        /// FloorData+0; that byte is set by FloorData::FromResName
        /// (0x101a6d60) from the FIRST LETTER of the floor's SPR2 resource
        /// label (e.g. "SRC6GADB84" = Berber Carpet). The four letters that
        /// occur in the shipped catalogs, pinned by cross-referencing the
        /// floors.iff SPR2 labels with the Build.iff STR#0x82 floor names:
        ///   M (tile/granite/linoleum) -> 0 (medium)
        ///   H (hardwood/parquet)      -> 1 (hard)
        ///   S (carpet/tatami)         -> 2 (soft)
        ///   C (macadam/cement/gravel) -> 3 (hard family)
        /// Any other label -> 0; a pattern missing from the catalog -> 2
        /// (native miss default). The letter jumptable itself lives in BSS
        /// (0x1064ef14, runtime-filled) — see
        /// coordination/evidence/AUD-20/footstep-law.md.
        /// </summary>
        private Dictionary<ushort, int> SoundClassById;

        public static int FloorSoundClassFromLabel(string label)
        {
            if (string.IsNullOrEmpty(label)) return 0;
            switch (label[0])
            {
                case 'M': return 0;
                case 'H': return 1;
                case 'S': return 2;
                case 'C': return 3;
                default: return 0;
            }
        }

        /// <summary>
        /// AUD-20: native floor-sound byte for a pattern id. Returns the
        /// catalog byte (0..3) or 2 when the pattern is unknown (native
        /// map-miss default).
        /// </summary>
        public int GetFloorSound(ushort pattern)
        {
            int sc;
            if (SoundClassById.TryGetValue(pattern, out sc)) return sc;
            return 2;
        }

        private void NoteFloorSoundClass(ushort floorID, SPR2 far)
        {
            if (far == null) return;
            SoundClassById[floorID] = FloorSoundClassFromLabel(far.ChunkLabel);
        }

        /// <summary>
        /// AUD-20: capture the footstep sound class for a floor shipped as a
        /// standalone .flr IFF — take the first SPR2 that carries a label.
        /// </summary>
        private void NoteFloorSoundClassFromIff(ushort floorID, IffFile iff)
        {
            if (iff == null) return;
            foreach (var chunk in iff.SilentListAll())
            {
                var spr = chunk as SPR2;
                if (spr != null && !string.IsNullOrEmpty(spr.ChunkLabel))
                {
                    SoundClassById[floorID] = FloorSoundClassFromLabel(spr.ChunkLabel);
                    return;
                }
            }
        }

        private void InitGlobals ()
        {
            /** There is a small handful of floors in a global file for some reason **/
            ushort floorID = 1;
            var floorStrs = BuildGlobals.Get<STR>(0x82);
            for (ushort i = 1; i < (floorStrs.Length / 3); i++)
            {
                var far = FloorGlobals.Get<SPR2>(i);
                var medium = FloorGlobals.Get<SPR2>((ushort)(i + 256));
                var near = FloorGlobals.Get<SPR2>((ushort)(i + 512)); //2048 is water tile

                far.FloorCopy = 1;
                medium.FloorCopy = 1;
                near.FloorCopy = 1;

                this.AddFloor(new Floor
                {
                    ID = floorID,
                    Far = far,
                    Medium = medium,
                    Near = near
                });

                Entries.Add(floorID, new FloorReference(this)
                {
                    ID = floorID,
                    FileName = "global",

                    Name = floorStrs.GetString((i - 1) * 3 + 1),
                    Price = int.Parse(floorStrs.GetString((i - 1) * 3 + 0)),
                    Description = floorStrs.GetString((i - 1) * 3 + 2)
                });

                // AUD-20: footstep sound class from the SPR2 resource label
                // (native FloorData::FromResName 0x101a6d60).
                NoteFloorSoundClass(floorID, far);

                floorID++;
            }

            var waterStrs = BuildGlobals.Get<STR>(0x85);
            //add pools for catalog logic
            Entries.Add(65535, new FloorReference(this)
            {
                ID = 65535,
                FileName = "global",

                Price = int.Parse(waterStrs.GetString(0)),
                Name = waterStrs.GetString(1),
                Description = waterStrs.GetString(2)
            });

            Entries.Add(65534, new FloorReference(this)
            {
                ID = 65534,
                FileName = "global",

                Price = int.Parse(waterStrs.GetString(3)),
                Name = waterStrs.GetString(4),
                Description = waterStrs.GetString(5)
            });


            floorID = 256;
        }

        public void InitTS1()
        {
            var floorGlobalsPath = Path.Combine(ContentManager.TS1BasePath, "GameData/floors.iff");
            var floorGlobals = new IffFile(floorGlobalsPath);
            FloorGlobals = floorGlobals;

            var buildGlobalsPath = Path.Combine(ContentManager.TS1BasePath, "GameData/Build.iff");
            BuildGlobals = new IffFile(buildGlobalsPath); //todo: centralize?

            InitGlobals();

            //load *.flr iffs from both the TS1 provider and folder

            ushort floorID = 256;
            var files = new FileProvider<IffFile>(ContentManager, new IffCodec(), new Regex(".*/Floors.*\\.flr"));
            files.UseTS1 = true;
            var ts1 = new TS1SubProvider<IffFile>(ContentManager.TS1Global, ".flr");
            files.Init();
            ts1.Init();
            var compo = new CompositeProvider<IffFile>(new List<IContentProvider<IffFile>>() {
                ts1,
                files
                });

            Floors = compo;
            var all = compo.ListGeneric();
            foreach (var entry in all)
            {
                var iff = (IffFile)entry.GetThrowawayGeneric();
                DynamicFloorFromID[Path.GetFileNameWithoutExtension(entry.ToString().Replace('\\', '/')).ToLowerInvariant()] = floorID;
                var catStrings = iff.Get<STR>(0);

                Entries.Add(floorID, new FloorReference(this)
                {
                    ID = floorID,
                    FileName = Path.GetFileName(entry.ToString().Replace('\\', '/')).ToLowerInvariant(),

                    Name = catStrings.GetString(0),
                    Price = int.Parse(catStrings.GetString(1)),
                    Description = catStrings.GetString(2)
                });
                NoteFloorSoundClassFromIff(floorID, iff); // AUD-20

                floorID++;
            }
            NumFloors = floorID;
        }

        /// <summary>
        /// Initiates loading of floors.
        /// </summary>
        public void Init()
        {
            var floorGlobalsPath = ContentManager.GetPath("objectdata/globals/floors.iff");
            var floorGlobals = new IffFile(floorGlobalsPath);
            FloorGlobals = floorGlobals;

            var buildGlobalsPath = ContentManager.GetPath("objectdata/globals/build.iff");
            BuildGlobals = new IffFile(buildGlobalsPath); //todo: centralize?

            InitGlobals();

            ushort floorID = 256;

            var archives = new string[]
            {
                "housedata/floors/floors.far",
                "housedata/floors2/floors2.far",
                "housedata/floors3/floors3.far",
                "housedata/floors4/floors4.far"
            };


            for (var i = 0; i < archives.Length; i++)
            {
                var archivePath = ContentManager.GetPath(archives[i]);
                var archive = new FAR1Archive(archivePath, true);
                var entries = archive.GetAllEntries();

                foreach (var entry in entries)
                {
                    DynamicFloorFromID[new string(entry.Key.TakeWhile(x => x != '.').ToArray()).ToLowerInvariant()] = floorID;
                    var iff = new IffFile();
                    var bytes = archive.GetEntry(entry);
                    using(var stream = new MemoryStream(bytes))
                    {
                        iff.Read(stream);
                    }


                    var catStrings = iff.Get<STR>(0);

                    Entries.Add(floorID, new FloorReference(this)
                    {
                        ID = floorID,
                        FileName = entry.Key,

                        Name = catStrings.GetString(0),
                        Price = int.Parse(catStrings.GetString(1)),
                        Description = catStrings.GetString(2)
                    });
                    NoteFloorSoundClassFromIff(floorID, iff); // AUD-20

                    floorID++;
                }
                archive.Close();
            }

            NumFloors = floorID;
            var far1 = new FAR1Provider<IffFile>(ContentManager, new IffCodec(), new Regex(".*/floors.*\\.far"));
            this.Floors = far1;
            far1.Init();
        }

        private void AddFloor(Floor floor)
        {
            ById.Add(floor.ID, floor);
        }


        public Texture2D GetFloorThumb(ushort id, GraphicsDevice device)
        {
            if (id < 256)
            {
                return TextureUtils.Copy(device, ById[id].Near.Frames[0].GetTexture(device));
            }
            else if (id == 65535)
            {

                return TextureUtils.Copy(device, FloorGlobals.Get<SPR2>(0x420).Frames[0].GetTexture(device));
            }
            else if (id == 65534)
            {
                var spr = FloorGlobals.Get<SPR2>(0x800);
                if (!spr.SpritePreprocessed)
                {
                    spr.ZAsAlpha = true;
                    spr.FloorCopy = 2;
                    spr.SpritePreprocessed = true;
                }
                return TextureUtils.Copy(device, spr.Frames[0].GetTexture(device));
            }
            else
            {
                IffFile iff;
                if (this.Floors is FAR1Provider<IffFile>)
                    iff = ((FAR1Provider<IffFile>)this.Floors).ThrowawayGet(Entries[(ushort)id].FileName);
                else
                    iff = this.Floors.Get(Entries[(ushort)id].FileName);

                var spr = iff?.Get<SPR2>(513);
                if (spr != null) spr.FloorCopy = 1;

                return spr?.Frames[0].GetTexture(device);
            }
        }

        public SPR2 GetGlobalSPR(ushort id)
        {
            var spr = FloorGlobals.Get<SPR2>(id);
            if (id >= 0x800 && id <= 0x830 && !spr.SpritePreprocessed)
            {
                spr.ZAsAlpha = true;
                spr.SpritePreprocessed = true;
                spr.FloorCopy = 2;
            }
            else if (spr.FloorCopy == 0)
            {
                spr.FloorCopy = (id >= 0x400 && id <= 0x430)?2:1;
            }
            return spr;
        }

        #region IContentProvider<Floor> Members

        public Floor Get(ulong id)
        {
            if (ById.ContainsKey((ushort)id))
            {
                return ById[(ushort)id];
            }
            else
            {
                //get from iff
                if (!Entries.ContainsKey((ushort)id)) return null;
                IffFile iff = this.Floors.Get(Entries[(ushort)id].FileName);
                if (iff == null) return null;

                var far = iff.Get<SPR2>(1);
                var medium = iff.Get<SPR2>(257);
                var near = iff.Get<SPR2>(513);

                far.FloorCopy = 1;
                medium.FloorCopy = 1;
                near.FloorCopy = 1;

                ById[(ushort)id] = new Floor
                {
                    ID = (ushort)id,
                    Near = near,
                    Medium = medium,
                    Far = far
                };
                return ById[(ushort)id];
            }
        }

        public Floor Get(uint type, uint fileID)
        {
            return null;
        }

        public List<IContentReference<Floor>> List()
        {
            return new List<IContentReference<Floor>>(Entries.Values);
        }

        public Floor Get(string name)
        {
            throw new NotImplementedException();
        }

        public Floor Get(ContentID id)
        {
            throw new NotImplementedException();
        }

        #endregion
    }

    public class FloorReference : IContentReference<Floor>
    {
        public ulong ID;
        public string FileName;

        public int Price; //remember these, just in place of a catalog
        public string Name;
        public string Description;

        private WorldFloorProvider Provider;

        public FloorReference(WorldFloorProvider provider)
        {
            this.Provider = provider;
        }

        #region IContentReference<Floor> Members

        public Floor Get()
        {
            return Provider.Get(ID);
        }

        public object GetThrowawayGeneric()
        {
            throw new NotImplementedException();
        }

        public object GetGeneric()
        {
            return Get();
        }

        #endregion
    }
}
