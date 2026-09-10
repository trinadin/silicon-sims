using FSO.LotView.Model;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace FSO.LotView.Utils
{
    /// <summary>
    /// The view-relative inputs of the original cutaway law
    /// (HouseViewer::ComputeCutawayMatrix / DoDynamicCutaway, decoded in
    /// tools/iff-dump/r244-cutaway-geom + r244-cutaway-state). Each field names
    /// the native state it mirrors. Create one per evaluation; the mask
    /// functions never mutate it.
    /// </summary>
    public class CutawayViewInputs
    {
        /// <summary>Viewer floor, 1-based (native viewer+0x18).</summary>
        public sbyte Floor;

        /// <summary>Current world rotation (native cRotatableWorld+0x84).</summary>
        public WorldRotation Rotation;

        /// <summary>
        /// Zoom bucket (native global viewer+8). The port's buckets map 1:1 onto
        /// the native zoom index: Far=1, Medium=2, Near=3. The extra native
        /// index 0 ("half-Far") is expressed as Far with PreciseZoom 0.5.
        /// </summary>
        public WorldZoom Zoom;

        /// <summary>
        /// Port-only sub-bucket sprite scale (native has no equivalent field;
        /// its zoom 0 covers half-Far). It cancels out of the room-mask overlap
        /// algebra (all rect terms scale linearly) and only feeds the cursor
        /// screen->tile mapping and the cursor Y adjustment.
        /// </summary>
        public float PreciseZoom = 1f;

        /// <summary>
        /// Screen bounds of the buffer the cursor is tested against
        /// (native GetBuffDims() of the main animation buffer). The PIP passes
        /// the MAIN view buffer here, because native shares the global mouse.
        /// </summary>
        public Rectangle BufferBounds;

        /// <summary>
        /// Mouse position in BufferBounds pixels (native EventRecord.where).
        /// Null = no cursor input.
        /// </summary>
        public Point? CursorScreenPos;

        /// <summary>
        /// Port equivalent of the native strategy predicate
        /// EdgeDetectScroller::IsWaitingForClick (mid-scroll / waiting for
        /// click): when true the whole cursor-vicinity section is skipped.
        /// </summary>
        public bool CursorSuppressed;

        /// <summary>
        /// Port equivalent of HouseViewer::PointToTile (native viewer vt2 slot
        /// 3): screen pixels (in BufferBounds space, AFTER the cursor Y
        /// adjustment) to a fractional world tile. The caller supplies it from
        /// its own view so scroll, centering and PreciseZoom are honored.
        /// Null = cursor-vicinity section skipped.
        /// </summary>
        public Func<Point, Vector2> ScreenToTile;

        /// <summary>
        /// Selected/tracked person's current room id (native person+0xaac,
        /// valid below 0xfffb). Null = person branch skipped — callers MUST
        /// leave this null outside live mode (native gates the person branch on
        /// CPState::GetMode()==2) and when no person is resolved.
        /// </summary>
        public uint? PersonRoomId;

        /// <summary>
        /// Person's floor (native person+0x110). The room is only ORed when it
        /// equals <see cref="Floor"/>.
        /// </summary>
        public sbyte PersonFloor;

        /// <summary>
        /// Person's tile (native person+0xfc/+0xfd, the same fields the native
        /// outside-person rectangle reads). Only used when the person's room is
        /// an outside room. Null = the rectangle is not marked.
        /// </summary>
        public Point? PersonTile;

        /// <summary>
        /// Caller-provided IsOutside answer for the person's room (native reads
        /// Room::IsOutside directly; this is the fallback for callers that
        /// cannot resolve the room object). The room's own flag wins when the
        /// room resolves.
        /// </summary>
        public bool PersonOutside;

        /// <summary>
        /// Room history, oldest first (native viewer+0x1fc cCutawaySet,
        /// capacity 3, duplicate insert is a no-op). Membership is all that is
        /// observable (the set is only ORed and cleared), so order is not
        /// required. Native clears it on every floor/view change.
        /// </summary>
        public IReadOnlyList<uint> HistoryRooms;

        /// <summary>Dynamic cutaway enabled (native viewer+0x49).</summary>
        public bool DynamicEnabled;

        /// <summary>
        /// DEVIATION HOOK (native tool callback cTool slot 18
        /// AdjustCutawayForTool(corner, matrix), dispatched after the cursor
        /// rectangle on the current build tool). No clean port equivalent
        /// exists yet — VMArchitectureTools keeps the touch-path approximation
        /// — so this stays null/unwired and the callback is skipped. Do not
        /// approximate silently; wire the real tool state here when ported.
        /// </summary>
        public Action<Point, bool[]> AdjustCutawayForTool;

        public CutawayViewInputs Clone()
        {
            return (CutawayViewInputs)MemberwiseClone();
        }
    }

    /// <summary>
    /// Per-view cache of room cutaway masks. Main-loop-thread confined: both
    /// the main view and the PIP renderer evaluate on the graphics thread; do
    /// not share an instance across threads.
    /// </summary>
    public sealed class CutawayMaskCache
    {
        private struct Key : IEquatable<Key>
        {
            public uint RoomId;
            public sbyte Floor;
            public WorldRotation Rotation;
            public int Zoom;
            public float PreciseZoom;

            public bool Equals(Key other)
            {
                return RoomId == other.RoomId && Floor == other.Floor
                    && Rotation == other.Rotation && Zoom == other.Zoom
                    && PreciseZoom == other.PreciseZoom;
            }
            public override bool Equals(object obj) => obj is Key k && Equals(k);
            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = (int)RoomId;
                    hash = (hash * 397) ^ Floor;
                    hash = (hash * 397) ^ (int)Rotation;
                    hash = (hash * 397) ^ Zoom;
                    hash = (hash * 397) ^ PreciseZoom.GetHashCode();
                    return hash;
                }
            }
        }

        private struct Entry
        {
            public bool[] Mask;
            public uint Revision;
        }

        private readonly Dictionary<Key, Entry> Masks = new Dictionary<Key, Entry>();

        /// <summary>
        /// Architecture/wall revision owned by the CALLER: bump this (or call
        /// <see cref="Invalidate"/>) whenever walls, floors or rooms change.
        /// Entries stamped with an older revision are recomputed on demand.
        /// </summary>
        public uint Revision;

        public bool[] GetOrCompute(Blueprint bp, uint roomId, CutawayViewInputs view)
        {
            var key = new Key { RoomId = roomId, Floor = view.Floor, Rotation = view.Rotation,
                Zoom = (int)view.Zoom, PreciseZoom = view.PreciseZoom };
            if (Masks.TryGetValue(key, out var entry) && entry.Revision == Revision) return entry.Mask;
            var mask = CutawayMatrix.ComputeRoomMask(bp, roomId, view);
            Masks[key] = new Entry { Mask = mask, Revision = Revision };
            return mask;
        }

        /// <summary>Drops every cached mask (e.g. after a batch architecture edit).</summary>
        public void Invalidate()
        {
            Masks.Clear();
        }
    }

    /// <summary>
    /// Faithful port of the decoded native cutaway law, expressed in the port's
    /// own projection space (WorldSpace.GetScreenFromTile conventions), with
    /// the port's room map / wall storage replacing the native room tile lists
    /// and packed wall bytes. Evidence: tools/iff-dump/r243-cutaway/review.md,
    /// r244-cutaway-geom/{decode,skeptic-corrections}.md,
    /// r244-cutaway-state/{decode,skeptic-corrections}.md.
    ///
    /// Deliberate departures from the native binary (each required by a skeptic
    /// correction or a missing port equivalent, never a silent approximation):
    /// - No global-viewer zoom rescale and no persistent origin/zoom mutation
    ///   (skeptic C1/C2: the rescale is an artifact of native's shared global
    ///   viewer and is temporary even there; the port projects statelessly at
    ///   its own view zoom).
    /// - No 32-bit mirrored-bit matrix wart (skeptic C3): the port mask is a
    ///   bool[width*height] indexed y*Width+x in WORLD (unrotated) tile space,
    ///   so native's x/x+32 column aliasing cannot occur.
    /// - The native rotation LUT generator is UNRESOLVED; the port defines its
    ///   own consistent rotated frame (see <see cref="RotMap"/>) proven equal
    ///   to WorldSpace.GetScreenFromTile at every rotation.
    /// - The outside-person altitude-table remap (native BuildMaxAltsTable) is
    ///   identity: the port has no max-alts table; the person tile is used
    ///   directly.
    /// </summary>
    public static class CutawayMatrix
    {
        // Native world bounds here are the lot tile rectangle [0..Width) x [0..Height);
        // the sweep interior is that rectangle inset by 1 (native world+0x70..0x7C + 1).
        // Native assumes a square world (worldSize from the floor table); the port
        // uses Width for x gates and Height for y gates.

        /// <summary>
        /// Port of Room::ComputeCutawayMatrix (native 0x125130): the projected
        /// wall-overlap mask of one room, bool[Width*Height] indexed y*Width+x in
        /// WORLD (unrotated) tile space. Outside rooms return an empty mask
        /// (native early-returns; room+0x7c stays cleared).
        /// </summary>
        public static bool[] ComputeRoomMask(Blueprint bp, uint roomId, CutawayViewInputs view)
        {
            var result = new bool[bp.Width * bp.Height];
            if (roomId >= (uint)bp.Rooms.Count) return result;
            var room = bp.Rooms[(int)roomId];
            if (room.IsOutside) return result;

            // room.Floor is the port's 0-BASED story index: VMRoomMap.GenerateMap
            // receives the 0-based story and copies it into Room.Floor verbatim,
            // and the other consumers (Blueprint.IsIndoorsPrecise,
            // LotFacadeGenerator) index RoomMap with it directly. (Viewer floors
            // and LotTilePos.Level are the 1-BASED convention - the +1 below.)
            var floorIdx = room.Floor;
            var roomMap = (floorIdx >= 0 && floorIdx < bp.RoomMap.Length) ? bp.RoomMap[floorIdx] : null;
            var walls = (floorIdx >= 0 && floorIdx < bp.Walls.Length) ? bp.Walls[floorIdx] : null;
            if (roomMap == null) return result;

            Metrics(bp, view, out var wh, out var hh, out var unitZ, out var h, out var v5c, out var v60, out var f);
            var rot = view.Rotation;

            // The native room keeps an explicit tile list (room+0x08/+0x0C). The port
            // room has none, so membership is scanned from the same data the VM fills:
            // RoomMap marks the room id in the low 16 bits (full tiles) and the two
            // half ids of a diagonal tile in low/high (VMRoomMap.GenerateMap, the port
            // equivalent of native ResolveDiagonal's node map).
            for (int y = 0; y < bp.Height; y++)
            {
                for (int x = 0; x < bp.Width; x++)
                {
                    var idx = y * bp.Width + x;
                    var packed = roomMap[idx];
                    int low = (ushort)packed;
                    int high = (int)(packed >> 16) & 0x7FFF;
                    if (low != roomId && high != roomId) continue;

                    // Seed the tile's own bit (native 0x125260).
                    result[idx] = true;

                    // Project the tile and form the inset screen rectangle
                    // R0..R3 = px-h+1, py+1, px+h-2, py+h-2 (native 0x1252EC..0x1252F8).
                    // px/py use the port projection (rotation-aware GetScreenFromTile
                    // equivalent, including terrain altitude and story z terms) instead
                    // of native's integer TileToPoint. ProjectTile's level is 1-based:
                    // the room's 0-based story + 1.
                    var p = ProjectTile(bp, rot, x, y, (sbyte)(floorIdx + 1), wh, hh, unitZ);
                    float px = p.X, py = p.Y;
                    float r0 = px - h + 1, r1 = py + 1, r2 = px + h - 2, r3 = py + h - 2;

                    // Diagonal side adjustments (native 0x1252C8..0x1253C0): only when
                    // the tile carries a diagonal wall segment, and only for the half
                    // orientation that appears vertical ("\\") on screen at this
                    // rotation - the cases native encodes as sides 1/3 (side 1 ->
                    // R2 -= h = left half, side 3 -> R0 += h = right half). Sides 2/4
                    // (the "/" orientation) change nothing. Native rotates side ids
                    // with ((s-1+((4-rot)&3))&3)+1; the port derives the half directly
                    // in the rotated frame, which subsumes that law.
                    if (walls != null)
                    {
                        var segments = walls[idx].Segments;
                        // high == 0 = no second half assigned (native id2 >= 0xFFFB:
                        // ResolveDiagonal resets the sides, so no adjustment either).
                        if ((segments & WallSegments.AnyDiag) != 0 && high != 0 && low != high)
                        {
                            var verticalOnScreen = ((int)rot & 1) == 0 ? WallSegments.VerticalDiag : WallSegments.HorizontalDiag;
                            if ((segments & verticalOnScreen) != 0)
                            {
                                bool lowIsRight = rot == WorldRotation.TopLeft || rot == WorldRotation.TopRight;
                                bool? rightHalf = (roomId == (uint)low) ? lowIsRight
                                    : (roomId == (uint)high) ? !lowIsRight : (bool?)null;
                                if (rightHalf == true) r0 += h;      // native side 3
                                else if (rightHalf == false) r2 -= h; // native side 1
                            }
                        }
                    }

                    // Sweep (native 0x1254D4..0x125954): three candidates per sweep at
                    // origin + {(0,+1), (+1,0), (+1,+1)} in the rotated frame (native's
                    // stateful dir-table steps entry1/entry6/entry1 land on the same
                    // absolute cells), origin advancing by (+1,+1) per sweep - i.e.
                    // marching "toward the viewer" (increasing projected y). Bounds are
                    // the rotated world interior inset by 1; only out-of-bounds
                    // candidates increment the termination counter, and the sweep ends
                    // when all three candidates are out of bounds (native r23 == 3).
                    var origin = RotMap(rot, x, y);
                    RotatedBounds(rot, bp.Width, bp.Height, out var minRX, out var maxRX, out var minRY, out var maxRY);

                    int oob;
                    while (true)
                    {
                        oob = 0;
                        for (int i = 0; i < 3; i++)
                        {
                            var crx = origin.X + SweepOffsets[i].X;
                            var cry = origin.Y + SweepOffsets[i].Y;
                            if (crx < minRX + 1 || crx > maxRX - 1 || cry < minRY + 1 || cry > maxRY - 1)
                            {
                                oob++;
                                continue;
                            }
                            var world = InvRotMap(rot, crx, cry);
                            var candIdx = world.Y * bp.Width + world.X;
                            if (result[candIdx]) continue; // already marked: no oob++ (native 0x125588)

                            // GetTheoreticalWallExtent (native 0x1CEF30) at the
                            // CANDIDATE's projected point, at the room tile's floor
                            // (native keeps the room tile's z in the sweep):
                            // W0..W3 = px-V5C-1, py-F, px+V5C-1, py+V60.
                            var cp = ProjectTile(bp, rot, world.X, world.Y, (sbyte)(floorIdx + 1), wh, hh, unitZ);
                            float w0 = cp.X - v5c - 1, w1 = cp.Y - f, w2 = cp.X + v5c - 1, w3 = cp.Y + v60;

                            // Strict overlap (native 0x1255AC..0x1255E8): a touch-only
                            // edge does not count.
                            if (r2 >= w0 + 1 && w2 >= r0 + 1 && r3 >= w1 + 1 && w3 >= r1 + 1)
                                result[candIdx] = true;
                        }
                        if (oob == 3) break;
                        origin.X += 1;
                        origin.Y += 1;
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Port of HouseViewer::DoDynamicCutaway (native 0x1CF3B0): the flat
        /// dynamic cut composition, bool[Width*Height] in world tile space.
        /// Returns an all-false array when !DynamicEnabled.
        ///
        /// Composition order mirrors native: OR the history rooms (recording
        /// whether any ORed room is outside - that is native's r26 flag; insert
        /// duplicates do not refresh recency but membership is all that matters);
        /// then the person branch, LIVE MODE ONLY (the caller must leave
        /// PersonRoomId null otherwise - native gates it on CPState mode 2),
        /// adding the decoded outside-person rectangle when the person's room is
        /// outside (not gated by r26); then the cursor vicinity, gated on the
        /// suppression predicate and the main-buffer bounds test, marked ONLY
        /// when the r26 flag is set (skeptic correction to r243-state: the
        /// cursor rectangle requires at least one ORed outside room).
        /// </summary>
        public static bool[] ComposeDynamic(Blueprint bp, CutawayViewInputs view, CutawayMaskCache cache = null)
        {
            var result = new bool[bp.Width * bp.Height];
            if (!view.DynamicEnabled) return result;

            bool anyOredRoomOutside = false;

            if (view.HistoryRooms != null)
            {
                foreach (var roomId in view.HistoryRooms)
                {
                    if (roomId >= (uint)bp.Rooms.Count) continue;
                    var room = bp.Rooms[(int)roomId];
                    if (room.IsOutside)
                    {
                        anyOredRoomOutside = true; // native 0x1CF5A8; outside rooms keep an empty mask
                        continue;
                    }
                    OrInto(result, MaskFor(bp, roomId, view, cache));
                }
            }

            // Person branch - live mode only (native 0x1CF5C8..0x1CF7B8). Validity:
            // room id < 0xfffb and person floor == viewer floor.
            if (view.PersonRoomId.HasValue && view.PersonRoomId.Value < 0xfffb
                && view.PersonFloor == view.Floor && view.PersonRoomId.Value < (uint)bp.Rooms.Count)
            {
                var roomId = view.PersonRoomId.Value;
                var room = bp.Rooms[(int)roomId];
                var outside = view.PersonOutside || room.IsOutside;
                if (!room.IsOutside) OrInto(result, MaskFor(bp, roomId, view, cache));
                if (outside)
                {
                    anyOredRoomOutside = true; // native 0x1CF75C
                    AddOutsidePersonRectangle(bp, view, result);
                }
            }

            // Cursor vicinity (native 0x1CFA64..0x1CFFC8).
            if (!view.CursorSuppressed && view.CursorScreenPos.HasValue
                && view.BufferBounds.Contains(view.CursorScreenPos.Value)
                && view.ScreenToTile != null)
            {
                // Native: mouse.y += 32 << viewerZoom before PointToTile. Zoom-index
                // mapping: the native zoom index z numbers half-Far..Near as 0..3 and
                // its tile width is 16<<z px, so z = log2(portTileWidth*PreciseZoom/16)
                // (native 0 = the port's Far @ PreciseZoom 0.5). The adjust lands in
                // buffer pixels, so scale by PreciseZoom (native has no sub-bucket).
                var yAdjust = (32 << NativeZoomIndex(view.Zoom, view.PreciseZoom)) * view.PreciseZoom;
                var adjusted = new Point(view.CursorScreenPos.Value.X,
                    view.CursorScreenPos.Value.Y + (int)Math.Round(yAdjust));
                var tileF = view.ScreenToTile(adjusted);
                var tile = new Point((int)Math.Floor(tileF.X), (int)Math.Floor(tileF.Y));
                var corner = new Point(tile.X - 4, tile.Y - 4); // native tile + 4*(-1,-1)

                // Pull an off-lot sample back toward the lot along (-1,-1), the
                // corner back along (+1,+1) (native 0x1CFC30/0x1CFD10, full bounds).
                while (!InWorld(bp, tile) && tile != corner) { tile.X -= 1; tile.Y -= 1; }
                while (!InWorld(bp, corner) && corner != tile) { corner.X += 1; corner.Y += 1; }

                // r26 gate (native 0x1CFDC4): mark only when an ORed room was
                // outside. Half-open [corner, tile) rectangle, cursor tile excluded,
                // per-tile bounds checks (native CTileRect iteration).
                if (anyOredRoomOutside && InWorld(bp, corner))
                {
                    for (int y = corner.Y; y < tile.Y; y++)
                    {
                        for (int x = corner.X; x < tile.X; x++)
                        {
                            if (x >= 0 && x < bp.Width && y >= 0 && y < bp.Height)
                                result[y * bp.Width + x] = true;
                        }
                    }
                }

                // Tool callback hook (native slot 18, dispatched on the current tool
                // when the corner is in bounds). Unwired by default - see
                // CutawayViewInputs.AdjustCutawayForTool.
                if (InWorld(bp, corner)) view.AdjustCutawayForTool?.Invoke(corner, result);
            }

            return result;
        }

        /// <summary>
        /// Port of RoomManager::ComputeCutaway (native 0x128AE0): the OR of every
        /// matching room's mask, as one flat world-space array (native stores
        /// per-room matrices; the port's readers consume a single array).
        /// floorArg follows the native convention: 0 = all floors, otherwise the
        /// 1-based floor. Native also gates on the room+0x34 "live" word; port
        /// rooms are live by construction - bp.Rooms only holds rooms the VM
        /// room partitioning handed out, and outside rooms are skipped because
        /// native leaves their matrices empty (early return in
        /// ComputeCutawayMatrix).
        /// </summary>
        public static bool[] ComputeCutawayRooms(Blueprint bp, sbyte floorArg, CutawayMaskCache cache = null, CutawayViewInputs view = null)
        {
            var result = new bool[bp.Width * bp.Height];
            if (view == null) view = new CutawayViewInputs { Zoom = WorldZoom.Near };
            for (uint roomId = 0; roomId < (uint)bp.Rooms.Count; roomId++)
            {
                var room = bp.Rooms[(int)roomId];
                if (room.IsOutside) continue;
                if (floorArg != 0 && room.Floor != floorArg - 1) continue; //room.Floor is 0-based; floorArg is 1-based
                OrInto(result, cache?.GetOrCompute(bp, roomId, view) ?? ComputeRoomMask(bp, roomId, view));
            }
            return result;
        }

        /// <summary>True when the two cut arrays differ anywhere (native XOR + dirty law).</summary>
        public static bool Differs(bool[] a, bool[] b)
        {
            if (ReferenceEquals(a, b)) return false;
            if (a == null || b == null || a.Length != b.Length) return true;
            for (var i = 0; i < a.Length; i++) if (a[i] != b[i]) return true;
            return false;
        }

        /// <summary>
        /// Native zoom index (0 = half-Far .. 3 = Near) of a port zoom bucket +
        /// precise zoom: native tile width is 16&lt;&lt;z px, the port's effective
        /// tile width is 32/64/128 * PreciseZoom px.
        /// </summary>
        public static int NativeZoomIndex(WorldZoom zoom, float preciseZoom)
        {
            float tileWidth = zoom == WorldZoom.Near ? 128f : zoom == WorldZoom.Medium ? 64f : 32f;
            var ratio = tileWidth * preciseZoom / 16f;
            if (ratio <= 1) return 0;
            if (ratio >= 8) return 3;
            return (int)Math.Round(Math.Log(ratio, 2));
        }

        private static bool[] MaskFor(Blueprint bp, uint roomId, CutawayViewInputs view, CutawayMaskCache cache)
        {
            return cache?.GetOrCompute(bp, roomId, view) ?? ComputeRoomMask(bp, roomId, view);
        }

        private static void OrInto(bool[] target, bool[] source)
        {
            var len = Math.Min(target.Length, source.Length);
            for (var i = 0; i < len; i++) if (source[i]) target[i] = true;
        }

        private static bool InWorld(Blueprint bp, Point tile)
        {
            return tile.X >= 0 && tile.X < bp.Width && tile.Y >= 0 && tile.Y < bp.Height;
        }

        /// <summary>
        /// Outside-person bounded rectangle (native 0x1CF7BC..0x1CF8B0): the
        /// person's tile gated to 1 &lt;= x,y &lt;= size-2, then the k=4..0
        /// first-inside probe and a [x, x+k) x [y, y+k) mark; k=0 degenerates to
        /// nothing. DEVIATION: native remaps the tile through BuildMaxAltsTable
        /// when the world carries altitude levels; the port has no max-alts
        /// table, so the tile is used as-is.
        /// </summary>
        private static void AddOutsidePersonRectangle(Blueprint bp, CutawayViewInputs view, bool[] result)
        {
            if (!view.PersonTile.HasValue) return;
            var x = view.PersonTile.Value.X;
            var y = view.PersonTile.Value.Y;
            if (x < 1 || x > bp.Width - 2 || y < 1 || y > bp.Height - 2) return;

            var k = 0;
            for (var probe = 4; probe >= 1; probe--)
            {
                if (x + probe < bp.Width && y + probe < bp.Height)
                {
                    k = probe;
                    break;
                }
            }
            if (k == 0) return;

            for (var yy = y; yy < y + k; yy++)
            {
                for (var xx = x; xx < x + k; xx++)
                {
                    if (xx >= 0 && xx < bp.Width && yy >= 0 && yy < bp.Height)
                        result[yy * bp.Width + xx] = true;
                }
            }
        }

        /// <summary>
        /// Rotated-frame grid transform, port-defined and proven consistent with
        /// WorldSpace.GetScreenFromTile: for every rotation the base rotated
        /// projection px=(rx-ry)*WH, py=(rx+ry)*HH-zTerm applied to RotMap(x,y)
        /// equals the port's rotation-specific formula on (x,y) (TopLeft is the
        /// identity, TopRight (x,y)->(-y,x), BottomRight (x,y)->(-x,-y),
        /// BottomLeft (x,y)->(y,-x)). The native LUT generator is UNRESOLVED
        /// (r244-cutaway-geom UNRESOLVED 1); the port only needs a self-
        /// consistent pair, which this is.
        /// </summary>
        private static Point RotMap(WorldRotation rot, int x, int y)
        {
            switch (rot)
            {
                case WorldRotation.TopRight: return new Point(-y, x);
                case WorldRotation.BottomRight: return new Point(-x, -y);
                case WorldRotation.BottomLeft: return new Point(y, -x);
                default: return new Point(x, y);
            }
        }

        private static Point InvRotMap(WorldRotation rot, int rx, int ry)
        {
            switch (rot)
            {
                case WorldRotation.TopRight: return new Point(ry, -rx);
                case WorldRotation.BottomRight: return new Point(-rx, -ry);
                case WorldRotation.BottomLeft: return new Point(-ry, rx);
                default: return new Point(rx, ry);
            }
        }

        /// <summary>Image of the world tile rectangle under <see cref="RotMap"/>.</summary>
        private static void RotatedBounds(WorldRotation rot, int w, int h, out int minRX, out int maxRX, out int minRY, out int maxRY)
        {
            minRX = minRY = int.MaxValue;
            maxRX = maxRY = int.MinValue;
            for (var i = 0; i < 4; i++)
            {
                var p = RotMap(rot, (i & 1) != 0 ? w - 1 : 0, (i & 2) != 0 ? h - 1 : 0);
                if (p.X < minRX) minRX = p.X;
                if (p.X > maxRX) maxRX = p.X;
                if (p.Y < minRY) minRY = p.Y;
                if (p.Y > maxRY) maxRY = p.Y;
            }
        }

        /// <summary>
        /// Per-view projection metrics, derived from WorldSpace (which derives
        /// them per zoom in Invalidate()): tile width 128/64/32 px, half-width
        /// WH = TilePxWidthHalf, half-height HH = TilePxHeightHalf = WH/2,
        /// OneUnitDistance = sqrt(TileWidth^2/2) and the per-z-unit screen
        /// term OneUnitDistance*cos(pi/6).
        ///
        /// Native viewer metrics comparison (command 0xf6 resets them to
        /// 0x10/8/8/4 &lt;&lt; zoom): h = TileHalfRaw/2 = 8&lt;&lt;z = half the tile
        /// width = WH; V5C (wall half width) = 8&lt;&lt;z = WH; V60 (wall bottom
        /// extent) = 8&lt;&lt;z = WH; F (story height px) = the projected delta of
        /// one story = 2.95 * OneUnitDistance*cos(pi/6) - the same 2.95
        /// tile-z-units-per-story every port wall/floor projection uses
        /// (e.g. WallComponent tilePosition z). PreciseZoom scales all five
        /// terms identically, so it cancels out of the overlap algebra and is
        /// not applied here.
        /// </summary>
        private static void Metrics(Blueprint bp, CutawayViewInputs view,
            out float wh, out float hh, out float unitZ, out float h, out float v5c, out float v60, out float f)
        {
            var tileWidth = view.Zoom == WorldZoom.Near ? 128f : view.Zoom == WorldZoom.Medium ? 64f : 32f;
            wh = tileWidth / 2f;
            hh = tileWidth / 4f;
            unitZ = (float)Math.Sqrt(tileWidth * tileWidth / 2.0) * (float)Math.Cos(Math.PI / 6);
            h = wh;
            v5c = wh;
            v60 = wh;
            f = 2.95f * unitZ;
        }

        private static readonly Point[] SweepOffsets =
        {
            new Point(0, 1), new Point(1, 0), new Point(1, 1)
        };

        /// <summary>
        /// WorldSpace.GetScreenFromTile equivalent for one tile, including the
        /// story z and terrain altitude terms exactly as wall drawing positions
        /// them ((level-1)*2.95 + altitude in tile-z units). Base-metric pixels
        /// (PreciseZoom cancels in every consumer).
        /// </summary>
        private static Vector2 ProjectTile(Blueprint bp, WorldRotation rot, int x, int y, sbyte floor, float wh, float hh, float unitZ)
        {
            var z = (floor - 1) * 2.95f + AltitudeAt(bp, x, y);
            float sx, sy;
            switch (rot)
            {
                case WorldRotation.TopRight:
                    sx = (-x - y) * wh;
                    sy = (x - y) * hh - z * unitZ;
                    break;
                case WorldRotation.BottomRight:
                    sx = (-x + y) * wh;
                    sy = (-x - y) * hh - z * unitZ;
                    break;
                case WorldRotation.BottomLeft:
                    sx = (x + y) * wh;
                    sy = (-x + y) * hh - z * unitZ;
                    break;
                default:
                    sx = (x - y) * wh;
                    sy = (x + y) * hh - z * unitZ;
                    break;
            }
            return new Vector2(sx, sy);
        }

        private static float AltitudeAt(Blueprint bp, int x, int y)
        {
            if (bp.AltitudeCenters == null) return 0f;
            return bp.GetAltitude(x, y);
        }
    }
}
