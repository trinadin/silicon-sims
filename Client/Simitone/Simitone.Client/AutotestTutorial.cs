using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FSO.Content;
using FSO.Files.Formats.IFF.Chunks;
using FSO.SimAntics;
using FSO.SimAntics.Engine;
using FSO.SimAntics.Entities;
using FSO.SimAntics.Model;
using FSO.SimAntics.Model.TS1Platform;
using FSO.SimAntics.Model.TSOPlatform;
using FSO.SimAntics.Primitives;
using Simitone.Client.UI.Panels;

namespace Simitone.Client
{
    internal static class AutotestTutorial
    {
        // Independent expectations: original TryTutorial, DoStream, destructor
        // and ObjectDialog::Show, recorded in r239-tutorial. No lot is ticked or
        // mutated: the latch fixtures use a separate VM and two inert entities.
        internal static bool Check(out string diagnostics)
        {
            var failures = new List<string>();
            Action<bool, string> check = (pass, name) => { if (!pass) failures.Add(name); };
            bool world = VM.UseWorld;
            bool globalTS1 = VM.GlobTS1;
            try
            {
                VM.UseWorld = false; // Thread-local, restored before returning.
                var context = new VMContext(null);
                var vm = new VM(context, null, null) { PlatformState = new VMTS1LotState() };
                context.RoomInfo = new[] { new VMRoomInfo { Entities = new List<VMEntity>() } };
                var definition = Content.Get().WorldObjects.Get(0xc3249a1d);
                if (definition == null) throw new Exception("Original Tutorial object unavailable");
                var first = new VMGameObject(definition, null);
                var second = new VMGameObject(definition, null);
                first.Thread = new VMThread(context, first, 4);
                second.Thread = new VMThread(context, second, 4);
                vm.AddEntity(first);
                vm.AddEntity(second);
                var frame = new VMStackFrame { Caller = first, Thread = first.Thread };
                var registration = VMContext.Primitives[10];
                check(registration?.GetHandler() is VMTS1Tutorial &&
                    registration.OperandModel == typeof(VMTS1TutorialOperand), "registration10");
                // JIT is supplied by the desktop host, not a Client dependency.
                var assemblyStore = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("FSO.SimAntics.JIT.Runtime.AssemblyStore"))
                    .FirstOrDefault(t => t != null);
                var acceptsVersion = assemblyStore?.GetMethod("IsVersionCompatible");
                Func<uint, bool> accepted = version => acceptsVersion != null &&
                    (bool)acceptsVersion.Invoke(null, new object[] { version });
                check(!accepted(0) && !accepted(3) && accepted(4) && !accepted(5),
                    "compiledModuleVersionBoundary");
                var handler = new VMTS1Tutorial();
                var operand = new VMTS1TutorialOperand();
                int events = 0;
                context.TutorialObjectChanged += (previous, current) => events++;

                byte[] reserved = { 0, 0x9b, 0xf4, 0x12, 0x34, 0x56, 0x78, 0xff };
                operand.Read(reserved);
                var written = new byte[8];
                operand.Write(written);
                check(written.SequenceEqual(reserved), "reservedRoundtrip");
                check(handler.Execute(frame, operand) == VMPrimitiveExitCode.GOTO_TRUE &&
                    context.TutorialObject == first && events == 1, "acquire");
                var ownerMessage = new VMDialogInfo { Caller = first, Operand = new VMDialogOperand {
                    Type = VMDialogType.Message, Flags = VMDialogFlags.NewEngageContinue } };
                check(UILotControl.IsNonmodalTutorial(ownerMessage), "ownedMessageNonmodal");
                ownerMessage.Caller = second;
                check(!UILotControl.IsNonmodalTutorial(ownerMessage), "foreignMessageUnchanged");
                check(handler.Execute(frame, operand) == VMPrimitiveExitCode.GOTO_FALSE && events == 1,
                    "repeatAcquireFails");
                frame.Caller = second;
                check(handler.Execute(frame, operand) == VMPrimitiveExitCode.GOTO_FALSE, "contendedAcquire");
                operand.Mode = 1;
                check(handler.Execute(frame, operand) == VMPrimitiveExitCode.GOTO_FALSE &&
                    context.TutorialObject == first, "foreignRelease");
                operand.Mode = 2;
                check(handler.Execute(frame, operand) == VMPrimitiveExitCode.ERROR, "invalid2");
                operand.Mode = 255;
                check(handler.Execute(frame, operand) == VMPrimitiveExitCode.ERROR, "invalid255");

                frame.Caller = first;
                operand.Mode = 1;
                first.Thread.BlockingState = new VMDialogResult();
                second.Thread.BlockingState = new VMDialogResult();
                var foreignDialog = second.Thread.BlockingState;
                vm.GlobalBlockingDialog = second;
                vm.SpeedMultiplier = -2;
                vm.LastSpeedMultiplier = 3;
                check(handler.Execute(frame, operand) == VMPrimitiveExitCode.GOTO_TRUE &&
                    first.Thread.BlockingState == null && second.Thread.BlockingState == foreignDialog &&
                    vm.GlobalBlockingDialog == second && vm.SpeedMultiplier == -2 && events == 2,
                    "releasePreservesForeignDialog");
                foreach (int speed in new[] { 0, 2 })
                {
                    context.SetTutorialObject(first);
                    first.Thread.BlockingState = new VMDialogResult();
                    vm.GlobalBlockingDialog = first;
                    vm.SpeedMultiplier = -2;
                    vm.LastSpeedMultiplier = speed;
                    context.SetTutorialObject(null);
                    check(vm.GlobalBlockingDialog == null && vm.SpeedMultiplier == speed &&
                        first.Thread.BlockingState == null, "releaseSpeed" + speed);
                }

                context.SetTutorialObject(first);
                context.RemoveObjectInstance(second);
                check(context.TutorialObject == first, "foreignDeletion");
                context.RemoveObjectInstance(first);
                check(context.TutorialObject == null && vm.TS1State.TutorialObjectID == 0 &&
                    vm.GetObjectById(first.ObjectID) == null, "ownerDeletion");
                vm.TS1State.TutorialObjectID = 1234;
                int before = events;
                context.SetTutorialObject(null);
                check(vm.TS1State.TutorialObjectID == 0 && events == before, "staleOwnerNormalization");

                // Legacy TS1 payload: false SIMI, UInt16 65535 family, then a
                // sentinel belonging to the enclosing marshal. v38 must not eat it.
                using (var input = new BinaryReader(new MemoryStream(new byte[] { 0, 255, 255, 0x5a })))
                {
                    var legacy = new VMTS1LotState(38);
                    legacy.Deserialize(input);
                    check(legacy.TutorialObjectID == 0 && input.ReadByte() == 0x5a, "v38Boundary");
                }
                var saved = new VMTS1LotState { TutorialObjectID = 301 };
                var bytes = Serialize(saved.SerializeInto);
                check(bytes.SequenceEqual(new byte[] { 0, 255, 255, 0x2d, 1 }), "v39ExactPayload");
                using (var input = new BinaryReader(new MemoryStream(bytes)))
                {
                    var restored = new VMTS1LotState(39);
                    restored.Deserialize(input);
                    check(restored.TutorialObjectID == 301 && input.BaseStream.Position == bytes.Length,
                        "v39Roundtrip");
                }
                var tso = new VMTSOLotState();
                byte[] tsoPayload = Serialize(tso.SerializeInto);
                foreach (int version in new[] { 38, 39 })
                {
                    using (var input = new BinaryReader(new MemoryStream(tsoPayload)))
                    {
                        new VMTSOLotState(version).Deserialize(input);
                        check(input.BaseStream.Position == tsoPayload.Length, "tsoBoundary" + version);
                    }
                }

                // Hand-encoded compressed shorts, independently derived from
                // Recon16's [5,8,13,16] width law. No production encoder used.
                foreach (var fixture in new[] {
                    (Value: (short)0, Bytes: new byte[] { 0 }),
                    (Value: (short)1, Bytes: new byte[] { 0x81 }),
                    (Value: (short)301, Bytes: new byte[] { 0xc1, 0x2d }),
                    (Value: (short)-1, Bytes: new byte[] { 0x9f }) })
                {
                    var payload = new byte[] { 0,0,0,0, 62,0,0,0, 0x4d,0x6a,0x62,0x4f,
                        1, 0, 0,0,0,0 }.Concat(fixture.Bytes).ToArray();
                    var objm = new OBJM();
                    using (var input = new MemoryStream(payload)) objm.Read(null, input);
                    check(objm.TutorialObjectID == fixture.Value, "objmOwner" + fixture.Value);
                }
                // One entry, then a mark pointing beyond three uninterpreted
                // instance bytes, a terminal mark, and owner301. Parsing the
                // owner does not require decoding that instance's resource.
                var oneObject = new byte[] { 0,0,0,0, 62,0,0,0, 0x4d,0x6a,0x62,0x4f,
                    1, 0x81,0x81,0, 11,0,0,0, 0xa5,0x5a,0xff, 0,0,0,0, 0xc1,0x2d };
                var marked = new OBJM();
                using (var input = new MemoryStream(oneObject)) marked.Read(null, input);
                check(marked.IDToOBJT.Count == 1 && marked.TutorialObjectID == 301, "objmMarkedInstance");
                var info = new VMDialogInfo { Operand = new VMDialogOperand {
                    Type = VMDialogType.Sims1Tutorial,
                    Flags = VMDialogFlags.Continue | VMDialogFlags.NewEngageContinue } };
                check(UILotControl.IsNonmodalTutorial(info), "tutorialContinueNonmodal");
                info.Operand.Continue = false;
                check(UILotControl.IsNonmodalTutorial(info), "nonmodalBitIndependentOfContinue");
                info.Operand.Flags = VMDialogFlags.Continue;
                check(!UILotControl.IsNonmodalTutorial(info), "tutorialBlockingModal");
                info.Operand.Type = VMDialogType.Message;
                info.Operand.Flags = VMDialogFlags.NewEngageContinue;
                check(!UILotControl.IsNonmodalTutorial(info), "ordinaryRoutingUnchanged");
            }
            catch (Exception e) { failures.Add(e.ToString()); }
            finally
            {
                VM.UseWorld = world;
                VM.GlobTS1 = globalTS1;
            }
            diagnostics = failures.Count == 0
                ? "opcode10 ownership/contention/errors; owner deletion/dialog isolation; v38/v39/TSO boundaries; 5 original trailer fixtures; tutorial modal routing passed"
                : string.Join("; ", failures);
            return failures.Count == 0;
        }

        private static byte[] Serialize(Action<BinaryWriter> write)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) write(writer);
                return stream.ToArray();
            }
        }
    }
}
