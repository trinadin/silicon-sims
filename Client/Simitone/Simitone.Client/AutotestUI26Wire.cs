/*
This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
If a copy of the MPL was not distributed with this file, You can obtain one at
http://mozilla.org/MPL/2.0/.
*/
using FSO.Client;
using FSO.Common;
using Simitone.Client.UI.Panels;
using Simitone.Client.UI.Panels.LiveSubpanels;
using Simitone.Client.UI.Screens;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Simitone.Client
{
    /// <summary>
    /// UI-26 'uioptswire' (opt-in, focused) — engine wires for the options rows,
    /// per the UI-26 readiness map (coordination/evidence/UI-26/result.md WIRE
    /// rows; r260-options-readiness). Every wired setting must flip REAL engine
    /// state, read back here in-process:
    ///   (a) Lighting → WorldConfig.LightingMode 0/1 + World.ChangedWorldConfig
    ///       (boot apply proven by asserting state == config-derived law BEFORE
    ///       any mutation; ForceAdvLight lots clamp to 1 inside
    ///       ChangedWorldConfig — VMContext.cs — so expectations add the clamp);
    ///   (b) Character Detail → FSO.Vitaboy.Avatar.DefaultTechnique, ladder
    ///       Low/Med/High → Vitaboy.fx techniques 0 NoSSAA / 2 AdvancedLighting /
    ///       3 SSAA (technique table identity pinned by NAME);
    ///   (c) Sim In Background → SimitoneGame.RelayFocus (the production
    ///       LostFocus/RegainFocus relay) → VM.ApplyFocus: focus lost suspends
    ///       SpeedMultiplier at 0 and regain restores the exact prior speed
    ///       (native cSimulator +52 signed-speed law); with the option ON the
    ///       simulator keeps running;
    ///   (d) Shadows → WorldConfig.ObjShadows round-trip (LMapBatch.DrawObjShadows
    ///       refuses to generate while off);
    ///   (e) OPTIONS mode mounts UIOriginalOptionsPanel DIRECTLY in ApplyMode
    ///       (dummy OptionsCategories deleted): subpanel identity, hidden plaque,
    ///       and NO category Select (ActiveCategory/Categories untouched);
    ///   (+) ROW wiring: the live graphics/play rows drive the same engine state
    ///       through their production Toggle/Set closures, and the flipped
    ///       preference is proven on disk in the run's private config.ini
    ///       (persistence round-trip), then restored.
    ///
    /// Not in the default Checks string; enable alone with -autotest-opts
    /// uioptswire. Runs post-uidump on the live lot screen (uiopts dispatch
    /// point). Terrain Detail / Quick Tips / Export HTML are BUILD-tranche
    /// follow-ups and intentionally unpinned here; Interface Effects stays a
    /// disclosed partial (PIP fade only, per map decode §3).
    /// </summary>
    internal static class AutotestUI26Wire
    {
        internal static bool Check(TS1GameScreen screen, out string details)
        {
            var ok = new List<string>();
            var bad = new List<string>();
            Action<bool, string> need = (cond, tag) =>
            {
                if (cond) ok.Add(tag); else bad.Add(tag);
            };

            var set = FSO.Client.GlobalSettings.Default;
            var vm = screen?.vm;
            var world = vm?.Context?.World;
            if (vm == null || world == null) { details = "no-vm-or-world"; return false; }

            // exact prior preference + engine state (restored in the finally)
            bool s0Lighting = set.Lighting;
            bool s0Shadows = set.TS1Shadows;
            bool s0Bg = set.TS1SimInBackground;
            int s0Detail = set.TS1CharacterDetail;
            int s0Mode = FSO.LotView.WorldConfig.Current.LightingMode; // exact runtime mode
            // on-disk stored key at scenario start (may be unset on a cold userdir —
            // Program.cs's ultra pin is in-memory until some save persists it)
            string s0StoredLightingMode = ConfigValue("LightingMode");

            try
            {
                // ---- (0a) BOOT APPLY LAW (P1 review fix): LightingMode derives
                // from Lighting ONLY when stored == -1; explicit values (incl.
                // the Program.cs ultra pin 3) pass through. Expected runtime mode
                // = max(derive(stored), ForceAdvLight clamp).
                need(SimitoneGame.DeriveBootLightingMode(3, false) == 3
                    && SimitoneGame.DeriveBootLightingMode(3, true) == 3
                    && SimitoneGame.DeriveBootLightingMode(2, true) == 2,
                    "boot-law-explicit-preserved");
                need(SimitoneGame.DeriveBootLightingMode(-1, true) == 1
                    && SimitoneGame.DeriveBootLightingMode(-1, false) == 0,
                    "boot-law-auto-derive");

                // ---- (0) BOOT APPLY: engine state must equal the config-derived
                // law; asserted before any mutation in this process.
                int clamp = world.ForceAdvLight ? 1 : 0;
                int expLight0 = Math.Max(
                    SimitoneGame.DeriveBootLightingMode(set.LightingMode, s0Lighting), clamp);
                need(FSO.LotView.WorldConfig.Current.LightingMode == expLight0,
                    "boot-lighting(mode=" + FSO.LotView.WorldConfig.Current.LightingMode
                    + ",stored=" + set.LightingMode + ",exp=" + expLight0
                    + ",forceAdv=" + world.ForceAdvLight + ")");
                // the stored key itself is untouched by the derive (the Program.cs
                // ultra pin keeps surviving on disk; the gate never saves LightingMode)
                // disk law: the derive never rewrites the stored key (warm or cold
                // userdir — the pin-survival claim is relative to scenario start)
                need(ConfigValue("LightingMode") == s0StoredLightingMode,
                    "ini-lightingmode(stored=" + set.LightingMode + ",disk="
                    + (s0StoredLightingMode ?? "unset") + ")");
                need(FSO.LotView.WorldConfig.Current.ObjShadows == s0Shadows, "boot-objshadows");
                need(FSO.Vitaboy.Avatar.DefaultTechnique ==
                    UIOriginalOptionsPanel.CharacterDetailTechnique(s0Detail),
                    "boot-technique(t=" + FSO.Vitaboy.Avatar.DefaultTechnique + ")");

                // Vitaboy.fx technique table identity (detail ladder 0/1/2 -> 0/2/3)
                var fx = FSO.Vitaboy.Avatar.Effect;
                need(fx != null
                    && fx.Techniques[0].Name == "NoSSAA"
                    && fx.Techniques[2].Name == "AdvancedLighting"
                    && fx.Techniques[3].Name == "SSAA", "fx-technique-names");

                // ---- (a) Lighting toggle apply through the production helper
                // (the TOGGLE law stays 0/1 — the TS1 row has no ultra tier; boot
                // preserves explicit modes, the row maps on top of them).
                UIOriginalOptionsPanel.ApplyLighting(vm, !s0Lighting);
                need(FSO.LotView.WorldConfig.Current.LightingMode ==
                    Math.Max((!s0Lighting) ? 1 : 0, clamp), "lighting-toggle(mode="
                    + FSO.LotView.WorldConfig.Current.LightingMode + ")");
                UIOriginalOptionsPanel.ApplyLighting(vm, s0Lighting);
                need(FSO.LotView.WorldConfig.Current.LightingMode ==
                    Math.Max(s0Lighting ? 1 : 0, clamp), "lighting-restore");

                // ---- (d) Shadows gate round-trip
                UIOriginalOptionsPanel.ApplyShadows(vm, false);
                need(FSO.LotView.WorldConfig.Current.ObjShadows == false, "shadows-off");
                UIOriginalOptionsPanel.ApplyShadows(vm, true);
                need(FSO.LotView.WorldConfig.Current.ObjShadows == true, "shadows-on");
                UIOriginalOptionsPanel.ApplyShadows(vm, s0Shadows);
                need(FSO.LotView.WorldConfig.Current.ObjShadows == s0Shadows, "shadows-restore");

                // ---- (b) Character detail ladder (read every Avatar.Draw)
                bool ladder = true;
                for (int d = 0; d <= 2; d++)
                {
                    UIOriginalOptionsPanel.ApplyCharacterDetail(d);
                    ladder &= FSO.Vitaboy.Avatar.DefaultTechnique ==
                        UIOriginalOptionsPanel.CharacterDetailTechnique(d);
                }
                need(ladder, "technique-ladder");
                UIOriginalOptionsPanel.ApplyCharacterDetail(s0Detail);
                need(FSO.Vitaboy.Avatar.DefaultTechnique ==
                    UIOriginalOptionsPanel.CharacterDetailTechnique(s0Detail), "technique-restore");

                // ---- (c) Focus suspend through the PRODUCTION relay
                int speed0 = vm.SpeedMultiplier;
                set.TS1SimInBackground = false; // the native default law under test
                SimitoneGame.RelayFocus(false);
                need(vm.FocusSuspended && vm.SpeedMultiplier == 0, "focus-suspend(speed="
                    + vm.SpeedMultiplier + ")");
                SimitoneGame.RelayFocus(true);
                need(!vm.FocusSuspended && vm.SpeedMultiplier == speed0, "focus-restore(speed="
                    + vm.SpeedMultiplier + ")");
                // 'Sim In Background' on: the simulator keeps running unfocused.
                set.TS1SimInBackground = true;
                SimitoneGame.RelayFocus(false);
                need(!vm.FocusSuspended && vm.SpeedMultiplier == speed0, "siminbackground-keeps-running");
                SimitoneGame.RelayFocus(true);
                set.TS1SimInBackground = s0Bg;

                // ---- (e) OPTIONS-mode identity: direct SetSubpanel, no dummy Select
                var mp = screen.Frontend?.MainPanel;
                if (mp == null)
                {
                    bad.Add("no-mainpanel");
                }
                else
                {
                    var prevMode = mp.Mode;
                    var catsBefore = mp.Switcher.Categories;
                    int activeBefore = mp.Switcher.ActiveCategory;
                    mp.SetMode(UIMainPanelMode.OPTIONS);
                    var panel = mp.SubPanel as UIOriginalOptionsPanel;
                    need(panel != null, "options-subpanel-direct");
                    need(!mp.Switcher.MainButton.Visible, "options-plaque-hidden");
                    need(mp.Switcher.ActiveCategory == activeBefore, "options-no-dummy-select");
                    need(ReferenceEquals(mp.Switcher.Categories, catsBefore), "options-no-initcategories");

                    if (panel != null)
                    {
                        // ---- ROW wiring: the production row closures drive the engine
                        panel.ShowScreen("graphics");
                        var shRow = panel.Rows.FirstOrDefault(r => r.CaptionIndex == 17);
                        var liRow = panel.Rows.FirstOrDefault(r => r.CaptionIndex == 20);
                        var chRow = panel.Rows.FirstOrDefault(r => r.CaptionIndex == 31);
                        need(shRow != null && liRow != null && chRow != null, "graphics-rows-present");

                        if (shRow != null)
                        {
                            bool before = FSO.LotView.WorldConfig.Current.ObjShadows;
                            shRow.Toggle();
                            need(FSO.LotView.WorldConfig.Current.ObjShadows == !before, "row-shadows");
                            need(ConfigValue("TS1Shadows") == (!before).ToString(), "ini-shadows");
                            shRow.Toggle();
                            need(FSO.LotView.WorldConfig.Current.ObjShadows == before, "row-shadows-restore");
                        }
                        if (liRow != null)
                        {
                            bool before = set.Lighting;
                            liRow.Toggle();
                            need(set.Lighting == !before
                                && FSO.LotView.WorldConfig.Current.LightingMode ==
                                    Math.Max((!before) ? 1 : 0, clamp), "row-lighting");
                            need(ConfigValue("Lighting") == (!before).ToString(), "ini-lighting");
                            liRow.Toggle();
                            need(set.Lighting == before
                                && FSO.LotView.WorldConfig.Current.LightingMode ==
                                    Math.Max(before ? 1 : 0, clamp), "row-lighting-restore");
                        }
                        if (chRow != null && chRow.Widget is UIOriginalOptionTriRadio tri)
                        {
                            // the tri radio's production Set closure (the mouse path
                            // invokes the same delegate)
                            var del = typeof(UIOriginalOptionTriRadio)
                                .GetField("Set", BindingFlags.NonPublic | BindingFlags.Instance)
                                ?.GetValue(tri) as Action<int>;
                            need(del != null, "row-chardetail-closure");
                            if (del != null)
                            {
                                int before = set.TS1CharacterDetail;
                                int probe = (before == 2) ? 0 : 2;
                                del(probe);
                                need(set.TS1CharacterDetail == probe
                                    && FSO.Vitaboy.Avatar.DefaultTechnique ==
                                        UIOriginalOptionsPanel.CharacterDetailTechnique(probe),
                                    "row-chardetail(t=" + FSO.Vitaboy.Avatar.DefaultTechnique + ")");
                                need(ConfigValue("TS1CharacterDetail") == probe.ToString(), "ini-chardetail");
                                del(before);
                                need(set.TS1CharacterDetail == before
                                    && FSO.Vitaboy.Avatar.DefaultTechnique ==
                                        UIOriginalOptionsPanel.CharacterDetailTechnique(before),
                                    "row-chardetail-restore");
                            }
                        }

                        panel.ShowScreen("play");
                        var bgRow = panel.Rows.FirstOrDefault(r => r.CaptionIndex == 65);
                        need(bgRow != null, "play-siminback-row");
                        if (bgRow != null)
                        {
                            bool before = set.TS1SimInBackground;
                            bgRow.Toggle();
                            need(set.TS1SimInBackground == !before, "row-siminback");
                            bgRow.Toggle();
                            need(set.TS1SimInBackground == before, "row-siminback-restore");
                        }
                    }
                    mp.SetMode(prevMode); // gate entered from a non-OPTIONS mode
                }
            }
            finally
            {
                // restore the exact prior preference + engine state
                set.Lighting = s0Lighting;
                set.TS1Shadows = s0Shadows;
                set.TS1SimInBackground = s0Bg;
                set.TS1CharacterDetail = s0Detail;
                set.Save();
                try
                {
                    // exact runtime mode (not the toggle's 0/1 — boot may have
                    // preserved an explicit mode, e.g. the Program.cs ultra pin)
                    FSO.LotView.WorldConfig.Current.LightingMode = s0Mode;
                    vm.Context.World.ChangedWorldConfig(GameFacade.GraphicsDevice);
                    UIOriginalOptionsPanel.ApplyShadows(vm, s0Shadows);
                }
                catch (Exception) { }
                UIOriginalOptionsPanel.ApplyCharacterDetail(s0Detail);
            }

            details = "ok=" + string.Join(",", ok)
                + (bad.Count > 0 ? " FAILS:" + string.Join(",", bad) : " ALL-WIRES-LIVE");
            return bad.Count == 0;
        }

        /// <summary>
        /// Read one key from the LIVE userdir config.ini (the run's private
        /// copy) — the persistence round-trip proof surface.
        /// </summary>
        private static string ConfigValue(string key)
        {
            try
            {
                var path = Path.Combine(FSOEnvironment.UserDir, "config.ini");
                foreach (var line in File.ReadAllLines(path))
                {
                    var clean = line.Trim();
                    int eq = clean.IndexOf('=');
                    if (eq <= 0) continue;
                    if (string.Equals(clean.Substring(0, eq).Trim(), key,
                        StringComparison.OrdinalIgnoreCase))
                        return clean.Substring(eq + 1).Trim();
                }
            }
            catch (Exception) { }
            return null;
        }
    }
}
