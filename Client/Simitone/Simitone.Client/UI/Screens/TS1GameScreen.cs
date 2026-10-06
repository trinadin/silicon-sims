
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

        public UILotControl LotControl { get; set; }
        public UIOriginalCameraOverlay CameraOverlay { get; set; }
        // R204: the STR# 148 'Paused' blink label (engine DrawPause law) —
        // mounts above the frontend at the default window position (0,0).
        public Simitone.Client.UI.Controls.UIOriginalPauseLabel PauseLabel { get; set; }
        private OriginalSnapshotCaptureScene SnapshotScene;
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
                case 0: vm.SpeedMultiplier = 0; break;
                case 1: vm.SpeedMultiplier = 1; break;
                case 2: vm.SpeedMultiplier = 3; break;
                case 3: vm.SpeedMultiplier = 10; break;
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
            if (nofocus && state.NewKeys.Contains(Keys.D1)) ChangeSpeedTo(1);
            if (nofocus && state.NewKeys.Contains(Keys.D2)) ChangeSpeedTo(2);
            if (nofocus && state.NewKeys.Contains(Keys.D3)) ChangeSpeedTo(3);
            if (nofocus && state.NewKeys.Contains(Keys.P)) ChangeSpeedTo(0);
            if (nofocus && state.NewKeys.Contains(Keys.D0))
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

            if (state.NewKeys.Contains(Microsoft.Xna.Framework.Input.Keys.F12) && GraphicsModeControl.Mode != GlobalGraphicsMode.Full2D)
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
                if (!Downtown) SavedLot = vm.Save();
                if (SwitchLot == ActiveFamily.HouseNumber && SavedLot != null)
                {
                    Downtown = false;
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
            //vm.Context.Clock.Hours = 12;
            if (vm != null) vm.Update();

            //SaveHouseButton_OnButtonClick(null);
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
            vm.SpeedMultiplier = -1;
            vm.Tick();
            vm.SpeedMultiplier = 1;

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
            if ((short)lotId == -1)
            {
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

    // Port literals (disclosed): confirm-2, the rezone receipt and the switch
    // failure wording are not in any decoded STR set; the rezone cascade
    // confirms reuse the user-approved STR# 131 (r197 decode).
    internal const string BulldozeTitle = "Bulldoze";
    internal const string BulldozeConfirmMessage = "Bulldoze this house? The building will be demolished.";
    internal const string BulldozeAlsoMessage = "Do you also want to bulldoze the house and delete the family members?";
    internal const string EvictDoneMessage = "The family has moved out.";
    internal const string BulldozeDoneMessage = "The house has been bulldozed.";
    internal const string RezoneTitle = "Rezone";
    internal const string RezoneDoneMessage = "Lot rezoned.";
    internal const string RezoneFailMessage = "Could not rezone the lot.";
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
                            // Confirm 2: its answer IS the killSims flag (law §2).
                            BulldozeConfirm2ForProbe++;
                            UIMobileAlert confirm2 = null;
                            confirm2 = new UIMobileAlert(new UIAlertOptions
                            {
                                Title = GameFacade.Strings.GetString("131", "2"),
                                Message = BulldozeAlsoMessage,
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
                Title = BulldozeTitle,
                Message = BulldozeConfirmMessage,
                Buttons = UIAlertButton.YesNo(
                    (b) => { confirm.Close(); _bulldozeDialog = null; ArmedBulldoze(house); },
                    (b) => { confirm.Close(); _bulldozeDialog = null; })
            });
            _bulldozeDialog = confirm;
            GlobalShowDialog(confirm, true);
            return;
        }

        // Vacant + unbuilt: two status messages, NO dialog (law §2).
        BulldozeStatusOnlyForProbe++;
        GameLog.Write("nghbtns: lot " + house + " has nothing to bulldoze");
        GameLog.Write("nghbtns: lot " + house + " is already undeveloped");
    }

    /// <summary>The EvictFamily executor's occupied arm: MoveOut FIRST (law §3),
    /// then the save, the per-item repaint and the OK dialog on success.</summary>
    private void ArmedEvict(int house, FAMI family, bool killSims)
    {
        var neigh = Content.Get().Neighborhood;
        ArmedEvictsForProbe++;
        if (neigh.MoveOut((short)house, killSims) != 1) return;   // native nonzero = nothing happened
        neigh.SaveNeighbourhood(false);   // the eviction confirm path owns the save
        RefreshLotTile(house);
        ShowBulldozeOk(EvictDoneMessage);
    }

    /// <summary>The vacant+built arm through NBR-03's BulldozeLot (1 bulldozed /
    /// 0 nothing / -1 occupied-refusal — the refusal and no-op legs surface as
    /// status only, matching the native's ticker messages).</summary>
    private void ArmedBulldoze(int house)
    {
        var neigh = Content.Get().Neighborhood;
        ArmedBulldozesForProbe++;
        var result = neigh.BulldozeLot((short)house);
        if (result == 1)
        {
            RefreshLotTile(house);
            ShowBulldozeOk(BulldozeDoneMessage);
        }
        else if (result == 0)
        {
            GameLog.Write("nghbtns: lot " + house + " has nothing to bulldoze");
        }
        else
        {
            GameLog.Write("nghbtns: lot " + house + " is occupied; evict the family first");
        }
    }

    // ---- NBR-05: the armed rezone lot flow (the STR# 151 [10] 'Evict or
    // Rezone' tool; the rezone twin of the UI-30 evict/bulldoze arm) ----
    // Armed lot clicks land here on the STR# 131 'EvictModeStrs' cascade
    // (r197 decode — the rezone strings live in the evict-mode set):
    //   occupied → confirm [6]/[7] "cannot rezone until ... evicted. Proceed
    //     with the eviction?" — YES = MoveOut(house, killSims:false) (eviction
    //     only; the native chain depth beyond this step is undecoded — the
    //     next armed click re-decides the branch); NO = inert.
    //   vacant + built → confirm [8]/[9] "cannot rezone until ... bulldozed.
    //     Proceed with the bulldoze?" — YES = BulldozeLot (NBR-03); NO = inert.
    //   vacant + unbuilt → direct SetZoningType toggle (nothing blocks the
    //     rezone; no dialog). Target = the provider's other value (0
    //     residential ↔ 1 community; NBR-03's clamp, absent = residential).
    // Success refreshes the lot tile; the receipt is a port literal (disclosed).
    internal int HouseRezonedForProbe = -1;        // last successfully rezoned lot
    internal void RezoneLotClickFlow(int house)
    {
        var neigh = Content.Get().Neighborhood;
        var family = neigh.GetFamilyForHouse((short)house);
        var simi = neigh.GetHouse(house)?.Get<SIMI>(1);
        // HouseInfo+0x18 proxy: the port's built marker (same gate as move-in
        // and the bulldoze arm).
        bool built = simi != null && (simi.ObjectsValue > 0 || simi.ArchitectureValue > 0);

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
                        if (neigh.MoveOut((short)house, false) != 1) return;   // native nonzero = nothing happened
                        neigh.SaveNeighbourhood(false);   // the eviction confirm path owns the save
                        RefreshLotTile(house);
                        ShowRezoneOk(EvictDoneMessage);
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
                    (b) => { confirm.Close(); _rezoneDialog = null; ArmedBulldoze(house); },
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
        var zone = neigh.GetZoningType((short)house);
        var target = (short)(zone == 1 ? 0 : 1);
        if (neigh.SetZoningType((short)house, target))
        {
            RezonesForProbe++;
            HouseRezonedForProbe = house;
            RefreshLotTile(house);
            ShowRezoneOk(RezoneDoneMessage);
        }
        else
        {
            GameLog.Write("nghbtns: lot " + house + " could not be rezoned (LotZoning.iff)");
        }
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
