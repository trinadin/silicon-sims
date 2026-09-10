using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using Microsoft.Xna.Framework;
using Simitone.Client.UI.Panels.LiveSubpanels;
using System;
using System.Collections.Generic;

namespace Simitone.Client.UI.Panels
{
    /// <summary>
    /// R145: the cWinCatalog BUY-mode toolbar chrome (engine law
    /// tools/iff-dump/r145/buy-catalog-law.md). MainPanel-local = catalog-local.
    ///
    /// Composition (DECODED):
    /// - 8 tab plaques in 2 rows of 4 at the irregular "hanging plaque"
    ///   anchors (anchor tables BSS 0x931c0/0x93140) for the MAIN sorts, or
    ///   the regular 38/48 grid (31,10) in SUBSORT state.
    /// - the big LEFT toggle plaque (BuyBack* art, one per (sort, current
    ///   main)) flush at the band's left edge (-2, (100-h)/2), hidden in the
    ///   main state; click returns to the main sorts.
    /// - the item GRID lives in the browse panel's desktop band branch at
    ///   (195,5) 45x45, 13 cols x 2 rows (26/page @1024), paged by res
    ///   102/101 arrows at (184,26)/right-edge.
    /// - sort law: the UCP Objects button TOGGLES room (0) <-> function (1)
    ///   on every activation; first-ever entry = ROOM main, Living selected
    ///   (CPState defaults + the toggle law, section 2).
    /// Tooltips STR# 150 ([0..7] rooms / [8..15] functions).
    /// </summary>
    public class UIOriginalBuyChrome : UIContainer
    {
        public int Sort = 0;          // 0 room, 1 function (engine NewCatalogSort)
        public int SortState = 0;     // 0 main plaques, 1 subsorts
        public int CurrentMain = 0;   // selected room/function index
        public int CurrentSub = 0;

        public UIOriginalSheetButton[] Plaques = new UIOriginalSheetButton[8];
        public UIOriginalSheetButton Toggle;
        public List<UICatalogSubcat> SubCats;    // the current main's subsorts
        public Action<int, int> OnMainSelect;   // (sort, mainIdx)
        public Action<int> OnSubSelect;

        // R146: the lot's catalog mode — Normal uses the room/function sorts;
        // an expansion mode (Downtown..Magictown) locks the band to that sort
        // (engine cWinViewControl TSOnCommand 0x2b4b24: sorts 2+ are KEPT —
        // the Objects button never toggles on expansion lots).
        public UICatalogMode LotMode = UICatalogMode.Normal;

        // anchor tables (net catalog-local; BSS 0x931c0 room main / 0x93140
        // function main / the shared sub grid 0x93180).
        public static readonly int[] RoomMainX = { 22, 61, 103, 141, 19, 64, 102, 144 };
        public static readonly int[] RoomMainY = { 7, 3, 7, 4, 53, 59, 55, 64 };
        public static readonly int[] FuncMainX = { 22, 59, 102, 149, 21, 65, 102, 144 };
        public static readonly int[] FuncMainY = { 8, 16, 1, 4, 61, 59, 55, 64 };
        public static readonly int[] SubX = { 31, 69, 107, 145, 31, 69, 107, 145 };
        public static readonly int[] SubY = { 10, 10, 10, 10, 58, 58, 58, 58 };

        // R146 expansion main anchors (catalog-local; sinit 0x26c7d0 §0.1 —
        // the "downtown pattern" top row + the standard bottom row; only
        // slots 0-3 and 7 are used, 4-6 are hidden by LoadBooks). Studio's
        // 4th main sits at 138, not 149.
        public static readonly int[] ExpMainX = { 22, 64, 98, 149, 0, 0, 0, 144 };
        public static readonly int[] ExpMainY = { 8, 12, 12, 14, 0, 0, 0, 64 };
        public static readonly int[] ExpMainXStudio = { 22, 64, 98, 138, 0, 0, 0, 144 };

        // STR# 150 display order (r122 canon).
        public static readonly string[] RoomMainArt =
        {
            "cpanel\\Catalog\\BuyRLiving.bmp", "cpanel\\Catalog\\BuyRDining.bmp",
            "cpanel\\Catalog\\BuyRBedroom.bmp", "cpanel\\Catalog\\BuyRStudy.bmp",
            "cpanel\\Catalog\\BuyRKitchen.bmp", "cpanel\\Catalog\\BuyRBathroom.bmp",
            "cpanel\\Catalog\\BuyROutside.bmp", "cpanel\\Catalog\\BuyRMisc.bmp",
        };
        public static readonly string[] FuncMainArt =
        {
            "cpanel\\Catalog\\BuyFSeating.bmp", "cpanel\\Catalog\\BuyFSurfaces.bmp",
            "cpanel\\Catalog\\BuyFDecorative.bmp", "cpanel\\Catalog\\BuyFElectronics.bmp",
            "cpanel\\Catalog\\BuyFAppliances.bmp", "cpanel\\Catalog\\BuyFPlumbing.bmp",
            "cpanel\\Catalog\\BuyFLighting.bmp", "cpanel\\Catalog\\BuyFMisc.bmp",
        };
        public static readonly string[] RoomBackArt =
        {
            "cpanel\\Catalog\\BackButtons\\BackRLiving.bmp", "cpanel\\Catalog\\BackButtons\\BackRDining.bmp",
            "cpanel\\Catalog\\BackButtons\\BackRBedroom.bmp", "cpanel\\Catalog\\BackButtons\\BackRStudy.bmp",
            "cpanel\\Catalog\\BackButtons\\BackRKitchen.bmp", "cpanel\\Catalog\\BackButtons\\BackRBathroom.bmp",
            "cpanel\\Catalog\\BackButtons\\BackROutside.bmp", "cpanel\\Catalog\\BackButtons\\BackRMisc.bmp",
        };
        public static readonly string[] FuncBackArt =
        {
            "cpanel\\Catalog\\BackButtons\\BackFSeating.bmp", "cpanel\\Catalog\\BackButtons\\BackFSurfaces.bmp",
            "cpanel\\Catalog\\BackButtons\\BackFDecorative.bmp", "cpanel\\Catalog\\BackButtons\\BackFElectronics.bmp",
            "cpanel\\Catalog\\BackButtons\\BackFAppliances.bmp", "cpanel\\Catalog\\BackButtons\\BackFPlumbing.bmp",
            "cpanel\\Catalog\\BackButtons\\BackFLighting.bmp", "cpanel\\Catalog\\BackButtons\\BackFMisc.bmp",
        };

        // R146/R176: expansion plaque art (8 slots — mains at 0-3, MISC at 7;
        // names from Res_CPanel.RT verbatim, incl. its studio/magic aliases onto
        // BuyD* art: kBuySTfood->BuyDDining, kBuySTMisc->BuyDMisc etc).
        // R176 corrects the old sequential-resource inference: the engine's
        // actual Init tables are ST [273,274,276,275,...277] and MT
        // [1705,1706,1708,1707,...1709], so visual slots 2/3 are deliberately
        // permuted into the same semantic order as their 1<<slot catalog masks.
        public static readonly System.Collections.Generic.Dictionary<UICatalogMode, string[]> ExpMainArt =
            new System.Collections.Generic.Dictionary<UICatalogMode, string[]>
        {
            { UICatalogMode.Downtown, new string[] {
                "cpanel\\Catalog\\BuyDDining.bmp", "cpanel\\Catalog\\BuyDShops.bmp",
                "cpanel\\Catalog\\BuyDOutdoor.bmp", "cpanel\\Catalog\\BuyDStreet.bmp",
                null, null, null, "cpanel\\Catalog\\BuyDMisc.bmp" } },
            { UICatalogMode.Community, new string[] {
                "cpanel\\Catalog\\BuyCOne.bmp", "cpanel\\Catalog\\BuyCTwo.bmp",
                "cpanel\\Catalog\\BuyCThree.bmp", "cpanel\\Catalog\\BuyCFour.bmp",
                null, null, null, "cpanel\\Catalog\\BuyCMisc.bmp" } },
            { UICatalogMode.Vacation, new string[] {
                "cpanel\\Catalog\\BuyVOne.bmp", "cpanel\\Catalog\\BuyVTwo.bmp",
                "cpanel\\Catalog\\BuyVThree.bmp", "cpanel\\Catalog\\BuyVFour.bmp",
                null, null, null, "cpanel\\Catalog\\BuyVMisc.bmp" } },
            { UICatalogMode.Studiotown, new string[] {
                "cpanel\\Catalog\\BuyDDining.bmp", "cpanel\\Catalog\\BuyDShops.bmp",
                "cpanel\\Catalog\\BuySTStudio.bmp", "cpanel\\Catalog\\BuySTSpa.bmp",
                null, null, null, "cpanel\\Catalog\\BuyDMisc.bmp" } },
            { UICatalogMode.Magictown, new string[] {
                "cpanel\\Catalog\\BuyDDining.bmp", "cpanel\\Catalog\\BuyDShops.bmp",
                "cpanel\\Catalog\\BuyMTmagic.bmp", "cpanel\\Catalog\\BuyMOutdoor.bmp",
                null, null, null, "cpanel\\Catalog\\BuyDMisc.bmp" } },
        };

        public static readonly System.Collections.Generic.Dictionary<UICatalogMode, string[]> ExpBackArt =
            new System.Collections.Generic.Dictionary<UICatalogMode, string[]>
        {
            { UICatalogMode.Downtown, new string[] {
                "cpanel\\Catalog\\BackButtons\\BackDDining.bmp", "cpanel\\Catalog\\BackButtons\\BackDShops.bmp",
                "cpanel\\Catalog\\BackButtons\\BackDOutdoor.bmp", "cpanel\\Catalog\\BackButtons\\BackDStreet.bmp",
                null, null, null, "cpanel\\Catalog\\BackButtons\\BackDMisc.bmp" } },
            { UICatalogMode.Community, new string[] {
                "cpanel\\Catalog\\BackButtons\\BackCOne.bmp", "cpanel\\Catalog\\BackButtons\\BackCTwo.bmp",
                "cpanel\\Catalog\\BackButtons\\BackCThree.bmp", "cpanel\\Catalog\\BackButtons\\BackCFour.bmp",
                null, null, null, "cpanel\\Catalog\\BackButtons\\BackCMisc.bmp" } },
            { UICatalogMode.Vacation, new string[] {
                "cpanel\\Catalog\\BackButtons\\BackVOne.bmp", "cpanel\\Catalog\\BackButtons\\BackVTwo.bmp",
                "cpanel\\Catalog\\BackButtons\\BackVThree.bmp", "cpanel\\Catalog\\BackButtons\\BackVFour.bmp",
                null, null, null, "cpanel\\Catalog\\BackButtons\\BackVMisc.bmp" } },
            { UICatalogMode.Studiotown, new string[] {
                "cpanel\\Catalog\\BackButtons\\BackDDining.bmp", "cpanel\\Catalog\\BackButtons\\BackDShops.bmp",
                "cpanel\\Catalog\\BackButtons\\BackSStudio.bmp", "cpanel\\Catalog\\BackButtons\\BackSSpa.bmp",
                null, null, null, "cpanel\\Catalog\\BackButtons\\BackDMisc.bmp" } },
            { UICatalogMode.Magictown, new string[] {
                "cpanel\\Catalog\\BackButtons\\BackDDining.bmp", "cpanel\\Catalog\\BackButtons\\BackDShops.bmp",
                "cpanel\\Catalog\\BackButtons\\BackMMagic.bmp", "cpanel\\Catalog\\BackButtons\\BackDOutdoor.bmp",
                null, null, null, "cpanel\\Catalog\\BackButtons\\BackDMisc.bmp" } },
        };

        // R146: subsort plaques in expansion sort state — the 8 FUNCTION sorts
        // (engine subsort tooltip base +8 everywhere). Community/Downtown/
        // Vacation carry their own sets; Studio/Magic reuse the DOWNTOWN art
        // (LoadBooks reads the +0x308/+0x328 copies of res 220..227).
        public static readonly System.Collections.Generic.Dictionary<UICatalogMode, string[]> ExpSubArt =
            new System.Collections.Generic.Dictionary<UICatalogMode, string[]>
        {
            { UICatalogMode.Downtown, ExpSubSet("Downtown") },
            { UICatalogMode.Community, ExpSubSet("Community") },
            { UICatalogMode.Vacation, ExpSubSet("Vacation") },
            { UICatalogMode.Studiotown, ExpSubSet("Downtown") },
            { UICatalogMode.Magictown, ExpSubSet("Downtown") },
        };

        private static string[] ExpSubSet(string family)
        {
            var slots = new string[] { "Seating", "Surfaces", "Decorative", "Electronics", "Appliances", "Plumbing", "Lighting", "Misc" };
            var res = new string[8];
            for (int i = 0; i < 8; i++)
                res[i] = "cpanel\\Catalog\\SubSortIcons\\" + family + "Subsort\\BuySubSort" + family + slots[i] + ".bmp";
            return res;
        }

        // R146: main-plaque tooltip base in STR# 150 (LoadBooks addi r0,i,base:
        // downtown 0x10, vacation 0x18, community 0x20, studio AND magic 0x28).
        // Magic starts from the Superstar block, then native LoadBooks replaces
        // slots 2/3 with MagiCo [44] and Outdoors [34] (R176).
        public static int ExpTipBase(UICatalogMode mode)
        {
            switch (mode)
            {
                case UICatalogMode.Downtown: return 16;
                case UICatalogMode.Vacation: return 24;
                case UICatalogMode.Community: return 32;
                default: return 40;   // studio + magic
            }
        }

        public static int ExpTipIndex(UICatalogMode mode, int slot)
        {
            if (mode == UICatalogMode.Magictown)
            {
                if (slot == 2) return 44; // MagiCo
                if (slot == 3) return 34; // Outdoors
            }
            return ExpTipBase(mode) + slot;
        }

        public UIOriginalBuyChrome()
        {
            for (int i = 0; i < 8; i++)
            {
                var p = new UIOriginalSheetButton(RoomMainArt[i]);
                var idx = i;
                // the SAME 8 buttons serve main and subsort states (engine
                // LoadBooks re-images the +0x3f8 array) — dispatch by state.
                // R149: a disabled plaque (empty enable-matrix cell) no-ops.
                p.OnButtonClick += (b) =>
                {
                    var sb = b as UIOriginalSheetButton;
                    if (sb != null && sb.Disabled) return;
                    if (SortState == 0) MainClick(idx); else SubClick(idx);
                };
                Add(p);
                Plaques[i] = p;
            }

            Toggle = new UIOriginalSheetButton(RoomBackArt[0])
            {
                Position = new Vector2(-2, 37),   // engine law: flush -2, (100-h)/2; Back art = 132x26 -> 37
            };
            Toggle.OnButtonClick += (b) => ToggleBack();
            Toggle.Visible = false;
            Add(Toggle);

            RelayoutMain();
        }

        public bool IsExpansion { get { return LotMode != UICatalogMode.Normal; } }

        public void SetLotMode(UICatalogMode mode)
        {
            LotMode = mode;
        }

        private void MainClick(int i)
        {
            CurrentMain = i;
            CurrentSub = 0;
            SortState = 1;
            OnMainSelect?.Invoke(Sort, i);
        }

        public void SubClick(int i)
        {
            CurrentSub = i;
            SortState = 1;
            OnSubSelect?.Invoke(i);
        }

        private void ToggleBack()
        {
            // engine: ShopSetSortState(0) — back to the main sorts; the item
            // grid keeps its contents (the catalog state persists).
            SortState = 0;
            RelayoutMain();
        }

        /// engine Objects-button toggle law: every buy activation flips
        /// room <-> function, resetting to the MAIN plaque state. Expansion
        /// sorts (2+) are KEPT — ToggleSort is never called for them.
        public void ToggleSort()
        {
            Sort = 1 - Sort;
            SortState = 0;
            RelayoutMain();
        }

        public void RelayoutMain()
        {
            if (IsExpansion)
            {
                // R146: 5 plaques (mains 0-3 + Misc at 7), 4-6 HIDDEN
                // (LoadBooks 0x269cd8: sortState 0 && i in {4,5,6} -> Hide).
                var art = ExpMainArt[LotMode];
                var ax = LotMode == UICatalogMode.Studiotown ? ExpMainXStudio : ExpMainX;
                var ay = ExpMainY;
                for (int i = 0; i < 8; i++)
                {
                    if (art[i] == null) Plaques[i].Visible = false;
                    else
                    {
                        Plaques[i].Remount(art[i]);
                        Plaques[i].Visible = true;
                        Plaques[i].Position = new Vector2(ax[i], ay[i]);
                        // In the main/back state UpdateView clears every plaque
                        // to frame 0. Selection frame 1 belongs only to the
                        // function-subs row after a main category is clicked.
                        Plaques[i].State = 0;
                        Plaques[i].Disabled = false;   // R149: mains never gray
                        Plaques[i].Tooltip = GameFacade.Strings.GetString("150", ExpTipIndex(LotMode, i).ToString());
                    }
                }
                Toggle.Visible = false;
                return;
            }
            var artN = Sort == 0 ? RoomMainArt : FuncMainArt;
            var axN = Sort == 0 ? RoomMainX : FuncMainX;
            var ayN = Sort == 0 ? RoomMainY : FuncMainY;
            for (int i = 0; i < 8; i++)
            {
                // engine LoadBooks re-images AND re-shows the whole +0x3f8
                // array on every sort change (a subsort state may have hidden
                // the tail slots) — restore visibility here. R149: main
                // plaques never gray (enable masks are subsort-state only).
                Plaques[i].Remount(artN[i]);
                Plaques[i].Visible = true;
                Plaques[i].Position = new Vector2(axN[i], ayN[i]);
                Plaques[i].State = 0;
                Plaques[i].Disabled = false;
                Plaques[i].Tooltip = GameFacade.Strings.GetString("150", (i + (Sort == 0 ? 0 : 8)).ToString());
            }
            Toggle.Visible = false;
        }

        /// switch the plaque row to the SUBSORT set for the current main
        /// (room mode = the 8 function subsorts; function mode = the family's
        /// own subsorts). The toggle plaque takes the Back art for (sort, main).
        public void RelayoutSub(List<UICatalogSubcat> cats)
        {
            SubCats = cats;
            SortState = 1;
            // R149: the engine LoadBooks enable-mask law — in subsort state
            // each plaque reads its cell from the matrix row of the CURRENT
            // main (Init 0x26bd3c matrices; LoadBooks +0x160/vt+0x170). A 0
            // cell = the ghosted frame-3 plaque, click no-op.
            var matrix = IsExpansion ? ExpMatrix() : (Sort == 0
                ? UIOriginalEnableMatrices.RoomFunc : UIOriginalEnableMatrices.FunctionSub);
            for (int i = 0; i < 8; i++)
            {
                bool enabled = true;
                if (IsExpansion) enabled = UIOriginalEnableMatrices.IsEnabled(matrix, CurrentMain == 7 ? 7 : CurrentMain & 3, i);
                else if (Sort == 0) enabled = UIOriginalEnableMatrices.IsEnabled(matrix, CurrentMain & 7, i);
                // function mode: the plaque column is the ENGINE subsort index
                // (OBJD FunctionSubsort value), translated from the cat MaskBit.
                else
                {
                    var mb = (cats != null && i < cats.Count) ? cats[i].MaskBit : -1;
                    var eng = UIOriginalEnableMatrices.EngineSubFromMaskBit(mb);
                    enabled = eng >= 0 && UIOriginalEnableMatrices.IsEnabled(matrix, CurrentMain & 7, eng);
                }
                if (cats != null && i < cats.Count)
                {
                    var c = cats[i];
                    var member = c.OriginalName;
                    if (member == null && Sort == 0) member = UIBuyBrowsePanel.RoomSubSortArt[i];
                    if (member != null)
                    {
                        Plaques[i].Remount(member);
                        Plaques[i].Visible = true;
                        Plaques[i].Tooltip = GameFacade.Strings.GetString(c.StrTable.ToString(), c.StrInd.ToString());
                    }
                    else Plaques[i].Visible = false;
                }
                else Plaques[i].Visible = false;
                Plaques[i].Position = new Vector2(SubX[i], SubY[i]);
                Plaques[i].State = (byte)((i == CurrentSub) ? 1 : 0);
                Plaques[i].Disabled = !enabled;
            }
            Toggle.Remount(IsExpansion
                ? ExpBackArt[LotMode][CurrentMain == 7 ? 7 : CurrentMain & 3]
                : (Sort == 0 ? RoomBackArt : FuncBackArt)[CurrentMain & 7]);
            // R148 engine law (LoadBooks 0x26a0c0-0x26a10c): SetArea(-2,
            // (100 - btnH)/2, btnW - 2, ...) — re-derive from the mounted art
            // (all Back sheets are 132x26 -> y 37).
            Toggle.Position = new Vector2(-2, (100f - Toggle.Size.Y) / 2f);
            Toggle.Visible = true;
        }

        /// the expansion enable matrix for the chrome's lot mode (R149).
        private byte[] ExpMatrix()
        {
            switch (LotMode)
            {
                case UICatalogMode.Downtown: return UIOriginalEnableMatrices.Downtown;
                case UICatalogMode.Vacation: return UIOriginalEnableMatrices.Vacation;
                case UICatalogMode.Studiotown: return UIOriginalEnableMatrices.Studio;
                case UICatalogMode.Magictown: return UIOriginalEnableMatrices.Magic;
                default: return UIOriginalEnableMatrices.Community;
            }
        }

        /// the engine's first-entry composition: ROOM main, Living selected —
        /// with the default subsort 0 already applied to the grid. Expansion
        /// lots enter the MAIN state of their own sort, main 0 (R146).
        public void ApplyEngineDefault()
        {
            Sort = 0;
            SortState = 0;
            CurrentMain = 0;
            CurrentSub = 0;
            RelayoutMain();
        }

        public void SetSubSelected(int i)
        {
            CurrentSub = i;
            for (int k = 0; k < 8; k++) Plaques[k].State = (byte)((SortState == 1 && k == i) ? 1 : 0);
        }
    }
}
