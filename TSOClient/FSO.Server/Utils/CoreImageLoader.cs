using FSO.Content.Model;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.IO;

namespace FSO.Server.Utils
{
    public class CoreImageLoader
    {
        public static TexBitmap SoftImageFetch(Stream stream, AbstractTextureRef texRef)
        {
            Image<Bgra32> result = null;
            try
            {
                result = Image.Load<Bgra32>(stream);
            }
            catch (Exception)
            {
                return new TexBitmap() { Data = new byte[0] };
            }
            stream.Close();
            
            if (result == null) return null;

            return new TexBitmap
            {
                Data = result.SavePixelData(),
                Width = result.Width,
                Height = result.Height,
                PixelSize = 4
            };
        }
    }
}
