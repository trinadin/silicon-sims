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
    ///
    /// CAS-02 framing law (cWinVitaBtn decode, owned PPC executable):
    /// UpdateTransform 0x2dbef0 anchors the skeleton ROOT at the window's
    /// horizontal center, 20px above the bottom (X = l+(r-l)/2, Y = b-0x14;
    /// pets 0x32 — cWinVitaBtnSolo keeps the same law with a -20 x shift),
    /// and VitaBoy::Render 0x38a6d0 clips to the window rect. The native
    /// render scale chain is 5.33333 px/BMF-unit (scale table 0x59bbc4[0xb0])
    /// over the 0.25 person record scale = 21⅓ px/BMU = 64 px per FreeSO
    /// world unit. On the shared Near camera that is exactly PreciseZoom
    /// 3/√2 (px/WU = 256·z/(6√2) = 64), and the root anchor at (50, 200)
    /// makes the look tile height 90px/64/3 = 0.46875. All constants are
    /// resolution-free: the surface renders 1:1 inside the 800x600 UI plane
    /// at both 800x600 and 1024x768.
    /// </summary>
    public sealed class UIOriginalVitaPreview : UIElement
    {
        // Decoded native framing constants (CAS-02 law, see class comment).
        internal const float NativePxPerWorldUnit = 64f;
        internal const float NativePreciseZoom = 3f / 1.4142135623730951f;
        internal const float NativeFeetRow = 200f;   // window bottom - 20 (0x2dbef0)
        internal const float NativeLookTileZ = 0.46875f; // (220/2 - 200)/64/3

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
            if (Camera == null) Camera = CreateNativeCamera(gd);

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

        /// <summary>
        /// The native preview camera: Near zoom with PreciseZoom 3/√2 renders
        /// at exactly 64 px/world unit, and the look point sits NativeLookTileZ
        /// tiles above the skeleton root so the root lands on (50, 200) — the
        /// window center, 20px above the bottom (UpdateTransform 0x2dbef0).
        /// </summary>
        internal static WorldCamera CreateNativeCamera(GraphicsDevice gd)
        {
            return new WorldCamera(gd)
            {
                Zoom = WorldZoom.Near,
                PreciseZoom = NativePreciseZoom,
                ViewDimensions = new Vector2(100, 220),
                CenterTile = new Vector3(0, 0, NativeLookTileZ),
            };
        }

        internal WorldCamera CameraForProbe => Camera;

        internal void EnsureCameraForProbe()
        {
            if (Camera == null) Camera = CreateNativeCamera(GameFacade.GraphicsDevice);
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
