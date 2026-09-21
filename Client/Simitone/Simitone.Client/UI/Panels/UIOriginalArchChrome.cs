using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simitone.Client.UI.Model;
using Simitone.Client.UI.Screens;
using System;
using System.Collections.Generic;

namespace Simitone.Client.UI.Panels
{
    /// <summary>
    /// R145: the cWinArch BUILD-mode toolbar chrome (engine law
    /// tools/iff-dump/r145/build-toolbar-law.md). MainPanel-local = arch-local
    /// (the arch window is cpanel (220,50,W,150); MainPanel sits at
    /// (220,SH-100), so local y here == arch band y 0..100).
    ///
    /// Composition (all DECODED, correcting r144 §4):
    /// - 12 tool buttons from the STATIC anchor table BSS 0x92a90 — two
    ///   hand-tuned rows (y 3..8 / 55..64, x 58..282); they never flow.
    /// - undo (11,21) / redo (11,53), 30x24 cells of the 120x24 sheets.
    /// - the (338,5) pitch-45 flow is the SUBTOOL PATTERN ROW (the browse
    ///   panel's desktop band grid), paged by arrows at (326,26)/(W-14,26).
    /// - Roof tool active: pattern row + arrows hidden, cWinRoofPanel shown
    ///   at (314,5,W,100): pitch icons steep/medium/shallow/flat at (6,1)/
    ///   (6,30)/(6,50)/(6,71), pattern grid (70,1) at 45px pitch in two
    ///   rows (8x2 @1024, 4x2 @800), panel page arrows (58,20)/(433,20)
    ///   @1024 or (58,20)/(253,20) @800.
    /// - toothpick strips (2x89) at x 49 / 319, y 6.
    /// Tooltips = STR# 139 'BldTips' [0..11] tools, [12..15] undo/redo/pages.
    /// </summary>
    public class UIOriginalArchChrome : UIContainer
    {
        public TS1GameScreen Game;
        public UIOriginalSheetButton[] Tools = new UIOriginalSheetButton[12];
        public UIOriginalSheetButton UndoBtn, RedoBtn;
        public UIOriginalRoofPanel RoofPanel;
        public int SelectedTool = -1;
        public Action<int> OnToolSelect;

        private Texture2D Toothpick1, Toothpick2;

        /// engine tool order (DATA 0x55290) -> art member (Res_CPanel.RT).
        public static readonly string[] ToolArt =
        {
            "cpanel\\Buttons\\toolBtnTerrain.bmp",   // 66 Terrain
            "cpanel\\Buttons\\toolBtnWater.bmp",     // 68 Pool
            "cpanel\\Buttons\\toolBtnWall.bmp",      // 70 Wall
            "cpanel\\Buttons\\toolBtnWallpaper.bmp", // 73 Wallpaper
            "cpanel\\Buttons\\toolBtnStairs.bmp",    // 74 Stairs
            "cpanel\\Buttons\\toolBtnFireplace.bmp", // 75 Fireplace
            "cpanel\\Buttons\\toolBtnTree.bmp",      // 67 Tree
            "cpanel\\Buttons\\toolBtnFloor.bmp",     // 69 Floor
            "cpanel\\Buttons\\toolBtnDoor.bmp",      // 71 Door
            "cpanel\\Buttons\\toolBtnWindow.bmp",    // 72 Window
            "cpanel\\Buttons\\toolBtnRoof.bmp",      // 76 Roof
            "cpanel\\Buttons\\toolBtnHand.bmp",      // 77 Hand
        };

        /// the 12 static anchors (BSS 0x92a90, net arch-local).
        public static readonly int[] ToolAnchorX = { 58, 105, 144, 188, 230, 282, 59, 97, 147, 189, 229, 282 };
        public static readonly int[] ToolAnchorY = { 4, 6, 4, 3, 4, 8, 57, 64, 55, 55, 55, 55 };

        /// engine tool index -> the port's UIBuyBrowsePanel.BuildCategories
        /// (group, subcategory) whose InitSubcategory re-inits the catalog to
        /// that tool's own category (the group distinction ends at the tool).
        public static readonly int[,] ToolSubcat =
        {
            { 1, 1 }, // Terrain  (outdoors, terrain)
            { 1, 2 }, // Pool     (outdoors, water)
            { 0, 0 }, // Wall     (architecture, wall)
            { 0, 1 }, // Wallpaper
            { 2, 2 }, // Stairs   (objects, staircase)
            { 2, 3 }, // Fireplace
            { 1, 0 }, // Tree     (outdoors, trees)
            { 0, 2 }, // Floor    (architecture, floor)
            { 2, 0 }, // Door
            { 2, 1 }, // Window
            { -1, -1 },// Roof    -> the engine roof panel (SetRoofMode)
            { -1, -1 },// Hand    -> selection-only (no catalog)
        };

        public UIOriginalArchChrome(TS1GameScreen game)
        {
            Game = game;

            // undo (11,21) / redo (11,53) — 30x24 cells. UI-22 tranche 1:
            // wired to the VM architecture undo stack (native ArchUndo/
            // ArchRedo @0x20ee60/0x20edc0 law); enable state follows the
            // stack via RefreshUndoRedo (the native UpdateViewFromCPState
            // dirty&4 refresh law, r145 §4).
            UndoBtn = new UIOriginalSheetButton("cpanel\\Buttons\\Undo.bmp")
            {
                Position = new Vector2(11, 21),
                Tooltip = GameFacade.Strings.GetString("139", "12"),
            };
            UndoBtn.OnButtonClick += (b) =>
                Game.vm?.SendCommand(new FSO.SimAntics.NetPlay.Model.Commands.VMNetArchUndoCmd { Redo = false });
            Add(UndoBtn);
            RedoBtn = new UIOriginalSheetButton("cpanel\\Buttons\\Redo.bmp")
            {
                Position = new Vector2(11, 53),
                Tooltip = GameFacade.Strings.GetString("139", "13"),
            };
            RedoBtn.OnButtonClick += (b) =>
                Game.vm?.SendCommand(new FSO.SimAntics.NetPlay.Model.Commands.VMNetArchUndoCmd { Redo = true });
            Add(RedoBtn);

            for (int i = 0; i < 12; i++)
            {
                var t = new UIOriginalSheetButton(ToolArt[i])
                {
                    Position = new Vector2(ToolAnchorX[i], ToolAnchorY[i]),
                    Tooltip = GameFacade.Strings.GetString("139", i.ToString()),
                };
                var idx = i;
                t.OnButtonClick += (b) => SelectTool(idx);
                Add(t);
                Tools[i] = t;
            }

            RoofPanel = new UIOriginalRoofPanel(game) { Position = new Vector2(314, 5), Visible = false };
            Add(RoofPanel);

            Toothpick1 = UIOriginal.EnsureResolved("cpanel\\Backgrounds\\BuildToothpkLeft.bmp")?.Get(GameFacade.GraphicsDevice);
            Toothpick2 = UIOriginal.EnsureResolved("cpanel\\Backgrounds\\BuildToothpkRight.bmp")?.Get(GameFacade.GraphicsDevice);
        }

        public void SelectTool(int i)
        {
            SelectedTool = i;
            for (int k = 0; k < 12; k++) Tools[k].State = (byte)((k == i) ? 1 : 0);
            OnToolSelect?.Invoke(i);
            // native refreshes the undo/redo enable state on tool clicks too
            RefreshUndoRedo();
        }

        /// <summary>
        /// UI-22: the undo/redo buttons enable exactly with the architecture
        /// stack (native ArchCanUndo/ArchCanRedo + UpdateViewFromCPState
        /// dirty&4 refresh law, r145 §4 / r256 §4). Called on tool select and
        /// every UIMainPanel build-mode frame, so a completed VM undo/redo
        /// command reflects on the buttons the next frame.
        /// </summary>
        public void RefreshUndoRedo()
        {
            var stack = Game.vm?.Context?.Architecture?.UndoStack;
            if (UndoBtn != null) UndoBtn.Disabled = !(stack?.CanUndo ?? false);
            if (RedoBtn != null) RedoBtn.Disabled = !(stack?.CanRedo ?? false);
        }

        /// engine SetRoofMode @0x25f870: roof tool swaps the pattern row for
        /// the roof panel (the pattern row itself lives in the browse panel).
        public void SetRoofMode(bool on)
        {
            RoofPanel.Visible = on;
            if (on) RoofPanel.RefreshSelection();
        }

        public override void Draw(UISpriteBatch batch)
        {
            // As with the people trend arrows, these manual blits occur after
            // UIContainer.Draw. Respect the mode child's visibility before
            // painting the build-only toothpick seams.
            if (!Visible) return;
            base.Draw(batch);
            // toothpicks (BuildToothpkLeft/Right 2x89) at x 49 / 319, y 6 —
            // the band's vertical seams (cWinArch::TSPaint 0x261ae4-0x261c1c).
            if (Toothpick1 != null) DrawLocalTexture(batch, Toothpick1, null, new Vector2(49, 6), Vector2.One);
            if (Toothpick2 != null) DrawLocalTexture(batch, Toothpick2, null, new Vector2(319, 6), Vector2.One);
        }
    }

    /// <summary>
    /// R145: the cWinRoofPanel (ctor 0x29e770, law section 7) — the roof
    /// tool's dedicated panel at arch-local (314,5,W,100): the four pitch
    /// icons (kBldSbTlRoof* 4-state sheets, cells 25..26 wide) down the left
    /// edge at engine anchors, the pattern swatch grid from (70,1) at 45px
    /// pitch in two rows (16/page at 1024-wide, 8/page at 800-wide), and the
    /// panel's own page arrows. Pitch
    /// click sends VMNetSetRoofCmd with the R130 engine-mapped pitch; pattern
    /// click sets the style (UIRoofer semantics).
    /// </summary>
    public class UIOriginalRoofPanel : UIContainer
    {
        public TS1GameScreen Game;
        public UIOriginalSheetButton[] PitchBtns = new UIOriginalSheetButton[4];
        public UIOriginalSheetButton PagePrev, PageNext;
        public List<UIOriginalCatalogCell> Patterns = new List<UIOriginalCatalogCell>();
        public int Page;

        public const int PatternOriginX = 70;
        public const int PatternOriginY = 1;
        public const int PatternPitch = 45;

        /// BuildButtons @0x29dfec: the shipped game has one exact 800-wide
        /// branch; every other supported desktop width takes the 16-item law.
        public static int PatternsPerPageForScreenWidth(int screenWidth)
        {
            return screenWidth == 800 ? 8 : 16;
        }

        /// STR#147 positions (256;20)/(460;20), followed by the branch-local
        /// x corrections -3/-27 at 0x29e04c/0x29e09c.
        public static int NextPageXForScreenWidth(int screenWidth)
        {
            return screenWidth == 800 ? 253 : 433;
        }

        /// SetCurPage @0x29c890-0x29ca74 bounds the flow to half a page wide
        /// and two button-heights tall: 4x2 at 800, 8x2 otherwise.
        public static Vector2 PatternPositionForSlot(int slot, int screenWidth)
        {
            int columns = PatternsPerPageForScreenWidth(screenWidth) / 2;
            return new Vector2(
                PatternOriginX + (slot % columns) * PatternPitch,
                PatternOriginY + (slot / columns) * PatternPitch);
        }

        private int CurrentScreenWidth
        {
            get { return GameFacade.Screens.CurrentUIScreen.ScreenWidth; }
        }

        private LiveSubpanels.Catalog.UIOriginalRoofResProvider Res =
            new LiveSubpanels.Catalog.UIOriginalRoofResProvider();

        public UIOriginalRoofPanel(TS1GameScreen game)
        {
            Game = game;
            // pitch column: steep (6,1) 3902, medium (6,30) 3901, shallow
            // (6,50) 3900, flat (6,71) 3904 — STR# 147 directives (10;1) etc.
            // with MoveTo(x-4, y).
            var defs = new[]
            {
                new { Art = "cpanel\\Build\\roofsteep.bmp",   Y = 1,  Rt = 3902, Str = "5" },
                new { Art = "cpanel\\Build\\roofmedium.bmp",  Y = 30, Rt = 3901, Str = "3" },
                new { Art = "cpanel\\Build\\roofshallow.bmp", Y = 50, Rt = 3900, Str = "1" },
                new { Art = "cpanel\\Build\\roofflat.bmp",    Y = 71, Rt = 3904, Str = "12" },
            };
            for (int i = 0; i < 4; i++)
            {
                var d = defs[i];
                var b = new UIOriginalSheetButton(d.Art)
                {
                    Position = new Vector2(6, d.Y),
                    Tooltip = GameFacade.Strings.GetString("147", d.Str),
                };
                var rt = d.Rt;
                b.OnButtonClick += (btn) => SetPitch(rt);
                Add(b);
                PitchBtns[i] = b;
            }

            PagePrev = new UIOriginalSheetButton("cpanel\\Buttons\\ScrollLeft.bmp")
            {
                Position = new Vector2(58, 20),
                Tooltip = GameFacade.Strings.GetString("147", "13"),
            };
            PagePrev.OnButtonClick += (b) => { SetPage(Page - 1); };
            Add(PagePrev);
            PageNext = new UIOriginalSheetButton("cpanel\\Buttons\\ScrollRight.bmp")
            {
                Position = new Vector2(NextPageXForScreenWidth(CurrentScreenWidth), 20),
                Tooltip = GameFacade.Strings.GetString("147", "14"),
            };
            PageNext.OnButtonClick += (b) => { SetPage(Page + 1); };
            Add(PageNext);

            BuildPatterns();
        }

        private void BuildPatterns()
        {
            foreach (var p in Patterns) Remove(p);
            Patterns.Clear();
            var roofs = FSO.Content.Content.Get().WorldRoofs;
            int total = roofs.Count;
            for (int i = 0; i < total; i++)
            {
                var elem = new LiveSubpanels.UICatalogElement
                {
                    Item = new FSO.Content.Interfaces.ObjectCatalogItem { Name = roofs.IDToName(i), Category = 17, Price = 0 },
                    Special = new LiveSubpanels.UISpecialCatalogElement
                    {
                        Control = typeof(FSO.Client.UI.Panels.LotControls.UIRoofer),
                        ResID = (uint)i,
                        Res = Res,
                        Parameters = new List<int> { i },
                    },
                };
                var cell = new UIOriginalCatalogCell(elem) { RoofSwatch = true };
                var id = i;
                cell.OnButtonClick += (b) =>
                {
                    Game.vm?.SendCommand(new FSO.SimAntics.NetPlay.Model.Commands.VMNetSetRoofCmd()
                    {
                        Pitch = Game.vm.Context.Architecture.RoofPitch,
                        Style = (uint)id,
                    });
                    SetPatternSelection(id);
                };
                Add(cell);
                Patterns.Add(cell);
            }
            SetPage(0);
        }

        public void SetPage(int page)
        {
            int screenWidth = CurrentScreenWidth;
            int perPage = PatternsPerPageForScreenWidth(screenWidth);
            int maxPage = Math.Max(0, (Patterns.Count - 1) / perPage);
            Page = Math.Max(0, Math.Min(maxPage, page));
            for (int i = 0; i < Patterns.Count; i++)
            {
                var slot = i - Page * perPage;
                var on = slot >= 0 && slot < perPage;
                Patterns[i].Visible = on;
                if (on) Patterns[i].Position = PatternPositionForSlot(slot, screenWidth);
            }
            PagePrev.Visible = Page != 0;
            PageNext.Visible = (Page + 1) * perPage < Patterns.Count;
        }

        public override void GameResized()
        {
            base.GameResized();
            PageNext.X = NextPageXForScreenWidth(CurrentScreenWidth);
            SetPage(Page);
        }

        private void SetPitch(int rtId)
        {
            Game.vm?.SendCommand(new FSO.SimAntics.NetPlay.Model.Commands.VMNetSetRoofCmd()
            {
                Pitch = LiveSubpanels.Catalog.UIOriginalRoofPitchResProvider.PitchForRtId(rtId),
                Style = Game.vm.Context.Architecture.RoofStyle,
            });
            RefreshSelection();
        }

        /// engine UpdateViewFromCPState @0x29c420: match the current pitch to
        /// the four buttons (|p-0.4|->shallow, |p-0.6|->medium, p<0.1->flat,
        /// else steep — port pitches 0.495/0.66/0.99/0).
        public void RefreshSelection()
        {
            var arch = Game.vm?.Context?.Architecture;
            if (arch == null) return;
            float p = arch.RoofPitch;
            int sel;
            if (p < 0.1f) sel = 3;                 // flat
            else if (Math.Abs(p - 0.495f) < 0.05f) sel = 2;  // shallow
            else if (Math.Abs(p - 0.66f) < 0.06f) sel = 1;   // medium
            else sel = 0;                          // steep
            for (int i = 0; i < 4; i++) PitchBtns[i].State = (byte)((i == sel) ? 1 : 0);
            // Native compares GetRoofPattern() against each stored filename.
            // WorldRoofs is the port's stable name<->id table, so RoofStyle is
            // the equivalent index into this same ordered pattern list.
            SetPatternSelection((int)arch.RoofStyle);
        }

        private void SetPatternSelection(int style)
        {
            for (int i = 0; i < Patterns.Count; i++) Patterns[i].Selected = i == style;
        }
    }
}
