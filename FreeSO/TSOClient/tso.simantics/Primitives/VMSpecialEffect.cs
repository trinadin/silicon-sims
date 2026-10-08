using FSO.SimAntics.Engine;
using FSO.SimAntics.Engine.Utils;
using FSO.Files.Utils;
using System.IO;
using FSO.Files.Formats.IFF.Chunks;
using FSO.SimAntics.Engine.Scopes;
using FSO.SimAntics.Model;

namespace FSO.SimAntics.Primitives
{
    public class VMSpecialEffect : VMPrimitiveHandler
    {
        public override VMPrimitiveExitCode Execute(VMStackFrame context, VMPrimitiveOperand args)
        {
            if (!context.VM.TS1) return VMPrimitiveExitCode.GOTO_TRUE;
            var operand = (VMSpecialEffectOperand)args;
            if (context.TS1UserEventPhase == 0)
            {
                // Original TryUserEvent uses StackElem+8: prepare, dispatch,
                // complete. Preserve its yields and serialize the phase.
                context.VM.SetGlobalValue(11, 1);
                context.TS1UserEventPhase = 1;
                return VMPrimitiveExitCode.CONTINUE_NEXT_TICK;
            }
            if (context.TS1UserEventPhase == 1)
            {
                context.TS1UserEventPhase = 2;
                if (context.StackObject == null || context.StackObject.Dead)
                {
                    context.TS1UserEventPhase = 0;
                    return VMPrimitiveExitCode.ERROR; // original error 23
                }
                if (!IsCallerEligible(context, operand))
                {
                    context.TS1UserEventPhase = 0;
                    return VMPrimitiveExitCode.GOTO_TRUE;
                }
                var request = BuildPIPEvent(context, operand);
                context.VM.SignalGenericVMEvt(VMEventType.TS1PictureInPicture, request);
                if (!request.Suppressed) return VMPrimitiveExitCode.CONTINUE_NEXT_TICK;
            }
            context.TS1UserEventPhase = 0;
            return VMPrimitiveExitCode.GOTO_TRUE;
        }

        public static VMTS1PIPEvent BuildPIPEvent(VMStackFrame context, VMSpecialEffectOperand operand)
        {
            byte flags = unchecked((byte)operand.Flags);
            short seconds = VMMemory.GetVariable(context,
                (flags & 0x20) != 0 ? VMVariableScope.Local : VMVariableScope.Literal,
                unchecked((short)operand.Timeout));
            string caption = "";
            int index = unchecked((byte)operand.StrIndex);
            if (index != 0)
            {
                var resource = context.ScopeResource;
                int routine = context.Routine?.ID ?? 4096;
                var source = routine >= 8192 ? resource.SemiGlobal?.Get<STR>(305)
                    : routine >= 4096 ? resource.Get<STR>(305)
                    : context.Global.Resource.Get<STR>(305);
                var text = source?.GetString(index - 1);
                if (!string.IsNullOrEmpty(text)) caption = VMDialogHandler.ParseDialogString(context, text, source);
            }
            return new VMTS1PIPEvent(context.StackObject, (flags & 1) != 0,
                (flags & 4) == 0, seconds * 1000,
                unchecked((byte)operand.Size), unchecked((byte)operand.Zoom),
                (flags & 8) != 0, (flags & 0x10) != 0, caption,
                (flags & 0x40) != 0, context.Caller);
        }

        public static bool IsCallerEligible(VMStackFrame context, VMSpecialEffectOperand operand)
        {
            if ((unchecked((byte)operand.Flags) & 8) != 0) return true;
            short house = context.VM.GetGlobalValue(10);
            short zoning;
            if (house == 0 || (FSO.Content.Content.Get().Neighborhood.ZoningDictionary
                .TryGetValue(house, out zoning) && zoning == 1)) return true;
            var caller = context.Caller as VMAvatar;
            if (caller == null) return false;
            short family = context.VM.GetGlobalValue(9);
            return caller.GetPersonData(VMPersonDataVariable.TS1FamilyNumber) == family ||
                (family == 0 && caller.GetPersonData((VMPersonDataVariable)77) != 0);
        }
    }

    public class VMSpecialEffectOperand : VMPrimitiveOperand
    {
        public ushort Timeout;
        public sbyte Size;
        public sbyte Zoom;
        public sbyte Flags;
        public sbyte StrIndex;
        private readonly byte[] Reserved = new byte[2];

        #region VMPrimitiveOperand Members
        public void Read(byte[] bytes)
        {
            using (var io = IoBuffer.FromBytes(bytes, ByteOrder.LITTLE_ENDIAN)){
                Timeout = io.ReadUInt16();
                Size = (sbyte)io.ReadByte();
                Zoom = (sbyte)io.ReadByte();
                Flags = (sbyte)io.ReadByte();
                StrIndex = (sbyte)io.ReadByte();
                Reserved[0] = io.ReadByte();
                Reserved[1] = io.ReadByte();
            }
        }

        public void Write(byte[] bytes) {
            using (var io = new BinaryWriter(new MemoryStream(bytes)))
            {
                io.Write(Timeout);
                io.Write(Size);
                io.Write(Zoom);
                io.Write(Flags);
                io.Write(StrIndex);
                io.Write(Reserved);
            }
        }
        #endregion
    }
}
