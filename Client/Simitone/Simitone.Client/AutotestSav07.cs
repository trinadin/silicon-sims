/*
This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
If a copy of the MPL was not distributed with this file, You can obtain one at
http://mozilla.org/MPL/2.0/.
*/
using FSO.Client;
using FSO.Common;
using FSO.Common.Utils;
using FSO.Content;
using FSO.Content.TS1;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Simitone.Client
{
    /// <summary>
    /// SAV-07 'sav07live' + 'sav07live2' (opt-in, focused) — the live export→
    /// import→save→fresh-load round-trip the SAV-05 impexport battery excluded
    /// (its header: "the export→re-import round-trip is intentionally OUT OF
    /// SCOPE (SAV-06's export tranche)"). Runs on the real bootstrap at the
    /// neighborhood screen via the same R247/R246 isolation idiom
    /// (BeginIsolation redirects FSOEnvironment.UserDir BEFORE Content.Init),
    /// but with a DETERMINISTIC isolation directory so run 2 is a genuine
    /// fresh-process reload of run 1's saved state (InitSpecific clones the
    /// pristine UserData copy-on-missing; an existing UserData is mounted as-is).
    ///
    /// RUN 1 (sav07live; the isolation dir is deleted first): seed the live
    /// template family 1 "Newbie" (FAMI cid=1, members nid 47 'user00001' /
    /// nid 48 'user00000', mutual rel rows) with a distinctive relationship row
    /// and two inventory items on nid 47, drive the production ExportFamily,
    /// verify the written .FAM (engine parse), raw-byte patch the staged copy's
    /// FAMI house to 5 and let the production import poll take it back:
    /// collision id → 0 sentinel claim, eviction of the house-5 family (Goth,
    /// cid=5, nids 32/33/34), member recreation (first-free NIDs 1/2, fresh
    /// GUIDs), FINV re-key onto the fresh NeighbourID, relationship re-key,
    /// Gtab old→new pairs spliced into House05.iff (the FAM BECOMES the house
    /// file), and the step-24 immediate neighborhood save. Closes with a
    /// fresh-parse of the SAVED Neighborhood.iff (file-level persistence proof).
    ///
    /// RUN 2 (sav07live2; the isolation dir is KEPT): fresh process, same dir —
    /// asserts the imported family identity, inventories (NGBH tail), re-keyed
    /// relationship rows and the House05.iff Gtab all survived the reload.
    /// Expected identity is content-derived (template UserData is static) and
    /// independently cross-checked offline with
    /// coordination/evidence/SAV-07/tools/fam_dump.py over the retained copies
    /// the probe keeps beside the isolation UserData.
    /// </summary>
    public class AutotestSav07
    {
        private Action<string> _log;
        private int _phase, _phaseTicks;
        private bool _done, _passed = true;
        private readonly List<string> _notes = new List<string>();
        private readonly List<string> _fails = new List<string>();
        private TS1NeighborhoodProvider N;

        // Deterministic isolation: run 1 resets it, run 2 mounts it as-is.
        internal static string RedirectDir;

        // content-derived constants (template UserData, static content)
        private const ushort ExportFamilyId = 1;          // "Newbie", house 7
        private const short OldNidA = 47;                  // 'user00001', 0x3251685c
        private const short OldNidB = 48;                  // 'user00000', 0x32aa2056
        private const uint OldGuidA = 0x3251685c;
        private const uint OldGuidB = 0x32aa2056;
        private const string NameA = "user00001";
        private const string NameB = "user00000";
        private const string FamName = "Newbie";
        private const int ExportBudget = 665;
        private static readonly short[] SeedRow = { 70, 25, 10 };
        private const ushort StageHouse = 5;               // occupied by Goth (cid=5)
        private const int EvictedMembers = 3;              // nids 32/33/34
        private const int EvictedBudget = 2542;            // cid=5 money pair
        private const int EvictedArch = 33116;
        private const int NewFamNumber = 10;               // max template famnum (9) + 1
        private const uint GothGuid0 = 0xc1207913;         // evicted 'user00011' (nid 32)
        private const uint GothGuid1 = 0xc0c6298e;         // evicted 'user00010' (nid 33)
        private const uint GothGuid2 = 0xc07f6184;         // evicted 'user00009' (nid 34)

        // member ORDER law: EXPi (and therefore FINV positions, the recreation
        // order and the fresh NID assignment) follows the FAMI guid order, which
        // for template family 1 is [0x32aa2056 'user00000', 0x3251685c 'user00001'].
        private static readonly uint[] FamGuidOrder = { OldGuidB, OldGuidA };
        private static readonly string[] FamNameOrder = { NameB, NameA };
        private static readonly short[] FamNidOrder = { OldNidB, OldNidA };

        private List<InventoryItem> _seedItems;            // picked in phase 1
        private string _exportPath;
        private readonly bool _run2;

        // recreation-law expectations (derived pre-import) and actuals (from Gtab)
        private short _nid0, _nid1, _nidSeed;              // actual recreated NIDs
        private uint _expGuid0, _expGuid1;                 // actual recreated GUIDs (Gtab new sides)
        private string _expName0, _expName1;               // recreated NBRS names (userid stems)
        private string[] _charFiles0;                      // baseline character files

        internal static bool BeginIsolation(string checksCsv)
        {
            var c = checksCsv?.Trim().Trim('"');
            if (string.IsNullOrEmpty(c)) return false;
            var names = c.Split(',').Select(s => s.Trim().ToLowerInvariant()).ToList();
            bool run1 = names.Contains("sav07live");
            bool run2 = names.Contains("sav07live2");
            if (!run1 && !run2) return false;
            try
            {
                RedirectDir = Path.Combine(Path.GetTempPath(), "simitone-sav07-live");
                if (run1 && Directory.Exists(RedirectDir))
                    Directory.Delete(RedirectDir, true); // fresh clone from pristine game-data
                Directory.CreateDirectory(RedirectDir);
                FSOEnvironment.UserDir = RedirectDir;
                return true;
            }
            catch (Exception e)
            {
                LogStatic("AUTOTEST sav07: isolation setup failed: " + e.GetType().Name + " " + e.Message);
                RedirectDir = null;
                return true; // opted in but broken: the probe must report it, not run unisolated
            }
        }

        private static void LogStatic(string msg)
        {
            Console.WriteLine("AUTOTEST sav07: " + msg);
        }

        public bool Passed { get { return _passed && _fails.Count == 0; } }
        public string Diagnostics { get { return string.Join("; ", _notes); } }
        public string Failures { get { return string.Join("; ", _fails); } }

        public AutotestSav07(Action<string> log, bool run2)
        {
            _log = log;
            _run2 = run2;
            if (RedirectDir == null)
            {
                Fail("isolation unavailable (BeginIsolation failed)");
                _done = true;
            }
        }

        public bool Tick()
        {
            if (_done) return true;
            if (++_phaseTicks > 900)
            {
                Fail("stalled 900 ticks in phase " + _phase);
                return true;
            }
            try
            {
                switch (_phase)
                {
                    case 0: PhaseReady(); break;
                    case 1: PhaseSeedExport(); break;
                    case 2: PhaseStageImport(); break;
                    case 3: PhaseVerifyImport(); break;
                    case 4: PhasePersistenceFile(); break;
                    case 5: PhaseVerifyReload(); break;
                }
            }
            catch (Exception e)
            {
                var at = (e.StackTrace ?? "").Split('\n').FirstOrDefault()
                    ?.TrimStart().Split('(').FirstOrDefault();
                Fail("EXC phase " + _phase + ": " + e.GetType().Name + " " + e.Message
                    + (at == null ? "" : " @ " + at));
                _done = true;
            }
            return _done;
        }

        private void Next() { _phase++; _phaseTicks = 0; }

        private void Check(bool cond, string what)
        {
            if (cond) _notes.Add("ok: " + what);
            else Fail(what);
        }

        private void Fail(string what)
        {
            _notes.Add("FAIL: " + what);
            _fails.Add(what);
            _passed = false;
            _log("AUTOTEST sav07: FAIL " + what);
        }

        private void Note(string what)
        {
            _notes.Add(what);
            _log("AUTOTEST sav07: " + what);
        }

        // --- helpers -------------------------------------------------------

        private int CharCount()
        {
            return Directory.GetFiles(Path.Combine(N.UserPath, "Characters"), "User*.iff").Length;
        }

        private string ExportCopyPath()
        {
            return Path.Combine(RedirectDir, "sav07-export.FAM");
        }

        private string HouseCopyPath()
        {
            return Path.Combine(RedirectDir, "sav07-House05.iff");
        }

        private string NeighborhoodCopyPath()
        {
            return Path.Combine(RedirectDir, "sav07-Neighborhood.iff");
        }

        private string MarkerPath()
        {
            return Path.Combine(RedirectDir, "sav07-run1.marker");
        }

        /// <summary>Patch the house field (FAMI payload offset 12, LE int32) of the
        /// first FAMI chunk in a raw IFF byte image (76-byte headers from byte 64,
        /// sizes inclusive). Same surgery the SAV-05 battery uses for templates —
        /// never a re-serialization through the port's writer.</summary>
        private static bool PatchFirstFamiHouse(byte[] bytes, int house)
        {
            int pos = 64;
            bool patched = false;
            while (pos + 76 <= bytes.Length)
            {
                string type = System.Text.Encoding.ASCII.GetString(bytes, pos, 4);
                uint size = (uint)((bytes[pos + 4] << 24) | (bytes[pos + 5] << 16)
                    | (bytes[pos + 6] << 8) | bytes[pos + 7]);
                if (size < 76 || pos + size > bytes.Length) return false;
                if (type == "FAMI" && !patched)
                {
                    int h = pos + 76 + 12;
                    bytes[h] = (byte)house;
                    bytes[h + 1] = (byte)(house >> 8);
                    bytes[h + 2] = (byte)(house >> 16);
                    bytes[h + 3] = (byte)(house >> 24);
                    patched = true;
                }
                pos += (int)size;
            }
            return patched;
        }

        private static bool ItemsEqual(List<InventoryItem> got, List<InventoryItem> want)
        {
            if (got == null || got.Count != want.Count) return false;
            for (int i = 0; i < want.Count; i++)
            {
                if (got[i].Type != want[i].Type || got[i].GUID != want[i].GUID
                    || got[i].Count != want[i].Count) return false;
            }
            return true;
        }

        private string ItemsStr(List<InventoryItem> list)
        {
            if (list == null) return "null";
            return "[" + string.Join(",", list.Select(x => "(t" + x.Type + " g" + x.GUID.ToString("X8") + " n" + x.Count + ")")) + "]";
        }

        /// <summary>Assert a recreated member's identity. Returns the Neighbour.</summary>
        private Neighbour CheckRecreated(uint newGuid, string wantName,
            Dictionary<uint, short> nidByGuid, string tag)
        {
            var live = N.GetNeighborByID(nidByGuid[newGuid]);
            Check(live != null, tag + " recreated member exists (guid " + newGuid.ToString("X8") + ")");
            if (live == null) return null;
            Check(live.Name == wantName, tag + " name '" + live.Name + "' == '" + wantName + "'");
            Check(nidByGuid[newGuid] != OldNidA && nidByGuid[newGuid] != OldNidB,
                tag + " fresh NID " + nidByGuid[newGuid] + " (not the exported " + OldNidA + "/" + OldNidB + ")");
            if (live.PersonData != null)
            {
                Check(live.PersonData[31] == nidByGuid[newGuid],
                    tag + " personData[31] carries its NID");
                Check(live.PersonData[61] == 0,
                    tag + " personData[61] carries the imported family id 0 (collision claim)");
            }
            else Fail(tag + " PersonData missing");
            return live;
        }

        // --- phases --------------------------------------------------------

        private void PhaseReady()
        {
            N = Content.Get().Neighborhood;
            if (N == null || N.MainResource == null || N.UserPath == null) return;
            if (_phaseTicks < 30) return; // let the neighborhood screen settle

            if (!N.UserPath.Replace('\\', '/').Contains("simitone-sav07-live"))
            {
                Fail("isolation not in effect: UserPath=" + N.UserPath);
                _done = true;
                return;
            }

            string[] marker = null;
            if (_run2)
            {
                if (!File.Exists(MarkerPath()))
                {
                    Fail("run2 marker missing — the isolation dir is not run 1's state (" + MarkerPath() + ")");
                    _done = true;
                    return;
                }
                marker = File.ReadAllLines(MarkerPath());
                foreach (var line in marker)
                {
                    var m = System.Text.RegularExpressions.Regex.Match(line, @"^rec(\d) nid=(-?\d+) guid=([0-9A-Fa-f]+) name=(.*)$");
                    if (m.Success)
                    {
                        if (m.Groups[1].Value == "0")
                        {
                            _nid0 = short.Parse(m.Groups[2].Value);
                            _expGuid0 = Convert.ToUInt32(m.Groups[3].Value, 16);
                            _expName0 = m.Groups[4].Value;
                        }
                        else
                        {
                            _nid1 = short.Parse(m.Groups[2].Value);
                            _expGuid1 = Convert.ToUInt32(m.Groups[3].Value, 16);
                            _expName1 = m.Groups[4].Value;
                        }
                    }
                    var sm = System.Text.RegularExpressions.Regex.Match(line, @"^seednid=(\d+)$");
                    if (sm.Success) _nidSeed = short.Parse(sm.Groups[1].Value);
                }
                Note("run2: mounted persisted isolation dir " + N.UserPath
                    + " (rec0 nid=" + _nid0 + " '" + _expName0 + "', rec1 nid=" + _nid1 + " '" + _expName1 + "')");
                _phase = 5;
                _phaseTicks = 0;
                return;
            }

            // content guards: the template must look like the design expects
            var fam1 = N.GetFamily(ExportFamilyId);
            Check(fam1 != null && fam1.FamilyGUIDs.Length == 2, "family 1 present with 2 members");
            Check(fam1 != null && fam1.FamilyGUIDs.SequenceEqual(FamGuidOrder),
                "family 1 guid order matches the design order law ("
                + (fam1 == null ? "?" : string.Join(",", fam1.FamilyGUIDs.Select(g => g.ToString("X8")))) + ")");
            Check(fam1 != null && fam1.Budget == ExportBudget && fam1.HouseNumber == 7,
                "family 1 budget " + (fam1?.Budget.ToString() ?? "?") + " house " + (fam1?.HouseNumber.ToString() ?? "?"));
            var goth = N.GetFamilyForHouse((short)StageHouse);
            Check(goth != null && goth.ChunkID == 5 && goth.FamilyGUIDs.Length == EvictedMembers,
                "house 5 pre-state: Goth (cid=5, " + (goth?.FamilyGUIDs.Length.ToString() ?? "?") + " members)");
            Check(N.GetNeighborByID(OldNidA) != null && N.GetNeighborByID(OldNidB) != null,
                "export source members nid " + OldNidA + "/" + OldNidB + " present");
            if (!_passed) { _done = true; return; }
            Note("baseline: chars=" + CharCount() + " userdir=" + N.UserPath);
            Next();
        }

        private void PhaseSeedExport()
        {
            // seed the distinctive relationship row both ways (overwrites the
            // template's mutual [55,0] so the carried value is unambiguous)
            var a = N.GetNeighborByID(OldNidA);
            var b = N.GetNeighborByID(OldNidB);
            a.Relationships[OldNidB] = new List<short>(SeedRow);
            b.Relationships[OldNidA] = new List<short>(SeedRow);

            // two real catalog GUIDs, deterministically picked (content-static)
            var objs = (TS1ObjectProvider)Content.Get().WorldObjects;
            var guids = objs.Entries.Keys.OrderBy(k => k).Take(2).ToList();
            _seedItems = new List<InventoryItem>
            {
                new InventoryItem { Type = 1, GUID = (uint)guids[0], Count = 2 },
                new InventoryItem { Type = 2, GUID = (uint)guids[1], Count = 1 },
            };
            N.Neighborhood.InventoryByID[OldNidA] = _seedItems.Select(x => x.Clone()).ToList();
            Note("seed: rel " + OldNidA + "<->" + OldNidB + " = [" + string.Join(",", SeedRow)
                + "]; inventory nid " + OldNidA + " = " + ItemsStr(_seedItems));

            // production export
            _exportPath = N.ExportFamily(ExportFamilyId);
            Check(_exportPath != null && File.Exists(_exportPath),
                "ExportFamily(1) wrote a file (" + (_exportPath ?? "null") + ")");
            if (!_passed) { _done = true; return; }

            // engine-parse verification of the export
            var famIff = new IffFile(_exportPath);
            var expi = famIff.List<EXPi>()?.FirstOrDefault();
            Check(expi != null && expi.ActiveMemberIDs.SequenceEqual(FamNidOrder),
                "export EXPi members [" + (expi == null ? "?" : string.Join(",", expi.ActiveMemberIDs)) + "] == ["
                + FamNidOrder[0] + "," + FamNidOrder[1] + "] (FAMI guid order)");
            var efami = famIff.List<FAMI>()?.FirstOrDefault();
            Check(efami != null && efami.Budget == ExportBudget && efami.HouseNumber == 7
                && efami.FamilyGUIDs.SequenceEqual(FamGuidOrder),
                "export FAMI budget " + (efami?.Budget.ToString() ?? "?") + " house " + (efami?.HouseNumber.ToString() ?? "?")
                + " guids " + (efami == null ? "?" : string.Join(",", efami.FamilyGUIDs.Select(g => g.ToString("X8")))));
            var efams = famIff.Get<FAMs>(ExportFamilyId);
            Check(efams != null && efams.GetString(0) == FamName,
                "export FAMs name '" + (efams?.GetString(0) ?? "?") + "' == '" + FamName + "'");
            var enbrs = famIff.List<NBRS>()?.FirstOrDefault();
            var recA = enbrs?.NeighbourByID.ContainsKey(OldNidA) == true ? enbrs.NeighbourByID[OldNidA] : null;
            var recB = enbrs?.NeighbourByID.ContainsKey(OldNidB) == true ? enbrs.NeighbourByID[OldNidB] : null;
            Check(recA != null && recB != null && enbrs.Entries.Count == 2,
                "export NBRS carries exactly the 2 member records");
            Check(recA != null && recA.Name == NameA
                && recA.Relationships.ContainsKey(OldNidB)
                && recA.Relationships[OldNidB].SequenceEqual(SeedRow),
                "export NBRS " + OldNidA + " ('" + (recA?.Name ?? "?") + "') rel[" + OldNidB + "] = ["
                + (recA != null && recA.Relationships.ContainsKey(OldNidB) ? string.Join(",", recA.Relationships[OldNidB]) : "?") + "]");
            Check(recB != null && recB.Name == NameB
                && recB.Relationships.ContainsKey(OldNidA)
                && recB.Relationships[OldNidA].SequenceEqual(SeedRow),
                "export NBRS " + OldNidB + " ('" + (recB?.Name ?? "?") + "') rel[" + OldNidA + "] seeded");
            var efinv = famIff.List<FINV>()?.FirstOrDefault();
            Check(efinv != null && efinv.Inventories.Count == 2,
                "export FINV present with 2 positional inventories");
            Check(efinv != null && efinv.Inventories[0].Count == 0,
                "export FINV[0] (EXPi member 0 = old nid " + FamNidOrder[0] + ") empty");
            Check(efinv != null && ItemsEqual(efinv.Inventories[1], _seedItems),
                "export FINV[1] (EXPi member 1 = old nid " + FamNidOrder[1] + ") == seeded items "
                + (efinv == null ? "?" : ItemsStr(efinv.Inventories[1])));
            Check(famIff.Get<uChr>((ushort)OldNidA) != null && famIff.Get<uChr>((ushort)OldNidB) != null,
                "export uChr@47/48 present (s13: '"
                + famIff.Get<uChr>((ushort)OldNidA)?.GetString(13) + "' / '"
                + famIff.Get<uChr>((ushort)OldNidB)?.GetString(13) + "')");
            Check(famIff.Get<CTSS>((ushort)(OldNidA + 2000)) != null
                && famIff.Get<CTSS>((ushort)(OldNidB + 2000)) != null,
                "export CTSS@nid+2000 present for both members");
            if (!_passed) { _done = true; return; }

            File.Copy(_exportPath, ExportCopyPath(), true);
            Note("export retained: " + ExportCopyPath());
            Next();
        }

        private void PhaseStageImport()
        {
            var bytes = File.ReadAllBytes(ExportCopyPath());
            if (!PatchFirstFamiHouse(bytes, StageHouse))
            {
                Fail("staged copy FAMI house patch failed");
                _done = true;
                return;
            }
            var importDir = Path.Combine(N.UserPath, "Import");
            Directory.CreateDirectory(importDir);
            var staged = Path.Combine(importDir, "Newbie_1.FAM");
            File.WriteAllBytes(staged, bytes);
            var verify = new IffFile(staged);
            var vfami = verify.List<FAMI>()?.FirstOrDefault();
            Check(vfami != null && vfami.HouseNumber == StageHouse,
                "staged copy parses with patched house " + (vfami?.HouseNumber.ToString() ?? "?"));

            int charsBefore = CharCount();
            _charFiles0 = Directory.GetFiles(Path.Combine(N.UserPath, "Characters"), "User*.iff");

            // recreation NBRS-name expectation (ImportAddNeighbor + the SAV-07
            // name-carry fix): the recreated record carries the FAM record's
            // neighborhood name. (The fresh NIDs are NOT pre-derived: the live
            // Entries grow as catalog persons mount during the session, so the
            // first-free scan result depends on WHEN the import runs.)
            _expName0 = FamNameOrder[0];
            _expName1 = FamNameOrder[1];

            var rc = N.CheckForNewImports();
            Check(rc == 1, "import poll Imported (rc=" + rc + ")");
            Check(!File.Exists(staged), "staged file consumed (FAM became the house file)");
            if (!_passed) { _done = true; return; }
            Note("chars before import: " + charsBefore);
            Next();
        }

        private void PhaseVerifyImport()
        {
            // family-level law: collision id → 0 sentinel claim at the staged house
            var fam0 = N.MainResource.Get<FAMI>(0);
            Check(fam0 != null && fam0.FamilyGUIDs.Length == 2 && fam0.HouseNumber == StageHouse,
                "import claims id-0 in place at house " + StageHouse + " ("
                + (fam0 == null ? "none" : fam0.FamilyGUIDs.Length + "m h" + fam0.HouseNumber) + ")");
            Check(fam0 != null && fam0.Budget == ExportBudget,
                "import budget carried from the export (" + (fam0?.Budget.ToString() ?? "?") + ")");
            Check(fam0 != null && fam0.FamilyNumber == NewFamNumber,
                "import FamilyNumber == max(template)+1 == " + NewFamNumber + " (got " + (fam0?.FamilyNumber.ToString() ?? "?") + ")");
            var fams0 = N.GetFamilyString(0);
            Check(fams0 != null && fams0.GetString(0) == FamName,
                "imported family display name '" + (fams0?.GetString(0) ?? "?") + "' == '" + FamName + "'");
            FAMI holder;
            N.FamilyForHouse.TryGetValue((short)StageHouse, out holder);
            Check(holder != null && holder.ChunkID == 0, "FamilyForHouse[" + StageHouse + "] is the import");

            // eviction law, by identity (the freed ids get reused by recreation):
            // the Goth member GUIDs are gone from the live neighbor store
            Check(N.GetNeighborIDForGUID(GothGuid0) == null && N.GetNeighborIDForGUID(GothGuid1) == null
                && N.GetNeighborIDForGUID(GothGuid2) == null,
                "evicted characters (Goth guids " + GothGuid0.ToString("X8") + "/"
                + GothGuid1.ToString("X8") + "/" + GothGuid2.ToString("X8") + ") deleted");

            // eviction law: the house-5 family lost the house with net-worth preserved
            var evicted = N.GetFamily(5);
            Check(evicted != null && evicted.HouseNumber == 0,
                "Goth loses the house (h" + (evicted?.HouseNumber.ToString() ?? "?") + ")");
            Check(evicted != null && evicted.Budget == EvictedBudget + EvictedArch && evicted.ValueInArch == 0,
                "Goth net-worth preserved (" + EvictedBudget + "+" + EvictedArch + " -> " + (evicted?.Budget.ToString() ?? "?") + ")");

            // Gtab: the FAM became House05.iff and carries the old→new pairs
            IffFile h5;
            Gtab gtab = null;
            try
            {
                h5 = new IffFile(N.GetHousePath(StageHouse));
                gtab = h5.List<Gtab>()?.FirstOrDefault();
            }
            catch (Exception e)
            {
                Fail("House05.iff fresh-parse failed: " + e.Message);
            }
            Check(gtab != null && gtab.Pairs.Count == 2, "House05.iff carries the Gtab with 2 pairs ("
                + (gtab == null ? "none" : string.Join(",", gtab.Pairs.Select(p => p.OldGUID.ToString("X8") + "->" + p.NewGUID.ToString("X8")))) + ")");
            var hfami = default(FAMI);
            try
            {
                hfami = new IffFile(N.GetHousePath(StageHouse)).List<FAMI>()?.FirstOrDefault();
            }
            catch { }
            Check(hfami != null && hfami.HouseNumber == StageHouse, "House05.iff carries the imported FAMI");

            if (gtab != null && gtab.Pairs.Count == 2)
            {
                // the recreation order == the EXPi order == the Gtab pair order:
                // pair 0 = old nid 48 ('user00000') → first free NID 1,
                // pair 1 = old nid 47 ('user00001') → NID 2.
                Check(gtab.Pairs[0].OldGUID == FamGuidOrder[0] && gtab.Pairs[1].OldGUID == FamGuidOrder[1],
                    "Gtab old sides are the exported GUIDs in EXPi order ("
                    + gtab.Pairs[0].OldGUID.ToString("X8") + "," + gtab.Pairs[1].OldGUID.ToString("X8") + ")");
                Check(gtab.Pairs[0].NewGUID != gtab.Pairs[1].NewGUID,
                    "Gtab new sides are distinct fresh GUIDs");
                if (gtab.Pairs[0].OldGUID == FamGuidOrder[0] && gtab.Pairs[1].OldGUID == FamGuidOrder[1]
                    && gtab.Pairs[0].NewGUID != gtab.Pairs[1].NewGUID)
                {
                    var nidByGuid = new Dictionary<uint, short>();
                    foreach (var n in N.Neighbors.Entries)
                    {
                        if (n.GUID == gtab.Pairs[0].NewGUID || n.GUID == gtab.Pairs[1].NewGUID)
                            nidByGuid[n.GUID] = n.NeighbourID;
                    }
                    var rec0 = CheckRecreated(gtab.Pairs[0].NewGUID, _expName0 ?? "<scan?>", nidByGuid, "0(" + FamNameOrder[0] + ")");
                    var rec1 = CheckRecreated(gtab.Pairs[1].NewGUID, _expName1 ?? "<scan?>", nidByGuid, "1(" + FamNameOrder[1] + ")");
                    _nid0 = nidByGuid.ContainsKey(gtab.Pairs[0].NewGUID) ? nidByGuid[gtab.Pairs[0].NewGUID] : (short)0;
                    _nid1 = nidByGuid.ContainsKey(gtab.Pairs[1].NewGUID) ? nidByGuid[gtab.Pairs[1].NewGUID] : (short)0;
                    _expGuid0 = gtab.Pairs[0].NewGUID;
                    _expGuid1 = gtab.Pairs[1].NewGUID;
                    Check(_nid0 != _nid1 && _nid0 > 0 && _nid1 > 0,
                        "recreated NIDs are distinct positive fresh ids (got "
                        + _nid0 + "/" + _nid1 + ")");

                    // NBRS name law (SAV-07 name-carry fix): the recreated Neighbour
                    // records carry the FAM record's neighborhood name — the same
                    // identity the export wrote — with the userid stem only as the
                    // fallback. The character file's CTSS@2000 display name carries
                    // it too; asserted below over the fresh User*.iff files.
                    Check(_expName0 != null && rec0 != null && rec0.Name == _expName0,
                        "recreated NBRS name 0 carries the exported name '" + _expName0 + "' (got '"
                        + (rec0?.Name ?? "?") + "')");
                    Check(_expName1 != null && rec1 != null && rec1.Name == _expName1,
                        "recreated NBRS name 1 carries the exported name '" + _expName1 + "' (got '"
                        + (rec1?.Name ?? "?") + "')");
                    int parsedCtss = 0;
                    var nowFiles = Directory.GetFiles(Path.Combine(N.UserPath, "Characters"), "User*.iff");
                    foreach (var f in nowFiles.Except(_charFiles0))
                    {
                        try
                        {
                            var ctss = new IffFile(f).Get<CTSS>(2000);
                            var s = ctss?.GetString(0);
                            if (s == NameA || s == NameB) parsedCtss++;
                        }
                        catch { }
                    }
                    Check(parsedCtss == 2,
                        "fresh character files carry the exported names via CTSS@2000 (" + parsedCtss + "/2)");

                    // FINV re-key: entry 0 (empty, old nid 48) lands on fresh NID of
                    // member 0; entry 1 (the seeded items, old nid 47) on member 1
                    var inv0 = N.GetInventoryByNID(_nid0);
                    Check(inv0 == null || inv0.Count == 0,
                        "FINV re-key: fresh NID " + _nid0 + " (old " + FamNidOrder[0] + ") carries no inventory");
                    var inv1 = N.GetInventoryByNID(_nid1);
                    Check(ItemsEqual(inv1, _seedItems),
                        "FINV re-keyed onto fresh NID " + _nid1 + " (old " + FamNidOrder[1] + "): " + ItemsStr(inv1));

                    // relationship re-key: seeded row carried onto fresh NIDs
                    Check(rec0 != null && rec0.Relationships.ContainsKey(_nid1)
                        && rec0.Relationships[_nid1].SequenceEqual(SeedRow),
                        "rel row re-keyed " + FamNameOrder[0] + "->" + _nid1 + " = ["
                        + (rec0 != null && rec0.Relationships.ContainsKey(_nid1) ? string.Join(",", rec0.Relationships[_nid1]) : "?") + "]");
                    Check(rec1 != null && rec1.Relationships.ContainsKey(_nid0)
                        && rec1.Relationships[_nid0].SequenceEqual(SeedRow),
                        "rel row re-keyed " + FamNameOrder[1] + "->" + _nid0);

                    // mutual-friend law: both rows lead with 70 (>= 50) → friends == 2
                    Check(fam0 != null && fam0.FamilyFriends == 2,
                        "FamilyFriends == 2 (mutual-friend law; got " + (fam0?.FamilyFriends.ToString() ?? "?") + ")");
                }
            }

            // the export SOURCE is untouched by its own re-import
            var src = N.GetFamily(ExportFamilyId);
            Check(src != null && src.Budget == ExportBudget && src.HouseNumber == 7
                && src.FamilyGUIDs.Length == 2,
                "source family 1 untouched (members " + (src?.FamilyGUIDs.Length.ToString() ?? "?")
                + " h" + (src?.HouseNumber.ToString() ?? "?") + " b" + (src?.Budget.ToString() ?? "?") + ")");
            Check(N.GetNeighborByID(OldNidA) != null && N.GetNeighborByID(OldNidB) != null,
                "source members nid " + OldNidA + "/" + OldNidB + " still present");

            int charsNow = CharCount();
            Note("chars after import: " + charsNow + " (expected baseline-1: -" + EvictedMembers + " evicted +2 recreated)");
            Next();
        }

        private void PhasePersistenceFile()
        {
            // step-24 saved the neighborhood: the saved FILE must already carry
            // the imported identity (file-level persistence proof, in-run)
            var savedPath = Path.Combine(N.UserPath, "Neighborhood.iff");
            IffFile saved = null;
            try
            {
                saved = new IffFile(savedPath);
            }
            catch (Exception e)
            {
                Fail("saved Neighborhood.iff fresh-parse failed: " + e.Message);
                _done = true;
                return;
            }
            var sfam0 = saved.List<FAMI>()?.FirstOrDefault(x => x.ChunkID == 0);
            Check(sfam0 != null && sfam0.FamilyGUIDs.Length == 2 && sfam0.HouseNumber == StageHouse
                && sfam0.Budget == ExportBudget,
                "saved file: id-0 family at house " + StageHouse + " with 2 members, budget " + (sfam0?.Budget.ToString() ?? "?"));
            var snbrs = saved.List<NBRS>()?.FirstOrDefault();
            var s0 = snbrs?.Entries.FirstOrDefault(x => x.NeighbourID == _nid0);
            var s1 = snbrs?.Entries.FirstOrDefault(x => x.NeighbourID == _nid1);
            Check(s0 != null && s0.GUID == _expGuid0, "saved NBRS nid " + _nid0 + " is the recreated member 0");
            Check(s1 != null && s1.GUID == _expGuid1, "saved NBRS nid " + _nid1 + " is the recreated member 1");
            var sngbh = saved.List<NGBH>()?.FirstOrDefault();
            var sinv = default(List<InventoryItem>);
            sngbh?.InventoryByID.TryGetValue(_nid1, out sinv);
            Check(ItemsEqual(sinv, _seedItems), "saved NGBH inventory tail carries nid " + _nid1 + " items " + ItemsStr(sinv));
            var src1 = saved.List<FAMI>()?.FirstOrDefault(x => x.ChunkID == ExportFamilyId);
            Check(src1 != null && src1.HouseNumber == 7, "saved file: source family 1 still at house 7");

            // retain artifacts for run 2 + offline independent parse
            File.Copy(N.GetHousePath(StageHouse), HouseCopyPath(), true);
            File.Copy(savedPath, NeighborhoodCopyPath(), true);
            File.WriteAllLines(MarkerPath(), new[]
            {
                "sav07 run1 " + DateTime.UtcNow.ToString("o"),
                "rec0 nid=" + _nid0 + " guid=" + _expGuid0.ToString("X8") + " name=" + _expName0,
                "rec1 nid=" + _nid1 + " guid=" + _expGuid1.ToString("X8") + " name=" + _expName1,
                "seednid=" + _nid1,
            });
            Note("artifacts retained: " + HouseCopyPath() + ", " + NeighborhoodCopyPath());
            Check(true, "run 1 complete");
            _done = true;
        }

        private void PhaseVerifyReload()
        {
            // fresh process, same dir: the persisted state reloaded from disk.
            // Re-pick the seed items the same deterministic way phase 1 did.
            var objs = (TS1ObjectProvider)Content.Get().WorldObjects;
            var guids = objs.Entries.Keys.OrderBy(k => k).Take(2).ToList();
            _seedItems = new List<InventoryItem>
            {
                new InventoryItem { Type = 1, GUID = (uint)guids[0], Count = 2 },
                new InventoryItem { Type = 2, GUID = (uint)guids[1], Count = 1 },
            };

            var fam0 = N.MainResource.Get<FAMI>(0);
            Check(fam0 != null && fam0.FamilyGUIDs.Length == 2 && fam0.HouseNumber == StageHouse
                && fam0.Budget == ExportBudget,
                "reload: imported family persisted ("
                + (fam0 == null ? "none" : fam0.FamilyGUIDs.Length + "m h" + fam0.HouseNumber + " b" + fam0.Budget) + ")");
            Check(fam0 != null && fam0.FamilyFriends == 2, "reload: FamilyFriends 2 persisted (got "
                + (fam0?.FamilyFriends.ToString() ?? "?") + ")");
            FAMI holder;
            N.FamilyForHouse.TryGetValue((short)StageHouse, out holder);
            Check(holder != null && holder.ChunkID == 0, "reload: FamilyForHouse[" + StageHouse + "] is the import");

            // Gtab survives in the house file; the recreated members resolve live
            Gtab gtab = null;
            try
            {
                gtab = new IffFile(N.GetHousePath(StageHouse)).List<Gtab>()?.FirstOrDefault();
            }
            catch (Exception e)
            {
                Fail("reload: House05.iff parse failed: " + e.Message);
            }
            Check(gtab != null && gtab.Pairs.Count == 2, "reload: Gtab persisted with 2 pairs");
            if (gtab != null && gtab.Pairs.Count == 2 && _expGuid0 != 0 && _expGuid1 != 0)
            {
                // run 1's identity (marker): pair 0/1 old→new; assert the recreated
                // members persisted under the same NIDs, names and Gtab mapping
                Check(gtab.Pairs[0].OldGUID == FamGuidOrder[0] && gtab.Pairs[1].OldGUID == FamGuidOrder[1],
                    "reload: Gtab old sides unchanged");
                var rec0 = N.Neighbors.Entries.FirstOrDefault(x => x.GUID == gtab.Pairs[0].NewGUID);
                var rec1 = N.Neighbors.Entries.FirstOrDefault(x => x.GUID == gtab.Pairs[1].NewGUID);
                Check(rec0 != null && gtab.Pairs[0].NewGUID == _expGuid0 && rec0.NeighbourID == _nid0
                    && rec0.Name == _expName0,
                    "reload: member 0 persisted (nid " + (rec0?.NeighbourID.ToString() ?? "?") + " '"
                    + (rec0?.Name ?? "?") + "')");
                Check(rec1 != null && gtab.Pairs[1].NewGUID == _expGuid1 && rec1.NeighbourID == _nid1
                    && rec1.Name == _expName1,
                    "reload: member 1 persisted (nid " + (rec1?.NeighbourID.ToString() ?? "?") + " '"
                    + (rec1?.Name ?? "?") + "')");
                if (rec0 != null && rec1 != null)
                {
                    Check(rec0.Relationships.ContainsKey(_nid1) && rec0.Relationships[_nid1].SequenceEqual(SeedRow),
                        "reload: rel row rec0->" + _nid1 + " persisted [" + (rec0.Relationships.ContainsKey(_nid1) ? string.Join(",", rec0.Relationships[_nid1]) : "?") + "]");
                    Check(rec1.Relationships.ContainsKey(_nid0) && rec1.Relationships[_nid0].SequenceEqual(SeedRow),
                        "reload: rel row rec1->" + _nid0 + " persisted");
                }
                var invSeed = N.GetInventoryByNID(_nidSeed);
                Check(ItemsEqual(invSeed, _seedItems),
                    "reload: FINV/NGBH inventory persisted on nid " + _nidSeed + " " + ItemsStr(invSeed));
            }

            var src = N.GetFamily(ExportFamilyId);
            Check(src != null && src.Budget == ExportBudget && src.HouseNumber == 7
                && src.FamilyGUIDs.Length == 2,
                "reload: source family 1 intact");
            Check(N.GetNeighborByID(OldNidA) != null && N.GetNeighborByID(OldNidB) != null,
                "reload: source members intact");
            var evicted = N.GetFamily(5);
            Check(evicted != null && evicted.HouseNumber == 0 && evicted.FamilyGUIDs.Length == EvictedMembers,
                "reload: eviction persisted (Goth h" + (evicted?.HouseNumber.ToString() ?? "?") + ")");
            Check(N.GetNeighborIDForGUID(GothGuid0) == null && N.GetNeighborIDForGUID(GothGuid1) == null
                && N.GetNeighborIDForGUID(GothGuid2) == null,
                "reload: evicted characters stay deleted");
            Note("run 2 complete");
            _done = true;
        }
    }
}
