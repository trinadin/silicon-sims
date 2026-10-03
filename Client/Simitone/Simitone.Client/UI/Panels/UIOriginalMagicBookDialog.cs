using FSO.Client;
using FSO.Client.UI.Framework;
using FSO.Common.Rendering.Framework.Model;
using FSO.Content;
using FSO.SimAntics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Simitone.Client.UI.Controls;
using Simitone.Client.UI.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simitone.Client.UI.Panels
{
    /// <summary>
    /// Original CWinMagicBook (Makin' Magic dialog types 12/15), recovered from
    /// the PPC binary: TryDialog constructs CWinMagicBook(person, cookbook)
    /// modally and DISCARDS its result — the primitive returns TRUE. The window
    /// art is UIGraphics.far members pinned from the game's own Res_CPanel.h:
    /// kMagicBookBkg=82 (cpanel\Backgrounds\magicbookbkg.bmp, 620x400) and the
    /// five device pages 90-94 (SPELLBOOK_*.bmp, 130x90 each). Content comes
    /// from MagicMasterSpells.iff (the Magic Controller resource): STR#15/16
    /// are the spellbook display orders over STR#3 (adult wand spells) and
    /// STR#4 (children's), STR#5/6 the nectar-press/oven recipes, STR#2 the
    /// ingredient names. See coordination/evidence/EXP-08/leg3-magic-book.md.
    /// Fidelity notes (documented for review): the tab captions are derived
    /// (native reads the localized blob, ids 1/8/9/10/12/13/14), and the adult
    /// charm page binds the same STR#4/16 list as the kid page (only one
    /// charm getter exists natively).
    /// </summary>
    public class UIOriginalMagicBookDialog : UIContainer
    {
        public const int WindowW = 652, WindowH = 462;
        public override Vector2 Size { get => new Vector2(WindowW, WindowH); set { } }
        public override Rectangle GetBounds() => new Rectangle(0, 0, WindowW, WindowH);
        public const int BoardW = 620, BoardH = 400, BoardX = 16, BoardY = 8;
        public const int TabY = 16, TabX0 = 26, TabPitch = 120, TabW = 112;
        public const int ListX = 30, ListY = 58, ListWidth = 270, ListRows = 13;
        public const int DescX = 312, DescY = 58, DescWidth = 280, DescMaxLines = 14;
        public const int ArtX = 486, ArtY = 260;
        public const string BoardMember = "cpanel\\Backgrounds\\magicbookbkg.bmp";
        public static readonly string[] PageMembers =
        {
            "cpanel\\Backgrounds\\SPELLBOOK_WandCharger.bmp",
            "cpanel\\Backgrounds\\SPELLBOOK_CharmMakerAdult.bmp",
            "cpanel\\Backgrounds\\SPELLBOOK_CharmMakerKid.bmp",
            "cpanel\\Backgrounds\\SPELLBOOK_BakersOven.bmp",
            "cpanel\\Backgrounds\\SPELLBOOK_NectarPress.bmp",
        };
        public static readonly string[] PageCaptions =
            { "Wand Charger", "Charm Maker", "Kids' Magic", "Baker's Oven", "Nectar Press" };

        public const uint MagicControllerGuid = 0xB6C90029u;

        // probe surface (EXP-08 fix card 3 gate)
        public static int DialogsMounted, CloseClicks, ArtsMounted, PagesWithRows;
        public static string BoundTables = "", FirstWandTitle = "";
        public static readonly int[] RowsPerPage = new int[5];
        public static UIOriginalMagicBookDialog LastMounted;

        public event Action<int> OnResult;
        private readonly List<PageData> Pages = new List<PageData>();
        private int CurrentPage;
        private readonly Texture2D Board;
        private readonly Texture2D[] Arts = new Texture2D[5];
        private readonly UIBigButton[] Tabs = new UIBigButton[5];
        private readonly UIOriginalTextList SpellList;
        private readonly UIOriginalText[] DescLines;
        private readonly OriginalGlyphFont DescFont;
        private readonly List<UIOriginalText> TabLabels = new List<UIOriginalText>();

        private class PageData
        {
            public string Caption;
            public List<string> Titles = new List<string>();
            public List<string> Descriptions = new List<string>();
        }

        public UIOriginalMagicBookDialog()
        {
            Size = new Vector2(WindowW, WindowH);
            UpdatePosition();
            Board = UIOriginal.EnsureResolved(BoardMember)?.Get(GameFacade.GraphicsDevice);
            for (int i = 0; i < 5; i++)
            {
                Arts[i] = UIOriginal.EnsureResolved(PageMembers[i])?.Get(GameFacade.GraphicsDevice);
                if (Arts[i] != null) ArtsMounted++;
            }
            LoadContent();

            var listFont = OriginalGlyphFont.LoadByIndex(12, GameFacade.GraphicsDevice);
            DescFont = OriginalGlyphFont.LoadByIndex(11, GameFacade.GraphicsDevice);
            for (int i = 0; i < 5; i++)
            {
                var tab = new UIBigButton(false) { Caption = "", Width = TabW, Position = new Vector2(TabX0 + i * TabPitch, TabY) };
                int index = i;
                tab.OnButtonClick += _ => SelectPage(index);
                Add(tab);
                Tabs[i] = tab;
                var caption = Pages.Count > i ? Pages[i].Caption : PageCaptions[i];
                var label = new UIOriginalText(caption, DescFont)
                { Position = tab.Position + new Vector2((TabW - DescFont.Measure(caption)) / 2, DescFont.ButtonCaptionY(33)),
                    Color = new Color(195, 205, 205) };
                Add(label);
                TabLabels.Add(label);
            }
            SpellList = new UIOriginalTextList(listFont, ListWidth, ListRows)
            { Position = new Vector2(ListX, ListY), KeyboardActive = true };
            SpellList.OnSelectionChange += Describe;
            Add(SpellList);
            DescLines = new UIOriginalText[DescMaxLines];
            for (int i = 0; i < DescMaxLines; i++)
            {
                DescLines[i] = new UIOriginalText("", DescFont)
                { Position = new Vector2(DescX, DescY + i * (DescFont.LineHeight + 2)), Color = new Color(195, 205, 205) };
                Add(DescLines[i]);
            }

            var cancel = GameFacade.Strings.GetString("142", "1");
            if (string.IsNullOrEmpty(cancel)) cancel = "Close";
            var closeButton = new UIBigButton(false) { Caption = "", Width = 100, Position = new Vector2(20, WindowH - 49) };
            Add(closeButton);
            var closeLabel = new UIOriginalText(cancel, DescFont)
            { Position = closeButton.Position + new Vector2((100 - DescFont.Measure(cancel)) / 2, DescFont.ButtonCaptionY(33)),
                Color = new Color(195, 205, 205) };
            Add(closeLabel);
            closeButton.OnButtonClick += _ => Close();

            SelectPage(0);
            LastMounted = this;
            DialogsMounted++;
        }

        /// <summary>STR binding law (MagicMasterSpells.iff via the Magic
        /// Controller): pages = wand (STR#15 order over STR#3), charm + kid
        /// (STR#16 order over STR#4), oven (STR#6), nectar (STR#5); STR#2 is
        /// the ingredient-name table for the recipe pages.</summary>
        private void LoadContent()
        {
            var controller = Content.Get().WorldObjects.Get(MagicControllerGuid);
            var resource = controller?.Resource;
            FSO.Files.Formats.IFF.Chunks.STR S2 = null, S3 = null, S4 = null, S5 = null, S6 = null, S15 = null, S16 = null;
            if (resource != null)
            {
                S2 = resource.Get<FSO.Files.Formats.IFF.Chunks.STR>(2);
                S3 = resource.Get<FSO.Files.Formats.IFF.Chunks.STR>(3);
                S4 = resource.Get<FSO.Files.Formats.IFF.Chunks.STR>(4);
                S5 = resource.Get<FSO.Files.Formats.IFF.Chunks.STR>(5);
                S6 = resource.Get<FSO.Files.Formats.IFF.Chunks.STR>(6);
                S15 = resource.Get<FSO.Files.Formats.IFF.Chunks.STR>(15);
                S16 = resource.Get<FSO.Files.Formats.IFF.Chunks.STR>(16);
            }
            BoundTables = string.Join(",", new[] { S2, S3, S4, S5, S6, S15, S16 }
                .Select((s, i) => s != null ? new[] { 2, 3, 4, 5, 6, 15, 16 }[i].ToString() : null).Where(x => x != null));

            Pages.Add(OrderedPage(PageCaptions[0], S15, S3, S2));
            Pages.Add(OrderedPage(PageCaptions[1], S16, S4, S2));
            Pages.Add(OrderedPage(PageCaptions[2], S16, S4, S2));
            Pages.Add(RecipePage(PageCaptions[3], S6, S2));
            Pages.Add(RecipePage(PageCaptions[4], S5, S2));
            PagesWithRows = Pages.Count(p => p.Titles.Count > 0);
            for (int i = 0; i < 5 && i < Pages.Count; i++) RowsPerPage[i] = Pages[i].Titles.Count;
            FirstWandTitle = Pages.Count > 0 && Pages[0].Titles.Count > 0 ? Pages[0].Titles[0] : "";
        }

        private static PageData OrderedPage(string caption,
            FSO.Files.Formats.IFF.Chunks.STR order, FSO.Files.Formats.IFF.Chunks.STR recipes,
            FSO.Files.Formats.IFF.Chunks.STR ingredients)
        {
            var page = new PageData { Caption = caption };
            if (order == null || recipes == null) return page;
            for (int i = 0; i < 64; i++)
            {
                var value = order.GetString(i);
                if (string.IsNullOrEmpty(value)) continue;
                int index;
                if (!int.TryParse(value.Trim(), out index)) continue;
                var prose = (recipes.GetComment(index) ?? "").TrimStart('!', ' ', '\t');
                var recipe = recipes.GetString(index) ?? "";
                if (string.IsNullOrEmpty(prose) && string.IsNullOrEmpty(recipe)) continue;
                string title, body;
                var colon = prose.IndexOf(':');
                var query = prose.IndexOf('?');
                var cut = colon < 0 ? query : (query < 0 ? colon : Math.Min(colon, query));
                if (cut < 0)
                {
                    title = prose.Trim();
                    body = IngredientNames(recipe, ingredients);
                }
                else if (colon >= 0 && (query < 0 || colon < query))
                {
                    title = prose.Substring(0, colon).Trim();
                    body = prose.Substring(colon + 1).Trim();
                }
                else
                {
                    title = prose.Substring(0, query + 1).Trim();
                    body = prose.Substring(query + 1).Trim();
                }
                if (string.IsNullOrEmpty(title)) title = "Spell " + index;
                if (string.IsNullOrEmpty(body)) body = IngredientNames(recipe, ingredients);
                page.Titles.Add(title);
                page.Descriptions.Add(body);
            }
            return page;
        }

        private static PageData RecipePage(string caption,
            FSO.Files.Formats.IFF.Chunks.STR recipes, FSO.Files.Formats.IFF.Chunks.STR ingredients)
        {
            var page = new PageData { Caption = caption };
            if (recipes == null) return page;
            for (int i = 1; i < 32; i++)
            {
                var recipe = recipes.GetString(i);
                if (string.IsNullOrEmpty(recipe) || recipe.Trim() == "not used") continue;
                var note = (recipes.GetComment(i) ?? "").TrimStart('!', ' ', '\t').Trim();
                var names = IngredientNames(recipe, ingredients);
                if (names == "") continue;
                page.Titles.Add(note != "" ? note + ": " + names : names);
                page.Descriptions.Add(note != "" ? names : recipe);
            }
            return page;
        }

        private static string IngredientNames(string recipe, FSO.Files.Formats.IFF.Chunks.STR ingredients)
        {
            if (string.IsNullOrEmpty(recipe) || ingredients == null) return "";
            var parts = recipe.Split(',');
            var names = new List<string>();
            foreach (var part in parts)
            {
                int id;
                if (!int.TryParse(part.Trim(), out id)) continue;
                var name = (ingredients.GetString(id) ?? "").Trim();
                if (name != "") names.Add(name);
            }
            return string.Join(" + ", names);
        }

        public void SelectPage(int index)
        {
            if (index < 0 || index >= Pages.Count) return;
            CurrentPage = index;
            SpellList.SetItems(Pages[index].Titles);
            SpellList.Select(Pages[index].Titles.Count > 0 ? 0 : -1);
            Describe(Pages[index].Titles.Count > 0 ? 0 : -1);
        }

        private void Describe(int index)
        {
            var body = (index >= 0 && index < Pages[CurrentPage].Descriptions.Count)
                ? Pages[CurrentPage].Descriptions[index] : "";
            var lines = Wrap(body, DescWidth);
            for (int i = 0; i < DescMaxLines; i++)
                DescLines[i].Text = i < lines.Count ? lines[i] : "";
        }

        private List<string> Wrap(string text, int width)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text)) return lines;
            foreach (var paragraph in text.Replace("\r\n", "\n").Split('\n'))
            {
                var current = "";
                foreach (var word in paragraph.Split(' '))
                {
                    var candidate = current == "" ? word : current + " " + word;
                    if (DescFont.Measure(candidate) <= width || current == "") current = candidate;
                    else { lines.Add(current); current = word; }
                }
                if (current != "") lines.Add(current);
                if (lines.Count >= DescMaxLines) break;
            }
            return lines;
        }

        /// <summary>Probe/drive close: fires the same OnResult the Close
        /// button and Escape use (the engine consumes it as a plain OK —
        /// the native discards the book's result).</summary>
        public void CloseViaProbe() { Close(); }

        public void Close()
        {
            CloseClicks++;
            OnResult?.Invoke(0);
            UIScreen.RemoveDialog(this);
        }

        public void UpdatePosition()
        {
            var screen = GameFacade.Screens.CurrentUIScreen;
            Position = new Vector2((screen.ScreenWidth - WindowW) / 2f, (screen.ScreenHeight - WindowH) / 2f);
        }
        public override void GameResized() { UpdatePosition(); base.GameResized(); }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            if (state.NewKeys.Contains(Keys.Escape)) Close();
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            UIOriginalDialogChrome.DrawPictureWindow(this, batch, 0, 0, WindowW, WindowH);
            if (Board != null) DrawLocalTexture(batch, Board, new Vector2(BoardX, BoardY));
            if (Arts[CurrentPage] != null) DrawLocalTexture(batch, Arts[CurrentPage], new Vector2(ArtX, ArtY));
            base.Draw(batch);
        }
    }
}
