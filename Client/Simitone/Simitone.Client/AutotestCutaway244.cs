using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FSO.Client;
using FSO.Client.UI.Framework;
using FSO.Common.Rendering.Framework.Model;
using FSO.LotView;
using FSO.LotView.Components;
using FSO.LotView.Model;
using FSO.LotView.Utils;
using FSO.SimAntics;
using FSO.SimAntics.Entities;
using FSO.SimAntics.Model;
using FSO.SimAntics.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simitone.Client.UI.Panels;
using Simitone.Client.UI.Screens;

namespace Simitone.Client
{
    // R244 'uicutaway' — the dynamic-cutaway check battery. Runs in the real
    // graphics loop on the loaded lot (house 5), against the engine's
    // CutawayMatrix composition (tools/iff-dump/r244-cutaway-state/decode.md is
    // the state contract; r244-cutaway-geom is the geometry contract).
    //
    // FLOOR CONVENTIONS (root-caused round 2): Room.Floor is the port's
    // 0-BASED story index (VMRoomMap.GenerateMap copies the 0-based story
    // verbatim; IsIndoorsPrecise/LotFacadeGenerator index RoomMap with it),
    // while every VIEW floor here (World.State.Level, LotTilePos.Level,
    // CutawayViewInputs.Floor) is 1-BASED. level 1 rooms => r.Floor == 0;
    // the synthetic floor-2 fixtures live on r.Floor == 1. Room handles are
    // captured as IDS, not object references: building the fixtures
    // regenerates Blueprint.Rooms (struct copies keep the old values).
    //
    // Parts a-d drive pure ComposeDynamic laws and the production history
    // driver (UILotControl.UpdateCutawayDynamic), including the floor-change
    // history-clear lifecycle; part e drives the real PIP renderer with
    // pipInputs (cross-floor mask = empty history ∪ live-gated person room —
    // NO all-rooms base); part f is the simvis-style grab() world-capture
    // diff. Fixture-backed sub-assertions that cannot be made deterministic
    // headlessly degrade to 'uicutaway-verbose' instead of weakening the
    // default gate; the native strict-overlap touch exclusion has no
    // constructible headless fixture at all (exact edge-touch is unreachable
    // in the port's integer-tile projection) and is a logged opt-in residual,
    // not a check. Ctor snapshots and Dispose restores every touched setting
    // (AutotestPIPUI240 pattern); PNG evidence lands under <UserDir>/ui-audit/r244.
    internal sealed class AutotestCutaway244 : UIContainer, IDisposable
    {
        private readonly TS1GameScreen Game;
        private readonly World World;
        private readonly UILotControl Lot;
        private readonly VM VM;
        private readonly Blueprint BP;

        // ctor snapshot (restored in Dispose)
        private readonly int Speed;
        private readonly int WallsMode;
        private readonly Vector2 CenterTile;
        private readonly sbyte Level;
        private readonly WorldRotation RotBefore;
        private readonly WorldZoom Zoom;
        private readonly AvatarComponent Anchor;
        private readonly bool[] CutawayBefore; //content copy for restore verification
        private readonly bool LiveMode; //the production person branch must be quiet under the battery

        private readonly bool Verbose;
        private readonly List<string> Failures = new List<string>();
        private readonly List<string> VerboseFailures = new List<string>();
        private int Phase;
        internal bool Ready { get; private set; }
        internal bool Passed => Ready && Failures.Count == 0;
        internal string Diagnostics => Failures.Count == 0
            ? "outside-empty, diagonal-sides, rotations/zooms, floor-change clears history, history insert/dup/evict/reset, person OR/floor-gate, cursor r26 rect, PIP empty/person masks+restore, world grab diff; state restored"
            : string.Join(", ", Failures);
        internal bool VerboseRan { get; private set; }
        internal bool VerbosePassed => VerboseFailures.Count == 0;
        internal string VerboseDiagnostics => VerboseFailures.Count == 0 ? "verbose sub-assertions passed" : string.Join(", ", VerboseFailures);

        // synthetic floor-2 fixture (level 2 = 0-based Floor 1): a sealed
        // 16x16 square with a cross wall = four 8x8 indoor rooms, driving the
        // production history battery, the PIP cross-floor live-person leg and
        // the PIP framing deterministically (see the diagonal-side residual
        // in BuildFloor2Fixtures for why there is no diagonal fixture)
        private WallTile[] FixWallsSnap;
        private List<int> FixWallsAtSnap;
        private FloorTile[] FixFloorsSnap;
        private List<uint> FixRoomSnap; //floor-2 partition (id/outside) before the fixtures
        private Rectangle CrossRect;
        private bool CrossBuilt;
        private uint[] CrossRooms = new uint[0]; //the four cross-split rooms

        internal AutotestCutaway244(TS1GameScreen game, bool verbose)
        {
            Game = game; Verbose = verbose;
            World = game.LotControl.World;
            Lot = game.LotControl;
            VM = game.vm;
            BP = VM.Context.Blueprint;
            // snapshot everything the battery touches
            Speed = VM.SpeedMultiplier;
            WallsMode = Lot.WallsMode;
            CenterTile = World.State.CenterTile;
            Level = World.State.Level;
            RotBefore = World.State.Rotation;
            Zoom = World.State.Zoom;
            Anchor = World.State.ScrollAnchor;
            CutawayBefore = BP.Cutaway != null ? (bool[])BP.Cutaway.Clone() : null;
            LiveMode = Lot.LiveMode;
            VM.SpeedMultiplier = 0; //stable rooms/person tiles for the whole battery
            Lot.WallsMode = 1; //dynamic mode: the native composition this round pins
            Lot.LiveMode = false; //keep the production person branch out of the history/cursor assertions
        }

        private void Require(bool value, string name, string detail = "")
        {
            GameLog.Write("AUTOTEST uicutaway " + name + ": " + (value ? "PASS" : "FAIL") + (detail.Length > 0 ? " " + detail : ""));
            if (!value) Failures.Add(name);
        }
        private void RequireVerbose(bool value, string name, string detail = "")
        {
            VerboseRan = true;
            GameLog.Write("AUTOTEST uicutaway-verbose " + name + ": " + (value ? "PASS" : "FAIL") + (detail.Length > 0 ? " " + detail : ""));
            if (!value) VerboseFailures.Add(name);
        }

        // ---------- composition helpers (all battery laws go through the engine) ----------

        private CutawayViewInputs Inputs(sbyte floor, WorldRotation rot, WorldZoom zoom, float preciseZoom, IEnumerable<uint> history)
        {
            // BufferBounds in LOGICAL pixels (the same units as the cursor
            // points, which are MouseState/DPIScaleFactor): mirrors the
            // production builders, and identical to the physical viewport at
            // DPI 1
            var viewport = GameFacade.GraphicsDevice.Viewport;
            float dpi = FSO.Common.FSOEnvironment.DPIScaleFactor;
            return new CutawayViewInputs
            {
                Floor = floor,
                Rotation = rot,
                Zoom = zoom,
                PreciseZoom = preciseZoom,
                BufferBounds = new Rectangle(0, 0, (int)(viewport.Width / dpi), (int)(viewport.Height / dpi)),
                HistoryRooms = (history ?? new uint[0]).ToArray(),
                DynamicEnabled = true,
                // the port's own projection, exactly what the production wiring passes
                ScreenToTile = pos => World.EstTileAtPosWithScroll(new Vector2(pos.X, pos.Y)),
            };
        }

        private bool[] Compose(sbyte floor, IEnumerable<uint> history,
            WorldRotation? rot = null, WorldZoom? zoom = null, float preciseZoom = 1f,
            Point? cursor = null, bool suppressed = false,
            uint? personRoom = null, sbyte personFloor = 0, bool personOutside = false)
        {
            // default to the PINNED live view (RunBattery pins TopLeft/Near and
            // the production driver composes at exactly that view) — NOT the
            // ctor snapshot, which is only the view the lot happened to enter
            // with and is restored in Dispose
            var view = Inputs(floor, rot ?? WorldRotation.TopLeft, zoom ?? WorldZoom.Near, preciseZoom, history);
            view.CursorScreenPos = cursor;
            view.CursorSuppressed = suppressed;
            if (personRoom.HasValue)
            {
                view.PersonRoomId = personRoom;
                view.PersonFloor = personFloor;
                view.PersonOutside = personOutside;
                if (PersonFixture.HasValue) view.PersonTile = new Point(PersonFixture.Value.x, PersonFixture.Value.y);
            }
            return CutawayMatrix.ComposeDynamic(BP, view);
        }

        // the synthetic outside-person fixture tile (center of the lot, floor 1)
        private static (int x, int y, int z)? PersonFixture;

        private static bool AnyTrue(bool[] mask, out int count)
        {
            count = 0;
            if (mask == null) return false;
            foreach (var b in mask) if (b) count++;
            return count > 0;
        }

        private static bool SameMask(bool[] a, bool[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        // The wall band a single-room composition can mark. The sweep only
        // advances toward the viewer: candidates sit at (x+i, y+j) with
        // |i-j| <= 1, and the strict-overlap test marks while
        // (i+j)*HH <= WH + F - 3, i.e. i+j <= 2*2.95*sqrt(2)*cos(30°) + 2
        // - 6/WH = 9.23 - 6/WH: 9 at Near/Medium, 8 at Far — so a single axis
        // reaches at most 5 tiles past the room bounds (j >= i-1), never
        // up-left. Verified at Near: (5,4) marks (9*32=288 <= 292.2), (5,5)
        // does not (320 > 292.2). Inflate(5) is the law-exact band.
        private static Rectangle Band(Rectangle roomBounds)
        {
            var band = roomBounds;
            band.Inflate(5, 5);
            return band;
        }

        private uint TileRoom(int x, int y, sbyte level)
        {
            return (ushort)BP.RoomMap[level - 1][y * BP.Width + x];
        }

        // move the picking camera so the target area is on-screen (CenterTile
        // setter invalidates scroll + camera; PrepareCamera reprojects) — the
        // ctor snapshot is restored in Dispose
        private void CenterCamera(Vector2 tile)
        {
            World.State.CenterTile = tile;
            World.State.PrepareCamera();
        }

        // a screen position (LOGICAL pixels — the units the production driver
        // and CutawayViewInputs.CursorScreenPos use) whose EstTileAtPosWithScroll
        // lands exactly on the target tile: self-calibrating by construction,
        // since it inverts through the same projection the production wiring
        // passes (World.EstTileAtPosWithScroll). No analytic forward estimate
        // is trusted; the whole buffer plus a 512px margin is scanned — the
        // margin covers the story-height pick shift at higher levels (the
        // level-2 pick slides the effective window ~3.6 tiles down-screen).
        private bool TryScreenForTile(Vector2 tile, out Point screen)
        {
            var bounds = LogicalBuffer();
            for (int y = -512; y < bounds.Height + 512; y += 8)
            {
                for (int x = -512; x < bounds.Width + 512; x += 8)
                {
                    var t = World.EstTileAtPosWithScroll(new Vector2(x, y));
                    if ((int)t.X == (int)tile.X && (int)t.Y == (int)tile.Y)
                    {
                        screen = new Point(x, y);
                        return true;
                    }
                }
            }
            screen = Point.Zero;
            return false;
        }

        // a screen point whose tile is OFF the lot interior ([1..W-2]) — the
        // deterministic MouseTrack off-lot/reset fixture
        private bool TryScreenOffLot(out Point screen)
        {
            var bounds = LogicalBuffer();
            for (int y = 0; y < bounds.Height; y += 8)
            {
                for (int x = 0; x < bounds.Width; x += 8)
                {
                    var t = World.EstTileAtPosWithScroll(new Vector2(x, y));
                    if (t.X < 1 || t.Y < 1 || t.X > BP.Width - 2 || t.Y > BP.Height - 2)
                    {
                        screen = new Point(x, y);
                        return true;
                    }
                }
            }
            screen = Point.Zero;
            return false;
        }

        private Rectangle LogicalBuffer()
        {
            var viewport = GameFacade.GraphicsDevice.Viewport;
            float dpi = FSO.Common.FSOEnvironment.DPIScaleFactor;
            return new Rectangle(0, 0, (int)(viewport.Width / dpi), (int)(viewport.Height / dpi));
        }

        // a tile deep inside the room: the whole 8-neighborhood maps to the same
        // room, so the behind-room probe walk stays inside (front == behind)
        private bool TryDeepTile(uint roomId, sbyte level, out Vector2 tile)
        {
            var room = BP.Rooms[(int)roomId];
            int bestScore = -1; tile = default;
            for (int y = room.Bounds.Top; y < room.Bounds.Bottom; y++)
            {
                for (int x = room.Bounds.Left; x < room.Bounds.Right; x++)
                {
                    if (x < 1 || y < 1 || x >= BP.Width - 1 || y >= BP.Height - 1) continue;
                    if (TileRoom(x, y, level) != roomId) continue;
                    int score = 0;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                            if (TileRoom(x + dx, y + dy, level) == roomId) score++;
                    if (score > bestScore) { bestScore = score; tile = new Vector2(x, y); }
                }
            }
            return bestScore >= 9; //full 3x3 block in-room
        }

        // distinct room ids owning tiles inside a level-2 rectangle, by tile
        // count (fixture-room resolution — robust against natural rooms whose
        // bounding boxes span the fixture area)
        private List<uint> RoomIdsInRect(Rectangle rect, sbyte level)
        {
            var counts = new Dictionary<uint, int>();
            for (int y = rect.Top; y < rect.Bottom; y++)
                for (int x = rect.Left; x < rect.Right; x++)
                {
                    var r = TileRoom(x, y, level);
                    counts[r] = (counts.TryGetValue(r, out var c) ? c : 0) + 1;
                }
            return counts.OrderByDescending(kv => kv.Value).Where(kv => kv.Key != 0).Select(kv => kv.Key).ToList();
        }

        // ---------- the battery ----------

        public override void PreDraw(UISpriteBatch batch)
        {
            if (Ready || !World.PictureInPictureReady || BP == null) return;
            try
            {
                if (Phase++ == 0) RunBattery(batch);
                else
                {
                    RestoreAndVerify();
                    Ready = true;
                }
            }
            catch (Exception e)
            {
                GameLog.Write("AUTOTEST uicutaway EXC " + e);
                Failures.Add("exception:" + e.GetType().Name);
                Ready = true;
            }
        }

        private void RunBattery(UISpriteBatch batch)
        {
            // stable 2D view: floor 1, near zoom, no scroll anchor interference
            World.State.ScrollAnchor = null;
            World.State.Level = 1;
            World.State.Zoom = WorldZoom.Near;
            World.State.Rotation = WorldRotation.TopLeft;
            World.State.PrepareCamera();
            Lot.CutawayHistoryReset();
            Lot.CutMaskCache.Invalidate();

            // diagnostics: full partition inventory (id:floor:indoor/outside:area)
            GameLog.Write("AUTOTEST uicutaway room-inventory: " +
                string.Join(";", BP.Rooms.Select(r => r.RoomID + ":f" + r.Floor + (r.IsOutside ? "o" : "i") + r.Area)));

            // level 1 (ground) rooms: r.Floor == 0 in the port's 0-based convention
            var groundIndoor = BP.Rooms.Where(r => r.Floor == 0 && !r.IsOutside && r.Area >= 9)
                .OrderByDescending(r => r.Area).ToList();
            var groundOutside = BP.Rooms.Where(r => r.Floor == 0 && r.IsOutside).ToList();
            Require(groundIndoor.Count > 0, "fixture-indoor-room",
                "groundIndoor=" + groundIndoor.Count + " groundOutside=" + groundOutside.Count);
            if (groundIndoor.Count == 0) return;
            uint mainRoom = (uint)BP.Rooms.IndexOf(groundIndoor[0]);
            uint outsideRoom = groundOutside.Count > 0 ? (uint)BP.Rooms.IndexOf(groundOutside[0]) : uint.MaxValue;

            // ---- fixtures first: the history battery drives the synthetic ----
            // ---- four-room floor-2 fixture through the production driver ----
            BuildFloor2Fixtures();

            // ---- (a) pure-law battery ----
            PureLawBattery(mainRoom, outsideRoom);

            // ---- (b) history battery (production driver, synthetic fixture) ----
            HistoryBattery();

            // ---- (b2) floor-change lifecycle (production driver) ----
            FloorChangeBattery(mainRoom);

            // ---- (c) person battery ----
            PersonBattery(mainRoom);

            // ---- (d) cursor battery ----
            CursorBattery(outsideRoom, mainRoom);

            // ---- (e) PIP battery ----
            PipBattery(mainRoom);

            // ---- (f) render-level proof ----
            GrabBattery(mainRoom);
        }

        private void PureLawBattery(uint mainRoom, uint outsideRoom)
        {
            var room = BP.Rooms[(int)mainRoom];
            var roomMask = Compose(1, new uint[] { mainRoom });
            // law-exact: the composition of a single indoor history room IS that
            // room's per-room mask (ComposeDynamic ORs the history rooms)
            var view = Inputs(1, WorldRotation.TopLeft, WorldZoom.Near, 1, new uint[] { mainRoom });
            var directMask = CutawayMatrix.ComputeRoomMask(BP, mainRoom, view);
            int cut;
            Require(AnyTrue(roomMask, out cut) && SameMask(roomMask, directMask), "room-mask-nonempty",
                "room=" + mainRoom + " area=" + room.Area + " cutTiles=" + cut + " equalsRoomMask=" + SameMask(roomMask, directMask));

            // cross-floor isolation is NOT an engine floor filter: the engine
            // ORs history rooms unconditionally (a level-1 room DOES compose a
            // mask under a level-2 view). The isolation law lives in the
            // client's floor-change history clear — FloorChangeBattery drives
            // it through the production pipeline below.
            var floor1Empty = Compose(1, new uint[0]);
            Require(!AnyTrue(floor1Empty, out _), "empty-history-empty-mask");

            // outside rooms produce empty masks
            RequireVerbose(outsideRoom != uint.MaxValue, "outside-room-fixture",
                "level-1 outside room=" + (outsideRoom == uint.MaxValue ? "none" : outsideRoom.ToString()));
            if (outsideRoom != uint.MaxValue)
            {
                var outsideMask = Compose(1, new uint[] { outsideRoom });
                Require(!AnyTrue(outsideMask, out _), "outside-room-empty-mask", "room=" + outsideRoom);
            }

            // diagonal half-rooms: existing lot pair, else the synthetic fixture
            var diag = FindExistingDiagonalPair();
            if (diag.HasValue)
            {
                var (a, b, lvl) = diag.Value;
                var ma = Compose(lvl, new uint[] { a });
                var mb = Compose(lvl, new uint[] { b });
                AnyTrue(ma, out int ca); AnyTrue(mb, out int cb);
                Require(ca > 0 && cb > 0 && !SameMask(ma, mb), "diagonal-side-distinct",
                    "rooms=" + a + "/" + b + " level=" + lvl + " cutA=" + ca + " cutB=" + cb);
            }
            else GameLog.Write("AUTOTEST uicutaway diagonal-pair: deferred to synthetic floor-2 fixture (no indoor/indoor diagonal on this lot)");

            // all four rotations and all zoom buckets recompute without exceptions
            bool rotationsOk = true; string rotInfo = "";
            foreach (WorldRotation rot in new[] { WorldRotation.TopLeft, WorldRotation.TopRight, WorldRotation.BottomRight, WorldRotation.BottomLeft })
            {
                foreach (var z in new[] { WorldZoom.Near, WorldZoom.Medium, WorldZoom.Far })
                {
                    try
                    {
                        var m = Compose(1, new uint[] { mainRoom }, rot, z);
                        if (m == null || m.Length != BP.Width * BP.Height) { rotationsOk = false; rotInfo = "bad-length " + rot + "/" + z; }
                    }
                    catch (Exception ex) { rotationsOk = false; rotInfo = rot + "/" + z + ":" + ex.Message; }
                }
                try
                {
                    var m = Compose(1, new uint[] { mainRoom }, rot, WorldZoom.Far, 0.5f);
                    if (m == null) { rotationsOk = false; rotInfo = "null " + rot + "/pz0.5"; }
                }
                catch (Exception ex) { rotationsOk = false; rotInfo = rot + "/pz0.5:" + ex.Message; }
            }
            Require(rotationsOk, "rotations-zooms-recompose", rotInfo);

            // strict-overlap band containment: every cut tile of a single-room
            // composition lies within the room's wall band (bounds + 4 — the
            // sweep's law-exact diagonal reach, see Band).
            var bounds = Band(room.Bounds);
            bool contained = true; int stray = 0;
            for (int y = 0; y < BP.Height && contained; y++)
                for (int x = 0; x < BP.Width; x++)
                {
                    int i = y * BP.Width + x;
                    if (!roomMask[i]) continue;
                    if (x < bounds.Left || x >= bounds.Right || y < bounds.Top || y >= bounds.Bottom) { stray++; contained = false; break; }
                }
            Require(contained, "single-room-band-containment", "stray=" + stray);

            // strict-overlap touch boundary (decode.md A.5): NO headless
            // fixture can exercise it — in the port's integer-tile projection
            // an exact edge touch is unreachable (x-edge slacks are ≡ -1/-2
            // mod the tile pitch; the y edges carry the irrational
            // story-height term 2.95*sqrt(W²/2)*cos(π/6)), so the native
            // minus-1 exclusion can never be observed through the public
            // engine API. Band containment above carries the default gate;
            // this stays a LOGGED opt-in residual, not a check (a check that
            // cannot fail is worse than none).
            GameLog.Write("AUTOTEST uicutaway strict-overlap residual: opt-in only — exact edge-touch is unreachable in the port's integer-tile projection (slacks ≡ -1/-2 mod tile pitch, irrational story-height term on the y edges), so the native minus-1 exclusion has no constructible headless fixture; single-room-band-containment carries the default gate");
        }

        private int Count(bool[] mask)
        {
            AnyTrue(mask, out int c);
            return c;
        }

        private (uint a, uint b, sbyte level)? FindExistingDiagonalPair()
        {
            for (sbyte lvl = 1; lvl <= BP.Stories; lvl++)
            {
                var walls = BP.Walls[lvl - 1];
                for (int i = 0; i < walls.Length; i++)
                {
                    var seg = walls[i].Segments;
                    if ((seg & WallSegments.AnyDiag) == 0) continue;
                    var map = BP.RoomMap[lvl - 1][i];
                    uint lo = (ushort)map, hi = (ushort)((map >> 16) & 0x7FFF);
                    if (lo == 0 || hi == 0 || lo >= (uint)BP.Rooms.Count || hi >= (uint)BP.Rooms.Count) continue;
                    if (lo == hi) continue;
                    if (!BP.Rooms[(int)lo].IsOutside && !BP.Rooms[(int)hi].IsOutside) return (lo, hi, lvl);
                }
            }
            return null;
        }

        private void HistoryBattery()
        {
            // Drive the REAL production pipeline (Lot.UpdateCutawayDynamic) at
            // level 2 over the four synthetic cross-split rooms: insert, the
            // mask equality, duplicate insert without recency refresh, cap-3
            // oldest eviction, and the off-lot reset that keeps the history.
            if (!CrossBuilt || CrossRooms.Length != 4)
            {
                RequireVerbose(false, "history-fixture-four-rooms",
                    "four-room fixture unavailable on level 2 (see floor2-fixture)");
                return;
            }
            var rooms = CrossRooms.ToList();
            CenterCamera(new Vector2(CrossRect.Center.X, CrossRect.Center.Y));
            World.State.Level = 2;
            World.State.PrepareCamera();
            Lot.CutawayHistoryReset();

            // calibrate one deep tile + screen per quarter, each under its OWN
            // picking camera: the level-2 pick slides the effective screen
            // window ~3.6 tiles down-screen (the ray is raised one story), so
            // a fixed camera leaves the farthest quarter's deep tile outside
            // the reachable pick region — and a screen point is only valid
            // under the camera it was calibrated with
            var screens = new List<Point>();
            var misses = new List<uint>();
            foreach (var room in rooms)
            {
                if (TryScreenForRoom(room, out var screen)) screens.Add(screen);
                else misses.Add(room);
            }
            Require(screens.Count == 4, "history-fixture-four-rooms",
                "found=" + screens.Count + " of 4" + (misses.Count > 0 ? " missed=[" + string.Join(",", misses) + "]" : ""));
            if (screens.Count < 4)
            {
                World.State.Level = 1;
                World.State.PrepareCamera();
                Lot.CutawayHistoryReset();
                return;
            }

            // r1, r2, r3 insert; the mask covers exactly the history rooms' band.
            // Every drive re-centers the camera on its own room first (see above).
            for (int i = 0; i < 3; i++) DriveCutawayRoom(rooms[i], screens[i]);
            var h3 = Lot.CutawayHistory.ToArray();
            bool insertOk = h3.Length == 3 && rooms.Take(3).All(r => h3.Contains(r));
            Require(insertOk, "hover-inserts-cursor-room", "history=[" + string.Join(",", h3) + "] drove=[" + string.Join(",", rooms.Take(3)) + "]");

            var maskABC = (bool[])BP.Cutaway.Clone();
            var expectABC = Compose(2, rooms.Take(3));
            Require(SameMask(maskABC, expectABC), "history-drives-mask", "maskTiles=" + Count(maskABC));

            // duplicate insert does NOT refresh recency: re-driving r1 keeps the
            // membership, then r4 evicts the OLDEST member (r1), leaving {r2,r3,r4}.
            DriveCutawayRoom(rooms[0], screens[0]);
            var afterDup = Lot.CutawayHistory.ToArray();
            Require(afterDup.Length == 3 && rooms.Take(3).All(r => afterDup.Contains(r)),
                "duplicate-no-recency", "history=[" + string.Join(",", afterDup) + "]");
            DriveCutawayRoom(rooms[3], screens[3]);
            var h4 = Lot.CutawayHistory.ToArray();
            Require(h4.Length == 3 && h4.Contains(rooms[1]) && h4.Contains(rooms[2]) && h4.Contains(rooms[3]) && !h4.Contains(rooms[0]),
                "cap3-evicts-oldest", "history=[" + string.Join(",", h4) + "] expected=[" + string.Join(",", rooms.Skip(1)) + "]");
            var expectBCD = Compose(2, rooms.Skip(1));
            Require(SameMask(BP.Cutaway, expectBCD), "evicted-room-mask-retires", "maskTiles=" + Count(BP.Cutaway));

            // off-lot cursor: ResetDynamicCutaway clears the matrix, the history
            // survives. Camera at the lot corner makes buffer points map off-lot.
            CenterCamera(new Vector2(2, 2));
            if (TryScreenOffLot(out var off))
            {
                Lot.UpdateCutawayDynamic(off, false);
                Require(!AnyTrue(BP.Cutaway, out _) && Lot.CutawayHistory.Count == 3,
                    "off-lot-resets-keeps-history", "cutTiles=" + Count(BP.Cutaway) + " history=" + Lot.CutawayHistory.Count);
            }
            else RequireVerbose(false, "off-lot-resets-keeps-history", "no off-lot screen point at the corner camera");
            Lot.CutawayHistoryReset();
            World.State.Level = 1;
            World.State.PrepareCamera();
        }

        // center the picking camera on one fixture room and calibrate a cursor
        // screen point for its deep tile
        private bool TryScreenForRoom(uint room, out Point screen)
        {
            var b = BP.Rooms[(int)room].Bounds;
            CenterCamera(new Vector2(b.Center.X, b.Center.Y));
            if (TryDeepTile(room, 2, out var tile)) return TryScreenForTile(tile, out screen);
            screen = Point.Zero;
            return false;
        }

        // drive the production cutaway pipeline for one fixture room: the
        // camera MUST be re-centered on that room first — a screen point maps
        // through the CURRENT picking camera, so driving a stale point under
        // another room's camera inserts the wrong room
        private void DriveCutawayRoom(uint room, Point screen)
        {
            var b = BP.Rooms[(int)room].Bounds;
            CenterCamera(new Vector2(b.Center.X, b.Center.Y));
            Lot.UpdateCutawayDynamic(screen, false);
        }

        private void FloorChangeBattery(uint mainRoom)
        {
            // cross-floor isolation is a CLIENT lifecycle law, not an engine
            // floor filter: the engine ORs history rooms unconditionally, and
            // the native SetLevel/ScrollToTile idiom (decode.md §1.2A) CLEARS
            // the history on every floor change. Drive the production pipeline
            // (UpdateCutawayDynamic) across a floor switch and verify both
            // legs. The floor-switch legs use a suppressed cursor (a real
            // production state — RMB scroll / mouse off the lot): inserts stop
            // and the compose stays live, so nothing re-pollutes the history.
            Lot.CutawayHistoryReset();
            var roomCenter = BP.Rooms[(int)mainRoom].Bounds.Center;
            CenterCamera(new Vector2(roomCenter.X, roomCenter.Y));
            if (!TryDeepTile(mainRoom, 1, out var tile) || !TryScreenForTile(tile, out var screen))
            {
                Require(false, "floor-change-fixture", "no deep tile/screen for room " + mainRoom);
                return;
            }
            World.State.Level = 1;
            Lot.UpdateCutawayDynamic(screen, false); //production driver inserts the cursor room
            Require(Lot.CutawayHistory.Contains(mainRoom), "floor-change-fixture-insert",
                "history=[" + string.Join(",", Lot.CutawayHistory) + "] drove=[" + mainRoom + "]");

            // switch to level 2 and run the production update: the client
            // clears the history (SetLevel idiom), so the standing mask is the
            // EMPTY composition — a level-1 room never survives the swap
            World.State.Level = 2;
            Lot.UpdateCutawayDynamic(screen, true);
            Require(Lot.CutawayHistory.Count == 0, "floor-change-clears-history",
                "history=[" + string.Join(",", Lot.CutawayHistory) + "]");
            var standing = (bool[])BP.Cutaway.Clone();
            Require(SameMask(standing, Compose(2, new uint[0])), "floor-change-standing-mask-empty",
                "standingTiles=" + Count(standing));

            // back on level 1 the room no longer contributes until re-inserted
            World.State.Level = 1;
            Lot.UpdateCutawayDynamic(screen, true);
            Require(SameMask(BP.Cutaway, Compose(1, new uint[0])), "floor-change-room-retires",
                "cutTiles=" + Count(BP.Cutaway));
            Lot.CutawayHistoryReset();
        }

        private void PersonBattery(uint mainRoom)
        {
            // selected person = the port's controlled sim (vm.MyUID, plumbob)
            var sim = Lot.ResolvePerson();
            Require(sim != null && sim.Position != LotTilePos.OUT_OF_WORLD, "person-fixture-sim", sim == null ? "no selected person" : "pos=" + sim.Position);
            if (sim == null || sim.Position == LotTilePos.OUT_OF_WORLD) return;

            // The sim spawns OUTSIDE (room 1 is the level-1 outside room) — an
            // outside room's mask is empty BY LAW, so a standing-outside person
            // proves nothing here. Warp the sim into the indoor room (the PIP
            // fixture's mechanism), assert, and restore.
            if (!TryDeepTile(mainRoom, 1, out var stand))
            {
                Require(false, "person-fixture-indoor-tile", "no deep tile in room " + mainRoom);
                return;
            }
            var posBefore = sim.Position;
            try
            {
                sim.Position = LotTilePos.FromBigTile((short)stand.X, (short)stand.Y, 1);
                var room = VM.Context.GetRoomAt(LotTilePos.FromBigTile((short)stand.X, (short)stand.Y, 1));
                var outside = VM.Context.RoomInfo[room].Room.IsOutside;
                Require(room == mainRoom && !outside, "person-fixture-indoor-room",
                    "room=" + room + " expected=" + mainRoom + " outside=" + outside);

                // person on the view floor: empty history ∪ live person's
                // INDOOR room == EXACTLY that room's mask, localized to its band
                var baseMask = Compose(1, new uint[0]);
                var personMask = Compose(1, new uint[0], personRoom: room, personFloor: 1, personOutside: outside);
                var roomMask = Compose(1, new uint[] { room });
                AnyTrue(personMask, out int pc); AnyTrue(baseMask, out int bc);
                var added = personMask.Select((v, i) => (v, i)).Where(x => x.v && !baseMask[x.i]).Select(x => x.i).ToList();
                var roomBounds = Band(BP.Rooms[(int)room].Bounds);
                bool localized = added.All(i =>
                {
                    int x = i % BP.Width, y = i / BP.Width;
                    return x >= roomBounds.Left && x < roomBounds.Right && y >= roomBounds.Top && y < roomBounds.Bottom;
                });
                Require(SameMask(personMask, roomMask) && pc > bc && pc > 0 && localized, "person-room-ors-on-view-floor",
                    "room=" + room + " base=" + bc + " with=" + pc + " equalsRoomMask=" + SameMask(personMask, roomMask) + " addedLocalized=" + localized);

                // person on ANOTHER floor ORs nothing (floor gate)
                var gatedMask = Compose(2, new uint[0], personRoom: room, personFloor: 1, personOutside: outside);
                var otherEmpty = Compose(2, new uint[0]);
                Require(SameMask(gatedMask, otherEmpty), "person-other-floor-ors-nothing", "floor=2");
            }
            finally
            {
                sim.Position = posBefore;
            }

            // outside person: the k-probe rectangle (k=4..0 first inside), exact set.
            // Synthetic: compose directly with an outside room + fixture tile — the
            // real sim is indoors after the warp above (outside-person-k-probe-rect
            // is exact, so it carries the rectangle law).
            var outRoomIdx = BP.Rooms.FindIndex(r => r.Floor == 0 && r.IsOutside);
            if (outRoomIdx >= 0)
            {
                PersonFixture = (BP.Width / 2, BP.Height / 2, 1);
                OutsidePersonRectBattery((uint)outRoomIdx, LotTilePos.FromBigTile((short)(BP.Width / 2), (short)(BP.Height / 2), 1));
                PersonFixture = null;
            }
            else RequireVerbose(false, "outside-person-rect", "no outside room fixture on level 1");
        }

        private void OutsidePersonRectBattery(uint outsideRoomId, LotTilePos tile)
        {
            // native: gate 1<=x,y<=size-2; probe k=4,3,2,1,0 for (x+k,y+k) inside
            // the world bounds; marks the half-open [x,x+k)x[y,y+k) block, k=0 nothing.
            int x = tile.TileX, y = tile.TileY;
            if (x < 1 || y < 1 || x > BP.Width - 2 || y > BP.Height - 2)
            {
                RequireVerbose(false, "outside-person-rect", "fixture tile " + x + "," + y + " outside the [1,size-2] gate");
                return;
            }
            int k = 0;
            foreach (var cand in new[] { 4, 3, 2, 1, 0 })
                if (x + cand <= BP.Width - 1 && y + cand <= BP.Height - 1) { k = cand; break; }
            var expected = new HashSet<int>();
            for (int yy = y; yy < y + k; yy++)
                for (int xx = x; xx < x + k; xx++)
                    expected.Add(yy * BP.Width + xx);
            var mask = Compose(1, new uint[0], personRoom: outsideRoomId, personFloor: 1, personOutside: true);
            var actual = new HashSet<int>();
            for (int i = 0; i < mask.Length; i++) if (mask[i]) actual.Add(i);
            Require(actual.SetEquals(expected) && k >= 1, "outside-person-k-probe-rect",
                "k=" + k + " tile=" + x + "," + y + " expected=" + expected.Count + " actual=" + actual.Count);
        }

        private void CursorBattery(uint outsideRoom, uint mainRoom)
        {
            // cursor over a deep tile of the main room, far from the lot edges
            var roomCenter = BP.Rooms[(int)mainRoom].Bounds.Center;
            CenterCamera(new Vector2(roomCenter.X, roomCenter.Y));
            if (!TryDeepTile(mainRoom, 1, out var tile) || !TryScreenForTile(tile, out var cursor))
            {
                Require(false, "cursor-fixture", "no deep tile/screen for room " + mainRoom);
                return;
            }
            var roomMask = Compose(1, new uint[] { mainRoom });

            // r26 false (all-indoor history): the cursor marks nothing
            var withCursor = Compose(1, new uint[] { mainRoom }, cursor: cursor);
            Require(SameMask(roomMask, withCursor), "cursor-indoor-marks-nothing", "room=" + mainRoom);

            // r26 true (one ORed room outside): the composition is the history
            // rooms' masks UNION the half-open [tile+(-4,-4), tile) rectangle —
            // exactly that set, nothing else (the indoor history room's mask
            // ORs too; only the outside room trips r26).
            var history = outsideRoom != uint.MaxValue
                ? new uint[] { mainRoom, outsideRoom }
                : new uint[] { mainRoom };
            var r26Mask = Compose(1, history, cursor: cursor);
            if (outsideRoom != uint.MaxValue)
            {
                // expected cursor tile: the engine samples Y += (32<<zoomIndex)*pz
                // below the cursor (its own NativeZoomIndex law), then maps through
                // the same ScreenToTile the production wiring passes
                var yAdjust = (32 << CutawayMatrix.NativeZoomIndex(WorldZoom.Near, 1f)) * 1f;
                var ct = World.EstTileAtPosWithScroll(cursor.ToVector2() + new Vector2(0, yAdjust));
                int cx = (int)Math.Floor(ct.X), cy = (int)Math.Floor(ct.Y);
                var expected = new HashSet<int>();
                for (int i = 0; i < roomMask.Length; i++) if (roomMask[i]) expected.Add(i);
                bool expectable = cx - 4 >= 0 && cy - 4 >= 0 && cx <= BP.Width && cy <= BP.Height;
                if (expectable)
                    for (int yy = cy - 4; yy < cy; yy++)
                        for (int xx = cx - 4; xx < cx; xx++)
                            expected.Add(yy * BP.Width + xx);
                var actual = new HashSet<int>();
                for (int i = 0; i < r26Mask.Length; i++) if (r26Mask[i]) actual.Add(i);
                Require(expectable && actual.SetEquals(expected), "cursor-r26-half-open-rect",
                    "cursorTile=" + cx + "," + cy + " expected=" + expected.Count + " actual=" + actual.Count);
            }
            else RequireVerbose(false, "cursor-r26-half-open-rect", "no outside room to trip the r26 gate");

            // cursor outside the buffer marks nothing
            var offBuffer = Compose(1, history, cursor: new Point(-1000, -1000));
            Require(SameMask(roomMask, offBuffer), "cursor-outside-buffer-marks-nothing");

            // suppressed cursor marks nothing
            var suppressed = Compose(1, history, cursor: cursor, suppressed: true);
            Require(SameMask(roomMask, suppressed), "cursor-suppressed-marks-nothing");
        }

        // ---------- the synthetic floor-2 fixtures ----------

        private void BuildFloor2Fixtures()
        {
            if (BP.Stories < 2) { RequireVerbose(false, "floor2-fixture", "lot has a single story"); return; }
            try
            {
                var arch = VM.Context.Architecture;
                // floor-2 partition snapshot (level 2 = 0-based Floor 1)
                FixRoomSnap = BP.Rooms.Where(r => r.Floor == 1).Select(r => (uint)((r.RoomID << 1) | (r.IsOutside ? 1 : 0))).ToList();
                FixWallsSnap = (WallTile[])arch.Walls[1].Clone();
                FixWallsAtSnap = new List<int>(arch.WallsAt[1]);
                FixFloorsSnap = (FloorTile[])arch.Floors[1].Clone();

                // fixture A: sealed 16x16 + cross = four 8x8 indoor rooms. The
                // quarters MUST be at least 8x8: the production history driver's
                // probe walk samples up to +96px screen-down from the cursor
                // (3 tiles at Near zoom), so a deep tile needs a 3-tile in-room
                // margin below it or every hover inserts the behind room too
                // (front+behind is correct native MouseTrack behavior — the
                // history battery pins the single-insert deep-tile case).
                if (!TryFindEmptySpot(16, out CrossRect))
                {
                    RequireVerbose(false, "floor2-fixture", "no empty 16x16 level-2 region");
                    RestoreFixture();
                    return;
                }
                DrawFixtureWall(new Point(CrossRect.X, CrossRect.Y), 16, 0);
                DrawFixtureWall(new Point(CrossRect.X, CrossRect.Y), 16, 2);
                DrawFixtureWall(new Point(CrossRect.X, CrossRect.Y + 16), 16, 0);
                DrawFixtureWall(new Point(CrossRect.X + 16, CrossRect.Y), 16, 2);
                DrawFixtureWall(new Point(CrossRect.X, CrossRect.Y + 8), 16, 0); //cross: horizontal split
                DrawFixtureWall(new Point(CrossRect.X + 8, CrossRect.Y), 16, 2); //cross: vertical split
                VM.Context.Architecture.Tick();

                var crossIds = RoomIdsInRect(new Rectangle(CrossRect.X, CrossRect.Y, 16, 16), 2);
                if (crossIds.Count == 4)
                {
                    CrossRooms = crossIds.ToArray();
                    CrossBuilt = true;
                }
                else RequireVerbose(false, "floor2-fixture", "cross split produced " + crossIds.Count + " of 4 rooms");

                // DIAGONAL-SIDE RESIDUAL (logged, not a check — no headless
                // fixture exists): the lot has no indoor/indoor diagonal pair,
                // and a synthetic one is impossible in this port: the room-map
                // flood (VMRoomMap.GenerateMap) enters a diagonal tile on
                // either side but each side's validSpread contains one
                // direction that crosses to the opposite triangle, so a
                // diagonal-split interior merges into ONE room whenever both
                // triangles are floodable (real diagonal rooms are separated
                // by routing obstacles, not by the room map). Diagonal
                // half-rooms therefore never exist as separate room-map ids,
                // and ComputeRoomMask's per-side masks cannot be exercised
                // through the public engine API. The diagonal-side adjustment
                // stays covered by the r244-cutaway-geom decode review.
                GameLog.Write("AUTOTEST uicutaway diagonal-side residual: no constructible headless fixture — the port's room-map flood merges diagonal-split interiors into one room (opposite-side spread dir crosses at the diag tile), so diagonal half-rooms never exist as separate room ids; the diagonal-side mask law is covered by the r244-cutaway-geom decode review, not a runtime check");
            }
            catch (Exception ex)
            {
                GameLog.Write("AUTOTEST uicutaway floor2-fixture EXC " + ex);
                RequireVerbose(false, "floor2-fixture", ex.Message);
                try { RestoreFixture(); } catch { }
            }
        }

        private bool TryFindEmptySpot(int size, out Rectangle spot)
        {
            for (int y = 2; y + size < BP.Height - 2; y += 2)
                for (int x = 2; x + size < BP.Width - 2; x += 2)
                {
                    bool empty = true;
                    for (int yy = y - 1; yy <= y + size && empty; yy++)
                        for (int xx = x - 1; xx <= x + size && empty; xx++)
                            if (BP.Walls[1][yy * BP.Width + xx].Segments != 0) empty = false;
                    if (empty) { spot = new Rectangle(x, y, size, size); return true; }
                }
            spot = Rectangle.Empty;
            return false;
        }

        private void DrawFixtureWall(Point pos, int length, int direction)
        {
            // force: floor-2 walls over unsupported yard tiles are a synthetic
            // fixture, not player construction — verification is bypassed.
            // style MUST be 1 (real wall): the room-map flood only treats
            // style==1 segments as opaque walls; a style!=1 separator (fence)
            // records adjacency to the neighbor room, so the fixture rooms'
            // LightBaseRoom collapses to the outside room and the
            // LightBaseRoom-translated Blueprint.RoomMap (what TileRoom and
            // ComputeRoomMask read) never shows their own ids.
            VMArchitectureTools.DrawWall(VM.Context.Architecture, pos, length, direction, 14, 1, 2, true);
        }

        private void RestoreFixture()
        {
            if (FixWallsSnap == null) return;
            var arch = VM.Context.Architecture;
            Array.Copy(FixWallsSnap, arch.Walls[1], FixWallsSnap.Length);
            arch.WallsAt[1].Clear();
            arch.WallsAt[1].AddRange(FixWallsAtSnap);
            Array.Copy(FixFloorsSnap, arch.Floors[1], FixFloorsSnap.Length);
            arch.SetWall(1, 1, 2, arch.GetWall(1, 1, 2)); //trips WallsDirty (RealMode)
            arch.Tick();
            FixWallsSnap = null; FixWallsAtSnap = null; FixFloorsSnap = null;
            CrossBuilt = false;
        }

        private void PipBattery(uint mainRoom)
        {
            var gd = GameFacade.GraphicsDevice;
            var output = Path.Combine(FSO.Common.FSOEnvironment.UserDir, "ui-audit", "r244");
            Directory.CreateDirectory(output);
            using (var renderer = World.CreatePictureInPictureRenderer())
            {
                // same-floor: pipInputs drives a real computed mask. With the main
                // mask forced all-false, the legacy (null-inputs) render shows
                // walls up while the pipInputs render cuts the main room's walls
                // — proof the inputs actually reach the secondary view. The PIP
                // centers on the room itself so its walls are in frame.
                var roomCenter = BP.Rooms[(int)mainRoom].Bounds.Center;
                var center = new Vector3(roomCenter.X, roomCenter.Y, 0);
                var before = BP.Cutaway;
                var contentBefore = (bool[])before.Clone();

                var sameInputs = new CutawayViewInputs
                {
                    Floor = 1, Rotation = World.State.Rotation, Zoom = WorldZoom.Near, PreciseZoom = 1,
                    BufferBounds = new Rectangle(0, 0, (int)(gd.Viewport.Width / FSO.Common.FSOEnvironment.DPIScaleFactor), (int)(gd.Viewport.Height / FSO.Common.FSOEnvironment.DPIScaleFactor)),
                    HistoryRooms = new uint[] { mainRoom },
                    DynamicEnabled = true,
                    ScreenToTile = pos => World.EstTileAtPosWithScroll(new Vector2(pos.X, pos.Y)),
                };
                // law-true grounding: the pip inputs MUST compose a non-empty
                // mask (exactly the main room's) — otherwise a 0 pixel diff is
                // indistinguishable from "both walls up". Render at Far zoom in
                // a 300px crop: at Near the 200px crop spans ~1.5 tiles, which
                // cannot contain any wall of a room this size.
                var expectedSame = CutawayMatrix.ComposeDynamic(BP, sameInputs);
                int expectedTiles = Count(expectedSame);
                BP.Cutaway = new bool[before.Length];
                BP.Changes.SetFlag(BlueprintGlobalChanges.WALL_CUT_CHANGED);
                var legacyRef = BP.Cutaway;
                var tex1 = Snapshot(renderer.Render(center, 1, WorldZoom.Far, 300, 0));
                var tex2 = Snapshot(renderer.Render(center, 1, WorldZoom.Far, 300, 0, sameInputs));
                int sameDiff = PixelDiff(tex1, tex2);
                Require(tex2 != null && sameDiff > 0 && expectedTiles > 0, "pip-samefloor-pipinputs-drives-mask",
                    "render=" + (tex2 != null) + " pixelDiff=" + sameDiff + " expectedTiles=" + expectedTiles);
                // the committed main mask (reference + content) survived both renders
                Require(BP.Cutaway == legacyRef && SameMask(BP.Cutaway, new bool[before.Length]),
                    "pip-samefloor-main-mask-restored", "refRestored=" + (BP.Cutaway == legacyRef));
                using (var file = File.Create(Path.Combine(output, "uicutaway-pip-samefloor.png"))) tex2.SaveAsPng(file, 300, 300);
                tex1.Dispose(); tex2.Dispose();
                BP.Cutaway = before;
                BP.Changes.SetFlag(BlueprintGlobalChanges.WALL_CUT_CHANGED);

                // cross-floor: the draw mask is EXACTLY ComposeDynamic's output
                // at the PIP view — empty history ∪ live-gated floor-gated
                // person room ∪ r26-gated cursor rect — with NO all-rooms base
                // (native DrawPictureInPicture clears cCutawaySet entering AND
                // leaving the target floor, decode.md §1.2A). So with no live
                // person on the target floor the mask is the EMPTY composition:
                // the secondary view renders walls UP, pixel-identical to the
                // legacy null path — walls up cross-floor is native.
                if (BP.Stories >= 2)
                {
                    // the cross quarter doubles as the level-2 person fixture:
                    // its walls sit within ~4 tiles of its center, inside the
                    // Far/300 crop (the diagonal fixture is only a raw-partition
                    // probe — its halves collapse in the translated RoomMap)
                    var center2 = CrossBuilt
                        ? new Vector3(BP.Rooms[(int)CrossRooms[0]].Bounds.Center.X, BP.Rooms[(int)CrossRooms[0]].Bounds.Center.Y, 0)
                        : new Vector3(BP.Width / 2f, BP.Height / 2f, 0);
                    var texOpen = Snapshot(renderer.Render(center2, 2, WorldZoom.Far, 300, 0)); //legacy null path: all-false, walls up

                    // production PIP inputs under the battery's live-mode gate
                    // (LiveMode false here): empty history, no person — the
                    // empty composition, walls up
                    var emptyInputs = Game.PictureInPicture?.BuildPipCutawayInputs(2, WorldZoom.Far);
                    if (emptyInputs == null) Require(false, "pip-crossfloor-empty-inputs", "production PIP builder returned null");
                    else
                    {
                        var texEmpty = Snapshot(renderer.Render(center2, 2, WorldZoom.Far, 300, 0, emptyInputs));
                        int emptyDiff = PixelDiff(texEmpty, texOpen);
                        Require(emptyDiff == 0, "pip-crossfloor-no-live-person-walls-up", "pixelDiff=" + emptyDiff);
                        texEmpty.Dispose();
                    }

                    // live person on the target floor (a cross quarter to stand
                    // in): the mask is exactly that room's contribution — the
                    // history stays empty and an indoor person room keeps r26
                    // false, so the cursor rect cannot add on
                    if (CrossBuilt)
                    {
                        PipCrossFloorPersonLeg(renderer, center2, output);
                    }
                    else RequireVerbose(false, "pip-crossfloor-nonempty-mask", "level-2 cross fixture unavailable");

                    using (var file = File.Create(Path.Combine(output, "uicutaway-pip-crossfloor-open.png"))) texOpen.SaveAsPng(file, 300, 300);
                    texOpen.Dispose();
                    Require(BP.Cutaway == before && SameMask(BP.Cutaway, contentBefore), "pip-crossfloor-main-mask-restored");
                }
                else RequireVerbose(false, "pip-crossfloor-no-live-person-walls-up", "lot has a single story");

                // exception path: a throw after the cutaway swap still restores
                var roof = BP.RoofComp;
                var drawRoofs = World.State.DrawRoofs;
                bool threw = false;
                try
                {
                    World.State.DrawRoofs = true;
                    BP.RoofComp = null; //DrawPictureInPicture draws roofs after the cutaway swap
                    renderer.Render(center, 1, WorldZoom.Near, 200, 0, sameInputs);
                }
                catch (Exception) { threw = true; }
                finally
                {
                    BP.RoofComp = roof;
                    World.State.DrawRoofs = drawRoofs;
                }
                Require(threw && BP.Cutaway == before && SameMask(BP.Cutaway, contentBefore),
                    "pip-exception-restores", "threw=" + threw + " refRestored=" + (BP.Cutaway == before));
            }
        }

        private void PipCrossFloorPersonLeg(WorldPictureInPictureRenderer renderer, Vector3 center2, string output)
        {
            // place the real controlled sim inside a cross quarter on level 2
            // and flip to live mode so the production PIP builder resolves the
            // person branch (native CPState mode 2 gate, decode.md §5); both
            // are restored before returning (the leg is synchronous inside one
            // PreDraw pass, so no Update frame runs in between).
            var sim = Lot.ResolvePerson();
            if (sim == null || sim.Position == LotTilePos.OUT_OF_WORLD)
            {
                RequireVerbose(false, "pip-crossfloor-nonempty-mask", "no person fixture to place on level 2");
                return;
            }
            // a tile the VM room map assigns to the first cross quarter
            var targetRoom = CrossRooms[0];
            var quarter = BP.Rooms[(int)targetRoom].Bounds;
            Point stand = Point.Zero; bool found = false;
            for (int y = quarter.Top; y < quarter.Bottom && !found; y++)
                for (int x = quarter.Left; x < quarter.Right && !found; x++)
                {
                    if ((uint)VM.Context.GetRoomAt(LotTilePos.FromBigTile((short)x, (short)y, 2)) == targetRoom)
                    {
                        stand = new Point(x, y); found = true;
                    }
                }
            if (!found)
            {
                RequireVerbose(false, "pip-crossfloor-nonempty-mask", "no fixture tile resolves to the target quarter");
                return;
            }
            var posBefore = sim.Position;
            Lot.LiveMode = true; //native mode 2 (LIVE) gate: the person branch is live-only
            try
            {
                sim.Position = LotTilePos.FromBigTile((short)stand.X, (short)stand.Y, 2);
                var inputs = Game.PictureInPicture?.BuildPipCutawayInputs(2, WorldZoom.Far);
                if (inputs == null || !inputs.PersonRoomId.HasValue)
                {
                    Require(false, "pip-crossfloor-nonempty-mask", "production PIP builder resolved no live level-2 person");
                    return;
                }
                // expected mask = EXACTLY the person room's contribution: empty
                // history, indoor person room (r26 stays false, so the cursor
                // rect cannot add on even with a live cursor)
                var expected = CutawayMatrix.ComposeDynamic(BP, inputs);
                var band = Band(BP.Rooms[(int)inputs.PersonRoomId.Value].Bounds);
                int cut = 0; bool localized = true;
                for (int i = 0; i < expected.Length; i++)
                {
                    if (!expected[i]) continue;
                    cut++;
                    int x = i % BP.Width, y = i / BP.Width;
                    if (x < band.Left || x >= band.Right || y < band.Top || y >= band.Bottom) localized = false;
                }
                Require(cut > 0 && localized && inputs.PersonRoomId.Value == targetRoom,
                    "pip-crossfloor-person-room-localized",
                    "room=" + inputs.PersonRoomId.Value + " expected=" + targetRoom + " cutTiles=" + cut + " localized=" + localized);

                // render-level proof, isolated to the mask: render the same
                // warped world with an empty-composition input (walls up) and
                // with the person inputs. Both show the avatar itself, so the
                // diff against the pre-warp legacy frame would not isolate the
                // mask — this pair does.
                var noPerson = new CutawayViewInputs
                {
                    Floor = 2, Rotation = World.State.Rotation, Zoom = WorldZoom.Near, PreciseZoom = 1,
                    BufferBounds = inputs.BufferBounds,
                    HistoryRooms = new uint[0],
                    DynamicEnabled = true,
                    ScreenToTile = pos => World.EstTileAtPosWithScroll(new Vector2(pos.X, pos.Y)),
                };
                var texNoPerson = Snapshot(renderer.Render(center2, 2, WorldZoom.Far, 300, 0, noPerson));
                var texPerson = Snapshot(renderer.Render(center2, 2, WorldZoom.Far, 300, 0, inputs));
                int diff = PixelDiff(texPerson, texNoPerson);
                Require(diff > 0, "pip-crossfloor-nonempty-mask", "pixelDiff=" + diff);
                using (var file = File.Create(Path.Combine(output, "uicutaway-pip-crossfloor-person.png"))) texPerson.SaveAsPng(file, 300, 300);
                texNoPerson.Dispose(); texPerson.Dispose();
            }
            finally
            {
                sim.Position = posBefore;
                Lot.LiveMode = false;
            }
        }

        // WorldPictureInPictureRenderer.Render returns the SAME cached
        // RenderTarget2D for a given size (RenderTargetUsage.PreserveContents),
        // so two renders must be copied apart before they can be diffed.
        private Texture2D Snapshot(Texture2D t)
        {
            var data = new Color[t.Width * t.Height];
            t.GetData(data);
            var copy = new Texture2D(GameFacade.GraphicsDevice, t.Width, t.Height);
            copy.SetData(data);
            return copy;
        }

        private int PixelDiff(Texture2D a, Texture2D b)
        {
            if (a == null || b == null || a.Width != b.Width) return -1;
            var da = new Color[a.Width * a.Height];
            var db = new Color[b.Width * b.Height];
            a.GetData(da); b.GetData(db);
            int diff = 0;
            for (int i = 0; i < da.Length; i++) if (da[i] != db[i]) diff++;
            return diff;
        }

        private void GrabBattery(uint mainRoom)
        {
            // simvis-style world capture: with/without the room in the
            // composition, wall pixels change and the change localizes to the
            // room's wall band (sampled through the port's own projection).
            // The camera centers on the room so its walls are in frame.
            var roomCenter = BP.Rooms[(int)mainRoom].Bounds.Center;
            CenterCamera(new Vector2(roomCenter.X, roomCenter.Y));
            var gd = GameFacade.GraphicsDevice;
            int sw = gd.Viewport.Width, sh = gd.Viewport.Height;
            byte[] withRoom, withoutRoom;
            var before = BP.Cutaway;
            try
            {
                var withInputs = Inputs(1, World.State.Rotation, WorldZoom.Near, 1, new uint[] { mainRoom });
                BP.Cutaway = CutawayMatrix.ComposeDynamic(BP, withInputs);
                BP.Changes.SetFlag(BlueprintGlobalChanges.WALL_CUT_CHANGED);
                withRoom = GrabWorld(gd, sw, sh);

                var withoutInputs = Inputs(1, World.State.Rotation, WorldZoom.Near, 1, new uint[0]);
                BP.Cutaway = CutawayMatrix.ComposeDynamic(BP, withoutInputs);
                BP.Changes.SetFlag(BlueprintGlobalChanges.WALL_CUT_CHANGED);
                withoutRoom = GrabWorld(gd, sw, sh);
            }
            finally
            {
                BP.Cutaway = before;
                BP.Changes.SetFlag(BlueprintGlobalChanges.WALL_CUT_CHANGED);
            }
            int diff = 0;
            var changed = new List<Point>();
            var band = Band(BP.Rooms[(int)mainRoom].Bounds);
            for (int i = 0; i < withRoom.Length; i += 3)
            {
                if (withRoom[i] != withoutRoom[i] || withRoom[i + 1] != withoutRoom[i + 1] || withRoom[i + 2] != withoutRoom[i + 2])
                {
                    diff++;
                    int px = (i / 3) % sw, py = (i / 3) / sw;
                    changed.Add(new Point(px, py));
                }
            }
            // localize: sample every 4th changed pixel through the port's own
            // projection; >=75% of the sampled pixels must land in the room band
            int samples = 0, sampled = 0, inBand = 0;
            foreach (var p in changed)
            {
                if (samples % 4 == 0)
                {
                    sampled++;
                    var t = World.EstTileAtPosWithScroll(new Vector2(p.X, p.Y));
                    int tx = (int)t.X, ty = (int)t.Y;
                    if (tx >= band.Left && tx < band.Right && ty >= band.Top && ty < band.Bottom) inBand++;
                }
                samples++;
            }
            Require(diff > 0 && sampled > 0 && inBand * 4 >= sampled * 3,
                "world-grab-localized-diff", "changedPx=" + diff + " sampled=" + sampled + " inBand=" + inBand);

            var output = Path.Combine(FSO.Common.FSOEnvironment.UserDir, "ui-audit", "r244");
            Directory.CreateDirectory(output);
            using (var rtSave = new RenderTarget2D(gd, sw, sh, false, SurfaceFormat.Color, DepthFormat.None))
            {
                gd.SetRenderTarget(rtSave);
                gd.Clear(new Color(0x72, 0x72, 0x72, 0xFF));
                try { World.PreDraw(gd); } catch { }
                gd.SetRenderTarget(rtSave);
                try { World.Draw(gd); } catch { }
                gd.SetRenderTarget(null);
                using (var file = File.Create(Path.Combine(output, "uicutaway-world-cut.png"))) rtSave.SaveAsPng(file, sw, sh);
            }
        }

        private byte[] GrabWorld(GraphicsDevice gd, int sw, int sh)
        {
            using (var rt = FSO.Common.Utils.PPXDepthEngine.CreateRenderTarget(gd, 1, 0, SurfaceFormat.Color, sw, sh, DepthFormat.None))
            {
                gd.SetRenderTarget(rt);
                gd.Clear(new Color(0x72, 0x72, 0x72, 0xFF));
                try { World.PreDraw(gd); } catch { }
                gd.SetRenderTarget(rt);
                try { World.Draw(gd); } catch { }
                gd.SetRenderTarget(null);
                var px = new Color[sw * sh];
                rt.GetData(px);
                var b = new byte[sw * sh * 3];
                for (int i = 0; i < px.Length; i++)
                { b[i * 3] = px[i].R; b[i * 3 + 1] = px[i].G; b[i * 3 + 2] = px[i].B; }
                return b;
            }
        }

        // ---------- restore ----------

        private void RestoreAndVerify()
        {
            if (FixWallsSnap != null) RestoreFixture();
            // the floor-2 partition must equal the pre-fixture snapshot
            if (FixRoomSnap != null)
            {
                var now = BP.Rooms.Where(r => r.Floor == 1).Select(r => (uint)((r.RoomID << 1) | (r.IsOutside ? 1 : 0))).ToList();
                Require(now.SequenceEqual(FixRoomSnap), "lot-rooms-restored",
                    "before=" + FixRoomSnap.Count + " after=" + now.Count);
            }
            World.State.CenterTile = CenterTile;
            World.State.Level = Level;
            World.State.Rotation = RotBefore;
            World.State.Zoom = Zoom;
            World.State.ScrollAnchor = Anchor;
            World.State.PrepareCamera();
            VM.SpeedMultiplier = Speed;
            Lot.WallsMode = WallsMode;
            Lot.LiveMode = LiveMode;
            if (CutawayBefore != null)
            {
                BP.Cutaway = CutawayBefore; //same content the lot entered with
                BP.Changes.SetFlag(BlueprintGlobalChanges.WALL_CUT_CHANGED);
            }
            Require(VM.SpeedMultiplier == Speed && Lot.WallsMode == WallsMode, "settings-restored");
            GameLog.Write("AUTOTEST uicutaway evidence: " + Path.Combine(FSO.Common.FSOEnvironment.UserDir, "ui-audit", "r244"));
        }

        public void Dispose()
        {
            try { if (FixWallsSnap != null) RestoreFixture(); } catch { }
        }
    }
}
