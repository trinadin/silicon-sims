using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using FSO.Content;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;
using FSO.SimAntics;
using FSO.SimAntics.Marshals.Threads;
using FSO.SimAntics.Model.TS1Platform;
using FSO.SimAntics.Utils;

namespace Simitone.Client
{
    internal static class AutotestOBJM242
    {
        internal static bool Check(out string diagnostics)
        {
            var failures = new List<string>();
            Action<bool, string> check = (pass, name) => { if (!pass) failures.Add(name); };
            bool oldWorld = VM.UseWorld, oldTS1 = VM.GlobTS1;
            try
            {
                VM.UseWorld = false;
                var context = new VMContext(null);
                var vm = new VM(context, null, null) { TS1 = true, PlatformState = new VMTS1LotState() };
                VM.GlobTS1 = true;
                var activator = new VMTS1ActivatorNew(vm, 56);
                var convert = typeof(VMTS1ActivatorNew).GetMethod("ConvertThread", BindingFlags.Instance | BindingFlags.NonPublic);
                Func<OBJT, OBJMStackFrame[], VMThreadMarshal> import = (types, frames) =>
                {
                    // Only the fields consumed by ConvertThread are needed. No
                    // original object or person arrays are mutated by fixtures.
                    var instance = new OBJMInstance();
                    instance.ObjectData = new short[68];
                    instance.ObjectData[11] = 292;
                    instance.TempRegisters = new short[] { 617, 23 };
                    instance.Stack = frames;
                    return (VMThreadMarshal)convert.Invoke(activator, new object[] { new OBJM(), types, instance });
                };
                var content = Content.Get();
                var social = content.WorldObjects.Get(0x9f6954fb);
                var litter = content.WorldObjects.Get(0xbedd7b26);
                if (social == null || litter == null) throw new Exception("Original Social Attack/litter-box definitions unavailable");
                var socialRoutine = (VMRoutine)social.Resource.GetRoutine(4110);
                var litterRoutine = (VMRoutine)litter.Resource.GetRoutine(4110);
                check(socialRoutine.Instructions[1].Opcode == 35 && litterRoutine.Instructions[1].Opcode == 27,
                    "same-tree-node-distinct-owned-opcodes");
                var entry = Empty<OBJTEntry>();
                entry.GUID = 0x9f6954fb;
                entry.TypeID = 1;
                var positiveTypes = new OBJT { Entries = new List<OBJTEntry> { entry } };
                foreach (int phase in new[] { 0, 1, 2, 3, 15, -1, 256 })
                {
                    var source = new OBJMStackFrame(333, 4110, 1, new short[] { 18, 29 }, new short[] { 4124, 255, 371 }, phase, 1);
                    var result = import(positiveTypes, new[] { source });
                    byte expected = phase == 1 || phase == 2 ? (byte)phase : (byte)0;
                    check(result.Stack.Length == 1 && result.Stack[0].TS1UserEventPhase == expected,
                        "actual-opcode35-phase-" + phase);
                    check(result.Stack[0].StackObject == 333 && result.Stack[0].RoutineID == 4110 &&
                        result.Stack[0].InstructionPointer == 1 && result.Stack[0].CodeOwnerGUID == 0x9f6954fb &&
                        result.Stack[0].Args.SequenceEqual(new short[] { 18, 29, 0, 0 }) &&
                        result.Stack[0].Locals.SequenceEqual(source.Locals) && result.TempRegisters[0] == 617 &&
                        source.PrimitiveState == phase, "other-frame-state-preserved-" + phase);
                }

                // Read actual original House56 OBJM and OBJT. The helper's claim
                // that this was Social2 PIP was a tree-ID collision: owner146 is
                // Dunginator9000, GUID bedd7b26, whose node is go-to-relative27.
                var house = new IffFile(Path.Combine(content.TS1BasePath, "UserData", "Houses", "House56.iff"), false);
                house.MarkThrowaway();
                var objt = house.Get<OBJT>(0);
                var objm = house.Get<OBJM>(1);
                objm.Prepare(type =>
                {
                    var typeEntry = objt.Entries[type - 1];
                    return new OBJMResource { OBJT = typeEntry, OBJD = content.WorldObjects.Get(typeEntry.GUID)?.OBJ };
                });
                var originalFrames = objm.ObjectData[292].Instance.Stack;
                var actual = originalFrames.Single(f => f.TreeID == 4110 && f.NodeID == 1);
                check(actual.PrimitiveState == 2 && actual.CodeOwnerObjType == 146 && actual.StackObjectID == 333 &&
                    objt.Entries[145].GUID == 0xbedd7b26 && objt.Entries[145].TypeID == 146 &&
                    litter.OBJ.GUID == objt.Entries[145].GUID, "original-house56-objt-identity");
                foreach (int state in new[] { 1, 2, 3, 15 })
                {
                    var frame = actual;
                    frame.PrimitiveState = state;
                    var result = import(objt, new[] { frame });
                    check(result.Stack.Length == 1 && result.Stack[0].TS1UserEventPhase == 0 &&
                        frame.PrimitiveState == state, "house56-non35-state-unmapped-" + state);
                }
                foreach (var invalid in new[] {
                    new OBJMStackFrame(333, 32767, 1, new short[0], new short[0], 2, 1),
                    new OBJMStackFrame(333, 4110, -1, new short[0], new short[0], 2, 1),
                    new OBJMStackFrame(333, 4110, (short)socialRoutine.Instructions.Length, new short[0], new short[0], 2, 1) })
                {
                    var valid = new OBJMStackFrame(333, 4110, 1, new short[0], new short[0], 1, 1);
                    var rejected = import(positiveTypes, new[] { valid, invalid });
                    check(rejected.Stack.Length == 0 && rejected.Queue.Length == 0 &&
                        rejected.ActiveQueueBlock == -1 && !rejected.Interrupt && rejected.ActionUID == 0,
                        "atomic-stale-stack-rejection-" + invalid.TreeID + "-" + invalid.NodeID);
                }
            }
            catch (Exception e) { failures.Add(e.ToString()); }
            finally { VM.UseWorld = oldWorld; VM.GlobTS1 = oldTS1; }
            diagnostics = failures.Count == 0
                ? "production ConvertThread: original opcode35 phases1/2; other states preserved; House56 OBJT owner bedd7b26 resolves opcode27, not PIP; invalid routine/IP atomic rejection passed"
                : string.Join("; ", failures);
            return failures.Count == 0;
        }

        private static T Empty<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
    }
}
