using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FSO.Client;
using FSO.Client.UI.Framework;
using FSO.Common.Rendering.Framework.Model;
using FSO.LotView;
using FSO.LotView.Components;
using FSO.SimAntics;
using FSO.SimAntics.Model;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Simitone.Client.UI.Model;
using Simitone.Client.UI.Panels;
using Simitone.Client.UI.Screens;

namespace Simitone.Client
{
    // Runs in the real graphics loop, with temporary album and restored camera,
    // settings, time and pending-capture state. No owner data is written.
    internal sealed class AutotestPIPUI240 : UIContainer, IDisposable
    {
        private readonly TS1GameScreen Game;
        private readonly World World;
        private readonly UIOriginalPictureInPicture Window;
        private readonly IDisposable AlbumScope, CaptureScope;
        private readonly string DirectoryName;
        private readonly Vector2 Center;
        private readonly Vector3? RotationAnchor;
        private readonly AvatarComponent Anchor;
        private readonly sbyte Level;
        private readonly WorldRotation Rotation;
        private readonly int Speed;
        private readonly bool Live, AutoCenter, AutoSnapshot, InterfaceFX;
        private readonly List<string> Failures = new List<string>();
        private int Phase;
        private long Time = 1000;
        private VMEntity First, Second;
        internal bool Ready { get; private set; }
        internal string Diagnostics => Failures.Count == 0 ? "size/zoom, all rotations and avatar renders; camera/GPU preserved; ownership/timer/options/input; both automatic-photo routes OK" : string.Join(", ", Failures);
        internal bool Passed => Ready && Failures.Count == 0;
        internal bool FadePassed { get; private set; }
        internal string FadeDiagnostics { get; private set; }
        internal AutotestPIPUI240(TS1GameScreen game)
        {
            Game = game; World = game.LotControl.World;
            Center = World.State.CenterTile; RotationAnchor = World.State.Camera2D.RotationAnchor; Level = World.State.Level; Rotation = World.State.Rotation; Anchor = World.State.ScrollAnchor;
            Speed = game.vm.SpeedMultiplier;
            Live = GlobalSettings.Default.TS1LivePIP; AutoCenter = GlobalSettings.Default.TS1AutoCenter;
            AutoSnapshot = GlobalSettings.Default.TS1AutoSnapshot;
            InterfaceFX = GlobalSettings.Default.TS1InterfaceFX;
            DirectoryName = Path.Combine(Path.GetTempPath(), "simitone-r240-pip-"+Guid.NewGuid().ToString("N"));
            AlbumScope = OriginalSnapshotAlbum.IsolateForTest(DirectoryName);
            CaptureScope = OriginalSnapshotCaptureScene.IsolatePendingForTest();
            OriginalSnapshotAlbum.ResetForFamily("PIPProbe_240");
            Window = new UIOriginalPictureInPicture(game, World) { Clock = () => Time };
            Add(Window);
            game.vm.SpeedMultiplier = 0;
            GlobalSettings.Default.TS1LivePIP = true;
            GlobalSettings.Default.TS1AutoCenter = true;
            GlobalSettings.Default.TS1AutoSnapshot = false;
            GlobalSettings.Default.TS1InterfaceFX = false;
        }
        private void Require(bool value, string name) { if (!value) Failures.Add(name); }
        private VMTS1PIPEvent Event(VMEntity entity, int size=1, int zoom=2, int duration=0, bool open=true,
            bool main=false, bool snapshot=false, string caption="") => new VMTS1PIPEvent(entity,open,false,duration,size,zoom,main,snapshot,caption);
        public override void PreDraw(UISpriteBatch batch)
        {
            if (Ready || !World.PictureInPictureReady) return;
            try
            {
                if (Phase++ == 0)
                {
                    var targets = Game.vm.Entities.Where(e => e.WorldUI is ObjectComponent obj && obj.Visible && obj.DGRP != null && obj.Room > 1 && e.Position.TileX>1 &&
                        e.Position.TileY>1 && e.Position.TileX<Game.vm.Context.Blueprint.Width-1 && e.Position.TileY<Game.vm.Context.Blueprint.Height-1 &&
                        e.Position.Level==Level).OrderBy(e => Vector2.DistanceSquared(new Vector2(e.Position.TileX,e.Position.TileY),new Vector2(Game.vm.Context.Blueprint.Width/2,Game.vm.Context.Blueprint.Height/2))).Take(2).ToArray();
                    if (targets.Length != 2) throw new Exception("Two rendered objects required");
                    First = targets[0]; Second = targets[1];
                    World.State.CenterTile = new Vector2(2,2); // target is outside the main viewport
                    GameLog.Write("PIP fixture target="+First.ToString()+" tile="+First.Position);
                    var output = Path.Combine(FSO.Common.FSOEnvironment.UserDir,"ui-audit","r240"); Directory.CreateDirectory(output);
                    for(int size=0; size<3; size++) for(int zoom=0; zoom<4; zoom++)
                    {
                        var gd=GameFacade.GraphicsDevice; var viewport=gd.Viewport; var bindings=gd.GetRenderTargets();
                        var center=World.State.CenterTile; var level=World.State.Level; var rotation=World.State.Rotation;
                        var mainZoom=World.State.Zoom;
                        Window.Handle(Event(First,size,zoom));
                        Window.PreDraw(batch);
                        int pixels=(size+1)*100;
                        Require(Window.Visible && Window.Image.Width==pixels && Window.Image.Height==pixels,"native-size"+pixels+"z"+zoom);
                        var close=Window.GetChildren().OfType<FSO.Client.UI.Controls.UIButton>().Single();
                        Require(close.Size.X>0 && close.Position.X+close.Size.X==pixels-4 && close.Position.Y==4,"native-close-button-position");
                        Require(Window.Position==new Vector2(Game.ScreenWidth-pixels-5,Game.ScreenHeight-pixels-105),"native-position"+pixels);
                        Require(World.State.CenterTile==center && World.State.Level==level && World.State.Rotation==rotation &&
                            World.State.Zoom==mainZoom && gd.Viewport.Equals(viewport) && gd.GetRenderTargets().SequenceEqual(bindings),"main-state-preserved"+size+zoom);
                        var data=new Color[pixels*pixels]; Window.Image.GetData(data);
                        Require(data.Select(c=>c.PackedValue).Distinct().Take(20).Count()>=20,"render-has-world-detail"+size+zoom);
                        using(var file=File.Create(Path.Combine(output,"pip-"+pixels+"-zoom"+zoom+".png"))) Window.Image.SaveAsPng(file,pixels,pixels);
                    }
                    foreach(WorldRotation rotation in new[]{WorldRotation.TopLeft,WorldRotation.TopRight,WorldRotation.BottomRight,WorldRotation.BottomLeft})
                    {
                        World.State.Rotation=rotation;
                        Window.Handle(Event(First)); Window.PreDraw(batch);
                        Require(World.State.Rotation==rotation && Window.Image.Width==200,"independent-rotation"+rotation);
                        using(var file=File.Create(Path.Combine(output,"pip-rotation-"+rotation+".png"))) Window.Image.SaveAsPng(file,200,200);
                    }
                    World.State.Rotation=Rotation;
                    var avatar=Game.vm.Entities.OfType<VMAvatar>().Where(e=>e.Position.Level==Level && e.Position.TileX>0 && e.WorldUI.Visible).OrderBy(e=>e.WorldUI.Room==1?0:1).FirstOrDefault();
                    Require(avatar!=null,"avatar-fixture-present");
                    if(avatar!=null)
                    {
                        Window.Handle(Event(avatar)); Window.PreDraw(batch);
                        var foot=Window.ProjectToImage(avatar.WorldUI.Position);
                        var translation=avatar.WorldUI.World.Translation;
                        var meshFoot=Window.ProjectToImage(new Vector3(translation.X,translation.Z,translation.Y)/3);
                        var pelvis=Window.ProjectToImage(((AvatarComponent)avatar.WorldUI).GetPelvisPosition());
                        var expectedFoot=new Vector2(100,100-UIOriginalPictureInPicture.AvatarVerticalOffset((int)World.State.Zoom));
                        Require(Vector2.Distance(foot,expectedFoot)<2 && Vector2.Distance(meshFoot,expectedFoot)<2,"avatar-physical-foot-centered");
                        GameLog.Write("PIP avatar="+avatar.Name+" logic="+avatar.Position+" visual="+avatar.WorldUI.Position+
                            " stored="+((AvatarComponent)avatar.WorldUI).StoredPosition+" foot="+foot+" meshFoot="+meshFoot+
                            " pelvis="+pelvis+" expected="+expectedFoot+" center="+Window.ImageCenterTarget+" targetScreen="+Window.ImageTargetScreen);
                        using(var file=File.Create(Path.Combine(output,"pip-avatar.png"))) Window.Image.SaveAsPng(file,200,200);
                    }
                    Require(UIOriginalPictureInPicture.WindowPosition(800,600,200,15)==new Vector2(595,245),"doublebyte-position");
                    Require(UIOriginalPictureInPicture.WindowPosition(800,600,200,1,true)==new Vector2(595,395),"mini-position");
                    Require(UIOriginalPictureInPicture.SizeForIndex(255)==300,"unsigned-large-size");
                    Window.Handle(Event(First,duration:1000)); Window.Handle(Event(Second));
                    Require(Window.Target==First,"timed-owner-blocks-replacement");
                    Window.Handle(Event(Second,open:false)); Require(Window.Visible,"foreign-close-ignored");
                    Window.Handle(Event(First,duration:2000)); Time=2100;
                    var state=new UpdateState { WindowFocused=true, Time=new GameTime() };
                    Window.Update(state); Require(Window.Visible,"same-owner-resets-timer");
                    Time=3100; Window.Update(state); Require(!Window.Visible,"timer-expires-with-paused-simulation");
                    Window.Handle(Event(Second)); Require(Window.Target==Second && Window.Visible,"expired-owner-replaceable");
                    state.NewKeys.Add(Keys.Escape); Window.Update(state); Require(!Window.Visible,"escape-hides");
                    state.NewKeys.Clear();
                    Window.Handle(Event(First,duration:-1)); Window.Handle(Event(Second)); Require(Window.Target==First,"negative-duration-ownership");
                    Window.Close(); Window.Handle(Event(First));
                    var button=Window.GetChildren().OfType<FSO.Client.UI.Controls.UIButton>().Single();
                    var mouse=typeof(FSO.Client.UI.Controls.UIButton).GetMethod("OnMouseEvent",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                    mouse.Invoke(button,new object[]{FSO.Common.Rendering.Framework.IO.UIMouseEventType.MouseDown,state});
                    mouse.Invoke(button,new object[]{FSO.Common.Rendering.Framework.IO.UIMouseEventType.MouseUp,state});
                    Require(!Window.Visible && Window.Target==null,"native-close-button-click");
                    Window.Handle(Event(First)); Window.PreDraw(batch);
                    int renders=Window.RenderCount; GlobalSettings.Default.TS1LivePIP=false; Window.PreDraw(batch);
                    Require(Window.RenderCount==renders && Window.Visible,"live-option-freezes-existing-image");
                    var suppressed=Event(Second); Window.Handle(suppressed); Require(suppressed.Suppressed && Window.Target==First,"live-option-suppresses-new-event");
                    GlobalSettings.Default.TS1LivePIP=true;
                    Window.ActivateTarget(); Require(!Window.Visible && World.State.ScrollAnchor==null && World.State.Level==First.Position.Level,"click-centers-and-hides");
                    GlobalSettings.Default.TS1AutoSnapshot=true;
                    Window.Handle(Event(First,snapshot:true,caption:"One automatic PIP photograph")); Window.PreDraw(batch); Window.PreDraw(batch);
                    Require(OriginalSnapshotAlbum.Pages.Count==1 && OriginalSnapshotAlbum.Pages[0].Description=="One automatic PIP photograph" &&
                        OriginalSnapshotAlbum.Pages[0].Photo.Width==200,"one-shot-pip-photo-caption");
                    Window.Close();
                    GlobalSettings.Default.TS1AutoCenter=false;
                    Window.Handle(Event(Second,main:true,snapshot:true));
                    Require(!Window.Visible,"main-route-does-not-open-window");
                    GlobalSettings.Default.TS1AutoCenter=true;
                    Window.Handle(Event(Second,open:false,main:true,snapshot:true,caption:"Main camera photograph"));
                }
                else
                {
                    Require(OriginalSnapshotAlbum.Pages.Count==2 && OriginalSnapshotAlbum.Pages[1].Description=="Main camera photograph","main-route-deferred-photo-caption");
                    var readback = OriginalSnapshotCaptureScene.LastReadbackRect;
                    int expectedHeight = World.State.Zoom == FSO.LotView.WorldZoom.Near ? 300 : 150;
                    Require(readback.Width==200 && readback.Height==expectedHeight &&
                        Math.Abs(readback.Center.X-GameFacade.GraphicsDevice.Viewport.Width/2)<=64,"main-photo-physical-frame");
                    Require(OriginalSnapshotAlbum.Pages.Last().Photo.Width==200 && OriginalSnapshotAlbum.Pages.Last().Photo.Height==expectedHeight,"main-photo-native-dimensions");
                    GameLog.Write("PIP main-photo physical="+readback+" viewport="+GameFacade.GraphicsDevice.Viewport);
                    Require(OriginalSnapshotAlbum.Pages.All(p=>p.FileName.StartsWith("PIPProbe_240_",StringComparison.Ordinal)),"temporary-family-isolation");
                    FadePassed = AutotestPIPFade243.Check(Game, First, Second, batch, out string fadeDetails);
                    FadeDiagnostics = fadeDetails;
                    Window.Handle(Event(First,caption:"Original event view")); Window.PreDraw(batch);
                    Ready=true;
                }
            }
            catch(Exception e) { Failures.Add(e.ToString()); Ready=true; }
        }
        public void Dispose()
        {
            Window.Dispose(); CaptureScope.Dispose(); AlbumScope.Dispose();
            World.State.CenterTile=Center; World.State.Level=Level; World.State.Rotation=Rotation; World.State.ScrollAnchor=Anchor; World.State.Camera2D.RotationAnchor=RotationAnchor;
            Game.vm.SpeedMultiplier=Speed;
            GlobalSettings.Default.TS1LivePIP=Live; GlobalSettings.Default.TS1AutoCenter=AutoCenter; GlobalSettings.Default.TS1AutoSnapshot=AutoSnapshot;
            GlobalSettings.Default.TS1InterfaceFX=InterfaceFX;
            try { if(Directory.Exists(DirectoryName)) Directory.Delete(DirectoryName,true); } catch { }
        }
    }
}
