using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Common.Rendering.Framework.IO;
using FSO.Common.Rendering.Framework.Model;
using FSO.SimAntics;
using FSO.SimAntics.Model;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Simitone.Client.UI.Panels;
using Simitone.Client.UI.Screens;

namespace Simitone.Client
{
    // Exercises the production PIP, including whole-window drawing, through a
    // deterministic wall clock. Runs inside R240's isolated album/camera scope.
    internal static class AutotestPIPFade243
    {
        internal static bool Check(TS1GameScreen game, VMEntity first, VMEntity second,
            UISpriteBatch outerBatch, out string diagnostics)
        {
            var failures = new List<string>();
            Action<bool, string> require = (ok, name) => { if (!ok) failures.Add(name); };
            var settings = GlobalSettings.Default;
            bool effects = settings.TS1InterfaceFX, live = settings.TS1LivePIP, snapshot = settings.TS1AutoSnapshot;
            var gd = GameFacade.GraphicsDevice;
            var targets = gd.GetRenderTargets(); var viewport = gd.Viewport;
            var blend = gd.BlendState; var depth = gd.DepthStencilState; var raster = gd.RasterizerState;
            var sampler = gd.SamplerStates[0]; var texture = gd.Textures[0]; var scissor = gd.ScissorRectangle;
            var root = new UIContainer { ScaleX = UIScreen.Current.ScaleX, ScaleY = UIScreen.Current.ScaleY };
            long time = 10000;
            var window = new UIOriginalPictureInPicture(game, game.LotControl.World) { Clock = () => time };
            root.Add(window);
            var state = new UpdateState { WindowFocused = true, Time = new GameTime() };
            VMTS1PIPEvent request(VMEntity owner, int size = 1, int duration = 0, int zoom = 2)
                => new VMTS1PIPEvent(owner, true, false, duration, size, zoom, false, false, "Original event view");
            void tick(int elapsed) { time += elapsed; window.Update(state); }
            outerBatch.Pause();
            try
            {
                settings.TS1LivePIP = true; settings.TS1AutoSnapshot = false;
                int width = (int)Math.Ceiling(game.ScreenWidth * root.ScaleX);
                int height = (int)Math.Ceiling(game.ScreenHeight * root.ScaleY);
                var output = Path.Combine(FSO.Common.FSOEnvironment.UserDir, "ui-audit", "r243");
                Directory.CreateDirectory(output);
                var underlay = new Color(45, 65, 45);
                using (var target = new RenderTarget2D(gd, width, height, false, SurfaceFormat.Color, DepthFormat.None))
                using (var batch = new UISpriteBatch(gd, 0))
                {
                    Color[] capture(string name)
                    {
                        Prime(root);
                        gd.SetRenderTarget(target); gd.Clear(underlay);
                        batch.UIBegin(BlendState.AlphaBlend, SpriteSortMode.Immediate);
                        window.Draw(batch); batch.End();
                        gd.SetRenderTargets(targets);
                        var pixels = new Color[width*height]; target.GetData(pixels);
                        using (var stream = File.Create(Path.Combine(output, "pip-fade-"+name+".png")))
                            target.SaveAsPng(stream, width, height);
                        return pixels;
                    }
                    for (int size = 0; size < 3; size++)
                    {
                        settings.TS1InterfaceFX = false; window.Close(); tick(1000);
                        window.Handle(request(first, size));
                        batch.UIBegin(BlendState.AlphaBlend, SpriteSortMode.Immediate);
                        window.PreDraw(batch); batch.End();
                        var reference = capture(size+"-opaque");
                        require(reference.Count(c => c != underlay) > 5000, "opaque-world-and-chrome-"+size);
                        window.Hide(); require(!window.Visible, "effects-off-immediate-hide");
                        settings.TS1InterfaceFX = true;
                        window.Handle(request(first, size));
                        require(window.Visible && window.WillDraw() && window.FadeOpacity == 0, "opening-logical-input");
                        tick(166);
                        require(window.FadeOpacity == 127, "native-opening-midpoint");
                        require(capture(size+"-opening166").All(c => c == underlay), "no-intermediate-window-or-children");
                        // Same-direction requests must not restart the ramp.
                        window.Handle(request(first, size)); tick(157);
                        require(window.FadeOpacity == 247, "opening323-alpha247");
                        require(capture(size+"-opening323").All(c => c == underlay), "last-hidden-bucket");
                        tick(1);
                        require(window.FadeOpacity == 248, "opening324-alpha248");
                        require(capture(size+"-opening324").SequenceEqual(reference), "whole-window-opaque-at248");
                        tick(9); require(window.FadeOpacity == 255 && window.Visible, "opening-completes333");
                        window.Hide(); tick(9);
                        require(capture(size+"-closing9").SequenceEqual(reference), "closing9-still-opaque");
                        window.Hide(); tick(1);
                        require(window.FadeOpacity == 247 && window.Visible && window.WillDraw(), "closing10-still-input-eligible");
                        require(capture(size+"-closing10").All(c => c == underlay), "closing10-no-pixels");
                        tick(322); require(window.Visible, "closing332-logically-visible");
                        tick(1); require(!window.Visible && !window.WillDraw(), "closing333-complete");
                    }

                    // Escape retains timed ownership until hiding completes; timer
                    // expiry and explicit close release it before that completion.
                    window.Handle(request(first, duration:5000)); tick(333);
                    state.NewKeys.Add(Keys.Escape); window.Update(state); state.NewKeys.Clear();
                    window.Handle(request(second)); require(window.Target == first && window.Visible, "escape-retains-timed-owner");
                    tick(332); window.Handle(request(second)); require(window.Target == first, "escape-owner-through332");
                    tick(1); window.Handle(request(second)); require(window.Target == second, "escape-owner-released333");
                    tick(333); window.Close(); tick(333);
                    window.Handle(request(first, duration:50)); tick(50);
                    require(window.Visible && window.Target == first, "short-timer-starts-reversed-close");
                    window.Handle(request(second)); require(window.Target == second, "timer-immediately-releases-owner");
                    tick(333); window.Close(); tick(333);
                    window.Handle(request(first, duration:5000)); tick(333); window.Close();
                    require(window.Target == null && window.Visible, "explicit-close-clears-target-before-fade");
                    window.Handle(request(second)); require(window.Target == second, "explicit-close-immediately-releases-duration");
                    tick(333); window.Close(); tick(333);
                    window.Handle(request(first, duration:-1)); tick(1000); window.Handle(request(second));
                    require(window.Target == first && window.Visible, "negative-owner-no-expiry");
                    // Same-owner invalid opens unsubscribe the previous timer.
                    window.Handle(request(first, zoom:4)); window.Handle(request(second));
                    require(window.Target == second, "invalid-same-owner-clears-duration");
                    window.Close(); tick(333);

                    window.Handle(request(first)); tick(100); window.Hide();
                    require(window.FadeOpacity == 77, "open-close-preserves-value");
                    tick(50); require(window.FadeOpacity == 38, "reversed-close-half");
                    window.Handle(request(first));
                    require(window.FadeOpacity == 217, "close-open-native-complement");
                    tick(282); require(window.FadeOpacity == 255, "reversed-open-rounding");
                    window.Close(); tick(333);

                    window.Handle(request(first)); tick(166);
                    var button = window.GetChildren().OfType<UIButton>().Single();
                    require(button.WillDraw(), "closebox-logical-eligibility-during-hidden-pixels");
                    var mouse = typeof(UIButton).GetMethod("OnMouseEvent", System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                    mouse.Invoke(button, new object[] { UIMouseEventType.MouseDown, state });
                    mouse.Invoke(button, new object[] { UIMouseEventType.MouseUp, state });
                    require(window.Target == null && window.Visible, "close-button-clears-target-before-hide-completion");
                    tick(166); require(!window.Visible, "close-button-completes-reversed-ramp");
                    window.Handle(request(first)); tick(333);
                    state.WindowFocused = false; state.NewKeys.Add(Keys.Escape); window.Update(state);
                    require(window.Visible && window.FadeOpacity == 255, "unfocused-escape-ignored");
                    state.WindowFocused = true; window.Update(state); state.NewKeys.Clear(); tick(333);
                    require(!window.Visible && window.Target == first, "escape-retains-target");

                    // Controlled viewport bounds exercise the real skip route:
                    // ownership is installed before the visibility early return.
                    var worldState = game.LotControl.World.State;
                    var skippedOwner = game.vm.Entities.FirstOrDefault(e =>
                    {
                        if (e.Position.Level != worldState.Level || e.Position.TileX <= 0 || e.Position.TileY <= 0 ||
                            e.Position.TileX >= game.vm.Context.Blueprint.Width-1 || e.Position.TileY >= game.vm.Context.Blueprint.Height-1) return false;
                        var members = e.MultitileGroup?.Objects;
                        if (members == null || members.Count != 1 || !(e.WorldUI is FSO.LotView.Components.ObjectComponent obj)) return false;
                        obj.ValidateSprite(worldState);
                        return !obj.Bounding.IsEmpty;
                    });
                    require(skippedOwner != null, "single-object-known-damage-skip-fixture");
                    if (skippedOwner != null)
                    {
                        var savedRectangle = worldState.WorldRectangle;
                        try
                        {
                            worldState.WorldRectangle = new Rectangle(-100000, -100000, 200000, 200000);
                            window.Handle(new VMTS1PIPEvent(skippedOwner, true, true, 1000, 1, 2, false, false, ""));
                        }
                        finally { worldState.WorldRectangle = savedRectangle; }
                        require(!window.Visible && window.Target == skippedOwner, "skip-keeps-window-hidden");
                        tick(1500); window.Handle(request(skippedOwner == first ? second : first));
                        require(!window.Visible && window.Target == skippedOwner, "hidden-skipped-duration-owns-without-timer");
                        window.Handle(request(skippedOwner)); tick(333); window.Close(); tick(333);
                    }

                    // Option changes do not cancel a ramp; the next direct
                    // Show/Hide changes only visibility, matching native flags.
                    window.Handle(request(first)); tick(100);
                    settings.TS1InterfaceFX = false; window.Hide();
                    require(!window.Visible, "toggle-off-direct-hide");
                    tick(50); window.Handle(request(second));
                    require(window.Visible && window.FadeOpacity == 115, "direct-show-preserves-active-ramp");
                    tick(183); require(window.FadeOpacity == 255, "old-ramp-completes");
                    window.Close(); require(!window.Visible && window.Target == null, "effects-off-direct-close");
                    settings.TS1InterfaceFX = true; window.Handle(request(first)); tick(100);
                    settings.TS1InterfaceFX = false; window.Hide(); tick(50);
                    settings.TS1InterfaceFX = true; window.Handle(request(first));
                    require(!window.Visible, "reenabled-show-preserves-dormant-opening");
                    tick(183); window.Handle(request(first));
                    require(window.Visible && window.FadeOpacity == 0, "expired-dormant-opening-starts-fresh");
                    tick(333);
                    window.ActivateTarget(); require(window.Target == first && window.Visible, "image-click-hides-with-target-retained");
                    tick(333); require(!window.Visible, "image-click-hide-completed");
                    window.Handle(request(first)); tick(333); window.Close(); tick(100);
                    settings.TS1InterfaceFX = false; tick(100);
                    require(window.Visible && window.Target == null, "option-toggle-alone-keeps-explicit-close-ramp");
                    tick(133); require(!window.Visible, "explicit-close-keeps-original-end");
                    settings.TS1InterfaceFX = true;
                    window.Handle(request(first)); window.Dispose();
                    require(!window.Visible && window.Target == null && window.Image == null, "dispose-aborts-ramp-and-renderer");
                }
            }
            catch (Exception ex) { failures.Add(ex.ToString()); }
            finally
            {
                root.Remove(window); window.Dispose();
                settings.TS1InterfaceFX = effects; settings.TS1LivePIP = live; settings.TS1AutoSnapshot = snapshot;
                gd.SetRenderTargets(targets); gd.Viewport = viewport; gd.ScissorRectangle = scissor;
                gd.BlendState = blend; gd.DepthStencilState = depth; gd.RasterizerState = raster;
                gd.SamplerStates[0] = sampler; gd.Textures[0] = texture;
                outerBatch.Resume();
            }
            diagnostics = failures.Count == 0
                ? "native 333ms/248-opacity threshold; 18 whole-window GPU captures at all sizes; ownership, reversals, input, option changes and disposal pass"
                : string.Join("; ", failures);
            return failures.Count == 0;
        }
        private static void Prime(UIElement element)
        {
            element.CalculateMatrix(); _ = element.BlendColor;
            if (element is UIContainer container) foreach (var child in container.GetChildren()) Prime(child);
        }
    }
}
