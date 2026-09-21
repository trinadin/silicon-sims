/*
This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
If a copy of the MPL was not distributed with this file, You can obtain one at
http://mozilla.org/MPL/2.0/.
*/
using System;
using FSO.Content;

namespace Simitone.Client
{
    /// <summary>
    /// UI-31 'uir258' — ADDITIVE pins for the three r258 decoded presentation
    /// laws (coordination/evidence/UI-24/r258-law.md; raw decodes
    /// simitone-fork/tools/iff-dump/r258-pef-revisit/). The ported surfaces
    /// live in UIOriginalVitaIdlePlayer (Vita idle sway), UINeighborhood
    /// BalloonLayer (balloon sway) and UINeighborhoodCloudLayer (Magicland
    /// cloud seeding), all in Simitone.Client.UI.Panels; this check pins the
    /// laws themselves:
    ///   1. VITA IDLE SWAY: amplitudes are STATIC code-section pools —
    ///      gA 0x5a4dc8 = (float)pi, gB 0x5a4830 = {0.25, ..., 0.5} — pi/4 at
    ///      the Init sites (SetAmplitude gA[0]*gB[0]) and pi/2 at the ctors
    ///      (gA[0]*gB[+8]); SineGenerator::GetVal = amplitude*sin(k*(t-t0))
    ///      over the 10000 ms period; the sine path is GATED cat&&dog
    ///      (Solo::UpdateTransform 0x2DB250) — the ungated preview is a plain
    ///      copy with NO sway.
    ///   2. BALLOON SWAY: m = seed % 1440; sway = (int)(m*sin(0.785*m/180))
    ///      with lfd DOUBLES (pool file 0x5a5348); the amplitude envelope IS
    ///      m (no amplitude constant; the r110 "64.0 float" never existed).
    ///   3. MAGICLAND CLOUD SEEDING (InitClouds 0x57C890..0x57CAB0): the
    ///      ladder tables record-for-record (the old "relocated tables" were
    ///      subfic immediates).
    /// Tooling erratum carried from r258: ppc_decode.py prints conditional
    /// branches INVERTED (BO12=true/beq) — the r258 doc accounts for it; the
    /// gate/branch readings here follow the doc's accounting.
    /// </summary>
    public static partial class AutotestRunner
    {
        private static void CheckUIR258Laws()
        {
            try
            {
                bool ok = true; string info = "";

                // ---- Law 1: VITA IDLE SWAY --------------------------------
                // Constants: single-float pool math, exact power-of-two splits.
                float piF = Simitone.Client.UI.Panels.UIOriginalVitaIdlePlayer.EnginePiF;
                bool vitaConst = piF == 3.1415927410125732f
                    && Simitone.Client.UI.Panels.UIOriginalVitaIdlePlayer.InitAmplitudeRad == piF * 0.25f
                    && Simitone.Client.UI.Panels.UIOriginalVitaIdlePlayer.CtorAmplitudeRad == piF * 0.5f
                    && Simitone.Client.UI.Panels.UIOriginalVitaIdlePlayer.CtorAmplitudeRad == 2f * Simitone.Client.UI.Panels.UIOriginalVitaIdlePlayer.InitAmplitudeRad
                    && !Simitone.Client.UI.Panels.UIOriginalVitaIdleLaw.SwayAmplitudeIsRuntimeOnly;
                // SineGenerator law at seeded quarter-period inputs:
                // t=0 -> 0; t=2500 -> +pi/4; t=5000 -> ~0; t=7500 -> -pi/4.
                bool vitaSine = Simitone.Client.UI.Panels.UIOriginalVitaIdlePlayer.SwayOffset(0) == 0f
                    && Math.Abs(Simitone.Client.UI.Panels.UIOriginalVitaIdlePlayer.SwayOffset(2500) - Simitone.Client.UI.Panels.UIOriginalVitaIdlePlayer.InitAmplitudeRad) < 1e-4
                    && Math.Abs(Simitone.Client.UI.Panels.UIOriginalVitaIdlePlayer.SwayOffset(5000)) < 1e-4
                    && Math.Abs(Simitone.Client.UI.Panels.UIOriginalVitaIdlePlayer.SwayOffset(7500) + Simitone.Client.UI.Panels.UIOriginalVitaIdlePlayer.InitAmplitudeRad) < 1e-4;
                // Branch (live avatar, the uivitaplay recipe): ungated flags ->
                // RadianDirection == facing EXACTLY (plain copy); cat&&dog set
                // -> the pi/4 sine composes (bounded by the amplitude).
                bool vitaBranch = false;
                var vm = _vm;
                if (vm == null || vm.Context == null) { ok = false; info += " no-corpus-vm;"; }
                else
                {
                    var av = (FSO.SimAntics.VMAvatar)vm.Context.CreateObjectInstance(0x7FD96B54,
                        FSO.LotView.Model.LotTilePos.OUT_OF_WORLD, FSO.LotView.Model.Direction.EAST, false).BaseObject;
                    try
                    {
                        av.Avatar.Skeleton = Content.Get().AvatarSkeletons.Get("adult.skel").Clone();
                        av.Avatar.BaseSkeleton = av.Avatar.Skeleton.Clone();
                        av.Avatar.ReloadSkeleton();
                    }
                    catch (Exception sk) { ok = false; info += " skelEXC " + sk.GetType().Name + ";"; }
                    try
                    {
                        var upd = new FSO.Common.Rendering.Framework.Model.UpdateState();
                        var step16 = new Microsoft.Xna.Framework.GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16));
                        const float facing = 1.5f;
                        var player = new Simitone.Client.UI.Panels.UIOriginalVitaIdlePlayer(av, false, true);
                        upd.Time = step16;
                        player.Update(upd, facing);
                        bool plainCopy = av.RadianDirection == facing;    // flags default 0
                        player.FlagCat = 1; player.FlagDog = 1;           // the near-dead co-hold
                        player.Update(upd, facing);
                        float want = facing + Simitone.Client.UI.Panels.UIOriginalVitaIdlePlayer.SwayOffset(player.ProbeSwayMs());
                        bool gatedComposed = Math.Abs(av.RadianDirection - want) < 1e-4;
                        player.FlagCat = 0; player.FlagDog = 0;
                        player.Update(upd, facing);
                        vitaBranch = plainCopy && gatedComposed;
                    }
                    catch (Exception pe) { ok = false; info += " playerEXC " + pe.GetType().Name + ";"; }
                    try { vm.RemoveEntity(av); } catch (Exception) { }
                }

                // ---- Law 2: BALLOON SWAY ----------------------------------
                // Doubles + hard anchors desk-computed from the law (IEEE754
                // double math, fctiwz truncate-toward-zero):
                //   m=1 -> 0; m=2 -> 0; m=359 -> 358; m=360 -> 359 (phase
                //   exactly 1.57, sin < 1); m=598 -> 304; m=599 -> 302;
                //   m=720 -> 1; m=1080 -> -1079; m=1439 -> -10.
                bool balloon = Simitone.Client.UI.Panels.UINeighborhoodBalloonLayer.EnginePiOver4 == 0.785
                    && Simitone.Client.UI.Panels.UINeighborhoodBalloonLayer.EngineDegrees == 180.0
                    && Simitone.Client.UI.Panels.UINeighborhoodBalloonLayer.EngineSway(1) == 0
                    && Simitone.Client.UI.Panels.UINeighborhoodBalloonLayer.EngineSway(2) == 0
                    && Simitone.Client.UI.Panels.UINeighborhoodBalloonLayer.EngineSway(359) == 358
                    && Simitone.Client.UI.Panels.UINeighborhoodBalloonLayer.EngineSway(360) == 359
                    && Simitone.Client.UI.Panels.UINeighborhoodBalloonLayer.EngineSway(598) == 304
                    && Simitone.Client.UI.Panels.UINeighborhoodBalloonLayer.EngineSway(599) == 302
                    && Simitone.Client.UI.Panels.UINeighborhoodBalloonLayer.EngineSway(720) == 1
                    && Simitone.Client.UI.Panels.UINeighborhoodBalloonLayer.EngineSway(1080) == -1079
                    && Simitone.Client.UI.Panels.UINeighborhoodBalloonLayer.EngineSway(1439) == -10;
                // the wind-band pool doubles (r258 dump, double view)
                var wg = Simitone.Client.UI.Panels.UINeighborhoodBalloonLayer.EngineWindGates;
                balloon &= wg != null && wg.Length == 5
                    && wg[0] == 1.5f && wg[1] == 3.0f && wg[2] == 4.5f && wg[3] == 5.0f && wg[4] == 6.0f;
                // live walk: seed 1, +1/step, respawn every 599 steps -> m =
                // (s % 599) + 1; DrawX must equal 300 + the law at m, every step.
                try
                {
                    var cfgsB = Simitone.Client.UI.Panels.UINeighborhoodSelectionPanel.Neighborhoods;
                    var balAnim = cfgsB[6].Balloons;
                    if (balAnim == null) { ok = false; info += " balAnim-missing;"; }
                    else
                    {
                        var bl = new Simitone.Client.UI.Panels.UINeighborhoodBalloonLayer(balAnim, 15);
                        for (int s = 0; s < 1400 && balloon; s++)
                        {
                            bl.StepFrame();
                            int m = (s % 599) + 1;
                            int want = 300 + (int)((double)m * Math.Sin(0.785 * (double)m / 180.0));
                            if (bl.DrawX != want) { balloon = false; info += " bl-step" + s + ":x=" + bl.DrawX + "!=law" + want + " "; }
                        }
                    }
                }
                catch (Exception be) { ok = false; info += " balEXC " + be.GetType().Name + ";"; }

                // ---- Law 3: CLOUD SEEDING record-for-record ---------------
                // Independent restatement of InitClouds (0x57C890..0x57CAB0)
                // fed by an identically-seeded Random — the layer's ctor draws
                // slot/interval per record (loop 1), ONE rand%5 per record in
                // loops 2/3, TWO in loop 4 (x draw first); loop 3 overwrites
                // loop 2's records 6..7; records 12 and 17..20 stay default.
                bool clouds = false; string clinfo = "";
                try
                {
                    var cfgsC = Simitone.Client.UI.Panels.UINeighborhoodSelectionPanel.Neighborhoods;
                    var cloudAnim = cfgsC[6].Clouds;
                    if (cloudAnim == null) { ok = false; info += " cloudAnim-missing;"; }
                    else
                    {
                        var cl = new Simitone.Client.UI.Panels.UINeighborhoodCloudLayer(cloudAnim, 15);
                        var rng = new Random(0xC10D);
                        var expect = new Simitone.Client.UI.Panels.UINeighborhoodCloudLayer.EngineCloud[21];
                        for (int i = 0; i < 21; i++)
                            expect[i] = new Simitone.Client.UI.Panels.UINeighborhoodCloudLayer.EngineCloud
                            {
                                X = 0, Y = 0,
                                Slot = rng.Next(3),
                                Tick = 1,
                                Frame = -110,
                                Interval = rng.Next(2) + 2,
                                WrapX = 0, WrapY = 0, Drift = 0
                            };
                        for (int j = 0; j <= 7; j++)
                        {
                            int d = rng.Next(5);
                            expect[j].Y = 60 + 16 * j;
                            expect[j].X = 342 - 32 * j + j * d;
                            expect[j].WrapX = 462 - 32 * j;
                            expect[j].WrapY = 0;
                            expect[j].Drift = 2;
                        }
                        for (int c = 10; c <= 15; c++)
                        {
                            int k = c - 10, d = rng.Next(5);
                            expect[6 + k].Y = 60 + 16 * k;
                            expect[6 + k].X = 282 - 48 * k + c * d;
                            expect[6 + k].WrapX = 462 - 48 * k;
                            expect[6 + k].WrapY = 0;
                            expect[6 + k].Drift = 3;
                        }
                        for (int k = 0; k <= 3; k++)
                        {
                            int dx = rng.Next(5), dw = rng.Next(5);
                            expect[13 + k].Y = 60 + 10 * k;
                            expect[13 + k].X = 142 + k * dx;
                            expect[13 + k].WrapX = 262 + k * dw;
                            expect[13 + k].WrapY = 0;
                            expect[13 + k].Drift = 2;
                        }
                        clouds = true;
                        for (int i = 0; i < 21 && clouds; i++)
                        {
                            var g = cl.CloudsInfo[i]; var e = expect[i];
                            if (g.X != e.X || g.Y != e.Y || g.Slot != e.Slot || g.Tick != e.Tick
                                || g.Frame != e.Frame || g.Interval != e.Interval
                                || g.WrapX != e.WrapX || g.WrapY != e.WrapY || g.Drift != e.Drift)
                            { clouds = false; clinfo += "c" + i + ":mismatch "; }
                        }
                        // post-step: drift/wrap keeps every cloud in [0, 462]
                        for (int s = 0; s < 700 && clouds; s++) cl.StepFrame();
                        for (int i = 0; i < 21 && clouds; i++)
                            if (cl.CloudsInfo[i].X < 0 || cl.CloudsInfo[i].X > 462)
                            { clouds = false; clinfo += "c" + i + ":post-x-out "; }
                    }
                }
                catch (Exception ce) { clouds = false; clinfo += "EXC " + ce.GetType().Name + " " + ce.Message + ";"; }

                ok = ok && vitaConst && vitaSine && vitaBranch && balloon && clouds;
                Log("AUTOTEST uir258: vitaConst=" + vitaConst + " vitaSine=" + vitaSine
                    + " vitaBranch=" + vitaBranch + " balloon=" + balloon + " clouds=" + clouds
                    + (clinfo.Length > 0 ? " " + clinfo.Trim() : "")
                    + (info.Length > 0 ? " " + info.Trim() : ""));
                if (ok) { Pass("uir258"); return; }
                Log("AUTOTEST uir258: the three r258 presentation laws must pin (Vita pi/4+pi/2 pools + cat&&dog gate; balloon m*sin(0.785m/180) doubles; cloud ladder record-for-record)");
                Fail("uir258");
            }
            catch (Exception e) { Log("AUTOTEST uir258 EXC " + e.GetType().Name + " " + e.Message + " AT " + (e.StackTrace ?? "").Replace("\n", " | ")); Fail("uir258"); }
        }
    }
}
