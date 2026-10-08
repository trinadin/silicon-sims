using System.Collections.Generic;

namespace FSO.SimAntics.Engine
{
    public class VMScheduler
    {
        // DEFECT-2 (ENG-01, run-13): the HD-window freeze spins the MAIN thread
        // outside the interpreter's instruction paths (the NextInstruction
        // watchdog was silent). Breadcrumb + stall-dump: the main thread marks
        // each risky region and pulses a heartbeat; a background timer writes
        // the crumb to stdout when the heartbeat stalls, so a NEVER-RETURNING
        // call still names its region + entity in the gate log (NSUnbufferedIO).
        public static volatile string Defect2Crumb = "init";
        private static long _defect2HB;
        private static System.Threading.Timer _defect2Timer;
        public static void Defect2Pulse() { System.Threading.Interlocked.Increment(ref _defect2HB); }
        public static void Defect2Mark(string s) { Defect2Crumb = s; }

        // ENG-02 (2026-09-21): the archundo-probe "stall" (ent=307 hb=924, 5/5) has a
        // candidate benign explanation — the VM is intentionally PARKED (SpeedMultiplier
        // <= 0: UI-26 focus suspend, the UIMainPanel non-LIVE park, or a TS1 blocking
        // dialog), which freezes the heartbeat forever without any thread being stuck.
        // Snapshot the live VM state on every InternalTick (BeginTick runs before the
        // speed gate) so the stall print can distinguish PAUSED from STUCK.
        public static int Defect2SnapSpeed;
        public static bool Defect2SnapReady;
        public static bool Defect2SnapGbd;
        public static bool Defect2SnapFocusSuspended;
        public static uint Defect2SnapTickID;
        public static int Defect2SnapClockMinutes;
        public static void Defect2Snap(VM vm, uint tickID)
        {
            Defect2SnapSpeed = vm.SpeedMultiplier;
            Defect2SnapReady = vm.Ready;
            Defect2SnapGbd = vm.GlobalBlockingDialog != null;
            Defect2SnapFocusSuspended = vm.FocusSuspended;
            Defect2SnapTickID = tickID;
            Defect2SnapClockMinutes = vm.Context.Clock.Hours * 60 + vm.Context.Clock.Minutes;
        }
        private static void Defect2Init()
        {
            if (_defect2Timer != null) return;
            long last = -1; int stalled = 0;
            _defect2Timer = new System.Threading.Timer(_ =>
            {
                long h = System.Threading.Interlocked.Read(ref _defect2HB);
                if (h == last)
                {
                    if (++stalled >= 3)
                    {
                        try
                        {
                            string pause = (Defect2SnapSpeed <= 0)
                                ? " VM-PAUSED(NOT-STUCK) speed=" + Defect2SnapSpeed : "";
                            Console.Out.WriteLine("[DEFECT-2 stall] crumb='" + Defect2Crumb + "' hb=" + h
                                + pause
                                + " [spd=" + Defect2SnapSpeed
                                + " ready=" + Defect2SnapReady + " gbd=" + Defect2SnapGbd
                                + " focus=" + Defect2SnapFocusSuspended + " tick=" + Defect2SnapTickID
                                + " clock=" + (Defect2SnapClockMinutes / 60) + ":" + (Defect2SnapClockMinutes % 60).ToString("D2")
                                + "] at " + System.DateTime.UtcNow.ToString("o"));
                            Console.Out.Flush();
                        } catch { }
                    }
                }
                else { stalled = 0; last = h; }
            }, null, 5000, 5000);
        }

        private VM vm;
        private Dictionary<uint, List<VMEntity>> TickSchedule = new Dictionary<uint, List<VMEntity>>();
        private List<VMEntity> TickThisFrame;
        public HashSet<VMEntity> PendingDeletion = new HashSet<VMEntity>();
        public uint CurrentTickID;
        public short CurrentObjectID;
        public bool RunningNow;

        public VMScheduler(VM vm)
        {
            this.vm = vm;
        }

        public void ScheduleTickIn(VMEntity ent, uint delay)
        {
            if (delay > 1 && ent.RunEveryFrame()) delay = 1;
            if (VM.UseWorld) ent.WorldUI.IdleFrames = (int)delay;
            ScheduleTick(ent, CurrentTickID + delay);
        }

        public void ScheduleTick(VMEntity ent, uint tick)
        {
            if (ent.Dead) return;
            List<VMEntity> targEnts;
            if (!TickSchedule.TryGetValue(tick, out targEnts))
            {
                targEnts = new List<VMEntity>();
                TickSchedule[tick] = targEnts;
            }
            ent.Thread.ScheduleIdleEnd = tick;
            VM.AddToObjList(targEnts, ent);
        }

        public void ScheduleCurrentTick(VMEntity ent)
        {
            ent.Thread.ScheduleIdleEnd = CurrentTickID;
            VM.AddToObjList(TickThisFrame, ent);
        }

        public void DescheduleTick(VMEntity ent)
        {
            //on delete or interrupt.
            if (ent.Thread != null && ent.Thread.ScheduleIdleEnd > CurrentTickID)
            {
                TickSchedule[ent.Thread.ScheduleIdleEnd].Remove(ent);
            }
        }

        public void RescheduleInterrupt(VMEntity ent)
        {
            //on interrupt.
            if (ent.Thread == null || ent.Thread.ScheduleIdleEnd == CurrentTickID || ent.Thread.ScheduleIdleEnd == 0) return;
            DescheduleTick(ent);
            if (ent.ObjectID > CurrentObjectID && TickThisFrame != null) ScheduleCurrentTick(ent);
            else ScheduleTickIn(ent, 1);
        }

        public void BeginTick(uint tickID)
        {
            Defect2Snap(vm, tickID); // ENG-02: refresh the pause/stuck discriminator every InternalTick
            if (CurrentTickID == 0)
            {
                //if we were on tick 0 it's likely we just resynced. Migrate ticks to the the correct tick id.
                List<VMEntity> firstTick;
                if (TickSchedule.TryGetValue(1, out firstTick))
                {
                    TickSchedule[tickID] = firstTick; //new objects are queued on next tick.
                    if (tickID != 1) TickSchedule.Remove(1);
                }
            }
            CurrentTickID = tickID;
        }

        public void RunTick()
        {
            if (vm.Aborting) return;
            Defect2Init();
            Defect2Mark("sched pass begin");
            Defect2Pulse();
            RunningNow = true;
            if (TickSchedule.TryGetValue(CurrentTickID, out TickThisFrame))
            {
                //Console.WriteLine(TickThisFrame.Count + " entities ticked out of " + Entities.Count);
                for (int i = 0; i < TickThisFrame.Count; i++)
                {
                    var ent = TickThisFrame[i];
                    CurrentObjectID = ent.ObjectID;
                    Defect2Mark("sched ent=" + ent.ObjectID + " " + (ent.Object?.Resource?.MainIff?.Filename ?? "?"));
                    ent.Tick();
                    Defect2Pulse();
                }
                TickSchedule.Remove(CurrentTickID);
            }

            vm.Context.RandomSeed += (ulong)vm.Entities.Count; // some "entropy" based on the number of entities present. forces a more strict sync
            CurrentObjectID = short.MaxValue;
            RunningNow = false;

            //delete all objets that were pending deletion
            foreach (var obj in PendingDeletion)
            {
                obj.Delete(false, vm.Context);
            }
            PendingDeletion.Clear();
        }

        public void Delete(VMEntity obj)
        {
            PendingDeletion.Add(obj);
        }

        public void Reset()
        {
            TickSchedule.Clear();
            CurrentTickID = 0;
            CurrentObjectID = short.MaxValue;
        }
    }
}
