using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using Microsoft.Xna.Framework;

namespace FSO.Files
{
    public class ImageLoader
    {
        public static bool UseSoftLoad = true;
        public static int PremultiplyPNG = 0;

        public static HashSet<uint> MASK_COLORS = new HashSet<uint>{
            new Microsoft.Xna.Framework.Color(0xFF, 0x00, 0xFF, 0xFF).PackedValue,
            new Microsoft.Xna.Framework.Color(0xFE, 0x02, 0xFE, 0xFF).PackedValue,
            new Microsoft.Xna.Framework.Color(0xFF, 0x01, 0xFF, 0xFF).PackedValue
        };

        public static Func<GraphicsDevice, Stream, Texture2D> BaseFunction = WinFromStream;
        public static Func<GraphicsDevice, Stream, Func<Texture2D>> BaseNonUIFunction = WinNonUIFromStream;


        public static Texture2D FromStream(GraphicsDevice gd, Stream str)
        {
            return BaseFunction(gd, str);
        }

        /// <summary>
        /// Get a Texture2D factory for the given stream.
        /// This runs the decoder work on the calling thread, and returns a function
        /// that creates the texture that should be called on the main thread.
        /// </summary>
        /// <param name="gd"></param>
        /// <param name="str"></param>
        /// <returns></returns>
        public static Func<Texture2D> NonUIFromStream(GraphicsDevice gd, Stream str)
        {
            return BaseNonUIFunction(gd, str);
        }

        private static Texture2D WinFromStream(GraphicsDevice gd, Stream str)
        {
            return WinFromStreamP(gd, str, 0);
        }

        private static Func<Texture2D> WinNonUIFromStream(GraphicsDevice gd, Stream str)
        {
            return WinNonUIFromStreamP(gd, str, 0);
        }

        public static bool Premultiply(Color[] buffer, int premult)
        {
            if (premult == 1)
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    var a = buffer[i].A;
                    if (a != 255)
                    {
                        buffer[i] = new Color((byte)((buffer[i].R * a) / 255), (byte)((buffer[i].G * a) / 255), (byte)((buffer[i].B * a) / 255), a);
                    }
                }

                return true;
            }
            else if (premult == -1) //divide out a premultiply... currently needed for dx since it premultiplies pngs without reason
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    var rawA = buffer[i].A;

                    if (rawA != 255)
                    {
                        var a = rawA / 255f;
                        buffer[i] = new Color((byte)(buffer[i].R / a), (byte)(buffer[i].G / a), (byte)(buffer[i].B / a), buffer[i].A);
                    }
                }

                return true;
            }

            return false;
        }

        public static bool Premultiply(byte[] buffer, int premult)
        {
            if (premult == 1)
            {
                for (int i = 0; i < buffer.Length; i += 4)
                {
                    var a = buffer[i + 3];
                    if (a != 255)
                    {
                        buffer[i] = (byte)((buffer[i] * a) / 255);
                        buffer[i+1] = (byte)((buffer[i+1] * a) / 255);
                        buffer[i+2] = (byte)((buffer[i+2] * a) / 255);
                    }
                }

                return true;
            }
            else if (premult == -1) //divide out a premultiply... currently needed for dx since it premultiplies pngs without reason
            {
                for (int i = 0; i < buffer.Length; i += 4)
                {
                    var rawA = buffer[i];

                    if (rawA != 255)
                    {
                        var a = rawA / 255f;

                        buffer[i] = (byte)(buffer[i] / a);
                        buffer[i+1] = (byte)(buffer[i+1] / a);
                        buffer[i+2] = (byte)(buffer[i+2] / a);
                    }
                }

                return true;
            }

            return false;
        }

        public static Func<Texture2D> WinNonUIFromStreamP(GraphicsDevice gd, Stream str, int premult)
        {
            //if (!UseSoftLoad)
            //{
            //attempt monogame load of image

            var magic = (str.ReadByte() | (str.ReadByte() << 8));
            str.Seek(0, SeekOrigin.Begin);
            magic += 0;
            if (magic == 0x4D42)
            {
                try
                {
                    //it's a bitmap. 
                    if (ImageLoaderHelpers.BitmapFunction != null)
                    {
                        var bmp = ImageLoaderHelpers.BitmapFunction(str);
                        if (bmp == null) return null;
                        return () =>
                        {
                            Texture2D tex = new Texture2D(gd, bmp.Item2, bmp.Item3);
                            tex.SetData(bmp.Item1);
                            ManualTextureMaskSingleThreaded(ref tex, MASK_COLORS.ToArray());
                            return tex;
                        };
                    }
                    else
                    {
                        return () =>
                        {
                            Texture2D tex = Texture2D.FromStream(gd, str);

                            ManualTextureMaskSingleThreaded(ref tex, MASK_COLORS.ToArray());
                            return tex;
                        };
                    }
                }
                catch (Exception)
                {
                    return null; //bad bitmap :(
                }
            }
            else
            {
                //test for targa
                str.Seek(-18, SeekOrigin.End);
                byte[] sig = new byte[16];
                str.Read(sig, 0, 16);
                str.Seek(0, SeekOrigin.Begin);
                if (ASCIIEncoding.Default.GetString(sig) == "TRUEVISION-XFILE")
                {
                    try
                    {
                        var tga = new TargaImagePCL.TargaImage(str);

                        return () =>
                        {
                            var tex = new Texture2D(gd, tga.Image.Width, tga.Image.Height);
                            tex.SetData(tga.Image.ToBGRA(true));
                            return tex;
                        };
                    }
                    catch (Exception)
                    {
                        return null; //bad tga
                    }
                }
                else
                {
                    //anything else
                    try
                    {
                        premult += PremultiplyPNG;

                        if (ImageLoaderHelpers.BitmapFunction != null)
                        {
                            var bmp = ImageLoaderHelpers.BitmapFunction(str);
                            if (bmp == null) return null;

                            Premultiply(bmp.Item1, premult);

                            return () =>
                            {
                                Texture2D tex = new Texture2D(gd, bmp.Item2, bmp.Item3);
                                tex.SetData(bmp.Item1);
                                return tex;
                            };

                            //buffer = bmp.Item1;
                        }
                        else
                        {
                            return () =>
                            {
                                Texture2D tex = Texture2D.FromStream(gd, str);

                                if (premult != 0)
                                {
                                    var buffer = new Color[tex.Width * tex.Height];
                                    tex.GetData(buffer);
                                    Premultiply(buffer, premult);
                                    tex.SetData(buffer);
                                }

                                return tex;
                            };
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("error: " + e.ToString());
                        return () => new Texture2D(gd, 1, 1);
                    }
                }
            }
        }

        public static Texture2D WinFromStreamP(GraphicsDevice gd, Stream str, int premult)
        {
            return WinNonUIFromStreamP(gd, str, premult)();
        }

        public static void ManualTextureMaskSingleThreaded(ref Texture2D Texture, uint[] ColorsFrom)
        {
            var ColorTo = Microsoft.Xna.Framework.Color.Transparent.PackedValue;

            var size = Texture.Width * Texture.Height * 4;
            byte[] buffer = new byte[size];

            Texture.GetData<byte>(buffer);

            var didChange = false;

            for (int i = 0; i < size; i += 4)
            {
                if (buffer[i] >= 248 && buffer[i + 2] >= 248 && buffer[i + 1] <= 4)
                {
                    buffer[i] = buffer[i + 1] = buffer[i + 2] = buffer[i + 3] = 0;
                    didChange = true;
                }
            }

            if (didChange)
            {
                Texture.SetData(buffer);
            }
            else return;
        }

    }
}
