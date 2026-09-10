using FSO.Client;
using FSO.UI.Framework;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Simitone.Client.UI.Controls
{
    /// <summary>
    /// R119: the engine-wide DEFAULT text renderer — an MSDFFont family that
    /// draws ORIGINAL .ffn glyph tables (variablesans 07-20, byte-pinned by the
    /// 'uiglyph' gate) instead of MSDF vector fields. Assigned once at
    /// SimitoneGame.LoadContent; every consumer that reads
    /// TextStyle.VFont/GameFacade.VectorFont (labels, buttons, dialogs, lists,
    /// text edits, tooltips, headlines) renders original glyphs through the
    /// virtual Draw/MeasureString overrides below.
    ///
    /// Family layout mirrors the old SpriteFont GetNearest ladder:
    ///   - one table instance per native size; VectorScale = 11.5/px so the
    ///     TextStyle.Size formula yields Scale == 1.0 EXACTLY on ladder sizes
    ///     (native bitmap rendering, no scaling) and px/tablePx off-ladder;
    ///   - a root instance for direct GameFacade.VectorFont callers. The root
    ///     delegates to the _12 reference table at the caller's scale, so the
    ///     MeasureString/Draw pair (e.g. UIHeadlineRenderer centering) stays
    ///     self-consistent in base units, exactly like the MSDF font did.
    ///
    /// Engine-derived choices (disclosed, data is canon): the nearest-table
    /// ladder with ties to the smaller size; _48 is NOT in the ladder — it is a
    /// 16-glyph wordmark font (letters+space, no digits), so sizes above 20
    /// render as scaled _20; bold (_bs) and condensed (_s) tables stay on their
    /// explicit per-panel loaders rather than the default family.
    /// </summary>
    public class OriginalVectorFont : MSDFFont
    {
        // R140: 16 is RESTORED. The R139b exclusion ("variablesans_16 is
        // internally inconsistent") was an artifact of the port reading the
        // atlas stream 4 bytes early since R96 (FromFFN used shapeOff+12;
        // the engine's MacGIMEX_read samples at record+16 — see the R140
        // comment in UIOriginalText.cs). Under the corrected base every
        // table including _16 renders clean letterforms. The engine's font
        // factory (binary ~0x4B0200) builds "VariableSans_%02d" directly
        // from the requested size and explicitly maps BOTH 15 and 16 to the
        // 16 px table, so the full shipped ladder is canon: 07,08,09,10,11,
        // 12,14,16,18,20. Nearest-table fallback keeps ties-smaller (17→16).
        public static readonly int[] LadderPx = { 7, 8, 9, 10, 11, 12, 14, 16, 18, 20 };

        // gate/diagnostic counters
        public static int TablesMounted;
        public static int DrawCallsRendered;
        public static int GlyphsDrawn;
        public static int LastTablePx;
        public static float LastGlyphScale;

        public int TablePx { get; private set; }
        private string _path;
        private OriginalGlyphFont _font;

        private static OriginalVectorFont _root;
        private static OriginalVectorFont _ref;      // _12: the root's reference table
        private static OriginalVectorFont[] _ladder;

        private static SpriteBatch _batch;
        private static GraphicsDevice _batchGd;

        public static OriginalVectorFont CreateRoot()
        {
            if (_root != null) return _root;
            _ladder = new OriginalVectorFont[LadderPx.Length];
            for (int i = 0; i < LadderPx.Length; i++)
            {
                _ladder[i] = new OriginalVectorFont(LadderPx[i],
                    "Fonts\\variablesans_" + LadderPx[i].ToString("00") + ".ffn");
            }
            _ref = _ladder[Array.IndexOf(LadderPx, 12)];
            _root = new OriginalVectorFont(0, null) { VectorScale = 1f, Height = 20f, YOff = 16f };
            return _root;
        }

        private OriginalVectorFont(int px, string path)
        {
            TablePx = px;
            _path = path;
            if (px > 0)
            {
                // px == 0 marks the root; tables get base-unit metrics such that
                // MeasureString(x).Y * Scale ~= px + 3 (line height at the
                // requested size), matching the old font's behavior.
                VectorScale = 11.5f / px;
                Height = px + 3;
                YOff = px * 0.8f;
            }
        }

        public override MSDFFont SelectForSize(int pxSize)
        {
            var best = LadderPx[0];
            for (int i = 1; i < LadderPx.Length; i++)
                if (Math.Abs(LadderPx[i] - pxSize) < Math.Abs(best - pxSize))
                    best = LadderPx[i];   // strict < keeps the SMALLER size on ties
            var table = _ladder[Array.IndexOf(LadderPx, best)];
            // R139: NATIVE rendering at every requested size (engine decode:
            // cTSFont::TSDrawChar 0x4b3420 blits the glyph records' own pixel
            // extents — the ORIGINAL never scales bitmap glyphs; every surface
            // renders its nearest table 1:1). The wrapper keeps the ladder
            // table's glyph data and line-height metrics but absorbs the
            // requested px into VectorScale so TextStyle.Size computes
            // Scale == 1.0 EXACTLY: on-ladder requests are unchanged
            // (11.5/px of the table == 11.5/px of the request), off-ladder
            // requests now render the snapped table CRISP AND UNSCALED
            // instead of linearly-filtered — and MeasureString().Y * Scale
            // keeps the same (tablePx+3)*px/11.5 line height as before, so
            // panel layout metrics do not move.
            if (table.VectorScale == 11.5f / pxSize) return table;
            return new OriginalVectorFont(table.TablePx, table._path)
            {
                VectorScale = 11.5f / pxSize
            };
        }

        private OriginalGlyphFont EnsureLoaded(GraphicsDevice gd)
        {
            if (_font != null) return _font;
            if (gd == null) return null;
            var f = OriginalGlyphFont.Load(_path, gd);
            if (f != null && f.Atlas != null) { _font = f; TablesMounted++; }
            return _font;
        }

        public override Texture2D GetAtlas(GraphicsDevice gd)
        {
            var f = TablePx > 0 ? EnsureLoaded(gd) : null;
            return f != null ? f.Atlas : null;
        }

        public override Vector2 MeasureString(string text)
        {
            if (TablePx == 0)
            {
                // root: reference-table width in base units (self-consistent with
                // the root Draw below for direct Measure+Draw callers).
                var f = _ref.EnsureLoaded(GameFacade.GraphicsDevice);
                var w = f != null ? f.Measure(text) : (text == null ? 0 : text.Length * 5);
                return new Vector2(w, Height / VectorScale);
            }
            var tf = EnsureLoaded(GameFacade.GraphicsDevice);
            var tw = tf != null ? tf.Measure(text) : (string.IsNullOrEmpty(text) ? 0 : text.Length * 5);
            return new Vector2(tw, Height / VectorScale);
        }

        public override void Draw(GraphicsDevice gd, string text, Vector2 pos, Color color, Vector2 scale, Matrix? mat)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (TablePx == 0)
            {
                // root delegates to the reference table converted into that
                // table's units: rendered width == MeasureString * scale.
                var conv = 11.5f / 12f;
                _ref.Draw(gd, text, pos, color, new Vector2(scale.X * conv, scale.Y * conv), mat);
                return;
            }
            var f = EnsureLoaded(gd);
            if (f == null || f.Atlas == null) return;   // table not mountable yet (pre-content): draw nothing

            float s = scale.X > 0f ? scale.X : (scale.Y > 0f ? scale.Y : 1f);
            if (_batch == null || _batchGd != gd)
            {
                if (_batch != null) _batch.Dispose();
                _batch = new SpriteBatch(gd);
                _batchGd = gd;
            }
            _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null,
                RasterizerState.CullNone, null, mat);
            float x = pos.X;
            foreach (var ch0 in text)
            {
                var ch = OriginalGlyphFont.MapChar(ch0);
                OriginalGlyphFont.Glyph g;
                if (f.ByChar.TryGetValue(ch, out g) && g.W > 1)
                {
                    _batch.Draw(f.Atlas, new Vector2(x, pos.Y + f.GlyphY(g) * s),
                        new Rectangle(g.U, g.V, g.W, g.H), color, 0f, Vector2.Zero,
                        new Vector2(s, s), SpriteEffects.None, 0f);
                    GlyphsDrawn++;
                }
                x += f.Advance(ch) * s;
            }
            _batch.End();
            DrawCallsRendered++;
            LastTablePx = TablePx;
            LastGlyphScale = s;
        }
    }
}
