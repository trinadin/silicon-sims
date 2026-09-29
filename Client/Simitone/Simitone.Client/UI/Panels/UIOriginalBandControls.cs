using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simitone.Client.UI.Controls;
using Simitone.Client.UI.Model;
using Simitone.Client.UI.Panels.LiveSubpanels;
using System;
using System.Collections.Generic;

namespace Simitone.Client.UI.Panels
{
    /// <summary>
    /// R145: one engine cTSWinBtn sheet cell — SetImage(buf, 4, 1) law: the
    /// member is a 4-frame state sheet, per-state cell = sheet/4 wide at the
    /// sheet's own height (natural per-button sizes: build tools 25..43 wide,
    /// 28..41 tall; buy plaques 36x36; roof pitch icons 25x26). State 0 =
    /// normal, 1 = selected (engine SetState(1) on click, r145
    /// build-toolbar-law.md section 4).
    /// </summary>
    public class UIOriginalSheetButton : UIButton
    {
        public string Member;
        public int CellWidth;
        public byte State;
        public bool Hovered;   // R148 compatibility mirror; drawing reads UIButton's exact entered state.
        private Texture2D[] Frames;

        private static Texture2D ResolveTexture(string member)
        {
            try { return UIOriginal.EnsureResolved(member)?.Get(GameFacade.GraphicsDevice); }
            catch { return null; }
        }

        public UIOriginalSheetButton(string member)
            : base(WithFallback(ResolveTexture(member)))
        {
            Member = member;
            var tx = Texture;
            if (tx != null && tx.Width > 4)
            {
                // R148: construct on the REAL sheet so the base class derives
                // m_Width/m_Height/Region natively (ImageStates=4 => w/4 x h).
                // The old 1x1-white base left Size.Y == 1 forever (UIButton's
                // Size setter only assigns Width) — 1px-tall hit strips.
                ImageStates = 4;
                CellWidth = tx.Width / 4;
            }
            else
            {
                CellWidth = 30;
                Size = new Vector2(CellWidth, 24);
                ClickHandler.Region = new Rectangle(0, 0, CellWidth, 24);
            }
            OnButtonHover += (b) => Hovered = true;
            OnButtonExit += (b) => Hovered = false;
        }

        /// <summary>
        /// cTSWinBtn::CalcRowCol @0x50d300..0x50d348: four-column sheets
        /// keep the pressed/selected image while entered. The six-column
        /// branch has a separate entered+selected state; applying that law
        /// to four-column sheets incorrectly displays the disabled image.
        /// </summary>
        internal static int NativeFrameForProbe(bool disabled, bool entered, bool active)
        {
            if (disabled) return 3;
            return active ? 1 : entered ? 2 : 0;
        }

        internal int CurrentNativeFrameForProbe
        {
            get { return NativeFrameForProbe(Disabled, base.Hovered, State != 0 || IsDown); }
        }

        private static Texture2D WithFallback(Texture2D tx)
        {
            return tx != null ? tx : FSO.Common.Utils.TextureGenerator.GetPxWhite(GameFacade.GraphicsDevice);
        }

        private Texture2D FrameTex(int i)
        {
            if (Frames == null)
            {
                var tx = UIOriginal.EnsureResolved(Member)?.Get(GameFacade.GraphicsDevice);
                if (tx == null) { Frames = new Texture2D[1]; return null; }
                Frames = new Texture2D[4];
                for (int k = 0; k < 4; k++)
                    Frames[k] = UIOriginal.Rect(Member, k * CellWidth, 0, CellWidth, tx.Height);
            }
            if (i >= Frames.Length) i = 0;
            return Frames[i];
        }

        /// R145: swap the member + re-measure (the plaque row re-images the
        /// SAME buttons per sort state — cWinCatalog::LoadBooks re-SetImages +
        /// re-SetAreas the +0x3f8 array; buy-catalog-law.md section 1).
        public void Remount(string member)
        {
            if (member == Member) return;
            Member = member;
            Frames = null;
            State = 0;
            var tx = UIOriginal.EnsureResolved(Member)?.Get(GameFacade.GraphicsDevice);
            if (tx != null && tx.Width > 4)
            {
                // R148: the Texture setter re-derives m_Width/m_Height/Region
                // with the standing ImageStates=4 => (w/4 x h) — the sheet's
                // own cell size.
                Texture = tx;
                CellWidth = tx.Width / 4;
            }
            else
            {
                CellWidth = 30;
                Size = new Vector2(CellWidth, 24);
                ClickHandler.Region = new Rectangle(0, 0, CellWidth, 24);
            }
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            // UIButton.Hovered is the real cTSWinBtn entered bit. In
            // particular it clears while a held pointer leaves the button,
            // whereas OnButtonExit is intentionally not raised until release.
            var tex = FrameTex(CurrentNativeFrameForProbe);
            if (tex != null) DrawLocalTexture(batch, tex, null, Vector2.Zero, Vector2.One);
        }
    }

    /// <summary>
    /// R145: one engine ProductButton grid cell (custom-draw type 6 —
    /// THUMBNAIL ONLY, no text; name/price live in the hover popup). 45x45
    /// with the original ThumbTemplate frames (R122 art) and one 37x37 state
    /// cell drawn at natural size; roof swatches use the roof template (R130).
    /// Four-state build-subtool sheets are cropped to one authored state and
    /// drawn at their natural per-state size instead of fitting the full strip.
    /// </summary>
    public class UIOriginalCatalogCell : UIButton
    {
        public Texture2D Icon;
        public bool NaturalIcon;   // four-state subtool sheet: crop one authored cell
        public bool RoofSwatch;
        public bool PairedProductIcon;
        public int ProductIconStates = 1;
        public bool Selected;
        public bool Hovered;       // R148: ThumbTemplate frame 2 = hover cell
        public LiveSubpanels.UICatalogElement Element;

        private static Texture2D ResolveCellTexture()
        {
            try
            {
                LiveSubpanels.Catalog.UICatalogItem.LoadOriginalFrames();
                return LiveSubpanels.Catalog.UICatalogItem.OriginalFrame
                    ?? FSO.Common.Utils.TextureGenerator.GetPxWhite(GameFacade.GraphicsDevice);
            }
            catch { return FSO.Common.Utils.TextureGenerator.GetPxWhite(GameFacade.GraphicsDevice); }
        }

        public UIOriginalCatalogCell(LiveSubpanels.UICatalogElement elem)
            : base(ResolveCellTexture())
        {
            Element = elem;
            ImageStates = 1;
            // R148: construct on the 45x45 frame texture so Size.Y/GetBounds
            // are real (UIButton's Size setter only assigns Width — the old
            // 1x1-white base left every cell logically 45x1). Region pinned
            // explicitly for the frame-not-yet-loaded fallback.
            ClickHandler.Region = new Rectangle(0, 0, 45, 45);
            OnButtonHover += (b) => Hovered = true;
            OnButtonExit += (b) => Hovered = false;
            LiveSubpanels.Catalog.UICatalogItem.LoadOriginalFrames();
            LiveSubpanels.Catalog.UICatalogItem.LoadRoofFrames();
            try
            {
                Icon = (elem.Special?.Res != null) ? elem.Special.Res.GetIcon(elem.Special.ResID)
                    : GetObjIcon(elem.Item.GUID);
            }
            catch { }
            NaturalIcon = elem.Special?.Res is LiveSubpanels.Catalog.UIOriginalTerrainResProvider
                || elem.Special?.Res is LiveSubpanels.Catalog.UIOriginalRoofPitchResProvider
                || elem.Special?.Res is LiveSubpanels.Catalog.UIOriginalPoolWaterResProvider;
            RoofSwatch = elem.Special?.Res is LiveSubpanels.Catalog.UIOriginalRoofResProvider;
            PairedProductIcon = elem.Special?.Res == null
                || elem.Special?.Res is FSO.Client.UI.Controls.Catalog.UICatalogWallResProvider;
            ProductIconStates = NaturalIcon ? 4 : PairedProductIcon ? 2 : 1;
        }

        /// R148 (engine ProductButton::FGBlt 0x20b960-0x20bb54): the icon blits
        /// at NATURAL SIZE centered in the 45px cell — the engine never scales
        /// (src rect == dest size). Port law: never upscale; scale DOWN only
        /// when the icon exceeds the 41px interior.
        public static Vector2 FitSize(int w, int h)
        {
            return LiveSubpanels.Catalog.UICatalogItem.FitSize(w, h);
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            var norm = (RoofSwatch && LiveSubpanels.Catalog.UICatalogItem.RoofFrame != null)
                ? LiveSubpanels.Catalog.UICatalogItem.RoofFrame : LiveSubpanels.Catalog.UICatalogItem.OriginalFrame;
            var sel = (RoofSwatch && LiveSubpanels.Catalog.UICatalogItem.RoofFrameSel != null)
                ? LiveSubpanels.Catalog.UICatalogItem.RoofFrameSel : LiveSubpanels.Catalog.UICatalogItem.OriginalFrameSel;
            var hov = LiveSubpanels.Catalog.UICatalogItem.OriginalFrameHover;
            if (norm == null)
            {
                // R148: a failed frame load used to draw NOTHING (invisible,
                // unhoverable-looking cells) — fall back to the plain engine
                // cell face (navy plate, ThumbTemplate frame-0 family colors).
                DrawLocalTexture(batch, FSO.Common.Utils.TextureGenerator.GetPxWhite(GameFacade.GraphicsDevice), null,
                    Vector2.Zero, new Vector2(45, 45), new Color(0x00, 0x08, 0x52, 0xFF));
                // steel-blue face: PORT-AUTHORED fallback hue (only the navy
                // plate is ThumbTemplate frame-0 anchored; the face hue is
                // undecoded private-surface chrome)
                DrawLocalTexture(batch, FSO.Common.Utils.TextureGenerator.GetPxWhite(GameFacade.GraphicsDevice), null,
                    new Vector2(3, 3), new Vector2(39, 39), new Color(0x6b, 0xa5, 0xbd, 0xFF));
            }
            else
            {
                var frame = (Selected && sel != null) ? sel : (Hovered && hov != null) ? hov : norm;
                DrawLocalTexture(batch, frame, null, Vector2.Zero, Vector2.One);
            }
            if (NaturalIcon && Icon != null)
            {
                // Build subtool members are horizontal 4-state sheets. Pick
                // exactly one authored state; fitting the entire sheet made
                // four tiny icons run through the center of every button.
                var state = Selected ? 1 : Hovered ? 2 : 0;
                var source = LiveSubpanels.Catalog.UICatalogItem.IconStateSource(
                    Icon.Width, Icon.Height, ProductIconStates, state);
                // These are cTSWinBtn sheets, not product thumbnails. Preserve
                // the authored per-button extent up to the complete 45px cell
                // (Pool/Water are exactly 45x45); only larger/corrupt art may
                // down-fit. The ordinary 74x37 ProductButton path below keeps
                // its separate 41px-interior law.
                var fit = LiveSubpanels.Catalog.UICatalogItem.FitSize(
                    source.Width, source.Height, 45f);
                DrawLocalTexture(batch, Icon, source,
                    new Vector2((45 - fit.X) / 2, (45 - fit.Y) / 2),
                    new Vector2(fit.X / source.Width, fit.Y / source.Height));
                return;
            }
            if (Icon != null)
            {
                // Engine law: choose one horizontal product state, then blit
                // that cell at natural size; fit down only past 41px.
                var source = LiveSubpanels.Catalog.UICatalogItem.ProductIconSource(
                    Icon.Width, Icon.Height, PairedProductIcon, Selected || Hovered);
                var fit = FitSize(source.Width, source.Height);
                DrawLocalTexture(batch, Icon, source,
                    new Vector2((45 - fit.X) / 2, (45 - fit.Y) / 2),
                    new Vector2(fit.X / source.Width, fit.Y / source.Height));
            }
        }

        private static readonly Dictionary<uint, Texture2D> _icons = new Dictionary<uint, Texture2D>();
        public static Texture2D GetObjIcon(uint guid)
        {
            Texture2D t;
            if (_icons.TryGetValue(guid, out t)) return t;
            try
            {
                var obj = FSO.Content.Content.Get().WorldObjects.Get(guid);
                var bmp = obj?.Resource?.Get<FSO.Files.Formats.IFF.Chunks.BMP>(obj.OBJ.CatalogStringsID);
                if (bmp != null) t = bmp.GetTexture(GameFacade.GraphicsDevice);
            }
            catch { }
            _icons[guid] = t;
            return t;
        }
    }

    /// <summary>
    /// R145: the engine cWinCatalogPopup (ctor 0x26e620) — the catalog HOVER
    /// info window, parented to the MAIN view (never the toolbar) and placed
    /// ABOVE-LEFT of the hovered button over the 3D view (BuildMyBuffer
    /// placement walk 0x26dd98-0x26dea4; r145 build-toolbar-law.md section 6).
    /// Composition: icon pane (0,0,168,127) + text pane from x168; the name —
    /// price line in font[10] at text origin +(10,12), description in font[8]
    /// word-wrapped below. The 14 rating lines (STR# 160) are a disclosed
    /// residual (the port catalog carries no per-item ratings data).
    /// </summary>
    public class UIOriginalCatalogPopup : UIContainer
    {
        public const int POPUP_W = 559;
        public const int POPUP_H = 127;
        public const int ICON_PANE_W = 168;
        public const int TEXT_LEFT = 178;
        public const int TEXT_RIGHT = 540;
        public const int DESCRIPTION_MAX_WIDTH = TEXT_RIGHT - TEXT_LEFT;

        // UIElement's default Size implementation intentionally returns zero
        // and ignores assignments; this compositor needs a real measured box
        // because both drawing and the above-band anchor consume its height.
        private Vector2 _Size = new Vector2(POPUP_W, POPUP_H);
        public override Vector2 Size
        {
            get { return _Size; }
            set { _Size = value; }
        }

        // Public law probes for the catalog regression gate.
        public int DescriptionMaxWidth => DESCRIPTION_MAX_WIDTH;
        public int DescriptionLineCount { get; private set; }
        public int DescriptionTop { get; private set; }
        public int DescriptionLineHeight { get; private set; }
        public int MeasuredHeight => (int)Size.Y;

        private UIOriginalText NameLabel;
        private UIOriginalText PriceLabel;
        private UIOriginalParagraph Desc;
        private Texture2D Icon;
        public bool OwnsIcon { get; private set; }
        private bool PairedProductIcon;
        public float Slide;   // engine slide-in ramp 0..1

        // UI-13: BuildMyBuffer's rating block (0x26d448 loop) draws 14 rows
        // under the description: GetCatalogRating(product, catalog, i) for
        // i in 0..13 with the STR#160 labels cached at popup+272[0..13] —
        // base index 0 (the i==7 special case is exactly the motive→skill
        // boundary: [0..6] 'X: %d' formats, [7..13] '+ Skill' strings),
        // matching the query panel's indexing; r145's "[1..14]" was off by
        // one. STR#160 [14..19] (usage flags) have no corpus encoding (r124
        // negative space) and are not ported.
        private List<UIOriginalText> RatingLabels;

        // UI-12 (tools/iff-dump/catalog-popup-slide-law.md): EnablePopup
        // 0x26e000 anchors the popup, then a SetupConstantTimeRamp
        // (start 0.0, target popupY−const, 250 ms) drives the DRAWN Y from
        // the source row (engine field +0x80) up to the anchor, linear.
        private long _slideStartTicks = -1;   // -1 = settled
        private float _slideFromY;

        public void BeginSlideIn(float fromY)
        {
            _slideFromY = fromY;
            _slideStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            Slide = 0f;
        }

        public UIOriginalCatalogPopup()
        {
            Size = new Vector2(POPUP_W, POPUP_H);
        }

        public void SetInfo(Texture2D icon, string name, int price, string desc)
        {
            SetInfo(icon, false, name, price, desc, false);
        }

        public void SetInfo(Texture2D icon, bool pairedProductIcon, string name, int price, string desc)
        {
            SetInfo(icon, pairedProductIcon, name, price, desc, false);
        }

        public void SetInfo(Texture2D icon, bool pairedProductIcon, string name, int price, string desc,
            bool ownsIcon, bool affordable = true, IList<string> ratingLines = null)
        {
            ReplaceIcon(icon, ownsIcon);
            PairedProductIcon = pairedProductIcon;
            Size = new Vector2(POPUP_W, POPUP_H);
            DescriptionLineCount = 0;
            DescriptionTop = 0;
            DescriptionLineHeight = 0;
            var gd = GameFacade.GraphicsDevice;
            var f10 = OriginalGlyphFont.LoadByIndex(10, gd);
            var f8 = OriginalGlyphFont.LoadByIndex(8, gd);
            if (NameLabel != null) { Remove(NameLabel); NameLabel = null; }
            if (PriceLabel != null) { Remove(PriceLabel); PriceLabel = null; }
            if (Desc != null) { Remove(Desc); Desc = null; }
            if (RatingLabels != null)
            {
                foreach (var rl in RatingLabels) if (rl != null) Remove(rl);
                RatingLabels = null;
            }
            if (f10 != null && name != null)
            {
                NameLabel = new UIOriginalText(name, f10) { Position = new Vector2(ICON_PANE_W + 10, 12) };
                if (price > 0)
                {
                    // BuildMyBuffer advances exactly four pixels after the
                    // measured product name before drawing the price string.
                    PriceLabel = new UIOriginalText("§" + price, f10)
                    {
                        Position = new Vector2(ICON_PANE_W + 10 + f10.Measure(name) + 4, 12)
                    };
                }
                // UI-12: BuildMyBuffer (0x26dccc-0x26dd94) tints the name and
                // price strings with the engine error color when the family
                // cannot afford the product (R198 SetColor(1)); the port had
                // the color pinned with no live consumer.
                if (!affordable)
                {
                    NameLabel.Color = UILotControl.TooltipErrorColor;
                    if (PriceLabel != null) PriceLabel.Color = UILotControl.TooltipErrorColor;
                }
                Add(NameLabel);
                if (PriceLabel != null) Add(PriceLabel);
            }
            var contentBottom = 0f;
            var haveText = false;
            if (f8 != null && desc != null)
            {
                var descTop = 12 + (f10 != null ? f10.LineHeight : 19);
                Desc = new UIOriginalParagraph(f8)
                {
                    Text = desc,
                    MaxWidth = DESCRIPTION_MAX_WIDTH,
                    LineHeight = f8.LineHeight
                };
                Desc.Position = new Vector2(ICON_PANE_W + 10, descTop);
                Add(Desc);

                // BuildMyBuffer keeps 127px as a minimum, then grows to the
                // measured description bottom plus the 12px lower gutter.
                var lines = CountWrappedLines(desc, f8, Desc.MaxWidth);
                DescriptionLineCount = lines;
                DescriptionTop = descTop;
                DescriptionLineHeight = f8.LineHeight;
                contentBottom = descTop + lines * f8.LineHeight;
                haveText = true;

                // UI-13: the rating block follows the description in the same
                // font (BuildMyBuffer composes both into one buffer).
                if (f8 != null && ratingLines != null && ratingLines.Count > 0)
                {
                    RatingLabels = new List<UIOriginalText>();
                    var ratingTop = contentBottom;
                    foreach (var line in ratingLines)
                    {
                        var label = new UIOriginalText(line, f8)
                        {
                            Position = new Vector2(ICON_PANE_W + 10, ratingTop + RatingLabels.Count * f8.LineHeight)
                        };
                        RatingLabels.Add(label);
                        Add(label);
                    }
                    contentBottom = ratingTop + ratingLines.Count * f8.LineHeight;
                }
            }
            if (haveText)
            {
                Size = new Vector2(POPUP_W, Math.Max(POPUP_H, contentBottom + 12));
            }
        }

        private void ReplaceIcon(Texture2D icon, bool ownsIcon)
        {
            if (ReferenceEquals(Icon, icon))
            {
                // Never relinquish ownership of the same live texture merely
                // because a repeated caller describes it as borrowed.
                OwnsIcon = OwnsIcon || ownsIcon;
                return;
            }
            ReleaseOwnedIcon();
            Icon = icon;
            OwnsIcon = ownsIcon && icon != null;
        }

        private void ReleaseOwnedIcon()
        {
            if (Icon != null && OwnsIcon)
            {
                try { Icon.Dispose(); }
                catch { }
            }
            Icon = null;
            OwnsIcon = false;
        }

        public void ClearIcon()
        {
            ReleaseOwnedIcon();
            PairedProductIcon = false;
        }

        public override void Removed()
        {
            ReleaseOwnedIcon();
            base.Removed();
        }

        private static int CountWrappedLines(string text, OriginalGlyphFont font, float maxWidth)
        {
            if (font == null || string.IsNullOrEmpty(text)) return 0;
            var result = 0;
            foreach (var paragraph in text.Replace("\r\n", "\n").Split('\n'))
            {
                var line = "";
                foreach (var word in paragraph.Split(' '))
                {
                    var candidate = line.Length == 0 ? word : line + " " + word;
                    if (line.Length == 0 || font.Measure(candidate) <= maxWidth) line = candidate;
                    else { result++; line = word; }
                }
                result++;
            }
            return result;
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            // UI-12: the decoded slide ramp shifts only the DRAWN Y;
            // Position (the R148 anchor law) stays readable at the anchor
            // between frames — restored after the children draw.
            var anchor = Position;
            if (_slideStartTicks >= 0)
            {
                var elapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - _slideStartTicks)
                    / (double)System.Diagnostics.Stopwatch.Frequency;
                Slide = (float)Math.Min(elapsed / 0.25, 1.0);
                if (Slide >= 1f) _slideStartTicks = -1;
            }
            Position = new Vector2(anchor.X, anchor.Y + (1f - Slide) * (_slideFromY - anchor.Y));
            // the engine composes into a private 16bpp surface; the port draws
            // the equivalent dark panel directly (surface chrome undecoded).
            DrawLocalTexture(batch, FSO.Common.Utils.TextureGenerator.GetPxWhite(GameFacade.GraphicsDevice), null,
                Vector2.Zero, Size, new Color(0x10, 0x10, 0x2c, 0xf2));
            DrawLocalTexture(batch, FSO.Common.Utils.TextureGenerator.GetPxWhite(GameFacade.GraphicsDevice), null,
                new Vector2(ICON_PANE_W, 0), new Vector2(1, Size.Y), new Color(0x6f, 0x6f, 0x9c, 0xff));
            if (Icon != null)
            {
                var source = LiveSubpanels.Catalog.UICatalogItem.ProductIconSource(
                    Icon.Width, Icon.Height, PairedProductIcon, false);
                // Ordinary object previews arrive already composed through
                // World.GetObjectThumb (Product::DrawIcon's type-1 analogue).
                // Type-5 special-provider art is naturally sized. Center both
                // in the complete 168x127 icon rect, down-fitting only when the
                // composed result is genuinely larger than that pane.
                var s = Math.Min(1f, Math.Min(ICON_PANE_W / (float)source.Width,
                    POPUP_H / (float)source.Height));
                // cWinCatalogPopup composes to integer surface coordinates.
                // Integer placement also avoids half-pixel linear-filter blur.
                var iconPos = new Vector2(
                    (int)((ICON_PANE_W - source.Width * s) / 2),
                    (int)((POPUP_H - source.Height * s) / 2));
                DrawLocalTexture(batch, Icon, source,
                    iconPos, new Vector2(s));
            }
            base.Draw(batch);
            Position = anchor;
        }
    }
}
