using MSDFData;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.IO;

namespace MSDFExtension
{
    public class AtlasBuilder
    {
        public int Width { get; }
        public int Height { get; }
        public char[] CharMap { get; }
        private readonly Rgba32[] RawData;
        public int GlyphSize { get; }
        private int Progress;

        public AtlasBuilder(int totalChars, int size)
        {
            int width = 1;
            int height = 1;
            while (width * height < totalChars)
            {
                width *= 2;
                if (width * height < totalChars)
                    height *= 2;
            }

            Width = width;
            Height = height;
            GlyphSize = size;
            CharMap = new char[totalChars];
            RawData = new Rgba32[Width * Height * GlyphSize * GlyphSize];
            Progress = 0;
        }

        public int AddChar(char c, Stream imageData)
        {
            using var image = Image.Load<Rgba32>(imageData);

            if (image.Width != GlyphSize || image.Height != GlyphSize)
            {
                throw new ArgumentException($"Glyph image size must be {GlyphSize}x{GlyphSize} pixels.");
            }

            var buf = new Rgba32[image.Width * image.Height];
            image.CopyPixelDataTo(buf);

            return AddChar(c, buf);
        }

        public int AddChar(char c, Rgba32[] imageData)
        {
            if (imageData.Length != GlyphSize * GlyphSize)
                throw new ArgumentException($"Pixel array length must be {GlyphSize * GlyphSize}.");

            lock (this)
            {
                if (Progress >= CharMap.Length)
                    throw new InvalidOperationException("Atlas is already full.");

                CharMap[Progress] = c;

                int xOffset = (Progress % Width) * GlyphSize;
                int yOffset = (Progress / Width) * GlyphSize;
                int lineWidth = Width * GlyphSize;

                for (int y = 0; y < GlyphSize; y++)
                {
                    int dstIndex = (yOffset + y) * lineWidth + xOffset;
                    int srcIndex = y * GlyphSize;
                    Array.Copy(imageData, srcIndex, RawData, dstIndex, GlyphSize);
                }

                return Progress++;
            }
        }

        public byte[] Save()
        {
            using var result = Image.LoadPixelData<Rgba32>(RawData, Width * GlyphSize, Height * GlyphSize);
            using var ms = new MemoryStream();
            result.SaveAsPng(ms);
            return ms.ToArray();
        }

        public FieldAtlas Finish()
        {
            return new FieldAtlas(Width, Height, GlyphSize, Save(), CharMap);
        }
    }
}
