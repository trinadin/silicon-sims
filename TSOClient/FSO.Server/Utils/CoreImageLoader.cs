using FSO.Content.Model;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

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
            finally
            {
                stream.Close();
            }

            if (result == null) return null;

            var pixels = new byte[result.Width * result.Height * 4];
            result.CopyPixelDataTo(pixels);

            return new TexBitmap
            {
                Data = pixels,
                Width = result.Width,
                Height = result.Height,
                PixelSize = 4
            };
        }
    }
}
