using FSO.Client;
using FSO.Common;
using FSO.Common.Utils;
using FSO.Content;
using FSO.Content.TS1;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;
using FSO.LotView;
using FSO.SimAntics;
using FSO.SimAntics.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Simitone.Client
{
    /// <summary>
    /// EXP-09 leg 3 (opt-in "exp09io"): pet import/export. Uses SHIPPED pet
    /// artifacts (TemplateFamilyUnleashed/Strays_4000.FAM — a pet family) and
    /// the production machinery only: stage a patched copy into UserData/Import
    /// (FAMI house → 11, FAMI chunk id 4000 → 60; the reserved template id is
    /// never importable), drive CheckForNewImports, verify the imported family
    /// carries pet member GUIDs, PlayHouse(11) to spawn them, then
    /// TS1NeighbourProvider.ExportFamily(60) and verify the export artifact
    /// carries the pets (EXPi members + NBRS records). Zero probe state writes.
    /// </summary>
    public class AutotestExp09Io
    {
        private Action<string> Log;
        private Func<VM> _vm;
        private Func<bool> _inLot;
        private Action<short> _playHouse;
        public bool Passed { get; private set; }
        public bool Done { get; private set; }
        public string Diagnostics = "";
        private List<string> _fails = new List<string>();
        private int _phase;
        private int _ticks;
        private TS1NeighborhoodProvider N;
        private FSO.Files.Formats.IFF.Chunks.FAMI _fami;
        private ushort _famId;
        private int _spawnWait;

        private List<string> _failsPub = new List<string>();
        public string Failures { get { return string.Join("; ", _failsPub); } }

        private void Fail(string m) { _fails.Add(m); _failsPub.Add(m); }
        private void Check(bool ok, string m) { if (!ok) Fail(m); }

        public AutotestExp09Io(Action<string> log, Func<VM> vm, Func<bool> inLot, Action<short> playHouse)
        { Log = log; _vm = vm; _inLot = inLot; _playHouse = playHouse; }

        private string ImportDir() { return Path.Combine(N.UserPath, "Import"); }

        /// <summary>Walk raw IFF chunk headers (type[4] size:u32-be id:u16
        /// flags:u16 label[64] data[size-76]; 60-byte file header + u32 map
        /// offset) and patch the first FAMI: house (payload offset 12, LE) and
        /// chunk id (header +8, u16 BE). FAMI payload is LITTLE-endian.</summary>
        private static bool PatchFirstFami(byte[] bytes, int house, ushort newId)
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
                    bytes[pos + 8] = (byte)(newId >> 8);
                    bytes[pos + 9] = (byte)newId;
                    patched = true;
                }
                pos += (int)size;
            }
            return patched;
        }

        public bool Tick()
        {
            try { return TickInner(); }
            catch (Exception e)
            {
                Fail("EXC " + e.GetType().Name + ": " + e.Message);
                Done = true; return true;
            }
        }

        private bool TickInner()
        {
            if (N == null)
            {
                N = Content.Get().Neighborhood;
                if (N == null || N.MainResource == null || N.UserPath == null) { _ticks++; return false; }
                if (_ticks++ < 30) return false;
                _phase = 1; _ticks = 0;
                return false;
            }
            switch (_phase)
            {
                case 1: PhaseStage(); break;
                case 2: PhaseImport(); break;
                case 3: PhaseSpawn(); break;
                case 4: PhaseExport(); break;
            }
            if (Done)
            {
                Diagnostics = "fails=" + _fails.Count + " " + string.Join("; ", _fails.Take(4));
                Passed = _fails.Count == 0;
                return true;
            }
            return false;
        }

        private void PhaseStage()
        {
            if (_ticks++ < 30) return;
            var src = Path.Combine(GlobalSettings.Default.TS1HybridPath,
                "TemplateFamilyUnleashed", "Strays_4000.FAM");
            if (!File.Exists(src)) { Fail("template pet FAM missing: " + src); Done = true; return; }
            var bytes = File.ReadAllBytes(src);
            if (!PatchFirstFami(bytes, 11, 60)) { Fail("FAMI patch failed"); Done = true; return; }
            var path = Path.Combine(ImportDir(), "Exp09Strays.FAM");
            File.WriteAllBytes(path, bytes);
            var verify = new IffFile(path);
            var fami = verify.List<FAMI>()?.FirstOrDefault();
            if (fami == null || fami.ChunkID != 60 || fami.HouseNumber != 11)
            { Fail("staged pet FAM parse failed (id=" + fami?.ChunkID + " house=" + fami?.HouseNumber + ")"); Done = true; return; }
            if (fami.FamilyGUIDs == null || fami.FamilyGUIDs.Length < 2)
            { Fail("staged pet FAM has " + (fami.FamilyGUIDs?.Length ?? 0) + " member guids (want >=2 pets)"); Done = true; return; }
            Log("AUTOTEST exp09io staged Strays_4000 -> id=60 house=11 members="
                + fami.FamilyGUIDs.Length + " [" + string.Join(",", fami.FamilyGUIDs.Select(g => "0x" + g.ToString("x8"))) + "]");
            _phase = 2; _ticks = 0;
        }

        private void PhaseImport()
        {
            if (_ticks++ < 10) return;
            var rc = N.CheckForNewImports();
            Log("AUTOTEST exp09io CheckForNewImports rc=" + rc);
            _fami = N.MainResource.List<FAMI>()?.FirstOrDefault(f => f != null && f.ChunkID == 60);
            if (_fami == null)
            { Fail("import did not create family 60 (rc=" + rc + ")"); Done = true; return; }
            if (_fami.FamilyGUIDs == null || _fami.FamilyGUIDs.Length < 2)
            { Fail("imported family 60 carries " + (_fami.FamilyGUIDs?.Length ?? 0) + " members (want >=2)"); Done = true; return; }
            Log("AUTOTEST exp09io IMPORTED family 60 house=" + _fami.HouseNumber + " members="
                + _fami.FamilyGUIDs.Length + " [" + string.Join(",", _fami.FamilyGUIDs.Select(g => "0x" + g.ToString("x8"))) + "]");
            _phase = 3; _ticks = 0; _spawnWait = 0;
            N.SetFamilyForHouse(11, _fami, false);
            _playHouse(11);
        }

        private void PhaseSpawn()
        {
            _spawnWait++;
            var vm = _vm();
            if (!_inLot() || vm == null)
            {
                if (_spawnWait > 3000) { Fail("lot 11 never loaded"); Done = true; }
                return;
            }
            var pets = _fami.FamilyGUIDs.Select(g => vm.Entities.OfType<VMAvatar>()
                .FirstOrDefault(a => a.Object.OBJ.GUID == g)).ToList();
            if (pets.All(a => a != null))
            {
                Log("AUTOTEST exp09io SPAWNED f=" + _spawnWait + " oids="
                    + string.Join(",", pets.Select(a => "oid" + a.ObjectID)) + " (pet import runtime-verified)");
                _phase = 4; _ticks = 0;
                return;
            }
            if (_spawnWait > 6000)
            {
                Fail("pet spawn timeout on lot 11: present="
                    + pets.Count(a => a != null) + "/" + pets.Count);
                Done = true;
            }
        }

        private void PhaseExport()
        {
            if (_ticks++ < 30) return;
            var exported = N.ExportFamily(60);
            if (exported == null) { Fail("ExportFamily(60) returned null"); Done = true; return; }
            var expPath = Path.Combine(N.UserPath, "Export", exported);
            if (!File.Exists(expPath)) exported = Path.GetFileName(exported);
            expPath = Path.Combine(N.UserPath, "Export", Path.GetFileName(exported));
            if (!File.Exists(expPath)) { Fail("export artifact missing: " + exported); Done = true; return; }
            var h = new IffFile(expPath);
            var expi = h.List<EXPi>()?.FirstOrDefault();
            var fami = h.List<FAMI>()?.FirstOrDefault();
            var nbrs = h.List<NBRS>()?.FirstOrDefault();
            if (expi == null || fami == null || nbrs == null)
            { Fail("export artifact chunks missing (EXPi/FAMI/NBRS)"); Done = true; return; }
            Check(fami.FamilyGUIDs != null && fami.FamilyGUIDs.Length >= 2, "export FAMI carries the pets");
            Check(expi.ActiveMemberIDs.Length >= 2, "export EXPi active members >= 2");
            Check(nbrs.Entries.Count >= 2, "export NBRS carries >= 2 pet records");
            Log("AUTOTEST exp09io EXPORT artifact=" + Path.GetFileName(expPath)
                + " expiActive=" + expi.ActiveMemberIDs.Length + " nbrs=" + nbrs.Entries.Count
                + " famiMembers=" + (fami.FamilyGUIDs?.Length ?? 0));
            if (_fails.Count == 0)
                Log("AUTOTEST exp09io *** PET IMPORT/EXPORT VERIFIED *** (import spawn + export artifact)");
            Done = true;
        }
    }
}
