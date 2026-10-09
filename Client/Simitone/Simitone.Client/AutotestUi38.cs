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

                // (a2) UI-38b probe setup (DISCLOSED, the trv06 restock idiom):
                // reset the persisted filter to the 0x10 DEFAULT (a probe
                // choice — the SHIPPED slot value is '4', a Gardening save,
                // which would legitimately override the entry default and
                // invalidate the mode-0 entry assertions below) and re-drive
                // the default through the real law path. The restore law
                // itself is asserted by the (g)/(h) legs and the restore log
                // on later entries.
                var nbhdR = FSO.Content.Content.Get().Neighborhood;
                Simitone.Client.UI.Panels.ULFilterLaws.SavePersistedFilterCmd(
                    System.IO.Path.Combine(nbhdR.UserPath, "LotLocations.iff"), 0x10);
                sw.ProcessULFilterByType(0x10);

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
                // 4934 mounted, engine mode 0, community lots BLUE. UI-38b LAW 2
                // second block: the PERSISTED filter (STR#6 slot 3) then
                // overrides the default — expect 0x10 exactly when nothing
                // valid is persisted (the shipped files carry '16' = default).
                var nbhd0 = FSO.Content.Content.Get().Neighborhood;
                // fresh-file read (the disk truth; the cached provider instance
                // only equals it at session load — the probe mutates mid-run)
                var persisted0 = Simitone.Client.UI.Panels.ULFilterLaws.LoadPersistedFilterCmd(
                    new FSO.Files.Formats.IFF.IffFile(System.IO.Path.Combine(nbhd0.UserPath, "LotLocations.iff")));
                var expectedCmd = persisted0 > 0 ? persisted0 : ULDefaultFilterCmd;
                var expectedIdx = System.Array.IndexOf(ULCmdBits, expectedCmd);
                if (expectedIdx < 0)
                { log("uidtbar: persisted cmd 0x" + expectedCmd.ToString("x") + " is not a filter bit"); return false; }
                if (sw.ULFilterCmdId != expectedCmd || sw.ULFilterEngineMode != ULModes[expectedIdx]
                    || sw.ULFilterPlaqueMember != ULPlaqueMembers[expectedIdx])
                { log("uidtbar: default filter cmd=" + sw.ULFilterCmdId + " mode=" + sw.ULFilterEngineMode + " plaque=" + sw.ULFilterPlaqueMember + " (expected persisted-or-default 0x" + expectedCmd.ToString("x") + ")"); return false; }
                if (!sw.ULFilterButtons[expectedIdx].Selected || sw.ULFilterButtons.Where((x, i) => i != expectedIdx).Any(x => x.Selected))
                { log("uidtbar: default selection state wrong (expected pressed idx=" + expectedIdx + ")"); return false; }
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

                // (f0) UI-38b review P1: SNAPSHOT the production census NOW —
                // before the (f) demo leg seeds and resets communityLots[0]'s
                // bits (the original census leg read the clobbered 0 and
                // pinned it: 58 is really 0x46).
                var censusSnapshot = new System.Collections.Generic.Dictionary<int, int>();
                foreach (var kv in lots)
                    if (communityLots.Contains(kv.Key))
                        censusSnapshot[kv.Key] = kv.Value.ULFilterCategoryBits;

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

                // ==== 3. UI-38b production legs (the two unbanked laws) ====
                // (g) LAW 1 — the production category bits: the census computed
                // from the lots' own house files. The shipped n=1 ground truth:
                // House54 -> 0x04 (Gardening: seeds + garden controller), House58
                // -> 0x40 (Recreation: basketball), House72 -> 0x04; every other
                // community lot 0. Assert the LIVE bits field (the census ran at
                // button build — no probe seeding).
                var censusDump = new System.Text.StringBuilder();
                foreach (var kv in censusSnapshot)
                    censusDump.Append(kv.Key).Append("=0x").Append(kv.Value.ToString("x")).Append(' ');
                log("uidtbar: production census lots: " + censusDump.ToString());
                // the pinned ground truth — the LIVE census of the shipped n=1
                // community-mode lots, snapshotted BEFORE any probe seeding
                // (review P1): 58 = the basketball/cafe/garden lot (0x46),
                // 70 = the big multi-venue lot (0x7c), 72 = the garden store:
                var expectBits = new System.Collections.Generic.Dictionary<int, int>
                { { 58, 0x46 }, { 61, 0x6e }, { 70, 0x7c }, { 71, 0x2e }, { 72, 0x04 }, { 73, 0x46 }, { 74, 0x26 }, { 75, 0x2e } };
                foreach (var kv in censusSnapshot)
                {
                    int want;
                    if (!expectBits.TryGetValue(kv.Key, out want)) want = 0;
                    if (kv.Value != want)
                    { log("uidtbar: census lot " + kv.Key + " = 0x" + kv.Value.ToString("x") + " expected 0x" + want.ToString("x")); return false; }
                }
                log("uidtbar: PRODUCTION CENSUS VERIFIED (58=0x46 61=0x6e 70=0x7c 71=0x2e 72=0x04 73=0x46 74=0x26 75=0x2e)");

                // and the production PLAQUE follows the census (Gardening on a
                // garden lot needs no probe bits):
                sw.ProcessULFilterByType(0x04);
                var gardenLot = communityLots.First(h => lots[h].ULFilterCategoryBits == 0x04);
                if (lots[gardenLot].ULPlaqueMember != ULPlaqueMembers[3])
                { log("uidtbar: production plaque not mounted on the garden lot"); return false; }

                // (h) LAW 2 — persistence (LoadCurrentFilter STR#6 slot 3):
                // the click above (0x04) must have written the file; read it
                // back through a FRESH IffFile (disk round-trip, not the cached
                // instance) and assert the slot.
                var nbhd = FSO.Content.Content.Get().Neighborhood;
                var llPath = System.IO.Path.Combine(nbhd.UserPath, "LotLocations.iff");
                var persisted = Simitone.Client.UI.Panels.ULFilterLaws.LoadPersistedFilterCmd(
                    new FSO.Files.Formats.IFF.IffFile(llPath));
                if (persisted != 0x04)
                { log("uidtbar: persisted filter wrong after click (got " + persisted + ")"); return false; }
                // and the loader API reads the same file the native would:
                if (Simitone.Client.UI.Panels.ULFilterLaws.LoadPersistedFilterCmd(nbhd.LotLocations) != 0x04)
                { log("uidtbar: cached-provider load mismatch"); return false; }
                log("uidtbar: FILTER PERSISTENCE VERIFIED (STR#6 slot 3 round-trip = 0x04)");

                // (i) the plaque PING-PONG law (UI-38 residual, 2026-10-09):
                // TSPaint increments +0x1dc mod 6 in the wave advance gate;
                // DrawFilterAt maps 0->0, 1|4->1, 2|3->2 — 3-frame ping-pong.
                var truth = new[] { 0, 1, 2, 2, 1, 0 };
                for (var s = 0; s < 6; s++)
                {
                    var want0 = truth[s];
                    var got0 = Simitone.Client.UI.Panels.UINeighborhoodSelectionPanel.ULPlaqueFrameForStep(s);
                    if (got0 != want0)
                    { log("uidtbar: plaque truth step " + s + " got " + got0 + " want " + want0); return false; }
                }
                var seq = new System.Text.StringBuilder();
                var interval = Simitone.Client.UI.Panels.UINeighborhoodAnimationLayer.OldTownCounterIntervalMilliseconds;
                for (var i = 0; i < 13; i++)
                {
                    panel.AdvanceULPlaque(interval);
                    seq.Append(panel.ULPlaqueFrame);
                }
                var seqStr = seq.ToString();
                var wantSeq = "1221001221001"; // 13 advances from c=0: c mod 6 = 1,2,3,4,5,0,... -> frames 1,2,2,1,0,0,...
                if (seqStr != wantSeq)
                { log("uidtbar: plaque counter sequence " + seqStr + " want " + wantSeq); return false; }
                log("uidtbar: PLAQUE PING-PONG VERIFIED (truth table + counter sequence " + seqStr + ")");

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
