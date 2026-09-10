using System;
using FSO.Client.UI.Framework;
using Microsoft.Xna.Framework;
using Simitone.Client.UI.Controls;

namespace Simitone.Client.UI.Panels.CAS
{
    /// <summary>cWinDesignFamily::PrepareButtons + cTSFont::DrawTextPara.</summary>
    public sealed class UIOriginalFamilyCaption : UIOriginalText
    {
        public const int SlotWidth = 85, SlotHeight = 105;
        public UIOriginalFamilyCaption(string text, OriginalGlyphFont font) : base(text, font)
        {
            Size = new Vector2(SlotWidth, SlotHeight);
        }

        public Rectangle CaptionBounds => new Rectangle(0, SlotHeight - (2 * Font.LineHeight + 2),
            SlotWidth, 2 * Font.LineHeight + 2);

        private static bool White(char c) => c == ' ';
        private static bool Return(char c) => c == '\n';
        private static int Ink(OriginalGlyphFont font, char c) =>
            font.ByChar.TryGetValue(OriginalGlyphFont.MapChar(c), out var glyph) ? glyph.W : 0;

        // Native CalculateWordsToFitInWidth (0x4ad3b0): test visible ink,
        // prefer a space/hyphen boundary, hard-break a long word, then consume
        // following whitespace. It returns a span of the original text, never
        // edits the stored name or introduces an ellipsis.
        public static int LineLength(OriginalGlyphFont font, string text, int start, int width)
        {
            if (start >= text.Length) return 0;
            if (Return(text[start])) return 1;
            int remaining = text.Length - start;
            int count = 0, lastBreak = 0, index = start, ink = Ink(font, text[start]);
            bool hardReturn = false;
            while (ink <= width)
            {
                count++;
                if (count >= remaining) return count;
                char current = text[index];
                if (Return(current)) { lastBreak = count; hardReturn = true; break; }
                if (current == ' ' || current == '-') lastBreak = count;
                ink -= Ink(font, current);
                ink += font.Advance(OriginalGlyphFont.MapChar(current));
                index++;
                if (index < text.Length) ink += Ink(font, text[index]);
            }
            if (lastBreak == 0)
            {
                if (count > 1) lastBreak = count - 1;
                else
                    while (start + lastBreak < text.Length && !Return(text[start + lastBreak])
                        && !White(text[start + lastBreak])) lastBreak++;
            }
            if (!hardReturn)
                while (start + lastBreak < text.Length && White(text[start + lastBreak])) lastBreak++;
            return Math.Max(1, lastBreak);
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible || Font?.Atlas == null || string.IsNullOrEmpty(Text)) return;
            var bounds = CaptionBounds;
            int offset = 0;
            for (int y = bounds.Top; y < bounds.Bottom && offset < Text.Length; y += Font.LineHeight)
            {
                int length = LineLength(Font, Text, offset, bounds.Width);
                var line = Text.Substring(offset, length);
                int x = (bounds.Width - Font.Measure(line)) / 2;
                foreach (char source in line)
                {
                    char ch = OriginalGlyphFont.MapChar(source);
                    if (Font.ByChar.TryGetValue(ch, out var glyph) && glyph.W > 1)
                    {
                        var dest = new Rectangle(x, y + Font.GlyphY(glyph), glyph.W, glyph.H);
                        var clip = Rectangle.Intersect(dest, new Rectangle(0, 0, SlotWidth, SlotHeight));
                        if (clip.Width > 0 && clip.Height > 0)
                            DrawLocalTexture(batch, Font.Atlas,
                                new Rectangle(glyph.U + clip.X - dest.X, glyph.V + clip.Y - dest.Y, clip.Width, clip.Height),
                                new Vector2(clip.X, clip.Y), Vector2.One, Color * Opacity);
                    }
                    x += Font.Advance(ch);
                }
                offset += length;
            }
            StringsDrawn++;
        }
    }
}
