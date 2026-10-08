using FSO.Common;
using FSO.Common.Utils;
using FSO.Files.RC;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using System.Threading;

namespace FSO.Files.Formats.IFF.Chunks
{
    /// <summary>
    /// Texture for a 3D Mesh. Can be jpg, png or bmp. 
    /// </summary>
    public class MTEX : IffChunk, IDGRP3DTextureHolder
    {
        private byte[] data;
        private Stream stream;

        private bool HasDecoded = false;

        private Func<Texture2D> Producer;

        private TextureData<Color>[] Decoded;
        private Point Size;

        private Texture2D Cached;

        public MTEX()
        {

        }

        public MTEX(Stream stream)
        {
            this.stream = stream;
        }

        /// <summary>
        /// Reads a BMP chunk from a stream.
        /// </summary>
        /// <param name="iff">An Iff instance.</param>
        /// <param name="stream">A Stream object holding a BMP chunk.</param>
        public override void Read(IffFile iff, Stream stream)
        {
            data = new byte[stream.Length];
            stream.Read(data, 0, (int)stream.Length);
        }

        public override bool Write(IffFile iff, Stream stream)
        {
            stream.Write(data, 0, data.Length);
            return true;
        }

        private int DecodingState;

        public void Decode(GraphicsDevice gd)
        {
            var exch = Interlocked.CompareExchange(ref DecodingState, 1, 0);
            if (exch > 0)
            {
                // Can't decode more than once.
                SpinWait wait = default;
                while (exch == 1)
                {
                    wait.SpinOnce();
                    exch = Volatile.Read(ref DecodingState);
                }

                return;
            }

            if (data != null)
            {
                stream = new MemoryStream(data);
            }

            var image = ImageLoader.DataFromStream(gd, stream);

            if (image == null)
            {
                throw new InvalidDataException("Invalid MTEX image!");
            }

            if (image.Value.Producer != null)
            {
                Producer = image.Value.Producer;
            }
            else if (image.Value.Data != null)
            {
                var data = image.Value.Data.Value;
                Size = new Point(data.Width, data.Height);

                if (FSOEnvironment.EnableNPOTMip)
                {
                    Decoded = TextureUtils.GenerateMips(data.Width, data.Height, TextureUtils.CalculateMipCount(data.Width, data.Height), data.Data);
                }
                else
                {
                    Decoded = new TextureData<Color>[] { new TextureData<Color>(0, data.Data) };
                }
            }

            if (!IffFile.RETAIN_CHUNK_DATA)
            {
                data = null;
            }

            stream.Dispose();
            stream = null;

            Interlocked.Exchange(ref DecodingState, 2);
            HasDecoded = true;
        }

        public Texture2D GetTexture(GraphicsDevice device)
        {
            if (Cached == null)
            {
                if (!HasDecoded)
                {
                    Decode(device);
                }

                if (Producer != null)
                {
                    Cached = Producer();
                    Producer = null;
                    if (FSOEnvironment.EnableNPOTMip)
                    {
                        var data = new Color[Cached.Width * Cached.Height];
                        Cached.GetData(data);
                        var n = new Texture2D(device, Cached.Width, Cached.Height, true, SurfaceFormat.Color);
                        Cached.Dispose();
                        Cached = n;

                        AssetStreaming.LoadTexture(n, AssetStreamingMode.Lot, () =>
                        {
                            return TextureUtils.GenerateMips(n, data);
                        });
                    }
                }
                else if (Decoded != null)
                {
                    Cached = new Texture2D(device, Size.X, Size.Y, FSOEnvironment.EnableNPOTMip, SurfaceFormat.Color);
                    TextureUtils.UploadTexData(Cached, Decoded);
                    Decoded = null;
                }
            }

            return Cached;
        }

        public void SetData(byte[] data)
        {
            this.data = data;
            HasDecoded = false;
            Cached = null;
            Decoded = null;
        }
    }
}
