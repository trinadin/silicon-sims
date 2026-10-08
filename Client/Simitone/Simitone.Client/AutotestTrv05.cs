using FSO.Content;
using FSO.Content.TS1;
using FSO.SimAntics;
using FSO.SimAntics.Primitives;
using Simitone.Client.UI.Screens;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Simitone.Client
{
    /// <summary>
    /// TRV-05 (opt-in "trv05"): the vacation booking persistence law through
    /// the REAL travel path. Native decode (sims-complete-pef):
    /// Family::DoStream 0x1006d710 field 8 = the vacation rental
    /// (Family+0x13C), one int after the GUID list for version > 7;
    /// cSimsApp::LoadGame 0x1024f7f0 (trip flag +0x109) writes
    /// family->0x13c = lot on vacation arrival and saves the neighborhood
    /// IMMEDIATELY; Neighborhood::RemoveFromVacation 0x100a83e0 clears the
    /// field and saves again on the return home; GenericTS1Call case 27
    /// counts rentals occupied by that field.
    ///
    /// The battery: (1) the FAMI codec round-trips the field in the v9 shape
    /// and keeps the v7 shape for bookless families; (2) entering the home
    /// lot and travelling to a Vacation Island rental (real
    /// vm.SignalLotSwitch, the mode-17 arrival funnel) books the family
    /// in-memory AND on disk; (3) the mode-27 occupancy count sees the
    /// booking; (4) travelling home clears it, in-memory AND on disk.
    /// </summary>
    public class AutotestTrv05
    {
        private Action<string> Log;
        public bool Passed { get; private set; }
        public bool Done { get; private set; }
        public string Diagnostics = "";
        private List<string> _fails = new List<string>();

        private Func<VM> _vm;
        private Func<TS1GameScreen> _screen;
        private Action<short> _playHouse;
        private Action<uint> _signalLotSwitch;

        private int _phase;
        private int _frames;
        private short _homeLot = -1;

        public AutotestTrv05(Action<string> log, Func<VM> vm, Func<TS1GameScreen> screen,
            Action<short> playHouse, Action<uint> signalLotSwitch)
        {
            Log = log;
            _vm = vm;
            _screen = screen;
            _playHouse = playHouse;
            _signalLotSwitch = signalLotSwitch;
        }

        private void Check(bool ok, string m)
        {
            if (!ok) { _fails.Add(m); Log("AUTOTEST trv05 FAIL " + m); }
        }

        private static FSO.Files.Formats.IFF.Chunks.FAMI FamilyOnDisk(TS1NeighborhoodProvider provider, int chunkID)
        {
            // fresh parse of the written Neighborhood.iff — proves the booking
            // reached the FILE, not just the in-memory chunk list
            var path = Path.Combine(provider.UserPath, "Neighborhood.iff");
            if (!File.Exists(path)) return null;
            var iff = new FSO.Files.Formats.IFF.IffFile(path);
            return iff.List<FSO.Files.Formats.IFF.Chunks.FAMI>().FirstOrDefault(f => f.ChunkID == chunkID);
        }

        public bool Tick()
        {
            if (Done) return true;
            var provider = Content.Get().Neighborhood;
            var screen = _screen();

            switch (_phase)
            {
                case 0: // pure codec law, no game state needed
                    {
                        var fam = new FSO.Files.Formats.IFF.Chunks.FAMI
                        {
                            ChunkID = 4242, HouseNumber = 5, FamilyNumber = 77, Budget = 4321,
                            FamilyGUIDs = new uint[] { 0xAA000001, 0xAA000002 },
                            VacationHouseNumber = 44
                        };
                        using (var ms = new MemoryStream())
                        {
                            fam.Write(null, ms); ms.Position = 0;
                            var rt = new FSO.Files.Formats.IFF.Chunks.FAMI();
                            rt.Read(null, ms);
                            Check(rt.VacationHouseNumber == 44, "codec v9 roundtrip field (got " + rt.VacationHouseNumber + ")");
                            Check(rt.Version == 9, "codec v9 promotion (got " + rt.Version + ")");
                            Check(ms.Position == ms.Length, "codec v9 stream exhausted (rem " + (ms.Length - ms.Position) + ")");
                        }
                        var plain = new FSO.Files.Formats.IFF.Chunks.FAMI
                        {
                            ChunkID = 4243, HouseNumber = 6, FamilyNumber = 78, Budget = 10,
                            FamilyGUIDs = new uint[] { 0xBB000001 }
                        };
                        using (var ms = new MemoryStream())
                        {
                            plain.Write(null, ms);
                            Check(ms.Length == 44, "codec v7 shape unchanged 40+4n (got " + ms.Length + ")");
                            ms.Position = 0;
                            var rt = new FSO.Files.Formats.IFF.Chunks.FAMI();
                            rt.Read(null, ms);
                            Check(rt.VacationHouseNumber == 0, "codec v7 no booking (got " + rt.VacationHouseNumber + ")");
                        }
                        var spellFam = new FSO.Files.Formats.IFF.Chunks.FAMI
                        {
                            ChunkID = 4244, HouseNumber = 7, FamilyNumber = 79, Budget = 11,
                            FamilyGUIDs = new uint[] { 0xCC000001 },
                            VacationHouseNumber = 41,
                            SpellWords = new short[] { 1, 2, 3, 0, 0, 5 }
                        };
                        using (var ms = new MemoryStream())
                        {
                            spellFam.Write(null, ms); ms.Position = 0;
                            var rt = new FSO.Files.Formats.IFF.Chunks.FAMI();
                            rt.Read(null, ms);
                            Check(rt.VacationHouseNumber == 41, "codec v9+spells field (got " + rt.VacationHouseNumber + ")");
                            Check(rt.SpellWords != null && rt.SpellWords.SequenceEqual(new short[] { 1, 2, 3, 0, 0, 5 }),
                                "codec v9+spells block after field");
                        }
                        // clean-slate observables on the live neighborhood
                        var fams = provider.MainResource.List<FSO.Files.Formats.IFF.Chunks.FAMI>();
                        Check(fams.All(f => f == null || f.VacationHouseNumber == 0), "clean slate: no bookings");
                        Check(VMGenericTS1Call.CountOccupiedVacationLots(fams) == 0, "clean slate: mode27 occupied==0");
                        Log("AUTOTEST trv05 p0 codec law verified (v9 field/promotion, v7 canon shape, v9+spells order)");
                        _phase = 1; _frames = 0;
                    }
                    return false;

                case 1: // enter the family's home lot (the real lot entry path)
                    if (screen == null || screen.InLot) { if (++_frames > 300) { Check(false, "p1 timeout entering home lot"); _phase = 9; } return false; }
                    var homeFam = provider.MainResource.List<FSO.Files.Formats.IFF.Chunks.FAMI>()
                        .FirstOrDefault(f => f != null && f.HouseNumber > 0 && f.HouseNumber < 21 && f.FamilyGUIDs.Length > 0);
                    if (homeFam == null) { Check(false, "p1 no home family in neighborhood"); _phase = 9; return false; }
                    _homeLot = (short)homeFam.HouseNumber;
                    _homeChunk = homeFam.ChunkID;
                    Log("AUTOTEST trv05 p1 entering home lot " + _homeLot + " (family chunk " + _homeChunk + ")");
                    _playHouse(_homeLot);
                    _phase = 2; _frames = 0;
                    return false;

                case 2: // wait for the home lot vm + family
                    if (_screen()?.vm == null || _screen()?.ActiveFamily == null)
                    {
                        if (++_frames > 600) { Check(false, "p2 timeout home lot vm/family"); _phase = 9; }
                        return false;
                    }
                    Log("AUTOTEST trv05 p2 home lot ready; active family house=" + _screen().ActiveFamily.HouseNumber);
                    _signalLotSwitch(44); // Vacation Island rental 44 (the mode-17 arrival funnel)
                    _phase = 3; _frames = 0;
                    return false;

                case 3: // arrival on the rental: booking written in-memory + on disk
                    {
                        var fam = _screen()?.ActiveFamily;
                        if (fam == null || fam.VacationHouseNumber != 44)
                        {
                            if (++_frames > 600) { Check(false, "p3 timeout booking write (vac=" + (fam?.VacationHouseNumber ?? -1) + ")"); _phase = 9; }
                            return false;
                        }
                        Log("AUTOTEST trv05 p3 ARRIVAL BOOKED: family " + fam.ChunkID + " vacation house " + fam.VacationHouseNumber
                            + " (native law: family+0x13c := lot + immediate Neighborhood::Save)");
                        var memFam = provider.MainResource.List<FSO.Files.Formats.IFF.Chunks.FAMI>()
                            .FirstOrDefault(f => f != null && f.ChunkID == _homeChunk);
                        Check(memFam != null && memFam.VacationHouseNumber == 44,
                            "p3 booking in MainResource FAMI (got " + (memFam?.VacationHouseNumber.ToString() ?? "null") + ")");
                        var diskFam = FamilyOnDisk(provider, _homeChunk);
                        Check(diskFam != null && diskFam.VacationHouseNumber == 44,
                            "p3 booking on DISK Neighborhood.iff (got " + (diskFam?.VacationHouseNumber.ToString() ?? "null") + ")");
                        var occupied = VMGenericTS1Call.CountOccupiedVacationLots(
                            provider.MainResource.List<FSO.Files.Formats.IFF.Chunks.FAMI>());
                        Check(occupied == 1, "p3 mode27 occupied==1 (got " + occupied + ")");
                        var curLot = 0; try { curLot = _screen()?.vm?.GetGlobalValue(10) ?? 0; } catch { }
                        Log("AUTOTEST trv05 p3 lot globals: current=" + curLot);
                        _signalLotSwitch(0xFFFFFFFF); // home (native RemoveFromVacation edge)
                        _phase = 4; _frames = 0;
                    }
                    return false;

                case 4: // return home: booking cleared in-memory + on disk
                    {
                        var fam = _screen()?.ActiveFamily;
                        if (fam == null || fam.VacationHouseNumber != 0)
                        {
                            if (++_frames > 600) { Check(false, "p4 timeout booking clear (vac=" + (fam?.VacationHouseNumber ?? -1) + ")"); _phase = 9; }
                            return false;
                        }
                        Log("AUTOTEST trv05 p4 RETURN CLEARED (native RemoveFromVacation law: +0x13c := 0 + Neighborhood::Save)");
                        var memFam = provider.MainResource.List<FSO.Files.Formats.IFF.Chunks.FAMI>()
                            .FirstOrDefault(f => f != null && f.ChunkID == _homeChunk);
                        Check(memFam != null && memFam.VacationHouseNumber == 0, "p4 clear in MainResource FAMI");
                        var diskFam = FamilyOnDisk(provider, _homeChunk);
                        Check(diskFam != null && diskFam.VacationHouseNumber == 0, "p4 clear on DISK Neighborhood.iff");
                        var occupied = VMGenericTS1Call.CountOccupiedVacationLots(
                            provider.MainResource.List<FSO.Files.Formats.IFF.Chunks.FAMI>());
                        Check(occupied == 0, "p4 mode27 occupied==0 (got " + occupied + ")");
                        Log("AUTOTEST trv05 p4 vacation booking round-trip complete (book 44 -> persist -> clear -> persist)");
                        _phase = 9;
                    }
                    return false;

                case 9:
                    if (_fails.Count == 0)
                        Log("AUTOTEST trv05 *** VACATION BOOKING LAW VERIFIED ***");
                    Diagnostics = "fails=" + _fails.Count + " " + string.Join("; ", _fails.Take(4));
                    Passed = _fails.Count == 0;
                    Done = true;
                    return true;
            }
            return false;
        }

        private int _homeChunk;
    }
}
