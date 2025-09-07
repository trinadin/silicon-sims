using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using System.Collections.Generic;

namespace MSDFData
{
    public class FieldFontReader : ContentTypeReader<FieldFont>
    {
        private Metrics ReadMetrics(ContentReader input)
        {
            float advance = input.ReadSingle();
            float scale = input.ReadSingle();
            Vector2 translation = input.ReadVector2();

            return new Metrics(advance, scale, translation);
        }

        private FieldGlyph ReadGlyph(ContentReader input)
        {
            char character = input.ReadChar();
            int atlasIndex = input.ReadInt32();
            var metrics = ReadMetrics(input);

            return new FieldGlyph(character, atlasIndex, metrics);
        }

        private KerningPair ReadKerningPair(ContentReader input)
        {
            char left = input.ReadChar();
            char right = input.ReadChar();
            float advance = input.ReadSingle();

            return new KerningPair(left, right, advance);
        }

        private FieldAtlas ReadAtlas(ContentReader input)
        {
            int width = input.ReadInt32();
            int height = input.ReadInt32();
            int glyphSize = input.ReadInt32();

            int pngDataSize = input.ReadInt32();
            var pngData = input.ReadBytes(pngDataSize);

            int charMapSize = input.ReadInt32();
            var charMap = input.ReadChars(charMapSize);

            return new FieldAtlas(width, height, glyphSize, pngData, charMap);
        }

        protected override FieldFont Read(ContentReader input, FieldFont existingInstance)
        {
            string name = input.ReadString();
            float pxRange = input.ReadSingle();

            int glyphCount = input.ReadInt32();
            var glyphs = new Dictionary<char, FieldGlyph>();
            for (int i = 0; i < glyphCount; i++)
            {
                glyphs.Add(input.ReadChar(), ReadGlyph(input));
            }

            int pairCount = input.ReadInt32();
            var pairs = new List<KerningPair>();
            for (int i = 0; i < pairCount; i++)
            {
                pairs.Add(ReadKerningPair(input));
            }

            var atlas = ReadAtlas(input);

            return new FieldFont(name, glyphs, pairs, pxRange, atlas);
        }
    }
}
