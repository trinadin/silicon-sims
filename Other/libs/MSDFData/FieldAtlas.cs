using Microsoft.Xna.Framework.Content;
using System.IO;

namespace MSDFData
{
    public readonly struct FieldAtlas
    {
        private readonly int WidthBackend;
        private readonly int HeightBackend;
        private readonly int GlyphSizeBackend;
        private readonly byte[] PNGDataBackend;
        private readonly char[] CharMapBackend;

        public FieldAtlas(int width, int height, int glyphSize, byte[] pngData, char[] charMap)
        {
            WidthBackend = width;
            HeightBackend = height;
            GlyphSizeBackend = glyphSize;
            PNGDataBackend = pngData;
            CharMapBackend = charMap;
        }

        public int Width => WidthBackend;
        public int Height => HeightBackend;
        public int GlyphSize => GlyphSizeBackend;
        public byte[] PNGData => PNGDataBackend;
        public char[] CharMap => CharMapBackend;
    }
}
