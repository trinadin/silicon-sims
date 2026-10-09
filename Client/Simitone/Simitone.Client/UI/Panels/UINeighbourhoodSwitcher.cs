using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Common;
using FSO.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simitone.Client.UI.Controls;
using Simitone.Client.UI.Model;
using System;
using System.Collections.Generic;

namespace Simitone.Client.UI.Panels
{
    // R160: the neighborhood NAVBAR, ported from the ORIGINAL PPC engine (The Sims
    // Complete) onto the R143-decoded cWinNeighborhoodVC/UL::Init law
    // (tools/iff-dump/r143/nbhd-layout-law.md §3.1-§3.3, disasm evidence
    // nbhd-disasm-winneighborhoodvc-init.txt / nbhd-disasm-nbhd-anchorwriter.txt):
    //
    //   - the top strip is the kNghBarBkg 5420 BANNER (NghUI/banner_neighborhood.bmp,
    //     800x52) mounted as a clickable cTSWinBtn at (offX, offY)-(offX+800, offY+52)
    //     (@0x474318-0x474384); Downtown screens use kDTBarBkg 5422.
    //   - the 14 TOOLBAR buttons come from the static resource table at img 0x6ce40
    //     and mount in the loop @0x474488-0x4748e4: SetImage(art, cols=4, rows=1)
    //     (SetImage(1,1) for the logo, i==9), SetArea at the banner-local BSS anchors,
    //     then banner->AddChild(button) @0x474614-0x474624. Main-row x-grid
    //     200..600 at exact 50px pitch; Previous/Next at 106/141; logo at 5;
    //     inet at 762; all y=0. The whole strip therefore follows offX/offY.
    //   - the credits button (entry 4) uses the language-switch anchor (41,543)
    //     for En/Fr/De/Sp (@0x474240-0x474314), then adds the banner's offX/offY
    //     at @0x474874-0x4748b0 — English art is CreditBtnEn 5315 (232x58, 4x1).
    //   - the "current neighborhood" indicator kNghCurrent 5206 (23x30) at
    //     x=124 (TOC word 0x7c), y=3 (@0x4740a4-0x4740f4).
    //   - tooltips are STR# 151 'NghbBtnTips' (English block 0..13; STR# 225 is the
    //     expansion-missing FAILURE set, unused with Complete data). The destination
    //     screens carry their own tables: 169 Downtown / 170 Vacation / 173 Studio
    //     Town / 174 Magicland, entry 0 = 'Return to Neighborhood View'.
    //
// Port wiring (disclosed where the port lacks the system): MoveIn = the port's
// CAS entry ('Select or Create Family'); Exit raises the same save/quit
// CloseAttempt dialog as the R121 options Quit; destination buttons PopMode;
// UI-30 ports the decoded laws' last dead buttons: Credits mounts the
// cWinCredits overlay (navbar button + banner), Bulldoze arms the evict/
// bulldoze mode (lot clicks follow the EvictModeLotHandler branch law on
// TS1GameScreen); Import is UI-21's staged-FAM flow; Previous/Next cycle the
// available neighborhoods through NBR-02's switch backend (NBR-05); Rezone
// arms the rezone mode (NBR-05 — the STR# 151 [10] 'Evict or Rezone' tool,
// armed lot clicks follow the STR# 131 rezone cascade on TS1GameScreen);
// Inet remains a mounted no-op (UI-29 disposition; explicit exclusion — the
// TSO-era web-pages viewer has no port system).
    // Destination modes mount the home grid minus home-only buttons plus a Return
    // button in the MoveIn slot — the engine's own DT/Vacation/Studio/Magic
    // toolbars (Return/Exchange/Credits/Import/Bulldoze per STR# 169/170/173/174,
    // per-screen Return.bmp/Import.bmp/Bulldoze.bmp art) are expansion systems
    // deferred with their screens; the Return glyph/tooltip are engine art/string.
    public class UINeighbourhoodSwitcher : UIContainer
    {
        public sealed class EngineNavSlot
        {
            public string DebugName;
            public string Member;       // UIGraphics.far sheet
            public int Idx;             // toolbar loop index (r143 §3.2 order)
            public int X, Y;            // BSS anchor, banner-local coords
            public int Cols, Rows;      // SetImage(art, cols, rows)
            public int Tip;             // STR# 151 index, -1 = none
            public ushort DestMode;     // port: PopMode target, 0 = not a destination
            public bool HomeOnly;       // mounted on the base-neighborhood screens only
        }

        // The r143 engine law, toolbar order (resource table img 0x6ce40 order).
        public static readonly EngineNavSlot[] EngineLaw = new EngineNavSlot[]
        {
            new EngineNavSlot { DebugName = "MoveIn",   Member = "NghUI\\MoveIn.bmp",       Idx = 0,  X = 200, Y = 0,   Cols = 4, Rows = 1, Tip = 0,  HomeOnly = true },
            new EngineNavSlot { DebugName = "Bulldoze", Member = "NghUI\\Bulldoze.bmp",     Idx = 1,  X = 300, Y = 0,   Cols = 4, Rows = 1, Tip = 1 },
            new EngineNavSlot { DebugName = "Exit",     Member = "NghUI\\Exit.bmp",         Idx = 2,  X = 600, Y = 0,   Cols = 4, Rows = 1, Tip = 2 },
            new EngineNavSlot { DebugName = "Inet",     Member = "NghUI\\InetBtn.bmp",      Idx = 3,  X = 762, Y = 0,   Cols = 4, Rows = 1, Tip = 3 },
            new EngineNavSlot { DebugName = "Credits",  Member = "Nbhd\\CreditBtnEn.bmp",   Idx = 4,  X = 41,  Y = 543, Cols = 4, Rows = 1, Tip = 8,  HomeOnly = true },
            new EngineNavSlot { DebugName = "Previous", Member = "NghUI\\Previous.bmp",     Idx = 5,  X = 106, Y = 0,   Cols = 4, Rows = 1, Tip = 4,  HomeOnly = true },
            new EngineNavSlot { DebugName = "Next",     Member = "NghUI\\Next.bmp",         Idx = 6,  X = 141, Y = 0,   Cols = 4, Rows = 1, Tip = 5,  HomeOnly = true },
            new EngineNavSlot { DebugName = "Import",   Member = "NghUI\\Import.bmp",       Idx = 7,  X = 250, Y = 0,   Cols = 4, Rows = 1, Tip = 6 },
            new EngineNavSlot { DebugName = "Downtown", Member = "NghUI\\Downtown.bmp",     Idx = 8,  X = 400, Y = 0,   Cols = 4, Rows = 1, Tip = 7,  DestMode = 2 },
            new EngineNavSlot { DebugName = "Logo",     Member = "NghUI\\TheSimsLogo.bmp",  Idx = 9,  X = 5,   Y = 0,   Cols = 1, Rows = 1, Tip = -1 },
            new EngineNavSlot { DebugName = "Vacation", Member = "NghUI\\Vacation.bmp",     Idx = 10, X = 450, Y = 0,   Cols = 4, Rows = 1, Tip = 9,  DestMode = 3 },
            new EngineNavSlot { DebugName = "Rezone",   Member = "NghUI\\Rezone.bmp",       Idx = 11, X = 350, Y = 0,   Cols = 4, Rows = 1, Tip = 10, HomeOnly = true },
            new EngineNavSlot { DebugName = "Studio",   Member = "NghUI\\Studiotown.bmp",   Idx = 12, X = 500, Y = 0,   Cols = 4, Rows = 1, Tip = 11, DestMode = 5 },
            new EngineNavSlot { DebugName = "Magic",    Member = "NghUI\\MagicLand.bmp",    Idx = 13, X = 550, Y = 0,   Cols = 4, Rows = 1, Tip = 12, DestMode = 7 },
        };

        // Destination-mode strip additions: the Return button (engine art/tooltip
        // STR# 169/170/173/174 [0]) replaces MoveIn at the (200,0) slot.
        private static readonly Dictionary<ushort, string> ReturnMember = new Dictionary<ushort, string>
        {
            { 2, "Downtown\\Return.bmp" },
            { 3, "NghUI\\Return.bmp" },        // no VIsland Return exists in the FAR (disclosed)
            { 5, "Studiotown\\Return.bmp" },
            { 7, "Magicland\\Return.bmp" },
        };
        private static readonly Dictionary<ushort, string> DestTipTable = new Dictionary<ushort, string>
        {
            { 2, "169" }, { 3, "170" }, { 5, "173" }, { 7, "174" },
        };
        private static readonly Dictionary<ushort, string> DestImportMember = new Dictionary<ushort, string>
        {
            { 2, "Downtown\\Import.bmp" }, { 3, "VIsland\\Import.bmp" },
            { 5, "Studiotown\\Import.bmp" }, { 7, "Magicland\\Import.bmp" },
        };
        private static readonly Dictionary<ushort, string> DestBulldozeMember = new Dictionary<ushort, string>
        {
            { 2, "Downtown\\Bulldoze.bmp" }, { 3, "NghUI\\Bulldoze.bmp" },  // no VIsland Bulldoze in the FAR (disclosed)
            { 5, "Studiotown\\Bulldoze.bmp" }, { 7, "Magicland\\Bulldoze.bmp" },
        };

        // R160 gate evidence: every mounted strip registers its law snapshot.
        public static int StripsMounted, ButtonsMounted, BannersMounted;
        public string LastBannerMember, LastTitleText;
        // EXP-08 leg-2 (the approved DT toolbars): filter bar + payphone probes
        public readonly List<UIOriginalNavbarButton> FilterButtons = new List<UIOriginalNavbarButton>();
        public bool FilterBarMounted, PayphoneMounted;
        public string LastFilterMember;
        public Vector2 PayphonePositionForProbe;
        // review P2-2: the dtphone art dims pin (70x135, the leg-2 manifest law)
        public Microsoft.Xna.Framework.Graphics.Texture2D PayphoneTextureForProbe => _payphoneButton?.Texture;
        private UIOriginalNavbarButton _payphoneButton;
        public readonly List<string> LastMembers = new List<string>();

        // UI-38 — the UL community filter toolbar (cWinNeighborhoodUL, PPC
        // The Sims Complete; receipt evidence/UI-38/ul-filter-toolbar-law.md).
        // The decoded engine law, in BuildFilterToolbar's button order
        // (this+0xd4+i*4; thunks 0x459ba4/0x459c54/0x459d00/0x459dac/
        // 0x459e58/0x459f04/0x459fb0; art ids byte-proven against
        // Res_Nbhd.h in UIGraphics.far; ladder x from the static-init writer
        // 0x465460, strip-local y=20; labels = STR# 171 english[0..6]).
        public sealed class ULFilterSlot
        {
            public string DebugName;   // category
            public int CmdBit;         // ProcessFilterToolbarByType input
            public int ArtId;          // Res_Nbhd.h id (kFilter*Button)
            public string Member;      // UIGraphics.far sheet (200x52, 4x1)
            public int X, Y;           // ladder pair (strip-local)
            public int Mode;           // ProcessFilterToolbarByType id->mode
            public int LabelIndex;     // STR# 171 english block (0-based)
            public string PlaqueMember; // SetFilterMode 4930..4936 art
        }
        public static readonly ULFilterSlot[] ULFilterLaw = new ULFilterSlot[]
        {
            new ULFilterSlot { DebugName = "DogCat",       CmdBit = 0x10, ArtId = 5064, Member = "NghUI\\FilterDogCatBtn.bmp",      X = 225, Y = 20, Mode = 0, LabelIndex = 0, PlaqueMember = "cpanel\\filterdogcat.bmp" },
            new ULFilterSlot { DebugName = "SmallAnimal",  CmdBit = 0x20, ArtId = 5065, Member = "NghUI\\FilterSmallAnimalBtn.bmp", X = 275, Y = 20, Mode = 1, LabelIndex = 1, PlaqueMember = "cpanel\\filtersmallanimal.bmp" },
            new ULFilterSlot { DebugName = "Shopping",     CmdBit = 0x08, ArtId = 5063, Member = "NghUI\\FilterShoppingBtn.bmp",    X = 325, Y = 20, Mode = 2, LabelIndex = 2, PlaqueMember = "cpanel\\filtershopping.bmp" },
            new ULFilterSlot { DebugName = "Gardening",    CmdBit = 0x04, ArtId = 5062, Member = "NghUI\\FilterGardeningBtn.bmp",   X = 375, Y = 20, Mode = 3, LabelIndex = 3, PlaqueMember = "cpanel\\filtergardening.bmp" },
            new ULFilterSlot { DebugName = "Food",         CmdBit = 0x02, ArtId = 5061, Member = "NghUI\\FilterFoodBtn.bmp",        X = 425, Y = 20, Mode = 4, LabelIndex = 4, PlaqueMember = "cpanel\\filterfood.bmp" },
            new ULFilterSlot { DebugName = "Recreation",   CmdBit = 0x40, ArtId = 5066, Member = "NghUI\\FilterRecreationBtn.bmp",  X = 475, Y = 20, Mode = 5, LabelIndex = 5, PlaqueMember = "cpanel\\filterpark.bmp" },
            new ULFilterSlot { DebugName = "Lodging",      CmdBit = 0x01, ArtId = 5060, Member = "NghUI\\FilterLodgingBtn.bmp",     X = 525, Y = 20, Mode = 6, LabelIndex = 6, PlaqueMember = "cpanel\\filterlodging.bmp" },
        };
        // probe surfaces
        public readonly List<UIOriginalNavbarButton> ULFilterButtons = new List<UIOriginalNavbarButton>();
        public UIOriginalNavbarButton ULFilterStrip;
        public bool ULFilterStripMounted, ULFilterButtonsMounted;
        public int ULFilterCmdId = -1;        // native this+0x188 (the active bit)
        public int ULFilterEngineMode = -1;   // native this+0xcc filter mode
        public string ULFilterPlaqueMember;   // native this+0x184 art
        public int ULFilterClicksForProbe;

        /// <summary>ProcessFilterToolbarByType @0x10458c60 + ProcessFilterToolbar
        /// @0x104598f0 + SetFilterMode @0x1045a1c0 + HighlightLotsForMode
        /// @0x1045a9c0, ported law-exact: clear the PREVIOUS bit's button,
        /// press the new one, set the plaque art, apply the lot hilites.</summary>
        public void ProcessULFilterByType(int cmdBit)
        {
            var slot = ULFilterCmdId >= 0
                ? System.Linq.Enumerable.FirstOrDefault(ULFilterLaw, s => s.CmdBit == ULFilterCmdId)
                : null;
            var prevBtn = slot != null && ULFilterButtons.Count == ULFilterLaw.Length
                ? ULFilterButtons[System.Array.FindIndex(ULFilterLaw, s => s.CmdBit == ULFilterCmdId)]
                : null;
            if (prevBtn != null) prevBtn.Selected = false;    // SetState(prev, 0)
            var next = System.Linq.Enumerable.FirstOrDefault(ULFilterLaw, s => s.CmdBit == cmdBit);
            if (next == null) return;
            int idx = System.Array.IndexOf(ULFilterLaw, next);
            if (ULFilterButtons.Count == ULFilterLaw.Length) ULFilterButtons[idx].Selected = true;
            ULFilterCmdId = cmdBit;                           // this+0x188 = id
            ULFilterEngineMode = next.Mode;                   // via ProcessFilterToolbarByType
            ULFilterPlaqueMember = next.PlaqueMember;         // SetFilterMode 0x1342+idx
            ULFilterClicksForProbe++;
            Panel?.ApplyULFilterMode(cmdBit, next.Mode, next.PlaqueMember);
            // UI-38b LAW 2: every filter change persists (the native writes the
            // cmd-bit back to STR# 6 slot 3 — the LoadCurrentFilter pair).
            try
            {
                var nbhd = FSO.Content.Content.Get().Neighborhood;
                if (nbhd?.LotLocations != null)
                    ULFilterLaws.SavePersistedFilterCmd(
                        System.IO.Path.Combine(nbhd.UserPath, "LotLocations.iff"), cmdBit, nbhd.LotLocations);
            }
            catch { }
            GameLog.Write("uidtbar: filter " + next.DebugName + " (cmd 0x" + cmdBit.ToString("x")
                + " mode " + next.Mode + " plaque " + next.PlaqueMember + ")");
        }

        /// <summary>STR# 171 'Filter Bar' english block with the R159-style
        /// literal fallback (the seven UL labels; UIText.iff).</summary>
        private static string ULTip171(int idx)
        {
            var names = new string[]
            {
                "Show Dog & Cat Adoption Centers", "Show Small Pet Shops",
                "Show Shopping Locations", "Show Gardening Shops",
                "Show Food Service Areas", "Show Recreational Areas",
                "Show Lodging Areas",
            };
            var t = GameFacade.Strings.GetString("171", idx.ToString());
            if (string.IsNullOrEmpty(t) || t.Contains("MISSING")) return names[idx];
            return t;
        }

        public UINeighborhoodSelectionPanel Panel;
        private ushort Mode;
        public bool MoveInMode;

        // UI-30 (bulldoze-law §1): the Bulldoze button ARMS a modal evict/bulldoze
        // mode; any other toolbar click disarms it before its own action (the
        // native EvictModeBtnHandler jump-table first calls 0x46fd70(this,0)). While
        // armed, lot clicks route to the EvictModeLotHandler branch law instead of
        // the normal lot flow — TS1GameScreen installs ArmedLotClick (it owns the
        // dialogs and the NBR-02/NBR-03 backends).
        public bool BulldozeArmed { get; private set; }
        public event Action<bool> BulldozeArmChanged;
        // The native re-click semantics were not separately decoded (disclosed):
        // the port toggles — a second Bulldoze click disarms.
        public UIOriginalNavbarButton BulldozeButtonForProbe;
        public UIOriginalNavbarButton CreditsButtonForProbe;
        public int BulldozeArmTogglesForProbe;

        public void SetBulldozeArmed(bool armed)
        {
            if (BulldozeArmed == armed) return;
            BulldozeArmed = armed;
            BulldozeArmTogglesForProbe++;
            if (BulldozeButtonForProbe != null) BulldozeButtonForProbe.Selected = armed;
            if (armed) SetRezoneArmed(false);
            BulldozeArmChanged?.Invoke(armed);
            GameLog.Write("uinav: bulldoze mode " + (armed ? "ARMED" : "disarmed"));
        }

        // NBR-05: the Rezone button arms the rezone twin of the evict/bulldoze
        // mode (STR# 151 [10] 'Evict or Rezone' vs Bulldoze's [1] 'Evict or
        // Bulldoze' — the two armed tools of the same EvictModeBtnHandler law).
        // The two modes are mutually exclusive: arming one disarms the other;
        // every non-tool toolbar click disarms both (the pre-subscribed hook).
        public bool RezoneArmed { get; private set; }
        public UIOriginalNavbarButton RezoneButtonForProbe;
        public int RezoneArmTogglesForProbe;

        public void SetRezoneArmed(bool armed)
        {
            if (RezoneArmed == armed) return;
            RezoneArmed = armed;
            RezoneArmTogglesForProbe++;
            if (RezoneButtonForProbe != null) RezoneButtonForProbe.Selected = armed;
            if (armed) SetBulldozeArmed(false);
            GameLog.Write("uinav: rezone mode " + (armed ? "ARMED" : "disarmed"));
        }

        // UI-21 probe seams: the mounted Import button (home + destination
        // strips — the last mount wins) and how many times its production
        // handler dispatched. The 'importui' battery presses the REAL button
        // through these and asserts the dialog opens.
        public UIOriginalNavbarButton ImportButtonForProbe;
        public int ImportClicksForProbe;

        private UIOriginalNavbarButton Banner;
        private UIOriginalText CurrentNumber;
        private Vector2 CurrentNumberInset;
        private readonly Dictionary<UIElement, Vector2> BaseAnchors = new Dictionary<UIElement, Vector2>();
        private readonly HashSet<UIElement> ArtboardAnchors = new HashSet<UIElement>();
        public readonly List<UIOriginalNavbarButton> Buttons = new List<UIOriginalNavbarButton>();

        internal Vector2 BannerPositionForProbe { get { return Banner?.Position ?? Vector2.Zero; } }
        internal UIOriginalNavbarButton BannerForProbe { get { return Banner; } }
        internal Vector2 CurrentPositionForProbe
        {
            get { return CurrentNumber == null ? Vector2.Zero : CurrentNumber.Position - CurrentNumberInset; }
        }
        internal Vector2 CurrentNumberPositionForProbe { get { return CurrentNumber?.Position ?? Vector2.Zero; } }
        internal string CurrentNumberTextForProbe { get { return CurrentNumber?.Text; } }
        internal bool CurrentNumberDrawsLastForProbe
        {
            get { return CurrentNumber != null && Children.Count > 0
                && object.ReferenceEquals(Children[Children.Count - 1], CurrentNumber); }
        }

        internal static Vector2 OriginalArtboardOffset(int width, int height)
        {
            return new Vector2(
                Math.Max(0, width - 800) / 2,
                Math.Max(0, height - 600) / 2);
        }

        public UINeighbourhoodSwitcher(UINeighborhoodSelectionPanel panel, ushort mode, bool moveIn)
        {
            Panel = panel;
            SetMode(mode, moveIn);
            UpdatePosition();
        }

        public bool IsHomeMode
        {
            get { return Mode == 1 || Mode == 4; }
        }

        // cWinNeighborhoodVC mounts the top controls as children of the centered
        // banner. Keep this container at window origin on desktop and apply the
        // original offX/offY once to every artboard control.
        public void UpdatePosition()
        {
            var screen = UIScreen.Current;
            int width = screen?.ScreenWidth ?? GlobalSettings.Default.GraphicsWidth;
            int height = screen?.ScreenHeight ?? GlobalSettings.Default.GraphicsHeight;
            if (FSOEnvironment.SoftwareKeyboard)
            {
                // Preserve the port's touch composition: one centered, scaled
                // artboard with every child in its 800x600 local coordinates.
                ScaleX = ScaleY = height / 600.0f;
                X = width / 2 - 400 * ScaleX;
                Y = height / 2 - 300 * ScaleY;
            }
            else
            {
                // Desktop anchors are expressed in the original 800x600 artboard.
                ScaleX = ScaleY = 1;
                X = Y = 0;
            }
            ApplyAnchors();
        }

        public override void GameResized()
        {
            UpdatePosition();
        }

        public void SetMode(ushort mode, bool moveIn)
        {
            MoveInMode = moveIn;
            Mode = mode;
            foreach (var child in new List<UIElement>(Children)) Remove(child);
            Buttons.Clear();
            FilterButtons.Clear();
            FilterBarMounted = PayphoneMounted = false;
            LastFilterMember = null;
            LastMembers.Clear();
            // UI-38: fresh UL filter state per strip (native Init rebuild).
            ULFilterButtons.Clear();
            ULFilterStrip = null;
            ULFilterStripMounted = ULFilterButtonsMounted = false;
            ULFilterCmdId = ULFilterEngineMode = -1;
            ULFilterPlaqueMember = null;
            BaseAnchors.Clear();
            ArtboardAnchors.Clear();
            CurrentNumber = null;
            CurrentNumberInset = Vector2.Zero;
            // A fresh strip mounts disarmed (the mode is a property of the armed
            // session, not of the strip).
            BulldozeArmed = false;
            BulldozeButtonForProbe = null;
            RezoneArmed = false;
            RezoneButtonForProbe = null;
            CreditsButtonForProbe = null;

            // Banner: kNghBarBkg (5420) on the neighborhood screens, kDTBarBkg
            // (5422) on the DESTINATION screens — the EXP-08 leg-5 PPC law:
            // cWinVacation::Init (0x4420e4) and cWinStudiotown::Init
            // (0x47b024) load 5422 natively (V/ST carry the Downtown
            // banner); cWinNeighborhoodUL/VC load 5420. Magic (mode 7) keeps
            // banner_neighborhood pending a cMagicland load-site scan (none
            // in the 5420/5422 sweep — leg 5's card note).
            var bannerMember = (mode == 2 || mode == 3 || mode == 5)
                ? "NghUI\\banner_downtown.bmp" : "NghUI\\banner_neighborhood.bmp";
            Banner = new UIOriginalNavbarButton(bannerMember, 1, 1, null);
            RegisterAnchor(Banner, Vector2.Zero, true);
            Banner.OnButtonClick += (btn) =>
            {
                // UI-30: the full strip is clickable and opens the credits/picker
                // (vt+0x98(0x400,0), RegularModeBtnHandler credits case) — the port
                // mounts the cWinCredits overlay; the armed tool modes disarm
                // first (the banner rides the same button law).
                SetBulldozeArmed(false);
                SetRezoneArmed(false);
                var gs = UIScreen.Current as Screens.TS1GameScreen;
                if (gs != null) gs.ShowCreditsScreen();
                else GameLog.Write("uinav: banner click (no TS1GameScreen)");
            };
            Add(Banner);
            LastBannerMember = bannerMember;
            BannersMounted++;

            // Current-neighborhood number: home screens only. The engine loads
            // kNghCurrent 5206 solely to obtain its 23x30 dimensions, builds a rect
            // at (124,3), then PostChildDraw paints the decimal number centered in
            // that rect. It never blits the blue Current.bmp pixels themselves.
            // kMarquee 5016 is likewise a dead Complete-engine slot.
            if (IsHomeMode)
            {
                var indTex = UIOriginal.EnsureResolved("NghUI\\Current.bmp")?.Get(GameFacade.GraphicsDevice);
                var font = OriginalGlyphFont.LoadByIndex(12, GameFacade.GraphicsDevice);
                if (indTex != null && font != null)
                {
                    const string number = "1"; // this port mounts the owned Neighborhood-1 save
                    int textWidth = font.Measure(number);
                    int textHeight = font.LineHeight;
                    var rectAnchor = new Vector2(124, 3);
                    var textAnchor = new Vector2(
                        124 + indTex.Width / 2 - 1 - textWidth / 2,
                        // The shared font now normalizes glyph destinations.
                        3 + indTex.Height / 2 - 1 - textHeight / 2);
                    CurrentNumberInset = textAnchor - rectAnchor;
                    CurrentNumber = new UIOriginalText(number, font) { Color = Color.White };
                    RegisterAnchor(CurrentNumber, textAnchor, true);
                    LastMembers.Add("NghUI\\Current.bmp");
                }
                LastTitleText = null;
            }
            else
            {
                CurrentNumber = null;
                CurrentNumberInset = Vector2.Zero;
                LastTitleText = null;
            }

            // The 14-slot engine law.
            bool home = IsHomeMode;
            string tipTable; DestTipTable.TryGetValue(mode, out tipTable);
            foreach (var slot in EngineLaw)
            {
                if (slot.HomeOnly && !home) continue;
                if (slot.DestMode != 0 && (slot.DestMode == mode || moveIn)) continue;
                if (slot.DebugName == "MoveIn" && moveIn) continue;

                var member = slot.Member;
                var tip = Tip151(slot.Tip);
                if (slot.DebugName == "Import" && !home)
                {
                    string m; if (DestImportMember.TryGetValue(mode, out m)) { member = m; tip = DestTip(tipTable, 3, "Import Family"); }
                }
                else if (slot.DebugName == "Bulldoze" && !home)
                {
                    string m; if (DestBulldozeMember.TryGetValue(mode, out m)) { member = m; tip = DestTip(tipTable, 4, "Bulldoze Lot"); }
                }
                else if (slot.DebugName == "Inet" && !home)
                {
                    tip = DestTip(tipTable, 1, "View Web Pages");
                }
                else if (slot.DebugName == "Logo" && !home)
                {
                    tip = DestTip(tipTable, 2, "Credits");
                }

                var btn = new UIOriginalNavbarButton(member, slot.Cols, slot.Rows, tip);
                // The language switch overwrites index 4's default with the
                // localized bottom-artboard anchor before the construction loop.
                // All entries ultimately share the banner/artboard origin.
                RegisterAnchor(btn, new Vector2(slot.X, slot.Y), true);
                if (slot.Cols == 1) btn.ForceState = 0;   // logo: SetImage(1,1), one state
                // UI-30 EvictModeBtnHandler law: a tool click arms its mode (and
                // disarms the other tool — NBR-05 mutual exclusion); EVERY other
                // toolbar click disarms both first. Subscribed BEFORE WireClick,
                // so the disarm lands before the button's own action.
                if (slot.DebugName == "Bulldoze")
                {
                    BulldozeButtonForProbe = btn;
                    btn.OnButtonClick += (b) => SetBulldozeArmed(!BulldozeArmed);
                }
                else if (slot.DebugName == "Rezone")
                {
                    RezoneButtonForProbe = btn;
                    btn.OnButtonClick += (b) => SetRezoneArmed(!RezoneArmed);
                }
                else
                {
                    btn.OnButtonClick += (b) =>
                    {
                        SetBulldozeArmed(false);
                        SetRezoneArmed(false);
                    };
                }
                if (slot.DebugName == "Credits") CreditsButtonForProbe = btn;
                WireClick(btn, slot, mode, tipTable);
                Add(btn);
                Buttons.Add(btn);
                LastMembers.Add(member);
            }

            // Return button in the MoveIn slot on destination screens.
            if (!home)
            {
                string retMember;
                if (ReturnMember.TryGetValue(mode, out retMember))
                {
                    var ret = new UIOriginalNavbarButton(retMember, 4, 1, DestTip(tipTable, 0, "Return to Neighborhood View"));
                    RegisterAnchor(ret, new Vector2(200, 0), true);
                    ret.OnButtonClick += (btn) =>
                    {
                        SetBulldozeArmed(false);   // UI-30: another toolbar click disarms first
                        SetRezoneArmed(false);     // NBR-05: both armed tools disarm
                        PopMode(4);
                    };
                    Add(ret);
                    Buttons.Add(ret);
                    LastMembers.Add(retMember);
                }
            }

            // EXP-08 leg-2 fix card (the approved DT toolbars): the 5-button
            // FILTER bar + the PAYPHONE on the destination screens. Native law
            // (cWinDowntown::Init 0x3ffd78-0x3ffee8 + the static-init writer at
            // 0x400420): five anchor slots with xs [323, 30000=SENTINEL,
            // 5=spacer, 422, 373] ys 0 — the three VISIBLE anchors are 323/422/
            // 373; i==2 is the single-state cell (SetImage(art,1,1)); tooltips
            // via the 0x87f60/0x25f440 mechanism into set 169. Per-screen sheet
            // heights: DT 62, ST/UL 72. The payphone (kDTPhone 5426,
            // dtphone.bmp 70x135) is the DT leave-town control (leg 2's decode).
            if (mode == 2 || mode == 3 || mode == 5 || mode == 7)
            {
                var filterMember = (mode == 2) ? "NghUI\\filter_toolbar.bmp"
                    : (mode == 5) ? "NghUI\\filter_toolbar_Studiotown.bmp"
                    : "NghUI\\filter_toolbar_Unleashed.bmp";
                // visible slots: [0]=323, [3]=422, [4]=373 (slots 1/2 are the
                // sentinel/spacer; the subf local-centering rides fixed cells)
                var filterAnchors = new int[] { 323, 422, 373 };
                for (int fi = 0; fi < 3; fi++)
                {
                    var fbtn = new UIOriginalNavbarButton(filterMember, 4, 1, null)
                    { ForceState = 0 };
                    RegisterAnchor(fbtn, new Vector2(filterAnchors[fi], 62), true);
                    Add(fbtn);
                    FilterButtons.Add(fbtn);
                }
                FilterBarMounted = FilterButtons.Count == 3
                    && FilterButtons.TrueForAll(b => b.OriginalMounted);
                LastFilterMember = filterMember;

                // the payphone: the DT leave-town control (70x135 art)
                if (mode == 2)
                {
                    var phone = new UIOriginalNavbarButton("Downtown\\dtphone.bmp", 1, 1,
                        GameFacade.Strings.GetString("169", "0"))
                    { ForceState = 0 };
                    RegisterAnchor(phone, new Vector2(762 - 70, 52), true);
                    phone.OnButtonClick += (btn) =>
                    {
                        SetBulldozeArmed(false);
                        SetRezoneArmed(false);
                        PopMode(4);
                    };
                    Add(phone);
                    PayphoneMounted = phone.OriginalMounted;
                    _payphoneButton = phone;
                }
            }

            // UI-38 — the UL community FILTER TOOLBAR (cWinNeighborhoodUL,
            // The Sims Complete PPC; receipt evidence/UI-38/ul-filter-toolbar-law.md):
            // on the community screen (0x111==0) the engine mounts NO navbar —
            // the strip IS the top chrome: kFilterToolbar 5045
            // NghUI\Filter_Toolbar_Unleashed.bmp 800x72 at the artboard origin
            // (cWinNeighborhoodUL::Init 0x10462810: strip button this+0x180,
            // SetImage(this+0x11c = 5045, 1, 1), no SetArea -> parent origin),
            // with SEVEN cTSWinBtn children at the static-init ladder
            // (225|275|325|375|425|475|525, y=20 — writer 0x465460-0x4656b4,
            // file 0x46e3bc; BuildFilterToolbar 0x10459b10 walks it via
            // [r2-0x470c] + 8/button; 200x52 sheets SetImage(4,1) -> 50x52
            // cells). Button order/art (thunks 0x459ba4.., Res_Nbhd.h ids):
            // DogCat 5064, SmallAnimal 5065, Shopping 5063, Gardening 5062,
            // Food 5061, Recreation 5066, Lodging 5060; tooltips STR# 171
            // 'Filter Bar' [0..6] (BuildFilterToolbar GetString(set, i+1),
            // 1-based). Click = ProcessFilterToolbarByType @0x10458c60:
            // bit->mode {1:6, 2:4, 4:3, 8:2, 0x10:0, 0x20:1, 0x40:5}, clear
            // the previous button (ProcessFilterToolbar 0x104598f0), press
            // the new one, SetFilterMode plaque
            // ({0x10:cpanel\filterdogcat, 0x20:filtersmallanimal,
            // 8:filtershopping, 4:filtergardening, 2:filterfood,
            // 0x40:filterpark, 1:filterlodging}.bmp — 4930..4936) and
            // HighlightLotsForMode. The Init tail enters with the DEFAULT
            // filter 0x10 (DogCat) — this+0x188 = 0x10, plaque 0x1346,
            // ProcessFilterToolbarByType(this, 0x10) — then the persisted
            // filter overrides (LoadCurrentFilter; port keeps the default,
            // persistence ported UI-38b).
            // DISCLOSED divergence: the port's mode 4 is the MERGED
            // residential+community view that keeps the engine navbar as its
            // navigation surface, so the strip band mounts directly BELOW
            // the 52px navbar (strip top at artboard y=52); the strip's
            // INTERNAL layout is engine-exact (buttons at strip-local
            // ladder y=20 -> artboard y=72).
            if (mode == 4)
            {
                var strip = new UIOriginalNavbarButton("NghUI\\filter_toolbar_Unleashed.bmp", 1, 1, null)
                { ForceState = 0 };
                RegisterAnchor(strip, new Vector2(0, 52), true);
                Add(strip);
                ULFilterStrip = strip;
                ULFilterStripMounted = strip.OriginalMounted;

                for (int ui = 0; ui < ULFilterLaw.Length; ui++)
                {
                    var slot = ULFilterLaw[ui];
                    // no ForceState: the pressed state renders cell 1 through
                    // Selected (the engine's SetState(btn, 0/1) on switch).
                    var ubtn = new UIOriginalNavbarButton(slot.Member, 4, 1, ULTip171(slot.LabelIndex));
                    RegisterAnchor(ubtn, new Vector2(slot.X, 52 + slot.Y), true);
                    var cmd = slot.CmdBit;
                    ubtn.OnButtonClick += (b) => ProcessULFilterByType(cmd);
                    Add(ubtn);
                    ULFilterButtons.Add(ubtn);
                }
                ULFilterButtonsMounted = ULFilterButtons.Count == 7
                    && ULFilterButtons.TrueForAll(b => b.OriginalMounted);
                // the Init default: filter 0x10 (DogCat) pressed + plaque +
                // HighlightLotsForMode (mode 0 -> community lots blue). THEN the
                // UI-38b LAW 2 tail (the native Init second block): load the
                // PERSISTED filter (LoadCurrentFilter @0x10458d60 — STR# 6
                // 'Filter Bar Settings' slot 3 in the neighborhood dir's
                // LotLocations.iff) and reprocess — the save overrides the
                // default (the shipped slot carries '4', a Gardening save).
                ProcessULFilterByType(0x10);
                var persistedCmd = ULFilterLaws.LoadPersistedFilterCmd(
                    FSO.Content.Content.Get().Neighborhood.LotLocations);
                if (persistedCmd > 0 && persistedCmd != 0x10)
                {
                    ProcessULFilterByType(persistedCmd);
                    GameLog.Write("uidtbar: persisted filter restored (cmd 0x" + persistedCmd.ToString("x") + ")");
                }
            }

            // The engine emits this from PostChildDraw, after the banner and
            // Prev/Next children. Mount it last so those overlapping cells do
            // not cover the centered decimal.
            if (CurrentNumber != null) Add(CurrentNumber);

            StripsMounted++;
            ButtonsMounted += Buttons.Count;
            ApplyAnchors();
            // post-ApplyAnchors so the probe carries the artboard shift
            if (_payphoneButton != null) PayphonePositionForProbe = _payphoneButton.Position;
        }

        private void RegisterAnchor(UIElement element, Vector2 anchor, bool followsArtboard)
        {
            BaseAnchors[element] = anchor;
            if (followsArtboard) ArtboardAnchors.Add(element);
            element.Position = anchor;
        }

        private void ApplyAnchors()
        {
            var screen = UIScreen.Current;
            var offset = FSOEnvironment.SoftwareKeyboard
                ? Vector2.Zero
                : OriginalArtboardOffset(
                    screen?.ScreenWidth ?? GlobalSettings.Default.GraphicsWidth,
                    screen?.ScreenHeight ?? GlobalSettings.Default.GraphicsHeight);
            foreach (var anchor in BaseAnchors)
            {
                anchor.Key.Position = anchor.Value
                    + (ArtboardAnchors.Contains(anchor.Key) ? offset : Vector2.Zero);
            }
        }

        private void WireClick(UIOriginalNavbarButton btn, EngineNavSlot slot, ushort mode, string tipTable)
        {
            switch (slot.DebugName)
            {
                case "Import":
                    // UI-21 (native RegularModeBtnHandler 0x4711d0 → the import-UI
                    // button is CycleThroughImports' only caller, decode §1.1): the
                    // button opens the staged-FAM confirmation dialog (STR# 143
                    // law) on the game screen. Destination strips carry their own
                    // Import art and reach the same flow (this case is mode-
                    // independent; the member swap above is art-only).
                    ImportButtonForProbe = btn;
                    btn.OnButtonClick += (b) =>
                    {
                        ImportClicksForProbe++;
                        var gs = UIScreen.Current as Screens.TS1GameScreen;
                        if (gs != null) gs.ShowImportDialog();
                        else GameLog.Write("uinav: Import click (no TS1GameScreen)");
                    };
                    break;
                case "Credits":
                    // UI-30 (native RegularModeBtnHandler credits case @0x4715dc):
                    // the button constructs the single cWinCredits overlay over
                    // the neighborhood window (ShowCreditsScreen owns the latch).
                    btn.OnButtonClick += (b) =>
                    {
                        var gs = UIScreen.Current as Screens.TS1GameScreen;
                        if (gs != null) gs.ShowCreditsScreen();
                        else GameLog.Write("uinav: Credits click (no TS1GameScreen)");
                    };
                    break;
                case "Bulldoze":
                    // Armed/disarmed by the pre-subscribed EvictModeBtnHandler
                    // law hook above; nothing further (native jump-table case
                    // ends in the mode switch itself).
                    break;
                case "Rezone":
                    // NBR-05: armed/disarmed by the pre-subscribed hook above
                    // (the STR# 151 [10] 'Evict or Rezone' tool — the rezone twin
                    // of the Bulldoze arm). Armed lot clicks follow the STR# 131
                    // rezone cascade on TS1GameScreen (RezoneLotClickFlow).
                    break;
                case "Previous":
                case "Next":
                    // NBR-05: the native Next/Previous (@0xad170/@0xad1f0) feed
                    // SwitchToNewNeighborhood a direction; the port's backend
                    // (NBR-02) takes the target id, so the wrap/selection order
                    // is this UI's concern: cycle the enumerated available ids.
                    btn.OnButtonClick += (b) =>
                    {
                        var gs = UIScreen.Current as Screens.TS1GameScreen;
                        if (gs != null) gs.SwitchNeighborhood(slot.DebugName == "Next" ? 1 : -1);
                        else GameLog.Write("uinav: " + slot.DebugName + " click (no TS1GameScreen)");
                    };
                    break;
                case "MoveIn":
                    btn.OnButtonClick += (b) =>
                    {
                        // R208: the engine switches screens DIRECTLY —
                        // cWinPickFamily::HandleButton 0x2d8f30 constructs the
                        // next screen (new + ctor + AddChild) with no wipe; the
                        // diagonal-stripe trans_cas wipe was a Simitone
                        // invention (R159 census finding 4) and is retired.
                        GameController.EnterCAS();
                    };
                    break;
                case "Exit":
                    btn.OnButtonClick += (b) =>
                    {
                        var gs = UIScreen.Current as Screens.TS1GameScreen;
                        if (gs != null) gs.CloseAttempt();   // same save/quit flow as the options Quit
                        else GameFacade.Game.Exit();
                    };
                    break;
                case "Downtown":
                case "Vacation":
                case "Studio":
                case "Magic":
                    btn.OnButtonClick += (b) => PopMode(slot.DestMode);
                    break;
                default:
                    btn.OnButtonClick += (b) =>
                    {
                        GameLog.Write("uinav: " + slot.DebugName + " click (system not ported)");
                    };
                    break;
            }
        }

        public void PopMode(ushort mode)
        {
            if (Panel == null) return;
            Panel.PopulateScreen(mode);
            SetMode(mode, MoveInMode);
        }

        // STR# 151 'NghbBtnTips' with the R159 literal-fallback idiom.
        private static string Tip151(int idx)
        {
            if (idx < 0) return null;
            var names = new string[]
            {
                "Select or Create Family", "Evict or Bulldoze", "Quit", "View Web Pages",
                "Switch To Previous Neighborhood", "Switch To Next Neighborhood", "Import Family",
                "Switch to Downtown View", "Credits", "Switch to Vacation Island View",
                "Evict or Rezone", "Switch to Studio Town View", "Switch to Magic Town View", ""
            };
            var t = GameFacade.Strings.GetString("151", idx.ToString());
            if (string.IsNullOrEmpty(t) || t.Contains("MISSING")) return names[idx];
            return t;
        }

        private static string DestTip(string table, int idx, string fallback)
        {
            if (table == null) return fallback;
            var t = GameFacade.Strings.GetString(table, idx.ToString());
            if (string.IsNullOrEmpty(t) || t.Contains("MISSING")) return fallback;
            return t;
        }
    }

    // R160: a cTSWinBtn on an ORIGINAL UIGraphics.far sheet, following the engine's
    // SetImage(art, cols, rows) law: the cell is (W/cols, H/rows) — note the toolbar
    // sheets are 212x52, which does NOT divide into square frames (53x52 cells), so
    // UIOriginal.Frame()'s W%H==0 crop cannot be used; this crops by cols directly
    // (UIOriginal.Rect law). State order is the UIButton convention (0 up / 1 pressed
    // or selected / 2 hover / 3 disabled). Sheets that fail to resolve keep the white-px base
    // (never in practice; the gate pins resolution).
    public class UIOriginalNavbarButton : UIButton
    {
        public readonly string Member;
        public readonly int Cols, Rows;
        public bool OriginalMounted;

        public UIOriginalNavbarButton(string member, int cols, int rows, string tooltip)
            : base(FSO.Common.Utils.TextureGenerator.GetPxWhite(GameFacade.GraphicsDevice))
        {
            Member = member;
            Cols = cols;
            Rows = rows;
            var tex = UIOriginal.EnsureResolved(member)?.Get(GameFacade.GraphicsDevice);
            OriginalMounted = tex != null;
            if (tex != null)
            {
                Texture = tex;
                ImageStates = cols;
                ButtonFrames = rows;
            }
            if (!string.IsNullOrEmpty(tooltip)) Tooltip = tooltip;
        }

        public int CellWidth { get { return Texture != null ? Texture.Width / Cols : 0; } }
        public int CellHeight { get { return Texture != null ? Texture.Height / Rows : 0; } }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible || Texture == null) { base.Draw(batch); return; }
            int frame = ForceState;
            if (frame < 0)
            {
                frame = Disabled ? 3 : CurrentFrame;
                if (Selected) frame = 1;
            }
            if (frame > Cols - 1) frame = Cols - 1;
            if (frame < 0) frame = 0;
            var cw = Texture.Width / Cols;
            var ch = Texture.Height / Rows;
            var row = Math.Max(0, Math.Min(Rows - 1, ButtonFrame));
            DrawLocalTexture(batch, Texture, new Rectangle(frame * cw, row * ch, cw, ch), Vector2.Zero);
        }
    }
}
