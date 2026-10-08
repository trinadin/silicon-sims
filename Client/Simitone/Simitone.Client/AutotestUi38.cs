/*
This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
If a copy of the MPL was not distributed with this file, You can obtain one at
http://mozilla.org/MPL/2.0/.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace Simitone.Client
{
    /// <summary>
    /// UI-38 (neighbourhood layout montage + UL filter-toolbar live mount) —
    /// the 'uidtbar' gate. Laws pinned here (binary = game-data/The Sims/The
    /// Sims Complete, PPC PEF; image = 0x10000000 + vaddr; vaddr = fileoff -
    /// 0x8E90; receipt coordination/evidence/UI-38/ul-filter-toolbar-law.md):
    ///   1. THE UL COMMUNITY FILTER TOOLBAR (cWinNeighborhoodUL, live mount):
    ///      the community screen (this+0x111==0) has NO navbar — the strip
    ///      kFilterToolbar 5045 (NghUI/Filter_Toolbar_Unleashed.bmp 800x72,
    ///      Res_Nbhd.h in UIGraphics.far) IS the top chrome (Init
    ///      0x10462810, strip button this+0x180, artboard origin). SEVEN
    ///      cTSWinBtn children at the static-init ladder (writer
    ///      0x465460-0x4656b4 = file 0x46e3bc): (225|275|325|375|425|475|525,
    ///      y=20), cells 50x52 (200x52 sheets, SetImage(4,1)); art ids
    ///      5060-5066 byte-proven (DogCat 5064, SmallAnimal 5065, Shopping
    ///      5063, Gardening 5062, Food 5061, Recreation 5066, Lodging 5060);
    ///      labels = STR# 171 'Filter Bar' english[0..6] (UIText.iff,
    ///      GetString 1-based). DISCLOSED: the port's mode 4 keeps the
    ///      navbar (merged view) so the strip band sits at artboard y=52.
    ///   2. THE CLICK/STATE LAW: ProcessFilterToolbarByType @0x10458c60
    ///      (bit->mode {1:6,2:4,4:3,8:2,0x10:0,0x20:1,0x40:5}) +
    ///      ProcessFilterToolbar @0x104598f0 (clear prev button SetState 0,
    ///      press new) + SetFilterMode @0x1045a1c0 (plaque 4930-4936 =
    ///      cpanel\filter*.bmp 150x50 3-frame) + the Init default filter
    ///      0x10 DogCat (this+0x188=0x10, plaque 0x1346, Init tail 0x46c9c0).
    ///   3. HIGHLIGHTLOTSFORMODE @0x1045a9c0 + Hilite__10cWinLotBtnFbiii
    ///      @0x102c9800: community (GetZoningType==2) -> BLUE (0,0,255);
    ///      mode==3 arm: residential unoccupied (lot+0x1b0==-1) -> GREEN
    ///      (0,200,0) else RED (242,0,0); mode==5: all but the target RED.
    ///      Render = buffer copy, +9% brighten interior ({1.0,0.09} pool at
    ///      PTR 0x105bc74c -> 0x59b970), flat tint outline at the mask
    ///      boundary, one scanline pass in native order.
    ///   4. THE PLAQUE (DrawFilterAt @0x1045a340, callers 0x45ac40/0x45b06c):
    ///      per lot matching `(cmdId & lot.categoryBits) != 0`, at the lot
    ///      CenterOn point minus (24,70), frame 0 (src w/3 of the 150x50
    ///      sheet). Production category bits are 0 until the native
    ///      lot+0x1ac writer is decoded (DISCLOSED); the probe drives the
    ///      machinery through the public bits field.
    /// </summary>
    public static class AutotestUi38
    {
        // ==== 1. canon statics (pure law pins) ====
        public const int ULStripArtId = 5045;              // kFilterToolbar
        public const int ULStripWidth = 800, ULStripHeight = 72;
        public static readonly int[] ULLadderXs = { 225, 275, 325, 375, 425, 475, 525 };
        public const int ULLadderY = 20;                   // strip-local
        public const int ULCellWidth = 50, ULCellHeight = 52;
        public const int ULDefaultFilterCmd = 0x10;        // the Init tail default (DogCat)
        public static readonly int[] ULButtonArtIds = { 5064, 5065, 5063, 5062, 5061, 5066, 5060 };
        public static readonly int[] ULCmdBits = { 0x10, 0x20, 0x08, 0x04, 0x02, 0x40, 0x01 };
        public static readonly int[] ULModes = { 0, 1, 2, 3, 4, 5, 6 };
        // STR# 171 english [0..6] (1-based native GetString i+1)
        public static readonly string[] ULLabels =
        {
            "Show Dog & Cat Adoption Centers", "Show Small Pet Shops",
            "Show Shopping Locations", "Show Gardening Shops",
            "Show Food Service Areas", "Show Recreational Areas",
            "Show Lodging Areas",
        };
        // plaque art ids 4930..4936 (SetFilterMode 0x1342+idx) + members
        public static readonly int[] ULPlaqueIds = { 4934, 4935, 4933, 4932, 4931, 4936, 4930 };
        public static readonly string[] ULPlaqueMembers =
        {
            "cpanel\\filterdogcat.bmp", "cpanel\\filtersmallanimal.bmp",
            "cpanel\\filtershopping.bmp", "cpanel\\filtergardening.bmp",
            "cpanel\\filterfood.bmp", "cpanel\\filterpark.bmp",
            "cpanel\\filterlodging.bmp",
        };
        public const int ULPlaqueWidth = 150, ULPlaqueHeight = 50, ULPlaqueFrames = 3;
        public static readonly int[] ULPlaqueOffset = { -24, -70 }; // lot CenterOn minus
        // HiliteFor category colors (literal wrapper args)
        public static readonly Color ULCommunityColor = new Color(0, 0, 255);
        public static readonly Color ULResidentialColor = new Color(0, 200, 0);
        public static readonly Color ULDisabledColor = new Color(242, 0, 0);
        public const float ULBrightenPool = 0.09f;         // {1.0, 0.09} at 0x59b970

        /// <summary>Pure canon pin: every table above is internally exact
        /// (ladder 50px pitch, contiguous art/plaque ids, mode permutation).</summary>
        public static bool ProbeULFilterCanon()
        {
            if (ULLadderXs.Length != 7) return false;
            for (int i = 1; i < 7; i++) if (ULLadderXs[i] - ULLadderXs[i - 1] != 50) return false;
            var modes = new HashSet<int>(ULModes);
            if (modes.Count != 7) return false;
            for (int m = 0; m < 7; m++) if (!modes.Contains(m)) return false;
            var plaqueIds = new HashSet<int>(ULPlaqueIds);
            for (int r = 4930; r <= 4936; r++) if (!plaqueIds.Contains(r)) return false;
            // the canon law entries on the switcher must equal the pin tables
            var law = Simitone.Client.UI.Panels.UINeighbourhoodSwitcher.ULFilterLaw;
            if (law.Length != 7) return false;
            for (int i = 0; i < 7; i++)
            {
                if (law[i].X != ULLadderXs[i] || law[i].Y != ULLadderY) return false;
                if (law[i].CmdBit != ULCmdBits[i]) return false;
                if (law[i].ArtId != ULButtonArtIds[i]) return false;
                if (law[i].Mode != ULModes[i]) return false;
                if (law[i].PlaqueMember != ULPlaqueMembers[i]) return false;
                if (!law[i].Member.ToLowerInvariant().Contains(ButtonNameFragment(i))) return false;
            }
            if (Simitone.Client.UI.Panels.UINeighborhoodHouseButton.ULPlaqueOffsetX != ULPlaqueOffset[0]
                || Simitone.Client.UI.Panels.UINeighborhoodHouseButton.ULPlaqueOffsetY != ULPlaqueOffset[1])
                return false;
            var c = Simitone.Client.UI.Panels.UINeighborhoodHouseButton.ULCommunityTint;
            if (c.R != 0 || c.G != 0 || c.B != 255) return false;
            var r2 = Simitone.Client.UI.Panels.UINeighborhoodHouseButton.ULResidentialTint;
            if (r2.R != 0 || r2.G != 200 || r2.B != 0) return false;
            var d = Simitone.Client.UI.Panels.UINeighborhoodHouseButton.ULDisabledTint;
            if (d.R != 242 || d.G != 0 || d.B != 0) return false;
            return Simitone.Client.UI.Panels.UINeighborhoodHouseButton.NativeFilterBrighten == ULBrightenPool;
        }

        private static string ButtonNameFragment(int i)
        {
            return new[] { "dogcat", "smallanimal", "shopping", "gardening",
                "food", "recreation", "lodging" }[i];
        }

        // ==== 2. the live mount probe ====
        /// <summary>'uidtbar' — constructs the REAL mode-4 community screen
        /// (panel + switcher) headlessly and asserts the decoded law: strip
        /// geometry, the 7-button ladder, the Init default (DogCat pressed,
        /// plaque, community lots blue), the click law, the mode-3 hilite
        /// matrix and the plaque gate/anchor. Returns pass/fail; log via the
        /// supplied sink.</summary>
        public static bool RunDtBarProbe(Action<string> log)
        {
            try
            {
                if (!ProbeULFilterCanon())
                { log("uidtbar: canon tables mismatch the pinned law"); return false; }

                var panel = new Simitone.Client.UI.Panels.UINeighborhoodSelectionPanel(4);
                var sw = new Simitone.Client.UI.Panels.UINeighbourhoodSwitcher(panel, 4, false);

                var screen = FSO.Client.UI.Framework.UIScreen.Current;
                int scrW = screen != null ? screen.ScreenWidth : 1024;
                int scrH = screen != null ? screen.ScreenHeight : 768;
                var board = Simitone.Client.UI.Panels.UINeighbourhoodSwitcher.OriginalArtboardOffset(scrW, scrH);

                // (a) the strip: mounted, engine dims, artboard (0,52) — the
                // disclosed merged-view band below the port's navbar.
                if (!sw.ULFilterStripMounted || sw.ULFilterStrip == null)
                { log("uidtbar: strip not mounted"); return false; }
                var strip = sw.ULFilterStrip;
                if (strip.Texture == null || strip.Texture.Width != ULStripWidth || strip.Texture.Height != ULStripHeight)
                { log("uidtbar: strip dims " + (strip.Texture == null ? "null" : strip.Texture.Width + "x" + strip.Texture.Height)); return false; }
                if (strip.Position != new Vector2(0, 52) + board)
                { log("uidtbar: strip pos " + strip.Position); return false; }

                // (b) the seven buttons: engine members, ladder anchors
                // (strip-local y=20 -> artboard 72), 50x52 cells, labels.
                var law = Simitone.Client.UI.Panels.UINeighbourhoodSwitcher.ULFilterLaw;
                if (sw.ULFilterButtons.Count != 7 || !sw.ULFilterButtonsMounted)
                { log("uidtbar: buttons mounted=" + sw.ULFilterButtons.Count + " all=" + sw.ULFilterButtonsMounted); return false; }
                for (int i = 0; i < 7; i++)
                {
                    var b = sw.ULFilterButtons[i];
                    if (!b.OriginalMounted || b.Texture == null)
                    { log("uidtbar: btn[" + i + "] art unresolved"); return false; }
                    if (b.Texture.Width != 200 || b.Texture.Height != 52 || b.CellWidth != ULCellWidth || b.CellHeight != ULCellHeight)
                    { log("uidtbar: btn[" + i + "] cell " + b.CellWidth + "x" + b.CellHeight); return false; }
                    if (b.Position != new Vector2(law[i].X, 52 + law[i].Y) + board)
                    { log("uidtbar: btn[" + i + "] pos " + b.Position); return false; }
                    if (string.IsNullOrEmpty(b.Tooltip) || !b.Tooltip.ToLowerInvariant().Contains(ButtonNameKeyword(i)))
                    { log("uidtbar: btn[" + i + "] tooltip '" + b.Tooltip + "'"); return false; }
                }

                // (c) the Init default: filter 0x10 (DogCat) pressed, plaque
                // 4934 mounted, engine mode 0, community lots BLUE.
                if (sw.ULFilterCmdId != ULDefaultFilterCmd || sw.ULFilterEngineMode != 0
                    || sw.ULFilterPlaqueMember != ULPlaqueMembers[0])
                { log("uidtbar: default filter cmd=" + sw.ULFilterCmdId + " mode=" + sw.ULFilterEngineMode + " plaque=" + sw.ULFilterPlaqueMember); return false; }
                if (!sw.ULFilterButtons[0].Selected || sw.ULFilterButtons.Skip(1).Any(x => x.Selected))
                { log("uidtbar: default selection state wrong"); return false; }
                var lots = panel.LotButtonsForProbe;
                if (lots.Count == 0) { log("uidtbar: no lot buttons on panel"); return false; }
                var neigh = FSO.Content.Content.Get().Neighborhood;
                var communityLots = new List<int>();
                var resFree = new List<int>();
                var resOcc = new List<int>();
                foreach (var kv in lots)
                {
                    short z; if (!neigh.ZoningDictionary.TryGetValue((short)kv.Key, out z)) z = 0;
                    if (z > 0) communityLots.Add(kv.Key);
                    else if (neigh.GetFamilyForHouse((short)kv.Key) == null) resFree.Add(kv.Key);
                    else resOcc.Add(kv.Key);
                }
                if (communityLots.Count == 0) { log("uidtbar: no community-zoned lots in test data"); return false; }
                foreach (int h in communityLots)
                {
                    var t = lots[h].ULFilterTintForProbe;
                    if (!t.HasValue || t.Value != ULCommunityColor)
                    { log("uidtbar: lot " + h + " not community-blue under mode 0"); return false; }
                    if (lots[h].ULHiliteTextureForProbe == null)
                    { log("uidtbar: lot " + h + " no hilite texture"); return false; }
                }
                foreach (int h in resFree.Concat(resOcc))
                    if (lots[h].ULFilterTintForProbe.HasValue)
                    { log("uidtbar: lot " + h + " hilited under mode 0 (engine: community only)"); return false; }

                // (d) the click law: Shopping (cmd 8) — prev cleared, new
                // pressed, plaque swapped, community lots still blue.
                int clicks0 = sw.ULFilterClicksForProbe;
                sw.ProcessULFilterByType(0x08);
                if (sw.ULFilterCmdId != 0x08 || sw.ULFilterEngineMode != 2
                    || sw.ULFilterPlaqueMember != ULPlaqueMembers[2] || sw.ULFilterClicksForProbe != clicks0 + 1)
                { log("uidtbar: click law state wrong after Shopping"); return false; }
                if (sw.ULFilterButtons[0].Selected || !sw.ULFilterButtons[2].Selected)
                { log("uidtbar: button press state wrong after Shopping"); return false; }
                foreach (int h in communityLots)
                    if (lots[h].ULFilterTintForProbe != ULCommunityColor)
                    { log("uidtbar: lot " + h + " lost blue after Shopping"); return false; }

                // (e) the mode-3 arm (Gardening): community blue, residential
                // green when unoccupied, red when occupied.
                sw.ProcessULFilterByType(0x04);
                if (sw.ULFilterEngineMode != 3) { log("uidtbar: gardening mode not 3"); return false; }
                foreach (int h in communityLots)
                    if (lots[h].ULFilterTintForProbe != ULCommunityColor)
                    { log("uidtbar: lot " + h + " not blue under mode 3"); return false; }
                foreach (int h in resFree)
                    if (lots[h].ULFilterTintForProbe != ULResidentialColor)
                    { log("uidtbar: free lot " + h + " not green under mode 3"); return false; }
                foreach (int h in resOcc)
                    if (lots[h].ULFilterTintForProbe != ULDisabledColor)
                    { log("uidtbar: occupied lot " + h + " not red under mode 3"); return false; }

                // (f) the plaque gate + anchor: a lot with the category bit
                // carries the plaque member; the anchor law is the static pin.
                int demo = communityLots[0];
                lots[demo].ULFilterCategoryBits = 0x08;      // the probe-only bits source (DISCLOSED)
                sw.ProcessULFilterByType(0x08);
                if (lots[demo].ULPlaqueMember != ULPlaqueMembers[2])
                { log("uidtbar: plaque not mounted on matching lot"); return false; }
                lots[demo].ULFilterCategoryBits = 0;
                sw.ProcessULFilterByType(0x08);
                if (!string.IsNullOrEmpty(lots[demo].ULPlaqueMember))
                { log("uidtbar: plaque mounted on non-matching lot"); return false; }
                var plaqueTex = Simitone.Client.UI.Model.UIOriginal.EnsureResolved(ULPlaqueMembers[2])
                    ?.Get(FSO.Client.GameFacade.GraphicsDevice);
                if (plaqueTex == null || plaqueTex.Width != ULPlaqueWidth || plaqueTex.Height != ULPlaqueHeight)
                { log("uidtbar: plaque art missing or wrong dims"); return false; }

                return true;
            }
            catch (Exception e)
            {
                log("uidtbar EXC " + e.GetType().Name + " " + e.Message);
                return false;
            }
        }

        private static string ButtonNameKeyword(int i)
        {
            return new[] { "dog", "small pet", "shopping", "gardening", "food", "recreational", "lodging" }[i];
        }
    }
}
