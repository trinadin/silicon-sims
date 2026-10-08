/*
This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
If a copy of the MPL was not distributed with this file, You can obtain one at
http://mozilla.org/MPL/2.0/.
*/
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simitone.Client
{
    /// <summary>
    /// AUD-20 'aud20' — the footstep sound class, decoded from the original
    /// PPC PEF and implemented in the port (full decode receipt:
    /// coordination/evidence/AUD-20/footstep-law.md).
    ///
    /// Layers:
    ///   A. NAME-LAW (hard, offline-safe): the 15-name native census (14 live
    ///      corpus ids + footstep_terrain_noshoe DEAD), the 12-case surface
    ///      jumptable law @0x1061a00c through VMFootstepLaw.SoundNameForClass
    ///      (including the dead-name fallback), the barefoot outfit set
    ///      {1,5,10,14} (native 1&lt;&lt;(outfit-1) &amp; 0x2013), the SPR2
    ///      label-letter law (FloorData::FromResName 0x101a6d60:
    ///      M=0/H=1/S=2/C=3), and the live floor catalog wiring for four
    ///      pinned base floors (Berber Carpet=soft, Foursquare Parquet=hard,
    ///      El Rojo Terracotta=medium, Macadam=concrete-3).
    ///   B. ANIM CORPUS (hard): walking animations loaded through the real
    ///      TS1 providers must retain their authored "footstep" TimeProps
    ///      events with values in {1,2,-1,-2} — the native cadence source
    ///      (508 events ship in Animation.far; engine has no step timer).
    ///   C. DISPATCH WINDOW (live, degrades without a sound device): the 14
    ///      live names fire through the real HITVM.PlaySoundEvent with
    ///      HITTrace on — each must dispatch created/already-alive (or
    ///      RES_DISABLED when the headless run has no device), and the dead
    ///      footstep_terrain_noshoe must come back NOT-FOUND so the port can
    ///      never silently fire a corpus-dead id.
    ///   D. ZOOM BYTE-LAW PIN: WorldZoom must stay on the native integer
    ///      scale (Far=1/Medium=2/Near=3) — the AUD-20 TickSounds law
    ///      (1024-(3-zoom)*280)/1024 is only native-true under that mapping.
    /// </summary>
    public static partial class AutotestRunner
    {
        private static void CheckAud20()
        {
            try
            {
                bool ok = true;
                string info = "";

                var audio = FSO.Content.Content.Get().Audio;
                var evts = audio?.Events;

                // ---------- A. NAME LAW -------------------------------------------------

                // A1 full 15-name census: 14 live + footstep_terrain_noshoe DEAD
                // (AUD-19 pin reaffirmed by AUD-20's jumptable decode: the
                // native CAN select it — natively that sub-case was silent).
                string[] liveNames =
                {
                    "footstep_terrain", "footstep_hard", "footstep_hard_noshoe",
                    "footstep_medium", "footstep_medium_noshoe",
                    "footstep_soft", "footstep_soft_noshoe",
                    "footstep_snow", "footstep_plant", "footstep_trash",
                    "footstep_ash", "footstep_roach", "footstep_puddle",
                    "footstep_pool_swim_stroke",
                };
                int liveHave = liveNames.Count(n => evts != null && evts.ContainsKey(n));
                bool deadAbsent = evts == null || !evts.ContainsKey("footstep_terrain_noshoe");
                info += " census(live=" + liveHave + "/14,deadAbsent=" + deadAbsent + ")";
                if (liveHave != liveNames.Length || !deadAbsent) ok = false;

                // A2 surface-class -> name law (12-case jumptable @0x1061a00c).
                // (class, noshoe, expected)
                var cases = new[]
                {
                    new [] { "0", "0", "footstep_terrain" },          // grass default (port: no nhood-terrain field)
                    new [] { "1", "0", "footstep_hard" },             // deck / fs3
                    new [] { "2", "0", "footstep_hard" },             // no-floor 0xFF / fs1
                    new [] { "3", "0", "footstep_medium" },           // fs0
                    new [] { "4", "0", "footstep_soft" },             // fs2
                    new [] { "5", "0", "footstep_medium" },           // unreachable natively
                    new [] { "6", "0", "footstep_plant" },
                    new [] { "7", "0", "footstep_trash" },
                    new [] { "8", "0", "footstep_ash" },
                    new [] { "9", "0", "footstep_roach" },
                    new [] { "10", "0", "footstep_puddle" },
                    new [] { "11", "0", "footstep_pool_swim_stroke" },
                    new [] { "1", "1", "footstep_hard_noshoe" },
                    new [] { "3", "1", "footstep_medium_noshoe" },
                    new [] { "4", "1", "footstep_soft_noshoe" },
                    new [] { "0", "1", "footstep_terrain" },          // terrain_noshoe is DEAD -> live twin
                    new [] { "6", "1", "footstep_plant" },            // single-name families ignore noshoe
                    new [] { "10", "1", "footstep_puddle" },
                };
                bool nameLaw = true;
                foreach (var c in cases)
                {
                    var got = FSO.SimAntics.Model.VMFootstepLaw.SoundNameForClass(int.Parse(c[0]), c[1] == "1");
                    if (got != c[2])
                    {
                        Log("AUTOTEST aud20: name law class=" + c[0] + " noshoe=" + c[1] + " got " + got + " want " + c[2]);
                        nameLaw = false;
                    }
                }
                info += " nameLaw=" + nameLaw;
                if (!nameLaw) ok = false;

                // A3 barefoot outfit set (native {1,5,10,14} = Naked,
                // Sleepwear, Luau, ExpandedSwimsuit).
                bool noShoe = FSO.SimAntics.Model.VMFootstepLaw.IsNoShoeOutfit(1)
                    && FSO.SimAntics.Model.VMFootstepLaw.IsNoShoeOutfit(5)
                    && FSO.SimAntics.Model.VMFootstepLaw.IsNoShoeOutfit(10)
                    && FSO.SimAntics.Model.VMFootstepLaw.IsNoShoeOutfit(14)
                    && !FSO.SimAntics.Model.VMFootstepLaw.IsNoShoeOutfit(0)
                    && !FSO.SimAntics.Model.VMFootstepLaw.IsNoShoeOutfit(2)
                    && !FSO.SimAntics.Model.VMFootstepLaw.IsNoShoeOutfit(4);
                info += " noShoe=" + noShoe;
                if (!noShoe) ok = false;

                // A4 SPR2 label-letter law (FloorData::FromResName):
                // M(tile)=0 H(hardwood)=1 S(carpet)=2 C(concrete)=3, else 0.
                bool labelLaw =
                    FSO.Content.WorldFloorProvider.FloorSoundClassFromLabel("MR73G39B18") == 0
                    && FSO.Content.WorldFloorProvider.FloorSoundClassFromLabel("HRC6G84B42") == 1
                    && FSO.Content.WorldFloorProvider.FloorSoundClassFromLabel("SRC6GADB84") == 2
                    && FSO.Content.WorldFloorProvider.FloorSoundClassFromLabel("CR19G19B19") == 3
                    && FSO.Content.WorldFloorProvider.FloorSoundClassFromLabel("C") == 3
                    && FSO.Content.WorldFloorProvider.FloorSoundClassFromLabel("") == 0
                    && FSO.Content.WorldFloorProvider.FloorSoundClassFromLabel(null) == 0
                    && FSO.Content.WorldFloorProvider.FloorSoundClassFromLabel("X1234567890") == 0;
                info += " labelLaw=" + labelLaw;
                if (!labelLaw) ok = false;

                // A5 live catalog wiring: the four pinned base floors resolve
                // to their native sound classes through the real provider
                // (floors.iff SPR2 labels + Build.iff STR#0x82 names).
                var floors = FSO.Content.Content.Get().WorldFloors;
                bool catalog = floors != null
                    && floors.GetFloorSound(1) == 2   // Berber Carpet (S)
                    && floors.GetFloorSound(4) == 1   // Foursquare Parquet (H)
                    && floors.GetFloorSound(5) == 0   // "El Rojo" Terracotta Tile (M)
                    && floors.GetFloorSound(9) == 3   // Macadam (C)
                    && floors.GetFloorSound(2) == 2   // Moss Green Carpet (S)
                    && floors.GetFloorSound(20) == 1  // Hedman Herringbone Hardwood (H)
                    && floors.GetFloorSound(9999) == 2; // unknown pattern -> native miss default
                info += " catalog=" + catalog;
                if (!catalog) ok = false;

                // ---------- B. ANIM CORPUS (footstep events retained) -------------------

                // The native trigger is anim events; the port parser must
                // keep them. Load walk-named animations through the real
                // TS1 provider and count footstep TimeProps entries with
                // lawful values.
                int animsWithSteps = 0, stepEvents = 0;
                var badValues = new List<string>();
                try
                {
                    var anims = FSO.Content.Content.Get().AvatarAnimations as FSO.Content.TS1.TS1BCFAnimationProvider;
                    var names = (anims != null && anims.BaseProvider != null && anims.BaseProvider.AnimHostBCF != null)
                        ? anims.BaseProvider.AnimHostBCF.Keys.ToList() : new List<string>();
                    foreach (var nm in names)
                    {
                        if (nm == null || !nm.ToLowerInvariant().Contains("walk")) continue;
                        FSO.Vitaboy.Animation anim = null;
                        try { anim = anims.Get(nm + ".anim"); } catch { } // provider Get() strips a trailing ".anim"
                        if (anim == null || anim.Motions == null) continue;
                        bool has = false;
                        foreach (var motion in anim.Motions)
                        {
                            if (motion == null || motion.TimeProperties == null) continue;
                            foreach (var tpl in motion.TimeProperties)
                            {
                                if (tpl == null || tpl.Items == null) continue;
                                foreach (var item in tpl.Items)
                                {
                                    var v = item.Properties != null ? item.Properties["footstep"] : null;
                                    if (v == null) continue;
                                    has = true;
                                    stepEvents++;
                                    if (v != "1" && v != "2" && v != "-1" && v != "-2") badValues.Add(nm + ":" + v);
                                }
                            }
                        }
                        if (has) animsWithSteps++;
                    }
                }
                catch (Exception ae)
                {
                    Log("AUTOTEST aud20 anim corpus EXC " + ae.GetType().Name + " " + ae.Message);
                    ok = false;
                }
                info += " anims(stepsIn=" + animsWithSteps + ",events=" + stepEvents + ",badVals=" + badValues.Count + ")";
                if (animsWithSteps <= 0 || stepEvents <= 0 || badValues.Count > 0) ok = false;

                // ---------- C. DISPATCH WINDOW ------------------------------------------

                var hit = FSO.HIT.HITVM.Get();
                if (hit == null)
                {
                    Log("AUTOTEST aud20: HITVM.Get() null — static laws only" + (ok ? "" : " (FAILED)") + info);
                    if (ok) { Pass("aud20"); } else { Fail("aud20"); }
                    return;
                }

                FSO.HIT.HITTrace.Reset();
                FSO.HIT.HITTrace.Enabled = true;
                try
                {
                    foreach (var n in liveNames) hit.PlaySoundEvent(n);
                    hit.PlaySoundEvent("footstep_terrain_noshoe"); // negative control: DEAD id
                }
                catch (Exception te)
                {
                    Log("AUTOTEST aud20 dispatch EXC " + te.GetType().Name + " " + te.Message);
                    ok = false;
                }
                FSO.HIT.HITTrace.Enabled = false;

                var counts = FSO.HIT.HITTrace.Counts();
                Func<string, int[]> cnt = n => { int[] c; return counts.TryGetValue(n, out c) ? c : new int[7]; };
                int dispatched = 0, notFoundLive = 0;
                foreach (var n in liveNames)
                {
                    var c = cnt(n);
                    if (FSO.HIT.HITVM.DISABLE_SOUND ? c[4] >= 1 : (c[3] >= 1 || c[1] >= 1)) dispatched++;
                    else notFoundLive++;
                }
                bool deadNotFound = cnt("footstep_terrain_noshoe")[2] >= 1;
                info += " dispatch(ok=" + dispatched + "/14,liveMiss=" + notFoundLive + ",deadNotFound=" + deadNotFound + ")";
                if (dispatched != liveNames.Length || !deadNotFound) ok = false;

                // ---------- D. ZOOM BYTE-LAW PIN -----------------------------------------

                // The AUD-20 TickSounds law (1024-(3-zoom)*280)/1024 is only
                // native-true while WorldZoom keeps the native integer scale.
                bool zoomScale = (int)FSO.LotView.WorldZoom.Near == 3
                    && (int)FSO.LotView.WorldZoom.Medium == 2
                    && (int)FSO.LotView.WorldZoom.Far == 1;
                // native factors at default att (0x14): zoom3=1.0, 2=~0.7266, 1=~0.4531
                var wantFactors = new Dictionary<int, float> { { 3, 1f }, { 2, (1024f - 280f) / 1024f }, { 1, (1024f - 560f) / 1024f } };
                bool zoomLaw = zoomScale;
                foreach (var kv in wantFactors)
                {
                    var factor = (1024 - (3 - kv.Key) * 280) / 1024f;
                    if (Math.Abs(factor - kv.Value) > 0.0001f) zoomLaw = false;
                }
                info += " zoomLaw=" + zoomLaw;
                if (!zoomLaw) ok = false;

                Log("AUTOTEST aud20:" + info + (ok ? "" : " (FAILED)"));
                if (ok) { Pass("aud20"); } else { Fail("aud20"); }
            }
            catch (Exception e)
            {
                Log("AUTOTEST aud20 EXC " + e.GetType().Name + " " + e.Message + "\n" + e.StackTrace);
                Fail("aud20");
            }
        }
    }
}
