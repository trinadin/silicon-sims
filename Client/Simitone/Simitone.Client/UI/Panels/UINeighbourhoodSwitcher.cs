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
        public readonly List<string> LastMembers = new List<string>();

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
            LastMembers.Clear();
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

            // The engine emits this from PostChildDraw, after the banner and
            // Prev/Next children. Mount it last so those overlapping cells do
            // not cover the centered decimal.
            if (CurrentNumber != null) Add(CurrentNumber);

            StripsMounted++;
            ButtonsMounted += Buttons.Count;
            ApplyAnchors();
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
