using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Common.Rendering.Framework.IO;
using FSO.Content.Framework;
using FSO.Content.Model;
using FSO.Files.Formats.IFF.Chunks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FSO.Common.Rendering.Framework.Model;
using FSO.HIT;
using FSO.Client;
using FSO.Content;
using FSO.Common.Utils;
using Simitone.Client.UI.Controls;
using Simitone.Client.UI.Model;
using Simitone.Client.UI.Screens;
using Simitone.Client.Utils;
using FSO.Common;
using System.IO;
using FSO.Files.Formats.IFF;
using FSO.Files.RC;
using FSO.LotView;
using FSO.LotView.Model;
using FSO.SimAntics;
using FSO.SimAntics.Engine.TSOTransaction;
using FSO.SimAntics.Model;
using FSO.SimAntics.NetPlay.Drivers;

namespace Simitone.Client.UI.Panels
{
    public class UINeighborhoodSelectionPanel : UIContainer
    {

        public static NeighborhoodViewConfig[] Neighborhoods = new NeighborhoodViewConfig[]
        {
            new NeighborhoodViewConfig()
            {
                Graphic = "Nbhd\\NScreen.BMP",
                Scale = 1f,
                FullImageAnimations = new NeighborhoodImageAnim[] {new NeighborhoodImageAnim("Nbhd\\DiffN1-N2_8.bmp", "Nbhd\\DiffN1-N3_8.bmp", "Nbhd\\DiffN1-N4_8.bmp") }
            },
            new NeighborhoodViewConfig()
            {
                Graphic = "Downtown\\DScreen.bmp",
                Music = "station_dtnhood",
                Scale = 1f,
                // kDowntownWater0-5, drawn at the ENGINE-LITERAL position (0, 0):
                // cWinDowntown's ctor zeroes the water position fields this+308 (x)
                // and this+312 (y) with li 0 (0x4002a8 -> stores 0x400308/0x400314
                // in The Sims Complete) and TSPaint__12cWinDowntown draws the six
                // water sprites through those fields (0x3fedf4/0x3fede8). R95 scan,
                // tools/iff-dump/r95/.
                FullImageAnimations = new NeighborhoodImageAnim[] {new NeighborhoodImageAnim(new Vector2(0, 0), "Downtown\\dscreen00.bmp", "Downtown\\dscreen01.bmp", "Downtown\\dscreen02.bmp", "Downtown\\dscreen03.bmp", "Downtown\\dscreen04.bmp", "Downtown\\dscreen05.bmp") }
            },
            new NeighborhoodViewConfig()
            {
                Graphic = "VIsland\\visland.bmp",
                Music = "station_vacation",
                Scale = 1f,
                // kVIWater waves, drawn at the ENGINE-LITERAL position (0, 0):
                // cWinVacation's ctor zeroes its water position fields this+316 (x)
                // and this+320 (y) with li 0 (0x442c58 -> stores 0x442cc0/0x442ccc)
                // and TSPaint__12cWinVacation draws the current frame
                // ([this+464] mod 5) through those fields (0x4416d4/0x4416b4).
                // R95 scan, tools/iff-dump/r95/.
                FullImageAnimations = new NeighborhoodImageAnim[] {
                    new NeighborhoodImageAnim(new Vector2(0, 0), "VIsland\\visland_waves001.bmp", "VIsland\\visland_waves002.bmp", "VIsland\\visland_waves003.bmp", "VIsland\\visland_waves004.bmp", "VIsland\\visland_waves005.bmp"),
                },
                // PostChildDraw__12cWinVacation paints these two static bitmaps
                // after the cWinLotBtn children: port first (this+0x130,
                // 0x43e134..0x43e1a4), then trees (this+0x134,
                // 0x43e1ac..0x43e21c). They are not water-animation frames.
                PostLotOverlays = new NeighborhoodImageAnim[] {
                    new NeighborhoodImageAnim("VIsland\\visland_port.bmp"),
                    new NeighborhoodImageAnim("VIsland\\visland_trees.bmp"),
                }
            },
            new NeighborhoodViewConfig()
            {
                Graphic = "Community\\NScreen_unleashed.bmp",
                Scale = 2f,
                Pulsate = false,
                // kNghFrameDelta1-6_UL: the six Unleashed waves frames (R88 uianim pins
                // all six byte-verbatim). R90/R93 engine scans: nessie is a CHEAT
                // EASTER EGG (DoNessie__18cWinNeighborhoodUL, "Usage: nessie. Same as
                // SimCity."), NOT a screen fixture — the R89 always-on family is
                // replaced in R93 by UINeighborhoodNessieLayer, which runs the decoded
                // engine machine: CheatCallback__18cWinNeighborhoodUL (0x4652c0..)
                // matches the typed command against "nessie", refuses with "Sorry
                // only one Nessie at a time." while active, and spawns at the li pair
                // (118, 519) @0x465310; DoNessie (0x462090..0x46273c) surfaces kNess1
                // -> kNess2 -> swims kNess3 left 2px / down 1px per tick until x==60
                // -> dives kNess2 -> kNess1 -> gone (flag cleared). Trigger: the cheat
                // bar (Ctrl+Shift+C) on this panel accepts "nessie".
                FullImageAnimations = new NeighborhoodImageAnim[] {
                    new NeighborhoodImageAnim("Community\\NScreen_unleashed_waves001.bmp", "Community\\NScreen_unleashed_waves002.bmp", "Community\\NScreen_unleashed_waves003.bmp", "Community\\NScreen_unleashed_waves004.bmp", "Community\\NScreen_unleashed_waves005.bmp", "Community\\NScreen_unleashed_waves006.bmp") {
                        // TSPaint__18cWinNeighborhoodUL (0x46b130..0x46b230):
                        // a 12-step counter selects floor(counter/2). Its frequency
                        // gate is 1000/elapsed <= 2*pi; timeGetTime is integer-ms,
                        // so the earliest event is 160 ms. Each image holds two
                        // events (320 ms). SetImage swaps it; there is no alpha blend.
                        Discrete = true,
                        FrameRepeat = UINeighborhoodAnimationLayer.OldTownFrameRepeat,
                        CounterIntervalMilliseconds = UINeighborhoodAnimationLayer.OldTownCounterIntervalMilliseconds,
                    },
                },
                Nessie = new NeighborhoodImageAnim("Community\\ness01.bmp", "Community\\ness02.bmp", "Community\\ness03.bmp"),
                BGSound = "river_loop"
            },
            new NeighborhoodViewConfig()
            {
                Graphic = "Studiotown\\DScreen.bmp",
                Music = "station_superstar",
                Scale = 1f,
                // kStudiotownCar0-19: the twenty ORIGINAL studio-lot car frames (blue-grey
                // sedan 0-13, yellow taxi 14-19; magenta key clears in ImageLoader). R91:
                // these are 20 STATIC car variants, not one cycling animation — the engine
                // picks each car's bitmap ONCE with Random() % 18 (so variants 18/19 are
                // never randomly drawn) and never cycles it. R90/R91 engine scan: the
                // original drives SIX carLaneInfo lanes (cWinStudiotown::InitCars
                // 0x475dd4 — full decode in tools/iff-dump/r91/); the live lane layers
                // below mount at the engine-literal coordinates.
                // DScreen_top_layer.bmp is a loose FAR entry, not a Res_Nbhd.RT
                // resource. The original Init loads only kStudiotownBackground 5321
                // (0x47afd0) plus the car resources; no engine path references or
                // paints the loose top-layer file, so it must not be mounted here.
                CarLanes = new NeighborhoodImageAnim(
                    "Studiotown\\Nhood_superstar_car0.bmp", "Studiotown\\Nhood_superstar_car1.bmp", "Studiotown\\Nhood_superstar_car2.bmp", "Studiotown\\Nhood_superstar_car3.bmp", "Studiotown\\Nhood_superstar_car4.bmp",
                    "Studiotown\\Nhood_superstar_car5.bmp", "Studiotown\\Nhood_superstar_car6.bmp", "Studiotown\\Nhood_superstar_car7.bmp", "Studiotown\\Nhood_superstar_car8.bmp", "Studiotown\\Nhood_superstar_car9.bmp",
                    "Studiotown\\Nhood_superstar_car10.bmp", "Studiotown\\Nhood_superstar_car11.bmp", "Studiotown\\Nhood_superstar_car12.bmp", "Studiotown\\Nhood_superstar_car13.bmp", "Studiotown\\Nhood_superstar_car14.bmp",
                    "Studiotown\\Nhood_superstar_car15.bmp", "Studiotown\\Nhood_superstar_car16.bmp", "Studiotown\\Nhood_superstar_car17.bmp", "Studiotown\\Nhood_superstar_car18.bmp", "Studiotown\\Nhood_superstar_car19.bmp")
            },
            new NeighborhoodViewConfig(),
            new NeighborhoodViewConfig()
            {
                Graphic = "Magicland\\DScreen.bmp",
                Pulsate = false,
                Music = "music_magictown",
                BGSound = "mt_river_loop",
                Scale = 1f,
                // kMagictownWater0-5 waves stay a cycling family (R90 scan: DoWater
                // draws at (0, 62), frame+1 advanced every 5th tick; the R92 scan
                // pinned the mod-6 reset at 0x57c72c — see tools/iff-dump/r92/). The
                // balloon and fog families moved OFF FullImageAnimations in R92: the
                // engine does not cycle either as a frame family —
                //  - fog kMagictownFogA/B/C = the 3-bitmap source for 21 drifting
                //    CloudInfo clouds (InitClouds 0x57c890 four seeding loops,
                //    AnimateClouds 0x57c1d0 drift/wrap logic — ported live in
                //    UINeighborhoodCloudLayer);
                //  - balloon 1-16 = the 16 WIND-STATE bitmaps of ONE BalloonInfo
                //    (AnimateBalloon 0x57c330 wind walk; DrawBalloon 0x57c090 indexes
                //    this+36+frame*4 with frame 0..15 — ported live in
                //    UINeighborhoodBalloonLayer).
                FullImageAnimations = new NeighborhoodImageAnim[] {
                    new NeighborhoodImageAnim(new Vector2(0, 62), "Magicland\\DScreen_waves1.bmp", "Magicland\\DScreen_waves2.bmp", "Magicland\\DScreen_waves3.bmp", "Magicland\\DScreen_waves4.bmp", "Magicland\\DScreen_waves5.bmp", "Magicland\\DScreen_waves6.bmp"),
                },
                Clouds = new NeighborhoodImageAnim(
                    "Magicland\\DScreen_mistA.tga", "Magicland\\DScreen_mistB.tga", "Magicland\\DScreen_mistC.tga"),
                Balloons = new NeighborhoodImageAnim(
                    "Magicland\\Nhood_balloon1.bmp", "Magicland\\Nhood_balloon2.bmp", "Magicland\\Nhood_balloon3.bmp", "Magicland\\Nhood_balloon4.bmp",
                    "Magicland\\Nhood_balloon5.bmp", "Magicland\\Nhood_balloon6.bmp", "Magicland\\Nhood_balloon7.bmp", "Magicland\\Nhood_balloon8.bmp",
                    "Magicland\\Nhood_balloon9.bmp", "Magicland\\Nhood_balloon10.bmp", "Magicland\\Nhood_balloon11.bmp", "Magicland\\Nhood_balloon12.bmp",
                    "Magicland\\Nhood_balloon13.bmp", "Magicland\\Nhood_balloon14.bmp", "Magicland\\Nhood_balloon15.bmp", "Magicland\\Nhood_balloon16.bmp")
            },
        };

        public TS1Provider Provider;
        public event Action<int> OnHouseSelect;
        public HITSound BgSound;
        public Dictionary<int, Vector2> HousePositions;
        // UI-30: armed evict/bulldoze lot-click routing. Set by TS1GameScreen
        // (the EvictModeLotHandler branch law lives there); when it returns true
        // the click was consumed and the normal lot flow must not run.
        public Func<int, bool> ArmedLotClick;
        // UI-30: the mounted lot buttons by house id — the post-operation
        // tile/thumbnail refresh target (the native vt+0x168 repaint law).
        private readonly Dictionary<int, UINeighborhoodHouseButton> LotButtonByHouse
            = new Dictionary<int, UINeighborhoodHouseButton>();
        // R93: the engine nessie cheat layer (Community/UL config) + the cheat-bar
        // plumbing (engine: the screen's CheatCallback receives the typed command).
        public UINeighborhoodNessieLayer NessieLayer;
        // R174: the executable's one shared cWinLotPopup. This replaces the
        // old port-authored bottom-left caption and is populated immediately
        // by each lot button's zero-delay generic-tooltip path.
        public UIOriginalLotPopup LotPopup;
        private UINeighborhoodHouseButton HoveredLotButton;
        // R100: the cheat-bar RESPONSE surface — the engine's own strings
        // ("Sorry only one Nessie at a time.") rendered from the R87-pinned
        // variablesans_09 dialog table.
        private UIOriginalText CheatMessage;
        private UITextBox CheatBar;
        private bool CheatBarOpen;
        public string LastCheatMessage;   // engine "Sorry only one Nessie at a time." surface

        private Vector2 _cp = new Vector2(800, 600) / 2;
        public float CenterPositionX
        {
            get
            {
                return _cp.X;
            }
            set
            {
                _cp.X = value;
            }
        }

        public float CenterPositionY
        {
            get
            {
                return _cp.Y;
            }
            set
            {
                _cp.Y = value;
                UpdatePosition();
            }
        }

        private float _z = 1f;
        public float Zoom
        {
            get
            {
                return _z;
            }
            set
            {
                _z = value;
            }
        }

        public UINeighborhoodSelectionPanel(ushort mode)
        {
            Provider = Content.Get().TS1Global;
            PopulateScreen(mode);
            GameResized();
        }

        internal static Vector2 OriginalArtboardCenter(int width, int height)
        {
            return new Vector2(
                Math.Max(0, width - 800) / 2 + 400,
                Math.Max(0, height - 600) / 2 + 300);
        }

        public void UpdatePosition()
        {
            base.GameResized();
            var screen = UIScreen.Current;
            int screenWidth = screen?.ScreenWidth ?? GlobalSettings.Default.GraphicsWidth;
            int screenHeight = screen?.ScreenHeight ?? GlobalSettings.Default.GraphicsHeight;
            // The desktop engine centers an unscaled 800x600 artboard inside larger
            // windows (offX/offY); it never stretches it to the window height. Keep
            // the existing scale-to-height behavior for touch layouts only.
            var scale = FSOEnvironment.SoftwareKeyboard
                ? screenHeight / 600.0f
                : 1.0f;
            ScaleX = ScaleY = scale * Zoom;

            float centerX;
            float centerY;
            if (FSOEnvironment.SoftwareKeyboard)
            {
                centerX = screenWidth / 2.0f;
                centerY = screenHeight / 2.0f;
            }
            else
            {
                // Exact r143 max(0, (window-artboard)/2) law. Below 800x600 the
                // artboard pins to (0,0) and the viewport clips it; it must not be
                // center-cropped into negative coordinates.
                var center = OriginalArtboardCenter(
                    screenWidth,
                    screenHeight);
                centerX = center.X;
                centerY = center.Y;
            }
            X = centerX - _cp.X * ScaleX;
            Y = centerY - _cp.Y * ScaleY;
        }

        public override void GameResized()
        {
            UpdatePosition();
        }

        private Texture2D texture;
        private int Mode;
        private IffFile NativeExpansionDesc;

        internal static bool IsNativeHouseForMode(ushort mode, int house)
        {
            // cWinStudiotown::Init creates exactly House81..House89. The next
            // LotLocations row is House90, but that is cMagicland's first lot;
            // mounting it here makes a Studio hotspot enter Magic Town. The
            // original Magic screen likewise creates exactly House90..House98.
            if (mode == 5) return house >= 81 && house <= 89;
            if (mode == 7) return house >= 90 && house <= 98;
            return house != 99;
        }

        public void PopulateScreen(ushort mode)
        {
            Mode = mode;
            NativeExpansionDesc = null;
            // The shared neighborhood provider historically routed every lot
            // below 80 through NeighborhoodDesc. The executable selects the
            // expansion descriptor for Downtown and Vacation, whose native
            // tables are already present in the active UserData/Houses folder.
            // Keep this correction local to the screen rather than mutating the
            // upstream FreeSO provider/submodule.
            if (mode == 2 || mode == 3)
            {
                try
                {
                    string member = mode == 2 ? "DTDesc.iff" : "VIDesc.iff";
                    NativeExpansionDesc = new IffFile(Path.Combine(
                        Content.Get().Neighborhood.UserPath, "Houses", member));
                }
                catch { NativeExpansionDesc = null; }
            }
            LotPopup = null;
            var childClone = new List<UIElement>(Children);
            var config = Neighborhoods[mode - 1];
            foreach (var child in childClone) Remove(child);

            // R160: the mobile ngbh_outline.png 9-slice frame is RETIRED — the
            // original screens are the raw 800x600 artboard (r143 law §1); the
            // surround is the engine kLargeMask law, owned by TS1GameScreen.Bg.
            var bg = new UIImage(((ITextureRef)Provider.Get(config.Graphic)).Get(GameFacade.GraphicsDevice));
            Add(bg);
            bg.ListenForMouse((evt, state) =>
            {
                if (evt == UIMouseEventType.MouseDown) ResetZoom();
            });

            HousePositions = new Dictionary<int, Vector2>();
            LotButtonByHouse.Clear();
            var locationIff = Content.Get().Neighborhood.LotLocations;
            var locations = locationIff.Get<STR>(mode);
            if (locations == null) return;

            var buttons = new List<UINeighborhoodHouseButton>();

            for (int i = 0; i < locations.Length; i++)
            {
                var loc = locations.GetString(i).Split(',');
                var num = int.Parse(loc[0].TrimStart());
                if (!IsNativeHouseForMode(mode, num)) continue;
                var button = new UINeighborhoodHouseButton(num, SelectHouse, config.Scale);
                button.Position = new Vector2(int.Parse(loc[1].TrimStart()), int.Parse(loc[2].TrimStart()));
                button.HoverNotify = (n, b, state) => ShowLotPopup(n, b, state);
                button.HoverLeave = HideLotPopup;
                HousePositions[num] = button.Position;
                LotButtonByHouse[num] = button;
                buttons.Add(button);
            }

            // The shared mount routine is also driven headlessly by the uinbhd gate,
            // so its under-lot -> source-order lots -> post-lot ordering cannot drift
            // away from the structural parity probe.
            MountImageAndLotLayers(config, buttons,
                layer =>
                {
                    // TSPaint paints animated water/deltas before cWinLotBtn children.
                    var lelem = new UINeighborhoodAnimationLayer(layer, config.Pulsate, config.FrameDuration);
                    lelem.Position = layer.Position;
                    Add(lelem);
                },
                btn => Add(btn),
                layer =>
                {
                    // Vacation's port/trees are opaque static PostChildDraw overlays.
                    var tex = ((ITextureRef)Provider.Get(layer.Frames[0])).Get(GameFacade.GraphicsDevice);
                    var lelem = new UIImage(tex) { Position = layer.Position };
                    Add(lelem);
                });

            // R91: the Studiotown car lanes — one layer per engine lane, each drawing
            // its cars at the engine-literal world coordinates.
            if (config.CarLanes != null)
            {
                for (int i = 0; i < UINeighborhoodCarLaneLayer.EngineCarLanes.Length; i++)
                {
                    var lane = new UINeighborhoodCarLaneLayer(config.CarLanes, i, config.FrameDuration);
                    Add(lane);
                }
            }

            // R92: engine cloud/balloon layers (Magicland) — same mount pattern as the
            // car lanes: the config supplies the bitmap source, the layer owns the
            // engine-decoded state machine.
            if (config.Clouds != null) Add(new UINeighborhoodCloudLayer(config.Clouds, config.FrameDuration));
            if (config.Balloons != null) Add(new UINeighborhoodBalloonLayer(config.Balloons, config.FrameDuration));

            // R93: the engine nessie cheat easter egg (Community/UL) — dormant until
            // the cheat fires; the layer IS the trigger target for the cheat bar.
            if (config.Nessie != null)
            {
                NessieLayer = new UINeighborhoodNessieLayer(config.Nessie, config.FrameDuration);
                Add(NessieLayer);
            }

            // cWinLotBtn::Init shares one popup across all lots in the active
            // neighborhood screen. Mount it last so the fully unfurled plaque
            // composes above map animation and lot sprites.
            LotPopup = new UIOriginalLotPopup();
            Add(LotPopup);

            BgSound?.RemoveOwner(-25);
            if (config.Music != null) FSO.HIT.HITVM.Get().PlaySoundEvent(config.Music);
            if (config.BGSound != null)
            {
                BgSound = HITVM.Get().PlaySoundEvent(config.BGSound);
                BgSound.AddOwner(-25);
            }
            Zoom = Zoom;
            CenterPositionX = CenterPositionX;
            CenterPositionY = CenterPositionY;
            //SimitoneNeighOBJExporter.SaveOBJ(Path.Combine(FSOEnvironment.UserDir, "NeighModel" + mode + "/"), locations);
        }

        // One implementation defines both the live child order and the headless
        // structural probe. The lots enumerable is never sorted: its source order is
        // the cWinNeighborhoodUL vector order recovered in R173.
        internal static void MountImageAndLotLayers<TLot>(NeighborhoodViewConfig config,
            IEnumerable<TLot> lots, Action<NeighborhoodImageAnim> mountUnderLot,
            Action<TLot> mountLot, Action<NeighborhoodImageAnim> mountPostLot)
        {
            foreach (var layer in config.FullImageAnimations) mountUnderLot(layer);
            foreach (var lot in lots) mountLot(lot);
            foreach (var layer in config.PostLotOverlays) mountPostLot(layer);
        }

        public void ResetZoom()
        {
            if (LastHS == null) return;
            LastHS.Kill();
            LastHS = null;
            GameFacade.Screens.Tween.To(this, 0.5f, new Dictionary<string, float>() { { "Zoom", 1f }, { "CenterPositionX", 400 }, { "CenterPositionY", 300 } }, TweenQuad.EaseOut);
        }

        public UIHouseSelectPanel LastHS;
        public static int DesktopDirectSelections;
        public static int DesktopNativeMoveInSelections;

        public static bool DispatchNativeDesktopSelection(bool desktop, bool movingFamily,
            int house, Action clearSelectionCard, Action<int> selected)
        {
            // Regular browse is the recovered direct-load path (R174). R197 ports
            // MoveInModeLotHandler's guard chain too: a desktop move-in click
            // dispatches straight to the native dialog decision in TS1GameScreen
            // (tutorial/community/afford/occupied/house-vs-lot, STR# 132). Only
            // touch layouts keep the zoomed selection card.
            if (!desktop) return false;
            // Card cleanup belongs inside the accepted native branch. Keeping it
            // here prevents a declined touch route from killing its live card and
            // leaving the old 500ms tween overlapped by a replacement.
            clearSelectionCard?.Invoke();
            if (movingFamily) DesktopNativeMoveInSelections++;
            else DesktopDirectSelections++;
            selected?.Invoke(house);
            return true;
        }

        /// <summary>
        /// UI-30: re-reads one lot's art (BMP/PNG 512/513 + THMB offsets) after a
        /// successful evict/bulldoze — the port's stand-in for the native's
        /// post-EvictFamily per-item repaint loop (bulldoze-law §2). Returns
        /// false when no button is mounted for the house (off-screen lot).
        /// </summary>
        public bool RefreshLotTile(int house)
        {
            UINeighborhoodHouseButton button;
            if (!LotButtonByHouse.TryGetValue(house, out button) || button == null) return false;
            button.ReloadArt();
            return true;
        }

        public void SelectHouse(int house)
        {
            // UI-30: while the evict/bulldoze mode is armed, lot clicks route to
            // the EvictModeLotHandler branch law INSTEAD of the normal flow
            // (bulldoze-law §1). Not armed -> the click falls through unchanged.
            if (ArmedLotClick != null && ArmedLotClick(house)) return;

            // Original desktop RegularModeLotHandler resolves the clicked lot and
            // calls cSimsApp::LoadGame directly; MoveInModeLotHandler (R197) runs
            // its own dialog guard chain on move-in clicks. The zoomed half-screen
            // UIHouseSelectPanel (diagonal stripes, repeated text twins and
            // Enter Lot/More pill buttons) is Simitone's touch UI, not a native
            // desktop lot-selection stage. Keep it only for touch layouts.
            var game = UIScreen.Current as TS1GameScreen;
            if (game != null && DispatchNativeDesktopSelection(game.Desktop,
                game.MoveInFamily != null, house, () =>
            {
                if (LastHS != null)
                {
                    LastHS.Kill();
                    LastHS = null;
                }
            }, OnHouseSelect)) return;

            if (LastHS != null && LastHS.HouseID == house)
            {
                ResetZoom();
            }
            else
            {
                LastHS?.Kill();
                LastHS = new UIHouseSelectPanel(house);
                GameFacade.Screens.CurrentUIScreen.Add(LastHS);
                GameFacade.Screens.Tween.To(this, 0.5f, new Dictionary<string, float>() {
                    { "Zoom", (Mode==4)?3f:1.5f },
                    { "CenterPositionX", HousePositions[house].X - ((Mode==4)?90f:180f) },
                    { "CenterPositionY", HousePositions[house].Y } }, TweenQuad.EaseOut);

                LastHS.OnSelected += (h) =>
                {
                    OnHouseSelect?.Invoke(h);
                    //HITVM.Get().PlaySoundEvent("bkground_fade");
                };
            }
        }

        public override void Removed()
        {
            base.Removed();
            BgSound?.RemoveOwner(-25);
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            base.Draw(batch);
            //DrawLocalTexture(batch, texture, new Vector2());
        }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            // cWinLotBtn passes its shared 300 ms RampGenerator to the popup;
            // both the open-house thumbnail and plaque therefore unfurl with
            // the same constant-time alpha rather than appearing on different
            // easing curves.
            if (LotPopup != null && LotPopup.Visible && HoveredLotButton != null)
                LotPopup.SetRampOpacity(HoveredLotButton.AlphaTime);
            UpdateCheatBar(state);
        }

        private void ShowLotPopup(int houseNumber, UINeighborhoodHouseButton button,
            UpdateState state)
        {
            if (LotPopup == null || button == null) return;
            FAMI family;
            string text = BuildNativeLotPopupText(houseNumber, out family);
            LotPopup.SetHouse(houseNumber, text, family);
            HoveredLotButton = button;
            // MouseOver fires before the next parent Update. Initialize from the
            // shared ramp immediately so there is no one-frame opaque flash.
            LotPopup.SetRampOpacity(button.AlphaTime);

            // cTSWin::GetTTPosition receives the cWinLotBtn's complete window
            // rectangle, not its smaller mouse-hit region. cTSWinBtn::SetImage
            // sizes that window to one thumbnail frame and CenterOn places it
            // around the configured lot point.
            var window = button.LocalWindowBounds;
            var lotRect = new Rectangle((int)(button.X + window.X),
                (int)(button.Y + window.Y), window.Width, window.Height);
            var mouse = GetMousePosition(state.MouseState);
            LotPopup.PlaceAtLot(lotRect, mouse, X, Y,
                UIScreen.Current.ScreenWidth, UIScreen.Current.ScreenHeight);
            LotPopup.Visible = true;
            UIOriginalText.HoverCaptionsShown++;
        }

        private string BuildNativeLotPopupText(int houseNumber, out FAMI family)
        {
            var neigh = Content.Get().Neighborhood;
            var address = BuildNativeLotAddress(houseNumber);
            family = neigh.GetFamilyForHouse((short)houseNumber);
            if (family != null)
            {
                var familyName = neigh.MainResource.Get<FAMs>(family.ChunkID)?.GetString(0) ?? "?";
                var worth = "§" + (family.ValueInArch + family.Budget).ToString("##,#0");
                if (Mode == 7 && houseNumber >= 90 && houseNumber <= 92)
                {
                    return address + "\n\n" + GameFacade.Strings.GetString("134", "34", new string[] {
                        familyName, worth, neigh.GetMagicoinsForFamily(family).ToString("##,#0"),
                        family.FamilyFriends.ToString() });
                }
                return address + "\n\n" + GameFacade.Strings.GetString("134", "0", new string[] {
                    familyName, worth, family.FamilyFriends.ToString() });
            }

            string name = "";
            string description = "";
            try
            {
                var nd = GetNativeHouseNameDesc(houseNumber);
                if (nd != null) { name = nd.Item1; description = nd.Item2; }
            }
            catch { }
            if (string.IsNullOrEmpty(name)) name = GameFacade.Strings.GetString("134", "22");
            if (string.IsNullOrEmpty(description)) description = GameFacade.Strings.GetString("134", "23");

            var house = neigh.GetHouse(houseNumber);
            var price = house?.Get<SIMI>(1)?.PurchaseValue ?? 0;
            if (Mode == 7 && houseNumber >= 90 && houseNumber <= 92)
            {
                int required = int.Parse(GameFacade.Strings.GetString("134",
                    (32 + Math.Abs(91 - houseNumber)).ToString()));
                return address + "\n\n" + GameFacade.Strings.GetString("134", "29", new string[] {
                    name, description, "§" + price.ToString("##,#0"), required.ToString() });
            }

            short zoning;
            if (!neigh.ZoningDictionary.TryGetValue((short)houseNumber, out zoning)) zoning = 0;
            if (zoning > 0)
                return address + "\n\n" + GameFacade.Strings.GetString("134", "17", new string[] { name, description });
            return address + "\n\n" + GameFacade.Strings.GetString("134", "1", new string[] {
                name, description, "§" + price.ToString("##,#0") });
        }

        private Tuple<string, string> GetNativeHouseNameDesc(int houseNumber)
        {
            // DTDesc owns 2021..2030; VIDesc owns 2040..2048. Outside those
            // native expansion ranges, retain the provider's neighborhood,
            // Studio Town, and Magic Town routing.
            bool expansionRange = (Mode == 2 && houseNumber >= 21 && houseNumber <= 30)
                || (Mode == 3 && houseNumber >= 40 && houseNumber <= 48);
            if (expansionRange && NativeExpansionDesc != null)
            {
                var str = NativeExpansionDesc.Get<STR>((ushort)(houseNumber + 2000));
                if (str != null) return new Tuple<string, string>(str.GetString(0), str.GetString(1));
            }
            return Content.Get().Neighborhood.GetHouseNameDesc(houseNumber);
        }

        private string BuildNativeLotAddress(int houseNumber)
        {
            var street = Content.Get().Neighborhood.StreetNames;
            try
            {
                var assignments = street?.Get<STR>(2001);
                var names = street?.Get<STR>(2000);
                int streetName;
                if (assignments != null && names != null
                    && int.TryParse(assignments.GetString(houseNumber - 1), out streetName)
                    && streetName > 0)
                {
                    var format = names.GetString(streetName - 1);
                    if (!string.IsNullOrEmpty(format))
                        return format.Replace("%s", houseNumber.ToString());
                }
            }
            catch { }

            // GetHouseInfo's native expansion files normally supply these
            // address formats. The mounted neighborhood provider exposes only
            // StreetNames.iff, so use the exact NghRollover fallbacks selected
            // by the executable when that native lookup is unavailable.
            int fallback;
            if (houseNumber >= 21 && houseNumber <= 23) fallback = 13;
            else if (houseNumber >= 24 && houseNumber <= 31) fallback = 14;
            else if (houseNumber >= 40 && houseNumber <= 42) fallback = 15;
            else if (houseNumber >= 43 && houseNumber <= 49) fallback = 16;
            else if (houseNumber >= 81 && houseNumber <= 83) fallback = 24;
            else if (houseNumber >= 84 && houseNumber <= 89) fallback = 25;
            else if (houseNumber >= 93 && houseNumber <= 99) fallback = 27;
            else fallback = 7;
            return GameFacade.Strings.GetString("134", fallback.ToString(),
                new string[] { houseNumber.ToString() });
        }

        private void HideLotPopup()
        {
            HoveredLotButton = null;
            if (LotPopup != null)
            {
                LotPopup.Visible = false;
                LotPopup.SetRampOpacity(1f);
            }
        }

        // R93: the original neighborhood screens take cheats through the same
        // Ctrl+Shift+C bar (CheatCallback__18cWinNeighborhoodUL receives the typed
        // command; "Usage: nessie. Same as SimCity."). Lazy-built so the headless
        // gate never creates the textbox.
        private void UpdateCheatBar(UpdateState state)
        {
            var pressed = state.KeyboardState.GetPressedKeys();
            if ((pressed.Contains(Microsoft.Xna.Framework.Input.Keys.LeftControl) || pressed.Contains(Microsoft.Xna.Framework.Input.Keys.RightControl))
                && (pressed.Contains(Microsoft.Xna.Framework.Input.Keys.LeftShift) || pressed.Contains(Microsoft.Xna.Framework.Input.Keys.RightShift))
                && state.NewKeys.Contains(Microsoft.Xna.Framework.Input.Keys.C))
            {
                CheatBarOpen = !CheatBarOpen;
                if (CheatBarOpen)
                {
                    if (CheatBar == null)
                    {
                        CheatBar = new UITextBox()
                        {
                            MaxLines = 1,
                            FlashOnEmpty = true,
                            ID = "UINbhdCheatBar",
                        };
                        CheatBar.SetSize(240, 23);
                        Add(CheatBar);
                    }
                    CheatBar.Visible = true;
                    state.InputManager.SetFocus(CheatBar);
                }
                else
                {
                    if (CheatBar != null) CheatBar.Visible = false;
                    state.InputManager.SetFocus(null);
                }
            }
            if (CheatBarOpen && CheatBar != null && state.NewKeys.Contains(Microsoft.Xna.Framework.Input.Keys.Enter))
            {
                var cmd = CheatBar.CurrentText == null ? "" : CheatBar.CurrentText.Trim();
                CheatBar.CurrentText = "";
                CheatBarOpen = false;
                CheatBar.Visible = false;
                state.InputManager.SetFocus(null);
                SubmitCheat(cmd);
            }
        }

        // The CheatCallback dispatch: only the engine-decoded command is handled;
        // everything else is ignored (the original plays an error dialog/sound).
        private void SubmitCheat(string command)
        {
            if (string.Equals(command, "nessie", StringComparison.OrdinalIgnoreCase) && NessieLayer != null)
            {
                if (!NessieLayer.CheatNessie())
                    ShowCheatMessage("Sorry only one Nessie at a time.");   // engine string
                else
                    LastCheatMessage = null;
            }
        }

        // R100: show the engine's cheat response in the ORIGINAL glyph style
        // (dialog table), auto-clearing after a few seconds.
        private void ShowCheatMessage(string message)
        {
            LastCheatMessage = message;
            if (CheatMessage == null)
            {
                var font = OriginalGlyphFont.LoadDialog(GameFacade.GraphicsDevice);
                if (font == null) return;
                CheatMessage = new UIOriginalText("", font);
                CheatMessage.X = 12;
                CheatMessage.Y = 34;
                Add(CheatMessage);
            }
            CheatMessage.Text = message;
            UIOriginalText.CheatMessagesShown++;
            GameThread.SetTimeout(() => { if (CheatMessage != null && CheatMessage.Text == message) CheatMessage.Text = ""; }, 4000);
        }
    }

    public class UINeighborhoodAnimationLayer : UIElement
    {
        // R89 cycling evidence for the 'uinbhd' gate: every constructed layer registers its
        // family (first frame name x frame count) and every frame advance is counted, both
        // from the live screen flow and from the gate's headless construction.
        public static int LayersMounted = 0;
        public static long FrameAdvances = 0;
        public static readonly Dictionary<string, int> AdvancesByFamily = new Dictionary<string, int>();

        public static string FamilyKey(NeighborhoodImageAnim anim)
        {
            var first = anim.Frames.Length > 0 ? anim.Frames[0] : "";
            return first + "x" + anim.Frames.Length;
        }

        public Texture2D[] Frames;
        public int FrameNum;
        public int SubFrame;
        public int FrameTime;
        private int TotalFrames;
        public NeighborhoodImageAnim Anim;
        public bool Discrete;
        public int FrameRepeat;
        public double CounterIntervalMilliseconds;
        public double CounterRemainderMilliseconds;

        // cWinNeighborhoodUL::TSPaint computes 1000/elapsed and advances when that
        // is <= 2*pi (float 0x40c90fdb, built in the ctor from 2.0f*pi). Because
        // timeGetTime and elapsed are integer milliseconds, 160 ms is the earliest
        // event. Its mod-12 counter is divided by two before indexing the six waves.
        // These constants apply only to Old Town; other families retain the generic
        // crossfading animator.
        public const float OldTownAdvancesPerSecondThreshold = 6.2831855f;
        public const double OldTownCounterIntervalMilliseconds = 160.0;
        public const int OldTownFrameRepeat = 2;

        internal bool DrawsInterpolatedFrameForProbe { get { return !Discrete; } }
        internal int VisibleFrameForProbe { get { return ResolveFrame(FrameNum); } }

        public UINeighborhoodAnimationLayer(NeighborhoodImageAnim anim, bool pulsate, int frameTime)
        {
            var provider = Content.Get().TS1Global;
            Frames = anim.Frames.Select(x => ((ITextureRef)provider.Get(x)).Get(GameFacade.GraphicsDevice)).ToArray();
            SubFrame = frameTime;
            FrameTime = frameTime;
            FrameTime *= GlobalSettings.Default.TargetRefreshRate;
            FrameTime /= 60;
            TotalFrames = pulsate ? (Frames.Length * 2 - 2) : Frames.Length;
            Anim = anim;
            Discrete = anim.Discrete;
            FrameRepeat = Math.Max(1, anim.FrameRepeat);
            CounterIntervalMilliseconds = anim.CounterIntervalMilliseconds;
            LayersMounted++;
            lock (AdvancesByFamily)
            {
                if (!AdvancesByFamily.ContainsKey(FamilyKey(anim))) AdvancesByFamily[FamilyKey(anim)] = 0;
            }
        }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            if (Discrete)
            {
                AdvanceMilliseconds(state.Time.ElapsedGameTime.TotalMilliseconds);
            }
            else StepFrame();
        }

        // Frame-advance logic split out of Update so the 'uinbhd' gate can drive a
        // constructed layer headlessly (UIElement.Update dereferences its state arg).
        public void StepFrame()
        {
            if (Discrete)
            {
                AdvanceCounter();
                return;
            }
            if (--SubFrame <= 0)
            {
                SubFrame = FrameTime;
                FrameNum++;
                if (TotalFrames != 0) FrameNum %= TotalFrames;
                RegisterAdvance();
            }
        }

        // Deterministic elapsed-time entry point used by Update and the headless
        // parity gate. The original advances at most once per TSPaint and then sets
        // lastMs to now; it intentionally does not catch up after a long frame.
        public void AdvanceMilliseconds(double elapsedMilliseconds)
        {
            if (!Discrete || CounterIntervalMilliseconds <= 0) return;
            CounterRemainderMilliseconds += Math.Max(0, elapsedMilliseconds);
            if (CounterRemainderMilliseconds >= CounterIntervalMilliseconds)
            {
                CounterRemainderMilliseconds = 0;
                AdvanceCounter();
            }
        }

        private void AdvanceCounter()
        {
            int counterFrames = TotalFrames * FrameRepeat;
            FrameNum++;
            if (counterFrames > 0) FrameNum %= counterFrames;
            RegisterAdvance();
        }

        private void RegisterAdvance()
        {
            FrameAdvances++;
            if (Anim != null)
            {
                lock (AdvancesByFamily)
                {
                    int cur;
                    AdvancesByFamily.TryGetValue(FamilyKey(Anim), out cur);
                    AdvancesByFamily[FamilyKey(Anim)] = cur + 1;
                }
            }
        }

        private int ResolveFrame(int counter)
        {
            int sequenceFrame = Discrete ? counter / FrameRepeat : counter;
            return (sequenceFrame >= Frames.Length)
                ? ((Frames.Length - 2) - (sequenceFrame - Frames.Length))
                : sequenceFrame;
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            var realFrame = ResolveFrame(FrameNum);
            DrawLocalTexture(batch, Frames[Math.Max(0, realFrame)], Vector2.Zero);
            if (Discrete) return;

            var fm2 = (TotalFrames != 0) ? ((FrameNum + 1) % TotalFrames) : 0;
            var realFrame2 = ResolveFrame(fm2);
            DrawLocalTexture(batch, Frames[Math.Max(0, realFrame2)], null, Vector2.Zero, Vector2.One, Color.White * (1 - ((float)SubFrame / FrameTime)));
        }
    }

    // R91: the Studiotown car system, reversed from the ORIGINAL PPC engine binary
    // (The Sims Complete). cWinStudiotown::InitCars @0x475dd4 builds six carLaneInfo
    // structs at this+344 stride 96 — every field below is an engine-literal store
    // (0x475e1c..0x4760ec); the direction split is `cmpwi r25,3` @0x475f54 (−1 for
    // lanes 0-2, +1 for lanes 3-5, stored at lane+12). Lanes 0-2 pack {x0, xEnd, y}
    // and run the upper road right-to-left (AnimateCarSW @0x4758c0); lanes 3-5 pack
    // {x0, y, xEnd} and run the lower road left-to-right (AnimateCarNE @0x4755b0).
    // Per car the engine picks its bitmap ONCE with Random() % 18 (magic 0x38E38E39
    // + `mulli ×18` @0x475c28/0x476000) out of the 20-frame set — variants 18/19 are
    // never randomly drawn — and never cycles it. AnimateCarNE/SW movement is built
    // from rare events (Random()%10 / Random()%40 gates) against the tick at
    // this+852. INTERPRETATION (disclosed, not pinned): base advance 1px/step in
    // dir, the 1-in-10 and 1-in-40 events each add one extra px, wrap keeps the
    // cars×spacing rhythm across the engine lane range. xEnd 816 and y 610 exceed
    // the 800x600 backdrop — engine-literal; the original world is larger than the
    // art and cars travel through off-screen segments.
    public class UINeighborhoodCarLaneLayer : UIElement
    {
        // R91 gate evidence (same pattern as the R89 cycling statics).
        public static int LanesMounted = 0;
        public static long LaneSteps = 0;

        public struct EngineCarLane { public int XStart, XEnd, Y, Dir, Cars, Spacing; }

        public static readonly EngineCarLane[] EngineCarLanes = new EngineCarLane[] {
            new EngineCarLane { XStart = 468, XEnd = 806, Y = 420, Dir = -1, Cars = 13, Spacing = 40 },
            new EngineCarLane { XStart = 480, XEnd = 806, Y = 440, Dir = -1, Cars = 11, Spacing = 35 },
            new EngineCarLane { XStart = 492, XEnd = 806, Y = 475, Dir = -1, Cars = 10, Spacing = 30 },
            new EngineCarLane { XStart = 606, XEnd = 816, Y = 538, Dir = 1, Cars = 8, Spacing = 20 },
            new EngineCarLane { XStart = 606, XEnd = 816, Y = 574, Dir = 1, Cars = 7, Spacing = 12 },
            new EngineCarLane { XStart = 606, XEnd = 816, Y = 610, Dir = 1, Cars = 5, Spacing = 10 },
        };

        // The engine's bitmap-choice modulus (Random() % 18).
        public const int BitmapChoiceModulus = 18;

        public Texture2D[] Frames;
        public int[] CarBitmap;   // per car: chosen index into Frames (Random() % 18)
        public float Phase;       // lane movement phase, in px, along dir
        public int Lane, Dir, Y, XStart, XEnd, Cars, Spacing;
        public int FrameTime, SubFrame;
        private Random RNG;

        public UINeighborhoodCarLaneLayer(NeighborhoodImageAnim anim, int laneIndex, int frameTime)
        {
            var provider = Content.Get().TS1Global;
            Frames = anim.Frames.Select(x => ((ITextureRef)provider.Get(x)).Get(GameFacade.GraphicsDevice)).ToArray();
            var L = EngineCarLanes[laneIndex];
            Lane = laneIndex; XStart = L.XStart; XEnd = L.XEnd; Y = L.Y;
            Dir = L.Dir; Cars = L.Cars; Spacing = L.Spacing;
            // Deterministic per-lane seed so the gate is reproducible (the engine uses
            // the global Random(); the DISTRIBUTION is the pin, not the draw).
            RNG = new Random(0x591 + laneIndex);
            CarBitmap = new int[Cars];
            for (int i = 0; i < Cars; i++) CarBitmap[i] = RNG.Next(BitmapChoiceModulus);
            SubFrame = frameTime;
            FrameTime = frameTime;
            FrameTime *= GlobalSettings.Default.TargetRefreshRate;
            FrameTime /= 60;
            LanesMounted++;
        }

        public float CarX(int i)
        {
            int range = XEnd - XStart;
            float p = ((i * Spacing) + Phase) % range;
            if (p < 0) p += range;
            return Dir > 0 ? XStart + p : XEnd - p;
        }

        // Movement step split out of Update for the headless gate (same pattern as
        // UINeighborhoodAnimationLayer.StepFrame).
        public void StepFrame()
        {
            int step = 1;
            if (RNG.Next(10) == 1) step++;   // engine 1-in-10 event (AnimateCarNE/SW %10)
            if (RNG.Next(40) == 1) step++;   // engine 1-in-40 event (AnimateCarNE/SW %40)
            Phase += Dir * step;
            LaneSteps++;
        }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            if (--SubFrame <= 0) { SubFrame = FrameTime; StepFrame(); }
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            for (int i = 0; i < Cars; i++)
                DrawLocalTexture(batch, Frames[CarBitmap[i]], null,
                    new Vector2(CarX(i), Y), Vector2.One, Color.White);
        }
    }

    // R92: the 21 drifting swamp-fog clouds, ported from the ORIGINAL PPC engine
    // (The Sims Complete) — every constant below has binary-offset provenance in
    // tools/iff-dump/r90/RE-ENGINE-CONSTANTS.md (R92 section) and the raw decodes in
    // tools/iff-dump/r92/. cMagiclandSprites keeps 21 CloudInfo records (36 bytes
    // each) at this+144; DoClouds (0x57c7f0) iterates all 21 behind the this+908
    // enable; AnimateClouds (0x57c1d0) is the per-tick logic:
    //   if (tick % interval == 0) { x -= drift; y += 1; }
    //   if (x <= 0) { x = wrapX; y = wrapY; frame = -255; slot = Random()%3; }
    //   if (x > 100) frame += 2; else if (x < 30) frame -= 3;
    //   tick++; DrawCloud(this, x, y, slot, frame)   // slot clamp 0..2, frame sentinels
    // InitClouds (0x57c890) seeds them in four loops — R258 ENGINE-EXACT
    // (r258-law.md item 3; the old "PEF-relocated table bases" were subfic
    // immediates: op 8 was missing from ppc_decode.py; raw decode in
    // tools/iff-dump/r258-pef-revisit/r258-initclouds-subfic.txt). Loop bases
    // are CLOUD-ARRAY-relative (record base = this+144, 36 bytes each): loop 1
    // all-21 defaults {x=0, y=0, slot=rand%3, tick=1, frame=-110,
    // interval=rand%2+2, wrapX=0, wrapY=0, drift=0}; loop 2 @0x57c940 records
    // 0..7 (j=0..7): y=60+16j, x=342-32j+j*(rand%5), wrapX=462-32j (no
    // jitter), drift=2; loop 3 @0x57c9a8 base this+360 = records 6..11
    // (c=10..15, k=c-10; 6..7 overwritten after loop 2 — loop 3 wins):
    // y=60+16k, x=282-48k+c*(rand%5), wrapX=462-48k, drift=3; loop 4
    // @0x57ca20 base this+612 = records 13..16 (k=0..3): y=60+10k,
    // x=142+k*(rand%5), wrapX=262+k*(rand%5), drift=2. Records 12 and 17..20
    // keep the defaults (r92's "8, 9, 16" was a misreading).
    public class UINeighborhoodCloudLayer : UIElement
    {
        public static int LayersMounted = 0;
        public static long CloudSteps = 0;

        public struct EngineCloud { public int X, Y, Slot, Tick, Frame, Interval, WrapX, WrapY, Drift; }

        public const int CloudCount = 21;          // DoClouds loop bound (cmpwi 21 @0x57c834)
        public const int SlotModulus = 3;          // Random()%3 (@0x57c268, @0x57c8cc)
        public const int IntervalModulus = 2;      // Random()%2+2 (@0x57c8f0..0x57c900)
        public const int SpreadModulus = 5;        // Random()%5 spreads (@0x57c960.., @0x57ca7c..)
        public const int WrapFrame = -255;         // frame reset on wrap (@0x57c248)
        public const int DefaultFrame = -110;      // InitClouds default (@0x57c8d8)

        public EngineCloud[] CloudsInfo;
        public Texture2D[] Frames;
        public int FrameTime, SubFrame;
        public bool[] Wrapped;                     // per-cloud wrap evidence for the gate
        private Random RNG;

        public UINeighborhoodCloudLayer(NeighborhoodImageAnim anim, int frameTime)
        {
            var provider = Content.Get().TS1Global;
            Frames = anim.Frames.Select(x => ((ITextureRef)provider.Get(x)).Get(GameFacade.GraphicsDevice)).ToArray();
            // Deterministic seed so the gate is reproducible (the engine uses the
            // global Random(); the DISTRIBUTION is the pin, not the draw).
            RNG = new Random(0xC10D);
            CloudsInfo = new EngineCloud[CloudCount];
            Wrapped = new bool[CloudCount];
            for (int i = 0; i < CloudCount; i++)
            {
                // InitClouds loop 1: all-21 defaults.
                CloudsInfo[i] = new EngineCloud
                {
                    X = 0, Y = 0,
                    Slot = RNG.Next(SlotModulus),
                    Tick = 1,
                    Frame = DefaultFrame,
                    Interval = RNG.Next(IntervalModulus) + 2,
                    WrapX = 0, WrapY = 0, Drift = 0,
                };
            }
            // R258 ENGINE-EXACT ladders (InitClouds 0x57C890..0x57CAB0; the
            // r105 "relocated tables" were subfic immediates all along — see
            // r258-pef-revisit/r258-initclouds-subfic.txt). ONE rand%5 draw
            // per record in loops 2/3, TWO in loop 4 (x draw first).
            // Loop 2 @0x57c940, records 0..7 (j = 0..7): y=60+16j;
            // x=342-32j + j*(rand%5); wrapX=462-32j (NO jitter); wrapY=0;
            // drift=2.
            for (int j = 0; j <= 7; j++)
            {
                var c = CloudsInfo[j];
                c.Y = 60 + 16 * j;
                c.X = 342 - 32 * j + j * RNG.Next(SpreadModulus);
                c.WrapX = 462 - 32 * j;
                c.WrapY = 0;
                c.Drift = 2;
                CloudsInfo[j] = c;
            }
            // Loop 3 @0x57c9a8, base this+360 = record 6; counters c = 10..15,
            // k = c-10 → records 6..11 (6..7 are written by BOTH loops; loop 3
            // runs later and wins): y=60+16k; x=282-48k + c*(rand%5);
            // wrapX=462-48k; wrapY=0; drift=3 (the literal 3 — r92's
            // "drift=T462b[k]+3" was a misreading).
            for (int c = 10; c <= 15; c++)
            {
                int k = c - 10;
                var w = CloudsInfo[6 + k];
                w.Y = 60 + 16 * k;
                w.X = 282 - 48 * k + c * RNG.Next(SpreadModulus);
                w.WrapX = 462 - 48 * k;
                w.WrapY = 0;
                w.Drift = 3;
                CloudsInfo[6 + k] = w;
            }
            // Loop 4 @0x57ca20, base this+612 = record 13 ((612-144)/36; r92's
            // "17..20" base arithmetic was wrong); counters 17..20, k = 0..3 →
            // records 13..16: y=60+10k; x=142+k*(rand%5); wrapX=262+k*(rand%5);
            // wrapY=0; drift=2.
            for (int k = 0; k <= 3; k++)
            {
                var c = CloudsInfo[13 + k];
                c.Y = 60 + 10 * k;
                c.X = 142 + k * RNG.Next(SpreadModulus);
                c.WrapX = 262 + k * RNG.Next(SpreadModulus);
                c.WrapY = 0;
                c.Drift = 2;
                CloudsInfo[13 + k] = c;
            }
            // Records keeping the loop-1 defaults: 12 and 17..20 (not "8, 9, 16").
            SubFrame = frameTime;
            FrameTime = frameTime;
            FrameTime *= GlobalSettings.Default.TargetRefreshRate;
            FrameTime /= 60;
            LayersMounted++;
        }

        // AnimateClouds (0x57c1d0) — one call per cloud per tick.
        public void StepCloud(ref EngineCloud c, int index)
        {
            if (c.Tick % c.Interval == 0) { c.X -= c.Drift; c.Y += 1; }
            if (c.X <= 0)
            {
                c.X = c.WrapX; c.Y = c.WrapY; c.Frame = WrapFrame;
                c.Slot = RNG.Next(SlotModulus);
                Wrapped[index] = true;
            }
            if (c.X > 100) c.Frame += 2;
            else if (c.X < 30) c.Frame -= 3;
            c.Tick++;
        }

        // Step split out of Update for the headless gate (same pattern as the lanes).
        public void StepFrame()
        {
            for (int i = 0; i < CloudCount; i++) StepCloud(ref CloudsInfo[i], i);
            CloudSteps++;
        }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            if (--SubFrame <= 0) { SubFrame = FrameTime; StepFrame(); }
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            for (int i = 0; i < CloudCount; i++)
            {
                var c = CloudsInfo[i];
                // DrawCloud clamps slot to 0..2 (0x57be24..0x57be30).
                int slot = c.Slot < 0 || c.Slot > 2 ? 0 : c.Slot;
                DrawLocalTexture(batch, Frames[slot], null,
                    new Vector2(c.X, c.Y), Vector2.One, Color.White);
            }
        }
    }

    // R92: the Magicland hot-air balloon, ported from the ORIGINAL PPC engine. It is
    // ONE BalloonInfo record (this+4; InitBalloons 0x57caf0 literal seed {f4:-100,
    // f8:0, f12:300, f16:400, f20:1, f24:100, f28:1, f32:1}); the 16 balloon bitmaps
    // are the WIND-STATE frames: DrawBalloon (0x57c090) indexes this+36+frame*4 with
    // frame gated to 0..15. AnimateBalloon (0x57c330) per tick:
    //   sway = (int)(m*sin(0.785*m/180)) — R258 ENGINE-EXACT (0x57C384..
    //        0x57C45C): m = seed%1440; the constants are lfd DOUBLES (pool
    //        file 0x5a5348); the amplitude envelope IS m — no amplitude
    //        constant exists and r110's "64.0 float" never did. The old
    //        amplitude-8 model was a disclosed approximation, now retired.
    //   wind walk (0x57c45c.., all integer literals engine-exact):
    //        if (seed % 35 == 0) wind = 2;            // 0xEA0FA0EB = /35 magic
    //        else if (wind < 16) wind++;              // ladder caps 6/10/14/16
    //        else { wind = 0; wind++; if (wind > 2) wind = 2; }   // init-only path (400)
    //        if (wind >= 15) wind = 15;               // clamp
    //        (r258 recovered the wind-band pool doubles {1.5, 3.0, 4.5, 5.0,
    //        6.0} — fcmpu'd against the phase with counters 6/10/...; the
    //        branch mapping stays modeled as open. The %35 cycle and every
    //        integer cap are literal.)
    //   draw at (300 + sway, seed + 1) with bitmap #wind; then f0 = sway,
    //   f4 = seed+1+f8, f16 = wind, seed++.
    // The draw y walks down 1px/tick; the off-screen respawn bound was not in the
    // decoded window — DISCLOSED: at y 600 the record resets to the literal init
    // (seed 1, wind 400 — the machine clamps it exactly like the engine first call).
    public class UINeighborhoodBalloonLayer : UIElement
    {
        public static int LayersMounted = 0;
        public static long BalloonSteps = 0;

        public const int WindModulus = 35;         // seed % 35 (0x57c45c, /35 magic)
        public const int AngleModulus = 1440;      // seed % 1440 (0x57c38c, /1440 magic)
        public const int XAnchor = 300;            // f12 literal (InitBalloons)
        public const int InitWind = 400;           // f16 literal
        public const int InitSeed = 1;             // f28 literal
        // R104/r258: the float block (file 0x5a5348 in The Sims Complete,
        // reached via the TOC entry at sec1+0x3d64 -> code+0x59c4b8) is read
        // with lfd — the constants are DOUBLES: 0.785 (phase scale, the
        // engine's rounded pi/4 literal) and 180.0 (phase divisor); the same
        // block carries the wind-band gates {1.5, 3.0, 4.5, 5.0, 6.0} (the
        // r258 dump's double view; its doc table lists the float-view of the
        // same bytes — see r258-pef-revisit/r258-balloon-float-block.txt) and
        // the int->double magic 2^52+2^31. Imports resolve to MathLib sin
        // (0x5a1bc0) and cos (0x5a1ba8).
        public const double EnginePiOver4 = 0.785;
        public const double EngineDegrees = 180.0;
        public static readonly float[] EngineWindGates = new float[] { 1.5f, 3.0f, 4.5f, 5.0f, 6.0f };

        // R258 (r258-law.md item 2; UI-31) — AnimateBalloon 0x57C384..0x57C45C,
        // instruction-exact: m = seed % 1440 (0xB60B60B7 magic); phase =
        // 0.785*m/180.0 (fmul/fdiv on lfd doubles); sway = (int)(m*sin(phase))
        // (fmul @0x57c404, fctiwz @0x57c40c — truncate-toward-zero). There is
        // NO amplitude constant: the envelope IS m (one full sine period per
        // seed%1440 cycle, linearly growing swing). The old port model
        // 8*sin(...) and r110's "64.0 float" at pool +0x48 never existed (no
        // code loads displacement 0x48 from the pool base anywhere).
        public static int EngineSway(int m)
        {
            return (int)((double)m * Math.Sin(EnginePiOver4 * (double)m / EngineDegrees));
        }

        public Texture2D[] Frames;
        public int Seed, Wind;
        public int DrawX, DrawY;
        public int FrameTime, SubFrame;

        public UINeighborhoodBalloonLayer(NeighborhoodImageAnim anim, int frameTime)
        {
            var provider = Content.Get().TS1Global;
            Frames = anim.Frames.Select(x => ((ITextureRef)provider.Get(x)).Get(GameFacade.GraphicsDevice)).ToArray();
            Seed = InitSeed;
            Wind = InitWind;
            SubFrame = frameTime;
            FrameTime = frameTime;
            FrameTime *= GlobalSettings.Default.TargetRefreshRate;
            FrameTime /= 60;
            LayersMounted++;
        }

        public int Sway()
        {
            // R258 engine-exact: sway = (int)(m * sin(0.785*m/180)), doubles,
            // m = seed % 1440 — see EngineSway. The old amplitude-8 disclosed
            // model is retired.
            return EngineSway(Seed % AngleModulus);
        }

        // AnimateBalloon's wind machine — see class comment for provenance.
        public void StepWind()
        {
            if (Seed % WindModulus == 0) Wind = 2;
            else if (Wind < 16) Wind++;
            else if (Wind != 15)
            {
                Wind = 0;
                Wind++;
                if (Wind > 2) Wind = 2;
            }
            if (Wind >= 15) Wind = 15;
        }

        public void StepFrame()
        {
            StepWind();
            DrawX = XAnchor + Sway();
            DrawY = Seed + 1;
            if (DrawY >= 600)
            {
                // DISCLOSED respawn model (off-screen reset; the engine bound was not
                // in the decoded window).
                Seed = InitSeed;
                Wind = InitWind;
                DrawY = Seed + 1;
            }
            else Seed++;
            BalloonSteps++;
        }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            if (--SubFrame <= 0) { SubFrame = FrameTime; StepFrame(); }
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            int frame = Wind;
            if (frame < 0 || frame >= Frames.Length) frame = 0;   // DrawBalloon gate 0..15
            DrawLocalTexture(batch, Frames[frame], null,
                new Vector2(DrawX, DrawY), Vector2.One, Color.White);
        }
    }

    // R93: the nessie CHEAT EASTER EGG, ported from the ORIGINAL PPC engine (The
    // Sims Complete). Every constant below has binary-offset provenance in
    // tools/iff-dump/r90/RE-ENGINE-CONSTANTS.md (R93 section) and raw decodes in
    // tools/iff-dump/r93/. The engine (cWinNeighborhoodUL, the Community/Old-Town
    // screen) treats nessie exactly like a SimCity cameo:
    //   CheatCallback (0x4652c0..0x465445) — the typed command is compared against
    //   "nessie"; if the active flag (this+240) is set it shows "Sorry only one
    //   Nessie at a time." and returns 0; otherwise flag=1, spawn x=118 (this+244),
    //   y=519 (this+248), state=-1 (this+252), tick-sync=-1 (this+260), return 1.
    //   DoNessie (0x462090..0x46273c), per timer tick:
    //     state < 0  : state=0; sprite=kNess1(5050); tick-sync; draw; return
    //     tick gate  : if already handled this tick, just draw (port: StepFrame IS
    //                  one tick)
    //     state 0    : play nessie_sfx; state=1; sprite=kNess2(5051)
    //     state 1    : state=2; sprite=kNess3(5052)
    //     state 2    : x -= 2; y += 1; if (x == 60) state = 3    // the swim
    //     state 3    : state=4; sprite=kNess2(5051)
    //     state 4    : state=5; sprite=kNess1(5050)
    //     state >= 5 : flag=0; sprite=null                        // gone (end-state)
    //   (The R90 "end-state not decoded" residual is CLOSED by this decode.)
    public class UINeighborhoodNessieLayer : UIElement
    {
        public static int LayersMounted = 0;
        public static long NessieTicks = 0;

        // CheatCallback li pair @0x465310/0x465318.
        public const int SpawnX = 118, SpawnY = 519;
        // DoNessie swim constants: dx=-2 dy=+1 @0x46244c/0x462458; turn at x==60
        // @0x462500.
        public const int SwimDx = -2, SwimDy = 1, SwimUntilX = 60;
        // Sprite ids (kNess1/2/3 = 5050/5051/5052) @0x4620f4/0x4622a8/0x462394.
        public const int IdNess1 = 5050, IdNess2 = 5051, IdNess3 = 5052;
        // R108: the sound the engine plays at state 0 — ENGINE-RECOVERED: the
        // DoNessie call (lwz r3, -18180(r2); addi r3, r3, 3; bl PlaySound
        // @0x462264..0x46226c) resolves through the TOC entry to sec1+0x6c6e0
        // whose bytes are "%d\0nessie_sfx\0..." — the +3 skips the 3-byte
        // "%d\0" prefix, so the engine plays the sound NAMED "nessie_sfx"
        // (the R93 by-name guess is engine-exact; r108/r108-nessie-sfx.md).
        public const string EngineSoundEvent = "nessie_sfx";

        public Texture2D[] Frames;      // 0 = kNess1, 1 = kNess2, 2 = kNess3
        public bool Active;             // engine this+240 flag
        public int State;               // engine this+252 (-1 just spawned, -2 dormant)
        public int X, Y;                // engine this+244/this+248
        public int FrameTime, SubFrame;

        public UINeighborhoodNessieLayer(NeighborhoodImageAnim anim, int frameTime)
        {
            var provider = Content.Get().TS1Global;
            Frames = anim.Frames.Select(x => ((ITextureRef)provider.Get(x)).Get(GameFacade.GraphicsDevice)).ToArray();
            Active = false;
            State = -2;
            SubFrame = frameTime;
            FrameTime = frameTime;
            FrameTime *= GlobalSettings.Default.TargetRefreshRate;
            FrameTime /= 60;
            LayersMounted++;
        }

        /// The CheatCallback "nessie" handler: false = "Sorry only one Nessie at a
        /// time." (engine message shown by the caller).
        public bool CheatNessie()
        {
            if (Active) return false;
            Active = true;
            X = SpawnX; Y = SpawnY;
            State = -1;
            return true;
        }

        public int FrameIndex
        {
            get
            {
                switch (State)
                {
                    case -1: case 0: case 4: return 0;   // kNess1
                    case 1: case 3: return 1;           // kNess2
                    case 2: return 2;                   // kNess3 (the swim)
                    default: return 0;
                }
            }
        }

        // DoNessie — one call = one engine timer tick (the engine's per-tick gate
        // at this+260 vs this+476 collapses to this).
        public void StepFrame()
        {
            NessieTicks++;
            if (!Active) return;
            if (State < 0)
            {
                State = 0;   // sprite = kNess1
                return;
            }
            switch (State)
            {
                case 0:
                    FSO.HIT.HITVM.Get().PlaySoundEvent(EngineSoundEvent);
                    State = 1;
                    break;
                case 1: State = 2; break;
                case 2:
                    X += SwimDx; Y += SwimDy;
                    if (X == SwimUntilX) State = 3;
                    break;
                case 3: State = 4; break;
                case 4: State = 5; break;
                default:
                    // end-state: gone; the flag clears so the cheat can fire again
                    Active = false;
                    State = -2;
                    break;
            }
        }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            if (--SubFrame <= 0) { SubFrame = FrameTime; StepFrame(); }
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            if (!Active) return;
            int f = FrameIndex;
            if (f < 0 || f >= Frames.Length) return;
            DrawLocalTexture(batch, Frames[f], null, new Vector2(X, Y), Vector2.One, Color.White);
        }
    }

    public class NeighborhoodViewConfig
    {
        public string Graphic;
        public float Scale;
        // Layers painted by the screen's TSPaint, before lot-button children.
        public NeighborhoodImageAnim[] FullImageAnimations = new NeighborhoodImageAnim[0];
        // Static bitmaps painted by the original PostChildDraw after lot buttons.
        // Vacation uses port then trees; other screens currently have none.
        public NeighborhoodImageAnim[] PostLotOverlays = new NeighborhoodImageAnim[0];
        // R91: when set, the panel mounts the six ENGINE car lanes (Studiotown) using
        // this anim's frames as the car-bitmap source instead of a fixed-position layer.
        public NeighborhoodImageAnim CarLanes;
        // R92: when set, the panel mounts the 21 ENGINE fog clouds / the ENGINE
        // balloon wind-walk (Magicland) using these anims' frames as bitmap sources.
        public NeighborhoodImageAnim Clouds;
        public NeighborhoodImageAnim Balloons;
        // R93: when set, the panel mounts the ENGINE nessie cheat layer
        // (Community/UL) — dormant until the cheat bar submits "nessie".
        public NeighborhoodImageAnim Nessie;
        // DISCLOSED port default frame duration for the generic animation
        // layers; the ENGINE-decoded families pass their own timing (the
        // 160ms law, R89/R92) and never read this default.
        public int FrameDuration = 15;
        public bool Pulsate = true;
        public string Music = "bkground_nhood1";
        public string BGSound;
    }

    public class NeighborhoodImageAnim
    {
        public string[] Frames;
        public Vector2 Position;
        public bool Discrete;
        public int FrameRepeat = 1;
        public double CounterIntervalMilliseconds;

        public NeighborhoodImageAnim(params string[] frames)
        {
            Frames = frames;
        }

        public NeighborhoodImageAnim(Vector2 position, params string[] frames) : this(frames)
        {
            Position = position;
        }
    }

    public class UINeighborhoodHouseButton : UIElement
    {
        internal static readonly Rectangle NeighborhoodArtboard = new Rectangle(0, 0, 800, 600);
        private Texture2D HouseTex;
        private Texture2D HouseOpenTex;
        private float HouseScale;
        private bool Hovered;
        private THMB Offsets;
        public float AlphaTime { get; set; }
        // DISCLOSED port hover-fade duration — no engine decode backs it
        // (cWinLotBtn's hover law decodes COLORS at 0x25d6b4, r143; the ramp
        // timing is unrecovered). Formerly misnamed "Native".
        public const float HoverRampSeconds = 0.300f;
        // cWinLotBtn's zero-delay shared cWinLotPopup hooks.
        public Action<int, UINeighborhoodHouseButton, UpdateState> HoverNotify;
        public Action HoverLeave;
        public Rectangle LocalHitBounds;
        public Rectangle LocalWindowBounds;

        public UINeighborhoodHouseButton(int houseNumber, Action<int> selectionCallback, float scale)
        {
            if (houseNumber == 71) { }
            AlphaTime = 0;
            HouseScale = scale;
            LoadArt(houseNumber);

            var w = (int)(HouseTex.Width / HouseScale);
            var h = (int)(HouseTex.Height / HouseScale);
            LocalWindowBounds = new Rectangle(-w / 2, -h / 2, w, h);
            LocalHitBounds = new Rectangle(w / -2, w / -4, w, h / 2);
            var clickHandler =
                ListenForMouse(LocalHitBounds, (evt, state) =>
                {
                    switch (evt)
                    {
                        case UIMouseEventType.MouseUp:
                            HITVM.Get().PlaySoundEvent(FSO.Client.UI.Model.UISounds.NeighborhoodClick);
                            selectionCallback(houseNumber); break;
                        case UIMouseEventType.MouseOver:
                            HITVM.Get().PlaySoundEvent(FSO.Client.UI.Model.UISounds.NeighborhoodRollover);
                            Hovered = true;
                            HoverNotify?.Invoke(houseNumber, this, state); break;
                        case UIMouseEventType.MouseOut:
                            Hovered = false;
                            HoverLeave?.Invoke(); break;
                    }
                });
        }

        // UI-30: art load split out of the ctor so a post-evict/bulldoze repaint
        // (RefreshLotTile) can re-read the house file's art and offsets.
        internal void ReloadArt()
        {
            LoadArt(HouseNumber);
        }

        internal int HouseNumber { get; private set; }

        private void LoadArt(int houseNumber)
        {
            HouseNumber = houseNumber;
            var house = Content.Get().Neighborhood.GetHouse(houseNumber);
            // SAV-07: an imported family's FAM replaces Houses/HouseNN.iff whole
            // (the 25-step import's file move) and carries no art chunks, so
            // every art read can miss. Fall back to the install template's house
            // art, then to a neutral placeholder, so the screen always builds.
            var art = house;
            HouseTex = art.Get<BMP>(513)?.GetTexture(GameFacade.GraphicsDevice);
            if (HouseTex == null)
            {
                art = Content.Get().Neighborhood.GetHouseArtFallback(houseNumber);
                if (art != null) HouseTex = art.Get<BMP>(513)?.GetTexture(GameFacade.GraphicsDevice);
                if (HouseTex == null) art = house;
            }
            if (HouseTex != null) {
                HouseOpenTex = art.Get<BMP>(512)?.GetTexture(GameFacade.GraphicsDevice) ?? HouseTex;
                Offsets = art.Get<THMB>(512) ?? new THMB() { Width = HouseTex.Width / 2, Height = HouseTex.Height / 2 }; //get offsets before scaling
            } else
            {
                HouseTex = art.Get<PNG>(513)?.GetTexture(GameFacade.GraphicsDevice)
                    ?? house.Get<PNG>(513)?.GetTexture(GameFacade.GraphicsDevice);
                HouseOpenTex = art.Get<PNG>(512)?.GetTexture(GameFacade.GraphicsDevice)
                    ?? house.Get<PNG>(512)?.GetTexture(GameFacade.GraphicsDevice)
                    ?? HouseTex;
                if (HouseTex == null)
                {
                    HouseTex = new Texture2D(GameFacade.GraphicsDevice, 2, 2);
                    HouseTex.SetData(new[] { new Microsoft.Xna.Framework.Color(32, 32, 32) });
                }
                Offsets = new THMB() { Width = HouseTex.Width / 2, Height = HouseTex.Height / 2 };
            }
        }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            float step = (float)(state.Time.ElapsedGameTime.TotalSeconds / HoverRampSeconds);
            if (Hovered) AlphaTime = Math.Min(1f, AlphaTime + step);
            else AlphaTime = Math.Max(0f, AlphaTime - step);
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            var yOff = new Vector2(Offsets.XOff, -Offsets.BaseYOff) / HouseScale;
            var yOff2 = yOff;
            yOff2.Y -= Offsets.AddYOff / (HouseScale);
            if (AlphaTime > 0)
            {
                DrawArtboardClipped(batch, HouseOpenTex,
                    new Vector2(-Offsets.Width, -Offsets.Height) / HouseScale + yOff2,
                    Color.White);
            }
            DrawArtboardClipped(batch, HouseTex,
                new Vector2(-Offsets.Width, -Offsets.Height) / HouseScale + yOff,
                Color.White * (1 - AlphaTime));
        }

        // cWinLotBtn::ImageBlt passes the neighborhood bounds at this+0x220 to
        // every composite blit (Complete PPC 0x2d31d8..0x2d3830). XNA does not
        // inherit that child-window clip, so crop the source explicitly.
        private void DrawArtboardClipped(UISpriteBatch batch, Texture2D texture,
            Vector2 localTopLeft, Color color)
        {
            if (texture == null) return;
            var source = ClipSourceToArtboard(Position + localTopLeft,
                texture.Width, texture.Height, HouseScale);
            if (source.Width <= 0 || source.Height <= 0) return;
            var clippedTopLeft = localTopLeft + new Vector2(source.X, source.Y) / HouseScale;
            DrawLocalTexture(batch, texture, source, clippedTopLeft,
                new Vector2(1f / HouseScale, 1f / HouseScale), color);
        }

        internal static Rectangle ClipSourceToArtboard(Vector2 absoluteTopLeft,
            int sourceWidth, int sourceHeight, float sourcePixelsPerArtboardPixel)
        {
            if (sourceWidth <= 0 || sourceHeight <= 0 || sourcePixelsPerArtboardPixel <= 0)
                return Rectangle.Empty;

            int left = Math.Max(0, (int)Math.Ceiling(
                (NeighborhoodArtboard.Left - absoluteTopLeft.X) * sourcePixelsPerArtboardPixel));
            int top = Math.Max(0, (int)Math.Ceiling(
                (NeighborhoodArtboard.Top - absoluteTopLeft.Y) * sourcePixelsPerArtboardPixel));
            int right = Math.Min(sourceWidth, (int)Math.Floor(
                (NeighborhoodArtboard.Right - absoluteTopLeft.X) * sourcePixelsPerArtboardPixel));
            int bottom = Math.Min(sourceHeight, (int)Math.Floor(
                (NeighborhoodArtboard.Bottom - absoluteTopLeft.Y) * sourcePixelsPerArtboardPixel));

            return (right > left && bottom > top)
                ? new Rectangle(left, top, right - left, bottom - top)
                : Rectangle.Empty;
        }

        public override void Removed()
        {
            HouseTex?.Dispose();
            HouseOpenTex?.Dispose();
        }
    }

    /// <summary>
    /// R174 executable-derived cWinLotPopup: zero-delay lot hover information,
    /// VariableSans_10 text, 200..370px adaptive width, original 37px tiled
    /// edge chrome, and one/two rows of 25px resident portraits.
    /// </summary>
    public class UIOriginalLotPopup : UIContainer
    {
        public const string TilerMember = "Nbhd\\HotspotPopupTiler.TGA";
        public const int TilerCell = 37;
        public const int ContentInset = 25;
        public const int MinContentWidth = 150;
        public const int MaxContentWidth = 320;
        public const int PortraitSize = 25;
        public static readonly Color NativeTextColor = new Color(0xC3, 0xCD, 0xCD, 0xFF);
        public static int NativeMounts;

        public Texture2D Tiler;
        public UIOriginalParagraph TextBlock;
        public readonly List<Texture2D> Portraits = new List<Texture2D>();
        private readonly Dictionary<int, List<Texture2D>> PortraitCache =
            new Dictionary<int, List<Texture2D>>();
        public string DisplayText = "";
        public int HouseID;
        public int ContentWidth;
        public int WrappedLines;
        public int PortraitRows;
        private Vector2 _size;
        public override Vector2 Size { get { return _size; } set { _size = value; } }

        private readonly OriginalGlyphFont Font;

        public UIOriginalLotPopup()
        {
            Tiler = UIOriginal.EnsureResolved(TilerMember)?.Get(GameFacade.GraphicsDevice);
            Font = OriginalGlyphFont.LoadByIndex(10, GameFacade.GraphicsDevice);
            TextBlock = new UIOriginalParagraph(Font)
            {
                Position = new Vector2(ContentInset, ContentInset),
                LineHeight = Font != null ? Font.LineHeight : 19,
                TextColor = NativeTextColor,
            };
            Add(TextBlock);
            Visible = false;
            NativeMounts++;
        }

        public void SetHouse(int houseID, string text, FAMI family)
        {
            HouseID = houseID;
            DisplayText = text ?? "";
            Portraits.Clear();
            if (family != null)
            {
                List<Texture2D> cached;
                if (PortraitCache.TryGetValue(houseID, out cached)) Portraits.AddRange(cached);
                else
                {
                    LoadPortraits(family);
                    PortraitCache[houseID] = new List<Texture2D>(Portraits);
                }
            }

            int lineHeight = Font != null ? Font.LineHeight : 19;
            var firstLine = DisplayText.Replace("\r\n", "\n").Split('\n')[0];
            ContentWidth = Clamp(Font != null ? Font.Measure(firstLine) : firstLine.Length * 8,
                MinContentWidth, MaxContentWidth);

            for (int pass = 0; pass < 3; pass++)
            {
                WrappedLines = CountWrappedLines(DisplayText, Font, ContentWidth);
                int baseWidth = ContentWidth + ContentInset * 2;
                int candidateColumns = Math.Max(1, ContentWidth / PortraitSize);
                PortraitRows = Portraits.Count == 0 ? 0 : (Portraits.Count <= candidateColumns ? 1 : 2);
                int candidatePortraitHeight = PortraitRows == 0 ? 0 : (PortraitRows == 1 ? 35 : 60);
                int totalHeight = WrappedLines * lineHeight + ContentInset * 2 + candidatePortraitHeight;
                if (totalHeight < (baseWidth * 2) / 3 || pass == 2 || ContentWidth >= MaxContentWidth)
                    break;
                ContentWidth = Clamp(ContentWidth * 3 / 2, MinContentWidth, MaxContentWidth);
            }

            WrappedLines = CountWrappedLines(DisplayText, Font, ContentWidth);
            int columns = Math.Max(1, ContentWidth / PortraitSize);
            PortraitRows = Portraits.Count == 0 ? 0 : (Portraits.Count <= columns ? 1 : 2);
            int portraitHeight = PortraitRows == 0 ? 0 : (PortraitRows == 1 ? 35 : 60);
            Size = new Vector2(ContentWidth + ContentInset * 2,
                WrappedLines * lineHeight + ContentInset * 2 + portraitHeight);
            TextBlock.Text = DisplayText;
            TextBlock.MaxWidth = ContentWidth;
            TextBlock.LineHeight = lineHeight;
        }

        public void SetRampOpacity(float opacity)
        {
            opacity = Math.Max(0f, Math.Min(1f, opacity));
            Opacity = opacity;
            // MouseOver can begin after this frame's Update, while the texture
            // helpers below read the cached _BlendColor directly. Resolve the
            // dirty opacity immediately so the plaque cannot flash its previous
            // alpha for one frame before UIElement.Update catches up.
            _ = BlendColor;
            // UIContainer opacity is not inherited by child elements. The
            // executable passes one RampGenerator to plaque and text, so keep
            // the wrapped glyph lines on that same alpha explicitly.
            TextBlock.TintLines(NativeTextColor * opacity);
        }

        public void PlaceAtLot(Rectangle lotRect, Vector2 mouse, float panelScreenX,
            float panelScreenY, int screenWidth, int screenHeight)
        {
            int x = Clamp((int)mouse.X, lotRect.Left, lotRect.Right - 1);
            int y = lotRect.Top - (int)Size.Y - 1;
            // GetTTPosition's top-room decision is in screen space. The 800x600
            // artboard is normally centered at panelScreenY=84 in a 768px
            // window, so a negative panel-local y can still be fully on-screen.
            if (y + panelScreenY < 3) y = lotRect.Bottom + 2;

            int minX = (int)Math.Ceiling(1 - panelScreenX);
            int minY = (int)Math.Ceiling(1 - panelScreenY);
            int maxX = (int)Math.Floor(screenWidth - panelScreenX - 1 - Size.X);
            int maxY = (int)Math.Floor(screenHeight - panelScreenY - 1 - Size.Y);
            X = Math.Max(minX, Math.Min(maxX, x));
            Y = Math.Max(minY, Math.Min(maxY, y));
        }

        private void LoadPortraits(FAMI family)
        {
            World world = null;
            try
            {
                world = new World(GameFacade.GraphicsDevice);
                world.Initialize(GameFacade.Scenes);
                var context = new VMContext(world);
                var vm = new VM(context, new VMServerDriver(new VMTS1GlobalLinkStub()),
                    new VMNullHeadlineProvider());
                vm.Init();
                var blueprint = new Blueprint(1, 1);
                context.Blueprint = blueprint;
                context.Architecture = new VMArchitecture(1, 1, blueprint, vm.Context);
                foreach (var guid in family.FamilyGUIDs)
                {
                    VMEntity obj = null;
                    try
                    {
                        obj = vm.Context.CreateObjectInstance(guid, LotTilePos.OUT_OF_WORLD,
                            Direction.NORTH, true).BaseObject;
                        var portrait = UIIconCache.GetObject(obj);
                        if (portrait != null) Portraits.Add(portrait);
                    }
                    catch { }
                    finally { obj?.Delete(true, vm.Context); }
                }
            }
            catch { }
            finally { world?.Dispose(); }
        }

        private static int CountWrappedLines(string text, OriginalGlyphFont font, int maxWidth)
        {
            if (font == null || string.IsNullOrEmpty(text)) return 0;
            int result = 0;
            foreach (var paragraph in text.Replace("\r\n", "\n").Split('\n'))
            {
                string line = "";
                foreach (var word in paragraph.Split(' '))
                {
                    string candidate = line.Length == 0 ? word : line + " " + word;
                    if (font.Measure(candidate) <= maxWidth || line.Length == 0) line = candidate;
                    else { result++; line = word; }
                }
                result++;
            }
            return result;
        }

        private static int Clamp(int value, int min, int max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            UIOriginalDialogChrome.DrawTiledWindow(this, batch, Tiler, TilerCell,
                0, 0, (int)Size.X, (int)Size.Y);
            base.Draw(batch);

            int columns = Math.Max(1, ContentWidth / PortraitSize);
            int firstY = (int)Size.Y - (PortraitRows == 2 ? 75 : 50);
            for (int i = 0; i < Portraits.Count; i++)
            {
                int row = i / columns;
                int col = i % columns;
                var portrait = Portraits[i];
                DrawLocalTexture(batch, portrait, null,
                    new Vector2(ContentInset + col * PortraitSize, firstY + row * PortraitSize),
                    new Vector2(PortraitSize / (float)portrait.Width,
                        PortraitSize / (float)portrait.Height));
            }
        }
    }
}
