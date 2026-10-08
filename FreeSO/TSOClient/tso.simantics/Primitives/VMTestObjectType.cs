using FSO.SimAntics.Engine;
using FSO.SimAntics.Engine.Scopes;
using FSO.Files.Utils;
using FSO.SimAntics.Engine.Utils;
using System.IO;

namespace FSO.SimAntics.Primitives
{
    public class VMTestObjectType : VMPrimitiveHandler
    {
        public override VMPrimitiveExitCode Execute(VMStackFrame context, VMPrimitiveOperand args)
        {
            var operand = (VMTestObjectTypeOperand)args;
            var objectID = VMMemory.GetVariable(context, operand.IdOwner, operand.IdData);

            var obj = (operand.IdOwner == VMVariableScope.StackObjectID)?context.StackObject:context.VM.GetObjectById(objectID);
            //var obj = context.StackObject;
            if (obj == null){
                // EXP-14 hdserve-residual (Ghidra 0x100e7664, the cXObject op-32 slot of
                // the TryElement dispatch table *(0x105bbac8) -> 0x10601898[32]): a
                // null/out-of-range target (id <= 0 or id > module count; the deleted-
                // object case lands here too) makes the native REPORT error 0x17 via
                // 0x10587890 and CONTINUE — lwz r28,0x11c(r27=0) reads the classic-Mac
                // low-memory word at 0x11c, which never resolves to a live ObjSelector,
                // so the GUID compares fall through and the primitive returns 0 (FALSE).
                // The old ERROR return aborted the WHOLE enclosing tree: the Hot-Date
                // podium's 'Eat Alone' TEST (DiningPodium.iff BHAV 4113) gosubs 4118
                // 'check can eat autonomously', whose no-controller path returns TRUE
                // with StackObjectID still 0 (its SetToNext scan anchor), and the
                // follow-up op-32 at 4113 ins1 then died — the podium's rows never
                // pooled for ANYONE while no Controller-Restaurant-Eat existed (the
                // controller is itself created BY the eat interaction, BHAV 4112 ins3
                // op-42 — a chicken-and-egg only this divergence produced). Native law:
                // report-and-continue -> GOTO_FALSE.
                return VMPrimitiveExitCode.GOTO_FALSE;
            }

            if (obj.Object.GUID == operand.GUID) return VMPrimitiveExitCode.GOTO_TRUE; //is my guid same?
            else if (obj.MasterDefinition != null && (obj.MasterDefinition.GUID == operand.GUID)) return VMPrimitiveExitCode.GOTO_TRUE; //is master guid same?
            else return VMPrimitiveExitCode.GOTO_FALSE;
        }
    }

    public class VMTestObjectTypeOperand : VMPrimitiveOperand
    {
        public uint GUID { get; set; }
        public short IdData { get; set; }
        public VMVariableScope IdOwner { get; set; }

        #region VMPrimitiveOperand Members
        public void Read(byte[] bytes){
            using (var io = IoBuffer.FromBytes(bytes, ByteOrder.LITTLE_ENDIAN)){
                GUID = io.ReadUInt32();
                IdData = io.ReadInt16();
                IdOwner = (VMVariableScope)io.ReadByte();
            }
        }

        public void Write(byte[] bytes) {
            using (var io = new BinaryWriter(new MemoryStream(bytes)))
            {
                io.Write(GUID);
                io.Write(IdData);
                io.Write((byte)IdOwner);
            }
        }
        #endregion
    }
}
