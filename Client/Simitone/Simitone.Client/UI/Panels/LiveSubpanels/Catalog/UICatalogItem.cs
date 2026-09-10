using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Controls.Catalog;
using FSO.Client.UI.Framework;
using FSO.Content;
using FSO.Files.Formats.IFF.Chunks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simitone.Client.UI.Controls;
using Simitone.Client.UI.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simitone.Client.UI.Panels.LiveSubpanels.Catalog
{
    public class UICatalogItem : UITSContainer
    {
        public static Dictionary<uint, Texture2D> IconCache = new Dictionary<uint, Texture2D>();
        // R122: the ORIGINAL catalog item frames (kCatalogTemplate1Frame = a
        // single 45x45 frame; the selected state crops frame 1 of the 4-frame
        // kCatalogTemplate sheet — frame order read with the UIButton convention,
        // our disclosed choice). Icons scale to fit the 45px cell.
        public static Texture2D OriginalFrame;
        public static Texture2D OriginalFrameSel;
        public static Texture2D OriginalFrameHover;
        public static int OriginalFrameLoads;
        public static void LoadOriginalFrames()
        {
            // R148: retry until the frames actually mount — the old one-shot
            // counter permanently blanked every grid cell if the first attempt
            // ran before content was ready (swallowed exception). Frame 2 of
            // the kCatalogTemplate sheet = the hover cell (UIButton convention).
            if (OriginalFrame != null && OriginalFrameSel != null && OriginalFrameHover != null) return;
            OriginalFrameLoads++;
            try
            {
                OriginalFrame = Simitone.Client.UI.Model.UIOriginal.Frame("cpanel\\Catalog\\ThumbTemplate1Frame.BMP", 0);
                OriginalFrameSel = Simitone.Client.UI.Model.UIOriginal.Frame("cpanel\\Catalog\\ThumbTemplate.BMP", 1);
                OriginalFrameHover = Simitone.Client.UI.Model.UIOriginal.Frame("cpanel\\Catalog\\ThumbTemplate.BMP", 2);
            }
            catch { }
        }
        // R130: the ROOF category's own cell template — kRoofPatternTemplate
        // (a 4-frame 180x45 sheet; frame 0 = normal, frame 1 = selected, our
        // disclosed choice following the ThumbTemplate convention). The
        // original composes the roof pattern swatch inside THIS frame, not
        // the generic Catalog ThumbTemplate.
        public static Texture2D RoofFrame;
        public static Texture2D RoofFrameSel;
        public static int RoofFrameLoads;
        public static void LoadRoofFrames()
        {
            if (RoofFrameLoads > 0) return;
            RoofFrameLoads++;
            try
            {
                RoofFrame = Simitone.Client.UI.Model.UIOriginal.Frame("cpanel\\Build\\RoofPatternTemplate.bmp", 0);
                RoofFrameSel = Simitone.Client.UI.Model.UIOriginal.Frame("cpanel\\Build\\RoofPatternTemplate.bmp", 1);
            }
            catch { }
        }
        public Texture2D BG;
        public Texture2D Icon;
        public Texture2D Outline;
        public bool Outlined;
        // TS1 object catalog BMPs and the wall-style BMPs are two horizontal
        // 37x37 state cells. The original terrain, pool/water and roof-pitch
        // subtools are FOUR-state sheets instead (normal/selected/hover/
        // disabled). Keep the count as data: drawing the complete special
        // sheet is what produced the repeated, crushed build-tool strips.
        public bool PairedProductIcon;
        public int ProductIconStates = 1;
        // Provider-typed four-state subtool art, not an aspect heuristic.
        // Each selected state cell is drawn at natural size; the complete
        // 144..180px sheet must never be scale-fit as one image.
        public bool NaturalIcon;
        // R130: roof PATTERN swatches compose inside the roof category's own
        // RoofPatternTemplate frame instead of the generic Catalog frames.
        public bool RoofSwatch;

        public UILabel PriceLabel;
        private UIBuyBrowsePanel BudgetProvider;

        public override void Draw(UISpriteBatch SBatch)
        {
            if (!Visible) return;
            if (NaturalIcon && Icon != null)
            {
                var source = IconStateSource(Icon.Width, Icon.Height,
                    ProductIconStates, Outlined ? 1 : 0);
                var fit = FitSize(source.Width, source.Height, 45f);
                DrawLocalTexture(SBatch, Icon, source,
                    new Vector2((90 - fit.X) / 2f, (105 - fit.Y) / 2f),
                    new Vector2(fit.X / source.Width, fit.Y / source.Height));
                base.Draw(SBatch);
                return;
            }
            if (OriginalFrame != null || (RoofSwatch && RoofFrame != null))
            {
                // R122: original 45x45 thumb cell — frame at native size, icon
                // scaled to fit inside, centered; selected state swaps the frame.
                // R130: roof pattern swatches use the roof category's OWN
                // template frames (kRoofPatternTemplate) instead.
                var norm = (RoofSwatch && RoofFrame != null) ? RoofFrame : OriginalFrame;
                var sel = (RoofSwatch && RoofFrame != null) ? RoofFrameSel : OriginalFrameSel;
                var fpos = new Vector2((90 - norm.Width) / 2f, (105 - norm.Height) / 2f);
                var frame = (Outlined && sel != null) ? sel : norm;
                DrawLocalTexture(SBatch, frame, null, fpos, Vector2.One, Color.White);
                if (Icon != null)
                {
                    var source = ProductIconSource(Icon.Width, Icon.Height, PairedProductIcon, Outlined);
                    var fit = FitSize(source.Width, source.Height);
                    DrawLocalTexture(SBatch, Icon, source,
                        fpos + new Vector2((norm.Width - fit.X) / 2f, (norm.Height - fit.Y) / 2f),
                        new Vector2(fit.X / source.Width, fit.Y / source.Height));
                }
                base.Draw(SBatch);
                return;
            }
            DrawLocalTexture(SBatch, BG, null, new Vector2(BG.Width-90, BG.Height-105) / -2, Vector2.One, new Color(104, 164, 184, 255));
            var iconSize = 55f;
            if (Icon != null)
            {
                
                if (Icon.Width / (float)Icon.Height < 1.1f || Icon.Width == 127 || Icon.Width == 128)
                {
                    iconSize = 77.7f;
                    var scale = iconSize/(float)Math.Sqrt(Icon.Width * Icon.Width + Icon.Height * Icon.Height);
                    DrawLocalTexture(SBatch, Icon, new Rectangle(0, 0, Icon.Width, Icon.Height), new Vector2((Icon.Width*scale-90) / -2, (Icon.Height*scale-105) / -2), new Vector2(scale));
                }
                else DrawLocalTexture(SBatch, Icon, new Rectangle(0, 0, Icon.Width / 2, Icon.Height), new Vector2((iconSize-90) / -2, (iconSize- 105) / -2), new Vector2(iconSize / Icon.Height, iconSize / Icon.Height));
            }

            if (Outlined) DrawLocalTexture(SBatch, Outline, null, new Vector2(Outline.Width - 90, Outline.Height - 105) / -2, Vector2.One, UIStyle.Current.ActiveSelection);
            base.Draw(SBatch);
        }

        public UICatalogItem(UICatalogElement elem, UIBuyBrowsePanel budgetProvider)
        {
            LoadOriginalFrames();
            BG = Content.Get().CustomUI.Get("pswitch_icon_bg.png").Get(GameFacade.GraphicsDevice);
            Icon = (elem.Special?.Res != null) ? elem.Special.Res.GetIcon(elem.Special.ResID) : GetObjIcon(elem.Item.GUID);
            PairedProductIcon = elem.Special?.Res == null
                || elem.Special?.Res is UICatalogWallResProvider;
            Outline = Content.Get().CustomUI.Get("pswitch_icon_sel.png").Get(GameFacade.GraphicsDevice);
            NaturalIcon = elem.Special?.Res is UIOriginalTerrainResProvider
                || elem.Special?.Res is UIOriginalRoofPitchResProvider
                || elem.Special?.Res is UIOriginalPoolWaterResProvider;
            ProductIconStates = NaturalIcon ? 4 : PairedProductIcon ? 2 : 1;
            RoofSwatch = elem.Special?.Res is UIOriginalRoofResProvider;
            LoadRoofFrames();

            PriceLabel = new UILabel();
            PriceLabel.Alignment = TextAlignment.Center | TextAlignment.Middle;
            PriceLabel.Position = new Vector2(0, 110);
            PriceLabel.Size = new Vector2(90, 1);
            PriceLabel.CaptionStyle = PriceLabel.CaptionStyle.Clone();
            PriceLabel.CaptionStyle.Color = UIStyle.Current.Text;
            PriceLabel.CaptionStyle.Size = 14;
            PriceLabel.Caption = "§" + elem.Item.Price.ToString();
            PriceLabel.Visible = !NaturalIcon;
            Add(PriceLabel);

            BudgetProvider = budgetProvider;
        }

        /// <summary>
        /// ProductButton::ForegroundBlt (0x20ba24-0x20bad4) halves the source
        /// width whenever the product button has more than one image state and
        /// advances by one half for its alternate state. The shipped catalog
        /// BMP corpus is uniformly 74x37, so each displayed object cell is the
        /// natural 37x37 half, never the complete pair sheet.
        /// </summary>
        public static Rectangle ProductIconSource(int width, int height, bool paired, bool alternate)
        {
            return IconStateSource(width, height, paired ? 2 : 1, alternate ? 1 : 0);
        }

        /// <summary>
        /// Select one horizontal state from original ProductButton/cTSWinBtn
        /// art. State sheets in this surface are authored with equal-width
        /// cells; clamping keeps a corrupt/fallback texture safely drawable.
        /// </summary>
        public static Rectangle IconStateSource(int width, int height, int states, int state)
        {
            if (width <= 0 || height <= 0) return Rectangle.Empty;
            states = Math.Max(1, states);
            var cellWidth = width / states;
            if (cellWidth <= 0) { states = 1; cellWidth = width; }
            state = Math.Max(0, Math.Min(states - 1, state));
            return new Rectangle(state * cellWidth, 0, cellWidth, height);
        }

        /// <summary>Natural-size ProductButton blit, shrinking only oversized art.</summary>
        public static Vector2 FitSize(int width, int height, float maximum = 41f)
        {
            var extent = Math.Max(width, height);
            if (extent <= maximum || extent == 0) return new Vector2(width, height);
            var scale = maximum / extent;
            return new Vector2(width * scale, height * scale);
        }

        public override void Selected()
        {
            if (BudgetProvider.ItemID == ItemID)
            {
                // Re-clicking the already-selected item: deselect it
                Outlined = false;
                BudgetProvider.Deselect();
            }
            else
            {
                Outlined = true;
                BudgetProvider.Selected(ItemID);
            }
        }

        public override void Deselected()
        {
            Outlined = false;
        }

        public Texture2D GetObjIcon(uint GUID)
        {
            if (!IconCache.ContainsKey(GUID))
            {
                var obj = Content.Get().WorldObjects.Get(GUID);
                if (obj == null)
                {
                    IconCache[GUID] = null;
                    return null;
                }
                var bmp = obj.Resource.Get<BMP>(obj.OBJ.CatalogStringsID);
                if (bmp != null) IconCache[GUID] = bmp.GetTexture(GameFacade.GraphicsDevice);
                else IconCache[GUID] = null;
            }
            return IconCache[GUID];
        }

        public static void ClearIconCache()
        {
            foreach (var item in IconCache)
            {
                item.Value?.Dispose();
            }
            IconCache.Clear();
        }
    }
}
