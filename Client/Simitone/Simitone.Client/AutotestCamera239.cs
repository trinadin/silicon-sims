using FSO.Client;
using FSO.Client.UI.Framework;
using FSO.Common;
using FSO.Common.Rendering.Framework.IO;
using FSO.Common.Rendering.Framework.Model;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Simitone.Client.UI.Model;
using Simitone.Client.UI.Panels;
using Simitone.Client.UI.Screens;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Simitone.Client
{
    /// <summary>Independent native-number fixtures and real input/persistence paths.
    /// Never requests a world capture or touches the owner's album.</summary>
    internal static class AutotestCamera239
    {
        internal static bool Check(TS1GameScreen game, out string diagnostics)
        {
            var failures = new List<string>();
            Action<bool, string> require = (pass, name) => { if (!pass) failures.Add(name); };
            string directory = Path.Combine(Path.GetTempPath(), "simitone-r239-camera-" + Guid.NewGuid().ToString("N"));
            var overlay = game?.CameraOverlay;
            var panel = game?.Frontend?.MainPanel?.CameraChrome;
            bool active = overlay?.CameraActive ?? false, visible = overlay?.Visible ?? false;
            bool pointer = overlay?.PointerOverWorld ?? false;
            int selectedSize = panel?.SnapshotSize ?? 1, modeEntries = UIOriginalCameraOverlay.ModeEntries;
            try
            {
                using (OriginalSnapshotAlbum.IsolateForTest(directory))
                {
                    // Fixed values independently transcribed from PPC edge arithmetic,
                    // including the bottom/right one-pixel exclusion and whole-frame offset.
                    require(UIOriginalCameraOverlay.ComputeFrameRect(400,300,400,300,1024,768) == new Rectangle(199,0,400,302), "frame-top-translation");
                    require(UIOriginalCameraOverlay.ComputeFrameRect(20,30,400,300,1024,768) == new Rectangle(0,0,400,302), "frame-left-top");
                    require(UIOriginalCameraOverlay.ComputeFrameRect(1020,760,600,400,1024,768) == new Rectangle(423,358,600,402), "frame-right");
                    require(UIOriginalCameraOverlay.ComputeFrameRect(1024,768,600,400,1024,768) == new Rectangle(423,365,600,402), "frame-right-bottom");
                    require(UIOriginalCameraOverlay.ComputeFrameRect(20,30,600,400,320,200) == new Rectangle(0,0,320,200), "frame-oversized");
                    require(UIOriginalCameraOverlay.ComputeFrameRect(203,400,201,149,1024,768) == new Rectangle(102,249,200,151), "frame-odd-width");
                    require(UIOriginalCameraOverlay.ComputeFrameRect(2,2,400,300,0,600) == Rectangle.Empty, "frame-empty-view");
                    require(UIOriginalCameraOverlay.ScaleCaptureRect(new Rectangle(11,7,198,150),2,1600,1200) == new Rectangle(22,14,396,300), "dpi-two");
                    require(UIOriginalCameraOverlay.ScaleCaptureRect(new Rectangle(11,7,198,150),1.5f,1600,1200) == new Rectangle(16,10,298,226), "dpi-fractional-edges");
                    require(UIOriginalCameraOverlay.ScaleCaptureRect(new Rectangle(11,7,198,150),2,300,200) == new Rectangle(22,14,278,186), "dpi-viewport-clip");
                    require(overlay != null && panel != null, "camera-available");
                    if (overlay != null && panel != null)
                    {
                        var input = new UpdateState { WindowFocused = true, KeyboardState = new KeyboardState(Keys.LeftControl) };
                        Action<Keys> key = k => { input.NewKeys.Clear(); input.NewKeys.Add(k); overlay.ProcessKeyboard(input); };
                        overlay.SetCameraActive(true);
                        overlay.OnMouse(UIMouseEventType.MouseOver, input);
                        panel.SelectSnapshotSize(0);
                        key(Keys.Right);
                        require(OriginalSnapshotAlbum.CustomW == 204 && OriginalSnapshotAlbum.CustomH == 150 && panel.SnapshotSize == 3, "key-from-current-preset");
                        key(Keys.Left);
                        require(panel.SnapshotSize == 0, "key-reclassifies-preset");
                        key(Keys.Up);
                        require(OriginalSnapshotAlbum.CustomH == 154, "up-grows-upward-four");
                        input.NewKeys.Clear(); overlay.ProcessKeyboard(input);
                        require(OriginalSnapshotAlbum.CustomH == 154, "no-render-frame-repeat");
                        input.KeyboardState = new KeyboardState(Keys.LeftControl, Keys.LeftShift);
                        key(Keys.Up);
                        require(OriginalSnapshotAlbum.CustomH == 158, "shift-does-not-change-step");
                        overlay.OnMouse(UIMouseEventType.MouseOut, input); key(Keys.Up);
                        require(OriginalSnapshotAlbum.CustomH == 158, "hud-child-blocks-keys");
                        overlay.OnMouse(UIMouseEventType.MouseOver, input);
                        input.WindowFocused = false; key(Keys.Up);
                        require(OriginalSnapshotAlbum.CustomH == 158, "inactive-window-blocks-keys");
                        input.WindowFocused = true; input.KeyboardState = new KeyboardState(); key(Keys.Up);
                        require(OriginalSnapshotAlbum.CustomH == 158, "ctrl-required");
                        input.KeyboardState = new KeyboardState(Keys.LeftControl);
                        panel.SelectSnapshotSize(2); key(Keys.Up);
                        require(panel.SnapshotSize == 2 && OriginalSnapshotAlbum.CustomH == 400, "max-clamp-retains-preset");
                    }
                    Directory.CreateDirectory(directory);
                    using (var legacy = Pattern(80,60))
                    using (var output = File.Create(Path.Combine(directory,"photo-9.png"))) legacy.SaveAsPng(output,80,60);
                    File.WriteAllText(Path.Combine(directory,"photo-9.txt"),"shared legacy");
                    var legacyBytes = File.ReadAllBytes(Path.Combine(directory,"photo-9.png"));
                    require(OriginalSnapshotAlbum.ExportName("Goth",5) == "Goth_5", "family-resource-id-prefix");
                    require(OriginalSnapshotAlbum.ExportName("A/B\\C",7) == "A_B_C_7", "family-name-path-boundary");
                    require(OriginalSnapshotAlbum.QualityForIndex(0) == 20 && OriginalSnapshotAlbum.QualityForIndex(1) == 50
                        && OriginalSnapshotAlbum.QualityForIndex(2) == 90 && OriginalSnapshotAlbum.QualityForIndex(99) == 50,"quality-ladder");
                    OriginalSnapshotAlbum.ResetForFamily("Goth_5");
                    require(OriginalSnapshotAlbum.Pages.Count == 1 && OriginalSnapshotAlbum.Pages[0].LegacyShared,"legacy-visible-in-family");
                    var low = OriginalSnapshotAlbum.AddCaptured(Pattern(198,150),0);
                    var lowPath = low.SourceImagePath;
                    require(low.FileName == "Goth_5_0000" && low.Photo.Width == 198 && low.Photo.Height == 150,"native-name-photo-size");
                    var high = OriginalSnapshotAlbum.AddCaptured(Pattern(198,150),2);
                    var highPath = high.SourceImagePath;
                    require(high.FileName == "Goth_5_0001","monotonic-sequence");
                    var lowBytes = File.ReadAllBytes(lowPath); var highBytes = File.ReadAllBytes(highPath);
                    require(lowBytes[0] == 255 && lowBytes[1] == 216 && highBytes[0] == 255 && highBytes[1] == 216,"jpeg-signature");
                    require(FirstQuantizer(lowBytes) > FirstQuantizer(highBytes) && FirstQuantizer(highBytes) > 0,"jpeg-quality-affects-quantization");
                    var thumb = SixLabors.ImageSharp.Image.Identify(Path.Combine(directory,"Goth_5_0000_thumb.jpg"));
                    require(thumb.Width == 99 && thumb.Height == 75,"thumbnail-aspect-fit");
                    OriginalSnapshotAlbum.SetCurrentDescription("New caption\nSecond line");
                    require(File.ReadAllBytes(highPath).SequenceEqual(highBytes) && File.ReadAllBytes(lowPath).SequenceEqual(lowBytes),"caption-does-not-reencode");
                    OriginalSnapshotAlbum.ResetForFamily("Goth_5");
                    require(OriginalSnapshotAlbum.Pages.Select(p=>p.FileName).SequenceEqual(new[]{"Goth_5_0000","Goth_5_0001","photo-9"}),"family-numeric-plus-legacy-order");
                    require(OriginalSnapshotAlbum.Pages[1].Description == "New caption\nSecond line", "caption-reload");
                    OriginalSnapshotAlbum.CurrentPage = 2;
                    OriginalSnapshotAlbum.SetCurrentDescription("Legacy caption edited");
                    require(File.ReadAllBytes(Path.Combine(directory,"photo-9.png")).SequenceEqual(legacyBytes),"legacy-caption-preserves-png");
                    OriginalSnapshotAlbum.ResetForFamily("Bachelor_2");
                    require(OriginalSnapshotAlbum.Pages.Count == 1 && OriginalSnapshotAlbum.Pages[0].LegacyShared,"family-isolation-with-shared-legacy");
                    var other = OriginalSnapshotAlbum.AddCaptured(Pattern(198,150));
                    require(other.FileName == "Bachelor_2_0000", "family-independent-sequence");
                    OriginalSnapshotAlbum.ResetForFamily("Goth_5");
                    OriginalSnapshotAlbum.CurrentPage = 1; OriginalSnapshotAlbum.DeleteCurrent();
                    require(!File.Exists(highPath) && !File.Exists(Path.Combine(directory,"Goth_5_0001_thumb.jpg"))
                        && !File.Exists(Path.Combine(directory,"Goth_5_0001.txt")),"delete-removes-triplet");
                    var next = OriginalSnapshotAlbum.AddCaptured(Pattern(198,150));
                    require(next.FileName == "Goth_5_0002","deleted-sequence-not-reused");
                    require(File.ReadAllBytes(lowPath).SequenceEqual(lowBytes) && File.Exists(Path.Combine(directory,"Bachelor_2_0000.jpg")),"other-pages-preserved");
                }
            }
            catch (Exception e) { failures.Add(e.GetType().Name + ": " + e.Message); }
            finally
            {
                if (overlay != null)
                {
                    overlay.SetCameraActive(true);
                    overlay.OnMouse(pointer ? UIMouseEventType.MouseOver : UIMouseEventType.MouseOut,new UpdateState());
                    overlay.CameraActive = active; overlay.Visible = visible;
                }
                panel?.SelectSnapshotSize(selectedSize);
                UIOriginalCameraOverlay.ModeEntries = modeEntries;
                try { if (Directory.Exists(directory)) Directory.Delete(directory,true); } catch { }
            }
            diagnostics = failures.Count == 0 ? "native-frame/input/dpi/jpeg/family/legacy OK" : string.Join(", ",failures);
            return failures.Count == 0;
        }

        private static IDisposable CaptureAlbumScope, PendingScope;
        private static string CaptureDirectory;
        private static TS1GameScreen CaptureGame;
        private static CaptureMarker Marker;
        private static int BeforeReadbacks, BeforeFailures, BeforeRequests;
        private static Rectangle BeforeFrame, BeforeCapture, ExpectedPhysical;
        internal static bool CaptureReady => CaptureAlbumScope != null
            && (OriginalSnapshotCaptureScene.Readbacks > BeforeReadbacks || OriginalSnapshotCaptureScene.ReadbackFailures > BeforeFailures);

        internal static void BeginCaptureCheck(TS1GameScreen game)
        {
            if (CaptureAlbumScope != null) throw new InvalidOperationException("Capture check already active.");
            if (game?.CameraOverlay == null || game.Frontend?.MainPanel?.CameraChrome == null)
                throw new InvalidOperationException("Camera unavailable.");
            var panel = game.Frontend.MainPanel.CameraChrome;
            int size = panel.SnapshotSize;
            CaptureDirectory = Path.Combine(Path.GetTempPath(), "simitone-r239-readback-" + Guid.NewGuid().ToString("N"));
            BeforeRequests = UIOriginalCameraOverlay.CapturesRequested;
            BeforeFrame = UIOriginalCameraOverlay.LastFrameRect; BeforeCapture = UIOriginalCameraOverlay.LastCaptureRect;
            try
            {
                CaptureAlbumScope = OriginalSnapshotAlbum.IsolateForTest(CaptureDirectory);
                PendingScope = OriginalSnapshotCaptureScene.IsolatePendingForTest();
                OriginalSnapshotAlbum.ResetForFamily("CaptureProbe_99");
                CaptureGame = game;
                // The marker is drawn in the UI pass over the exact photograph
                // region. A readback that accidentally includes UI yields a solid
                // magenta JPEG and fails. Require that the marker actually drew.
                Marker = new CaptureMarker(new Rectangle(200,249,198,150));
                game.Add(Marker);
                BeforeReadbacks = OriginalSnapshotCaptureScene.Readbacks;
                BeforeFailures = OriginalSnapshotCaptureScene.ReadbackFailures;
                panel.SelectSnapshotSize(0);
                var viewport = GameFacade.GraphicsDevice.Viewport;
                ExpectedPhysical = UIOriginalCameraOverlay.ScaleCaptureRect(new Rectangle(200,249,198,150),
                    FSOEnvironment.DPIScaleFactor, viewport.Width, viewport.Height);
                game.CameraOverlay.RequestCapture(300,400);
            }
            catch { CancelCaptureCheck(); throw; }
            finally { panel.SelectSnapshotSize(size); }
        }

        internal static bool FinishCaptureCheck(out string diagnostics)
        {
            bool ok = false;
            try
            {
                var page = OriginalSnapshotAlbum.Pages.SingleOrDefault();
                bool readback = CaptureReady && OriginalSnapshotCaptureScene.ReadbackFailures == BeforeFailures
                    && OriginalSnapshotCaptureScene.LastReadbackRect == ExpectedPhysical;
                bool photo = page?.Photo != null && page.Photo.Width == 198 && page.Photo.Height == 150;
                bool exclusion = false;
                int magenta = -1, unique = 0;
                if (photo)
                {
                    var pixels = new Color[page.Photo.Width*page.Photo.Height];
                    page.Photo.GetData(pixels);
                    magenta = pixels.Count(c=>c.R>230 && c.G<25 && c.B>230);
                    unique = pixels.Select(c=>c.PackedValue).Distinct().Take(9).Count();
                    exclusion = Marker != null && Marker.Draws > 0 && magenta < pixels.Length/2 && unique > 8;
                }
                bool persisted = page != null && File.Exists(page.SourceImagePath)
                    && File.Exists(Path.Combine(CaptureDirectory,"CaptureProbe_99_0000_thumb.jpg"));
                ok = readback && photo && exclusion && persisted;
                diagnostics = "scene-readback="+readback+" original-size="+photo+" UI-excluded="+exclusion
                    +" persisted="+persisted+" marker-draws="+(Marker?.Draws ?? 0)+" magenta="+magenta+" unique="+unique
                    +" physical="+OriginalSnapshotCaptureScene.LastReadbackRect;
            }
            catch (Exception e) { diagnostics = e.GetType().Name+": "+e.Message; }
            finally { CancelCaptureCheck(); }
            return ok;
        }

        internal static void CancelCaptureCheck()
        {
            if (Marker != null) { CaptureGame?.Remove(Marker); Marker.Release(); Marker = null; }
            PendingScope?.Dispose(); PendingScope = null;
            if (CaptureAlbumScope != null)
            {
                CaptureAlbumScope.Dispose(); CaptureAlbumScope = null;
                UIOriginalCameraOverlay.CapturesRequested = BeforeRequests;
                UIOriginalCameraOverlay.LastFrameRect = BeforeFrame;
                UIOriginalCameraOverlay.LastCaptureRect = BeforeCapture;
            }
            CaptureGame = null;
            try { if (CaptureDirectory != null && Directory.Exists(CaptureDirectory)) Directory.Delete(CaptureDirectory,true); } catch { }
            CaptureDirectory = null;
        }

        private sealed class CaptureMarker : UIContainer
        {
            private readonly Rectangle Region;
            private readonly Texture2D Pixel;
            internal int Draws;
            internal CaptureMarker(Rectangle region)
            {
                Region = region;
                Pixel = new Texture2D(GameFacade.GraphicsDevice,1,1);
                Pixel.SetData(new[] {Color.Magenta});
            }
            public override void Draw(UISpriteBatch batch)
            {
                DrawLocalTexture(batch,Pixel,null,new Vector2(Region.X,Region.Y),new Vector2(Region.Width,Region.Height));
                Draws++;
            }
            internal void Release() { Pixel.Dispose(); }
        }

        private static Texture2D Pattern(int width, int height)
        {
            var texture = new Texture2D(GameFacade.GraphicsDevice,width,height);
            var pixels = new Color[width*height];
            for (int y=0;y<height;y++) for (int x=0;x<width;x++)
                pixels[y*width+x] = new Color((byte)(x*17+y*13),(byte)(x*7+y*23),(byte)(x*31+y*3),(byte)255);
            texture.SetData(pixels);
            return texture;
        }

        private static int FirstQuantizer(byte[] jpeg)
        {
            for (int i=2;i+5<jpeg.Length;)
            {
                if (jpeg[i++] != 255) return -1;
                byte marker = jpeg[i++];
                if (marker == 0xda || marker == 0xd9) return -1;
                int length = jpeg[i]*256+jpeg[i+1];
                if (length < 2 || i+length > jpeg.Length) return -1;
                if (marker == 0xdb) return (jpeg[i+2] >> 4) == 0 ? jpeg[i+3] : jpeg[i+3]*256+jpeg[i+4];
                i += length;
            }
            return -1;
        }
    }
}
