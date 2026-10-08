using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace FSO.SimAntics.Engine
{
    /// <summary>
    /// W4-REG: autotest-gated routine-EXECUTION recorder. The brainlive/carseek
    /// probes historically sampled thread STACKS at frame boundaries, which only
    /// reliably sees PARKED frames — fast legs of a loop (PersonGlobals main
    /// loop ins14 'check next motive' -> 8216 -> 8219; CarPortal 'send to work'
    /// 4105) execute and complete between samples, so a lawfully executing brain
    /// could go unobserved and flip the gate on park-timing alone (wave4-final
    /// battery 2026-10-08: brainlive brain-id-hit=False with the loop provably
    /// cycling). This recorder counts every instruction EXECUTION (owner iff +
    /// routine id), letting the probes union "executed" with "parked" evidence —
    /// the checks' own documented intent ("the engine EXECUTES the original
    /// brain in the live in-lot VM"). Zero cost when disabled.
    /// </summary>
    public static class VMRoutineExecRecorder
    {
        public static bool Enabled;

        // owner-iff filename -> routine id -> execution count. Keyed by owner
        // first because routine ids collide across IFFs (4096 is private
        // everywhere); the probes already attribute ids by owner file.
        private static Dictionary<string, Dictionary<ushort, long>> _Counts =
            new Dictionary<string, Dictionary<ushort, long>>();
        private static long _Total;
        private static object _Lock = new object();

        private static ConditionalWeakTable<VMRoutine, string> _OwnerByRoutine =
            new ConditionalWeakTable<VMRoutine, string>();

        public static void Record(VMStackFrame frame)
        {
            try
            {
                var routine = frame.Routine;
                if (routine == null) return;
                string owner;
                if (!_OwnerByRoutine.TryGetValue(routine, out owner))
                {
                    try { owner = frame.ScopeResource?.MainIff?.Filename ?? "?"; }
                    catch { owner = "?"; }
                    _OwnerByRoutine.Add(routine, owner);
                }
                lock (_Lock)
                {
                    _Total++;
                    Dictionary<ushort, long> byId;
                    if (!_Counts.TryGetValue(owner, out byId))
                    {
                        byId = new Dictionary<ushort, long>();
                        _Counts[owner] = byId;
                    }
                    long c;
                    byId[(ushort)(routine.Chunk?.ChunkID ?? 0)] = (byId.TryGetValue((ushort)(routine.Chunk?.ChunkID ?? 0), out c) ? c : 0) + 1;
                }
            }
            catch { }
        }

        /// <summary>Copy-on-read snapshot of everything executed since the last
        /// drain (or since enable). Cheap: bounded by distinct (owner, id).</summary>
        public static Dictionary<string, Dictionary<ushort, long>> Drain()
        {
            lock (_Lock)
            {
                var snap = _Counts;
                _Counts = new Dictionary<string, Dictionary<ushort, long>>();
                return snap;
            }
        }

        public static void Reset()
        {
            lock (_Lock)
            {
                _Counts = new Dictionary<string, Dictionary<ushort, long>>();
                _Total = 0;
            }
        }

        public static long TotalInstructions()
        {
            lock (_Lock) { return _Total; }
        }
    }
}
