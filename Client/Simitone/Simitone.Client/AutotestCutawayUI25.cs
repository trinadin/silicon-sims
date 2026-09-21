using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FSO.Client;
using FSO.Client.UI.Framework;
using FSO.Common.Rendering.Framework.Model;
using FSO.LotView;
using FSO.LotView.Components;
using FSO.LotView.Model;
using FSO.LotView.Utils;
using FSO.SimAntics;
using FSO.SimAntics.Model;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simitone.Client.UI.Panels;
using Simitone.Client.UI.Screens;

namespace Simitone.Client
{
    // UI-25 'uicutaway25' (opt-in, additive) — the cutaway-orphan implementation
    // pins, driven in the real graphics loop on the loaded lot. Decode contract:
    // tools/iff-dump/r259-cutaway-orphans/decode.md. Focused run:
    // -autotest-opts "corpus,lot,uicutaway25".
    //
    // Pins (each names its decode item):
    //   1. rotation LUT law (pure reflection unit over the engine's RotMap/
    //      InvRotMap, 64x64x4): native BuildRotationLookup page law
    //      rot1 (63-y,x), rot2 (63-x,63-y), rot3 (y,63-x) — i.e. the port
    //      family translated by (63,0)/(63,63)/(0,63) — and the inverse-page
    //      pairing (inverse of rot r = rotation 4-r) mirrored by InvRotMap.
    //   2. cMoveTool AdjustCutawayForTool leg: the engine AdjustForDrag marks
    //      exactly the dragged footprint gated to [1..size-2], and the
    //      ComposeDynamic hook actually marks it (the production wiring shape).
    //   4. PIP parity: same-floor PIP builder keeps the hover history (native
    //      floor-equal branch shares the standing matrix), cross-floor PIP
    //      builder wipes it (native ENTER 0x1c1068 / RESTORE 0x1c161c clear)
    //      with empty HistoryRooms, and its cursor picker resolves through the
    //      TARGET floor (World.EstTileAtPosWithScroll(pos, targetLevel)).
    //
    // The PreDraw same-floor decision itself (null inputs on
    // Target.Position.Level == World.State.Level) is render-coupled and not
    // driven here; its engine half (the renderer null path draws the main
    // matrix unchanged) is exercised by the uicutaway battery's legacy
    // renders. Ctor snapshots and Dispose restores every touched setting
    // (AutotestCutaway244 pattern). No architecture fixtures: the history
    // battery uses an existing ground indoor room.
    internal sealed class AutotestCutawayUI25 : UIContainer, IDisposable
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
        private readonly bool[] CutawayBefore;
        private readonly bool LiveMode;

        private readonly List<string> Failures = new List<string>();
        private int Phase;
        internal bool Ready { get; private set; }
        internal bool Passed => Ready && Failures.Count == 0;
        internal string Diagnostics => Failures.Count == 0
            ? "rotation-LUT law 64x64x4, inverse-page pairing, drag-footprint engine pin, drag bounds gate, tool-hook compose marks, PIP same-floor keeps history, PIP cross-floor clears, PIP cross-floor empty history, PIP picker target-floor; state restored"
            : string.Join(", ", Failures);

        internal AutotestCutawayUI25(TS1GameScreen game)
        {
            Game = game;
            World = game.LotControl.World;
            Lot = game.LotControl;
            VM = game.vm;
            BP = VM.Context.Blueprint;
            Speed = VM.SpeedMultiplier;
            WallsMode = Lot.WallsMode;
            CenterTile = World.State.CenterTile;
            Level = World.State.Level;
            RotBefore = World.State.Rotation;
            Zoom = World.State.Zoom;
            Anchor = World.State.ScrollAnchor;
            CutawayBefore = BP.Cutaway != null ? (bool[])BP.Cutaway.Clone() : null;
            LiveMode = Lot.LiveMode;
            VM.SpeedMultiplier = 0;
            Lot.WallsMode = 1;      //dynamic mode: the composition the pins drive
            Lot.LiveMode = false;   //keep the production person branch quiet
        }

        private void Require(bool value, string name, string detail = "")
        {
            GameLog.Write("AUTOTEST uicutaway25 " + name + ": " + (value ? "PASS" : "FAIL") + (detail.Length > 0 ? " " + detail : ""));
            if (!value) Failures.Add(name);
        }

        public override void PreDraw(UISpriteBatch batch)
        {
            if (Ready || !World.PictureInPictureReady || BP == null) return;
            try
            {
                if (Phase++ == 0) RunBattery();
                else
                {
                    RestoreAndVerify();
                    Ready = true;
                }
            }
            catch (Exception e)
            {
                GameLog.Write("AUTOTEST uicutaway25 EXC " + e);
                Failures.Add("exception:" + e.GetType().Name);
                Ready = true;
            }
        }

        private void RunBattery()
        {
            // stable 2D view: floor 1, near zoom, TopLeft — the port's identity
            // rotation, so the rotation pin's law table reads directly
            World.State.ScrollAnchor = null;
            World.State.Level = 1;
            World.State.Zoom = WorldZoom.Near;
            World.State.Rotation = WorldRotation.TopLeft;
            World.State.PrepareCamera();
            Lot.CutawayHistoryReset();
            Lot.CutMaskCache.Invalidate();

            // ---- item 1: rotation LUT law (pure unit, 64x64x4) ----
            RotationLawBattery();

            // ---- item 2: cMoveTool drag-footprint leg ----
            ToolHeldBattery();

            // ---- item 4: PIP history + picker parity ----
            PipParityBattery();
        }

        // ---------- item 1: the decoded BuildRotationLookup law ----------

        private void RotationLawBattery()
        {
            // The engine RotMap/InvRotMap are private (same family as the
            // native LUT, port frame); reflection keeps this pin additive.
            MethodInfo rotMap, invRotMap;
            try
            {
                var flags = BindingFlags.NonPublic | BindingFlags.Static;
                rotMap = typeof(CutawayMatrix).GetMethod("RotMap", flags);
                invRotMap = typeof(CutawayMatrix).GetMethod("InvRotMap", flags);
            }
            catch (Exception ex)
            {
                Require(false, "rotation-lut-reflection", ex.Message);
                return;
            }
            if (rotMap == null || invRotMap == null)
            {
                Require(false, "rotation-lut-reflection", "RotMap/InvRotMap not found");
                return;
            }

            // native law (decode.md item 1), expressed port-frame: native =
            // port + translation, port is total (no 0xFF domain; the native
            // out-of-domain entry has no port equivalent).
            var law = new Dictionary<WorldRotation, Func<int, int, Point>>
            {
                { WorldRotation.TopLeft,     (x, y) => new Point(x, y) },       //rot 0
                { WorldRotation.TopRight,    (x, y) => new Point(-y, x) },      //rot 1
                { WorldRotation.BottomRight, (x, y) => new Point(-x, -y) },     //rot 2
                { WorldRotation.BottomLeft,  (x, y) => new Point(y, -x) },      //rot 3
            };
            var translation = new Dictionary<WorldRotation, Point>
            {
                { WorldRotation.TopLeft,     new Point(0, 0) },
                { WorldRotation.TopRight,    new Point(63, 0) },
                { WorldRotation.BottomRight, new Point(63, 63) },
                { WorldRotation.BottomLeft,  new Point(0, 63) },
            };

            bool forwardOk = true, inverseOk = true, pairingOk = true;
            string firstBad = "";
            foreach (var pair in law)
            {
                var rot = pair.Key;
                for (int x = 0; x < 64; x++)
                {
                    for (int y = 0; y < 64; y++)
                    {
                        var got = (Point)rotMap.Invoke(null, new object[] { rot, x, y });
                        var want = pair.Value(x, y);
                        if (got.X != want.X || got.Y != want.Y)
                        {
                            forwardOk = false;
                            if (firstBad.Length == 0) firstBad = rot + "(" + x + "," + y + ")->" + got;
                        }
                        // native inverse page ((4-rot)&3)-1 = the map of rotation
                        // 4-r: the port mirrors it with InvRotMap — check both
                        // directions of the pairing on every cell.
                        var back = (Point)invRotMap.Invoke(null, new object[] { rot, got.X, got.Y });
                        if (back.X != x || back.Y != y)
                        {
                            inverseOk = false;
                            if (firstBad.Length == 0) firstBad = "inv" + rot + "(" + got.X + "," + got.Y + ")->" + back;
                        }
                        var invIn = (Point)invRotMap.Invoke(null, new object[] { rot, x, y });
                        var forth = (Point)rotMap.Invoke(null, new object[] { rot, invIn.X, invIn.Y });
                        if (forth.X != x || forth.Y != y) pairingOk = false;
                    }
                }
            }
            Require(forwardOk, "rotation-lut-law-64x64x4", firstBad);
            Require(inverseOk && pairingOk, "rotation-inverse-page-pairing", firstBad);

            // the decoded native shape itself: translate the port result into
            // native [0..63] coordinates and re-check the printed law for one
            // representative cell (x=10,y=20) per rotation:
            // rot1 (63-y,x)=(43,10), rot2 (63-x,63-y)=(53,43), rot3 (y,63-x)=(20,53)
            bool spotOk = true;
            var rot1 = (Point)rotMap.Invoke(null, new object[] { WorldRotation.TopRight, 10, 20 });
            spotOk &= (rot1.X + 63 == 63 - 20) && (rot1.Y == 10);
            var rot2 = (Point)rotMap.Invoke(null, new object[] { WorldRotation.BottomRight, 10, 20 });
            spotOk &= (rot2.X + 63 == 63 - 10) && (rot2.Y + 63 == 63 - 20);
            var rot3 = (Point)rotMap.Invoke(null, new object[] { WorldRotation.BottomLeft, 10, 20 });
            spotOk &= (rot3.X == 20) && (rot3.Y + 63 == 63 - 10);
            Require(spotOk, "rotation-law-spot-native", "");
        }

        // ---------- item 2: the cMoveTool drag-footprint leg ----------

        private void ToolHeldBattery()
        {
            // engine pin: AdjustForDrag marks EXACTLY the interior tiles of the
            // dragged footprint — edge tiles ([0] and [size-1]) are out of the
            // native [1..size-2] domain and must stay clear.
            var mask = new bool[BP.Width * BP.Height];
            var drag = new[]
            {
                new Point(5, 5), new Point(6, 5), new Point(5, 6), new Point(6, 6),
                new Point(0, 3), new Point(BP.Width - 1, 3), new Point(3, 0), new Point(3, BP.Height - 1),
            };
            CutawayMatrix.AdjustForDrag(BP, mask, drag);
            var expected = new HashSet<int> { 5 * BP.Width + 5, 5 * BP.Width + 6, 6 * BP.Width + 5, 6 * BP.Width + 6 };
            var actual = new HashSet<int>();
            for (int i = 0; i < mask.Length; i++) if (mask[i]) actual.Add(i);
            Require(actual.SetEquals(expected), "drag-footprint-engine-pin",
                "marked=" + actual.Count + " expected=" + expected.Count);

            // degenerate guards: null/wrong-length mask must no-op without throwing
            bool guarded = true;
            try
            {
                CutawayMatrix.AdjustForDrag(BP, null, drag);
                CutawayMatrix.AdjustForDrag(null, new bool[BP.Width * BP.Height], drag);
                CutawayMatrix.AdjustForDrag(BP, new bool[BP.Width * BP.Height + 1], drag);
            }
            catch { guarded = false; }
            Require(guarded, "drag-footprint-guards", "");

            // compose integration: the wired hook marks the footprint into the
            // real composition (the shape UILotControl.BuildCutawayInputs
            // assigns from ObjectHolder.Holding.CursorTiles). A deep cursor is
            // required: the hook fires only with the corner (cursorTile-4) in
            // world bounds, like the native dispatch gate.
            if (!TryScreenForTile(new Vector2(10, 10), out var cursor))
            {
                Require(false, "tool-hook-compose-marks", "no screen point maps to tile (10,10)");
                return;
            }
            var toolTiles = new[] { new Point(10, 10), new Point(11, 10), new Point(10, 11), new Point(11, 11) };
            var withTool = CutawayMatrix.ComposeDynamic(BP, Inputs(cursor,
                (corner, m) => CutawayMatrix.AdjustForDrag(BP, m, toolTiles)));
            var withoutTool = CutawayMatrix.ComposeDynamic(BP, Inputs(cursor, null));
            var toolSet = new HashSet<int>(toolTiles.Select(t => t.Y * BP.Width + t.X));
            var gotWith = new HashSet<int>();
            var gotWithout = new HashSet<int>();
            for (int i = 0; i < withTool.Length; i++)
            {
                if (withTool[i]) gotWith.Add(i);
                if (withoutTool[i]) gotWithout.Add(i);
            }
            Require(gotWith.SetEquals(toolSet) && gotWithout.Count == 0, "tool-hook-compose-marks",
                "with=" + gotWith.Count + " without=" + gotWithout.Count);
        }

        private CutawayViewInputs Inputs(Point? cursor, Action<Point, bool[]> tool)
        {
            var viewport = GameFacade.GraphicsDevice.Viewport;
            float dpi = FSO.Common.FSOEnvironment.DPIScaleFactor;
            return new CutawayViewInputs
            {
                Floor = 1,
                Rotation = WorldRotation.TopLeft,
                Zoom = WorldZoom.Near,
                PreciseZoom = 1,
                BufferBounds = new Rectangle(0, 0, (int)(viewport.Width / dpi), (int)(viewport.Height / dpi)),
                HistoryRooms = new uint[0],
                DynamicEnabled = true,
                CursorScreenPos = cursor,
                CursorSuppressed = false,
                ScreenToTile = pos => World.EstTileAtPosWithScroll(new Vector2(pos.X, pos.Y)),
                AdjustCutawayForTool = tool,
            };
        }

        // ---------- item 4: PIP history + picker parity ----------

        private void PipParityBattery()
        {
            if (Game.PictureInPicture == null)
            {
                Require(false, "pip25-fixture", "no UIOriginalPictureInPicture mounted");
                return;
            }

            // drive the production history over an existing ground indoor room
            var indoor = BP.Rooms.Where(r => r.Floor == 0 && !r.IsOutside && r.Area >= 9)
                .OrderByDescending(r => r.Area).ToList();
            Require(indoor.Count > 0, "pip25-fixture-indoor-room", "groundIndoor=" + indoor.Count);
            if (indoor.Count == 0) return;
            uint roomId = (uint)BP.Rooms.IndexOf(indoor[0]);
            var bounds = indoor[0].Bounds;
            CenterCamera(new Vector2(bounds.Center.X, bounds.Center.Y));
            if (!TryDeepTile(roomId, 1, out var tile) || !TryScreenForTile(tile, out var screen))
            {
                Require(false, "pip25-history-fixture", "no deep tile/screen in room " + roomId);
                return;
            }
            Lot.CutawayHistoryReset();
            Lot.UpdateCutawayDynamic(screen, false); //production driver: inserts cursor + behind rooms
            Require(Lot.CutawayHistory.Count > 0 && Lot.CutawayHistory.Contains(roomId),
                "pip25-history-driver-inserts", "history=[" + string.Join(",", Lot.CutawayHistory) + "] room=" + roomId);

            // same-floor PIP builder: native floor-equal branch (0x1c1050)
            // shares the standing matrix — the history must survive untouched
            var sameInputs = Game.PictureInPicture.BuildPipCutawayInputs(1, WorldZoom.Far);
            Require(sameInputs != null && Lot.CutawayHistory.Count > 0, "pip-samefloor-keeps-history",
                "history=" + Lot.CutawayHistory.Count);

            if (BP.Stories >= 2)
            {
                if (Lot.CutawayHistory.Count == 0) Lot.UpdateCutawayDynamic(screen, false); //re-arm
                // cross-floor PIP builder: native clears the live history on
                // ENTER (0x1c1068) AND RESTORE (0x1c161c) — the production
                // ClearCutHistory must have wiped it, and the PIP inputs
                // compose with an empty history
                var crossInputs = Game.PictureInPicture.BuildPipCutawayInputs(2, WorldZoom.Far);
                Require(crossInputs != null && Lot.CutawayHistory.Count == 0, "pip-crossfloor-clears-history",
                    "history=" + (crossInputs != null ? Lot.CutawayHistory.Count : -1));
                Require(crossInputs == null || (crossInputs.HistoryRooms != null && crossInputs.HistoryRooms.Count == 0),
                    "pip-crossfloor-empty-pip-history", "");

                // picker: the PIP cursor tile must resolve through the TARGET
                // floor (item 4c). If no sampled position discriminates the
                // level argument on this lot, the equality pin still holds and
                // the residual is logged.
                if (crossInputs != null)
                {
                    bool pickerOk = true;
                    int discriminating = 0;
                    var samples = new List<Point>
                    {
                        new Point(10, 10),
                        new Point(120, 90),
                        new Point(LogicalBuffer().Width / 2, LogicalBuffer().Height / 2),
                    };
                    foreach (var p in samples)
                    {
                        var via = crossInputs.ScreenToTile(p);
                        var wantTarget = World.EstTileAtPosWithScroll(p.ToVector2(), 2);
                        var wantMain = World.EstTileAtPosWithScroll(p.ToVector2()); //default = State.Level
                        if ((int)via.X != (int)wantTarget.X || (int)via.Y != (int)wantTarget.Y) pickerOk = false;
                        if ((int)wantTarget.X != (int)wantMain.X || (int)wantTarget.Y != (int)wantMain.Y) discriminating++;
                    }
                    Require(pickerOk, "pip-picker-target-floor", "samples=" + samples.Count + " discriminating=" + discriminating);
                    if (discriminating == 0)
                        GameLog.Write("AUTOTEST uicutaway25 picker residual: no sampled position discriminates floor 1 vs 2 picking on this lot; equality pin carried the law");
                }
            }
            else
            {
                GameLog.Write("AUTOTEST uicutaway25 cross-floor pins: lot has a single story — cross-floor builder/picker pins not drivable");
            }
        }

        // ---------- helpers (AutotestCutaway244 pattern, trimmed) ----------

        private void CenterCamera(Vector2 tile)
        {
            World.State.CenterTile = tile;
            World.State.PrepareCamera();
        }

        private Rectangle LogicalBuffer()
        {
            var viewport = GameFacade.GraphicsDevice.Viewport;
            float dpi = FSO.Common.FSOEnvironment.DPIScaleFactor;
            return new Rectangle(0, 0, (int)(viewport.Width / dpi), (int)(viewport.Height / dpi));
        }

        // a screen position whose EstTileAtPosWithScroll lands exactly on the
        // target tile (self-calibrating: inverts through the same projection
        // the production wiring passes)
        private bool TryScreenForTile(Vector2 tile, out Point screen)
        {
            var b = LogicalBuffer();
            for (int y = -256; y < b.Height + 256; y += 8)
            {
                for (int x = -256; x < b.Width + 256; x += 8)
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

        private uint TileRoom(int x, int y, sbyte level)
        {
            return (ushort)BP.RoomMap[level - 1][y * BP.Width + x];
        }

        // a tile whose whole 3x3 neighborhood stays in the room, so the
        // production behind-room probe walk cannot leave it
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
            return bestScore >= 9;
        }

        // ---------- restore ----------

        private void RestoreAndVerify()
        {
            Lot.CutawayHistoryReset();
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
                BP.Cutaway = CutawayBefore;
                BP.Changes.SetFlag(BlueprintGlobalChanges.WALL_CUT_CHANGED);
            }
            Require(VM.SpeedMultiplier == Speed && Lot.WallsMode == WallsMode, "settings-restored");
        }

        public void Dispose() { }
    }
}
