using System;

namespace Simitone.Client.UI.Model
{
    /// <summary>
    /// R136/R137: the ORIGINAL route-history bookkeeping behind the Layout
    /// rating (House::AddLayoutTick 0x8c4f0 / ClearRouteHistory 0x8c430 /
    /// the pair rotation in House::EnterLiveMode's case 220 / the
    /// GetHouseStats tail 0x8c2ec-0x8c3d8 — machine-verified in
    /// tools/iff-dump/r136/, the call site decoded in r137/).
    ///
    /// ENGINE MODEL (decoded):
    ///   * House keeps twelve counters at +48..+92 = six (total, flagged)
    ///     generations; AddLayoutTick(flag) adds one sample per call.
    ///   * R137 — THE CALL SITE (cXPerson::Simulate(long) 0x10bff0, the
    ///     per-Sim update loop, decoded): the tick fires when
    ///       delta % 10 == 0
    ///       && s16[this+1484] == 0        (the sleep/consciousness state: AWAKE)
    ///       && s16[this+142] == 0         (the hidden flag: VISIBLE)
    ///       && f32[this+0x7b8] >= 0.0f    (Motives[11] >= 0)
    ///     and the FLAG = (GetCurrentRoute() != NULL) — the Sim currently
    ///     HOLDS A ROUTE (is moving). So the total channel counts sampled
    ///     awake+visible Sims and the flagged channel counts those in
    ///     motion; LayoutScore = 100 x (1 - 2 x route occupancy), decaying
    ///     as Sims spend more of their sampled time trekking.
    ///   * ClearRouteHistory 0x8c430 reseeds all twelve from a global
    ///     ([TOC-29460], BSS — the seed N0 is statically unrecoverable);
    ///     called from the house-rebuild/stat-snapshot paths
    ///     0xb05f4/0xb1684/0xb1c20 (renovation contexts).
    ///   * THE LAYOUT LAW (GetHouseStats tail): layoutScore =
    ///     (int)(100.0 * clamp((den - sub)/den, 0.0, 1.0)) with den built
    ///     through the CW int->double magic on a RAW low word — only the
    ///     correct signed conversion with bit-31-based counters, i.e.
    ///     den = N0 + flagged(+76); the engine's own max(sum,1) division
    ///     guard at 0x8c338 pins the intended divisor as the TOTAL.
    ///
    /// PORT (disclosed where engine-internal):
    ///   * sampling: one pass per cadence tick; each live avatar
    ///     contributes one total sample, flagged when it is currently
    ///     moving (VMAvatar.Velocity != 0 — set by the routing frame on
    ///     movement ticks, zeroed otherwise; the engine's proxy for
    ///     GetCurrentRoute()). Sampling only advances when the VM clock
    ///     ticks (pause = no Simulate = no samples, engine-faithful).
    ///     The awake/visible/motive[11] gates are approximated by
    ///     liveness (Dead/GhostImage excluded) — disclosed.
    ///   * the seed N0 is unrecoverable; the port normalizes by the total
    ///     (the engine's own guarded sum):
    ///     LayoutScore = (int)(100.0 * clamp(1f - 2f*flagged/max(1,total), 0, 1)).
    ///   * ClearRouteHistory = the port's WallsChanged (renovation) signal.
    /// </summary>
    public static class OriginalRouteHistory
    {
        public static int TotalSamples;    // the +88 lineage (population samples)
        public static int FlaggedSamples;  // the +92 lineage (in-motion samples)
        private static bool Wired;
        private static long LastClockTicks = -1;

        /// <summary>
        /// One sampling pass over every Sim: each live avatar is one sample
        /// (the engine's AddLayoutTick population channel); the flag marks
        /// Sims currently holding a route (moving this tick).
        /// </summary>
        public static void Sample(FSO.SimAntics.VM vm)
        {
            if (vm == null || vm.Context == null) return;
            Wire(vm);
            // engine-faithful pause handling: Simulate only runs when the VM
            // ticks — no clock advance, no samples
            long ticks;
            try { ticks = vm.Context.Clock.Ticks; }
            catch { return; }
            if (ticks == LastClockTicks) return;
            LastClockTicks = ticks;

            foreach (var e in vm.Entities)
            {
                var av = e as FSO.SimAntics.VMAvatar;
                if (av == null || av.Dead || av.GhostImage) continue;
                TotalSamples++;
                bool moving;
                try { moving = av.Velocity != Microsoft.Xna.Framework.Vector3.Zero; }
                catch { moving = false; }
                if (moving) FlaggedSamples++;
            }
        }

        /// <summary>
        /// The decoded Layout law with the disclosed total normalization:
        /// ratio in f32 (the engine's fdivs), the x100 scale in f64 (its
        /// fmul against the [TOC-23516]+16 = 100.0 pool double), then
        /// fctiwz truncation.
        /// </summary>
        public static int LayoutScore
        {
            get
            {
                int total = TotalSamples;
                if (total < 1) total = 1; // the engine's max(sum, 1) guard
                float ratio = 1f - (2f * FlaggedSamples) / total;
                if (ratio < 0f) ratio = 0f;
                if (ratio > 1f) ratio = 1f;
                return (int)(100.0 * ratio);
            }
        }

        /// <summary>ClearRouteHistory — the renovation reset.</summary>
        public static void Clear()
        {
            TotalSamples = 0;
            FlaggedSamples = 0;
        }

        private static void Wire(FSO.SimAntics.VM vm)
        {
            if (Wired) return;
            Wired = true;
            try
            {
                vm.Context.Architecture.WallsChanged += OnWallsChanged;
            }
            catch { }
        }

        private static void OnWallsChanged(FSO.SimAntics.VMArchitecture caller)
        {
            // the room map is rebuilt (ids renumbered) and the engine clears
            // the route history on the house-rebuild paths
            Clear();
        }
    }
}
