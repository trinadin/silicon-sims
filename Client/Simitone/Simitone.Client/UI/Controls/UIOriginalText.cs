using FSO.Client;
using FSO.Client.UI.Framework;
using FSO.Common.Rendering.Framework.Model;
using FSO.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace Simitone.Client.UI.Controls
{
    /// <summary>
    /// R96: text rendered from the ORIGINAL .ffn glyph tables (UIGraphics.far,
    /// byte-pinned 1:1 by the R87 'uiglyph' gate). FNTF decode, verified against
    /// the original files (tools/iff-dump/r96/ has the format evidence):
    ///
    /// header (32 bytes, little-endian):
    ///   char[4] "FNTF"; u32 fileSize; u16 version (101 = 1.01);
    ///   u16 numChars; u32 flags; u8 centerX, centerY, ascent, descent;
    ///   u32 charTableOffset (32); u32 kerningOffset (0 here); u32 shapeOffset.
    ///
    /// char table, numChars x 11 bytes:
    ///   u16 charCode; u8 width; u8 height; u16 u, v (top-left in the atlas);
    ///   i8 tail0; i8 tail1; i8 tail2 (tail2 = the y draw offset — large for
    ///   x-height glyphs, 1-2 for caps; verified on both sample fonts).
    ///   tail0/tail1 semantics vary per font (advance for the 07pt table,
    ///   bearing-like small values for the 14pt table where advance = width +
    ///   tail0 gives coherent 14pt metrics) — DISCLOSED interpretation.
    ///
    /// shape record at shapeOffset (R140, engine-decoded):
    ///   u8 recordId (0x7A, not compressed); u8[3] nextOffset; u16 atlasW;
    ///   u16 atlasH; u32 0; u32 0 — a 16-BYTE record header, then the atlas
    ///   as 4-BIT grayscale nibbles (high nibble first at even x), rows
    ///   byte-aligned: value 0 = transparent, 15 = full ink. The atlas bytes
    ///   end exactly at EOF (len == shapeOffset + 16 + W*H/2). Engine proof:
    ///   MacGIMEX_read (binary 0xB9B0) samples at record + 16 with rowBytes
    ///   = atlasW/2; MacGIMEX_open (0xC210) byte-swaps the u16s at +4/+6
    ///   and the u32s at +8/+12 of this record on load.
    /// </summary>
    public class OriginalGlyphFont
    {
        public struct Glyph
        {
            public ushort Char; public byte W, H; public ushort U, V;
            public sbyte T0, T1, T2;
        }

        public static int FontsParsed = 0;

        public const string DefaultFontPath = "Fonts\\variablesans_14.ffn";
        // R97: the smaller caption table (hover lot labels) — also R87-pinned.
        public const string CaptionFontPath = "Fonts\\variablesans_10.ffn";
        // R98: the BOLD table (lot-screen money readout) — also R87-pinned.
        public const string BoldFontPath = "Fonts\\variablesans_12_bs.ffn";
        // R169: cWinTextWrap's font-table slot 12 is the regular table. The
        // separate _12_bs face is not selected by that factory path.
        public const string Regular12FontPath = "Fonts\\variablesans_12.ffn";
        // R99: the SMALL table (toolbar time/floor/friends readouts) — R87-pinned.
        public const string SmallFontPath = "Fonts\\variablesans_11.ffn";
        // R100: the DIALOG table (cheat-bar response messages) — R87-pinned.
        public const string DialogFontPath = "Fonts\\variablesans_09.ffn";
        // R106: the TITLE table (UIDialog captions) — R87-pinned.
        public const string TitleFontPath = "Fonts\\variablesans_08.ffn";

        public Glyph[] Glyphs;
        public Dictionary<ushort, Glyph> ByChar;
        public Texture2D Atlas;
        public int AtlasW, AtlasH;
        public int NumChars, Version, Ascent, Descent;

        /// <summary>
        /// cTSFont::TSCharHeight returns the maximum bitmapped-glyph bottom stored
        /// in the font object. The FFN header ascent/descent bytes are zero in the
        /// shipped tables, so reproduce InitBitmapped's maximum (glyph height plus
        /// its vertical draw offset) from the decoded cells instead.
        /// </summary>
        public int LineHeight
        {
            get
            {
                int bottom = 1;
                int top = 0;
                if (Glyphs != null)
                    foreach (var glyph in Glyphs)
                    {
                        // cTSFont::InitBitmapped skips the exceptional NBSP
                        // container record (character 160) and empty cells.
                        if (glyph.Char == 160 || glyph.W == 0 || glyph.H == 0) continue;
                        bottom = System.Math.Max(bottom, glyph.H + glyph.T2);
                        top = System.Math.Min(top, glyph.T2);
                    }
                return bottom - top;
            }
        }

        // InitBitmapped 0x4b4640–47dc normalizes every runtime glyph offset
        // by the minimum nonempty, non-NBSP top. Preserve raw FFN T2 for
        // byte-level evidence; only destinations use the normalized offset.
        public int RuntimeTopOffset { get; private set; }
        public int GlyphY(Glyph glyph) => glyph.T2 + RuntimeTopOffset;

        public static OriginalGlyphFont FromFFN(byte[] d, GraphicsDevice gd)
        {
            var f = new OriginalGlyphFont();
            f.Version = d[8] | d[9] << 8;
            f.NumChars = d[10] | d[11] << 8;
            f.Ascent = d[18];
            f.Descent = d[19];
            int dirOff = d[20] | d[21] << 8 | d[22] << 16 | d[23] << 24;
            int shapeOff = d[28] | d[29] << 8 | d[30] << 16 | d[31] << 24;
            f.Glyphs = new Glyph[f.NumChars];
            f.ByChar = new Dictionary<ushort, Glyph>(f.NumChars);
            for (int k = 0; k < f.NumChars; k++)
            {
                int o = dirOff + k * 11;
                var g = new Glyph
                {
                    Char = (ushort)(d[o] | d[o + 1] << 8),
                    W = d[o + 2], H = d[o + 3],
                    U = (ushort)(d[o + 4] | d[o + 5] << 8),
                    V = (ushort)(d[o + 6] | d[o + 7] << 8),
                    T0 = (sbyte)d[o + 8], T1 = (sbyte)d[o + 9], T2 = (sbyte)d[o + 10],
                };
                f.Glyphs[k] = g;
                if (g.Char != 160 && g.W != 0 && g.H != 0)
                    f.RuntimeTopOffset = System.Math.Max(f.RuntimeTopOffset, -g.T2);
                if (!f.ByChar.ContainsKey(g.Char)) f.ByChar[g.Char] = g;
            }
            // shape record -> white-on-transparent atlas, alpha from the nibbles
            f.AtlasW = d[shapeOff + 4] | d[shapeOff + 5] << 8;
            f.AtlasH = d[shapeOff + 6] | d[shapeOff + 7] << 8;
            // R140: the nibble stream starts at shapeOff + 16, not + 12. The
            // shape record header is 16 bytes: 0x7A tag, next[3], u16 atlasW,
            // u16 atlasH, u32 0, u32 0. Engine proof: MacGIMEX_read (binary
            // 0xB9B0) computes its sample base as record + 16 (0x4BA08) and
            // the atlas bytes end EXACTLY at EOF from there (len == shapeOff
            // + 16 + W*H/2 for every table). The old + 12 base read the pad
            // u32 as pixels, shifting the whole atlas 8 px left: every glyph
            // cell sampled its neighbour's strokes. It still read as text
            // (fragments of the brush letterforms) for 44 rounds, but R139b's
            // "variablesans_16 is internally inconsistent" verdict was an
            // artifact of this shift — under + 16 every table including _16
            // renders clean letterforms (tools/iff-dump/r140/r140-ffn-law.md).
            if (f.AtlasW <= 0 || f.AtlasH <= 0 || f.AtlasW > 4096 || f.AtlasH > 4096 ||
                shapeOff + 16 + (f.AtlasW * f.AtlasH + 1) / 2 > d.Length)
                throw new System.InvalidOperationException(
                    "FNTF shape not a plain 4-bit atlas: " + f.AtlasW + "x" + f.AtlasH +
                    " shapeOff=" + shapeOff + " len=" + d.Length);
            var px = new Color[f.AtlasW * f.AtlasH];
            int img = shapeOff + 16;
            for (int y = 0; y < f.AtlasH; y++)
            {
                for (int x = 0; x < f.AtlasW; x++)
                {
                    int i = y * f.AtlasW + x;
                    byte b = d[img + i / 2];
                    int nib = (i % 2 == 0) ? b >> 4 : b & 0xF;
                    // R138: PREMULTIPLIED white ink (rgb == alpha == nib*17).
                    // Every text draw path blends with BlendState.AlphaBlend
                    // (One, InverseSourceAlpha — MonoGame's premultiplied
                    // convention). The previous straight-alpha white upload
                    // (rgb=255 constant) made ANY pixel with alpha >= 1/15
                    // blend to full ink, collapsing every glyph to a solid
                    // block — the user-reported unreadable text. Evidence:
                    // tools/iff-dump/r138/ (textdiag BEFORE/AFTER logs; the
                    // BEFORE rgb-art shows solid rectangles where the alpha
                    // art shows clean AA'd glyphs).
                    byte v = (byte)(nib * 17);
                    px[i] = new Color(v, v, v, v);
                }
            }
            f.Atlas = new Texture2D(gd, f.AtlasW, f.AtlasH);
                Simitone.Client.UI.Model.UIArtProvenance.NoteOriginal(f.Atlas, "glyph-atlas"); // R210
            f.Atlas.SetData(px);
            FontsParsed++;
            return f;
        }

        private static readonly System.Collections.Generic.Dictionary<string, OriginalGlyphFont> _cache =
            new System.Collections.Generic.Dictionary<string, OriginalGlyphFont>();

        public static OriginalGlyphFont LoadDefault(GraphicsDevice gd)
        {
            return Load(DefaultFontPath, gd);
        }

        public static OriginalGlyphFont LoadCaption(GraphicsDevice gd)
        {
            return Load(CaptionFontPath, gd);
        }

        public static OriginalGlyphFont LoadBold(GraphicsDevice gd)
        {
            return Load(BoldFontPath, gd);
        }

        public static OriginalGlyphFont LoadSmall(GraphicsDevice gd)
        {
            return Load(SmallFontPath, gd);
        }

        public static OriginalGlyphFont LoadDialog(GraphicsDevice gd)
        {
            return Load(DialogFontPath, gd);
        }

        public static OriginalGlyphFont LoadTitle(GraphicsDevice gd)
        {
            return Load(TitleFontPath, gd);
        }

        /// R144: the engine addresses fonts by TABLE INDEX (font[i] at BSS
        /// 0x92900+4i, InitSimsColors factory call). The index->resource map
        /// lives inside cITSFontSys (undecoded); the ffn corpus however is
        /// named by point size (variablesans_08.._14) and every decoded
        /// font[i] use matches a plausible point size, so index == the
        /// filename number. R169's cWinPictureDialog decode closes the formerly
        /// ambiguous slot 12 mapping: it requests the regular variablesans_12
        /// face, while explicit bold callers continue through LoadBold(). Engine
        /// rects for the remaining slots are in r144/toolbar-law.md section 7.
        public static OriginalGlyphFont LoadByIndex(int idx, GraphicsDevice gd)
        {
            switch (idx)
            {
                case 8: return Load(TitleFontPath, gd);      // clock digits, motive labels
                case 9: return Load(DialogFontPath, gd);
                case 10: return Load(CaptionFontPath, gd);   // money readout
                case 11: return Load(SmallFontPath, gd);     // sub-panel titles
                case 12: return Load(Regular12FontPath, gd); // cWinTextWrap body font
                case 14: return Load(DefaultFontPath, gd);
                // R238: original Budget row table and OK font factory.
                case 16: return Load("Fonts\\variablesans_16.ffn", gd);
                case 20: return Load("Fonts\\variablesans_20.ffn", gd);
                // R180: cWinSubpanelReportCard::Init selects font[18] for
                // its one full-width grade control. This table ships in the
                // original Fonts archive and is not a synthesized scale.
                case 18: return Load("Fonts\\variablesans_18.ffn", gd);
                // R145: the Interest/Gift band titles (font[6]) and the gift
                // stack COUNT (font[7]) — r145/interest-gift-law.md. No _06.ffn
                // ships in any archive (r142 manifest), so font[6] falls to the
                // _07 table; disclosed fallback under the point-size hypothesis.
                case 6: return Load("Fonts\\variablesans_06.ffn", gd) ?? Load("Fonts\\variablesans_07.ffn", gd);
                case 7: return Load("Fonts\\variablesans_07.ffn", gd);
                default: return null;
            }
        }

        /// Loads a table by stored FAR name. More than one archive can carry the
        /// stored name (patch overrides); the first entry that parses as a plain
        /// 4-bit FNTF atlas wins. (FAR streams do not always survive a second
        /// GetEntry — each candidate is read at most once per process here.)
        public static OriginalGlyphFont Load(string path, GraphicsDevice gd)
        {
            OriginalGlyphFont cached;
            if (_cache.TryGetValue(path, out cached)) return cached;
            var ts1 = Content.Get()?.TS1Global;
            if (ts1 == null) return null;   // R119: pre-content callers (default font family) must get null, not an NRE
            var entries = ts1.GetFarEntries(".ffn");
            if (entries == null || gd == null) return null; // retry after resource/device initialization
            foreach (var en in entries)
            {
                if (en != null && en.FarEntry != null && en.FarEntry.Filename == path)
                {
                    try
                    {
                        var f = FromFFN(en.Archive.GetEntry(en.FarEntry), gd);
                        if (f.NumChars > 0 && f.NumChars <= 1024) { _cache[path] = f; return f; }
                    }
                    catch { }
                }
            }
            return null;
        }

        /// R139: the Unicode -> Windows-1252 boundary conversion (engine
        /// decode: cTSFont::TSCharWidth 0x4acff0 / GetCharDims index a
        /// 256-slot table by (c & 0xFF) — the ORIGINAL's strings were
        /// CP1252 BYTES, its tables are byte-keyed, and the engine never
        /// receives a char outside 0..255). A port-authored Unicode curly
        /// quote (U+201C) is the same TEXT the original encoded as byte
        /// 0x93, so mapping it here renders exactly the glyph the engine
        /// would have drawn. Bytes with no glyph (and chars with no CP1252
        /// byte) draw blank with zero advance — the engine's zeroed-slot
        /// behavior (unmapped slots index a blank record).
        private static readonly System.Collections.Generic.Dictionary<char, char> Cp1252Map =
            new System.Collections.Generic.Dictionary<char, char>
        {
            { '\u20AC', (char)0x80 }, { '\u201A', (char)0x82 }, { '\u0192', (char)0x83 },
            { '\u201E', (char)0x84 }, { '\u2026', (char)0x85 }, { '\u2020', (char)0x86 },
            { '\u2021', (char)0x87 }, { '\u02C6', (char)0x88 }, { '\u2030', (char)0x89 },
            { '\u0160', (char)0x8A }, { '\u2039', (char)0x8B }, { '\u0152', (char)0x8C },
            { '\u017D', (char)0x8E }, { '\u2018', (char)0x91 }, { '\u2019', (char)0x92 },
            { '\u201C', (char)0x93 }, { '\u201D', (char)0x94 }, { '\u2022', (char)0x95 },
            { '\u2013', (char)0x96 }, { '\u2014', (char)0x97 }, { '\u02DC', (char)0x98 },
            { '\u2122', (char)0x99 }, { '\u0161', (char)0x9A }, { '\u203A', (char)0x9B },
            { '\u0153', (char)0x9C }, { '\u017E', (char)0x9E }, { '\u0178', (char)0x9F },
        };

        public static char MapChar(char c)
        {
            char m;
            if (c >= 0x80 && Cp1252Map.TryGetValue(c, out m)) return m;
            return c;
        }

        /// Layout advance of one glyph: width + tail0. R109 measured this
        /// convention across the whole corpus (tools/iff-dump/r109/): UNIFORM
        /// on all seven English variablesans tables (the old "_07 t0-as-
        /// advance" note was a misreading — _07 'A' = 6+1, the quoted '7' was
        /// the sum). Space has a degenerate 1x1 cell; fixed 5px by convention.
        public int Advance(ushort c)
        {
            Glyph g;
            if (ByChar.TryGetValue(c, out g) && g.W > 1) return g.W + g.T0;
            return c == 32 ? 5 : 0;   // space has a degenerate 1x1 cell
        }

        public int Measure(string s)
        {
            int w = 0;
            foreach (var ch in s) w += Advance(MapChar(ch));
            return w;
        }

        // cTSWinBtn caption paint 0x50cb28–90 centers the capital-A ink
        // bounds, then subtracts its draw offset. This is expressed in the
        // same normalized draw space used by UIOriginalText. Any common runtime
        // glyph offset cancels between the caption origin and glyph drawing.
        public int ButtonCaptionY(int height)
        {
            Glyph capitalA;
            return ByChar.TryGetValue('A', out capitalA)
                ? (height - capitalA.H) / 2 - GlyphY(capitalA)
                : (height - LineHeight) / 2;
        }

        /// Plain-SpriteBatch draw (headless gate path). The UIElement control
        /// below draws through the UI batch instead.
        public void Draw(SpriteBatch batch, string s, Vector2 pos, Color color)
        {
            float x = pos.X;
            foreach (var ch0 in s)
            {
                var ch = MapChar(ch0);
                Glyph g;
                if (ByChar.TryGetValue(ch, out g) && g.W > 1)
                {
                    var src = new Rectangle(g.U, g.V, g.W, g.H);
                    batch.Draw(Atlas, new Vector2(x, pos.Y + GlyphY(g)), src, color);
                }
                x += Advance(ch);
            }
        }
    }

    /// <summary>
    /// A UIElement that renders its Text with an ORIGINAL glyph table — the
    /// first original-style text surface in the client (R96). The placement is
    /// new (the original screens carry no such label); the STYLE — glyph
    /// shapes, metrics and antialiasing — is the original .ffn data verbatim.
    /// </summary>
    public class UIOriginalText : UIElement
    {
        public static int TextsMounted = 0;
        public static long StringsDrawn = 0;
        public static long HoverCaptionsShown = 0;
        public static long MoneyUpdatesDrawn = 0;
        public static long ToolbarReadoutsDrawn = 0;
        public static long CheatMessagesShown = 0;
        public static long DialogTitlesDrawn = 0;

        public string Text;
        public OriginalGlyphFont Font;
        // The ordinary original-text ink: InitSimsColors 0x25d680..6a0 builds
        // RGB 195,205,205 and the font-table init loop applies it to slot12
        // (r240 caption-skeptical-review; R175 system-slot decode agrees).
        // UIOriginalLotPopup.NativeTextColor already used the recovered ink;
        // this default completes the same decode for the shared classes
        // (was the port-invented #FFFFF0 the r240 decode refuted).
        public Color Color = new Color(0xC3, 0xCD, 0xCD, 0xFF);
        private Vector2 _size;

        // UIElement's default Size setter is intentionally a no-op. Original
        // glyph labels that define a native text rectangle (for clipping,
        // hit-testing, or geometry probes) therefore need their own storage.
        // Drawing remains glyph-metric based and is unchanged by this value.
        public override Vector2 Size
        {
            get { return _size; }
            set { _size = value; }
        }

        public UIOriginalText(string text, OriginalGlyphFont font)
        {
            Text = text;
            Font = font;
            TextsMounted++;
        }

        public override void Draw(UISpriteBatch batch)
        {
            // UIElement.Visible is a plain field; custom draw overrides must
            // enforce it themselves. Without this guard, hidden original-glyph
            // twins continued painting inside UICachedContainer (most visibly
            // the adult Job rows behind the child Report Card).
            if (!Visible || Font == null || Font.Atlas == null || string.IsNullOrEmpty(Text)) return;
            // R115: Opacity-aware so tweened floaters (money-change popups) fade like the
            // modern labels did; existing surfaces never set Opacity != 1.
            var col = Color * Opacity;
            float x = 0;
            foreach (var ch0 in Text)
            {
                var ch = OriginalGlyphFont.MapChar(ch0);
                OriginalGlyphFont.Glyph g;
                if (Font.ByChar.TryGetValue(ch, out g) && g.W > 1)
                {
                    DrawLocalTexture(batch, Font.Atlas,
                        new Rectangle(g.U, g.V, g.W, g.H),
                        new Vector2(x, Font.GlyphY(g)), Vector2.One, col);
                }
                x += Font.Advance(ch);
            }
            StringsDrawn++;
        }
    }

    /// <summary>
    /// R113: a word-wrapping original-glyph paragraph — a UIContainer of UIOriginalText
    /// lines rebuilt lazily when Text/MaxWidth change (caption mutation after ctor is the
    /// norm for the lot-query panel). Wraps at spaces with hard-break for over-long words,
    /// honours embedded \r\n (the original NghRollover strings carry them), and supports
    /// right/bottom anchoring for bottom-right aligned fields. Line height is the caller's
    /// disclosed choice (the original dialog body geometry is engine-drawn).
    /// </summary>
    public class UIOriginalParagraph : UIContainer
    {
        public static int ParagraphsMounted = 0;

        public OriginalGlyphFont Font;
        public string Text = "";
        public float LineHeight = 13f;
        public float MaxWidth = 300f;
        public bool RightAlign = false;
        public bool BottomAnchor = false;
        // Decoded ordinary ink (r240 slot12 / R175: RGB 195,205,205) — was the
        // port-invented #FFFFF0 the r240 decode explicitly refuted.
        public Color TextColor = new Color(0xC3, 0xCD, 0xCD, 0xFF);

        private string LastKey;

        public UIOriginalParagraph(OriginalGlyphFont font)
        {
            Font = font;
        }

        public void WrapIfNeeded()
        {
            if (Font == null || Font.Atlas == null) return;
            // AUD-17 A-4: LineHeight participates in the wrapped-row Y layout —
            // mutating it after the first render used to keep stale Ys until
            // the text itself changed.
            var key = MaxWidth + "|" + RightAlign + "|" + BottomAnchor + "|" + LineHeight + "|" + Text;
            if (key == LastKey) return;
            LastKey = key;
            var old = Children.ToArray();
            foreach (var c in old) Remove(c);
            var lines = new System.Collections.Generic.List<string>();
            foreach (var para in (Text ?? "").Replace("\r\n", "\n").Split('\n'))
            {
                var line = "";
                foreach (var word in para.Split(' '))
                {
                    var candidate = (line == "") ? word : line + " " + word;
                    if (Font.Measure(candidate) <= MaxWidth || line == "") line = candidate;
                    else { lines.Add(line); line = word; }
                }
                lines.Add(line);
            }
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i] == "") continue;
                var t = new UIOriginalText(lines[i], Font) { Color = TextColor };
                var w = Font.Measure(lines[i]);
                t.Position = new Vector2(RightAlign ? MaxWidth - w : 0,
                    (BottomAnchor ? (i - lines.Count) : i) * LineHeight);
                Add(t);
            }
            ParagraphsMounted++;
        }

        /// R124: containers need to fade the wrapped lines with their own opacity
        /// tween (Children is protected, so the tint goes through here).
        public void TintLines(Color c)
        {
            WrapIfNeeded();
            foreach (var ch in Children.ToArray())
            {
                var t = ch as UIOriginalText;
                if (t != null) t.Color = c;
            }
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            WrapIfNeeded();
            base.Draw(batch);
        }
    }
}
