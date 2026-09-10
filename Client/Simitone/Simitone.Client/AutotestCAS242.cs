using System;
using System.IO;
using FSO.Client;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simitone.Client.UI.Panels.CAS;

namespace Simitone.Client
{
    internal static class AutotestCAS242
    {
        // Render the production background over a varied underlay. A punched
        // window must preserve every underlying pixel, including its edges;
        // zero alpha with retained RGB instead adds the painted placeholder.
        internal static bool Check(UIOriginalDesignChar panel, out string diagnostics)
        {
            var gd = GameFacade.GraphicsDevice;
            var targets = gd.GetRenderTargets(); var viewport = gd.Viewport;
            var blend = gd.BlendState; var depth = gd.DepthStencilState;
            var rasterizer = gd.RasterizerState; var scissor = gd.ScissorRectangle;
            var sampler = gd.SamplerStates[0]; var texture = gd.Textures[0];
            try
            {
                const int width = 800, height = 600;
                var expected = new Color[width * height];
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    expected[y * width + x] = new Color(25 + x % 97, 30 + y % 83, 40 + (x + y) % 71);
                using (var underlay = new Texture2D(gd, width, height))
                using (var target = new RenderTarget2D(gd, width, height, false, SurfaceFormat.Color, DepthFormat.None))
                using (var batch = new SpriteBatch(gd))
                {
                    underlay.SetData(expected);
                    gd.SetRenderTarget(target); gd.Clear(Color.Black);
                    batch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                        DepthStencilState.None, RasterizerState.CullNone);
                    batch.Draw(underlay, Vector2.Zero, Color.White);
                    batch.Draw(panel.Background.Texture, Vector2.Zero, Color.White);
                    batch.End();
                    var pixels = new Color[expected.Length]; target.GetData(pixels);
                    int mismatches = 0;
                    for (int y = 145; y < 365; y++) for (int x = 618; x < 718; x++)
                        if (pixels[y * width + x] != expected[y * width + x]) mismatches++;
                    var output = Path.Combine(FSO.Common.FSOEnvironment.UserDir, "ui-audit", "r242");
                    Directory.CreateDirectory(output);
                    using (var stream = File.Create(Path.Combine(output, "cas-preview-composite.png")))
                        target.SaveAsPng(stream, width, height);
                    diagnostics = "production CAS background over varied underlay: " + mismatches + "/22000 changed preview pixels";
                    return mismatches == 0;
                }
            }
            catch (Exception ex) { diagnostics = ex.ToString(); return false; }
            finally
            {
                gd.SetRenderTargets(targets); gd.Viewport = viewport; gd.ScissorRectangle = scissor;
                gd.BlendState = blend; gd.DepthStencilState = depth; gd.RasterizerState = rasterizer;
                gd.SamplerStates[0] = sampler; gd.Textures[0] = texture;
            }
        }
    }
}
