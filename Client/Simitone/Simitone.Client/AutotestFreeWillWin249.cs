/*
This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
If a copy of the MPL was not distributed with this file, You can obtain one at
http://mozilla.org/MPL/2.0/.
*/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using FSO.Client;
using FSO.SimAntics;
using FSO.SimAntics.Engine;
using FSO.SimAntics.Entities;
using FSO.SimAntics.Model;
using FSO.SimAntics.Primitives;
using FSO.Files.Formats.IFF.Chunks;

namespace Simitone.Client
{
    /// <summary>
    /// R249 'freewillwin' (opt-in, focused): validates the REBUILT TS1 free-will winner
    /// selection end-to-end against the native law decoded in tools/iff-dump/r249-freewill/
    /// decode.md + skeptic-corrections.md and tools/iff-dump/r249-freewill-cfg/decode.md
    /// (the maintainer's adjudication is binding):
    ///
    ///   gather      = per-candidate TestInteraction ONLY — NO per-candidate scoring
    ///                 (R250 binding call-site adjudication: GetInteractionScore 0x9f9d4
    ///                 has exactly two call sites in the binary, the live one at 0x109d48
    ///                 immediately after the WINNER's re-test 0x109d1c;
    ///                 AppendInteractionsForAuto contains none, so gather never scores,
    ///                 Candidate.Score rides lazily at 0, and the draw is uniform over
    ///                 gather order)
    ///   winnerScore = atten * (GetInteractionScore(winner ads + re-test MotiveAdChanges)
    ///                 - H)  (positive = motive improvement; curves = person GLOB
    ///                 STR#501/503; the re-test's tree may rewrite the ad copy and the
    ///                 hand-off score consumes the mutated copy — Winner.Score carries it)
    ///   "sort"      = the faithful heapsort transpile of CW routine 0x59a370 with the
    ///                 game comparator (eps +1e-7 / -1e-7) — net effect rotate-left-by-one
    ///   K           = max(1, FCNS "random selection count"(=4) - trunc(attr18/2000))
    ///   drawnIdx    = 0                          when (visitor OR post-sort[0].flag != 0)
    ///                                            AND the lot is NOT downtown
    ///               = rand() % min(K, count)     otherwise  (VMContext.NextRandom xorshift)
    ///   accepted    = standing (attr0 == 0)   : unconditional
    ///                 engaged  (attr0 != 0)   : winnerScore >= cutoff (FCNS 1e-6)
    ///
    /// Scenario (the controlled multi-candidate design, R249 finish): ride the default
    /// house-5 soak window (elapsed < 9; the soak itself runs 10 sim-min). The SUBJECT is
    /// selected by the round task's stratum knob — the adult avatar whose
    /// (house-number + object id) &amp; 3 stratum opens the EVEN-ID candidate pool
    /// (stratum 2: obj21 on house 5). Rationale: the faithful heapsort's net effect is
    /// rotate-left-by-one, so the winner pool (post-sort[0..K-1]) is exactly
    /// gather[1..K] — the first ADMITTED entries in module order — and the previous
    /// subject's stratum-3 pool head was entirely chair 'sit' (act4107) entries whose
    /// test trees fail (with the R250 per-candidate gather tests restored those
    /// candidates are now EXCLUDED at gather — the pool is test-filtered, and (g)
    /// requires a SERVED winner as a hard pass condition). Motive pins cannot reshuffle
    /// that head (the adult Fun curve is flat above 75, so any Fun pin that kills the
    /// chair ads kills every Fun ad). All avatars are pinned to the competition set
    /// Fun=0 (with gather scores gone, the >= 2-candidate competition comes from the
    /// test-passing Fun-advertising entries themselves), Social=50, Hunger/Energy/Hygiene=75, Bladder=100, Comfort/Room=75;
    /// the subject additionally pins personality attrs 2..7 + attr18=0 (K = 4) and the
    /// AutonomyLevel ad ceiling; VM.FreeWillEnabled=true (restored in finally).
    /// Decisions are observed through the frozen engine seam
    /// VMFindBestAction.TS1DecisionObserved (engine-side rebuild round), bound TYPED
    /// (freewillvar/AddScoredActions precedent): the decision snapshot is the seam's
    /// contract shape {caller id, isChild/isVisitor, freeWill, cutoff,
    /// candidates [{calleeId, actionNumber, Param0, score, flag, dist}] (post-"sort"
    /// order), PreSortCandidates, drawnIdx, accepted, motive snapshot (9 values),
    /// RandomSeed at decision}; per decision we copy the FULL snapshot + the harness
    /// wall frame. Because the natural brain's autonomy gate did not open inside the
    /// short window in any prior run, the battery deterministically SERVES the original
    /// brain's 'try autonomy' node (PersonGlobals 8233) by PUSHING it onto each
    /// avatar's REAL thread stack (subject at elapsed 3, the other adult at 6, the
    /// child at 7 — the latter two only after the free-will OFF control releases): the
    /// tree then executes on the avatar's own tick with its real queue, exactly like a
    /// natural brain run, so a served winner's EnqueueAction lands in the observable
    /// queue (the earlier RunInMyStack probe ran the tree against a swapped-in temp
    /// queue whose contents are discarded on return — insertions were structurally
    /// unobservable there). Push-attributed decisions (within 30 frames of the push)
    /// are tagged and disclosed; they exercise the same primitive/seam/queue path as
    /// natural decisions.
    ///
    /// The pure-law predictor is fed ONLY the decision snapshot (plus the avatar's pinned
    /// INPUT attributes at the wall frame — never engine outputs): it re-applies the
    /// faithful heapsort transpile (ported from tools/iff-dump/r249-freewill-cfg/verify.py
    /// heapsort_59a370), recomputes K, replays the draw LOCALLY from the snapshot's
    /// RandomSeed with the VMContext xorshift (see DrawReplayLaw comment for the capture
    /// point the landed seam uses), and applies the cutoff law to the accept decision.
    /// </summary>
    internal static class AutotestFreeWillWin249
    {
        // ---- lifecycle state -------------------------------------------------------
        private static Action<string> _log;
        private static VM _vm;
        private static List<VMAvatar> _avatars;
        private static bool _armed;
        private static bool _evaluated;
        private static bool _restored;
        private static int _startMinute;      // sim-minute when armed
        private static long _frame;           // harness frames since arm (no wall-clock waits)
        private static int _offControlStartFrame = -1;
        private static bool _offControlStarted;
        private static bool _offControlDone;
        private static int _offControlDecisions;
        private static int _offControlFrames;
        private static VMAvatar _subject;      // the battery's avatar (first ADULT; the
                                               // loaded order leads with a child on Goth)
        private static string _loadPathStatic; // the loaded house file path (game-data pin target)
        private static string _housePathHash;
        private static bool _seamBound;
        private static bool _seamBoundAtArm;
        private static bool _seamEverFired;

        // ---- saved/restore state (finally semantics) --------------------------------
        private static short[] _av0Motives = new short[16];
        private static short[] _av0PersonData;
        private static short _av0Attr18Original;   // attr18 as LOADED, captured before the window pin
        private static bool _attr18RestoreChecked; // Restore re-read attr18 after writing the snapshot back
        private static short _attr18RestoreObserved;
        private static readonly List<short[]> _otherMotives = new List<short[]>();
        private static readonly List<VMAvatar> _otherAvatars = new List<VMAvatar>();
        private static bool _freeWillSaved;
        private static bool _freeWillValue;

        // ---- observation -------------------------------------------------------------
        private static readonly List<Rec> _recs = new List<Rec>();
        private static readonly object RecLock = new object();
        // R250: first-appearance tracking is by REFERENCE identity. The old UID==0
        // filter was a blind spot: EnqueueAction assigns UID = ActionUID++ FROM 0, so
        // each avatar's FIRST queue insertion carries UID 0 and was never recorded —
        // the scan then misread that avatar's served winner as a hand-off drop.
        private static readonly HashSet<VMQueuedAction> _seenQueueActions = new HashSet<VMQueuedAction>();
        private static readonly List<QueueInsertion> _queueInsertions = new List<QueueInsertion>();

        private sealed class Rec
        {
            public long Frame;
            public short CallerId;
            public bool IsVisitor, IsChild, FreeWill, Accepted;
            public float Cutoff;
            public int DrawnIdx;
            public ulong Seed;
            public short Attr0AtFrame;   // Posture (input state at the wall frame)
            public short Attr18AtFrame;  // LogicSkill (input state at the wall frame)
            public string Reason;        // "uniform" | "deterministic0" per predictor
            public int PredictedIdx;
            public int K, PoolSize;
            public List<Cand> Candidates;        // snapshot post-"sort" order
            public List<Cand> PreSortCandidates; // snapshot gather order (when the seam still holds it)
            public Cand Winner;                  // snapshot's drawn winner record
            public bool DuringProbe;             // arrived within the serve-probe attribution window
        }

        private sealed class Cand
        {
            public short CalleeId;
            public ushort ActionNumber;
            public short Param0;
            public float Score;
            public byte Flag;
            public float Dist;
        }

        private sealed class QueueInsertion
        {
            public long Frame;
            public short AvatarId;
            public short CalleeId;
            public ushort ActionRoutineId;
            public short Arg0;
            public short Priority;
        }

        // ---- verdicts (one per sub-assertion; each logs its own PASS/FAIL line) ------
        private static readonly Dictionary<string, bool> _verdicts = new Dictionary<string, bool>();
        private static readonly List<string> _disclosures = new List<string>();

        /// <summary>True when every sub-assertion passed (runner maps this to Pass/Fail).</summary>
        public static bool AllPassed { get; private set; }
        public static bool VerdictReady { get; private set; }

        private static void Sub(string name, bool ok, string detail)
        {
            _verdicts[name] = ok;
            _log("AUTOTEST freewillwin " + name + ": " + (ok ? "PASS" : "FAIL") +
                (string.IsNullOrEmpty(detail) ? "" : " (" + detail + ")"));
        }

        // ---- arm / per-frame / finish ------------------------------------------------

        /// <summary>Arm the battery at the start of the soak (called once from StateSample).</summary>
        public static void Begin(VM vm, List<VMAvatar> avatars, string loadPath, Action<string> log)
        {
            _vm = vm; _avatars = avatars; _log = log; _loadPathStatic = loadPath;
            try
            {
                _startMinute = vm.Context.Clock.Minutes;
                _frame = 0;

                // battery subject (R249 finish, the round task's stratum knob): the ADULT
                // avatar whose (house + object id) & 3 stratum opens the EVEN-ID candidate
                // pool (stratum 2 — obj21 on house 5). The previous subject (first adult,
                // obj18, stratum 3 = odd ids) drew its winner pool exclusively from the
                // first admitted module-order entries — four chairs' 'sit' (act4107)
                // entries — and every chair-sit hand-off drops in the port's check-tree
                // serving (the (g) scoping). Adult = age 0 or >= 18 (VMAvatar's own
                // classifier); loaded entity order leads with the Goth child.
                short houseNumber = (vm.GlobalState != null && vm.GlobalState.Length > 10) ? vm.GlobalState[10] : (short)0;
                Func<VMAvatar, bool> isAdult = a =>
                {
                    try
                    {
                        var age = a.GetPersonData(VMPersonDataVariable.PersonsAge);
                        return age == 0 || age >= 18;
                    }
                    catch { return false; }
                };
                _subject = avatars.Where(isAdult).FirstOrDefault(a => ((houseNumber + a.ObjectID) & 3) == 2) ??
                    avatars.FirstOrDefault(isAdult) ?? avatars[0];
                var a0 = _subject;
                _log("AUTOTEST freewillwin roster: " + string.Join(" | ", avatars.Select((av, i) =>
                    "av" + i + "=obj" + av.ObjectID + " age=" + av.GetPersonData(VMPersonDataVariable.PersonsAge) +
                    " type=" + av.GetPersonData(VMPersonDataVariable.PersonType) +
                    " stratum=" + ((houseNumber + av.ObjectID) & 3) +
                    " queue=" + (av.Thread?.Queue?.Count ?? -1))) +
                    " — subject=obj" + a0.ObjectID + " stratum=" + ((houseNumber + a0.ObjectID) & 3) +
                    " (stratum-2 knob: even-id pool); house=" + houseNumber);
                for (var i = 0; i < 16; i++) _av0Motives[i] = a0.GetMotiveData((VMMotive)i);
                _av0PersonData = a0.GetPersonDataClone();
                _otherMotives.Clear();
                _otherAvatars.Clear();
                foreach (var av in avatars.Where(av => !ReferenceEquals(av, _subject)))
                {
                    var m = new short[16];
                    for (var i = 0; i < 16; i++) m[i] = av.GetMotiveData((VMMotive)i);
                    _otherMotives.Add(m);
                    _otherAvatars.Add(av);
                }
                _freeWillSaved = true;
                _freeWillValue = VM.FreeWillEnabled;

                // scenario pins: subject Fun=0 (positive-score competition), the rest safe;
                // SetMotiveData clamps to [-100, limit] — Bladder=100 needs no overfill.
                a0.SetMotiveData(VMMotive.Fun, 0);
                a0.SetMotiveData(VMMotive.Social, 50);
                a0.SetMotiveData(VMMotive.Hunger, 75);
                a0.SetMotiveData(VMMotive.Energy, 75);
                a0.SetMotiveData(VMMotive.Hygiene, 75);
                a0.SetMotiveData(VMMotive.Bladder, 100);
                a0.SetMotiveData(VMMotive.Comfort, 75);
                a0.SetMotiveData(VMMotive.Room, 75);
                // personality attrs 2..7 pinned to their loaded values + attr18 (LogicSkill,
                // the K reducer) pinned to 0 so K = FCNS count = 4 for every decision.
                // The PRE-PIN attr18 is captured first (P1-2): the pin must not touch the
                // _av0PersonData snapshot — Restore writes that snapshot back verbatim, so
                // mutating it here would make the pinned 0 permanent (battery self-poison).
                _av0Attr18Original = a0.GetPersonData((VMPersonDataVariable)18);
                for (short p = 2; p <= 7; p++)
                    a0.SetPersonData((VMPersonDataVariable)p, _av0PersonData[p]);
                a0.SetPersonData((VMPersonDataVariable)18, 0);
                // the native ad ceiling is attr36 (AutonomyLevel): candidates are admitted
                // iff 0 < advertise weight <= attr36. Engine/spawn default can be 0 (admits
                // nothing) — raise to a safe ceiling for the window if lower.
                if (a0.GetPersonData(VMPersonDataVariable.AutonomyLevel) < 100)
                    a0.SetPersonData(VMPersonDataVariable.AutonomyLevel, 100);
                // R249 finish — MULTIPLE SUBJECTS observed (round task knob (c)): every
                // avatar gets the same competition pin set so each stratum's pool is
                // positive-score competitive and any avatar's served decision can feed
                // the (g) queue cross-check (their motives are saved above and restored
                // in the finally path).
                foreach (var av in _otherAvatars)
                {
                    av.SetMotiveData(VMMotive.Fun, 0);
                    av.SetMotiveData(VMMotive.Social, 50);
                    av.SetMotiveData(VMMotive.Hunger, 75);
                    av.SetMotiveData(VMMotive.Energy, 75);
                    av.SetMotiveData(VMMotive.Hygiene, 75);
                    av.SetMotiveData(VMMotive.Bladder, 100);
                    av.SetMotiveData(VMMotive.Comfort, 75);
                    av.SetMotiveData(VMMotive.Room, 75);
                }
                // staged real-stack 'try autonomy' pushes: the subject at elapsed 3 (the
                // natural-gate classification point), the remaining avatars staggered at
                // elapsed 6,7,... — only after the (h) OFF control has released (it runs
                // elapsed 4..~6; a push during OFF would fire its prim 3 against the
                // disabled gate and waste the serve).
                _probeSchedule.Clear();
                _probeSchedule.Add(new KeyValuePair<short, int>(_subject.ObjectID, 3));
                var stage = 6;
                foreach (var av in _otherAvatars)
                    _probeSchedule.Add(new KeyValuePair<short, int>(av.ObjectID, stage++));
                // free will must be ON for the observation window (restored in finally)
                VM.FreeWillEnabled = true;
                NoteMutation("arm-time motive/persondata pins + FreeWillEnabled=true");

                // game-data pin (assertion (j)): hash the loaded house file read-only
                _housePathHash = HashFile(_loadPathStatic);

                CensusGatherInputs();
                ScanServeWeights(houseNumber);

                BindSeam();
                _seamBoundAtArm = _seamBound;
                _armed = true;
                _log("AUTOTEST freewillwin armed house=" + _loadPathStatic +
                    " subject=obj" + a0.ObjectID +
                    " posture=" + a0.GetPersonData(VMPersonDataVariable.Posture) +
                    " attr18=" + a0.GetPersonData((VMPersonDataVariable)18) +
                    " attr36=" + a0.GetPersonData(VMPersonDataVariable.AutonomyLevel) +
                    " age=" + a0.GetPersonData(VMPersonDataVariable.PersonsAge) +
                    " personType=" + a0.GetPersonData(VMPersonDataVariable.PersonType) +
                    " fun=" + a0.GetMotiveData(VMMotive.Fun) +
                    " freeWill=" + VM.FreeWillEnabled + " seamBound=" + _seamBound +
                    " (motives pinned Fun=0 Social=50 Hunger/Energy/Hygiene=75 Bladder=100)");
            }
            catch (Exception e)
            {
                _log("AUTOTEST freewillwin arm EXC " + e.GetType().Name + " " + e.Message);
                Restore("arm-exc");
                Evaluate();
            }
        }

        /// <summary>Per-frame hook from StateSample. Returns true when the battery finished.</summary>
        public static bool SampleTick()
        {
            if (!_armed || _evaluated) return _evaluated;
            try
            {
                _frame++;

                // Tier-A: first-appearance scan of each avatar's queue for Autonomous-priority
                // insertions (the winner hand-off). Runs outside the VM tick; a queue entry
                // that appeared since the previous frame is attributable to this frame window.
                ScanQueues();

                var minute = _vm.Context.Clock.Minutes;
                var elapsed = minute - _startMinute;

                // staged serve-probes: the subject at elapsed >= 3, the remaining avatars
                // at their scheduled elapsed points (6,7,...) but only after the OFF
                // control released — see the probe schedule comment in Begin. One push
                // per frame; each entry fires once.
                for (int i = 0; i < _probeSchedule.Count; i++)
                {
                    var s = _probeSchedule[i];
                    if (elapsed < s.Value) continue;
                    if (s.Key != _subject.ObjectID && !_offControlDone) continue;
                    _probeSchedule.RemoveAt(i);
                    var av = _avatars.FirstOrDefault(x => x.ObjectID == s.Key);
                    if (av != null) RunTryAutonomyProbe(av);
                    break;
                }

                // (b)-family knob, staged AFTER the primary subject decision so (a)-(f)
                // keep their original unpinned pool: apply the ad-ceiling window (when the
                // weights allow it) that drops the seating-family pool head, then serve a
                // SECOND subject push that re-draws from the reshaped pool. The finally
                // path restores the subject's original attr36 with the rest of the
                // persondata clone.
                if (!_servePinApplied && _offControlDone && elapsed >= 6)
                {
                    _servePinApplied = true;
                    NoteMutation("serve-pin attr36 -> " + _servePinAttr36);
                    if (_servePinAttr36 > 0 && _subject != null)
                    {
                        _subject.SetPersonData(VMPersonDataVariable.AutonomyLevel, _servePinAttr36);
                        _log("AUTOTEST freewillwin serve-pin: subject obj" + _subject.ObjectID +
                            " attr36 -> " + _servePinAttr36 +
                            " (ad-ceiling window isolating the person-person family from the seating head)");
                    }
                    else
                    {
                        _log("AUTOTEST freewillwin serve-pin: NOT APPLICABLE (act8662 advertises at the same weight as the seating head — no ad-ceiling window exists); staging a repeat subject serve-push instead");
                    }
                    _probeSchedule.Add(new KeyValuePair<short, int>(_subject.ObjectID, 6));
                }

                // (h) free-will OFF control: mid-window, after elapsed >= 4, disable free will
                // for 120 frames — the byte gate must silence the subject's decisions entirely.
                if (!_offControlStarted && !_offControlDone && elapsed >= 4)
                {
                    _offControlStarted = true;
                    _offControlStartFrame = (int)_frame;
                    VM.FreeWillEnabled = false;
                    NoteMutation("free-will OFF control engaged");
                    _log("AUTOTEST freewillwin OFF-control engaged at elapsed=" + elapsed +
                        " (120 frames; VM.FreeWillEnabled=false)");
                }
                if (_offControlStarted && !_offControlDone)
                {
                    _offControlFrames++;
                    if (_offControlFrames >= 120)
                    {
                        _offControlDone = true;
                        VM.FreeWillEnabled = true;
                        NoteMutation("free-will OFF control released");
                        _log("AUTOTEST freewillwin OFF-control released after " + _offControlFrames +
                            " frames, subject decisions during OFF=" + _offControlDecisions);
                    }
                }

                // evaluate before elapsed >= 9 (inside the 10-sim-minute soak window,
                // one-shot — leaves the staggered non-subject pushes at 6/7 time to
                // decide; the runner's finish shim evaluates whatever was observed if
                // the soak ends first)
                if (elapsed >= 9)
                {
                    Restore("window-end");
                    Evaluate();
                }
            }
            catch (Exception e)
            {
                _log("AUTOTEST freewillwin tick EXC " + e.GetType().Name + " " + e.Message);
                Restore("tick-exc");
                Evaluate();
            }
            return _evaluated;
        }

        /// <summary>Finish shim: restore + (if never evaluated) evaluate with what was observed.</summary>
        public static void FinishShim(string why)
        {
            if (!_armed) return;
            Restore(why);
            if (!_evaluated) Evaluate();
        }

        // ---- diagnostic serve-probe (classification + queue-observation serving) ---------

        // The natural soak never opened the brain's autonomy gate inside the short window
        // (every prior run), so the battery deterministically serves the ORIGINAL brain's
        // 'try autonomy' (PersonGlobals 8233, the prim-3 dispatch node) by PUSHING it onto
        // each avatar's REAL thread stack — the tree then runs on the avatar's own tick
        // with its real queue, so a served winner's EnqueueAction lands in the observable
        // queue (a RunInMyStack probe would run against a temp queue that is discarded on
        // return). This classifies a silent engine path AND gives the (g) queue
        // cross-check real insertions to verify. Decisions within 30 frames of an avatar's
        // push are tagged DuringProbe (harness-served; the (a)-(f) battery stays
        // natural-reach-first but runs on whatever decisions the window actually produced —
        // the served path is the same primitive/seam/queue path a natural brain takes).
        private static readonly Dictionary<short, int> _probePushFrame = new Dictionary<short, int>();
        private static readonly List<KeyValuePair<short, int>> _probeSchedule = new List<KeyValuePair<short, int>>();
        private static int _probeDecisions;
        private static short _servePinAttr36 = -1;   // ad-ceiling window isolating the
                                                     // person-person family (when the
                                                     // weights allow); applied between
                                                     // serve-pushes
        private static bool _servePinApplied;

        // R250: documented harness state mutations. The (g) hard pass treats an
        // accepted-winner drop as a FAIL unless the drop coincides with one of THESE
        // (a same-tick state change the harness itself made and disclosed). The window
        // is N..N+1 (P2-3: the code allowed N..N+2 — an off-by-one against this
        // rationale; aligned on N..N+1): the hook runs outside the VM tick, so a
        // mutation at frame N can lawfully influence VM work at frame N or N+1 —
        // never N+2.
        private sealed class HarnessMutation { public long Frame; public string Why; }
        /// <summary>Documented excusal window: a harness mutation at frame M can
        /// lawfully influence a decision (and its drop) at frames M..M+1 only.</summary>
        private const int MutationWindowFrames = 1;
        private static readonly List<HarnessMutation> _harnessMutations = new List<HarnessMutation>();

        private static void NoteMutation(string why)
        {
            _harnessMutations.Add(new HarnessMutation { Frame = _frame, Why = why });
        }

        /// <summary>Read-only TTAB lookup: the entry's TestFunction — the check-tree BHAV
        /// id the hand-off re-test runs (the native test tree at 0x106570). 0 = the entry
        /// auto-passes its test (the native entry+0 == 0 law).
        /// CAVEAT (P2-6, log-only): FirstOrDefault matches the FIRST TTAB entry carrying
        /// this action number — when an object advertises one action number from several
        /// entries the logged testBHAV may be a sibling entry's, not the drawn one. The
        /// value feeds drop-diagnostic log lines only; no verdict reads it.</summary>
        private static ushort FindTestBhavId(short calleeId, ushort actionNumber)
        {
            try
            {
                var obj = _vm.Context.ObjectQueries.WithAutonomy.FirstOrDefault(o => o.ObjectID == calleeId);
                var tt = obj?.TreeTable;
                if (tt == null) return 0;
                var entry = tt.Interactions.FirstOrDefault(e => e.ActionFunction == actionNumber);
                return entry?.TestFunction ?? (ushort)0;
            }
            catch { return 0; }
        }

        private static void RunTryAutonomyProbe(VMAvatar av)
        {
            if (av == null) return;
            try
            {
                var semi = av?.Object?.Resource?.SemiGlobal;
                var routine = semi?.GetRoutine((ushort)8233) as VMRoutine;
                if (routine == null || routine.Instructions == null || routine.Instructions.Length == 0)
                {
                    _log("AUTOTEST freewillwin probe: 'try autonomy' 8233 unavailable on obj" +
                        av.ObjectID + "'s semi-global");
                    return;
                }
                _probePushFrame[av.ObjectID] = (int)_frame;
                NoteMutation("serve-probe push ('try autonomy' 8233) on obj" + av.ObjectID);
                // REAL-stack push: the tree executes on the avatar's own thread/queue over
                // the following ticks, exactly like a natural brain dispatch, so a served
                // winner's EnqueueAction lands in the observable queue.
                var pushed = av.Thread?.Push(new VMStackFrame
                {
                    Routine = routine,
                    Caller = av,
                    Callee = av,
                    StackObject = av,
                    CodeOwner = av.Object,
                    Args = new short[4]
                }) ?? false;
                var natural = _recs.Count(r => r.CallerId == av.ObjectID &&
                    !_WasPushAt(r.CallerId, r.Frame));
                _log("AUTOTEST freewillwin probe: 'try autonomy' 8233 PUSHED on obj" + av.ObjectID +
                    " real stack (pushed=" + pushed + " frame=" + _frame +
                    "; natural decisions for this avatar so far=" + natural +
                    "; stack-depth=" + (av.Thread?.Stack.Count ?? -1) + ")");
            }
            catch (Exception e)
            {
                _log("AUTOTEST freewillwin probe EXC obj" + av.ObjectID + " " +
                    e.GetType().Name + " " + e.Message);
            }
        }

        private static bool _WasPushAt(short avatarId, long frame)
        {
            int pushFrame;
            return _probePushFrame.TryGetValue(avatarId, out pushFrame) &&
                frame >= pushFrame && (frame - pushFrame) <= 30;
        }

        // ---- seam binding (typed against the landed frozen contract) --------------------

        // The landed contract (VMFindBestAction.cs, R249 OBSERVATION SEAM):
        //   static event Action<VMTS1Decision> TS1DecisionObserved
        //   VMTS1Decision {CallerId, IsVisitor, IsChild, FreeWill, Cutoff, DrawnIdx,
        //     Accepted, RandomSeed, MotiveSnapshot[9], Candidates, PreSortCandidates, Winner}
        //   VMTS1DecisionCandidate {CalleeId, ActionNumber, Param0, Score, Flag, Dist}
        // RandomSeed is captured immediately BEFORE the draw (seam comment: "captures the
        // seed immediately BEFORE the draw so a checker can replay it") — the replay law
        // below applies the xorshift first.
        private static void BindSeam()
        {
            VMFindBestAction.TS1DecisionObserved += OnDecision;
            _seamBound = true;
            _log("AUTOTEST freewillwin seam bound: VMFindBestAction.TS1DecisionObserved (typed, landed contract)");
        }

        private static void UnbindSeam()
        {
            if (!_seamBound) return;
            try { VMFindBestAction.TS1DecisionObserved -= OnDecision; } catch { }
            _seamBound = false;
        }

        /// <summary>
        /// Copy the decision snapshot into harness records, then run the pure-law
        /// predictor inline for subject decisions (Predict). The predictor only reads
        /// the frozen snapshot plus input-state persondata — it does NO engine mutation
        /// and no gather work (the engine already gathered; the seam hands us the result).
        /// </summary>
        private static void OnDecision(VMTS1Decision d)
        {
            try
            {
                if (d == null) return;
                _seamEverFired = true;
                var duringOff = _offControlStartFrame > 0 && !_offControlDone;
                var subject = _subject;
                var rec = new Rec
                {
                    Frame = _frame,
                    CallerId = d.CallerId,
                    IsVisitor = d.IsVisitor,
                    IsChild = d.IsChild,
                    FreeWill = d.FreeWill,
                    Accepted = d.Accepted,
                    Cutoff = d.Cutoff,
                    DrawnIdx = d.DrawnIdx,
                    Seed = d.RandomSeed
                };
                var subjectDecision = subject != null && rec.CallerId == subject.ObjectID;
                rec.DuringProbe = _WasPushAt(rec.CallerId, _frame);
                if (subjectDecision && rec.DuringProbe) _probeDecisions++;
                if (duringOff)
                {
                    if (subjectDecision) _offControlDecisions++;
                    return; // OFF-control decisions are counted, not analyzed (the landed
                            // prelude returns before the seam fires for gated sims, so this
                            // counter stays 0 unless the gate leaks)
                }
                rec.Candidates = CopyCands(d.Candidates);
                rec.PreSortCandidates = CopyCands(d.PreSortCandidates);
                rec.Winner = CopyCand(d.Winner);
                if (subject != null && subjectDecision)
                {
                    // input state at the wall frame (NOT engine outputs): posture (attr0)
                    // classifies standing/engaged for the cutoff law; attr18 feeds K.
                    rec.Attr0AtFrame = subject.GetPersonData(VMPersonDataVariable.Posture);
                    rec.Attr18AtFrame = subject.GetPersonData((VMPersonDataVariable)18);
                    Predict(rec);
                }
                lock (RecLock) { _recs.Add(rec); }
            }
            catch (Exception e)
            {
                _log("AUTOTEST freewillwin decision-handler EXC " + e.GetType().Name + " " + e.Message);
            }
        }

        private static Cand CopyCand(VMTS1DecisionCandidate c)
        {
            if (c == null) return null;
            return new Cand
            {
                CalleeId = c.CalleeId, ActionNumber = c.ActionNumber, Param0 = c.Param0,
                Score = c.Score, Flag = c.Flag, Dist = c.Dist
            };
        }

        private static List<Cand> CopyCands(List<VMTS1DecisionCandidate> list)
        {
            if (list == null) return null;
            var outList = new List<Cand>(list.Count);
            foreach (var c in list) outList.Add(CopyCand(c));
            return outList;
        }


        // ---- Tier-A queue cross-check --------------------------------------------------

        private static void ScanQueues()
        {
            try
            {
                foreach (var av in _avatars)
                {
                    var q = av.Thread?.Queue;
                    if (q == null) continue;
                    foreach (var qa in q)
                    {
                        if (qa == null || !_seenQueueActions.Add(qa)) continue;
                        // record EVERY new queued action; the (g) cross-check classifies:
                        // an Autonomous insertion must trace to an accepted seam decision
                        // (identity match), and any insertion matching a seam decision's
                        // identity with a NON-Autonomous priority is a real violation.
                        _queueInsertions.Add(new QueueInsertion
                        {
                            Frame = _frame,
                            AvatarId = av.ObjectID,
                            CalleeId = (short)(qa.Callee?.ObjectID ?? 0),
                            ActionRoutineId = (ushort)(qa.ActionRoutine?.ID ?? 0),
                            Arg0 = (qa.Args != null && qa.Args.Length > 0) ? qa.Args[0] : (short)0,
                            Priority = qa.Priority
                        });
                    }
                }
            }
            catch (Exception e)
            {
                _log("AUTOTEST freewillwin queue-scan EXC " + e.GetType().Name + " " + e.Message);
            }
        }

        // ---- the pure-law predictor ------------------------------------------------------

        // Draw-replay law (VMContext.NextRandom xorshift, VMContext.cs:596):
        //   seed ^= seed >> 12; seed ^= seed << 25; seed ^= seed >> 27;
        //   return (seed * 2685821657736338717) % max;
        // The landed seam captures RandomSeed AT the decision, immediately BEFORE the
        // engine consumes the winner draw (VMFindBestAction.cs, seam block) — so the
        // replay applies the xorshift first, then the multiplier-mod, and compares to
        // drawnIdx. (If the capture sat after the draw, the xorshift step would be
        // omitted; this is the one capture-point-dependent line in the predictor.)
        private static int LocalDraw(ulong seed, int pool)
        {
            seed ^= seed >> 12;
            seed ^= seed << 25;
            seed ^= seed >> 27;
            return (int)((seed * 2685821657736338717UL) % (ulong)pool);
        }

        // Faithful heapsort transpile — ported from tools/iff-dump/r249-freewill-cfg/
        // verify.py heapsort_59a370 (group H fixtures: [1..10] -> [2..10,1], [5,1,9] ->
        // [1,9,5], n=2 swap, all-equal identity). Comparator (0x10a040, eps +1e-7/-1e-7):
        //   +1 iff b.score - a.score >= 1e-7 ; -1 iff |delta| < 1e-7 ; 0 otherwise.
        // Element-level transpilation: verify.py swaps 17 bytes per pair on the engine's
        // byte image (w=16 + 1 trailing byte that aliases the next record's first byte —
        // a memory-layout artifact below the seam contract's record resolution); the
        // record permutation it induces is what this transpile reproduces, and for
        // strictly-distinct scores it is exactly rotate-left-by-one.
        private static void GameHeapsort(List<Cand> a)
        {
            int n = a.Count;
            if (n < 2) return;
            int r19; // transpile scratch (the routine's r19 register)
            int r24 = (n >> 1) + 1;
            int r30 = r24;        // element cursor (was r24*w, w=1 at element granularity)
            int r25 = n;
            int r27 = r24 - 1;    // (r24-1)*w
            int r28 = n - 1;      // (n-1)*w
            while (true)          // L3c0
            {
                if (r24 > 1)
                {
                    r30 -= 1; r27 -= 1; r24 -= 1;
                }
                else
                {
                    var t = a[r27]; a[r27] = a[r28]; a[r28] = t;
                    r25 -= 1;
                    if (r25 == 1) return;
                    r28 -= 1;
                }
                int r0 = r30 - 1;     // r30 + r31, r31 = -w
                int r26 = r24;
                int r29 = r0;
                while (true)          // L4bc
                {
                    if (r26 * 2 <= r25)
                    {
                        r26 *= 2;                    // L424
                        r0 = r26 - 1;
                        r19 = r29;
                        r29 = r0;                    // left child
                        if (r26 < r25)
                        {
                            int r20 = r29 + 1;
                            if (Cmp(a[r29], a[r20]) < 0) { r29 = r20; r26 += 1; }
                        }
                        if (Cmp(a[r19], a[r29]) >= 0) break;
                        var t = a[r19]; a[r19] = a[r29]; a[r29] = t;
                    }
                    else break;
                }
            }
        }

        // CompareScoredInteractions (0x10a040) with the compiled eps +1e-7 / -1e-7
        private static int Cmp(Cand a, Cand b)
        {
            float d = b.Score - a.Score;
            return (d >= 1e-7f) ? 1 : ((d > -1e-7f) ? -1 : 0);
        }

        /// <summary>Predict the lawful outcome for a subject decision from the snapshot alone.</summary>
        private static void Predict(Rec rec)
        {
            try
            {
                var cands = rec.Candidates;
                int count = (cands == null) ? 0 : cands.Count;
                // K = max(1, FCNS "random selection count"(4) - trunc(attr18/2000))
                int k = Math.Max(1, 4 - (rec.Attr18AtFrame / 2000));
                rec.K = k;
                rec.PoolSize = Math.Min(k, count);
                if (count == 0) { rec.Reason = "empty"; rec.PredictedIdx = -1; return; }

                // house 5 is a residential lot — downtown == false (zoning != 1), a scenario
                // pin. The LANDED engine draw law (VMFindBestAction.ExecuteTS1Native):
                //   deterministic = (visitor || post-sort[0].Flag != 0)
                //   idx = 0 only when deterministic AND ZoningIsDowntown — and the port's
                //   ZoningIsDowntown is the constant false (no downtown lots mounted), so
                //   the uniform xorshift draw runs for every decision in practice.
                // POLARITY NOTE: the task brief quoted "idx 0 when deterministic AND NOT
                // downtown"; the CFG decode (r249-freewill-cfg §4c) and the landed engine
                // both force idx 0 only ON downtown — this predictor codes against what
                // landed (the landing's DISCLOSURE covers the adjudication).
                bool downtown = false;
                bool deterministicZero = (rec.IsVisitor || cands[0].Flag != 0) && downtown;
                if (deterministicZero)
                {
                    rec.Reason = "deterministic0";
                    rec.PredictedIdx = 0;
                }
                else
                {
                    rec.Reason = "uniform";
                    rec.PredictedIdx = LocalDraw(rec.Seed, rec.PoolSize);
                }
            }
            catch (Exception e)
            {
                rec.Reason = "predict-exc:" + e.GetType().Name;
                rec.PredictedIdx = -1;
            }
        }

        // ---- restore + evaluate ---------------------------------------------------------

        private static void Restore(string why)
        {
            if (_restored || !_armed) return;
            _restored = true;
            UnbindSeam();
            try
            {
                VM.FreeWillEnabled = _freeWillSaved ? _freeWillValue : true;
                var subject = _subject;
                if (subject == null) { _log("AUTOTEST freewillwin state restored (" + why + "): FreeWillEnabled=" + VM.FreeWillEnabled); return; }
                if (_av0PersonData != null)
                {
                    for (int p = 0; p < _av0PersonData.Length; p++)
                        subject.SetPersonData((VMPersonDataVariable)p, _av0PersonData[p]);
                    // P1-2 restore contract, proven not assumed: re-read attr18 right after
                    // the write-back — it must equal the PRE-PIN original (the snapshot is
                    // pristine since the pin stopped mutating it), never the window's 0.
                    _attr18RestoreObserved = subject.GetPersonData((VMPersonDataVariable)18);
                    _attr18RestoreChecked = true;
                }
                for (var i = 0; i < 16; i++) subject.SetMotiveData((VMMotive)i, _av0Motives[i]);
                for (int oi = 0; oi < _otherAvatars.Count && oi < _otherMotives.Count; oi++)
                    for (var i = 0; i < 16; i++)
                        _otherAvatars[oi].SetMotiveData((VMMotive)i, _otherMotives[oi][i]);
                _log("AUTOTEST freewillwin state restored (" + why + "): motives + persondata subject obj" +
                    subject.ObjectID + ", motives other avatars, FreeWillEnabled=" + VM.FreeWillEnabled);
            }
            catch (Exception e)
            {
                _log("AUTOTEST freewillwin restore EXC " + e.GetType().Name + " " + e.Message);
            }
        }

        private static void Evaluate()
        {
            if (_evaluated) return;
            _evaluated = true;
            try
            {
                var subjId = (_subject != null) ? _subject.ObjectID : (short)-1;
                var subjRecs = _recs.Where(r => r.CallerId == subjId).ToList();
                var otherCount = _recs.Count - subjRecs.Count;
                var subjNatural = subjRecs.Count(r => !r.DuringProbe);
                _log("AUTOTEST freewillwin observed decisions=" + _recs.Count +
                    " subject=" + subjRecs.Count + " (natural=" + subjNatural +
                    " served=" + _probeDecisions + ") other-avatars=" + otherCount +
                    " frames=" + _frame + " seamBound=" + _seamBound + " seamFired=" + _seamEverFired +
                    " queue-insertions-tracked=" + _queueInsertions.Count +
                    " subject-queue-now=" + ((_subject?.Thread?.Queue != null) ? string.Join(",", _subject.Thread.Queue.Select(q => q.Priority + "/" + (ushort)(q.ActionRoutine?.ID ?? 0))) : "none") +
                    " subject-persondata: posture=" + ((_subject != null) ? _subject.GetPersonData(VMPersonDataVariable.Posture) : -1) +
                    " autonomy=" + ((_subject != null) ? _subject.GetPersonData(VMPersonDataVariable.AutonomyLevel) : -1) +
                    " fun=" + ((_subject != null) ? _subject.GetMotiveData(VMMotive.Fun) : -1));
                if (subjNatural == 0 && _probeDecisions > 0)
                    _disclosures.Add("natural-reach: the soak produced no natural subject decisions; the real-stack 'try autonomy'(8233) serve-push DID produce " +
                        _probeDecisions + " — the rebuilt primitive + seam are live and the battery below ran on served decisions (the brain's autonomy gate did not open naturally inside the window)");
                if (_recs.Count > 0 && _recs.All(r => r.DuringProbe))
                    _disclosures.Add("natural-reach: EVERY decision this run was harness-served (real-stack 'try autonomy'(8233) pushes); the natural autonomy gate never opened in-window for any avatar");

                // per-decision evidence line (the scenario's actual competing-candidate set,
                // every avatar); capped at the first 40 so the run log stays bounded
                int logged = 0;
                foreach (var r in _recs)
                {
                    if (logged++ >= 40) continue;
                    var set = (r.Candidates == null) ? "" :
                        string.Join(" | ", r.Candidates.Select(c =>
                            "obj" + c.CalleeId + "#act" + c.ActionNumber +
                            (c.Param0 != 0 ? "(p" + c.Param0 + ")" : "") +
                            " s=" + c.Score.ToString("0.#######") + " f=" + c.Flag));
                    _log("AUTOTEST freewillwin decision av=obj" + r.CallerId +
                        (r.CallerId == subjId ? "(subject)" : "") +
                        " " + (r.DuringProbe ? "served" : "natural") +
                        " frame=" + r.Frame +
                        " n=" + (r.Candidates == null ? 0 : r.Candidates.Count) +
                        " drawnIdx=" + r.DrawnIdx + " accepted=" + r.Accepted +
                        " law=" + r.Reason + "/" + r.PredictedIdx +
                        " K=" + r.K + " attr0=" + r.Attr0AtFrame + " cutoff=" + r.Cutoff.ToString("0.############") +
                        " seed=" + r.Seed + " [" + set + "]");
                }
                if (logged > 40) _log("AUTOTEST freewillwin decision evidence capped: " +
                    (logged - 40) + " further decisions summarized by the assertions below");

                // (a) at least one subject decision with >= 2 candidates
                var multi = subjRecs.Where(r => r.Candidates != null && r.Candidates.Count >= 2).ToList();
                Sub("a-multi-candidate", multi.Count >= 1,
                    multi.Count + " subject decision(s) with >=2 candidates" +
                    (_seamBoundAtArm ? "" : " [SEAM-ABSENT: engine seam never bound]") +
                    (multi.Count == 0 && _seamBound ? " — the Fun=0 pin yielded no competing set (honest finding)" : ""));

                // (b) R250 HAND-OFF SCORE LAW (hard assert). With the corrected native law
                // (gather never scores — the draw is uniform over gather order), the old
                // "candidates carry positive scores" claim is meaningless: pool scores ride
                // lazily at 0. The law-true facts asserted instead:
                //   1. every POOL score is the gather-lazy 0 (no per-candidate scoring
                //      crept back in),
                //   2. every drawn subject winner carries a finite HAND-OFF score
                //      (GetInteractionScore at 0x109d48 on the re-test's tree-mutated ad
                //      copy), and
                //   3. the cutoff decision matches that score's sign/law — standing
                //      (attr0 == 0) accepts unconditionally; engaged accepts iff
                //      winnerScore >= cutoff (FCNS 1e-6).
                int bChecked = 0; bool bLaw = true; float bMaxAbs = 0f; float bPoolMax = 0f;
                foreach (var r in subjRecs)
                {
                    if (r.Candidates == null) continue;
                    foreach (var c in r.Candidates) bPoolMax = Math.Max(bPoolMax, Math.Abs(c.Score));
                    if (r.DrawnIdx < 0 || r.DrawnIdx >= r.Candidates.Count) continue;
                    if (r.Winner == null || float.IsNaN(r.Winner.Score) || float.IsInfinity(r.Winner.Score))
                    {
                        bLaw = false;
                        continue;
                    }
                    bChecked++;
                    bMaxAbs = Math.Max(bMaxAbs, Math.Abs(r.Winner.Score));
                    bool bLawAccept = (r.Attr0AtFrame == 0) || (r.Winner.Score >= r.Cutoff);
                    if (bLawAccept != r.Accepted) bLaw = false;
                }
                bool bOk = bChecked > 0 && bLaw && bPoolMax == 0f;
                Sub("b-handoff-score-law", bOk,
                    bOk ? (bChecked + " drawn subject winner(s) carry a finite hand-off score " +
                        "(|score| up to " + bMaxAbs.ToString("0.######") +
                        "); pool gather scores all lazy-0; cutoff decisions match the hand-off-score law") :
                        ("hand-off score law violated: drawn=" + bChecked + " law-consistent=" + bLaw +
                        " max-pool-|score|=" + bPoolMax.ToString("0.######") + " (must be 0)"));

                // (c)+(d)+(f): per-decision lawfulness, seed replay, winner identity
                int cOk = 0, cBad = 0, dChecked = 0, dOk = 0, dBad = 0, fOk = 0, fBad = 0;
                var uniformSeen = false;
                foreach (var r in subjRecs)
                {
                    if (r.Candidates == null || r.Candidates.Count == 0 || r.PredictedIdx < 0) continue;
                    // (c) drawnIdx lawful
                    bool cPass;
                    if (r.Reason == "deterministic0") cPass = r.DrawnIdx == 0;
                    else cPass = r.DrawnIdx >= 0 && r.DrawnIdx < r.PoolSize;
                    if (cPass) cOk++; else cBad++;
                    // (d) seed replay (uniform path only — the deterministic path consumes
                    // no draw; asserting rand()==0 there would test nothing)
                    if (r.Reason == "uniform")
                    {
                        uniformSeen = true;
                        dChecked++;
                        if (LocalDraw(r.Seed, r.PoolSize) == r.DrawnIdx) dOk++; else dBad++;
                    }
                    // (f) winner identity: the candidate at MY predicted lawful index must be
                    // the snapshot's drawn winner — identity compared across the snapshot's
                    // own Winner record, the Candidates array at drawnIdx, AND the Tier-A
                    // queued action shape (callee+action+Param0). The seam's PreSortCandidates
                    // IS the true gather order (the engine snapshots the candidate list before
                    // the transpile mutates it), so the heapsort-transpile PERMUTATION assert
                    // below runs per decision: transposing the gather order through the
                    // harness's own GameHeapsort must reproduce the snapshot's post-"sort"
                    // array element-for-element. (An unchanged PreSortCandidates — the old
                    // copy-of-post-sort degenerate case — is detected and skips the
                    // permutation assert, but the engine no longer does that.)
                    bool fPass = r.PredictedIdx == r.DrawnIdx;
                    if (fPass && r.Winner != null)
                    {
                        var dw = r.Candidates[r.DrawnIdx];
                        // R250: identity = CalleeId/ActionNumber/Param0/Flag. Score is NOT
                        // an identity field anymore: the pool entries carry the gather-lazy
                        // 0 while the Winner carries the hand-off score (0x109d48) — they
                        // are lawfully different numbers.
                        fPass = r.Winner.CalleeId == dw.CalleeId && r.Winner.ActionNumber == dw.ActionNumber &&
                            r.Winner.Param0 == dw.Param0 && r.Winner.Flag == dw.Flag;
                    }
                    var preSortIsTrueGather = false;
                    if (fPass && r.PreSortCandidates != null && r.PreSortCandidates.Count == r.Candidates.Count)
                    {
                        preSortIsTrueGather = !r.PreSortCandidates.Select((c, i) => new { c, i })
                            .Any(x => x.c.CalleeId != r.Candidates[x.i].CalleeId ||
                                      x.c.ActionNumber != r.Candidates[x.i].ActionNumber ||
                                      Math.Abs(x.c.Score - r.Candidates[x.i].Score) > 1e-9f ||
                                      x.c.Flag != r.Candidates[x.i].Flag);
                        if (preSortIsTrueGather)
                        {
                            var permuted = r.PreSortCandidates.Select(c => new Cand
                            {
                                CalleeId = c.CalleeId, ActionNumber = c.ActionNumber, Param0 = c.Param0,
                                Score = c.Score, Flag = c.Flag, Dist = c.Dist
                            }).ToList();
                            GameHeapsort(permuted);
                            for (int i = 0; i < permuted.Count && fPass; i++)
                            {
                                var p = permuted[i]; var q = r.Candidates[i];
                                if (p.CalleeId != q.CalleeId || p.ActionNumber != q.ActionNumber ||
                                    Math.Abs(p.Score - q.Score) > 1e-9f || p.Flag != q.Flag)
                                    fPass = false;
                            }
                        }
                    }
                    if (fPass) fOk++; else fBad++;
                }
                var trueGatherDecisions = subjRecs.Count(r => r.PreSortCandidates != null &&
                    r.Candidates != null && r.PreSortCandidates.Count == r.Candidates.Count &&
                    !r.PreSortCandidates.Select((c, i) => new { c, i })
                        .Any(x => x.c.CalleeId != r.Candidates[x.i].CalleeId ||
                                  x.c.ActionNumber != r.Candidates[x.i].ActionNumber ||
                                  Math.Abs(x.c.Score - r.Candidates[x.i].Score) > 1e-9f ||
                                  x.c.Flag != r.Candidates[x.i].Flag));
                if (_seamBound && subjRecs.Any(r => r.PreSortCandidates != null))
                    _disclosures.Add("f-winner-identity: the seam's PreSortCandidates is the TRUE gather order " +
                        "(engine snapshots before the sort transpile) — the heapsort-transpile PERMUTATION assert ran against it on " +
                        trueGatherDecisions + " decision(s), plus the group-H predictor self-test below" +
                        (trueGatherDecisions == 0 ? " [DEGENERATE: every PreSortCandidates equaled the post-sort array this run]" : ""));
                // predictor self-test (non-vacuous): the transpile must reproduce verify.py's
                // group-H fixtures exactly or every (f) verdict above is meaningless.
                var st = new[] { 5f, 1f, 9f }.Select(v => new Cand { Score = v }).ToList();
                GameHeapsort(st);
                var stOk = st.Select(c => c.Score).SequenceEqual(new[] { 1f, 9f, 5f });
                var st2 = new[] { 1f, 2f, 3f, 4f }.Select(v => new Cand { Score = v }).ToList();
                GameHeapsort(st2);
                stOk &= st2.Select(c => c.Score).SequenceEqual(new[] { 2f, 3f, 4f, 1f });
                _log("AUTOTEST freewillwin transpile self-test (verify.py group H): " + (stOk ? "OK" : "DRIFT"));
                Sub("c-drawnidx-lawful", cBad == 0 && cOk > 0, cOk + " lawful / " + cBad + " unlawful");
                if (!uniformSeen)
                {
                    // deterministic-0 dominated the window (first post-sort candidate carried
                    // the autonomy flag on a residential lot): no draw was consumed, so the
                    // seed replay has nothing to check — disclosed, not vacuous. (R244 lesson:
                    // a downgrade with a stated reason, never a silent pass.)
                    _disclosures.Add("d-seed-replay: no uniform-path decision observed (every decision took the deterministic idx-0 fast path) — seed replay untestable this run");
                    Sub("d-seed-replay", true, "DISCLOSED-DOWNGRADE: no uniform-path decisions; deterministic0 path asserted instead in (c)");
                }
                else Sub("d-seed-replay", dBad == 0 && dOk > 0, dOk + " replayed-draw matches / " + dBad + " mismatch");
                Sub("f-winner-identity", fBad == 0 && fOk > 0 && stOk,
                    fOk + " winner identities match / " + fBad + " mismatch; transpile self-test " + (stOk ? "OK" : "DRIFT"));

                // (e) cutoff law vs the accept decision, standing vs engaged. R250: the
                // score side of the law is the WINNER's HAND-OFF score (Winner.Score) —
                // Candidates[DrawnIdx].Score is the gather-lazy 0 and cannot decide
                // anything (see (b)).
                int eChecked = 0, eBad = 0; bool standingSeen = false, engagedSeen = false;
                foreach (var r in subjRecs)
                {
                    if (r.Candidates == null || r.DrawnIdx < 0 || r.DrawnIdx >= (r.Candidates?.Count ?? 0)) continue;
                    if (r.Winner == null) continue;
                    eChecked++;
                    bool lawAccept;
                    if (r.Attr0AtFrame == 0) { standingSeen = true; lawAccept = true; }
                    else
                    {
                        engagedSeen = true;
                        lawAccept = r.Winner.Score >= r.Cutoff;
                    }
                    if (lawAccept != r.Accepted) eBad++;
                }
                var eNote = eChecked + " decisions; standing=" + standingSeen + " engaged=" + engagedSeen;
                if (!standingSeen && !engagedSeen) _disclosures.Add("e-cutoff: no decidable accept decisions captured");
                if (!engagedSeen) _disclosures.Add("e-cutoff: no ENGAGED decision captured this run (the subject never took a directed posture mid-window); only the unconditional standing law was exercised");
                Sub("e-cutoff-law", eChecked > 0 && eBad == 0, eNote + " mismatches=" + eBad);

                // (g) queue cross-check — R250 HARD PASS. The LAW pins (violations fail):
                // (1) an accepted drawn winner must SERVE (identity+priority match within
                // its documented window) unless a DOCUMENTED harness mutation coincides,
                // and at least one serving winner is REQUIRED per run; (2) an insertion
                // carrying a seam winner's identity at a non-Autonomous priority is a
                // violation. (R250 upgrade of the R249 scoping:
                // the "tolerated disclosed drops" clause is GONE — with the engine's
                // per-candidate gather tests restored, the pool is test-filtered and a
                // drawn winner must serve; see the drop classification below. The old
                // act4107 "chair-sit/slot-routing" drop scope was a misclassification.)
                // RE-SCOPE (R250 score-law iteration-1 gate finding): an Autonomous
                // insertion with NO matching accepted seam decision is NOT a violation.
                // The engine audit shows exactly two Autonomous minters: the prim-3
                // winner hand-off (VMFindBestAction — and it emits the seam BEFORE the
                // enqueue unconditionally, so a hand-off enqueue ALWAYS carries its
                // accepted decision's winner identity) and the tree-driven Push
                // Interaction primitive (VMPushPriority.Autonomous — e.g. the brain's
                // sit fallback after a failed autonomy attempt, or sub-pushes from a
                // served action's tree). With the corrected score law (uniform draw over
                // the full test-passing pool) winners now include entries whose trees
                // exercise that Push Interaction path in-window; the old score-filtered
                // draw simply never surfaced it. The old "untracked = violation" clause
                // asserted a prim-3 exclusivity the native never had. These insertions
                // are logged as push-path insertions and disclosed, not failed.
                var gServing = new List<string>();
                var gViolations = new List<string>();
                var gDrops = new List<string>();
                var gPushPath = new List<string>();
                foreach (var ins in _queueInsertions)
                {
                    bool IdentityMatch(Rec r) => r != null && r.CallerId == ins.AvatarId &&
                        r.Accepted && r.DrawnIdx >= 0 && r.Candidates != null &&
                        r.DrawnIdx < r.Candidates.Count &&
                        r.Candidates[r.DrawnIdx].CalleeId == ins.CalleeId &&
                        r.Candidates[r.DrawnIdx].ActionNumber == ins.ActionRoutineId &&
                        r.Candidates[r.DrawnIdx].Param0 == ins.Arg0;
                    var matches = _recs.Where(IdentityMatch).ToList();
                    var autonomous = ins.Priority == (short)VMQueuePriority.Autonomous;
                    if (matches.Count > 0 && autonomous)
                    {
                        var m = matches[0];
                        gServing.Add("frame" + ins.Frame + " av" + ins.AvatarId + " obj" + ins.CalleeId +
                            "#act" + ins.ActionRoutineId + " arg0=" + ins.Arg0 +
                            " <= decision frame" + m.Frame + " drawnIdx=" + m.DrawnIdx +
                            " (identity+priority match)");
                    }
                    else if (matches.Count > 0)
                    {
                        gViolations.Add("frame" + ins.Frame + " av" + ins.AvatarId + " obj" + ins.CalleeId +
                            "#act" + ins.ActionRoutineId + " arg0=" + ins.Arg0 +
                            " priority=" + ins.Priority + " carries a seam winner's identity but was NOT enqueued Autonomous");
                    }
                    else if (autonomous)
                    {
                        // re-scoped (see the (g) header): not from the prim-3 hand-off by
                        // construction (a hand-off enqueue always carries its accepted
                        // decision's winner identity) — tree-driven Push Interaction push.
                        gPushPath.Add("frame" + ins.Frame + " av" + ins.AvatarId + " obj" + ins.CalleeId +
                            "#act" + ins.ActionRoutineId + " arg0=" + ins.Arg0 +
                            " (Autonomous via the tree-driven Push Interaction path — outside the free-will hand-off law)");
                    }
                    // a non-Autonomous insertion with no seam decision is a directed/NPC
                    // path — outside the free-will law, not a violation.
                }
                // drop classification (R250 — the misclassification removed): an ACCEPTED
                // drawn winner that never enqueued dropped at the hand-off re-test (the
                // native's own winner re-test, TestInteraction 0x109d1c inside
                // TryFindBestAction, before the cutoff). Engine invariants make that the
                // ONLY synchronous cause: the re-test and the insertion resolve the action
                // through the same deterministic GetAction in the same tick, so a winner
                // that passed the re-test always enqueues — no insertion means the
                // re-test's check tree returned false. With the R250 per-candidate gather
                // tests restored, the pool holds only test-passing candidates (the old
                // test-failing drop class — urn "Mourn" 4107, phone ring latch 8208,
                // token-gated magic socials 8662 — is excluded at gather, and the old
                // "chair-sit/slot-routing" 4107 label was a misclassification: act 4107
                // is UrnStone.iff "Mourn"). So a drop means conditions genuinely changed
                // between the gather test and the winner re-test. A drop caused by a
                // DOCUMENTED harness mutation is the narrow disclosed escape; any other
                // drop is a FAIL under (g).
                // P2-5 DISCLOSED EDGE: the port's queue path can also remove a just-served
                // winner LAWFULLY in the same tick — VMThread.AttemptPush's insertion
                // re-check runs CheckAction with auto=0 (the directed-use law, not the
                // auto=1 the gather/hand-off tests run) and Queue.RemoveAt on failure —
                // which would present here exactly as an unexplained drop. The
                // log-and-fail stands; this note is the standing disclosure.
                var acceptedDrawn = _recs.Where(r => r.Accepted && r.DrawnIdx >= 0 &&
                    r.Candidates != null && r.DrawnIdx < r.Candidates.Count).ToList();
                var gUnexcusedDrops = 0;
                foreach (var r in acceptedDrawn)
                {
                    var w = r.Candidates[r.DrawnIdx];
                    // P2-4 per-decision scoping: a serving insertion excuses THIS
                    // decision's drop only if it lands within the documented window
                    // AFTER this decision's frame (N..N+1 — the hand-off insertion is
                    // synchronous with the seam decision, and the harness queue scan
                    // first observes it at frame N or N+1). A later same-identity
                    // insertion (the same winner re-drawn at a later decision) can no
                    // longer excuse an earlier unexplained drop.
                    var served = _queueInsertions.Any(ins => ins.AvatarId == r.CallerId &&
                        ins.Priority == (short)VMQueuePriority.Autonomous &&
                        ins.CalleeId == w.CalleeId && ins.ActionRoutineId == w.ActionNumber &&
                        ins.Arg0 == w.Param0 &&
                        ins.Frame >= r.Frame && ins.Frame - r.Frame <= MutationWindowFrames);
                    if (served) continue;
                    var testBhav = FindTestBhavId(w.CalleeId, w.ActionNumber);
                    // P2-3: the excusal window is the documented N..N+1 (the code's old
                    // N..N+2 was an off-by-one against the HarnessMutation rationale).
                    var cause = _harnessMutations
                        .Where(m => r.Frame >= m.Frame && r.Frame - m.Frame <= MutationWindowFrames)
                        .Select(m => m.Why).FirstOrDefault();
                    var excused = cause != null;
                    if (!excused) gUnexcusedDrops++;
                    gDrops.Add("frame" + r.Frame + " av" + r.CallerId + " winner obj" + w.CalleeId +
                        "#act" + w.ActionNumber + " (p" + w.Param0 + ") testBHAV=" + testBhav +
                        " -> hand-off re-test returned false" +
                        (excused
                            ? " after DOCUMENTED harness mutation '" + cause + "' (disclosed same-tick change)"
                            : " with NO documented same-tick cause — FAIL under (g)"));
                }
                _log("AUTOTEST freewillwin queue-insertions: " +
                    (gServing.Count == 0 ? "none served" : string.Join(" ; ", gServing)));
                if (gPushPath.Count > 0)
                {
                    _log("AUTOTEST freewillwin push-path insertions: " + string.Join(" ; ", gPushPath));
                    _disclosures.Add("g-queue-crosscheck: " + gPushPath.Count +
                        " Autonomous insertion(s) attributed to the tree-driven Push Interaction path " +
                        "(the engine's other Autonomous minter, outside the free-will hand-off law; logged above, not violations)");
                }
                if (gDrops.Count > 0)
                    _log("AUTOTEST freewillwin hand-off drops: " + string.Join(" ; ", gDrops));
                foreach (var v in gViolations)
                    _disclosures.Add("g-queue-crosscheck VIOLATION " + v);
                // (g) R250 HARD PASS: at least one SERVED winner per run (a queue insertion
                // matching the seam's winner identity at Priority Autonomous) is REQUIRED.
                // With gather filtering, the drawn winner must serve unless conditions
                // changed mid-tick — nearly impossible synchronously, and lawful only when
                // the change is a DOCUMENTED harness mutation (the narrow escape above);
                // any unexcused drop is a FAIL. (The R249 "[DISCLOSED-DOWNGRADE: serving
                // gap scoped]" path and its chair-'sit'-4107 residual are gone — the
                // per-candidate gather tests removed that failure class at the source.)
                var gServingAchieved = gServing.Count > 0;
                if (!gServingAchieved)
                {
                    _disclosures.Add("g-queue-crosscheck: NO serving winner this run (" +
                        acceptedDrawn.Count + " accepted drawn winner(s), " + gDrops.Count +
                        " hand-off drop(s)) — the hard pass condition failed");
                }
                bool gOk = gViolations.Count == 0 && gServingAchieved && gUnexcusedDrops == 0;
                Sub("g-queue-crosscheck", gOk,
                    (gServingAchieved
                        ? gServing.Count + " serving winner(s) matched (callee+ActionRoutine+Args[0]+Autonomous priority)"
                        : "no serving winner observed") +
                    "; " + gDrops.Count + " hand-off drop(s) (" + gUnexcusedDrops +
                    " unexcused — each is a FAIL)" +
                    "; " + gPushPath.Count + " push-path insertion(s) (disclosed)" +
                    "; " + gViolations.Count + " queue-content violation(s)" +
                    (acceptedDrawn.Count == 0 ? " [UNVERIFIABLE: no accepted drawn winner this run]" : ""));

                // (h) free-will OFF control: no subject decisions during the OFF sub-window
                Sub("h-freewill-off-gate", _offControlDone && _offControlFrames >= 120 && _offControlDecisions == 0,
                    "OFF window " + _offControlFrames + " frames, subject decisions=" + _offControlDecisions +
                    (_offControlDone ? "" : " (control window never ran)") +
                    (_offControlDecisions > 0 ? " — decisions leaked through the disabled gate" : ""));

                // (i) state restore
                Sub("i-state-restore", _restored, "motives/personality/free-will/settings restored in finally path");

                // (i2) attr18 restore contract (P1-2): the window pin (attr18=0) must be
                // undone by Restore — the restored value equals the PRE-PIN loaded original,
                // proving the snapshot stayed pristine (the old code pinned by mutating the
                // snapshot, which wrote 0 back permanently).
                Sub("i2-attr18-restore-proof", _attr18RestoreChecked && _attr18RestoreObserved == _av0Attr18Original,
                    "restored attr18=" + _attr18RestoreObserved + " vs pre-pin original=" + _av0Attr18Original +
                    ( !_attr18RestoreChecked ? " (restore never re-read attr18)" : ""));

                // (j) game-data untouched
                var nowHash = HashFile(_loadPathStatic);
                Sub("j-game-data-untouched", nowHash == _housePathHash,
                    "house file sha256 unchanged across the window");

                // optional PNG evidence: one decision-time capture of the lot
                if (multi.Count > 0) CaptureLot();

                foreach (var dsc in _disclosures) _log("AUTOTEST freewillwin DISCLOSURE " + dsc);
                AllPassed = _verdicts.Count > 0 && _verdicts.Values.All(v => v);
                VerdictReady = true;
                _log("AUTOTEST freewillwin VERDICT " + (AllPassed ? "PASS" : "FAIL") +
                    " sub-assertions=" + _verdicts.Count + " failed=" + _verdicts.Values.Count(v => !v));
            }
            catch (Exception e)
            {
                _log("AUTOTEST freewillwin evaluate EXC " + e.GetType().Name + " " + e.Message);
                AllPassed = false; VerdictReady = true;
            }
        }

        // ---- helpers ---------------------------------------------------------------------

        private static string HashFile(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return "missing:" + path;
                using (var sha = SHA256.Create())
                using (var fs = File.OpenRead(path))
                    return Convert.ToBase64String(sha.ComputeHash(fs));
            }
            catch (Exception e) { return "hash-exc:" + e.GetType().Name; }
        }

        /// <summary>
        /// Read-only census of the candidate-gather inputs (run once at arm): walks the
        /// autonomy set like the landed gather law and histograms where advertised TTAB
        /// entries die (weight gate, autonomy ceiling, flags, gender, visitor) — pinpoints
        /// a zero-candidate window WITHOUT instrumenting the engine. Read-only: no test
        /// trees run here.
        /// </summary>
        private static void CensusGatherInputs()
        {
            try
            {
                var autonomyLevel = _subject.GetPersonData(VMPersonDataVariable.AutonomyLevel);
                var gender = _subject.GetPersonData(VMPersonDataVariable.Gender);
                int objects = 0, withTtab = 0, entries = 0, wPos = 0, wAdmitted = 0,
                    f80 = 0, g1 = 0, g2 = 0, g3 = 0, flagged = 0;
                var samples = new List<string>();
                foreach (var obj in _vm.Context.ObjectQueries.WithAutonomy)
                {
                    objects++;
                    var tt = obj.TreeTable;
                    if (tt == null || tt.Interactions.Length == 0) continue;
                    withTtab++;
                    foreach (var e in tt.Interactions)
                    {
                        entries++;
                        var weight = (short)((ushort)e.AutonomyThreshold & 0xFFFF);
                        if (weight <= 0) continue;
                        wPos++;
                        if (autonomyLevel < weight) continue;
                        wAdmitted++;
                        if ((e.Flags & (TTABFlags)0x80) != 0) { f80++; continue; }
                        if ((gender & 1) != 0 && (e.Flags & (TTABFlags)0x400) == 0) { g1++; continue; }
                        if ((gender & 2) != 0 && (e.Flags & (TTABFlags)0x200) == 0) { g2++; continue; }
                        if ((gender & 3) == 0 && (e.Flags & (TTABFlags)0x40) != 0) { g3++; continue; }
                        if ((e.Flags & (TTABFlags)0x01000000) != 0) flagged++;
                        if (samples.Count < 12)
                            samples.Add("obj" + obj.ObjectID + "#tta" + e.TTAIndex + "/act" + e.ActionFunction +
                                " w=" + weight + " flags=0x" + ((int)e.Flags).ToString("X8") +
                                " attCode=" + e.AttenuationCode);
                    }
                }
                _log("AUTOTEST freewillwin gather-census objects=" + objects + " withTtab=" + withTtab +
                    " entries=" + entries + " weight>0=" + wPos + " weight<=attr" + autonomyLevel + "=" + wAdmitted +
                    " flags0x80=" + f80 + " gender&1!=0x400=" + g1 + " gender&2!=0x200=" + g2 +
                    " plain!=0x40=" + g3 + " autonomyFlagSet=" + flagged +
                    " subjectGender=" + gender +
                    " (admitted-after-static-gates=" + (wAdmitted - f80 - g1 - g2 - g3) + ")");
                if (samples.Count > 0)
                    _log("AUTOTEST freewillwin gather-census samples: " + string.Join(" | ", samples));
                if (wPos == 0)
                    _log("AUTOTEST freewillwin gather-census FINDING: every TTAB entry reads AutonomyThreshold<=0 — " +
                        "the advertise-weight port mapping or the corpus admits nothing");
            }
            catch (Exception e)
            {
                _log("AUTOTEST freewillwin gather-census EXC " + e.GetType().Name + " " + e.Message);
            }
        }

        /// <summary>
        /// Serve-window census (arm-time, read-only): the subject's stratum pool head is
        /// determined purely by module-order admission (the faithful heapsort's net effect
        /// is rotate-left-by-one, so post-sort[0..K-1] == gather[1..K] — the FIRST admitted
        /// entries), and motive pins cannot separate the Fun-advertised families (the adult
        /// Fun curve is flat above 75, so any Fun pin that kills the seating-family ads
        /// kills the person-person ads too). The ONE remaining legal separator is the ad
        /// ceiling: if the person-person 'act 8662' entries advertise at a strictly lower
        /// AutonomyThreshold weight than the seating-family pool head, an attr36 window
        /// admits the person-person family while dropping the head. This scan only REPORTS
        /// whether that window exists; the pin itself is staged between serve-pushes (see
        /// SampleTick) so the primary subject decision keeps the unpinned pool.
        /// </summary>
        private static void ScanServeWeights(short houseNumber)
        {
            try
            {
                var subjStratum = (houseNumber + _subject.ObjectID) & 3;
                int w8662 = int.MaxValue, wMinOther = int.MaxValue;
                var samples = new List<string>();
                foreach (var obj in _vm.Context.ObjectQueries.WithAutonomy)
                {
                    if (obj.Position == FSO.LotView.Model.LotTilePos.OUT_OF_WORLD) continue;
                    if (((houseNumber + obj.ObjectID) & 3) != subjStratum) continue;
                    var tt = obj.TreeTable;
                    if (tt == null) continue;
                    foreach (var e in tt.Interactions)
                    {
                        var w = (short)((ushort)e.AutonomyThreshold & 0xFFFF);
                        if (w <= 0) continue;
                        if ((e.Flags & (TTABFlags)0x80) != 0) continue;
                        if (samples.Count < 24)
                            samples.Add("obj" + obj.ObjectID + "#act" + e.ActionFunction + " w=" + w);
                        if (e.ActionFunction == 8662) { if (w < w8662) w8662 = w; }
                        else if (w < wMinOther) wMinOther = w;
                    }
                }
                _servePinAttr36 = (w8662 != int.MaxValue && w8662 > 0 && w8662 < wMinOther)
                    ? (short)w8662 : (short)-1;
                _log("AUTOTEST freewillwin serve-window census (stratum " + subjStratum + "):" +
                    " act8662_minWeight=" + (w8662 == int.MaxValue ? -1 : w8662) +
                    " minOtherWeight=" + (wMinOther == int.MaxValue ? -1 : wMinOther) +
                    " -> attr36 serve-pin " + (_servePinAttr36 < 0 ? "NOT APPLICABLE" : "= " + _servePinAttr36) +
                    " [" + string.Join(" | ", samples) + "]");
            }
            catch (Exception e)
            {
                _log("AUTOTEST freewillwin serve-window census EXC " + e.GetType().Name + " " + e.Message);
            }
        }

        /// <summary>One decision-time lot capture (SurveyDump GPU recipe) into ui-audit/r249.</summary>
        private static void CaptureLot()
        {
            try
            {
                var ui = GameFacade.Screens;
                var gd = GameFacade.GraphicsDevice;
                if (ui == null || gd == null) return;
                int w = gd.Viewport.Width, h = gd.Viewport.Height;
                using (var rt = FSO.Common.Utils.PPXDepthEngine.CreateRenderTarget(gd, 1, 0,
                    Microsoft.Xna.Framework.Graphics.SurfaceFormat.Color, w, h,
                    Microsoft.Xna.Framework.Graphics.DepthFormat.None))
                {
                    gd.SetRenderTarget(rt);
                    gd.Clear(new Microsoft.Xna.Framework.Color(0x72, 0x72, 0x72, 0xFF));
                    ui.SpriteBatch.UIBegin(Microsoft.Xna.Framework.Graphics.BlendState.AlphaBlend,
                        Microsoft.Xna.Framework.Graphics.SpriteSortMode.Immediate);
                    try { ui.Draw(ui.SpriteBatch); } finally { ui.SpriteBatch.End(); }
                    gd.SetRenderTarget(null);
                    var dir = Path.Combine(FSO.Common.FSOEnvironment.UserDir, "ui-audit", "r249");
                    Directory.CreateDirectory(dir);
                    var path = Path.Combine(dir, "freewillwin-decision.png");
                    using (var fs = File.Create(path))
                        rt.SaveAsPng(fs, rt.Width, rt.Height);
                    _log("AUTOTEST freewillwin capture " + w + "x" + h + " -> " + path);
                }
            }
            catch (Exception e)
            {
                _log("AUTOTEST freewillwin capture EXC " + e.GetType().Name + " " + e.Message);
            }
        }
    }
}
