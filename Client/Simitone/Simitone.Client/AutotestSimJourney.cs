using FSO.Content;
using FSO.Files.Formats.IFF.Chunks;
using FSO.SimAntics;
using FSO.SimAntics.Engine;
using FSO.SimAntics.Model;
using FSO.SimAntics.NetPlay.Model.Commands;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simitone.Client
{
    /// <summary>
    /// SIM-02 'simjrn' (opt-in, focused): live object-use journey battery through the
    /// REAL command paths, on the House-5 fixture. R250's static winner/routing law is
    /// respected (not re-derived); this battery exercises the genuinely-live journeys:
    ///   J1 object use: pie cmd (VMNetInteractionCmd) -> queue -> route -> multitile bed
    ///      Sleep executes -> Energy motive delta (skill/motive effect path).
    ///   J2 interrupt/cancel: a queued interaction cancelled via VMNetInteractionCancelCmd
    ///      (queue removal), then the ACTIVE sleep cancelled (interrupt -> thread idle).
    ///   J3 routing failure: push an interaction, then delete the target object; assert
    ///      the R250 GoToRoutingSlot-FALSE -> DROPPED-action law live (queue drains, no
    ///      exception, worker idle).
    ///   J4 disabled rejection: a command against a Disabled VMGameObject returns false.
    /// Run focused: -autotest 5 -autotest-opts "lot,simjrn"  (extends the soak ~12h sim).
    /// </summary>
    internal static class AutotestSimJourney
    {
        private static int _state;                 // 0 setup, 1 J1 wait, 2 J2 issue/settle, 3 J3 route-fail, 4 done
        private static int _waitStart = -1;        // sim-minute at which the current wait began
        private static int _clockLast = -1;
        private static VMEntity _bed;
        private static VMEntity _j3Target;
        private static VMAvatar _worker;
        private static int _energyBase = int.MinValue;
        private static bool _j1Pushed, _j1Routed, _j1Energy, _j2Cancel, _j2ActiveCancel, _j3Dropped, _j4Rejected;
        private static bool _j2Issued, _j2CancelCmd, _j2ActiveCmd;
        private static ushort _j2QueuedUid, _j2ActiveUid;
        private static string _fail = null;

        private static Action<string> _log = _ => { };
        private static void Fail(string why) { if (_fail == null) _fail = why; _log("AUTOTEST simjrn FAIL: " + why); }
        private static void Log(string s) { _log(s); }

        private static int ClockNow(VM vm) { return vm.Context.Clock.Hours * 60 + vm.Context.Clock.Minutes; }
        private static int MinutesSince(int start, VM vm)
        {
            int now = ClockNow(vm);
            var adv = now - start; if (adv < 0) adv += 24 * 60;
            return adv;
        }

        /// <summary>Called once per sim-minute from the career soak tick (AutotestRunner).</summary>
        public static void Tick(VM vm, VMAvatar worker, Action<string> log)
        {
            if (_state >= 4) return;
            _log = log;
            try
            {
                // advance the sim-minute counter (per clock change, not per frame)
                int now = ClockNow(vm);
                if (now == _clockLast) return;
                _clockLast = now;
                if (_waitStart < 0) _waitStart = now;
                var since = MinutesSince(_waitStart, vm);

                switch (_state)
                {
                    case 0: Setup(vm, worker); break;
                    case 1: WaitEnergy(vm, worker, since); break;
                    case 2: if (_j2Issued) WaitJ2Settled(vm, worker, since); else StartJ2(vm, worker); break;
                    case 3: WaitRouteFail(vm, worker, since); break;
                }
            }
            catch (Exception e)
            {
                Fail("tick EXC " + e.GetType().Name + ": " + e.Message);
                _state = 4;
            }
        }

        private static void Setup(VM vm, VMAvatar worker)
        {
            if (worker == null) { Fail("no worker avatar"); _state = 4; return; }
            _worker = worker;
            _bed = vm.Entities.FirstOrDefault(e =>
            {
                try { return (e?.Object?.Resource?.MainIff?.Filename ?? "").Equals("Beds.iff", StringComparison.OrdinalIgnoreCase); }
                catch { return false; }
            });
            if (_bed == null) { Fail("no Beds.iff entity on the fixture lot"); _state = 4; return; }
            Log("AUTOTEST simjrn: worker=obj" + worker.ObjectID + " bed=obj" + _bed.ObjectID
                + " multitile=" + (_bed.MultitileGroup != null && _bed.MultitileGroup.MultiTile)
                + " groupSize=" + (_bed.MultitileGroup?.Objects?.Count ?? 0));
            // the Sleep TEST tree rejects fully-rested sims — lower Energy into the
            // tired band first (probe state-set, the R219/R222 precedent; -40 is far
            // above the -98 death gate)
            try { worker.SetMotiveData(VMMotive.Energy, (short)Math.Max(0, worker.GetMotiveData(VMMotive.Energy) - 60)); }
            catch { }
            _energyBase = worker.GetMotiveData(VMMotive.Energy);

            // J1: push the Beds 'Sleep' interaction through the real command path.
            // Attempt 2 defect fix: '4106' is the Sleep BHAV TREE id, not a TTAB row
            // index — VMEntity.GetAction keys on the TTAB row index, so pushing 4106
            // silently enqueued nothing (Execute still returned true). Resolve the
            // TTAB row whose ActionFunction is the Sleep tree (4106) instead.
            var sleepRow = FindInteractionRow(_bed, 4106, "Sleep");
            if (sleepRow < 0) { Fail("J1: Beds TTAB has no Sleep row (no row with ActionFunction=4106 or name 'Sleep')"); _state = 4; return; }
            Log("AUTOTEST simjrn J1: Beds TTAB Sleep row=" + sleepRow);
            var cmd = new VMNetInteractionCmd { Interaction = (ushort)sleepRow, CalleeID = _bed.ObjectID, CallerID = worker.ObjectID, Param0 = 0 };
            var ok = cmd.Execute(vm, worker);
            _j1Pushed = ok;
            if (!ok) { Fail("J1: VMNetInteractionCmd rejected (Sleep TTAB row " + sleepRow + ")"); _state = 4; return; }
            var queued = worker.Thread?.Queue?.Count ?? -1;
            Log("AUTOTEST simjrn J1: Sleep pushed; queue=" + queued + " energyBase=" + _energyBase);
            if (queued < 1) { Fail("J1: interaction not enqueued"); _state = 4; return; }
            _waitStart = ClockNow(vm);
            _state = 1;
        }

        /// <summary>
        /// Resolve a TTAB row index on the entity. Pass 1 (wantActionFn >= 0): the row
        /// whose ActionFunction equals it. Pass 2 (wantName != null): exact row-name
        /// match. Pass 3 (both null/-1): first row with a non-zero action tree.
        /// Returns -1 when nothing matches.
        /// </summary>
        private static int FindInteractionRow(VMEntity e, int wantActionFn, string wantName)
        {
            var ttab = e?.TreeTable;
            if (ttab?.InteractionByIndex == null) return -1;
            var rows = ttab.InteractionByIndex.OrderBy(x => x.Key);
            if (wantActionFn >= 0)
            {
                foreach (var kv in rows)
                    if (kv.Value.ActionFunction == wantActionFn) return (int)kv.Key;
            }
            if (wantName != null)
            {
                foreach (var kv in rows)
                {
                    var nm = e.TreeTableStrings?.GetString((int)kv.Value.TTAIndex) ?? "";
                    if (nm.Trim().Equals(wantName, StringComparison.OrdinalIgnoreCase)) return (int)kv.Key;
                }
            }
            if (wantActionFn < 0 && wantName == null)
            {
                foreach (var kv in rows)
                    if (kv.Value.ActionFunction != 0) return (int)kv.Key;
            }
            return -1;
        }

        private static void WaitEnergy(VM vm, VMAvatar worker, int since)
        {
            // routing complete = no routing frame on the stack; the sleep loop then raises Energy
            var routing = worker.Thread?.Stack?.OfType<FSO.SimAntics.Engine.VMRoutingFrame>().Any() == true;
            if (!routing && !_j1Routed)
            {
                _j1Routed = true;
                Log("AUTOTEST simjrn J1: routing complete (no VMRoutingFrame on the worker stack); waiting for the Energy delta");
            }
            int energy = worker.GetMotiveData(VMMotive.Energy);
            if (_j1Routed && energy >= _energyBase + 8)
            {
                _j1Energy = true;
                Log("AUTOTEST simjrn J1: Energy " + _energyBase + " -> " + energy + " after " + since
                    + " sim-min (multitile bed Sleep executed; motive effect path live)");
                StartJ2(vm, worker);
                return;
            }
            if (since > 360) { Fail("J1: Energy did not rise within 360 sim-min (energy=" + energy + ", routed=" + _j1Routed + ")"); _state = 4; }
        }

        private static void StartJ2(VM vm, VMAvatar worker)
        {
            // Attempt 2+3 defect fix: VMThread.CancelAction is DEFERRED for entries that
            // don't qualify for the immediate queue-skip branch — it sets the avatar's
            // InteractionCanceled flag (Priority=0) and the thread unwinds on a later
            // tick. Sampling synchronously after the cmd can never observe the removal,
            // so both legs are now issued here and verified per-tick in WaitJ2Settled.
            // (Attempt 4 defect: leaving the state at 1 re-entered StartJ2 every tick —
            // advance to state 2 here so WaitEnergy cannot re-fire.)
            _state = 2;
            // J2a: queue a second interaction behind the active sleep, cancel it while queued.
            var cmd = new VMNetInteractionCmd { Interaction = (ushort)FindInteractionRow(_bed, 4106, "Sleep"), CalleeID = _bed.ObjectID, CallerID = worker.ObjectID, Param0 = 0 };
            if (!cmd.Execute(vm, worker)) { Fail("J2: second push rejected"); _state = 4; return; }
            var entry = worker.Thread.Queue.LastOrDefault();
            if (entry == null) { Fail("J2: queued entry missing"); _state = 4; return; }
            _j2QueuedUid = entry.UID;
            _j2CancelCmd = new VMNetInteractionCancelCmd { ActionUID = _j2QueuedUid }.Execute(vm, worker);
            if (!_j2CancelCmd) { Fail("J2: cancel cmd returned false"); _state = 4; return; }
            Log("AUTOTEST simjrn J2a: queued-cancel cmd accepted (uid=" + _j2QueuedUid + " ttabFlags=" + entry.Flags + "); waiting for the queue removal");

            // J2b: cancel the ACTIVE sleep (interrupt path) — worker wakes, queue drains.
            _j2ActiveUid = worker.Thread.Queue.FirstOrDefault()?.UID ?? ushort.MaxValue;
            if (_j2ActiveUid == ushort.MaxValue) { Fail("J2: no active action uid to cancel"); _state = 4; return; }
            _j2ActiveCmd = new VMNetInteractionCancelCmd { ActionUID = _j2ActiveUid }.Execute(vm, worker);
            if (!_j2ActiveCmd) { Fail("J2: active cancel cmd returned false"); _state = 4; return; }
            Log("AUTOTEST simjrn J2b: active-interrupt cmd accepted (uid=" + _j2ActiveUid + "); waiting for the unwind");
            _waitStart = ClockNow(vm);
            _j2Issued = true;
        }

        private static void WaitJ2Settled(VM vm, VMAvatar worker, int since)
        {
            var queue = worker.Thread?.Queue;
            if (!_j2Cancel && queue != null && !queue.Any(q => q.UID == _j2QueuedUid))
            {
                _j2Cancel = true;
                Log("AUTOTEST simjrn J2a: queued entry removed after " + since + " sim-min (queue-removal semantics live)");
            }
            var routing = worker.Thread?.Stack?.OfType<FSO.SimAntics.Engine.VMRoutingFrame>().Any() == true;
            if (_j2Cancel && queue != null && queue.Count == 0 && !routing)
            {
                _j2ActiveCancel = true;
                Log("AUTOTEST simjrn J2b: active sleep interrupted and thread drained after " + since + " sim-min (InteractionCanceled unwind live)");
                StartJ3(vm, worker);
                return;
            }
            if (since > 60)
            {
                Fail($"J2: cancel not settled within 60 sim-min (queuedRemoved={_j2Cancel} drained=" + (queue?.Count ?? -1)
                    + " routing=" + routing + ")");
                _state = 4;
            }
        }

        private static void StartJ3(VM vm, VMAvatar worker)
        {
            // J3: push an interaction, then delete the target while routing -> the R250
            // GoToRoutingSlot-FALSE/DROPPED law must drain the queue without a crash.
            _j3Target = vm.Entities.FirstOrDefault(e =>
            {
                try { var f = e?.Object?.Resource?.MainIff?.Filename; return f != null && f.IndexOf("Chairs", StringComparison.OrdinalIgnoreCase) >= 0; }
                catch { return false; }
            }) ?? vm.Entities.FirstOrDefault(e =>
            {
                try { var f = e?.Object?.Resource?.MainIff?.Filename; return f != null && f.EndsWith(".iff") && !f.Equals("CarPortal.iff", StringComparison.OrdinalIgnoreCase) && e is VMGameObject && !(e is VMAvatar); }
                catch { return false; }
            });
            if (_j3Target == null) { Log("AUTOTEST simjrn J3: SKIPPED (no suitable target object)"); _state = 4; return; }
            // pick the target's first TTAB row with a real action tree (attempt 2 defect
            // fix: EntryPoints is the OBJf function table — init/main/portal — not TTAB
            // interactions; those ids are not valid Interaction numbers).
            var j3Row = FindInteractionRow(_j3Target, -1, null);
            if (j3Row < 0) { Log("AUTOTEST simjrn J3: SKIPPED (target advertises no TTAB interactions)"); _state = 4; return; }
            ushort interaction = (ushort)j3Row;
            var cmd = new VMNetInteractionCmd { Interaction = interaction, CalleeID = _j3Target.ObjectID, CallerID = worker.ObjectID, Param0 = 0 };
            if (!cmd.Execute(vm, worker)) { Log("AUTOTEST simjrn J3: SKIPPED (push rejected for the chosen target)"); _state = 4; return; }
            Log("AUTOTEST simjrn J3: interaction " + interaction + " pushed on obj" + _j3Target.ObjectID
                + "; deleting the target next tick to force the routing failure");
            _waitStart = ClockNow(vm);
            _state = 3;
        }

        private static void WaitRouteFail(VM vm, VMAvatar worker, int since)
        {
            // delete the target on the first tick after the push (route now pending/failing)
            if (_j3Target != null && !_j3Target.Dead)
            {
                try
                {
                    _j3Target.Delete(false, vm.Context);
                    Log("AUTOTEST simjrn J3: target deleted mid-journey");
                }
                catch (Exception de) { Log("AUTOTEST simjrn J3: target delete EXC " + de.GetType().Name + " " + de.Message); }
                _j3Target = null;
                return;
            }
            var routing = worker.Thread?.Stack?.OfType<FSO.SimAntics.Engine.VMRoutingFrame>().Any() == true;
            var queued = worker.Thread?.Queue?.Count ?? 0;
            if (!routing && queued == 0)
            {
                _j3Dropped = true;
                Log("AUTOTEST simjrn J3: DROPPED law live — routing failure drained the journey (worker idle, no exception) after " + since + " sim-min");
                _state = 4;
                J4(vm);
            }
            else if (since > 120) { Fail("J3: routing-failure drop not observed within 120 sim-min (routing=" + routing + " queue=" + queued + ")"); _state = 4; }
        }

        private static void J4(VM vm)
        {
            // J4: a command against a Disabled object must be rejected at the engine gate
            try
            {
                var anyObj = vm.Entities.FirstOrDefault(e => e is VMGameObject && !(e is VMAvatar));
                if (anyObj is VMGameObject go)
                {
                    var saved = go.Disabled;
                    go.Disabled = VMGameObjectDisableFlags.Transient;
                    var cmd = new VMNetInteractionCmd { Interaction = 1, CalleeID = go.ObjectID, CallerID = 0, Param0 = 0 };
                    var rejected = !cmd.Execute(vm, null);
                    go.Disabled = saved;
                    _j4Rejected = rejected;
                    Log("AUTOTEST simjrn J4: disabled-object cmd rejected=" + rejected + " (engine gate VMNetInteractionCmd.Disabled)");
                }
            }
            catch (Exception j4) { Log("AUTOTEST simjrn J4: EXC " + j4.Message); }
        }

        /// <summary>Verdict lines for the runner; ok = the required journeys all passed.</summary>
        public static bool Verdict(Action<string> log)
        {
            log("AUTOTEST simjrn RESULT pushed=" + _j1Pushed + " routed=" + _j1Routed + " energyDelta=" + _j1Energy
                + " queuedCancelCmd=" + _j2CancelCmd + " queuedCancel=" + _j2Cancel
                + " activeCancelCmd=" + _j2ActiveCmd + " activeInterrupt=" + _j2ActiveCancel
                + " routeFailDrop=" + _j3Dropped + " disabledRejected=" + _j4Rejected);
            var ok = _j1Pushed && _j1Energy && _j2Cancel && _j3Dropped;
            if (!ok && _fail != null) log("AUTOTEST simjrn first-failure: " + _fail);
            return ok;
        }
    }
}
