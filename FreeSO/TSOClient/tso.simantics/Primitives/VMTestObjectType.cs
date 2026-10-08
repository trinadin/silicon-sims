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
                VMOp32NullProbe.Note(context, operand);
                return VMPrimitiveExitCode.GOTO_FALSE;
            }

            if (obj.Object.GUID == operand.GUID) return VMPrimitiveExitCode.GOTO_TRUE; //is my guid same?
            else if (obj.MasterDefinition != null && (obj.MasterDefinition.GUID == operand.GUID)) return VMPrimitiveExitCode.GOTO_TRUE; //is master guid same?
            else return VMPrimitiveExitCode.GOTO_FALSE;
        }
    }

    /// <summary>
    /// W4-REG probe (env-gated, SIMTONE_OP32PROBE=1): names every distinct site
    /// where op-32 evaluates a null/out-of-range object in the live VM, with the
    /// enclosing frame chain. Diagnostic only — never alters flow.
    /// </summary>
    public static class VMOp32NullProbe
    {
        public static readonly bool Enabled =
            (System.Environment.GetEnvironmentVariable("SIMTONE_OP32PROBE") ?? "0") == "1";
        private static readonly System.Collections.Generic.Dictionary<string, int> _Seen =
            new System.Collections.Generic.Dictionary<string, int>();

        public static void Note(VMStackFrame context, VMTestObjectTypeOperand operand)
        {
            if (!Enabled) return;
            try
            {
                string iff = null, label = null;
                ushort rid = 0; int ip = -1;
                try { iff = context.ScopeResource?.MainIff?.Filename; } catch { }
                try { rid = context.Routine?.Chunk?.ChunkID ?? 0; } catch { }
                try { label = context.Routine?.Rti?.Name; } catch { }
                try { ip = context.InstructionPointer; } catch { }
                var key = (iff ?? "?") + "#" + rid + " ins" + ip;
                int n;
                lock (_Seen) { n = _Seen.TryGetValue(key, out var c) ? _Seen[key] = c + 1 : (_Seen[key] = 1); }
                if (n != 1 && n % 500 != 0) return;
                var chain = new System.Text.StringBuilder();
                try
                {
                    var stack = context.Thread?.Stack;
                    if (stack != null)
                    {
                        for (int i = stack.Count - 1; i >= 0 && chain.Length < 160; i--)
                        {
                            var fr = stack[i];
                            if (fr == null) continue;
                            chain.Append(' ').Append(fr.Routine?.Chunk?.ChunkID ?? 0)
                                 .Append(':').Append(fr.InstructionPointer)
                                 .Append('(').Append(fr.Routine?.Rti?.Name ?? "?").Append(')');
                        }
                    }
                }
                catch { }
                System.Console.WriteLine("[OP32NULL" + (n == 1 ? " " : " x" + n + " ") + "] " + key
                    + " '" + (label ?? "?") + "' guid=0x" + operand.GUID.ToString("X8")
                    + " caller=obj" + (context.Caller != null ? context.Caller.ObjectID.ToString() : "?")
                    + " chain:" + chain.ToString());
            }
            catch { }
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
