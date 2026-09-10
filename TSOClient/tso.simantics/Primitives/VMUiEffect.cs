using FSO.SimAntics.Engine;
using FSO.SimAntics.Engine.Utils;
using FSO.SimAntics.Engine.Scopes;

namespace FSO.SimAntics.Primitives
{
    /// <summary>
    /// The TS1 tutorial control-flash request (TryElement opcode 0x22 = 34,
    /// PPC 0xf0410 table at TOC-0x5998; opcode index = (s16)opcode & 0x7fff,
    /// skeptic C5). The lesson script flashes UI targets through three
    /// exe-side primitives reached from this one tree opcode:
    /// Tut_FlashButtonByImageID (0x1569e0, image/bmp resource id),
    /// Tut_FlashPersonPanelButton (0x156440, neighbor id identified by
    /// portrait buffer identity) and Tut_FlashRelButton (0x156370, neighbor
    /// id gating existence only — the first relationship panel window is
    /// flashed, skeptic C4). The engine does not resolve the target: it
    /// dispatches the decoded request to UI listeners, which own the window
    /// tree and the cTSWinMgrW95+0x20c highlighter lifecycle (skeptic C2).
    /// </summary>
    public class VMUiEffect : VMPrimitiveHandler
    {
        public override VMPrimitiveExitCode Execute(VMStackFrame context, VMPrimitiveOperand args)
        {
            if (!context.VM.TS1) return VMPrimitiveExitCode.GOTO_TRUE;
            var operand = (VMUiEffectOperand)args;
            // Operand byte 0: 0 = flash by image id, 1 = person panel button,
            // 2 = relationship panel. Every other sub-op dispatches nothing and
            // still returns 1 (tryelement-region.txt).
            var subOp = operand.SubOp;
            if (subOp > 2) return VMPrimitiveExitCode.GOTO_TRUE;
            // The id argument is InterpValue(byte1, (s16)(operand+2), 0):
            // byte 1 is the addressing mode, the s16 at operand+2 its data.
            var id = VMMemory.GetVariable(context, (VMVariableScope)operand.Mode, operand.Data);
            // Operand byte 4 bit 0 selects flash on/off.
            context.VM.SignalTutorialUIEffect(subOp, id, (operand.Flags & 1) != 0);
            return VMPrimitiveExitCode.GOTO_TRUE;
        }
    }

    public class VMUiEffectOperand : VMPrimitiveOperand
    {
        public byte SubOp { get; set; }
        public byte Mode { get; set; }
        public short Data { get; set; }
        public byte Flags { get; set; }
        private readonly byte[] Reserved = new byte[3];

        public void Read(byte[] bytes)
        {
            SubOp = bytes[0];
            Mode = bytes[1];
            Data = (short)(bytes[2] | (bytes[3] << 8));
            Flags = bytes[4];
            System.Array.Copy(bytes, 5, Reserved, 0, 3);
        }

        public void Write(byte[] bytes)
        {
            bytes[0] = SubOp;
            bytes[1] = Mode;
            bytes[2] = (byte)Data;
            bytes[3] = (byte)(Data >> 8);
            bytes[4] = Flags;
            for (var i = 0; i < Reserved.Length; i++) bytes[5 + i] = Reserved[i];
        }
    }
}
