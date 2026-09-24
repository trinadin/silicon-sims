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
    /// NBR-05 opt-in pin ("nbr05ui"): the neighborhood-screen completion battery —
    /// the armed rezone tool (the STR# 151 [10] 'Evict or Rezone' twin of UI-30's
    /// bulldoze arm) and the Previous/Next neighborhood cycling (NBR-02 backend),
    /// driven through the REAL toolbar buttons and the production lot-click
    /// routing (Panel.SelectHouse → ArmedLotClick), mirroring AutotestUI30's
    /// structure and template laws (fresh isolated userdir INSIDE the launcher's
    /// private userdir; template lot 11 = vacant+unbuilt, lot 10 = vacant+built,
    /// bin FAMI 4 for occupancy synthesis).
    ///
    /// Phases:
    /// 1. String law — STR# 131 'EvictModeStrs' [6]/[7]/[8]/[9] (the rezone
    ///    cascade, r197 decode) + [10] 'Error'; STR# 151 'NghbBtnTips' [4]/[5]
    ///    (Switch To Previous/Next Neighborhood) + [10] ('Evict or Rezone').
    /// 2. Arm law — the Rezone toolbar click arms the rezone mode; arming one
    ///    tool disarms the other (mutual exclusion); any other toolbar click
    ///    disarms both; a second tool click toggles off (disclosed semantics).
    /// 3. Vacant+unbuilt rezone — direct SetZoningType toggle 0→1 with the
    ///    receipt dialog, LotZoning.iff persisted (fresh-parse), then restored.
    /// 4. Vacant+built rezone — confirm [8]/[9]; YES = BulldozeLot (NBR-03); the
    ///    disclosed single-step chain (no auto-rezone after the bulldoze).
    /// 5. Occupied rezone — confirm [6]/[7] names the family; NO is inert (no
    ///    backend call, family stays bound). The YES-evict leg shares the
    ///    UI-30-validated eviction lambda and NBR-02's nbrmgmt-pinned MoveOut.
    /// 6. Previous/Next — the real buttons call SwitchNeighborhood; on the
    ///    single-neighborhood isolated userdir the switch is a counted no-op
    ///    (enumeration law asserted).
    /// 7. Previous/Next wrap math (P2, indep-review-nbr05-20260924) — a
    ///    second neighborhood materialized in the isolated userdir (byte-copy
    ///    of the live UserData = id 1) opens the single-hood guard; the real
    ///    buttons cycle the ascending enumeration with wrap both ways, and
    ///    every success rebuilds the screen in place through the production
    ///    RefreshNeighborhoodScreen.
    /// 8. Switch failure leg (P2, indep-review-nbr05-20260924) — the bounded
    ///    failure law: an id with no materialized dir and no template backing
    ///    refuses and stays put (the ShowSwitchFail trigger predicate), and
    ///    the real ShowSwitchFail mounts the OK alert (STR# 131 [10] 'Error'
    ///    title + the disclosed port literal) whose OK closes it.
    ///
    /// Disclosed: the lot-query Rezone button is the touch-layout surface and
    /// shares RezoneLotClickFlow with the toolbar tool proven here (desktop lot
    /// selection is DispatchNativeDesktopSelection; the touch panel is not
    /// mounted on the desktop battery).
    /// </summary>
    public class AutotestNBR05UI
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
        private UIMobileAlert _failDialog;   // phase 8: the mounted switch-fail alert
        private int _failCloseTicks;         // phase 8: fade-out wait counter

        // isolation (UI-30 idiom, workspace-persistent: INSIDE the private userdir)
        private static string _priorUserDir;
        internal static string RedirectDir;
        internal static string IsolationError;

        internal static bool BeginIsolation(string checksCsv)
        {
            var c = checksCsv?.Trim().Trim('"');
            if (string.IsNullOrEmpty(c)) return false;
            var names = c.Split(',').Select(s => s.Trim().ToLowerInvariant()).ToList();
            if (!names.Contains("nbr05ui")) return false;
            try
            {
                _priorUserDir = FSOEnvironment.UserDir;
                RedirectDir = Path.Combine(_priorUserDir ?? ".",
                    "nbr05-fresh-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
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

        public AutotestNBR05UI(Action<string> log)
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
                    case 1: PhaseStringsLaw(); break;
                    case 2: PhaseArmLaw(); break;
                    case 3: PhaseVacantUnbuiltRezone(); break;
                    case 4: PhaseVacantBuiltRezone(); break;
                    case 5: PhaseOccupiedRezoneNo(); break;
                    case 6: PhasePrevNext(); break;
                    case 7: PhaseNeighborhoodWrap(); break;
                    case 8: PhaseSwitchFail(); break;
                    default:
                        _done = true;
                        break;
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
            _log("AUTOTEST nbr05ui: FAIL " + what);
        }

        // --- helpers (UI-30 idioms) ----------------------------------------

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

        private void ArmRezone()
        {
            if (!Switcher.RezoneArmed) Press(ToolbarButton("NghUI\\Rezone.bmp"));
        }

        private void DisarmAll()
        {
            if (Switcher.BulldozeArmed || Switcher.RezoneArmed)
                Press(ToolbarButton("NghUI\\InetBtn.bmp"));
        }

        private UIButton RezoneDialogButton(UIAlertButtonType type)
        {
            UIButton btn = null;
            Screen._rezoneDialog?.ButtonMap.TryGetValue(type, out btn);
            return btn;
        }

        // The rezone cascade's YES-bulldoze leg reuses ArmedBulldoze, whose
        // receipt rides the UI-30 bulldoze dialog seam.
        private UIButton BulldozeDialogButton(UIAlertButtonType type)
        {
            UIButton btn = null;
            Screen._bulldozeDialog?.ButtonMap.TryGetValue(type, out btn);
            return btn;
        }

        private static Tuple<int, int> SimiBuildingValues(string housePath)
        {
            var iff = new IffFile(housePath);
            var simi = iff.Get<SIMI>(1);
            return simi == null ? null : Tuple.Create((int)simi.ObjectsValue, (int)simi.ArchitectureValue);
        }

        private string HousePath(int house)
        {
            return Path.Combine(N.UserPath, "Houses", "House" + house + ".iff");
        }

        private string LotZoningPath()
        {
            return Path.Combine(N.UserPath, "LotZoning.iff");
        }

        /// <summary>Re-parse the persisted LotZoning.iff and read one lot's zone
        /// from the STR# 1 table (independent of the live dictionary).</summary>
        private int PersistedZone(short lot)
        {
            var iff = new IffFile(LotZoningPath());
            var str = iff.Get<FSO.Files.Formats.IFF.Chunks.STR>(1);
            if (str == null) return -1;
            for (int i = 0; i < str.Length; i++)
            {
                var parts = str.GetString(i).Split(',');
                if (short.TryParse(parts[0].Trim(), out var id) && id == lot)
                    return parts.Length > 1 && parts[1].Trim() == "community" ? 1 : 0;
            }
            return 0;   // absent = residential (the dictionary's construction law)
        }

        // --- phases --------------------------------------------------------

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
            Check(Switcher.RezoneButtonForProbe != null, "rezone-toolbar-button-mounted");
            Check(!Switcher.RezoneArmed && !Switcher.BulldozeArmed, "starts-disarmed");
            if (!_passed) { _done = true; return; }
            Next();
        }

        private void PhaseStringsLaw()
        {
            // STR# 131 'EvictModeStrs' — the rezone cascade (r197 decode) + Error.
            Check(GameFacade.Strings.GetString("131", "6") == "Are You Sure?", "str131-6-are-you-sure");
            Check((GameFacade.Strings.GetString("131", "7") ?? "").Contains("rezone")
                && (GameFacade.Strings.GetString("131", "7") ?? "").Contains("evicted"),
                "str131-7-evict-cascade-question");
            Check(GameFacade.Strings.GetString("131", "8") == "Are you sure?", "str131-8-are-you-sure");
            Check((GameFacade.Strings.GetString("131", "9") ?? "").Contains("rezone")
                && (GameFacade.Strings.GetString("131", "9") ?? "").Contains("bulldozed"),
                "str131-9-bulldoze-cascade-question");
            Check(GameFacade.Strings.GetString("131", "10") == "Error", "str131-10-error");
            // STR# 151 'NghbBtnTips' — the tool/switch tips the mounted buttons carry.
            Check(GameFacade.Strings.GetString("151", "10") == "Evict or Rezone", "str151-10-evict-or-rezone");
            Check(GameFacade.Strings.GetString("151", "4") == "Switch To Previous Neighborhood", "str151-4-previous-tip");
            Check(GameFacade.Strings.GetString("151", "5") == "Switch To Next Neighborhood", "str151-5-next-tip");
            if (!_passed) { _done = true; return; }
            Next();
        }

        private void PhaseArmLaw()
        {
            var rezone = ToolbarButton("NghUI\\Rezone.bmp");
            var bulldoze = ToolbarButton("NghUI\\Bulldoze.bmp");
            var inet = ToolbarButton("NghUI\\InetBtn.bmp");
            Check(rezone != null && bulldoze != null && inet != null, "toolbar-buttons-found");
            if (rezone == null || bulldoze == null || inet == null) { _done = true; return; }

            Press(rezone);
            Check(Switcher.RezoneArmed, "rezone-click-arms");
            Check(rezone.Selected, "armed-rezone-button-selected-state");
            Check(!Switcher.BulldozeArmed, "rezone-arm-left-bulldoze-disarmed");
            Press(rezone);
            Check(!Switcher.RezoneArmed, "second-rezone-click-disarms");

            // Mutual exclusion: arming the other tool disarms the first.
            Press(bulldoze);
            Check(Switcher.BulldozeArmed, "bulldoze-arms");
            Press(rezone);
            Check(Switcher.RezoneArmed && !Switcher.BulldozeArmed, "rezone-arm-disarms-bulldoze");
            Press(bulldoze);
            Check(Switcher.BulldozeArmed && !Switcher.RezoneArmed, "bulldoze-arm-disarms-rezone");

            // Any other toolbar click disarms both (the pre-subscribed hook).
            Press(inet);
            Check(!Switcher.BulldozeArmed && !Switcher.RezoneArmed, "other-toolbar-click-disarms-both");
            if (!_passed) { _done = true; return; }
            Next();
        }

        private void PhaseVacantUnbuiltRezone()
        {
            // Template law (UI-30): lot 11 is vacant+unbuilt (no family, SIMI 0).
            Check(N.GetFamilyForHouse(11) == null, "lot11-vacant-on-template");
            var simi = SimiBuildingValues(HousePath(11));
            Check(simi != null && simi.Item1 == 0 && simi.Item2 == 0, "lot11-unbuilt-on-template");
            var zone0 = N.GetZoningType(11);
            Check(zone0 == 0, "lot11-starts-residential (" + zone0 + ")");
            var other0 = PersistedZone(10);
            var probe0 = Screen.RezoneDirectForProbe;

            ArmRezone();
            Check(Switcher.RezoneArmed, "armed-for-lot11-rezone");
            Panel.SelectHouse(11);
            Check(Screen.RezoneDirectForProbe == probe0 + 1, "direct-rezone-attempted");
            Check(Screen.RezonesForProbe == probe0 + 1, "rezones-counted");
            Check(Screen._rezoneDialog != null, "rezone-receipt-dialog-shown");
            Check(N.GetZoningType(11) == 1, "lot11-zoning-toggled-to-community");
            Check(PersistedZone(11) == 1, "lot11-zone-persisted-in-lotzoning-iff");
            Check(PersistedZone(10) == other0, "other-lot-zone-unchanged");
            Check(Screen.RezoneEvictsForProbe == 0 && Screen.ArmedBulldozesForProbe == 0,
                "direct-rezone-made-no-evict-or-bulldoze-call");
            var receiptOk = RezoneDialogButton(UIAlertButtonType.OK);
            Check(receiptOk != null, "receipt-has-ok");
            if (receiptOk != null) Press(receiptOk);
            Check(Screen._rezoneDialog == null, "receipt-ok-closed");

            // Restore the template state (the toggle runs both directions).
            Check(N.SetZoningType(11, 0), "restore-rezone-to-residential");
            Check(N.GetZoningType(11) == 0 && PersistedZone(11) == 0, "lot11-restored-and-persisted");
            DisarmAll();
            if (!_passed) { _done = true; return; }
            Next();
        }

        private void PhaseVacantBuiltRezone()
        {
            // Template law (UI-30): lot 10 is vacant+built → confirm [8]/[9]
            // → YES = BulldozeLot. The disclosed single-step chain: the bulldoze
            // does NOT auto-run the rezone (the next armed click re-decides).
            Check(N.GetFamilyForHouse(10) == null, "lot10-vacant-on-template");
            var simi = SimiBuildingValues(HousePath(10));
            Check(simi != null && (simi.Item1 > 0 || simi.Item2 > 0), "lot10-built-on-template");
            var bulldozes0 = Screen.ArmedBulldozesForProbe;
            var directs0 = Screen.RezoneDirectForProbe;

            ArmRezone();
            Panel.SelectHouse(10);
            Check(Screen.RezoneConfirmBulldozeForProbe == 1, "vacant-built-rezone-confirm-shown");
            Check(Screen._rezoneDialog != null, "rezone-confirm-mounted");
            Check(Screen._rezoneDialog.MessageTextForProbe == GameFacade.Strings.GetString("131", "9"),
                "confirm-carries-str131-9-verbatim");
            Press(RezoneDialogButton(UIAlertButtonType.No));
            Check(Screen._rezoneDialog == null, "confirm-no-aborts");
            Check(Screen.ArmedBulldozesForProbe == bulldozes0, "abort-made-no-backend-call");

            ArmRezone();
            Panel.SelectHouse(10);
            var yes = RezoneDialogButton(UIAlertButtonType.Yes);
            Check(yes != null, "confirm-has-yes");
            Press(yes);
            Check(Screen.ArmedBulldozesForProbe == bulldozes0 + 1, "yes-ran-bulldozelot");
            var after = SimiBuildingValues(HousePath(10));
            Check(after != null && after.Item1 == 0 && after.Item2 == 0,
                "bulldoze-zeroed-simi-building-values-in-the-file");
            Check(Screen.RezoneDirectForProbe == directs0, "single-step-chain-no-auto-rezone");
            Check(Screen._bulldozeDialog != null, "bulldoze-receipt-shown");
            var bulldozeOk = BulldozeDialogButton(UIAlertButtonType.OK);
            if (bulldozeOk != null) Press(bulldozeOk);
            DisarmAll();
            if (!_passed) { _done = true; return; }
            Next();
        }

        private void PhaseOccupiedRezoneNo()
        {
            // Synthesize occupancy (UI-30 idiom): bin FAMI 4 onto lot 11, then
            // the [6]/[7] confirm; NO is inert. The YES-evict leg shares the
            // UI-30 eviction lambda + NBR-02's MoveOut law (nbrmgmt-pinned).
            var fam = N.GetFamily(4);
            Check(fam != null, "bin-family-4-found");
            if (fam == null) { _done = true; return; }
            N.SetFamilyForHouse(11, fam, false);
            Check(N.GetFamilyForHouse(11) == fam, "lot11-bound-for-rezone-cascade");
            var evicts0 = Screen.RezoneEvictsForProbe;

            ArmRezone();
            Panel.SelectHouse(11);
            Check(Screen.RezoneConfirmEvictForProbe == 1, "occupied-rezone-confirm-shown");
            var dlg = Screen._rezoneDialog;
            Check(dlg != null, "rezone-confirm-mounted");
            Check(dlg.MessageTextForProbe?.Contains(
                N.MainResource.Get<FAMs>(fam.ChunkID)?.GetString(0) ?? "?") == true,
                "confirm-names-the-family");
            Check(dlg.MessageTextForProbe?.Contains("rezone") == true
                && dlg.MessageTextForProbe?.Contains("evicted") == true,
                "confirm-carries-str131-7-law");
            var no = RezoneDialogButton(UIAlertButtonType.No);
            Check(no != null, "confirm-has-no");
            Press(no);
            Check(Screen._rezoneDialog == null, "confirm-no-aborts");
            Check(Screen.RezoneEvictsForProbe == evicts0, "abort-made-no-backend-call");
            Check(N.GetFamilyForHouse(11) == fam, "abort-left-family-bound");

            // Cleanup: release the synthesized binding through the production
            // backend (the lot is vacant+unbuilt again for any later leg).
            N.MoveOut(11, true);
            Check(N.GetFamilyForHouse(11) == null, "binding-released");
            DisarmAll();
            if (!_passed) { _done = true; return; }
            Next();
        }

        private void PhasePrevNext()
        {
            var prev = ToolbarButton("NghUI\\Previous.bmp");
            var next = ToolbarButton("NghUI\\Next.bmp");
            Check(prev != null && next != null, "prev-next-buttons-found");
            if (prev == null || next == null) { _done = true; return; }

            // The isolated fresh userdir rematerializes neighborhood 0 only —
            // the single-hood guard law: counted attempt, no switch, no dialog.
            var available = N.GetAvailableNeighborhoods();
            Check(available.Count == 1 && available[0] == 0,
                "isolated-userdir-single-neighborhood (" + available.Count + ")");
            Check(N.CurrentNeighborhoodID == 0, "starts-on-neighborhood-0");
            var attempts0 = Screen.SwitchAttemptsForProbe;

            Press(next);
            Check(Screen.SwitchAttemptsForProbe == attempts0 + 1, "next-click-counted");
            Check(N.CurrentNeighborhoodID == 0, "single-hood-next-stays-put");
            Press(prev);
            Check(Screen.SwitchAttemptsForProbe == attempts0 + 2, "previous-click-counted");
            Check(N.CurrentNeighborhoodID == 0, "single-hood-previous-stays-put");
            Check(Screen._rezoneDialog == null, "no-failure-dialog-on-the-guarded-no-op");
            // P2 (indep-review-nbr05-20260924): the single-hood guard law is
            // asserted — continue into the wrap-math + failure-leg phases
            // (additive; the proposal's "Prev/Next id math + failure leg").
            Next();
        }

        private void PhaseNeighborhoodWrap()
        {
            // P2 (indep-review-nbr05-20260924): machine-verify the Prev/Next
            // wrap math (TS1GameScreen.SwitchNeighborhood — cycle the ascending
            // enumeration with both-way modulo wrap) that the reviewed probe
            // left inspection-cleared. Materialize a second neighborhood dir in
            // the isolated userdir — a byte-copy of the live UserData (id 1),
            // disposable with the rest of the fixture — so the enumeration
            // carries two ids and the single-hood guard opens.
            var second = Path.Combine(RedirectDir, "UserData2");
            Check(!Directory.Exists(second), "second-neighborhood-dir-absent-before-materialization");
            CopyDirectory(N.UserPath, second);
            Check(File.Exists(Path.Combine(second, "Neighborhood.iff")),
                "second-neighborhood-materialized-from-the-live-userdata");
            var available = N.GetAvailableNeighborhoods();
            Check(available.Count == 2 && available[0] == 0 && available[1] == 1,
                "two-neighborhoods-enumerated-ascending (" + string.Join(",", available) + ")");
            Check(N.CurrentNeighborhoodID == 0, "wrap-legs-start-on-neighborhood-0");

            var prev = ToolbarButton("NghUI\\Previous.bmp");
            var next = ToolbarButton("NghUI\\Next.bmp");
            Check(prev != null && next != null, "wrap-leg-buttons-found");
            if (prev == null || next == null) { _done = true; return; }
            var attempts0 = Screen.SwitchAttemptsForProbe;
            var switches0 = Screen.SwitchesForProbe;
            var firstSwitcher = Switcher;

            // Wrap DOWN: Previous from the FIRST neighborhood lands on the
            // LAST (((0-1)%2+2)%2 = 1), through the real toolbar button.
            Press(prev);
            Check(Screen.SwitchAttemptsForProbe == attempts0 + 1, "wrap-previous-click-counted");
            Check(N.CurrentNeighborhoodID == available[available.Count - 1],
                "previous-from-first-wraps-to-last (id " + N.CurrentNeighborhoodID + ")");
            Check(ReferenceEquals(GameFacade.Screens.CurrentUIScreen, Screen),
                "successful-switch-rebuilds-in-place");
            Check(Screen.TS1NeighSwitcher != null
                && !ReferenceEquals(Screen.TS1NeighSwitcher, firstSwitcher),
                "successful-switch-remounts-the-switcher");
            Switcher = Screen.TS1NeighSwitcher as UINeighbourhoodSwitcher;
            if (Switcher == null) { _done = true; return; }

            // Wrap UP: Next from the LAST neighborhood lands on the FIRST —
            // on the REBUILT switcher's real button (the production remount).
            next = ToolbarButton("NghUI\\Next.bmp");
            if (next == null) { _done = true; return; }
            Press(next);
            Check(Screen.SwitchAttemptsForProbe == attempts0 + 2, "wrap-next-click-counted");
            Check(N.CurrentNeighborhoodID == available[0],
                "next-from-last-wraps-to-first (id " + N.CurrentNeighborhoodID + ")");
            Switcher = Screen.TS1NeighSwitcher as UINeighbourhoodSwitcher;
            next = ToolbarButton("NghUI\\Next.bmp");
            prev = ToolbarButton("NghUI\\Previous.bmp");
            if (next == null || prev == null) { _done = true; return; }

            // The in-range directions pin the non-wrap half of the cycle.
            Press(next);
            Check(Screen.SwitchAttemptsForProbe == attempts0 + 3, "step-next-click-counted");
            Check(N.CurrentNeighborhoodID == available[available.Count - 1],
                "next-from-first-advances-in-range (id " + N.CurrentNeighborhoodID + ")");
            Switcher = Screen.TS1NeighSwitcher as UINeighbourhoodSwitcher;
            prev = ToolbarButton("NghUI\\Previous.bmp");
            if (prev == null) { _done = true; return; }

            Press(prev);
            Check(Screen.SwitchAttemptsForProbe == attempts0 + 4, "step-previous-click-counted");
            Check(N.CurrentNeighborhoodID == available[0],
                "previous-from-last-retreats-in-range (id " + N.CurrentNeighborhoodID + ")");
            Check(Screen.SwitchesForProbe == switches0 + 4,
                "successful-switches-counted (" + (Screen.SwitchesForProbe - switches0) + ")");
            Check(N.CurrentNeighborhoodID == 0, "wrap-legs-end-on-neighborhood-0");
            if (!_passed) { _done = true; return; }
            Next();
        }

        private void PhaseSwitchFail()
        {
            // P2 (indep-review-nbr05-20260924): the failure leg. Through the
            // real SwitchNeighborhood the fail path (ShowSwitchFail) is
            // defensive-only — the wrap target always comes from the
            // materialized enumeration that SwitchToNeighborhood re-checks, so
            // a consistent tree cannot fail the mount. The leg verifies both
            // halves of the landed failure law at their real seams:
            // (1) the trigger predicate the code defines — an id with no
            //     materialized dir AND no template backing refuses and stays
            //     put (no silent switch, no save);
            // (2) the real ShowSwitchFail on the live screen mounts the
            //     bounded OK alert (STR# 131 [10] 'Error' title + the
            //     disclosed port literal) and OK closes it. Invoked by
            //     reflection (the Press idiom — the dialog rides no probe
            //     seam); the close fades out, so removal is awaited.
            if (_failDialog == null)
            {
                Check(N.GetAvailableNeighborhoods().Count == 2,
                    "two-neighborhoods-before-failure-leg");
                var before = N.CurrentNeighborhoodID;
                Check(!N.SwitchToNeighborhood(999), "unbacked-id-switch-refused (999)");
                Check(N.CurrentNeighborhoodID == before, "refused-switch-stays-put");

                var showFail = typeof(TS1GameScreen).GetMethod("ShowSwitchFail",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Check(showFail != null, "showswitchfail-resolved");
                if (showFail == null) { _done = true; return; }
                showFail.Invoke(Screen, null);
                var dlg = GameFacade.Screens.TopVisibleDialog as UIMobileAlert;
                Check(dlg != null, "switch-fail-dialog-mounted");
                if (dlg == null) { _done = true; return; }
                Check(dlg.TitleTextForProbe == GameFacade.Strings.GetString("131", "10"),
                    "switch-fail-title-is-str131-10-error");
                Check(dlg.MessageTextForProbe == "Could not switch neighborhood.",
                    "switch-fail-message-is-the-port-literal");
                UIButton ok = null;
                dlg.ButtonMap.TryGetValue(UIAlertButtonType.OK, out ok);
                Check(ok != null, "switch-fail-has-ok");
                if (ok == null) { _done = true; return; }
                _failDialog = dlg;
                Press(ok);
                return; // Close() fades out; removal completes on a later tick
            }

            // OK dispatched the real Close(): once the fade removes the alert
            // it is no longer the top visible dialog (bounded wait, then an
            // honest FAIL — the mounted alert must be dismissible).
            if (GameFacade.Screens.TopVisibleDialog == _failDialog
                && ++_failCloseTicks < 300) return;
            Check(GameFacade.Screens.TopVisibleDialog != _failDialog, "switch-fail-ok-closed");
            _done = true;
        }

        /// <summary>Recursive copy of the live neighborhood dir — materializes
        /// the second neighborhood the wrap legs cycle onto, inside the
        /// disposable isolated userdir (the UI-30 fixture idiom's cost: the
        /// fresh dir is private to this run and discarded with it).</summary>
        private static void CopyDirectory(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (var file in Directory.GetFiles(src))
                File.Copy(file, Path.Combine(dst, Path.GetFileName(file)), true);
            foreach (var dir in Directory.GetDirectories(src))
                CopyDirectory(dir, Path.Combine(dst, Path.GetFileName(dir)));
        }
    }
}
