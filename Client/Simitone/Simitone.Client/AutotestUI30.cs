/*
This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
If a copy of the MPL was not distributed with this file, You can obtain one at
http://mozilla.org/MPL/2.0/.
*/
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
using Microsoft.Xna.Framework.Input;
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
    /// UI-30 'nghbtns' (opt-in, focused) — the runtime acceptance fixture for the
    /// neighborhood Credits screen and the armed Bulldoze/Evict mode, on the
    /// UI-29 decoded laws (coordination/evidence/UI-29/credits-law.md +
    /// bulldoze-law.md). Runs on the real game bootstrap at the neighborhood
    /// screen (R247 takeover idiom); BeginIsolation redirects FSOEnvironment.
    /// UserDir into a fresh dir INSIDE the launcher's private in-worktree
    /// userdir (never /tmp), so the neighborhood rematerializes from the
    /// pristine install template and every mutation lands in run-local space.
    ///
    /// Credits law pinned: composition = Mac 'STR#' 10000 block FIRST + sets
    /// 171/170/169/167/166/165 + the Deluxe branch (168 on the shipped Complete
    /// data — 164/163 stay off the roll) by set availability; one shared
    /// timeline (+1000 ms first line, +770 ms per line ACROSS sets); the
    /// per-line Credit machine (waiting → armed 400 ms → crawling → done);
    /// exit on ESC and Enter ONLY; the one-window latch; entry through the REAL
    /// Credits button AND the banner click; reopen after close.
    ///
    /// Bulldoze law pinned (through the real buttons, dialogs and the
    /// integrated NBR-02/NBR-03 backends, on throwaway template state):
    /// Bulldoze click arms, any other toolbar click disarms first, re-click
    /// disarms (disclosed); armed lot click — vacant+unbuilt → status only
    /// (no dialog); occupied → confirm 1 (STR# 131 chrome, $family
    /// substituted), No aborts, Yes evicts with members KEPT (killSims=false
    /// on an unbuilt lot); occupied+built (state synthesized via the
    /// production SetFamilyForHouse binding) → confirm 2 appears and its
    /// answer IS killSims (No: members kept, house structure NOT demolished;
    /// Yes: members/char files deleted, family emptied, house still built);
    /// vacant+built → confirm → BulldozeLot zeroes the SIMI building values
    /// in the house FILE (byte check) + tile refresh; a second armed click on
    /// the now-unbuilt lot takes the status-only branch (branch flip).
    /// </summary>
    public class AutotestUI30
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
        private UICreditsScreen CreditsProbe;

        // isolation (R246/R247 idiom, workspace-persistent: INSIDE the private userdir)
        private static string _priorUserDir;
        internal static string RedirectDir;
        internal static string IsolationError;

        internal static bool BeginIsolation(string checksCsv)
        {
            var c = checksCsv?.Trim().Trim('"');
            if (string.IsNullOrEmpty(c)) return false;
            var names = c.Split(',').Select(s => s.Trim().ToLowerInvariant()).ToList();
            if (!names.Contains("nghbtns")) return false;
            try
            {
                _priorUserDir = FSOEnvironment.UserDir;
                RedirectDir = Path.Combine(_priorUserDir ?? ".",
                    "ui30-fresh-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
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

        public AutotestUI30(Action<string> log)
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
                    case 1: PhaseCreditsLaw(); break;
                    case 2: PhaseCreditsEntryExit(); break;
                    case 3: PhaseArmDisarmLaw(); break;
                    case 4: PhaseVacantUnbuilt(); break;
                    case 5: PhaseOccupiedUnbuilt(); break;
                    case 6: PhaseOccupiedBuiltNoKill(); break;
                    case 7: PhaseOccupiedBuiltKill(); break;
                    case 8: PhaseVacantBuiltBulldoze(); break;
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
            _log("AUTOTEST nghbtns: FAIL " + what);
        }

        private void Note(string what)
        {
            _notes.Add(what);
            _log("AUTOTEST nghbtns: " + what);
        }

        // --- helpers -------------------------------------------------------

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

        private void Arm()
        {
            if (!Switcher.BulldozeArmed) Press(ToolbarButton("NghUI\\Bulldoze.bmp"));
        }

        private void Disarm()
        {
            if (Switcher.BulldozeArmed) Press(ToolbarButton("NghUI\\InetBtn.bmp"));
        }

        private UIButton DialogButton(UIAlertButtonType type)
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

        private int CharFileCount()
        {
            return Directory.GetFiles(Path.Combine(N.UserPath, "Characters"), "User*.iff").Length;
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
            if (!_passed) { _done = true; return; }
            Next();
        }

        private void PhaseCreditsLaw()
        {
            CreditsProbe = new UICreditsScreen();

            // Composition law: Mac block first + expansion order + the Deluxe
            // branch (Complete data → 168; 164/163 off the roll).
            var expected = new[] { 10000, 171, 170, 169, 167, 166, 165, 168 };
            Check(CreditsProbe.SetsLoaded.Count == expected.Length,
                "sets-loaded-count (" + string.Join(",", CreditsProbe.SetsLoaded) + ")");
            Check(expected.Select((v, i) => v == CreditsProbe.SetsLoaded[i]).All(x => x),
                "sets-loaded-order-mac-expansion-deluxe (" + string.Join(",", CreditsProbe.SetsLoaded) + ")");
            Check(!CreditsProbe.SetsLoaded.Contains(163) && !CreditsProbe.SetsLoaded.Contains(164),
                "complete-data-deluxe-branch-hides-163-164");

            // Timeline law: first line +1000 ms, then +770 ms PER LINE across ALL
            // sets (AddCredits advances one shared tick).
            Check(CreditsProbe.LineCount > 47, "timeline-has-mac-plus-expansion-lines ("
                + CreditsProbe.LineCount + ")");
            Check(CreditsProbe.Line(0).Text == "THE SIMS COMPLETE FOR MACINTOSH",
                "mac-credits-open-the-roll");
            Check(CreditsProbe.Line(0).StartMs == UICreditsScreen.FirstLineDelayMs,
                "first-line-starts-plus-1000ms (" + CreditsProbe.Line(0).StartMs + ")");
            var cadence = true;
            for (int i = 0; i < CreditsProbe.LineCount; i++)
            {
                if (CreditsProbe.Line(i).StartMs != UICreditsScreen.FirstLineDelayMs
                    + UICreditsScreen.LineDelayMs * i) { cadence = false; break; }
            }
            Check(cadence, "shared-timeline-cadence-770ms-per-line");

            // Credit::Tick machine (deterministic clock).
            CreditsProbe.Advance(999);
            Check(CreditsProbe.Line(0).State == UICreditsScreen.StateWaiting,
                "line0-waiting-before-its-tick");
            CreditsProbe.Advance(1);
            Check(CreditsProbe.Line(0).State == UICreditsScreen.StateArmed,
                "line0-armed-at-its-tick");
            CreditsProbe.Advance(UICreditsScreen.ArmedPreRollMs - 1);
            Check(CreditsProbe.Line(0).State == UICreditsScreen.StateArmed,
                "line0-armed-through-preroll");
            CreditsProbe.Advance(1);
            Check(CreditsProbe.Line(0).State == UICreditsScreen.StateCrawling,
                "line0-crawling-after-400ms-preroll");
            Check(CreditsProbe.Line(10).State != UICreditsScreen.StateCrawling,
                "line10-still-scheduled-7700ms-out");
            CreditsProbe.Advance(30000);
            Check(CreditsProbe.Line(0).State == UICreditsScreen.StateDone,
                "line0-done-after-passing-the-clip");

            if (!_passed) { _done = true; return; }
            Next();
        }

        private void PhaseCreditsEntryExit()
        {
            // The REAL Credits button mounts the single overlay.
            Check(Switcher.CreditsButtonForProbe != null, "credits-button-mounted");
            Press(Switcher.CreditsButtonForProbe);
            Check(Screen.CreditsOpensForProbe == 1, "credits-button-dispatched-showscreen");
            Check(Screen._creditsScreen != null, "credits-overlay-mounted");
            // The native latch: a second entry while mounted is a no-op.
            Screen.ShowCreditsScreen();
            Check(Screen.CreditsOpensForProbe == 2, "second-entry-counted");
            Check(Screen._creditsScreen != null, "latch-keeps-one-window");

            // Exit law: ESC and Enter ONLY.
            Check(!Screen._creditsScreen.HandleKey(Keys.A), "other-key-swallowed");
            Check(Screen._creditsScreen != null, "non-exit-key-leaves-credits-up");
            Screen._creditsScreen.HandleKey(Keys.Escape);
            Check(Screen._creditsScreen == null, "esc-closes-credits");
            // Reopen works after the close (native latch law).
            Screen.ShowCreditsScreen();
            Check(Screen._creditsScreen != null, "credits-reopen-after-esc");
            Screen._creditsScreen.HandleKey(Keys.Enter);
            Check(Screen._creditsScreen == null, "enter-closes-credits");

            // The banner is the same credits/picker path (vt+0x98(0x400,0)).
            var banner = Switcher.BannerForProbe;
            Check(banner != null, "banner-mounted");
            if (banner != null)
            {
                Press(banner);
                Check(Screen._creditsScreen != null, "banner-click-opens-credits");
                Screen._creditsScreen.HandleKey(Keys.Escape);
                Check(Screen._creditsScreen == null, "banner-credits-esc-closes");
            }
            if (!_passed) { _done = true; return; }
            Next();
        }

        private void PhaseArmDisarmLaw()
        {
            var bulldoze = ToolbarButton("NghUI\\Bulldoze.bmp");
            var inet = ToolbarButton("NghUI\\InetBtn.bmp");
            Check(bulldoze != null && inet != null, "toolbar-buttons-found");
            if (bulldoze == null || inet == null) { _done = true; return; }

            Check(!Switcher.BulldozeArmed, "starts-disarmed");
            Press(bulldoze);
            Check(Switcher.BulldozeArmed, "bulldoze-click-arms");
            Check(bulldoze.Selected, "armed-button-selected-state");
            Press(inet);
            Check(!Switcher.BulldozeArmed, "other-toolbar-click-disarms-first");
            // Re-click toggle (native semantics not separately decoded — disclosed).
            Press(bulldoze);
            Press(bulldoze);
            Check(!Switcher.BulldozeArmed, "second-bulldoze-click-disarms");
            if (!_passed) { _done = true; return; }
            Next();
        }

        private void PhaseVacantUnbuilt()
        {
            // Template law: lot 11 is vacant+unbuilt (no family, SIMI values 0).
            Check(N.GetFamilyForHouse(11) == null, "lot11-vacant-on-template");
            var simi = SimiBuildingValues(HousePath(11));
            Check(simi != null && simi.Item1 == 0 && simi.Item2 == 0, "lot11-unbuilt-on-template");

            Arm();
            Check(Switcher.BulldozeArmed, "armed-for-lot11");
            Panel.SelectHouse(11);
            Check(Screen.BulldozeStatusOnlyForProbe == 1, "vacant-unbuilt-status-only");
            Check(Screen._bulldozeDialog == null, "vacant-unbuilt-no-dialog");
            Check(Screen.ArmedEvictsForProbe == 0 && Screen.ArmedBulldozesForProbe == 0,
                "vacant-unbuilt-no-backend-call");
            Disarm();
            if (!_passed) { _done = true; return; }
            Next();
        }

        private void PhaseOccupiedUnbuilt()
        {
            // Template law: lot 7 is occupied+unbuilt (FAMI 1, no house file →
            // built=false → NO second confirm; killSims stays false).
            var fam = N.GetFamilyForHouse(7);
            Check(fam != null, "lot7-occupied-on-template");
            Check(!File.Exists(HousePath(7)), "lot7-unbuilt-on-template");
            if (fam == null) { _done = true; return; }
            var members0 = fam.FamilyGUIDs.Length;
            var chars0 = CharFileCount();

            Arm();
            Panel.SelectHouse(7);
            Check(Screen.BulldozeConfirm1ForProbe == 1, "occupied-confirm1-shown");
            var dlg = Screen._bulldozeDialog;
            Check(dlg != null, "confirm1-mounted");
            Check(dlg.MessageTextForProbe?.Contains(
                N.MainResource.Get<FAMs>(fam.ChunkID)?.GetString(0) ?? "?") == true,
                "confirm1-names-the-family");
            Check(dlg.MessageTextForProbe?.Contains("$family") == false,
                "confirm1-substituted");
            var no = DialogButton(UIAlertButtonType.No);
            Check(no != null, "confirm1-has-no");
            Press(no);
            Check(Screen._bulldozeDialog == null, "confirm1-no-aborts");
            Check(Screen.ArmedEvictsForProbe == 0, "abort-made-no-backend-call");
            Check(N.GetFamilyForHouse(7) == fam, "abort-left-family-bound");

            Arm();
            Panel.SelectHouse(7);
            var yes = DialogButton(UIAlertButtonType.Yes);
            Check(yes != null, "confirm1-has-yes");
            Press(yes);
            Check(Screen.ArmedEvictsForProbe == 1, "yes-ran-moveout");
            Check(Screen.BulldozeConfirm2ForProbe == 0, "unbuilt-lot-skipped-confirm2");
            Check(N.GetFamilyForHouse(7) == null, "evict-unbound-the-family");
            Check(fam.HouseNumber == 0, "evict-cleared-fami-house");
            Check(fam.FamilyGUIDs.Length == members0, "evict-kept-members (" + fam.FamilyGUIDs.Length
                + " vs " + members0 + ")");
            Check(CharFileCount() == chars0, "evict-kept-character-files");
            var ok = DialogButton(UIAlertButtonType.OK);
            Check(ok != null && Screen._bulldozeDialog != null, "success-ok-dialog-shown");
            Check(Screen.LotTileRefreshesForProbe == 1, "success-refreshed-lot-tile");
            if (ok != null) Press(ok);
            Check(Screen._bulldozeDialog == null, "ok-closed");
            Disarm();
            if (!_passed) { _done = true; return; }
            Next();
        }

        private void PhaseOccupiedBuiltNoKill()
        {
            // Synthesize occupied+built with the PRODUCTION move-in binding
            // (MoveInAndPlay's SetFamilyForHouse): bin FAMI 2 onto built lot 21.
            var fam = N.GetFamily(2);
            Check(fam != null, "bin-family-2-found");
            var simi = SimiBuildingValues(HousePath(21));
            Check(simi != null && (simi.Item1 > 0 || simi.Item2 > 0), "lot21-built-on-template");
            if (fam == null) { _done = true; return; }
            N.SetFamilyForHouse(21, fam, false);
            var members0 = fam.FamilyGUIDs.Length;
            var chars0 = CharFileCount();

            Arm();
            Panel.SelectHouse(21);
            Press(DialogButton(UIAlertButtonType.Yes));   // confirm 1
            Check(Screen.BulldozeConfirm2ForProbe == 1, "built-lot-showed-confirm2");
            Check(Screen._bulldozeDialog != null, "confirm2-mounted");
            Press(DialogButton(UIAlertButtonType.No));    // answer 2 = killSims FALSE
            Check(Screen.ArmedEvictsForProbe == 1, "confirm2-no-ran-moveout-killSims-false");
            Check(N.GetFamilyForHouse(21) == null, "killSims-false-unbound-the-family");
            Check(fam.FamilyGUIDs.Length == members0, "killSims-false-kept-members");
            Check(CharFileCount() == chars0, "killSims-false-kept-character-files");
            Check(Screen._bulldozeDialog != null, "killSims-false-ok-dialog");
            Press(DialogButton(UIAlertButtonType.OK));

            // The EVICT law: the house structure is NOT demolished — the SIMI
            // building values survive a killSims=false eviction byte-for-byte.
            var after = SimiBuildingValues(HousePath(21));
            Check(after != null && after.Item1 == simi.Item1 && after.Item2 == simi.Item2,
                "evict-left-house-structure-standing");
            Disarm();
            if (!_passed) { _done = true; return; }
            Next();
        }

        private void PhaseOccupiedBuiltKill()
        {
            // FAMI 1 (the lot-7 family, real members) onto built lot 22; the
            // second confirm's YES is the killSims=TRUE answer.
            var fam = N.GetFamilyForHouse(7) ?? N.GetFamily(1);
            Check(fam != null, "family-1-found");
            if (fam == null) { _done = true; return; }
            N.SetFamilyForHouse(22, fam, false);
            var chars0 = CharFileCount();

            Arm();
            Panel.SelectHouse(22);
            Press(DialogButton(UIAlertButtonType.Yes));   // confirm 1
            Check(Screen.BulldozeConfirm2ForProbe == 2, "second-built-lot-confirm2");
            Press(DialogButton(UIAlertButtonType.Yes));   // answer 2 = killSims TRUE
            Check(Screen.ArmedEvictsForProbe == 2, "confirm2-yes-ran-moveout-killSims-true");
            Check(N.GetFamilyForHouse(22) == null, "killSims-true-unbound-the-family");
            Check(fam.FamilyGUIDs.Length == 0, "killSims-true-emptied-membership");
            Check(CharFileCount() < chars0, "killSims-true-deleted-character-files ("
                + CharFileCount() + " vs " + chars0 + ")");
            Check(Screen._bulldozeDialog != null, "killSims-true-ok-dialog");
            Press(DialogButton(UIAlertButtonType.OK));

            var after = SimiBuildingValues(HousePath(22));
            Check(after != null && (after.Item1 > 0 || after.Item2 > 0),
                "killSims-evict-still-left-the-house-standing");
            Disarm();
            if (!_passed) { _done = true; return; }
            Next();
        }

        private void PhaseVacantBuiltBulldoze()
        {
            // Template law: lot 10 is vacant+built → confirm → BulldozeLot.
            Check(N.GetFamilyForHouse(10) == null, "lot10-vacant-on-template");
            var simi = SimiBuildingValues(HousePath(10));
            Check(simi != null && (simi.Item1 > 0 || simi.Item2 > 0), "lot10-built-on-template");

            Arm();
            Panel.SelectHouse(10);
            Check(Screen.BulldozeVacantBuiltConfirmsForProbe == 1, "vacant-built-confirm-shown");
            Press(DialogButton(UIAlertButtonType.No));
            Check(Screen._bulldozeDialog == null, "bulldoze-confirm-no-aborts");
            Check(Screen.ArmedBulldozesForProbe == 0, "abort-made-no-backend-call");

            Arm();
            Panel.SelectHouse(10);
            var yes = DialogButton(UIAlertButtonType.Yes);
            Check(yes != null, "bulldoze-confirm-has-yes");
            Press(yes);
            Check(Screen.ArmedBulldozesForProbe == 1, "yes-ran-bulldozelot");
            var after = SimiBuildingValues(HousePath(10));
            Check(after != null && after.Item1 == 0 && after.Item2 == 0,
                "bulldoze-zeroed-simi-building-values-in-the-file");
            Check(Screen.LotTileRefreshesForProbe == 2, "bulldoze-refreshed-lot-tile");
            Check(Screen._bulldozeDialog != null, "bulldoze-ok-dialog-shown");
            Press(DialogButton(UIAlertButtonType.OK));

            // Branch flip: the now-unbuilt lot takes the status-only arm.
            Arm();
            Panel.SelectHouse(10);
            Check(Screen.BulldozeStatusOnlyForProbe == 2, "post-bulldoze-lot-status-only");
            Check(Screen._bulldozeDialog == null, "post-bulldoze-no-dialog");
            Disarm();
            Check(!Switcher.BulldozeArmed, "disarmed-after-matrix");
            _done = true;
        }
    }
}
