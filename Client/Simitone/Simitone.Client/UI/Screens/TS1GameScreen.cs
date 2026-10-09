
using FSO.Client;
using FSO.Client.Debug;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Client.UI.Model;
using FSO.Common;
using FSO.Common.Rendering.Framework;
using FSO.Common.Utils;
using FSO.Content;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;
using FSO.Files.RC;
using FSO.HIT;
using FSO.LotView;
using FSO.LotView.Model;
using FSO.SimAntics;
using FSO.SimAntics.Engine;
using FSO.SimAntics.Engine.TSOTransaction;
using FSO.SimAntics.Marshals;
using FSO.SimAntics.Model;
using FSO.SimAntics.NetPlay;
using FSO.SimAntics.NetPlay.Drivers;
using FSO.SimAntics.NetPlay.Model;
using FSO.SimAntics.NetPlay.Model.Commands;
using FSO.SimAntics.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Simitone.Client.UI.Controls;
using Simitone.Client.UI.Panels;
using Simitone.Client.UI.Panels.WorldUI;
using Simitone.Client.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simitone.Client.UI.Screens
{
    public class TS1GameScreen : FSO.Client.UI.Framework.GameScreen
    {
        public UIContainer WindowContainer;
        public bool Downtown;
        public bool Desktop = !FSOEnvironment.SoftwareKeyboard;
        // TRV-06 residual lane: set at the vacation-return edge, consumed in
        // Update once the home lot's family avatars are live (the native
        // carry-home push representation — see TryPushVacationCarryHome).
        private bool PendingVacationCarryHome;
        // TRV-06b lane B: frames remaining to look for (and cancel) the stale
        // return-transit parked in global 281 'Wait For Notify' after arrival
        // (see CompleteStaleTransit).
        private int StaleTransitClearBudget;

        public UILotControl LotControl { get; set; }
        public UIOriginalCameraOverlay CameraOverlay { get; set; }
        // R204: the STR# 148 'Paused' blink label (engine DrawPause law) —
        // mounts above the frontend at the default window position (0,0).
        public Simitone.Client.UI.Controls.UIOriginalPauseLabel PauseLabel { get; set; }
        private OriginalSnapshotCaptureScene SnapshotScene;
        private OriginalWebExportScene WebExportScene; // ENG-27
        public UIOriginalPictureInPicture PictureInPicture { get; private set; }
        public UISimitoneFrontend Frontend { get; set; }
        private FSO.LotView.World World;
        public FSO.SimAntics.VM vm { get; set; }
        public VMNetDriver Driver;
        public UISimitoneBg Bg;
        public uint VisualBudget { get; set; }

        //for TS1 hybrid mode
        public UINeighborhoodSelectionPanel TS1NeighPanel;
        public UIElement TS1NeighSwitcher;   // R141: tracked so null-switcher entry paths tear it down too
        public FAMI ActiveFamily;
        private ushort CurrentNeighborhoodMode = 4; // default to Normal/Old Town

        // Weather effects state tracking
        private float LastSoundIntensity = -1;
        private WeatherType LastWeatherType = WeatherType.Rain;
        private bool LastThunder = false;
        private bool TerrainSnowApplied = false;
        private int ArchValueFrame;
        private float ThunderTimer = 0f;
        private Random ThunderRandom = new Random();
        private short? PendingWeatherData = null;

        public bool InLot
        {
            get
            {
                return (vm != null);
            }
        }

        private int m_ZoomLevel;
        public int ZoomLevel
        {
            get
            {
                return m_ZoomLevel;
            }
            set
            {
                value = Math.Max(1, Math.Min(3, value));

                if (value < 4)
                {
                    if (vm == null)
                    {

                    }
                    else
                    {
                        var targ = (WorldZoom)(4 - value); //near is 3 for some reason... will probably revise
                        //HITVM.Get().PlaySoundEvent(UIMusic.None);
                        LotControl.Visible = true;
                        Bg.Visible = false;
                        World.Visible = true;
                        //ucp.SetMode(UIUCP.UCPMode.LotMode);
                        LotControl.SetTargetZoom(targ);
                        if (m_ZoomLevel != value) vm.Context.World.InitiateSmoothZoom(targ);
                        vm.Context.World.State.Zoom = targ;
                        m_ZoomLevel = value;
                    }
                }
                else //cityrenderer! we'll need to recreate this if it doesn't exist...
                {
                    if (m_ZoomLevel < 4)
                    { //coming from lot view... snap zoom % to 0 or 1
                        if (World != null)
                        {
                            LotControl.Visible = false;
                        }
                    }
                    m_ZoomLevel = value;
                }
            }
        }

        private int _Rotation = 0;
        public int Rotation
        {
            get
            {
                return _Rotation;
            }
            set
            {
                _Rotation = value;
                World.State.CenterTile = World.EstTileAtPosWithScroll(new Vector2(ScreenWidth / 2, ScreenHeight / 2));
                if (World != null)
                {
                    switch (_Rotation)
                    {
                        case 0:
                            World.State.Rotation = WorldRotation.TopLeft; break;
                        case 1:
                            World.State.Rotation = WorldRotation.TopRight; break;
                        case 2:
                            World.State.Rotation = WorldRotation.BottomRight; break;
                        case 3:
                            World.State.Rotation = WorldRotation.BottomLeft; break;
                    }
                }
                World.RestoreTerrainToCenterTile();
            }
        }

        public sbyte Level
        {
            get
            {
                if (World == null) return 1;
                else return World.State.Level;
            }
            set
            {
                if (World != null)
                {
                    World.State.Level = value;
                }
            }
        }

        public sbyte Stories
        {
            get
            {
                if (World == null) return 2;
                return World.Stories;
            }
        }

        public VMAvatar SelectedAvatar
        {
            get
            {
                return vm.GetAvatarByPersist(vm.MyUID);
            }
        }

        internal static ushort GetNeighborhoodModeFromHouse(short house)
        {
            // House number ranges match the lot types
            if (house >= 21 && house <= 31) return 2;  // Downtown
            else if (house >= 40 && house <= 49) return 3;  // Vacation
            else if (house >= 81 && house <= 89) return 5;  // Studiotown
            else if (house >= 90 && house <= 99) return 7;  // Magictown
            else return 4;  // Normal/Old Town (default)
        }

        public TS1GameScreen(NeighSelectionMode mode) : base()
        {
            // R160: the Simitone gradient is RETIRED behind the neighborhood — the
            // engine surround is the kLargeMask law (r143 §1: windows wider than the
            // 800x600 artboard load kLargeMask 5001 = Downtown/LargeBack.bmp 1024x768
            // as surround). loadModernFallback=false + ResolveNeighborhoodSurround.
            Bg = new UISimitoneBg(loadModernFallback: false);
            Bg.ResolveNeighborhoodSurround();
            Bg.Position = (new Vector2(ScreenWidth, ScreenHeight)) / 2;
            Add(Bg);

            WindowContainer = new UIContainer();
            Add(WindowContainer);

            if (Content.Get().TS1)
            {
                NeighSelection(mode);

                // R118: the Simitone GitHub update-check dialog is REMOVED - it popped an
                // "A new version of Simitone is available!" alert (MSDF-rendered) over the
                // game; the original game has no update plumbing. Services/UpdateChecker.cs
                // is deleted with it.
            }
        }
        public int? MoveInFamily;

        public void StartMoveIn(int familyID)
        {
            MoveInFamily = familyID;
        }

        // ROUND-197: cWinNeighborhoodVC::MoveInModeLotHandler @0x471d82, decoded
        // (r197/r197-lot-movein-law.md). The engine guard ORDER on a move-in lot
        // click: tutorial flag (HouseInfo+0x14) → invalid family → precomputed
        // can-afford (+0x10) → occupied (+0x24 != -1) → the +0x18 house-built
        // split between the two YesNo confirms. Every string pair is STR# 132
        // 'MoveInModeStrs'; the AskDialog style-2 (YesNo) YES button returns 4
        // and only then does the engine call LoadGame(lot, family).
        public enum NativeMoveInOutcome
        {
            TouchFallback,       // not the desktop path — keep the Simitone card
            ExitMoveInMode,      // family query failed: SetMode(0), silent return
            RefuseTutorial,      // 132[16]/[17]
            RefuseCommunity,     // 132[12]/[13] (the UL community guard)
            RefuseUnaffordable,  // 132[2]/[3]
            RefuseOccupied,      // 132[14]/[15]
            ConfirmPurchaseHouse,// 132[0]/[1] YesNo
            ConfirmPurchaseLot   // 132[6]/[7] YesNo
        }

        public static int NativeMoveInTitleIndex(NativeMoveInOutcome o)
        {
            switch (o)
            {
                case NativeMoveInOutcome.RefuseTutorial: return 16;
                case NativeMoveInOutcome.RefuseCommunity: return 12;
                case NativeMoveInOutcome.RefuseUnaffordable: return 2;
                case NativeMoveInOutcome.RefuseOccupied: return 14;
                case NativeMoveInOutcome.ConfirmPurchaseHouse: return 0;
                case NativeMoveInOutcome.ConfirmPurchaseLot: return 6;
                default: return -1;
            }
        }

        public static bool NativeMoveInIsConfirm(NativeMoveInOutcome o)
        {
            return o == NativeMoveInOutcome.ConfirmPurchaseHouse
                || o == NativeMoveInOutcome.ConfirmPurchaseLot;
        }

        public static NativeMoveInOutcome NativeMoveInDecision(bool desktop, bool familyValid,
            bool tutorial, bool community, bool canAfford, bool occupied, bool houseBuilt)
        {
            if (!desktop) return NativeMoveInOutcome.TouchFallback;
            if (tutorial) return NativeMoveInOutcome.RefuseTutorial;
            if (!familyValid) return NativeMoveInOutcome.ExitMoveInMode;
            if (community) return NativeMoveInOutcome.RefuseCommunity;
            if (!canAfford) return NativeMoveInOutcome.RefuseUnaffordable;
            if (occupied) return NativeMoveInOutcome.RefuseOccupied;
            return houseBuilt ? NativeMoveInOutcome.ConfirmPurchaseHouse
                              : NativeMoveInOutcome.ConfirmPurchaseLot;
        }

        // ---- NBR-06: the community-lot build/buy ENTRY matrix ----
        // Native law (ghidra-n1 decode): the cSimsApp::LoadGame @0x258680 tail
        // computes the visit flag (the sim context's +0x50, set on the
        // visit branches: GetZoningType==community native-2, or the public
        // Magic Town lots 93..99), then gates the lot session:
        //   DisableLiveMode(visit)                    — LIVE unavailable while visiting
        //   DisableSave(community||93-99 ? !visit     — save ENABLED on community
        //                 : (!visit && noFamily))       lots while visiting (that is
        //                                              how TS1 persists community
        //                                              edits); a familyless
        //                                              residential lot never saves
        //   HideButtonsForLocation(visit)             — extra view chrome hidden
        //   visit → SetMode(BUY)+Pause; else EnteringHouse
        // and CPState::SetMode @0x210790's mode-1 (BUY) case picks the catalog
        // purely by destination (downtown 21..30 → 2, vacation 40..49 → 3,
        // community-zoned non-magic → 4, studio 81..89 → 5, magic 93..98 → 6,
        // else 1). The port's zoning encoding is 0/1 (NBR-03); these pure
        // statics take the boolean so the encoding never leaks in.
        public static bool NativeEntryVisit(bool zoningCommunity, int lot)
        {
            return zoningCommunity || (lot > 0x5c && lot < 100);
        }

        public static bool NativeEntrySaveDisabled(bool zoningCommunity, int lot, bool visit, bool hasFamily)
        {
            if (zoningCommunity || (lot > 0x5c && lot < 100)) return !visit;
            return !visit && !hasFamily;
        }

        public static int NativeBuyCatalog(bool zoningCommunity, int lot)
        {
            if (lot >= 21 && lot <= 30) return 2;                       // downtown
            if (lot >= 40 && lot <= 49) return 3;                       // vacation
            if (zoningCommunity && !(lot > 0x5c && lot < 99)) return 4; // community (non-magic)
            if (lot >= 81 && lot <= 89) return 5;                       // studiotown
            if (lot > 0x5c && lot < 99) return 6;                       // magic town public
            return 1;                                                   // normal home
        }


        public void NeighSelection(NeighSelectionMode mode)
        {
            var nbd = (ushort)((mode == NeighSelectionMode.MoveInMagic) ? 7 : 4);
            NeighSelection(nbd, mode != NeighSelectionMode.Normal);
        }

        public void NeighSelection(ushort neighborhoodMode, bool moveInMode = false)
        {
            Content.Get().Neighborhood.PreparePersonDataFromObject = PersonGeneratorHelper.PreparePersonDataFromObject;
            Content.Get().Neighborhood.AddMissingNeighbors();
            TS1NeighPanel = new UINeighborhoodSelectionPanel(neighborhoodMode);
            var switcher = new UINeighbourhoodSwitcher(TS1NeighPanel, neighborhoodMode, moveInMode);
            TS1NeighPanel.OnHouseSelect += (house) =>
            {
                if (MoveInFamily != null)
                {
                    //move them in first
                    //confirm it
                    // R197: the native MoveInModeLotHandler guard chain decides the
                    // dialog (tutorial/community/afford/occupied/house-vs-lot). The
                    // old unconditional "Purchase House?" confirm was the Simitone
                    // surface; desktop now follows the engine law, touch keeps it.
                    var neigh = Content.Get().Neighborhood;
                    var moveIn = neigh.GetFamily((ushort)MoveInFamily.Value);
                    var houseIff = neigh.GetHouse(house);
                    var simi = houseIff?.Get<FSO.Files.Formats.IFF.Chunks.SIMI>(1);
                    var price = simi?.PurchaseValue ?? 0;
                    // HouseInfo+0x18 proxy: an empty lot carries land value only; a
                    // built house adds objects/architecture to the SIMI record.
                    bool houseBuilt = simi != null && (simi.ObjectsValue > 0 || simi.ArchitectureValue > 0);
                    bool occupied = neigh.GetFamilyForHouse((short)house) != null;
                    short zoning;
                    if (!neigh.ZoningDictionary.TryGetValue((short)house, out zoning)) zoning = 0;
                    var outcome = NativeMoveInDecision(Desktop,
                        moveIn != null && moveIn.FamilyGUIDs.Length > 0,
                        // R247 (r247-tut-lifecycle §G): HouseInfo+0x14 — the
                        // FIRST guard — is the house file's SIMI global 58.
                        // The literal false kept the R197 refusal permanently
                        // inert; the engine's IsTutorialHouse reads the same
                        // source. API missing → false (the legacy contract).
                        Simitone.Client.Utils.TutorialEngine247.IsTutorialHouse((short)house),
                        zoning > 0,
                        moveIn != null && moveIn.Budget >= price,
                        occupied, houseBuilt);
                    if (outcome == NativeMoveInOutcome.ExitMoveInMode)
                    {
                        MoveInFamily = null;
                        return;
                    }
                    if (outcome == NativeMoveInOutcome.TouchFallback)
                    {
                        UIMobileAlert confirmDialog = null;
                        confirmDialog = new UIMobileAlert(new UIAlertOptions()
                        {
                            Title = GameFacade.Strings.GetString("132", "0"),
                            Message = GameFacade.Strings.GetString("132", "1"),
                            Buttons = UIAlertButton.YesNo((b) =>
                            {
                                confirmDialog.Close();
                                MoveInAndPlay((short)house, MoveInFamily.Value, switcher);
                            },
                            (b) => confirmDialog.Close())
                        });
                        UIScreen.GlobalShowDialog(confirmDialog, true);
                        return;
                    }
                    UIMobileAlert nativeDialog = null;
                    var titleIdx = NativeMoveInTitleIndex(outcome);
                    var isConfirm = NativeMoveInIsConfirm(outcome);
                    FSO.Client.UI.Controls.UIAlertButton[] buttons;
                    if (isConfirm)
                    {
                        buttons = UIAlertButton.YesNo((b) =>
                        {
                            nativeDialog.Close();
                            MoveInAndPlay((short)house, MoveInFamily.Value, switcher);
                        },
                        (b) => nativeDialog.Close());
                    }
                    else
                    {
                        buttons = UIAlertButton.Ok((b) => nativeDialog.Close());
                    }
                    nativeDialog = new UIMobileAlert(new UIAlertOptions()
                    {
                        Title = GameFacade.Strings.GetString("132", titleIdx.ToString()),
                        Message = GameFacade.Strings.GetString("132", (titleIdx + 1).ToString()),
                        Buttons = buttons
                    });
                    UIScreen.GlobalShowDialog(nativeDialog, true);
                }
                else
                {
                    var occupied = Content.Get().Neighborhood.GetFamilyForHouse((short)house) != null;
                    if (NativeDesktopEmptyLotRequiresDialog(Desktop, neighborhoodMode,
                        MoveInFamily != null, house, occupied))
                    {
                        // cWinNeighborhoodVC and the private Creepy Hollow lots
                        // refuse an empty regular-mode lot with STR#133 and one
                        // OK button. Old Town and all public destination lots
                        // dispatch directly, even when no family owns the lot.
                        UIMobileAlert emptyDialog = null;
                        emptyDialog = new UIMobileAlert(new UIAlertOptions()
                        {
                            Title = GameFacade.Strings.GetString("133", "0"),
                            Message = GameFacade.Strings.GetString("133", "1"),
                            Buttons = UIAlertButton.Ok((b) => emptyDialog.Close())
                        });
                        UIScreen.GlobalShowDialog(emptyDialog, true);
                        return;
                    }
                    PlayHouse((short)house, switcher);
                }
            };
            Add(TS1NeighPanel);
            Add(switcher);
            TS1NeighSwitcher = switcher;
            WireArmedLotClicks(TS1NeighPanel, switcher);   // UI-30: armed lot clicks -> the EvictMode branch law
        }

        public static bool NativeDesktopEmptyLotRequiresDialog(bool desktop,
            ushort neighborhoodMode, bool movingFamily, int house, bool occupied)
        {
            if (!desktop || movingFamily || occupied) return false;
            return neighborhoodMode == 1
                || (neighborhoodMode == 7 && house >= 90 && house <= 92);
        }

        public void PlayHouse(short house, UIElement switcher)
        {
            ActiveFamily = Content.Get().Neighborhood.GetFamilyForHouse((short)house);
            CurrentNeighborhoodMode = GetNeighborhoodModeFromHouse(house);
            InitializeLot(Content.Get().Neighborhood.GetHousePath(house), false);// "UserData/Houses/House21.iff"
            Remove(TS1NeighPanel);
            if (switcher != null) Remove(switcher);
            // R141: entry paths that hand us a null switcher (the autotest harness)
            // used to leave the neighborhood switcher mounted in-lot — five 96x96
            // edge buttons bleeding over the lot view (survey uisurvey-ucp evidence).
            if (TS1NeighSwitcher != null) { Remove(TS1NeighSwitcher); TS1NeighSwitcher = null; }
            var kids = GetChildren();
            if (kids != null)
                foreach (var child in kids.Where(c => c is UINeighbourhoodSwitcher).ToList()) Remove(child);
            // UI-30: the credits overlay and the armed modes belong to the
            // neighborhood screen — neither survives a lot load (NBR-05 adds
            // the rezone twin to the disarm law).
            CloseCreditsScreen();
            if (switcher is UINeighbourhoodSwitcher nav)
            {
                nav.SetBulldozeArmed(false);
                nav.SetRezoneArmed(false);
            }
        }

        public void MoveInAndPlay(short house, int family, UIElement switcher)
        {
            MoveInFamily = null;
            var neigh = Content.Get().Neighborhood;
            var fami = neigh.GetFamily((ushort)family);
            neigh.SetFamilyForHouse(house, fami, true);
            PlayHouse(house, switcher);
        }

        public void EvictLot(FAMI family, short houseID)
        {
            // AUD-17 E-3: this touch-card path used the in-memory-only MoveOut
            // overload — the eviction silently rolled back if the player quit
            // from the neighborhood screen (no save), and the lot tile was
            // never repainted. Route through the armed executor so one law
            // owns evict+save+repaint (killSims=false preserves the card's
            // family-returns-to-bin semantics; the refund MoveOut already
            // performs internally subsumes the old manual Budget lines).
            ArmedEvict(houseID, family, false);
            TS1NeighPanel.SelectHouse(houseID);
        }

        public override void GameResized()
        {
            base.GameResized();
            Bg.Position = (new Vector2(ScreenWidth, ScreenHeight)) / 2;
            World?.GameResized();
        }

        public void Initialize(string propertyName, bool external)
        {
            GameFacade.CurrentCityName = propertyName;
            ZoomLevel = 1; //screen always starts at near zoom
            InitializeLot(propertyName, external);
        }

        private int SwitchLot = -1;

        public void ChangeSpeedTo(int speed)
        {
            //0 speed is 0x
            //1 speed is 1x
            //2 speed is 3x
            //3 speed is 10x

            if (vm == null) return;
            if (vm.SpeedMultiplier == -1) return;

            switch (vm.SpeedMultiplier)
            {
                case 0:
                    switch (speed)
                    {
                        case 1:
                            HITVM.Get().PlaySoundEvent(UISounds.SpeedPTo1); break;
                        case 2:
                            HITVM.Get().PlaySoundEvent(UISounds.SpeedPTo2); break;
                        case 3:
                            HITVM.Get().PlaySoundEvent(UISounds.SpeedPTo3); break;
                    }
                    break;
                case 1:
                    switch (speed)
                    {
                        case 0:
                            HITVM.Get().PlaySoundEvent(UISounds.Speed1ToP); break;
                        case 2:
                            HITVM.Get().PlaySoundEvent(UISounds.Speed1To2); break;
                        case 3:
                            HITVM.Get().PlaySoundEvent(UISounds.Speed1To3); break;
                    }
                    break;
                case 3:
                    switch (speed)
                    {
                        case 0:
                            HITVM.Get().PlaySoundEvent(UISounds.Speed2ToP); break;
                        case 1:
                            HITVM.Get().PlaySoundEvent(UISounds.Speed2To1); break;
                        case 3:
                            HITVM.Get().PlaySoundEvent(UISounds.Speed2To3); break;
                    }
                    break;
                case 10:
                    switch (speed)
                    {
                        case 0:
                            HITVM.Get().PlaySoundEvent(UISounds.Speed3ToP); break;
                        case 1:
                            HITVM.Get().PlaySoundEvent(UISounds.Speed3To1); break;
                        case 2:
                            HITVM.Get().PlaySoundEvent(UISounds.Speed3To2); break;
                    }
                    break;
            }

            switch (speed)
            {
                case 4: vm.SpeedMultiplier = 0; vm.PauseButtonPark = true; break;
                case 0: vm.SpeedMultiplier = 0; vm.PauseButtonPark = true; break;
                case 1: vm.SpeedMultiplier = 1; vm.PauseButtonPark = false; break;
                case 2: vm.SpeedMultiplier = 3; vm.PauseButtonPark = false; break;
                case 3: vm.SpeedMultiplier = 10; vm.PauseButtonPark = false; break;
            }
            vm.ResetTickAlign();
        }

        public override void Update(FSO.Common.Rendering.Framework.Model.UpdateState state)
        {
            // Tutorial dialog keys precede world hotkeys (Space selects a Sim
            // in the frontend). Only the topmost visible dialog can consume.
            GetChildren().OfType<UIMobileAlert>().LastOrDefault(x => x.Visible)?.HandleTutorialKeys(state);
            GameFacade.Game.IsFixedTimeStep = (vm == null || vm.Ready);
            
            Visible = World?.Visible != false && World?.State.Cameras.HideUI != true;
            GameFacade.Game.IsMouseVisible = Visible;

            var nofocus = state.InputManager.GetFocus() == null;
            // AUD-17 E-9: NewKeys broadcast to every child — with a modal
            // dialog up (quit confirm, move-in, switch-fail, budget...) the
            // speed keys and F12 still fired and the frontend's Space handler
            // still ran. Native modal windows swallow the keyboard; only the
            // topmost visible dialog gets keys (its own handlers decide).
            var modalKeys = GameFacade.Screens.TopVisibleDialog;
            if (nofocus && modalKeys == null && state.NewKeys.Contains(Keys.D1)) ChangeSpeedTo(1);
            if (nofocus && modalKeys == null && state.NewKeys.Contains(Keys.D2)) ChangeSpeedTo(2);
            if (nofocus && modalKeys == null && state.NewKeys.Contains(Keys.D3)) ChangeSpeedTo(3);
            if (nofocus && modalKeys == null && state.NewKeys.Contains(Keys.P)) ChangeSpeedTo(0);
            if (nofocus && modalKeys == null && state.NewKeys.Contains(Keys.D0))
            {
                //frame advance
                ChangeSpeedTo(1);
                GameThread.NextUpdate((FSO.Common.Rendering.Framework.Model.UpdateState ustate) => ChangeSpeedTo(0));
            }
            base.Update(state);

            // R137: periodic live arch-value refresh (objects change value
            // without touching walls; wall changes fire WallsChanged)
            if ((ArchValueFrame++ % 600) == 0) RefreshArchValue();

            // R247: the neighborhood-screen import poll (1 s cadence, frontmost only)
            TickTutorialImportPoll();

            // Update weather effects (sounds and terrain)
            UpdateWeatherEffects();

            if (state.NewKeys.Contains(Microsoft.Xna.Framework.Input.Keys.F12) && modalKeys == null
                && GraphicsModeControl.Mode != GlobalGraphicsMode.Full2D)
            {
                GraphicsModeControl.ChangeMode((GraphicsModeControl.Mode == GlobalGraphicsMode.Full3D) ? GlobalGraphicsMode.Hybrid2D : GlobalGraphicsMode.Full3D);
            }

            /*
            if (state.NewKeys.Contains(Keys.F12))
            {
                ChangeSpeedTo(1);
                //running 10000 ticks
                var timer = new System.Diagnostics.Stopwatch();
                timer.Start();

                for (int i=0; i<10000; i++)
                {
                    vm.Tick();
                }

                timer.Stop();
                UIScreen.GlobalShowDialog(new UIMobileAlert(new UIAlertOptions() {
                    Title = "Benchmark",
                    Message = "10000 ticks took " + timer.ElapsedMilliseconds + "ms."
                }), true);
            }
            */

            if (World != null)
            {
                //stub smooth zoom?
            }

            if (SwitchLot > 0)
            {
                // AUD-17 E-10: ExitLot can run in the same frame between a
                // pending engine lot-switch request and this Update — vm is
                // gone and ActiveFamily may be null (family-less lot). Drop
                // the stale switch instead of NRE-ing the loop.
                if (vm == null || ActiveFamily == null)
                {
                    SwitchLot = -1;
                }
                else
                {
                    // TRV-05 (native cSimsApp::LoadGame 0x1024f7f0, trip-flag
                    // +0x109 path; and Neighborhood::RemoveFromVacation
                    // 0xa83e0): the vacation booking lives on the FAMILY
                    // record (native Family+0x13C = FAMI.VacationHouseNumber).
                    // On arrival at a Vacation Island rental (40..49) the
                    // native writes family->0x13c = lot and IMMEDIATELY saves
                    // the neighborhood; on the return home it clears the field
                    // and saves again. The port mirrors both edges of the trip
                    // here, at the single choke point every travel lot switch
                    // funnels through (mode 17 -> SignalLotSwitch ->
                    // VMLotSwitch -> SwitchLot).
                    try
                    {
                        var isVacationDestination = SwitchLot >= 40 && SwitchLot < 50;
                        if (isVacationDestination && ActiveFamily.VacationHouseNumber != SwitchLot)
                        {
                            ActiveFamily.VacationHouseNumber = SwitchLot;
                            Content.Get().Neighborhood.SaveNeighbourhood(false);
                        }
                        else if (!isVacationDestination && ActiveFamily.VacationHouseNumber != 0)
                        {
                            ActiveFamily.VacationHouseNumber = 0;
                            Content.Get().Neighborhood.SaveNeighbourhood(false);
                            // TRV-06 residual lane: the carry-home law. The native return
                            // (RemoveFromVacation 0x100a83e0) broadcasts to the ObjectModule,
                            // through which the 'Spawn Vacation Purchases' umbrella runs as a
                            // REAL queued interaction per token-carrying member (proven by the
                            // tree itself: 4104 ins1 op-50 change_action_string needs a queue
                            // item; 'do the drop' 313 ins8 goto_routing_slot needs a walkable
                            // thread — both impossible in check-context sync drives, which is
                            // why probe-driven spawns destructively unwound at 313@31). The
                            // port's representation of the native push: a deferred flag
                            // consumed once the home lot's family avatars are live.
                            PendingVacationCarryHome = true;
                            // TRV-06b lane B: same edge — the native arrival clears the
                            // trip residue (ClearHistory/ClearRouteHistory); the persisted
                            // transit action re-runs parked in 281 otherwise. The budget
                            // spans the whole arrival window: the transit's resume-and-
                            // park timing varies by run (lb4 parked <360f, lb5 >1200f).
                            StaleTransitClearBudget = 2400;
                        }
                    }
                    catch (Exception tripEx)
                    {
                        GameLog.Write("trv05 booking edge: " + tripEx.GetType().Name + " " + tripEx.Message);
                    }
                    if (!Downtown) SavedLot = vm.Save();
                    if (SwitchLot == ActiveFamily.HouseNumber && SavedLot != null)
                    {
                        Downtown = false;
                        // cab-chain-decode: the DOWNTOWN/away return parks the
                        // same 281 transit (callee 0xA6F31853 row-2/row-4 trees
                        // park before sims_call mode 17) — arm the completion
                        // scan at EVERY home return, not just the vacation edge.
                        StaleTransitClearBudget = 2400;
                        InitializeLot(SavedLot);
                        SavedLot = null;
                    }
                    else
                    {
                        Downtown = true;
                        InitializeLot(Content.Get().Neighborhood.GetHousePath(SwitchLot), false);
                    }
                    SwitchLot = -1;
                }
            }
            //vm.Context.Clock.Hours = 12;
            if (vm != null) vm.Update();

            // TRV-06 residual lane: the deferred carry-home push — once the home
            // lot is live and the family avatars materialized, enqueue the real
            // 'Spawn Vacation Purchases' interaction on each token-carrying member.
            if ((PendingVacationCarryHome || StaleTransitClearBudget > 0) && vm != null && ActiveFamily != null && !Downtown)
            {
                var famGuids = new HashSet<uint>(ActiveFamily.FamilyGUIDs ?? new uint[0]);
                var members = vm.Context.ObjectQueries.Avatars
                    .Where(a => a != null && a.Object?.OBJ != null && famGuids.Contains(a.Object.OBJ.GUID))
                    .ToList();
                if (members.Count > 0)
                {
                    // Lane B FIRST (the umbrella queues behind the transit): complete
                    // the stale return-transit action — the native arrival discards
                    // the outbound trip's residual execution (RemoveFromVacation
                    // ClearHistory/ClearRouteHistory @0x100a83e0); the port's
                    // engine-funnel lot switch bypasses that completion, leaving the
                    // persisted transit action parked in global 281 'Wait For Notify'
                    // forever (4/4 reproduction starving the carry-home walks —
                    // trv06b receipt). Player-visible the transit is DONE: the family
                    // is home. Cancel the parked action (the player-cancel law).
                    // Retry over a short window: the transit may not have resumed
                    // (and parked at 281) at the exact frame the avatars materialize.
                    if (StaleTransitClearBudget > 0)
                    {
                        StaleTransitClearBudget--;
                        if (CompleteStaleTransit(members)) StaleTransitClearBudget = 0;
                        else if (StaleTransitClearBudget == 0)
                            GameLog.Write("trv06b arrival: no stale transit found in the arrival window (none parked at 281 — nothing to complete)");
                    }
                    if (PendingVacationCarryHome)
                    {
                        PendingVacationCarryHome = false;
                        TryPushVacationCarryHome(members);
                    }
                }
            }

            //SaveHouseButton_OnButtonClick(null);
        }

        /// <summary>
        /// TRV-06b lane B: complete the stale return-transit at the arrival edge.
        /// The persisted transit queue item re-runs on the home lot parked in
        /// global 281 'Wait For Notify' (the transit's signature frame) with a
        /// route that can never satisfy — starving every later walk (4/4
        /// reproduction; the trv06b receipt). The native arrival completes the
        /// trip by construction (RemoveFromVacation clears the histories); the
        /// port's engine-funnel lot switch bypasses that, so this mirrors it:
        /// cancel the parked transit action (the player-cancel law) on each
        /// member whose active action sits under a global.iff 281 frame.
        /// </summary>
        private bool CompleteStaleTransit(List<VMEntity> members)
        {
            var cancelled = false;
            try
            {
                foreach (var memberEnt in members)
                {
                    var member = memberEnt as VMAvatar;
                    if (member?.Thread == null) continue;
                    var act = member.Thread.ActiveAction;
                    if (act == null) continue;
                    var parkedInTransitWait = false;
                    foreach (var fr in member.Thread.Stack)
                    {
                        var parent = fr.Routine?.Chunk?.ChunkParent?.Filename;
                        if (fr.Routine?.Chunk?.ChunkID == 281 && parent != null
                            && parent.IndexOf("global", StringComparison.OrdinalIgnoreCase) >= 0)
                        { parkedInTransitWait = true; break; }
                    }
                    // review P2-1: the structural 281-park alone is too broad (any
                    // notify-waiting action in the arrival window would cancel) —
                    // require the action's callee to be a TRAVEL PLUGIN (the
                    // transit interactions' owner) as well. cab-chain-decode
                    // (2026-10-09): ALL FIVE travel plugins park their persisted
                    // transit in global.iff 281 before sims_call mode 17 —
                    // downtown 0xA6F31853, vacation 0xABA9DF4A, community
                    // 0xEAA79D86, studio 0xC61F8102, magic 0x99197314 (the
                    // 281-frame requirement safely excludes the magic portals,
                    // which mode-17 with NO park). Natively no BHAV frame
                    // survives a lot switch at all (LoadHouse clears histories
                    // on every load); the residue is a port construction.
                    if (!parkedInTransitWait) continue;
                    var calleeGuid = act.Callee?.Object?.OBJ?.GUID ?? 0;
                    if (!(calleeGuid == 0xA6F31853u || calleeGuid == 0xABA9DF4Au
                        || calleeGuid == 0xEAA79D86u || calleeGuid == 0xC61F8102u
                        || calleeGuid == 0x99197314u)) continue;
                    member.Thread.CancelAction(act.UID);
                    cancelled = true;
                    GameLog.Write("trv06b arrival: completed the stale return-transit '"
                        + (act.Name ?? "?") + "' on member oid=" + member.ObjectID
                        + " (native arrival clears the trip residue; the family is home)");
                }
            }
            catch (Exception transitEx)
            {
                GameLog.Write("trv06b arrival transit clear failed: " + transitEx.GetType().Name + " " + transitEx.Message);
            }
            return cancelled;
        }

        /// <summary>
        /// TRV-06 residual lane: enqueue the plugin's 'Spawn Vacation Purchases'
        /// umbrella (VacationPhonePlugin 4104) as a REAL queued action on every
        /// member whose inventory carries purchase tokens (type-5 base or type-6
        /// variant). The interaction's own FindToken gates decide which spawn
        /// trees run (vacation souvenirs; the other packs' spawns are Global[20]
        /// flag-gated inside 4104). CheckRoutine is deliberately null: the engine
        /// push already knows purchases exist — the TTAB TEST (4119) gates
        /// pie-menu availability, not engine-initiated returns. Review note: the
        /// deferred flag is memory-only — a quit between the return edge and
        /// avatar materialization loses that return's push (tokens survive in
        /// inventory; the next vacation return re-runs the umbrella), and the
        /// flag can survive a Downtown detour to fire on the next home entry.
        /// </summary>
        private void TryPushVacationCarryHome(List<VMEntity> members)
        {
            try
            {
                var plugin = vm.Entities.FirstOrDefault(e => e?.Object?.OBJ != null && e.Object.OBJ.GUID == 0xABA9DF4Au);
                if (plugin == null)
                {
                    // review P3: never enqueue with a null Callee — a saved action with a
                    // dead callee NREs the TS1 activator's queue-name recovery at load.
                    GameLog.Write("trv06 carry-home: phone plugin entity not on lot — push skipped");
                    return;
                }
                var tree = plugin.GetRoutineWithOwner(4104, vm.Context);
                if (tree?.routine == null)
                {
                    var owner = Content.Get().WorldObjects.Get(0xABA9DF4Au);
                    var routine = owner?.Resource?.GetRoutine(4104) as VMRoutine;
                    if (routine != null && owner != null) tree = new VMBHAVOwnerPair(routine, owner);
                }
                if (tree?.routine == null)
                {
                    GameLog.Write("trv06 carry-home: plugin routine 4104 unresolved — push skipped");
                    return;
                }
                var pushed = 0;
                foreach (var memberEnt in members)
                {
                    var member = memberEnt as VMAvatar;
                    if (member == null) continue;
                    var nid = member.GetPersonData(VMPersonDataVariable.NeighborId);
                    var inv = Content.Get().Neighborhood.GetInventoryByNID(nid);
                    if (inv == null || !inv.Any(x => x.Type == 5 || x.Type == 6)) continue;
                    var action = new VMQueuedAction
                    {
                        Callee = plugin,
                        IconOwner = plugin,
                        CodeOwner = tree.owner,
                        ActionRoutine = (VMRoutine)tree.routine,
                        StackObject = member,
                        Name = "Spawn Vacation Purchases",
                        Priority = (short)VMQueuePriority.Maximum
                    };
                    action.Flags |= FSO.Files.Formats.IFF.Chunks.TTABFlags.FSOSkipPermissions;
                    member.Thread.EnqueueAction(action);
                    pushed++;
                }
            }
            catch (Exception carryEx)
            {
                GameLog.Write("trv06 carry-home push failed: " + carryEx.GetType().Name + " " + carryEx.Message);
            }
        }

        private void UpdateWeatherEffects()
        {
            // Apply deferred weather state once Blueprint is available
            if (PendingWeatherData.HasValue && vm?.Context?.Blueprint?.Weather != null)
            {
                vm.Context.Blueprint.Weather.SetWeather(PendingWeatherData.Value);
                PendingWeatherData = null;
            }

            if (vm?.Context?.Blueprint?.Weather == null) return;

            var weather = vm.Context.Blueprint.Weather;
            var currentType = weather.WeatherType;
            var currentThunder = weather.IsThunder;
            var currentIntensity = weather.WeatherIntensity;

            // Thunder timer runs every frame regardless of state changes
            if (LastThunder && LastWeatherType == WeatherType.Rain && LastSoundIntensity > 0.1f && vm?.SpeedMultiplier > 0)
            {
                ThunderTimer -= 1f / 60f;
                if (ThunderTimer <= 0)
                {
                    WeatherSounds.PlayThunder(LastSoundIntensity * 0.8f);
                    ThunderTimer = 5f + (float)ThunderRandom.NextDouble() * 10f;
                }
            }

            // Pause/resume rain sound to match game speed
            // SpeedMultiplier is 0 when explicitly paused, -1 in build/buy/options mode
            if (vm.SpeedMultiplier <= 0)
                WeatherSounds.PauseRain();
            else
                WeatherSounds.ResumeRain();

            bool stateChanged = currentType != LastWeatherType ||
                              currentThunder != LastThunder ||
                              Math.Abs(currentIntensity - LastSoundIntensity) > 0.05f;

            if (!stateChanged) return;

            if (currentType == WeatherType.Rain && currentIntensity > 0.1f)
                WeatherSounds.PlayRain(currentIntensity);
            else
            {
                WeatherSounds.StopRain();
                ThunderTimer = 0f;
            }

            if (vm?.Context?.Blueprint?.Terrain != null)
            {
                if (currentType == WeatherType.Snow && currentIntensity > 0.1f)
                {
                    // if it's snowing, make the grass snowy
                    if (!TerrainSnowApplied)
                    {
                        var terrain = vm.Context.Blueprint.Terrain;
                        terrain.ForceSnow(0);
                        terrain.UpdateLotType();
                        terrain.TerrainDirty = true;
                        TerrainSnowApplied = true;
                    }
                }
                else
                {   // remove it when it stops #TODO, don't do this on snow vacation lots
                    if (TerrainSnowApplied)
                    {
                        var terrain = vm.Context.Blueprint.Terrain;
                        terrain.ForceSnow(1);
                        terrain.UpdateLotType();
                        terrain.TerrainDirty = true;
                        TerrainSnowApplied = false;
                    }
                }
            }

            LastWeatherType = currentType;
            LastThunder = currentThunder;
            LastSoundIntensity = currentIntensity;
        }

        public override void PreDraw(UISpriteBatch batch)
        {
            base.PreDraw(batch);
            vm?.PreDraw();
        }

        public void CleanupLastWorld()
        {
            if (vm == null) return;

            // AUD-17 C2-7: the per-process catalog icon caches (fresh
            // BMP.GetTexture copies, privately owned) are swept at lot exit —
            // the UIBuyBrowsePanel cleanup hook has been deliberately disabled
            // since upstream ("might want to be careful here") because panels
            // share it; the lot boundary is the safe edge. (UIIconCache head
            // textures are intentionally kept — tiny, neighborhood-bounded,
            // and now shared by the live button.)
            Simitone.Client.UI.Panels.LiveSubpanels.Catalog.UICatalogItem.ClearIconCache();
            Simitone.Client.UI.Panels.UIOriginalCatalogCell.ClearIconCache();

            //clear our cache too, if the setting lets us do that
            TimedReferenceController.Clear();
            TimedReferenceController.Clear();

            if (ZoomLevel < 4) ZoomLevel = 5;
            WeatherSounds.StopRain();
            LastSoundIntensity = -1;
            LastWeatherType = WeatherType.Rain;
            LastThunder = false;
            vm.Context.Ambience.Kill();
            foreach (var ent in vm.Entities)
            { //stop object sounds
                var threads = ent.SoundThreads;
                for (int i = 0; i < threads.Count; i++)
                {
                    threads[i].Sound.RemoveOwner(ent.ObjectID);
                }
                threads.Clear();
            }
            vm.CloseNet(VMCloseNetReason.LeaveLot);
            GameFacade.Scenes.Remove(World);
            if (SnapshotScene != null) { GameFacade.Scenes.Remove(SnapshotScene); SnapshotScene.Dispose(); SnapshotScene = null; }
            if (WebExportScene != null) { GameFacade.Scenes.Remove(WebExportScene); WebExportScene.Dispose(); WebExportScene = null; } // ENG-27
            if (CameraOverlay != null) { this.Remove(CameraOverlay); CameraOverlay = null; }
            if (PictureInPicture != null) { Remove(PictureInPicture); PictureInPicture.Dispose(); PictureInPicture = null; }
            World.Dispose();
            //LotControl.Dispose();
            this.Remove(LotControl);
            this.Remove(Frontend);
            vm.SuppressBHAVChanges();
            vm = null;
            World = null;
            Driver = null;
            LotControl = null;
        }

        private VMMarshal SavedLot;

        public void InitializeLot()
        {
            CleanupLastWorld();
            World = new FSO.LotView.World(GameFacade.GraphicsDevice);

            World.Opacity = 1;
            GameFacade.Scenes.Add(World);

            var globalLink = new VMTS1GlobalLinkStub();
            Driver = new VMServerDriver(globalLink);

            vm = new VM(new VMContext(World), Driver, new UIHeadlineRendererProvider());
            vm.ListenBHAVChanges();
            vm.Init();

            LastSoundIntensity = -1;
            LastWeatherType = WeatherType.Rain;
            LastThunder = false;
            TerrainSnowApplied = false;
            ThunderTimer = 0f;
            PendingWeatherData = null;

            LotControl = new UILotControl(vm, World);
            this.AddAt(0, LotControl);

            // R190: the camera-mode world surface sits directly above the lot
            // control (and below the frontend) so it consumes world clicks in
            // camera mode only; the snapshot readback hook draws right after
            // the world scene. The family album resets per lot load.
            CameraOverlay = new UIOriginalCameraOverlay(this);
            this.AddAt(1, CameraOverlay);
            SnapshotScene = new OriginalSnapshotCaptureScene(GameFacade.GraphicsDevice);
            GameFacade.Scenes.Add(SnapshotScene);
            WebExportScene = new OriginalWebExportScene(GameFacade.GraphicsDevice); // ENG-27
            GameFacade.Scenes.Add(WebExportScene);
            Simitone.Client.UI.Model.OriginalSnapshotAlbum.ResetForLot(ActiveFamily);

            if (m_ZoomLevel > 3)
            {
                World.Visible = false;
                LotControl.Visible = false;
            }

            ZoomLevel = Math.Max(ZoomLevel, 4);

            if (IDEHook.IDE != null) IDEHook.IDE.StartIDE(vm);

            PictureInPicture = new UIOriginalPictureInPicture(this, World);
            Add(PictureInPicture);

            vm.OnFullRefresh += VMRefreshed;
            //vm.OnEODMessage += LotControl.EODs.OnEODMessage;
            vm.OnRequestLotSwitch += VMLotSwitch;
            vm.OnGenericVMEvent += Vm_OnGenericVMEvent;

            // R137: the ORIGINAL recomputes the architecture value whenever
            // the house changes (cFixedWorld::ComputeArchValue 0x15ea70,
            // decoded r137); the port's FAMI.ValueInArch was file-read only.
            // (WallsChanged is wired lazily in RefreshArchValue — the
            // architecture is not attached yet at InitializeLot time.)
            RefreshArchValue();
        }

        private void OnArchWallsChanged(FSO.SimAntics.VMArchitecture caller)
        {
            RefreshArchValue();
        }

        /// <summary>
        /// R137: recompute the family's architecture value live — the same
        /// formula the save path's UpdateSIMI snapshots into SIMI
        /// (GetArchValue on the decoded engine law + build-mode objects),
        /// mirrored into FAMI.ValueInArch so the neighborhood net-worth
        /// surfaces and the evict payout read a current number.
        /// </summary>
        private bool ArchWired;

        public void RefreshArchValue()
        {
            try
            {
                if (vm == null || vm.Context == null || vm.Context.Architecture == null) return;
                if (!ArchWired)
                {
                    ArchWired = true;
                    vm.Context.Architecture.WallsChanged += OnArchWallsChanged;
                }
                var family = ActiveFamily;
                if (family == null) return;
                var objValue = FSO.SimAntics.Utils.VMArchitectureStats.GetObjectValue(vm);
                int arch = FSO.SimAntics.Utils.VMArchitectureStats.GetArchValue(vm.Context.Architecture)
                    + objValue.Item2;
                family.ValueInArch = arch;
                if (vm.TS1State != null && vm.TS1State.SimulationInfo != null)
                    vm.TS1State.SimulationInfo.ArchitectureValue = arch;
            }
            catch { }
        }

        public void InitializeLot(VMMarshal marshal)
        {
            InitializeLot();
            vm.MyUID = 65537;
            vm.Load(marshal);

            vm.TS1State.ActivateFamily(vm, ActiveFamily);

            var settings = GlobalSettings.Default;
            var myClient = new VMNetClient
            {
                PersistID = 1,
                RemoteIP = "local",
                AvatarState = new VMNetAvatarPersistState()
                {
                    Name = settings.LastUser ?? "",
                    DefaultSuits = new VMAvatarDefaultSuits(settings.DebugGender),
                    BodyOutfit = settings.DebugBody,
                    HeadOutfit = settings.DebugHead,
                    PersistID = 1,
                    SkinTone = (byte)settings.DebugSkin,
                    Gender = (short)(settings.DebugGender ? 1 : 0),
                    Permissions = FSO.SimAntics.Model.TSOPlatform.VMTSOAvatarPermissions.Admin,
                    Budget = 1000000
                }

            };

            var server = (VMServerDriver)Driver;
            server.ConnectClient(myClient);

            GameFacade.Cursor.SetCursor(CursorType.Normal);
            ZoomLevel = 1;

            Frontend = new UISimitoneFrontend(this);
            this.Add(Frontend);
            MountPauseLabel();
            WireCameraOverlayMode();
        }

        private bool CameraModeWired;

        // R204: engine cDDDSimsView::Init creates the pause label once per
        // frontend mount; R214: the label follows the decoded DrawPause law —
        // a steady indicator whose only hide-timer is the one-shot 3000ms
        // TSOnTimerMsg fire on clock-stopping mode entries (see
        // UIOriginalPauseLabel).
        private void MountPauseLabel()
        {
            if (PauseLabel == null)
            {
                try
                {
                    PauseLabel = new Simitone.Client.UI.Controls.UIOriginalPauseLabel(GameFacade.GraphicsDevice);
                    this.Add(PauseLabel);
                }
                catch { /* headless/probe contexts without a device keep the engine surface off, disclosed */ }
            }
            // CPState::SetMode's DrawPause arms ride the mode event; each
            // frontend remount builds a fresh MainPanel, so rewire per mount.
            if (PauseLabel != null && Frontend != null && Frontend.MainPanel != null)
                Frontend.MainPanel.ModeChanged += PauseLabel.OnModeChanged;
        }

        private void WireCameraOverlayMode()
        {
            if (CameraModeWired || Frontend == null || Frontend.MainPanel == null || CameraOverlay == null) return;
            CameraModeWired = true;
            Frontend.MainPanel.ModeChanged += (mode) =>
            {
                CameraOverlay?.SetCameraActive(mode == UI.Panels.UIMainPanelMode.CAMERA);
            };
        }

        public void ShowLoadErrors(List<VMLoadError> errors, bool verbose)
        {
            var errorMsg = GameFacade.Strings.GetString("153", "16");

            if (verbose)
            {
                errorMsg += "\n";
                foreach (var error in errors)
                {
                    errorMsg += "\n" + error.ToString();
                }
            }

            //signal thru the VM so we can stop time appropriately
            vm.LastSpeedMultiplier = vm.SpeedMultiplier;
            vm.SpeedMultiplier = 0;
            vm.SignalDialog(new VMDialogInfo
            {
                Block = true,
                Caller = null,
                Yes = "OK",
                DialogID = 0,
                Title = GameFacade.Strings.GetString("153", "17"),
                Message = errorMsg,
            });

            /*
            CloseAlert = new UIMobileAlert(new FSO.Client.UI.Controls.UIAlertOptions
            {
                Title = GameFacade.Strings.GetString("153", "17"), //missing objects!
                Message = errorMsg,
                Buttons = UIAlertButton.Ok(
                        (b) => { CloseAlert.Close(); CloseAlert = null; }
                        )
            });
            */
        }

        public void InitializeLot(string lotName, bool external)
        {
            if (lotName == "" || lotName[0] == '!') return;
            InitializeLot();
            
            if (!external)
            {
                // TRV-03 (F-TRAVEL-PETS, defect banked in
                // coordination/evidence/TRV-03/ + EXP-09): away loads skipped
                // family activation entirely, so the traveling family's PETS
                // never arrived on the away lot (only the human, via the
                // transit machinery) and a duplicate human instance appeared.
                // Activate the bound family on away loads too when a trip is
                // in progress (LotTransitInfo >= 1): VerifyFamily creates only
                // the MISSING members, so the traveler is not duplicated.
                var tripInProgress = (Content.Get().Neighborhood?.GameState?.LotTransitInfo ?? 0) >= 1;
                if (ActiveFamily != null && (!Downtown || tripInProgress))
                {
                    ActiveFamily.SelectWholeFamily();
                    vm.TS1State.ActivateFamily(vm, ActiveFamily);
                    // away loads lack the home path's activator VerifyFamily
                    // call — without it the traveling pets never spawn (the
                    // TRV-03 gap). Creates only MISSING members. DEFERRED to
                    // the next update: running it synchronously inside
                    // InitializeLot crashes the load (run-3 NRE in Draw).
                    // capture the SCREEN, not the vm: at callback time the
                    // screen's vm is the newly-loaded away lot.
                    var tripScreen = this;
                    GameThread.NextUpdate((state) => {
                        if (tripScreen.vm != null)
                            tripScreen.vm.TS1State.VerifyFamily(tripScreen.vm);
                    });
                }
                BlueprintReset(lotName, null);
                // Read saved weather state now; apply it deferred in UpdateWeatherEffects
                // once Blueprint is available (BlueprintReset queues a command, so Blueprint
                // may still be null when this line runs).
                short pendingWeather = (short)(1 << 8);
                var weatherPath = Path.Combine(FSOEnvironment.UserDir, "LocalHouse/cas.weather");
                if (File.Exists(weatherPath))
                {
                    var bytes = File.ReadAllBytes(weatherPath);
                    if (bytes.Length >= 2) pendingWeather = BitConverter.ToInt16(bytes, 0);
                }
                PendingWeatherData = pendingWeather;

                if (vm.LoadErrors.Count > 0) GameThread.NextUpdate((state) => ShowLoadErrors(vm.LoadErrors, true));

                vm.MyUID = 65537;
                var settings = GlobalSettings.Default;
                var myClient = new VMNetClient
                {
                    PersistID = 1,
                    RemoteIP = "local",
                    AvatarState = new VMNetAvatarPersistState()
                    {
                        Name = settings.LastUser ?? "",
                        DefaultSuits = new VMAvatarDefaultSuits(settings.DebugGender),
                        BodyOutfit = settings.DebugBody,
                        HeadOutfit = settings.DebugHead,
                        PersistID = 1,
                        SkinTone = (byte)settings.DebugSkin,
                        Gender = (short)(settings.DebugGender ? 1 : 0),
                        Permissions = FSO.SimAntics.Model.TSOPlatform.VMTSOAvatarPermissions.Admin,
                        Budget = 1000000
                    }
                };

                if (Downtown)
                {
                    var ngbh = Content.Get().Neighborhood;
                    var crossData = ngbh.GameState;
                    var neigh = ngbh.GetNeighborIDForGUID(crossData.DowntownSimGUID);
                    if (neigh != null) {
                        var inv = ngbh.GetInventoryByNID(neigh.Value);
                        if (inv != null) {
                            var hr = inv.FirstOrDefault(x => x.Type == 2 && x.GUID == 7)?.Count ?? 0;
                            var min = inv.FirstOrDefault(x => x.Type == 2 && x.GUID == 8)?.Count ?? 0;
                            Driver.SendCommand(new VMNetSetTimeCmd()
                            {
                                Hours = hr,
                                Minutes = min,
                            });
                        }
                    }
                }

                var server = (VMServerDriver)Driver;
                server.ConnectClient(myClient);
                LoadSurrounding(short.Parse(lotName.Substring(lotName.Length - 6, 2)));

                GameFacade.Cursor.SetCursor(CursorType.Normal);
                ZoomLevel = 1;
            }

            Frontend = new UISimitoneFrontend(this);
            this.Add(Frontend);
            MountPauseLabel();
            WireCameraOverlayMode();
        }


        public void LoadSurrounding(short houseID)
        {
            return;
            var surrounding = new NBHm(new OBJ(File.OpenRead(@"C:\Users\Rhys\Desktop\fso 2018\nb4.obj")));
            NBHmHouse myH = null;
            var myHeight = vm.Context.Blueprint.InterpAltitude(new Vector3(0, 0, 0));
            if (!surrounding.Houses.TryGetValue(houseID, out myH)) return;
            foreach (var house in surrounding.Houses)
            {
                if (house.Key == houseID) continue;
                var h = house.Value;
                //let's make their lot as a surrounding lot
                var gd = World.State.Device;
                var subworld = World.MakeSubWorld(gd);
                subworld.Initialize(gd);
                var tempVM = new VM(new VMContext(subworld), new VMServerDriver(new VMTSOGlobalLinkStub()), new VMNullHeadlineProvider());
                tempVM.Init();
                BlueprintReset(Content.Get().Neighborhood.GetHousePath(house.Key), tempVM);
                subworld.State.Level = 5;
                var subHeight = tempVM.Context.Blueprint.InterpAltitude(new Vector3(0, 0, 0));
                tempVM.Context.Blueprint.BaseAlt = (int)Math.Round(((subHeight - myHeight) + myH.Position.Y - h.Position.Y) / tempVM.Context.Blueprint.TerrainFactor);
                subworld.UseFade = false;
                subworld.GlobalPosition = new Vector2((myH.Position.X - h.Position.X), (myH.Position.Z - h.Position.Z));

                foreach (var obj in tempVM.Entities)
                {
                    obj.Position = obj.Position;
                }

                vm.Context.Blueprint.SubWorlds.Add(subworld);
            }
            vm.Context.World.InitSubWorlds();
        }

        public void BlueprintReset(string path, VM vm)
        {
            string filename = Path.GetFileName(path);
            bool isSurrounding = true;
            if (vm == null)
            {
                isSurrounding = false;
                vm = this.vm;
            }
            try
            {
                using (var file = new BinaryReader(File.OpenRead(Path.Combine(FSOEnvironment.UserDir, "LocalHouse/") + filename.Substring(0, filename.Length - 4) + ".fsov")))
                {
                    var marshal = new FSO.SimAntics.Marshals.VMMarshal();
                    marshal.Deserialize(file);
                    //vm.SendCommand(new VMStateSyncCmd()
                    //{
                    //    State = marshal
                    //});

                    vm.Load(marshal);
                    vm.Reset();
                }
            }
            catch (Exception)
            {
                var floorClip = Rectangle.Empty;
                var offset = new Point();
                var targetSize = 0;

                var isIff = path.EndsWith(".iff");
                short jobLevel = -1;
                if (isIff) jobLevel = short.Parse(path.Substring(path.Length - 6, 2));
                vm.SendCommand(new VMBlueprintRestoreCmd
                {
                    JobLevel = jobLevel,
                    XMLData = File.ReadAllBytes(path),
                    IffData = isIff,

                    FloorClipX = floorClip.X,
                    FloorClipY = floorClip.Y,
                    FloorClipWidth = floorClip.Width,
                    FloorClipHeight = floorClip.Height,
                    OffsetX = offset.X,
                    OffsetY = offset.Y,
                    TargetSize = targetSize
                });
            }

            var isSimless = (ActiveFamily == null && !isSurrounding);
            // ENG-28b: mark this as the load-time PRIMING pass — the home-lot
            // restricted tick must not fire on it (see VM.PrimingTick; the
            // arrival choreography is calibrated to a non-ticking priming pass).
            // review P2-2: try/finally so a non-BHAV exception out of Tick()
            // can never strand the flag (a stuck-true flag degrades to the
            // consistent full freeze, but must not outlive the call).
            vm.PrimingTick = true;
            try
            {
                vm.SpeedMultiplier = -1;
                vm.Tick();
                vm.SpeedMultiplier = 1;
            }
            finally { vm.PrimingTick = false; }

            if (isSimless)
            {
                vm.SpeedMultiplier = -1;
            }
            vm.SetGlobalValue(32, (short)(isSimless ? 1 : 0));
        }


        private void Vm_OnGenericVMEvent(VMEventType type, object data)
        {
            switch (type)
            {
                case VMEventType.TS1PictureInPicture:
                    PictureInPicture?.Handle((VMTS1PIPEvent)data);
                    break;
                case VMEventType.TS1BuildBuyChange:
                    Frontend?.ModeSwitcher?.UpdateBuildBuy();
                    Frontend?.DesktopUCP?.UpdateBuildBuy();
                    break;
            }
        }

        private void VMLotSwitch(uint lotId)
        {
            vm.SpeedMultiplier = 0;
            vm.PauseButtonPark = false; // transit = the modal full-freeze class
            if ((short)lotId == -1)
            {
                // AUD-17 E-10: the -1 "home lot" sentinel dereferenced
                // ActiveFamily with no guard — a family-less lot has no home.
                if (ActiveFamily == null) return;
                lotId = (uint)ActiveFamily.HouseNumber;
            }
            SwitchLot = (int)lotId;
        }

        private void VMRefreshed()
        {
            if (vm == null) return;
            PictureInPicture?.Close();
            LotControl.ActiveEntity = null;
            LotControl.RefreshCut();
        }

        private void SaveHouseButton_OnButtonClick(UIElement button)
        {
            if (vm == null) return;

            var exporter = new VMWorldExporter();
            Directory.CreateDirectory(Path.Combine(FSOEnvironment.UserDir, "Blueprints/cas.xml"));
            exporter.SaveHouse(vm, Path.Combine(FSOEnvironment.UserDir, "Blueprints/cas.xml"));
            var marshal = vm.Save();
            Directory.CreateDirectory(Path.Combine(FSOEnvironment.UserDir, "LocalHouse/"));
            using (var output = new FileStream(Path.Combine(FSOEnvironment.UserDir, "LocalHouse/cas.fsov"), FileMode.Create))
            {
                marshal.SerializeInto(new BinaryWriter(output));
            }
            var weatherData = vm.Context.Blueprint?.Weather?.WeatherData ?? (short)(1 << 8);
            File.WriteAllBytes(Path.Combine(FSOEnvironment.UserDir, "LocalHouse/cas.weather"), BitConverter.GetBytes(weatherData));
        }

        private UIMobileAlert CloseAlert;
        public override bool CloseAttempt()
        {
            if (CloseAlert != null) return true;

            GameThread.NextUpdate(x =>
            {
                if (CloseAlert == null)
                {
                    var canSave = vm != null;
                    CloseAlert = new UIMobileAlert(new FSO.Client.UI.Controls.UIAlertOptions
                    {
                        Title = GameFacade.Strings.GetString("153", "1"), //quit?
                        Message = GameFacade.Strings.GetString("153", canSave?"6":"2"), //are you sure (2), save before quitting (3)
                        Buttons = 
                        canSave?
                        UIAlertButton.YesNoCancel(
                            (b) => { Save(); GameFacade.Game.Exit(); },
                            (b) => { GameFacade.Game.Exit(); },
                            (b) => { CloseAlert.Close(); CloseAlert = null; }
                            )
                        :
                        UIAlertButton.YesNo(
                            (b) => { GameFacade.Game.Exit(); },
                            (b) => { CloseAlert.Close(); CloseAlert = null; }
                            )
                    });
                    GlobalShowDialog(CloseAlert, true);
                }
            });
            return false;
        }

        public void ReturnToNeighbourhood()
        {
            if (CloseAlert == null)
            {
                CloseAlert = new UIMobileAlert(new FSO.Client.UI.Controls.UIAlertOptions
                {
                    Title = GameFacade.Strings.GetString("153", "3"), //save
                    Message = GameFacade.Strings.GetString("153", "4"), //Do you want to save the game?
                    Buttons =
                    UIAlertButton.YesNoCancel(
                        (b) => { Save(); ExitLot(); CloseAlert.Close(); CloseAlert = null; },
                        (b) => { ExitLot(); CloseAlert.Close(); CloseAlert = null; },
                        (b) => { CloseAlert.Close(); CloseAlert = null; }
                        )
                });
                GlobalShowDialog(CloseAlert, true);
            }
        }

        public void Save()
        {
            //save the house first
            var iff = new IffFile();
            RefreshArchValue(); // R137: ValueInArch mirrors the SIMI snapshot
            vm.TS1State.RefreshSpellBlockFromController(vm); // ENG-12: refresh the FAMI spell block from the live controller BEFORE any serializer touches the family (review P3: was below the marshal — the FSOV copy is block-less by design, so this is belt-and-braces ordering, but the refresh belongs first)
            vm.TS1State.UpdateSIMI(vm);
            var marshal = vm.Save();
            var fsov = new FSOV();
            fsov.ChunkLabel = "Simitone Lot Data";
            fsov.ChunkID = 1;
            fsov.ChunkProcessed = true;
            fsov.ChunkType = "FSOV";
            fsov.AddedByPatch = true;

            using (var stream = new MemoryStream())
            {
                marshal.SerializeInto(new BinaryWriter(stream));
                fsov.Data = stream.ToArray();
            }

            iff.AddChunk(fsov);

            var simi = vm.TS1State.SimulationInfo;
            simi.ChunkProcessed = true;
            simi.AddedByPatch = true;
            iff.AddChunk(simi);

            Texture2D roofless = null;
            var thumb = World.GetLotThumb(GameFacade.GraphicsDevice, (tex) => roofless = FSO.Common.Utils.TextureUtils.Decimate(tex, GameFacade.GraphicsDevice, 2, false));
            thumb = FSO.Common.Utils.TextureUtils.Decimate(thumb, GameFacade.GraphicsDevice, 2, false);

            var tPNG = GeneratePNG(thumb);
            tPNG.ChunkID = 513;
            iff.AddChunk(tPNG);

            var rPNG = GeneratePNG(roofless);
            rPNG.ChunkID = 512;
            iff.AddChunk(rPNG);

            Content.Get().Neighborhood.SaveHouse(vm.GetGlobalValue(10), iff);
            Content.Get().Neighborhood.SaveNeighbourhood(true);
            // ENG-27: the original at-save family web page (the named
            // GetHouseStats consumer, R133) — Options → Export HTML.
            if (GlobalSettings.Default.TS1ExportHTML)
                Simitone.Client.UI.Model.OriginalWebExporter.Export(this);

            // Write weather sidecar alongside the save
            var weatherData = vm.Context.Blueprint?.Weather?.WeatherData ?? (short)(1 << 8);
            Directory.CreateDirectory(Path.Combine(FSOEnvironment.UserDir, "LocalHouse/"));
            File.WriteAllBytes(Path.Combine(FSOEnvironment.UserDir, "LocalHouse/cas.weather"), BitConverter.GetBytes(weatherData));
        }

        public PNG GeneratePNG(Texture2D data)
        {
            var png = new PNG();
            using (var stream = new MemoryStream())
            {
                data.SaveAsPng(stream, data.Width, data.Height);
                png.data = stream.ToArray();
            }

            png.ChunkLabel = "Lot Thumbnail";
            png.ChunkProcessed = true;
            png.ChunkType = "PNG_";
            png.AddedByPatch = true;

            return png;
        }

        public void ExitLot()
        {
            CleanupLastWorld();
            NeighSelection(CurrentNeighborhoodMode);
            Downtown = false;
            SavedLot = null;
        }

        // ---- R247: the neighborhood-screen import poll -----------------------
        // r247-fam-import decode §1.1: cWinNeighborhoodVC runs CheckForNewImports
        // once per second (Init + TSOnTimerMsg's 1000 ms re-arm) while the
        // neighborhood screen is open. The port binds the same cadence in its
        // Update tick, ONLY while the neighborhood screen is frontmost (never
        // in a lot; CAS is a separate screen so its Update never runs here).
        //
        // Return contract (coordinated with the engine): imported → the client
        // rebuilds the neighborhood screen WHEN the import carried a house
        // (native DoNbhdScreen(true) of the auto-import law, gated on the
        // provider's LastImportHouse — decode §1.2 reloadScreen); nothing or
        // error keeps the screen as-is. The native "Couldn't import the
        // family"/"Error" literals are surfaced only through the tri-state
        // (TutorialEngine247 maps 0/1/2). Missing engine API → wired but inert
        // (the cadence calls a no-op, disclosed by TutorialEngine247).
        internal bool TutorialImportPollSuppressed; // R247 battery seam: the battery drives PollNeighborhoodImports() directly
        internal int TutorialImportPollAutoCount; // battery evidence: how many cadence polls actually fired
        private DateTime _lastTutorialImportPoll = DateTime.MinValue;
        // R247 repair F6: the 1 s cadence replays a persistent engine failure
        // every poll, and this dialog would stack a new modal each time (the
        // native MessageDialog had a 1 s timer too, but the port must not
        // stack). Choice: the error dialog shows only on an ok→error
        // TRANSITION and at most once per session — a persistent failure never
        // re-alerts; a recovery followed by a NEW failure episode re-arms it.
        private bool _lastImportPollError;
        private bool _importErrorDialogShown;

        private void TickTutorialImportPoll()
        {
            if (InLot || TS1NeighPanel == null || TutorialImportPollSuppressed) return;
            var now = DateTime.UtcNow;
            if ((now - _lastTutorialImportPoll).TotalMilliseconds < 1000) return;
            _lastTutorialImportPoll = now;
            TutorialImportPollAutoCount++;
            // UI-21 (native §1.2 FileExists gate): the 1 s cadence auto-imports
            // ONLY while Import/Tutorial.FAM is staged — the one file the native's
            // CheckForNewImports auto-imports. Any other staged FAM waits for the
            // explicit Import-button dialog (the CycleThroughImports flow), so the
            // user is never asked about a file the poll already consumed. The
            // ENGINE CheckForNewImports contract stays generic (SAV-05); the
            // batteries drive it directly and are unaffected by this client gate.
            if (!Simitone.Client.Utils.TutorialEngine247.TutorialFamStaged()) return;
            PollNeighborhoodImports();
        }

        /// <summary>One poll step; internal so the battery can drive the native
        /// cadence deterministically. Returns the bridge's result.</summary>
        internal Simitone.Client.Utils.TutorialEngine247.ImportPollResult PollNeighborhoodImports()
        {
            var result = Simitone.Client.Utils.TutorialEngine247.PollImports();
            switch (result)
            {
                case Simitone.Client.Utils.TutorialEngine247.ImportPollResult.Imported:
                    // R248 P2-9 (native §1.2 reloadScreen law): only an import that
                    // carried a HOUSE rebuilds the neighborhood screen (ii.+0x110
                    // != 0 && ii.+0x150 != 0); a family-only import leaves it alone.
                    if (Simitone.Client.Utils.TutorialEngine247.GetLastImportHouse() != 0)
                    {
                        RefreshNeighborhoodScreen(); // native DoNbhdScreen(true)
                    }
                    break;
                case Simitone.Client.Utils.TutorialEngine247.ImportPollResult.Error:
                    {
                        // r247-fam-import §2.3: any import failure shows
                        // MessageDialog("Couldn't import the family", "Error",
                        // style 0). Both literals are static strings in the
                        // original binary (blob 0x6d1c0), safe to pin here.
                        // Debounced (F6): only the ok→error transition of an
                        // episode, and only the first one this session.
                        if (_lastImportPollError || _importErrorDialogShown) break;
                        _importErrorDialogShown = true;
                        UIMobileAlert fail = null;
                        fail = new UIMobileAlert(new FSO.Client.UI.Controls.UIAlertOptions
                        {
                            Title = "Error",
                            Message = "Couldn't import the family",
                            Buttons = FSO.Client.UI.Controls.UIAlertButton.Ok((b) => fail.Close())
                        });
                        GlobalShowDialog(fail, true);
                    }
                    break;
            }
            _lastImportPollError = result == Simitone.Client.Utils.TutorialEngine247.ImportPollResult.Error;
            return result;
        }

    /// <summary>The native DoNbhdScreen(true) equivalence: a full rebuild
    /// of the neighborhood screen on the current mode (in-lot this is the
    /// ExitLot production teardown).</summary>
    public void RefreshNeighborhoodScreen()
    {
        if (InLot) { ExitLot(); return; }
        var mode = CurrentNeighborhoodMode;
        if (TS1NeighPanel != null) Remove(TS1NeighPanel);
        if (TS1NeighSwitcher != null) Remove(TS1NeighSwitcher);
        TS1NeighPanel = null;
        TS1NeighSwitcher = null;
        NeighSelection(mode);
    }

    // ---- UI-21: the generic FAM import dialog (native CycleThroughImports flow)
    // r247-fam-import decode §1.3: the neighborhood import-UI button opens the
    // staged-FAM flow; the dialog presents the FAM's ImportInfo read set and the
    // confirmed action drives the SAME import consumer the poll uses
    // (TutorialEngine247.PollImports → CheckForNewImports → ImportFamFile).
    // All presentation strings are UIText.iff STR# 143 'ImportStrs' (9 English
    // entries, decoded byte-verbatim from game-data — tools/iff-dump/
    // r254-import-ui/uitext-143-168.txt) with the native $family/$lot
    // placeholders substituted literally. Chrome: the existing R142 GenDlg
    // UIMobileAlert (cTSWinMsgBox law) — no new visual language. A failure after
    // confirmation surfaces the native "Couldn't import the family"/"Error"
    // literals (blob 0x6d1c0+0xaf/+0xca) EVERY time — the poll's once-per-session
    // debounce is an auto-poll law (a persistent background failure must not
    // stack dialogs); a user confirming an import is waiting on THIS action.
    internal int ImportDialogOpensForProbe;  // battery evidence: ShowImportDialog calls
    internal int ImportConfirmYesForProbe;   // battery evidence: Yes dispatched the import
    internal int ImportUserErrorDialogsForProbe;
    internal UIMobileAlert _importDialog;    // probe seam: the mounted import dialog

    /// <summary>The neighborhood Import button's flow: describe the first valid
    /// staged FAM (scan order == the poll's first-valid), show the STR# 143
    /// confirmation on the GenDlg chrome. With nothing importable staged this is
    /// a logged no-op — the native import UI is presence-gated (model+0x160).
    /// The auto-poll cannot race the confirmation: since UI-21 the 1 s cadence
    /// only auto-imports a literally-named Tutorial.FAM (decode §1.2 gate).</summary>
    public void ShowImportDialog()
    {
        ImportDialogOpensForProbe++;
        var staged = Simitone.Client.Utils.TutorialEngine247.DescribeImports();
        if (staged.Count == 0)
        {
            GameLog.Write("import: no importable *.FAM staged (import UI presence-gated)");
            return;
        }
        var desc = staged[0];
        CloseImportDialog();

        var msg = ImportStr(0).Replace("$family", desc.FamilyName ?? "");
        msg += "\n\n" + ImportScenarioLine(desc);
        if (desc.MemberNames.Length > 0)
        {
            msg += "\n\n" + ImportStr(5);
            foreach (var name in desc.MemberNames) msg += "\n" + name;
        }

        UIMobileAlert dialog = null;
        dialog = new UIMobileAlert(new FSO.Client.UI.Controls.UIAlertOptions
        {
            Title = ImportStr(8),
            Message = msg,
            Buttons = new FSO.Client.UI.Controls.UIAlertButton[]
            {
                new FSO.Client.UI.Controls.UIAlertButton(FSO.Client.UI.Controls.UIAlertButtonType.Yes)
                {
                    Text = ImportStr(6),
                    Handler = (b) =>
                    {
                        ImportConfirmYesForProbe++;
                        CloseImportDialog();
                        ImportConfirmed();
                    }
                },
                new FSO.Client.UI.Controls.UIAlertButton(FSO.Client.UI.Controls.UIAlertButtonType.No)
                {
                    Text = ImportStr(7),
                    Handler = (b) => CloseImportDialog()
                }
            }
        });
        _importDialog = dialog;
        GlobalShowDialog(dialog, true);
    }

    /// <summary>STR# 143[1]-[4]: the occupancy scenario line, with the native
    /// $family/$lot placeholders substituted. house==0 → bin line [1]; an
    /// occupied lot → the displacement line [2]; a house file with no occupant
    /// → the vacant-house line [4]; otherwise the empty-lot line [3] (which the
    /// import's structural guard will then refuse, surfacing the error dialog).
    /// </summary>
    private static string ImportScenarioLine(Simitone.Client.Utils.TutorialEngine247.ImportFileDto desc)
    {
        string line;
        if (desc.House == 0) line = ImportStr(1);
        else if (desc.OccupantName != null) line = ImportStr(2);
        else if (desc.HouseFileExists) line = ImportStr(4);
        else line = ImportStr(3);
        return line.Replace("$family", desc.OccupantName ?? desc.FamilyName ?? "")
                   .Replace("$lot", desc.House.ToString());
    }

    /// <summary>The confirmed half: run the production import consumer and
    /// apply the same post-import laws as the poll (house import rebuilds the
    /// neighborhood screen; a refusal shows the native literals).</summary>
    private void ImportConfirmed()
    {
        var result = Simitone.Client.Utils.TutorialEngine247.PollImports();
        switch (result)
        {
            case Simitone.Client.Utils.TutorialEngine247.ImportPollResult.Imported:
                if (Simitone.Client.Utils.TutorialEngine247.GetLastImportHouse() != 0)
                {
                    RefreshNeighborhoodScreen(); // native DoNbhdScreen(true)
                }
                break;
            case Simitone.Client.Utils.TutorialEngine247.ImportPollResult.Error:
                ImportUserErrorDialogsForProbe++;
                UIMobileAlert fail = null;
                fail = new UIMobileAlert(new FSO.Client.UI.Controls.UIAlertOptions
                {
                    Title = "Error",
                    Message = "Couldn't import the family",
                    Buttons = FSO.Client.UI.Controls.UIAlertButton.Ok((b) => fail.Close())
                });
                GlobalShowDialog(fail, true);
                break;
        }
    }

    private void CloseImportDialog()
    {
        if (_importDialog != null)
        {
            _importDialog.Close();
            _importDialog = null;
        }
    }

    // STR# 143 'ImportStrs' — the 9 English (lang-1) entries, decoded byte-
    // verbatim from game-data UIText.iff this round (r254-import-ui). Literal
    // fallback per the R159 Tip151 idiom: the table should always be present
    // (ContentStrings.LoadTS1 reads every UIText STR chunk), the literals keep
    // the dialog law-true even if the table ever fails to load.
    private static readonly string[] ImportStrsFallback =
    {
        "A family was found. Do you want to import the $family family into your neighborhood?",
        "They will show up in the family selection screen.",
        "They will displace the $family family in lot number $lot. The old family will show up in the family selection screen.",
        "They will be placed in empty lot number $lot.",
        "They will displace the vacant house on lot number $lot.",
        "The members of the new family are:",
        "Yes",
        "No",
        "Import Family",
    };

    private static string ImportStr(int idx)
    {
        var t = GameFacade.Strings.GetString("143", idx.ToString());
        if (string.IsNullOrEmpty(t) || t.Contains("MISSING")) return ImportStrsFallback[idx];
        return t;
    }

    // ---- UI-30: the neighborhood credits screen (cWinCredits law,
    // coordination/evidence/UI-29/credits-law.md) ----
    // Entry: the navbar Credits button AND the clickable banner (native
    // RegularModeBtnHandler credits case @0x4715dc on every screen; the banner
    // rides the same path via vt+0x98(0x400,0)). The native latches a TOC
    // static so only ONE credits window exists; the overlay covers the whole
    // neighborhood window, has no background art, one shared timeline
    // (+1000 ms first line, +770 ms per line) and exits on ESC/Enter only.
    internal UICreditsScreen _creditsScreen;   // probe seam: the mounted credits overlay
    internal int CreditsOpensForProbe;         // battery evidence: ShowCreditsScreen calls

    /// <summary>The Credits button/banner flow: mount the single cWinCredits
    /// overlay. A second call while mounted is the native latch no-op.</summary>
    public void ShowCreditsScreen()
    {
        CreditsOpensForProbe++;
        if (_creditsScreen != null) return;   // native latch: only one credits window
        var credits = new UICreditsScreen(CloseCreditsScreen);
        _creditsScreen = credits;
        Add(credits);
    }

    internal void CloseCreditsScreen()
    {
        if (_creditsScreen == null) return;
        Remove(_creditsScreen);
        _creditsScreen = null;
    }

    // ---- UI-30: the armed evict/bulldoze lot flow (EvictModeLotHandler
    // 0x471980 law, coordination/evidence/UI-29/bulldoze-law.md) ----
    // The navbar Bulldoze ARMS a mode (UINeighbourhoodSwitcher owns the arm/
    // disarm law); armed lot clicks land here INSTEAD of the normal lot flow:
    //   occupied → confirm 1 ("evict this family?", STR# 131 [2]/[3] chrome)
    //     → if BUILT a second confirm whose ANSWER IS killSims
    //     → MoveOut(houseID, killSims) — the EvictFamily entry calls MoveOut
    //       FIRST (law §3); NBR-02's port MoveOut is the eviction executor.
    //   vacant + built → confirm → BulldozeLot(lot) — NBR-03, the port's
    //     demolition half (the port's MoveOut no-family arm does not demolish).
    //   vacant + unbuilt → two status messages, NO dialog.
    // Success refreshes the lot tile/thumbnail (the native per-item repaint
    // law, vt+0x168) and shows the OK-only dialog. Dialog WORDING beyond the
    // STR# 131 reuse is port literals — the native blob strings were not
    // extracted (bulldoze-law §6 residual, disclosed). The native's busy-
    // cursor/details-popup pre-checks have no port surface (disclosed).
    internal UIMobileAlert _bulldozeDialog;        // probe seam: the mounted confirm
    internal int BulldozeConfirm1ForProbe;         // occupied confirm shown
    internal int BulldozeConfirm2ForProbe;         // occupied+built second confirm shown
    internal int BulldozeVacantBuiltConfirmsForProbe; // vacant+built confirm shown
    internal int BulldozeStatusOnlyForProbe;       // vacant+unbuilt status-only outcomes
    internal int ArmedEvictsForProbe;              // MoveOut calls through the armed path
    internal int ArmedBulldozesForProbe;           // BulldozeLot calls through the armed path
    internal int LotTileRefreshesForProbe;         // post-success repaints

    // NBR-05 probe seams: the armed rezone flow (the STR# 151 [10] 'Evict or
    // Rezone' tool) and the neighborhood switcher's Previous/Next backend.
    internal UIMobileAlert _rezoneDialog;          // probe seam: the mounted rezone confirm
    internal int RezoneConfirmEvictForProbe;       // occupied rezone confirm ([6]/[7]) shown
    internal int RezoneConfirmBulldozeForProbe;    // vacant+built rezone confirm ([8]/[9]) shown
    internal int RezoneDirectForProbe;             // vacant+unbuilt direct rezone attempts
    internal int RezonesForProbe;                  // successful SetZoningType toggles
    internal int RezoneEvictsForProbe;             // MoveOut calls through the rezone cascade
    internal int SwitchAttemptsForProbe;           // Previous/Next switch attempts
    internal int SwitchesForProbe;                 // successful SwitchToNeighborhood + rebuild

    // Port literals (disclosed): the receipts and the switch failure wording
    // are not in any decoded STR set; the rezone cascade confirms reuse the
    // user-approved STR# 131 (r197 decode).
    internal const string BulldozeTitle = "Bulldoze";
    internal const string EvictDoneMessage = "The family has moved out.";
    internal const string BulldozeDoneMessage = "The house has been bulldozed.";
    internal const string RezoneTitle = "Rezone";
    internal const string RezoneDoneMessage = "Lot rezoned.";
    internal const string RezoneFailMessage = "Could not rezone the lot.";
    // NBR-06 (r262/ghidra-n1 decode — cWinNeighborhoodUL::EvictModeLotHandler
    // @0x469760 and RezoneModeLotHandler @0x469160): BOTH bulldoze confirms
    // (the vacant+built arm AND the occupied chain's second confirm) are STR#
    // 131 [0]/[1] "Bulldoze House?"/"Do you want to bulldoze this house?"; the
    // evict-mode vacant+unbuilt leg is STR# 132 [4]/[5] "Nothing to
    // Bulldoze"/"There is no house here to bulldoze." mounted as an OK dialog;
    // the executor failure leg is STR# 131 [10]/[11] "Error"/"Could not
    // evict!". The old port confirm literals are retired. Same fallback law
    // as SwitchFailTitle: a missing table must not blank the chrome.
    internal static string BulldozeHouseTitle
    {
        get
        {
            var s = GameFacade.Strings.GetString("131", "0");
            return (string.IsNullOrEmpty(s) || s.StartsWith("131:")) ? BulldozeTitle : s;
        }
    }
    internal static string BulldozeHouseMessage
    {
        get
        {
            var s = GameFacade.Strings.GetString("131", "1");
            return (string.IsNullOrEmpty(s) || s.StartsWith("131:")) ? "Do you want to bulldoze this house?" : s;
        }
    }
    internal static string NothingToBulldozeTitle
    {
        get
        {
            var s = GameFacade.Strings.GetString("132", "4");
            return (string.IsNullOrEmpty(s) || s.StartsWith("132:")) ? BulldozeTitle : s;
        }
    }
    internal static string NothingToBulldozeMessage
    {
        get
        {
            var s = GameFacade.Strings.GetString("132", "5");
            return (string.IsNullOrEmpty(s) || s.StartsWith("132:")) ? "There is no house here to bulldoze." : s;
        }
    }
    internal static string EvictFailTitle
    {
        get
        {
            var s = GameFacade.Strings.GetString("131", "10");
            return (string.IsNullOrEmpty(s) || s.StartsWith("131:")) ? SwitchFailTitle : s;
        }
    }
    internal static string EvictFailMessage
    {
        get
        {
            var s = GameFacade.Strings.GetString("131", "11");
            return (string.IsNullOrEmpty(s) || s.StartsWith("131:")) ? "Could not evict!" : s;
        }
    }
    // AUD-17 E-6: from STR# 131[10] (the probe pins it == "Error") — keeps
    // localization coherent with the other reused 131 strings.
    internal static string SwitchFailTitle
    {
        get
        {
            var s = GameFacade.Strings.GetString("131", "10");
            return (string.IsNullOrEmpty(s) || s.StartsWith("131:")) ? "Error" : s;
        }
    }
    internal const string SwitchFailMessage = "Could not switch neighborhood.";

    /// <summary>Installs the armed lot-click hook on the mounted neighborhood
    /// panel + switcher (called from NeighSelection where both are live). The
    /// PANEL owns the lot-click routing (SelectHouse), the SWITCHER owns the
    /// armed state.</summary>
    internal void WireArmedLotClicks(UINeighborhoodSelectionPanel panel, UINeighbourhoodSwitcher switcher)
    {
        panel.ArmedLotClick = (house) =>
        {
            if (switcher.BulldozeArmed)
            {
                BulldozeLotClickFlow(house);
                return true;
            }
            if (switcher.RezoneArmed)   // NBR-05: the rezone twin tool
            {
                RezoneLotClickFlow(house);
                return true;
            }
            return false;
        };
    }

    /// <summary>The EvictModeLotHandler branch decision, on the clicked lot.</summary>
    private void BulldozeLotClickFlow(int house)
    {
        var neigh = Content.Get().Neighborhood;
        var family = neigh.GetFamilyForHouse((short)house);
        var simi = neigh.GetHouse(house)?.Get<SIMI>(1);
        // HouseInfo+0x18 proxy: the port's built marker (same gate as move-in).
        bool built = simi != null && (simi.ObjectsValue > 0 || simi.ArchitectureValue > 0);

        if (family != null)
        {
            var familyName = neigh.MainResource.Get<FAMs>(family.ChunkID)?.GetString(0) ?? "selected";
            BulldozeConfirm1ForProbe++;
            UIMobileAlert confirm1 = null;
            confirm1 = new UIMobileAlert(new UIAlertOptions
            {
                Title = GameFacade.Strings.GetString("131", "2"),
                Message = GameFacade.Strings.GetString("131", "3", new string[]
                {
                    familyName,
                    "§" + (family.ValueInArch + family.Budget).ToString("##,#0")
                }),
                Buttons = UIAlertButton.YesNo(
                    (b) =>
                    {
                        confirm1.Close();
                        _bulldozeDialog = null;
                        if (built)
                        {
                            // Confirm 2 (NBR-06): the native second confirm IS
                            // the STR# 131 [0]/[1] bulldoze pair — its answer
                            // is the MoveOut bulldoze flag (0x469760 @
                            // 0x469bf0 region).
                            BulldozeConfirm2ForProbe++;
                            UIMobileAlert confirm2 = null;
                            confirm2 = new UIMobileAlert(new UIAlertOptions
                            {
                                Title = BulldozeHouseTitle,
                                Message = BulldozeHouseMessage,
                                Buttons = UIAlertButton.YesNo(
                                    (b2) => { confirm2.Close(); _bulldozeDialog = null; ArmedEvict(house, family, true); },
                                    (b2) => { confirm2.Close(); _bulldozeDialog = null; ArmedEvict(house, family, false); })
                            });
                            _bulldozeDialog = confirm2;
                            GlobalShowDialog(confirm2, true);
                        }
                        else
                        {
                            ArmedEvict(house, family, false);
                        }
                    },
                    (b) => { confirm1.Close(); _bulldozeDialog = null; })
            });
            _bulldozeDialog = confirm1;
            GlobalShowDialog(confirm1, true);
            return;
        }

        if (built)
        {
            BulldozeVacantBuiltConfirmsForProbe++;
            UIMobileAlert confirm = null;
            confirm = new UIMobileAlert(new UIAlertOptions
            {
                Title = BulldozeHouseTitle,
                Message = BulldozeHouseMessage,
                Buttons = UIAlertButton.YesNo(
                    (b) => { confirm.Close(); _bulldozeDialog = null; ArmedBulldoze(house); },
                    (b) => { confirm.Close(); _bulldozeDialog = null; })
            });
            _bulldozeDialog = confirm;
            GlobalShowDialog(confirm, true);
            return;
        }

        // Vacant + unbuilt: the native mounts the STR# 132 [4]/[5] OK dialog
        // ("Nothing to Bulldoze" / "There is no house here to bulldoze." —
        // NBR-06 decode @0x469a58 region; the old port surfaced logs only).
        BulldozeStatusOnlyForProbe++;
        GameLog.Write("nghbtns: lot " + house + " has nothing to bulldoze");
        UIMobileAlert nothing = null;
        nothing = new UIMobileAlert(new UIAlertOptions
        {
            Title = NothingToBulldozeTitle,
            Message = NothingToBulldozeMessage,
            Buttons = UIAlertButton.Ok((b) => { nothing.Close(); _bulldozeDialog = null; })
        });
        _bulldozeDialog = nothing;
        GlobalShowDialog(nothing, true);
    }

    /// <summary>The EvictFamily executor's occupied arm: MoveOut FIRST (law §3),
    /// then the save, the per-item repaint and the OK dialog on success. The
    /// failure leg mounts the native STR# 131 [10]/[11] "Error"/"Could not
    /// evict!" dialog (NBR-06 decode: EvictFamily @0x2323a0 returns 0 on
    /// failure and the handler answers with 131[0xb]/131[10]).
    /// NBR-06 DEMOLITION LAW: the native EvictFamily(lot, 1) — the confirm-2
    /// YES answer to "Do you want to bulldoze this house?" — deletes the house
    /// FILE (Neighborhood::MoveOut @0xb1800 gates the record purge 0xb1c54 and
    /// the Houses/Import file deletion 0xb1cdc on the SAME r30 bool that gates
    /// the character-file purge). The port expresses that net state through
    /// NBR-03's BulldozeLot (the reviewed SIMI-zeroing demolition primitive)
    /// after the eviction unbinds the lot. This supersedes the UI-29 reading
    /// ("evict never demolishes") — its AutotestUI30 'house-standing' pin is
    /// stale and flagged on the NBR-06 receipt for a coordinator update.</summary>
    private void ArmedEvict(int house, FAMI family, bool killSims)
    {
        var neigh = Content.Get().Neighborhood;
        ArmedEvictsForProbe++;
        if (neigh.MoveOut((short)house, killSims) != 1)
        {
            ShowEvictFail();
            return;   // native nonzero = nothing happened
        }
        neigh.SaveNeighbourhood(false);   // the eviction confirm path owns the save
        if (killSims) neigh.BulldozeLot((short)house);   // the demolition rode the evict (one native call)
        RefreshLotTile(house);
        ShowBulldozeOk(EvictDoneMessage);
    }

    /// <summary>NBR-06: the executor-failure dialog (STR# 131 [10]/[11]).</summary>
    private void ShowEvictFail()
    {
        UIMobileAlert fail = null;
        fail = new UIMobileAlert(new UIAlertOptions
        {
            Title = EvictFailTitle,
            Message = EvictFailMessage,
            Buttons = UIAlertButton.Ok((b) => { fail.Close(); _bulldozeDialog = null; })
        });
        _bulldozeDialog = fail;
        GlobalShowDialog(fail, true);
    }

    /// <summary>The vacant+built arm through NBR-03's BulldozeLot (1 bulldozed /
    /// 0 nothing / -1 occupied-refusal). NBR-06: the nothing leg mounts the
    /// native STR# 132 [4]/[5] OK dialog (the executor's own refusal surface);
    /// the -1 leg stays a log (unreachable through the armed flow — an occupied
    /// click always routes through the evict arm).</summary>
    private void ArmedBulldoze(int house)
    {
        ArmedBulldozeInner(house, null);
    }

    /// <summary>ArmedBulldoze with the NBR-06 rezone chain hook: when the
    /// bulldoze came from the rezone cascade, a successful demolition completes
    /// the pending SetZoningType in the same click (RezoneModeLotHandlerUL
    /// @0x469160 chains EvictFamily/BulldozeLot straight into the rezone).</summary>
    private bool ArmedBulldozeInner(int house, short? rezoneTarget)
    {
        var neigh = Content.Get().Neighborhood;
        ArmedBulldozesForProbe++;
        var result = neigh.BulldozeLot((short)house);
        if (result == 1)
        {
            RefreshLotTile(house);
            if (rezoneTarget != null)
            {
                CompleteChainedRezone(house, rezoneTarget.Value);
                return true;
            }
            ShowBulldozeOk(BulldozeDoneMessage);
            return true;
        }
        else if (result == 0)
        {
            GameLog.Write("nghbtns: lot " + house + " has nothing to bulldoze");
            UIMobileAlert nothing = null;
            nothing = new UIMobileAlert(new UIAlertOptions
            {
                Title = NothingToBulldozeTitle,
                Message = NothingToBulldozeMessage,
                Buttons = UIAlertButton.Ok((b) => { nothing.Close(); _bulldozeDialog = null; })
            });
            _bulldozeDialog = nothing;
            GlobalShowDialog(nothing, true);
        }
        else
        {
            GameLog.Write("nghbtns: lot " + house + " is occupied; evict the family first");
            ShowEvictFail();
        }
        return false;
    }

    // ---- NBR-05: the armed rezone lot flow (the STR# 151 [10] 'Evict or
    // Rezone' tool; the rezone twin of the UI-30 evict/bulldoze arm) ----
    // NBR-06 CHAIN LAW (RezoneModeLotHandlerUL @0x469160, ghidra-n1 decode):
    // armed lot clicks run the STR# 131 'EvictModeStrs' cascade and the
    // rezone COMPLETES IN THE SAME CLICK —
    //   occupied → confirm [6]/[7] "cannot rezone until ... evicted. Proceed
    //     with the eviction?" — YES → (built? second confirm [8]/[9], answer =
    //     the MoveOut bulldoze flag) → MoveOut → SetZoningType(target);
    //     NO = inert.
    //   vacant + built → confirm [8]/[9] "cannot rezone until ... bulldozed.
    //     Proceed with the bulldoze?" — YES = BulldozeLot (NBR-03) →
    //     SetZoningType(target); NO = inert.
    //   vacant + unbuilt → direct SetZoningType toggle (the native's first
    //     dialog offers the zone choice with same-zone picks inert — with the
    //     port's two-zone model that is exactly the toggle; no dialog).
    // Target = the provider's other value (0 residential ↔ 1 community;
    // NBR-03's clamp, absent = residential). The native enum is
    // 1=residential/2=community (SetZoningType @0xaa700 writes the ",
    // community" suffix for 2); the port keeps its 0/1 IFF-derived encoding.
    // Executor failure mounts STR# 131 [10]/[11]. Success refreshes the lot
    // tile; the receipt is a port literal (disclosed).
    internal int HouseRezonedForProbe = -1;        // last successfully rezoned lot
    internal int RezoneChainedConfirmsForProbe;    // occupied+built second confirm ([8]/[9]) shown
    internal int RezoneChainedForProbe;            // auto-SetZoningType completions after evict/bulldoze
    internal void RezoneLotClickFlow(int house)
    {
        var neigh = Content.Get().Neighborhood;
        var family = neigh.GetFamilyForHouse((short)house);
        var simi = neigh.GetHouse(house)?.Get<SIMI>(1);
        // HouseInfo+0x18 proxy: the port's built marker (same gate as move-in
        // and the bulldoze arm).
        bool built = simi != null && (simi.ObjectsValue > 0 || simi.ArchitectureValue > 0);
        // The rezone target is decided up front (the native's zone-choice
        // dialog precedes everything; a same-zone pick is inert).
        var currentZone = neigh.GetZoningType((short)house);
        var target = (short)(currentZone == 1 ? 0 : 1);

        if (family != null)
        {
            var familyName = neigh.MainResource.Get<FAMs>(family.ChunkID)?.GetString(0) ?? "selected";
            RezoneConfirmEvictForProbe++;
            UIMobileAlert confirm = null;
            confirm = new UIMobileAlert(new UIAlertOptions
            {
                Title = GameFacade.Strings.GetString("131", "6"),
                Message = GameFacade.Strings.GetString("131", "7", new string[]
                {
                    familyName,
                    "§" + (family.ValueInArch + family.Budget).ToString("##,#0")
                }),
                Buttons = UIAlertButton.YesNo(
                    (b) =>
                    {
                        confirm.Close(); _rezoneDialog = null;
                        RezoneEvictsForProbe++;
                        if (built)
                        {
                            // NBR-06: the chained second confirm — its answer is
                            // the MoveOut bulldoze flag, then the rezone completes.
                            RezoneChainedConfirmsForProbe++;
                            UIMobileAlert confirm2 = null;
                            confirm2 = new UIMobileAlert(new UIAlertOptions
                            {
                                Title = GameFacade.Strings.GetString("131", "8"),
                                Message = GameFacade.Strings.GetString("131", "9"),
                                Buttons = UIAlertButton.YesNo(
                                    (b2) =>
                                    {
                                        confirm2.Close(); _rezoneDialog = null;
                                        EvictAndCompleteRezone(house, family, true, target);
                                    },
                                    (b2) =>
                                    {
                                        confirm2.Close(); _rezoneDialog = null;
                                        EvictAndCompleteRezone(house, family, false, target);
                                    })
                            });
                            _rezoneDialog = confirm2;
                            GlobalShowDialog(confirm2, true);
                        }
                        else
                        {
                            EvictAndCompleteRezone(house, family, false, target);
                        }
                    },
                    (b) => { confirm.Close(); _rezoneDialog = null; })
            });
            _rezoneDialog = confirm;
            GlobalShowDialog(confirm, true);
            return;
        }

        if (built)
        {
            RezoneConfirmBulldozeForProbe++;
            UIMobileAlert confirm = null;
            confirm = new UIMobileAlert(new UIAlertOptions
            {
                Title = GameFacade.Strings.GetString("131", "8"),
                Message = GameFacade.Strings.GetString("131", "9"),
                Buttons = UIAlertButton.YesNo(
                    (b) => { confirm.Close(); _rezoneDialog = null; ArmedBulldozeInner(house, target); },
                    (b) => { confirm.Close(); _rezoneDialog = null; })
            });
            _rezoneDialog = confirm;
            GlobalShowDialog(confirm, true);
            return;
        }

        // Vacant + unbuilt: the direct rezone. Toggle the zoning (0 ↔ 1); the
        // backend persists LotZoning.iff atomically (NBR-03), so no neighborhood
        // save rides here.
        RezoneDirectForProbe++;
        CompleteChainedRezone(house, target);
    }

    /// <summary>NBR-06: the rezone cascade's eviction executor — MoveOut, the
    /// neighborhood save, the repaint, then the SAME-CLICK rezone completion
    /// (the native chains SetZoningType immediately after a successful
    /// EvictFamily; the NBR-05 single-step-chain disclosure is closed). The
    /// bulldoze-flag YES arm also demolishes the house (EvictFamily(lot, 1)
    /// deletes the house file — see ArmedEvict's NBR-06 demolition note).</summary>
    private void EvictAndCompleteRezone(int house, FAMI family, bool killSims, short target)
    {
        var neigh = Content.Get().Neighborhood;
        if (neigh.MoveOut((short)house, killSims) != 1)
        {
            ShowEvictFail();
            return;   // native nonzero = nothing happened (and no rezone runs)
        }
        neigh.SaveNeighbourhood(false);   // the eviction confirm path owns the save
        if (killSims) neigh.BulldozeLot((short)house);   // the native's bulldoze-flag demolition
        RefreshLotTile(house);
        CompleteChainedRezone(house, target);
    }

    /// <summary>NBR-06: the shared rezone completion — the SetZoningType call,
    /// the receipt dialog and the tile refresh (used by the direct toggle and
    /// both chained arms).</summary>
    private void CompleteChainedRezone(int house, short target)
    {
        var neigh = Content.Get().Neighborhood;
        if (neigh.SetZoningType((short)house, target))
        {
            RezonesForProbe++;
            RezoneChainedForProbe++;
            HouseRezonedForProbe = house;
            RefreshLotTile(house);
            ShowRezoneOk(RezoneDoneMessage);
        }
        else
        {
            GameLog.Write("nghbtns: lot " + house + " could not be rezoned (LotZoning.iff)");
            // AUD-17 E-5 (the open NBR-05 P3): the failed rezone was a log
            // line only — surface the receipt dialog instead of a silent
            // nothing.
            ShowRezoneFail(RezoneFailMessage);
        }
    }

    private void ShowRezoneFail(string message)
    {
        UIMobileAlert ok = null;
        ok = new UIMobileAlert(new UIAlertOptions
        {
            Title = RezoneTitle,
            Message = message,
            Buttons = UIAlertButton.Ok((b) => { ok.Close(); })
        });
        GlobalShowDialog(ok, true);
    }

    private void ShowRezoneOk(string message)
    {
        UIMobileAlert ok = null;
        ok = new UIMobileAlert(new UIAlertOptions
        {
            Title = RezoneTitle,
            Message = message,
            Buttons = UIAlertButton.Ok((b) => { ok.Close(); _rezoneDialog = null; })
        });
        _rezoneDialog = ok;
        GlobalShowDialog(ok, true);
    }

    // ---- NBR-05: Previous/Next neighborhood cycling (NBR-02 backend) ----
    // The native Next/Previous (@0xad170/@0xad1f0) feed SwitchToNewNeighborhood
    // a direction; the port's SwitchToNeighborhood takes the target id, so the
    // wrap/selection order is this UI's concern (undecoded — disclosed): cycle
    // the enumerated available ids in ascending order, wrapping both ways.
    // SwitchToNeighborhood saves the live neighborhood then mounts the target
    // (InitSpecific full reload); the screen rebuilds through the production
    // RefreshNeighborhoodScreen path. A target with no materialized dir AND no
    // template backing fails bounded: OK alert, stay put.
    public void SwitchNeighborhood(int delta)
    {
        SwitchAttemptsForProbe++;
        var neigh = Content.Get().Neighborhood;
        var available = neigh.GetAvailableNeighborhoods();
        if (available.Count < 2)
        {
            GameLog.Write("nghbtns: neighborhood switch requested with "
                + available.Count + " available (single-hood install)");
            return;
        }
        var idx = available.IndexOf(neigh.CurrentNeighborhoodID);
        if (idx < 0) idx = 0;
        var target = available[((idx + delta) % available.Count + available.Count) % available.Count];
        if (!neigh.SwitchToNeighborhood(target))
        {
            ShowSwitchFail();
            return;
        }
        SwitchesForProbe++;
        GameLog.Write("nghbtns: switched to neighborhood " + target);
        RefreshNeighborhoodScreen();
    }

    private void ShowSwitchFail()
    {
        UIMobileAlert ok = null;
        ok = new UIMobileAlert(new UIAlertOptions
        {
            Title = SwitchFailTitle,
            Message = SwitchFailMessage,
            Buttons = UIAlertButton.Ok((b) => ok.Close())
        });
        GlobalShowDialog(ok, true);
    }

    private void RefreshLotTile(int house)
    {
        LotTileRefreshesForProbe++;
        if (TS1NeighPanel != null) TS1NeighPanel.RefreshLotTile(house);
    }

    private void ShowBulldozeOk(string message)
    {
        UIMobileAlert ok = null;
        ok = new UIMobileAlert(new UIAlertOptions
        {
            Title = BulldozeTitle,
            Message = message,
            Buttons = UIAlertButton.Ok((b) => { ok.Close(); _bulldozeDialog = null; })
        });
        _bulldozeDialog = ok;
        GlobalShowDialog(ok, true);
    }
}

    public enum NeighSelectionMode
    {
        Normal,
        MoveIn,
        MoveInMagic
    }
}
