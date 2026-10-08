using FSO.Common.Utils;
using FSO.Content.Framework;
using FSO.Content.Interfaces;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FSO.Content.TS1
{
    public class TS1ObjectProvider : AbstractObjectProvider, IObjectCatalog
    {
        private TS1SubProvider<IffFile> GameObjects;
        private static List<ObjectCatalogItem>[] ItemsByCategory;
        private static Dictionary<uint, ObjectCatalogItem> ItemsByGUID;
        /// <summary>IFF-literal GUID -> mounted OBJD index (Round 50; pure IFF data from the
        /// object .iff members - no invented fields). Exposed for the IFF-literal behavioral-entry
        /// corpus pin (Autotest 'catalog' Part E).</summary>
        public static Dictionary<uint, OBJD> ObjdByGUID = new Dictionary<uint, OBJD>();
        /// <summary>Round 57 - IFF-LITERAL STR-content census: IFF CTSS (catalog-text) block count the
        /// engine IFF parser exposes (IFF data; mirror of STR.Read semantics). Pinned by Autotest 'ctss'.</summary>
        public static int MtCtssBlocks;
        public static int MtCtssChunks;
        public static readonly Dictionary<string, int> MtCtssPerMember = new Dictionary<string, int>();
        public static int MtStrSharpBlocks;
        public static int MtStrSharpChunks;
        public static int MtTTAsBlocks;
        public static int MtTTAsChunks;
        public static int MtBconChunks;
        public static int MtBconBlocks;
        public static int MtTprpChunks;
        public static int MtTprpBlocks;
        public static int MtGlobChunks;
        public static readonly Dictionary<string, int> MtGlobPerMember = new Dictionary<string, int>();
        public static int MtBhavInstructions;
        public static readonly Dictionary<ushort, int> MtBhavOps = new Dictionary<ushort, int>();
        public static int MtDgrpRefs;
        public static int MtDgrpChunks;
        public static int MtPaltEntries;
        public static int MtPaltChunks;
        public static int MtSlotChunks;
        public static int MtSlotEntries;
        public static int MtSprChunks;
        public static int MtSprFrames;
        public static int MtSpr2Chunks;
        public static int MtSpr2Frames;
        public static readonly int[] MtOperandHisto = new int[256];
        public static readonly Dictionary<ushort, int[]> MtOperandByOpcode = new Dictionary<ushort, int[]>();
        public static readonly Dictionary<string, int> MtChunkInv = new Dictionary<string, int>();
        public static readonly Dictionary<string, int> MtStrPerMember = new Dictionary<string, int>();
        public static readonly Dictionary<string, int> MtTTAsPerMember = new Dictionary<string, int>();
        public HashSet<uint> PersonGUIDs = new HashSet<uint>();

        public TS1ObjectProvider(Content contentManager, TS1Provider provider) : base(contentManager)
        {
            _global = provider;
            GameObjects = new TS1SubProvider<IffFile>(provider, ".iff");
        }

        private readonly TS1Provider _global;

        private static void CensusStrFamily(IffFile file, string filename)
        {
            try
            {
                var ctssL = file.List<CTSS>();
                if (ctssL != null)
                {
                    int per = 0;
                    foreach (var c in ctssL)
                    {
                        if (c == null) continue;
                        MtCtssChunks++;
                        if (c.LanguageSets != null && c.LanguageSets.Length > 0 && c.LanguageSets[0].Strings != null)
                            MtCtssBlocks += c.LanguageSets[0].Strings.Length;
                        per++;
                    }
                    if (per > 0) MtCtssPerMember[filename] = per;
                }
            }
            catch (Exception) { }
            try
            {
                var strSharpL = file.List<STR>();
                if (strSharpL != null)
                {
                    foreach (var c in strSharpL)
                    {
                        if (c == null || c.LanguageSets == null) continue;
                        MtStrSharpChunks++;
                        if (filename.IndexOf("ingledeluxe", StringComparison.OrdinalIgnoreCase) >= 0) System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "r58-str-ids.txt"), filename + ";STR#;" + c.ChunkID + ";" + c.LanguageSets.Length + "\n");
                        MtStrPerMember[filename] = MtStrPerMember.ContainsKey(filename) ? MtStrPerMember[filename] + 1 : 1;
                        if (c.LanguageSets.Length > 0 && c.LanguageSets[0] != null && c.LanguageSets[0].Strings != null)
                            MtStrSharpBlocks += c.LanguageSets[0].Strings.Length;
                    }
                }
            }
            catch (Exception) { }
            try
            {
                var ttasL = file.List<TTAs>();
                if (ttasL != null)
                {
                    foreach (var c in ttasL)
                    {
                        if (c == null || c.LanguageSets == null) continue;
                        MtTTAsChunks++;
                        MtTTAsPerMember[filename] = MtTTAsPerMember.ContainsKey(filename) ? MtTTAsPerMember[filename] + 1 : 1;
                        if (c.LanguageSets.Length > 0 && c.LanguageSets[0] != null && c.LanguageSets[0].Strings != null)
                            MtTTAsBlocks += c.LanguageSets[0].Strings.Length;
                    }
                }
            }
            catch (Exception) { }
        }

        private static void CensusConsts(IffFile file, string filename)
        {
            try
            {
                var bconL = file.List<BCON>();
                if (bconL != null)
                {
                    foreach (var c in bconL)
                    {
                        if (c == null) continue;
                        MtBconChunks++;
                        if (c.Constants != null) MtBconBlocks += c.Constants.Length;
                    }
                }
            }
            catch (Exception) { }
            try
            {
                var tprpL = file.List<TPRP>();
                if (tprpL != null)
                {
                    foreach (var c in tprpL)
                    {
                        if (c == null) continue;
                        MtTprpChunks++;
                        int n = 0;
                        if (c.ParamNames != null) n += c.ParamNames.Length;
                        if (c.LocalNames != null) n += c.LocalNames.Length;
                        MtTprpBlocks += n;
                    }
                }
            }
            catch (Exception) { }
            try
            {
                var globL = file.List<GLOB>();
                if (globL != null)
                {
                    foreach (var c in globL)
                    {
                        if (c == null) continue;
                        MtGlobChunks++;
                        MtGlobPerMember[filename] = MtGlobPerMember.ContainsKey(filename) ? MtGlobPerMember[filename] + 1 : 1;
                    }
                }
            }
            catch (Exception) { }
        }

        public void Init()
        {
            GameObjects.Init();

            Entries = new Dictionary<ulong, GameObjectReference>();
            Cache = new TimedReferenceCache<ulong, GameObject>();

            ItemsByGUID = new Dictionary<uint, ObjectCatalogItem>();
            ItemsByCategory = new List<ObjectCatalogItem>[30];
            for (int i = 0; i < 30; i++) ItemsByCategory[i] = new List<ObjectCatalogItem>();

            var allIffs = GameObjects.ListGeneric();
            foreach (var iff in allIffs)
            {
                IffFile file = null;
                try
                {
                    file = (IffFile)iff.GetThrowawayGeneric();
                }
                catch (Exception ex)
                {
                    // Log failed object file loads
                    string failedFilename = Path.GetFileName(iff.ToString().Replace('\\', '/'));
                    Content.FailedContentFiles.Add(new TS1BCFProvider.FailedFileInfo
                    {
                        Filename = failedFilename,
                        ErrorMessage = $"Failed to load object file: {ex.Message}",
                        ErrorType = ex.GetType().Name
                    });
                    continue;
                }
                
                if (file == null) continue;
                
                var source = GameObjectSource.Far;
                string filename = Path.GetFileName(iff.ToString().Replace('\\', '/'));
                if (iff is FileContentReference<object>)
                {
                    //if we're in downloads, remember the real filename and set as standalone
                    //for easy editing in volcanic (not patching)
                    var fileIff = iff as FileContentReference<object>;
                    if (fileIff.Filename.Contains("Downloads"))
                    {
                        filename = fileIff.Filename;
                        source = GameObjectSource.Standalone;
                    }
                }
                file.MarkThrowaway();
                var objects = file.List<OBJD>();
                CensusStrFamily(file, filename);
                CensusConsts(file, filename);
                try
                {
                    var bhL = file.List<BHAV>();
                    if (bhL != null)
                        foreach (var b in bhL)
                        {
                            if (b == null || b.Instructions == null) continue;
                            MtBhavInstructions += b.Instructions.Length;
                            foreach (var ins in b.Instructions)
                                if (ins != null)
                                {
                                    MtBhavOps[ins.Opcode] = MtBhavOps.ContainsKey(ins.Opcode) ? MtBhavOps[ins.Opcode] + 1 : 1;
                                    if (ins.Operand != null) foreach (var ob in ins.Operand) { MtOperandHisto[ob]++; var opArr = MtOperandByOpcode.TryGetValue(ins.Opcode, out var oa) ? oa : (MtOperandByOpcode[ins.Opcode] = new int[256]); opArr[ob]++; }
                                }
                        }
                }
                catch (Exception) { }
                try
                {
                    var dl = file.List<DGRP>();
                    if (dl != null) foreach (var dd in dl) if (dd != null && dd.Images != null) { MtDgrpChunks++; foreach (var im in dd.Images) if (im != null && im.Sprites != null) MtDgrpRefs += im.Sprites.Length; }
                    var pl = file.List<PALT>();
                    if (pl != null) foreach (var pp in pl) if (pp != null && pp.Colors != null) { MtPaltChunks++; MtPaltEntries += pp.Colors.Length; }
                    var slL = file.List<SLOT>();
                    if (slL != null) foreach (var sl in slL) if (sl != null && sl.Chronological != null) { MtSlotChunks++; MtSlotEntries += sl.Chronological.Count; }
                    var spL = file.List<SPR>();
                    if (spL != null) foreach (var sp in spL) if (sp != null && sp.Frames != null) { MtSprChunks++; MtSprFrames += sp.Frames.Count; }
                    var s2L = file.List<SPR2>();
                    if (s2L != null) foreach (var s2 in s2L) if (s2 != null && s2.Frames != null) { MtSpr2Chunks++; MtSpr2Frames += s2.Frames.Length; }
                    try { var la = file.ListAll(); if (la != null) foreach (var cc in la) if (cc != null && cc.ChunkType != null) MtChunkInv[cc.ChunkType] = MtChunkInv.ContainsKey(cc.ChunkType) ? MtChunkInv[cc.ChunkType] + 1 : 1; } catch (Exception) { }
                }
                catch (Exception) { }
                var slots = file.List<SLOT>();
                if (objects != null)
                {
                    foreach (var obj in objects)
                    {
                        Entries[obj.GUID] = new GameObjectReference(this)
                        {
                            FileName = filename,
                            ID = obj.GUID,
                            Name = obj.ChunkLabel,
                            Source = source,
                            Group = (short)obj.MasterID,
                            SubIndex = obj.SubIndex,
                            GlobalSimObject = obj.Global == 1 && obj.ObjectType != OBJDType.GiftToken
                        };
                        if (obj.ObjectType == OBJDType.Person) PersonGUIDs.Add(obj.GUID);
                        ObjdByGUID[obj.GUID] = obj; // IFF-literal: mounted OBJD == IFF data


                        //does this object appear in the catalog?
                        bool passesCatalogCheck = (obj.FunctionFlags > 0 || obj.BuildModeType > 0) && obj.Disabled == 0 && 
                            (obj.IsMultiTile || obj.NumGraphics > 0) && (obj.MasterID == 0 || obj.SubIndex == -1);
                        
                        if (passesCatalogCheck)
                        {
                            try
                            {
                                //todo: more than one of these set? no normal game objects do this
                                //todo: room sort
                                var cat = (sbyte)Math.Log(obj.FunctionFlags, 2);
                                if (obj.FunctionFlags == 0) cat = (sbyte)(obj.BuildModeType+7);
                                var item = new ObjectCatalogItem()
                                {
                                    Category = (sbyte)(cat), //0-7 buy categories. 8-15 build mode categories
                                    RoomSort = (byte)obj.RoomFlags,
                                    GUID = obj.GUID,
                                    DisableLevel = 0,
                                    Price = obj.Price,
                                    Name = obj.ChunkLabel,

                                    Subsort = (byte)obj.FunctionSubsort,
                                    CommunitySort = (byte)obj.CommunitySubsort,
                                    DowntownSort = (byte)obj.DTSubsort,
                                    MagictownSort = (byte)obj.MTSubsort,
                                    StudiotownSort = (byte)obj.STSubsort,
                                    VacationSort = (byte)obj.VacationSubsort
                                };
                                ItemsByCategory[item.Category].Add(item);
                                ItemsByGUID[item.GUID] = item;
                            }
                            catch (Exception ex)
                            {
                                // Log catalog item creation failures
                                Content.FailedContentFiles.Add(new TS1BCFProvider.FailedFileInfo
                                {
                                    Filename = filename,
                                    ErrorMessage = $"Failed to add object '{obj.ChunkLabel}' to catalog: {ex.Message}",
                                    ErrorType = "CatalogError"
                                });
                            }
                        }
                    }
                }
            }

            // Round 58: the IFF STR-family corpus includes the GLOBAL .iff members too (PersonGlobals,
            // CatGlobals, ... - IFF data the engine parses through the globals IFF stream: GameObjects is
            // BuildDictionary(".iff", "globals") and excludes them). Census those through the same IFF
            // parser so the 'strs' pin covers the WHOLE IFF STR-family corpus, object + globals.
            try
            {
                var globs = _global.GetFarEntries(".iff");
                if (globs != null)
                {
                    foreach (var e in globs)
                    {
                        var gnm = Path.GetFileName(e.FarEntry.Filename.ToLowerInvariant().Replace('\\', '/'));
                        if (!gnm.Contains("globals")) continue;
                        object gb = null;
                        try { gb = _global.GetFar(e.FarEntry.Filename); } catch (Exception) { continue; }
                        if (gb is IffFile gf) { CensusStrFamily(gf, gnm); CensusConsts(gf, gnm); var bhL2 = gf.List<BHAV>(); if (bhL2 != null) foreach (var b in bhL2) if (b != null && b.Instructions != null) { MtBhavInstructions += b.Instructions.Length; foreach (var ins in b.Instructions) if (ins != null) { MtBhavOps[ins.Opcode] = MtBhavOps.ContainsKey(ins.Opcode) ? MtBhavOps[ins.Opcode] + 1 : 1; if (ins.Operand != null) foreach (var ob in ins.Operand) { MtOperandHisto[ob]++; var opArr = MtOperandByOpcode.TryGetValue(ins.Opcode, out var oa) ? oa : (MtOperandByOpcode[ins.Opcode] = new int[256]); opArr[ob]++; } } } var dl2 = gf.List<DGRP>(); if (dl2 != null) foreach (var dd in dl2) if (dd != null && dd.Images != null) { MtDgrpChunks++; foreach (var im in dd.Images) if (im != null && im.Sprites != null) MtDgrpRefs += im.Sprites.Length; } var pl2 = gf.List<PALT>(); if (pl2 != null) foreach (var pp in pl2) if (pp != null && pp.Colors != null) { MtPaltChunks++; MtPaltEntries += pp.Colors.Length; } var slL2 = gf.List<SLOT>(); if (slL2 != null) foreach (var sl in slL2) if (sl != null && sl.Chronological != null) { MtSlotChunks++; MtSlotEntries += sl.Chronological.Count; } var spL2 = gf.List<SPR>(); if (spL2 != null) foreach (var sp in spL2) if (sp != null && sp.Frames != null) { MtSprChunks++; MtSprFrames += sp.Frames.Count; } var s2L2 = gf.List<SPR2>(); if (s2L2 != null) foreach (var s2 in s2L2) if (s2 != null && s2.Frames != null) { MtSpr2Chunks++; MtSpr2Frames += s2.Frames.Length; } try { var la2 = gf.ListAll(); if (la2 != null) foreach (var cc in la2) if (cc != null && cc.ChunkType != null) MtChunkInv[cc.ChunkType] = MtChunkInv.ContainsKey(cc.ChunkType) ? MtChunkInv[cc.ChunkType] + 1 : 1; } catch (Exception) { } }
                    }
                }
            }
            catch (Exception) { }

            var globalSims = Entries.Values.Where(x => x.GlobalSimObject);
            ControllerObjects.Clear();
            ControllerObjects.AddRange(globalSims);

            ContentManager.Neighborhood.LoadCharacters(false);
        }

        protected override Func<string, GameObjectResource> GenerateResource(GameObjectReference reference)
        {
            return (fname) =>
            {
                /** Better set this up! **/
                IffFile iff = null;

                if (reference.Source == GameObjectSource.Far)
                {
                    iff = GameObjects.Get(reference.FileName.ToLower());
                    iff.InitHash();
                    if (iff != null) iff.RuntimeInfo.Path = reference.FileName;
                }
                else
                {
                    //unused
                    iff = new IffFile(reference.FileName, reference.Source == GameObjectSource.User);
                    iff.InitHash();
                    iff.RuntimeInfo.Path = reference.FileName;
                    iff.RuntimeInfo.State = IffRuntimeState.Standalone;
                }

                if (iff != null)
                {
                    if (iff != null && iff.RuntimeInfo.State == IffRuntimeState.PIFFPatch)
                    {
                        //OBJDs may have changed due to patch. Remove all file references
                        ResetFile(iff);
                    }
                    iff.RuntimeInfo.UseCase = IffUseCase.Object;
                }

                return new GameObjectResource(iff, null, null, reference.FileName, ContentManager);
            };
        }

        public List<ObjectCatalogItem> All()
        {
            var result = new List<ObjectCatalogItem>();
            foreach (var cat in ItemsByCategory)
            {
                result.AddRange(cat);
            }
            return result;
        }

        public List<ObjectCatalogItem> GetItemsByCategory(sbyte category)
        {
            return ItemsByCategory[category];
        }

        public ObjectCatalogItem? GetItemByGUID(uint guid)
        {
            ObjectCatalogItem item;
            if (ItemsByGUID.TryGetValue(guid, out item))
                return item;
            else return null;
        }

        public List<uint> GetUntradableGUIDs()
        {
            return new List<uint>();
        }
    }
}
