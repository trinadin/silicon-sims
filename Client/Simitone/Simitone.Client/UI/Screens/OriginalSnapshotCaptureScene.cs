using FSO.Common.Rendering.Framework;
using FSO.Common.Rendering.Framework.Model;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace Simitone.Client.UI.Screens
{
    /// <summary>
    /// R190: the snapshot readback hook. HouseViewer::TakeAsyncSnapshot
    /// (0x1c4e10) captures the 3D view asynchronously; the port performs
    /// the equivalent readback in the scene pass, appended to the scene
    /// list directly after the lot World so the backbuffer holds the
    /// finished world with no UI painted over it. The scene draws nothing.
    /// </summary>
    public class OriginalSnapshotCaptureScene : _3DAbstract
    {
        private static Rectangle? Pending;
        private static int OutputWidth, OutputHeight, PendingQuality;
        private static Func<Rectangle> DeferredRectangle;
        private static string PendingCaption;
        private static bool DeferredLogicalCoordinates = true;
        public static int Readbacks;
        public static int ReadbackFailures;
        public static Rectangle LastReadbackRect;

        internal static IDisposable IsolatePendingForTest() => new PendingTestScope();
        private sealed class PendingTestScope : IDisposable
        {
            private readonly Rectangle? SavedRectangle = Pending;
            private readonly int Width = OutputWidth, Height = OutputHeight, Quality = PendingQuality;
            private readonly int Count = Readbacks, Failures = ReadbackFailures;
            private readonly Rectangle Last = LastReadbackRect;
            private readonly Func<Rectangle> Deferred = DeferredRectangle;
            private readonly string Caption = PendingCaption;
            private readonly bool Logical = DeferredLogicalCoordinates;
            private bool Disposed;
            public PendingTestScope() { Pending = null; DeferredRectangle = null; PendingCaption = null; }
            public void Dispose()
            {
                if (Disposed) return;
                Disposed = true;
                Pending = SavedRectangle; OutputWidth = Width; OutputHeight = Height; PendingQuality = Quality;
                Readbacks = Count; ReadbackFailures = Failures; LastReadbackRect = Last;
                DeferredRectangle = Deferred; PendingCaption = Caption; DeferredLogicalCoordinates = Logical;
            }
        }

        public OriginalSnapshotCaptureScene(GraphicsDevice device) : base(device) { }

        public static void Request(Rectangle rect, int outputWidth = 0, int outputHeight = 0, int quality = 1)
        {
            DeferredRectangle = null;
            PendingCaption = null;
            Pending = rect;
            OutputWidth = outputWidth > 0 ? outputWidth : rect.Width;
            OutputHeight = outputHeight > 0 ? outputHeight : rect.Height;
            PendingQuality = quality;
        }

        public static void RequestDeferred(Func<Rectangle> rectangle, int quality, string caption, bool logicalCoordinates = true)
        {
            Pending = null;
            DeferredRectangle = rectangle;
            DeferredLogicalCoordinates = logicalCoordinates;
            PendingQuality = quality;
            PendingCaption = caption;
        }

        public override void Update(UpdateState state) { }

        public override void Draw(GraphicsDevice device)
        {
            var rect = Pending;
            var deferred = DeferredRectangle;
            var caption = PendingCaption;
            Pending = null;
            DeferredRectangle = null;
            PendingCaption = null;
            if (deferred != null)
            {
                var logical = deferred();
                OutputWidth = logical.Width; OutputHeight = logical.Height;
                rect = DeferredLogicalCoordinates ? Panels.UIOriginalCameraOverlay.ScaleCaptureRect(logical, FSO.Common.FSOEnvironment.DPIScaleFactor,
                    device.Viewport.Width, device.Viewport.Height) : logical;
            }
            if (rect == null || rect.Value.Width <= 0 || rect.Value.Height <= 0) return;
            Texture2D captured = null;
            try
            {
                var r = Rectangle.Intersect(rect.Value, new Rectangle(0, 0, device.Viewport.Width, device.Viewport.Height));
                if (r.Width <= 0 || r.Height <= 0) return;
                var data = new Color[r.Width * r.Height];
                device.GetBackBufferData((Rectangle?)r, data, 0, data.Length);
                // The camera frame is expressed in original logical pixels.
                // At high DPI read its entire physical region, then reduce to
                // the original photo dimensions. This runs before UI drawing;
                // no render target or viewport state is changed.
                if (r.Width != OutputWidth || r.Height != OutputHeight)
                {
                    var pixels = new Rgba32[data.Length];
                    for (int i = 0; i < data.Length; i++) pixels[i] = new Rgba32(data[i].R, data[i].G, data[i].B, data[i].A);
                    using (var resized = Image.LoadPixelData<Rgba32>(pixels, r.Width, r.Height))
                    {
                        resized.Mutate(x => x.Resize(OutputWidth, OutputHeight));
                        pixels = new Rgba32[OutputWidth * OutputHeight];
                        resized.CopyPixelDataTo(pixels);
                        data = new Color[pixels.Length];
                        for (int i = 0; i < pixels.Length; i++) data[i] = new Color(pixels[i].R, pixels[i].G, pixels[i].B, pixels[i].A);
                    }
                }
                captured = new Texture2D(device, OutputWidth, OutputHeight);
                captured.SetData(data);
                Readbacks++;
                LastReadbackRect = r;
                Simitone.Client.UI.Model.OriginalSnapshotAlbum.AddCaptured(captured, PendingQuality);
                captured = null; // the album owns this texture (and may replace it with decoded JPEG)
                if (caption != null) Simitone.Client.UI.Model.OriginalSnapshotAlbum.SetCurrentDescription(caption);
            }
            catch (Exception e)
            {
                ReadbackFailures++;
                GameLog.Write("snapshot readback EXC " + e.GetType().Name + " " + e.Message);
            }
            finally { captured?.Dispose(); }
        }

        public override void DeviceReset(GraphicsDevice device) { }

        public override void Add(_3DComponent item) { }
        public override List<_3DComponent> GetElements() { return new List<_3DComponent>(); }
    }
}
