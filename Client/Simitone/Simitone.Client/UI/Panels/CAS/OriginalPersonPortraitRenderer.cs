using System;
using System.Collections.Generic;
using System.Linq;
using FSO.Vitaboy;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Simitone.Client.UI.Panels.CAS
{
    /// <summary>MakePersonBtnImage/MakePersonHeadImage, owned PPC executable 0x23cfe0/0x23b8a0.</summary>
    public static class OriginalPersonPortraitRenderer
    {
        // Native fixed crop (76,45,122,125), widened symmetrically to the 35:41 output aspect.
        public static readonly Rectangle Crop = new Rectangle(65, 45, 68, 80);
        public const float PixelsPerUnit = 160f / 1.4142135623730951f;

        public static Texture2D Generate(GraphicsDevice device, SimAvatar avatar)
        {
            // A fresh head-only avatar keeps the native HEAD-local vertices; posed world
            // bones, idle animations and the gameplay camera do not enter this render.
            var head = new SimAvatar(avatar.Skeleton.Clone()) { Head = avatar.Head };
            var bindings = head.Bindings.Select(b => (b.Texture, b.Mesh,
                Vertices: b.Mesh.VertexBuffer.Select(v => new VertexPositionNormalTexture(v.Position, v.Normal, v.TextureCoordinate)).ToArray())).ToArray();
            var oldTargets = device.GetRenderTargets();
            var oldViewport = device.Viewport;
            var oldBlend = device.BlendState;
            var oldDepth = device.DepthStencilState;
            var oldRaster = device.RasterizerState;
            var oldSampler = device.SamplerStates[0];
            var output = new Color[105 * 41];
            using var target = new RenderTarget2D(device, 200, 200, false, SurfaceFormat.Color, DepthFormat.Depth24);
            using var effect = new BasicEffect(device)
            {
                TextureEnabled = true,
                // Native render quality 0 and full ambient preserve the original skin's baked shading.
                LightingEnabled = false,
                View = Matrix.CreateRotationY(MathHelper.PiOver4) * Matrix.CreateRotationX(MathHelper.ToRadians(30)),
                Projection = Matrix.CreateOrthographic(200 / PixelsPerUnit, 200 / PixelsPerUnit, -100, 100)
            };
            try
            {
                for (int view = 0; view < 3; view++)
                {
                    float yaw = view == 1 ? 9 : view == 2 ? -9 : 0;
                    float tilt = view == 1 ? -7 : view == 2 ? 7 : 0;
                    // X-reflected BMF coordinates reverse the native Y/Z rotations.
                    effect.World = Matrix.CreateRotationY(-MathHelper.ToRadians(9 + yaw))
                        * Matrix.CreateRotationZ(-MathHelper.PiOver2)
                        * Matrix.CreateRotationY(-MathHelper.PiOver4 - MathHelper.ToRadians(tilt));
                    device.SetRenderTarget(target);
                    device.Clear(Color.Transparent);
                    device.DepthStencilState = DepthStencilState.Default;
                    device.BlendState = BlendState.Opaque;
                    device.RasterizerState = RasterizerState.CullNone;
                    device.SamplerStates[0] = SamplerState.LinearWrap;
                    foreach (var binding in bindings)
                    {
                        effect.Texture = binding.Texture.Get(device);
                        foreach (var pass in effect.CurrentTechnique.Passes)
                        {
                            pass.Apply();
                            device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, binding.Vertices, 0,
                                binding.Vertices.Length, binding.Mesh.IndexBuffer, 0, binding.Mesh.NumPrimitives);
                        }
                    }
                    device.SetRenderTarget(null);
                    var pixels = new Color[200 * 200];
                    target.GetData(pixels);
                    var face = Resize(pixels, 200, Crop, 35, 41);
                    for (int y = 0; y < 41; y++) Array.Copy(face, y * 35, output, y * 105 + view * 35, 35);
                }
            }
            finally
            {
                device.SetRenderTargets(oldTargets);
                device.Viewport = oldViewport;
                device.BlendState = oldBlend;
                device.DepthStencilState = oldDepth;
                device.RasterizerState = oldRaster;
                device.SamplerStates[0] = oldSampler;
            }
            var result = new Texture2D(device, 105, 41);
            result.SetData(output);
            return result;
        }

        // ScaleBitmap (0x1e9a10): separable 1-3t²+2t³ kernel, integer sample
        // origins, reflected edges, colorkey exclusion and 16-bit intermediate.
        public static Color[] Resize(Color[] source, int stride, Rectangle crop, int width, int height)
        {
            var horizontal = Weights(crop.Width, width);
            var vertical = Weights(crop.Height, height);
            var temp = new Color[width * crop.Height];
            for (int y = 0; y < crop.Height; y++)
                for (int x = 0; x < width; x++)
                    temp[y * width + x] = Filter(horizontal[x], i => source[(crop.Y + y) * stride + crop.X + i]);
            var result = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    result[y * width + x] = Filter(vertical[y], i => temp[i * width + x]);
            return result;
        }

        private static List<(int Index, float Weight)>[] Weights(int input, int output)
        {
            float scale = (float)output / input, radius = Math.Max(1, 1 / scale);
            var result = new List<(int, float)>[output];
            for (int x = 0; x < output; x++)
            {
                var weights = result[x] = new List<(int, float)>();
                float center = x / scale;
                for (int sample = (int)Math.Ceiling(center - radius); sample <= (int)Math.Floor(center + radius); sample++)
                {
                    float t = Math.Abs(center - sample) / radius;
                    float weight = t >= 1 ? 0 : (1 + (2 * t - 3) * t * t) / radius;
                    int index = sample;
                    while (index < 0 || index >= input) index = index < 0 ? -index : 2 * input - index - 2;
                    weights.Add((index, weight));
                }
            }
            return result;
        }

        private static Color Filter(List<(int Index, float Weight)> weights, Func<int, Color> pixel)
        {
            float r = 0, g = 0, b = 0, sum = 0;
            foreach (var entry in weights)
            {
                var c = pixel(entry.Index);
                if (c.A == 0) continue;
                r += (c.R & 248) * entry.Weight;
                g += (c.G & 252) * entry.Weight;
                b += (c.B & 248) * entry.Weight;
                sum += entry.Weight;
            }
            if (sum <= 0.000001f) return Color.Transparent;
            return new Color(((int)(r / sum + .5f)) & 248, ((int)(g / sum + .5f)) & 252,
                ((int)(b / sum + .5f)) & 248, 255);
        }
    }
}
