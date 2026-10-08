using FSO.Client.UI.Framework;
using FSO.Content;
using Simitone.Client.UI.Controls;
using Simitone.Client.UI.Screens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FSO.Common.Rendering.Framework.Model;
using FSO.Client;
using Microsoft.Xna.Framework;
using Simitone.Client.UI.Panels.LiveSubpanels;
using FSO.Client.UI.Model;
using Microsoft.Xna.Framework.Input;
using FSO.SimAntics;
using FSO.SimAntics.NetPlay.Model.Commands;
using FSO.HIT;
using Simitone.Client.UI.Panels.Desktop;

namespace Simitone.Client.UI.Panels
{
    public class UISimitoneFrontend : UIContainer
    {
        public UIClockPanel Clock;
        public UITwoStateButton CutBtn;
        public UICutawayPanel CutPanel;
        public TS1GameScreen Game;
        public UIMoneyPanel Money;
        public UIMainPanel MainPanel;
        public UIStencilButton ExtendPanelBtn;
        public UIModeSwitcher ModeSwitcher;
        public UIDesktopUCP DesktopUCP;
        public UICheatTextbox CheatTextbox;

        public bool PanelActive;
        public int LastCut = 0;

        public UISimitoneFrontend(TS1GameScreen screen)
        {
            var ui = Content.Get().CustomUI;
            Game = screen;

            if (!Game.Desktop)
            {
                CutBtn = new UITwoStateButton(ui.Get("cut_btn_down.png").Get(GameFacade.GraphicsDevice));
                CutBtn.X = screen.ScreenWidth - (256 + 15);
                CutBtn.Y = 15;
                CutBtn.OnButtonClick += CutButton;
                Add(CutBtn);

                Clock = new UIClockPanel(screen.vm);
                Clock.X = screen.ScreenWidth - (334 + 15);
                Clock.Y = 15;
                Add(Clock);

                Money = new UIMoneyPanel(screen);
                Money.Position = new Vector2(15, screen.ScreenHeight - 172);
                Add(Money);

                ExtendPanelBtn = new UIStencilButton(ui.Get("panel_expand.png").Get(GameFacade.GraphicsDevice));
                ExtendPanelBtn.OnButtonClick += ExpandClicked;
                Add(ExtendPanelBtn);
            }

            CheatTextbox = new UICheatTextbox(Game.vm);
            // R207: engine SetArea(20,20,220,41) — the bar anchors at
            // (20,20), not the old (10,10) eyeball.
            CheatTextbox.Position = new Vector2(20, 20);
            CheatTextbox.Visible = false;
            Add(CheatTextbox);

            // R120/R141: the desktop UCP must exist BEFORE MainPanel is placed — the panel's X is
            // derived from the UCP's right edge (R120 fixed a 136px overlap). R141: the UCP is
            // the ORIGINAL plate (UniversalBack.TGA 220x183) mounted flush to the bottom-left
            // corner like the engine composes it — no 15px float margin, no 292x281 chrome.
            if (Game.Desktop)
            {
                DesktopUCP = new UIDesktopUCP(screen);
                DesktopUCP.Position = new Vector2(0, screen.ScreenHeight - 183);
                DesktopUCP.OnModeClick += LiveButtonClicked;
                Add(DesktopUCP);
            }

            MainPanel = new UIMainPanel(screen);
            MainPanel.OnEndSelect += OnEndSelect;
            MainPanel.ModeChanged += ModeChanged;
            Add(MainPanel);

            // R172: cWinCPanel paints kBackPatch in its own background before
            // child windows are composed.  The view-control/UCP child therefore
            // sits above the opaque 52x100 bridge wherever UniversalBack's
            // quarter-round plate has pixels, while the bridge remains visible
            // through the plate's transparent outside edge.  MainPanel owns the
            // BackPatch image in this port, so keep the equivalent sibling order:
            // toolbar first, UCP last.  Drawing MainPanel after DesktopUCP put the
            // fully-opaque bridge over the plate's rightmost 52 pixels and made
            // the permanent-left/swapping-right join look cut apart.
            if (Game.Desktop) Add(DesktopUCP);

            if (!Game.Desktop)
            {
                var mode = new UIModeSwitcher(screen);
                mode.Position = new Vector2(64 + 15, screen.ScreenHeight - (64 + 15));
                mode.OnModeClick += LiveButtonClicked;
                Add(mode);
                ModeSwitcher = mode;
                ExtendPanelBtn.Position = new Vector2(mode.X + 54, mode.Y - 50);
            }

            MainPanel.X = 64 + 15;
            // R142: flush against the UCP's right edge — the original composition is
            // UCP(220) + PanelBack(804) = 1024 with no seam; the old +8 gap was ours.
            if (Game.Desktop) MainPanel.X = (int)(DesktopUCP.X + DesktopUCP.Size.X);
            MainPanel.GameResized();
            // R142: the desktop bar sits flush at the bottom — PanelBack's own
            // 100px height (was SH-143, leaving a 15px float gap + 128px panel).
            MainPanel.Y = Game.Desktop ? screen.ScreenHeight - 100 : screen.ScreenHeight - (128 + 15);
            MainPanel.Visible = false;

            // NBR-06 entry law (cSimsApp::LoadGame @0x258680 tail): a visit lot
            // (community-zoned or the public Magic lots 93..99 — the pure matrix
            // TS1GameScreen.NativeEntryVisit) enters in BUY mode PAUSED; the
            // engine's own visit flag is global 32 (the port's simless session,
            // which is the only reachable visit state — families cannot move
            // into community lots, 132[12]/[13]). Either arm forces the entry.
            var entryLot = Game.vm != null ? Game.vm.GetGlobalValue(10) : 0;
            bool zoningCommunity;
            try
            {
                zoningCommunity = FSO.Content.Content.Get().Neighborhood.GetZoningType((short)entryLot) == 1;
            }
            catch { zoningCommunity = false; }
            if (Game.vm.GetGlobalValue(32) > 0
                || Screens.TS1GameScreen.NativeEntryVisit(zoningCommunity, entryLot))
            {
                MainPanel.SetMode(UIMainPanelMode.BUY);
                ModeSwitcher?.EndSwitch(MainPanel.Mode);
                MainPanel.Open();
            } else
            {
                FSO.HIT.HITVM.Get().PlaySoundEvent(UIMusic.None);
                // R206 ENGINE LAW (the R199 residual closed): the original
                // composes the control panel PERMANENTLY at lot entry —
                // cSimsApp::RebuildControlPanel 0x24de40 (new(0x108) cWinCPanel
                // ctor 0x270d40) + CPState::EnteringHouse 0x211c80 -> SetMode;
                // the panel exists from the FIRST frame. The port's hidden-
                // until-first-click reveal is retired on desktop (touch keeps
                // its mobile flow). Untweened Open() end-state.
                if (Game.Desktop) MainPanel.ComposeAtLotEntry();
            }
        }

        private void ModeChanged(UIMainPanelMode obj)
        {
            Clock?.SetHidden(obj != UIMainPanelMode.LIVE);
            DesktopUCP?.SetMode(obj);
            var lotType = MainPanel.GetLotType(true);
            var hit = FSO.HIT.HITVM.Get();
            switch (obj)
            {
                case UIMainPanelMode.LIVE:
                case UIMainPanelMode.OPTIONS:
                case UIMainPanelMode.CAMERA:
                    hit.PlaySoundEvent(UIMusic.None); break;
                case UIMainPanelMode.BUY:
                    switch (lotType)
                    {
                        case UICatalogMode.Downtown:
                            hit.PlaySoundEvent(UIMusic.Downtown); break;
                        case UICatalogMode.Vacation:
                            hit.PlaySoundEvent(UIMusic.Vacation); break;
                        case UICatalogMode.Community:
                            hit.PlaySoundEvent(UIMusic.Unleashed); break;
                        case UICatalogMode.Studiotown:
                            hit.PlaySoundEvent(UIMusic.SuperstarTransition); break;
                        case UICatalogMode.Magictown:
                            hit.PlaySoundEvent(UIMusic.MagictownBuy); break;
                        default:
                            hit.PlaySoundEvent(UIMusic.Buy); break;
                    }
                    break;
                case UIMainPanelMode.BUILD:
                    switch (lotType)
                    {
                        case UICatalogMode.Downtown:
                            hit.PlaySoundEvent(UIMusic.Downtown); break;
                        case UICatalogMode.Vacation:
                            hit.PlaySoundEvent(UIMusic.Vacation); break;
                        case UICatalogMode.Community:
                            hit.PlaySoundEvent(UIMusic.Unleashed); break;
                        case UICatalogMode.Studiotown:
                            hit.PlaySoundEvent(UIMusic.SuperstarTransition); break;
                        case UICatalogMode.Magictown:
                            hit.PlaySoundEvent(UIMusic.MagictownBuild); break;
                        default:
                            hit.PlaySoundEvent(UIMusic.Build); break;
                    }
                    break;
            }
        }

        private void ExpandClicked(UIElement button)
        {
            MainPanel.Open();
            MainPanel.Switcher_OnCategorySelect(MainPanel.Switcher.ActiveCategory);
        }

        /// <summary>
        /// Programmatically switch to a different mode (Buy, Build, Live,
        /// Options, Camera).
        /// Used by eyedropper to auto-switch modes when clicking cross-mode objects.
        /// </summary>
        public void SwitchMode(UIMainPanelMode mode)
        {
            LiveButtonClicked(mode);
        }

        private bool LiveButtonClicked(UIMainPanelMode mode)
        {
            // R145: the engine's Objects-button law — every BUY activation
            /// while already in buy mode TOGGLES room <-> function sort
            /// (cWinViewControl::TSOnCommand 0x2b4b24; buy-catalog-law.md §2).
            if (Game.Desktop && mode == UIMainPanelMode.BUY && MainPanel.Mode == UIMainPanelMode.BUY
                && MainPanel.PanelActive && !MainPanel.ShowingSelect)
            {
                MainPanel.ToggleBuySortEngine();
                return false;
            }
            // R199: CPState::SetMode @0x210790 opens with `if (this->mode ==
            // requested && !force) return` — and every cWinViewControl::
            // TSOnCommand mode-button case (LIVE 0x2b4bec -> SetMode(0,0),
            // BUY 0x2b4bb0, BUILD, OPTIONS...) passes force 0 — so clicking
            // the ALREADY-ACTIVE desktop mode button is a complete no-op.
            // The old fallthrough into StartSelect() mounted the mobile
            // UISwitchAvatarPanel (pswitch_bg strip) over the native panel;
            // on desktop nothing like it exists. One port artifact: the
            // control panel starts hidden at lot entry (the original
            // composes it permanently), so the no-op still reveals it.
            if (Game.Desktop && mode == MainPanel.Mode)
            {
                if (!MainPanel.PanelActive) MainPanel.Open();
                MainPanel.SwitchAvatar?.Kill();
                return false;
            }
            var deskAuto = Game.Desktop && (mode != UIMainPanelMode.LIVE || MainPanel.Mode != UIMainPanelMode.LIVE);
            if (MainPanel.PanelActive || deskAuto)
            {
                if (MainPanel.ShowingSelect || deskAuto)
                {
                    if (!MainPanel.PanelActive) MainPanel.Open();
                    //switch to the target mode
                    MainPanel.SetMode(mode);
                    MainPanel.SwitchAvatar?.Kill();
                    return false;
                }
                else
                {
                    StartSelect();
                    return true;
                }
            } else
            {
                MainPanel.Open();
                StartSelect();
                return true;
            }
        }

        private void StartSelect()
        {
            MainPanel.ShowSelect();
        }

        private void OnEndSelect()
        {
            ModeSwitcher?.EndSwitch(MainPanel.Mode);
        }

        private void CutButton(UIElement button)
        {
            if (CutPanel != null)
            {
                CutBtn.Selected = false;
                CutPanel.Kill();
                CutPanel = null;
            } else
            {
                CutBtn.Selected = true;
                CutPanel = new UICutawayPanel(LastCut);
                CutPanel.X = CutBtn.X-39;
                CutPanel.Y = 15;
                CutPanel.OnSelection += CutPanel_OnSelection;
                AddAt(0, CutPanel);
            }
        }

        private void CutPanel_OnSelection(int obj)
        {
            CutBtn.Selected = false;
            Game.LotControl.World.State.DrawRoofs = (obj == 3);
            Game.LotControl.WallsMode = obj;
            CutPanel?.Kill();
            CutPanel = null;
        }

        public float ClockTween;
        public override void Update(UpdateState state)
        {
            // R200: the tracking sync laws run from this always-mounted host —
            // the engine's stops (selection change, manual camera input, the
            // level buttons' anchor clear) are mode-independent, while the
            // people chrome only ticks in live mode. Inert when nothing is
            // tracked (mobile never tracks through this model).
            Simitone.Client.UI.Panels.UIOriginalPeopleChrome.SyncTracking(Game);

            // Only switch Sims with Space if no text input has focus.
            // AUD-17 E-1: family-less lots (unowned community/downtown) never
            // ActivateFamily — CurrentFamily stays null and the old deref of
            // RuntimeSubset crashed the update loop on the first Space press.
            // AUD-17 E-9: a visible modal dialog owns the keyboard natively —
            // don't cycle Sims through a quit/move-in confirm.
            if (state.NewKeys.Contains(Keys.Space) && state.InputManager.GetFocus() == null
                && FSO.Client.GameFacade.Screens.TopVisibleDialog == null
                && Game.vm.TS1State.CurrentFamily != null)
            {
                var selected = Game.LotControl.ActiveEntity;
                var familyMembers = Game.vm.Context.ObjectQueries.Avatars.Where(x =>
                    Game.vm.TS1State.CurrentFamily.RuntimeSubset.Contains(x.Object.OBJ.GUID)
                        ).ToList();
                var index = familyMembers.IndexOf(selected);
                if (familyMembers.Count > 0)
                {
                    index = (index + 1) % (familyMembers.Count);
                    HITVM.Get().PlaySoundEvent(UISounds.QueueAdd);
                    Game.vm.SendCommand(new VMNetChangeControlCmd() { TargetID = familyMembers[index].ObjectID });
                }
            }

            base.Update(state);
            if (!Game.Desktop)
            {
                if (LastCut != Game.LotControl.WallsMode)
                {
                    LastCut = Game.LotControl.WallsMode;
                    var ui = Content.Get().CustomUI;
                    string cutImg = "cut_btn_down.png";
                    switch (LastCut)
                    {
                        case 1:
                            cutImg = "cut_btn_away.png"; break;
                        case 2:
                            cutImg = "cut_btn_up.png"; break;
                        case 3:
                            cutImg = "cut_btn_roof.png"; break;
                    }
                    CutBtn.Texture = ui.Get(cutImg).Get(GameFacade.GraphicsDevice);
                }
                if (Clock.TweenHook != ClockTween)
                {
                    ClockTween = Clock.TweenHook;
                    CutBtn.X = Game.ScreenWidth - (256 + (138f * ClockTween) + 15);
                    if (CutPanel != null) CutPanel.X = CutBtn.X - 39;
                }
                ModeSwitcher.LiveButton.Switching = MainPanel.ShowingSelect;
                ExtendPanelBtn.Visible = !MainPanel.PanelActive;
            }
        }

        public override void GameResized()
        {
            base.GameResized();
            if (!Game.Desktop)
            {
                CutBtn.X = Game.ScreenWidth - (256 + (138f * ClockTween) + 15);
                if (CutPanel != null) CutPanel.X = CutBtn.X - 39;
                Clock.X = Game.ScreenWidth - (334 + 15);
                Clock.Y = 15;
                Money.Position = new Vector2(15, Game.ScreenHeight - 172);
                var mode = ModeSwitcher;
                mode.Position = new Vector2(64 + 15, Game.ScreenHeight - (64 + 15));
                ExtendPanelBtn.Position = new Vector2(mode.X + 54, mode.Y - 50);
                MainPanel.Y = Game.ScreenHeight - (128 + 15);
            }
            else
            {
                DesktopUCP.Position = new Vector2(0, Game.ScreenHeight - 183);   // R141: original plate, flush bottom-left
                MainPanel.X = (int)(DesktopUCP.X + DesktopUCP.Size.X);   // R142: flush (no seam) after resize too
                MainPanel.Y = Game.ScreenHeight - 100;   // R142: PanelBack's own height, flush bottom
            }
        }
    }
}
