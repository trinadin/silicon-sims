using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Client.UI.Model;
using FSO.Common;
using FSO.Common.Rendering.Framework.Model;
using FSO.Common.Utils;
using FSO.Content;
using FSO.LotView.RC;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Simitone.Client.UI.Controls;
using Simitone.Client.UI.Model;
using Simitone.Client.UI.Screens;
using System;
using System.Collections.Generic;

namespace Simitone.Client.UI.Panels.Desktop
{
    /// <summary>
    /// R141: the desktop control panel rebuilt on the ORIGINAL engine art. Canon sources:
    /// UIGraphics.far `Res_CPanel.RT` maps every gadget id to its bitmap — kUniversalBkgTGA
    /// 2037 = CPanel/Backgrounds/UniversalBack.tga (220x183, the quarter-round dial plate),
    /// the mode buttons are kPeople 2010 (people.bmp 200x50 = 4x 50x50), kObjects 2008
    /// (objects.bmp 180x47 = 4x 45x45), kArch 2003 (arch.bmp 156x39 = 4x 39x39) and
    /// kOptions 2009 (options.bmp 92x23 = 4x 23x23), camera kZoomIn/kZoomOut/kRotLeft/
    /// kRotRight 2013/2014/2012/2011 (108x27 = 4x 27x27), walls kNoCut 2006 / kDynaCut 2004 /
    /// kNoWall 2007 / kLevelRoof 2025, floors kLevel2 2024 / kLevel1 2023, and the per-mode
    /// patches kLiveModePatch 4803 / kBuyModePatch 4804 / kBuildModePatch 4805 /
    /// kOptionsModePatch 4806. The exact gadget anchors come from the hard-coded table
    /// writer at 0x2b8230 (r142/tsui-template-law.md); the exact patch positions come
    /// from cWinViewControl::BlitPrivateBufferToParent at 0x2b5460 (r144/toolbar-law.md):
    /// each patch is right/bottom-aligned in the 220x183 plate.
    ///
    /// Removed vs the Simitone-era UCP (all port-authored, zero corpus hits): the
    /// d_live_bg.png chrome plate, the d_live_* stencil art, the fabricated floor ordinal,
    /// the friend-count icon+label, and the eyedropper button (the E-key tool itself stays,
    /// invisible). Pause/speed and money/time now use their original sheets and decoded
    /// plate anchors; Lev1/Lev2 themselves display the active story.
    /// </summary>
    public class UIDesktopUCP : UICachedContainer
    {
        public UIImage Background;
        public UIImage LivePatch, BuyPatch, BuildPatch, OptionsPatch, CameraPatch;

        public UIButton LiveButton;
        public UIButton BuyButton;
        public UIButton BuildButton;
        public UIButton OptionsButton;
        public UIButton CameraButton;

        // TS1 has no ordinal floor readout on the desktop plate. Lev1/Lev2 are
        // direct story selectors whose selected state is the readout.
        public UILabel FloorLabel;
        public UILabel FloorLabelShadow;
        public Simitone.Client.UI.Controls.UIOriginalText FloorOriginal;

        public UIButton RoofButton;
        public UIButton WallsUpButton;
        public UIButton WallsCutButton;
        public UIButton WallsDownButton;

        public UIButton FloorUpButton;
        public UIButton FloorDownButton;

        // R142 ENGINE LAW: kPause (85,135) 15x15 + kSpeed1/2/3 (105/120/141,135) live
        // ON THE PLATE (layout-init 0x2b8230 anchors). Keep the complete original
        // state sheets: pause.bmp is 4x2; speed/level/wall/camera sheets are 4x1.
        public Simitone.Client.UI.Panels.UIOriginalNavbarButton PauseButton;
        public Simitone.Client.UI.Panels.UIOriginalNavbarButton HelpButton;   // R162: kHelpBtn 4993 at (5,164)
        public Simitone.Client.UI.Panels.UIOriginalNavbarButton[] SpeedButtons;

        public UIButton ZoomInButton;
        public UIButton ZoomOutButton;
        public UIButton RotateCWButton;
        public UIButton RotateCCWButton;

        // R218: the view pie (the engine's cDDDSimsView+0x13c cTSPieMenu with
        // the four ViewMenu sheet items) — torn off a small zoom/rotate button
        // by dragging while it is held (UIOriginalViewPie has the law).
        public Simitone.Client.UI.Panels.UIOriginalViewPie ViewPie;
        private UIButton _pieArmButton;
        private Vector2 _pieArmPos;

        // R144: plate readouts (engine section 2 law)
        public Simitone.Client.UI.Controls.UIOriginalText MoneyOriginal;
        public Simitone.Client.UI.Controls.UIOriginalText ClockDigits;
        // R205 ENGINE LAW: the FAMILY FRIEND COUNT readout —
        // cWinViewControl::PostChildDraw 0x2b57c0 compare-then-sets the +0x1a8
        // gadget caption from CPState::GetFamilyFriendCount 0x20c770 (dedup
        // friend set cached at CPState+0x224); gadget rect =
        // (moneyLeft-w, moneyBottom, moneyLeft, moneyBottom+h) — directly
        // BELOW the money readout, right-aligned to its left edge (creation
        // block 0x2b7520-0x2b75a8, the block r144 misattributed to the
        // clock; the clock digits live at TSPaint 0x2b5978 + the (95,118)
        // dial). Tooltip = STR# 138[20] 'Family Friend Count' (the one r117
        // swap never mounted). Click = cWinViewControl::TSOnCommand 0x2b5114:
        // modal cWinPictureDialog built from STR# 162 (0xa2 literal in the
        // disasm) — title [0], message [1], ONE button [2] 'OK' — the R161
        // budget-dialog pattern.
        public Simitone.Client.UI.Controls.UIOriginalText FriendOriginal;
        public static int FriendReadoutUpdates = 0;   // compare-then-set law counter
        public static int FriendDialogsOpened = 0;

        private TS1GameScreen Game;

        public Func<UIMainPanelMode, bool> OnModeClick;

        public UIDesktopUCP(TS1GameScreen screen)
        {
            Game = screen;
            var ui = Content.Get().CustomUI;
            var gd = GameFacade.GraphicsDevice;

            // The original plate. UniversalBack is a 32-bit TGA far member; the FAR
            // texture resolver covers .bmp members only, so it mounts through the
            // byte-faithful png twin (generated from the far bytes, evidence r141).
            Background = new UIImage(Simitone.Client.UI.Model.UIOriginal.ResolveOrPng(
                "cpanel\\Backgrounds\\UniversalBack.TGA", "orig_ucp_back.png", ui.Get("d_live_bg.png").Get(gd)));
            Add(Background);

            // Per-mode patches (kLiveModePatch..kOptionsModePatch; BITMAP far members).
            LivePatch = PatchImage("cpanel\\Backgrounds\\LivePatch.bmp");
            BuyPatch = PatchImage("cpanel\\Backgrounds\\BuyPatch.bmp");
            BuildPatch = PatchImage("cpanel\\Backgrounds\\BuildPatch.bmp");
            OptionsPatch = PatchImage("cpanel\\Backgrounds\\OptionsPatch.bmp");
            CameraPatch = PatchImage("cpanel\\Backgrounds\\CameraPatch.bmp");

            // R117/R141: every tooltip reads the ORIGINAL UIText.iff 138 'VCtlTips'.
            // Mode art per Res_CPanel.RT: Live = people.bmp (NOT House.bmp — that is
            // the kHouseBtn HOUSE TAB; R141 corrects the R86-era misuse).
            // R142 ENGINE-EXACT ANCHORS: the R141 "TSUI template" premise is BUSTED —
            // bl 0x591440 is MSL operator new (100/380/480 were sizeof cTSBuffer/
            // cWinArch/cWinViewControl); the REAL law is the hardcoded {x,y} anchor
            // table written by layout-init @ 0x2b8230 through cTSWinBtn SetImage/
            // SetArea (r142/tsui-template-law.md). Mode cascade top-left -> bottom-right;
            // camera diamond centered ~(35,127); wall row on the plate's left half at
            // y 77..110 (NOT the bottom recess R141 guessed); pause+speeds at y=135.
            Add(LiveButton = ModeButton("cpanel\\Buttons\\people.bmp", new Vector2(10, 6), GameFacade.Strings.GetString("138", "10")));
            Add(BuyButton = ModeButton("cpanel\\Buttons\\objects.bmp", new Vector2(69, 20), GameFacade.Strings.GetString("138", "11")));
            Add(BuildButton = ModeButton("cpanel\\Buttons\\arch.bmp", new Vector2(117, 45), GameFacade.Strings.GetString("138", "12")));
            Add(OptionsButton = ModeButton("cpanel\\Buttons\\options.bmp", new Vector2(193, 125), GameFacade.Strings.GetString("138", "13")));
            // cWinViewControl::Init 0x2b6d3c: kCameraBtn 2034 is a 4x1,
            // 29x29-cell sheet at (175,95). It occupies the cyan bubble in
            // UniversalBack directly above Options; Options itself remains at
            // its engine anchor (193,125).
            Add(CameraButton = ModeButton("cpanel\\Buttons\\camera.bmp", new Vector2(175, 95), GameFacade.Strings.GetString("138", "19")));

            // camera diamond (27x27 frames of the 108x27 strips)
            Add(ZoomInButton = OriginalButton("cpanel\\Buttons\\zoomin.bmp", 4, 1, new Vector2(26, 105), GameFacade.Strings.GetString("138", "5")));
            Add(ZoomOutButton = OriginalButton("cpanel\\Buttons\\zoomout.bmp", 4, 1, new Vector2(26, 149), GameFacade.Strings.GetString("138", "6")));
            Add(RotateCCWButton = OriginalButton("cpanel\\Buttons\\rotleft.bmp", 4, 1, new Vector2(9, 127), GameFacade.Strings.GetString("138", "3")));
            Add(RotateCWButton = OriginalButton("cpanel\\Buttons\\rotright.bmp", 4, 1, new Vector2(43, 127), GameFacade.Strings.GetString("138", "4")));

            // R217: the R193 ViewMenu label mounts are RETIRED. The decode is
            // corrected: the four sheets are cTSPieMenu ITEMS (cDDDSimsView::Init
            // 0x218ee8-0x218f98 = cTSPieMenu::InsertString(tag, gadget) into the
            // pie at view+0x13c), rendered only inside the pie POPUP with its
            // ViewMenuBackground chip (member 825) — never as panel plaques on
            // button-hold. The panel buttons' own law is an ANIMATED step
            // (cWinViewControl::TSOnCommand zoom/rotate branches ->
            // DoSpeedTransitionSound 0x2b2f90 + the speed curve), no flyout.
            // The pie subsystem is a disclosed residual (r217 law doc).

            // walls row in the plate's bottom strip (measured dark recess 45..195 x 125..182)
            Add(RoofButton = OriginalButton("cpanel\\Buttons\\LevRoof.bmp", 4, 1, new Vector2(32, 78), GameFacade.Strings.GetString("138", "2")));
            Add(WallsUpButton = OriginalButton("cpanel\\Buttons\\nocut.bmp", 4, 1, new Vector2(56, 80), GameFacade.Strings.GetString("138", "7")));
            Add(WallsCutButton = OriginalButton("cpanel\\Buttons\\dynacut.bmp", 4, 1, new Vector2(80, 85), GameFacade.Strings.GetString("138", "8")));
            Add(WallsDownButton = OriginalButton("cpanel\\Buttons\\nowall.bmp", 4, 1, new Vector2(103, 95), GameFacade.Strings.GetString("138", "9")));

            // level arrows + readout (kLevel1/kLevel2)
            Add(FloorUpButton = OriginalButton("cpanel\\Buttons\\Lev2.bmp", 4, 1, new Vector2(5, 92), GameFacade.Strings.GetString("138", "1")));
            Add(FloorDownButton = OriginalButton("cpanel\\Buttons\\Lev1.bmp", 4, 1, new Vector2(5, 77), GameFacade.Strings.GetString("138", "0")));

            // pause + speed row at y=135 (engine anchors; the toolbar never carries them)
            try
            {
                PauseButton = new Simitone.Client.UI.Panels.UIOriginalNavbarButton(
                    "cpanel\\Buttons\\pause.bmp", 4, 2, GameFacade.Strings.GetString("138", "15"))
                {
                    Position = new Vector2(85, 135)
                };
                PauseButton.OnButtonClick += (b) => Game.Frontend.MainPanel.SwitchSpeed(4);
                Add(PauseButton);
                SpeedButtons = new Simitone.Client.UI.Panels.UIOriginalNavbarButton[3];
                int[] sx = { 105, 120, 141 };
                for (int i = 0; i < 3; i++)
                {
                    int speed = i + 1;
                    SpeedButtons[i] = new Simitone.Client.UI.Panels.UIOriginalNavbarButton(
                        "cpanel\\Buttons\\Speed" + speed + ".bmp", 4, 1,
                        GameFacade.Strings.GetString("138", (16 + i).ToString()))
                    {
                        Position = new Vector2(sx[i], 135)
                    };
                    SpeedButtons[i].OnButtonClick += (b) => Game.Frontend.MainPanel.SwitchSpeed(speed);
                    Add(SpeedButtons[i]);
                }
            }
            catch (Exception pe) { Simitone.Client.GameLog.Write("ucp-ctor: speed cluster EXC " + pe.GetType().Name + " " + pe.Message); }

            // R162: kHelpBtn 4993 (r144 law: 40x15 sheet -> 10x15 states at
            // (5,164)) — the engine's help button, unmounted since the r159
            // census. Click -> cWinHelp modal (SetSelected(GetSelected())
            // persists the page across opens).
            try
            {
                HelpButton = new Simitone.Client.UI.Panels.UIOriginalNavbarButton(
                    "cpanel\\buttons\\helpbutton.bmp", 4, 1, null)
                {
                    Position = new Vector2(5, 164)
                };
                HelpButton.OnButtonClick += (b) =>
                {
                    var helpDlg = new Simitone.Client.UI.Panels.UIOriginalHelpDialog();
                    // AUD-17 B-12: native SetBlockSimulator — the sim pauses
                    // while the help window is open (released in its Removed).
                    Simitone.Client.UI.Model.UIModalSimPause.Pause(helpDlg,
                        (FSO.Client.GameFacade.Screens.CurrentUIScreen as Simitone.Client.UI.Screens.TS1GameScreen)?.vm);
                    FSO.Client.UI.Framework.UIScreen.GlobalShowDialog(helpDlg, true);
                    Simitone.Client.UI.Panels.UIOriginalHelpDialog.HelpButtonOpens++;
                };
                Add(HelpButton);
            }
            catch (Exception he) { Simitone.Client.GameLog.Write("ucp-ctor: helpbtn EXC " + he.GetType().Name + " " + he.Message); }

            // R144: the plate READOUTS (engine law r144/toolbar-law.md section 2 —
            // both are cWinViewControl children, NOT toolbar elements):
            // - money: font[10], sized for worst-case "$9,999,999", right-
            //   aligned to plate anchor (178,158) — X set per measured string.
            // - clock digits: font[8], centered in plate rect (95,118,151,125)
            //   (the round dial in the plate art; box sized for "999", the
            //   right edge grows by measured width).
            try
            {
                var moneyFont = Simitone.Client.UI.Controls.OriginalGlyphFont.LoadByIndex(10, gd);
                var clockFont = Simitone.Client.UI.Controls.OriginalGlyphFont.LoadByIndex(8, gd);
                if (moneyFont != null)
                {
                    MoneyOriginal = new Simitone.Client.UI.Controls.UIOriginalText("§0", moneyFont);
                    Add(MoneyOriginal);
                    SetMoneyText(0);
                    // R161: the funds readout IS the budget button (engine
                    // cWinViewControl::TSOnCommand sender == this+256 @0x2b5094
                    // -> modal cWinBudgetDlg, SetBlockSimulator + DoSimsModalDialog).
                    // Hotspot covers the plate money zone right of the friend
                    // gadget's fixed left edge (the two zones are DISJOINT —
                    // every matching ListenForMouse rect fires, and R215's
                    // zones overlapped: one click opened both dialogs).
                    ListenForMouse(new Rectangle(90, 144, 92, 36), (evt, state) =>
                    {
                        if (evt != FSO.Common.Rendering.Framework.IO.UIMouseEventType.MouseUp) return;
                        var game = FSO.Client.GameFacade.Screens.CurrentUIScreen as Simitone.Client.UI.Screens.TS1GameScreen;
                        if (game == null || game.vm == null) return;
                        FSO.Client.UI.Framework.UIScreen.GlobalShowDialog(
                            new Simitone.Client.UI.Panels.UIOriginalBudgetDialog(game), true);
                        Simitone.Client.UI.Panels.UIOriginalBudgetDialog.BudgetButtonOpens++;
                    });
                }
                if (clockFont != null)
                {
                    ClockDigits = new Simitone.Client.UI.Controls.UIOriginalText("12:00 AM", clockFont);
                    Add(ClockDigits);
                    SetClockText("12:00 AM");
                }
                // R205: the friend-count readout shares the money face
                // (font[10], the r144 0x2b74f8 attribution) and its rect is
                // computed against the money gadget's FIXED worst-case left
                // edge (R216 decode of the creation block 0x2b7520-0x2b75a8:
                // SetArea(moneyLeft−w, moneyTop, moneyLeft, moneyTop+fontH) —
                // the digit paints on the money LINE, right-aligned to the
                // worst-case box; the old '+ line height' drop was the port's
                // misread of R144's moneyBottom prose).
                FriendOriginal = new Simitone.Client.UI.Controls.UIOriginalText("0", moneyFont)
                {
                    Tooltip = GameFacade.Strings.GetString("138", "20")
                };
                Add(FriendOriginal);
                ListenForMouse(new Rectangle(64, 154, 26, 26), (evt, state) =>
                {
                    if (evt != FSO.Common.Rendering.Framework.IO.UIMouseEventType.MouseUp) return;
                    OpenFriendCountDialog();
                });
                UpdateFriendCount();
            }
            catch (Exception me) { Simitone.Client.GameLog.Write("ucp-ctor: readouts EXC " + me.GetType().Name + " " + me.Message); }

            RoofButton.OnButtonClick += (btn) => SetCut(3);
            WallsUpButton.OnButtonClick += (btn) => SetCut(2);
            WallsCutButton.OnButtonClick += (btn) => SetCut(1);
            WallsDownButton.OnButtonClick += (btn) => SetCut(0);

            LiveButton.OnButtonClick += (btn) => OnModeClick?.Invoke(UIMainPanelMode.LIVE);
            BuyButton.OnButtonClick += (btn) => OnModeClick?.Invoke(UIMainPanelMode.BUY);
            BuildButton.OnButtonClick += (btn) => OnModeClick?.Invoke(UIMainPanelMode.BUILD);
            OptionsButton.OnButtonClick += (btn) => OnModeClick?.Invoke(UIMainPanelMode.OPTIONS);
            CameraButton.OnButtonClick += (btn) => OnModeClick?.Invoke(UIMainPanelMode.CAMERA);

            ZoomInButton.OnButtonClick += ZoomControl;
            ZoomOutButton.OnButtonClick += ZoomControl;
            RotateCWButton.OnButtonClick += RotateClockwise;
            RotateCCWButton.OnButtonClick += RotateCounterClockwise;

            // Original cWinViewControl::TSOnCommand: Lev1 and Lev2 directly call
            // SetLevel(1) and SetLevel(2). They are not increment/decrement arrows.
            FloorUpButton.OnButtonClick += (b) => SelectStory(2);
            FloorDownButton.OnButtonClick += (b) => SelectStory(1);

            // R245: the flash-by-image-id registry (TutorialControlMap) — every
            // gadget under its ORIGINAL resource id (r142/rt-inventory.txt rows
            // cited in each provenance string). The cWinLotBtn +50px override
            // (0x2d2ae0) belongs to the NEIGHBORHOOD lot buttons (its own
            // method cluster HiliteForResidential/Community/Import +
            // IsLotOccupied, r141/symbol-index.txt 4256-4262); no UCP gadget
            // is a lot button, so every entry below is lotButton: false.
            TutorialControlMap.Register(LiveButton, 2010, false, "kPeople CPanel/Buttons/People.bmp 200x50 (rt-inv 499)");
            TutorialControlMap.Register(BuyButton, 2008, false, "kObjects CPanel/Buttons/Objects.bmp 180x47 (rt-inv 497)");
            TutorialControlMap.Register(BuildButton, 2003, false, "kArch CPanel/Buttons/Arch.bmp 156x39 (rt-inv 493)");
            TutorialControlMap.Register(OptionsButton, 2009, false, "kOptions CPanel/Buttons/Options.bmp 92x23 (rt-inv 498)");
            TutorialControlMap.Register(CameraButton, 2034, false, "kCameraBtn CPanel/Buttons/camera.bmp 116x29 (rt-inv 508)");
            TutorialControlMap.Register(ZoomInButton, 2013, false, "kZoomIn CPanel/Buttons/ZoomIn.bmp 108x27 (rt-inv 502)");
            TutorialControlMap.Register(ZoomOutButton, 2014, false, "kZoomOut CPanel/Buttons/ZoomOut.bmp 108x27 (rt-inv 503)");
            TutorialControlMap.Register(RotateCWButton, 2011, false, "kRotRight CPanel/Buttons/RotRight.bmp 108x27 (rt-inv 500)");
            TutorialControlMap.Register(RotateCCWButton, 2012, false, "kRotLeft CPanel/Buttons/RotLeft.bmp 108x27 (rt-inv 501)");
            TutorialControlMap.Register(RoofButton, 2025, false, "kLevelRoof CPanel/Buttons/LevRoof.bmp 88x19 (rt-inv 506)");
            TutorialControlMap.Register(WallsUpButton, 2006, false, "kNoCut CPanel/Buttons/NoCut.bmp 84x21 (rt-inv 495)");
            TutorialControlMap.Register(WallsCutButton, 2004, false, "kDynaCut CPanel/Buttons/DynaCut.bmp 84x21 (rt-inv 494)");
            TutorialControlMap.Register(WallsDownButton, 2007, false, "kNoWall CPanel/Buttons/NoWall.bmp 88x15 (rt-inv 496)");
            TutorialControlMap.Register(FloorUpButton, 2024, false, "kLevel2 CPanel/Buttons/Lev2.bmp 88x15 (rt-inv 505)");
            TutorialControlMap.Register(FloorDownButton, 2023, false, "kLevel1 CPanel/Buttons/Lev1.bmp 84x11 (rt-inv 504)");
            if (PauseButton != null)
                TutorialControlMap.Register(PauseButton, 2026, false, "kPause CPanel/Buttons/pause.bmp 60x30 (rt-inv 507)");
            if (SpeedButtons != null)
            {
                int[] speedIds = { 4705, 4706, 4707 };
                for (int i = 0; i < 3 && i < SpeedButtons.Length; i++)
                    TutorialControlMap.Register(SpeedButtons[i], speedIds[i], false,
                        "kSpeed" + (i + 1) + " CPanel/Buttons/Speed" + (i + 1) + ".bmp (rt-inv 594-596)");
            }
            if (HelpButton != null)
                TutorialControlMap.Register(HelpButton, 4993, false, "kHelpBtn CPanel/Buttons/HelpButton.BMP 40x15 (rt-inv 660)");

            Size = new Vector2(Background.Width, Background.Height);

            UpdateBuildBuy();
            UpdateZoomButton();
        }

        private UIImage PatchImage(string member)
        {
            Microsoft.Xna.Framework.Graphics.Texture2D tex = null;
            try { tex = Simitone.Client.UI.Model.UIOriginal.EnsureResolved(member)?.Get(GameFacade.GraphicsDevice); } catch { }
            var img = new UIImage(tex ?? TextureGenerator.GetPxWhite(GameFacade.GraphicsDevice));
            // cWinViewControl::BlitPrivateBufferToParent right/bottom-aligns every
            // mode patch in the 220x183 UniversalBack plate.
            img.Position = new Vector2(Background.Width - img.Width, Background.Height - img.Height);
            img.Visible = false;   // SetMode selects the active patch
            Add(img);
            return img;
        }

        private UIButton ModeButton(string member, Vector2 pos, string tooltip)
        {
            // UIButton self-crops 4-frame sheets (m_Width = Width/ImageStates, default 4)
            // and renders the up/down/hover/disabled frames itself — pass the FULL sheet,
            // exactly like the original engine's 4-state strips.
            var tex = Simitone.Client.UI.Model.UIOriginal.EnsureResolved(member)?.Get(GameFacade.GraphicsDevice);
            var btn = new UIButton(tex ?? Content.Get().CustomUI.Get("btn_live.png").Get(GameFacade.GraphicsDevice));
            btn.Position = pos;
            btn.Tooltip = tooltip;
            return btn;
        }

        private Simitone.Client.UI.Panels.UIOriginalNavbarButton OriginalButton(
            string member, int cols, int rows, Vector2 pos, string tooltip)
        {
            return new Simitone.Client.UI.Panels.UIOriginalNavbarButton(member, cols, rows, tooltip)
            {
                Position = pos
            };
        }

        private bool CanUseSecondStory()
        {
            // R254/UI-18 ENGINE LAW (the r169 "writers not port-modeled" disclosure is
            // now modeled): the two Lev2 gate bytes are WORLD state, not camera state.
            // Is2ndLevelFloorable (0x20ce20) reads cFixedWorld+0x81 and
            // HasBeenTo2ndLevel (0x20ce70) reads cFixedWorld+0x80, both reached
            // through TOC-0x7354; they are cleared at world construction and at the
            // head of every story-2 ComputeRooms pass (0x15f040) and set inside that
            // pass from story-2 tile content (+0x81 in the tile-marking arms
            // 0x160644/0x1607d8, +0x80 in the story-2 room scan 0x15f264) — i.e.
            // "the lot HAS a second story", recomputed on load and on every edit.
            // cWinViewControl::UpdateViewFromCPState 0x2b3f48 then enables Lev2:
            //   enabled = story2Content(+0x80) || (mode==BUILD && floorable(+0x81))
            // The old port latch (a per-UI camera-visit bit seeded from Level==2)
            // could never arm on a two-story lot entered at story 1 — Lev2 and the
            // PageUp key stayed dead in live mode.
            var arch = Game.vm?.Context?.Architecture;
            return SecondStoryAvailableForState(
                Game.InLot,
                HasSecondStoryContent(arch),
                Game.Frontend?.MainPanel?.Mode == UIMainPanelMode.BUILD,
                AnySupported(arch));
        }

        /// cFixedWorld+0x80: any story-2 wall segment or floor pattern exists.
        internal static bool HasSecondStoryContent(FSO.SimAntics.VMArchitecture arch)
        {
            if (arch == null) return false;
            var floors = (arch.Floors != null && arch.Floors.Length > 1) ? arch.Floors[1] : null;
            if (floors != null)
                foreach (var t in floors) if (t.Pattern > 0) return true;
            var walls = (arch.Walls != null && arch.Walls.Length > 1) ? arch.Walls[1] : null;
            if (walls != null)
                foreach (var t in walls) if (t.Segments != 0) return true;
            return false;
        }

        /// cFixedWorld+0x81: any tile supports 2nd-story construction (the
        /// r169-disclosed mapping of Is2ndLevelFloorable onto Supported[0]).
        internal static bool AnySupported(FSO.SimAntics.VMArchitecture arch)
        {
            var supported = arch?.Supported;
            if (supported == null || supported.Length == 0 || supported[0] == null) return false;
            foreach (var tile in supported[0]) if (tile) return true;
            return false;
        }

        internal static bool SecondStoryAvailableForState(
            bool inLot, bool story2Present, bool buildMode, bool floorable)
        {
            return inLot && (story2Present || (buildMode && floorable));
        }

        private void SelectStory(sbyte story)
        {
            if (story == 2 && !CanUseSecondStory()) return;
            Game.Level = (sbyte)Math.Max(1, Math.Min(2, (int)story));
            if (Game.LotControl?.World?.State != null) Game.LotControl.World.State.ScrollAnchor = null;
        }

        private FSO.LotView.Utils.Camera.ICameraController GetActiveCamera()
        {
            return Game.vm?.Context?.World?.State?.Cameras?.ActiveCamera;
        }

        private void ZoomControl(UIElement button)
        {
            if (ViewPie != null) return; // R218: the gesture belongs to the pie
            if (GetActiveCamera()?.UseZoomHold != false && Game.InLot) return;
            Game.ZoomLevel = (Game.ZoomLevel + ((button == ZoomInButton) ? -1 : 1));
        }

        private void RotateCounterClockwise(UIElement button)
        {
            if (ViewPie != null) return; // R218: the gesture belongs to the pie
            if (GetActiveCamera()?.UseRotateHold != false && Game.InLot) return;
            var newRot = (Game.Rotation - 1);
            if (newRot < 0) newRot = 3;
            Game.Rotation = newRot;
        }

        private void RotateClockwise(UIElement button)
        {
            if (ViewPie != null) return; // R218: the gesture belongs to the pie
            if (GetActiveCamera()?.UseRotateHold != false && Game.InLot) return;
            Game.Rotation = (Game.Rotation + 1) % 4;
        }

        // R218: the view-pie tear-off. The engine pops its cTSPieMenu from the
        // window-manager popup path (not statically traceable); the port model
        // is the classic tear-off — dragging >= DragThreshold px while a small
        // zoom/rotate button is held opens the radial AT that button; a plain
        // click still steps (the pie suppresses the click while it is up).
        private void UpdateViewPieGesture(UpdateState state)
        {
            UIButton held = null;
            if (ZoomInButton != null && ZoomInButton.IsDown) held = ZoomInButton;
            else if (ZoomOutButton != null && ZoomOutButton.IsDown) held = ZoomOutButton;
            else if (RotateCWButton != null && RotateCWButton.IsDown) held = RotateCWButton;
            else if (RotateCCWButton != null && RotateCCWButton.IsDown) held = RotateCCWButton;

            if (held != null)
            {
                var m = new Vector2(state.MouseState.X, state.MouseState.Y);
                if (_pieArmButton == null)
                {
                    _pieArmButton = held;
                    _pieArmPos = m;
                }
                else if (_pieArmButton == held && ViewPie == null
                    && (m - _pieArmPos).Length() >= Simitone.Client.UI.Panels.UIOriginalViewPie.DragThreshold)
                {
                    OpenViewPie(held);
                }
            }
            else
            {
                _pieArmButton = null;
            }
        }

        private void OpenViewPie(UIButton source)
        {
            if (Game == null || ViewPie != null) return;
            try
            {
                // screen center of the button, into the game-screen space the
                // pie mounts in (the UIPieMenu idiom: screen / DPIScale).
                var c = source.LocalPoint(new Vector2(source.Size.X / 2f, source.Size.Y / 2f));
                ViewPie = new Simitone.Client.UI.Panels.UIOriginalViewPie(SelectViewPieItem, () => Game.ZoomLevel, ViewPieRemoved)
                {
                    Position = c / FSO.Common.FSOEnvironment.DPIScaleFactor
                };
                Game.Add(ViewPie);
                Simitone.Client.UI.Panels.UIOriginalViewPie.PiesOpened++;
                FSO.HIT.HITVM.Get().PlaySoundEvent(FSO.Client.UI.Model.UISounds.PieMenuAppear);
            }
            catch (Exception pe) { Simitone.Client.GameLog.Write("ucp: viewpie EXC " + pe.GetType().Name + " " + pe.Message); }
        }

        /// <summary>The gate path — the same open the drag gesture takes.</summary>
        public void OpenViewPieForProbe(string buttonKey)
        {
            UIButton b = null;
            switch (buttonKey)
            {
                case "zoomin": b = ZoomInButton; break;
                case "zoomout": b = ZoomOutButton; break;
                case "rotright": b = RotateCWButton; break;
                case "rotleft": b = RotateCCWButton; break;
            }
            if (b != null) OpenViewPie(b);
        }

        public void SelectViewPieItem(string key, int mag)
        {
            ViewPieRemoved();
            // UI-37 decoded commit (UpdateViewMenu @0x1020ccb0): zoom state
            // ±mag clamped [1,3] (the port's ZoomLevel setter applies the
            // same clamp); rotation ±mag with the native ±2 rotation cap.
            int m = Math.Max(1, Math.Min(3, mag));
            switch (key)
            {
                case "zoomin": ApplyZoom(-m); break;
                case "zoomout": ApplyZoom(+m); break;
                case "rotleft": ApplyRotation(-Math.Min(2, m)); break;
                case "rotright": ApplyRotation(+Math.Min(2, m)); break;
            }
        }

        private void ApplyZoom(int delta)
        {
            if (Game == null) return;
            Game.ZoomLevel = Math.Max(1, Math.Min(3, Game.ZoomLevel + delta));
        }

        private void ApplyRotation(int delta)
        {
            if (Game == null) return;
            Game.Rotation = ((Game.Rotation + delta) % 4 + 4) % 4;
        }

        /// <summary>The pie removes itself from the screen on completion; the
        /// UCP drops its reference so clicks un-suppress on the same frame.</summary>
        public void ViewPieRemoved()
        {
            ViewPie = null;
            _pieArmButton = null;
        }

        private string LastClock = "";
        private int LastCut;
        private sbyte LastFloor = 0;
        private int LastZoom;
        public override void Update(UpdateState state)
        {
            var vm = Game.vm;
            // R205: engine PostChildDraw refreshes the friend readout every
            // redraw; the compare-then-set keeps the write itself rare.
            try { UpdateFriendCount(); } catch { }
            if (vm != null && vm.Context != null)
            {
                var min = vm.Context.Clock.Minutes;
                var hour = vm.Context.Clock.Hours;
                string suffix = (hour > 11) ? "PM" : "AM";
                hour %= 12;
                if (hour == 0) hour = 12;
                var text = hour.ToString() + ":" + min.ToString().PadLeft(2, '0') + " " + suffix;
                if (text != LastClock) LastClock = text;   // clock text now lives in the toolbar (UIMainPanel)

                // R142: plate speed/pause selected-state sync (the toolbar cluster is
                // mobile-only now; same RemapSpeed law UIMainPanel.Update applies).
                try
                {
                    var speed = Simitone.Client.UI.Panels.UIMainPanel.RemapSpeed[Math.Max(0, vm.SpeedMultiplier)];
                    if (PauseButton != null)
                    {
                        PauseButton.ButtonFrame = (speed == 4) ? 1 : 0;
                        PauseButton.Selected = (speed == 4);
                    }
                    if (SpeedButtons != null) for (int i = 0; i < 3; i++) SpeedButtons[i].Selected = (i + 1 == speed);
                }
                catch { }
            }

            // Keep story state coherent even when entering/leaving a lot without a
            // level change. This also repairs old saves/UI state that exposed the
            // port's unused architecture layers 3-5 on the two-story TS1 control.
            if (Game.Level < 1 || Game.Level > 2)
                Game.Level = (sbyte)Math.Max(1, Math.Min(2, (int)Game.Level));
            if (Game.Level == 2 && !CanUseSecondStory()) Game.Level = 1;
            var floorStateChanged = Game.Level != LastFloor;
            LastFloor = Game.Level;
            FloorDownButton.Selected = LastFloor == 1;
            FloorUpButton.Selected = LastFloor == 2;
            FloorDownButton.Disabled = !Game.InLot;
            FloorUpButton.Disabled = !CanUseSecondStory();
            if (floorStateChanged) Invalidated = true;

            if (LastCut != Game.LotControl.WallsMode)
            {
                LastCut = Game.LotControl.WallsMode;
                RoofButton.Selected = LastCut == 3;
                WallsUpButton.Selected = LastCut == 2;
                WallsCutButton.Selected = LastCut == 1;
                WallsDownButton.Selected = LastCut == 0;
            }

            if (LastZoom != Game.ZoomLevel) UpdateZoomButton();

            base.Update(state);

            //KEY SHORTCUTS
            var keys = state.NewKeys;
            var nofocus = state.InputManager.GetFocus() == null;
            if (Game.InLot)
            {
                if (nofocus && keys.Contains(Keys.F1) && !LiveButton.Disabled) OnModeClick?.Invoke(UIMainPanelMode.LIVE);
                if (nofocus && keys.Contains(Keys.F2) && !BuyButton.Disabled) OnModeClick?.Invoke(UIMainPanelMode.BUY);
                if (nofocus && keys.Contains(Keys.F3) && !BuildButton.Disabled) OnModeClick?.Invoke(UIMainPanelMode.BUILD);
                if (nofocus && keys.Contains(Keys.F4) && !CameraButton.Disabled) OnModeClick?.Invoke(UIMainPanelMode.CAMERA);
                if (nofocus && keys.Contains(Keys.F5)) OnModeClick?.Invoke(UIMainPanelMode.OPTIONS);

                // R200: RETURN in the cDDDSimsView hotkey dispatcher
                // (0x21761c -> 0x217a3c) tracks the selected sim — the branch
                // is gated GetMode()==2 (BUILD, where the people window is not
                // on screen); TrackPerson's same-person branch toggles off.
                if (nofocus && keys.Contains(Keys.Enter)
                    && Game.Frontend != null && Game.Frontend.MainPanel != null
                    && Game.Frontend.MainPanel.Mode == UIMainPanelMode.BUILD)
                {
                    var chrome = Game.Frontend.MainPanel.PeopleChrome;
                    if (chrome != null) chrome.TrackPerson(Game.SelectedAvatar);
                }

                // E key for the (now buttonless) eyedropper tool, Buy/Build only
                if (nofocus && keys.Contains(Keys.E) && Game.LotControl?.ObjectHolder != null && Game.LotControl.World.State.BuildMode > 0)
                {
                    var holder = Game.LotControl.ObjectHolder;
                    holder.EyedropperMode = !holder.EyedropperMode;
                }

                // R218: the view-pie tear-off gesture (press-drag on a small
                // zoom/rotate button opens the radial at that button; plain
                // clicks step — see UIOriginalViewPie for the engine law).
                try { UpdateViewPieGesture(state); } catch { }

                if (nofocus)
                {
                    var world = Game.vm.Context.World;
                    var cameras = world.State.Cameras;
                    var activeCamera = cameras.ActiveCamera;
                    if (activeCamera.UseRotateHold)
                    {
                        var cam = Game.vm.Context.World.State.Cameras.Camera3D;
                        if (RotateCWButton.IsDown || state.KeyboardState.IsKeyDown(Keys.OemPeriod)) cam.RotationX += 2f / FSOEnvironment.RefreshRate;
                        if (RotateCCWButton.IsDown || state.KeyboardState.IsKeyDown(Keys.OemComma)) cam.RotationX -= 2f / FSOEnvironment.RefreshRate;
                    }
                    else
                    {
                        if (keys.Contains(Keys.OemComma)) RotateCounterClockwise(null);
                        if (keys.Contains(Keys.OemPeriod)) RotateClockwise(null);
                    }
                    if (activeCamera.UseZoomHold)
                    {
                        if (ZoomInButton.IsDown || (state.KeyboardState.IsKeyDown(Keys.OemPlus) && !state.CtrlDown)) Game.LotControl.TargetZoom = Math.Max(0.25f, Math.Min(Game.LotControl.TargetZoom + 1f / FSOEnvironment.RefreshRate, 2));
                        if (ZoomOutButton.IsDown || (state.KeyboardState.IsKeyDown(Keys.OemMinus) && !state.CtrlDown)) Game.LotControl.TargetZoom = Math.Max(0.25f, Math.Min(Game.LotControl.TargetZoom - 1f / FSOEnvironment.RefreshRate, 2));
                    }
                    else
                    {
                        if (keys.Contains(Keys.OemPlus) && !state.CtrlDown && !ZoomInButton.Disabled) { Game.ZoomLevel -= 1; UpdateZoomButton(); }
                        if (keys.Contains(Keys.OemMinus) && !state.CtrlDown && !ZoomOutButton.Disabled) { Game.ZoomLevel += 1; UpdateZoomButton(); }
                    }
                    if (keys.Contains(Keys.PageDown)) SelectStory(1);
                    if (keys.Contains(Keys.PageUp)) SelectStory(2);
                    if (keys.Contains(Keys.Home)) UpdateWallsViewKeyHandler(1);
                    if (keys.Contains(Keys.End)) UpdateWallsViewKeyHandler(0);
                }
            }
        }

        private void UpdateWallsViewKeyHandler(int type)
        {
            // AUD-17 C1-3: Home/End used to bump WallsMode directly — the
            // renderer gates roofs on World.State.DrawRoofs, which only SetCut
            // writes, so the key path desynced from the buttons (Roof selected
            // with no roofs drawn, or roofs left over walls-up). Route through
            // SetCut so both stay atomic.
            var mode = Game.LotControl.WallsMode;
            switch (type)
            {
                case 0:
                    if (mode > 0) SetCut(mode - 1);
                    break;
                case 1:
                    if (mode < 3) SetCut(mode + 1);
                    break;
            }
        }

        // R144: plate readout setters — money right-anchored to plate (178,158)
        // (font[10], sized for "$9,999,999"), clock digits centered in the
        // plate dial rect (95,118,151,125) (font[8]).
        public void SetMoneyText(int money)
        {
            if (MoneyOriginal == null) return;
            var mtext = "§" + money.ToString("##,#0");
            MoneyOriginal.Text = mtext;
            MoneyOriginal.X = 178 - MoneyOriginal.Font.Measure(mtext);
            MoneyOriginal.Y = 158;
            Invalidated = true;
            Simitone.Client.UI.Controls.UIOriginalText.MoneyUpdatesDrawn++;
        }

        public void SetClockText(string text)
        {
            if (ClockDigits == null) return;
            ClockDigits.Text = text;
            ClockDigits.X = (95 + 151) / 2 - ClockDigits.Font.Measure(text) / 2;
            ClockDigits.Y = 118;
            Invalidated = true;
            Simitone.Client.UI.Controls.UIOriginalText.ToolbarReadoutsDrawn++;
        }

        // R205: engine PostChildDraw law — refresh the readout every update;
        // the caption WRITE only happens when the count CHANGED (the engine's
        // compare-then-set against the gadget's +0x54 caption slot). R216: the
        // rect anchors to the FIXED worst-case money gadget left edge AND to
        // the money line (creation block 0x2b7520: SetArea(moneyLeft−w,
        // moneyTop, moneyLeft, moneyTop+fontH)) — the engine sizes the money
        // gadget once for "$9,999,999" (r144 toolbar-law §2) and computes the
        // friend gadget against that box, so the digit sits at the fixed
        // anchor on the money LINE regardless of the live money width. The
        // earlier live re-derivation floated the digits under short money
        // strings (the user-visible "0 coming in from the bottom").
        public void UpdateFriendCount()
        {
            if (FriendOriginal == null) return;
            var text = ComputeFamilyFriendCount(Game).ToString();
            if (FriendOriginal.Text == text)
            {
                PositionFriendReadout();
                return;
            }
            FriendOriginal.Text = text;
            PositionFriendReadout();
            FriendReadoutUpdates++;
            Invalidated = true;
        }

        private float? _fixedMoneyLeft;
        /// The money gadget's worst-case left edge: 178 − measure("$9,999,999")
        /// in font[10] — the engine's once-at-Init anchor (r144/r205 law).
        public float FixedMoneyLeftForProbe
        {
            get
            {
                if (_fixedMoneyLeft == null)
                {
                    var f = (MoneyOriginal != null && MoneyOriginal.Font != null)
                        ? MoneyOriginal.Font
                        : (FriendOriginal != null && FriendOriginal.Font != null) ? FriendOriginal.Font : null;
                    _fixedMoneyLeft = 178 - (f != null ? f.Measure("$9,999,999") : 88);
                }
                return _fixedMoneyLeft.Value;
            }
        }

        private void PositionFriendReadout()
        {
            if (FriendOriginal == null) return;
            var w = (FriendOriginal.Font != null) ? FriendOriginal.Font.Measure(FriendOriginal.Text) : 0;
            var line = (FriendOriginal.Font != null) ? FriendOriginal.Font.LineHeight : 12;
            var moneyLeft = FixedMoneyLeftForProbe;
            // R216 engine law (creation block 0x2b7520-0x2b75a8, arg order
            // pinned by the money gadget's own SetArea at 0x2b72f4):
            // SetArea(moneyLeft−w, moneyTop, moneyLeft, moneyTop+fontH) —
            // the digit sits on the money LINE, right-aligned to the fixed
            // worst-case money box. The R205/R215 '+ line height' drop was a
            // misread (R144's moneyBottom prose); it pushed the digit under
            // the plate band.
            FriendOriginal.X = moneyLeft - w;
            FriendOriginal.Y = 158;
            FriendOriginal.Size = new Vector2(w, line);
        }

        // Engine CPState::GetFamilyFriendCount 0x102038e0 (r2 0x2038e0):
        // formats Neighborhood::GetFamilyFriendsCount 0x100a9330 -> cached
        // string at CPState+0x224. The SET law is UpdateFamilyFriendsCount
        // 0x100a81c0 (ORIG-02 friend-set-law.md; AUD-18-B re-verified to the
        // byte): neighbor must BELONG to a family (+0xEE != 0, townies out),
        // must not be a counted-family member (Family::TestMember 0x1006de80),
        // must be person type (+0xF6 <= 1, pets/other out), and ANY member
        // needs mutual DAILY (RelMatrix slot 0) >= T both directions, where
        // T = the shared friendship-threshold global ([TOC-0x726c] = TOC slot
        // image 0x105ba1f4 -> data 0x48eb8 = 25; startup loader 0x101098c0
        // stores FloatConstants::Get("friendship threshold", 25.0f) as int —
        // key at name-blob 0x491c0+0x28a, default float 25.0 at code pool
        // 0x59a524+0x60). STR# 162[1]'s "counted once" is the inner-loop
        // break, not a string-set dedup (the old 0x1417e0 "StringSet builder"
        // record was raw-file-offset misreading — 0x1417e0 = a
        // StringEditBuffer ctor; ORIG-02 corrected).
        public static int ComputeFamilyFriendCount(TS1GameScreen game)
        {
            try
            {
                var provider = Content.Get()?.Neighborhood;
                var family = (game == null) ? null : game.ActiveFamily;
                if (provider == null || family == null || family.FamilyGUIDs == null) return 0;
                var memberIds = new System.Collections.Generic.List<short>();
                foreach (var guid in family.FamilyGUIDs)
                {
                    var id = provider.GetNeighborIDForGUID(guid);
                    if (id.HasValue) memberIds.Add(id.Value);
                }
                var friendIds = new System.Collections.Generic.HashSet<short>();
                foreach (var memberId in memberIds)
                {
                    var member = provider.GetNeighborByID(memberId);
                    if (member == null || member.Relationships == null) continue;
                    foreach (var other in provider.Neighbors.Entries)
                    {
                        if (other == null || other.NeighbourID == memberId
                            || other.Relationships == null) continue;
                        if (memberIds.Contains(other.NeighbourID)) continue; // same family
                        // ORIG-02 friend-set law (GetFamilyFriendsCount
                        // 0xa9330/UpdateFamilyFriendsCount 0xa81c0): a friend
                        // must BELONG to a family (townies excluded: family
                        // number +0xEE != 0), must be a person (type +0xF6 <= 1
                        // — pets excluded), and ANY member needs mutual DAILY
                        // (slot 0) >= 25 BOTH ways (the friendship threshold
                        // autonomy constant, default 25.0 pinned in-binary —
                        // NOT the old 50).
                        // townie: PersonData[TS1FamilyNumber] == 0 (family +0xEE)
                        var opd = other.PersonData;
                        var otherFam = (opd != null && opd.Length > (int)FSO.SimAntics.Model.VMPersonDataVariable.TS1FamilyNumber)
                            ? opd[(int)FSO.SimAntics.Model.VMPersonDataVariable.TS1FamilyNumber] : 0;
                        if (otherFam == 0) continue;
                        // pet gate (type +0xF6 <= 1): the gender raw's dog/cat
                        // bits (ENG-25 adapter)
                        var otherG = (opd != null && opd.Length > (int)FSO.SimAntics.Model.VMPersonDataVariable.Gender)
                            ? opd[(int)FSO.SimAntics.Model.VMPersonDataVariable.Gender] : 0;
                        if ((otherG & 8) != 0 || (otherG & 16) != 0) continue;
                        System.Collections.Generic.List<short> fwd, rev;
                        if (!member.Relationships.TryGetValue(other.NeighbourID, out fwd)
                            || !other.Relationships.TryGetValue(member.NeighbourID, out rev)
                            || fwd.Count == 0 || rev.Count == 0
                            || fwd[0] < 25 || rev[0] < 25) continue;
                        friendIds.Add(other.NeighbourID);
                    }
                }
                return friendIds.Count;
            }
            catch { return 0; }
        }

        // Engine TSOnCommand 0x2b5114: the dialog content is STR# 162 top to
        // bottom — title [0], message [1], ONE button captioned [2] 'OK'
        // (explicit text, the same table — not the 152 system default).
        public Simitone.Client.UI.Panels.UIMobileAlert BuildFriendCountDialog()
        {
            return new Simitone.Client.UI.Panels.UIMobileAlert(new FSO.Client.UI.Controls.UIAlertOptions
            {
                Title = GameFacade.Strings.GetString("162", "0"),
                Message = GameFacade.Strings.GetString("162", "1"),
                Buttons = new FSO.Client.UI.Controls.UIAlertButton[] {
                    new FSO.Client.UI.Controls.UIAlertButton(
                        FSO.Client.UI.Controls.UIAlertButtonType.OK, null,
                        GameFacade.Strings.GetString("162", "2")) }
            });
        }

        public void OpenFriendCountDialog()
        {
            FSO.Client.UI.Framework.UIScreen.GlobalShowDialog(BuildFriendCountDialog(), true);
            FriendDialogsOpened++;
        }

        // R144: money change floaters, plate-anchored (the engine's
        // SetState(1)+flash law is a flash-on-change; the floater is the
        // port's standing interpretation since R99, re-anchored).
        public int FloatersTwinned;
        // R194: floaters ride the original speech-balloon chrome
        // (kSpeechMediumBmp 9000 nine-slice; GenerateSpeechIcons law)
        public int FloatersBallooned;
        public void DisplayChange(int change)
        {
            if (change == 0 || MoneyOriginal == null || MoneyOriginal.Font == null || MoneyOriginal.Font.Atlas == null) return;
            var text = ((change > 0) ? "+" : "-") + "§" + Math.Abs(change);
            var balloon = new Simitone.Client.UI.Controls.UIOriginalSpeechBalloon(text, MoneyOriginal.Font, false);
            var w = MoneyOriginal.Font.Measure(text);
            balloon.SizeToContent(w, 13);
            balloon.Position = new Vector2(MoneyOriginal.X - 6, MoneyOriginal.Y - 34f);
            balloon.Label.Color = (change > 0) ? UIStyle.Current.Text : UIStyle.Current.NegMoney;
            Add(balloon);
            GameFacade.Screens.Tween.To(balloon, 1.5f, new Dictionary<string, float>() { { "Y", balloon.Y - 30 }, { "Opacity", 0 } });
            FSO.Common.Utils.GameThread.SetTimeout(() => { Remove(balloon); Invalidated = true; }, 1500);
            Invalidated = true;
            FloatersTwinned++;
            FloatersBallooned++;
        }

        public void UpdateZoomButton()        {
            ZoomInButton.Disabled = (!Game.InLot) || (!FSOEnvironment.Enable3D && (Game.ZoomLevel == 1));
            ZoomOutButton.Disabled = (!FSOEnvironment.Enable3D && (Game.ZoomLevel == 3));
            LastZoom = Game.ZoomLevel;
        }

        public void SetMode(UIMainPanelMode mode)
        {
            LiveButton.Selected = mode == UIMainPanelMode.LIVE;
            BuyButton.Selected = mode == UIMainPanelMode.BUY;
            BuildButton.Selected = mode == UIMainPanelMode.BUILD;
            OptionsButton.Selected = mode == UIMainPanelMode.OPTIONS;
            CameraButton.Selected = mode == UIMainPanelMode.CAMERA;

            // the per-mode patch behind the dial (kLiveModePatch family)
            LivePatch.Visible = mode == UIMainPanelMode.LIVE;
            BuyPatch.Visible = mode == UIMainPanelMode.BUY;
            BuildPatch.Visible = mode == UIMainPanelMode.BUILD;
            OptionsPatch.Visible = mode == UIMainPanelMode.OPTIONS;
            CameraPatch.Visible = mode == UIMainPanelMode.CAMERA;
            Invalidated = true;
        }

        public void UpdateBuildBuy()
        {
            var bbEnable = Game.vm.Context.Architecture.BuildBuyEnabled;
            BuyButton.Disabled = !bbEnable;
            BuildButton.Disabled = !bbEnable;
            LiveButton.Disabled = Game.vm.GetGlobalValue(32) != 0;
            // NBR-07 hide-slot pairing decode (UpdateViewFromCPState raw
            // 0x2b3e40 tail): the NATIVE drives are IsLiveModeDisabled
            // (CPState+0x55) → BUILD+CAMERA, and IsBuyAndBuildDisabled
            // (CPState+0x54) → BUY+LIVE (TS1GameScreen.
            // NativeUcpDisablePairing is the law's single source). The port
            // pairs {BUY,BUILD} with the engine's BuildBuyEnabled and
            // {LIVE,CAMERA} with global 32 — a DISCLOSED port-side pairing:
            // on the correlated visit-lock path both drives are true
            // together (the visit lock sets BuildBuyEnabled=false AND
            // global 32), so the visible outcome matches the native law
            // there; the drives are correlated, not coupled (review P3-2);
            // see the NBR-07 receipt before changing this gate.
            CameraButton.Disabled = LiveButton.Disabled;
        }

        public void SetCut(int cut)
        {
            Game.LotControl.World.State.DrawRoofs = (cut == 3);
            Game.LotControl.WallsMode = cut;
        }
    }
}
