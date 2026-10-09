using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Common;
using FSO.Common.Rendering.Framework.IO;
using FSO.Common.Rendering.Framework.Model;
using FSO.Content;
using FSO.Content.TS1;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;
using Microsoft.Xna.Framework;
using Simitone.Client.UI.Panels;
using Simitone.Client.UI.Screens;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Simitone.Client
{
    /// <summary>
    /// NBR-07 opt-in pin ("nbr07"): the expansion-venue evict laws + the two
    /// closed base-hood residuals —
    ///   (a) the four venue EvictModeLotHandler variants (raw file offsets in
    ///       `The Sims Complete`): cWinDowntown @0x3fdb10 and
    ///       cWinStudiotown @0x4795c0 answer an OCCUPIED click with a silent
    ///       no-op (no dialog, no executor); cWinVacation @0x4401c0 runs the
    ///       STR# 131 [18]/[19] vacation-evict confirm and mounts NO dialog on
    ///       the vacant+unbuilt leg (sounds only); cWinMagicland @0x5840d0
    ///       runs the UL net-worth confirm with the VENUE [14]/[15] bulldoze
    ///       pair (its family-home-on-vacation pre-scan → [16]/[17] is
    ///       unreachable port-side — no vacation state; decode-banked);
    ///   (b) the venue confirm pair STR# 131 [14]/[15] ("Bulldoze Lot?"/"Do
    ///       you want to bulldoze this lot?") — every venue LoadUIStrings
    ///       overwrites slot 0/1 with rows 15/16 (GetString is 1-based);
    ///   (c) the zone-choice strings: UIText STR# 250 'Rezone Mode strings'
    ///       [0]/[1] + STR# 251 'Zoning Types' [0]/[1] (the ZoningData
    ///       captions) — the DIALOG itself is WIRED as of NBR-08 (mounted
    ///       ahead of the cascade with same-zone picks inert; pinned by this
    ///       probe's PhaseZoneChoice);
    ///   (d) the UCP hide-slot pairing law: IsLiveModeDisabled (CPState+0x55)
    ///       drives BUILD+CAMERA; IsBuyAndBuildDisabled (CPState+0x54) drives
    ///       BUY+LIVE (TS1GameScreen.NativeUcpDisablePairing).
    ///
    /// Driven through the REAL toolbar arm + production lot-click routing with
    /// the panel's SetViewModeForProbe seam (the venue branch keys off the
    /// MOUNTED view mode; remounting the whole screen per venue is not needed
    /// for the branch law — the lot-band gating is PopulateScreen's, already
    /// pinned by uinbhd). Template lots: 11 = vacant+unbuilt, 6 = vacant+built,
    /// 3 = vacant+built for occupancy synthesis (bin FAMI 4).
    /// </summary>
    public class AutotestNbr07
    {
        private Action<string> _log;
        private int _phase, _phaseTicks;
        private bool _done, _passed = true;
        private readonly List<string> _notes = new List<string>();
        private readonly List<string> _fails = new List<string>();
        private TS1NeighborhoodProvider N;
        private TS1GameScreen Screen;
        private UINeighbourhoodSwitcher Switcher;
        private UINeighborhoodSelectionPanel Panel;

        // isolation (UI-30/nbr05ui/nbr06 idiom, workspace-persistent: INSIDE
        // the launcher's private userdir)
        private static string _priorUserDir;
        internal static string RedirectDir;
        internal static string IsolationError;

        internal static bool BeginIsolation(string checksCsv)
        {
            var c = checksCsv?.Trim().Trim('"');
            if (string.IsNullOrEmpty(c)) return false;
            var names = c.Split(',').Select(s => s.Trim().ToLowerInvariant()).ToList();
            if (!names.Contains("nbr07")) return false;
            try
            {
                _priorUserDir = FSOEnvironment.UserDir;
                RedirectDir = Path.Combine(_priorUserDir ?? ".",
                    "nbr07-fresh-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
                Directory.CreateDirectory(RedirectDir);
                FSOEnvironment.UserDir = RedirectDir;
                return true;
            }
            catch (Exception e)
            {
                IsolationError = e.GetType().Name + ": " + e.Message;
                return true;
            }
        }

        public bool Passed { get { return _passed && _fails.Count == 0; } }
        public string Diagnostics { get { return string.Join("; ", _notes); } }
        public string Failures { get { return string.Join("; ", _fails); } }

        public AutotestNbr07(Action<string> log)
        {
            _log = log;
            if (IsolationError != null)
            {
                Fail("isolation unavailable: " + IsolationError);
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
                    case 1: PhaseStrings(); break;
                    case 2: PhasePureLaws(); break;
                    case 3: PhaseVenueQuiet(); break;
                    case 4: PhaseVenueVacation(); break;
                    case 5: PhaseVenueMagic(); break;
                    case 6: PhaseZoneChoice(); break;       // NBR-08
                    case 7: PhaseBulldozeSounds(); break;   // NBR-08
                    case 8: PhaseRestore(); break;
                    default:
                        _done = true;
                        break;
                }
            }
            catch (Exception e)
            {
                var at = (e.StackTrace ?? "").Split('\n').FirstOrDefault()
                    ?.TrimStart().Split('(').FirstOrDefault();
                var msg = e.GetType().Name + " " + e.Message;
                for (var ie = e.InnerException; ie != null; ie = ie.InnerException)
                    msg += " || INNER " + ie.GetType().Name + " " + ie.Message;
                Fail("EXC phase " + _phase + ": " + msg
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
            _log("AUTOTEST nbr07: FAIL " + what);
        }

        // --- helpers (UI-30 / nbr05ui / nbr06 idioms) ----------------------

        private static readonly MethodInfo MouseEvent = typeof(UIButton).GetMethod("OnMouseEvent",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private static UpdateState FreshState()
        {
            return new UpdateState
            {
                Time = new GameTime(),
                WindowFocused = true,
                InputManager = new FSO.Common.Rendering.Framework.IO.InputManager()
            };
        }

        private void Press(UIButton button)
        {
            var state = FreshState();
            MouseEvent.Invoke(button, new object[] { UIMouseEventType.MouseDown, state });
            MouseEvent.Invoke(button, new object[] { UIMouseEventType.MouseUp, state });
        }

        private UIOriginalNavbarButton ToolbarButton(string member)
        {
            return Switcher.Buttons.FirstOrDefault(b => b.Member == member);
        }

        private void ArmBulldoze()
        {
            if (!Switcher.BulldozeArmed) Press(ToolbarButton("NghUI\\Bulldoze.bmp"));
        }

        private void ArmRezone()
        {
            if (!Switcher.RezoneArmed) Press(ToolbarButton("NghUI\\Rezone.bmp"));
        }

        private void DisarmAll()
        {
            if (Switcher.BulldozeArmed || Switcher.RezoneArmed)
                Press(ToolbarButton("NghUI\\InetBtn.bmp"));
        }

        // NBR-08: the zone-choice dialog's buttons — Yes = STR# 251 [0]
        // "Residential" (target 0), No = [1] "Community" (target 1).
        private UIButton RezoneChoiceButton(int target)
        {
            UIButton btn = null;
            Screen._rezoneChoiceDialog?.ButtonMap.TryGetValue(
                target == 0 ? UIAlertButtonType.Yes : UIAlertButtonType.No, out btn);
            return btn;
        }

        // NBR-08 sound pins (the AUD-20 event-snapshot idiom): the ordered
        // ui_nhood_bdoze* dispatch names in the trace window (other UI sounds
        // are filtered out), and whether any of them failed to resolve.
        private static List<string> BdozeSequence()
        {
            var names = new List<string>();
            foreach (var e in FSO.HIT.HITTrace.Snapshot())
                if (e.Kind == FSO.HIT.HITTrace.KIND_EVENT && e.EventName != null
                    && e.EventName.StartsWith("ui_nhood_bdoze"))
                    names.Add(e.EventName);
            return names;
        }

        private static bool BdozeAllResolved()
        {
            foreach (var e in FSO.HIT.HITTrace.Snapshot())
                if (e.Kind == FSO.HIT.HITTrace.KIND_EVENT && e.EventName != null
                    && e.EventName.StartsWith("ui_nhood_bdoze")
                    && e.Result == FSO.HIT.HITTrace.RES_NOT_FOUND) return false;
            return true;
        }

        private static string BdozeJoin(List<string> seq)
        {
            return string.Join(",", seq.ToArray());
        }

        private UIButton BulldozeDialogButton(UIAlertButtonType type)
        {
            UIButton btn = null;
            Screen._bulldozeDialog?.ButtonMap.TryGetValue(type, out btn);
            return btn;
        }

        private string HousePath(int house)
        {
            return Path.Combine(N.UserPath, "Houses", "House" + house.ToString("00") + ".iff");
        }

        private static Tuple<int, int> SimiBuildingValues(string housePath)
        {
            var iff = new IffFile(housePath);
            var simi = iff.Get<SIMI>(1);
            return simi == null ? null : Tuple.Create((int)simi.ObjectsValue, (int)simi.ArchitectureValue);
        }

        // --- phases ---------------------------------------------------------

        private void PhaseReady()
        {
            Screen = GameFacade.Screens?.CurrentUIScreen as TS1GameScreen;
            N = Content.Get().Neighborhood;
            if (Screen == null || N == null || N.MainResource == null || N.UserPath == null) return;
            if (_phaseTicks < 30) return; // let the neighborhood screen settle

            Switcher = Screen.TS1NeighSwitcher as UINeighbourhoodSwitcher;
            Check(Switcher != null, "neighborhood-switcher-mounted");
            Panel = Screen.TS1NeighPanel;
            Check(Panel != null, "neighborhood-selection-panel-mounted");
            Check(Directory.Exists(RedirectDir) && N.UserPath.StartsWith(RedirectDir),
                "isolated-fresh-userdir (" + N.UserPath + ")");
            Check(Panel.CurrentViewMode == 4, "starts-on-base-view-mode-4");
            // Seed any missing template house files (the nbr06 idiom).
            try
            {
                var srcDir = Path.Combine(FSO.Content.Content.Get().TS1BasePath, "UserData", "Houses");
                var dstDir = Path.Combine(N.UserPath, "UserData", "Houses");
                Directory.CreateDirectory(dstDir);
                foreach (var f in Directory.GetFiles(srcDir, "House*.iff"))
                {
                    var to = Path.Combine(dstDir, Path.GetFileName(f));
                    if (!File.Exists(to)) File.Copy(f, to);
                }
            }
            catch { /* seeding is best-effort; phases fail loudly if a lot is missing */ }
            if (!_passed) { _done = true; return; }
            Next();
        }

        private void PhaseStrings()
        {
            // The NBR-07 recovered pairs (UIText.iff, byte-verbatim).
            Check(GameFacade.Strings.GetString("131", "14") == "Bulldoze Lot?", "str131-14-bulldoze-lot");
            Check(GameFacade.Strings.GetString("131", "15") == "Do you want to bulldoze this lot?",
                "str131-15-bulldoze-lot-question");
            Check(GameFacade.Strings.GetString("131", "16")
                == "This family is currently on vacation. Strict laws forbid evictions of Sims while they are away from home.",
                "str131-16-vacation-refusal");
            Check(GameFacade.Strings.GetString("131", "17") == "Not So Fast!", "str131-17-not-so-fast");
            Check(GameFacade.Strings.GetString("131", "18")
                == "There is a family on vacation on this lot. If you evict them from the lot, the family will be sent home. Are you sure you want to evict this family?",
                "str131-18-vacation-evict");
            Check(GameFacade.Strings.GetString("131", "19") == "Are you sure?", "str131-19-are-you-sure");
            Check(GameFacade.Strings.GetString("131", "20")
                == "A family is visiting this lot. You have to send them home before you can bulldoze this lot.",
                "str131-20-visiting-refusal (import-flow leg, decode-banked)");
            Check(GameFacade.Strings.GetString("250", "0") == "Rezone House?", "str250-0-rezone-house");
            Check(GameFacade.Strings.GetString("250", "1")
                == "This lot is currently zoned as a %s lot. Please choose what type of lot you want this to be:",
                "str250-1-choice-format");
            Check(GameFacade.Strings.GetString("251", "0") == "Residential", "str251-0-residential");
            Check(GameFacade.Strings.GetString("251", "1") == "Community", "str251-1-community");

            // The production getters resolve to the native strings.
            Check(TS1GameScreen.BulldozeLotTitle == "Bulldoze Lot?", "getter-bulldoze-lot-title-native");
            Check(TS1GameScreen.BulldozeLotMessage == "Do you want to bulldoze this lot?",
                "getter-bulldoze-lot-message-native");
            Check(TS1GameScreen.VacationEvictTitle == "Are you sure?", "getter-vacation-evict-title-native");
            Check((TS1GameScreen.VacationEvictMessage ?? "").StartsWith("There is a family on vacation on this lot."),
                "getter-vacation-evict-message-native");
            Check(TS1GameScreen.RezoneChoiceTitle == "Rezone House?", "getter-rezone-choice-title-native");
            Check((TS1GameScreen.RezoneChoiceMessage(0) ?? "").Contains("Residential")
                && (TS1GameScreen.RezoneChoiceMessage(0) ?? "").Contains("%s") == false,
                "getter-choice-message-formats-residential");
            Check((TS1GameScreen.RezoneChoiceMessage(1) ?? "").Contains("Community"),
                "getter-choice-message-formats-community");
            Check(TS1GameScreen.NativeZoneName(0) == "Residential"
                && TS1GameScreen.NativeZoneName(1) == "Community",
                "getter-zone-names-native");
            if (!_passed) { _done = true; return; }
            Next();
        }

        private void PhasePureLaws()
        {
            // The decoded UCP hide-slot pairing (UpdateViewFromCPState raw
            // 0x2b3e40 tail): +0x54 (buy&build disabled) → BUY+LIVE; +0x55
            // (live disabled) → BUILD+CAMERA.
            bool buy, build, live, camera;
            TS1GameScreen.NativeUcpDisablePairing(false, false, out buy, out build, out live, out camera);
            Check(!buy && !build && !live && !camera, "ucp-none-disabled-when-clear");
            TS1GameScreen.NativeUcpDisablePairing(true, false, out buy, out build, out live, out camera);
            Check(buy && live && !build && !camera, "ucp-buybuild-drive-pairs-BUY-LIVE");
            TS1GameScreen.NativeUcpDisablePairing(false, true, out buy, out build, out live, out camera);
            Check(!buy && !live && build && camera, "ucp-live-drive-pairs-BUILD-CAMERA");
            TS1GameScreen.NativeUcpDisablePairing(true, true, out buy, out build, out live, out camera);
            Check(buy && live && build && camera, "ucp-both-drives-all-four");
            if (!_passed) { _done = true; return; }
            Next();
        }

        private void PhaseVenueQuiet()
        {
            // cWinDowntown @0x3fdb10 / cWinStudiotown @0x4795c0: the occupied
            // leg is a SILENT no-op; the vacant legs use the venue [14]/[15]
            // pair; the unbuilt leg keeps the 132 [4]/[5] nothing-dialog.
            var fam = N.GetFamily(4);
            Check(fam != null, "family-4-found");
            if (fam == null) { _done = true; return; }
            Check(N.GetFamilyForHouse(3) == null, "lot3-vacant");
            var simi3 = SimiBuildingValues(HousePath(3));
            Check(simi3 != null && (simi3.Item1 > 0 || simi3.Item2 > 0), "lot3-built");
            if (simi3 == null || N.GetFamilyForHouse(3) != null)
            { Fail("template-lot3-not-vacant-built"); _done = true; return; }
            N.SetFamilyForHouse(3, fam, false);
            var noops0 = Screen.VenueOccupiedNoOpsForProbe;
            var ev0 = Screen.ArmedEvictsForProbe;
            var bd0 = Screen.ArmedBulldozesForProbe;

            Panel.SetViewModeForProbe(2);   // Downtown
            ArmBulldoze();
            Panel.SelectHouse(3);
            Check(Screen.VenueOccupiedNoOpsForProbe == noops0 + 1, "dt-occupied-silent-noop-counted");
            Check(Screen._bulldozeDialog == null, "dt-occupied-mounted-no-dialog");
            Check(Screen.ArmedEvictsForProbe == ev0 && Screen.ArmedBulldozesForProbe == bd0,
                "dt-occupied-made-no-backend-call");
            DisarmAll();

            // Vacant+built on the venue pair: NO is inert (lot 6).
            Check(N.GetFamilyForHouse(6) == null, "lot6-vacant");
            var simi6 = SimiBuildingValues(HousePath(6));
            Check(simi6 != null && (simi6.Item1 > 0 || simi6.Item2 > 0), "lot6-built");
            if (simi6 == null || N.GetFamilyForHouse(6) != null)
            { Fail("template-lot6-not-vacant-built"); _done = true; return; }

            Panel.SetViewModeForProbe(5);   // Studio Town
            ArmBulldoze();
            Panel.SelectHouse(6);
            var confirm = Screen._bulldozeDialog;
            Check(confirm != null, "st-vacant-built-confirm-mounted");
            if (confirm != null)
            {
                Check(confirm.TitleTextForProbe == "Bulldoze Lot?", "st-confirm-title-131-14");
                Check(confirm.MessageTextForProbe == "Do you want to bulldoze this lot?",
                    "st-confirm-message-131-15");
            }
            var no = BulldozeDialogButton(UIAlertButtonType.No);
            Check(no != null, "st-confirm-has-no");
            if (no != null) Press(no);
            Check(Screen.ArmedBulldozesForProbe == bd0, "st-confirm-no-made-no-backend-call");

            // YES bulldozes through the production arm.
            ArmBulldoze();
            Panel.SelectHouse(6);
            var yes = BulldozeDialogButton(UIAlertButtonType.Yes);
            Check(yes != null, "st-confirm-has-yes");
            if (yes != null) Press(yes);
            Check(Screen.ArmedBulldozesForProbe == bd0 + 1, "st-confirm-yes-ran-bulldozelot");
            var after = SimiBuildingValues(HousePath(6));
            Check(after != null && after.Item1 == 0 && after.Item2 == 0,
                "st-vacant-built-bulldoze-zeroed-simi");
            var ok = BulldozeDialogButton(UIAlertButtonType.OK);
            if (ok != null) Press(ok);   // the receipt
            DisarmAll();
            // Release the synthesis lot through the production backend (the
            // DT no-op leg deliberately evicted nothing).
            Check(N.MoveOut((short)3, false) == 1, "dt-cleanup-moveout");
            Panel.SetViewModeForProbe(4);
            if (!_passed) { _done = true; return; }
            Next();
        }

        private void PhaseVenueVacation()
        {
            // cWinVacation @0x4401c0: occupied → the [18]/[19] confirm; built
            // sub-confirm = the venue [14]/[15] pair; vacant+unbuilt → NO
            // dialog at all (sounds only).
            var fam = N.GetFamily(4);
            Check(fam != null, "family-4-found-vacation");
            if (fam == null) { _done = true; return; }
            Check(N.GetFamilyForHouse(3) == null, "lot3-vacant-vacation");
            var simi3 = SimiBuildingValues(HousePath(3));
            Check(simi3 != null && (simi3.Item1 > 0 || simi3.Item2 > 0), "lot3-built-vacation");
            if (simi3 == null || N.GetFamilyForHouse(3) != null)
            { Fail("template-lot3-not-vacant-built-vacation"); _done = true; return; }
            N.SetFamilyForHouse(3, fam, false);
            var members0 = fam.FamilyGUIDs.Length;
            var confirms0 = Screen.VenueVacationEvictConfirmsForProbe;
            var ev0 = Screen.ArmedEvictsForProbe;
            var noDlg0 = Screen.VenueVacationNoDialogForProbe;

            Panel.SetViewModeForProbe(3);   // Vacation Island
            ArmBulldoze();
            Panel.SelectHouse(3);
            var confirm1 = Screen._bulldozeDialog;
            Check(Screen.VenueVacationEvictConfirmsForProbe == confirms0 + 1, "vac-occupied-confirm-counted");
            Check(confirm1 != null, "vac-occupied-confirm-mounted");
            if (confirm1 != null)
            {
                Check(confirm1.TitleTextForProbe == "Are you sure?", "vac-confirm-title-131-19");
                Check((confirm1.MessageTextForProbe ?? "")
                    .StartsWith("There is a family on vacation on this lot."),
                    "vac-confirm-message-131-18");
            }
            Press(BulldozeDialogButton(UIAlertButtonType.Yes));
            var confirm2 = Screen._bulldozeDialog;
            Check(confirm2 != null, "vac-occupied-second-confirm-mounted (built)");
            if (confirm2 != null)
            {
                Check(confirm2.TitleTextForProbe == "Bulldoze Lot?", "vac-second-confirm-title-131-14");
                Check(confirm2.MessageTextForProbe == "Do you want to bulldoze this lot?",
                    "vac-second-confirm-message-131-15");
            }
            Press(BulldozeDialogButton(UIAlertButtonType.No));   // keep the house
            Check(Screen.ArmedEvictsForProbe == ev0 + 1, "vac-confirm2-no-ran-moveout");
            Check(N.GetFamilyForHouse(3) == null, "vac-evict-unbound-family");
            Check(fam.FamilyGUIDs.Length == members0, "vac-killSims-false-kept-members");
            var after = SimiBuildingValues(HousePath(3));
            Check(after != null && after.Item1 == simi3.Item1 && after.Item2 == simi3.Item2,
                "vac-killSims-false-house-standing");
            var evictReceipt = BulldozeDialogButton(UIAlertButtonType.OK);
            if (evictReceipt != null) Press(evictReceipt);   // the evict receipt

            // Vacant+unbuilt: NO dialog — the sound-only leg (lot 11).
            Check(N.GetFamilyForHouse(11) == null, "lot11-vacant-vacation");
            var simi11 = SimiBuildingValues(HousePath(11));
            Check(simi11 != null && simi11.Item1 == 0 && simi11.Item2 == 0, "lot11-unbuilt-vacation");
            var bd0 = Screen.ArmedBulldozesForProbe;
            ArmBulldoze();
            Panel.SelectHouse(11);
            Check(Screen.VenueVacationNoDialogForProbe == noDlg0 + 1, "vac-unbuilt-sound-only-counted");
            Check(Screen._bulldozeDialog == null, "vac-unbuilt-mounted-no-dialog");
            Check(Screen.ArmedEvictsForProbe == ev0 + 1 && Screen.ArmedBulldozesForProbe == bd0,
                "vac-unbuilt-made-no-backend-call");
            DisarmAll();
            Panel.SetViewModeForProbe(4);
            if (!_passed) { _done = true; return; }
            Next();
        }

        private void PhaseVenueMagic()
        {
            // cWinMagicland @0x5840d0: the occupied leg keeps the UL net-worth
            // confirm ([2]/[3]) but the built sub-confirm is the VENUE [14]/[15]
            // pair. (The [16]/[17] home-family-on-vacation pre-scan refusal is
            // unreachable port-side — no vacation state; decode-banked.)
            var fam = N.GetFamily(4);
            Check(fam != null, "family-4-found-magic");
            if (fam == null) { _done = true; return; }
            Check(N.GetFamilyForHouse(3) == null, "lot3-vacant-magic");
            var simi3 = SimiBuildingValues(HousePath(3));
            Check(simi3 != null && (simi3.Item1 > 0 || simi3.Item2 > 0), "lot3-built-magic");
            if (simi3 == null || N.GetFamilyForHouse(3) != null)
            { Fail("template-lot3-not-vacant-built-magic"); _done = true; return; }
            N.SetFamilyForHouse(3, fam, false);
            var ev0 = Screen.ArmedEvictsForProbe;

            Panel.SetViewModeForProbe(7);   // Magic Town
            ArmBulldoze();
            Panel.SelectHouse(3);
            var confirm1 = Screen._bulldozeDialog;
            Check(confirm1 != null, "magic-occupied-confirm1-mounted");
            if (confirm1 != null)
            {
                Check(confirm1.TitleTextForProbe == "Evict Family?", "magic-confirm1-title-131-2");
                Check((confirm1.MessageTextForProbe ?? "").Contains("net worth"),
                    "magic-confirm1-message-131-3-format");
            }
            Press(BulldozeDialogButton(UIAlertButtonType.Yes));
            var confirm2 = Screen._bulldozeDialog;
            Check(confirm2 != null, "magic-occupied-second-confirm-mounted (built)");
            if (confirm2 != null)
            {
                Check(confirm2.TitleTextForProbe == "Bulldoze Lot?", "magic-second-confirm-title-131-14-VENUE-PAIR");
                Check(confirm2.MessageTextForProbe == "Do you want to bulldoze this lot?",
                    "magic-second-confirm-message-131-15-VENUE-PAIR");
            }
            Press(BulldozeDialogButton(UIAlertButtonType.No));   // keep the house
            Check(Screen.ArmedEvictsForProbe == ev0 + 1, "magic-confirm2-no-ran-moveout");
            Check(N.GetFamilyForHouse(3) == null, "magic-evict-unbound-family");
            var receipt = BulldozeDialogButton(UIAlertButtonType.OK);
            if (receipt != null) Press(receipt);   // the evict receipt
            DisarmAll();
            Panel.SetViewModeForProbe(4);
            if (!_passed) { _done = true; return; }
            Next();
        }

        private void PhaseZoneChoice()
        {
            // NBR-08: the zone-choice dialog is WIRED ahead of the rezone
            // cascade (was decode-banked) — RezoneModeLotHandlerUL
            // 0x4691f0-0x46927c, cSimsApp vt+0x118, STR# 250 [0]/[1] with the
            // %s = the current zone's STR# 251 name, the STR# 251 captions as
            // the two choice buttons. Same-zone pick INERT (0x4692a0-0x4692dc
            // re-query the zone AFTER the choice returns); dismiss returns;
            // other-zone pick runs the cascade at the picked target.
            Check(Panel.CurrentViewMode == 4, "zone-choice-leg-base-mode");
            Check(N.GetFamilyForHouse(11) == null, "lot11-vacant-zonechoice");
            var simi = SimiBuildingValues(HousePath(11));
            Check(simi != null && simi.Item1 == 0 && simi.Item2 == 0, "lot11-unbuilt-zonechoice");
            Check(N.GetZoningType(11) == 0, "lot11-residential-before-choice");
            var shown0 = Screen.RezoneChoiceShownForProbe;
            var same0 = Screen.RezoneChoiceSameZoneForProbe;
            var rezones0 = Screen.RezonesForProbe;
            var directs0 = Screen.RezoneDirectForProbe;

            // MOUNT: the armed click mounts the CHOICE, not the cascade.
            ArmRezone();
            Panel.SelectHouse(11);
            var choice = Screen._rezoneChoiceDialog;
            Check(Screen.RezoneChoiceShownForProbe == shown0 + 1, "zone-choice-mounted");
            Check(choice != null, "zone-choice-dialog-live");
            if (choice != null)
            {
                Check(choice.TitleTextForProbe == "Rezone House?", "zone-choice-title-str250-0");
                Check(choice.MessageTextForProbe
                    == GameFacade.Strings.GetString("250", "1").Replace("%s", "Residential"),
                    "zone-choice-message-str250-1-residential-formatted");
                var res = RezoneChoiceButton(0);
                var com = RezoneChoiceButton(1);
                Check(res != null && com != null, "zone-choice-has-both-buttons");
                Check(res != null && res.Caption == "Residential", "zone-choice-button0-str251-0");
                Check(com != null && com.Caption == "Community", "zone-choice-button1-str251-1");
            }
            else { Fail("zone-choice-dialog-missing"); _done = true; return; }

            // SAME-ZONE pick: INERT — no rezone, no receipt, zone unchanged.
            var sameBtn = RezoneChoiceButton(0);   // current zone is residential
            Check(sameBtn != null, "same-zone-button-present");
            if (sameBtn != null) Press(sameBtn);
            Check(Screen.RezoneChoiceSameZoneForProbe == same0 + 1, "same-zone-pick-counted");
            Check(Screen.RezonesForProbe == rezones0 && Screen.RezoneDirectForProbe == directs0,
                "same-zone-pick-made-no-rezone");
            Check(N.GetZoningType(11) == 0, "same-zone-zone-unchanged");
            Check(Screen._rezoneDialog == null, "same-zone-no-receipt-dialog");

            // DISMISS: a bare Close() (the native -1 arm) — nothing runs.
            Panel.SelectHouse(11);
            Check(Screen.RezoneChoiceShownForProbe == shown0 + 2, "zone-choice-remounted-for-dismiss");
            Screen._rezoneChoiceDialog?.Close();
            Screen._rezoneChoiceDialog = null;   // mirrors the handlers' seam cleanup
            Check(Screen.RezonesForProbe == rezones0 && Screen.RezoneDirectForProbe == directs0,
                "dismiss-made-no-rezone");
            Check(N.GetZoningType(11) == 0, "dismiss-zone-unchanged");
            Check(Screen.RezoneChoiceSameZoneForProbe == same0 + 1,
                "dismiss-not-counted-as-same-zone");

            // OTHER-ZONE pick: the direct rezone completes at the PICKED target.
            Panel.SelectHouse(11);
            Check(Screen.RezoneChoiceShownForProbe == shown0 + 3, "zone-choice-remounted-for-other-zone");
            var otherBtn = RezoneChoiceButton(1);
            Check(otherBtn != null, "other-zone-button-present");
            if (otherBtn != null) Press(otherBtn);
            Check(Screen.RezoneDirectForProbe == directs0 + 1, "other-zone-direct-rezone-ran");
            Check(Screen.RezonesForProbe == rezones0 + 1, "other-zone-rezone-counted");
            Check(N.GetZoningType(11) == 1, "other-zone-toggled-to-community");
            Check(Screen._rezoneDialog != null, "other-zone-receipt-shown");
            Check(Screen.HouseRezonedForProbe == 11, "other-zone-receipt-lot-recorded");
            UIButton okBtn = null;
            Screen._rezoneDialog?.ButtonMap.TryGetValue(UIAlertButtonType.OK, out okBtn);
            if (okBtn != null) Press(okBtn);
            // restore the template zone
            Check(N.SetZoningType(11, 0), "zone-choice-restore-residential");
            DisarmAll();
            if (!_passed) { _done = true; return; }
            Next();
        }

        private void PhaseBulldozeSounds()
        {
            // NBR-08 (P3-3/P3-4): the base/UL bulldoze sound law, pinned two
            // ways — (a) CORPUS: the four neighborhood bulldoze names resolve
            // in the mounted HOT event tables (HITVM + the HOT loader both
            // lowercase; wrong/missing names fail loudly); (b) DISPATCH: the
            // AUD-20 event-snapshot idiom — HITTrace windows around the REAL
            // legs: the unbuilt OK-dialog bracket cancel→bdoze
            // (0x469878-0x4698b4), demolish on the confirm-YES arms
            // (0x4698dc), evict on the occupied keep-house executor arm
            // (0x469a08-0x469a38, flag-0 side).
            Check(Panel.CurrentViewMode == 4, "sounds-leg-base-mode");
            var audio = FSO.Content.Content.Get().Audio;
            Check(audio != null && audio.Events != null, "hit-event-tables-mounted");
            if (audio != null && audio.Events != null)
            {
                Check(audio.Events.ContainsKey(FSO.Client.UI.Model.UISounds.BulldozeCancel),
                    "corpus-ui_nhood_bdoze_cancel-resolves");
                Check(audio.Events.ContainsKey(FSO.Client.UI.Model.UISounds.Bulldoze),
                    "corpus-ui_nhood_bdoze-resolves");
                Check(audio.Events.ContainsKey(FSO.Client.UI.Model.UISounds.BulldozeDemolish),
                    "corpus-ui_nhood_bdoze_demolish-resolves");
                Check(audio.Events.ContainsKey(FSO.Client.UI.Model.UISounds.BulldozeEvict),
                    "corpus-ui_nhood_bdoze_evict-resolves");
                Check(!audio.Events.ContainsKey("ui_nhood_bdoze_nosuch"),
                    "corpus-negative-control-absent");
            }

            var hit = FSO.HIT.HITVM.Get();
            if (hit == null)
            {
                // The AUD-20 degrade idiom: corpus laws stand, dispatch pins skip.
                _notes.Add("sounds: dispatch pins skipped (no HITVM)");
                if (!_passed) { _done = true; return; }
                Next();
                return;
            }

            // (b1) OCCUPIED keep-house arm → evict: lot 3 is vacant+built after
            // the venue legs; bind FAMI 4, confirm1 YES, confirm2 NO.
            var fam = N.GetFamily(4);
            Check(fam != null, "sounds-family-4-found");
            if (fam == null) { _done = true; return; }
            Check(N.GetFamilyForHouse(3) == null, "sounds-lot3-vacant");
            var simi3 = SimiBuildingValues(HousePath(3));
            Check(simi3 != null && (simi3.Item1 > 0 || simi3.Item2 > 0), "sounds-lot3-built");
            if (simi3 == null || N.GetFamilyForHouse(3) != null)
            { Fail("sounds-template-lot3-not-vacant-built"); _done = true; return; }
            N.SetFamilyForHouse(3, fam, false);
            FSO.HIT.HITTrace.Reset();
            FSO.HIT.HITTrace.Enabled = true;
            ArmBulldoze();
            Panel.SelectHouse(3);
            Press(BulldozeDialogButton(UIAlertButtonType.Yes));   // confirm1 [2]/[3]
            Press(BulldozeDialogButton(UIAlertButtonType.No));    // confirm2 [0]/[1] keep house
            FSO.HIT.HITTrace.Enabled = false;
            var s1 = BdozeSequence();
            Check(s1.Count == 1 && s1[0] == "ui_nhood_bdoze_evict",
                "occupied-keep-house-plays-evict (" + BdozeJoin(s1) + ")");
            Check(BdozeAllResolved(), "occupied-arm-sounds-resolved");
            Check(N.GetFamilyForHouse(3) == null, "sounds-evict-unbound-family");
            var r1 = BulldozeDialogButton(UIAlertButtonType.OK);
            if (r1 != null) Press(r1);   // the evict receipt
            DisarmAll();

            // (b2) VACANT+BUILT confirm YES → demolish (lot 3, house still
            // standing after the keep-house arm).
            FSO.HIT.HITTrace.Reset();
            FSO.HIT.HITTrace.Enabled = true;
            ArmBulldoze();
            Panel.SelectHouse(3);
            Press(BulldozeDialogButton(UIAlertButtonType.Yes));
            FSO.HIT.HITTrace.Enabled = false;
            var s2 = BdozeSequence();
            Check(s2.Count == 1 && s2[0] == "ui_nhood_bdoze_demolish",
                "vacant-built-yes-plays-demolish (" + BdozeJoin(s2) + ")");
            Check(BdozeAllResolved(), "vacant-built-sound-resolved");
            var after3 = SimiBuildingValues(HousePath(3));
            Check(after3 != null && after3.Item1 == 0 && after3.Item2 == 0,
                "sounds-vacant-built-bulldozed");
            var r2 = BulldozeDialogButton(UIAlertButtonType.OK);
            if (r2 != null) Press(r2);   // the bulldoze receipt
            DisarmAll();

            // (b3) VACANT+UNBUILT → the cancel/bdoze bracket around the
            // 132 [4]/[5] OK dialog (0x469878-0x4698b4; lot 11).
            var status0 = Screen.BulldozeStatusOnlyForProbe;
            FSO.HIT.HITTrace.Reset();
            FSO.HIT.HITTrace.Enabled = true;
            ArmBulldoze();
            Panel.SelectHouse(11);
            FSO.HIT.HITTrace.Enabled = false;
            var s3 = BdozeSequence();
            Check(Screen.BulldozeStatusOnlyForProbe == status0 + 1, "sounds-unbuilt-leg-counted");
            Check(Screen._bulldozeDialog != null, "sounds-unbuilt-nothing-dialog-mounted");
            Check(s3.Count == 2 && s3[0] == "ui_nhood_bdoze_cancel" && s3[1] == "ui_nhood_bdoze",
                "unbuilt-leg-cancel-bdoze-bracket (" + BdozeJoin(s3) + ")");
            Check(BdozeAllResolved(), "bracket-sounds-resolved");
            var r3 = BulldozeDialogButton(UIAlertButtonType.OK);
            if (r3 != null) Press(r3);   // the nothing-dialog OK
            DisarmAll();
            if (!_passed) { _done = true; return; }
            Next();
        }

        private void PhaseRestore()
        {
            DisarmAll();
            Check(!Switcher.BulldozeArmed && !Switcher.RezoneArmed, "ends-disarmed");
            Check(Panel.CurrentViewMode == 4, "view-mode-restored-to-base");
            Check(N.GetZoningType(11) == 0, "zone-choice-leg-zone-restored");
            _notes.Add("summary: venues=DT/ST-occupied-noop+VA[18]/[19]+MG-venue-pair; " +
                "zone-choice=STR250/251-WIRED(NBR-08); ucp-pairing=+0x54→BUY+LIVE/+0x55→BUILD+CAMERA; " +
                "sounds=corpus+evict/demolish/cancel-bdoze-bracket-pinned");
            _done = true;
        }
    }
}
