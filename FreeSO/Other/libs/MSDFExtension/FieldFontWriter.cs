using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Content.Pipeline.Serialization.Compiler;
using System;

namespace MSDFData
{
    [ContentTypeWriter]
    public class FieldFontWriter : ContentTypeWriter<FieldFont>
    {
        public override string GetRuntimeReader(TargetPlatform targetPlatform)
        {
            var targetType = typeof(FieldFontReader);
            return targetType.FullName + ", " + targetType.Assembly.FullName;
            /*
            return typeof(FieldFontReader).AssemblyQualifiedName ?? string.Empty;
            */
        }
        /*

        public override string GetRuntimeType(TargetPlatform targetPlatform)
        {
            //_targetType.FullName + ", " + _targetType.Assembly.FullName
            return typeof(FieldFont).AssemblyQualifiedName ?? string.Empty;
        }
        */

        private void WriteMetrics(ContentWriter output, Metrics value)
        {
            output.Write(value.Advance);
            output.Write(value.Scale);
            output.Write(value.Translation);
        }

        private void WriteFieldGlyph(ContentWriter output, FieldGlyph value)
        {
            output.Write(value.Character);
            output.Write(value.AtlasIndex);
            WriteMetrics(output, value.Metrics);
        }

        private void WriteKerningPair(ContentWriter output, KerningPair value)
        {
            output.Write(value.Left);
            output.Write(value.Right);
            output.Write(value.Advance);
        }

        private void WriteAtlas(ContentWriter output, FieldAtlas value)
        {
            output.Write(value.Width);
            output.Write(value.Height);
            output.Write(value.GlyphSize);

            output.Write(value.PNGData.Length);
            output.Write(value.PNGData);

            output.Write(value.CharMap.Length);
            output.Write(value.CharMap);
        }

        protected override void Write(ContentWriter output, FieldFont value)
        {
            output.Write(value.Name);
            output.Write(value.PxRange);

            var glyphs = value.GlyphsRaw;
            output.Write(glyphs.Count);

            foreach (var glyph in glyphs)
            {
                output.Write(glyph.Key);
                WriteFieldGlyph(output, glyph.Value);
            }

            var pairs = value.KerningPairs;
            output.Write(pairs.Count);
            foreach (var pair in pairs)
            {
                WriteKerningPair(output, pair);
            }

            WriteAtlas(output, value.Atlas);
        }
    }
}
