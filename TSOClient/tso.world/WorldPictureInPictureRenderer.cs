using System;
using System.Collections.Generic;
using System.Linq;
using FSO.Common.Utils;
using FSO.LotView.Components;
using FSO.LotView.Model;
using FSO.LotView.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FSO.LotView
{
    public partial class World
    {
        public bool PictureInPictureReady => HasInit && WorldPictureInPictureRenderer.Ready(State, Blueprint);

        public WorldPictureInPictureRenderer CreatePictureInPictureRenderer()
        {
            if (!HasInit || Blueprint == null) throw new InvalidOperationException("The lot is not initialized.");
            return new WorldPictureInPictureRenderer(State, Blueprint);
        }
    }

    /// <summary>
    /// An independent original-isometric view of the live lot. Owns the camera,
    /// target, terrain geometry and sprite caches, but never updates simulation.
    /// Call between draw batches, on the graphics thread; dispose when its window closes.
    /// </summary>
    public sealed class WorldPictureInPictureRenderer : IDisposable
    {
        private readonly WorldState Main;
        private readonly Blueprint Blueprint;
        private readonly WorldState View;
        private readonly WallComponent Walls;
        private readonly Dictionary<ObjectComponent, ObjectView> Objects = new Dictionary<ObjectComponent, ObjectView>();
        private readonly Dictionary<AvatarComponent, _2DStandaloneSprite> Headlines = new Dictionary<AvatarComponent, _2DStandaloneSprite>();
        private readonly Dictionary<ParticleComponent, ParticleComponent> Particles = new Dictionary<ParticleComponent, ParticleComponent>();
        private TerrainComponent Terrain;
        private TerrainComponent TerrainSource;
        private int TerrainRevision = -1;
        private readonly CutawayMaskCache PipMasks = new CutawayMaskCache();
        private RenderTarget2D Target;
        private readonly Dictionary<int, RenderTarget2D> Targets = new Dictionary<int, RenderTarget2D>();
        private bool Disposed;
        public Texture2D Texture => Target;
        public int RenderCount { get; private set; }
        public Vector3 LastTarget { get; private set; }
        public WorldZoom LastZoom { get; private set; }
        public sbyte LastLevel { get; private set; }
        public float LastVerticalOffset { get; private set; }
        private Matrix LastViewProjection;
        public Vector2 LastTargetScreen => ProjectToImage(LastTarget);

        // Project through the actual camera used for the retained image. This
        // also allows framing checks to inspect the rendered avatar location,
        // which can differ from its simulation position during routing/slots.
        public Vector2 ProjectToImage(Vector3 tile)
        {
            if (Target == null || RenderCount == 0) return new Vector2(float.NaN);
            var clip = Vector4.Transform(new Vector4(WorldSpace.GetWorldFromTile(tile), 1), LastViewProjection);
            return new Vector2((clip.X / clip.W + 1) * Target.Width / 2,
                (1 - clip.Y / clip.W) * Target.Height / 2);
        }

        public bool IsReady => !Disposed && Ready(Main, Blueprint);

        internal static bool Ready(WorldState state, Blueprint blueprint)
        {
            return state?.Device != null && blueprint?.Terrain?.VertexBuffer != null
                && !blueprint.Terrain.VertexBuffer.IsDisposed && !blueprint.Terrain.TerrainDirty
                && blueprint.FloorGeom?.Floors != null && blueprint.RoofComp != null
                && state.Rooms?.RoomMaps != null && state.Rooms.RoomMaps.Count >= blueprint.Stories
                && state.Rooms.RoomMaps.Take(blueprint.Stories).All(x => x != null && !x.IsDisposed)
                && state.OutsidePx != null && !state.OutsidePx.IsDisposed;
        }

        internal WorldPictureInPictureRenderer(WorldState main, Blueprint blueprint)
        {
            Main = main;
            Blueprint = blueprint;
            // A null invalidation owner is intentional: changing this view must
            // never dirty the main world's objects, subworlds or scroll caches.
            View = new WorldState(main.Device, 100, 100, null);
            Walls = new WallComponent { blueprint = blueprint };
        }

        // verticalOffset uses the native signed screen-space argument: a
        // negative offset places the target's floor point below image center.
        // pipInputs optionally carries the native-law cutaway inputs (floor,
        // rotation, zoom, selected-person room, cursor). When it is null the
        // pre-R244 behavior is kept exactly: cross-floor PIP renders with an
        // empty mask (native "disables the old floor's wall cutaway"). When
        // supplied, the draw mask is EXACTLY the DoDynamicCutaway composition
        // (native viewer+0x204) evaluated at this view's state: the caller-
        // supplied history rooms (native clears them around the secondary
        // render), the selected-person room (live mode, floor-gated), and the
        // cursor vicinity (r26-gated). Native law: the secondary render's
        // draw input is the composed dynamic matrix only -
        // RoomManager::ComputeCutaway(targetFloor) in the native sequence
        // exists solely to (re)fill the per-room matrix intermediates that
        // DoDynamicCutaway ORs from; it is never the draw input, so no
        // all-rooms base is drawn here. The committed main mask is never
        // mutated: the swap is
        // scoped to this draw and the original reference is restored in
        // finally, on success or exception alike. WALL_CUT_CHANGED is never
        // raised from inside the draw.
        public Texture2D Render(Vector3 targetTile, sbyte level, WorldZoom zoom, int size, float verticalOffset = 0, CutawayViewInputs pipInputs = null)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(WorldPictureInPictureRenderer));
            if (!IsReady) throw new InvalidOperationException("The lot graphics are not ready for PIP capture.");
            if (size != 100 && size != 200 && size != 300) throw new ArgumentOutOfRangeException(nameof(size));
            if ((int)zoom < 0 || zoom > WorldZoom.Near) throw new ArgumentOutOfRangeException(nameof(zoom));
            if (level < 1 || level > Blueprint.Stories) throw new ArgumentOutOfRangeException(nameof(level));
            if (!float.IsFinite(targetTile.X) || !float.IsFinite(targetTile.Y) || !float.IsFinite(targetTile.Z))
                throw new ArgumentOutOfRangeException(nameof(targetTile));
            if (!float.IsFinite(verticalOffset)) throw new ArgumentOutOfRangeException(nameof(verticalOffset));
            var gd = Main.Device;
            using (var gpu = new PictureInPictureGraphicsScope(gd))
            {
                EnsureResources(size);
                using (PPXDepthEngine.PushTarget(Target, null, Blueprint.OutsideColor))
                {
                    var oldFloorAlternate = Blueprint.FloorGeom.AlternateDrawState;
                    var oldCutaway = Blueprint.Cutaway;
                    try
                    {
                        // Native secondary Render disables the old floor's
                        // cutaway before rendering a target on another floor.
                        if (level != Main.Level) Blueprint.Cutaway = new bool[oldCutaway.Length];
                        View.SetDimensions(new Vector2(size));
                        // Native SetScale also accepts zero, a half-Far view.
                        // Sprite resources stop at Far, so precise projection
                        // scales those resources for that additional level.
                        View.Zoom = (int)zoom == 0 ? WorldZoom.Far : zoom;
                        View.PreciseZoom = (int)zoom == 0 ? 0.5f : 1;
                        View.SilentRotation = Main.Rotation;
                        View.WorldSpace.Invalidate();
                        View.InvalidateCamera();
                        View.Level = level;
                        View.FramePerDraw = 0; // draw never advances avatar interpolation
                        View.DrawRoofs = Main.DrawRoofs;
                        View.Rooms = Main.Rooms;
                        View.AmbientLight = Main.AmbientLight;
                        View.OutsidePx = Main.OutsidePx;
                        View.Light = Main.Light;
                        View.OutsideColor = Blueprint.OutsideColor;
                        View.LightingAdjust = Main.LightingAdjust;
                        View.CenterTile = View.Project2DCenterTile(targetTile);
                        View.CenterTile += View.WorldSpace.GetTileFromScreen(new Vector2(0, verticalOffset / View.PreciseZoom));
                        View._2D.PreciseZoom = View.PreciseZoom;
                        View._2D.Begin(View.Camera2D);
                        View._2D.ResetMatrices(size, size);
                        View.PrepareCamera();
                        View.PrepareLighting();
                        if (pipInputs != null)
                        {
                            // Real native-law mask for the target floor. Computed after
                            // CenterTile so the cursor's default screen->tile mapping can
                            // use this view, and before the walls draw against it.
                            Blueprint.Cutaway = ComputePipCutaway(level, zoom, pipInputs);
                        }
                        gd.DepthStencilState = DepthStencilState.Default;
                        gd.RasterizerState = RasterizerState.CullNone;
                        Terrain?.Draw(gd, View);
                        Walls.Draw(gd, View);
                        View._2D.Pause();
                        View._2D.Resume();
                        if (View.DrawRoofs) Blueprint.RoofComp.Draw(gd, View);
                        DrawAvatars(gd);
                        DrawObjects(gd);
                        DrawParticles(gd);
                        View._2D.End();
                        LastViewProjection = View.View * View.Projection;
                        RenderCount++;
                        LastTarget = targetTile;
                        LastZoom = zoom;
                        LastLevel = level;
                        LastVerticalOffset = verticalOffset;
                    }
                    finally
                    {
                        Blueprint.Cutaway = oldCutaway;
                        Blueprint.FloorGeom.AlternateDrawState = oldFloorAlternate;
                    }
                }
            }
            return Target;
        }

        /// <summary>
        /// Drops the PIP's cached per-room cutaway masks. The renderer also
        /// self-invalidates on active architecture edit flags; call this after
        /// bulk blueprint surgery that does not raise them.
        /// </summary>
        public void InvalidateCutawayCache()
        {
            PipMasks.Invalidate();
        }

        private bool[] ComputePipCutaway(sbyte level, WorldZoom zoom, CutawayViewInputs inputs)
        {
            // Cheap non-destructive fingerprint of active architecture edits
            // (reading Dirty never consumes it - PreDraw owns clearing).
            var dirty = Blueprint.Changes?.Dirty ?? 0;
            if ((dirty & (BlueprintGlobalChanges.WALL_CHANGED | BlueprintGlobalChanges.WALL_CUT_CHANGED
                | BlueprintGlobalChanges.ROOM_CHANGED)) != 0)
                PipMasks.Invalidate();

            // Normalize the caller's inputs to what this render actually uses.
            var view = inputs.Clone();
            view.Floor = level;
            view.Rotation = Main.Rotation; // the PIP view renders with the main rotation
            view.Zoom = (int)zoom == 0 ? WorldZoom.Far : zoom;
            view.PreciseZoom = (int)zoom == 0 ? 0.5f : 1f;
            if (view.ScreenToTile == null && view.CursorScreenPos.HasValue)
            {
                // Native-literal default: the secondary viewer's PointToTile applied
                // to the shared mouse position (best-effort through the port's
                // standard screen->tile path). Callers wanting the main view's
                // picking path should supply ScreenToTile in pipInputs.
                var vs = View;
                view.ScreenToTile = p => vs.WorldSpace.GetTileAtPosWithScroll(new Vector2(p.X, p.Y) / vs.PreciseZoom);
            }

            // DoDynamicCutaway(true) at this view's state - the exact native
            // draw input for the secondary render (viewer+0x204). With the
            // history empty (native clears it on every floor change), that is
            // the history rooms ∪ the person branch only when the caller
            // resolved a live-mode person (native nulls the tracked object
            // around secondary renders, so its mode-2 branch falls back to
            // the selected person) ∪ the cursor vicinity when the caller
            // shares the mouse (r26-gated). Native's ComputeCutaway(target
            // floor) in this sequence only (re)fills the per-room matrices
            // DoDynamicCutaway ORs from - it is an intermediate, never the
            // draw input, so no all-rooms base is ORed here. The live-mode
            // gating is the caller's contract: leave PersonRoomId null
            // outside live mode. PipMasks caches exactly the per-room masks
            // the composition pulls in (history/person rooms); those masks
            // depend only on room/floor/rotation/zoom, and the person/cursor
            // inputs only shape the composition step, which is never cached.
            return CutawayMatrix.ComposeDynamic(Blueprint, view, PipMasks);
        }

        private void EnsureResources(int size)
        {
            var gd = Main.Device;
            if (Target == null || Target.Width != size)
            {
                // A previously displayed target may still be bound in one of
                // the caller's texture slots. Keep the three native sizes
                // alive until window disposal so restoration never rebinds a
                // texture disposed during this capture.
                if (!Targets.TryGetValue(size, out Target))
                    Targets[size] = Target = new RenderTarget2D(gd, size, size, false, SurfaceFormat.Color,
                        DepthFormat.Depth24Stencil8, 0, RenderTargetUsage.PreserveContents);
            }
            if (View._2D == null)
                View._2D = new _2DWorldBatch(gd, 0, new SurfaceFormat[0], new bool[0], 0);
            var source = Blueprint.Terrain;
            if (source != TerrainSource || source?.GeometryRevision != TerrainRevision || source?.TerrainDirty == true)
            {
                Terrain?.Dispose();
                Terrain = source?.CreateIndependentView();
                TerrainSource = source;
                TerrainRevision = source?.GeometryRevision ?? -1;
            }
        }

        private void DrawAvatars(GraphicsDevice gd)
        {
            gd.DepthStencilState = DepthStencilState.Default;
            gd.BlendState = BlendState.AlphaBlend;
            gd.RasterizerState = RasterizerState.CullCounterClockwise;
            var effect = WorldContent.AvatarEffect;
            effect.CurrentTechnique = effect.Techniques[WorldConfig.Current.Directional && WorldConfig.Current.AdvancedLighting
                ? 5 : WorldConfig.Current.PassOffset * 2];
            effect.Parameters["View"].SetValue(View.View);
            effect.Parameters["Projection"].SetValue(View.Projection);
            View._2D.OffsetPixel(Vector2.Zero);
            View._2D.OffsetTile(Vector3.Zero);
            View._2D.PrepareImmediate(Effects.WorldBatchTechniques.drawZSpriteDepthChannel);
            foreach (var avatar in Blueprint.Avatars)
            {
                if (avatar.Level > View.Level || !avatar.Visible) continue;
                var oldHeadline = avatar.HeadlineSprite;
                var oldPosition = avatar.Avatar.Position;
                var oldLights = avatar.Avatar.LightPositions;
                if (!Headlines.TryGetValue(avatar, out var headline))
                    Headlines[avatar] = headline = new _2DStandaloneSprite();
                try
                {
                    avatar.HeadlineSprite = headline;
                    avatar.Draw(gd, View);
                }
                finally
                {
                    avatar.HeadlineSprite = oldHeadline;
                    avatar.Avatar.Position = oldPosition;
                    avatar.Avatar.LightPositions = oldLights;
                }
            }
            foreach (var avatar in Headlines.Keys.Where(x => !Blueprint.Avatars.Contains(x)).ToArray())
            { Headlines[avatar].Dispose(); Headlines.Remove(avatar); }
            gd.RasterizerState = RasterizerState.CullNone;
        }

        private void DrawObjects(GraphicsDevice gd)
        {
            View._2D.PrepareImmediate(Effects.WorldBatchTechniques.drawZSpriteDepthChannel);
            gd.DepthStencilState = DepthStencilState.Default;
            gd.BlendState = BlendState.NonPremultiplied;
            foreach (var obj in Blueprint.Objects.OrderBy(x => View.WorldSpace.GetDepthFromTile(x.Position)))
            {
                if (!obj.Visible || (View.Level == Main.Level && obj.CutawayHidden)
                    || obj.Level > View.Level || obj.Room == 0 || obj.DGRP == null) continue;
                if (!Objects.TryGetValue(obj, out var cache)) Objects[obj] = cache = new ObjectView(obj);
                cache.Draw(obj, View);
            }
            foreach (var obj in Objects.Keys.Where(x => !Blueprint.Objects.Contains(x)).ToArray())
            { Objects[obj].Dispose(); Objects.Remove(obj); }
            View._2D.EndImmediate();
        }

        public void Dispose()
        {
            if (Disposed) return;
            Disposed = true;
            foreach (var target in Targets.Values) target.Dispose();
            Targets.Clear();
            View._2D?.Dispose();
            Terrain?.Dispose();
            foreach (var obj in Objects.Values) obj.Dispose();
            foreach (var headline in Headlines.Values) headline.Dispose();
            foreach (var particle in Particles.Values) particle.Dispose();
            Objects.Clear(); Headlines.Clear(); Particles.Clear();
            // View.Rooms/light textures are borrowed from Main and must survive.
        }

        private void DrawParticles(GraphicsDevice gd)
        {
            var live = new HashSet<ParticleComponent>(Blueprint.ObjectParticles.Concat(Blueprint.Particles));
            foreach (var particle in live)
            {
                if (particle.Dead || particle.Level > View.Level) continue;
                var owner = particle.Owner;
                if (owner != null && (!owner.Visible || (owner.Position.X < -2043 && owner.Position.Y < -2043))) continue;
                if (!Particles.TryGetValue(particle, out var view))
                    Particles[particle] = view = particle.CreateIndependentView();
                view.SynchronizeIndependentView(particle);
                view.Draw(gd, View);
            }
            foreach (var particle in Particles.Keys.Where(x => !live.Contains(x)).ToArray())
            { Particles[particle].Dispose(); Particles.Remove(particle); }
        }

        private sealed class ObjectView : IDisposable
        {
            private readonly DGRPRenderer Sprite;
            private readonly _2DStandaloneSprite Headline = new _2DStandaloneSprite();
            private WorldZoom Zoom;
            private WorldRotation Rotation;
            internal ObjectView(ObjectComponent obj)
            {
                Sprite = new DGRPRenderer(obj.DGRP, obj.Obj.OBJ)
                { DynamicSpriteBaseID = obj.Obj.OBJ.DynamicSpriteBaseId, NumDynamicSprites = obj.Obj.OBJ.NumDynamicSprites };
            }
            internal void Draw(ObjectComponent obj, WorldState state)
            {
                if (Sprite.DGRP != obj.DGRP) Sprite.DGRP = obj.DGRP;
                if (Sprite.Direction != obj.Direction) Sprite.Direction = obj.Direction;
                if (Zoom != state.Zoom || Rotation != state.Rotation || Sprite.DynamicSpriteFlags != obj.DynamicSpriteFlags
                    || Sprite.DynamicSpriteFlags2 != obj.DynamicSpriteFlags2 || Sprite.Room != obj.Room || Sprite.Level != obj.Level)
                    Sprite.InvalidateZoom();
                Zoom = state.Zoom; Rotation = state.Rotation;
                Sprite.DynamicSpriteFlags = obj.DynamicSpriteFlags;
                Sprite.DynamicSpriteFlags2 = obj.DynamicSpriteFlags2;
                Sprite.Room = obj.Room; Sprite.Level = obj.Level; Sprite.ObjectID = obj.ObjectID;
                Sprite.Position = obj.Position;
                Sprite.ValidateSprite(state);
                if (Sprite.Bounding.HasValue && !state.WorldRectangle.Intersects(Sprite.Bounding.Value)) return;
                Sprite.DrawImmediate(state);
                var texture = obj.Headline;
                if (texture == null || texture.IsDisposed) return;
                var offset = new Vector3(0, 0, 0.66f);
                var pixel = state.WorldSpace.GetScreenFromTile(offset);
                var baseline = new[] { new Vector2(18,87), new Vector2(35,174), new Vector2(69,348) }[(int)state.Zoom - 1];
                Headline.Pixel = texture;
                Headline.Depth = TextureGenerator.GetWallZBuffer(state.Device)[30];
                Headline.SrcRect = new Rectangle(0, 0, texture.Width, texture.Height);
                Headline.WorldPosition = offset;
                Headline.DestRect = new Rectangle((int)pixel.X - texture.Width / 2 + (int)baseline.X,
                    (int)pixel.Y - texture.Height / 2 + (int)baseline.Y, texture.Width, texture.Height);
                Headline.AbsoluteDestRect = Headline.DestRect;
                Headline.AbsoluteDestRect.Offset(state.WorldSpace.GetScreenFromTile(obj.Position));
                Headline.AbsoluteWorldPosition = offset + WorldSpace.GetWorldFromTile(obj.Position);
                Headline.PrepareVertices(state.Device);
                state._2D.DrawImmediate(Headline);
            }
            public void Dispose() { Sprite.Dispose(); Headline.Dispose(); }
        }
    }
}
