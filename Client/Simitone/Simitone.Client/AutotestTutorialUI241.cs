using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Common.Rendering.Framework.Model;
using FSO.Content;
using FSO.Files.Formats.IFF.Chunks;
using FSO.SimAntics;
using FSO.SimAntics.Engine;
using FSO.SimAntics.Entities;
using FSO.SimAntics.Model;
using FSO.SimAntics.Model.TS1Platform;
using FSO.SimAntics.Primitives;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Simitone.Client.UI.Panels;
using Simitone.Client.UI.Screens;

namespace Simitone.Client
{
    // Actual VM dialog sink and GPU drawing, using an isolated unticked VM.
    // The active household, settings and its dialogs are never replaced.
    internal static class AutotestTutorialUI241
    {
        internal static bool Check(TS1GameScreen game, out string diagnostics)
        {
            var failures = new List<string>();
            Action<bool, string> require = (ok, label) => { if (!ok) failures.Add(label); };
            bool useWorld = VM.UseWorld, globalTS1 = VM.GlobTS1;
            var gd = GameFacade.GraphicsDevice;
            var targets = gd.GetRenderTargets(); var viewport = gd.Viewport;
            var blend = gd.BlendState; var depth = gd.DepthStencilState;
            var rasterizer = gd.RasterizerState; var sampler = gd.SamplerStates[0];
            var oldTexture = gd.Textures[0];
            UILotControl control = null;
            var root = new UIContainer { ScaleX = UIScreen.Current.ScaleX, ScaleY = UIScreen.Current.ScaleY };
            try
            {
                VM.UseWorld = false;
                var context = new VMContext(null);
                var vm = new VM(context, new FSO.SimAntics.NetPlay.Drivers.VMServerDriver(null), null)
                    { PlatformState = new VMTS1LotState() };
                context.RoomInfo = new[] { new VMRoomInfo { Entities = new List<VMEntity>() } };
                var definition = Content.Get().WorldObjects.Get(0xc3249a1d);
                if (definition == null) throw new Exception("Original tutorial object missing");
                var owner = new VMGameObject(definition, null);
                owner.Thread = new VMThread(context, owner, 4);
                vm.AddEntity(owner);
                context.SetTutorialObject(owner);
                control = new UILotControl(vm, game.LotControl.World);
                root.Add(control);

                var operand = new VMDialogOperand { Type = VMDialogType.Sims1Tutorial,
                    Flags = VMDialogFlags.Continue | VMDialogFlags.NewEngageContinue | VMDialogFlags.Unknown1 };
                var info = new VMDialogInfo { Caller = owner, Operand = operand, DialogID = 241,
                    Title = "Suppressed tutorial title", Message = definition.Resource.Get<STR>(301).GetString(2),
                    IconResource = definition.Resource, Block = false };
                vm.SignalDialog(info);
                var presenter = control.TutorialPresenter;
                var dialog = presenter.CurrentDialog;
                require(presenter != null && dialog != null, "production-dialog-sink");
                long time = 1000;
                presenter.Clock = () => time;
                // Restart the initial transition against deterministic time.
                presenter.SetOwner(null); presenter.SetOwner(owner); presenter.Open(dialog);
                int width = UIScreen.Current.ScreenWidth, height = UIScreen.Current.ScreenHeight;
                require(presenter.Home == new Rectangle(width-46, 1, 45, 45), "45px-icon-one-pixel-inset");
                require(dialog.TitleTextForProbe == "" && dialog.ButtonMap.Count == 0, "guid-title-and-nonmodal-bottom-row");
                require(Math.Abs(dialog.Opacity-200f/255f) < .0001f, "native-opacity200");
                var close = dialog.TutorialCloseBoxForProbe;
                require(close != null && close.Size.X > 0 && close.X+close.Size.X == dialog.BoxWidth-5 && close.Y == 5,
                    "native-closebox31-natural-size");
                require(dialog.X+dialog.BoxWidth == width-1 && dialog.Y == 1, "initial-top-right-anchor");
                require(dialog.OriginalBodyTopForProbe == 21, "titleless-body-origin");

                var output = Path.Combine(FSO.Common.FSOEnvironment.UserDir, "ui-audit", "r241");
                Directory.CreateDirectory(output);
                int physicalWidth = (int)Math.Ceiling(width*root.ScaleX);
                int physicalHeight = (int)Math.Ceiling(height*root.ScaleY);
                using (var target = new RenderTarget2D(gd, physicalWidth, physicalHeight, false, SurfaceFormat.Color, DepthFormat.None))
                using (var batch = new UISpriteBatch(gd, 0))
                {
                    Action<string, UIMobileAlert> capture = (name, current) =>
                    {
                        Prime(root);
                        if (current != null) Prime(current);
                        batch.UIBegin(BlendState.AlphaBlend, SpriteSortMode.Immediate);
                        current?.PreDraw(batch);
                        batch.End();
                        gd.SetRenderTarget(target); gd.Clear(new Color(45, 65, 45));
                        // Use the production UI sampler for both the opaque
                        // reference and composite. PointClamp only in the
                        // reference falsely compares two rasterization laws at
                        // fractional DPI (UILayer uses UIBegin/LinearClamp).
                        batch.UIBegin(BlendState.AlphaBlend, SpriteSortMode.Immediate);
                        presenter.Draw(batch);
                        current?.Draw(batch);
                        batch.End(); gd.SetRenderTargets(targets);
                        using (var stream = File.Create(Path.Combine(output, "tutorial-"+name+".png")))
                            target.SaveAsPng(stream, physicalWidth, physicalHeight);
                    };
                    require(!dialog.Visible && presenter.Animating, "dialog-hidden-at-zero");
                    time += 250; presenter.Advance();
                    capture("opening", dialog);
                    var outline = presenter.Outline;
                    var pixels = new Color[physicalWidth*physicalHeight]; target.GetData(pixels);
                    // Every pixel of all four outside edges must be black,
                    // including corners at fractional display scaling.
                    int left=(int)(outline.Left*root.ScaleX), right=(int)(outline.Right*root.ScaleX);
                    int top=(int)(outline.Top*root.ScaleY), bottom=(int)(outline.Bottom*root.ScaleY);
                    bool continuous = true;
                    for (int x=left; x<right; x++) continuous &= pixels[top*physicalWidth+x]==Color.Black && pixels[(bottom-1)*physicalWidth+x]==Color.Black;
                    for (int y=top; y<bottom; y++) continuous &= pixels[y*physicalWidth+left]==Color.Black && pixels[y*physicalWidth+right-1]==Color.Black;
                    require(continuous, "rendered-outline-four-edges-continuous");
                    time += 249; presenter.Advance(); require(!dialog.Visible, "hidden-at499ms");
                    time++; presenter.Advance(); require(dialog.Visible && !presenter.Animating, "shown-at500ms");
                    dialog.OriginalOpacityMultiplier=1; dialog.Opacity=1;
                    capture("opaque-reference", dialog);
                    var opaquePixels = new Color[physicalWidth*physicalHeight]; target.GetData(opaquePixels);
                    dialog.OriginalOpacityMultiplier=200f/255f; dialog.Opacity=200f/255f;
                    capture("open", dialog);
                    target.GetData(pixels);
                    int compositeErrors=0;
                    for (int i=0; i<pixels.Length; i++)
                    {
                        var src=opaquePixels[i]; var actual=pixels[i];
                        if (Math.Abs(actual.R-(src.R*200+45*55)/255.0)>2 ||
                            Math.Abs(actual.G-(src.G*200+65*55)/255.0)>2 ||
                            Math.Abs(actual.B-(src.B*200+45*55)/255.0)>2) compositeErrors++;
                    }
                    require(compositeErrors==0, "whole-window-single-alpha-composite("+compositeErrors+")");
                    var fullViewport=gd.Viewport;
                    try
                    {
                        for (int size=1; size<=8; size++)
                        {
                            gd.Viewport=new Viewport(0,0,fullViewport.Width-size,fullViewport.Height-size);
                            batch.UIBegin(BlendState.AlphaBlend, SpriteSortMode.Immediate);
                            dialog.PreDraw(batch); batch.End();
                            require(dialog.TutorialBufferCountForProbe<=2, "bounded-resize-buffers"+size);
                        }
                    }
                    finally { gd.Viewport=fullViewport; }
                    var dialogBounds = new Rectangle((int)(dialog.X*root.ScaleX), (int)(dialog.Y*root.ScaleY),
                        (int)((dialog.X+dialog.BoxWidth)*root.ScaleX)-(int)(dialog.X*root.ScaleX),
                        (int)((dialog.Y+dialog.Height)*root.ScaleY)-(int)(dialog.Y*root.ScaleY));
                    int outside = 0, inside = 0;
                    for (int y=0; y<physicalHeight; y++) for (int x=0; x<physicalWidth; x++)
                        if (pixels[y*physicalWidth+x] != new Color(45,65,45))
                        { if (dialogBounds.Contains(x,y)) inside++; else outside++; }
                    require(inside>1000 && outside==0, "actual-dialog-art-and-text-contained("+inside+","+outside+")");

                    var otherModal = new UIContainer();
                    UIScreen.GlobalShowDialog(otherModal, true);
                    try
                    {
                        var modalKeys = new UpdateState { WindowFocused=true, NewKeys=new List<Keys> { Keys.Escape } };
                        dialog.HandleTutorialKeys(modalKeys);
                        require(dialog.Visible && modalKeys.NewKeys.Contains(Keys.Escape), "higher-nonalert-modal-owns-keys");
                    }
                    finally { UIScreen.RemoveDialog(otherModal); }

                    var oldPosition = dialog.Position;
                    info.DialogID++;
                    vm.SignalDialog(info);
                    var replacement = presenter.CurrentDialog;
                    require(!UIScreen.Current.GetChildren().Contains(dialog) && replacement != dialog && replacement.Position == oldPosition,
                        "replacement-aborts-old-and-remembers-position");
                    time += 500; presenter.Advance();
                    require(!replacement.Visible && presenter.Animating, "replacement-shrinks-before-open");
                    time += 500; presenter.Advance();
                    require(replacement.Visible, "replacement-opens-after1000ms");
                    var keys = new UpdateState { WindowFocused = false, NewKeys = new List<Keys> { Keys.Space } };
                    replacement.HandleTutorialKeys(keys);
                    require(replacement.Visible, "unfocused-key-ignored");
                    keys.WindowFocused = true;
                    replacement.HandleTutorialKeys(keys);
                    require(!UIScreen.Current.GetChildren().Contains(replacement) && presenter.CurrentDialog == null && !keys.NewKeys.Contains(Keys.Space),
                        "space-closes-and-consumes-before-world-hotkey");
                    time += 250; presenter.Advance(); capture("closing", null);
                    time += 250; presenter.Advance(); capture("icon", null);
                    require(!presenter.Animating, "close-completes-at500ms");

                    // Modal release must unregister immediately, not leave an
                    // invisible modal blocker for the shrink duration.
                    info.DialogID++; info.Block = true;
                    operand.Flags &= ~VMDialogFlags.NewEngageContinue;
                    vm.SignalDialog(info);
                    var modal = presenter.CurrentDialog;
                    time += 500; presenter.Advance();
                    require(modal.ButtonMap.Count == 1 && modal.TutorialCloseBoxForProbe == null, "modal-keeps-primary-bottom-button");
                    modal.Close();
                    require(!UIScreen.Current.GetChildren().Contains(modal) && GameFacade.Screens.TopVisibleDialog != modal,
                        "modal-unregistered-at-shrink-start");
                    context.SetTutorialObject(null);
                    require(!presenter.Animating && presenter.CurrentDialog == null, "owner-release-cancels-animation");

                    // GUID title/opacity rules are independent of ownership.
                    info.DialogID++; info.Block=false;
                    operand.Flags |= VMDialogFlags.NewEngageContinue;
                    vm.SignalDialog(info);
                    var unowned = (UIMobileAlert)GameFacade.Screens.TopVisibleDialog;
                    unowned.InterpolatedAnimation=1;
                    require(unowned.TitleTextForProbe=="" && Math.Abs(unowned.Opacity-200f/255f)<.0001f &&
                        !unowned.TutorialPresentation, "unowned-guid-opacity-without-icon-animation");
                    UIScreen.RemoveDialog(unowned);

                    context.SetTutorialObject(owner);
                    info.DialogID++; operand.Type=VMDialogType.YesNo;
                    vm.SignalDialog(info);
                    var yesNo = presenter.CurrentDialog;
                    time += 500; presenter.Advance();
                    var space = new UpdateState { WindowFocused=true, NewKeys=new List<Keys> { Keys.Space } };
                    yesNo.HandleTutorialKeys(space);
                    require(yesNo.Visible && space.NewKeys.Contains(Keys.Space), "yes-no-does-not-bind-space");
                    context.SetTutorialObject(null);

                    context.SetTutorialObject(owner);
                    info.DialogID++; operand.Type=VMDialogType.Sims1Tutorial;
                    vm.SignalDialog(info);
                    var interrupted = presenter.CurrentDialog;
                    var lastTarget = new Rectangle((int)interrupted.X,(int)interrupted.Y,interrupted.BoxWidth,interrupted.Height);
                    time+=250; presenter.Advance();
                    info.DialogID++; vm.SignalDialog(info);
                    require(presenter.Outline==lastTarget, "mid-expansion-replacement-starts-at-last-target");
                    context.SetTutorialObject(null);

                    var otherDefinition = game.vm.Entities.OfType<VMGameObject>().Select(x=>x.Object)
                        .First(x=>x.OBJ.GUID!=0xc3249a1d && x.Resource.Get<BMP>(300)==null);
                    var otherOwner = new VMGameObject(otherDefinition,null);
                    otherOwner.Thread = new VMThread(context,otherOwner,4); vm.AddEntity(otherOwner);
                    context.SetTutorialObject(otherOwner);
                    info.DialogID++; info.Caller=otherOwner; operand.Type=VMDialogType.Message;
                    vm.SignalDialog(info);
                    var ownedMessage=presenter.CurrentDialog;
                    require(ownedMessage.TitleTextForProbe==info.Title && ownedMessage.Opacity==1,
                        "foreign-guid-owner-keeps-title-and-opacity");
                    require(presenter.Home.Width==45 && presenter.Home.Height==45,"missing-icon-native45px-fallback");
                    context.SetTutorialObject(null);
                }

                require(UIOriginalTutorial.Anchor(new Rectangle(10,10,20,20), new Point(100,80),800,600)==new Vector2(10,10), "anchor-TL");
                require(UIOriginalTutorial.Anchor(new Rectangle(770,10,20,20), new Point(100,80),800,600)==new Vector2(690,10), "anchor-TR");
                require(UIOriginalTutorial.Anchor(new Rectangle(10,570,20,20), new Point(100,80),800,600)==new Vector2(10,510), "anchor-BL");
                require(UIOriginalTutorial.Anchor(new Rectangle(770,570,20,20), new Point(100,80),800,600)==new Vector2(690,510), "anchor-BR");
                require(UIOriginalTutorial.Anchor(new Rectangle(390,290,20,20), new Point(100,80),800,600)==new Vector2(390,290), "anchor-tie-first");
                require(UIOriginalTutorial.Anchor(new Rectangle(780,10,40,40), new Point(100,80),800,600)==new Vector2(720,10), "anchor-no-clamp");
                var shortLesson = new UIMobileAlert(new UIAlertOptions { Title="", Message="One short lesson.", Buttons=Array.Empty<UIAlertButton>() }, true, 2.0);
                require(shortLesson.BoxWidth < 300 && shortLesson.BoxWidth >= 142, "short-lesson-native-halving-search");
                shortLesson.Removed();
            }
            catch (Exception ex) { failures.Add(ex.ToString()); }
            finally
            {
                if (control != null) root.Remove(control);
                VM.UseWorld = useWorld; VM.GlobTS1 = globalTS1;
                gd.SetRenderTargets(targets); gd.Viewport=viewport;
                gd.BlendState=blend; gd.DepthStencilState=depth; gd.RasterizerState=rasterizer;
                gd.SamplerStates[0]=sampler; gd.Textures[0]=oldTexture;
            }
            diagnostics = failures.Count==0 ? "isolated production dialog route; original icon/closebox/body/group opacity; timing/replacement/keys/modal cleanup; native anchors; five GPU captures pass"
                : string.Join("; ", failures);
            return failures.Count==0;
        }

        private static void Prime(UIElement element)
        {
            element.CalculateMatrix();
            _ = element.BlendColor;
            if (element is UIContainer container)
                foreach (var child in container.GetChildren()) Prime(child);
        }
    }
}
