using FSO.Client;
using FSO.Common;
using System.Linq;
using FSO.Client.UI.Framework;
using FSO.Common.Rendering.Framework.IO;
using FSO.Common.Utils;
using FSO.Common.Rendering.Framework.Model;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Simitone.Client.UI.Model;
using Simitone.Client.UI.Screens;
using System;

namespace Simitone.Client.UI.Panels
{
    /// <summary>
    /// R190: the camera-mode world surface on the decoded original law.
    ///
    /// cDDDSimsView::TSOnMouseDownL (0x2165b0): while the cursor mode is 4
    /// (camera), a left click on the active view consumes the event and
    /// snapshots HouseViewer::ComputeCameraFrame's rectangle. ComputeCameraFrame
    /// (0x1c4f90) positions the CPState snapshot dimensions (CamGetWidth/
    /// CamGetHeight, +0x218/+0x21c) above the pointer, translates into the view rect;
    /// the click handler insets the rect 1px on every edge before calling
    /// HouseViewer::TakeAsyncSnapshot (0x1c4e10).
    ///
    /// STR# 140 [17] documents the interaction: "Move the frame unto the
    /// game screen and click the mouse to capture the highlighted scene"
    /// and defines the custom size as CTRL+arrow keys.
    ///
    /// The overlay is a full-screen listener that is only WillDraw-able in
    /// camera mode, so it consumes world clicks exactly then (the engine's
    /// mode-4 consume) while the UCP band and dialogs keep their higher
    /// depth. The actual backbuffer readback happens in
    /// OriginalSnapshotCaptureScene during the scene pass, when only the
    /// world has been drawn.
    /// </summary>
    public class UIOriginalCameraOverlay : UIContainer
    {
        public static int ModeEntries;
        public static int CapturesRequested;
        public static Rectangle LastFrameRect;      // pre-inset compute result (probe)
        public static Rectangle LastCaptureRect;    // post-inset snapshot rect (probe)

        public Simitone.Client.UI.Screens.TS1GameScreen Game;
        public bool CameraActive;

        private Texture2D White;
        private Vector2 LastMouse;
        private readonly UIMouseEventRef MouseRegion;
        public bool PointerOverWorld { get; private set; }
        public override Vector2 Size { get => new Vector2(Game.ScreenWidth, Game.ScreenHeight); set { } }
        public override Rectangle GetBounds() => new Rectangle(0, 0, Game.ScreenWidth, Game.ScreenHeight);

        public UIOriginalCameraOverlay(Simitone.Client.UI.Screens.TS1GameScreen game)
        {
            Game = game;
            White = TextureGenerator.GetPxWhite(GameFacade.GraphicsDevice);
            MouseRegion = ListenForMouse(GetBounds(), OnMouse);
            Visible = false;
        }

        public void SetCameraActive(bool active)
        {
            CameraActive = active;
            Visible = active;
            if (!active) PointerOverWorld = false;
            if (active) ModeEntries++;
        }

        public override void GameResized()
        {
            MouseRegion.Region = GetBounds();
            base.GameResized();
        }

        public void OnMouse(UIMouseEventType type, UpdateState state)
        {
            if (!CameraActive) return;
            if (type == UIMouseEventType.MouseOver) PointerOverWorld = true;
            if (type == UIMouseEventType.MouseOut) PointerOverWorld = false;
            if (type == UIMouseEventType.MouseDown && state.WindowFocused)
            {
                var pt = GetMousePosition(state.MouseState);
                RequestCapture((int)pt.X, (int)pt.Y);
            }
        }

        /// <summary>ComputeCameraFrame0x1c4f90: pointer at bottom center,
        /// translate the whole frame into the view, then intersect oversized
        /// frames. The calls to0x37c70 are OffsetRect, not clipping.</summary>
        public static Rectangle ComputeFrameRect(int mx, int my, int w, int h, int viewW, int viewH)
        {
            if (w <= 0 || h <= 0 || viewW <= 0 || viewH <= 0) return Rectangle.Empty;
            int l = mx - w / 2 - 1, r = mx + w / 2 - 1;
            int t = my - h - 2, b = my;
            int dx = l < 0 ? -l : r >= viewW ? viewW - r - 1 : 0;
            int dy = t < 0 ? -t : b >= viewH ? viewH - b - 1 : 0;
            l += dx; r += dx; t += dy; b += dy;
            l = Math.Max(0, Math.Min(viewW, l)); r = Math.Max(0, Math.Min(viewW, r));
            t = Math.Max(0, Math.Min(viewH, t)); b = Math.Max(0, Math.Min(viewH, b));
            return new Rectangle(l, t, Math.Max(0, r - l), Math.Max(0, b - t));
        }

        internal static Rectangle ScaleCaptureRect(Rectangle logical, float scale, int width, int height)
        {
            int left = (int)Math.Round(logical.Left * (double)scale);
            int top = (int)Math.Round(logical.Top * (double)scale);
            int right = (int)Math.Round(logical.Right * (double)scale);
            int bottom = (int)Math.Round(logical.Bottom * (double)scale);
            return Rectangle.Intersect(new Rectangle(left, top, Math.Max(0, right-left), Math.Max(0, bottom-top)),
                new Rectangle(0, 0, width, height));
        }

        public void RequestCapture(int mx, int my)
        {
            int size = Game?.Frontend?.MainPanel?.CameraChrome != null
                ? Game.Frontend.MainPanel.CameraChrome.SnapshotSize : 1;
            int w, h;
            OriginalSnapshotAlbum.DimsForSize(size, out w, out h);
            var frame = ComputeFrameRect(mx, my, w, h, Game.ScreenWidth, Game.ScreenHeight);
            var capture = new Rectangle(frame.X + 1, frame.Y + 1,
                Math.Max(1, frame.Width - 2), Math.Max(1, frame.Height - 2));
            LastFrameRect = frame;
            LastCaptureRect = capture;
            if (frame.Width <= 2 || frame.Height <= 2) return;
            CapturesRequested++;
            FSO.HIT.HITVM.Get().PlaySoundEvent("ui_camera_photo");
            var viewport = GameFacade.GraphicsDevice.Viewport;
            var physical = ScaleCaptureRect(capture, FSOEnvironment.DPIScaleFactor, viewport.Width, viewport.Height);
            int quality = Game?.Frontend?.MainPanel?.CameraChrome?.SnapshotQuality ?? 1;
            OriginalSnapshotCaptureScene.Request(physical, capture.Width, capture.Height, quality);
        }

        public override void Update(UpdateState state)
        {
            LastMouse = GetMousePosition(state.MouseState);
            base.Update(state);
            ProcessKeyboard(state);
        }

        public void ProcessKeyboard(UpdateState state)
        {
            if (!CameraActive || !PointerOverWorld || !state.WindowFocused || !state.CtrlDown) return;
            var panel = Game?.Frontend?.MainPanel?.CameraChrome;
            int w, h;
            OriginalSnapshotAlbum.DimsForSize(panel?.SnapshotSize ?? 1, out w, out h);
            bool changed = false;
            // TSOnKeyDown0x21774c..770: four pixels per key event. Up
            // increases height because the frame grows upward from the pointer.
            foreach (var key in state.NewKeys.Distinct())
            {
                if (key == Keys.Left) { w -= 4; changed = true; }
                if (key == Keys.Right) { w += 4; changed = true; }
                if (key == Keys.Up) { h += 4; changed = true; }
                if (key == Keys.Down) { h -= 4; changed = true; }
            }
            if (!changed) return;
            OriginalSnapshotAlbum.ClampCustom(ref w, ref h);
            OriginalSnapshotAlbum.CustomW = w; OriginalSnapshotAlbum.CustomH = h;
            int size = 3;
            for (int i = 0; i < 3; i++)
                if (OriginalSnapshotAlbum.FixedSnapshotDims[i, 0] == w && OriginalSnapshotAlbum.FixedSnapshotDims[i, 1] == h) size = i;
            panel?.SelectSnapshotSize(size);
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible || !PointerOverWorld) return;
            // HouseViewer render paths1d4b0c..24 use an opaque white outline.
            // MouseEnteredChild clears the frame when a UI child owns input.
            var pt = LastMouse;
            int size = Game?.Frontend?.MainPanel?.CameraChrome != null
                ? Game.Frontend.MainPanel.CameraChrome.SnapshotSize : 1;
            int w, h;
            OriginalSnapshotAlbum.DimsForSize(size, out w, out h);
            var r = ComputeFrameRect((int)pt.X, (int)pt.Y, w, h, Game.ScreenWidth, Game.ScreenHeight);
            DrawBorder(batch, r, 1);
            base.Draw(batch);
        }

        private void DrawBorder(UISpriteBatch batch, Rectangle r, int t)
        {
            DrawLocalTexture(batch, White, null, new Vector2(r.X, r.Y), new Vector2(r.Width, t));
            DrawLocalTexture(batch, White, null, new Vector2(r.X, r.Y + r.Height - t), new Vector2(r.Width, t));
            DrawLocalTexture(batch, White, null, new Vector2(r.X, r.Y + t), new Vector2(t, r.Height - 2 * t));
            DrawLocalTexture(batch, White, null, new Vector2(r.X + r.Width - t, r.Y + t), new Vector2(t, r.Height - 2 * t));
        }
    }
}
