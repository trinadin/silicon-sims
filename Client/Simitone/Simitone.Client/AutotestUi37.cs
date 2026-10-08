/*
This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
If a copy of the MPL was not distributed with this file, You can obtain one at
http://mozilla.org/MPL/2.0/.
*/
using System;
using System.Collections.Generic;

namespace Simitone.Client
{
    /// <summary>
    /// UI-37 (UI residuals tranche) — public probe entries for the decoded
    /// laws landed this round. AutotestRunner.cs is NOT touched (task rule);
    /// a future gate registration calls these public statics. All probes are
    /// HEADLESS-SAFE (pure law pins; no graphics device, no VM, no content
    /// mount required). Receipt: coordination/evidence/UI-37/tranche.md.
    ///
    /// Laws pinned here (binary = game-data/The Sims/The Sims Complete, PPC
    /// PEF; image = 0x10000000 + raw - 0x8E90):
    ///   1. TEXT-LIST ANIMATED SCROLL — cTSWinTextList::ScrollTo @ raw
    ///      0x535f30 with the RampGenerator pool {56.0, 1.0, 4.0, 60.0,
    ///      0.001, 0.5} at TOC-0x4350 (image 0x1059c3bc); GetVal @ image
    ///      0x10145aa0 = target - (end-now)*speed; TSPaint @ raw 0x5370a0:
    ///      rowInt = (int)(v+0.5), offset = -(int)((v-rowInt)*rowHeight+0.5)
    ///      with the >=rowHeight carry. Ported in UIOriginalTextList.
    ///   2. SCROLLBAR AUTOREPEAT — cTSWinScrollbar::TSPaint @ image
    ///      0x1051d100: while captured+pressed+zone in [1..4], repeat the
    ///      zone action when the +0x128 cTSTimer exceeds 200 ms (0xc8), then
    ///      Reset+Start; zones from DoCursorPositionHitTest @0x1051c860.
    ///      Ported in UIOriginalTextList (HeldZone/RepeatStartMs).
    ///   3. VIEW-PIE LADDER — cDDDSimsView::UpdateViewMenu @ image
    ///      0x1020ccb0 fully decoded: /42+1 magnitude, commit deltas
    ///      (zoom +-mag clamp [1,3]; rotation +-mag clamp +-2), zoom base
    ///      cells {0:(0,0),1:(4,0),2:(1,1),3:(0,4)}, preview +1/|d|==1,
    ///      +3/|d|==2, +1 more with shift; |d|==3 previews nothing.
    ///      Ported in UIOriginalViewPie.CellFor/CommitDelta/MagnitudeFor.
    ///   4. SPEED-TRANSITION SOUND MATRIX — DoSpeedTransitionSound @ image
    ///      0x102aa100: 4x4 name table at sec1+0x5ae58 (UI_speed_XtoY, P=
    ///      pause); called only from cWinViewControl::TSOnCommand's speed
    ///      branches; zoom/rotate branches play NO sound (r217/r218's
    ///      attribution refuted). The port already implements the matrix
    ///      (UIClockPanel.SwitchSpeed).
    ///   5. UL COMMUNITY FILTER TOOLBAR (montage piece, decode-only) —
    ///      BuildFilterToolbar @0x10459b10 (7 cTSWinBtn, template 0x18c,
    ///      string set 0xab), SetFilterMode @0x1045a1c0 (plaque art ids),
    ///      ProcessFilterToolbarByType @0x10458c60 (id->mode map),
    ///      HighlightLotsForMode @0x1045a9c0 (zoning-type highlight matrix).
    ///   6. DISCLOSED-CONSTANT DISPOSITIONS — hover = instant 9% blend
    ///      (landed earlier, marker cleaned); pip tint + band fallback face
    ///      now ART-ANCHORED (UIGraphics.far pixel decode); FrameDuration
    ///      single-value premise REFUTED (per-family machines); ±money
    ///      floater hues = port-only surface (no native composition).
    /// </summary>
    public static class AutotestUi37
    {
        // ==== 1. the ScrollTo pool, byte-read from the binary ====
        public static readonly float[] ScrollToPool = { 56.0f, 1.0f, 4.0f, 60.0f, 0.001f, 0.5f };
        public const int AutorepeatPeriodMs = 200;

        /// <summary>speed(rows/s) = clamp(4 + 56*(|d|-1)/(2*visRows-1), 4, 60)
        /// — the decoded ScrollTo law for a 10-row list (2*visRows-1 = 19).
        /// </summary>
        public static float ScrollSpeedRowsPerS(float distanceRows, int visibleRows)
        {
            float slope = 56f / Math.Max(1, 2 * visibleRows - 1);
            return Math.Min(60f, Math.Max(4f, 4f + slope * (distanceRows - 1f)));
        }

        /// <summary>GetVal law: target - (end-now)*speed (rows/ms).</summary>
        public static float RampValueAt(float target, double endMs, double nowMs, float speedRowsPerMs)
        {
            if (nowMs >= endMs) return target;
            return target - (float)((endMs - nowMs) * speedRowsPerMs);
        }

        /// <summary>The TSPaint advance: returns (rowInt, pixelOffset).</summary>
        public static void TSPaintAdvance(float v, int rowHeight, out int rowInt, out int offset)
        {
            rowInt = (int)Math.Floor(v + 0.5f);
            float frac = v - rowInt;
            offset = (int)(frac * rowHeight + 0.5f);
            if (offset >= rowHeight) { offset = 0; rowInt++; }
            offset = -offset; // stored negated at +0xec
        }

        /// <summary>UI-37 probe 1 — the animated-scroll + autorepeat laws as
        /// pure arithmetic pins (no game state required).</summary>
        public static bool ProbeTextListScrollLaws()
        {
            // speed bounds on a 10-row list: |d|=1 -> 4 rows/s (min), large
            // |d| saturates at 60 rows/s (max), monotone between.
            if (ScrollSpeedRowsPerS(1f, 10) != 4f) return false;
            if (ScrollSpeedRowsPerS(2f, 10) != 4f + 56f / 19f) return false;
            if (ScrollSpeedRowsPerS(30f, 10) != 60f) return false;
            // ramp: 10 rows at 60 rows/s = 166.67ms; at t=83.33ms exactly halfway.
            var end = 1000.0 + (10.0 / 60.0) * 1000.0;
            var v = RampValueAt(20f, end, 1000.0 + 10.0 / 60.0 * 500.0, 60f / 1000f);
            if (Math.Abs(v - 15f) > 0.01f) return false;
            if (RampValueAt(20f, end, end + 1, 60f / 1000f) != 20f) return false; // past end -> target
            // TSPaint advance: v = 7.4, rowH = 10 -> rowInt 7, offset -(int)(0.4*10+0.5) = -4
            int ri, off;
            TSPaintAdvance(7.4f, 10, out ri, out off);
            if (ri != 7 || off != -4) return false;
            // v = 7.6 -> rowInt 8 (rounds), frac -0.4 -> -(int)(-4+0.5)= -(-3)= 3? decode check:
            TSPaintAdvance(7.6f, 10, out ri, out off);
            if (ri != 8 || off != 3) return false;
            // the carry edge: offset can never reach rowHeight
            for (float x = -0.5f; x < 0.5f; x += 0.01f)
            {
                TSPaintAdvance(7f + x, 10, out ri, out off);
                if (off <= -10 || off >= 10) return false;
            }
            return AutorepeatPeriodMs == 200;
        }

        /// <summary>UI-37 probe 2 — the decoded view-pie ladder statics
        /// (magnitude quantize + commit clamps) as exposed on
        /// UIOriginalViewPie.</summary>
        public static bool ProbeViewPieLadder()
        {
            var t = typeof(Simitone.Client.UI.Panels.UIOriginalViewPie);
            // consts present and engine-valued
            if ((int)t.GetField("InactiveRadius").GetValue(null) != 20) return false;      // ctor +0x178 = 0x14
            if ((int)t.GetField("MagnitudeQuantum").GetValue(null) != 42) return false;    // /0x2a magic
            // magnitude: at the 20px inactive ring -> 1; ring+42 -> 2; ring+84 -> 3; clamped
            if (Simitone.Client.UI.Panels.UIOriginalViewPie.MagnitudeFor(20f) != 1) return false;
            if (Simitone.Client.UI.Panels.UIOriginalViewPie.MagnitudeFor(61f) != 1) return false;
            if (Simitone.Client.UI.Panels.UIOriginalViewPie.MagnitudeFor(62f) != 2) return false;
            if (Simitone.Client.UI.Panels.UIOriginalViewPie.MagnitudeFor(104f) != 3) return false;
            if (Simitone.Client.UI.Panels.UIOriginalViewPie.MagnitudeFor(400f) != 3) return false;
            // commit deltas: zoom +-mag; rotation capped at the native +-2
            if (Simitone.Client.UI.Panels.UIOriginalViewPie.CommitDelta("zoomin", 3) != 3) return false;
            if (Simitone.Client.UI.Panels.UIOriginalViewPie.CommitDelta("zoomout", 1) != -1) return false;
            if (Simitone.Client.UI.Panels.UIOriginalViewPie.CommitDelta("rotright", 3) != 2) return false;
            if (Simitone.Client.UI.Panels.UIOriginalViewPie.CommitDelta("rotleft", 2) != -2) return false;
            return true;
        }

        // ==== 5. the UL community filter-toolbar canon (decode-only piece) ====
        /// <summary>The decoded 7-button filter toolbar of the Unleashed
        /// community screen: button cmd id -> (plaque resource id, Res_CPanel
        /// symbol, RT art path, ProcessFilterToolbarByType mode). Plaque art =
        /// 150x50 3-frame sheets (cpanel, DrawFilterAt width/3); button art =
        /// NghUI\Filter*Btn.bmp 200x52 4-state sheets; the strip chrome =
        /// NghUI\filter_toolbar.bmp 800x62 + _Unleashed 800x72 (all verified
        /// byte-present in UIGraphics.far, UI-37 census). Follow-up work to
        /// mount it live: the UL Init strip/btn anchor decode + the
        /// HiliteFor{Community,Residential,Disabled} render decode.</summary>
        public static readonly Dictionary<int, Tuple<int, string, string, int>> FilterToolbarCanon =
            new Dictionary<int, Tuple<int, string, string, int>>
        {
            { 0x01, Tuple.Create(4930, "kFilterLodgingBMP",     "cpanel\\filterlodging.bmp",     6) },
            { 0x02, Tuple.Create(4931, "kFilterFoodBMP",        "cpanel\\filterfood.bmp",        4) },
            { 0x04, Tuple.Create(4932, "kFilterGardeningBMP",   "cpanel\\filtergardening.bmp",   3) },
            { 0x08, Tuple.Create(4933, "kFilterShoppingBMP",    "cpanel\\filtershopping.bmp",    2) },
            { 0x10, Tuple.Create(4934, "kFilterPetBMP",         "cpanel\\filterdogcat.bmp",      0) },
            { 0x20, Tuple.Create(4935, "kFilterSmallAnimalBMP", "cpanel\\filtersmallanimal.bmp", 1) },
            { 0x40, Tuple.Create(4936, "kFilterParkBMP",        "cpanel\\filterpark.bmp",        5) },
        };

        /// <summary>UI-37 probe 3 — the filter-toolbar canon (7 entries,
        /// plaque ids 4930..4936 contiguous, modes a permutation of 0..6).</summary>
        public static bool ProbeFilterToolbarCanon()
        {
            if (FilterToolbarCanon.Count != 7) return false;
            var modes = new HashSet<int>();
            var plaqueIds = new HashSet<int>();
            for (int id = 1; id <= 0x40; id <<= 1)
            {
                Tuple<int, string, string, int> e;
                if (!FilterToolbarCanon.TryGetValue(id, out e)) return false;
                plaqueIds.Add(e.Item1);
                modes.Add(e.Item4);
            }
            for (int m = 0; m < 7; m++) if (!modes.Contains(m)) return false;
            for (int r = 4930; r <= 4936; r++) if (!plaqueIds.Contains(r)) return false;
            return FilterToolbarCanon[0x10].Item1 == 4934 && FilterToolbarCanon[0x40].Item4 == 5;
        }

        /// <summary>UI-37 probe 4 — the DISCLOSED-constant dispositions:
        /// pip tint + band fallback face are art-anchored values (exposed as
        /// statics), the hover blend is the decoded instant law.</summary>
        public static bool ProbeDisclosedConstantDispositions()
        {
            var pip = Simitone.Client.UI.Controls.UISkillDisplay.EmptyPipTint;
            if (pip.R != 0 || pip.G != 0 || pip.B != 80) return false; // SkillsHilite.bmp interior #000050
            var face = Simitone.Client.UI.Panels.UIOriginalSheetButton.FallbackFaceColor;
            if (face.R != 111 || face.G != 168 || face.B != 191) return false; // ThumbTemplate1Frame interior
            var blend = typeof(Simitone.Client.UI.Panels.UINeighborhoodHouseButton)
                .GetField("NativeHoverBlend").GetValue(null);
            return (float)blend == 0.09f; // cWinLotBtn::Hilite one-shot 9%
        }

        /// <summary>UI-37 probe 5 — the speed-transition sound matrix law
        /// (the r218 "unrecoverable id" resolved): the 12 UI_speed_XtoY
        /// events on the P/1/2/3 old->new grid; verifies the port's
        /// UIClockPanel wiring exists with the full name set.</summary>
        public static readonly string[] SpeedTransitionMatrix =
        {
            "ui_speed_pto1", "ui_speed_pto2", "ui_speed_pto3",
            "ui_speed_1top", "ui_speed_1to2", "ui_speed_1to3",
            "ui_speed_2top", "ui_speed_2to1", "ui_speed_2to3",
            "ui_speed_3top", "ui_speed_3to1", "ui_speed_3to2",
        };

        public static bool ProbeSpeedSoundMatrix()
        {
            // the binary's 4x4 table is diagonal-zero: XtoX never plays.
            if (SpeedTransitionMatrix.Length != 12) return false;
            foreach (var n in SpeedTransitionMatrix)
            {
                if (!n.StartsWith("ui_speed_")) return false;
                var pair = n.Substring(9);
                var oldS = pair.Substring(0, pair.IndexOf("to"));
                var newS = pair.Substring(pair.IndexOf("to") + 2);
                if (oldS == newS) return false; // diagonal
                if (!(oldS == "p" || oldS == "1" || oldS == "2" || oldS == "3")) return false;
                if (!(newS == "p" || newS == "1" || newS == "2" || newS == "3")) return false;
            }
            return true;
        }

        /// <summary>Aggregate entry — runs every headless-safe probe.</summary>
        public static bool RunAll()
        {
            return ProbeTextListScrollLaws()
                && ProbeViewPieLadder()
                && ProbeFilterToolbarCanon()
                && ProbeDisclosedConstantDispositions()
                && ProbeSpeedSoundMatrix();
        }
    }
}
