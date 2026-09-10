using FSO.SimAntics.Engine;
using System;

namespace FSO.SimAntics.Primitives
{
    /// <summary>The Sims' exclusive tutorial-object latch (primitive 10).</summary>
    public class VMTS1Tutorial : VMPrimitiveHandler
    {
        public override VMPrimitiveExitCode Execute(VMStackFrame context, VMPrimitiveOperand args)
        {
            if (!context.VM.TS1) return VMPrimitiveExitCode.GOTO_TRUE;
            var mode = ((VMTS1TutorialOperand)args).Mode;
            if (mode == 0)
                return context.VM.Context.SetTutorialObject(context.Caller).AsGotoExitCode();
            if (mode == 1)
                return (context.VM.Context.TutorialObject == context.Caller &&
                    context.VM.Context.SetTutorialObject(null)).AsGotoExitCode();
            // TryTutorial reports primitive error 53 for every other byte value.
            return VMPrimitiveExitCode.ERROR;
        }
    }

    public class VMTS1TutorialOperand : VMPrimitiveOperand
    {
        public byte Mode { get; set; }
        private readonly byte[] Reserved = new byte[7];

        public void Read(byte[] bytes)
        {
            Mode = bytes[0];
            Array.Copy(bytes, 1, Reserved, 0, 7);
        }

        public void Write(byte[] bytes)
        {
            bytes[0] = Mode;
            Array.Copy(Reserved, 0, bytes, 1, 7);
        }
    }
}
