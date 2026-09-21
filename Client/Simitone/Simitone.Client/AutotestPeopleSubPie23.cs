/*
This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
If a copy of the MPL was not distributed with this file, You can obtain one at
http://mozilla.org/MPL/2.0/.
*/
using System;
using System.Linq;
using FSO.Client;
using FSO.SimAntics;
using Microsoft.Xna.Framework;

namespace Simitone.Client
{
    /// <summary>
    /// UI-23 'uipiesub' (additive, focused): the PEOPLE-PIE SUB-PIE on the
    /// decoded r257 two-ring law (evidence UI-23/r257-people-subpie-law.md
    /// §2/§4/§7). Body lives in this separate file; AutotestRunner.cs
    /// carries only the dispatch hook (six sibling worktrees also touch
    /// that file). Pins:
    ///
    ///   (1) geometry-identical second instance — ring 2 is the same
    ///       201px-disc window (WindowSize = 2*(150+60)), mounted at the
    ///       SAME pop point/size as ring 1 (Init 0x28f130 block configures
    ///       the +216 window byte-identical to +212's 0x28f03c block);
    ///       the back item rides the slot-0 anchor of the SAME SlotBox
    ///       table (measured label + 6, Layout text path).
    ///   (2) item-0 back affordance — HasBack on ring 2 only; slot 0 is
    ///       label-only (InsertString vt+560 with a NULL sub-button → no
    ///       portrait button); Back click = the vt+628(−1) step-out (ring 2
    ///       closes, ring 1 remains).
    ///   (3) ESC stepping sub→main — the sub ring has key priority
    ///       (TSOnCommand 0x28cd8c-28 vt+508() liveness, sub first): ESC
    ///       with a live sub steps OUT (sub closes, main stays); ESC with
    ///       no sub live is the full dismissal.
    ///   (4) active-ring rebuild — the census splits 8 | overflow: ring 1
    ///       holds exactly the first 8, ring 2 the remainder; a ring-2
    ///       member click routes vm.MyUID and dismisses BOTH rings main
    ///       first (CancelPieMenu 0x2125e0 — the "MS" receipt).
    ///   (5) no-engagement baseline — with a ≤8 census ring 2 stays dark
    ///       (the sanctioned port condition; the native view-global
    ///       predicate is BSS-walled and pet routing awaits REF-01 — law
    ///       doc §6; disclosed, not invented).
    ///
    /// Live TS1 families cap at 8 members, so the >8 overflow path is
    /// exercised through the internal censusOverride gate seam (repeated
    /// live avatars — routing arithmetic, not real >8 families).
    /// </summary>
    internal static class AutotestPeopleSubPie23
    {
        public static bool Check(out string details)
        {
            bool law = Simitone.Client.UI.Panels.UIOriginalPeoplePie.WindowSize
                == 2 * (Simitone.Client.UI.Panels.UIOriginalPeoplePie.MaxRadius
                      + Simitone.Client.UI.Panels.UIOriginalPeoplePie.CardinalPad)
                && Simitone.Client.UI.Panels.UIOriginalPeoplePie.DiscSize == 201
                && Simitone.Client.UI.Panels.UIOriginalPeoplePie.SlotCount == 8;

            var game = GameFacade.Screens.CurrentUIScreen as Simitone.Client.UI.Screens.TS1GameScreen;
            bool live = false, geom = false, backBox = false, mainRing = false, backStep = false;
            bool rebuild = false, subsel = false, esc = false;
            if (game != null && game.vm != null)
            {
                var center = new Vector2(400, 300);
                var popsB = Simitone.Client.UI.Panels.UIOriginalPeoplePie.SubPops;
                var closesB = Simitone.Client.UI.Panels.UIOriginalPeoplePie.SubCloses;
                var stepsB = Simitone.Client.UI.Panels.UIOriginalPeoplePie.SubSteps;
                var selsB = Simitone.Client.UI.Panels.UIOriginalPeoplePie.SubSelections;
                var closesMainB = Simitone.Client.UI.Panels.UIOriginalPeoplePie.Closes;
                var selMainB = Simitone.Client.UI.Panels.UIOriginalPeoplePie.Selections;

                // (1)+(2) ring 2 at the identical window constants, back item 0
                var sub = new Simitone.Client.UI.Panels.UIOriginalPeoplePie(game, center, true);
                var blocal = Simitone.Client.UI.Panels.UIOriginalPeoplePie.SlotBox(0, sub.BackBox.Width, sub.BackBox.Height);
                geom = sub.Size == new Vector2(Simitone.Client.UI.Panels.UIOriginalPeoplePie.WindowSize, Simitone.Client.UI.Panels.UIOriginalPeoplePie.WindowSize)
                    && sub.HasBack;
                backBox = sub.BackBox.X == blocal.X + (int)sub.DiscCenter.X
                    && sub.BackBox.Y == blocal.Y + (int)sub.DiscCenter.Y
                    && sub.BackBox.Width == blocal.Width && sub.BackBox.Height == blocal.Height;

                // (5) ring 1: no back affordance; engagement iff census > 8
                // (the sanctioned condition — verified as a condition, not
                // an assumed ≤8 environment)
                var main = new Simitone.Client.UI.Panels.UIOriginalPeoplePie(game, center);
                int censusCount = 0;
                try
                {
                    var fam = game.ActiveFamily;
                    var guids = fam != null ? fam.FamilyGUIDs : null;
                    censusCount = game.vm.Entities.OfType<VMAvatar>()
                        .Where(a => a.PersistID != 0 && (guids == null || guids.Contains((uint)a.Object.GUID)))
                        .Count();
                }
                catch { }
                mainRing = !main.HasBack && main.Sub == null
                    && (main.PendingSub == null) == (censusCount <= 8);

                // (2) Back click = step-out: ring 2 closes, ring 1 probes untouched
                sub.SelectSlot(0);
                backStep = Simitone.Client.UI.Panels.UIOriginalPeoplePie.SubCloses == closesB + 1
                    && Simitone.Client.UI.Panels.UIOriginalPeoplePie.SubSteps == stepsB + 1
                    && Simitone.Client.UI.Panels.UIOriginalPeoplePie.SubPops == popsB + 1
                    && Simitone.Client.UI.Panels.UIOriginalPeoplePie.Closes == closesMainB
                    && Simitone.Client.UI.Panels.UIOriginalPeoplePie.Selections == selMainB;

                // (3)+(4) forced overflow split (REF-01 wall: the live census
                // caps at 8 — the override repeats live avatars to reach 9)
                var pool = game.vm.Entities.OfType<VMAvatar>().Where(a => a.PersistID != 0).Take(9).ToList();
                if (pool.Count > 0)
                {
                    var full = Enumerable.Range(0, 9).Select(i => pool[i % pool.Count]).ToArray();
                    var m9 = new Simitone.Client.UI.Panels.UIOriginalPeoplePie(game, new Vector2(320, 260), false, full);
                    rebuild = m9.Family.Length == 8
                        && Enumerable.Range(0, 8).All(i => m9.Family[i] == full[i])
                        && m9.PendingSub != null;
                    if (rebuild)
                    {
                        game.Add(m9); // mount so the sub ring can attach (Update-time route; gate drives MountSub directly)
                        m9.MountSub();
                        var s9 = m9.Sub;
                        rebuild = s9 != null && s9.Owner == m9 && s9.Parent == m9.Parent
                            && s9.Position == m9.Position && s9.Size == m9.Size
                            && s9.HasBack && s9.Family[0] == null && s9.Family[1] == full[8]
                            && s9.Family.Skip(2).All(a => a == null)
                            && s9.SlotButtons[0] == null && s9.SlotButtons[1] != null;

                        // ring-2 member click: vm.MyUID + BOTH rings dismissed main-first
                        Simitone.Client.UI.Panels.UIOriginalPeoplePie.LastDismissal = "";
                        var cmB = Simitone.Client.UI.Panels.UIOriginalPeoplePie.Closes;
                        var cB2 = Simitone.Client.UI.Panels.UIOriginalPeoplePie.SubCloses;
                        s9.SelectSlot(1);
                        subsel = Simitone.Client.UI.Panels.UIOriginalPeoplePie.SubSelections == selsB + 1
                            && game.vm.MyUID == full[8].PersistID
                            && Simitone.Client.UI.Panels.UIOriginalPeoplePie.LastDismissal == "MS"
                            && m9.Parent == null && s9.Parent == null
                            && Simitone.Client.UI.Panels.UIOriginalPeoplePie.Closes == cmB + 1
                            && Simitone.Client.UI.Panels.UIOriginalPeoplePie.SubCloses == cB2 + 1;

                        // (3) ESC steps sub→main, then the second ESC dismisses
                        var m10 = new Simitone.Client.UI.Panels.UIOriginalPeoplePie(game, new Vector2(240, 260), false, full);
                        game.Add(m10);
                        m10.MountSub();
                        var stB = Simitone.Client.UI.Panels.UIOriginalPeoplePie.SubSteps;
                        var scB = Simitone.Client.UI.Panels.UIOriginalPeoplePie.SubCloses;
                        var cmB2 = Simitone.Client.UI.Panels.UIOriginalPeoplePie.Closes;
                        bool stepped = m10.EscapeStep();
                        esc = stepped && m10.Sub == null && m10.Parent != null
                            && Simitone.Client.UI.Panels.UIOriginalPeoplePie.SubSteps == stB + 1
                            && Simitone.Client.UI.Panels.UIOriginalPeoplePie.SubCloses == scB + 1;
                        bool fullClose = !m10.EscapeStep();
                        esc = esc && fullClose && m10.Parent == null
                            && Simitone.Client.UI.Panels.UIOriginalPeoplePie.Closes == cmB2 + 1;
                    }
                }
                live = true;
            }

            bool ok = law && live && geom && backBox && mainRing && backStep && rebuild && subsel && esc;
            details = "law=" + law + " live=" + live + " geom=" + geom + " backbox=" + backBox
                + " mainring=" + mainRing + " backstep=" + backStep
                + " rebuild=" + rebuild + " subsel=" + subsel + " esc=" + esc;
            return ok;
        }
    }
}
