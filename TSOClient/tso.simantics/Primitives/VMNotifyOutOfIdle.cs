using FSO.SimAntics.Engine;
using FSO.Files.Utils;
using System.Linq;

namespace FSO.SimAntics.Primitives
{
    public class VMNotifyOutOfIdle : VMPrimitiveHandler
    {
        public override VMPrimitiveExitCode Execute(VMStackFrame context, VMPrimitiveOperand args)
        {
            if (context.VM.TS1)
            {
                // TryElement opcode49 (PPC f0634): a missing stack object is
                // error21. Only the target's currently executing idle is notified;
                // other instructions must not leave an interrupt for a future idle.
                var target = context.StackObject;
                if (target == null) return VMPrimitiveExitCode.ERROR;
                var frame = target.Thread?.Stack.LastOrDefault();
                if (frame?.Routine == null || frame.InstructionPointer >= frame.Routine.Instructions.Length)
                    return VMPrimitiveExitCode.GOTO_TRUE;
                var opcode = frame.GetCurrentInstruction().Opcode & 0x7fff;
                if (opcode != 0 && opcode != 17) return VMPrimitiveExitCode.GOTO_TRUE;
                if (frame.Args != null && frame.Args.Length > 0) frame.Args[0] = 0;
                if (opcode == 0)
                {
                    // Native sleep scheduling is cleared only for opcode0.
                    // Interrupt represents that scheduler wake in the port.
                    context.VM.Scheduler.RescheduleInterrupt(target);
                    target.Thread.Interrupt = true;
                }
                return VMPrimitiveExitCode.GOTO_TRUE;
            }
            if (context.StackObject?.Thread != null)
            {
                context.VM.Scheduler.RescheduleInterrupt(context.StackObject);
                context.StackObject.Thread.Interrupt = true;
            }
            return VMPrimitiveExitCode.GOTO_TRUE;
        }
    }

    public class VMNotifyOutOfIdleOperand : VMPrimitiveOperand
    {
        #region VMPrimitiveOperand Members
        public void Read(byte[] bytes){
            using (var io = IoBuffer.FromBytes(bytes, ByteOrder.LITTLE_ENDIAN)){

            }
        }

        public void Write(byte[] bytes) { }
        #endregion
    }
}
