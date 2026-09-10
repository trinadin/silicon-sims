using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FSO.Content;
using FSO.SimAntics;
using FSO.SimAntics.Engine;
using FSO.SimAntics.Entities;
using FSO.SimAntics.Marshals.Threads;
using FSO.SimAntics.Model;
using FSO.SimAntics.Model.TS1Platform;
using FSO.SimAntics.Model.TSOPlatform;
using FSO.SimAntics.Primitives;

namespace Simitone.Client
{
    internal static class AutotestPIP240
    {
        // Original PPC TryUserEvent fac70..faf74 and independent byte fixtures
        // in r240-pip. Isolated VM: no lot tick, network traffic or photo writes.
        internal static bool Check(out string diagnostics)
        {
            var failures = new List<string>();
            Action<bool,string> check = (ok,name) => { if (!ok) failures.Add(name); };
            bool world = VM.UseWorld, ts1 = VM.GlobTS1;
            try
            {
                VM.UseWorld = false;
                var context = new VMContext(null);
                var vm = new VM(context, null, null) { PlatformState = new VMTS1LotState(), GlobalState = new short[38] };
                vm.TS1 = true;
                VM.GlobTS1 = true;
                context.RoomInfo = new[] { new VMRoomInfo { Entities = new List<VMEntity>() } };
                var definition = Content.Get().WorldObjects.Get(0xc3249a1d);
                if (definition == null) throw new Exception("Original Tutorial fixture unavailable");
                var entity = new VMGameObject(definition, null);
                entity.Thread = new VMThread(context, entity, 4);
                vm.AddEntity(entity);
                var frame = new VMStackFrame { Caller = entity, Callee = entity, StackObject = entity,
                    Thread = entity.Thread, CodeOwner = definition, Locals = new short[] { 9, -7, 32767 } };
                var handler = new VMSpecialEffect();
                var operand = new VMSpecialEffectOperand();
                var registration = VMContext.Primitives[35];
                check(registration?.GetHandler() is VMSpecialEffect &&
                    registration.OperandModel == typeof(VMSpecialEffectOperand), "registration35");
                int events = 0;
                bool suppress = false;
                VMTS1PIPEvent last = null;
                vm.OnGenericVMEvent += (type,data) => {
                    if (type != VMEventType.TS1PictureInPicture) return;
                    events++; last = (VMTS1PIPEvent)data; last.Suppressed = suppress;
                };
                operand.Read(new byte[] { 10,0,1,3,0x15,0,0,0 });
                var request = VMSpecialEffect.BuildPIPEvent(frame, operand);
                check(request.Target == entity && request.Caller == entity && request.ObjectID == entity.ObjectID &&
                    request.Open && !request.SkipIfVisible && request.DurationMilliseconds == 10000 &&
                    request.SizeIndex == 1 && request.ZoomIndex == 3 && request.AutoSnapshot &&
                    !request.MainCameraRoute && !request.SuppressPreDispatchNotification && request.Caption == "", "literalFixture");
                operand.Read(new byte[] { 255,255,2,0,1,0,0xa5,0x5a });
                var encoded = new byte[8]; operand.Write(encoded);
                request = VMSpecialEffect.BuildPIPEvent(frame, operand);
                check(encoded.SequenceEqual(new byte[] { 255,255,2,0,1,0,0xa5,0x5a }) &&
                    request.DurationMilliseconds == -1000 && request.SizeIndex == 2 && request.ZoomIndex == 0 &&
                    request.SkipIfVisible, "negativeLiteralReservedZoom0");
                operand.Read(new byte[] { 1,0,255,128,0xe5,0,1,2 });
                request = VMSpecialEffect.BuildPIPEvent(frame, operand);
                check(request.DurationMilliseconds == -7000 && request.SizeIndex == 255 && request.ZoomIndex == 128 &&
                    request.Open && !request.SkipIfVisible && request.SuppressPreDispatchNotification &&
                    !request.MainCameraRoute && !request.AutoSnapshot, "localSignedUnsignedFields");
                operand.Read(new byte[] { 2,0,1,2,0x29,0,0,0 });
                request = VMSpecialEffect.BuildPIPEvent(frame, operand);
                check(request.DurationMilliseconds == 32767000 && request.MainCameraRoute && request.Open,
                    "localMaximumMainRoute");
                operand.Read(new byte[8]);
                request = VMSpecialEffect.BuildPIPEvent(frame, operand);
                check(!request.Open && request.SkipIfVisible && request.DurationMilliseconds == 0, "closeFixture");
                operand.Flags = 0x18;
                request = VMSpecialEffect.BuildPIPEvent(frame, operand);
                check(!request.Open && request.MainCameraRoute && request.AutoSnapshot, "closedMainSnapshotRoute");

                var unrelated = new VMDialogResult { WaitTime = 17 };
                entity.Thread.BlockingState = unrelated;
                vm.SetGlobalValue(10, 0); // original zoning-zero bypass
                operand.Flags = 1;
                check(handler.Execute(frame, operand) == VMPrimitiveExitCode.CONTINUE_NEXT_TICK &&
                    frame.TS1UserEventPhase == 1 &&
                    vm.GetGlobalValue(11) == 1 && events == 0, "prepareYields");
                // Restore the saved phase before invoking again, proving no re-prepare.
                frame = RestoreFrame(frame, context);
                check(handler.Execute(frame, operand) == VMPrimitiveExitCode.CONTINUE_NEXT_TICK && events == 1 &&
                    frame.TS1UserEventPhase == 2, "restoredDispatchYieldsOnce");
                frame = RestoreFrame(frame, context);
                check(handler.Execute(frame, operand) == VMPrimitiveExitCode.GOTO_TRUE && events == 1 &&
                    entity.Thread.BlockingState == unrelated && frame.TS1UserEventPhase == 0, "restoredCompletionDoesNotRedispatch");
                suppress = true;
                check(handler.Execute(frame, operand) == VMPrimitiveExitCode.CONTINUE_NEXT_TICK &&
                    handler.Execute(frame, operand) == VMPrimitiveExitCode.GOTO_TRUE && events == 2 &&
                    entity.Thread.BlockingState == unrelated && frame.TS1UserEventPhase == 0 && last.Suppressed, "optionSuppressionSkipsSecondYield");
                suppress = false;
                frame.StackObject = null;
                check(handler.Execute(frame, operand) == VMPrimitiveExitCode.CONTINUE_NEXT_TICK &&
                    handler.Execute(frame, operand) == VMPrimitiveExitCode.ERROR && events == 2 &&
                    entity.Thread.BlockingState == unrelated && frame.TS1UserEventPhase == 0, "missingTargetError23");
                frame.StackObject = entity;
                entity.Dead = true;
                check(handler.Execute(frame, operand) == VMPrimitiveExitCode.CONTINUE_NEXT_TICK &&
                    handler.Execute(frame, operand) == VMPrimitiveExitCode.ERROR && events == 2 &&
                    entity.Thread.BlockingState == unrelated && frame.TS1UserEventPhase == 0, "deletedTargetError23");
                entity.Dead = false;

                var zoning = Content.Get().Neighborhood.ZoningDictionary;
                short residential = zoning.Where(x => x.Value == 0 && x.Key != 0).Select(x => x.Key).First();
                vm.SetGlobalValue(10, residential);
                check(!VMSpecialEffect.IsCallerEligible(frame, operand), "residentialObjectRejected");
                check(handler.Execute(frame, operand) == VMPrimitiveExitCode.CONTINUE_NEXT_TICK &&
                    handler.Execute(frame, operand) == VMPrimitiveExitCode.GOTO_TRUE && events == 2 &&
                    entity.Thread.BlockingState == unrelated && frame.TS1UserEventPhase == 0, "ineligibleSkipsDispatch");
                operand.Flags = 9;
                check(VMSpecialEffect.IsCallerEligible(frame, operand), "mainCameraBypassesCaller");
                operand.Flags = 1;
                var avatar = new VMAvatar(definition);
                frame.Caller = avatar;
                vm.SetGlobalValue(9, 7);
                avatar.SetPersonData(VMPersonDataVariable.TS1FamilyNumber, 7);
                check(VMSpecialEffect.IsCallerEligible(frame, operand), "activeFamilyEligible");
                avatar.SetPersonData(VMPersonDataVariable.TS1FamilyNumber, 8);
                check(!VMSpecialEffect.IsCallerEligible(frame, operand), "foreignFamilyRejected");
                vm.SetGlobalValue(9, 0);
                avatar.SetPersonData((VMPersonDataVariable)77, 1);
                check(VMSpecialEffect.IsCallerEligible(frame, operand), "noFamilyPerson77Eligible");
                avatar.SetPersonData((VMPersonDataVariable)77, 0);
                check(!VMSpecialEffect.IsCallerEligible(frame, operand), "noFamilyPerson77ZeroRejected");
                foreach (var site in zoning.Where(x => x.Value == 1).Take(1))
                {
                    vm.SetGlobalValue(10, site.Key); frame.Caller = entity;
                    check(VMSpecialEffect.IsCallerEligible(frame, operand), "communityObjectEligible");
                }
                vm.TS1 = false;
                vm.SetGlobalValue(11, 19);
                check(handler.Execute(frame, operand) == VMPrimitiveExitCode.GOTO_TRUE && events == 2 &&
                    entity.Thread.BlockingState == unrelated && frame.TS1UserEventPhase == 0 && vm.GetGlobalValue(11) == 19, "tsoNoOpUnchanged");

                vm.TS1 = true;
                vm.SetGlobalValue(10, 0);
                var nested = new VMStackFrame { Caller = entity, Callee = entity, StackObject = entity,
                    Thread = entity.Thread, CodeOwner = definition };
                check(handler.Execute(frame, operand) == VMPrimitiveExitCode.CONTINUE_NEXT_TICK &&
                    handler.Execute(nested, operand) == VMPrimitiveExitCode.CONTINUE_NEXT_TICK &&
                    frame.TS1UserEventPhase == 1 && nested.TS1UserEventPhase == 1, "nestedSeparatePrepare");
                check(handler.Execute(nested, operand) == VMPrimitiveExitCode.CONTINUE_NEXT_TICK &&
                    frame.TS1UserEventPhase == 1 && nested.TS1UserEventPhase == 2, "nestedSeparateDispatch");
                check(handler.Execute(nested, operand) == VMPrimitiveExitCode.GOTO_TRUE &&
                    frame.TS1UserEventPhase == 1 && nested.TS1UserEventPhase == 0, "nestedCompletionPreservesOuter");
                check(handler.Execute(frame, operand) == VMPrimitiveExitCode.CONTINUE_NEXT_TICK &&
                    handler.Execute(frame, operand) == VMPrimitiveExitCode.GOTO_TRUE && events == 4 &&
                    entity.Thread.BlockingState == unrelated && unrelated.WaitTime == 17, "unrelatedBlockingStateUntouched");

                foreach (byte phase in new byte[] { 1, 2 })
                {
                    var savedFrame = new VMStackFrameMarshal { TS1UserEventPhase = phase };
                    var bytes = Serialize(savedFrame.SerializeInto);
                    var expected = new byte[25];
                    for (int i = 14; i < 22; i++) expected[i] = 255; // null local/arg counts
                    expected[24] = phase;
                    check(bytes.SequenceEqual(expected), "framePhaseExactPayload" + phase);
                    var saved = new VMThreadMarshal { Stack = new[] { savedFrame },
                        Queue = Array.Empty<VMQueuedActionMarshal>(),
                        ActionUID = 0x1234, DialogCooldown = 0x456789, ScheduleIdleStart = 0x12345678 };
                    using (var input = new BinaryReader(new MemoryStream(Serialize(saved.SerializeInto))))
                    {
                        var restored = new VMThreadMarshal(40); restored.Deserialize(input);
                        check(restored.Stack.Single().TS1UserEventPhase == phase &&
                            restored.ActionUID == 0x1234 && restored.DialogCooldown == 0x456789 &&
                            restored.ScheduleIdleStart == 0x12345678 && input.BaseStream.Position == input.BaseStream.Length,
                            "threadPhaseBoundary" + phase);
                    }
                }
                // Independently encoded v38/v39 frames: the byte after ActionTree
                // belongs to the enclosing payload and must not be consumed.
                foreach (int version in new[] { 38, 39 })
                {
                    var bytes = new byte[25];
                    for (int i = 14; i < 22; i++) bytes[i] = 255;
                    bytes[24] = 0x5a;
                    using (var input = new BinaryReader(new MemoryStream(bytes)))
                    {
                        var oldFrame = new VMStackFrameMarshal(version); oldFrame.Deserialize(input);
                        check(oldFrame.TS1UserEventPhase == 0 && input.ReadByte() == 0x5a, "legacyFrameBoundary" + version);
                    }
                }
                // Legacy empty-thread bytes are hand encoded, followed by a sentinel.
                // v38/39/40 share the same shape; the phase byte belongs to frames, so empty threads do not change.
                foreach (int version in new[] { 38,39,40 })
                {
                    var bytes = new byte[72]; bytes[71] = 0x5a;
                    using (var input = new BinaryReader(new MemoryStream(bytes)))
                    {
                        var restored = new VMThreadMarshal(version); restored.Deserialize(input);
                        check(restored.BlockingState == null && input.ReadByte() == 0x5a, "legacyThreadBoundary" + version);
                    }
                }
            }
            catch (Exception e) { failures.Add(e.ToString()); }
            finally { VM.UseWorld = world; VM.GlobTS1 = ts1; }
            diagnostics = failures.Count == 0 ? "opcode35 fixtures, eligibility, prepare/dispatch/complete yields, option suppression, per-frame restore/nesting, v38/39/40 boundaries, unrelated dialog isolation, TSO no-op passed" : string.Join("; ", failures);
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
        private static VMStackFrame RestoreFrame(VMStackFrame frame, VMContext context)
        {
            using (var reader = new BinaryReader(new MemoryStream(Serialize(frame.Save().SerializeInto))))
            {
                var data = new VMStackFrameMarshal(40); data.Deserialize(reader);
                return new VMStackFrame(data, context, frame.Thread);
            }
        }
    }
}
