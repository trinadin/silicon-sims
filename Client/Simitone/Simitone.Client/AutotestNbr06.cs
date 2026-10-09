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
    /// NBR-06 opt-in pin ("nbr06"): the recovered lot-management laws —
    ///   (a) the EvictModeLotHandlerUL bulldoze/evict dialog law (STR# 131
    ///       [0]/[1] on BOTH confirms, STR# 132 [4]/[5] as the vacant+unbuilt
    ///       OK dialog, STR# 131 [10]/[11] on executor failure) —
    ///       cWinNeighborhoodUL::EvictModeLotHandler @0x469760;
    ///   (b) the FULL rezone chain — the eviction/bulldoze confirms chain
    ///       straight into SetZoningType in the SAME click (the NBR-05
    ///       single-step-chain disclosure closed) — RezoneModeLotHandlerUL
    ///       @0x469160;
    ///   (c) the occupied-bulldoze DEMOLITION — EvictFamily(lot, 1)
    ///       @0x2323a0 forwards the bool to Neighborhood::MoveOut @0xb1800,
    ///       whose r30 arm purges the house records (0xb1c54) and DELETES the
    ///       house files (0xb1cdc); the port expresses it as NBR-03's
    ///       BulldozeLot SIMI-zeroing after the eviction;
    ///   (d) the community-lot build/buy ENTRY matrix — cSimsApp::LoadGame
    ///       @0x258680 tail (visit flag, DisableLiveMode, the DisableSave
    ///       polarity: community lots SAVE while visiting, familyless
    ///       residential never saves) + CPState::SetMode @0x210790's BUY
    ///       catalog selection — pinned through the pure statics
    ///       TS1GameScreen.NativeEntryVisit / NativeEntrySaveDisabled /
    ///       NativeBuyCatalog (the live UCP gating rides engine global 32,
    ///       equivalent on every reachable state — see the receipt).
    ///
    /// Driven through the REAL toolbar arm + production lot-click routing
    /// (Panel.SelectHouse -> ArmedLotClick), mirroring AutotestUI30 /
    /// AutotestNBR05UI (fresh isolated userdir INSIDE the launcher's private
    /// userdir; template lots: 11 = vacant+unbuilt, 10/3/6 = vacant+built,
    /// bin FAMI 4 for occupancy synthesis). The live save-button consumption
    /// (UIOriginalOptionsPanel) is pinned via the same pure matrix — the
    /// panel delegate is a four-line pass-through.
    ///
    /// COORDINATOR WIRING (not registered here): mirror the nbr05ui plumbing —
    /// call AutotestNbr06.BeginIsolation(checksCsv) in the runner's init (next
    /// to AutotestNBR05UI.BeginIsolation), add a CheckEnabled("nbr06") branch
    /// at the neighborhood screen that constructs this class, and drive
    /// Tick() each frame; Passed/Failures/Diagnostics are the verdict.
    /// NOTE: nbr06 SUPERSEDES two stale UI-30/NBR-05 pins —
    /// AutotestUI30's "killSims-evict-still-left-the-house-standing" and
    /// AutotestNBR05UI's phase-4/5 single-step-chain expectations now
    /// describe the OLD behavior (see the NBR-06 receipt).
    /// </summary>
    public class AutotestNbr06
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

        // isolation (UI-30/nbr05ui idiom, workspace-persistent: INSIDE the
        // launcher's private userdir)
        private static string _priorUserDir;
        internal static string RedirectDir;
        internal static string IsolationError;

        internal static bool BeginIsolation(string checksCsv)
        {
            var c = checksCsv?.Trim().Trim('"');
            if (string.IsNullOrEmpty(c)) return false;
            var names = c.Split(',').Select(s => s.Trim().ToLowerInvariant()).ToList();
            if (!names.Contains("nbr06")) return false;
            try
            {
                _priorUserDir = FSOEnvironment.UserDir;
                RedirectDir = Path.Combine(_priorUserDir ?? ".",
                    "nbr06-fresh-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
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

        public AutotestNbr06(Action<string> log)
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
                    case 2: PhasePureMatrices(); break;
                    case 3: PhaseBulldozeNothing(); break;
                    case 4: PhaseBulldozeVacantBuilt(); break;
                    case 5: PhaseBulldozeOccupiedKeepHouse(); break;
                    case 6: PhaseRezoneChainOccupied(); break;
                    case 7: PhaseRezoneChainVacantBuilt(); break;
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
            _log("AUTOTEST nbr06: FAIL " + what);
        }

        // --- helpers (UI-30 / nbr05ui idioms) -------------------------------

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

        private UIButton BulldozeDialogButton(UIAlertButtonType type)
        {
            UIButton btn = null;
            Screen._bulldozeDialog?.ButtonMap.TryGetValue(type, out btn);
            return btn;
        }

        private UIButton RezoneDialogButton(UIAlertButtonType type)
        {
            UIButton btn = null;
            Screen._rezoneDialog?.ButtonMap.TryGetValue(type, out btn);
            return btn;
        }

        // NBR-08: answer the mounted zone-choice dialog (STR# 250/251) with
        // the given target — Yes = Residential (0), No = Community (1).
        private void AnswerRezoneChoice(int target)
        {
            UIButton btn = null;
            Screen._rezoneChoiceDialog?.ButtonMap.TryGetValue(
                target == 0 ? UIAlertButtonType.Yes : UIAlertButtonType.No, out btn);
            if (btn != null) Press(btn);
        }

        private string HousePath(int house)
        {
            // The port's house-file convention is zero-padded (House05.iff —
            // see the runner's LOT-TRY paths); single-digit lots must pad too.
            return Path.Combine(N.UserPath, "Houses", "House" + house.ToString("00") + ".iff");
        }

        private static Tuple<int, int> SimiBuildingValues(string housePath)
        {
            var iff = new IffFile(housePath);
            var simi = iff.Get<SIMI>(1);
            return simi == null ? null : Tuple.Create((int)simi.ObjectsValue, (int)simi.ArchitectureValue);
        }

        private int PersistedZone(short lot)
        {
            var iff = new IffFile(Path.Combine(N.UserPath, "LotZoning.iff"));
            var str = iff.Get<STR>(1);
            if (str == null) return -1;
            for (int i = 0; i < str.Length; i++)
            {
                var parts = str.GetString(i).Split(',');
                if (short.TryParse(parts[0].Trim(), out var id) && id == lot)
                    return parts.Length > 1 && parts[1].Trim() == "community" ? 1 : 0;
            }
            return 0;   // absent = residential (the dictionary's construction law)
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
            Check(!Switcher.RezoneArmed && !Switcher.BulldozeArmed, "starts-disarmed");
            // Seed any missing template house files: the isolated fresh
            // userdir materializes neighborhood records but not every house
            // file (House06.iff was absent while the game-data template
            // carries it — the later phases read template lots directly).
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
            // The NBR-06 recovered pairs (UIText.iff, byte-verbatim).
            Check(GameFacade.Strings.GetString("131", "0") == "Bulldoze House?", "str131-0-bulldoze-house");
            Check(GameFacade.Strings.GetString("131", "1") == "Do you want to bulldoze this house?",
                "str131-1-bulldoze-house-question");
            Check(GameFacade.Strings.GetString("131", "10") == "Error", "str131-10-error");
            Check(GameFacade.Strings.GetString("131", "11") == "Could not evict!", "str131-11-could-not-evict");
            Check(GameFacade.Strings.GetString("132", "4") == "Nothing to Bulldoze", "str132-4-nothing-title");
            Check(GameFacade.Strings.GetString("132", "5") == "There is no house here to bulldoze.",
                "str132-5-nothing-message");
            // The production getters resolve to the native strings (not the
            // disclosed fallback literals).
            Check(TS1GameScreen.BulldozeHouseTitle == "Bulldoze House?", "getter-bulldoze-title-native");
            Check(TS1GameScreen.BulldozeHouseMessage == "Do you want to bulldoze this house?",
                "getter-bulldoze-message-native");
            Check(TS1GameScreen.NothingToBulldozeTitle == "Nothing to Bulldoze", "getter-nothing-title-native");
            Check(TS1GameScreen.NothingToBulldozeMessage == "There is no house here to bulldoze.",
                "getter-nothing-message-native");
            Check(TS1GameScreen.EvictFailTitle == "Error", "getter-evict-fail-title-native");
            Check(TS1GameScreen.EvictFailMessage == "Could not evict!", "getter-evict-fail-message-native");
            if (!_passed) { _done = true; return; }
            Next();
        }

        private void PhasePureMatrices()
        {
            // NativeEntryVisit (LoadGame @0x258680): community-zoned OR the
            // public Magic lots 93..99.
            Check(TS1GameScreen.NativeEntryVisit(true, 5), "visit-community-zone");
            Check(!TS1GameScreen.NativeEntryVisit(false, 5), "no-visit-residential");
            Check(TS1GameScreen.NativeEntryVisit(false, 93), "visit-magic-93");
            Check(TS1GameScreen.NativeEntryVisit(false, 99), "visit-magic-99");
            Check(!TS1GameScreen.NativeEntryVisit(false, 100), "no-visit-lot-100");
            Check(!TS1GameScreen.NativeEntryVisit(false, 92), "no-visit-private-magic-92");
            Check(TS1GameScreen.NativeEntryVisit(true, 92), "visit-community-beats-lot-id");

            // NativeEntrySaveDisabled: community/magic → disabled only when NOT
            // visiting (community edits SAVE while visiting — the TS1
            // persistence law); residential → disabled when no family.
            Check(!TS1GameScreen.NativeEntrySaveDisabled(true, 5, true, false),
                "community-visit-SAVES");
            Check(TS1GameScreen.NativeEntrySaveDisabled(true, 5, false, false),
                "community-nonvisit-no-save");
            Check(!TS1GameScreen.NativeEntrySaveDisabled(false, 93, true, false),
                "magic-visit-SAVES");
            Check(TS1GameScreen.NativeEntrySaveDisabled(false, 5, false, false),
                "familyless-residential-no-save");
            Check(!TS1GameScreen.NativeEntrySaveDisabled(false, 5, false, true),
                "residential-family-saves");
            Check(!TS1GameScreen.NativeEntrySaveDisabled(false, 5, true, false),
                "residential-visit-saves (native matrix)");

            // NativeBuyCatalog (SetMode @0x210790 mode-1 case): downtown 21-30
            // -> 2, vacation 40-49 -> 3, community non-magic -> 4, studio
            // 81-89 -> 5, magic 93-98 -> 6, else 1. Order matters: community
            // beats studio; magic beats community; lot 99 is NOT catalog-magic.
            Check(TS1GameScreen.NativeBuyCatalog(false, 5) == 1, "catalog-normal-home");
            Check(TS1GameScreen.NativeBuyCatalog(false, 25) == 2, "catalog-downtown");
            Check(TS1GameScreen.NativeBuyCatalog(false, 45) == 3, "catalog-vacation");
            Check(TS1GameScreen.NativeBuyCatalog(true, 5) == 4, "catalog-community");
            Check(TS1GameScreen.NativeBuyCatalog(false, 85) == 5, "catalog-studiotown");
            Check(TS1GameScreen.NativeBuyCatalog(false, 95) == 6, "catalog-magic-95");
            Check(TS1GameScreen.NativeBuyCatalog(false, 98) == 6, "catalog-magic-98");
            Check(TS1GameScreen.NativeBuyCatalog(true, 85) == 4, "community-beats-studio");
            Check(TS1GameScreen.NativeBuyCatalog(true, 95) == 6, "magic-beats-community");
            Check(TS1GameScreen.NativeBuyCatalog(true, 99) == 4, "lot99-not-catalog-magic");
            Check(TS1GameScreen.NativeBuyCatalog(false, 90) == 1, "private-magic-90-normal-catalog");
            if (!_passed) { _done = true; return; }
            Next();
        }

        private void PhaseBulldozeNothing()
        {
            // Template law: lot 11 vacant+unbuilt. The armed click now mounts
            // the native STR# 132 [4]/[5] OK dialog (was a log-only status).
            Check(N.GetFamilyForHouse(11) == null, "lot11-vacant");
            var simi = SimiBuildingValues(HousePath(11));
            Check(simi != null && simi.Item1 == 0 && simi.Item2 == 0, "lot11-unbuilt");
            var status0 = Screen.BulldozeStatusOnlyForProbe;
            var ev0 = Screen.ArmedEvictsForProbe;
            var bd0 = Screen.ArmedBulldozesForProbe;

            ArmBulldoze();
            Panel.SelectHouse(11);
            Check(Screen.BulldozeStatusOnlyForProbe == status0 + 1, "nothing-leg-counted");
            Check(Screen._bulldozeDialog != null, "nothing-dialog-mounted");
            var ok = BulldozeDialogButton(UIAlertButtonType.OK);
            Check(ok != null, "nothing-dialog-has-ok");
            Check(Screen._bulldozeDialog != null && Screen._bulldozeDialog.TitleTextForProbe == "Nothing to Bulldoze",
                "nothing-dialog-title-132-4");
            Check(Screen._bulldozeDialog != null
                && Screen._bulldozeDialog.MessageTextForProbe == "There is no house here to bulldoze.",
                "nothing-dialog-message-132-5");
            if (ok != null) Press(ok);
            Check(Screen._bulldozeDialog == null, "nothing-dialog-ok-closed");
            Check(Screen.ArmedEvictsForProbe == ev0 && Screen.ArmedBulldozesForProbe == bd0,
                "nothing-leg-made-no-backend-call");
            DisarmAll();
            if (!_passed) { _done = true; return; }
            Next();
        }

        private void PhaseBulldozeVacantBuilt()
        {
            // Template law: lot 6 vacant+built. The confirm is the native
            // STR# 131 [0]/[1] pair (title/message verbatim); NO is inert;
            // YES bulldozes.
            Check(N.GetFamilyForHouse(6) == null, "lot6-vacant");
            var simi = SimiBuildingValues(HousePath(6));
            Check(simi != null && (simi.Item1 > 0 || simi.Item2 > 0), "lot6-built");
            if (simi == null || simi.Item1 == 0 && simi.Item2 == 0
                || N.GetFamilyForHouse(6) != null)
            {
                // Template drifted — fail loudly rather than guess a lot.
                Fail("template-lot6-not-vacant-built");
                _done = true;
                return;
            }
            var bd0 = Screen.ArmedBulldozesForProbe;

            ArmBulldoze();
            Panel.SelectHouse(6);
            var confirm = Screen._bulldozeDialog;
            Check(confirm != null, "vacant-built-confirm-mounted");
            if (confirm != null)
            {
                Check(confirm.TitleTextForProbe == "Bulldoze House?", "confirm-title-131-0");
                Check(confirm.MessageTextForProbe == "Do you want to bulldoze this house?",
                    "confirm-message-131-1");
            }
            var no = BulldozeDialogButton(UIAlertButtonType.No);
            Check(no != null, "confirm-has-no");
            if (no != null) Press(no);
            Check(Screen.ArmedBulldozesForProbe == bd0, "confirm-no-made-no-backend-call");

            ArmBulldoze();
            Panel.SelectHouse(6);
            var yes = BulldozeDialogButton(UIAlertButtonType.Yes);
            Check(yes != null, "confirm-has-yes");
            if (yes != null) Press(yes);
            Check(Screen.ArmedBulldozesForProbe == bd0 + 1, "confirm-yes-ran-bulldozelot");
            var after = SimiBuildingValues(HousePath(6));
            Check(after != null && after.Item1 == 0 && after.Item2 == 0,
                "vacant-built-bulldoze-zeroed-simi");
            DisarmAll();
            if (!_passed) { _done = true; return; }
            Next();
        }

        private void PhaseBulldozeOccupiedKeepHouse()
        {
            // Occupied+built, confirm-2 NO: evict WITHOUT demolition — the
            // house must survive byte-value (the killSims=false law).
            var fam = N.GetFamily(4);
            Check(fam != null, "family-4-found");
            if (fam == null) { _done = true; return; }
            Check(N.GetFamilyForHouse(3) == null, "lot3-vacant");
            var simi = SimiBuildingValues(HousePath(3));
            Check(simi != null && (simi.Item1 > 0 || simi.Item2 > 0), "lot3-built");
            if (simi == null || N.GetFamilyForHouse(3) != null)
            { Fail("template-lot3-not-vacant-built"); _done = true; return; }
            N.SetFamilyForHouse(3, fam, false);
            var members0 = fam.FamilyGUIDs.Length;
            var ev0 = Screen.ArmedEvictsForProbe;

            ArmBulldoze();
            Panel.SelectHouse(3);
            var confirm1 = Screen._bulldozeDialog;
            Check(confirm1 != null, "occupied-confirm1-mounted");
            if (confirm1 != null)
            {
                Check(confirm1.TitleTextForProbe == "Evict Family?", "confirm1-title-131-2");
                Check((confirm1.MessageTextForProbe ?? "").Contains("family")
                    && (confirm1.MessageTextForProbe ?? "").Contains("net worth"),
                    "confirm1-message-131-3-format");
            }
            Press(BulldozeDialogButton(UIAlertButtonType.Yes));
            var confirm2 = Screen._bulldozeDialog;
            Check(confirm2 != null, "occupied-confirm2-mounted (built)");
            if (confirm2 != null)
            {
                Check(confirm2.TitleTextForProbe == "Bulldoze House?", "confirm2-title-131-0-NBR06");
                Check(confirm2.MessageTextForProbe == "Do you want to bulldoze this house?",
                    "confirm2-message-131-1-NBR06");
            }
            Press(BulldozeDialogButton(UIAlertButtonType.No));   // NO = keep the house
            Check(Screen.ArmedEvictsForProbe == ev0 + 1, "confirm2-no-ran-moveout");
            Check(N.GetFamilyForHouse(3) == null, "evict-unbound-family");
            Check(fam.FamilyGUIDs.Length == members0, "killSims-false-kept-members");
            var after = SimiBuildingValues(HousePath(3));
            Check(after != null && after.Item1 == simi.Item1 && after.Item2 == simi.Item2,
                "killSims-false-house-standing (NBR-06 law)");
            Press(BulldozeDialogButton(UIAlertButtonType.OK));   // receipt
            DisarmAll();
            if (!_passed) { _done = true; return; }
            Next();
        }

        private void PhaseRezoneChainOccupied()
        {
            // The NBR-06 chain: occupied+built rezone = [6]/[7] YES -> chained
            // [8]/[9] YES -> MoveOut(killSims=true) + DEMOLITION +
            // SetZoningType(target) — all in the SAME click sequence, no
            // second armed click.
            var fam = N.GetFamily(4);
            Check(fam != null, "family-4-found-again");
            if (fam == null) { _done = true; return; }
            N.SetFamilyForHouse(3, fam, false);
            var zone0 = N.GetZoningType(3);
            Check(zone0 == 0, "lot3-residential-before-chain");
            var chained0 = Screen.RezoneChainedForProbe;
            var confirm2count0 = Screen.RezoneChainedConfirmsForProbe;

            ArmRezone();
            Panel.SelectHouse(3);
            AnswerRezoneChoice(1);   // NBR-08: answer the zone choice (Community)
            var c1 = Screen._rezoneDialog;
            Check(c1 != null && c1.TitleTextForProbe == "Are You Sure?", "chain-confirm-6-7-title");
            Press(RezoneDialogButton(UIAlertButtonType.Yes));
            var c2 = Screen._rezoneDialog;
            Check(Screen.RezoneChainedConfirmsForProbe == confirm2count0 + 1,
                "chained-second-confirm-counted");
            Check(c2 != null && c2.TitleTextForProbe == "Are you sure?", "chain-confirm-8-9-title");
            Press(RezoneDialogButton(UIAlertButtonType.Yes));

            Check(Screen.RezoneChainedForProbe == chained0 + 1, "chain-auto-rezone-completed");
            Check(N.GetFamilyForHouse(3) == null, "chain-evicted-family");
            var after = SimiBuildingValues(HousePath(3));
            Check(after != null && after.Item1 == 0 && after.Item2 == 0,
                "chain-bulldoze-flag-DEMOLISHED-the-house (EvictFamily(lot,1) law)");
            Check(N.GetZoningType(3) == 1, "chain-flipped-zoning-to-community");
            Check(PersistedZone(3) == 1, "chain-zoning-persisted");
            Check(Screen.HouseRezonedForProbe == 3, "chain-receipt-lot-recorded");
            var ok = RezoneDialogButton(UIAlertButtonType.OK);
            if (ok != null) Press(ok);   // the rezone receipt
            DisarmAll();
            // restore the template's residential zone
            Check(N.SetZoningType(3, 0), "chain-restore-residential");
            if (!_passed) { _done = true; return; }
            Next();
        }

        private void PhaseRezoneChainVacantBuilt()
        {
            // vacant+built rezone: [8]/[9] YES -> bulldoze + SetZoningType in
            // the same click (lot 10).
            Check(N.GetFamilyForHouse(10) == null, "lot10-vacant");
            var simi = SimiBuildingValues(HousePath(10));
            Check(simi != null && (simi.Item1 > 0 || simi.Item2 > 0), "lot10-built");
            if (simi == null || N.GetFamilyForHouse(10) != null)
            { Fail("template-lot10-not-vacant-built"); _done = true; return; }
            var chained0 = Screen.RezoneChainedForProbe;

            ArmRezone();
            Panel.SelectHouse(10);
            AnswerRezoneChoice(1);   // NBR-08: answer the zone choice (Community)
            var c = Screen._rezoneDialog;
            Check(c != null && c.TitleTextForProbe == "Are you sure?", "vacant-built-chain-confirm");
            Press(RezoneDialogButton(UIAlertButtonType.Yes));
            Check(Screen.RezoneChainedForProbe == chained0 + 1, "vacant-built-chain-completed");
            var after = SimiBuildingValues(HousePath(10));
            Check(after != null && after.Item1 == 0 && after.Item2 == 0,
                "vacant-built-chain-bulldozed");
            Check(N.GetZoningType(10) == 1, "vacant-built-chain-rezoned");
            Check(PersistedZone(10) == 1, "vacant-built-chain-persisted");
            var ok = RezoneDialogButton(UIAlertButtonType.OK);
            if (ok != null) Press(ok);
            DisarmAll();
            Check(N.SetZoningType(10, 0), "vacant-built-restore-residential");
            if (!_passed) { _done = true; return; }
            Next();
        }

        private void PhaseRestore()
        {
            DisarmAll();
            Check(!Switcher.BulldozeArmed && !Switcher.RezoneArmed, "ends-disarmed");
            Check(N.GetZoningType(3) == 0 && N.GetZoningType(10) == 0, "zones-restored");
            _notes.Add("summary: dialogs=131[0]/[1]+132[4]/[5]+131[10]/[11]; " +
                "chain=evict/bulldoze->auto-rezone; demolition=killSims-arm; " +
                "matrix=visit/save/catalog pinned");
            _done = true;
        }
    }
}
