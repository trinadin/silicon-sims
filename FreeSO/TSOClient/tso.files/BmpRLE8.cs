using Microsoft.Xna.Framework;
using System;
using System.IO;

namespace FSO.Files
{
    /// <summary>
    /// R143: exact BI_RLE8 BMP decoder. 237 of the 282 .bmp members the TS1
    /// UIGraphics.far mount feeds this engine are run-length encoded
    /// (every neighborhood / create-a-sim screen and its buttons). On
    /// Simitone.Desktop these decoded through the ImageSharp BitmapFunction
    /// but only as opaque RGB - the magic-pink palette entries that key the
    /// sheet shapes never became alpha (and stb_image / GDI+ reject RLE8
    /// outright), so the family composited wrong or not at all. Transparency
    /// law matches the loader's existing ManualTextureMaskData: any palette
    /// entry within the magic-pink threshold is transparent, everything else
    /// (including palette index 0, an ordinary navy fill in this family -
    /// PickBkg is 66% idx0 and opaque in the original) stays opaque.
    /// </summary>
    public static class BmpRLE8
    {
        /// <summary>
        /// Decodes a BI_RLE8 BMP from the stream (read from its current
        /// position). Returns null - leaving the stream rewound to its start -
        /// when the stream is not an 8bpp RLE BMP, so callers fall through to
        /// the generic bitmap path.
        /// </summary>
        public static ImageData? TryDecode(Stream str)
        {
            if (!str.CanSeek) return null;
            var start = str.Position;
            try
            {
                var head = new byte[54];
                ReadExact(str, head);
                if (head[0] != (byte)'B' || head[1] != (byte)'M') { str.Seek(start, SeekOrigin.Begin); return null; }
                var dataOffset = BitConverter.ToUInt32(head, 10);
                var headerSize = BitConverter.ToUInt32(head, 14);
                var width = BitConverter.ToInt32(head, 18);
                var height = BitConverter.ToInt32(head, 22);
                var bpp = BitConverter.ToUInt16(head, 28);
                var compression = BitConverter.ToUInt32(head, 30);
                var clrUsed = BitConverter.ToUInt32(head, 46);
                if (bpp != 8 || compression != 1 || width <= 0 || height == 0 || headerSize < 40)
                { str.Seek(start, SeekOrigin.Begin); return null; }

                var paletteEntries = (int)(clrUsed == 0 ? 256 : Math.Min(clrUsed, 256));
                var paletteBytes = new byte[paletteEntries * 4];
                ReadExact(str, paletteBytes);
                // BGRA quads -> RGBA palette. Transparency follows the corpus
                // law the rest of this loader already implements
                // (ManualTextureMaskData): magic pink. The sheets key their
                // shapes with literal magenta palette ENTRIES (AddPersonBtn
                // idx1, ScrollBar idx2), while palette index 0 is an ordinary
                // fill color in this family (PickBkg is 66% idx0 navy and the
                // screen is opaque in the original) - so idx0 stays opaque.
                var palette = new Color[256];
                for (int i = 0; i < 256; i++)
                {
                    if (i < paletteEntries)
                    {
                        var b = paletteBytes[i * 4]; var g = paletteBytes[i * 4 + 1]; var r = paletteBytes[i * 4 + 2];
                        palette[i] = (r >= 248 && b >= 248 && g <= 4)
                            ? new Color((byte)0, (byte)0, (byte)0, (byte)0)
                            : new Color((byte)r, (byte)g, (byte)b, (byte)255);
                    }
                    else palette[i] = new Color((byte)0, (byte)0, (byte)0, (byte)255);
                }

                var topDown = height < 0;
                var h = Math.Abs(height);
                var w = width;
                var pixels = new byte[w * h]; // default 0 = transparent
                str.Seek(start + dataOffset, SeekOrigin.Begin);

                // RLE streams address the image bottom-up unless the header
                // declares a negative height.
                int row = topDown ? 0 : h - 1;
                int rowStep = topDown ? 1 : -1;
                int col = 0;
                var data = new byte[str.Length - (start + dataOffset)];
                ReadExact(str, data);
                int p = 0;
                while (p + 1 < data.Length)
                {
                    byte count = data[p++];
                    byte value = data[p++];
                    if (count != 0)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            if (col >= w || row < 0 || row >= h) break;
                            pixels[row * w + col] = value;
                            col++;
                        }
                    }
                    else
                    {
                        if (value == 0) { col = 0; row += rowStep; } // end of line
                        else if (value == 1) break;                  // end of bitmap
                        else if (value == 2)                         // delta
                        {
                            if (p + 1 >= data.Length) break;
                            var dx = data[p++]; var dy = data[p++];
                            col += dx; row += rowStep * dy;
                        }
                        else                                          // absolute run
                        {
                            var n = (int)value;
                            var take = Math.Min(n, w - col);
                            if (take > 0 && row >= 0 && row < h)
                            {
                                for (int i = 0; i < take && p + i < data.Length; i++)
                                    pixels[row * w + col + i] = data[p + i];
                            }
                            col += Math.Max(0, take);
                            p += n;
                            if ((p & 1) == 1) p++; // absolute runs pad to 16-bit boundary
                        }
                    }
                    if (row < 0 || row >= h) break; // ran past the image; done
                }

                var colors = new Color[w * h];
                for (int i = 0; i < colors.Length; i++) colors[i] = palette[pixels[i]];
                return new ImageData(colors, w, h);
            }
            catch
            {
                return null;
            }
            finally
            {
                if (str.CanSeek) str.Seek(start, SeekOrigin.Begin);
            }
        }

        private static void ReadExact(Stream str, byte[] buffer)
        {
            var off = 0;
            while (off < buffer.Length)
            {
                var read = str.Read(buffer, off, buffer.Length - off);
                if (read <= 0) throw new EndOfStreamException();
                off += read;
            }
        }
    }
}
