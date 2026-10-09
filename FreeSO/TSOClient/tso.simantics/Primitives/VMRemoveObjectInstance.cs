using FSO.Files.Utils;
using System.IO;

namespace FSO.SimAntics.Engine.Primitives
{
    public class VMRemoveObjectInstance : VMPrimitiveHandler
    {
        /// <summary>
        /// Read-only watch point for op-18 removals (trv06 residual lane).
        /// Null unless an autotest probe subscribes; never changes behavior.
        /// </summary>
        public static Action<string> AutotestRemoveSink;

        public override VMPrimitiveExitCode Execute(VMStackFrame context, VMPrimitiveOperand args)
        {
            var operand = (VMRemoveObjectInstanceOperand)args;
            VMEntity obj;
            if (operand.Target == 0) obj = context.Caller;
            else obj = context.StackObject;

            // trv06 residual watch (the pet-gone pattern above): names the BHAV
            // frame behind every op-18 removal while a subscriber is attached.
            // Null in normal play; read-only, never alters the removal.
            if (AutotestRemoveSink != null && obj != null)
            {
                try
                {
                    AutotestRemoveSink("rm guid=0x" + obj.Object.OBJ.GUID.ToString("x8") + " oid=" + obj.ObjectID
                        + " target=" + operand.Target + " at " + FSO.SimAntics.Primitives.VMGenericTSOCall.FrameInfo(context));
                }
                catch { }
            }

            //TODO: what do CleanupAll and ReturnImmediately do?
            //cleanup all likely resets all avatars using the object, though that should really happen regardless?
            //return immediately is an actual mystery
            // EXP-05 V3 (unl-pets2) read-only watch hook (VMGenericTSOCall
            // pattern, ported with the EXP-05 arc): names the BHAV that deletes
            // an avatar (F-PETS-DESPAWN). Null in normal play.
            if (FSO.SimAntics.Primitives.VMGenericTSOCall.AutotestPetGoneSink != null && obj is FSO.SimAntics.VMAvatar)
                FSO.SimAntics.Primitives.VMGenericTSOCall.AutotestPetGoneSink("petremove target=0x"
                    + obj.Object.OBJ.GUID.ToString("x8") + "/oid" + obj.ObjectID + " at "
                    + FSO.SimAntics.Primitives.VMGenericTSOCall.FrameInfo(context));
            obj?.Delete(true, context.VM.Context);

            //if (obj == context.StackObject) context.StackObject = null;

            // yield if we are going to delete
            return (obj == context.Caller) ? VMPrimitiveExitCode.GOTO_TRUE_NEXT_TICK : VMPrimitiveExitCode.GOTO_TRUE;
        }
    }

    public class VMRemoveObjectInstanceOperand : VMPrimitiveOperand
    {
        public short Target { get; set; }
        public byte Flags { get; set; }

        #region VMPrimitiveOperand Members
        public void Read(byte[] bytes)
        {
            using (var io = IoBuffer.FromBytes(bytes, ByteOrder.LITTLE_ENDIAN))
            {
                Target = io.ReadInt16();
                Flags = io.ReadByte();
            }
        }

        public void Write(byte[] bytes) {
            using (var io = new BinaryWriter(new MemoryStream(bytes)))
            {
                io.Write(Target);
                io.Write(Flags);
            }
        }
        #endregion

        public bool ReturnImmediately
        {
            get
            {
                return ((Flags & 1) == 1);
            }
            set
            {
                if (value) Flags |= 1;
                else Flags &= unchecked((byte)~1);
            }
        }

        public bool CleanupAll
        {
            get
            {
                return ((Flags & 2) == 2);
            }
            set
            {
                if (value) Flags |= 2;
                else Flags &= unchecked((byte)~2);
            }
        }
    }
}
