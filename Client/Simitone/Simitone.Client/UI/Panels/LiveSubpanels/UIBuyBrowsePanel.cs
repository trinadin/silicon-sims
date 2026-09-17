using FSO.Client.UI.Controls.Catalog;
using FSO.Content;
using FSO.Content.Interfaces;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simitone.Client.UI.Controls;
using Simitone.Client.UI.Screens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FSO.Common.Rendering.Framework.Model;
using FSO.Client.UI.Framework;
using FSO.Client.UI.Controls;
using FSO.Client;
using Simitone.Client.UI.Model;
using FSO.Client.UI.Panels.LotControls;
using FSO.Client.UI.Model;
using FSO.LotView.Components;
using FSO.LotView.Model;
using FSO.Files.Formats.IFF.Chunks;
using Simitone.Client.UI.Panels.LiveSubpanels.Catalog;

namespace Simitone.Client.UI.Panels.LiveSubpanels
{
    public class UIBuyBrowsePanel : UISubpanel
    {
        public UITouchScroll CatContainer;
        public List<UICatalogElement> FullCategory;
        public IEnumerable<UICatalogElement> FilterCategory;

        // R131: the ORIGINAL roof panel is PAGED (engine cWinRoofPanel decode,
        // R130: panel ids 3/4 -> SetCurPage(page -/+ 1); buttons kCatalogPrevPage
        // 100 / kCatalogNextPage 101 = cpanel\Buttons\ScrollLeft/ScrollRight.bmp;
        // page titles STR# 147 [14] 'Roof Pitch' / [16] 'Roof Patterns'). Page 0
        // = the four pitch sub-tools, page 1 = the pattern swatches.
        public List<UICatalogElement> RoofPitchItems;
        public List<UICatalogElement> RoofPatternItems;
        public int RoofPage;
        public UILabel RoofPageTitle;
        public UIElasticButton RoofPrevBtn;
        public UIElasticButton RoofNextBtn;
        public static int RoofPagerMounts;
        public UICatalogMode Mode;
        public List<UICatButton> SelButtons = new List<UICatButton>();
        public List<UILabel> SelLabels = new List<UILabel>();
        public sbyte Category;
        public bool ChoosingSub;
        public int ItemID = -1;
        // R122: ROOM main sort (STR# 150 [0..7]) — the panel browses one original
        // RoomFlags bit across all 8 function categories instead of one function.
        public bool RoomMode;

        private string ActiveSearchTerm;
        private IEnumerable<UICatalogElement> PreSearchFilterCategory;
        public UILabel NoResultsLabel;

        public static int[] RemapString = new int[]
        {
            0, //seat
            1, //surf
            4, //
            3,
            5,
            2,
            7,
            6
        };

        // R122: canon function order (STR# 150 [8..15] / kBuyF*) -> the subsort-icon
        // family directory on disk, and canon index -> catalog function ID (the
        // inverse of RemapString, which is catalog ID -> canon index).
        public static string[] CanonFuncFamilies = new string[]
        {
            "Seating", "Surfaces", "Decorative", "Electronics",
            "Appliances", "Plumbing", "Lighting", "Miscellaneous"
        };
        public static int[] CanonFuncToCatalogID = new int[] { 0, 1, 5, 3, 2, 4, 7, 6 };

        // R122: the ORIGINAL RoomFlags bit order, derived from the data itself
        // (empirical anchors in the live catalog: Stove=r0x01, Bed=r0x02,
        // Toilet=r0x04, sofas=r0x08, dining tables=r0x20, plants/outdoor
        // lights=r0x40, Bookshelf=r0x80): the internal order is Kitchen,
        // Bedroom, Bathroom, Living, Misc, Dining, Outside, Study — NOT the
        // STR# 150 [0..7] display order. Display index -> flag bit:
        // Living->3, Dining->5, Bedroom->1, Study->7, Kitchen->0,
        // Bathroom->2, Outside->6, Misc->4.
        public static int[] CanonRoomToFlagBit = new int[] { 3, 5, 1, 7, 0, 2, 6, 4 };

        // R122: subsort slot art inside each family dir (STR# 200-207 entries 0-3
        // = SubSort One..Four). Manifest-exact names; the OrdinalIgnoreCase
        // fallback in UIOriginal.EnsureResolved tolerates residual case drift.
        public static string[] SubSortSlotArt = new string[] { "One.BMP", "Two.bmp", "Three.BMP", "Four.BMP" };
        public const string SubSortOtherArt = "cpanel\\Catalog\\SubSortIcons\\BuySubSortOther.bmp";
        public const string SubSortAllArt = "cpanel\\Catalog\\SubSortIcons\\BuySubSortAll.bmp";
        public const string SubSortPetsArt = "cpanel\\Catalog\\SubSortIcons\\Miscellaneous\\BuySubSortPets.BMP";
        public const string SubSortMagicArt = "cpanel\\Catalog\\SubSortIcons\\Miscellaneous\\BuySubSortMagic.BMP";

        // R122: room-mode subsort sheets (kBuyRSubSort* = the 8 FUNCTION sorts
        // shown inside a room), manifest-exact names in canon order.
        public static string[] RoomSubSortArt = new string[]
        {
            "cpanel\\Catalog\\SubSortIcons\\RoomSubsort\\BuySubSortRoomSeating.BMP",
            "cpanel\\Catalog\\SubSortIcons\\RoomSubsort\\BuySubSortRoomSurfaces.BMP",
            "cpanel\\Catalog\\SubSortIcons\\RoomSubsort\\BuySubSortRoomDecorative.BMP",
            "cpanel\\Catalog\\SubSortIcons\\RoomSubsort\\BuySubSortRoomElectronics.bmp",
            "cpanel\\Catalog\\SubSortIcons\\RoomSubsort\\BuySubSortRoomAppliances.BMP",
            "cpanel\\Catalog\\SubSortIcons\\RoomSubsort\\BuySubSortRoomPlumbing.BMP",
            "cpanel\\Catalog\\SubSortIcons\\RoomSubsort\\BuySubSortRoomLighting.BMP",
            "cpanel\\Catalog\\SubSortIcons\\RoomSubsort\\BuySubSortRoomMisc.bmp"
        };

        // R122: expansion-lot main-sort plaques for the subsort row (DTCategories
        // MaskBit 0-3). Studiotown/Magictown follow the res_cpanel.RT aliases
        // (kBuySTfood -> BuyDDining.BMP etc.) — the RT template is authoritative
        // over the unreferenced BuyST* alternates on disk.
        public static Dictionary<UICatalogMode, string[]> DTSortArt = new Dictionary<UICatalogMode, string[]>
        {
            { UICatalogMode.Downtown, new string[] {
                "cpanel\\Catalog\\BuyDDining.BMP", "cpanel\\Catalog\\BuyDShops.bmp",
                "cpanel\\Catalog\\BuyDOutdoor.bmp", "cpanel\\Catalog\\BuyDStreet.bmp" } },
            { UICatalogMode.Community, new string[] {
                "cpanel\\Catalog\\BuyCOne.BMP", "cpanel\\Catalog\\BuyCTwo.bmp",
                "cpanel\\Catalog\\BuyCThree.bmp", "cpanel\\Catalog\\BuyCFour.bmp" } },
            { UICatalogMode.Vacation, new string[] {
                "cpanel\\Catalog\\BuyVOne.BMP", "cpanel\\Catalog\\BuyVTwo.bmp",
                "cpanel\\Catalog\\BuyVThree.bmp", "cpanel\\Catalog\\BuyVFour.bmp" } },
            { UICatalogMode.Studiotown, new string[] {
                "cpanel\\Catalog\\BuyDDining.BMP", "cpanel\\Catalog\\BuyDShops.bmp",
                "cpanel\\Catalog\\BuySTStudio.bmp", "cpanel\\Catalog\\BuySTSpa.bmp" } },
            { UICatalogMode.Magictown, new string[] {
                "cpanel\\Catalog\\BuyDDining.BMP", "cpanel\\Catalog\\BuyDShops.bmp",
                "cpanel\\Catalog\\BuyMTmagic.bmp", "cpanel\\Catalog\\BuyMOutdoor.bmp" } },
        };

        // R126: the ORIGINAL build-mode tool plaques (res_cpanel.RT kToolBtn*
        // 64..77 -> cpanel\Buttons\toolBtn*.bmp), keyed by the subcategory's
        // own STR# 139 'BldTips' index (= the port BuildCategories StrInd).
        // Manifest-exact names; the OrdinalIgnoreCase FAR lookup tolerates
        // case drift. Hand/Undo/Redo have no port surface (disclosed).
        public static Dictionary<int, string> BuildSubSortArt = new Dictionary<int, string>
        {
            { 0, "cpanel\\Buttons\\toolBtnTerrain.bmp" },
            { 1, "cpanel\\Buttons\\toolBtnWater.bmp" },
            { 2, "cpanel\\Buttons\\toolBtnWall.bmp" },
            { 3, "cpanel\\Buttons\\toolBtnWallpaper.bmp" },
            { 4, "cpanel\\Buttons\\toolBtnStairs.bmp" },
            { 5, "cpanel\\Buttons\\toolBtnFireplace.bmp" },
            { 6, "cpanel\\Buttons\\toolBtnTree.bmp" },
            { 7, "cpanel\\Buttons\\toolBtnFloor.bmp" },
            { 8, "cpanel\\Buttons\\toolBtnDoor.bmp" },
            { 9, "cpanel\\Buttons\\toolBtnWindow.bmp" },
            { 10, "cpanel\\Buttons\\toolBtnRoof.bmp" }
        };

        // R126: build tool plaques are SINGLE-frame (text baked in at natural
        // widths; toolBtnTerrain is 156x39 = 4x39, which Frame() would mis-crop
        // as a 4-frame sheet), so build loads the FULL image; the buy subsort
        // sheets (144x36 = 4 real frames) keep Frame(member, 0).
        public static Texture2D ResolveSubSortArt(UICatalogSubcat cat, UICatalogMode mode)
        {
            if (cat.OriginalName == null) return null;
            try
            {
                if (mode == UICatalogMode.Build)
                    return UIOriginal.EnsureResolved(cat.OriginalName)?.Get(GameFacade.GraphicsDevice);
                return UIOriginal.Frame(cat.OriginalName, 0);
            }
            catch { return null; }
        }

        public static List<List<UICatalogSubcat>> Categories;
        public static string[][] CatIcons = new string[][]
        {
            new string[]
            {
                "seat_dine",
                "seat_loun",
                "seat_sofa",
                "seat_beds"
            },
            new string[]
            {
                "surf_count",
                "surf_tabl",
                "surf_endt",
                "surf_desk"
            },
            new string[]
            {
                "appl_stov",
                "appl_frig",
                "appl_smal",
                "appl_larg"
            },
            new string[]
            {
                "elec_ent",
                "elec_vide",
                "elec_audi",
                "elec_phon"
            },

            new string[]
            {
                "plum_toil",
                "plum_show",
                "plum_sink",
                "plum_hott"
            },

            new string[]
            {
                "deco_pain",
                "deco_scul",
                "deco_rugs",
                "deco_plan"
            },
            new string[]
            {
                "misc_recr",
                "misc_know",
                "misc_crea",
                "misc_ward",
                "misc_pets",
                "misc_pets",
                "misc_magi",
            },
            new string[]
            {
                "ligh_tabl",
                "ligh_stan",
                "ligh_wall",
                "ligh_hang"
            },
        };

        public static List<List<UICatalogSubcat>> BuildCategories = new List<List<UICatalogSubcat>>()
        {
            new List<UICatalogSubcat>() //architecture
            {
                new UICatalogSubcat() { MaskBit = 7+6, StrTable = 139, StrInd = 2}, //wall
                new UICatalogSubcat() { MaskBit = 7+8, StrTable = 139, StrInd = 3}, //wallpaper
                new UICatalogSubcat() { MaskBit = 7+9, StrTable = 139, StrInd = 7}, //floor
                new UICatalogSubcat() { MaskBit = 7+10, StrTable = 139, StrInd = 10}, //roof
            },
            new List<UICatalogSubcat>() //outdoors
            {
                new UICatalogSubcat() { MaskBit = 7+4, StrTable = 139, StrInd = 6}, //trees
                new UICatalogSubcat() { MaskBit = 7+11, StrTable = 139, StrInd = 0}, //terrain
                new UICatalogSubcat() { MaskBit = 7+7, StrTable = 139, StrInd = 1}, //water
            },
            new List<UICatalogSubcat>() //objects
            {
                new UICatalogSubcat() { MaskBit = 7+1, StrTable = 139, StrInd = 8}, //door
                new UICatalogSubcat() { MaskBit = 7+2, StrTable = 139, StrInd = 9}, //window
                new UICatalogSubcat() { MaskBit = 7+3, StrTable = 139, StrInd = 4}, //staircase
                new UICatalogSubcat() { MaskBit = 7+5, StrTable = 139, StrInd = 5}, //fireplaces
            },
        };

        public static string[][] BuildIcons = new string[][]
        {
            new string[]
            {
                "build_wall",
                "build_walp",
                "build_flor",
                "build_roof",
            },
            new string[]
            {
                "build_tree",
                "build_terr",
                "build_watr",
            },
            new string[]
            {
                "build_door",
                "build_wind",
                "build_stai",
                "build_fire"
            },
        };

        public static Dictionary<UICatalogMode, List<UICatalogSubcat>> DTCategories = new Dictionary<UICatalogMode, List<UICatalogSubcat>>()
        {
            {
                UICatalogMode.Downtown,
                new List<UICatalogSubcat>()
                {
                    new UICatalogSubcat() { MaskBit = 0, StrTable = 150, StrInd = 16}, //food
                    new UICatalogSubcat() { MaskBit = 1, StrTable = 150, StrInd = 17}, //shops
                    new UICatalogSubcat() { MaskBit = 2, StrTable = 150, StrInd = 18}, //outside
                    new UICatalogSubcat() { MaskBit = 3, StrTable = 150, StrInd = 19}, //street
                }
            },

            {
                UICatalogMode.Community,
                new List<UICatalogSubcat>()
                {
                    new UICatalogSubcat() { MaskBit = 0, StrTable = 150, StrInd = 32}, //food
                    new UICatalogSubcat() { MaskBit = 1, StrTable = 150, StrInd = 33}, //shops
                    new UICatalogSubcat() { MaskBit = 2, StrTable = 150, StrInd = 34}, //outside
                    new UICatalogSubcat() { MaskBit = 3, StrTable = 150, StrInd = 35}, //street
                }
            },

            {
                UICatalogMode.Vacation,
                new List<UICatalogSubcat>()
                {
                    new UICatalogSubcat() { MaskBit = 0, StrTable = 150, StrInd = 24}, //lodging
                    new UICatalogSubcat() { MaskBit = 1, StrTable = 150, StrInd = 25}, //shops
                    new UICatalogSubcat() { MaskBit = 2, StrTable = 150, StrInd = 26}, //recreation
                    new UICatalogSubcat() { MaskBit = 3, StrTable = 150, StrInd = 27}, //ameneties
                }
            },

            {
                UICatalogMode.Studiotown,
                new List<UICatalogSubcat>()
                {
                    new UICatalogSubcat() { MaskBit = 0, StrTable = 150, StrInd = 40}, //food
                    new UICatalogSubcat() { MaskBit = 1, StrTable = 150, StrInd = 41}, //shops
                    new UICatalogSubcat() { MaskBit = 2, StrTable = 150, StrInd = 42}, //studio
                    new UICatalogSubcat() { MaskBit = 3, StrTable = 150, StrInd = 43}, //spa
                }
            },

            {
                UICatalogMode.Magictown,
                new List<UICatalogSubcat>()
                {
                    new UICatalogSubcat() { MaskBit = 0, StrTable = 150, StrInd = 16}, //food
                    new UICatalogSubcat() { MaskBit = 1, StrTable = 150, StrInd = 17}, //shops
                    new UICatalogSubcat() { MaskBit = 2, StrTable = 150, StrInd = 44}, //magico
                    new UICatalogSubcat() { MaskBit = 3, StrTable = 150, StrInd = 18}, //outside
                }
            },
        };

        public static Dictionary<UICatalogMode, List<string>> DTIcons = new Dictionary<UICatalogMode, List<string>>()
        {
            {
                UICatalogMode.Downtown,
                new List<string>()
                {
                    "dt_food", //food
                    "dt_shop", //shops
                    "dt_out", //outside
                    "dt_street", //street
                }
            },

            {
                UICatalogMode.Community,
                new List<string>()
                {
                    "dt_food", //food
                    "dt_shop", //shops
                    "dt_out", //outside
                    "dt_street", //street
                }
            },

            {
                UICatalogMode.Vacation,
                new List<string>()
                {
                    "vac_lodg", //food
                    "dt_shop", //shops
                    "vac_recr", //recreation
                    "vac_amen", //amenities
                }
            },

            {
                UICatalogMode.Studiotown,
                new List<string>()
                {
                    "dt_food", //food
                    "dt_shop", //shops
                    "st_studio", //studio
                    "st_spa", //spa
                }
            },

            {
                UICatalogMode.Magictown,
                new List<string>()
                {
                    "dt_food", //food
                    "dt_shop", //shops
                    "misc_magi", //magic
                    "dt_out", //outside
                }
            },
        };

        static UIBuyBrowsePanel()
        {
            Categories = new List<List<UICatalogSubcat>>();

            for (int i=0; i<8; i++)
            {
                //init buy categories.
                var cat = new List<UICatalogSubcat>();
                var modi = RemapString[i];
                for (int j=0; j<4; j++)
                {
                    cat.Add(new UICatalogSubcat()
                    {
                        StrTable = 200 + modi,
                        MaskBit = j,
                        StrInd = j
                    });
                }

                if (i == 6)
                {
                    cat.Add(new UICatalogSubcat()
                    {
                        StrTable = 210,
                        MaskBit = 5,
                        StrInd = 3, //pets
                    });

                    cat.Add(new UICatalogSubcat()
                    {
                        StrTable = 210,
                        MaskBit = 6,
                        StrInd = 4, //magic
                    });
                }

                cat.Add(new UICatalogSubcat()
                {
                    StrTable = 210,
                    MaskBit = 7,
                    StrInd = 1, //other
                });

                cat.Add(new UICatalogSubcat()
                {
                    StrTable = 210,
                    MaskBit = 8,
                    StrInd = 2, //all
                });
                Categories.Add(cat);
            }
        }

        public bool HoldingEvents;

        // R145: the DESKTOP band branch — on desktop the engine composes the
        // whole catalog INSIDE the 100px toolbar band (cWinArch's subtool
        // pattern row at (338,5) 45px pitch / cWinCatalog's grid at (195,5)
        // 13x2), with NO subsort plaque row and NO touch scroll. BandMode
        // swaps the mobile chrome for the engine grid (law:
        // tools/iff-dump/r145/build-toolbar-law.md section 5 +
        /// buy-catalog-law.md section 3).
        public bool BandMode;
        public List<UICatalogSubcat> CurrentSubCats;   // the resolved subsort list (band chrome reads it)
        public List<Simitone.Client.UI.Panels.UIOriginalCatalogCell> BandCells =
            new List<Simitone.Client.UI.Panels.UIOriginalCatalogCell>();
        public Simitone.Client.UI.Panels.UIOriginalSheetButton BandPagePrev, BandPageNext;
        public int BandPage;
        public Simitone.Client.UI.Panels.UIOriginalCatalogPopup BandPopup;
        // Product::DrawIcon type 1 asks the object renderer for an iconic view;
        // it does not reuse ProductButton's 74x37 two-state catalog BMP. Cache
        // those comparatively expensive compositor results for this panel's
        // lifetime. The popup only borrows them; this panel owns/disposes them.
        private readonly Dictionary<uint, Texture2D> BandObjectThumbCache =
            new Dictionary<uint, Texture2D>();
        public int BandMainSlot = -1;   // R146: expansion main plaque slot (0-3 or 7=Misc) driving the mask filter

        private bool CheckedPendingEyedropper;

        public UIBuyBrowsePanel(TS1GameScreen screen, sbyte category, UICatalogMode mode, bool roomMode = false) : base(screen) {
            // R146: the decoded band owns BUY on EVERY desktop lot — home
            // (room/function sorts) AND expansion (Downtown/Vacation/Community/
            // Studio/Magic, their own plaque sets) — plus Build. The mobile
            // touch composition remains only for non-desktop builds.
            BandMode = screen.Desktop;
            CatContainer = new UITouchScroll(() => FilterCategory?.Count() ?? 0, CatalogElemProvider);
            CatContainer.ItemWidth = 90;
            CatContainer.DrawBounds = false;
            CatContainer.Margin = 15;
            CatContainer.SetScroll(-15);
            CatContainer.Size = new Vector2(775, 128);
            Category = category;
            RoomMode = roomMode;

            Add(CatContainer);
            Mode = mode;
            GameResized();

            NoResultsLabel = new UILabel();
            NoResultsLabel.Caption = "No items found";
            NoResultsLabel.Alignment = TextAlignment.Middle | TextAlignment.Center;
            NoResultsLabel.Position = new Vector2(0, 50);
            NoResultsLabel.Size = new Vector2(Size.X, 25);
            NoResultsLabel.CaptionStyle = NoResultsLabel.CaptionStyle.Clone();
            NoResultsLabel.CaptionStyle.Size = 14;
            NoResultsLabel.CaptionStyle.Color = UIStyle.Current.Text;
            NoResultsLabel.Visible = false;
            Add(NoResultsLabel);

            InitCategory(category, false);

            // R145: desktop band branch — the engine grid + page arrows replace
            // the mobile touch scroll; the subsort row is the band chrome's
            // (UIOriginalArchChrome tools / UIOriginalBuyChrome plaques).
            if (BandMode)
            {
                CatContainer.Visible = false;
                NoResultsLabel.Visible = false;
                try { SetupBand(); }
                catch (Exception be) { Simitone.Client.GameLog.Write("band-ctor EXC " + be.GetType().Name + " " + be.Message); }
            }

            screen.LotControl.ObjectHolder.OnPickup += ObjectHolder_OnPickup;
            screen.LotControl.ObjectHolder.OnPutDown += ObjectHolder_OnPutDown;
            screen.LotControl.ObjectHolder.OnDelete += ObjectHolder_OnDelete;
            screen.LotControl.ObjectHolder.OnEyedropperPick += ObjectHolder_OnEyedropperPick;
            screen.LotControl.ObjectHolder.OnEyedropperArchitecturePick += ObjectHolder_OnEyedropperArchitecturePick;
            screen.LotControl.OnCustomControlReleased += LotControl_OnCustomControlReleased;
            HoldingEvents = true;
        }

        /// <summary>
        /// Maps a WorldCatalog category to the Build mode UI category and subcategory.
        /// Returns null if the category is not a valid Build mode object category.
        /// </summary>
        private static (int uiCategory, int subcatIndex)? MapCatalogToBuildCategory(int catalogCategory)
        {
            // Based on BuildCategories MaskBit values
            switch (catalogCategory)
            {
                // Objects (UI Category 2)
                case 8: return (2, 0);  // Doors - MaskBit 7+1
                case 9: return (2, 1);  // Windows - MaskBit 7+2
                case 10: return (2, 2); // Stairs - MaskBit 7+3
                case 12: return (2, 3); // Fireplaces - MaskBit 7+5

                // Outdoors (UI Category 1)
                case 11: return (1, 0); // Trees - MaskBit 7+4
                case 14: return (1, 2); // Water - MaskBit 7+7
                case 18: return (1, 1); // Terrain - MaskBit 7+11

                // Architecture (UI Category 0) - these are special elements, not clickable objects
                case 13: return (0, 0); // Walls - MaskBit 7+6
                case 15: return (0, 1); // Wallpaper - MaskBit 7+8
                case 16: return (0, 2); // Floors - MaskBit 7+9
                case 17: return (0, 3); // Roofs - MaskBit 7+10

                default: return null;
            }
        }

        /// <summary>
        /// Checks if there's a pending eyedropper GUID or architecture pattern to select after a category or mode switch.
        /// Called from Update since Parent may not be set during constructor.
        /// </summary>
        private void CheckPendingEyedropperSelection()
        {
            if (CheckedPendingEyedropper) return;
            CheckedPendingEyedropper = true;

            var mainPanel = Parent as UIMainPanel;
            if (mainPanel == null) return;

            // Check for pending architecture pick first
            if (mainPanel.PendingEyedropperPatternID != null && mainPanel.PendingEyedropperArchType != null)
            {
                var patternID = mainPanel.PendingEyedropperPatternID.Value;
                var archType = mainPanel.PendingEyedropperArchType.Value;
                mainPanel.PendingEyedropperPatternID = null;
                mainPanel.PendingEyedropperArchType = null;

                SelectArchitectureByPatternID(patternID, archType);
                return;
            }

            // Check for pending GUID pick
            if (mainPanel.PendingEyedropperGUID != null)
            {
                var guid = mainPanel.PendingEyedropperGUID.Value;
                mainPanel.PendingEyedropperGUID = null;

                // Use SelectItemByGUID to handle category switching if needed
                // (e.g., after a mode switch, we may be in the wrong category)
                SelectItemByGUID(guid);
            }
        }

        private void ObjectHolder_OnEyedropperPick(uint guid)
        {
            // Turn off eyedropper mode (button state is synced in UIDesktopUCP)
            Game.LotControl.ObjectHolder.EyedropperMode = false;

            // Select the item in catalog
            SelectItemByGUID(guid);
        }

        private void ObjectHolder_OnEyedropperArchitecturePick(ushort patternID, ArchitectureType archType)
        {
            // Turn off eyedropper mode (button state is synced in UIDesktopUCP)
            Game.LotControl.ObjectHolder.EyedropperMode = false;

            // Architecture picks only work in Build mode
            if (Mode != UICatalogMode.Build)
            {
                // Switch to Build mode first
                var mainPanel = Parent as UIMainPanel;
                if (mainPanel != null)
                {
                    mainPanel.PendingEyedropperPatternID = patternID;
                    mainPanel.PendingEyedropperArchType = archType;
                    var frontend = Game.Frontend as UISimitoneFrontend;
                    frontend?.SwitchMode(UIMainPanelMode.BUILD);
                }
                return;
            }

            // Already in Build mode - select the architecture item
            SelectArchitectureByPatternID(patternID, archType);
        }

        /// <summary>
        /// Selects an architecture item (floor or wallpaper) by its pattern ID.
        /// </summary>
        private void SelectArchitectureByPatternID(ushort patternID, ArchitectureType archType)
        {
            // Architecture items are in UI Category 0 (Architecture)
            // Floor = subcategory index 2, Wallpaper = subcategory index 1
            int targetUICategory = 0;
            int targetSubcatIndex = (archType == ArchitectureType.Floor) ? 2 : 1;

            // Switch to Architecture category if needed
            if (Category != targetUICategory)
            {
                var mainPanel = Parent as UIMainPanel;
                if (mainPanel != null)
                {
                    mainPanel.PendingEyedropperPatternID = patternID;
                    mainPanel.PendingEyedropperArchType = archType;
                    mainPanel.Switcher.Select(targetUICategory);
                }
                return;
            }

            // Initialize the correct subcategory
            var subcat = BuildCategories[targetUICategory][targetSubcatIndex];
            if (ChoosingSub)
            {
                InitSubcategory(subcat);
            }

            // Find and select the item by pattern ID (matching Special.ResID)
            if (FilterCategory != null)
            {
                int index = 0;
                foreach (var elem in FilterCategory)
                {
                    if (elem.Special?.ResID == patternID)
                    {
                        Selected(index);
                        CatContainer.ScrollToItem(index);
                        return;
                    }
                    index++;
                }
            }
        }

        /// <summary>
        /// Finds an item by GUID in the catalog and selects it.
        /// If the item is in a different category, switches to that category first.
        /// If the item is in a different mode (Buy vs Build), switches modes first.
        /// </summary>
        public void SelectItemByGUID(uint guid)
        {
            // First, look up the item in the world catalog to find its category
            var catalogItem = Content.Get().WorldCatalog.GetItemByGUID(guid);
            if (catalogItem == null) return;

            var targetCategory = catalogItem.Value.Category;
            var mainPanel = Parent as UIMainPanel;

            // Determine if this is a Buy item (0-7) or Build item (8+)
            bool isBuyItem = targetCategory >= 0 && targetCategory <= 7;
            bool isBuildItem = targetCategory >= 8;

            // Check if we need to switch modes
            if (Mode == UICatalogMode.Build && isBuyItem)
            {
                // In Build mode but clicked a Buy item → switch to Buy mode
                if (mainPanel != null)
                {
                    mainPanel.PendingEyedropperGUID = guid;
                    // Trigger mode switch to Buy
                    var frontend = Game.Frontend as UISimitoneFrontend;
                    frontend?.SwitchMode(UIMainPanelMode.BUY);
                }
                return;
            }
            else if (Mode != UICatalogMode.Build && isBuildItem)
            {
                // In Buy mode but clicked a Build item → switch to Build mode
                if (mainPanel != null)
                {
                    mainPanel.PendingEyedropperGUID = guid;
                    // Trigger mode switch to Build
                    var frontend = Game.Frontend as UISimitoneFrontend;
                    frontend?.SwitchMode(UIMainPanelMode.BUILD);
                }
                return;
            }

            // Same mode - handle normally
            if (Mode == UICatalogMode.Build)
            {
                // Build mode - map catalog category to Build UI category
                var mapping = MapCatalogToBuildCategory(targetCategory);
                if (mapping == null) return; // Not a valid build object

                var (targetUICategory, targetSubcatIndex) = mapping.Value;

                // Check if we need to switch Build UI categories
                if (targetUICategory != Category)
                {
                    if (mainPanel != null)
                    {
                        // Store for after category switch
                        mainPanel.PendingEyedropperGUID = guid;
                        mainPanel.Switcher.Select(targetUICategory);
                    }
                    return;
                }

                // Same Build category - initialize the subcategory and select
                if (ChoosingSub)
                {
                    var subcat = BuildCategories[Category][targetSubcatIndex];
                    InitSubcategory(subcat);
                }
                SelectItemInCurrentCategory(guid);
                return;
            }

            // Buy mode logic
            if (targetCategory != Category)
            {
                // Store the GUID to select after category switch
                if (mainPanel != null)
                {
                    mainPanel.PendingEyedropperGUID = guid;
                    // Switch to the target category - this will create a new panel
                    mainPanel.Switcher.Select(targetCategory);
                }
                return;
            }

            // Item is in this category - select it
            SelectItemInCurrentCategory(guid);
        }

        /// <summary>
        /// Selects an item by GUID within the current category.
        /// </summary>
        /// R124: public for the autotest gate — this is the REAL "leave the subsort
        /// row / show all items" transition (the eyedropper path), which the gate
        /// drives before selecting items; guid 0 collapses the row without a hit.
        public void SelectItemInCurrentCategory(uint guid)
        {
            // If we're still choosing subcategory, skip to show all items
            if (ChoosingSub)
            {
                ChoosingSub = false;
                foreach (var btn in SelButtons)
                {
                    Remove(btn);
                }
                foreach (var label in SelLabels)
                {
                    Remove(label);
                }
                SelButtons.Clear();
                SelLabels.Clear();

                // Show all items in this category
                FilterCategory = FullCategory.Where(x => GetSubsort(x.Item) > 0);
                CatContainer.Opacity = 1f;
                CatContainer.Reset();
            }

            // Search in FilterCategory first
            if (FilterCategory != null)
            {
                int index = 0;
                foreach (var item in FilterCategory)
                {
                    if (item.Item.GUID == guid)
                    {
                        CatContainer.SelectItem(index);
                        CatContainer.ScrollToItem(index);
                        return;
                    }
                    index++;
                }
            }

            // If not found in filtered, search in FullCategory
            int fullIndex = 0;
            foreach (var item in FullCategory)
            {
                if (item.Item.GUID == guid)
                {
                    // Show all items and select
                    FilterCategory = FullCategory;
                    CatContainer.Reset();
                    CatContainer.SelectItem(fullIndex);
                    CatContainer.ScrollToItem(fullIndex);
                    return;
                }
                fullIndex++;
            }
        }

        private void ObjectHolder_OnDelete(UIObjectSelection holding, UpdateState state)
        {
            HideBandPopup();
            Game.Frontend.MainPanel.SetSubpanelPickup(1f);
            Game.LotControl.QueryPanel.Active = false;
            ItemID = -1;
        }

        private void LotControl_OnCustomControlReleased()
        {
            HideBandPopup();
            Game.LotControl.QueryPanel.Active = false;
            ItemID = -1;
        }

        private void ObjectHolder_OnPutDown(UIObjectSelection holding, UpdateState state)
        {
            HideBandPopup();
            Game.LotControl.QueryPanel.Active = false;
            Game.Frontend.MainPanel.SetSubpanelPickup(1f);
            if (ItemID != -1)
            {
                if (!holding.IsBought && (state.ShiftDown))
                {
                    //place another
                    var prevDir = holding.Dir;
                    Selected(ItemID);
                    Game.LotControl.QueryPanel.SetShown(false);
                    Game.LotControl.ObjectHolder.Holding.Dir = prevDir;
                }
                else
                {
                    ItemID = -1;
                }
            }
        }

        private void ObjectHolder_OnPickup(UIObjectSelection holding, UpdateState state)
        {
            HideBandPopup();
            Game.LotControl.PickupPanel.SetInfo(Game.LotControl.vm, holding.RealEnt ?? holding.Group.BaseObject);
            Game.Frontend.MainPanel.SetSubpanelPickup(0f);
        }

        private void RemoveEvents()
        {
            if (HoldingEvents)
            {
                HoldingEvents = false;
                Game.LotControl.ObjectHolder.OnPickup -= ObjectHolder_OnPickup;
                Game.LotControl.ObjectHolder.OnPutDown -= ObjectHolder_OnPutDown;
                Game.LotControl.ObjectHolder.OnDelete -= ObjectHolder_OnDelete;
                Game.LotControl.ObjectHolder.OnEyedropperPick -= ObjectHolder_OnEyedropperPick;
                Game.LotControl.ObjectHolder.OnEyedropperArchitecturePick -= ObjectHolder_OnEyedropperArchitecturePick;
                Game.LotControl.OnCustomControlReleased -= LotControl_OnCustomControlReleased;
                Game.LotControl.ObjectHolder.EyedropperMode = false;
                Game.LotControl.QueryPanel.Active = false;
                Game.Frontend.MainPanel.SetSubpanelPickup(1f);

                if (Game.LotControl.CustomControl != null)
                {
                    Game.LotControl.CustomControl.Release();
                    Game.LotControl.CustomControl = null;
                }

                if (Game.LotControl.ObjectHolder.Holding != null)
                {
                    //delete object that hasn't been placed yet
                    //TODO: all holding objects should obviously just be ghosts.
                    //Holder.Holding.Group.Delete(vm.Context);
                    Game.LotControl.ObjectHolder.ClearSelected();
                }
            }
        }

        public override void Kill()
        {
            RemoveEvents();
            // R148: the band popup mounts on the MAIN panel (so it can rise
            // above the band) — it outlived this subpanel and stuck on screen
            // across BUY -> LIVE/BUILD switches. Detach it with the panel.
            try
            {
                if (BandPopup != null)
                {
                    BandPopup.Visible = false;
                    ((UIMainPanel)Game.Frontend.MainPanel)?.Remove(BandPopup);
                }
            }
            catch { }
            DisposeBandObjectThumbCache();
            base.Kill();
        }

        public void Selected(int itemID)
        {
            var holder = Game.LotControl.ObjectHolder;
            var control = Game.LotControl;
            holder.ClearSelected();
            var item = FilterCategory.ElementAt(itemID);

            //todo: check if over budget?

            // if (OldSelection != -1) Catalog.SetActive(OldSelection, false);
            //Catalog.SetActive(selection, true);

            if (control.CustomControl != null)
            {
                control.CustomControl.Release();
                control.CustomControl = null;
            }

            if (item.Special != null)
            {
                var res = item.Special.Res;
                var resID = item.Special.ResID;
                if (res != null && res.GetName(resID) != "")
                {
                    Game.LotControl.QueryPanel.SetInfo(res.GetThumb(resID), res.GetName(resID), res.GetDescription(resID), res.GetPrice(resID), res.DoDispose());
                    Game.LotControl.QueryPanel.Mode = 1;
                    //QueryPanel.Tab = 0;
                    Game.LotControl.QueryPanel.Active = true;
                    Game.LotControl.QueryPanel.SetShown(true);
                }
                control.CustomControl = (UICustomLotControl)Activator.CreateInstance(item.Special.Control, control.vm, control.World, control, item.Special.Parameters);
            }
            else
            {
                var BuyItem = control.vm.Context.CreateObjectInstance(item.Item.GUID, LotTilePos.OUT_OF_WORLD, Direction.NORTH, holder.UseNet);
                if (BuyItem.Objects.Count != 0)
                {
                    Game.LotControl.QueryPanel.SetInfo(Game.LotControl.vm, BuyItem.Objects[0], false);
                    Game.LotControl.QueryPanel.Mode = 1;
                    //QueryPanel.Tab = 0;
                    Game.LotControl.QueryPanel.Active = true;
                    Game.LotControl.QueryPanel.SetShown(true);
                    holder.SetSelected(BuyItem);
                }
            }

            ItemID = itemID;
        }

        public void Deselect()
        {
            HideBandPopup();
            var holder = Game.LotControl.ObjectHolder;
            holder.ClearSelected();
            Game.LotControl.QueryPanel.Active = false;
            Game.Frontend.MainPanel.SetSubpanelPickup(1f);
            if (Game.LotControl.CustomControl != null)
            {
                Game.LotControl.CustomControl.Release();
                Game.LotControl.CustomControl = null;
            }
            ItemID = -1;
        }

        public override void Removed()
        {
            RemoveEvents();
            DisposeBandObjectThumbCache();
            base.Removed();
            //Catalog.UICatalogItem.ClearIconCache(); //might want to be careful here...
        }

        /// <summary>
        /// Render the object definition's iconic view through the same world
        /// compositor used by UIQueryPanel. The temporary ghost supplies the
        /// DGRP/multitile components required by World.GetObjectThumb, then is
        /// removed immediately; only the returned standalone texture is cached.
        /// </summary>
        private Texture2D GetBandObjectThumb(uint guid)
        {
            Texture2D cached;
            if (BandObjectThumbCache.TryGetValue(guid, out cached)) return cached;
            if (guid == 0 || guid == uint.MaxValue) return null;

            FSO.SimAntics.Entities.VMMultitileGroup ghost = null;
            var vm = Game?.LotControl?.vm;
            try
            {
                var world = Game?.LotControl?.World;
                if (vm == null || world == null) return null;

                ghost = vm.Context.CreateObjectInstance(
                    guid, LotTilePos.OUT_OF_WORLD, Direction.NORTH, true);
                if (ghost == null || ghost.Objects.Count == 0) return null;

                var components = new ObjectComponent[ghost.Objects.Count];
                for (int i = 0; i < ghost.Objects.Count; i++)
                {
                    components[i] = ghost.Objects[i].WorldUI as ObjectComponent;
                    if (components[i] == null) return null;
                }

                var thumb = world.GetObjectThumb(
                    components, ghost.GetBasePositions(), GameFacade.GraphicsDevice);
                if (thumb == null) return null;
                BandObjectThumbCache[guid] = thumb;
                return thumb;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (ghost != null && vm != null)
                {
                    try { ghost.Delete(vm.Context); }
                    catch { }
                }
            }
        }

        private void DisposeBandObjectThumbCache()
        {
            // Break the popup's borrowed reference before disposing this
            // panel-owned cache (and release a currently owned special thumb).
            BandPopup?.ClearIcon();
            foreach (var thumb in BandObjectThumbCache.Values)
            {
                try { thumb?.Dispose(); }
                catch { }
            }
            BandObjectThumbCache.Clear();
        }

        public void Reset()
        {
            GameFacade.Screens.Tween.To(CatContainer, 0.5f, new Dictionary<string, float>() { { "Opacity", 0f } }, TweenQuad.EaseOut);
            InitCategory(Category, false);
        }

        public void InitCategory(sbyte category, bool build)
        {
            //start by populating with entries from the catalog
            if (!build) ((UIMainPanel)Parent)?.Switcher?.MainButton?.RestoreImage();
            var catalog = Content.Get().WorldCatalog;

            if (RoomMode && Mode == UICatalogMode.Normal && !build)
            {
                // R122: room main sort — every buy item whose original RoomFlags
                // bit (RoomSort) carries the selected room, across all 8 function
                // categories. `category` is the STR# 150 [0..7] DISPLAY index;
                // CanonRoomToFlagBit translates it to the data's own bit order.
                FullCategory = new List<UICatalogElement>();
                int roomBit = CanonRoomToFlagBit[category];
                for (sbyte c = 0; c < 8; c++)
                {
                    foreach (var it in catalog.GetItemsByCategory(c))
                    {
                        if ((it.RoomSort & (1 << roomBit)) != 0)
                        {
                            FullCategory.Add(new UICatalogElement()
                            {
                                Item = it,
                                CalcPrice = (int)it.Price
                            });
                        }
                    }
                }
            }
            else if (BandMode && Mode != UICatalogMode.Normal && Mode != UICatalogMode.Build)
            {
                // R146: expansion-lot band — the whole mode catalog (any item
                // carrying this expansion's sort byte), then the selected
                // main's mask (GetMagictownMask law: sub-catalog i -> 1<<i;
                // the Misc plaque is slot 7 = bit 0x80). `category` is the
                // plaque slot (0-3 or 7).
                FullCategory = new List<UICatalogElement>();
                for (sbyte c = 0; c < 8; c++)
                {
                    foreach (var it in catalog.GetItemsByCategory(c))
                    {
                        if (GetSubsort(it) != 0)
                        {
                            FullCategory.Add(new UICatalogElement()
                            {
                                Item = it,
                                CalcPrice = (int)it.Price
                            });
                        }
                    }
                }
                BandMainSlot = category;   // filtered after the price sort, in the BandMode tail
            }
            else
            {
                var items = catalog.GetItemsByCategory(category);

                FullCategory = items.Select(x => new UICatalogElement()
                {
                    Item = x,
                    CalcPrice = (int)x.Price
                }).ToList();

                //pull from other categories

                if (category == 15) AddWallpapers();
                if (category == 14 || category == 16) AddFloors(category);
                if (category == 17) AddRoofs();
                if (category == 18) AddTerrainTools();
            }

            FullCategory = FullCategory.OrderBy(x => (int)x.Item.Price).ToList();
            if (category == 13) AddWallStyles();

            // R131: the roof category is the engine's PAGED panel — apply the
            // current page's filter and pager visibility after the rebuild.
            if (category == 17 && Mode == UICatalogMode.Build) SetRoofPage(RoofPage);
            UpdateRoofPagerVisibility(category == 17 && Mode == UICatalogMode.Build);

            // Resolve CTSS display names so search can match "Soma Plasma TV" not just "TV Expensive"
            foreach (var elem in FullCategory)
            {
                if (elem.Special != null)
                {
                    elem.DisplayName = elem.Special.Res?.GetName(elem.Special.ResID);
                }
                else if (elem.Item.GUID != 0 && elem.Item.GUID != uint.MaxValue)
                {
                    var worldObj = Content.Get().WorldObjects.Get(elem.Item.GUID);
                    if (worldObj != null)
                    {
                        var ctss = worldObj.Resource.Get<CTSS>(worldObj.OBJ.CatalogStringsID);
                        elem.DisplayName = ctss?.GetString(0);
                    }
                }
                // Fall back to Item.CatalogName or Item.Name when DisplayName is null (handled by ToString)
                elem.DisplayName = elem.DisplayName ?? elem.Item.CatalogName;
            }
            //if we're not build mode, init the subcategory selection
            if (build) return;

            foreach (var btn in SelButtons) Remove(btn);
            foreach (var label in SelLabels) Remove(label);
            SelButtons.Clear();
            SelLabels.Clear();

            ChoosingSub = true;

            List<UICatalogSubcat> cats;
            if (RoomMode && Mode == UICatalogMode.Normal)
            {
                // R122: inside a room the original shows the 8 FUNCTION subsorts
                // (kBuyRSubSort*, captions STR# 150 [8..15]) — selecting one
                // filters on the item's catalog function category.
                cats = new List<UICatalogSubcat>();
                for (int i = 0; i < 8; i++)
                {
                    cats.Add(new UICatalogSubcat()
                    {
                        StrTable = 150,
                        StrInd = 8 + i,
                        FuncId = CanonFuncToCatalogID[i],
                        OriginalName = RoomSubSortArt[i]
                    });
                }
            }
            else if (Mode == UICatalogMode.Build) cats = BuildCategories[category];
            else if (BandMode && Mode != UICatalogMode.Normal)
            {
                // R146: expansion band — the subsort state shows the 8 FUNCTION
                // subsorts (engine subsort art +0x2a8/2c8/2e8/308/328, tooltips
                // STR# 150 [8..15]); they filter main-mask AND function.
                cats = new List<UICatalogSubcat>();
                var subArt = Simitone.Client.UI.Panels.UIOriginalBuyChrome.ExpSubArt[Mode];
                for (int i = 0; i < 8; i++)
                {
                    cats.Add(new UICatalogSubcat()
                    {
                        StrTable = 150,
                        StrInd = 8 + i,
                        FuncId = CanonFuncToCatalogID[i],
                        OriginalName = subArt[i]
                    });
                }
            }
            else if (Mode != UICatalogMode.Normal)
            {
                cats = DTCategories[Mode];
                if (cats.Count == 4) //haven't added other or all subcats yet
                {
                    cats.Add(new UICatalogSubcat()
                    {
                        StrTable = 210,
                        MaskBit = 7,
                        StrInd = 1, //other
                    });

                    cats.Add(new UICatalogSubcat()
                    {
                        StrTable = 210,
                        MaskBit = 8,
                        StrInd = 2, //all
                    });
                }
            }
            else
            {
                cats = Categories[category];
            }

            // R145: the desktop band branch keeps ChoosingSub (InitSubcategory
            // gates on it) but never builds the mobile subsort row — the band
            // chrome (tools/plaques) owns subsort selection. The ORIGINALNAME
            // resolution still runs (the band's sub plaques read it).
            if (BandMode)
            {
                for (int i = 0; i < cats.Count; i++)
                {
                    var cat = cats[i];
                    if (cat.OriginalName == null && Mode == UICatalogMode.Build)
                    {
                        string buildArt;
                        if (BuildSubSortArt.TryGetValue(cat.StrInd, out buildArt)) cat.OriginalName = buildArt;
                    }
                    if (cat.OriginalName == null && Mode != UICatalogMode.Build)
                    {
                        if (RoomMode && Mode == UICatalogMode.Normal) continue;   // already carries RoomSubSortArt
                        if (Mode != UICatalogMode.Normal)
                        {
                            var dtArt = DTSortArt[Mode];
                            cat.OriginalName = (cat.MaskBit == 7) ? SubSortOtherArt
                                : (cat.MaskBit == 8) ? SubSortAllArt
                                : dtArt[cat.MaskBit];
                        }
                        else
                        {
                            var family = CanonFuncFamilies[RemapString[category]];
                            cat.OriginalName = (cat.MaskBit == 7) ? SubSortOtherArt
                                : (cat.MaskBit == 8) ? SubSortAllArt
                                : (cat.MaskBit == 5) ? SubSortPetsArt
                                : (cat.MaskBit == 6) ? SubSortMagicArt
                                : "cpanel\\Catalog\\SubSortIcons\\" + family + "\\BuySubSort" + SubSortSlotArt[cat.MaskBit];
                        }
                    }
                }
                CurrentSubCats = cats;
                // R146: expansion mains — apply the plaque mask now that the
                // price sort + name resolution have settled FullCategory.
                if (BandMainSlot >= 0)
                {
                    int mmask = 1 << BandMainSlot;
                    FilterCategory = FullCategory.Where(x => (GetSubsort(x.Item) & mmask) != 0);
                }
                return;
            }

            var boff = CatContainer.Size.X/(cats.Count + 0.5f) / 2f;

            for (int i=0; i<cats.Count; i++)
            {
                var cat = cats[i];
                var str = GameFacade.Strings.GetString(cat.StrTable.ToString(), cat.StrInd.ToString());

                var label = new UILabel();
                label.Caption = str;
                label.Alignment = TextAlignment.Middle | TextAlignment.Center;
                label.Wrapped = true;
                label.Position = new Vector2(boff * (1.5f+i*2) - (120/2), 106);
                label.Size = new Vector2(120, 1);
                label.CaptionStyle = label.CaptionStyle.Clone();
                label.CaptionStyle.Size = 12;
                label.CaptionStyle.Color = UIStyle.Current.Text;
                SelLabels.Add(label);
                Add(label);

                var name = "";
                if (Mode == UICatalogMode.Build) {
                    name = BuildIcons[category][i];
                }
                else if (Mode != UICatalogMode.Normal) {
                    if (cat.MaskBit == 7) name = "other";
                    else if (cat.MaskBit == 8) name = "all";
                    else name = DTIcons[Mode][cat.MaskBit];
                }
                else
                {
                    if (cat.MaskBit == 7) name = "other";
                    else if (cat.MaskBit == 8) name = "all";
                    else name = CatIcons[category][cat.MaskBit];
                }

                cat.IconName = name;

                // R122: resolve the ORIGINAL subsort art for this slot (room mode
                // arrives with OriginalName already set; the function/expansion
                // paths derive it here). Multi-frame sheets crop to the up frame.
                // R126: build subcats resolve from the kToolBtn* plaques keyed
                // by their own STR# 139 index.
                if (cat.OriginalName == null && Mode == UICatalogMode.Build)
                {
                    string buildArt;
                    if (BuildSubSortArt.TryGetValue(cat.StrInd, out buildArt)) cat.OriginalName = buildArt;
                }
                if (cat.OriginalName == null && Mode != UICatalogMode.Build)
                {
                    if (Mode != UICatalogMode.Normal)
                    {
                        var dtArt = DTSortArt[Mode];
                        cat.OriginalName = (cat.MaskBit == 7) ? SubSortOtherArt
                            : (cat.MaskBit == 8) ? SubSortAllArt
                            : dtArt[cat.MaskBit];
                    }
                    else
                    {
                        var family = CanonFuncFamilies[RemapString[category]];
                        cat.OriginalName = (cat.MaskBit == 7) ? SubSortOtherArt
                            : (cat.MaskBit == 8) ? SubSortAllArt
                            : (cat.MaskBit == 5) ? SubSortPetsArt
                            : (cat.MaskBit == 6) ? SubSortMagicArt
                            : "cpanel\\Catalog\\SubSortIcons\\" + family + "\\BuySubSort" + SubSortSlotArt[cat.MaskBit];
                    }
                }
                Texture2D origTex = ResolveSubSortArt(cat, Mode);

                UICatButton subbutton;
                if (origTex != null)
                {
                    subbutton = new UICatButton(origTex);
                    subbutton.OriginalStyle = true;
                }
                else
                {
                    subbutton = new UICatButton(Content.Get().CustomUI.Get("cat_"+name+".png").Get(GameFacade.GraphicsDevice));
                }
                // R122: the caption is the button TOOLTIP (the tables' own labels
                // call these strings sort tips); the art carries the visible text.
                subbutton.Tooltip = str;
                subbutton.OnButtonClick += (btn) => { InitSubcategory(cat); };
                subbutton.Position = new Vector2(boff * (1.5f + i * 2) - subbutton.Texture.Width / 2f, 16);
                subbutton.Disabled = SubcatIsEmpty(cat);
                SelButtons.Add(subbutton);
                Add(subbutton);
            }

            //InitSubcategory(0);
        }

        private void AddFloors(sbyte category)
        {
            // The two category-14 sentinel floors execute through
            // UIFloorPainter, but display with the original pool/water
            // four-state UI sheets and dedicated popup art. Ordinary floor
            // patterns retain the floor thumbnail provider.
            UICatalogResProvider res = category == 14
                ? (UICatalogResProvider)new UIOriginalPoolWaterResProvider()
                : new UICatalogFloorResProvider();

            var floors = Content.Get().WorldFloors.List();

            for (int i = 0; i < floors.Count; i++)
            {
                var floor = (FloorReference)floors[i];

                if ((category == 14) != (floor.ID >= 65534)) continue;
                FullCategory.Insert(0, new UICatalogElement
                {
                    Item = new ObjectCatalogItem()
                    {
                        Name = floor.Name,
                        Category = category,
                        Price = (uint)floor.Price,
                    },
                    Special = new UISpecialCatalogElement
                    {
                        Control = typeof(UIFloorPainter),
                        ResID = floor.ID,
                        Res = res,
                        Parameters = new List<int> { (int)floor.ID } //pattern
                    }
                });
            }
        }

        private void AddWallpapers()
        {
            var res = new UICatalogWallpaperResProvider();

            var walls = Content.Get().WorldWalls.List();

            for (int i = 0; i < walls.Count; i++)
            {
                var wall = (WallReference)walls[i];
                FullCategory.Insert(0, new UICatalogElement
                {
                    Item = new ObjectCatalogItem()
                    {
                        Name = wall.Name,
                        Category = 15,
                        Price = (uint)wall.Price,
                    },
                    Special = new UISpecialCatalogElement
                    {
                        Control = typeof(UIWallPainter),
                        ResID = wall.ID,
                        Res = res,
                        Parameters = new List<int> { (int)wall.ID } //pattern
                    }
                });
            }
        }

        private void AddRoofs()
        {
            // R130: the ORIGINAL roof panel contents (STR# 147 'roofpanelstrs'
            // + the kBldSbTlRoof* plaques; canon in tools/iff-dump/r130/): the
            // four PITCH sub-tools lead the list in the engine panel's display
            // order (layout directives Steep (10;1), Medium (10;30),
            // Shallow (10;50), Flat (10;71)), then the pattern swatches —
            // each composed inside the roof's own 45x45 RoofPatternTemplate
            // frame (UICatalogItem.RoofSwatch) on canon popup thumbs.
            // R131: split into the engine's two PAGES (SetCurPage): page 0 =
            // pitch sub-tools, page 1 = patterns; FullCategory keeps the R130
            // flat order for name resolution/search, the page filter picks the
            // visible list.
            var res = new UIOriginalRoofResProvider();

            var total = Content.Get().WorldRoofs.Count;
            RoofPatternItems = new List<UICatalogElement>();
            for (int i = 0; i < total; i++)
            {
                sbyte category = 17;
                RoofPatternItems.Add(new UICatalogElement
                {
                    Item = new ObjectCatalogItem()
                    {
                        Name = "",
                        Category = category,
                        Price = 0,
                    },
                    Special = new UISpecialCatalogElement
                    {
                        Control = typeof(UIRoofer),
                        ResID = (uint)i,
                        Res = res,
                        Parameters = new List<int> { i } //pattern
                    }
                });
            }

            RoofPitchItems = new List<UICatalogElement>();
            var pitchRes = new UIOriginalRoofPitchResProvider();
            for (int i = 0; i < UIOriginalRoofPitchResProvider.PitchIds.Length; i++)
            {
                int rtId = UIOriginalRoofPitchResProvider.PitchIds[i];
                RoofPitchItems.Add(new UICatalogElement
                {
                    Item = new ObjectCatalogItem()
                    {
                        Name = pitchRes.GetName((ulong)rtId),
                        Category = 17,
                        Price = 0,
                    },
                    Special = new UISpecialCatalogElement
                    {
                        Control = typeof(UIRoofPitcher),
                        ResID = (uint)rtId,
                        Res = pitchRes,
                        Parameters = new List<int> { rtId } //roof pitch (RT id)
                    }
                });
            }

            FullCategory.InsertRange(0, RoofPitchItems);
            FullCategory.InsertRange(RoofPitchItems.Count, RoofPatternItems);
            BuildRoofPager();
        }

        private void BuildRoofPager()
        {
            if (RoofPageTitle != null) { RoofPagerRemount(); return; }
            RoofPageTitle = new UILabel();
            RoofPageTitle.CaptionStyle = RoofPageTitle.CaptionStyle.Clone();
            RoofPageTitle.CaptionStyle.Size = 15;
            RoofPageTitle.CaptionStyle.Color = UIStyle.Current.Text;
            RoofPageTitle.Size = new Vector2(1);
            RoofPageTitle.Position = new Vector2(74, 106);
            Add(RoofPageTitle);

            var prevTex = Simitone.Client.UI.Model.UIOriginal.EnsureResolved("cpanel\\Buttons\\ScrollLeft.bmp")?.Get(GameFacade.GraphicsDevice);
            var nextTex = Simitone.Client.UI.Model.UIOriginal.EnsureResolved("cpanel\\Buttons\\ScrollRight.bmp")?.Get(GameFacade.GraphicsDevice);
            if (prevTex != null)
            {
                RoofPrevBtn = new UIElasticButton(prevTex);
                RoofPrevBtn.Position = new Vector2(28, 116);
                RoofPrevBtn.ScaleX = RoofPrevBtn.ScaleY = 0.42f;
                RoofPrevBtn.OnButtonClick += (b) => SetRoofPage(RoofPage - 1);
                Add(RoofPrevBtn);
            }
            if (nextTex != null)
            {
                RoofNextBtn = new UIElasticButton(nextTex);
                RoofNextBtn.Position = new Vector2(478, 116);
                RoofNextBtn.ScaleX = RoofNextBtn.ScaleY = 0.42f;
                RoofNextBtn.OnButtonClick += (b) => SetRoofPage(RoofPage + 1);
                Add(RoofNextBtn);
            }
            RoofPagerMounts++;
        }

        private void RoofPagerRemount()
        {
            // the panel rebuilds FullCategory per category switch; re-apply the
            // current page so FilterCategory points at the FRESH page lists.
            SetRoofPage(RoofPage);
        }

        public void SetRoofPage(int page)
        {
            if (RoofPitchItems == null || RoofPatternItems == null) return;
            RoofPage = Math.Max(0, Math.Min(1, page));
            FilterCategory = (RoofPage == 0) ? (IEnumerable<UICatalogElement>)RoofPitchItems : RoofPatternItems;
            if (RoofPageTitle != null)
                RoofPageTitle.Caption = GameFacade.Strings.GetString("147", RoofPage == 0 ? "14" : "16");
            if (RoofPrevBtn != null) RoofPrevBtn.Disabled = (RoofPage == 0);
            if (RoofNextBtn != null) RoofNextBtn.Disabled = (RoofPage == 1);
            CatContainer.Reset();
        }

        private void UpdateRoofPagerVisibility(bool on)
        {
            if (RoofPageTitle == null) return;
            RoofPageTitle.Visible = on;
            if (RoofPrevBtn != null) RoofPrevBtn.Visible = on;
            if (RoofNextBtn != null) RoofNextBtn.Visible = on;
        }

        public static short[] WallStyleIDs =
        {
            0x1, //wall
            0x2, //picket fence
            0xD, //iron fence
            0xC, //privacy fence
            0xE //banisters
        };

        public static short[] WallStylePatterns =
        {
            0, //wall
            248, //picket fence
            250, //iron fence
            249, //privacy fence
            251, //banisters
        };

        private void AddWallStyles()
        {
            var res = new UICatalogWallResProvider();

            for (int i = 0; i < WallStyleIDs.Length; i++)
            {
                var walls = Content.Get().WorldWalls;
                var style = walls.GetWallStyle((ulong)WallStyleIDs[i]);
                FullCategory.Insert(i, new UICatalogElement
                {
                    Item = new ObjectCatalogItem()
                    {
                        Name = style.Name,
                        Category = 13,
                        Price = (uint)style.Price,
                    },
                    Special = new UISpecialCatalogElement
                    {
                        Control = typeof(UIWallPlacer),
                        ResID = (ulong)WallStyleIDs[i],
                        Res = res,
                        Parameters = new List<int> { WallStylePatterns[i], WallStyleIDs[i] } //pattern, style
                    }
                });
            }
        }

        private void AddTerrainTools()
        {
            var res = new UICatalogWallResProvider();

            // R127: the ORIGINAL terrain tool set — four tools on canon
            // kBldSbTl* plaque art (UIOriginalTerrainResProvider). The port's
            // old combined "Raise/Lower" tool splits back into the original
            // Up/Down pair (distinct icons AND popups per direction;
            // UITerrainRaiser parameters[0]==1 = lower mode). Display order
            // follows the RT popup sequence Up(3600), Down(3601), Level(3602),
            // then the Hot Date Grass(3607). The old provider resolved TSO
            // UIFileIDs that cannot exist on TS1 data (blank 1x1 cells).
            FullCategory.Insert(0, new UICatalogElement
            {
                Item = new ObjectCatalogItem()
                {
                    Name = "Raise Terrain",
                    Category = 18,
                    Price = 1,
                },
                Special = new UISpecialCatalogElement
                {
                    Control = typeof(UITerrainRaiser),
                    ResID = 0,
                    Res = new UIOriginalTerrainResProvider(),
                    Parameters = new List<int> { }
                }
            });

            FullCategory.Insert(1, new UICatalogElement
            {
                Item = new ObjectCatalogItem()
                {
                    Name = "Lower Terrain",
                    Category = 18,
                    Price = 1,
                },
                Special = new UISpecialCatalogElement
                {
                    Control = typeof(UITerrainRaiser),
                    ResID = 3,
                    Res = new UIOriginalTerrainResProvider(),
                    Parameters = new List<int> { 1 }
                }
            });

            FullCategory.Insert(2, new UICatalogElement
            {
                Item = new ObjectCatalogItem()
                {
                    Name = "Flatten Terrain",
                    Category = 18,
                    Price = 1,
                },
                Special = new UISpecialCatalogElement
                {
                    Control = typeof(UITerrainFlatten),
                    ResID = 1,
                    Res = new UIOriginalTerrainResProvider(),
                    Parameters = new List<int> { }
                }
            });

            FullCategory.Insert(3, new UICatalogElement
            {
                Item = new ObjectCatalogItem()
                {
                    Name = "Grass Tool",
                    Category = 18,
                    Price = 1,
                },
                Special = new UISpecialCatalogElement
                {
                    Control = typeof(UIGrassPaint),
                    ResID = 2,
                    Res = new UIOriginalTerrainResProvider(),
                    Parameters = new List<int> { }
                }
            });
        }

        public override void GameResized()
        {
            base.GameResized();
            CatContainer.Size = new Vector2(Size.X, 128);
            if (NoResultsLabel != null)
                NoResultsLabel.Size = new Vector2(Size.X, 25);
            if (BandMode) { RebuildBand(); return; }   // R145: no mobile subsort row to reset
            if (ChoosingSub) Reset();
        }

        public override void Update(UpdateState state)
        {
            // Check for pending eyedropper selection (after category switch)
            CheckPendingEyedropperSelection();

            Invalidate();
            var first = SelButtons.FirstOrDefault();
            if (first != null && first.Opacity == 0)
            {
                foreach (var btn in SelButtons) Remove(btn);
                foreach (var label in SelLabels) Remove(label);
                SelButtons.Clear();
                SelLabels.Clear();
            }
            base.Update(state);
        }

        public UITSContainer CatalogElemProvider(int index)
        {
            var elem = new Catalog.UICatalogItem(FilterCategory.ElementAt(index), this);
            return elem;
        }

        // ---------------- R145 desktop band branch ----------------

        /// the engine grid geometry per mode (band-local == panel-local: the
        /// panel mounts at MainPanel (0,0) covering the whole band on desktop
        /// BUILD/BUY). BUILD = the subtool pattern row, ONE row of ~10 at 45px
        /// pitch from (338,5), paged by res 100/101 arrows at (326,26)/(W-14,26)
        /// (build-toolbar-law.md section 5); BUY = 13 cols x 2 rows from
        /// (195,5) (26/page @1024) with res 102/101 arrows at (184,26) and the
        /// right edge (buy-catalog-law.md section 3).
        private void SetupBand()
        {
            bool build = Mode == UICatalogMode.Build;
            BandPagePrev = new Simitone.Client.UI.Panels.UIOriginalSheetButton(
                build ? "cpanel\\Buttons\\ScrollLeft.bmp" : "cpanel\\Buttons\\ScrollLeftBuy.bmp")
            {
                Position = new Vector2(build ? 326 : 184, 26),
                Tooltip = GameFacade.Strings.GetString("154", "0"),
            };
            BandPagePrev.OnButtonClick += (b) => { BandPage--; RebuildBand(); };
            Add(BandPagePrev);
            BandPageNext = new Simitone.Client.UI.Panels.UIOriginalSheetButton("cpanel\\Buttons\\ScrollRight.bmp")
            {
                Tooltip = GameFacade.Strings.GetString("154", "1"),
            };
            BandPageNext.OnButtonClick += (b) => { BandPage++; RebuildBand(); };
            Add(BandPageNext);
            BandPopup = new Simitone.Client.UI.Panels.UIOriginalCatalogPopup();
            BandPopup.Visible = false;
            // the popup rises ABOVE the 100px band — mount it on the MAIN
            /// panel (the subpanel is a cached container whose RT would clip it).
            ((UIMainPanel)Game.Frontend.MainPanel)?.Add(BandPopup);
            RebuildBand();
        }

        public void RebuildBand()
        {
            if (BandPagePrev == null || BandPageNext == null) return;   // ctor-time GameResized precedes SetupBand
            float w = Size.X > 0 ? Size.X : (FSO.Client.GameFacade.Screens.CurrentUIScreen.ScreenWidth - 220);
            bool build = Mode == UICatalogMode.Build;
            int ox = build ? 338 : 195;
            int wrap = build ? (int)w - 4 : (int)w - 12;
            int cols = 0;
            for (int x = ox; x + 45 <= wrap; x += 45) cols++;
            int rows = build ? 1 : 2;
            int perPage = cols * rows;

            foreach (var c in BandCells) Remove(c);
            BandCells.Clear();
            var items = FilterCategory?.ToList() ?? new List<UICatalogElement>();
            int maxPage = Math.Max(0, (items.Count - 1) / perPage);
            BandPage = Math.Max(0, Math.Min(maxPage, BandPage));

            BandPageNext.Position = new Vector2(build ? w - 14 : w - 24 - 9, 26);
            BandPagePrev.Visible = BandPage != 0;
            // engine build law: next shown iff count > cols (0x260974); buy:
            // prev hidden at page 0, next while more pages remain.
            BandPageNext.Visible = build ? (items.Count > cols) : ((BandPage + 1) * perPage < items.Count);

            for (int i = 0; i < items.Count; i++)
            {
                int slot = i - BandPage * perPage;
                if (slot < 0 || slot >= perPage) continue;
                var cell = new Simitone.Client.UI.Panels.UIOriginalCatalogCell(items[i])
                {
                    Position = new Vector2(ox + (slot % cols) * 45, 5 + (slot / cols) * 45),
                };
                var idx = i;
                cell.OnButtonClick += (b) => BandCellClick(idx, cell);
                // R148 engine hover law (cWinCatalog cmd 0x13 -> EnablePopup
                // 0x26a76c; TSOnMouseExitChild -> DisablePopup 0x26a8c0): the
                // info popup follows the HOVERED cell, not just the click.
                cell.OnButtonHover += (b) => ShowBandPopup(idx, cell);
                // cWinCatalog::TSOnMouseExitChild (0x26a8c0) disables the
                // popup whenever the exited child is not the popup itself.
                // Selection does not make the hover surface sticky.
                cell.OnButtonExit += (b) => HideBandPopup();
                Add(cell);
                BandCells.Add(cell);
            }
            if (BandPopup != null) BandPopup.Visible = false;   // re-page: the hovered cell is gone
            SyncBandSelection();
        }

        private void BandCellClick(int index, Simitone.Client.UI.Panels.UIOriginalCatalogCell cell)
        {
            if (ItemID == index)
            {
                cell.Selected = false;
                Deselect();
                if (BandPopup != null) BandPopup.Visible = false;
            }
            else
            {
                Selected(index);
                // Selected() opens the separate PopupInfo description panel.
                // Keeping the hover compositor enabled here produced two
                // product descriptions and left the hover surface stranded
                // over the world while the user moved the object.
                HideBandPopup();
            }
            SyncBandSelection();
        }

        private void HideBandPopup()
        {
            if (BandPopup != null) BandPopup.Visible = false;
        }

        /// Engine cWinCatalogPopup hover surface, placed ABOVE-LEFT of the
        /// button over the 3D view (BuildMyBuffer 0x26dd98-0x26dea4).
        public static bool HasDedicatedPopupThumb(UICatalogResProvider provider)
        {
            if (provider == null) return false;
            var method = provider.GetType().GetMethod(
                nameof(UICatalogResProvider.GetThumb), new Type[] { typeof(ulong) });
            return method != null && method.DeclaringType != typeof(UICatalogResProvider);
        }

        private void ShowBandPopup(int index, Simitone.Client.UI.Panels.UIOriginalCatalogCell cell)
        {
            if (BandPopup == null) return;
            var elem = FilterCategory.ElementAt(index);
            string name = elem.DisplayName ?? elem.Item.Name;
            string desc = null;
            int price = (int)elem.Item.Price;
            List<string> ratingLines = null;
            try
            {
                if (elem.Special?.Res != null)
                {
                    name = elem.Special.Res.GetName(elem.Special.ResID) ?? name;
                    desc = elem.Special.Res.GetDescription(elem.Special.ResID);
                    price = elem.Special.Res.GetPrice(elem.Special.ResID);
                }
                else if (elem.Item.GUID != 0 && elem.Item.GUID != uint.MaxValue)
                {
                    var worldObj = FSO.Content.Content.Get().WorldObjects.Get(elem.Item.GUID);
                    desc = worldObj?.Resource?.Get<CTSS>(worldObj.OBJ.CatalogStringsID)?.GetString(1);
                    if (worldObj != null) ratingLines = BuildPopupRatingLines(worldObj.OBJ);
                }
            }
            catch { }
            // cell coords are panel-local; the popup mounts on the MAIN panel
            // whose origin == this panel's origin (both MainPanel children at
            /// the band's top-left on desktop).
            var abs = cell.Position + Position;
            Texture2D popupIcon = null;
            var pairedProductIcon = false;
            var ownsPopupIcon = false;
            if (elem.Special?.Res != null)
            {
                // Only an override denotes dedicated popup art. The base
                // GetThumb delegates to GetIcon; floor/wall providers make a
                // fresh texture copy there while declaring DoDispose=false.
                // Calling that path on every hover would abandon a GPU texture,
                // so providers without an override reuse the existing cell icon.
                if (HasDedicatedPopupThumb(elem.Special.Res))
                {
                    Texture2D dedicatedThumb = null;
                    try { dedicatedThumb = elem.Special.Res.GetThumb(elem.Special.ResID); }
                    catch { }
                    if (dedicatedThumb != null)
                    {
                        popupIcon = dedicatedThumb;
                        try { ownsPopupIcon = elem.Special.Res.DoDispose(); }
                        catch { }
                    }
                }
                if (popupIcon == null) popupIcon = cell.Icon;
                if (ReferenceEquals(popupIcon, cell.Icon))
                    pairedProductIcon = cell.PairedProductIcon;
            }
            else
            {
                // Product::DrawIcon type 1 renders the GUID object's iconic
                // view. The paired 74x37 ProductButton sheet belongs only to
                // the grid cell and must never be used as the popup preview.
                popupIcon = GetBandObjectThumb(elem.Item.GUID);
            }
            // UI-12: BuildMyBuffer tints the name/price with the engine error
            // red when the family funds (VM global 0) are under the price.
            // The purchase-path budget gate itself stays undecoded (the todo
            // below remains).
            bool affordable = true;
            try { affordable = Game.LotControl.vm.GetGlobalValue(0) >= price; } catch { }
            BandPopup.SetInfo(popupIcon, pairedProductIcon, name, price, desc, ownsPopupIcon, affordable, ratingLines);
            // R148 anchor law (BuildMyBuffer 0x26dd98-0x26dea4): the popup's
            // top-right sits at the button's top-left — x = btnX - 559,
            // y = btnY - the measured popup height (127px minimum). The engine
            // itself would push column-0 popups off-screen; clamp x at 0
            // (disclosed deviation).
            BandPopup.Position = new Microsoft.Xna.Framework.Vector2(
                Math.Max(0, abs.X - Simitone.Client.UI.Panels.UIOriginalCatalogPopup.POPUP_W),
                abs.Y - BandPopup.Size.Y);
            // SetupBand runs inside this subpanel's constructor, before
            // UIMainPanel.SetSubpanel appends the band itself. Merely mounting
            // the popup there therefore leaves it *below* the catalog in draw
            // order. Second-row popups end at y=50 and the band consequently
            // covered their lower 50px (the user's truncated descriptions).
            // The engine's EnablePopup adds the window to the main view at
            // show time and marks it as overlapping the scroll area. Re-Add is
            // the UIContainer equivalent: an existing child moves to the top
            // without invoking Removed() or disturbing texture ownership.
            ((UIMainPanel)Game.Frontend.MainPanel)?.Add(BandPopup);
            BandPopup.Visible = true;
            // UI-12: the decoded 250 ms slide-up from the source row (the
            // hovered cell's row stands in for engine field +0x80); drawn-Y
            // only, the anchor Position above is untouched.
            BandPopup.BeginSlideIn(abs.Y);
        }

        // UI-13: the BuildMyBuffer rating block, on the query panel's
        // data-validated law (r124): STR#160 [0..6] 'X: %d' with the signed
        // OBJD rating when nonzero; [7..13] '+ Skill' when the
        // RatingSkillFlags bit is set. The i==7 special case in the engine
        // loop is exactly this motive→skill boundary, which pins the popup's
        // label indexing to STR#160 [0..13] (r145's "[1..14]" was off by
        // one). [14..19] usage flags: no OBJD encoding (r124 negative
        // space) — not rendered, as in the query panel.
        private static List<string> BuildPopupRatingLines(FSO.Files.Formats.IFF.Chunks.OBJD def)
        {
            if (def == null) return null;
            var lines = new List<string>();
            var ratings = new short[] { def.RatingHunger, def.RatingComfort, def.RatingHygiene,
                                        def.RatingBladder, def.RatingEnergy, def.RatingFun, def.RatingRoom };
            for (int i = 0; i < 7; i++)
            {
                if (ratings[i] != 0)
                    lines.Add(GameFacade.Strings.GetString("160", i.ToString()).Replace("%d", ratings[i].ToString()));
            }
            var sFlags = def.RatingSkillFlags;
            for (int i = 0; i < 7; i++)
            {
                if ((sFlags & (1 << i)) > 0)
                    lines.Add(GameFacade.Strings.GetString("160", (i + 7).ToString()));
            }
            return lines.Count > 0 ? lines : null;
        }

        public void SyncBandSelection()
        {
            var sel = (ItemID >= 0 && FilterCategory != null && ItemID < FilterCategory.Count())
                ? FilterCategory.ElementAt(ItemID) : null;
            foreach (var c in BandCells) c.Selected = (sel != null && ReferenceEquals(c.Element, sel));
        }

        public byte GetSubsort(ObjectCatalogItem item)
        {
            switch (Mode)
            {
                case UICatalogMode.Downtown:
                    return item.DowntownSort;
                case UICatalogMode.Community:
                    return item.CommunitySort;
                case UICatalogMode.Vacation:
                    return item.VacationSort;
                case UICatalogMode.Studiotown:
                    return item.StudiotownSort;
                case UICatalogMode.Magictown:
                    return item.MagictownSort;
                default:
                    if (item.RoomSort == 0) return 0; //items without a room sort should not appear.
                    return item.Subsort;
            }
        }

        private bool SubcatIsEmpty(UICatalogSubcat cat)
        {
            // R122: room-mode subsorts filter on the item's catalog function
            // category (FuncId), not the subsort bitmask.
            if (RoomMode && cat.FuncId >= 0)
            {
                return !FullCategory.Any(x => x.Item.Category == cat.FuncId);
            }
            var index = cat.MaskBit;
            if (Mode == UICatalogMode.Build)
            {
                return FullCategory.FirstOrDefault() == null;
            }
            else if (index == 8)
            {
                return !FullCategory.Any(x => (GetSubsort(x.Item)) > 0);
            }
            else
            {
                var mask = 1 << index;
                return !FullCategory.Any(x => (GetSubsort(x.Item) & mask) > 0);
            }
        }

        public void ApplyNameFilter(string term)
        {
            // In Build mode, FullCategory contains wrong (buy-mode) items until a subcategory
            // is chosen, so skip filtering. In Buy mode, FullCategory is valid immediately.
            if (ChoosingSub && Mode == UICatalogMode.Build) return;

            if (string.IsNullOrEmpty(term))
            {
                ActiveSearchTerm = null;
                // Always restore, even if PreSearchFilterCategory was null
                FilterCategory = PreSearchFilterCategory;
                PreSearchFilterCategory = null;
                // Restore the subcategory overlay if it was visible before the search
                foreach (var btn in SelButtons) btn.Visible = true;
                foreach (var label in SelLabels) label.Visible = true;
                NoResultsLabel.Visible = false;
                CatContainer.Reset();
                return;
            }

            // Save the pre-search FilterCategory the first time we enter search mode
            if (ActiveSearchTerm == null)
            {
                PreSearchFilterCategory = FilterCategory;
                // Hide the subcategory overlay while searching so results aren't obscured
                foreach (var btn in SelButtons) btn.Visible = false;
                foreach (var label in SelLabels) label.Visible = false;
            }

            ActiveSearchTerm = term;

            // Search within the current category's items only
            FilterCategory = (FullCategory ?? Enumerable.Empty<UICatalogElement>())
                .Where(elem => elem.Item.GUID != uint.MaxValue &&
                               (elem.DisplayName?.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                elem.Item.Name?.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                elem.Item.CatalogName?.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0))
                .ToList();

            NoResultsLabel.Visible = !FilterCategory.Any();
            CatContainer.Reset();
        }

        public void InitSubcategory(UICatalogSubcat cat)
        {
            var index = cat.MaskBit;
            // R146: the filter ALWAYS re-runs — sub-plaque clicks land here
            // repeatedly on the same panel (ChoosingSub only gates the mobile
            // row visuals on the FIRST transition).
            if (ChoosingSub)
            {
                ChoosingSub = false;
                if (!BandMode)
                {
                    ((UIMainPanel)Parent).Switcher.Close();
                    // R122: when the slot carries ORIGINAL art, the switcher main button
                    // shows that art's up frame instead of the modern png.
                    Texture2D selOrig = ResolveSubSortArt(cat, Mode);
                    if (selOrig != null) ((UIMainPanel)Parent).Switcher.MainButton.ReplaceImage(selOrig);
                    else ((UIMainPanel)Parent).Switcher.MainButton.ReplaceImage(Content.Get().CustomUI.Get("cat_" + cat.IconName + ".png").Get(GameFacade.GraphicsDevice));

                    foreach (var btn in SelButtons)
                    {
                        GameFacade.Screens.Tween.To(btn, 0.5f, new Dictionary<string, float>() { { "Opacity", 0f } }, TweenQuad.EaseOut);
                    }
                    foreach (var label in SelLabels)
                    {
                        GameFacade.Screens.Tween.To(label, 0.5f, new Dictionary<string, float>() { { "Opacity", 0f } }, TweenQuad.EaseOut);
                    }

                    CatContainer.Opacity = 0f;
                    GameFacade.Screens.Tween.To(CatContainer, 0.5f, new Dictionary<string, float>() { { "Opacity", 1f } }, TweenQuad.EaseOut);
                }
            }

            if (Mode == UICatalogMode.Build)
            {
                InitCategory((sbyte)index, true);
                if (index == 17) SetRoofPage(RoofPage); // R131: engine paged roof panel
                else FilterCategory = FullCategory;
            }
            else if (cat.FuncId >= 0)
            {
                // R122: room subsort = one function category within the room's
                // items. R146: expansion subsort = main-mask AND function
                // within the mode's catalog (engine mask law 1<<slot).
                IEnumerable<UICatalogElement> src = FullCategory;
                if (Mode != UICatalogMode.Normal && Mode != UICatalogMode.Build && BandMainSlot >= 0)
                {
                    int mmask = 1 << BandMainSlot;
                    src = FullCategory.Where(x => (GetSubsort(x.Item) & mmask) != 0);
                }
                FilterCategory = src.Where(x => x.Item.Category == cat.FuncId);
            }
            else if (index == 8)
            {
                FilterCategory = FullCategory.Where(x => (GetSubsort(x.Item)) > 0);
            }
            else
            {
                var mask = 1 << index;
                if (Mode == UICatalogMode.Normal && index == 7)
                {
                    mask |= 16;
                }
                FilterCategory = FullCategory.Where(x => (GetSubsort(x.Item) & mask) > 0);
            }
            if (BandMode)
            {
                // R145: the engine band grid re-pages from the new filter.
                BandPage = 0;
                RebuildBand();
                return;
            }
            CatContainer.Reset();
        }
    }

    public enum UICatalogMode
    {
        Normal,
        Downtown,
        Community,
        Vacation,
        Studiotown,
        Magictown,
        Build
    }

    public class UICatalogElement
    {
        public ObjectCatalogItem Item;
        public int CalcPrice;
        public UISpecialCatalogElement Special;
        public string DisplayName; // CTSS catalog display name (e.g. "Soma Plasma TV"); null until resolved

        public override string ToString()
        {
            return DisplayName ?? Item.Name ?? "(unknown)";
        }
    }

    public class UISpecialCatalogElement
    {
        public Type Control;
        public ulong ResID;
        public UICatalogResProvider Res;
        public List<int> Parameters;
    }

    public class UICatalogSubcat
    {
        public int StrTable;
        public int StrInd;
        public int MaskBit;
        public string IconName;
        // R122: the ORIGINAL cpanel\Catalog art member for this slot (multi-frame
        // sheets crop to their up frame) and, for room-mode subsorts, the catalog
        // function category the slot filters on.
        public string OriginalName;
        public int FuncId = -1;
    }
}
