using FSO.Common;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FSO.Content.TS1
{
    /// <summary>
    /// Provides families, neighbors, neighborhood structure and more given a current userdata folder. Should also allow the game to save these things.
    /// </summary>
    public class TS1NeighborhoodProvider
    {
        //injected from game. requires instantiating a vm and getting the person data after Init runs.
        public Func<uint, short[]> PreparePersonDataFromObject;

        public IffFile MainResource;
        public IffFile LotLocations;
        public IffFile LotZoning;
        public IffFile StreetNames;
        public IffFile NeighbourhoodDesc;
        public IffFile STDesc;
        public IffFile MTDesc;
        public Dictionary<short, short> ZoningDictionary = new Dictionary<short, short>();
        public NBRS Neighbors;
        public NGBH Neighborhood;
        public TATT TypeAttributes;
        public Dictionary<short, FAMI> FamilyForHouse = new Dictionary<short, FAMI>();
        public Content ContentManager;
        public TS1GameState GameState = new TS1GameState();
        public string UserPath;
        public int NextSim;

        //NBR-02 (Neighborhood::GetCurrentNeighborhoodNumber @0xac7a0): the native
        //reads the number byte off the live neighborhood record; the port's
        //equivalent is the id passed to InitSpecific (0=UserData, n=UserData{n+1}).
        //The neighbourhood switcher must draw this, not a constant.
        public int CurrentNeighborhoodID { get; private set; }

        public HashSet<uint> DirtyAvatars = new HashSet<uint>();

        /// <summary>
        /// SAV-08 fault-injection seam: when non-null (only the opt-in autotest
        /// 'savefault' battery sets it), every persistence stream is routed
        /// through this wrapper so a write can be failed at a controlled offset.
        /// Always null in normal play.
        /// </summary>
        public static Func<Stream, Stream> SaveStreamFaultHook;

        /// <summary>
        /// Writes `path` atomically: content goes to path + ".savtmp", which
        /// replaces the destination only after the write closure succeeds. A
        /// mid-write failure (disk full, serialization error, injected fault)
        /// leaves the destination byte-identical, deletes the temp file and
        /// rethrows — recovery is defined: previous save intact, nothing
        /// half-written on the real path.
        /// </summary>
        private void AtomicWrite(string path, Action<Stream> write)
        {
            var tmp = path + ".savtmp";
            try
            {
                using (var raw = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var stream = (SaveStreamFaultHook != null) ? SaveStreamFaultHook(raw) : raw)
                {
                    write(stream);
                    stream.Flush();
                }
                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
            }
            catch
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                throw;
            }
        }

        public TS1NeighborhoodProvider(Content contentManager)
        {
            ContentManager = contentManager;
            InitSpecific(0);
        }

        /// <summary>
        /// Intializes a specific neighbourhood. Also counts as a save discard, since it unloads the current neighbourhood.
        /// </summary>
        /// <param name="id"></param>
        public void InitSpecific(int id)
        {
            DirtyAvatars.Clear();
            ZoningDictionary.Clear();
            FamilyForHouse.Clear();

            var udName = NeighborhoodDirName(id);
            CurrentNeighborhoodID = id;
            //simitone shouldn't modify existing ts1 data, since our house saves are incompatible.
            //therefore we should copy to the simitone user data.
            
            var userPath = Path.Combine(FSOEnvironment.UserDir, udName + "/");
            
            if (!Directory.Exists(userPath))
            {
                
                string source;
                
                // Check if user selected Steam install via Content.TS1SteamInstall flag
                if (Content.TS1SteamInstall)
                {
                    // Use Steam's "Saved Games" location (used by The Sims Legacy Collection)
                    source = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        "Saved Games", "Electronic Arts", "The Sims 25", udName + "/"
                    );
                }
                else
                {
                    // Use install directory saves (non-Steam installs)
                    source = Path.Combine(ContentManager.TS1BasePath, udName + "/");
                }
                
                
                var destination = userPath;

                // Normalize paths for comparison (remove trailing slashes, use consistent separators)
                var normalizedSource = source.TrimEnd('/', '\\');
                var normalizedDest = destination.TrimEnd('/', '\\');

                // Create directory structure
                foreach (string dirPath in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
                {
                    var relativePath = dirPath.Substring(normalizedSource.Length).TrimStart('\\', '/');
                    var destDir = Path.Combine(normalizedDest, relativePath);
                    Directory.CreateDirectory(destDir);
                }

                // Copy files with error handling
                foreach (string srcPath in Directory.GetFiles(source, "*.*", SearchOption.AllDirectories))
                {
                    var relativePath = srcPath.Substring(normalizedSource.Length).TrimStart('\\', '/');
                    var destPath = Path.Combine(normalizedDest, relativePath);
                    try
                    {
                        File.Copy(srcPath, destPath, true);
                    }
                    catch (IOException ex)
                    {
                        throw; // Re-throw to show error dialog
                    }
                }
                
            }
            
            UserPath = userPath;

            MainResource = new IffFile(Path.Combine(UserPath, "Neighborhood.iff"));
            LotLocations = new IffFile(Path.Combine(UserPath, "LotLocations.iff"));
            LotZoning = new IffFile(Path.Combine(UserPath, "LotZoning.iff"));
            StreetNames = new IffFile(Path.Combine(UserPath, "StreetNames.iff"));
            NeighbourhoodDesc = new IffFile(Path.Combine(UserPath, "Houses/NeighborhoodDesc.iff"));
            STDesc = new IffFile(Path.Combine(UserPath, "Houses/STDesc.iff"));
            MTDesc = new IffFile(Path.Combine(UserPath, "Houses/MTDesc.iff"));

            var zones = LotZoning.Get<STR>(1);
            for (int i = 0; i < zones.Length; i++)
            {
                var split = zones.GetString(i).Split(',');
                ZoningDictionary[short.Parse(split[0])] = (short)((split[1] == " community") ? 1 : 0);
            }
            Neighbors = MainResource.List<NBRS>().FirstOrDefault();
            Neighborhood = MainResource.List<NGBH>().FirstOrDefault();
            TypeAttributes = MainResource.List<TATT>().FirstOrDefault();

            FamilyForHouse = new Dictionary<short, FAMI>();
            var families = MainResource.List<FAMI>();
            foreach (var fam in families)
            {
                FamilyForHouse[(short)fam.HouseNumber] = fam;
            }

            LoadCharacters(true);
            //todo: manage avatar iffs here
        }

        //NBR-02: directory name for a neighborhood id (InitSpecific's mount rule).
        private static string NeighborhoodDirName(int id)
        {
            return "UserData" + ((id == 0) ? "" : (id + 1).ToString());
        }

        //NBR-02 (Neighborhood::CheckForNewUserDataDirectories @0xac7f0): enumerate
        //the materialized neighborhoods - the owned UserData plus every UserDataN
        //under the user dir that carries a Neighborhood.iff. Ordered by id.
        public List<int> GetAvailableNeighborhoods()
        {
            var result = new List<int>();
            var zero = Path.Combine(FSOEnvironment.UserDir, NeighborhoodDirName(0), "Neighborhood.iff");
            if (File.Exists(zero)) result.Add(0);
            foreach (var dir in Directory.GetDirectories(FSOEnvironment.UserDir, "UserData*"))
            {
                var name = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar));
                if (name.Length <= 8 || !int.TryParse(name.Substring(8), out int n) || n < 2) continue;
                var id = n - 1;
                if (File.Exists(Path.Combine(dir, "Neighborhood.iff"))) result.Add(id);
            }
            result.Sort();
            return result;
        }

        //NBR-02: install-template directory for a neighborhood name — the same
        //source rules InitSpecific uses for its copy-on-missing create path.
        private string TemplateDirFor(string udName)
        {
            if (Content.TS1SteamInstall)
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Saved Games", "Electronic Arts", "The Sims 25", udName + "/");
            }
            return Path.Combine(ContentManager.TS1BasePath, udName + "/");
        }

        //NBR-02 (Neighborhood::SwitchToNewNeighborhood @0xacff0 + Next/Previous
        //@0xad170/@0xad1f0): save the live neighborhood, then mount the target id.
        //Next/Previous selection order is UI concern (NBR-05); the backend takes
        //the target id. A target never materialized in the user dir is CREATED
        //from its install template on demand (the native create path); ids with
        //no template backing fail bounded.
        public bool SwitchToNeighborhood(int id)
        {
            if (id == CurrentNeighborhoodID) return true;
            var udName = NeighborhoodDirName(id);
            if (!File.Exists(Path.Combine(FSOEnvironment.UserDir, udName, "Neighborhood.iff"))
                && !File.Exists(Path.Combine(TemplateDirFor(udName), "Neighborhood.iff")))
                return false;
            SaveNeighbourhood(true); //the native switch saves the current record before the load
            InitSpecific(id);        //unmount + full reload = reopen consistency by construction
            return File.Exists(Path.Combine(FSOEnvironment.UserDir, udName, "Neighborhood.iff"));
        }

        public void AddMissingNeighbors()
        {
            var objs = (TS1ObjectProvider)ContentManager.WorldObjects;
            var missing = objs.PersonGUIDs.Where(x => !Neighbors.Entries.Any(y => y.GUID == x)).Select(x => objs.Get(x));
            foreach (var obj in missing)
            {
                var id = Neighbors.GetFreeID();
                Neighbors.AddNeighbor(new Neighbour()
                {
                    NeighbourID = id,
                    GUID = (uint)obj.GUID,
                    Name = Path.GetFileName(obj.Resource.Name).ToLowerInvariant().Replace(".iff", ""),
                    PersonData = PreparePersonDataFromObject((uint)obj.GUID),
                    PersonMode = 9,
                    Relationships = new Dictionary<int, List<short>>()
                });
            }
        }

        public void LoadCharacters(bool clearLast)
        {
            var objs = (TS1ObjectProvider)ContentManager.WorldObjects;
            if (objs.Entries == null) return;
            if (clearLast)
            {
                foreach (var obj in objs.Entries.Where(x => x.Value.Source == GameObjectSource.User))
                {
                    objs.RemoveObject((uint)obj.Key);
                    objs.PersonGUIDs.Add((uint)obj.Key);
                }
            }

            NextSim = 0;
            var path = Path.Combine(UserPath, "Characters/");
            var files = Directory.EnumerateFiles(path);
            foreach (var filename in files)
            {
                if (Path.GetExtension(filename) != ".iff") return;

                int userID;
                var name = Path.GetFileName(filename);
                if (name.Length > 8 && int.TryParse(name.Substring(4, 5), out userID) && userID >= NextSim)
                {
                    NextSim = userID + 1;
                }

                var file = new IffFile(filename);
                file.MarkThrowaway();

                var objects = file.List<OBJD>();
                if (objects != null)
                {
                    foreach (var obj in objects)
                    {
                        objs.Entries[obj.GUID] = new GameObjectReference(objs)
                        {
                            FileName = filename,
                            ID = obj.GUID,
                            Name = obj.ChunkLabel,
                            Source = GameObjectSource.User,
                            Group = (short)obj.MasterID,
                            SubIndex = obj.SubIndex
                        };
                        if (obj.ObjectType == OBJDType.Person) objs.PersonGUIDs.Add(obj.GUID);
                    }
                }
            }
        }

        public void SaveNewNeighbour(GameObject obj)
        {
            var objs = (TS1ObjectProvider)ContentManager.WorldObjects;
            //save to a new user iff
            var path = Path.Combine(UserPath, "Characters/");
            var filename = Path.Combine(path, "User" + (NextSim++).ToString().PadLeft(5, '0') + ".iff");
            AtomicWrite(filename, s => obj.Resource.MainIff.Write(s));

            objs.Entries[obj.OBJ.GUID] = new GameObjectReference(objs)
            {
                FileName = filename,
                ID = obj.GUID,
                Name = obj.OBJ.ChunkLabel,
                Source = GameObjectSource.User,
                Group = (short)obj.OBJ.MasterID,
                SubIndex = obj.OBJ.SubIndex
            };
        }

        public Neighbour GetNeighborByID(short ID)
        {
            Neighbour result = null;
            Neighbors.NeighbourByID.TryGetValue(ID, out result);
            return result;
        }

        public FAMI GetFamilyForHouse(short ID)
        {
            FAMI result = null;
            FamilyForHouse.TryGetValue(ID, out result);
            return result;
        }

        public int GetMagicoinsForNeighbor(short ID)
        {
            return GetInventoryByNID(ID)?.FirstOrDefault(x => x.GUID == 0x99E81BEC)?.Count ?? 0;
        }

        public int GetMagicoinsForFamily(FAMI family)
        {
            if (family == null) return 0;
            return family.FamilyGUIDs.Select(x => GetMagicoinsForNeighbor(GetNeighborIDForGUID(x) ?? -1)).Sum();
        }

        //NBR-02 (Neighborhood::MoveOut @0xb1800, port-complete): clears the
        //family<->house binding; liquidates the lot's net worth into the family
        //budget (ValueInArch, as decoded); with killSims the member neighbours,
        //their character files and their inventories are deleted and the family's
        //membership is emptied (decode-literal; the FAMI record itself survives as
        //an empty bin shell - whether the native also removes that record is
        //unresolved in the NBR-01 decode and stays open for review). Returns
        //-1 when no family owns the lot, else 1 (the EvictFamily contract).
        //The eviction confirm path owns the neighborhood save afterwards.
        public int MoveOut(short houseID, bool killSims)
        {
            var old = GetFamilyForHouse(houseID);
            if (old == null) return -1;

            old.Budget += old.ValueInArch;
            old.ValueInArch = 0;
            old.HouseNumber = 0;
            FamilyForHouse.Remove(houseID);

            if (killSims)
            {
                var objs = (TS1ObjectProvider)ContentManager.WorldObjects;
                foreach (var guid in old.FamilyGUIDs)
                {
                    short? nid = GetNeighborIDForGUID(guid);
                    if (nid != null)
                    {
                        var neigh = GetNeighborByID(nid.Value);
                        if (neigh != null) Neighbors.RemoveNeighbor(neigh);
                        Neighborhood.InventoryByID.Remove(nid.Value);
                    }
                    if (guid != 0 && objs.Entries.TryGetValue(guid, out var entry)
                        && entry.Source == GameObjectSource.User)
                    {
                        try { File.Delete(entry.FileName); }
                        catch (IOException) { /* keep going: the record removal still unlinks it */ }
                        objs.RemoveObject(guid);
                    }
                }
                old.FamilyGUIDs = new uint[0];
            }
            return 1;
        }

        //Pre-law convenience overload: unbind only (family returns to the bin,
        //members kept). The native's UI eviction passes killSims=true.
        public void MoveOut(short houseID)
        {
            MoveOut(houseID, false);
        }

        /// <summary>
        /// NBR-03 (Neighborhood::GetZoningType @0xaaa20): walks the zoning table
        /// (this+0x170/+0x174 stride 8, key=+0 value=+4) and returns the matched
        /// value, 0 when the key is the 0 sentinel or when not found. The port's
        /// zoning table is ZoningDictionary (derived from LotZoning.iff STR#1).
        /// </summary>
        public short GetZoningType(short lot)
        {
            return ZoningDictionary.TryGetValue(lot, out var zone) ? zone : (short)0;
        }

        /// <summary>
        /// NBR-03: rezone a lot between residential (0) and community (1).
        /// Native Neighborhood::SetZoningType @0xaa700 finds the table row, writes
        /// row+0x04 = zoneType and persists. The port's zoning table is
        /// ZoningDictionary (derived from LotZoning.iff STR#1); this updates the
        /// dictionary, rewrites the matching STR#1 entry ("lot, community" / "lot, ")
        /// and persists atomically via SAV-08's AtomicWrite (temp + File.Replace),
        /// leaving every other lot's zone unchanged. zoneType is clamped to 0/1.
        /// Returns false on a malformed/missing LotZoning.iff.
        /// </summary>
        public bool SetZoningType(short lot, short zoneType)
        {
            zoneType = (short)(zoneType == 0 ? 0 : 1);
            ZoningDictionary[lot] = zoneType;
            var zones = LotZoning?.Get<STR>(1);
            if (zones == null) return false;
            for (int i = 0; i < zones.Length; i++)
            {
                var parts = zones.GetString(i).Split(',');
                if (short.TryParse(parts[0].Trim(), out var id) && id == lot)
                {
                    zones.SetString(i, lot + ((zoneType == 1) ? ", community" : ", "));
                    try
                    {
                        AtomicWrite(Path.Combine(UserPath, "LotZoning.iff"), s => LotZoning.Write(s));
                    }
                    catch { return false; }
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// NBR-03 (bulldoze — the empty-lot branch of the evict mode). Native
        /// EvictModeLotHandler: the lot-popup option is "Bulldoze" when the lot is
        /// vacant and "Evict" when occupied; an occupied lot is evicted (NBR-02
        /// MoveOut, never bulldozed). Bulldozing a VACANT built lot clears the lot's
        /// building content so it becomes an undeveloped lot (the native reloads the
        /// empty lot thumbnail). Returns a result code:
        ///   1 = bulldozed (building content cleared),
        ///   0 = nothing to bulldoze (no house file, or already undeveloped),
        ///  -1 = refused (a family owns the lot — it must be evicted, NBR-02).
        /// </summary>
        public int BulldozeLot(short lot)
        {
            //Occupied lots are evicted (NBR-02 MoveOut), never bulldozed.
            if (GetFamilyForHouse(lot) != null) return -1;
            return ClearLotBuilding(lot) ? 1 : 0;
        }

        /// <summary>
        /// NBR-03: clears a vacant lot's building content so the lot becomes
        /// undeveloped, persisting the result. The port's "built" marker is the
        /// house file's SIMI ObjectsValue/ArchitectureValue (the client's houseBuilt
        /// gate reads the same source); bulldozing zeroes them. A whole-file
        /// IffFile.Write cannot round-trip a lazily-parsed house (HOUS/ARRY/THMB
        /// have no Write override — PatchHouseSimiGlobal's doc), so each SIMI field
        /// is zeroed by a two-byte-atomic byte patch in place, leaving everything
        /// else byte-identical. Returns false when there is nothing to clear (no
        /// house file or already undeveloped), so a second bulldoze is a no-op.
        /// </summary>
        private bool ClearLotBuilding(short lot)
        {
            var housePath = GetHousePath(lot);
            if (!File.Exists(housePath)) return false;
            //The port's houseBuilt gate: a lot is built when either building value > 0.
            if (!ZeroHouseSimiBuildingValues(housePath)) return false;
            return true;
        }

        /// <summary>
        /// Zeroes the SIMI ObjectsValue and ArchitectureValue fields of a house
        /// file in place. The SIMI chunk body layout (FSO SIMI codec) is
        /// [pad:4][version:4][magic "IMIS":4][globals, i16 LE...][Unknown1:2]
        /// [Unknown2..Unknown4 + GUID1 + GUID2 + LotValue: 6 x i32][ObjectsValue:i32]
        /// [ArchitectureValue:i32][6 BudgetDays]. Chunk records start after the
        /// 60-byte identifier + 4-byte resource-map offset (off=64); each is
        /// [type:4][size:u32 BE][id:2][flags:2][label:64], size inclusive.
        /// </summary>
        private bool ZeroHouseSimiBuildingValues(string housePath)
        {
            try
            {
                var data = File.ReadAllBytes(housePath);
                var off = 64;
                while (off + 76 <= data.Length)
                {
                    var size = (data[off + 4] << 24) | (data[off + 5] << 16) | (data[off + 6] << 8) | data[off + 7];
                    if (size < 76 || off + size > data.Length) return false;
                    if (data[off] == 'S' && data[off + 1] == 'I' && data[off + 2] == 'M' && data[off + 3] == 'I')
                    {
                        var payload = off + 76;
                        //verify the SIMI body magic before touching anything
                        if (payload + 12 > data.Length
                            || data[payload + 8] != 'I' || data[payload + 9] != 'M'
                            || data[payload + 10] != 'I' || data[payload + 11] != 'S') return false;

                        var version = data[payload + 4] | (data[payload + 5] << 8) | (data[payload + 6] << 16) | (data[payload + 7] << 24);
                        var items = (version > 0x3F) ? 0x40 : 0x20;
                        var objPos = payload + 12 + (items * 2) + 26;   //after globals + Unknown1 + 6 x i32
                        var archPos = objPos + 4;
                        if (archPos + 4 > payload + (size - 76)) return false; //not present in this SIMI version

                        //Read-only check first: already undeveloped → nothing to bulldoze.
                        var objVal = BitConverter.ToInt32(data, objPos);
                        var archVal = BitConverter.ToInt32(data, archPos);
                        if (objVal == 0 && archVal == 0) return false;

                        //write both int32 building values as zero, atomic for the file
                        using (var stream = new FileStream(housePath, FileMode.Open, FileAccess.Write, FileShare.None))
                        {
                            stream.Seek(objPos, SeekOrigin.Begin);
                            stream.Write(new byte[8], 0, 8); //ObjectsValue + ArchitectureValue = 0
                        }
                        return true;
                    }
                    off += size;
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void SetFamilyForHouse(short houseID, FAMI family, bool buy)
        {
            family.HouseNumber = houseID;
            if (buy)
            {
                family.Budget -= GetHouse(houseID)?.Get<SIMI>(1)?.PurchaseValue ?? 0;
            }
            FamilyForHouse[houseID] = family;
        }

        public FAMI GetFamily(ushort ID)
        {
            return MainResource.Get<FAMI>(ID);
        }

        public FAMs GetFamilyString(ushort ID)
        {
            return MainResource.Get<FAMs>(ID);
        }

        /// <summary>
        /// SAV-06 / r253-import-export port-audit §3.2: serialize familyId to a
        /// .FAM in &lt;UserPath&gt;Export/ and return the written path (or null on
        /// failure). Reuses the R248 FAM layout. Chunk set written in this tranche:
        /// EXPi, FAMI (live clone), FAMs, NBRS (member subset). uChr/CTSS/FINV/SIMI
        /// are NOT yet written (see the SAV-06 result note / follow-up).
        /// </summary>
        public string ExportFamily(ushort familyId)
        {
            var fami = GetFamily(familyId);
            if (fami == null) return null; //no such family -> no export

            // EXPi: 0-terminated member id list (native efi, up to 8).
            var expi = new EXPi { ChunkID = 0, FamilyID = (short)familyId, FlagByte = 1 };
            var ids = new List<short>();
            foreach (var guid in fami.FamilyGUIDs)
            {
                var nid = GetNeighborIDForGUID(guid);
                if (nid != null) ids.Add(nid.Value);
            }
            for (int i = 0; i < Math.Min(ids.Count, 8); i++) expi.MemberIDs[i] = ids[i];
            expi.MemberIDs[Math.Min(ids.Count, 8)] = 0; //0-terminate

            // NBRS subset: only the family's member records.
            var nbrs = new NBRS();
            foreach (var nid in expi.ActiveMemberIDs)
            {
                var n = GetNeighborByID(nid);
                if (n == null) continue;
                nbrs.Entries.Add(n);
                nbrs.NeighbourByID[n.NeighbourID] = n;
                nbrs.DefaultNeighbourByGUID[n.GUID] = n.NeighbourID;
            }

            var fam = new IffFile();
            fam.AddChunk(expi);
            fam.AddChunk(fami);            // live FAMI (ChunkID == familyId)
            var fams = GetFamilyString(familyId);
            if (fams != null) fam.AddChunk(fams);
            fam.AddChunk(nbrs);

            // (SAV-06 uChr/CTSS tranche) uChr + CTSS per member (port-audit §3.2):
            // uChr = the member's STR#200 bodystring set (chunk id = member id);
            // CTSS = name/bio (chunk id = member id + 2000). CTSS name/bio is derived
            // from the live Neighbour Name; uChr carries the member kind (string 13
            // "human") and clones the character STR#200 when a live character object
            // is present.
            foreach (var nid in expi.ActiveMemberIDs)
            {
                var n = GetNeighborByID(nid);
                if (n == null) continue;
                var ctss = new CTSS { ChunkID = (ushort)(nid + 2000) };
                PutString(ctss, 0, n.Name ?? "");
                PutString(ctss, 1, "");
                ctss.ChunkLabel ??= "";
                fam.AddChunk(ctss);

                var uchr = new uChr { ChunkID = (ushort)nid };
                // Only source the character STR#200 when the world object store is
                // initialized and actually carries this member (it is null/empty in a
                // headless caller); otherwise the bodystring set is left sparse.
                if (ContentManager.WorldObjects.Entries != null &&
                    ContentManager.WorldObjects.Entries.ContainsKey(n.GUID))
                {
                    var obj = ContentManager.WorldObjects.Get(n.GUID);
                    CopyStrings(uchr, obj?.Resource.Get<STR>(200));
                }
                if (string.IsNullOrEmpty(uchr.GetString(13))) PutString(uchr, 13, "human");
                uchr.ChunkLabel ??= "";
                fam.AddChunk(uchr);
            }

            // (SAV-06 FINV tranche) FINV (optional, port-audit §3.2): positional
            // member inventories, parallel to the EXPi member ids. Written when any
            // member carries an inventory; the import re-keys entry i onto the
            // recreated character.
            var finv = new FINV { ChunkID = 0, Version = 2 };
            foreach (var nid in expi.ActiveMemberIDs)
                finv.Inventories.Add(GetInventoryByNID(nid) ?? new List<InventoryItem>());
            finv.ChunkLabel ??= "";
            if (finv.Inventories.Any(x => x.Count > 0)) fam.AddChunk(finv);

            // IffFile.Write requires a non-null ChunkLabel (WriteCString pads it);
            // newly-created chunks default it to null.
            foreach (var c in new IffChunk[] { expi, fami, fams, nbrs })
                if (c != null && c.ChunkLabel == null) c.ChunkLabel = "";

            var exportDir = Path.Combine(UserPath, "Export");
            Directory.CreateDirectory(exportDir);
            var path = Path.Combine(exportDir, ExportFileName(fams, familyId));
            using (var s = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                fam.Write(s);
            return path;
        }

        private string ExportFileName(FAMs fams, ushort familyId)
        {
            var name = fams?.GetString(0);
            if (string.IsNullOrEmpty(name)) name = "Family";
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            foreach (var c in Path.GetInvalidPathChars()) name = name.Replace(c, '_');
            return name + "_" + familyId + ".FAM";
        }

        /// <summary>Clone a source STR's default-language string set into a fresh STR.</summary>
        private static void CopyStrings(STR dst, STR src)
        {
            if (src == null || src.LanguageSets.Length == 0 || src.LanguageSets[0].Strings == null) return;
            dst.LanguageSets[0].Strings = src.LanguageSets[0].Strings
                .Select(x => new STRItem { LanguageCode = x.LanguageCode, Value = x.Value, Comment = x.Comment })
                .ToArray();
        }

        /// <summary>Write a string into a STR chunk at an index, growing the default set to fit.</summary>
        private static void PutString(STR chunk, int index, string value)
        {
            var ls = chunk.LanguageSets[0];
            while (index >= ls.Strings.Length)
            {
                Array.Resize(ref ls.Strings, ls.Strings.Length + 1);
                ls.Strings[ls.Strings.Length - 1] = new STRItem { Value = "", Comment = "" };
            }
            ls.Strings[index].Value = value ?? "";
        }

        public short? GetNeighborIDForGUID(uint GUID)
        {
            short result = 0;
            if (Neighbors.DefaultNeighbourByGUID.TryGetValue(GUID, out result))
                return result;
            return null;
        }

        public List<InventoryItem> GetInventoryByNID(short ID)
        {
            List<InventoryItem> result = null;
            Neighborhood.InventoryByID.TryGetValue(ID, out result);
            return result;
        }

        public void SetInventoryForNID(short ID, List<InventoryItem> list)
        {
            Neighborhood.InventoryByID[ID] = list;
        }

        public short SetToNext(short current)
        {
            //short form: if we can find current, then we can get its entry index and add 1.
            var currentN = GetNeighborByID(current);
            if (currentN != null)
            {
                var id = currentN.RuntimeIndex + 1;
                return (short)((id == Neighbors.Entries.Count) ? -1 : Neighbors.Entries[id]?.NeighbourID ?? -1);
            }
            return (short)(Neighbors.Entries.FirstOrDefault(x => x.NeighbourID > current)?.NeighbourID ?? -1);
        }

        public short SetToNext(short current, uint guid)
        {
            IEnumerable<Neighbour> search = Neighbors.Entries;
            var firstNID = GetNeighborIDForGUID(guid);
            if (firstNID != null)
            {
                var firstN = GetNeighborByID(firstNID.Value);
                if (firstN != null)
                    search = search.Skip(firstN.RuntimeIndex);
            }
            return (short)(search.FirstOrDefault(x => x.NeighbourID > current && x.GUID == guid)?.NeighbourID ?? -1);
        }

        public void AvatarChanged(uint guid)
        {
            DirtyAvatars.Add(guid);
        }

        public bool SaveNeighbourhood(bool withSims)
        {
            //todo: save iffs for dirty avatars. 
            if (withSims)
            {
                foreach (var ava in DirtyAvatars)
                {
                    var obj = ContentManager.WorldObjects.Get(ava);
                    if (obj != null)
                    {
                        AtomicWrite(obj.Resource.Name, s => obj.Resource.MainIff.Write(s));
                    }
                }
                DirtyAvatars.Clear();
            }

            AtomicWrite(Path.Combine(UserPath, "Neighborhood.iff"), s => MainResource.Write(s));

            return true;
        }

        public bool SaveHouse(int houseID, IffFile file)
        {
            AtomicWrite(GetHousePath(houseID), s => file.Write(s));

            return true;
        }

        /// <summary>
        /// Patches one signed word of a house file's SIMI globals on disk: a targeted
        /// read-modify-write that rewrites only those two bytes and leaves the rest
        /// of the file byte-identical. This mirrors the native completion law's SIMI
        /// patch (reconstitute SIMI, SetGlobal, write the chunk back — r247 §C)
        /// because a whole-file IffFile.Write cannot round-trip a lazily opened
        /// house: chunk types without a serializer (HOUS/ARRY/THMB have no Write
        /// override) produce null data, and unregistered types (EXPi/uChr in the
        /// tutorial FAM-as-house) never even enter the chunk list. The SIMI payload
        /// layout is [pad:4][version:4][magic "IMIS":4][globals, i16 LE...], per the
        /// SIMI codec's Read.
        /// </summary>
        public bool PatchHouseSimiGlobal(int house, int globalIndex, short value)
        {
            try
            {
                var data = File.ReadAllBytes(GetHousePath(house));

                //chunk records start after the 60-byte identifier + 4-byte
                //resource-map offset; each is [type:4][size:u32 BE][id:2][flags:2]
                //[label:64], size inclusive, per IffFile.Read.
                var off = 64;
                while (off + 76 <= data.Length)
                {
                    var size = (data[off + 4] << 24) | (data[off + 5] << 16) | (data[off + 6] << 8) | data[off + 7];
                    if (size < 76 || off + size > data.Length) return false;
                    if (data[off] == 'S' && data[off + 1] == 'I' && data[off + 2] == 'M' && data[off + 3] == 'I')
                    {
                        var payload = off + 76;
                        //verify the SIMI body magic before touching anything
                        if (payload + 12 > data.Length
                            || data[payload + 8] != 'I' || data[payload + 9] != 'M'
                            || data[payload + 10] != 'I' || data[payload + 11] != 'S') return false;

                        var globalPos = payload + 12 + (globalIndex * 2);
                        if (globalPos + 2 > payload + (size - 76)) return false; //not present in this SIMI version

                        //write both bytes atomically for the file, LE i16
                        var bytes = new byte[2] { (byte)value, (byte)((value >> 8) & 0xff) };
                        using (var stream = new FileStream(GetHousePath(house), FileMode.Open, FileAccess.Write, FileShare.None))
                        {
                            stream.Seek(globalPos, SeekOrigin.Begin);
                            stream.Write(bytes, 0, 2);
                        }
                        return true;
                    }
                    off += size;
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public IffFile GetHouse(int id)
        {
            return new IffFile(Path.Combine(UserPath, "Houses/House"+id.ToString().PadLeft(2, '0')+".iff"));
        }

        public string GetHousePath(int id)
        {
            return Path.Combine(UserPath, "Houses/House" + id.ToString().PadLeft(2, '0') + ".iff");
        }

        public BMP GetHouseThumb(int id)
        {
            return GetHouse(id)?.Get<BMP>(512); //roof on
        }

        public short GetTATT(uint guid, int index)
        {
            short[] dat = null;
            if (TypeAttributes.TypeAttributesByGUID.TryGetValue(guid, out dat))
            {
                if (index >= dat.Length) return 0;
                else return dat[index];
            }
            return 0;
        }

        public Tuple<string, string> GetHouseNameDesc(int houseID)
        {
            STR res;
            if (houseID < 80) res = NeighbourhoodDesc.Get<STR>((ushort)(houseID + 2000));
            else if (houseID < 90) res = STDesc.Get<STR>((ushort)(houseID + 2000));
            else res = MTDesc.Get<STR>((ushort)(houseID + 2000));

            if (res == null) return new Tuple<string, string>("", "");
            else return new Tuple<string, string>(res.GetString(0), res.GetString(1));
        }

        public void SetTATT(uint guid, int index, short value)
        {
            short[] dat = null;
            if (!TypeAttributes.TypeAttributesByGUID.TryGetValue(guid, out dat))
            {
                var obj = ContentManager.WorldObjects.Get(guid);
                if (obj == null) return;
                dat = new short[32];
                TypeAttributes.TypeAttributesByGUID[guid] = dat;
            }
            if (index >= dat.Length) return;
            else dat[index] = value;
        }

        /// <summary>
        /// The neighborhood's tutorial completion state (native Neighborhood object
        /// field +0x12a, persisted as NGBH word 1; r247 decode.md §C). Generic TS1
        /// sim calls 0/8/9 write arg+1 (1 house, 2 family, 3 architecture complete);
        /// once it reaches 3 the LoadHouse tail tutorial spawner is permanently
        /// disabled for this neighborhood. Persisted via SaveNeighbourhood.
        /// </summary>
        public int TutorialState
        {
            get { return Neighborhood?.TutorialState ?? 0; }
            set { if (Neighborhood != null) Neighborhood.TutorialState = (short)value; }
        }

        /// <summary>
        /// The tutorial house latch (native Neighborhood object field +0x12c, NGBH
        /// word 2). Holds the house number an imported tutorial family moved into;
        /// every completion write clears it to 0.
        /// </summary>
        public short TutorialHouse
        {
            get { return Neighborhood?.TutorialHouse ?? (short)0; }
            set { if (Neighborhood != null) Neighborhood.TutorialHouse = value; }
        }

        /// <summary>
        /// Whether the given house's saved file is a tutorial house: its SIMI global
        /// 58 is nonzero (r247 GetHouseFileInfo law — HouseInfo+0x14, the move-in
        /// refusal's first guard). Reads Houses/HouseNN.iff off disk; the live
        /// simulator never sets global 58, so this re-derives the data-side flag
        /// exactly like the original's per-refresh GetHouseInfo.
        /// </summary>
        public bool IsTutorialHouse(short house)
        {
            if (house <= 0) return false;
            SIMI simi = null;
            try
            {
                simi = GetHouse(house)?.Get<SIMI>(1);
            }
            catch
            {
                //a missing/unreadable house file is simply not a tutorial house.
                return false;
            }
            var globals = simi?.GlobalData;
            return globals != null && globals.Length > 58 && globals[58] != 0;
        }

        /// <summary>
        /// The file action of the native ResetTutorial (r247 decode.md §B): copy
        /// the pristine &lt;UserPath&gt;Tutorial.FAM into &lt;UserPath&gt;Import/Tutorial.FAM,
        /// failing if the destination already exists, then clear its read-only
        /// attribute. The confirm dialog, save gate and neighborhood-screen
        /// navigation around it are the caller's (client) responsibility.
        /// </summary>
        /// <returns>true when the pristine file was staged for import.</returns>
        public bool StageTutorialReset()
        {
            var src = Path.Combine(UserPath, "Tutorial.FAM");
            var dst = Path.Combine(UserPath, "Import", "Tutorial.FAM");
            try
            {
                if (!File.Exists(src)) return false;
                if (File.Exists(dst)) return false; //CopyFileA fail-if-exists flag
                File.Copy(src, dst);

                var attr = File.GetAttributes(dst);
                if ((attr & FileAttributes.ReadOnly) != 0)
                    File.SetAttributes(dst, attr & ~FileAttributes.ReadOnly);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // ------------------------------------------------------------------
        // R248: the family import (r247-fam-import decode.md + skeptic-corrections.md)
        // ------------------------------------------------------------------

        /// <summary>
        /// The human template neighbor GUID the native AddNewCharacter dispatches on
        /// for kind 0 (decode §3: 0 → 0x7fd96b54, 1 → dog 0x4a70df92, 2 → cat
        /// 0x7bea0977). Same constant as SimitoneNeighbourGenerator.TEMPLATE_GUID,
        /// which lives in FSO.SimAntics (this project cannot reference it).
        /// </summary>
        private static readonly uint ImportTemplatePersonGUID = 0x7FD96B54;

        // Native Neighborhood::ImportFamily return law (decode §5): 0 = success,
        // negative = structural refusal, -50 = the house-file move failure.
        private const int IMPORT_OK = 0;

        /// <summary>
        /// The neighborhood-screen import poll (r247-fam-import decode §1.2, C5: the
        /// one-arg scan's *.FAM wildcard is part of the path; FileExists gate on
        /// Import/Tutorial.FAM is what triggers the auto-import). Returns the poll
        /// tri-state the client contract maps (TutorialEngine247.ImportPollResult):
        /// 0 = nothing — including the native's SILENT GetImportInfoForFile failure
        /// (decode §1.2 shows no dialog on that branch; only an ImportFamily failure
        /// raises "Couldn't import the family"/"Error", which the client surfaces on
        /// the 2/Error result), 1 = imported, 2 = import refused.
        /// </summary>
        public int CheckForNewImports()
        {
            var importDir = Path.Combine(UserPath, "Import");
            string[] fams;
            try
            {
                fams = Directory.GetFiles(importDir, "*.FAM");
            }
            catch (Exception)
            {
                return 0; //no Import/ directory is simply "nothing staged"
            }
            if (fams.Length == 0) return 0; //import-UI toggle stays client-side (model+0x160)

            // Generic import (SAV-05 / r253-import-export port-audit §3.1): the
            // literal "Import/Tutorial.FAM" latch is removed. Import ONE valid
            // *.FAM per poll, in scan order, keeping the poll bounded/deterministic;
            // the rest are handled on subsequent (one-per-second) polls. A tutorial
            // FAM is no longer special here: it is a generic FAM whose SIMI global
            // 26 still drives the step-20 tutorial latch (unchanged). Invalid /
            // incomplete .FAM files are skipped silently and stay on disk, matching
            // the native's silent GetImportInfoForFile failure (decode §1.2).
            foreach (var famPath in fams)
            {
                //GetImportInfoForFile equivalent (decode §2.2: open + EXPi +
                //LoadFamily reads) — a failure here is a SILENT skip.
                if (!ValidateImportFile(famPath)) continue;
                var rc = ImportFamFile(famPath, out _);
                return (rc == IMPORT_OK) ? 1 : 2; //import one (or refuse) per poll
            }
            return 0;
        }

        /// <summary>
        /// The house number the last SUCCESSFUL import moved into; 0 after a
        /// refusal, a family-only import, or no import this session. The client's
        /// post-import screen-refresh gate: the native sets reloadScreen only when
        /// the imported family has house chunks AND a nonzero house (decode §1.2:
        /// ii.+0x110 != 0 &amp;&amp; ii.+0x150 != 0) — a family-only import must
        /// NOT rebuild the neighborhood screen.
        /// </summary>
        public int LastImportHouse;

        /// <summary>
        /// GetImportInfoForFile's read set (steps 1-5 of the import law): the file
        /// must open as an IFF and carry the EXPi index and the FAMI it names.
        /// </summary>
        private bool ValidateImportFile(string famPath)
        {
            try
            {
                var famIff = new IffFile(famPath);
                var expi = famIff.List<EXPi>()?.FirstOrDefault();
                if (expi == null) return false;
                if (famIff.Get<FAMI>((ushort)expi.FamilyID) == null) return false;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// The corrected 25-step family import law (tools/iff-dump/r247-fam-import
        /// decode.md §3, as amended by skeptic-corrections.md — the step numbers in
        /// the comments are the skeptic's §7 verified operation order). Adaptations
        /// against the port's data model are documented per step; every deviation is
        /// marked ADAPT. Returns 0 on success, -1 on a structural guard (all of
        /// them pre-mutation except the step-16 Gtab splice — the guard block
        /// lists them exactly), -50 when the FAM-over-house file move fails
        /// (rollback included).
        /// </summary>
        public int ImportFamFile(string famPath, out int newFamilyId)
        {
            newFamilyId = -1;
            LastImportHouse = 0; //a refusal leaves the refresh gate closed

            // --- steps 1-7: reads. Refusal-order law (R248 reconciliation): every
            // refusal EXCEPT the step-16 Gtab splice fires BEFORE any mutation.
            // Pre-mutation, in order: (1) IFF open, (2) 'EXPi', (3) 'FAMI',
            // (4) display name, (5) 'NBRS', (6) house-file presence, (7) id-0
            // occupancy, (8) multi-claimant, (9) template-person presence. The
            // splice refusal alone is LATE — the native fires it after member
            // creation too, and the table carries the freshly created GUIDs, so
            // hoisting it is not provably equivalent. Skeptic C3 notes the
            // native leaks model mutations on any refusal — a post-Save-
            // unobservable state no fixture can observe, disclosed here rather
            // than replicated. With the atomic splice (WriteGtabIntoFamFile) the
            // late refusal leaves NO disk corruption: the staged
            // Import/Tutorial.FAM stays byte-identical, so the leak is model-only
            // (mirroring the native's own C3 leak) and the poll's retry re-runs
            // the import from the intact file — see the splice call below. ---
            IffFile famIff;
            try
            {
                famIff = new IffFile(famPath); //step 1: open + ValidFile
            }
            catch (Exception)
            {
                return -1;
            }

            var expi = famIff.List<EXPi>()?.FirstOrDefault(); //step 2: 'EXPi'
            if (expi == null) return -1;
            var memberIDs = expi.ActiveMemberIDs; //up to 8, 0-terminated

            var importFami = famIff.Get<FAMI>((ushort)expi.FamilyID); //step 3: LoadFamily ('FAMI')
            if (importFami == null) return -1;

            //step 4: display name — FAMs string 1 (native 1-based; port STR index 0),
            //with the CTSS version-1000 string-1 override when the FAM carries one
            //(Tutorial.FAM does not — the FAM's CTSS chunks are the per-member sets).
            var famName = famIff.Get<FAMs>((ushort)expi.FamilyID)?.GetString(0);
            var nameOverride = famIff.Get<CTSS>(1000)?.GetString(0);
            if (nameOverride != null) famName = nameOverride;
            if (famName == null) return -1;

            var famNbrs = famIff.List<NBRS>()?.FirstOrDefault(); //step 5: 'NBRS' v1 temp records
            if (famNbrs == null) return -1;

            var finv = famIff.List<FINV>()?.FirstOrDefault(); //step 6: 'FINV' v2 (positional)

            //step 7: the tutorial latch — 'SIMI' global 26 != 0 (skeptic C2: the same
            //field the port's SIMI codec reads for LoadHouse; Tutorial.FAM g26 = 1).
            bool tutFlag = false;
            var famSimi = famIff.Get<SIMI>(1);
            if (famSimi?.GlobalData != null && famSimi.GlobalData.Length > 26)
                tutFlag = famSimi.GlobalData[26] != 0;

            var famiChunks = MainResource.List<FAMI>();
            if (famiChunks == null) famiChunks = new List<FAMI>();

            // --- guard block (native sites in decode §5, moved pre-mutation) ---
            //family id law (0xadedc..0xadf2c): keep famId when free in the bin,
            //otherwise 0 — there is NO replace-on-collision.
            ushort wantedId = (ushort)expi.FamilyID;
            ushort newId = famiChunks.Any(x => x != null && x.ChunkID == wantedId)
                ? (ushort)0 : wantedId;

            var house = (ushort)Math.Max(0, importFami.HouseNumber); //FAMI house number

            //guard (house-only): the lot-table entry must exist with flag 1
            //(0xae8a4..0xae8f0). The port's lot table has no flag consumer; the same
            //observable is the house file existing on disk — it is about to be
            //replaced by the FAM.
            if (house != 0 && !File.Exists(GetHousePath(house))) return -1;

            //guard (family-only): another family already holds id 0 (0xae908). In the
            //port's id-keyed chunk store id 0 is the Default sentinel shell; a REAL
            //(member-carrying) id-0 family means the slot is genuinely occupied.
            if (house == 0 && newId == 0)
            {
                var zero = MainResource.Get<FAMI>(0);
                if (zero != null && zero.FamilyGUIDs.Length > 0) return -1;
            }

            //guard (house): a family still claims the house after eviction
            //(0xae920..0xae99c). The port can hold at most one claimant per house
            //(FamilyForHouse is keyed by house; the sweep below catches raw FAMI
            //anomalies), so this is decidable before any mutation: more than one
            //FAMI claiming the house, or a claimant other than the one that will be
            //evicted, refuses.
            if (house != 0)
            {
                var claimants = famiChunks.Where(x => x != null && x.HouseNumber == house).ToList();
                if (claimants.Count > 1) return -1;
            }

            //guard (AddNewCharacter, 0xae260): the template neighbor must resolve.
            var objs = (TS1ObjectProvider)ContentManager.WorldObjects;
            if (objs.Get(ImportTemplatePersonGUID) == null) return -1;

            // --- step 12: eviction (0xadff0..0xae0c0). house == 0 evicts "the first
            //bin family with id 0" — the Default sentinel shell, a no-op the port's
            //sentinel handling covers — so eviction only runs for a house import. ---
            var evicted = (house != 0)
                ? famiChunks.FirstOrDefault(x => x != null && x.HouseNumber == house)
                : null;
            if (evicted != null)
            {
                //ADAPT: the native sets the money pair (+0x118/+0x11c) to
                //(GetNetWorth, 0). The port's net worth is budget + value-in-arch.
                evicted.HouseNumber = 0; //loses the house
                evicted.Budget += evicted.ValueInArch;
                evicted.ValueInArch = 0;
                evicted.Unknown &= ~8; //clear flag bit 28 (value 8)

                // --- step 13: delete the house's current characters (0xae0c4..0xae110).
                //ADAPT: the port's Neighbour records carry no house field; residency
                //is the occupying family's membership, so the sweep deletes the
                //evicted family's members. ---
                FamilyForHouse.Remove((short)house);
                foreach (var guid in evicted.FamilyGUIDs)
                {
                    DeleteImportCharacter(guid);
                }
            }

            // --- steps 8-11: family creation, now that every refusal is behind us.
            //ADAPT: the native's bin is an array that could carry a second id-0
            //family; the port's chunk store is id-keyed, so a colliding import
            //claims the id-0 Default sentinel chunk IN PLACE (fields reset below).
            var newFami = MainResource.Get<FAMI>(newId);
            if (newFami != null)
            {
                newFami.FamilyGUIDs = new uint[0];
                newFami.FamilyFriends = 0;
                newFami.Unknown = 0;
                newFami.HouseNumber = 0;
            }
            else
            {
                newFami = new FAMI()
                {
                    ChunkLabel = "",
                    ChunkID = newId,
                    ChunkProcessed = true,
                    ChunkType = "FAMI",
                    ChunkParent = MainResource,
                    AddedByPatch = true,
                    FamilyGUIDs = new uint[0],
                };
                MainResource.AddChunk(newFami);
            }
            newFami.Budget = importFami.Budget;         //step 10: the money pair
            newFami.ValueInArch = importFami.ValueInArch;
            var realFamilies = famiChunks.Where(x => x != null).ToList();
            newFami.FamilyNumber = (short)((realFamilies.Count > 0)
                ? realFamilies.Max(x => x.FamilyNumber) + 1 : 1); //UpdateFamilyNumbers equivalent

            SetImportFamilyName(newId, famName); //step 9: FAMs name (CTSS-1000 override applied above)

            // --- step 14: member creation loop (0xae114..0xae608) ---
            var created = new List<KeyValuePair<short, Neighbour>>(); //(old FAM id, live neighbor)
            var memberGuids = new List<uint>();
            int finvCount = finv?.Inventories.Count ?? 0;

            for (int i = 0; i < memberIDs.Length; i++)
            {
                var pid = memberIDs[i];
                Neighbour famRecord = null;
                famNbrs.NeighbourByID.TryGetValue(pid, out famRecord);

                //kind discriminator: uChr string 13 — "dog"/"cat" or human-else.
                var uchr = famIff.Get<uChr>((ushort)pid);
                var kind = uchr?.GetString(13)?.Trim().ToLowerInvariant();
                if (kind == "dog" || kind == "cat")
                {
                    //Pets are OUT OF SCOPE for TS1 (the port has no pet machinery):
                    //the pets-off law applies (native member loop, pets-disabled
                    //quirk) — the pet member is skipped AND the previously created
                    //member is removed from the family (RemoveFromFamily(previous),
                    //skeptic §3). The record/file of the previous member stay; only
                    //family membership is lost. Disclosed: unreachable for
                    //Tutorial.FAM (both members are human, string 13 = "27").
                    if (memberGuids.Count > 0) memberGuids.RemoveAt(memberGuids.Count - 1);
                    continue;
                }

                var guid = GenerateImportGUID();
                var userid = PrepareImportTemplatePerson(famIff, pid, uchr, guid);
                var neighbour = ImportAddNeighbor(guid, famRecord, newId, userid);

                //FINV inventory carry (positional): re-key entry i onto the fresh
                //NeighbourID, replacing any live inventory for it (decode §3).
                if (finv != null && i < finvCount)
                {
                    Neighborhood.InventoryByID[neighbour.NeighbourID] =
                        finv.Inventories[i].Select(x => x.Clone()).ToList();
                }

                created.Add(new KeyValuePair<short, Neighbour>(pid, neighbour));
                memberGuids.Add(guid);
            }
            newFami.FamilyGUIDs = memberGuids.ToArray(); //newFam.AddMember per member

            // --- step 15: GUID translation + RelMatrix carry (skeptic correction C1,
            //0xae6dc..0xae78c): the Gtab gains an old→new pair per member, and each
            //live neighbor receives EVERY imported member's relationship row — the
            //source row indexed by the OLD FAM id, the destination by the NEW id,
            //with row removal when the source row count is 0. The port's live
            //consumer is Neighbour.Relationships (VMRelationship's TS1 neighbor
            //matrix + VMMemory's mutual friend count), so this is live state, not
            //just file-level. ---
            var gtab = famIff.List<Gtab>()?.FirstOrDefault() ?? new Gtab();
            foreach (var member in created)
            {
                Neighbour rec;
                famNbrs.NeighbourByID.TryGetValue(member.Key, out rec);
                var live = member.Value;
                if (rec == null || live == null) continue; //native "if not rec or not live"

                gtab.Pairs.Add(new GtabPair() { OldGUID = rec.GUID, NewGUID = live.GUID });

                for (int j = 0; j < created.Count; j++)
                {
                    var oldId = created[j].Key;
                    var newNid = created[j].Value.NeighbourID;
                    List<short> row = null;
                    if (rec.Relationships != null)
                        rec.Relationships.TryGetValue(oldId, out row);
                    if (row != null && row.Count > 0)
                        live.Relationships[newNid] = new List<short>(row);
                    else
                        live.Relationships.Remove(newNid);
                }
                //persistent s16 fields (+0x74+2*id) were carried at creation time as
                //the FAM record's PersonData (the port's model of that block).
            }

            // --- step 16: persist the Gtab into the FAM BEFORE the move ("The 'Gtab'
            //GUID map is written into it before the move"). ADAPT: the port cannot
            //re-serialize the whole lazily-parsed FAM (PatchHouseSimiGlobal's doc
            //records why), so the chunk is spliced at the raw-byte level; a previous
            //Gtab is removed and the new one appended (chunk order is not
            //semantically meaningful in IFF). ---
            if (house != 0)
            {
                //The ONE post-mutation refusal (native: Save rc → refusal). The
                //members already created stay in the model when it fires — the
                //model-only leak disclosed at the guard block. The atomic splice
                //keeps the staged file intact, so the 1 s poll retries the import
                //from the pristine bytes; each retry creates one more member pair
                //and the previous attempt's pair is orphaned in the model (no
                //family references it) — a later attempt's step-24 save would
                //persist it. Still no disk corruption in any failure case.
                if (!WriteGtabIntoFamFile(famPath, gtab)) return -1;
            }

            //UpdateFamilyFriendsCount equivalent: mutual friends (rel[0] >= 50 both
            //ways — VMMemory's friend law) summed over the imported members.
            int friends = 0;
            foreach (var member in created)
            {
                foreach (var rel in member.Value.Relationships)
                {
                    if (rel.Value.Count == 0 || rel.Value[0] < 50) continue;
                    var other = GetNeighborByID((short)rel.Key);
                    if (other?.Relationships == null) continue;
                    List<short> back;
                    if (other.Relationships.TryGetValue(member.Value.NeighbourID, out back)
                        && back.Count > 0 && back[0] >= 50)
                    {
                        friends++;
                    }
                }
            }
            newFami.FamilyFriends = friends;

            // --- steps 17-19: occupancy bookkeeping (0xae8a4..0xaea28). The
            //structural guards already fired pre-mutation. ---
            if (house != 0)
            {
                newFami.HouseNumber = house;               //newFam->+0x110
                newFami.Unknown = (newFami.Unknown & ~8) | 4; //set bit29 (4), clear bit28 (8)
                FamilyForHouse[(short)house] = newFami;
            }
            else
            {
                newFami.HouseNumber = 0;
                newFami.Unknown &= ~4; //clear bit29
            }
            //FAMI flags → new family: the decode's "bit31 (loaded fam +0x138 bit0)"
            //reads bit 0 of the loaded flags word (Tutorial.FAM word = 17 → set).
            if ((importFami.Unknown & 1) != 0) newFami.Unknown |= 1;

            // --- step 20: tutorial latch (0xaea48..0xaeaec; r247 §C field law) ---
            if (tutFlag)
            {
                TutorialHouse = (short)house;          //+0x12c = house
                Neighborhood.NeighborhoodData[3] = 0;  //+0x12e = 0
                //The native's ObjectFolder selector type-attr sweep is native-inert
                //here: the tested bit ((s16)(sel->+0x60)->+0xb6 & 0x2) is UNRESOLVED
                //in the decode and no observable is pinned, so the port implements
                //no equivalent.
            }
            else if (TutorialHouse == (short)house)
            {
                TutorialHouse = 0; //non-tutorial re-import over the same house
            }

            newFamilyId = newId;
            //ADAPT (0xfa0): the native skips the out-id write AND the Export refresh
            //for the reserved id 4000; the port's out param is always written
            //(unobservable through this call path — CheckForNewImports ignores it),
            //and the Export refresh is skipped for 0xfa0 exactly.

            // --- steps 21-23: consume the Import/ file (0xaeaf4..0xaecac). The
            //.tmp rename/move/delete dance with rollback; failure → -50. ---
            if (house != 0)
            {
                var housePath = GetHousePath(house);
                var tmpPath = housePath + ".tmp";
                try
                {
                    File.Move(housePath, tmpPath);
                }
                catch (Exception)
                {
                    return -50;
                }
                try
                {
                    File.Move(famPath, housePath); //THE FAM BECOMES THE HOUSE FILE
                }
                catch (Exception)
                {
                    try { File.Move(tmpPath, housePath); } catch (Exception) { } //rollback
                    return -50;
                }
                try { File.Delete(tmpPath); } catch (Exception) { }
            }
            else
            {
                //family-only import: consume. The native's DeleteFileA result is
                //unchecked (no -50 branch decoded); a stranded file would re-trigger
                //the poll, exactly like the original.
                try { File.Delete(famPath); } catch (Exception) { }
            }

            // --- step 24: the immediate neighborhood save (Save__12NeighborhoodFl).
            //ADAPT: withSims false — the import already wrote/deleted the character
            //files it touched and no avatar is dirty on the neighborhood screen. ---
            SaveNeighbourhood(false);

            // --- step 25: portrait regeneration — N/A (ADAPT): the port renders
            //neighborhood cards from live data; there is no PersonFinder::
            //GenerateBitmaps bitmap cache to refresh. The Export/ mirror refresh of
            //the free wrapper (imported + evicted families, skip id 4000) is
            //UNIMPLEMENTED: nothing in the port writes or reads Export/ — a
            //divergence on disk, disclosed at CheckForNewImports.
            LastImportHouse = house; //reloadScreen gate for the client (decode §1.2)
            return IMPORT_OK;
        }

        /// <summary>
        /// The fresh character-file factory (steps 13-14; native AddNewCharacter
        /// 0xb3770 + the member-tail selector writes). Mirrors the proven
        /// SimitoneNeighbourGenerator.PrepareTemplatePerson path (CAS + make-new-
        /// character canon; the generator lives in FSO.SimAntics, this project
        /// cannot reference it): the template person object is renamed/re-GUIDed,
        /// its CTSS 2000 carries the member name/bio, its STR# 200 is replaced with
        /// the uChr clone (the native SetInfo(sel, 200, 'STR#') law), and the file
        /// is saved as the next Characters/User#####.iff. Returns the userid used.
        /// ADAPT (name source): the native reads the catalog pair as CTSS strings
        /// 1/2 (1-based); in the port's STR model that is string 0's value/comment.
        /// </summary>
        private int PrepareImportTemplatePerson(IffFile famIff, short pid, uChr uchr, uint guid)
        {
            var userid = NextSim;
            var tempObj = ContentManager.WorldObjects.Get(ImportTemplatePersonGUID);

            //name/bio: the FAM's CTSS at the member id; generator-style fallbacks.
            //Native two-step (decode §3 step 13): read CTSS pid+2000 first (the
            //per-member catalog pair), then fall back to CatalogResource::Load
            //at the plain pid when that read fails. Tutorial.FAM carries no CTSS
            //at pid+2000 — the skeptic confirmed the native's own +2000 read
            //fails on it and the same pid chunk supplies the data — so the
            //two-step is faithful and changes nothing for the shipped file.
            var memberCtss = famIff.Get<CTSS>((ushort)(pid + 2000))
                ?? famIff.Get<CTSS>((ushort)pid);
            var name = memberCtss?.GetString(0);
            var bio = memberCtss?.GetComment(0) ?? "";
            if (string.IsNullOrEmpty(name))
            {
                //native fallback when the CTSS catalog is missing: the plain catalog
                //load — the port has no other text source, so the file name stands in.
                name = "user" + userid.ToString().PadLeft(5, '0');
                bio = "";
            }

            tempObj.OBJ.ChunkParent.RetainChunkData = true;
            tempObj.OBJ.GUID = guid;
            //native rename law: "<shortname> - <name>"
            tempObj.OBJ.ChunkLabel = "user" + userid.ToString().PadLeft(5, '0') + " - " + name;

            var ctss = tempObj.Resource.Get<CTSS>(2000);
            if (ctss == null)
            {
                ctss = new CTSS()
                {
                    ChunkLabel = "",
                    ChunkID = 2000,
                    ChunkProcessed = true,
                    ChunkType = "CTSS",
                    ChunkParent = tempObj.Resource.MainIff,
                    AddedByPatch = true,
                };
                tempObj.Resource.MainIff.AddChunk(ctss);
                ctss.InsertString(0, new STRItem() { Value = "", Comment = "" });
                ctss.InsertString(0, new STRItem() { Value = "", Comment = "" });
            }
            ctss.SetString(0, name);
            ctss.SetString(1, bio);
            tempObj.OBJ.CatalogStringsID = 2000;

            //STR# 200 := the uChr clone (whole-resource replacement; indices beyond
            //the uChr set are blanked to keep the clone exact). The template's
            //strings are snapshotted and fully restored after the save — this path
            //mutates more slots (HAND groups, 3-6) than the generator's fixed
            //replacement set, so its strings-1/2-only reset would leak them into
            //the next CAS-created character. The snapshot must be a DEEP clone
            //(R248 P0): STR.SetString writes through the STRItem references, so a
            //bare array clone leaves the "snapshot" aliasing the very items the
            //uChr loop overwrites — the restore would reassign already-mutated
            //objects and every slot outside the replace set would permanently
            //hold imported text on this SHARED template object.
            var bodyStrings = tempObj.Resource.Get<STR>(200);
            STRItem[] templateStrings = null;
            if (bodyStrings != null)
            {
                templateStrings = bodyStrings.LanguageSets[0].Strings
                    .Select(x => new STRItem()
                    {
                        LanguageCode = x.LanguageCode,
                        Value = x.Value,
                        Comment = x.Comment
                    }).ToArray();
                var uchrLen = uchr?.Length ?? 0;
                var total = Math.Max(bodyStrings.Length, uchrLen);
                for (int i = 0; i < total; i++)
                {
                    var value = (uchr != null && i < uchrLen) ? (uchr.GetString(i) ?? "") : "";
                    if (i < bodyStrings.Length)
                    {
                        bodyStrings.SetString(i, value);
                    }
                    else
                    {
                        bodyStrings.InsertString(i, new STRItem() { Value = value, Comment = "" });
                    }
                }
            }

            SaveNewNeighbour(tempObj); //writes Characters/User#####.iff, NextSim++

            //restore the template's bodystrings wholesale (supersedes the
            //generator's strings-1/2-only reset — see snapshot above). The
            //snapshot holds private copies of every item, so this reassignment
            //genuinely repairs the slots the uChr loop mutated in place.
            if (bodyStrings != null && templateStrings != null)
            {
                bodyStrings.LanguageSets[0].Strings = templateStrings;
            }
            return userid;
        }

        /// <summary>
        /// Registers the recreated character in the neighborhood (native
        /// AddNewNeighbor): lowest-free NeighbourID, fresh GUID, the FAM record's
        /// PersonData as the persistent s16 fields (re-keyed: pd[31] NeighborId =
        /// the fresh id, pd[61] family = the imported family's id — the port model
        /// of the +0x74+2*id persistent block), generator person mode 9 (the port's
        /// created-playable-sim canon; the FAM record's own mode field is not the
        /// port's PersonMode source).
        /// </summary>
        private Neighbour ImportAddNeighbor(uint guid, Neighbour famRecord, ushort familyId, int userid)
        {
            var ns = Neighbors.Entries;
            short newID = 1;
            for (int i = 0; i < ns.Count; i++)
            {
                if (ns[i].NeighbourID == newID) newID++;
                else if (ns[i].NeighbourID < newID) continue;
                else break;
            }

            short[] personData;
            if (famRecord?.PersonData != null)
            {
                personData = (short[])famRecord.PersonData.Clone();
            }
            else if (PreparePersonDataFromObject != null)
            {
                personData = PreparePersonDataFromObject(ImportTemplatePersonGUID) ?? new short[88];
            }
            else
            {
                personData = new short[88];
            }
            if (personData.Length < 88)
            {
                Array.Resize(ref personData, 88);
            }
            personData[31] = newID;      //VMPersonDataVariable.NeighborId
            personData[61] = (short)familyId; //VMPersonDataVariable.FamilyID

            // R252 DISCLOSED RESIDUAL — NOT changed this round. ImportAddNeighbor
            // (the family-import / FAM path, r247-fam-import) is a REMAINING
            // NON-original writer: the constructed Neighbour below is left with the
            // class defaults (Version=0xA, Unknown3=9) and PersonMode=9, so Save
            // emits the LEGACY port record shape — Version 0xA with a 0x200-byte
            // (256-short) PersonData block and PersonMode 9. That diverges from the
            // byte-faithful ORIGINAL created-record format (Version 0x4, 0xa0-byte
            // PersonData, PersonMode 5) that R252 CAS-created records now use.
            // Deleting the family/import flow from this path is separate and risky
            // (the FAM round-trip is exercised elsewhere), so it is documented here
            // and in r252-record-format/review-fixes.md §residuals rather than
            // "fixed" blind. Do NOT touch Version/PersonMode here without first
            // auditing the import round-trip.
            var newN = new Neighbour()
            {
                Name = "user" + userid.ToString().PadLeft(5, '0'),
                NeighbourID = newID,
                GUID = guid,
                PersonMode = 9,
                PersonData = personData,
                Relationships = new Dictionary<int, List<short>>(),
            };
            Neighbors.AddNeighbor(newN);
            return newN;
        }

        /// <summary>
        /// The character deletion sweep (step 13; native DeleteCharacter 0xb2510):
        /// removes the Neighbor record, the character's world registration, and its
        /// inventory entry. ADAPT (disclosed): the on-disk User#####.iff is deleted
        /// too — whether the 356-byte native DeleteCharacter unlinks the file was
        /// not decodable, but keeping it would let the port's boot re-scan
        /// (LoadCharacters + AddMissingNeighbors) resurrect the deleted character
        /// as a townie on the next session. Only runs inside the (isolated) UserPath.
        /// </summary>
        private void DeleteImportCharacter(uint guid)
        {
            var nid = GetNeighborIDForGUID(guid);
            if (nid.HasValue)
            {
                Neighbors.RemoveNeighbor(GetNeighborByID(nid.Value));
                Neighborhood.InventoryByID.Remove(nid.Value);
            }

            var objs = (TS1ObjectProvider)ContentManager.WorldObjects;
            GameObjectReference reference;
            if (objs.Entries.TryGetValue(guid, out reference))
            {
                var file = reference.FileName;
                objs.RemoveObject(guid);
                objs.PersonGUIDs.Remove(guid);
                //containment guard (R248 P1-2): only ever unlink inside the
                //port's own UserPath — a reference pointing elsewhere (stale
                //registration over a foreign path) is skipped and logged, never
                //deleted. UserPath carries the trailing separator, so a sibling
                //directory whose name merely prefixes it cannot pass.
                if (!string.IsNullOrEmpty(file)
                    && file.StartsWith(UserPath, StringComparison.OrdinalIgnoreCase)
                    && File.Exists(file))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception)
                    {
                        //an undeletable file degrades to the resurrect-on-reboot behavior
                        //documented above; the import itself still succeeds.
                    }
                }
                else
                {
                    Console.WriteLine("[TS1NeighborhoodProvider] DeleteImportCharacter: skipped "
                        + "character file outside UserPath: '" + file + "' (guid "
                        + guid.ToString("X8") + ")");
                }
            }
        }

        /// <summary>
        /// Fresh GUID law (GenerateGUID canon): random, unique against loaded
        /// objects and existing neighbor GUIDs.
        /// </summary>
        private uint GenerateImportGUID()
        {
            var objs = (TS1ObjectProvider)ContentManager.WorldObjects;
            lock (objs.Entries)
            {
                var rand = new Random();
                var guid = (uint)rand.Next();
                while (objs.Entries.ContainsKey(guid)
                    || Neighbors.DefaultNeighbourByGUID.ContainsKey(guid))
                {
                    guid = (uint)rand.Next();
                }
                return guid;
            }
        }

        /// <summary>
        /// Creates or retitles the FAMs chunk carrying the imported family's display
        /// name (the CreateFamily FAMs pattern).
        /// </summary>
        private void SetImportFamilyName(ushort id, string name)
        {
            var fams = MainResource.Get<FAMs>(id);
            if (fams == null)
            {
                fams = new FAMs()
                {
                    ChunkLabel = "",
                    ChunkID = id,
                    ChunkProcessed = true,
                    ChunkType = "FAMs",
                    ChunkParent = MainResource,
                    AddedByPatch = true,
                };
                fams.InsertString(0, new STRItem() { Comment = "", Value = name });
                MainResource.AddChunk(fams);
            }
            else if (fams.Length == 0)
            {
                fams.InsertString(0, new STRItem() { Comment = "", Value = name });
            }
            else
            {
                fams.SetString(0, name);
            }
        }

        /// <summary>
        /// Step 16: splices the Gtab chunk into the FAM file at the raw-byte level
        /// (records are [type:4][size:u32 BE][id:2][flags:2][label:64] from byte 64,
        /// sizes inclusive — the same walk PatchHouseSimiGlobal uses). An existing
        /// Gtab is removed first so re-imports cannot accumulate duplicates. The
        /// result is written to a temp file beside the target and atomically
        /// renamed over it (R248 P1-1): any failure leaves the staged
        /// Import/Tutorial.FAM byte-identical, so the poll can retry it instead of
        /// silently skipping a truncated file forever.
        /// </summary>
        private bool WriteGtabIntoFamFile(string famPath, Gtab gtab)
        {
            var tmpPath = famPath + ".gtab-tmp";
            try
            {
                byte[] payload;
                using (var ms = new MemoryStream())
                {
                    gtab.Write(null, ms);
                    payload = ms.ToArray();
                }

                var data = File.ReadAllBytes(famPath);

                //splice out any existing Gtab
                var off = 64;
                while (off + 76 <= data.Length)
                {
                    var size = (data[off + 4] << 24) | (data[off + 5] << 16) | (data[off + 6] << 8) | data[off + 7];
                    if (size < 76 || off + size > data.Length) break;
                    if (data[off] == 'G' && data[off + 1] == 't' && data[off + 2] == 'a' && data[off + 3] == 'b')
                    {
                        var shrunk = new byte[data.Length - size];
                        Array.Copy(data, 0, shrunk, 0, off);
                        Array.Copy(data, off + size, shrunk, off, data.Length - off - size);
                        data = shrunk;
                        break; //offsets after the splice are stale; one Gtab per file
                    }
                    off += size;
                }

                //append the new chunk record
                var chunk = new byte[76 + payload.Length];
                chunk[0] = (byte)'G'; chunk[1] = (byte)'t'; chunk[2] = (byte)'a'; chunk[3] = (byte)'b';
                var sizeBe = 76 + payload.Length;
                chunk[4] = (byte)(sizeBe >> 24); chunk[5] = (byte)(sizeBe >> 16);
                chunk[6] = (byte)(sizeBe >> 8); chunk[7] = (byte)sizeBe;
                chunk[11] = 0x10; //flags, matching the shipped chunk convention
                Array.Copy(payload, 0, chunk, 76, payload.Length);

                var merged = new byte[data.Length + chunk.Length];
                Array.Copy(data, merged, data.Length);
                Array.Copy(chunk, 0, merged, data.Length, chunk.Length);
                //atomic publish: temp write + replace, never an in-place
                //truncating write of the staged file
                File.WriteAllBytes(tmpPath, merged);
                File.Replace(tmpPath, famPath, null);
                return true;
            }
            catch (Exception)
            {
                try { File.Delete(tmpPath); } catch (Exception) { }
                return false;
            }
        }
    }

    public class TS1GameState
    {
        public FAMI ActiveFamily;
        public uint DowntownSimGUID;
        public short LotTransitInfo;
    }
}
