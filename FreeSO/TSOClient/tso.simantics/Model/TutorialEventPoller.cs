using FSO.Content;
using FSO.SimAntics.Engine;

namespace FSO.SimAntics.Model
{
    /// <summary>
    /// Port of Tut_CheckForEvents (0x156550, r245 decode.md §2 + skeptic
    /// corrections), the tutorial UI-event poll. In the original this runs
    /// once per cDDDSimsView::Simulate tick from its only call site 0x21715c.
    /// The client must tick this class the same way:
    ///
    ///   var poller = vm.TutorialEvents;          // per-VM instance
    ///   poller.MirrorLastButton(id);             // every tick, before Poll
    ///   poller.Poll();                           // once per view update
    ///
    /// Law (decode.md §2 pseudocode): mirror the last-activated button into
    /// simulator global 12 unconditionally (native does this BEFORE the owner
    /// guard); then, with a tutorial owner, run its named tree "query wait
    /// event" (BHAV 4121 — the real IFF tree: temp0=myAttr[2], temp1=myAttr[3],
    /// myAttr[2]=0, myAttr[7]+=1 so the script's flush-events spin stays
    /// alive), read ev/param from the tree's temps the way the native reads
    /// owner+0x3a/+0x3c, evaluate the 16-entry jump table against
    /// <see cref="TutorialUIState"/>, and on a match run "got wait event"
    /// (BHAV 4122) once and reset the edge latch. The trees are never
    /// reimplemented in C# — the actual owner trees run, so the myAttr[7]
    /// heartbeat and the myAttr[2] clear that fire-once-per-arming semantics
    /// depend on happen naturally.
    ///
    /// RE-ENTRANCY GUARD (deliberate port safety addition — skeptic C3): the
    /// native poll at 0x21715c runs BEFORE the view+0x110/+0x111 simulator
    /// gates, and a lesson tree can open a modal dialog (TryDialog sub-ops
    /// 0xc/0xf pump a nested DoModalWin loop), so the original can re-enter
    /// the poll and nest RunTree on the same owner. The port refuses a run
    /// while one is in flight (InPoll) or while the client reports a modal
    /// tutorial dialog being tracked (ModalDialogTracked — the native's only
    /// defenses never covered the poll itself). Check IsReentrancyBlocked
    /// from the client before work that must not interleave with a poll.
    /// </summary>
    public class TutorialEventPoller
    {
        private readonly VM VM;

        /// <summary>
        /// The UI-published snapshot the jump-table conditions evaluate
        /// against. The client UI is the single writer; see
        /// <see cref="TutorialUIState"/> for the threading contract.
        /// </summary>
        public readonly TutorialUIState State = new TutorialUIState();

        /// <summary>
        /// The native single shared change-detection latch (TOC-0x25a0,
        /// statically -1; SHARED by event codes 6 and 7, skeptic C7 — a
        /// first-sight cache from one poisons the other until the reaction
        /// path re-arms it).
        /// </summary>
        private int EdgeLatch = -1;

        private bool InPoll;

        /// <summary>
        /// Client-set: true while a modal dialog opened by the tutorial owner
        /// is being tracked (the port's SetBlockSimulator/DoModalWin window;
        /// view+0x111). Blocks <see cref="Poll"/> the same way an in-flight
        /// poll is blocked.
        /// </summary>
        public bool ModalDialogTracked;

        /// <summary>True while a poll/tree run must not start.</summary>
        public bool IsReentrancyBlocked => InPoll || ModalDialogTracked;

        public TutorialEventPoller(VM vm)
        {
            VM = vm;
        }

        /// <summary>
        /// The unconditional part of the native poll that runs before the
        /// owner guard (0x15659c..0x1565d4): simulator global 12 always holds
        /// the image id of the last-activated button, 0 when none
        /// (cSimulator::SetGlobal(0xc, btn->+0xd0->+0x30)). The client calls
        /// this every tick with the current last-activated button's image id
        /// (0 when none) — it also feeds State.LastButtonClickImageId for
        /// event code 4. Never blocked by the re-entrancy guard, like the
        /// native.
        /// </summary>
        public void MirrorLastButton(int imageId)
        {
            State.LastButtonClickImageId = imageId;
            VM.SetGlobalValue(12, (short)(imageId > 0 ? imageId : 0));
        }

        /// <summary>
        /// One poll: at most one "query wait event" run, one condition
        /// evaluation, and one "got wait event" run. Safe to call when no
        /// tutorial owner exists (returns immediately after the caller's
        /// MirrorLastButton has maintained global 12).
        /// </summary>
        public void Poll()
        {
            var vm = VM;
            if (!vm.TS1) return;
            if (IsReentrancyBlocked) return;
            // GetTutorialObject (0xe2d80) = ObjectModule+0xac; the port
            // resolves the serialized id against the live VM instead.
            var owner = vm.Context.TutorialObject;
            if (owner == null || owner.Dead || owner.Thread == null) return;

            InPoll = true;
            try
            {
                short ev, param;
                // (u8)result == 0 → cache the latch as -1 and return
                // (decode.md §2 step 4).
                if (!RunQueryWaitEvent(owner, out ev, out param))
                {
                    EdgeLatch = -1;
                    return;
                }

                if (Matches(ev, param))
                {
                    // 0x156994: run "got wait event" once per found poll.
                    // A missing tree runs nothing (ExecuteNamedEntryPoint
                    // returns false), matching a missing native name.
                    owner.ExecuteNamedEntryPoint("got wait event", vm.Context, true, null, new short[4]);
                    EdgeLatch = -1;
                }
            }
            finally
            {
                // Exceptions never leave the poll latched: the guard and the
                // owner state survive every tree failure (VMThread catches
                // inside RunInMyStack; check threads end on DialogCooldown).
                InPoll = false;
            }
        }

        /// <summary>
        /// The named-tree run behind owner->RunTree(owner->+0x11c->+0xc,
        /// 0, "query wait event", 0, 0) (0xefd30; name from the r31+0x00
        /// string table slot). Mirrors VMEntity.ExecuteNamedEntryPoint +
        /// VMThread.EvaluateCheck exactly, but keeps the check thread alive so
        /// the tree's temp0/temp1 (the native's object-resident +0x3a/+0x3c)
        /// can be read after the run. Temps start from a clone of the owner's
        /// live temps, never mutating them.
        /// </summary>
        private bool RunQueryWaitEvent(VMEntity owner, out short ev, out short param)
        {
            ev = 0;
            param = 0;
            if (owner.TreeByName == null) owner.FetchTreeByName(VM.Context);
            if (owner.TreeByName == null) return false;
            VMTreeByNameTableEntry entry;
            if (!owner.TreeByName.TryGetValue("query wait event", out entry)) return false;
            var routine = (VMRoutine)entry.bhav;
            var pair = owner.GetRoutineWithOwner(routine.ID, VM.Context);
            if (pair == null) return false;

            var thread = new VMThread(VM.Context, owner, 5);
            if (owner.Thread != null)
            {
                thread.TempRegisters = (short[])owner.Thread.TempRegisters.Clone();
                thread.TempXL = (int[])owner.Thread.TempXL.Clone();
            }
            thread.IsCheck = true;
            thread.Push(new VMStackFrame
            {
                Caller = owner,
                Callee = owner,
                CodeOwner = pair.owner,
                Routine = pair.routine,
                StackObject = null,
                Args = new short[4]
            });
            while (thread.Stack.Count > 0 && thread.DialogCooldown == 0 && !owner.Dead)
            {
                thread.Tick();
                thread.ThreadBreak = VMThreadBreakMode.Active; //cannot breakpoint in check trees
            }

            ev = thread.TempRegisters[0];
            param = thread.TempRegisters[1];
            return !VM.Aborting && thread.DialogCooldown == 0 &&
                thread.LastStackExitCode == VMPrimitiveExitCode.RETURN_TRUE;
        }

        /// <summary>
        /// The 16-entry jump table *(TOC-0x57b8)[ev] (decode.md §2 step 6,
        /// skeptic-corrected page names). Codes 0/3/10/12 and anything outside
        /// 0..15 never fire.
        /// </summary>
        private bool Matches(short ev, short param)
        {
            var state = State;
            switch (ev)
            {
                case 1: // call-panel mode 0 (CPState exists and GetCPMode()==0)
                    return state.CpModeIsZero;
                case 2: // person panel page == "motives"
                    return PageIs("motives");
                case 9: // "rel" (skeptic C1: NOT swapped with 11)
                    return PageIs("rel");
                case 11: // "job"
                    return PageIs("job");
                case 13: // "skill"
                    return PageIs("skill");
                case 15: // "per.ity"
                    return PageIs("per.ity");
                case 4: // last-activated button's image id == param
                    return state.LastButtonClickImageId >= 0 && state.LastButtonClickImageId == param;
                case 5: // neighbor param's portrait is the shown buffer
                    return param >= 0 && state.ShownPortraitNeighborId != 0 &&
                        state.ShownPortraitNeighborId != uint.MaxValue &&
                        state.ShownPortraitNeighborId == (uint)param;
                case 6: // rotation change, shared -1 latch (first sight caches)
                    return EdgeDetected(state.RotationEdge);
                case 7: // scroll change, same shared latch
                    return EdgeDetected(state.ScrollEdge);
                case 8: // cursor over/activating the button with image id param
                    return state.HoverButtonImageId >= 0 && state.HoverButtonImageId == param;
                case 14: // person panel page absent (inverse of case 2's gate)
                    return state.PersonPanelPage == null;
                default: // 0, 3, 10, 12, negative and >15: never
                    return false;
            }
        }

        private bool PageIs(string caption)
        {
            // StringBuffer equality (0x141e90): exact, case sensitive.
            return State.PersonPanelPage == caption;
        }

        private bool EdgeDetected(int value)
        {
            if (EdgeLatch == -1)
            {
                // first sight just caches (decode.md §2 cases 6/7)
                EdgeLatch = value;
                return false;
            }
            return value != EdgeLatch;
        }
    }
}
