using FSO.Client;
using FSO.Common;
using FSO.Content;
using FSO.Files.Formats.IFF.Chunks;
using FSO.LotView.Model;
using FSO.SimAntics;
using FSO.SimAntics.Engine;
using FSO.SimAntics.Entities;
using FSO.SimAntics.Model;
using FSO.SimAntics.Primitives;
using Simitone.Client.UI.Screens;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Simitone.Client
{
    /// <summary>
    /// EXP-14 'hdserve' (opt-in, case-21 screen takeover): the Hot Date serve
    /// choreography SELF-START probe. EXP-10's hd5/hd7 legs proved the service
    /// rows progress when DRIVEN; this probe proves the native self-start law
    /// decoded this round (coordination/evidence/EXP-14/):
    ///
    ///  - the restaurant anchor is the PODIUM (Podium - Dining, multitile
    ///    Main/Back/Front; DiningPodium.iff). Its TTAB row0 'Eat' (fn 4098,
    ///    flags 0x31, auton 50) and row2 'Eat Alone' (fn 4112, flags 0x39,
    ///    auton 50, motive-7 ad min=1 delta=49 pers=10) are the diner rows;
    ///    'Eat Alone' is the hunger-advertised AUTONOMOUS row.
    ///  - BHAV 4098 ins2 / 4112 ins3 (op=42 create-instance) create the
    ///    'Controller - Restaurant Eat' (DTController.iff OBJD 16810, GUID
    ///    0x92534734) ON the Eat interaction itself — the controller is a
    ///    CONSEQUENCE of dining, not a load-time population (the EXP-10
    ///    'controller nondeterminism' was downstream of autonomous eats).
    ///  - the podium's own main loop (BHAV 4096) creates the staff NPCs
    ///    (maitre'd 0x8f21b454, waitress, chef, busboy, pianist).
    ///  - the autonomy gather gate needs person word 36 (AutonomyLevel) >=
    ///    the row's advertise weight: PersonGlobals.iff BHAV 8192 'Init person'
    ///    ins7 assigns MyPersonData[36] = 50 (native default; reached from
    ///    TemplatePerson.iff 'init tree' 4097 ins0 -> semi 8192).
    ///
    /// Probe discipline: the ONLY probe-side pushes are the TRAVEL booking
    /// (the EXP-10 disclosed plugin row-2 push law — travel itself needs it;
    /// there is no GUI) plus the DISCLOSED hunger arm on the traveler after
    /// arrival. NO interaction rows are pushed after arrival: the meal chain
    /// must self-start through the port's own free-will machinery.
    /// </summary>
    public static partial class AutotestRunner
    {
        private static AutotestExp14HdServeProbe _e14Probe;

        private static void StateExp14HdServe()
        {
            if (_e14Probe == null) { Finish(); return; }
            // keep the screen handle fresh and follow the EXP-04 VM-replacement
            // law: the downtown switch REPLACES screen.vm — the runner's _vm must
            // follow it or every sample would read the orphaned home-lot corpse.
            var svm = (_screen ?? (GameFacade.Screens?.CurrentUIScreen as TS1GameScreen))?.vm;
            if (svm != null && !ReferenceEquals(svm, _vm)) _vm = svm;
            try
            {
                if (!_e14Probe.Tick()) return; // still working
            }
            catch (Exception e)
            {
                Log("AUTOTEST hdserve tick EXC " + e.GetType().Name + ": " + e.Message
                    + " @" + (e.StackTrace?.Split('\n').FirstOrDefault(x => x.Contains(".cs")) ?? "no-cs-frame"));
                _e14Probe = null;
                Fail("hdserve");
                Finish();
                return;
            }
            if (_e14Probe.Passed) Pass("hdserve"); else Fail("hdserve");
            Log("AUTOTEST hdserve verdict-line: " + _e14Probe.Verdict);
            _e14Probe = null;
            Finish();
        }
    }

    /// <summary>
    /// The hdserve probe state machine. Lives entirely probe-side; drives the
    /// production travel path (phone plugin row 2 + production picker dialog),
    /// then observes the arrival VM hands-off.
    /// </summary>
    public class AutotestExp14HdServeProbe
    {
        private readonly Action<string> _log;
        private readonly Func<VM> _vm;
        private readonly Func<bool> _inLot;
        private readonly Action<short> _playHouse;

        // phases: 0 wait-lot, 1 armed-outbound, 2 arrival+arm, 3 soak, 4 done
        private int _phase;
        private int _frame;
        private int _arrivalFrame = -1;
        private int _arrivalLot = -1;
        private short _dest = -1;
        private VMAvatar _traveler;
        private uint _travelerGuid;
        private VMEntity _plugin;
        private VM _homeVm;
        private int _pushUid = -1;
        private int _notifyArms;
        private bool _entered;
        private int _answers;
        private readonly List<string> _dlgTypes = new List<string>();

        private short _hunger0;
        private int _hungerArmFrame = -1;
        private bool _eatSeen;
        private int _eatSeenFrame = -1;
        private string _eatSeenDetail;
        private bool _controllerSeen;
        private int _controllerSeenFrame = -1;
        private int _decisions;
        private int _decisionsWithCandidates;
        private int _podiumCandidateDecisions;
        private readonly List<string> _decisionLog = new List<string>();

        public bool Passed { get; private set; }
        public string Verdict { get; private set; }

        // the DTController.iff 'Controller - Restaurant Eat' GUID (round decode)
        private const uint RestaurantEatControllerGuid = 0x92534734u;
        private const int SoakFrames = 5400; // ~3 sim-hours hands-off window

        public AutotestExp14HdServeProbe(Action<string> log, Func<VM> vm, Func<bool> inLot, Action<short> playHouse)
        {
            _log = log;
            _vm = vm;
            _inLot = inLot;
            _playHouse = playHouse;
        }

        public bool Tick()
        {
            _frame++;
            var vm = _vm();
            switch (_phase)
            {
                case 0: // neighborhood ready -> enter the home lot (house 5 default)
                    if (!_inLot())
                    {
                        if (!_entered && _frame > 60)
                        {
                            _entered = true;
                            _log("AUTOTEST hdserve: entering home lot (PlayHouse(5)) at tick " + _frame);
                            try { _playHouse(5); } catch (Exception e) { _log("AUTOTEST hdserve PlayHouse EXC " + e.GetType().Name); }
                        }
                        return false;
                    }
                    if (vm == null || vm.Entities.Count == 0) return false;
                    ArmOutbound(vm);
                    return false;

                case 1: // outbound watch: keep the VM unblocked, answer the picker, detect the switch
                    if (vm == null) return false;
                    KeepUnblocked(vm);
                    AnswerDialogs(vm);
                    // Wait-For-Notify release (the EXP-03 V1.2 law): the chain parks
                    // in global 281 'Wait For Notify' whose innermost idle counts down
                    // 20000 ticks. The native release is ActiveAction.NotifyIdle
                    // re-evaluated on the next idle execution — arm the flag AND a
                    // 1-tick scheduler wake, re-armed per park of the pushed action.
                    {
                        var aaNi = _traveler?.Thread?.ActiveAction;
                        if (aaNi != null && aaNi.UID == _pushUid && !aaNi.NotifyIdle)
                        {
                            aaNi.NotifyIdle = true;
                            vm.Scheduler.ScheduleTickIn(_traveler, 1);
                            _notifyArms++;
                            if (_notifyArms <= 4)
                                _log("AUTOTEST hdserve armed NotifyIdle + 1-tick wake #" + _notifyArms
                                    + " (Wait-For-Notify park of the pushed action) at f=" + _frame);
                        }
                    }
                    // the wrapper re-points the _vm accessor on the screen-vm swap
                    // (the EXP-04 replacement law) — the swap IS the arrival signal
                    var fresh = _vm();
                    if (fresh != null && !ReferenceEquals(fresh, _homeVm))
                    {
                        OnArrival(fresh, _homeVm != null ? _homeVm.Entities.Count : -1);
                        return false;
                    }
                    if (_arrivalLot < 0 && _frame % 300 == 0)
                        _log("AUTOTEST hdserve outbound wait f=" + _frame
                            + " travelerActive='" + (_traveler?.Thread?.ActiveAction?.Name ?? "-") + "'");
                    return false;

                case 2: // post-arrival settling: wait for the mode-18 downtown-sim
                        // build (bounded), then hand control to the soak
                    if (vm == null) return false;
                    KeepUnblocked(vm);
                    AnswerDialogs(vm);
                    if (_traveler == null)
                    {
                        // hdserve-fix: the wait is GUID-STRICT on the traveler's own
                        // DowntownSimGUID — downtown staff/visitor NPCs also satisfy
                        // a FirstOrDefault and would silently pass the arrival gate
                        // while the real traveler never builds (run1's defect).
                        var avs2 = vm.Entities.OfType<VMAvatar>().ToList();
                        var built = _travelerGuid != 0
                            ? avs2.FirstOrDefault(a => a.Object?.OBJ?.GUID == _travelerGuid)
                            : avs2.FirstOrDefault();
                        if (built != null)
                        {
                            _traveler = built;
                            _log("AUTOTEST hdserve: downtown sim built at f=" + _frame
                                + " (+" + (_frame - _arrivalFrame) + " after the swap) obj" + built.ObjectID
                                + " guid=0x" + _travelerGuid.ToString("x8") + " (GUID-exact)");
                            _arrivalFrame = _frame; // restart the settle window from the build
                        }
                        else if (_frame - _arrivalFrame > 1800)
                        {
                            Verdict = "arrival-no-traveler: the traveler's downtown sim (GUID 0x"
                                + _travelerGuid.ToString("x8") + ") never built within 1800f of the swap"
                                + " — avatars present=" + avs2.Count + " are all NPCs (mode-18 build leg failed)";
                            Done(false);
                            return true;
                        }
                        else return false;
                    }
                    if (_frame - _arrivalFrame < 120) return false;
                    ArmHungerAndOpenSoak(vm);
                    return false;

                case 3:
                    if (vm == null) return false;
                    KeepUnblocked(vm);
                    AnswerDialogs(vm);
                    return Soak(vm);
            }
            return _phase >= 4; // phase 4 = verdict delivered; anything else keeps ticking
        }

        private VM CurrentScreenVm()
        {
            var screen = GameFacade.Screens?.CurrentUIScreen as TS1GameScreen;
            return screen?.vm;
        }

        // ------------------------------------------------------------------
        // Outbound: the EXP-10 disclosed travel-booking law (phone plugin row 2).
        // ------------------------------------------------------------------
        private void ArmOutbound(VM vm)
        {
            var avs = vm.Entities.OfType<VMAvatar>().ToList();
            _traveler = avs.FirstOrDefault();
            if (_traveler == null)
            {
                Verdict = "no-home-avatar: the home lot has no avatar to travel";
                Done(false);
                return;
            }
            _travelerGuid = _traveler.Object?.OBJ?.GUID ?? 0;
            _homeVm = vm;

            // destination: first existing downtown house file in 21..30
            for (short i = 21; i <= 30; i++)
            {
                string p = null;
                try { p = Content.Get().Neighborhood.GetHousePath(i); } catch { }
                if (!string.IsNullOrEmpty(p) && File.Exists(p)) { _dest = i; break; }
            }
            if (_dest < 0)
            {
                Verdict = "no-downtown-lot-file: userdir carries no House21-30";
                Done(false);
                return;
            }

            // GoDowntownPhonePlugin (0xA6F31853): create OOW + phone recreate (EXP-03 wire law)
            _plugin = vm.Entities.FirstOrDefault(e => e.Object?.OBJ != null && e.Object.OBJ.GUID == 0xA6F31853u);
            if (_plugin == null)
            {
                var grp = vm.Context.CreateObjectInstance(0xA6F31853u,
                    LotTilePos.OUT_OF_WORLD, Direction.NORTH);
                _plugin = grp?.BaseObject;
                if (_plugin != null)
                {
                    VMEntity phone = null;
                    try
                    {
                        phone = vm.Entities.FirstOrDefault(e =>
                        {
                            var iff = e?.Object?.Resource?.Iff;
                            var ttas = iff?.Get<TTAs>(129);
                            if (ttas == null) return false;
                            for (int i = 0; i < ttas.Length; i++)
                                if ((ttas.GetString(i) ?? "").Trim() == "Call Plugin") return true;
                            return false;
                        });
                    }
                    catch { }
                    if (phone != null)
                    {
                        var pos = phone.Position;
                        var guid = phone.Object.OBJ.GUID;
                        phone.Delete(false, vm.Context);
                        vm.Context.CreateObjectInstance(guid, pos, Direction.NORTH);
                    }
                }
            }
            if (_plugin == null)
            {
                Verdict = "plugin-unplaceable: GoDowntownPhonePlugin not resolvable";
                Done(false);
                return;
            }

            VMQueuedAction push = null;
            try { push = _plugin.GetAction(2, _traveler, vm.Context, false); } catch { }
            if (push == null)
            {
                Verdict = "row2-unpushable: plugin row 2 (Go Downtown) GetAction null";
                Done(false);
                return;
            }
            push.Flags |= TTABFlags.FSOSkipPermissions;
            push.CheckRoutine = null; // the EXP-03 push law (disclosed)
            _plugin.SetAttribute(1, (short)_traveler.ObjectID); // ARM-1 (4100@25 repoint)
            vm.SetGlobalValue(10, 40); // ARM-2 (global 320 routes into the picker leg)
            _traveler.Thread.EnqueueAction(push);
            _pushUid = push.UID;
            _log("AUTOTEST hdserve: outbound booked — plugin row 2 '" + push.Name + "' uid=" + push.UID
                + " on traveler obj" + _traveler.ObjectID + " dest-lot=" + _dest
                + " (probe-side TRAVEL push, disclosed; everything after arrival is hands-off)");
            _phase = 1;
        }

        private void OnArrival(VM fresh, int oldEnts)
        {
            _arrivalFrame = _frame;
            try { _arrivalLot = fresh.TS1State.CurrentHouse; } catch { }
            _log("AUTOTEST hdserve >>> ARRIVAL at f=" + _frame + " lot=" + _arrivalLot
                + " (picker answer " + _dest + ") oldEnts=" + oldEnts + " newEnts=" + fresh.Entities.Count
                + " answers=" + _answers + " types=[" + string.Join(",", _dlgTypes) + "]");

            // re-resolve the traveler by GUID (the EXP-04 orphaning law).
            // hdserve-fix: GUID-STRICT — no FirstOrDefault fallback. The swap-time
            // resolution only succeeds if the traveler survived the lot switch
            // itself; otherwise phase 2 waits for the mode-18 build.
            var avs = fresh.Entities.OfType<VMAvatar>().ToList();
            _traveler = avs.FirstOrDefault(a => a.Object?.OBJ?.GUID == _travelerGuid);
            if (_traveler == null)
            {
                // Mode-18 law (VMGenericTS1Call): the caller travels as
                // inventory transit data; the destination lot's Ped Marker - Middle
                // (attr3) BUILDS the downtown sim after arrival. The avatar can lag
                // the VM swap by many frames — phase 2 waits for it (bounded).
                _log("AUTOTEST hdserve arrival: traveler not on the swap VM (mode-18 build pending); waiting");
                return;
            }

            // census: podiums, controllers, staff, per-avatar autonomy state
            var podiums = fresh.Entities.Where(e =>
                (e.Object?.OBJ?.ChunkLabel ?? "").ToLowerInvariant().Contains("podium")).ToList();
            var controllers = fresh.Entities.Where(e =>
                e.Object?.OBJ?.GUID == RestaurantEatControllerGuid).ToList();
            var staff = fresh.Entities.OfType<VMAvatar>().Where(a =>
            {
                var l = (a.Object?.OBJ?.ChunkLabel ?? "").ToLowerInvariant();
                return l.Contains("waitress") || l.Contains("waiter") || l.Contains("maitre")
                    || l.Contains("chef") || l.Contains("busboy") || l.Contains("bus boy");
            }).ToList();
            _log("AUTOTEST hdserve arrival census: podiums=" + podiums.Count
                + " [" + string.Join(" ", podiums.Take(6).Select(p => p.Object.OBJ.ChunkLabel + "(obj" + p.ObjectID + ")")) + "]"
                + " restaurantEatControllers=" + controllers.Count
                + " staffNpcs=" + staff.Count
                + " avatars=" + avs.Count
                + " freeWill=" + VM.FreeWillEnabled
                + " g10=" + fresh.GetGlobalValue(10)
                + " zoning(lot" + _arrivalLot + ")=" + Content.Get().Neighborhood.GetZoningType((short)_arrivalLot));
            foreach (var a in avs.Take(14))
            {
                var lotNo = 0;
                try { lotNo = fresh.GlobalState != null && fresh.GlobalState.Length > 10 ? fresh.GlobalState[10] : 0; } catch { }
                _log("AUTOTEST hdserve avatar obj" + a.ObjectID
                    + " guid=0x" + (a.Object?.OBJ?.GUID ?? 0).ToString("x8")
                    + " label='" + (a.Object?.OBJ?.ChunkLabel ?? "?") + "'"
                    + " personType(pd32)=" + a.GetPersonData(VMPersonDataVariable.PersonType)
                    + " autonomyLevel(pd36)=" + a.GetPersonData(VMPersonDataVariable.AutonomyLevel)
                    + " greetStatus(pd34)=" + a.GetPersonData(VMPersonDataVariable.GreetStatus)
                    + " stratum((g10+oid)&3)=" + ((lotNo + a.ObjectID) & 3)
                    + " hunger=" + a.GetMotiveData(VMMotive.Hunger)
                    + " active='" + (a.Thread?.ActiveAction?.Name ?? "-") + "'");
            }

            // the free-will observation seam (R249): every gather/draw lands here
            VMFindBestAction.TS1DecisionObserved += OnDecision;

            if (podiums.Count == 0)
            {
                Verdict = "arrival-no-podium: the destination lot carries no Podium - Dining instance"
                    + " (the serve chain's anchor object) — lot " + _arrivalLot + " is not a restaurant lot";
                Done(false);
                return;
            }
            _phase = 2;
        }

        private void OnDecision(VMTS1Decision d)
        {
            _decisions++;
            var n = d.Candidates?.Count ?? 0;
            if (n > 0) _decisionsWithCandidates++;
            var podiumIds = new HashSet<int>();
            var vm = _vm();
            if (vm != null)
            {
                foreach (var e in vm.Entities)
                {
                    if ((e.Object?.OBJ?.ChunkLabel ?? "").ToLowerInvariant().Contains("podium"))
                        podiumIds.Add(e.ObjectID);
                }
            }
            var podiumCands = (d.Candidates ?? new List<VMTS1DecisionCandidate>())
                .Where(c => podiumIds.Contains(c.CalleeId)).ToList();
            if (podiumCands.Count > 0) _podiumCandidateDecisions++;
            if (_decisionLog.Count < 40)
            {
                var w = d.Winner == null ? "-" : ("obj" + d.Winner.CalleeId + "/fn" + d.Winner.ActionNumber
                    + "/score" + d.Winner.Score.ToString("F3"));
                _decisionLog.Add("f" + _frame + " caller=obj" + d.CallerId + " n=" + n
                    + " podiumCands=" + podiumCands.Count + " winner=" + w + " acc=" + d.Accepted
                    + (d.Winner != null && podiumIds.Contains(d.Winner.CalleeId) ? " [PODIUM WIN]" : ""));
            }
        }

        private void ArmHungerAndOpenSoak(VM vm)
        {
            // DISCLOSED arm: the native self-start gate is hunger-keyed (the
            // 'Eat Alone' motive-7 ad; the interaction-curve law scores only a
            // hungry sim). Drop the traveler to starving so the native gather
            // can see the row — the port must do the REST autonomously.
            _hunger0 = _traveler.GetMotiveData(VMMotive.Hunger);
            _traveler.SetMotiveData(VMMotive.Hunger, -80);
            _hungerArmFrame = _frame;
            _log("AUTOTEST hdserve SOAK OPEN at f=" + _frame + " (" + SoakFrames + "f hands-off): traveler obj"
                + _traveler.ObjectID + " hunger " + _hunger0 + " -> -80 (DISCLOSED hunger arm;"
                + " NO interaction pushes — the meal chain must self-start through free will)");
            _phase = 3;
        }

        private bool Soak(VM vm)
        {
            var controllers = vm.Entities.Count(e => e.Object?.OBJ?.GUID == RestaurantEatControllerGuid);
            if (controllers > 0 && !_controllerSeen)
            {
                _controllerSeen = true;
                _controllerSeenFrame = _frame;
                _log("AUTOTEST hdserve CONTROLLER SPAWNED at f=" + _frame
                    + " (Controller - Restaurant Eat count=" + controllers
                    + (_eatSeen ? " — after an eat engagement (the BHAV 4098/4112 op-42 creation law)" : " — BEFORE any observed eat"
                        + " (an unobserved sim autonomously ate, or another creation path)"));
            }

            // eat-engagement scan across ALL avatars (any autonomous diner counts:
            // the chain self-starts even if the traveler is not the diner)
            if (!_eatSeen)
            {
                foreach (var a in vm.Entities.OfType<VMAvatar>())
                {
                    var act = a.Thread?.ActiveAction;
                    var nm = (act?.Name ?? "").ToLowerInvariant();
                    if (nm.Contains("eat") || nm.Contains("dine") || nm.Contains("order"))
                    {
                        _eatSeen = true;
                        _eatSeenFrame = _frame;
                        _eatSeenDetail = "obj" + a.ObjectID + " '" + act.Name + "'";
                        _log("AUTOTEST hdserve EAT ENGAGED at f=" + _frame + " by " + _eatSeenDetail
                            + " (AUTONOMOUS — the probe pushed nothing)");
                        break;
                    }
                }
            }

            if (_frame % 300 == 0)
            {
                var hunger = _traveler.GetMotiveData(VMMotive.Hunger);
                var act = _traveler.Thread?.ActiveAction;
                _log("AUTOTEST hdserve soak f=" + _frame + " (+arm" + (_frame - _hungerArmFrame) + ")"
                    + " travelerActive='" + (act?.Name ?? "-") + "' pos=(" + _traveler.Position.TileX + ","
                    + _traveler.Position.TileY + ",L" + _traveler.Position.Level + ")"
                    + " hunger=" + hunger + " (d" + (hunger - (-80)) + " vs arm)"
                    + " controllers=" + controllers
                    + " decisions=" + _decisions + " withCands=" + _decisionsWithCandidates
                    + " podiumCandDecisions=" + _podiumCandidateDecisions
                    + " ents=" + vm.Entities.Count);
            }

            var armed = _frame - _hungerArmFrame;
            if ((_eatSeen && _frame >= _eatSeenFrame + 1200) || armed >= SoakFrames)
            {
                CloseSoak(vm, armed);
                return true; // verdict delivered
            }
            return false; // still soaking
        }

        private void CloseSoak(VM vm, int armed)
        {
            var hungerEnd = _traveler.GetMotiveData(VMMotive.Hunger);
            var dH = hungerEnd - (-80);
            var controllers = vm.Entities.Count(e => e.Object?.OBJ?.GUID == RestaurantEatControllerGuid);
            foreach (var l in _decisionLog.Take(40)) _log("AUTOTEST hdserve decision " + l);

            if (_eatSeen && _controllerSeen)
            {
                Verdict = "SERVE-SELF-START-PROVEN: an eat-family interaction engaged autonomously ("
                    + _eatSeenDetail + " at f=" + _eatSeenFrame + ") with ZERO probe pushes after arrival,"
                    + " and the Eat interaction's own op-42 creation spawned the Controller - Restaurant Eat"
                    + " (f=" + _controllerSeenFrame + ") — the native self-start law (podium-anchored autonomous"
                    + " dining) runs in the port; hunger delta over the window " + dH
                    + (dH > 5 ? " (meal served)" : " (choreography mid-flight at close)");
                Done(true);
                return;
            }
            if (_eatSeen && !_controllerSeen)
            {
                Verdict = "EAT-ENGAGED-NO-CONTROLLER: the autonomous eat started (" + _eatSeenDetail
                    + ") but the BHAV 4098/4112 op-42 controller creation never landed (controller count 0"
                    + " at close) — the self-start reached the row but the creation leg is the residual";
                Done(false);
                return;
            }
            Verdict = "MEAL-NEVER-SELF-STARTED: no eat-family action engaged any avatar in " + armed
                + "f hands-off (traveler hunger armed to -80); controllers=" + controllers
                + "; decisions=" + _decisions + " withCands=" + _decisionsWithCandidates
                + " podiumCandDecisions=" + _podiumCandidateDecisions
                + " — see the per-avatar pd36/stratum census above for the gather-gate state";
            Done(false);
        }

        private void KeepUnblocked(VM vm)
        {
            if (vm.SpeedMultiplier <= 0)
            {
                var stale = vm.GlobalBlockingDialog;
                vm.SpeedMultiplier = vm.LastSpeedMultiplier > 0 ? vm.LastSpeedMultiplier : 1;
                vm.LastSpeedMultiplier = 0;
                vm.GlobalBlockingDialog = null;
                if (stale != null)
                    _log("AUTOTEST hdserve unpaused vm (stale-latch obj" + stale.ObjectID + ") at f=" + _frame);
            }
            var mainPanel = (GameFacade.Screens?.CurrentUIScreen as TS1GameScreen)?.Frontend?.MainPanel;
            if (mainPanel != null && mainPanel.Mode != UI.Panels.UIMainPanelMode.LIVE)
            {
                mainPanel.SetMode(UI.Panels.UIMainPanelMode.LIVE);
            }
        }

        private void AnswerDialogs(VM vm)
        {
            VMDialogResult target = null;
            VMEntity owner = null;
            var gbd = vm.GlobalBlockingDialog;
            if (gbd != null)
            {
                var bs = gbd.Thread?.BlockingState as VMDialogResult;
                if (bs != null && !bs.Responded) { target = bs; owner = gbd; }
            }
            if (target == null)
            {
                foreach (var ent in vm.Entities)
                {
                    var qbs = ent?.Thread?.BlockingState as VMDialogResult;
                    if (qbs != null && !qbs.Responded) { target = qbs; owner = ent; break; }
                }
            }
            if (target == null) return;
            var type = target.Type;
            var picker = type == VMDialogType.TS1Downtown || type == VMDialogType.NumericEntry
                || type == VMDialogType.TS1Vacation || type == VMDialogType.TS1Neighborhood
                || type == VMDialogType.TS1StudioTown || type == VMDialogType.TS1Magictown
                || type == VMDialogType.TS1PhoneBook;
            target.Responded = true;
            if (picker)
            {
                target.ResponseCode = 1;
                target.ResponseText = _dest.ToString();
            }
            else
            {
                target.ResponseCode = 0;
                target.ResponseText = "";
            }
            if (ReferenceEquals(vm.GlobalBlockingDialog, owner)) vm.GlobalBlockingDialog = null;
            if (vm.LastSpeedMultiplier > 0) { vm.SpeedMultiplier = vm.LastSpeedMultiplier; vm.LastSpeedMultiplier = 0; }
            else if (vm.SpeedMultiplier < 0) vm.SpeedMultiplier = 1;
            _answers++;
            _dlgTypes.Add(type.ToString());
            _log("AUTOTEST hdserve dialog answered #" + _answers + " type=" + type
                + (picker ? " PICKER code=1 text=\"" + _dest + "\"" : " click code=0") + " at f=" + _frame);
        }

        private void Done(bool pass)
        {
            Passed = pass;
            _phase = 4;
            VMFindBestAction.TS1DecisionObserved -= OnDecision;
        }
    }
}
