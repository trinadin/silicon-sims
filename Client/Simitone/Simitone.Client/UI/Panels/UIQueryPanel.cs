using FSO.Client;
using FSO.Client.UI.Framework;
using FSO.Content;
using FSO.Files.Formats.IFF.Chunks;
using FSO.LotView;
using FSO.LotView.Components;
using FSO.SimAntics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simitone.Client.UI.Controls;
using Simitone.Client.UI.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using FSO.Common;
using FSO.Common.Rendering.Framework.Model;
using FSO.UI.Panels;

namespace Simitone.Client.UI.Panels
{
    /// <summary>
    /// R124: the catalog item DESCRIPTION panel in the ORIGINAL composition. This
    /// replaces the FSO-authored modern chrome (query_title/cat_thumb_bg pngs,
    /// UIStyle rects, UILabels) wholesale — the panel is now built from canon:
    ///
    /// ART (UIGraphics.far, byte-pinned by the R124 'uidesc' gate):
    ///   cpanel\Backgrounds\PopupInfo.BMP  = kCatalogPopupBack, 559x127 rounded
    ///     plaque — the fixed top zone (preview/name/price/ratings);
    ///   cpanel\Backgrounds\PopupInfoTiles.bmp = kCatalogPopupBackTiles, 36x36,
    ///     each tile carrying its own left/right borders — the vertical FILL the
    ///     description body grows through (composition read from the art itself:
    ///     the plaque is one complete bordered panel, the tile is a border-carrying
    ///     body cell; see tools/iff-dump/r124/r124-art-canon.txt).
    ///
    /// STRINGS (UIText.iff STR# 160 'CatalogRatings', 20 English entries,
    /// sha-pinned by the gate): [0..6] are 'Hunger: %d'.. 'Room: %d' format
    /// strings — rendered VERBATIM by substituting %d (the old code stripped the
    /// last 2 chars and re-appended '{0}'); [7..13] are '+ Skill' lines keyed to
    /// OBJD RatingSkillFlags bits 0..6 (data-validated r124: chess=Logic 0x0004,
    /// guitar/easel=Creativity 0x0010, mirrors=Charisma 0x0020, computers=Study
    /// 0x0040). OBJD rating fields 80..86 are SIGNED and sit in exactly the
    /// caption order (coffee's Bladder = 0xFFF8 = -8).
    ///
    /// NEGATIVE SPACE (deliberate non-display): STR# 160 [14..19] ('Can only be
    /// used by adults/kids/pets/dogs/cats', 'Group Activity') have NO encoding
    /// anywhere in the corpus — MiscFlags(89) is all-zero except two FX objects,
    /// no OBJD field distinguishes usage restrictions (the only pet-clustered
    /// bits are the PETS SUBSORT placement bit f92.5 and pet dream flags f98,
    /// which doghouses/chew toys do NOT carry), CTSS is exactly name+description
    /// and its comments are Maxis authoring notes. Nothing is invented to force
    /// these captions on.
    ///
    /// OTHER DATA: name = CTSS[0], description = CTSS[1] (word-wrapped in the
    /// original dialog glyphs), price = OBJD price ('§' is IN the original font
    /// tables — r124 probe) — CTSS carries no price entry.
    ///
    /// ENGINE CHOICES (canon gives no (x;y) directives for this panel —
    /// disclosed): toolbar-anchored placement (unchanged from the port), panel
    /// width = the plaque's own 559, preview box inside the plaque's left side,
    /// text positions inside the plaque, tile tiling geometry, and the height
    /// tween semantics. Fonts: title = variablesans_08, ratings/price =
    /// variablesans_11, description = variablesans_09 (the R96-R106 .ffn set).
    /// </summary>
    public class UIQueryPanel : UICachedContainer
    {
        // R144: a UICachedContainer draws its (possibly stale) cache even at
        // Opacity 0 — the invisible query strip tinted the toolbar (survey:
        // webcam cells 2-3). Skip all painting once fully faded out.
        public override void PreDraw(UISpriteBatch batch)
        {
            if (!Visible || Opacity <= 0.01f) return;
            base.PreDraw(batch);
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible || Opacity <= 0.01f) return;
            base.Draw(batch);
        }


        public const string PlaqueMember = "cpanel\\Backgrounds\\PopupInfo.BMP";
        public const string TilesMember = "cpanel\\Backgrounds\\PopupInfoTiles.bmp";

        // gate instrumentation (R124)
        public static int CanonStringsLoaded = 0;   // STR# 160 tables read from the live chunk
        public static int OriginalMounts = 0;       // instances whose plaque art mounted

        public Texture2D Thumbnail;
        public bool Disposable;
        public UI3DThumb Thumb3D;
        public VMEntity ActiveEntity;
        public int Mode;

        // rendered canon text (gate reads these)
        public string NameText = "";
        public string PriceText = "";
        public string DescriptionText = "";
        public List<string> RatingLines = new List<string>();

        public bool Shown;
        private float _ShowPCT = 0f;
        public float ShowPCT
        {
            get { return _ShowPCT; }
            set
            {
                _ShowPCT = value;
                Size = new Vector2(Size.X, value * FullHeight + (1 - value) * 45);
                Y = -(5 + Size.Y);
            }
        }

        private bool _Active;
        public bool Active
        {
            get { return _Active; }
            set
            {
                if (_Active != value)
                {
                    if (!FSOEnvironment.SoftwareKeyboard)
                    {
                        // The 0.5s opacity tween came from Simitone's touch UI,
                        // not the TS1 cpanel. On desktop it could leave an old
                        // description fading over the replacement selection.
                        Opacity = value ? 1f : 0f;
                        Visible = value;
                    }
                    else GameFacade.Screens.Tween.To(this, 0.5f,
                        new Dictionary<string, float>() { { "Opacity", (value) ? 1f : 0f } }, TweenQuad.EaseOut);
                }
                _Active = value;
            }
        }

        public float FullHeight = 45;
        private World World;

        private UIOriginalText NameLabel;
        private UIOriginalText PriceLabel;
        private List<UIOriginalText> RatingLabels = new List<UIOriginalText>();
        private UIOriginalParagraph Description;
        private List<UIElement> TextChildren = new List<UIElement>();

        private static OriginalGlyphFont _TitleFont;
        private static OriginalGlyphFont _SmallFont;
        private static OriginalGlyphFont _DialogFont;

        public UIQueryPanel(World world)
        {
            World = world;
            Size = new Vector2(559, 45);
            Opacity = 0;
            InternalBefore = true;   // chrome (plaque/tiles/preview) under the text children
        }

        private static OriginalGlyphFont TitleFont()
        {
            if (_TitleFont == null) _TitleFont = OriginalGlyphFont.LoadTitle(GameFacade.GraphicsDevice);
            return _TitleFont;
        }
        private static OriginalGlyphFont SmallFont()
        {
            if (_SmallFont == null) _SmallFont = OriginalGlyphFont.LoadSmall(GameFacade.GraphicsDevice);
            return _SmallFont;
        }
        private static OriginalGlyphFont DialogFont()
        {
            if (_DialogFont == null) _DialogFont = OriginalGlyphFont.LoadDialog(GameFacade.GraphicsDevice);
            return _DialogFont;
        }

        public override void Update(UpdateState state)
        {
            Visible = Opacity > 0;
            if (Thumb3D != null && Visible) Invalidate();
            base.Update(state);
            // the paragraph builds plain-colored lines; keep them fading with the
            // panel opacity like every other label
            if (Description != null && Opacity < 1)
            {
                Description.TintLines(new Color(0xFF, 0xFF, 0xF0) * Opacity);
            }
        }

        public void SetShown(bool show)
        {
            if (!FSOEnvironment.SoftwareKeyboard)
            {
                // The inherited mobile reveal resized a 559x127 original
                // plaque through a 45px-tall crop. That is the half-panel seen
                // after the cursor entered the world to place a product:
                // UILotControl calls SetShown(false) while Active deliberately
                // remains true. Desktop TS1 swaps the complete composed surface
                // on/off; keep touch's authored 45px summary/expansion, but
                // never expose an intermediate crop in original desktop mode.
                Shown = show;
                ShowPCT = 1f;
                Opacity = show ? 1f : 0f;
                Visible = show;
                return;
            }
            if (Shown && show == Shown && FullHeight != Size.Y)
            {
                ShowPCT = Size.Y / FullHeight;
            }
            if (show)
            {
                GameFacade.Screens.Tween.To(this, 0.5f, new Dictionary<string, float>() { { "ShowPCT", 1f } }, TweenQuad.EaseOut);
            }
            else
            {
                GameFacade.Screens.Tween.To(this, 0.5f, new Dictionary<string, float>() { { "ShowPCT", 0f } }, TweenQuad.EaseOut);
            }
            Shown = show;
        }

        public void SetInfo(VM vm, VMEntity entity, bool bought)
        {
            ActiveEntity = entity;
            var obj = entity.Object;
            var def = entity.MasterDefinition;
            if (def == null) def = entity.Object.OBJ;

            CTSS catString = obj.Resource.Get<CTSS>(def.CatalogStringsID);
            NameText = catString != null ? catString.GetString(0) : "No information available.";
            DescriptionText = catString != null ? catString.GetString(1) : "";
            PriceText = "§" + entity.MultitileGroup.Price;

            // ratings: STR# 160 [0..6] verbatim 'X: %d' with the signed OBJD value;
            // skills: [7..13] verbatim '+ Skill' from RatingSkillFlags bits 0..6
            RatingLines = new List<string>();
            var ratings = new short[] { def.RatingHunger, def.RatingComfort, def.RatingHygiene,
                                        def.RatingBladder, def.RatingEnergy, def.RatingFun, def.RatingRoom };
            for (int i = 0; i < 7; i++)
            {
                if (ratings[i] != 0)
                    RatingLines.Add(GameFacade.Strings.GetString("160", i.ToString()).Replace("%d", ratings[i].ToString()));
            }
            var sFlags = def.RatingSkillFlags;
            for (int i = 0; i < 7; i++)
            {
                if ((sFlags & (1 << i)) > 0)
                    RatingLines.Add(GameFacade.Strings.GetString("160", (i + 7).ToString()));
            }
            CanonStringsLoaded++;

            if (entity is VMGameObject)
            {
                var objects = entity.MultitileGroup.Objects;
                ObjectComponent[] objComps = new ObjectComponent[objects.Count];
                for (int i = 0; i < objects.Count; i++)
                {
                    objComps[i] = (ObjectComponent)objects[i].WorldUI;
                }
                if (Thumbnail != null && Disposable) Thumbnail.Dispose();
                if (Thumb3D != null) Thumb3D.Dispose();
                Thumbnail = null; Thumb3D = null;
                if (FSOEnvironment.Enable3D)
                {
                    Thumb3D = new UI3DThumb(entity);
                }
                else
                {
                    var thumb = World.GetObjectThumb(objComps, entity.MultitileGroup.GetBasePositions(), GameFacade.GraphicsDevice);
                    Thumbnail = thumb;
                }
            }
            else
            {
                if (Thumbnail != null && Disposable) Thumbnail.Dispose();
                if (Thumb3D != null) Thumb3D.Dispose();
                Thumbnail = null; Thumb3D = null;
            }

            RebuildText();
            Disposable = true;
        }

        public void SetInfo(Texture2D thumb, string name, string description, int price, bool doDispose)
        {
            ActiveEntity = null;
            NameText = name;
            DescriptionText = description;
            PriceText = "§" + price;
            RatingLines = new List<string>();

            if (Thumbnail != null && Disposable) Thumbnail.Dispose();
            if (Thumb3D != null) Thumb3D.Dispose();
            Thumbnail = null; Thumb3D = null;
            Thumbnail = thumb;
            Disposable = doDispose;

            RebuildText();
        }

        /// <summary>Wraps like UIOriginalParagraph to predict the wrapped line count
        /// (the paragraph itself wraps lazily in Draw; the panel needs the height now).</summary>
        private static int CountWrappedLines(string text, OriginalGlyphFont font, float maxWidth)
        {
            if (font == null || string.IsNullOrEmpty(text)) return 0;
            int lines = 0;
            foreach (var para in (text ?? "").Replace("\r\n", "\n").Split('\n'))
            {
                var line = "";
                int n = 1;
                foreach (var word in para.Split(' '))
                {
                    var candidate = (line == "") ? word : line + " " + word;
                    if (font.Measure(candidate) <= maxWidth || line == "") line = candidate;
                    else { n++; line = word; }
                }
                lines += n;
            }
            return lines;
        }

        private void RebuildText()
        {
            foreach (var c in TextChildren) Remove(c);
            TextChildren.Clear();
            NameLabel = null; PriceLabel = null;
            RatingLabels = new List<UIOriginalText>();
            Description = null;

            var title = TitleFont();
            var small = SmallFont();
            var dialog = DialogFont();

            if (title != null)
            {
                NameLabel = new UIOriginalText(NameText, title) { Position = new Vector2(206, 12) };
                Add(NameLabel); TextChildren.Add(NameLabel);
            }
            if (small != null)
            {
                PriceLabel = new UIOriginalText(PriceText, small) { Position = new Vector2(206, 12 + 22) };
                Add(PriceLabel); TextChildren.Add(PriceLabel);

                // ratings block, right-aligned inside the plaque (engine choice)
                float y = 12;
                for (int i = 0; i < RatingLines.Count && i < 9; i++)
                {
                    var w = small.Measure(RatingLines[i]);
                    var l = new UIOriginalText(RatingLines[i], small)
                    {
                        Position = new Vector2(547 - w, y)
                    };
                    Add(l); TextChildren.Add(l); RatingLabels.Add(l);
                    y += 12;
                }
            }
            if (dialog != null && !string.IsNullOrEmpty(DescriptionText))
            {
                Description = new UIOriginalParagraph(dialog)
                {
                    Text = DescriptionText,
                    MaxWidth = 531,
                    LineHeight = 11f
                };
                Description.Position = new Vector2(14, 131);
                Add(Description); TextChildren.Add(Description);
            }

            int lines = CountWrappedLines(DescriptionText, dialog, 531);
            int tileRows = lines == 0 ? 0 : (lines * 11 + 12 + 35) / 36;
            FullHeight = 127 + tileRows * 36;
            ShowPCT = Shown ? 1f : 0f;
            Invalidate();
        }

        public override void Removed()
        {
            if (Thumbnail != null && Disposable) Thumbnail.Dispose();
            if (Thumb3D != null) Thumb3D.Dispose();
        }

        public override void InternalDraw(UISpriteBatch batch)
        {
            var plaqueRef = UIOriginal.EnsureResolved(PlaqueMember);
            var plaque = plaqueRef != null ? plaqueRef.Get(batch.GraphicsDevice) : null;
            if (plaque != null)
            {
                OriginalMounts++;
                DrawLocalTexture(batch, plaque, null, Vector2.Zero, Vector2.One);
            }
            var tilesRef = UIOriginal.EnsureResolved(TilesMember);
            var tiles = tilesRef != null ? tilesRef.Get(batch.GraphicsDevice) : null;
            // description body: the 36x36 border-carrying tile, laid across the
            // plaque width and down to the wrapped description's depth
            if (tiles != null && Size.Y > 127)
            {
                for (float ty = 127; ty < Size.Y; ty += 36)
                {
                    for (float tx = 0; tx < 559; tx += 36)
                    {
                        var src = new Rectangle(0, 0, (int)Math.Min(36, 559 - tx), (int)Math.Min(36, Size.Y - ty));
                        DrawLocalTexture(batch, tiles, src, new Vector2(tx, ty), Vector2.One);
                    }
                }
            }

            // preview: the selected object's own sprite, fitted into the plaque's
            // left side (engine choice; the original has no separate preview art)
            Texture2D thumb = null;
            if (Thumb3D != null)
            {
                Thumb3D.Draw();
                thumb = Thumb3D.Tex;
            }
            else if (Thumbnail != null)
            {
                thumb = Thumbnail;
            }
            if (thumb != null)
            {
                var scale = Math.Min(178f / thumb.Width, 106f / thumb.Height);
                var pos = new Vector2(10 + (178 - thumb.Width * scale) / 2, 10 + (106 - thumb.Height * scale) / 2);
                DrawLocalTexture(batch, thumb, null, pos, new Vector2(scale));
            }
        }
    }
}
