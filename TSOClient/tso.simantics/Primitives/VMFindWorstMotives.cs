using FSO.SimAntics.Engine;
using FSO.SimAntics.Model;
using System.Linq;

namespace FSO.SimAntics.Primitives
{
    /// <summary>
    /// ORIG-02 prim 33 (opcode confirmed from the rebuilt cXObject TryElement
    /// table, case 0xe7994 -> TryFind5WorstMotives 0xeef00):
    /// "find 5 worst motives". Operands: bytes 0-1 = WHO (0 or >=3 = the
    /// executing Sim; 1 = Stack Object; 2 = null -> error 10), bytes 2-3 =
    /// SET (0..2 validated, else error 30; otherwise unused by the engine).
    /// Sorts the engine-ID template {5,6,7,8,9,13,14,15} (Energy, Comfort,
    /// Hunger, Hygiene, Bladder, Room, Social, Fun — the same list as
    /// VMFindBestAction) ascending by current value and writes the FIVE
    /// LOWEST motive IDs to temps 0..4. AUD-18 binary re-derivation
    /// (decompile 0x100eef00): the native stores to (this + 0x3a..+0x42) —
    /// the EXECUTING object's temp slots, NOT the WHO target — and the
    /// comparator (_MotiveSort 0xef0f0) is a ±epsilon banded ascending
    /// compare (worst first). Port mapping: the Temps scope reads
    /// Thread.TempRegisters (VMMemory.cs:66/521), so the IDs go to
    /// context.Thread.TempRegisters[0..4] (the ORIG-02 port note's mapping;
    /// the earlier ObjectData[100+i] write was unreadable through the Temps
    /// scope and targeted the wrong object when WHO = 1).
    /// </summary>
    public class VMFindWorstMotives : VMPrimitiveHandler
    {
        private static readonly int[] MotiveIDs = { 5, 6, 7, 8, 9, 13, 14, 15 };

        public override VMPrimitiveExitCode Execute(VMStackFrame context, VMPrimitiveOperand args)
        {
            var operand = (VMFindWorstMotivesOperand)args;
            if (operand.Who == 2) return VMPrimitiveExitCode.ERROR; // native error 10
            if (operand.Set < 0 || operand.Set > 2) return VMPrimitiveExitCode.ERROR; // native error 30

            var target = (operand.Who == 1) ? context.StackObject : context.Caller;
            var avatar = target as VMAvatar;
            if (avatar == null) return VMPrimitiveExitCode.ERROR;

            var worst = MotiveIDs
                .OrderBy(id => avatar.GetMotiveData((VMMotive)id))
                .Take(5)
                .ToArray();
            for (int i = 0; i < 5 && i < context.Thread.TempRegisters.Length; i++)
                context.Thread.TempRegisters[i] = (short)worst[i];

            return VMPrimitiveExitCode.GOTO_TRUE;
        }
    }

    public class VMFindWorstMotivesOperand : VMPrimitiveOperand
    {
        public short Who;
        public short Set;

        public void Read(byte[] bytes)
        {
            Who = (short)(bytes[0] | (bytes[1] << 8));
            Set = (short)(bytes[2] | (bytes[3] << 8));
        }

        public void Write(byte[] bytes)
        {
            bytes[0] = (byte)(Who & 0xFF);
            bytes[1] = (byte)((Who >> 8) & 0xFF);
            bytes[2] = (byte)(Set & 0xFF);
            bytes[3] = (byte)((Set >> 8) & 0xFF);
        }
    }
}
