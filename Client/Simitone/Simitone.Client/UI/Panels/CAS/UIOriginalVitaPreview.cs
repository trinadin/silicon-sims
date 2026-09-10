using System;
using FSO.Client;
using FSO.Client.UI.Framework;
using FSO.LotView;
using FSO.LotView.Utils;
using FSO.SimAntics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Simitone.Client.UI.Panels.CAS
{
    /// <summary>
    /// One posed Sim in the native 100x220 Vita rectangle. Camera and depth
    /// belong to this surface; neither lot contents nor host resolution enter
    /// its projection. R209/R212's existing idle player remains the pose owner.
    /// </summary>
    public sealed class UIOriginalVitaPreview : UIElement
    {
        private static Texture2D Backdrop;
        private RenderTarget2D Target;
        private Effect Effect;
        private WorldCamera Camera;
        public VMAvatar Person;
        internal Texture2D RenderedAvatar => Target;
        public override Vector2 Size { get => new Vector2(100, 220); set { } }
        public override Rectangle GetBounds() => new Rectangle(0, 0, 100, 220);

        public UIOriginalVitaPreview(Texture2D background, Rectangle source)
        {
            Position = new Vector2(source.X, source.Y);
            Visible = false;
            // Preserve the authored backdrop before the existing background
            // layer is cut for its independently tested view-control opening.
            if (Backdrop == null || Backdrop.IsDisposed)
            {
                var pixels = new Color[source.Width * source.Height];
                background.GetData(0, source, pixels, 0, pixels.Length);
                Backdrop = new Texture2D(background.GraphicsDevice, source.Width, source.Height);
                Backdrop.SetData(pixels);
            }
        }

        public override void PreDraw(UISpriteBatch batch)
        {
            if (!Visible || Person?.Avatar == null) return;
            batch.Pause();
            try { RenderAvatar(); }
            finally { batch.Resume(); }
        }

        internal void RenderAvatar()
        {
            if (Person?.Avatar == null) return;
            var gd = GameFacade.GraphicsDevice;
            if (Target == null || Target.IsDisposed)
                Target = new RenderTarget2D(gd, 100, 220, false, SurfaceFormat.Color,
                    DepthFormat.Depth24Stencil8, 0, RenderTargetUsage.PreserveContents);
            if (Effect == null || Effect.IsDisposed) Effect = FSO.Vitaboy.Avatar.Effect.Clone();
            if (Camera == null)
                Camera = new WorldCamera(gd) { Zoom = WorldZoom.Near,
                    ViewDimensions = new Vector2(100, 220), CenterTile = new Vector3(0, 0, 1) };

            var targets = gd.GetRenderTargets();
            var viewport = gd.Viewport;
            var scissor = gd.ScissorRectangle;
            var blend = gd.BlendState;
            var depth = gd.DepthStencilState;
            var rasterizer = gd.RasterizerState;
            var sampler = gd.SamplerStates[0];
            var indices = gd.Indices;
            var avatar = Person.Avatar;
            var lights = avatar.LightPositions;
            try
            {
                gd.SetRenderTarget(Target);
                gd.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.Transparent, 1, 0);
                gd.BlendState = BlendState.AlphaBlend;
                gd.DepthStencilState = DepthStencilState.Default;
                gd.RasterizerState = RasterizerState.CullNone;
                gd.SamplerStates[0] = SamplerState.LinearClamp;
                Camera.ProjectionDirty();
                Effect.CurrentTechnique = Effect.Techniques[0];
                Effect.Parameters["View"].SetValue(Camera.View);
                Effect.Parameters["Projection"].SetValue(Camera.Projection);
                Effect.Parameters["World"].SetValue(Microsoft.Xna.Framework.Matrix.CreateRotationY(MathHelper.Pi - Person.RadianDirection));
                Effect.Parameters["AmbientLight"].SetValue(Vector4.One);
                // DrawGeometry consumes the current bone pose without changing
                // the avatar's scene, world transform, outfits or animation.
                // Lot-space shadow lights do not belong to this local surface.
                avatar.LightPositions = null;
                avatar.DrawGeometry(gd, Effect);
            }
            finally
            {
                avatar.LightPositions = lights;
                gd.SetRenderTargets(targets);
                gd.Viewport = viewport;
                gd.ScissorRectangle = scissor;
                gd.BlendState = blend;
                gd.DepthStencilState = depth;
                gd.RasterizerState = rasterizer;
                gd.SamplerStates[0] = sampler;
                gd.Indices = indices;
            }
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible || Person == null) return;
            DrawLocalTexture(batch, Backdrop, Vector2.Zero);
            if (Target != null && !Target.IsDisposed) DrawLocalTexture(batch, Target, Vector2.Zero);
        }

        public void Release()
        {
            Person = null;
            Visible = false;
            Target?.Dispose(); Target = null;
            Effect?.Dispose(); Effect = null;
            Camera = null;
        }

        public override void Removed()
        {
            Release();
            base.Removed();
        }
    }
}
