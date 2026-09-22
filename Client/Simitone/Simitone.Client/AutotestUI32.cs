/*
This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
If a copy of the MPL was not distributed with this file, You can obtain one at
http://mozilla.org/MPL/2.0/.
*/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FSO.Common;
using FSO.Client;
using FSO.Common.Utils;
using FSO.Files.Formats.IFF.Chunks;
using FSO.LotView;
using FSO.Vitaboy;
using Simitone.Client.UI.Panels;
using Simitone.Client.UI.Screens;

namespace Simitone.Client
{
    public static partial class AutotestRunner
    {
        /// <summary>
        /// UI-32 'censorpixel' — RE-PINNED to the NATIVE law (r221 decode:
        /// HouseViewer::Censor @0x1ce894 + RenderCensoredBlocks @0x1ce5b4;
        /// decisions-20260922 "implement the native law"). The previous body
        /// pinned the port's disclosed approximation (static 12-color 8x8
        /// palette over a 35/50/70 rect) and is REPLACED.
        ///
        /// Leg A (pure, exact): Avatar.CensorCellColor pins — mean+jitter
        /// literals, clamp, seed dependence, ladder + tier mapping.
        /// Leg 0 (live raster): the simvis grab idiom (frame camera on
        /// avatar0, render world PreDraw+Draw offscreen with the component's
        /// CensorshipFlags forced 0 then 3) — the diff must be a dense block.
        /// Leg B (ladder): horizontal run structure of the mosaic must match
        /// the native zoom cell ladder (1,3)/(2,6)/(4,12) scaled to the
        /// surface — and NOT the old rectW/8 grid (color-change rate).
        /// Leg C (mean law): the mosaic rect is recovered from the diff and
        /// each cell's ON color must equal CensorCellColor over the OFF
        /// frame's underlying pixels with the exact seed mix (drift between
        /// the two grabs is the only error source; low-drift majority +
        /// small median error required).
        /// </summary>
        private static void CheckCensorPixel()
        {
            // ---- Leg A: the pure law, pinned by exact literals ----------------
            // (literals derived from the law's integer arithmetic; if the
            // implementation drifts at all, these fail)
            try
            {
                var seed = Avatar.CensorJitterSeed; // 0x53494D53
                Color[] uni;
                // 2x2 cell of (10,20,30), 4-wide buffer -> mean (10,20,30),
                // jitters (6,-6,-5) -> (16,14,25)
                uni = new Color[] { new Color(10,20,30), new Color(10,20,30), new Color(10,20,30), new Color(10,20,30),
                                    new Color(10,20,30), new Color(10,20,30), new Color(10,20,30), new Color(10,20,30) };
                var c1 = Avatar.CensorCellColor(uni, 4, new Rectangle(0, 0, 2, 2), seed);
                var a1 = Avatar.CensorCellColor(uni, 4, new Rectangle(0, 0, 2, 2), seed);
                bool exact = c1.R == 16 && c1.G == 14 && c1.B == 25;
                bool determinism = c1.R == a1.R && c1.G == a1.G && c1.B == a1.B;
                bool seedDep = false;
                {
                    var d = Avatar.CensorCellColor(uni, 4, new Rectangle(0, 0, 2, 2), seed + 1);
                    seedDep = (d.R != c1.R) || (d.G != c1.G) || (d.B != c1.B); // (13,22,24)
                }
                // 4x2 cell, mean 115 gray -> (121,109,110)
                var grays = new Color[] { new Color(100,100,100), new Color(110,110,110), new Color(120,120,120), new Color(130,130,130),
                                          new Color(100,100,100), new Color(110,110,110), new Color(120,120,120), new Color(130,130,130) };
                var c2 = Avatar.CensorCellColor(grays, 4, new Rectangle(0, 0, 4, 2), seed);
                bool mean2 = c2.R == 121 && c2.G == 109 && c2.B == 110;
                // 2x3 cell, mean (200,50,25) -> (206,44,19)
                var mix = new Color[] { new Color(199,50,25), new Color(201,50,25), new Color(200,49,26),
                                        new Color(200,51,24), new Color(200,50,25), new Color(199,50,25),
                                        new Color(200,50,25), new Color(200,50,25), new Color(200,50,25) };
                var c3 = Avatar.CensorCellColor(mix, 3, new Rectangle(0, 0, 2, 3), seed);
                bool mean3 = c3.R == 206 && c3.G == 44 && c3.B == 19;
                // clamp: black cell -> (6,0,0); white cell -> (255,249,250)
                var blacks = Enumerable.Repeat(new Color(0,0,0), 8).ToArray();
                var cb = Avatar.CensorCellColor(blacks, 4, new Rectangle(0, 0, 2, 2), seed);
                var whites = Enumerable.Repeat(new Color(255,255,255), 8).ToArray();
                var cw = Avatar.CensorCellColor(whites, 4, new Rectangle(0, 0, 2, 2), seed);
                bool clamp = cb.R == 6 && cb.G == 0 && cb.B == 0 && cw.R == 255 && cw.G == 249 && cw.B == 250;
                // jitter stays within +-8 of the mean over a scramble of cells
                bool bounds = true;
                {
                    var rndBuf = new Color[64];
                    for (int i = 0; i < 64; i++) rndBuf[i] = new Color((i * 37) % 256, (i * 91) % 256, (i * 53) % 256);
                    for (int cy = 0; cy < 8; cy += 2)
                        for (int cx = 0; cx < 8; cx += 2)
                        {
                            var got = Avatar.CensorCellColor(rndBuf, 8, new Rectangle(cx, cy, 2, 2), seed ^ (cx * 31 + cy * 17));
                            long mr = 0, mg = 0, mb = 0;
                            for (int y = cy; y < cy + 2; y++) for (int x = cx; x < cx + 2; x++)
                            { mr += rndBuf[y * 8 + x].R; mg += rndBuf[y * 8 + x].G; mb += rndBuf[y * 8 + x].B; }
                            bounds &= Math.Abs(got.R - mr / 4) <= 8 && Math.Abs(got.G - mg / 4) <= 8 && Math.Abs(got.B - mb / 4) <= 8;
                        }
                }
                // ladder + tier
                bool ladder = Avatar.CensorCellSize(Avatar.CENSOR_FAR, 1f) == new Point(1, 3)
                    && Avatar.CensorCellSize(Avatar.CENSOR_MED, 1f) == new Point(2, 6)
                    && Avatar.CensorCellSize(Avatar.CENSOR_NEAR, 1f) == new Point(4, 12)
                    && Avatar.CensorCellSize(Avatar.CENSOR_NEAR, 2f) == new Point(8, 24)
                    && Avatar.CensorCellSize(Avatar.CENSOR_FAR, 0.2f) == new Point(1, 1);
                bool tier = Avatar.CensorZoomTier(0.02f, false) == Avatar.CENSOR_NEAR
                    && Avatar.CensorZoomTier(0.01f, false) == Avatar.CENSOR_MED
                    && Avatar.CensorZoomTier(0.005f, false) == Avatar.CENSOR_FAR
                    && Avatar.CensorZoomTier(0.9f, true) == Avatar.CENSOR_MED;
                var lawOK = exact && determinism && seedDep && mean2 && mean3 && clamp && bounds && ladder && tier;
                Log("AUTOTEST censorpixel LegA exact=" + exact + " det=" + determinism + " seedDep=" + seedDep +
                    " means=" + mean2 + "/" + mean3 + " clamp=" + clamp + " bounds=" + bounds +
                    " ladder=" + ladder + " tier=" + tier);
                if (!lawOK) { Fail("censorpixel"); return; }
            }
            catch (Exception le) { Log("AUTOTEST censorpixel LegA EXC " + le.GetType().Name + " " + le.Message); Fail("censorpixel"); return; }

            // ---- Legs 0/B/C: live composition ---------------------------------
            try
            {
                var scr = GameFacade.Screens.CurrentUIScreen as TS1GameScreen;
                var world = scr?.LotControl?.World;
                var gd = GameFacade.GraphicsDevice;
                if (world == null || gd == null || _avatars.Count == 0)
                { Log("AUTOTEST censorpixel: no world/device/avatar"); Fail("censorpixel"); return; }
                var a0 = _avatars[0];
                var comp = a0.WorldUI as FSO.LotView.Components.AvatarComponent;
                if (comp == null) { Log("AUTOTEST censorpixel: null AvatarComponent"); Fail("censorpixel"); return; }
                int sw = gd.Viewport.Width, sh = gd.Viewport.Height;
                var origCenter = world.State.CenterTile;
                var origLevel = world.State.Level;
                int prevFlags = -1;
                try { prevFlags = comp.CensorshipFlags; } catch { }
                System.Func<Color[]> grab = () =>
                {
                    using (var rt = PPXDepthEngine.CreateRenderTarget(
                        gd, 1, 0, SurfaceFormat.Color, sw, sh, DepthFormat.None))
                    {
                        gd.SetRenderTarget(rt);
                        gd.Clear(new Color(0x72, 0x72, 0x72, 0xFF));
                        try { world.PreDraw(gd); } catch { }
                        gd.SetRenderTarget(rt);
                        try { world.Draw(gd); } catch { }
                        gd.SetRenderTarget(null);
                        var px = new Color[sw * sh];
                        rt.GetData<Color>(px);
                        return px;
                    }
                };
                try
                {
                    // frame the camera on the sim (simvis idiom) so the mosaic is on-screen
                    world.State.CenterTile = new Vector2(a0.Position.x / 16f, a0.Position.y / 16f);
                    world.State.Level = (sbyte)Math.Max(0, (int)a0.Position.Level);
                    world.State.PrepareCamera();
                    comp.CensorshipFlags = 0;
                    var off = grab();
                    comp.CensorshipFlags = 3;
                    var on = grab();
                    // restore before any analysis
                    comp.CensorshipFlags = (short)(prevFlags >= 0 ? prevFlags : 0);
                    world.State.CenterTile = origCenter;
                    world.State.Level = origLevel;
                    world.State.PrepareCamera();
                    // save the pair to UserDir (evidence trail)
                    try
                    {
                        using (var tOff = new Texture2D(gd, sw, sh))
                        { tOff.SetData(off); using (var f = File.Create(Path.Combine(FSO.Common.FSOEnvironment.UserDir, "ui32-censor-off.png"))) tOff.SaveAsPng(f, sw, sh); }
                        using (var tOn = new Texture2D(gd, sw, sh))
                        { tOn.SetData(on); using (var f = File.Create(Path.Combine(FSO.Common.FSOEnvironment.UserDir, "ui32-censor-on.png"))) tOn.SaveAsPng(f, sw, sh); }
                    }
                    catch { }

                    // expected tier + cell ladder at this surface
                    var proj = world.State.Projection;
                    int tier = Avatar.CensorZoomTier(Math.Abs(proj.M11), Math.Abs(proj.M34) > 0.0001f);
                    var cell = Avatar.CensorCellSize(tier, sw / 800f);
                    int expRectW = tier == Avatar.CENSOR_NEAR ? 70 : (tier == Avatar.CENSOR_MED ? 50 : 35);
                    int expRectH = (int)(expRectW * 1.4f);

                    // Leg 0: total diff + densest-window search
                    int diff = 0;
                    for (int y = 0; y < sh; y++)
                        for (int x = 0; x < sw; x++)
                        {
                            var p1 = on[y * sw + x]; var p2 = off[y * sw + x];
                            if (p1.R != p2.R || p1.G != p2.G || p1.B != p2.B) diff++;
                        }
                    if (diff == 0)
                    { Log("AUTOTEST censorpixel: ZERO diff through the live composition (mosaic not rasterized)"); Fail("censorpixel"); return; }

                    int winW = 96, winH = 112;
                    int wx0 = 0, wy0 = 0, bestCnt = -1;
                    for (int oy = 0; oy + winH <= sh; oy += 16)
                        for (int ox = 0; ox + winW <= sw; ox += 16)
                        {
                            int cnt = 0;
                            for (int y = oy; y < oy + winH; y += 2)
                                for (int x = ox; x < ox + winW; x += 2)
                                {
                                    var p1 = on[y * sw + x]; var p2 = off[y * sw + x];
                                    if (p1.R != p2.R || p1.G != p2.G || p1.B != p2.B) cnt++;
                                }
                            if (cnt > bestCnt) { bestCnt = cnt; wx0 = ox; wy0 = oy; }
                        }
                    int wx1 = wx0 + winW - 1, wy1 = wy0 + winH - 1;
                    var wmask = new bool[sh * sw];
                    int winDiff = 0;
                    for (int y = wy0; y <= wy1; y++)
                        for (int x = wx0; x <= wx1; x++)
                        {
                            var p1 = on[y * sw + x]; var p2 = off[y * sw + x];
                            if (p1.R == p2.R && p1.G == p2.G && p1.B == p2.B) continue;
                            winDiff++; wmask[y * sw + x] = true;
                        }
                    int bw = wx1 - wx0 + 1, bh = wy1 - wy0 + 1;
                    double density = winDiff / (double)(bw * bh);
                    Log("AUTOTEST censorpixel: totalDiff=" + diff + " winDiff=" + winDiff + " window=" + bw + "x" + bh +
                        " @(" + wx0 + "," + wy0 + ") density=" + density.ToString("0.00") +
                        " tier=" + tier + " cell=" + cell.X + "x" + cell.Y);
                    if (!(winDiff >= 1000 && density >= 0.25))
                    { Log("AUTOTEST censorpixel: Leg0 coverage/density FAIL"); Fail("censorpixel"); return; }

                    // Leg B: native ladder structure. Horizontal runs of diff
                    // pixels: the native cells are cellW wide (some merge via
                    // coincidental jitter) -> median run in [cellW, 2cellW+1].
                    var runs = new List<int>();
                    for (int y = wy0; y <= wy1; y++)
                    {
                        int run = 0;
                        for (int x = wx0; x <= wx1; x++)
                        {
                            if (wmask[y * sw + x]) run++;
                            else { if (run > 0) runs.Add(run); run = 0; }
                        }
                        if (run > 0) runs.Add(run);
                    }
                    runs.Sort();
                    int runMed = runs.Count > 0 ? runs[runs.Count / 2] : 0;
                    bool runOK = runMed >= cell.X && runMed <= 2 * cell.X + 1;
                    // color-change rate across the mosaic: native rectW/cellW
                    // distinct cells per row (old 8x8 grid: only ~8)
                    var rect2 = RecoverMosaicRect(wmask, sw, sh, wx0, wy0, wx1, wy1);
                    bool rateOK = false; double rate = 0; bool rectFound = rect2.Width > 0;
                    if (rectFound)
                    {
                        int changes = 0; int rows = 0;
                        for (int y = rect2.Y; y < rect2.Y + rect2.Height; y++)
                        {
                            int rch = 0; Color prev = on[y * sw + rect2.X]; bool any = false;
                            for (int x = rect2.X + 1; x < rect2.X + rect2.Width; x++)
                            {
                                var cur = on[y * sw + x];
                                if (cur.R != prev.R || cur.G != prev.G || cur.B != prev.B) rch++;
                                prev = cur; any = true;
                            }
                            if (any) { changes += rch; rows++; }
                        }
                        rate = rows > 0 ? changes / (double)rows : 0;
                        double expect = rect2.Width / (double)cell.X;
                        rateOK = rate >= expect * 0.5 && rate <= expect * 3.0;
                    }
                    Log("AUTOTEST censorpixel: LegB runMed=" + runMed + " (cell " + cell.X + " -> expect " + cell.X + ".." + (2 * cell.X + 1) + ") runOK=" + runOK +
                        " rect=" + (rectFound ? rect2.Width + "x" + rect2.Height + "@(" + rect2.X + "," + rect2.Y + ")" : "none") +
                        " changes/row=" + rate.ToString("0.0") + " rateOK=" + rateOK);
                    if (!runOK || !rateOK)
                    { Log("AUTOTEST censorpixel: LegB ladder structure FAIL"); Fail("censorpixel"); return; }

                    // Leg C: mean law. Predict each cell from the OFF frame
                    // (CensorCellColor + the exact seed mix) and compare with
                    // the ON frame's cell center. Drift between grabs is the
                    // only error source.
                    bool meanLaw = false; double medErr = -1; double frac = -1;
                    if (rectFound && Math.Abs(rect2.Width - expRectW) <= 8 && Math.Abs(rect2.Height - expRectH) <= 12)
                    {
                        // clip the predicted region to the surface, exactly as the draw does
                        var src = Rectangle.Intersect(rect2, new Rectangle(0, 0, sw, sh));
                        int okCells = 0, allCells = 0; var errs = new List<int>();
                        for (int y = 0; y < src.Height; y += cell.Y)
                        {
                            int ch = Math.Min(cell.Y, src.Height - y);
                            for (int x = 0; x < src.Width; x += cell.X)
                            {
                                int cwid = Math.Min(cell.X, src.Width - x);
                                var seed = Avatar.CensorJitterSeed
                                    ^ ((src.X + x) * 73856093)
                                    ^ ((src.Y + y) * 19349663);
                                var pred = Avatar.CensorCellColor(off, sw, new Rectangle(src.X + x, src.Y + y, cwid, ch), seed);
                                var act = on[(src.Y + y + ch / 2) * sw + (src.X + x + cwid / 2)];
                                int err = Math.Max(Math.Max(Math.Abs(pred.R - act.R), Math.Abs(pred.G - act.G)), Math.Abs(pred.B - act.B));
                                errs.Add(err); allCells++;
                                if (err <= 12) okCells++;
                            }
                        }
                        errs.Sort();
                        medErr = errs.Count > 0 ? errs[errs.Count / 2] : -1;
                        frac = allCells > 0 ? okCells / (double)allCells : -1;
                        meanLaw = frac >= 0.45 && medErr <= 6;
                    }
                    Log("AUTOTEST censorpixel: LegC mean-law frac=" + frac.ToString("0.00") + " medianErr=" + medErr +
                        " (need frac>=0.45 medErr<=6) meanLaw=" + meanLaw);
                    if (!(runOK && rateOK && meanLaw))
                    { Log("AUTOTEST censorpixel: LegC native mean-law FAIL"); Fail("censorpixel"); return; }
                    Pass("censorpixel");
                }
                catch (Exception ce)
                {
                    try { comp.CensorshipFlags = (short)(prevFlags >= 0 ? prevFlags : 0); } catch { }
                    world.State.CenterTile = origCenter;
                    world.State.Level = origLevel;
                    Log("AUTOTEST censorpixel EXC " + ce.GetType().Name + " " + ce.Message);
                    Fail("censorpixel");
                }
            }
            catch (Exception oe) { Log("AUTOTEST censorpixel outer EXC " + oe.GetType().Name); Fail("censorpixel"); }
        }

        /// <summary>
        /// Recover the drawn mosaic rect from the diff mask: rows/columns
        /// whose masked fraction within the search window exceeds 0.35. The
        /// mosaic is an opaque overwrite, so its rect shows as dense rows.
        /// </summary>
        private static Rectangle RecoverMosaicRect(bool[] mask, int sw, int sh, int wx0, int wy0, int wx1, int wy1)
        {
            int x0 = -1, x1 = -1, y0 = -1, y1 = -1;
            for (int y = wy0; y <= wy1; y++)
            {
                int cnt = 0;
                for (int x = wx0; x <= wx1; x++) if (mask[y * sw + x]) cnt++;
                if (cnt >= (wx1 - wx0 + 1) * 0.35) { if (y0 < 0) y0 = y; y1 = y; }
            }
            for (int x = wx0; x <= wx1; x++)
            {
                int cnt = 0;
                for (int y = wy0; y <= wy1; y++) if (mask[y * sw + x]) cnt++;
                if (cnt >= (wy1 - wy0 + 1) * 0.35) { if (x0 < 0) x0 = x; x1 = x; }
            }
            if (x0 < 0 || y0 < 0) return Rectangle.Empty;
            return new Rectangle(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        }

        /// <summary>
        /// UI-32 'uitall' — the R237 tall-backdrop restoration pins (the
        /// authored patch, integrated at the squashed baseline):
        /// (1) UIMainPanel.UsesTallSubpanel == CPState::IsDoubleByteUI
        /// @0x20c670 exactly (language IDs 15,17,18,19,20) over all 256
        /// values; (2) the double-byte arm is CONSTRUCTIBLE — the
        /// TallSubPanel.TGA art resolves at its native 600x150 and mounts in
        /// a UIImage; (3) live arm split under the current game language:
        /// single-byte mounts NOTHING (TallSubpanel null) while the native
        /// 804x100 PanelBack stays; the paint law keeps a mounted backdrop
        /// LIVE-desktop-only.
        /// </summary>
        private static void CheckUI32TallBackdrop()
        {
            try
            {
                // (1) the 256-value language truth table
                var want = new HashSet<byte> { 15, 17, 18, 19, 20 };
                bool table = Enumerable.Range(0, 256).All(c =>
                    UIMainPanel.UsesTallSubpanel((byte)c) == want.Contains((byte)c));
                bool spot = UIMainPanel.UsesTallSubpanel(15) && UIMainPanel.UsesTallSubpanel(17)
                    && UIMainPanel.UsesTallSubpanel(18) && UIMainPanel.UsesTallSubpanel(19)
                    && UIMainPanel.UsesTallSubpanel(20)
                    && !UIMainPanel.UsesTallSubpanel(0) && !UIMainPanel.UsesTallSubpanel(1)
                    && !UIMainPanel.UsesTallSubpanel(2) && !UIMainPanel.UsesTallSubpanel(14)
                    && !UIMainPanel.UsesTallSubpanel(16) && !UIMainPanel.UsesTallSubpanel(21)
                    && !UIMainPanel.UsesTallSubpanel(255);

                // (2) the double-byte arm's art + ctor (constructible without
                // CJK game data: the panel's own resolution + mount calls)
                bool constructible = false;
                try
                {
                    var tex = Simitone.Client.UI.Model.UIOriginal.EnsureResolved(
                        "cpanel\\Backgrounds\\TallSubPanel.TGA")?.Get(GameFacade.GraphicsDevice);
                    if (tex != null && tex.Width == 600 && tex.Height == 150)
                    {
                        var img = new FSO.Client.UI.Controls.UIImage(tex);
                        constructible = img != null;
                    }
                }
                catch (Exception ce) { Log("AUTOTEST uitall arm EXC " + ce.GetType().Name + " " + ce.Message); }

                // (3) live arm split + paint law
                var game = GameFacade.Screens.CurrentUIScreen as TS1GameScreen;
                var mp = game?.Frontend?.MainPanel;
                bool arms = false, paintLaw = true;
                if (mp != null)
                {
                    bool requiresTall = UIMainPanel.UsesTallSubpanel((byte)STR.DefaultLangCode);
                    arms = requiresTall
                        ? mp.TallSubpanel != null && mp.TallSubpanel.Texture.Width == 600
                            && mp.TallSubpanel.Texture.Height == 150
                        : (mp.TallSubpanel == null && mp.OriginalPanelBack != null
                            && mp.OriginalPanelBack.Size == new Vector2(804, 100));
                    if (mp.TallSubpanel != null)
                        paintLaw = mp.TallSubpanel.Visible == (mp.Mode == UIMainPanelMode.LIVE);
                }
                bool ok = table && spot && constructible && arms && paintLaw;
                Log("AUTOTEST uitall table=" + table + " spot=" + spot + " constructible=" + constructible +
                    " arms=" + arms + " paintLaw=" + paintLaw);
                if (ok) Pass("uitall"); else Fail("uitall");
            }
            catch (Exception oe) { Log("AUTOTEST uitall outer EXC " + oe.GetType().Name); Fail("uitall"); }
        }
    }
}
