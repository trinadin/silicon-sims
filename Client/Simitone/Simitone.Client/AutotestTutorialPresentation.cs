using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FSO.Content;
using FSO.SimAntics;
using FSO.SimAntics.Engine;
using FSO.SimAntics.Entities;
using FSO.SimAntics.Model;
using FSO.SimAntics.Model.TS1Platform;
using FSO.SimAntics.NetPlay.Model;
using FSO.SimAntics.NetPlay.Model.Commands;
using FSO.SimAntics.Primitives;

namespace Simitone.Client
{
    internal static class AutotestTutorialPresentation
    {
        internal static bool Check(out string diagnostics)
        {
            var failures = new List<string>();
            Action<bool, string> check = (pass, name) => { if (!pass) failures.Add(name); };
            var oldWorld = VM.UseWorld;
            var oldTS1 = VM.GlobTS1;
            try
            {
                VM.UseWorld = false;
                var context = new VMContext(null);
                var vm = new VM(context, null, null) { TS1 = true, PlatformState = new VMTS1LotState() };
                VM.GlobTS1 = true;
                context.RoomInfo = new[] { new VMRoomInfo { Entities = new List<VMEntity>() } };
                var definition = Content.Get().WorldObjects.Get(0xc3249a1d);
                if (definition == null) throw new Exception("Original Tutorial object unavailable");
                var owner = new VMGameObject(definition, null);
                var foreign = new VMGameObject(definition, null);
                owner.Thread = new VMThread(context, owner, 4);
                foreign.Thread = new VMThread(context, foreign, 4);
                vm.AddEntity(owner);
                vm.AddEntity(foreign);
                owner.FetchTreeByName(context);
                context.SetTutorialObject(owner);

                // Independent byte fixtures from the owned original Tutorial.iff,
                // not expectations generated through the command implementation.
                var show = owner.GetRoutineWithOwner(4099, context).routine.Chunk.Instructions;
                check(show.Length == 3 && show[0].Opcode == 2 && show[0].TruePointer == 2 &&
                    show[1].Opcode == 49 && show[1].TruePointer == 254 &&
                    show[2].Opcode == 2 && show[2].TruePointer == 1 &&
                    Hex(show[0].Operand) == "0100010000050007" &&
                    Hex(show[1].Operand) == "0000000000000000" &&
                    Hex(show[2].Operand) == "00000b0000050a03", "owned-show-info-byte-contract");
                var pause = owner.GetRoutineWithOwner(4146, context);
                check(pause.routine.Chunk.Instructions.Length == 4 &&
                    pause.routine.Chunk.Instructions[0].Opcode == 0 &&
                    pause.routine.Chunk.Instructions[2].TruePointer == 255 &&
                    Hex(pause.routine.Chunk.Instructions[2].Operand) == "0100000000050007",
                    "owned-pause-false-redisplay-contract");
                var main = new VMStackFrame { Caller = owner, Callee = owner,
                    CodeOwner = pause.owner, Routine = pause.routine,
                    StackObject = foreign, Args = new short[] { 200, 19, 23, 29 } };
                owner.Thread.Push(main);
                owner.Thread.TempRegisters[0] = 617;
                var queue = owner.Thread.Queue;
                var ownerDialog = new VMDialogResult();
                var foreignDialog = new VMDialogResult();
                owner.Thread.BlockingState = ownerDialog;
                foreign.Thread.BlockingState = foreignDialog;
                vm.GlobalBlockingDialog = foreign;
                vm.SpeedMultiplier = -2;
                vm.LastSpeedMultiplier = 2;
                vm.Scheduler.CurrentTickID = 10;
                vm.Scheduler.ScheduleTick(owner, 1000);
                owner.Thread.ScheduleIdleStart = 1;
                ulong random = context.RandomSeed;
                var command = new VMNetTS1TutorialInfoCmd { ActorUID = 0x11223344 };
                check(command.Verify(vm, null) && command.Execute(vm, null), "owned-named-check-tree-runs");
                check(owner.GetAttribute(1) == 1 && main.Args[0] == 0 &&
                    owner.Thread.Interrupt && owner.Thread.ScheduleIdleEnd == 11,
                    "sleep-flag-zero-and-scheduler-wake");
                check(owner.Thread.Stack.Count == 1 && owner.Thread.Stack[0] == main &&
                    main.InstructionPointer == 0 && main.StackObject == foreign &&
                    main.Args.Skip(1).SequenceEqual(new short[] { 19, 23, 29 }) &&
                    owner.Thread.TempRegisters[0] == 617 && owner.Thread.Queue == queue &&
                    !owner.Thread.IsCheck && context.RandomSeed == random, "main-stack-and-temps-preserved");
                check(owner.Thread.BlockingState == ownerDialog && !ownerDialog.Responded &&
                    foreign.Thread.BlockingState == foreignDialog && !foreignDialog.Responded &&
                    ownerDialog.WaitTime == 0 && foreignDialog.WaitTime == 0 &&
                    vm.GlobalBlockingDialog == foreign && vm.SpeedMultiplier == -2 &&
                    vm.LastSpeedMultiplier == 2, "no-dialog-answer-or-speed-change");

                // Advance only the isolated owner's original pause tree. The named
                // action must make it return false and clear its redisplay flag.
                owner.Thread.BlockingState = null;
                owner.Thread.IsCheck = true; // prevent auto-start of the lot main tree
                vm.Scheduler.CurrentTickID = 11;
                owner.Thread.Tick();
                check(owner.Thread.Stack.Count == 0 && owner.Thread.LastStackExitCode == VMPrimitiveExitCode.RETURN_FALSE &&
                    owner.GetAttribute(1) == 0 && main.Args[0] == 0 &&
                    !owner.Thread.Interrupt && owner.Thread.ScheduleIdleStart == 0 &&
                    context.RandomSeed == random, "natural-pause-returns-false-without-rng");

                var notify = new VMNotifyOutOfIdle();
                var invoking = new VMStackFrame { Caller = foreign, Thread = foreign.Thread, StackObject = owner };
                var operand = new VMNotifyOutOfIdleOperand();
                foreach (ushort opcode in new ushort[] { 0, 17, 2 })
                {
                    vm.Scheduler.Reset();
                    vm.Scheduler.CurrentTickID = 20;
                    owner.Thread.Stack.Clear();
                    owner.Thread.Interrupt = false;
                    var target = Frame(owner, opcode);
                    owner.Thread.Push(target);
                    vm.Scheduler.ScheduleTick(owner, 1000);
                    check(notify.Execute(invoking, operand) == VMPrimitiveExitCode.GOTO_TRUE,
                        "notify-return-" + opcode);
                    check(target.Args[0] == (opcode == 2 ? 200 : 0) && target.Args[1] == 80 &&
                        owner.Thread.Interrupt == (opcode == 0) &&
                        owner.Thread.ScheduleIdleEnd == (opcode == 0 ? 21u : 1000u),
                        "native-notify-opcode-" + opcode);
                }
                owner.Thread.Stack.Clear();
                owner.Thread.Interrupt = false;
                check(notify.Execute(invoking, operand) == VMPrimitiveExitCode.GOTO_TRUE &&
                    !owner.Thread.Interrupt, "empty-main-no-future-interrupt");
                invoking.StackObject = null;
                check(notify.Execute(invoking, operand) == VMPrimitiveExitCode.ERROR, "missing-target-error21");
                invoking.StackObject = owner;

                // Native scheduler wake zeros the operand-selected parameter too,
                // even when opcode49 itself addressed parameter0.
                var sleeping = Frame(owner, 0);
                owner.Thread.Push(sleeping);
                owner.Thread.Interrupt = true;
                owner.Thread.ScheduleIdleStart = 2;
                check(new VMSleep().Execute(sleeping, new VMSleepOperand { StackVarToDec = 1 }) ==
                    VMPrimitiveExitCode.GOTO_TRUE && sleeping.Args[1] == 0 &&
                    sleeping.Args[0] == 200 && !owner.Thread.Interrupt &&
                    owner.Thread.ScheduleIdleStart == 0 && context.RandomSeed == random,
                    "ts1-wake-selected-argument-before-decrement");

                vm.TS1 = false;
                check(!command.Verify(vm, null) && !command.Execute(vm, null), "tso-command-rejected");
                owner.Thread.Stack.Clear();
                vm.Scheduler.Reset();
                vm.Scheduler.CurrentTickID = 30;
                vm.Scheduler.ScheduleTick(owner, 1000);
                check(notify.Execute(invoking, operand) == VMPrimitiveExitCode.GOTO_TRUE &&
                    owner.Thread.Interrupt && owner.Thread.ScheduleIdleEnd == 31, "tso-empty-stack-legacy-interrupt");
                invoking.StackObject = null;
                check(notify.Execute(invoking, operand) == VMPrimitiveExitCode.GOTO_TRUE, "tso-missing-target-legacy-success");
                owner.Thread.Push(sleeping);
                sleeping.Args[1] = 80;
                owner.Thread.ScheduleIdleStart = 0;
                check(new VMSleep().Execute(sleeping, new VMSleepOperand { StackVarToDec = 1 }) ==
                    VMPrimitiveExitCode.GOTO_TRUE && sleeping.Args[1] == 79 && !owner.Thread.Interrupt,
                    "tso-interrupt-decrement-unchanged");
                vm.TS1 = true;
                context.SetTutorialObject(null);
                check(!command.Execute(vm, null), "no-owner-no-action");
                context.SetTutorialObject(owner);
                var names = owner.TreeByName;
                owner.TreeByName = new Dictionary<string, VMTreeByNameTableEntry>();
                check(!command.Execute(vm, null), "missing-named-tree-no-action");
                owner.TreeByName = names;

                using (var memory = new MemoryStream())
                {
                    var envelope = new VMNetCommand(command);
                    using (var writer = new BinaryWriter(memory, System.Text.Encoding.UTF8, true))
                        envelope.SerializeInto(writer);
                    check(memory.ToArray().SequenceEqual(new byte[] { 49, 0x44, 0x33, 0x22, 0x11 }),
                        "command49-exact-five-byte-envelope");
                    memory.Position = 0;
                    var restored = new VMNetCommand();
                    using (var reader = new BinaryReader(memory, System.Text.Encoding.UTF8, true)) restored.Deserialize(reader);
                    check(restored.Command is VMNetTS1TutorialInfoCmd && restored.Command.ActorUID == 0x11223344 &&
                        memory.Position == 5, "command49-roundtrip");
                }
            }
            catch (Exception e) { failures.Add(e.ToString()); }
            finally { VM.UseWorld = oldWorld; VM.GlobTS1 = oldTS1; }
            diagnostics = failures.Count == 0
                ? "native named-tree bytes; isolated main-stack/dialog preservation; pause redisplay return; opcode0/17/other/missing notifications; TS1 wake and TSO isolation; command49 roundtrip passed"
                : string.Join("; ", failures);
            return failures.Count == 0;
        }

        private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();

        private static VMStackFrame Frame(VMEntity owner, ushort opcode)
        {
            return new VMStackFrame { Caller = owner, Callee = owner, CodeOwner = owner.Object,
                Routine = new VMRoutine { Instructions = new[] { new VMInstruction { Opcode = opcode,
                    Operand = new VMSleepOperand(), TruePointer = 254, FalsePointer = 255 } } },
                StackObject = owner, Args = new short[] { 200, 80, 23, 29 } };
        }
    }
}
