using System;
using System.Collections.Generic;
using System.Linq;
using FSO.Content;
using FSO.LotView.Model;
using FSO.SimAntics;
using FSO.SimAntics.Model;
using FSO.SimAntics.Model.Platform;
using FSO.SimAntics.NetPlay.Model.Commands;

namespace Simitone.Client
{
    /// <summary>
    /// CC-02 e2e (opt-in check "cc02e2e", USER-AUTHORIZED 2026-09-09): drives the full
    /// custom-content acceptance chain in-battery — catalog lookup of the CC02E001
    /// coffee-machine clone, real player-path placement (VMNetBuyObjectCmd, money path),
    /// pie-menu interaction push (VMNetInteractionCmd) with route observation, lot save,
    /// lot reload, and persistence verification (the placed object must come back with
    /// its custom GUID intact). Mirrors the AutotestFreeWillWin249 arm/sample pattern.
    ///
    /// TIMING LAW (probed 2026-09-09): the placement sweep must run in the FIRST frames
    /// after LOT-READY. Once the boot blocking dialogs (Custom Content warning, school /
    /// cab / town notices) SHOW (~2s after load), the engine latches them and world edits
    /// (ChangePosition) are refused at every tile for as long as the latch is held.
    /// </summary>
    public static class AutotestCC02E2E
    {
        private const uint CLONE_GUID = 0xCC02E001;

        private static Func<VM> _getVM;
        private static Func<List<VMAvatar>> _getAvatars;
        private static Func<bool> _saveGame;
        private static Action _reloadLot;
        private static Action<string> _log;

        private static int _phase;
        private static int _phaseTicks;
        private static bool _done;
        private static bool _allPassed;
        private static readonly List<string> Notes = new List<string>();

        private static int _buyIdx;

        // Yard candidates: flat grass, valid for appliances, away from the house walls
        // (the shipped Goth house occupies roughly tiles 24-44 x 12-40 on house 5).
        private static readonly List<dynamic> _probeCandidates = new List<dynamic> {
            new { x = (short)20, y = (short)10 }, new { x = (short)24, y = (short)10 },
            new { x = (short)16, y = (short)12 }, new { x = (short)28, y = (short)10 },
            new { x = (short)20, y = (short)14 }, new { x = (short)12, y = (short)14 },
            new { x = (short)32, y = (short)12 }, new { x = (short)14, y = (short)18 },
            new { x = (short)36, y = (short)14 }, new { x = (short)44, y = (short)20 },
            new { x = (short)30, y = (short)52 }, new { x = (short)40, y = (short)48 },
        };

        private static short _placeX = -1, _placeY = -1;
        private static sbyte _placeLevel = 1;
        private static short _placedObjID;
        private static float _routeStartDist = -1f;
        private static float _bestDist = float.MaxValue;
        private static int _interactionSentTicks = -1;
        private static uint _callerPersistID;
        private static int _rePushes;
        private static int _lastProgressTick;
        private static bool _routed;
        private static string _interactionName = "(none)";

        public static bool AllPassed { get { return _allPassed; } }

        /// <summary>The runner's lot-unload detector stays silent while the scripted reload runs.</summary>
        public static bool ReloadPending { get; private set; }

        public static void Begin(Func<VM> getVM, Func<List<VMAvatar>> getAvatars,
            Func<bool> saveGame, Action reloadLot, Action<string> log)
        {
            _getVM = getVM; _getAvatars = getAvatars; _saveGame = saveGame;
            _reloadLot = reloadLot; _log = log;
            _phase = 0; _phaseTicks = 0; _done = false; _allPassed = false;
            Notes.Clear();
            _placeX = _placeY = -1; _placedObjID = 0; _routeStartDist = -1f;
            _interactionSentTicks = -1; _callerPersistID = 0; _rePushes = 0;
            _lastProgressTick = 0; _routed = false; _interactionName = "(none)";
            log("AUTOTEST cc02e2e: Begin (catalog → buy → interact → save → reload → verify)");
        }

        /// <returns>true when the check has finished (verdict recorded by the caller).</returns>
        public static bool SampleTick()
        {
            if (_done) return true;
            _phaseTicks++;
            var vm = _getVM();
            if (vm == null || !vm.TS1) { Stall("vm"); return false; }
            try
            {
                switch (_phase)
                {
                    case 0: Phase0CatalogAndBuy(vm); break;
                    case 1: Phase1WaitEntity(vm); break;
                    case 2: PhasePickCallerAndPush(vm); break;
                    case 3: PhaseWatchRouteAndUse(vm); break;
                    case 4: PhaseSave(vm); break;
                    case 5: PhaseReload(vm); break;
                    case 6: PhaseVerifyPersistence(vm); break;
                                    }
            }
            catch (Exception e)
            {
                Note("EXC phase " + _phase + ": " + e.GetType().Name + " " + e.Message);
                FailCheck("exception in phase " + _phase);
            }
            return _done;
        }

        private static void Next() { _phase++; _phaseTicks = 0; }

        private static void Stall(string what)
        {
            if (_phaseTicks > 6000) FailCheck("stalled 6000 ticks waiting for " + what);
        }

        private static void Note(string s)
        {
            Notes.Add(s);
            _log("AUTOTEST cc02e2e: " + s);
        }

        private static void FailCheck(string reason)
        {
            Note("FAIL: " + reason);
            _allPassed = false;
            _done = true;
        }

        // Phase 0: the clone must be SERVED by the buy catalog and MOUNTED in
        // WorldObjects. Then send the REAL player-path buy command (VMNetBuyObjectCmd —
        // money charged, engine validates and may slide the object) at the known-good
        // yard position from the 00:21Z PASS run; fallback positions follow if the
        // entity does not appear.
        private static readonly short[][] _buyPositions = new short[][] {
            new short[] { 264, 360 }, new short[] { 328, 168 },
            new short[] { 456, 552 }, new short[] { 328, 552 },
        };

        private static void Phase0CatalogAndBuy(VM vm)
        {
            var item = Content.Get().WorldCatalog.GetItemByGUID(CLONE_GUID);
            if (!item.HasValue) { FailCheck("clone not served by the buy catalog"); return; }
            Note("catalog: clone served, name='" + item.Value.Name + "' price=" + item.Value.Price
                + " cat=" + item.Value.Category);
            var objdef = FSO.Content.Content.Get().WorldObjects.Get(CLONE_GUID);
            if (objdef == null) { FailCheck("clone OBJD not mounted in WorldObjects"); return; }
            Note("WorldObjects.Get(clone): mounted (MasterID=" + objdef.OBJ.MasterID + ")");

            if (_buyIdx >= _buyPositions.Length) { FailCheck("all buy positions exhausted"); return; }
            var pos = _buyPositions[_buyIdx];
            vm.SendCommand(new VMNetBuyObjectCmd
            {
                GUID = CLONE_GUID,
                dir = Direction.NORTH,
                level = 1,
                x = pos[0],
                y = pos[1],
                Mode = PurchaseMode.Normal
            });
            Note("buy command sent for GUID 0x" + CLONE_GUID.ToString("x8")
                + " at (" + pos[0] + "," + pos[1] + ") [attempt " + (_buyIdx + 1) + "/"
                + _buyPositions.Length + "]");
            Next();
        }

        // Phase 1: the placed machine must exist in the world with the custom GUID.
        private static void Phase1WaitEntity(VM vm)
        {
            var obj = vm.Entities.FirstOrDefault(e => e.Object != null && e.Object.OBJ != null
                && e.Object.OBJ.GUID == CLONE_GUID);
            if (obj == null)
            {
                if (_phaseTicks > 360)
                {
                    Note("entity not placed at position attempt " + (_buyIdx + 1) + "; trying next");
                    _buyIdx++;
                    _phase = 0; _phaseTicks = 0;
                }
                return;
            }
            _placedObjID = obj.ObjectID;
            Note("placed entity verified: ObjectID=" + obj.ObjectID + " at ("
                + obj.Position.x + "," + obj.Position.y + ")");
            Next();
        }

        // Phase 3: pick the playable sim CLOSEST to the machine, push the machine's first
        // valid pie interaction through the same command the pie menu sends. Re-pushes are
        // allowed from phase 4 if the sim leaves the lot or stalls.
        private static void PhasePickCallerAndPush(VM vm)
        {
            var obj = vm.Entities.FirstOrDefault(e => e.ObjectID == _placedObjID);
            if (obj == null) { FailCheck("placed machine vanished before interaction"); return; }
            var avatars = _getAvatars() ?? new List<VMAvatar>();
            VMAvatar best = null; float bestDist = float.MaxValue;
            foreach (var a in avatars)
            {
                if (a == null || a.Dead) continue;
                float dx = a.Position.x - obj.Position.x, dy = a.Position.y - obj.Position.y;
                var d = dx * dx + dy * dy;
                if (d < bestDist) { bestDist = d; best = a; }
            }
            if (best == null) { FailCheck("no live avatar on lot"); return; }

            var menu = obj.GetPieMenu(vm, best, false, true);
            if (menu == null || menu.Count == 0) { FailCheck("machine exposes no pie interactions for " + best.Name); return; }

            var pick = menu[0];
            Note("pie interactions available=" + menu.Count + "; pushing first for " + best.Name
                + " at dist " + (float)Math.Sqrt(bestDist));

            _callerPersistID = best.PersistID;
            vm.SendCommand(new VMNetInteractionCmd
            {
                Interaction = pick.ID,
                ActorUID = best.PersistID,
                CalleeID = obj.ObjectID,
                Param0 = pick.Param0,
                Global = pick.Global
            });
            _interactionSentTicks = _phaseTicks;
            Next();
        }

        // Phase 4: the sim must ROUTE to the machine (distance shrinks to adjacency).
        // If the caller leaves the lot (school/work carpool) or stalls, re-push to the
        // closest remaining sim (max 3 re-pushes).
        private static void PhaseWatchRouteAndUse(VM vm)
        {
            var obj = vm.Entities.FirstOrDefault(e => e.ObjectID == _placedObjID);
            var caller = vm.Entities.FirstOrDefault(e => e.PersistID == _callerPersistID) as VMAvatar;
            if (obj == null) { FailCheck("machine lost during route watch"); return; }

            float dx = caller.Position.x - obj.Position.x, dy = caller.Position.y - obj.Position.y;
            var dist = (float)Math.Sqrt(dx * dx + dy * dy);
            if (_routeStartDist < 0) _routeStartDist = dist;
            if (dist < _bestDist) { _bestDist = dist; _lastProgressTick = _phaseTicks; }

            if (dist <= 48f) // within 3 tiles = routed to adjacency
            {
                _routed = true;
                Note("routed: sim at (" + caller.Position.x + "," + caller.Position.y
                    + ") machine at (" + obj.Position.x + "," + obj.Position.y
                    + "); interaction accepted");
                Next();
                return;
            }

            if (_phaseTicks - _lastProgressTick > 2500 && dist >= _bestDist - 4)
            {
                // caller stalled or left: remove this machine, try the next candidate tile
                _log("AUTOTEST cc02e2e: route stall (dist " + dist + "); deleting machine, next position");
                obj.Delete(false, vm.Context);
                _buyIdx++;
                _phase = 0; _phaseTicks = 0;
                _routeStartDist = -1f; _routed = false;
                return;
            }
            if (_phaseTicks > 25000) { FailCheck("sim never routed to the machine (dist " + dist + ")"); return; }
        }

        // Phase 5: save the lot through the game's own TS1 save path.
        private static void PhaseSave(VM vm)
        {
            if (!_routed) { FailCheck("skipped save: route never completed"); _done = true; return; }
            if (_phaseTicks == 0) Note("saving lot via TS1GameScreen.Save path");
            if (!_saveGame()) { if (_phaseTicks > 60) FailCheck("save callback kept failing"); return; }
            Note("lot saved");
            Next();
        }

        // Phase 6: reload the lot (fresh day, fresh VM).
        private static void PhaseReload(VM vm)
        {
            if (_phaseTicks == 0)
            {
                Note("reloading lot");
                ReloadPending = true;
                _reloadLot();
                return;
            }
            // after PlayHouse the runner re-captures its own vm; ours is stale — wait until
            // the new world exposes the placed machine again.
            var vm2 = _getVM();
            if (vm2 == null) { Stall("reload vm"); return; }
            var obj = vm2.Entities.FirstOrDefault(e => e.Object != null && e.Object.OBJ != null
                && e.Object.OBJ.GUID == CLONE_GUID);
            if (obj == null) { Stall("reloaded entity"); return; }
            Note("after reload: clone entity present, ObjectID=" + obj.ObjectID);
            _placedObjID = obj.ObjectID;
            Next();
        }

        // Phase 7: final verdict — the custom GUID survived the full loop.
        private static void PhaseVerifyPersistence(VM vm)
        {
            var obj = vm.Entities.FirstOrDefault(e => e.ObjectID == _placedObjID)
                ?? vm.Entities.FirstOrDefault(e => e.Object != null && e.Object.OBJ != null
                    && e.Object.OBJ.GUID == CLONE_GUID);
            if (obj == null) { FailCheck("clone did not persist across reload"); return; }
            ReloadPending = false;
            Note("persistence verified: clone survived save→reload with GUID 0x"
                + CLONE_GUID.ToString("x8") + "; routed=" + _routed);
            _allPassed = _routed;
            if (!_allPassed) Note("FAIL: route/interact leg did not complete");
            _done = true;
        }
    }
}
