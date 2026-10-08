using FSO.SimAntics.Engine;
using FSO.Files.Utils;
using System.IO;

namespace FSO.SimAntics.Primitives
{
    public class VMIdleForInput : VMPrimitiveHandler
    {
        public override VMPrimitiveExitCode Execute(VMStackFrame context, VMPrimitiveOperand args) //TODO: Behaviour for being notified out of idle and interaction canceling
        {
            var operand = (VMIdleForInputOperand)args;
            var idleStart = context.Thread.ScheduleIdleStart;
            context.Thread.ScheduleIdleStart = 0;

            context.Args[operand.StackVarToDec] -= (short)((idleStart != 0 && idleStart < context.VM.Scheduler.CurrentTickID) ? (context.VM.Scheduler.CurrentTickID - idleStart) : 1);
            
            //if we're main, attempt to run a queued interaction. We just idle if this fails.
            if (operand.AllowPush == 1 && (context.VM.TS1 || !context.ActionTree) && context.Thread.AttemptPush())
            {
                return VMPrimitiveExitCode.CONTINUE; //control handover 
                //TODO: does this forcefully end the rest of the idle? (force a true return, must loop back to run again)
            }

            if ((context.ActionTree && context.Thread.ActiveAction.NotifyIdle) || (!context.ActionTree && context.Thread.Queue.Count > 0))
            {
                context.Caller.SetFlag(VMEntityFlags.NotifiedByIdleForInput, true);
                return VMPrimitiveExitCode.GOTO_TRUE;
            }

            if (context.Thread.Interrupt)
            {
                context.Thread.Interrupt = false;
                return VMPrimitiveExitCode.GOTO_TRUE;
            }

            if (context.Args[operand.StackVarToDec] < 0)
            {
                return VMPrimitiveExitCode.GOTO_TRUE;
            }
            else
            {
                if (operand.AllowPush == 1) return VMPrimitiveExitCode.CONTINUE_NEXT_TICK; //interactions must be checked every tick
                else if (context.VM.TS1
                    && context.Thread.Queue.Count > context.Thread.ActiveQueueBlock + 1)
                {
                    //SIM-19 (addendum 5/6 + 2026-09-20 review fix): native TS1 op17
                    //idles never long-sleep — while an unexamined action sits queued
                    //the idle polls per-tick, and the poll itself must attempt the push
                    //(mirroring the AllowPush==1 success path). The
                    //CONTINUE_FUTURE_TICK long-sleep starved the queue for the whole
                    //idle countdown (fm-fix28: the greet halves sat queued through 27
                    //sim-min of idle re-execution with no promotion).
                    //REVIEW FIX (P1-1): the former `!context.ActionTree` conjunct made
                    //this branch UNREACHABLE — the plain-tree case with queued actions
                    //already exits GOTO_TRUE at the NotifyByIdleForInput check above,
                    //so only the ACTION-TREE case (a nested routine idling while the
                    //greet waits queued — the actual fm-fix28 starvation) can arrive
                    //here. AllowPush==1 idles keep the every-tick poll at the top of
                    //Execute; this branch is for the AllowPush==0 nested idles.
                    if (context.Thread.AttemptPush())
                    {
                        return VMPrimitiveExitCode.CONTINUE; //control handover
                    }
                    return VMPrimitiveExitCode.CONTINUE_NEXT_TICK;
                }
                else
                {
                    context.Thread.ScheduleIdleStart = context.VM.Scheduler.CurrentTickID;
                    context.VM.Scheduler.ScheduleTickIn(context.Caller, (uint)context.Args[operand.StackVarToDec] + 1);
                    return VMPrimitiveExitCode.CONTINUE_FUTURE_TICK;
                }
            }
        }
    }

    public class VMIdleForInputOperand : VMPrimitiveOperand
    {
        public short StackVarToDec { get; set; }
        public ushort AllowPush { get; set; }

        #region VMPrimitiveOperand Members
        public void Read(byte[] bytes)
        {
            using (var io = IoBuffer.FromBytes(bytes, ByteOrder.LITTLE_ENDIAN))
            {
                StackVarToDec = io.ReadInt16();
                AllowPush = io.ReadUInt16();
            }
        }

        public void Write(byte[] bytes)
        {
            using (var io = new BinaryWriter(new MemoryStream(bytes)))
            {
                io.Write(StackVarToDec);
                io.Write(AllowPush);
            }
        }
        #endregion
    }
}
