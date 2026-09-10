using FSO.Client;
using FSO.Client.UI.Framework;
using FSO.Client.UI.Model;
using FSO.Common.Utils;
using FSO.Content;
using FSO.SimAntics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simitone.Client.UI.Controls;
using Simitone.Client.UI.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FSO.Common.Rendering.Framework.Model;
using Simitone.Client.UI.Panels.LiveSubpanels;
using Simitone.Client.UI.Screens;
using FSO.Client.UI.Controls;

namespace Simitone.Client.UI.Panels
{
    public class UIMainPanel : UIContainer
    {
        private float _CurWidth;
        public float CurWidth
        {
            get
            {
                return _CurWidth;
            }
            set
            {
                _CurWidth = value;
                UpdateWidth();
            }
        }

        private Texture2D Div;
        private Texture2D WhitePx;
        private Rectangle DivRect;
        private UIDiagonalStripe Diag;
        public UIImage OriginalPanelBack;
        public UIImage TallSubpanel;   // kTallSubpanel 4918; double-byte UI only.

        // CPState::IsDoubleByteUI @0x20c670: language IDs 15,17,18,19,20.
        // R193 inverted the caller's beq and mounted this over English LIVE.
        public static bool UsesTallSubpanel(byte languageCode)
        {
            return languageCode == 15 || (languageCode >= 17 && languageCode <= 20);
        }
        // R144: kBackPatch bridge strip + the cWinPeople chrome (webcams/tabs)
        public UIImage BackPatch;
        public UIOriginalPeopleChrome PeopleChrome;
        // R145: the BUILD/BUY band chrome (cWinArch tools + cWinCatalog
        // plaques — engine law tools/iff-dump/r145/build-toolbar-law.md +
        /// buy-catalog-law.md).
        public UIOriginalArchChrome ArchChrome;
        public UIOriginalBuyChrome BuyChrome;
        public UIOriginalCameraPanel CameraChrome;
        public UISubpanel SubPanel;
        public TS1GameScreen Game;

        // R141: the ONE original speed cluster at the toolbar's right end — pause
        // (kPause 2026, 60x30 = 2x 30x30) + the three speed buttons (kSpeed1/2/3
        // 4705-4707, 44/80/128x15 sheets). This replaces BOTH the desktop-UCP
        // speed pills and the old static OriginalSpeedStrip image (two clusters
        // were visible at once; the static one was not even clickable).
        public UIStencilButton PauseButton;
        public UIStencilButton[] SpeedButtons;
        // R98/R99 twins relocated from the desktop UCP: time + funds readouts in
        // original glyphs beside the speed cluster (the original carries the
        // clock/speed/funds at the toolbar's right end).
        public Simitone.Client.UI.Controls.UIOriginalText TimeOriginal;
        public Simitone.Client.UI.Controls.UIOriginalText MoneyOriginal;

        public static Dictionary<int, int> RemapSpeed = new Dictionary<int, int>()
        {
            {0, 4}, //pause
            {1, 1}, //1 speed
            {3, 2}, //2 speed
            {10, 3}, //3 speed
        };

        public UIStencilButton FloorUpBtn;
        public UIStencilButton FloorDownBtn;
        public UILabel FloorLabel;
        public UILabel FloorLabelShadow;
        public UICategorySwitcher Switcher;
        public UIImage Divider;
        public UIStencilButton HideButton;
        public bool ShowingSelect;

        public UISwitchAvatarPanel SwitchAvatar;
        public bool PanelActive;
        public UIMainPanelMode Mode;
        private bool ModeTransitioning;

        private UITextBox CatalogSearchField;
        private UILabel SearchPlaceholder;
        private bool _lastLeftButtonDown;

        public event Action OnEndSelect;
        public event Action<UIMainPanelMode> ModeChanged;

        // Eyedropper tool support - stores GUID to select after category switch
        public uint? PendingEyedropperGUID;
        // Architecture eyedropper - stores pattern ID and type for floors/wallpaper
        public ushort? PendingEyedropperPatternID;
        public ArchitectureType? PendingEyedropperArchType;

        // R99: the ORIGINAL-styled floor readout twin (small .ffn table).
        private Simitone.Client.UI.Controls.UIOriginalText FloorOriginal;
        // R121: FreewillOriginal removed — free will now lives in the canon Play
        // Options screen (UIOriginalOptionsPanel), caption 'Free Will' = 145 [59].
        public string[] FloorNames = new string[]
        {
            "1st",
            "2nd",
            "3rd",
            "4th",
            "5th"
        };
        public int LastFloor = -1;
        private int RouteFrame;

        private List<UICategory> LiveCategories = new List<UICategory>()
        {
            new UICategory() { ID = 0, IconName = "live_motives.png",       OriginalName = "cpanel\\Buttons\\Mood.bmp" },
            new UICategory() { ID = 3, IconName = "live_relationships.png", OriginalName = "cpanel\\Buttons\\Relationship.BMP" },
            new UICategory() { ID = 1, IconName = "live_job.png",           OriginalName = "cpanel\\Buttons\\Job.bmp" },
            // R132: the ORIGINAL House tab (kHouseBtn 4502, engine tab order
            // Mood/Personality/House/Job/Relationship/Interest from
            // 0000_CPanel.h 4500-4505) — the port was missing it.
            // cpanel\Buttons\House.bmp loads IFF-first via the switcher's
            // OriginalName path.
            new UICategory() { ID = 5, IconName = "live_house.png",         OriginalName = "cpanel\\Buttons\\House.bmp" },
            new UICategory() { ID = 2, IconName = "live_personality.png",   OriginalName = "cpanel\\Buttons\\Personality.bmp" },
            // R185: the hidden switcher is state storage on desktop, but its
            // category census must still be the native seven-tab set and order.
            // Legacy inventory id 4 was never a cWinPeople tab; Interest/Gift
            // are the expansion-era panels 6/7 decoded in R145.
            new UICategory() { ID = 6, IconName = "live_interest.png",      OriginalName = "cpanel\\Buttons\\Interest.bmp" },
            new UICategory() { ID = 7, IconName = "live_gift.png",          OriginalName = "cpanel\\Buttons\\Gift.bmp" }
        };


        // R122: the ORIGINAL buy-mode main sorts — UIText.iff STR# 150
        // 'BuyModeCatalogSortTips' English [8..15] in the table's own order
        // (Seating, Surfaces, Decorative, Electronics, Appliances, Plumbing,
        // Lighting, Miscellaneous — the kBuyF* plaque order), with the original
        // cpanel\Catalog plaque art. IDs stay the WorldCatalog function indices;
        // the canon order is NOT the catalog's internal order, which is exactly
        // what UIBuyBrowsePanel.RemapString maps (catalog ID -> canon index).
        // The captions go to the button TOOLTIPS: the table's own label calls
        // these strings sort TIPS, and the plaque art carries the visible text.
        private static readonly (int id, string art, string png)[] BuyFunctionDefs = new (int, string, string)[]
        {
            (0, "cpanel\\Catalog\\BuyFSeating.BMP",     "cat_seat.png"),
            (1, "cpanel\\Catalog\\BuyFSurfaces.bmp",    "cat_surf.png"),
            (5, "cpanel\\Catalog\\BuyFDecorative.bmp",  "cat_deco.png"),
            (3, "cpanel\\Catalog\\BuyFElectronics.bmp", "cat_elec.png"),
            (2, "cpanel\\Catalog\\BuyFAppliances.bmp",  "cat_appl.png"),
            (4, "cpanel\\Catalog\\BuyFPlumbing.bmp",    "cat_plum.png"),
            (7, "cpanel\\Catalog\\BuyFLighting.bmp",    "cat_ligt.png"),
            (6, "cpanel\\Catalog\\BuyFMisc.bmp",        "cat_misc.png"),
        };

        // R122: the ROOM sorts — STR# 150 English [0..7] (the kBuyR* plaque
        // order). IDs are 100 + the DISPLAY index; UIBuyBrowsePanel's
        // CanonRoomToFlagBit translates it to the original's internal RoomFlags
        // bit order (a data-derived map — see the empirical anchors there).
        private static readonly (string art, string png)[] BuyRoomDefs = new (string, string)[]
        {
            ("cpanel\\Catalog\\BuyRLiving.BMP",   "cat_seat.png"),
            ("cpanel\\Catalog\\BuyRDining.bmp",   "cat_surf.png"),
            ("cpanel\\Catalog\\BuyRBedroom.bmp",  "cat_misc.png"),
            ("cpanel\\Catalog\\BuyRStudy.bmp",    "cat_misc.png"),
            ("cpanel\\Catalog\\BuyRKitchen.bmp",  "cat_appl.png"),
            ("cpanel\\Catalog\\BuyRBathroom.bmp", "cat_plum.png"),
            ("cpanel\\Catalog\\BuyROutside.bmp",  "cat_build_outs.png"),
            ("cpanel\\Catalog\\BuyRMisc.bmp",     "cat_misc.png"),
        };

        private List<UICategory> GetBuyFunctionCategories()
        {
            var result = new List<UICategory>();
            for (int i = 0; i < 8; i++)
                result.Add(new UICategory()
                {
                    ID = BuyFunctionDefs[i].id,
                    IconName = BuyFunctionDefs[i].png,
                    OriginalName = BuyFunctionDefs[i].art,
                    Caption = GameFacade.Strings.GetString("150", (8 + i).ToString())
                });
            return result;
        }

        private List<UICategory> GetBuyRoomCategories()
        {
            var result = new List<UICategory>();
            for (int i = 0; i < 8; i++)
                result.Add(new UICategory()
                {
                    ID = 100 + i,
                    IconName = BuyRoomDefs[i].png,
                    OriginalName = BuyRoomDefs[i].art,
                    Caption = GameFacade.Strings.GetString("150", i.ToString())
                });
            return result;
        }

        // R122: BUY mode pages the main-sort row between the ROOM page and the
        // FUNCTION page (both sets are canon; 16 plaques do not fit the expanding
        // column at once). The arrows are the original paging art
        // (kCatalogPrevPage/kCatalogNextPage = cpanel\Buttons\ScrollLeft.bmp /
        // ScrollRight.bmp) and the tooltips are the original 'Previous Page' /
        // 'Next Page' = STR# 154 [0]/[1].
        public bool BuyRoomPage;
        public UIStencilButton BuyPagePrev;
        public UIStencilButton BuyPageNext;

        public List<UICategory> CurrentBuyCategories()
        {
            return BuyRoomPage ? GetBuyRoomCategories() : GetBuyFunctionCategories();
        }

        public void ToggleBuySortPage()
        {
            BuyRoomPage = !BuyRoomPage;
            if (Mode == UIMainPanelMode.BUY)
                Switcher.InitCategories(CurrentBuyCategories());
        }

        private List<UICategory> BuildCategories = new List<UICategory>()
        {
            new UICategory() { ID = 0, IconName = "cat_build_arch.png" },
            new UICategory() { ID = 1, IconName = "cat_build_outs.png" },
            new UICategory() { ID = 2, IconName = "cat_build_objs.png" },
        };

        // Construction trigger only. Desktop cWinOptions has no category plaque;
        // ApplyMode hides the switcher's visual after Select creates the panel.
        private List<UICategory> OptionsCategories = new List<UICategory>()
        {
            new UICategory() { ID = 0, IconName = "cat_build_arch.png" },
        };

        public UIMainPanel(TS1GameScreen game) : base()
        {
            Game = game;
            // R141: the diagonal-cut chrome is mobile-only (the original toolbar is
            // bitmap chrome; the desktop bar carries the original PanelBack art).
            if (!Game.Desktop)
            {
                Diag = new UIDiagonalStripe(new Point(0, 128), UIDiagonalStripeSide.RIGHT, UIStyle.Current.Bg);
                Add(Diag);
            }

            // IFF-factual original live-toolbar backdrop (UIGraphics.far 0119_PanelBack.bmp,
            // steel glimmer #6F6F9C over deep navy #000052). Mounted as the lowest child so the
            // original navy toolbar reads behind Simitone's controls. Scale matches the 128px panel
            // height nearly 1:1 (804x100 vs 1024x128); evidence via uidump, pin via uipal/manual.
            try
            {
                // R210: mount through the FAR resolver — cpanel\Backgrounds\
                // PanelBack.bmp is a .bmp member (the resolver covers bmps;
                // the png twin stays as the fallback) and self-registers in
                // the provenance registry for the ui-total gate.
                var pbRef = Simitone.Client.UI.Model.UIOriginal.EnsureResolved("cpanel\\Backgrounds\\PanelBack.bmp");
                var pb = (pbRef != null)
                    ? pbRef.Get(GameFacade.GraphicsDevice)
                    : Content.Get().CustomUI.Get("orig_panel_back.png").Get(GameFacade.GraphicsDevice);
                OriginalPanelBack = new UIImage(pb);
                // R142/R144: PanelBack.bmp is always a natural 804x100 buffer mounted
                // flush right of the 220px UCP. Narrow windows clip that buffer at the
                // viewport; shrinking the UIImage would compress every landmark.
                if (Game.Desktop)
                    OriginalPanelBack.SetSize(804, 100);
                else
                    OriginalPanelBack.SetSize(Math.Max(0, Game.ScreenWidth), 128);
                Add(OriginalPanelBack);
                GameLog.Write("panel-ctor: original toolbar backdrop mounted " + pb.Width + "x" + pb.Height);
            }
            catch (Exception pbe)
            {
                GameLog.Write("panel-ctor: original toolbar backdrop FAILED - " + pbe.GetType().Name + " " + pbe.Message);
                OriginalPanelBack = null;
            }

            // Original cWinCPanel::Init @0x270238 skips this buffer when
            // IsDoubleByteUI returns false. Keep the existing double-byte
            // placement model pending its separate composition restoration.
            if (Game.Desktop && UsesTallSubpanel((byte)FSO.Files.Formats.IFF.Chunks.STR.DefaultLangCode))
            {
                try
                {
                    var tsp = Simitone.Client.UI.Model.UIOriginal.EnsureResolved("cpanel\\Backgrounds\\TallSubPanel.TGA")?.Get(GameFacade.GraphicsDevice);
                    if (tsp != null)
                    {
                        TallSubpanel = new UIImage(tsp) { Position = new Microsoft.Xna.Framework.Vector2(0, -50) };
                        Add(TallSubpanel);
                    }
                }
                catch (Exception tse) { GameLog.Write("panel-ctor: tall subpanel EXC " + tse.GetType().Name); TallSubpanel = null; }
            }

            // R144: kBackPatch 2036 (52x100) blits at cpanel-local (168,50) =
            // screen (168, SH-100) — the strip bridging UCP plate and toolbar
            // (cWinCPanel::TSPaint 0x26f510, r144/toolbar-law.md section 0).
            if (Game.Desktop)
            {
                try
                {
                    var bp = UIOriginal.EnsureResolved("cpanel\\Backgrounds\\BackPatch.bmp")?.Get(GameFacade.GraphicsDevice);
                    if (bp != null)
                    {
                        BackPatch = new UIImage(bp) { Position = new Vector2(-52, 0) };
                        Add(BackPatch);
                    }
                }
                catch { }
                // R144: the cWinPeople chrome (8-webcam strip, LiveGadget
                // plaque, 7 category tabs, mood trend arrows) — people-local
                // layout per r144/toolbar-law.md section 1, visible in LIVE.
                PeopleChrome = new UIOriginalPeopleChrome(Game);
                PeopleChrome.OnCategorySelect += (id) => { Switcher_OnCategorySelect(id); };
                Add(PeopleChrome);
                // R245: this panel hosts the person-panel flash surfaces — the
                // webcam strip (portrait identity) and the LIVE subpanel host
                // (the cWinRelationship window equivalent).
                TutorialControlMap.RegisterPeopleChrome(PeopleChrome);
                TutorialControlMap.RegisterMainPanel(this);
                // R145: the BUILD/BUY band chrome (desktop only). Visible per
                // mode in SetMode; the pattern row / item grid live in the
                // browse panel's band branch.
                try
                {
                    ArchChrome = new UIOriginalArchChrome(Game);
                    ArchChrome.Visible = false;
                    ArchChrome.OnToolSelect += (i) => SelectBuildTool(i);
                    Add(ArchChrome);
                }
                catch (Exception ace) { GameLog.Write("panel-ctor: arch chrome EXC " + ace.GetType().Name + " " + ace.Message); ArchChrome = null; }
                try
                {
                    BuyChrome = new UIOriginalBuyChrome();
                    BuyChrome.Visible = false;
                    // R146: expansion lots dispatch to their own band selection
                    // (the plaque row is that lot-type's sort, not room/function).
                    BuyChrome.OnMainSelect += (sort, main) => { if (BuyChrome.IsExpansion) SelectExpMain(main); else SelectBuyMain(sort, main); };
                    BuyChrome.OnSubSelect += (sub) => SelectBuySub(sub);
                    Add(BuyChrome);
                }
                catch (Exception bce) { GameLog.Write("panel-ctor: buy chrome EXC " + bce.GetType().Name + " " + bce.Message); BuyChrome = null; }
                try
                {
                    // cWinCamPanel is a full 804x100 cWinCPanel child, parallel
                    // to cWinPeople/cWinArch/cWinCatalog. Its own class mounts
                    // the exact eight camera controls and two separator strips.
                    CameraChrome = new UIOriginalCameraPanel { Visible = false };
                    // R190: CamViewAlbum (STR# 140:9 'View Photo Album') opens
                    // cWinScrapbook — the family album on the STR# 144 law.
                    CameraChrome.OnViewAlbum += () =>
                        UIScreen.GlobalShowDialog(new UIOriginalScrapbookDialog(), true);
                    Add(CameraChrome);
                }
                catch (Exception cce) { GameLog.Write("panel-ctor: camera chrome EXC " + cce.GetType().Name + " " + cce.Message); CameraChrome = null; }
            }

            // R141: the ONE original speed cluster — REPLACED the old static
            // OriginalSpeedStrip image with real controls on the ORIGINAL member art.
            // R142 ENGINE LAW (r142/tsui-template-law.md): kPause (85,135) 15x15 and
            // kSpeed1/2/3 (105/120/141,135) live ON THE CONTROL-PANEL PLATE, not the
            // toolbar — desktop builds them in UIDesktopUCP; this toolbar-end cluster
            // remains for touch builds only (the 804x100 toolbar's own row is built
            // inside cWinPeople/cWinArch children, still undecoded).
            if (Game.Desktop)
            {
                GameLog.Write("panel-ctor: desktop speed cluster is on the UCP plate (R142 engine anchors)");
            }
            else try
            {
                var sTooltips = new string[]
                {
                    GameFacade.Strings.GetString("138", "16"),
                    GameFacade.Strings.GetString("138", "17"),
                    GameFacade.Strings.GetString("138", "18")
                };
                int rx = (int)(Game.ScreenWidth - X);
                PauseButton = new UIStencilButton(Simitone.Client.UI.Model.UIOriginal.FrameW("cpanel\\Buttons\\pause.bmp", 0, 30)
                    ?? TextureGenerator.GetPxWhite(GameFacade.GraphicsDevice))
                {
                    Position = new Vector2(rx - 150, 40),
                    Tooltip = GameFacade.Strings.GetString("138", "15")
                };
                PauseButton.OnButtonClick += (b) => SwitchSpeed(4);
                Add(PauseButton);
                SpeedButtons = new UIStencilButton[3];
                int[] fw = { 11, 20, 32 };
                int sx = rx - 110;
                for (int i = 0; i < 3; i++)
                {
                    int speed = i + 1;
                    SpeedButtons[i] = new UIStencilButton(Simitone.Client.UI.Model.UIOriginal.FrameW("cpanel\\Buttons\\Speed" + speed + ".bmp", 0, fw[i])
                        ?? TextureGenerator.GetPxWhite(GameFacade.GraphicsDevice))
                    {
                        Position = new Vector2(sx, 47),
                        Tooltip = sTooltips[i]
                    };
                    SpeedButtons[i].OnButtonClick += (b) => SwitchSpeed(speed);
                    Add(SpeedButtons[i]);
                    sx += fw[i] + 4;
                }
                // R144: clock + money moved to the UCP PLATE (engine law
                // r144/toolbar-law.md section 2 — cWinViewControl children:
                // money right-anchored at plate (178,158) font[10], clock
                // digits font[8] centered in plate rect (95,118,151,125);
                // NOTHING time/money renders on the 804x100 toolbar). The
                // twins now live on UIDesktopUCP; Update targets them there.
                GameLog.Write("panel-ctor: original speed cluster mounted (pause+3)");
            }
            catch (Exception sse)
            {
                GameLog.Write("panel-ctor: original speed cluster FAILED - " + sse.GetType().Name + " " + sse.Message);
            }
            WhitePx = TextureGenerator.GetPxWhite(GameFacade.GraphicsDevice);
            var ui = Content.Get().CustomUI;
            Div = ui.Get("panel_div.png").Get(GameFacade.GraphicsDevice);

            if (!Game.Desktop)
            {
                FloorUpBtn = new UIStencilButton(ui.Get("level_up.png").Get(GameFacade.GraphicsDevice));
                FloorUpBtn.Position = new Vector2(80, 10);
                FloorUpBtn.OnButtonClick += (b) => { if (Game.Level < 5) Game.Level++; };
                Add(FloorUpBtn);

                FloorDownBtn = new UIStencilButton(ui.Get("level_down.png").Get(GameFacade.GraphicsDevice));
                FloorDownBtn.Position = new Vector2(80, 68);
                FloorDownBtn.OnButtonClick += (b) => { if (Game.Level > 1) Game.Level--; };
                Add(FloorDownBtn);

                FloorLabel = new UILabel();
                FloorLabel.CaptionStyle = FloorLabel.CaptionStyle.Clone();
                FloorLabel.CaptionStyle.Size = 15;
                FloorLabel.CaptionStyle.Color = UIStyle.Current.Text;
                FloorLabel.Alignment = TextAlignment.Middle | TextAlignment.Center;
                FloorLabel.Position = new Vector2(80, 64);
                FloorLabel.Size = new Vector2(51, 18);

                FloorLabelShadow = new UILabel();
                FloorLabelShadow.CaptionStyle = FloorLabel.CaptionStyle.Clone();
                FloorLabelShadow.Alignment = TextAlignment.Middle | TextAlignment.Center;
                FloorLabelShadow.Position = new Vector2(83, 67);
                FloorLabelShadow.Size = new Vector2(51, 18);
                FloorLabelShadow.CaptionStyle.Color = Color.Black * 0.5f;
                Add(FloorLabelShadow);
                Add(FloorLabel);
                // R99: the ORIGINAL-styled floor readout twin (small .ffn table).
                var smallFont = Simitone.Client.UI.Controls.OriginalGlyphFont.LoadSmall(GameFacade.GraphicsDevice);
                if (smallFont != null)
                {
                    FloorOriginal = new Simitone.Client.UI.Controls.UIOriginalText("1st", smallFont);
                    FloorOriginal.Y = 67;
                    Add(FloorOriginal);
                    FloorLabel.Visible = FloorLabelShadow.Visible = false;
                }

                Divider = new UIImage(ui.Get("divider.png").Get(GameFacade.GraphicsDevice));
                Divider.Position = new Vector2(146, 29);
                Add(Divider);
            }

            // R141: the panel-hide X is mobile-only chrome (no original counterpart;
            // the original toolbar has no collapse control).
            if (!Game.Desktop)
            {
                HideButton = new UIStencilButton(ui.Get("panel_hide.png").Get(GameFacade.GraphicsDevice));
                HideButton.X = Game.ScreenWidth - (50 + 64 + 15);
                HideButton.Y = 26;
                HideButton.OnButtonClick += (b) => { Close(); };
                Add(HideButton);
            }

            Switcher = new UICategorySwitcher();
            Switcher.Position = new Vector2(164 - (Game.Desktop ? 16 : 0), 0);
            Switcher.InitCategories(LiveCategories);
            Switcher.OnCategorySelect += Switcher_OnCategorySelect;
            Switcher.OnOpen += Switcher_OnOpen;
            Add(Switcher);
            if (Game.Desktop && PeopleChrome != null)
            {
                // R144: LIVE is the entry mode — the original tab column owns
                // category selection; the mobile plaque stays hidden until a
                // mode that uses it. (InitCategories above already selected
                // category 0 through the normal event path.)
                Switcher.MainButton.Visible = false;
                PeopleChrome.SetTabSelected(0);
            }

            // R122: the original catalog paging arrows (BUY mode only) — see
            // ToggleBuySortPage. Placement right of the sort switcher is ours
            // (canon gives the art + the tooltip strings, not a position).
            BuyPagePrev = MakeBuyPageButton("cpanel\\Buttons\\ScrollLeft.BMP", "Previous Page", 0);
            BuyPageNext = MakeBuyPageButton("cpanel\\Buttons\\ScrollRight.BMP", "Next Page", 1);
            BuyPagePrev.OnButtonClick += (b) => ToggleBuySortPage();
            BuyPageNext.OnButtonClick += (b) => ToggleBuySortPage();
            BuyPagePrev.Visible = BuyPageNext.Visible = false;
            Add(BuyPagePrev);
            Add(BuyPageNext);

            // R141: the catalog search box is mobile/touch-era chrome — the original
            // catalog has no name search (desktop drops it entirely).
            if (!Game.Desktop)
            {
                CatalogSearchField = new UITextBox();
                CatalogSearchField.SetSize(230, 32);
                CatalogSearchField.Position = new Vector2(263, -32);
                CatalogSearchField.TextMargin = new Rectangle(8, 8, 8, 8);
                CatalogSearchField.Visible = false;
                CatalogSearchField.OnChange += (elem) =>
                {
                    (SubPanel as UIBuyBrowsePanel)?.ApplyNameFilter(CatalogSearchField.CurrentText);
                };
                Add(CatalogSearchField);

                SearchPlaceholder = new UILabel();
                SearchPlaceholder.Caption = "Search selected category\u2026";
                SearchPlaceholder.Position = new Vector2(271, -24);
                SearchPlaceholder.Size = new Vector2(214, 20);
                SearchPlaceholder.CaptionStyle = SearchPlaceholder.CaptionStyle.Clone();
                SearchPlaceholder.CaptionStyle.Size = 12;
                SearchPlaceholder.CaptionStyle.Color = UIStyle.Current.Text * 0.4f;
                SearchPlaceholder.Visible = false;
                Add(SearchPlaceholder);
            }

            foreach (var fade in GetFadeables())
            {
                fade.Opacity = 0;
            }

            Game.LotControl.QueryPanel.Position = new Vector2(53 + (Game.Desktop ? 25 : 0), -5);
            Add(Game.LotControl.QueryPanel);
            Game.LotControl.PickupPanel.Opacity = 0;
            Add(Game.LotControl.PickupPanel);

            CurWidth = 0;
        }

        private void Switcher_OnOpen()
        {
            var panel = SubPanel as UIBuyBrowsePanel;
            if (panel != null)
            {
                panel.Reset();
            }
        }

        // R122: one original paging arrow. Frame() crops 4-frame sheets to the up
        // frame and passes single art through; null art falls back to the modern
        // arrows so paging never breaks on a missing member.
        private UIStencilButton MakeBuyPageButton(string member, string tooltip, int slot)
        {
            UIStencilButton btn;
            var tex = UIOriginal.Frame(member, 0);
            if (tex != null) btn = new UIStencilButton(tex);
            else btn = new UIStencilButton(Content.Get().CustomUI.Get(slot == 0 ? "level_up.png" : "level_down.png").Get(GameFacade.GraphicsDevice));
            btn.Tooltip = GameFacade.Strings.GetString("154", slot.ToString());
            btn.X = Switcher.X + 88;
            btn.Y = slot == 0 ? 15 : 66;
            return btn;
        }

        public void SetMode(UIMainPanelMode mode)
        {
            if (mode == Mode) return;
            Mode = mode;

            // The original cWinCPanel::SetPanel hides the previous mode child
            // before showing the replacement. Make the mode boundary atomic:
            // a retiring live panel must not remain visible during its 0.3s
            // cleanup tween behind the build/buy band.
            ModeTransitioning = true;
            try
            {
                // A rapid category change can leave more than the current
                // SubPanel mounted while its cleanup tween runs. Hide every
                // mounted subpanel at the mode boundary, not only the latest
                // one, or an older retiring catalog can still draw into the
                // replacement mode for the rest of its 300ms lifetime.
                foreach (var retiring in GetChildren().OfType<UISubpanel>())
                    retiring.Visible = false;
                ApplyMode(mode);
            }
            finally
            {
                ModeTransitioning = false;
            }
        }

        private void ApplyMode(UIMainPanelMode mode)
        {
            // These are mutually exclusive cWinCPanel children. Clear every
            // desktop mode chrome first so BUILD <-> BUY also cannot retain the
            // previously visible child; the selected branch below enables one.
            if (Game.Desktop)
            {
                if (PeopleChrome != null) PeopleChrome.Visible = false;
                if (ArchChrome != null) ArchChrome.Visible = false;
                if (BuyChrome != null) BuyChrome.Visible = false;
                if (CameraChrome != null) CameraChrome.Visible = false;
            }

            Game.LotControl.World.State.BuildMode = 0;
            switch (mode)
            {
                case UIMainPanelMode.LIVE:
                    Switcher.InitCategories(LiveCategories);
                    Switcher.MainButton.OriginalStyle = Switcher.OriginalChrome;
                    break;
                case UIMainPanelMode.BUY:
                    Switcher.InitCategories(CurrentBuyCategories());
                    Switcher.MainButton.OriginalStyle = true;
                    Game.LotControl.World.State.BuildMode = 1;
                    break;
                case UIMainPanelMode.BUILD:
                    Switcher.InitCategories(BuildCategories);
                    Switcher.MainButton.OriginalStyle = Switcher.OriginalChrome;
                    Game.LotControl.World.State.BuildMode = 2;
                    break;
                case UIMainPanelMode.OPTIONS:
                    Switcher.InitCategories(OptionsCategories);
                    Switcher.MainButton.OriginalStyle = Switcher.OriginalChrome;
                    break;
                case UIMainPanelMode.CAMERA:
                    // Camera has its own complete cWinCamPanel child; there is
                    // no category-switcher or catalog subpanel in this mode.
                    SetSubpanel(null);
                    break;
            }
            // R145a/R146: the mobile paging arrows only show when the mobile
            // plaque composition owns BUY (the band chrome — now on EVERY
            // desktop lot — has its own res 102/101 arrows).
            bool bandBuy = Game.Desktop && BuyChrome != null && mode == UIMainPanelMode.BUY;
            if (BuyPagePrev != null) BuyPagePrev.Visible = BuyPageNext.Visible = (mode == UIMainPanelMode.BUY) && !bandBuy;

            var live = (mode == UIMainPanelMode.LIVE);
            Game.LotControl.LiveMode = live;
            if (HideButton != null) HideButton.Visible = live;   // R141: desktop has no hide button

            // R144: the cWinPeople chrome (webcam strip / tab column / trend
            // arrows) is LIVE-only, and in LIVE the ORIGINAL tab column
            // replaces the mobile category plaque; other desktop modes keep
            // the plaque (the cWinCatalog tab row is a future round).
            // R145: BUILD/BUY replace the mobile plaque with the decoded band
            // chrome (cWinArch tools / cWinCatalog plaques); the band entry
            // states follow the engine laws (BUILD: no tool selected, empty
            // pattern row; BUY: room main, Living, default subsort applied).
            if (Game.Desktop)
            {
                if (PeopleChrome != null) PeopleChrome.Visible = live;
                bool plaque = !live && ArchChrome == null && BuyChrome == null;   // fallback if chrome failed
                // R146: the decoded band now owns BUY on EVERY lot — home lots
                // use the room/function sorts (first entry: room main, Living,
                // default subsort applied), expansion lots (Downtown/Vacation/
                // Community/Studio/Magic) lock to their own sort: main state,
                // main 0 (R145a's Normal-only gate left those lots on the R122
                // mobile composition — the "duplicate overlapping buttons").
                if (mode == UIMainPanelMode.BUILD && ArchChrome != null)   // build law is lot-independent
                {
                    Switcher.MainButton.Visible = false;
                    ArchChrome.Visible = true;
                    ArchChrome.SetRoofMode(false);
                    SetSubpanel(null);   // engine: pattern row empty until a tool is picked
                }
                else if (mode == UIMainPanelMode.BUY && BuyChrome != null)
                {
                    Switcher.MainButton.Visible = false;
                    BuyChrome.Visible = true;
                    var lotMode = GetLotType(false);
                    BuyChrome.SetLotMode(lotMode);
                    BuyChrome.ApplyEngineDefault();
                    if (lotMode == UICatalogMode.Normal) SelectBuyMain(0, 0, true);   // room Living + default subsort 0
                    else SelectExpMain(0, true);   // expansion sort: main state, main 0, subcatalog 0 applied
                }
                else if (mode == UIMainPanelMode.CAMERA)
                {
                    Switcher.MainButton.Visible = false;
                    if (CameraChrome != null)
                    {
                        CameraChrome.Visible = true;
                        CameraChrome.RefreshSelection();
                    }
                }
                else if (mode == UIMainPanelMode.OPTIONS)
                {
                    // cWinOptions itself owns the full 804x100 band. The green
                    // house/category plaque was Simitone mobile chrome.
                    Switcher.MainButton.Visible = false;
                }
                else
                {
                    if (ArchChrome != null) ArchChrome.Visible = false;
                    if (BuyChrome != null) BuyChrome.Visible = false;
                    Switcher.MainButton.Visible = !live || plaque;
                }
            }
            else if (HideButton != null)
            {
                HideButton.Visible = live;
            }

            // R193: the tall people-window backdrop serves LIVE desktop only
            if (TallSubpanel != null) TallSubpanel.Visible = Game.Desktop && mode == UIMainPanelMode.LIVE;

            ModeChanged?.Invoke(mode);
        }

        public void Switcher_OnCategorySelect(int obj)
        {
            // R185: desktop cWinPeople tabs call this route directly instead of
            // UICategorySwitcher.Select. Keep that hidden switcher's state in
            // sync so family-selector completion and panel reopen restore the
            // accepted native panel rather than snapping back to Mood.
            //
            // Native SetPanel rejects every People request when no person is
            // selected, and id 4 is not one of its seven visible categories.
            // Rejection clears the host and all tab highlights without changing
            // the last accepted category.
            if (Mode == UIMainPanelMode.LIVE
                && (!IsNativeLiveCategory(obj) || !HasSelectedPerson()))
            {
                PeopleChrome?.SetTabSelected(-1);
                SetSubpanel(null);
                return;
            }

            // cWinPeople::SetPanel accepts House only for native zoning type
            // 1 (residential). TS1NeighbourProvider stores the same two LotZoning
            // strings as 0=residential / 1=community, so translate the semantic
            // value instead of comparing the incompatible numeric encodings.
            if (Mode == UIMainPanelMode.LIVE && obj == 5 && !HasNativeHousePanelZoning())
            {
                PeopleChrome?.SetTabSelected(-1);
                SetSubpanel(null);
                return;
            }

            if (Switcher != null) Switcher.ActiveCategory = obj;
            // R144: keep the original tab column's highlight in sync with any
            // accepted selection path (mobile plaque, autotest, tab clicks).
            if (Mode == UIMainPanelMode.LIVE && PeopleChrome != null)
            {
                var tab = System.Array.IndexOf(UIOriginalPeopleChrome.TabCategory, obj);
                PeopleChrome.SetTabSelected(tab);
            }
            UISubpanel panel = null;
            switch (Mode)
            {
                case UIMainPanelMode.LIVE:
                    switch (obj)
                    {
                        case 0:
                            panel = new UIMotiveSubpanel(Game); break;
                        case 1:
                            panel = new UIJobSubpanel(Game); break;
                        case 2:
                            panel = new UIPersonalitySubpanel(Game); break;
                        case 3:
                            panel = new UIRelationshipSubpanel(Game); break;
                        case 5:
                            panel = new UIHouseSubpanel(Game); break;
                        // R145: the Interest/Gift tabs (engine people panels 6/7,
                        // r145/interest-gift-law.md) — Live.iff STR# 140/141
                        // surfaces on the band law.
                        case 6:
                            panel = new UIOriginalInterestSubpanel(Game); break;
                        case 7:
                            panel = new UIOriginalGiftSubpanel(Game); break;
                    }
                    break;
                case UIMainPanelMode.BUY:
                    // R122: IDs >= 100 are ROOM sorts (STR# 150 [0..7], original
                    // RoomFlags bit = ID - 100); 0-7 stay the catalog function IDs.
                    panel = new UIBuyBrowsePanel(Game, (sbyte)(obj >= 100 ? obj - 100 : obj), GetLotType(false), obj >= 100);
                    break;
                case UIMainPanelMode.BUILD:
                    panel = new UIBuyBrowsePanel(Game, (sbyte)obj, UICatalogMode.Build);
                    break;
                case UIMainPanelMode.OPTIONS:
                    // R121: the ORIGINAL options screen (STR# 145 'optionstrs' + the
                    // original cpanel button art) — six canon-positioned buttons incl.
                    // the Graphics/Sound/Play sub-screens and their About popups.
                    panel = new UIOriginalOptionsPanel(Game);
                    break;
            }
            SetSubpanel(panel);
        }

        internal static bool IsNativeLiveCategory(int category)
        {
            return System.Array.IndexOf(UIOriginalPeopleChrome.TabCategory, category) >= 0;
        }

        private bool HasSelectedPerson()
        {
            try { return Game?.vm != null && Game.SelectedAvatar != null; }
            catch { return false; }
        }

        private bool HasNativeHousePanelZoning()
        {
            try
            {
                short house = Game.vm.GetGlobalValue(10);
                short localZoning;
                bool found = Content.Get().Neighborhood.ZoningDictionary.TryGetValue(house, out localZoning);
                return IsNativeHousePanelZoning(house, found, localZoning);
            }
            catch { return false; }
        }

        /// <summary>
        /// Native Neighborhood::GetZoningType returns 1 for house zero and for
        /// the LotZoning word "residential", 2 for "community", and 0 when a
        /// nonzero house is absent. The port's dictionary encodes those two
        /// words as 0 and 1 respectively.
        /// </summary>
        internal static bool IsNativeHousePanelZoning(short house, bool found, short localZoning)
        {
            if (house == 0) return true;
            return found && localZoning == 0;
        }

        public UICatalogMode GetLotType(bool music)
        {
            var house = Game.vm.GetGlobalValue(10);
            var zones = Content.Get().Neighborhood.ZoningDictionary;
            short result = 1;
            zones.TryGetValue(house, out result);
            var community = result == 1;
            return GetLotTypeForHouse(house, music, community, Game.vm.GetGlobalValue(32) > 0);
        }

        // R176: keep the original expansion address bands disjoint. Private
        // Magic lots 90-92 use the normal residential BUY catalog, but still
        // classify as Magic for music/neighborhood routing; public 93-99 use
        // the Magic catalog through their community zoning.
        internal static UICatalogMode GetLotTypeForHouse(short house, bool music,
            bool community, bool downtownMusic)
        {
            if (house >= 21 && house <= 31) return UICatalogMode.Downtown;
            if (house >= 40 && house <= 49) return UICatalogMode.Vacation;
            if (house >= 81 && house <= 89) return UICatalogMode.Studiotown;
            if (house >= 90 && house <= 99 && (music || community)) return UICatalogMode.Magictown;
            if (community) return UICatalogMode.Community;
            return (music && downtownMusic) ? UICatalogMode.Downtown : UICatalogMode.Normal;
        }

        // ---------------- R145: desktop band selection paths ----------------

        /// A build tool was clicked (engine CPState::ArchSetCurrentTool —
        /// UpdateViewFromCPState rebuilds the pattern row for the tool; the
        /// Roof tool swaps it for the roof panel, the Hand tool is
        /// selection-only). Tool -> subcategory via the r145 law map.
        public void SelectBuildTool(int toolIndex)
        {
            if (ArchChrome == null) return;
            if (Mode != UIMainPanelMode.BUILD) return;
            if (toolIndex == 10)
            {
                // engine SetRoofMode(true): pattern row + arrows hidden, the
                // roof panel (314,5) takes their place.
                SetSubpanel(null);
                ArchChrome.SetRoofMode(true);
                return;
            }
            ArchChrome.SetRoofMode(false);
            if (toolIndex == 11) { SetSubpanel(null); return; }   // Hand: no catalog
            int g = UIOriginalArchChrome.ToolSubcat[toolIndex, 0];
            int s = UIOriginalArchChrome.ToolSubcat[toolIndex, 1];
            if (g < 0) return;
            try
            {
                var subcat = LiveSubpanels.UIBuyBrowsePanel.BuildCategories[g][s];
                var panel = new LiveSubpanels.UIBuyBrowsePanel(Game, (sbyte)g, UICatalogMode.Build);
                SetSubpanel(panel);
                panel.InitSubcategory(subcat);
            }
            catch (Exception be) { GameLog.Write("select-buildtool EXC " + be.GetType().Name + " " + be.Message); }
        }

        /// A buy main plaque was clicked (engine ShopSet{Room,Function}Catalog
        /// + ShopSetSortState(1)): create the room/function browse panel and
        /// switch the plaque row to the subsorts. applyDefaultSub mirrors the
        /// engine's entry composition (subcatalog 0 applied, main state kept).
        public void SelectBuyMain(int sort, int main, bool applyDefaultSub = false)
        {
            if (BuyChrome == null || Mode != UIMainPanelMode.BUY) return;
            try
            {
                LiveSubpanels.UIBuyBrowsePanel panel;
                List<LiveSubpanels.UICatalogSubcat> cats = null;
                if (sort == 0)
                {
                    panel = new LiveSubpanels.UIBuyBrowsePanel(Game, (sbyte)main, GetLotType(false), true);
                    // room subsorts = the 8 function sorts (kBuyRSubSort*)
                    cats = new List<LiveSubpanels.UICatalogSubcat>();
                    for (int i = 0; i < 8; i++)
                        cats.Add(new LiveSubpanels.UICatalogSubcat
                        {
                            StrTable = 150,
                            StrInd = 8 + i,
                            FuncId = LiveSubpanels.UIBuyBrowsePanel.CanonFuncToCatalogID[i],
                            OriginalName = LiveSubpanels.UIBuyBrowsePanel.RoomSubSortArt[i],
                        });
                }
                else
                {
                    var def = BuyFunctionDefs[main];
                    panel = new LiveSubpanels.UIBuyBrowsePanel(Game, (sbyte)def.id, GetLotType(false));
                    // the panel's own InitCategory pass resolves the family's
                    // subsort art (OriginalName) — read it back, not the raw static.
                    cats = panel.CurrentSubCats ?? LiveSubpanels.UIBuyBrowsePanel.Categories[LiveSubpanels.UIBuyBrowsePanel.RemapString[def.id]];
                }
                SetSubpanel(panel);
                BuyChrome.SubCats = cats;   // sub-plaque clicks read this
                if (applyDefaultSub && cats != null && cats.Count > 0)
                {
                    // engine entry composition: subcatalog 0 APPLIED to the
                    // grid while the plaque row stays in the MAIN state.
                    panel.InitSubcategory(cats[0]);
                    BuyChrome.SetSubSelected(0);
                }
                else
                {
                    BuyChrome.RelayoutSub(cats);
                    BuyChrome.SetSubSelected(0);
                }
            }
            catch (Exception be)
            {
                var st = (be.StackTrace ?? "").Split('\n');
                GameLog.Write("select-buymain EXC " + be.GetType().Name + " " + be.Message
                    + (st.Length > 0 ? " AT " + st[0].Trim() : "") + (st.Length > 1 ? " / " + st[1].Trim() : ""));
            }
        }

        /// A buy subsort plaque was clicked (engine ShopSet*SubCatalog(i)).
        public void SelectBuySub(int sub)
        {
            if (SubPanel is LiveSubpanels.UIBuyBrowsePanel panel && BuyChrome != null)
            {
                var cats = BuyChrome.SubCats;
                if (cats != null && sub < cats.Count) panel.InitSubcategory(cats[sub]);
                BuyChrome.SetSubSelected(sub);
            }
        }

        /// R146: an EXPANSION-lot main plaque was clicked (engine ShopSetSortState(1)
        /// + ShopSet{Downtown,Vacation,Community,Studio,Magic}Catalog(i) +
        /// SubCatalog(0)): the browse panel re-filters to the main's mask
        /// (slot 0-3 or 7=Misc, bit 1<<slot) and the plaque row becomes the
        /// 8 function subsorts with the toggle plaque (BuyBack{C,V,D,...}).
        public void SelectExpMain(int mainSlot, bool applyDefaultSub = false)
        {
            if (BuyChrome == null || Mode != UIMainPanelMode.BUY) return;
            try
            {
                var lotMode = GetLotType(false);
                var panel = new LiveSubpanels.UIBuyBrowsePanel(Game, (sbyte)mainSlot, lotMode);
                SetSubpanel(panel);
                // the subsort row: the panel's BandMode cats are the 8 function
                // subsorts with the mode's own subsort art.
                var cats = panel.CurrentSubCats;
                BuyChrome.SubCats = cats;
                if (applyDefaultSub)
                {
                    // engine ENTRY composition: subcatalog 0 applied to the grid
                    // while the plaque row stays in the MAIN state.
                    if (cats != null && cats.Count > 0) panel.InitSubcategory(cats[0]);
                }
                else
                {
                    // engine main-CLICK law: SortState(1) + SubCatalog(0).
                    if (cats != null && cats.Count > 0) panel.InitSubcategory(cats[0]);
                    BuyChrome.RelayoutSub(cats);
                    BuyChrome.SetSubSelected(0);
                }
            }
            catch (Exception be)
            {
                var st = (be.StackTrace ?? "").Split('\n');
                GameLog.Write("select-expmain EXC " + be.GetType().Name + " " + be.Message
                    + (st.Length > 0 ? " AT " + st[0].Trim() : "") + (st.Length > 1 ? " / " + st[1].Trim() : ""));
            }
        }

        private List<LiveSubpanels.UICatalogSubcat> RoomSubcatList()
        {
            var cats = new List<LiveSubpanels.UICatalogSubcat>();
            for (int i = 0; i < 8; i++)
                cats.Add(new LiveSubpanels.UICatalogSubcat
                {
                    StrTable = 150,
                    StrInd = 8 + i,
                    FuncId = LiveSubpanels.UIBuyBrowsePanel.CanonFuncToCatalogID[i],
                    OriginalName = LiveSubpanels.UIBuyBrowsePanel.RoomSubSortArt[i],
                });
            return cats;
        }

        /// the engine's Objects-button toggle: every buy activation flips
        /// room <-> function (buy-catalog-law.md section 2).
        public void ToggleBuySortEngine()
        {
            if (BuyChrome == null || GetLotType(false) != UICatalogMode.Normal) return;
            BuyChrome.ToggleSort();
            SelectBuyMain(BuyChrome.Sort, BuyChrome.CurrentMain, true);
        }

        public void SetSubpanel(UISubpanel sub)
        {
            // R171: desktop cWinCPanel/cWinPeople replacement is atomic. The
            // original SetPanel hides the old child before showing its
            // replacement; it never cross-fades two category panels. A rapid
            // Mood -> Needs/Job switch could leave more than one retiring
            // UISubpanel in its 300 ms Kill tween, drawing duplicate gauges and
            // labels behind the active panel. Hide every retiring desktop
            // subpanel synchronously (including an older one already waiting
            // for removal), while retaining the touch UI's authored fades.
            if (Game.Desktop)
            {
                foreach (var retiring in GetChildren().OfType<UISubpanel>())
                    retiring.Visible = false;
            }
            if (SubPanel != null)
            {
                // Clear any active search before replacing the panel
                (SubPanel as UIBuyBrowsePanel)?.ApplyNameFilter("");
                // UISubpanel.Kill retains its delayed cleanup. Original desktop
                // SetPanel hides the outgoing child synchronously; leaving it
                // drawable is what put live gauges/icons over the replacement.
                if (Game.Desktop || ModeTransitioning) SubPanel.Visible = false;
                SubPanel.Kill();
            }
            SubPanel = sub;
            var isBrowsePanel = sub is UIBuyBrowsePanel;
            if (CatalogSearchField != null)
            {
                CatalogSearchField.CurrentText = "";
                CatalogSearchField.Visible = isBrowsePanel;
                SearchPlaceholder.Visible = isBrowsePanel;
            }
            if (sub != null)
            {
                // R144: desktop LIVE sub-panels mount at the engine's people
                // host anchor (300,50 people-local = MainPanel-local (300,0),
                // r144/toolbar-law.md section 1.7); the mobile column keeps
                // its own (263,0) frame. R145: desktop BUILD/BUY mount at
                // (0,0) covering the WHOLE band — the engine band children
                // (cWinArch/cWinCatalog) own the full 804x100 area. R174:
                // cWinOptions is another full-band cWinCPanel child. Width
                // always ends at the screen's right edge.
                int mountX = 263;
                if (Game.Desktop)
                    mountX = (Mode == UIMainPanelMode.LIVE) ? 300
                        : (Mode == UIMainPanelMode.BUILD || Mode == UIMainPanelMode.BUY || Mode == UIMainPanelMode.OPTIONS) ? 0 : 263;
                SubPanel.Position = new Vector2(mountX, 0);
                var subpanelWidth = Game.ScreenWidth - 220 - mountX;
                if (Game.Desktop && Mode == UIMainPanelMode.LIVE)
                {
                    var nativeWidth = UISubpanel.NativeDesktopPeopleWidth(SubPanel, Game.ScreenWidth);
                    if (nativeWidth > 0) subpanelWidth = nativeWidth;
                }
                SubPanel.Size = new Vector2(subpanelWidth, Game.Desktop ? 100 : 128);
                if (Game.Desktop || ModeTransitioning)
                {
                    // UISubpanel starts a 0 -> 1 constructor tween before this
                    // owner can see the instance. Original desktop SetPanel
                    // composes every incoming child directly, so hold it at
                    // full opacity for that tween's lifetime. UITween evaluates
                    // in insertion order; this later 1 -> 1 hold is therefore
                    // the final opacity write each frame. Touch layouts retain
                    // their normal category fade.
                    SubPanel.Opacity = 1f;
                    GameFacade.Screens.Tween.To(SubPanel, 0.3f,
                        new Dictionary<string, float>() { { "Opacity", 1f } });
                }
                Add(SubPanel);
                if (SubPanel is LiveSubpanels.UIBuyBrowsePanel bp && bp.BandMode) bp.RebuildBand();
            }
        }

        public void SetSubpanelPickup(float opacity)
        {
            //used to hide subpanels to make way for the PickupPanel
            if (SubPanel != null) GameFacade.Screens.Tween.To(SubPanel, 0.3f, new Dictionary<string, float>() { { "Opacity", opacity } }, TweenQuad.EaseOut);
            GameFacade.Screens.Tween.To(Switcher.MainButton, 0.3f, new Dictionary<string, float>() { { "Opacity", opacity } }, TweenQuad.EaseOut);
            GameFacade.Screens.Tween.To(Game.LotControl.PickupPanel, 0.3f, new Dictionary<string, float>() { { "Opacity", 1-opacity } }, TweenQuad.EaseOut);
            if (opacity == 0) Switcher.Close();
        }

        private void UpdateWidth()
        {
            //prepanel width is 167
            //div width is 52

            var iWidth = (int)CurWidth;
            if (Diag == null)
            {
                // R141: desktop has no diagonal chrome — only the divider rect tracks width.
                DivRect = new Rectangle(0, 0, System.Math.Min(System.Math.Max(iWidth - 211, 0), 52), 128);
                return;
            }
            if (iWidth < 211)
            {
                Diag.X = 0;
                Diag.BodySize = new Point(iWidth, 128);
                DivRect = new Rectangle();
            } else if (iWidth < 211+52)
            {
                Diag.X = iWidth;
                Diag.BodySize = new Point(0, 128);
                DivRect = new Rectangle(0, 0, iWidth - 211, 128);
            } else
            {
                Diag.X = 211 + 52;
                Diag.BodySize = new Point(iWidth - (211 + 52), 128);
                DivRect = new Rectangle(0, 0, 52, 128);
            }
        }

        public UIElement[] GetFadeables()
        {
            if (Game.Desktop)
            {
                // R141: no hide button on desktop
                return new UIElement[]
                {
                Switcher.MainButton
                };
            }
            else
            {
                return new UIElement[]
                {
                FloorUpBtn,
                FloorDownBtn,
                FloorLabel,
                FloorLabelShadow,
                Switcher.MainButton,
                Divider,
                HideButton
                };
            }
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            if (CatalogSearchField != null && CatalogSearchField.Visible)
            {
                // Draw a solid background behind the search field so it's readable against any backdrop
                DrawLocalTexture(batch, WhitePx, null,
                    new Vector2(CatalogSearchField.X - 2, CatalogSearchField.Y - 2),
                    new Vector2(CatalogSearchField.Size.X + 4, CatalogSearchField.Size.Y + 4),
                    UIStyle.Current.TitleBg);
            }

            if (CurWidth > 211)
            {
                if (OriginalPanelBack != null && (Game.Desktop || Mode == UIMainPanelMode.LIVE))
                {
                    // R85/R141: the original PanelBack is the UNIVERSAL toolbar chrome
                    // (kUniversalCPBkg, one bitmap for every mode) — never paint
                    // Simitone's flat navy over it on desktop, in any mode.
                }
                else if (ShowingSelect)
                {
                    DrawLocalTexture(batch, WhitePx, null, new Vector2(0, 0), new Vector2(211+52, 128), UIStyle.Current.Bg);
                }
                else
                {
                    DrawLocalTexture(batch, WhitePx, null, new Vector2(0, 0), new Vector2(211, 128), UIStyle.Current.Bg);
                    DrawLocalTexture(batch, Div, DivRect, new Vector2(211, 0), Vector2.One, UIStyle.Current.Bg);
                }
            }
            base.Draw(batch);

        }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            Visible = _CurWidth > 0;

            // cWinPeople owns no content host without a selected person. Clear
            // an already-mounted panel as soon as control is lost so panels
            // whose own Update returns early cannot leave stale values painted.
            if (Game.Desktop && Mode == UIMainPanelMode.LIVE && !HasSelectedPerson())
            {
                if (SubPanel != null) SetSubpanel(null);
                if (PeopleChrome != null && PeopleChrome.ActiveTab != -1)
                    PeopleChrome.SetTabSelected(-1);
            }

            if (!Game.Desktop)
            {
                if (Game.Level != LastFloor)
                {
                    LastFloor = Game.Level;
                    FloorLabel.Caption = FloorNames[LastFloor - 1];
                    FloorLabelShadow.Caption = FloorNames[LastFloor - 1];
                    FloorDownBtn.Disabled = LastFloor == 1;
                    FloorUpBtn.Disabled = LastFloor == 5;
                    if (FloorOriginal != null)
                    {
                        FloorOriginal.Text = FloorNames[LastFloor - 1];
                        FloorOriginal.X = System.Math.Max(80, 80 + (51 - FloorOriginal.Font.Measure(FloorNames[LastFloor - 1])) / 2);
                        Simitone.Client.UI.Controls.UIOriginalText.ToolbarReadoutsDrawn++;
                    }
                }
            }

            Game.LotControl.PickupPanel.Visible = Game.LotControl.PickupPanel.Opacity > 0;

            // R141/R144: the readout runtime sync. Since R144 the twins live on
            // the UCP PLATE (engine law section 2: money right-anchored at plate
            // (178,158) font[10]; clock digits font[8] centered in (95,118,151,125)).
            if (Game.Desktop && Game.vm != null && Game.vm.Context != null)
            {
                var ucp = (Game as Simitone.Client.UI.Screens.TS1GameScreen).Frontend?.DesktopUCP;
                var clock = Game.vm.Context.Clock;
                // R159: AM/PM from the original corpus (STR# 138 [21]/[22] —
                // the r117 'mapped-UNUSED' entries, now wired).
                var amStr = FSO.Client.GameFacade.Strings.GetString("138", "21");
                var pmStr = FSO.Client.GameFacade.Strings.GetString("138", "22");
                if (string.IsNullOrEmpty(amStr) || amStr.StartsWith("138:")) amStr = "AM";
                if (string.IsNullOrEmpty(pmStr) || pmStr.StartsWith("138:")) pmStr = "PM";
                int ch = clock.Hours; var suffix = (ch > 11) ? pmStr : amStr;
                ch %= 12; if (ch == 0) ch = 12;
                var text = ch.ToString() + ":" + clock.Minutes.ToString().PadLeft(2, '0') + " " + suffix;
                if (text != _lastClockText)
                {
                    _lastClockText = text;
                    if (ucp != null) ucp.SetClockText(text);
                }
                var money = Game.ActiveFamily?.Budget ?? 0;
                if (money != _lastMoney)
                {
                    if (_lastMoney != 0 || money != 0) ucp?.DisplayChange(money - _lastMoney);
                    _lastMoney = money;
                    if (ucp != null) ucp.SetMoneyText(money);
                }
            }

            if (Mode != UIMainPanelMode.LIVE)
            {
                Game.vm.SpeedMultiplier = -1;
            } else if (Game.vm.SpeedMultiplier == -1)
            {
                Game.vm.SpeedMultiplier = 0;
            }

            // R136: the route-history sampler behind the House tab's Layout
            // rating (the engine samples every 10th movement tick per Sim in
            // the VM; the port samples room transitions every 30 frames from
            // the always-mounted main panel — see OriginalRouteHistory).
            if (Mode == UIMainPanelMode.LIVE && (RouteFrame++ % 30) == 0)
            {
                Simitone.Client.UI.Model.OriginalRouteHistory.Sample(Game.vm);
            }

            // Update search placeholder visibility
            if (CatalogSearchField != null && CatalogSearchField.Visible)
            {
                // In build mode, FullCategory has wrong data until a subcategory is chosen.
                // Disable and explain the field so it doesn't appear broken.
                var browsePanel = SubPanel as UIBuyBrowsePanel;
                bool buildNeedsSubcat = browsePanel != null
                    && browsePanel.ChoosingSub
                    && browsePanel.Mode == UICatalogMode.Build;

                if (buildNeedsSubcat)
                {
                    SearchPlaceholder.Caption = "Select a subcategory first\u2026";
                    CatalogSearchField.Mode = UITextEditMode.ReadOnly;
                    CatalogSearchField.Opacity = 0.45f;
                    SearchPlaceholder.Visible = true;
                }
                else
                {
                    SearchPlaceholder.Caption = "Search selected category\u2026";
                    CatalogSearchField.Mode = UITextEditMode.Editor;
                    CatalogSearchField.Opacity = 1f;
                    SearchPlaceholder.Visible = !CatalogSearchField.HasText;
                }

                var searchFocused = state.InputManager.GetFocus() == CatalogSearchField;

                // Click outside the search field → remove focus
                var leftDown = state.MouseState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
                if (searchFocused && leftDown && !_lastLeftButtonDown)
                {
                    var localMouse = GlobalPoint(new Vector2(state.MouseState.X, state.MouseState.Y));
                    var fieldBounds = new Rectangle(
                        (int)CatalogSearchField.X, (int)CatalogSearchField.Y,
                        (int)CatalogSearchField.Size.X, (int)CatalogSearchField.Size.Y);
                    if (!fieldBounds.Contains((int)localMouse.X, (int)localMouse.Y))
                        state.InputManager.SetFocus(null);
                }
                _lastLeftButtonDown = leftDown;

                // Two-stage Escape: clear text first; close panel only when field is already empty
                if (searchFocused && state.NewKeys.Contains(Microsoft.Xna.Framework.Input.Keys.Escape))
                {
                    if (CatalogSearchField.HasText)
                    {
                        CatalogSearchField.CurrentText = "";
                        (SubPanel as UIBuyBrowsePanel)?.ApplyNameFilter("");
                    }
                    // When field is empty, Escape falls through to normal panel behaviour (none currently)
                }
            }
        }

        private string _lastClockText = "";
        private int _lastSpeedSel = -1;
        private int _lastMoney;

        // R141: speed switching moved from the desktop UCP to the toolbar's own
        // cluster (original kPause/kSpeed1-3 semantics; sounds unchanged).
        public void SwitchSpeed(int speed)
        {
            var vm = Game.vm;
            if (vm.SpeedMultiplier == -1) return;
            switch (vm.SpeedMultiplier)
            {
                case 0:
                    switch (speed)
                    {
                        case 1: FSO.HIT.HITVM.Get().PlaySoundEvent(UISounds.SpeedPTo1); break;
                        case 2: FSO.HIT.HITVM.Get().PlaySoundEvent(UISounds.SpeedPTo2); break;
                        case 3: FSO.HIT.HITVM.Get().PlaySoundEvent(UISounds.SpeedPTo3); break;
                    }
                    break;
                case 1:
                    switch (speed)
                    {
                        case 4: FSO.HIT.HITVM.Get().PlaySoundEvent(UISounds.Speed1ToP); break;
                        case 2: FSO.HIT.HITVM.Get().PlaySoundEvent(UISounds.Speed1To2); break;
                        case 3: FSO.HIT.HITVM.Get().PlaySoundEvent(UISounds.Speed1To3); break;
                    }
                    break;
                case 3:
                    switch (speed)
                    {
                        case 4: FSO.HIT.HITVM.Get().PlaySoundEvent(UISounds.Speed2ToP); break;
                        case 1: FSO.HIT.HITVM.Get().PlaySoundEvent(UISounds.Speed2To1); break;
                        case 3: FSO.HIT.HITVM.Get().PlaySoundEvent(UISounds.Speed2To3); break;
                    }
                    break;
                case 10:
                    switch (speed)
                    {
                        case 4: FSO.HIT.HITVM.Get().PlaySoundEvent(UISounds.Speed3ToP); break;
                        case 1: FSO.HIT.HITVM.Get().PlaySoundEvent(UISounds.Speed3To1); break;
                        case 2: FSO.HIT.HITVM.Get().PlaySoundEvent(UISounds.Speed3To2); break;
                    }
                    break;
            }
            switch (speed)
            {
                case 4: vm.SpeedMultiplier = 0; break;
                case 1: vm.SpeedMultiplier = 1; break;
                case 2: vm.SpeedMultiplier = 3; break;
                case 3: vm.SpeedMultiplier = 10; break;
            }
        }

        // R115/R141: money-change FLOATERS in original glyphs (relocated from the
        // desktop UCP together with the funds readout).
        public static int FloatersTwinned = 0;

        public void DisplayChange(int change)
        {
            if (change == 0) return;
            var text = ((change > 0) ? "+" : "-") + "§" + Math.Abs(change);
            if (MoneyOriginal != null && MoneyOriginal.Font != null && MoneyOriginal.Font.Atlas != null)
            {
                var twin = new Simitone.Client.UI.Controls.UIOriginalText(text, MoneyOriginal.Font)
                {
                    Color = (change > 0) ? UIStyle.Current.Text : UIStyle.Current.NegMoney
                };
                twin.Position = new Vector2(MoneyOriginal.X, MoneyOriginal.Y - 20f);
                Add(twin);
                GameFacade.Screens.Tween.To(twin, 1.5f, new Dictionary<string, float>() { { "Y", twin.Y - 30 }, { "Opacity", 0 } });
                GameThread.SetTimeout(() => { Remove(twin); }, 1500);
                FloatersTwinned++;
            }
        }

        public void Open()
        {
            Visible = true;
            GameFacade.Screens.Tween.To(this, 0.5f, new Dictionary<string, float>() { { "CurWidth", GameFacade.Screens.CurrentUIScreen.ScreenWidth-X} }, TweenQuad.EaseOut);
            foreach (var fade in GetFadeables())
            {
                GameFacade.Screens.Tween.To(fade, 0.3f, new Dictionary<string, float>() { { "Opacity", 1f } });
            }
            PanelActive = true;
        }

        // R206 ENGINE LAW (the R199 residual closed): the original composes
        // the control panel PERMANENTLY at lot entry — cSimsApp::
        // RebuildControlPanel 0x24de40 builds the cWinCPanel (new(0x108) +
        // ctor 0x270d40 into app+0x84) and CPState::EnteringHouse 0x211c80
        // runs SetMode immediately; the panel is present from the FIRST
        // frame, with no reveal tween. The port's Open() animates — this is
        // the untweened end-state used once at desktop lot entry.
        public void ComposeAtLotEntry()
        {
            var screen = GameFacade.Screens.CurrentUIScreen;
            Visible = true;
            CurWidth = (screen != null) ? screen.ScreenWidth - X : CurWidth;
            foreach (var fade in GetFadeables())
            {
                fade.Opacity = 1f;
            }
            PanelActive = true;
        }

        public void Close()
        {
            GameFacade.Screens.Tween.To(this, 0.5f, new Dictionary<string, float>() { { "CurWidth", 0 } }, TweenQuad.EaseOut);
            SetSubpanel(null);
            foreach (var fade in GetFadeables())
            {
                GameFacade.Screens.Tween.To(fade, 0.3f, new Dictionary<string, float>() { { "Opacity", 0f } });
            }
            if (Switcher.CategoryExpand > 0) Switcher.Close();

            SwitchAvatar?.Kill();
            SwitchAvatar = null;
            PanelActive = false;
        }

        public void ShowSelect()
        {
            var add = new UISwitchAvatarPanel(Game);
            Add(add);

            SetSubpanel(null);
            foreach (var fade in GetFadeables())
            {
                GameFacade.Screens.Tween.To(fade, 0.3f, new Dictionary<string, float>() { { "Opacity", 0f } });
            }
            if (Switcher.CategoryExpand > 0) Switcher.Close();
            ShowingSelect = true;
            SwitchAvatar = add;

            add.OnEnd += () =>
            {
                Open();
                Switcher_OnCategorySelect(Switcher.ActiveCategory);
                SwitchAvatar = null;
                ShowingSelect = false;
                OnEndSelect?.Invoke();
            };
        }

        public override void GameResized()
        {
            base.GameResized();
            // Desktop stays pixel-for-pixel and relies on viewport clipping. Mobile
            // retains its screen-width stretch and must update after a resize.
            if (OriginalPanelBack != null)
            {
                if (Game.Desktop)
                    OriginalPanelBack.SetSize(804, 100);
                else
                    OriginalPanelBack.SetSize(System.Math.Max(0, Game.ScreenWidth), 128);
            }
            if (PanelActive) CurWidth = Game.ScreenWidth - X;
            if (HideButton != null) HideButton.X = Game.ScreenWidth - (50 + X);
            // R141: re-anchor the right-end speed/clock/funds cluster
            if (PauseButton != null)
            {
                int rx = (int)(Game.ScreenWidth - X);
                PauseButton.X = rx - 150;
                int[] fw = { 11, 20, 32 };
                int sx = rx - 110;
                if (SpeedButtons != null) for (int i = 0; i < 3; i++) { SpeedButtons[i].X = sx; sx += fw[i] + 4; }
                if (TimeOriginal != null && TimeOriginal.Font != null)
                    TimeOriginal.X = System.Math.Max(0, rx - 150 + (114 - TimeOriginal.Font.Measure(TimeOriginal.Text)) / 2);
                if (MoneyOriginal != null && MoneyOriginal.Font != null)
                    MoneyOriginal.X = System.Math.Max(0, rx - 150 + (138 - MoneyOriginal.Font.Measure(MoneyOriginal.Text)) / 2);
            }
        }
    }

    public enum UIMainPanelMode
    {
        LIVE = 0,
        BUY = 1,
        BUILD = 2,
        OPTIONS = 3,
        CAMERA = 4
    }
}
