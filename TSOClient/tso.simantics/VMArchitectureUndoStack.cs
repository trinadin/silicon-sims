using FSO.LotView.Model;
using FSO.SimAntics.Entities;
using FSO.SimAntics.Model;
using System;
using System.Collections.Generic;

namespace FSO.SimAntics
{
    /// <summary>
    /// UI-22 tranche 1 — build-mode architecture undo/redo for TS1 lots.
    /// Implements the native law decoded in tools/iff-dump/r256-undo-decode/
    /// undo-machinery-law.md at VM-batch granularity:
    ///
    /// - One UndoManager equivalent per VMArchitecture (native: one at
    ///   cGameTools+8 for the whole game; the port's architecture state is per
    ///   lot, which also gives the native lifecycle for free — the stack is
    ///   runtime-only, dies on lot switch/reload and SURVIVES save because
    ///   VMArchitectureMarshal never carries it).
    /// - SubmitUndoable law (law §2): every applied real-mode batch pushes one
    ///   entry (one drag = one step, the native 12-caller census §6) and CLEARS
    ///   the redo list. Native depth is unbounded; this port caps at 100
    ///   entries (DEVIATION, design §5.1 — full-story snapshots weigh more
    ///   than the native 60-byte delta records).
    /// - Undo/redo law (§3): undo pops the newest entry, restores it, pushes it
    ///   onto the redo list; redo pops the newest redo entry, replays it and
    ///   pushes it back. A failed undo drops the remaining undo list; a failed
    ///   redo drops the remaining redo list.
    /// - Money law (§4 floors): undo refunds the batch's signed cost, redo
    ///   re-charges it, through the same GlobalLink.PerformTransaction path
    ///   the live architecture queue uses (uid1 pays uid2; null budget family
    ///   = maxis-infinite-money no-op, exactly like the purchase path).
    /// - Lifecycle law (§5): leaving build mode clears both stacks (the
    ///   UIMainPanel.SetMode hook mirrors the native CPState::SetMode
    ///   old-mode==2 guard); no save path touches the stack.
    ///
    /// Deliberate deviations (implementation-design.md §5): depth cap 100;
    /// redo re-runs the recorded batch (the restore makes state bit-identical
    /// to pre-batch) instead of a stored snapshot swap; refused batches never
    /// reach the stack because the port refuses them pre-transaction in
    /// VMTS1GlobalLinkStub.Tick (native flush-on-commit-fail is unreachable).
    /// </summary>
    public class VMArchitectureUndoEntry
    {
        /// <summary>The applied batch (per drag) — replayed verbatim on redo.</summary>
        public List<VMArchitectureCommand> Forward;

        /// <summary>Signed cost RunCommands returned for the batch (purchase > 0, sellback &lt; 0).</summary>
        public int Cost;

        /// <summary>Actor the batch was attributed to (transaction counterpart).</summary>
        public uint ActorUID;

        /// <summary>Pre-batch story snapshots, keyed by level (1-based).</summary>
        public Dictionary<int, VMArchitectureStorySnapshot> Saved;
    }

    /// <summary>
    /// Pre-batch copy of one story's wall/floor arrays. WallsAt is NOT stored:
    /// it is fully derived from the Walls arrays (VMArchitecture.RebuildWallsAt
    /// is the authoritative rebuild), so restoring Walls regenerates it.
    /// </summary>
    public class VMArchitectureStorySnapshot
    {
        public WallTile[] Walls;
        public FloorTile[] Floors;
    }

    public class VMArchitectureUndoStack
    {
        /// <summary>DEVIATION from the native unbounded stack (law §2.3 / design §5.1).</summary>
        public const int MaxDepth = 100;

        private readonly List<VMArchitectureUndoEntry> UndoList = new List<VMArchitectureUndoEntry>();
        private readonly List<VMArchitectureUndoEntry> RedoList = new List<VMArchitectureUndoEntry>();

        /// <summary>
        /// Set while the stack itself drives RunCommands (redo replay) so the
        /// replay is not captured as a new user step (would clear the redo
        /// list and loop). Set across undo restores too, for symmetry.
        /// </summary>
        public bool CaptureSuspended;

        public bool CanUndo
        {
            get { return UndoList.Count > 0; }
        }

        public bool CanRedo
        {
            get { return RedoList.Count > 0; }
        }

        /// <summary>Native FlushCommandQueues equivalent (law §5): both stacks drop.</summary>
        public void Clear()
        {
            UndoList.Clear();
            RedoList.Clear();
        }

        /// <summary>
        /// Native SubmitUndoable (law §2): push the applied batch as one undo
        /// step and CLEAR the redo list — any newly submitted command kills
        /// redo. Called from VMArchitecture.RunCommands only for real-mode,
        /// non-transient, wall/floor-only TS1 batches that changed the lot.
        /// </summary>
        public void Submit(VMArchitectureUndoEntry entry)
        {
            UndoList.Add(entry);
            if (UndoList.Count > MaxDepth) UndoList.RemoveAt(0);
            RedoList.Clear();
        }

        /// <summary>
        /// Begin a capture for the touched stories of a batch. Returns null
        /// when capture must not happen (transient preview, visual (non-real)
        /// mode, non-TS1 lot, terrain/grass batch — tranche 2, or a replay in
        /// progress).
        /// </summary>
        public VMArchitectureUndoEntry BeginCapture(VMArchitecture arch, List<VMArchitectureCommand> commands, bool transient)
        {
            if (transient || !arch.RealMode || CaptureSuspended) return null;
            var vm = arch.Context.VM;
            if (vm == null || !vm.TS1) return null;

            bool hasTerrain = false;
            var levels = new List<int>();
            foreach (var cmd in commands)
            {
                if (cmd.Type == VMArchitectureCommandType.TERRAIN_RAISE ||
                    cmd.Type == VMArchitectureCommandType.TERRAIN_FLATTEN ||
                    cmd.Type == VMArchitectureCommandType.GRASS_DOT)
                {
                    hasTerrain = true; // tranche 1: terrain batches are not captured (design §4)
                }
                var level = cmd.level;
                if (level < 1) level = 1;
                if (level > arch.Stories) level = (sbyte)arch.Stories;
                if (!levels.Contains(level)) levels.Add(level);
            }
            if (hasTerrain) return null;

            var entry = new VMArchitectureUndoEntry
            {
                Forward = commands,
                ActorUID = (commands.Count > 0) ? commands[0].CallerUID : 0,
                Saved = new Dictionary<int, VMArchitectureStorySnapshot>()
            };
            foreach (var level in levels)
            {
                var walls = arch.Walls[level - 1];
                var floors = arch.Floors[level - 1];
                var snapWalls = new WallTile[walls.Length];
                var snapFloors = new FloorTile[floors.Length];
                Array.Copy(walls, snapWalls, walls.Length);
                Array.Copy(floors, snapFloors, floors.Length);
                entry.Saved[level] = new VMArchitectureStorySnapshot { Walls = snapWalls, Floors = snapFloors };
            }
            return entry;
        }

        /// <summary>
        /// Native UndoLastCommand (law §3): restore the newest entry, move it
        /// to the redo list. Refunds the batch's signed cost first (money law
        /// §4); a refused takeback drops the remaining undo list, mirroring
        /// the native ReleaseUndoList failure path.
        /// </summary>
        public bool Undo(VMArchitecture arch)
        {
            if (UndoList.Count == 0) return false;
            var entry = UndoList[UndoList.Count - 1];
            UndoList.RemoveAt(UndoList.Count - 1);

            if (!ApplyMoney(arch, entry, true))
            {
                UndoList.Clear(); // native: a failed undo breaks the chain
                return false;
            }

            CaptureSuspended = true;
            try
            {
                foreach (var kv in entry.Saved)
                {
                    var walls = arch.Walls[kv.Key - 1];
                    var floors = arch.Floors[kv.Key - 1];
                    Array.Copy(kv.Value.Walls, walls, walls.Length);
                    Array.Copy(kv.Value.Floors, floors, floors.Length);
                }
                arch.RebuildWallsAt();     // native UndoOne + list rebuild
                arch.SignalAllDirty();     // keep the dirty flags the Tick path expects
                arch.RegenRoomMap();       // native DirtyTileNeighborhood/room regen equivalent
                arch.SignalRedraw();
            }
            finally
            {
                CaptureSuspended = false;
            }

            RedoList.Add(entry);
            return true;
        }

        /// <summary>
        /// Native RedoLastCommand (law §3): replay the newest redo entry and
        /// push it back onto the undo list. Re-charges the batch's signed cost
        /// (Purchase law §4); a refused re-charge drops the remaining redo
        /// list, mirroring the native ReleaseRedoList failure path.
        /// </summary>
        public bool Redo(VMArchitecture arch)
        {
            if (RedoList.Count == 0) return false;
            var entry = RedoList[RedoList.Count - 1];
            RedoList.RemoveAt(RedoList.Count - 1);

            if (!ApplyMoney(arch, entry, false))
            {
                RedoList.Clear(); // native: a failed redo drops the rest of the redo list
                return false;
            }

            CaptureSuspended = true;
            try
            {
                // state is bit-identical to pre-batch after undo, so the batch
                // replays cleanly (design §2; native forward-iteration law §4).
                arch.RunCommands(entry.Forward, false);
            }
            finally
            {
                CaptureSuspended = false;
            }

            UndoList.Add(entry);
            return true;
        }

        /// <summary>
        /// Money law (§4 floors: per-change Purchase/Refund hooks): undo
        /// applies -Cost (refund a purchase / take back a sellback), redo
        /// re-applies +Cost (re-charge a purchase / replay a sellback).
        /// Routed through the same PerformTransaction path the live
        /// architecture queue uses; no budget family (maxis infinite money)
        /// is a no-op, exactly like purchases.
        /// </summary>
        private static bool ApplyMoney(VMArchitecture arch, VMArchitectureUndoEntry entry, bool undo)
        {
            var vm = arch.Context.VM;
            if (vm == null || !vm.TS1) return true;
            int amount = Math.Abs(entry.Cost);
            if (amount == 0) return true;

            bool toPlayer = undo ? (entry.Cost > 0) : (entry.Cost < 0);
            uint uid1 = toPlayer ? uint.MaxValue : entry.ActorUID;
            uint uid2 = toPlayer ? entry.ActorUID : uint.MaxValue;

            if (!toPlayer)
            {
                // affordability pre-check, mirroring the stub's
                // CanTransactBudgetForFamily guard (the interface PerformTransaction
                // overloads are void, so refuse before mutating anything).
                var payer = vm.GetObjectByPersist(entry.ActorUID) as VMAvatar;
                var family = (payer != null) ? vm.TS1State?.CurrentFamily : null;
                if (family != null && family.Budget - amount < 0) return false;
            }

            // null callback = purely synchronous transaction (the family budget
            // readouts read FAMI.Budget directly every frame).
            vm.GlobalLink?.PerformTransaction(vm, false, uid1, uid2, amount, (short)0, null);
            return true;
        }
    }
}
