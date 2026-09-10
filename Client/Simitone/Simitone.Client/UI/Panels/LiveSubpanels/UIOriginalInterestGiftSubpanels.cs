using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Common.Rendering.Framework.IO;
using FSO.Common.Rendering.Framework.Model;
using FSO.SimAntics.Model;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simitone.Client.UI.Controls;
using Simitone.Client.UI.Model;
using Simitone.Client.UI.Panels;
using Simitone.Client.UI.Screens;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simitone.Client.UI.Panels.LiveSubpanels
{
    /// <summary>
    /// R145: the cWinPeople INTEREST panel (cWinPeople::BuildInterestWindow
    /// @0x287010, law tools/iff-dump/r145/interest-gift-law.md) — band layout:
    /// title font[6] 'Interests' (Live.iff STR# 140 [0]); topic cells 125x25
    /// (icon plaque 75x25 at (0,0), bar background 40x16 at (80,5), fill bar
    /// revealed value*4 px); COLUMN-MAJOR grid at pitch 128x27, 3x3 at 1024
    /// logical pixels and 2x3 at 800, from (leftArrowW+4, titleBottom+5);
    /// paged 9/6 per page with res 100/101 arrows (9x49) vertically centered.
    /// Value law: raw/100 clamped to <=10
    /// units (cWinInterest::TSPaint @0x405e80).
    /// R150: the words now carry REAL values — the port VM executes every
    /// sim's own 'init traits' tree (raw 0..10 rolls into words 46..55;
    /// family avatars verified live that round), so the panel reads the FULL
    /// word and x100s it into the display scale. Topics 8-11 share 0-3's
    /// words (engine widget law, r145 section 2.5).
    /// R184: all 19 widgets remain available, but the original never displays
    /// all 19 as one sequence. BuildInterests selects one of two alias arrays:
    /// adults show 0..7 + 12..18 (15 topics), while age 1..17 shows
    /// 8..11 + 4..7 (8 child/pet topics). The prior port paged all 19 and
    /// therefore exposed three non-native pages. The expansion-word decode
    /// (tools/iff-dump/r151/r151-expansion-interest-decode.md) proved
    /// the "expansion block" is the SAME person-word array at LOW indices:
    /// person word N = cXPerson + 0x58c + 2N — Exercise 13, Food 14,
    /// Parties 16, Style 20, Hollywood 26 (aliases of dead TSO-era names;
    /// the port's ChatBaloonOn writes are TS1-gated to keep 26 clean),
    /// Technology 54, Romance 55 (the init-traits tree's rolls #9/#10).
    /// Writers per the corpus scan: every character tree writes
    /// MyPersonData[20] = 5 (Style = 5 for tree sims); nothing else
    /// writes 13/14/16/26 in the corpus — the engine's own
    /// cXPerson::RandomizeAllInterests does, at ×100 scale behind its
    /// all-8-words-&lt;=10 gate (r153 correction of the r151 "all-10
    /// sentinel" misread; ported R152 in the generator);
    /// the port's ×100 display bridge (below) is the R150 disclosed law.
    /// Residuals (disclosed): PersonGlobals BHAV 9761 'Convert Interests
    /// to 0-1000' (the script-side ≤10-gated ×100 bridge) has no traced
    /// caller — when the original runs it is INTERP; interests re-roll
    /// each session (standing disclosed model).
    /// </summary>
    public class UIOriginalInterestSubpanel : UISubpanel
    {
        public UIOriginalText HostTitle;
        public UIOriginalInterestCell[] Cells = new UIOriginalInterestCell[19];
        public Simitone.Client.UI.Panels.UIOriginalSheetButton PagePrev, PageNext;
        public int Page;
        public const int WidgetCount = 19;   // the engine widget table's full set (STR# 140 [1..19])
        public const int CellW = 125, CellH = 25, PitchX = 128, PitchY = 27;
        public const int Rows = 3, Columns = 3, PerPage = Rows * Columns;
        public const int NarrowColumns = 2, NarrowPerPage = Rows * NarrowColumns;
        public const int ContentTop = 18;
        public const int PagerY = 34;
        public static readonly int[] AdultTopics =
            { 0, 1, 2, 3, 4, 5, 6, 7, 12, 13, 14, 15, 16, 17, 18 };
        public static readonly int[] YoungTopics =
            { 8, 9, 10, 11, 4, 5, 6, 7 };
        private int[] ActiveTopics = AdultTopics;
        private bool LastYoung;
        public UIOriginalGiftPopup CurrentPopup;
        public int PopupTopic = -1;
        public IReadOnlyList<int> VisibleTopicSequence => ActiveTopics;

        public static bool UsesYoungTopics(int age)
        {
            return age > 0 && age < 18;
        }

        /// <summary>
        /// Native cWinPeople uses three columns at 1024 logical pixels and two
        /// at 800 (host widths 504 and 280 respectively).
        /// </summary>
        public static int ColumnsForHostWidth(float hostWidth)
        {
            return hostWidth >= 504 ? Columns : NarrowColumns;
        }

        public static int PerPageForHostWidth(float hostWidth)
        {
            return Rows * ColumnsForHostWidth(hostWidth);
        }

        /// topic display index (STR# 140 [1..]) -> the person word backing it
        /// (the init-traits tree's raw 0..10 roll; the panel x100s it into the
        /// engine's 0..1000 display scale where raw/100 clamp<=10 applies).
        /// TOPICS 8-11 DELIBERATELY SHARE 0-3'S WORDS: the engine's
        /// SetInterest jump table routes Toys/Aliens/Pets/School to the SAME
        /// person halfwords as Travel/Money/Politics/The-60s (the pet-topic
        /// sharing — for humans those widgets mirror the base four;
        /// r145 interest-gift-law.md section 2.5, decoded widget table).
        /// TOPICS 12-16 are the Hot-Date-era low-index words (decode:
        /// r151-expansion-interest-decode.md — Exercise 13, Food 14,
        /// Parties 16, Style 20 [the trees' own MyPersonData[20]=5],
        /// Hollywood 26); 17/18 are the word-run continuation +0x5f8/+0x5fa
        /// = the tree's rolls #9/#10.
        public static readonly int[] TopicWord =
        {
            46, // 0 Travel
            47, // 1 Money    (STR# slot named 'Money', icon Violence)
            48, // 2 Politics
            49, // 3 The 60's
            50, // 4 Weather
            51, // 5 Sports
            52, // 6 Music
            53, // 7 Outdoors
            46, // 8 Toys     (shares Travel's word — engine widget law)
            47, // 9 Aliens   (shares Money's word)
            48, // 10 Pets    (shares Politics' word)
            49, // 11 School  (shares The-60s' word)
            13, // 12 Exercise (+0x5a6)
            14, // 13 Food     (+0x5a8)
            16, // 14 Parties  (+0x5ac)
            20, // 15 Style    (+0x5b4 — trees write 5)
            26, // 16 Hollywood (+0x5c0)
            54, // 17 Technology (+0x5f8 — init-traits roll #9)
            55, // 18 Romance    (+0x5fa — init-traits roll #10)
        };

        /// the engine fill law: value = clamp(raw/100, 0, 10) units of 4px.
        public static int FillWidthForValue(int raw)
        {
            return Math.Max(0, Math.Min(10, raw / 100)) * 4;
        }

        /// R152: the two ORIGINAL writer dialects reconciled at read. The
        /// character trees write RAW 0..10 (R150); the engine's creation
        /// path (cXPerson::Initialize -> RandomizeAllInterests) writes x100
        /// (R152, r151 decode sec 2.1/2.4). The panel's display scale is
        /// the x100 dialect (cWinInterest divides by 100), so raw words
        /// bridge x100 and x100 words pass through — TOTAL on both domains:
        /// the raw domain max (10) is below the smallest nonzero x100
        /// value (100), so no word is ambiguous. Disclosed interpretation
        /// of the decode's sec-2.4 scale tension.
        public static int BridgeToDisplayScale(int word)
        {
            return (word >= 0 && word <= 10) ? word * 100 : word;
        }

        public UIOriginalInterestSubpanel(TS1GameScreen game) : base(game)
        {
            var gd = GameFacade.GraphicsDevice;

            // title: font[6] at the people-text anchor {5,0} (r144 section 1.7)
            var titleFont = OriginalGlyphFont.LoadByIndex(6, gd);
            var titleStr = OriginalLiveStrings.Entry(140, 0) ?? "Interests";
            if (titleFont != null)
            {
                HostTitle = new UIOriginalText(titleStr, titleFont) { Position = new Vector2(5, 0) };
                Add(HostTitle);
            }

            for (int i = 0; i < WidgetCount; i++)
            {
                var topic = i;
                var c = new UIOriginalInterestCell(i)
                {
                    Position = new Vector2(13, ContentTop),
                    Visible = false,
                };
                // cWinInterest forwards its left-button action to the one
                // cWinLivePopup client. The base handler fires the native
                // pin command on mouse-down rather than on release.
                c.OnButtonDown += b => ToggleTopicPopup(topic);
                Add(c);
                Cells[i] = c;
            }

            PagePrev = new Simitone.Client.UI.Panels.UIOriginalSheetButton("cpanel\\Buttons\\ScrollLeft.bmp")
            {
                Position = new Vector2(2, PagerY),
                Tooltip = GameFacade.Strings.GetString("154", "0"),
            };
            PagePrev.OnButtonClick += (b) => SetPage(Page - 1);
            Add(PagePrev);
            PageNext = new Simitone.Client.UI.Panels.UIOriginalSheetButton("cpanel\\Buttons\\ScrollRight.bmp")
            {
                Position = new Vector2(Size.X - 11, PagerY),
                Tooltip = GameFacade.Strings.GetString("154", "1"),
            };
            PageNext.OnButtonClick += (b) => SetPage(Page + 1);
            Add(PageNext);
            RefreshTopicSet(true);
        }

        public void SetPage(int page)
        {
            int perPage = PerPageForHostWidth(Size.X);
            int maxPage = Math.Max(0, (ActiveTopics.Length - 1) / perPage);
            Page = Math.Max(0, Math.Min(maxPage, page));
            PageNext.Position = new Vector2(Size.X - 11, PagerY);
            for (int i = 0; i < WidgetCount; i++) Cells[i].Visible = false;
            for (int slot = 0; slot < perPage; slot++)
            {
                int sequenceIndex = Page * perPage + slot;
                if (sequenceIndex >= ActiveTopics.Length) break;
                int topic = ActiveTopics[sequenceIndex];
                var cell = Cells[topic];
                cell.Visible = true;
                // BuildInterests loops columns outside rows: linear slot =
                // col*Rows + row, not the former row-major port ordering.
                int col = slot / Rows;
                int row = slot % Rows;
                cell.Position = new Vector2(13 + col * PitchX, ContentTop + row * PitchY);
            }
            PagePrev.Visible = Page != 0;
            PageNext.Visible = (Page + 1) * perPage < ActiveTopics.Length;
            if (PopupTopic >= 0 && (PopupTopic >= Cells.Length || !Cells[PopupTopic].Visible))
                ClosePopup();
            Invalidate();
        }

        public override void GameResized()
        {
            base.GameResized();
            SetPage(Page);
            PositionPopup();
        }

        internal void ToggleTopicPopupForProbe(int topic)
        {
            ToggleTopicPopup(topic);
        }

        private void ToggleTopicPopup(int topic)
        {
            if (topic < 0 || topic >= WidgetCount) return;
            if (CurrentPopup != null && PopupTopic == topic)
            {
                ClosePopup();
                return;
            }

            var title = OriginalLiveStrings.Entry(140, topic + 1) ?? "";
            var body = OriginalLiveStrings.Entry(142, topic + 1) ?? "";
            if (CurrentPopup == null)
            {
                CurrentPopup = new UIOriginalGiftPopup(title, body);
                DynamicOverlay.Add(CurrentPopup);
            }
            else CurrentPopup.SetContent(title, body);
            PopupTopic = topic;
            PositionPopup();
        }

        private void PositionPopup()
        {
            if (CurrentPopup == null) return;
            CurrentPopup.Position = new Vector2(Size.X - CurrentPopup.Size.X, -CurrentPopup.Size.Y);
        }

        public void ClosePopup()
        {
            if (CurrentPopup != null)
            {
                DynamicOverlay.Remove(CurrentPopup);
                CurrentPopup = null;
            }
            PopupTopic = -1;
        }

        private void RefreshTopicSet(bool force = false)
        {
            var av = Game.SelectedAvatar;
            var age = av?.GetPersonData(VMPersonDataVariable.PersonsAge) ?? (short)27;
            // This is the executable's person+0x600 test. It intentionally
            // includes children (and the expansion pet encodings) in the
            // eight-topic sequence.
            bool young = UsesYoungTopics(age);
            if (!force && young == LastYoung) return;
            LastYoung = young;
            ActiveTopics = young ? YoungTopics : AdultTopics;
            SetPage(0);
        }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            RefreshTopicSet();
            var av = Game.SelectedAvatar;
            if (av != null)
            {
                for (int i = 0; i < WidgetCount; i++)
                {
                    var w = TopicWord[i];
                    if (w < 0) { Cells[i].RawValue = 0; continue; }
                    // R150/R152: the word is the interest in ONE of the two
                    // original dialects — the trees' raw 0..10 scale or the
                    // engine creation path's x100 — bridged into the panel's
                    // x100 display scale (BridgeToDisplayScale), then the
                    // pinned display law applies verbatim.
                    Cells[i].RawValue = BridgeToDisplayScale(av.GetPersonData((VMPersonDataVariable)w));
                }
            }
        }

        public override void Kill()
        {
            ClosePopup();
            base.Kill();
        }
    }

    /// One cWinInterest cell (125x25): the topic's 75x25 pictorial plaque at
    /// (0,0) + the bar background 40x16 at (80,5) with the 40x16 fill revealed
    /// value*4 px (art kInterestBarsBackground 1620 / kInterestBars 1619).
    public class UIOriginalInterestCell : UIButton
    {
        public readonly int Topic;
        public int RawValue;
        private Texture2D Plaque, BarBack, BarFill;
        private Vector2 CellSize = new Vector2(125, 25);

        public override Vector2 Size
        {
            get { return CellSize; }
            set
            {
                CellSize = value;
                base.Size = value;
                if (ClickHandler != null) ClickHandler.Region.Height = (int)value.Y;
            }
        }

        public UIOriginalInterestCell(int topic) : base(FSO.Common.Utils.TextureGenerator.GetPxWhite(GameFacade.GraphicsDevice))
        {
            Topic = topic;
            ImageStates = 1;
            Size = new Vector2(125, 25);
            Tooltip = OriginalLiveStrings.Entry(140, topic + 1) ?? "";
        }

        public override Rectangle GetBounds()
        {
            return new Rectangle(0, 0, (int)CellSize.X, (int)CellSize.Y);
        }

        public void LoadArt()
        {
            if (Plaque != null || BarBack != null) return;
            var gd = GameFacade.GraphicsDevice;
            // icon res order (1600..): Travel, Violence(=STR# 140's 'Money'),
            // Politics, The60s, Weather, Sports, Music, Outdoors, Toys,
            // Aliens, Pets, School, Exercise, Food, Parties, then the four
            // expansion plaques in STR# 140 order — Style, Hollywood (art
            // named 'charisma'), Technology (art named 'health'), Romance.
            var members = new[]
            {
                "cpanel\\Buttons\\InterestTravel.bmp", "cpanel\\Buttons\\InterestViolence.bmp",
                "cpanel\\Buttons\\InterestPolitics.bmp", "cpanel\\Buttons\\InterestThe60s.bmp",
                "cpanel\\Buttons\\InterestWeather.bmp", "cpanel\\Buttons\\InterestSports.bmp",
                "cpanel\\Buttons\\InterestMusic.bmp", "cpanel\\Buttons\\InterestOutdoors.bmp",
                "cpanel\\Buttons\\InterestToys.bmp", "cpanel\\Buttons\\InterestAliens.bmp",
                "cpanel\\Buttons\\InterestPets.bmp", "cpanel\\Buttons\\InterestSchool.bmp",
                "cpanel\\Buttons\\InterestExercise.bmp", "cpanel\\Buttons\\InterestFood.bmp",
                "cpanel\\Buttons\\InterestParties.bmp", "cpanel\\Buttons\\InterestStyle.bmp",
                "cpanel\\Buttons\\InterestCharisma.bmp", "cpanel\\Buttons\\InterestHealth.bmp",
                "cpanel\\Buttons\\InterestRomance.bmp",
            };
            try
            {
                if (Topic >= 0 && Topic < members.Length) Plaque = UIOriginal.EnsureResolved(members[Topic])?.Get(gd);
                BarBack = UIOriginal.EnsureResolved("cpanel\\Backgrounds\\InterestBarsBackground.bmp")?.Get(gd);
                BarFill = UIOriginal.EnsureResolved("cpanel\\InterestBars.bmp")?.Get(gd);
            }
            catch { }
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            LoadArt();
            if (Plaque != null) DrawLocalTexture(batch, Plaque, null, Vector2.Zero, Vector2.One);
            if (BarBack != null) DrawLocalTexture(batch, BarBack, null, new Vector2(80, 5), Vector2.One);
            var w = UIOriginalInterestSubpanel.FillWidthForValue(RawValue);
            if (BarFill != null && w > 0)
                DrawLocalTexture(batch, BarFill, new Rectangle(0, 0, w, 16), new Vector2(80, 5), Vector2.One);
        }

        /// the mounted topic plaque (null until LoadArt) — gate read.
        public Texture2D PlaqueTex { get { LoadArt(); return Plaque; } }
    }

    /// <summary>
    /// R145: the cWinPeople GIFT panel (BuildGiftPanel @0x286820) — band
    /// layout: title font[6] 'Inventory' (Live.iff STR# 141 [0]); a single row
    /// of 9 cells at 1024 or 5 at 800 (42x60 — the LEFT HALF of the item's
    /// catalog icon centered + a stack-count label font[7] below); three
    /// filter buttons (res
    /// 4112/4113/4114, 20x20 cells) in a vertical stack at host x=2 filtering
    /// inventory cats {0,4,5} / {7} / {8}; items sorted by id.
    /// R187: cWinGift::TSOnMouseDownL @0x407e30 only delegates to the base
    /// button. cWinPeople::TSOnCommand @0x28cb40 then toggles the shared
    /// cWinLivePopup, populated by cWinGift::SetupClient @0x4081f0 with the
    /// catalog name and description. It does not give/select/mutate an item.
    /// </summary>
    public class UIOriginalGiftSubpanel : UISubpanel
    {
        public const int ItemsPerPage = 9;
        public const int NarrowItemsPerPage = 5;
        public const int ContentLeft = 45;
        public const int CellWidth = 42;
        public UIOriginalText HostTitle;
        public List<UIOriginalGiftCell> Cells = new List<UIOriginalGiftCell>();
        public Simitone.Client.UI.Panels.UIOriginalSheetButton[] Filters = new Simitone.Client.UI.Panels.UIOriginalSheetButton[3];
        public Simitone.Client.UI.Panels.UIOriginalSheetButton PagePrev, PageNext;
        public int Filter = 0;
        public int Page;
        public UIOriginalGiftPopup CurrentPopup;
        public uint? PopupItemID;
        private List<FSO.Files.Formats.IFF.Chunks.InventoryItem> Items = new List<FSO.Files.Formats.IFF.Chunks.InventoryItem>();

        // InitInventorySorts stores Gift/Magic/Other at +0x1a8/+0x1a4/+0x1ac.
        // BuildGifts @0x288834 tests those fields as {0,4,5}/{7}/{8}.
        private static readonly int[][] FilterCats = { new[] { 0, 4, 5 }, new[] { 7 }, new[] { 8 } };

        public static IReadOnlyList<int> CategoriesForFilter(int filter)
        {
            if (filter < 0 || filter >= FilterCats.Length)
                throw new ArgumentOutOfRangeException(nameof(filter));
            return FilterCats[filter];
        }

        /// <summary>
        /// cWinPeople stores max-items+1 as 10 in 1024 mode and 6 in 800
        /// mode, producing native page capacities of nine and five.
        /// </summary>
        public static int ItemsPerPageForHostWidth(float hostWidth)
        {
            return hostWidth >= 504 ? ItemsPerPage : NarrowItemsPerPage;
        }

        public static int ClampPage(int requestedPage, int itemCount, float hostWidth)
        {
            // BuildGifts' zero-item path hides both arrows and returns before
            // touching people+0x36c, so an empty filter retains its page for a
            // later non-empty filter selection.
            if (itemCount <= 0) return Math.Max(0, requestedPage);
            int perPage = ItemsPerPageForHostWidth(hostWidth);
            int maxPage = Math.Max(0, (itemCount - 1) / perPage);
            return Math.Max(0, Math.Min(maxPage, requestedPage));
        }

        public UIOriginalGiftSubpanel(TS1GameScreen game) : base(game)
        {
            var gd = GameFacade.GraphicsDevice;
            var titleFont = OriginalGlyphFont.LoadByIndex(6, gd);
            var titleStr = OriginalLiveStrings.Entry(141, 0) ?? "Inventory";
            if (titleFont != null)
            {
                HostTitle = new UIOriginalText(titleStr, titleFont) { Position = new Vector2(5, 0) };
                Add(HostTitle);
            }

            var fdef = new[]
            {
                "cpanel\\Buttons\\InventoryGiftSort.bmp",    // res 4112
                "cpanel\\Buttons\\InventoryMagicSort.bmp",   // res 4113
                "cpanel\\Buttons\\InventoryOtherSort.bmp",   // res 4114
            };
            // STR# 241 is ordered Other, Magic, Ingredients, while the
            // executable's button fields are Gift/Ingredients, Magic, Other.
            // Indexing this table by the button array silently swapped the
            // first and last quicktips.
            var tooltipIndex = new[] { 2, 1, 0 };
            // InitInventorySorts 0x28e780..0x28e8a4 measures font[6] through
            // vt+0x60, then places the single-byte host-local stack at:
            //   Magic = M+20; Other = M+H(gift)+21;
            //   Gift = M+H(gift)+H(magic)+22.
            // The raw 0x41820140 at 0x28e640 is beq (English/zero takes this
            // branch); the old r131/r159 text decoder incorrectly called it
            // bne, which reverses the SB/DB labels without changing the math.
            // Every shipped sheet cell is 20px high, so this is a 21px-pitch
            // stack below the title. The former M-10 start covered "Inv".
            int titleHeight = titleFont?.LineHeight ?? 10;
            var filterY = new[] { titleHeight + 62, titleHeight + 20, titleHeight + 41 };
            for (int i = 0; i < 3; i++)
            {
                var b = new Simitone.Client.UI.Panels.UIOriginalSheetButton(fdef[i])
                {
                    Position = new Vector2(2, filterY[i]),
                    Tooltip = GameFacade.Strings.GetString("241", tooltipIndex[i].ToString()),
                };
                var idx = i;
                // Native TSOnCommand changes the active sort and rebuilds at
                // the existing page; BuildGifts only clamps if it is now past
                // the filtered result's last page.
                b.OnButtonClick += (btn) => { Filter = idx; Rebuild(); };
                Add(b);
                Filters[i] = b;
            }

            PagePrev = new Simitone.Client.UI.Panels.UIOriginalSheetButton("cpanel\\Buttons\\ScrollLeft.bmp")
            {
                Position = new Vector2(34, ContentTop(titleFont) + (100 - ContentTop(titleFont) - 49) / 2),
                Tooltip = GameFacade.Strings.GetString("154", "0"),
            };
            PagePrev.OnButtonClick += (b) => SetPage(Page - 1);
            Add(PagePrev);
            PageNext = new Simitone.Client.UI.Panels.UIOriginalSheetButton("cpanel\\Buttons\\ScrollRight.bmp")
            {
                Position = new Vector2(Size.X - 11, ContentTop(titleFont) + (100 - ContentTop(titleFont) - 49) / 2),
                Tooltip = GameFacade.Strings.GetString("154", "1"),
            };
            PageNext.OnButtonClick += (b) => SetPage(Page + 1);
            Add(PageNext);
            Rebuild();
        }

        private static int ContentTop(OriginalGlyphFont font)
        {
            return (font?.LineHeight ?? 10) + 5;
        }

        private void Fetch()
        {
            Items.Clear();
            try
            {
                var sel = Game.SelectedAvatar;
                if (sel == null) return;
                var nid = sel.GetPersonData(VMPersonDataVariable.NeighborId);
                var inv = FSO.Content.Content.Get().Neighborhood.GetInventoryByNID(nid);
                if (inv != null)
                {
                    var cats = FilterCats[Filter];
                    Items = inv.Where(x => Array.IndexOf(cats, x.Type) >= 0)
                        .OrderBy(x => x.GUID).ToList();
                }
            }
            catch { }
        }

        public void Rebuild()
        {
            Fetch();
            LayoutPage(Page);
        }

        public void SetPage(int page)
        {
            LayoutPage(page);
        }

        private void LayoutPage(int page)
        {
            int perPage = ItemsPerPageForHostWidth(Size.X);
            Page = ClampPage(page, Items.Count, Size.X);
            PageNext.Position = new Vector2(Size.X - 11, PageNext.Position.Y);
            foreach (var c in Cells) Remove(c);
            Cells.Clear();
            int first = Page * perPage;
            int count = Math.Min(perPage, Math.Max(0, Items.Count - first));
            var contentTop = ContentTop(HostTitle?.Font);
            int rowY = contentTop + (100 - contentTop - 60) / 2;
            for (int i = 0; i < count; i++)
            {
                var c = new UIOriginalGiftCell(Items[first + i])
                {
                    Position = new Vector2(ContentLeft + i * CellWidth, rowY),
                };
                c.OnClicked += TogglePopup;
                Add(c);
                Cells.Add(c);
            }
            for (int i = 0; i < 3; i++) Filters[i].State = (byte)((i == Filter) ? 1 : 0);
            PagePrev.Visible = Items.Count > 0 && Page > 0;
            PageNext.Visible = Items.Count > 0 && (Page + 1) * perPage < Items.Count;
            Invalidate();
        }

        public override void GameResized()
        {
            base.GameResized();
            LayoutPage(Page);
            PositionPopup();
        }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            if (GameFrames++ % 60 == 0) Rebuild();
        }
        private int GameFrames;

        public void TogglePopup(UIOriginalGiftCell cell)
        {
            if (cell == null) return;
            TogglePopup(cell.Item.GUID, cell.ItemName, cell.ItemDescription);
        }

        internal void TogglePopupForProbe(uint itemID, string name, string description)
        {
            TogglePopup(itemID, name, description);
        }

        private void TogglePopup(uint itemID, string name, string description)
        {
            if (CurrentPopup != null && PopupItemID == itemID)
            {
                ClosePopup();
                return;
            }

            if (CurrentPopup == null)
            {
                CurrentPopup = new UIOriginalGiftPopup(name, description);
                DynamicOverlay.Add(CurrentPopup);
            }
            else CurrentPopup.SetContent(name, description);
            PopupItemID = itemID;
            PositionPopup();
        }

        private void PositionPopup()
        {
            if (CurrentPopup == null) return;
            CurrentPopup.Position = new Vector2(Size.X - CurrentPopup.Size.X, -CurrentPopup.Size.Y);
        }

        public void ClosePopup()
        {
            if (CurrentPopup != null)
            {
                DynamicOverlay.Remove(CurrentPopup);
                CurrentPopup = null;
            }
            PopupItemID = null;
        }

        public override void Kill()
        {
            ClosePopup();
            base.Kill();
        }
    }

    /// One cWinGift cell (42x60): the left half of the item's catalog icon
    /// centered + the stack count in font[7] below.
    public class UIOriginalGiftCell : UIContainer
    {
        public static readonly Color CountBackgroundColor = new Color(0x00, 0x00, 0x4A, 0xFF);
        public FSO.Files.Formats.IFF.Chunks.InventoryItem Item;
        public string ItemName;
        public string ItemDescription;
        public event Action<UIOriginalGiftCell> OnClicked;
        public bool UsesFallbackIcon;
        private UIOriginalText Count;
        private Texture2D Icon;
        private int CountTop;
        private int CountHeight;

        public UIOriginalGiftCell(FSO.Files.Formats.IFF.Chunks.InventoryItem item)
        {
            Item = item;
            Size = new Vector2(42, 60);
            ResolveCatalogText(item.GUID, out ItemName, out ItemDescription);
            Tooltip = ItemName;
            FSO.Client.Utils.UIUtils.GiveTooltip(this);
            Icon = Simitone.Client.UI.Panels.UIOriginalCatalogCell.GetObjIcon(item.GUID);
            if (Icon == null)
            {
                Icon = UIOriginal.EnsureResolved("cpanel\\CatalogUnknown.bmp")?.Get(GameFacade.GraphicsDevice);
                UsesFallbackIcon = Icon != null;
            }
            var f7 = OriginalGlyphFont.LoadByIndex(7, GameFacade.GraphicsDevice);
            if (f7 != null)
            {
                // BuildGifts copies the inventory token's +0x60 halfword into
                // cWinGift+0x190 for every category path; it is not a constant.
                var count = item.Count.ToString();
                CountTop = (Icon?.Height ?? 37) + 3;
                CountHeight = f7.LineHeight;
                Count = new UIOriginalText(count, f7)
                {
                    Position = new Vector2(21 - f7.Measure(count) / 2, CountTop),
                };
                Add(Count);
            }
            ListenForMouse(new Rectangle(0, 0, 42, 60), MouseEvent);
        }

        public string CountText => Count?.Text ?? Item.Count.ToString();
        public bool HasIcon => Icon != null;
        public Rectangle CountBackgroundRect => new Rectangle(0, CountTop, 42, CountHeight);

        public override Rectangle GetBounds()
        {
            return new Rectangle(0, 0, 42, 60);
        }

        private static void ResolveCatalogText(uint guid, out string name, out string description)
        {
            name = "";
            description = "";
            try
            {
                var worldObj = FSO.Content.Content.Get().WorldObjects.Get(guid);
                var ctss = worldObj?.Resource?.Get<FSO.Files.Formats.IFF.Chunks.CTSS>(worldObj.OBJ.CatalogStringsID);
                name = ctss?.GetString(0) ?? worldObj?.OBJ?.ChunkLabel ?? "";
                description = ctss?.GetString(1) ?? "";
            }
            catch { }
        }

        private void MouseEvent(UIMouseEventType type, UpdateState state)
        {
            // The original cTSWinBtn dispatches SendButtonCommand from
            // TSOnMouseDownL itself, after playing its normal click sound.
            if (type == UIMouseEventType.MouseDown)
            {
                FSO.HIT.HITVM.Get().PlaySoundEvent(FSO.Client.UI.Model.UISounds.Click);
                OnClicked?.Invoke(this);
            }
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            if (Icon != null)
            {
                int halfW = Icon.Width / 2;
                DrawLocalTexture(batch, Icon, new Rectangle(0, 0, halfW, Icon.Height),
                    new Vector2((42 - halfW) / 2, 0), Vector2.One);
            }
            if (Count != null && CountHeight > 0)
            {
                var px = FSO.Common.Utils.TextureGenerator.GetPxWhite(GameFacade.GraphicsDevice);
                DrawLocalTexture(batch, px, null, new Vector2(0, CountTop),
                    new Vector2(42, CountHeight), CountBackgroundColor);
            }
            base.Draw(batch);
        }
    }

    /// <summary>
    /// The Gift specialization of cWinLivePopup. BuildGiftPanel constructs its
    /// client with both picture sources null, so RebuildBuffer reserves no
    /// 168px art pane: text spans x=15..545 in the fixed 559px window.
    /// </summary>
    public class UIOriginalGiftPopup : UIContainer
    {
        public const int WindowWidth = 559;
        public const int MinimumHeight = 127;
        public const int TextLeft = 15;
        public const int TextRight = 545;
        public const int TextWidth = TextRight - TextLeft;
        public const int TitleTop = 12;
        public const int BodyTop = 31;
        public const int BodyLineHeight = 17;

        public string Title;
        public string Body;
        public int WrappedBodyLines;
        public UIOriginalText TitleText;
        public UIOriginalParagraph BodyParagraph;
        private Vector2 _size;
        public override Vector2 Size { get { return _size; } set { _size = value; } }

        public UIOriginalGiftPopup(string title, string body)
        {
            Title = title ?? "";
            Body = body ?? "";
            var titleFont = OriginalGlyphFont.LoadByIndex(10, GameFacade.GraphicsDevice);
            var bodyFont = OriginalGlyphFont.LoadByIndex(9, GameFacade.GraphicsDevice);
            WrappedBodyLines = CountWrappedLines(Body, bodyFont, TextWidth);
            int titleHeight = titleFont?.LineHeight ?? 19;
            int height = Math.Max(MinimumHeight,
                24 + titleHeight + WrappedBodyLines * BodyLineHeight);
            Size = new Vector2(WindowWidth, height);

            TitleText = new UIOriginalText(Title, titleFont)
            {
                Position = new Vector2(TextLeft, TitleTop),
                Size = new Vector2(TextWidth, titleHeight),
                Color = new Color(0xC3, 0xCD, 0xCD, 0xFF),
            };
            Add(TitleText);

            if (bodyFont != null)
            {
                BodyParagraph = new UIOriginalParagraph(bodyFont)
                {
                    Text = Body,
                    MaxWidth = TextWidth,
                    LineHeight = BodyLineHeight,
                    Position = new Vector2(TextLeft, BodyTop),
                    TextColor = new Color(0xC3, 0xCD, 0xCD, 0xFF),
                };
                Add(BodyParagraph);
            }
        }

        /// <summary>
        /// Retarget the one cWinLivePopup window to a different Interest/Gift
        /// client without replacing its chrome object.
        /// </summary>
        public void SetContent(string title, string body)
        {
            Title = title ?? "";
            Body = body ?? "";
            var titleFont = TitleText?.Font;
            var bodyFont = BodyParagraph?.Font;
            WrappedBodyLines = CountWrappedLines(Body, bodyFont, TextWidth);
            int titleHeight = titleFont?.LineHeight ?? 19;
            int height = Math.Max(MinimumHeight,
                24 + titleHeight + WrappedBodyLines * BodyLineHeight);
            Size = new Vector2(WindowWidth, height);
            if (TitleText != null)
            {
                TitleText.Text = Title;
                TitleText.Size = new Vector2(TextWidth, titleHeight);
            }
            if (BodyParagraph != null) BodyParagraph.Text = Body;
            Invalidate();
        }

        private static int CountWrappedLines(string text, OriginalGlyphFont font, int maxWidth)
        {
            if (font == null || string.IsNullOrEmpty(text)) return 0;
            int count = 0;
            foreach (var paragraph in text.Replace("\r\n", "\n").Split('\n'))
            {
                string line = "";
                foreach (var word in paragraph.Split(' '))
                {
                    var candidate = line.Length == 0 ? word : line + " " + word;
                    if (font.Measure(candidate) <= maxWidth || line.Length == 0) line = candidate;
                    else { count++; line = word; }
                }
                count++;
            }
            return count;
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            UIOriginalDialogChrome.DrawPictureWindow(this, batch, 0, 0,
                (int)Size.X, (int)Size.Y);
            base.Draw(batch);
        }
    }
}
