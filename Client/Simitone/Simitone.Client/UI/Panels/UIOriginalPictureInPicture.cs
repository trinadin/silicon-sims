using System;
using System.Linq;
using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Common;
using FSO.Common.Rendering.Framework.IO;
using FSO.Common.Rendering.Framework.Model;
using FSO.Files.Formats.IFF.Chunks;
using FSO.LotView;
using FSO.LotView.Components;
using FSO.LotView.Model;
using FSO.SimAntics;
using FSO.SimAntics.Model;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Simitone.Client.UI.Controls;
using Simitone.Client.UI.Model;
using Simitone.Client.UI.Screens;

namespace Simitone.Client.UI.Panels
{
    // cWinPictureInPicture 298c90..29a368. Original dimensions, ownership,
    // timer and event routes; a separate world camera supplies the image.
    public sealed class UIOriginalPictureInPicture : UIContainer, IDisposable
    {
        private readonly TS1GameScreen Game;
        private readonly World World;
        private readonly UIMouseEventRef MouseRegion;
        private readonly UIButton CloseButton;
        private readonly UIOriginalPIPCaption Caption;
        private WorldPictureInPictureRenderer Renderer;
        private VMTS1PIPEvent Request;
        private long Deadline;
        private int OwnerDuration, FadeDirection;
        private long FadeEnd;
        private float FadeTo, FadeSlope;
        private int Pixels = 100;
        private bool NeedsImage, SnapshotPending;
        private Point LastMouse; //R244: the cursor input for the secondary-view cutaway composition
        private static readonly uint[] EmptyCutHistory = new uint[0]; //native clears cCutawaySet around the secondary render
        internal Func<long> Clock = () => Environment.TickCount64;
        public VMEntity Target => Request?.Target;
        public int RenderCount => Renderer?.RenderCount ?? 0;
        public Texture2D Image => Renderer?.Texture;
        internal Vector2 ProjectToImage(Vector3 tile) => Renderer?.ProjectToImage(tile) ?? new Vector2(float.NaN);
        internal Vector3 ImageCenterTarget => Renderer?.LastTarget ?? new Vector3(float.NaN);
        internal Vector2 ImageTargetScreen => Renderer?.LastTargetScreen ?? new Vector2(float.NaN);

        public override Vector2 Size { get => new Vector2(Pixels); set { } }
        public override Rectangle GetBounds() => new Rectangle(0, 0, Pixels, Pixels);

        public UIOriginalPictureInPicture(TS1GameScreen game, World world)
        {
            Game = game;
            World = world;
            MouseRegion = ListenForMouse(GetBounds(), OnMouse);
            var texture = UIOriginal.EnsureResolvedByID(30)?.Get(GameFacade.GraphicsDevice);
            CloseButton = new UIButton(texture) { ImageStates = 4 };
            CloseButton.OnButtonClick += _ => Close();
            Caption = new UIOriginalPIPCaption(OriginalGlyphFont.LoadByIndex(12, GameFacade.GraphicsDevice));
            Add(Caption);
            Add(CloseButton);
            Tooltip = GameFacade.Strings.GetString("156", "0");
            FSO.Client.Utils.UIUtils.GiveTooltip(this);
            Visible = false;
        }

        public static int SizeForIndex(int index) => index < 1 ? 100 : index == 1 ? 200 : 300;
        public static Vector2 WindowPosition(int width, int height, int size, byte language, bool miniUI = false)
            => new Vector2(width-size-5, height-size-(miniUI ? 5 : UIMainPanel.UsesTallSubpanel(language) ? 155 : 105));

        public void Handle(VMTS1PIPEvent request)
        {
            var settings = GlobalSettings.Default;
            if (!request.MainCameraRoute && !settings.TS1LivePIP) { request.Suppressed = true; return; }
            if (request.MainCameraRoute)
            {
                if (settings.TS1AutoCenter && Valid(request.Target))
                {
                    Center(request.Target);
                    if (request.AutoSnapshot && settings.TS1AutoSnapshot)
                    {
                        var target = request.Target;
                        OriginalSnapshotCaptureScene.RequestDeferred(() => MainSnapshotRect(target),
                            Game.Frontend?.MainPanel?.CameraChrome?.SnapshotQuality ?? 1, request.Caption, logicalCoordinates: false);
                    }
                }
                return;
            }
            if (!request.Open) { if (Target == request.Target) Close(); return; }
            // A nonzero duration owns the window, including native negative
            // durations (which never subscribe a timer).
            if (OwnerDuration != 0 && Target != request.Target) return;
            Deadline = 0;
            OwnerDuration = 0;
            if (!Valid(request.Target) || request.ZoomIndex > 3) return;
            Request = request;
            OwnerDuration = request.DurationMilliseconds;
            SnapshotPending = request.AutoSnapshot && settings.TS1AutoSnapshot;
            Caption.Text = SnapshotPending ? "" : request.Caption;
            if (request.SkipIfVisible && FullyVisible(request.Target)) return;
            Pixels = SizeForIndex(request.SizeIndex);
            MouseRegion.Region = GetBounds();
            CloseButton.Position = new Vector2(Pixels-CloseButton.Size.X-4, 4);
            Caption.Pixels = Pixels;
            GameResized();
            NeedsImage = true;
            if (request.DurationMilliseconds > 0) Deadline = Clock() + request.DurationMilliseconds;
            Fade(true);
        }

        private bool Valid(VMEntity target)
        {
            if (target?.WorldUI == null || Game.vm?.GetObjectById(target.ObjectID) != target || Game.vm.Context.Blueprint == null) return false;
            var pos = target.Position;
            return pos.TileX > 0 && pos.TileY > 0 && pos.TileX < Game.vm.Context.Blueprint.Width-1 &&
                pos.TileY < Game.vm.Context.Blueprint.Height-1 && pos.Level > 0 && pos.Level <= Game.vm.Context.Blueprint.Stories;
        }

        private bool FullyVisible(VMEntity target)
        {
            if (target.Position.Level != World.State.Level) return false;
            var visibleArea = World.State.WorldRectangle;
            int band = Game.Frontend?.Visible == false ? 0 : UIMainPanel.UsesTallSubpanel((byte)STR.DefaultLangCode) ? 150 : 100;
            visibleArea.Height = Math.Max(0,visibleArea.Height-(int)Math.Ceiling(band*FSOEnvironment.DPIScaleFactor/World.State.PreciseZoom));
            var objects = target.MultitileGroup?.Objects ?? new System.Collections.Generic.List<VMEntity> { target };
            foreach (var entity in objects)
            {
                // Sprite damage bounds are known; unknown avatar bounds must
                // not incorrectly suppress an offscreen event.
                if (!(entity.WorldUI is ObjectComponent component)) return false;
                component.ValidateSprite(World.State);
                if (component.Bounding.IsEmpty || !visibleArea.Contains(component.Bounding)) return false;
            }
            return objects.Count > 0;
        }

        private Vector3 CenterPoint()
        {
            // The port keeps routing interpolation and container-slot placement
            // on AvatarComponent, while native fixed locations track the actor.
            // Center on the same physical foot position that DrawAvatarMesh uses.
            if (Target.WorldUI is AvatarComponent actor)
            {
                var physical = actor.Position;
                physical.X = (float)Math.Round(physical.X * 16, MidpointRounding.AwayFromZero) / 16;
                physical.Y = (float)Math.Round(physical.Y * 16, MidpointRounding.AwayFromZero) / 16;
                return physical;
            }
            var members = Target.MultitileGroup?.Objects;
            var fixedPoint = members == null || members.Count == 0
                ? new Vector2(Target.Position.x, Target.Position.y)
                : members.Aggregate(Vector2.Zero, (sum, item) => sum + new Vector2(item.Position.x, item.Position.y)) / members.Count;
            // GetCenterOnPt (298e50): average the physical fixed-point locations,
            // rounding each coordinate to the nearest sixteenth, ties away from zero.
            // WorldUI.Position includes sprite-origin, interpolation and slot offsets.
            var point = new Vector3((float)Math.Round(fixedPoint.X, MidpointRounding.AwayFromZero) / 16,
                (float)Math.Round(fixedPoint.Y, MidpointRounding.AwayFromZero) / 16, (Target.Position.Level - 1) * 2.95f);
            point.Z += Game.vm.Context.Blueprint.InterpAltitude(point);
            return point;
        }

        private void Center(VMEntity target)
        {
            World.State.ScrollAnchor = null;
            World.CenterTo(target.WorldUI);
            Game.Level = target.Position.Level;
        }

        internal static int AvatarVerticalOffset(int mainZoom)
        {
            // BuildImage 299170..214: float(2 * float(2*sqrt(6))),
            // scaled by the main viewer's zoom, truncated, then divided by 2.
            float bodyHeight = 2f * (float)(2d * Math.Sqrt(6d));
            return -((int)(bodyHeight * (1 << (mainZoom+1))) / 2);
        }
        internal static int ObjectVerticalOffset(int damageHeight, int mainZoom, int requestedZoom)
            => -(damageHeight >> (mainZoom-requestedZoom+3));
        private int ScreenVerticalOffset()
        {
            if (Target is VMAvatar) return AvatarVerticalOffset((int)World.State.Zoom);
            if (Target.WorldUI is ObjectComponent component)
            {
                component.ValidateSprite(World.State);
                return ObjectVerticalOffset(component.Bounding.Height, (int)World.State.Zoom, Request.ZoomIndex);
            }
            return 0;
        }

        // R244: real view-specific cutaway inputs for the secondary render —
        // native DrawPictureInPicture recomputes cutaway for the TARGET floor
        // (never an all-false stand-in). The room history is empty: the native
        // secondary Render clears cCutawaySet on entering AND leaving the target
        // floor (decode.md §1.2A). The person is the same tracked/selected sim
        // the main view resolves (UILotControl.ResolvePerson), LIVE-MODE gated
        // (native CPState::GetMode()==2) and floor-gated to the target floor;
        // PersonTile is the avatar's tile (native person+0xfc/+0xfd) so the
        // decoded outside-person rectangle is live here too. The cursor rides
        // the main buffer bounds with the main suppression conditions (RMB
        // camera scroll / mouse off the lot).
        internal FSO.LotView.Utils.CutawayViewInputs BuildPipCutawayInputs(sbyte targetLevel, WorldZoom zoom)
        {
            var lot = Game.LotControl;
            if (lot == null || Game.vm?.Context?.Blueprint == null) return null;
            // UI-25 item 4b: native DrawPictureInPicture's floor-diff branch
            // (cmpw viewer+0x18,target 0x1c104c) clears the live hover history
            // on ENTER (0x1c1068) AND AGAIN on RESTORE (0x1c161c) and recomposes
            // the main mask without it — i.e. every cross-floor PIP render
            // wipes the history. Same-floor renders never reach this builder
            // (PreDraw passes null inputs there) and must not clear.
            if (targetLevel != World.State.Level) lot.ClearCutHistory();
            var viewport = GameFacade.GraphicsDevice.Viewport;
            // logical pixels — the same units as CursorScreenPos (below), so
            // the cursor-in-buffer gate is DPI-independent (identical to the
            // physical viewport at DPI 1)
            float dpi = FSOEnvironment.DPIScaleFactor;
            var inputs = new FSO.LotView.Utils.CutawayViewInputs
            {
                Floor = targetLevel,
                Rotation = World.State.Rotation, //the secondary view renders at the main rotation
                Zoom = zoom,
                PreciseZoom = (int)zoom == 0 ? 0.5f : 1f,
                BufferBounds = new Rectangle(0, 0, (int)(viewport.Width / dpi), (int)(viewport.Height / dpi)),
                CursorScreenPos = new Point((int)(LastMouse.X / FSOEnvironment.DPIScaleFactor), (int)(LastMouse.Y / FSOEnvironment.DPIScaleFactor)),
                CursorSuppressed = lot.RMBScroll || !lot.MouseIsOn,
                HistoryRooms = EmptyCutHistory,
                DynamicEnabled = lot.WallsMode == 1,
                // native shares the global mouse: the cursor tile resolves
                // through the PIP-swapped viewer — the TARGET floor's picking
                // level (UI-25 item 4c; World.EstTileAtPosWithScroll's level
                // argument closes the R244 cursor-frame deviation)
                ScreenToTile = pos => World.EstTileAtPosWithScroll(new Vector2(pos.X, pos.Y), targetLevel),
            };
            // native gates the person branch on CPState mode 2 (LIVE) — decode.md
            // §5; outside live mode the person inputs stay null.
            var person = lot.LiveMode ? lot.ResolvePerson() : null;
            if (person != null && person.Position != LotTilePos.OUT_OF_WORLD && person.Position.Level == targetLevel)
            {
                var room = Game.vm.Context.GetRoomAt(LotTilePos.FromBigTile(person.Position.TileX, person.Position.TileY, person.Position.Level));
                inputs.PersonRoomId = room;
                inputs.PersonFloor = person.Position.Level;
                inputs.PersonOutside = Game.vm.Context.RoomInfo[room].Room.IsOutside;
                inputs.PersonTile = new Point(person.Position.TileX, person.Position.TileY);
            }
            return inputs;
        }

        // The main-camera route captures the native tile footprint union,
        // computed after the newly centered world has rendered.
        private Rectangle MainSnapshotRect(VMEntity target)
        {
            if (!Valid(target)) return Rectangle.Empty;
            int zoom = (int)World.State.Zoom;
            int width = Math.Max(200 / (1 << (3-zoom)), 200);
            int height = Math.Max(300 / (1 << (3-zoom)), 150);
            var members = target.MultitileGroup?.Objects ?? new System.Collections.Generic.List<VMEntity> { target };
            Rectangle? union = null;
            foreach (var member in members)
            {
                var pos = member.Position;
                var blueprint = Game.vm.Context.Blueprint;
                var corner = NativeAltitudeCorner(World.State.Rotation);
                // TS1 import retains PackedAlt byte1 at ten managed height units
                // per native unit. Rotation selects the corresponding grid corner.
                var altitude = (blueprint.Altitude[(pos.TileY + corner.Y) * blueprint.Width + pos.TileX + corner.X]
                    - blueprint.BaseAlt) / 10;
                var point = World.State.WorldSpace.GetScreenFromTile(new Vector2(pos.TileX, pos.TileY))
                    + World.State.WorldSpace.GetPointScreenOffset();
                // ComputePixelTopOfTile 1bfc40; native floor lookup is 58,116,232.
                // Keep this native crop law local: managed world floor projection
                // is slightly taller and is not changed by snapshot framing.
                point.Y -= (58 << (zoom - 1)) * (pos.Level - 1) + (altitude << (zoom + 1));
                var tile = new Rectangle((int)point.X-(8 << zoom), (int)point.Y-(4 << zoom), 16 << zoom, 8 << zoom);
                union = union.HasValue ? Rectangle.Union(union.Value, tile) : tile;
            }
            if (!union.HasValue) return Rectangle.Empty;
            var u = union.Value;
            return new Rectangle((u.Left+u.Right)/2-width/2, (u.Top+u.Bottom)/2+u.Height-height, width, height);
        }

        internal static Point NativeAltitudeCorner(WorldRotation rotation)
        {
            // GetPackedAlt permutation (data4ad40), with shared-corner writes in
            // SetPackedAlt 162038..16218c: byte1,byte0,byte3,byte2 respectively.
            switch (rotation)
            {
                case WorldRotation.TopRight: return new Point(0, 1);
                case WorldRotation.BottomRight: return new Point(1, 1);
                case WorldRotation.BottomLeft: return new Point(1, 0);
                default: return Point.Zero;
            }
        }

        // Native RampGenerator stores an endpoint and slope, and evaluates
        // with a single-precision fused subtraction (14e890..14e970).
        private float FadeValue(long now) => now >= FadeEnd ? FadeTo
            : MathF.FusedMultiplyAdd(-(float)(FadeEnd-now), FadeSlope, FadeTo);
        internal int FadeOpacity => !Visible ? 0 : FadeDirection == 0 ? 255
            : (int)MathF.FusedMultiplyAdd(255, FadeValue(Clock()), .5f);
        private void HideWindow() { Visible = false; Deadline = 0; OwnerDuration = 0; }
        private void AdvanceFade(long now)
        {
            if (FadeDirection == 0 || now < FadeEnd) return;
            if (FadeDirection < 0) HideWindow();
            FadeDirection = 0;
        }
        private void Fade(bool show)
        {
            if (!GlobalSettings.Default.TS1InterfaceFX)
            {
                // Original direct Show/Hide changes visibility, not an existing
                // ramp. Changing the option alone likewise leaves it running.
                if (show) Visible = true; else HideWindow();
                return;
            }
            long now = Clock();
            AdvanceFade(now);
            if (show ? Visible && FadeDirection >= 0 : !Visible || FadeDirection < 0) return;
            if (show && FadeDirection > 0) return;
            float from = show ? 0 : 1;
            int duration = 333;
            if (FadeDirection != 0)
            {
                float value = FadeValue(now);
                // AsyncShowHideWin deliberately complements the current value
                // when reversing a close (502fb4..50301c), unlike open->close.
                from = FadeDirection < 0 ? 1-value : value;
                duration = (int)(.5f + 333f*from);
            }
            FadeDirection = show ? 1 : -1;
            FadeTo = show ? 1 : 0;
            FadeSlope = duration == 0 ? 0 : (FadeTo-from)/duration;
            FadeEnd = now+duration;
            if (show) Visible = true;
        }
        public void Hide() => Fade(false);
        public void Close()
        {
            Request = null; SnapshotPending = false;
            Hide(); Deadline = 0; OwnerDuration = 0;
        }
        internal void ActivateTarget() { if (Valid(Target)) Center(Target); Hide(); }
        private void OnMouse(UIMouseEventType type, UpdateState state)
        {
            if (Visible && type == UIMouseEventType.MouseDown && state.WindowFocused) ActivateTarget();
        }
        public override void Update(UpdateState state)
        {
            LastMouse = state.MouseState.Position;
            if (Visible) AdvanceFade(Clock());
            if (Deadline != 0 && Clock() >= Deadline)
            {
                // TSOnTimerMsg releases its owner before starting the close;
                // Escape/image activation keep ownership until actual hiding.
                Deadline = 0; OwnerDuration = 0; Hide();
            }
            if (Visible && FadeDirection >= 0 && !Valid(Target)) Hide();
            if (Visible && state.WindowFocused && state.NewKeys.Contains(Keys.Escape)) Hide();
            base.Update(state);
        }
        public override void GameResized()
        {
            Position = WindowPosition(Game.ScreenWidth, Game.ScreenHeight, Pixels, (byte)STR.DefaultLangCode);
            base.GameResized();
        }
        public override void PreDraw(UISpriteBatch batch)
        {
            if (Visible) AdvanceFade(Clock());
            if (Visible && Valid(Target) && World.PictureInPictureReady && (NeedsImage || GlobalSettings.Default.TS1LivePIP))
            {
                Renderer ??= World.CreatePictureInPictureRenderer();
                Texture2D texture;
                batch.Pause();
                try
                {
                    // UI-25 item 4a: native same-floor PIP (floor-equal branch
                    // 0x1c1050/0x1c1604) skips the whole floor-swap block — it
                    // renders the STANDING main matrix untouched, hover history
                    // included. Null inputs keep the renderer drawing the main
                    // Blueprint.Cutaway unchanged, and nothing is cleared.
                    var targetLevel = Target.Position.Level;
                    var pipInputs = targetLevel == World.State.Level
                        ? null
                        : BuildPipCutawayInputs(targetLevel, (WorldZoom)Request.ZoomIndex);
                    texture = Renderer.Render(CenterPoint(), targetLevel, (WorldZoom)Request.ZoomIndex, Pixels, ScreenVerticalOffset(), pipInputs);
                }
                finally { batch.Resume(); }
                NeedsImage = false;
                if (SnapshotPending)
                {
                    SnapshotPending = false;
                    var data = new Color[Pixels*Pixels];
                    texture.GetData(data);
                    var photo = new Texture2D(GameFacade.GraphicsDevice, Pixels, Pixels);
                    photo.SetData(data);
                    OriginalSnapshotAlbum.AddCaptured(photo, Game.Frontend?.MainPanel?.CameraChrome?.SnapshotQuality ?? 1);
                    OriginalSnapshotAlbum.SetCurrentDescription(Request.Caption);
                    Caption.Text = "";
                }
            }
            base.PreDraw(batch);
        }
        public override void Draw(UISpriteBatch batch)
        {
            if (Visible) AdvanceFade(Clock());
            // The original PIP refuses fade-background capture. With its normal
            // flags, generic BlitPrivateBufferToParent copies only the top
            // 5-bit opacity bucket (506424..50650c); other buckets need that
            // absent capture. Gate the whole window, including its children.
            // Logical visibility and mouse eligibility remain independent.
            if (!Visible || FadeOpacity < 248) return;
            if (Image != null) DrawLocalTexture(batch, Image, Vector2.Zero);
            base.Draw(batch);
        }
        public void Dispose()
        {
            HideWindow(); FadeDirection = 0; Request = null; SnapshotPending = false;
            Renderer?.Dispose(); Renderer = null;
        }
    }

    // Native font paragraph output, local to PIP: the general UI paragraph
    // has different wrapping and can paint outside the original image buffer.
    internal sealed class UIOriginalPIPCaption : UIElement
    {
        internal readonly OriginalGlyphFont Font;
        internal string Text = "";
        internal int Pixels = 100;
        private string LastText;
        private int LastPixels;
        private readonly System.Collections.Generic.List<string> Lines = new System.Collections.Generic.List<string>();
        private static readonly Color Ink = new Color(195, 205, 205);
        internal UIOriginalPIPCaption(OriginalGlyphFont font) { Font = font; }

        // CalculateWordsToFitInWidth 4ad3b0: ink width tests the next glyph;
        // advance moves the pen. Native breaks include spaces and hyphens,
        // preserve following spaces, and use the count-1 long-word fallback.
        internal static int NativeLineLength(string text, int start, int width,
            Func<char, int> inkWidth, Func<char, int> advance)
        {
            int remaining = text.Length-start;
            if (remaining <= 0) return 0;
            int count = 0, lastBreak = 0, at = start;
            int extent = inkWidth(text[at]);
            bool hardReturn = false;
            while (extent <= width)
            {
                count++;
                if (count >= remaining) return count;
                char c = text[at];
                if (c == '\n') { lastBreak = count; hardReturn = true; break; }
                if (c == ' ' || c == '-') lastBreak = count;
                extent += advance(c)-inkWidth(c);
                at++;
                extent += inkWidth(text[at]);
            }
            if (lastBreak == 0)
            {
                if (count > 1) lastBreak = count-1;
                else while (lastBreak < remaining && text[start+lastBreak] != '\0' &&
                    text[start+lastBreak] != '\n' && text[start+lastBreak] != ' ') lastBreak++;
            }
            if (!hardReturn)
                while (lastBreak < remaining && text[start+lastBreak] == ' ') lastBreak++;
            return Math.Max(1, lastBreak);
        }
        private int InkWidth(char c)
        {
            OriginalGlyphFont.Glyph glyph;
            return Font.ByChar.TryGetValue(OriginalGlyphFont.MapChar(c), out glyph) ? glyph.W : 0;
        }
        internal void Layout()
        {
            if (LastText == Text && LastPixels == Pixels) return;
            LastText = Text; LastPixels = Pixels; Lines.Clear();
            var text = (Text ?? "").Replace("\r\n", "\n");
            for (int start = 0; start < text.Length && text[start] != '\0';)
            {
                int length = NativeLineLength(text, start, Pixels-10, InkWidth,
                    c => Font.Advance(OriginalGlyphFont.MapChar(c)));
                Lines.Add(text.Substring(start, length));
                start += length;
            }
        }
        internal int LineCount { get { Layout(); return Lines.Count; } }
        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible || Font?.Atlas == null || string.IsNullOrEmpty(Text)) return;
            Layout();
            int y = Pixels-5-Lines.Count*Font.LineHeight;
            // DrawTextPara 4ad974 uses an unsigned Y/bottom comparison.
            if (y < 0) return;
            var bounds = new Rectangle(0, 0, Pixels, Pixels);
            foreach (var line in Lines)
            {
                int x = 5;
                foreach (var character in line)
                {
                    char c = OriginalGlyphFont.MapChar(character);
                    OriginalGlyphFont.Glyph glyph;
                    if (Font.ByChar.TryGetValue(c, out glyph) && glyph.W > 1)
                    {
                        var dest = new Rectangle(x, y+Font.GlyphY(glyph), glyph.W, glyph.H);
                        var clip = Rectangle.Intersect(dest, bounds);
                        if (clip.Width > 0 && clip.Height > 0)
                            DrawLocalTexture(batch, Font.Atlas,
                                new Rectangle(glyph.U+clip.X-dest.X, glyph.V+clip.Y-dest.Y, clip.Width, clip.Height),
                                new Vector2(clip.X, clip.Y), Vector2.One, Ink*Opacity);
                    }
                    x += Font.Advance(c);
                }
                y += Font.LineHeight;
            }
        }
    }
}
