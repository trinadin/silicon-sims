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
using System.Security.Cryptography;

namespace Simitone.Client
{
    /// <summary>
    /// SAV-05 'impexport' (opt-in, focused) — the runtime acceptance fixture for the
    /// generic family import (tools/iff-dump/r253-import-export/port-audit.md §4).
    /// Runs on the real game bootstrap at the neighborhood screen (the R247 takeover
    /// idiom): BeginIsolation redirects FSOEnvironment.UserDir into a fresh temp dir
    /// BEFORE Content.Init, so TS1NeighborhoodProvider.InitSpecific clones the
    /// pristine UserData there and game-data stays read-only (sources SHA-pinned).
    /// The battery stages template FAMs into the isolated UserData/Import, drives the
    /// production poll (CheckForNewImports) and asserts the 25-step import law's
    /// observable outcomes: generic non-tutorial import (gate removal), id-collision
    /// → id-0 in-place claim, house move, occupied-house eviction with net-worth
    /// preservation, refusal/silent-skip negatives leaving NO partial mutation.
    /// The export→re-import round-trip is intentionally OUT OF SCOPE (SAV-06's
    /// export tranche — the current ExportFamily writes EXPi/FAMI/FAMs/NBRS only).
    /// </summary>
    public class AutotestImpexport
    {
        private Action<string> _log;
        private int _phase, _phaseTicks;
        private bool _done, _passed = true;
        private readonly List<string> _notes = new List<string>();
        private readonly List<string> _fails = new List<string>();
        private TS1NeighborhoodProvider N;

        // isolation (R246/R247 BeginIsolation idiom)
        private static string _priorUserDir;
        internal static string RedirectDir;
        internal static string IsolationError;

        // game-data integrity pins (read-only proof)
        private static readonly List<string> _gdPaths = new List<string>();
        private static readonly List<string> _gdShas = new List<string>();

        private int _fami0, _char0;
        private string[] _charFiles0;
        private int _tutHouse0, _tutState0;
        private int _b0, _v0; // id-0 family money pair captured before eviction

        internal static bool BeginIsolation(string checksCsv)
        {
            var c = checksCsv?.Trim().Trim('"');
            if (string.IsNullOrEmpty(c)) return false;
            var names = c.Split(',').Select(s => s.Trim().ToLowerInvariant()).ToList();
            if (!names.Contains("impexport")) return false;
            try
            {
                _priorUserDir = FSOEnvironment.UserDir;
                RedirectDir = Path.Combine(Path.GetTempPath(), "simitone-sav05-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(RedirectDir);
                FSOEnvironment.UserDir = RedirectDir;
                return true;
            }
            catch (Exception e)
            {
                // Isolation is mandatory for this flow — never run unisolated.
                IsolationError = e.GetType().Name + ": " + e.Message;
                return true;
            }
        }

        public bool Passed { get { return _passed && _fails.Count == 0; } }
        public string Diagnostics { get { return string.Join("; ", _notes); } }
        public string Failures { get { return string.Join("; ", _fails); } }

        public AutotestImpexport(Action<string> log)
        {
            _log = log;
            if (IsolationError != null)
            {
                Fail("isolation unavailable: " + IsolationError);
                _done = true;
            }
        }

        /// <returns>true when the battery has finished (verdict recorded by the runner).</returns>
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
                    case 1: PhaseGenericImport(); break;
                    case 2: PhaseCollisionHouseMove(); break;
                    case 3: PhaseEviction(); break;
                    case 4: PhaseRefusalNegatives(); break;
                    case 5: PhaseIntegrityVerdict(); break;
                }
            }
            catch (Exception e)
            {
                var at = (e.StackTrace ?? "").Split('\n').FirstOrDefault()
                    ?.TrimStart().Split('(').FirstOrDefault();
                Fail("EXC phase " + _phase + ": " + e.GetType().Name + " " + e.Message
                    + (at == null ? "" : " @ " + at));
                _done = true; // an exception in a phase fails the battery honestly — no tick retry
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
            _log("AUTOTEST impexport: FAIL " + what);
        }

        private void Note(string what)
        {
            _notes.Add(what);
            _log("AUTOTEST impexport: " + what);
        }

        // --- helpers -------------------------------------------------------

        private int CharCount()
        {
            return Directory.GetFiles(Path.Combine(N.UserPath, "Characters"), "User*.iff").Length;
        }

        private string ImportDir()
        {
            var d = Path.Combine(N.UserPath, "Import");
            Directory.CreateDirectory(d);
            return d;
        }

        /// <summary>Copy a template FAM from read-only game-data and patch its FAMI
        /// house field in the isolated copy (§4.3: FAMI payload offset 12, big-endian
        /// int32). Raw-byte surgery + engine-parse verification — the template is
        /// never re-serialized through the port's writer.</summary>
        private string StageFam(string srcName, int house, string stagedName)
        {
            var src = Path.Combine(GlobalSettings.Default.TS1HybridPath,
                "TemplateFamilyUnleashed", srcName);
            var bytes = File.ReadAllBytes(src);
            if (!PatchFirstFamiHouse(bytes, house)) return null;
            var path = Path.Combine(ImportDir(), stagedName);
            File.WriteAllBytes(path, bytes);
            // engine-parse verification: the patched copy must load with the right house
            var verify = new IffFile(path);
            var fami = verify.List<FAMI>()?.FirstOrDefault();
            if (fami == null || fami.HouseNumber != house) return null;
            return path;
        }

        /// <summary>Negative fixture: a template copy with its FAMs chunks AND the
        /// CTSS-1000 display-name override REMOVED (the step-4 law falls back to that
        /// override), house patched to 0. Loads, carries EXPi+FAMI (ValidateImportFile
        /// passes) but no display name — the import law's step 4 must refuse it.
        /// Byte-level chunk drop + parse verification.</summary>
        private string StageNoFamsFam(string stagedName)
        {
            var src = Path.Combine(GlobalSettings.Default.TS1HybridPath,
                "TemplateFamilyUnleashed", "Charming_13.FAM");
            var bytes = File.ReadAllBytes(src);
            var keep = new List<byte>(bytes.Length);
            keep.AddRange(bytes.Take(64)); //magic + map offset
            int pos = 64, dropped = 0;
            while (pos + 76 <= bytes.Length)
            {
                string type = System.Text.Encoding.ASCII.GetString(bytes, pos, 4);
                uint size = (uint)((bytes[pos + 4] << 24) | (bytes[pos + 5] << 16)
                    | (bytes[pos + 6] << 8) | bytes[pos + 7]);
                if (size < 76 || pos + size > bytes.Length) return null;
                uint cid = (uint)((bytes[pos + 8] << 8) | bytes[pos + 9]);
                bool drop = (type == "FAMs") || (type == "CTSS" && cid == 1000);
                if (drop) dropped++;
                else keep.AddRange(bytes.Skip(pos).Take((int)size));
                pos += (int)size;
            }
            if (dropped == 0 || !PatchFirstFamiHouse(keep.ToArray(), 0)) return null;
            var patched = keep.ToArray();
            if (!PatchFirstFamiHouse(patched, 0)) return null;
            var path = Path.Combine(ImportDir(), stagedName);
            File.WriteAllBytes(path, patched);
            // verification: loads, EXPi+FAMI present, FAMs and CTSS-1000 gone, house 0
            var verify = new IffFile(path);
            var vfami = verify.List<FAMI>()?.FirstOrDefault();
            if (verify.List<EXPi>()?.FirstOrDefault() == null
                || vfami == null || vfami.HouseNumber != 0
                || verify.List<FAMs>()?.FirstOrDefault() != null
                || verify.Get<CTSS>((ushort)1000) != null) return null;
            return path;
        }

        /// <summary>Walk raw IFF chunk headers (type[4] size:u32-be id:u16 flags:u16
        /// label[64] data[size-76]; file header = 60-byte magic + u32) and patch the
        /// house field (payload offset 12) of the first FAMI chunk. The FAMI payload
        /// is LITTLE-endian (FAMI.Read: IoBuffer LITTLE_ENDIAN); headers are BE.</summary>
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

        private static string Sha256(string path)
        {
            using (var sha = SHA256.Create())
            using (var fs = File.OpenRead(path))
                return Convert.ToBase64String(sha.ComputeHash(fs));
        }

        private void PinGameData(string rel)
        {
            var p = Path.Combine(GlobalSettings.Default.TS1HybridPath, rel);
            if (!File.Exists(p)) { Fail("game-data pin missing: " + rel); return; }
            _gdPaths.Add(p);
            _gdShas.Add(Sha256(p));
        }

        // --- phases --------------------------------------------------------

        private void PhaseReady()
        {
            N = Content.Get().Neighborhood;
            if (N == null || N.MainResource == null || N.UserPath == null) return;
            if (_phaseTicks < 30) return; // let the neighborhood screen settle

            var src = Path.Combine(GlobalSettings.Default.TS1HybridPath,
                "TemplateFamilyUnleashed", "Charming_13.FAM");
            var src2 = Path.Combine(GlobalSettings.Default.TS1HybridPath,
                "TemplateFamilyUnleashed", "Strays_4000.FAM");
            if (!File.Exists(src) || !File.Exists(src2))
            {
                Fail("template FAM sources missing in game-data (Charming_13/Strays_4000)");
                _done = true;
                return;
            }
            PinGameData(Path.Combine("TemplateFamilyUnleashed", "Charming_13.FAM").Replace('\\', '/'));
            PinGameData(Path.Combine("TemplateFamilyUnleashed", "Strays_4000.FAM").Replace('\\', '/'));
            PinGameData(Path.Combine("UserData", "Neighborhood.iff").Replace('\\', '/'));
            PinGameData(Path.Combine("UserData", "Tutorial.FAM").Replace('\\', '/'));
            if (!_passed) { _done = true; return; }

            _fami0 = N.MainResource.List<FAMI>().Count(x => x != null);
            _charFiles0 = Directory.GetFiles(Path.Combine(N.UserPath, "Characters"), "User*.iff");
            _char0 = _charFiles0.Length;
            _tutHouse0 = N.TutorialHouse;
            _tutState0 = N.TutorialState;
            Note("baseline: fami=" + _fami0 + " chars=" + _char0
                + " tutHouse=" + _tutHouse0 + " tutState=" + _tutState0
                + " userdir=" + N.UserPath);
            Next();
        }

        /// <summary>§4.3 Scenario 1 — generic import of a non-tutorial family
        /// (the SAV-05 gate removal): Charming_13 staged family-only (house 0).</summary>
        private void PhaseGenericImport()
        {
            var staged = StageFam("Charming_13.FAM", 0, "Charming_13.FAM");
            if (staged == null) { Fail("S1 staging failed"); _done = true; return; }
            var rc = N.CheckForNewImports();
            Check(rc == 1, "S1 poll Imported (rc=" + rc + ")");
            Check(staged == null || !File.Exists(staged), "S1 staged file consumed");
            var fam = N.MainResource.Get<FAMI>((ushort)13);
            Check(fam != null && fam.FamilyGUIDs.Length == 2,
                "S1 family id 13 with 2 members (got "
                + (fam == null ? "none" : fam.FamilyGUIDs.Length.ToString()) + ")");
            var fams = N.GetFamilyString((ushort)13);
            Check(fams != null && fams.GetString(0) == "Charming",
                "S1 FAMs name 'Charming' (got '" + (fams?.GetString(0) ?? "none") + "')");
            Check(fam != null && fam.Budget == 1373, "S1 budget 1373 preserved (got "
                + (fam?.Budget.ToString() ?? "n/a") + ")");
            Check(N.TutorialHouse == _tutHouse0 && N.TutorialState == _tutState0,
                "S1 tutorial latch unchanged");
            Check(N.LastImportHouse == 0, "S1 family-only import leaves refresh gate closed");
            var nowFiles = Directory.GetFiles(Path.Combine(N.UserPath, "Characters"), "User*.iff");
            Check(nowFiles.Length == _char0 + 2, "S1 two fresh character files ("
                + _char0 + " -> " + nowFiles.Length + ")");
            int parsed = 0;
            foreach (var f in nowFiles.Except(_charFiles0))
            {
                try
                {
                    var cif = new IffFile(f);
                    if (cif.Get<OBJD>((ushort)128) != null) parsed++;
                }
                catch { }
            }
            Check(parsed == 2, "S1 fresh characters carry OBJD 128 (" + parsed + "/2)");
            _charFiles0 = nowFiles; _char0 = nowFiles.Length;
            Next();
        }

        /// <summary>Collision law + house move: re-stage the same FAM (id 13 now
        /// taken) targeting house 5 — the import must claim the id-0 sentinel IN
        /// PLACE (no replace-on-collision) and move into House05.iff.</summary>
        private void PhaseCollisionHouseMove()
        {
            // the pre-existing house-5 claimant (if any) is evicted: fold its
            // member count into the expected character delta.
            FAMI prev;
            N.FamilyForHouse.TryGetValue(5, out prev);
            int prevMembers = prev?.FamilyGUIDs.Length ?? 0;

            var staged = StageFam("Charming_13.FAM", 5, "Charming_13.FAM");
            if (staged == null) { Fail("S2 staging failed"); _done = true; return; }
            var rc = N.CheckForNewImports();
            Check(rc == 1, "S2 poll Imported (rc=" + rc + ")");
            var fam0 = N.MainResource.Get<FAMI>((ushort)0);
            Check(fam0 != null && fam0.FamilyGUIDs.Length == 2 && fam0.HouseNumber == 5,
                "S2 colliding import claims id-0 in place at house 5 (fam0="
                + (fam0 == null ? "none" : fam0.FamilyGUIDs.Length + "m h" + fam0.HouseNumber) + ")");
            FAMI holder;
            N.FamilyForHouse.TryGetValue(5, out holder);
            Check(holder != null && holder.ChunkID == 0, "S2 FamilyForHouse[5] is the id-0 family");
            Check(N.LastImportHouse == 5, "S2 LastImportHouse == 5");
            try
            {
                var h5 = new IffFile(N.GetHousePath(5));
                var hfam = h5.List<FAMI>()?.FirstOrDefault();
                Check(hfam != null && hfam.HouseNumber == 5, "S2 House05.iff carries the imported FAMI");
            }
            catch (Exception e)
            {
                Fail("S2 House05.iff fresh-parse failed: " + e.Message);
            }
            var nowFiles = Directory.GetFiles(Path.Combine(N.UserPath, "Characters"), "User*.iff");
            int expect = _char0 + 2 - prevMembers;
            Check(nowFiles.Length == expect, "S2 character delta ("
                + _char0 + " -> " + nowFiles.Length + ", expected " + expect + ")");
            _b0 = fam0?.Budget ?? -1;
            _v0 = fam0?.ValueInArch ?? -1;
            _charFiles0 = nowFiles; _char0 = nowFiles.Length;
            Next();
        }

        /// <summary>Occupied-house eviction (§4.3 Scenario 2b): Strays_4000 into
        /// the occupied house 5 — the occupant loses the house with net-worth
        /// preservation, its characters are deleted, the import moves in.</summary>
        private void PhaseEviction()
        {
            // expected imported member count: the import skips pets (uChr s13 dog/cat)
            int expectedB = 0;
            try
            {
                var tif = new IffFile(Path.Combine(GlobalSettings.Default.TS1HybridPath,
                    "TemplateFamilyUnleashed", "Strays_4000.FAM"));
                var expi = tif.List<EXPi>()?.FirstOrDefault();
                foreach (var pid in expi.ActiveMemberIDs)
                {
                    var uchr = tif.Get<uChr>((ushort)pid);
                    var kind = uchr?.GetString(13)?.Trim().ToLowerInvariant();
                    if (kind != "dog" && kind != "cat") expectedB++;
                }
            }
            catch (Exception e)
            {
                Fail("S3 Strays template pre-read failed: " + e.Message);
                _done = true;
                return;
            }

            var staged = StageFam("Strays_4000.FAM", 5, "Strays_4000.FAM");
            if (staged == null) { Fail("S3 staging failed"); _done = true; return; }
            var rc = N.CheckForNewImports();
            Check(rc == 1, "S3 poll Imported (rc=" + rc + ")");
            var evicted = N.MainResource.Get<FAMI>((ushort)0);
            Check(evicted != null && evicted.HouseNumber == 0, "S3 occupant loses the house");
            Check(evicted != null && evicted.Budget == _b0 + _v0 && evicted.ValueInArch == 0,
                "S3 net-worth preserved (budget " + _b0 + "+" + _v0 + " -> "
                + (evicted?.Budget.ToString() ?? "n/a") + ", via " + evicted?.ValueInArch + ")");
            Check(evicted != null && (evicted.Unknown & 8) == 0, "S3 flag bit 28 cleared");
            var famB = N.MainResource.Get<FAMI>((ushort)4000);
            Check(famB != null && famB.HouseNumber == 5 && famB.FamilyGUIDs.Length == expectedB,
                "S3 Strays occupies house 5 with " + expectedB + " human members (got "
                + (famB == null ? "none" : famB.FamilyGUIDs.Length.ToString()) + ")");
            FAMI holder;
            N.FamilyForHouse.TryGetValue(5, out holder);
            Check(holder != null && holder.ChunkID == 4000, "S3 FamilyForHouse[5] is the import");
            Check(N.LastImportHouse == 5, "S3 LastImportHouse == 5");
            try
            {
                var h5 = new IffFile(N.GetHousePath(5));
                var hfam = h5.List<FAMI>()?.FirstOrDefault();
                var hstr = h5.List<FAMs>()?.FirstOrDefault();
                Check(hfam != null && hfam.HouseNumber == 5
                    && (hstr?.GetString(0) ?? "") .StartsWith("Strays"),
                    "S3 House05.iff is now the Strays FAM file");
            }
            catch (Exception e)
            {
                Fail("S3 House05.iff fresh-parse failed: " + e.Message);
            }
            var nowFiles = Directory.GetFiles(Path.Combine(N.UserPath, "Characters"), "User*.iff");
            int expect = _char0 - 2 + expectedB;
            Check(nowFiles.Length == expect, "S3 eviction deletes occupant characters ("
                + _char0 + " -> " + nowFiles.Length + ", expected " + expect + ")");
            _charFiles0 = nowFiles; _char0 = nowFiles.Length;
            Next();
        }

        /// <summary>Refusal / silent-skip negatives (acceptance: cancel/refusal,
        /// missing chunks, free/colliding ids, failure cleanup): every refusal
        /// leaves the staged file on disk and the neighborhood unmutated.</summary>
        private void PhaseRefusalNegatives()
        {
            var famiBefore = N.MainResource.List<FAMI>().Count(x => x != null);
            var charsBefore = CharCount();

            // (a) garbage bytes: ValidateImportFile fails -> SILENT skip, file stays
            var garbage = Path.Combine(ImportDir(), "BadGarbage.FAM");
            File.WriteAllBytes(garbage, new byte[80]);
            var rcA = N.CheckForNewImports();
            Check(rcA == 0, "S4a garbage FAM silently skipped (rc=" + rcA + ")");
            Check(File.Exists(garbage), "S4a garbage file stays on disk");
            Check(N.MainResource.List<FAMI>().Count(x => x != null) == famiBefore
                && CharCount() == charsBefore, "S4a no partial mutation");
            File.Delete(garbage); //one file per poll: keep each sub-case isolated

            // (b) EXPi+FAMI but no FAMs (missing chunk): Validate passes, step 4
            // refuses -> poll 2, file stays, no mutation
            var nofams = StageNoFamsFam("BadNoFAMs.FAM");
            if (nofams == null) Fail("S4b negative fixture construction failed");
            var rcB = (nofams == null) ? -99 : N.CheckForNewImports();
            Check(rcB == 2, "S4b missing-FAMs import refused (rc=" + rcB + ")");
            if (nofams != null) Check(File.Exists(nofams), "S4b refused file stays on disk");
            Check(N.MainResource.List<FAMI>().Count(x => x != null) == famiBefore
                && CharCount() == charsBefore, "S4b no partial mutation");
            if (nofams != null && File.Exists(nofams)) File.Delete(nofams);

            // (c) family-only import while id-0 is occupied (colliding id -> 0,
            // but the sentinel slot is taken): structural refusal
            var coll = StageFam("Charming_13.FAM", 0, "Charming_13.FAM");
            var rcC = N.CheckForNewImports();
            Check(rcC == 2, "S4c id-0-occupied family import refused (rc=" + rcC + ")");
            Check(coll == null || File.Exists(coll), "S4c refused file stays on disk");
            Check(N.MainResource.List<FAMI>().Count(x => x != null) == famiBefore
                && CharCount() == charsBefore, "S4c no partial mutation");
            if (coll != null && File.Exists(coll)) File.Delete(coll);
            Next();
        }

        private void PhaseIntegrityVerdict()
        {
            for (int i = 0; i < _gdPaths.Count; i++)
            {
                if (!File.Exists(_gdPaths[i])) { Fail("pinned game-data file vanished: " + _gdPaths[i]); continue; }
                Check(Sha256(_gdPaths[i]) == _gdShas[i],
                    "game-data byte-identical: " + Path.GetFileName(_gdPaths[i]));
            }
            Note("done: fami=" + N.MainResource.List<FAMI>().Count(x => x != null)
                + " (baseline " + _fami0 + ") chars=" + CharCount() + " (baseline " + _char0 + " pre-battery)");
            _done = true;
        }
    }
}
