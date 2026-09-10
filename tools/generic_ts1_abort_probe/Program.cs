using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using FSO.Content;
using FSO.Files.Formats.IFF.Chunks;
using FSO.SimAntics;
using FSO.SimAntics.Engine;
using FSO.SimAntics.Model;
using FSO.SimAntics.Primitives;

internal static class Program
{
    private sealed class StackClearingRoutine : VMRoutine
    {
        public bool Ran;

        public override VMPrimitiveExitCode Execute(
            VMStackFrame frame, out VMInstruction instruction)
        {
            frame.StackObject.Thread.Stack.Clear();
            Ran = true;
            instruction = Instructions[0];
            return VMPrimitiveExitCode.RETURN_TRUE;
        }
    }

    private sealed class Fixture
    {
        public VM VM;
        public VMContext Context;
    }

    private static void SetInternalProperty(object target, string name, object value)
    {
        var property = target.GetType().GetProperty(
            name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property?.SetMethod == null)
        {
            throw new Exception(target.GetType().Name + "." + name + " setter not found");
        }
        property.SetValue(target, value);
    }

    private static void SetPrivateField(object target, string name, object value)
    {
        var field = target.GetType().GetField(
            name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
        {
            throw new Exception(target.GetType().Name + "." + name + " field not found");
        }
        field.SetValue(target, value);
    }

    private static Fixture NewFixture()
    {
        var vm = (VM)RuntimeHelpers.GetUninitializedObject(typeof(VM));
        vm.TS1 = true;
        vm.Entities = new List<VMEntity>();
        vm.Scheduler = new VMScheduler(vm);

        var context = (VMContext)RuntimeHelpers.GetUninitializedObject(typeof(VMContext));
        context.VM = vm;
        context.ObjectQueries = new VMObjectQueries(context);
        SetInternalProperty(vm, nameof(VM.Context), context);
        return new Fixture { VM = vm, Context = context };
    }

    private static VMGameObject NewObject()
    {
        var result = (VMGameObject)RuntimeHelpers.GetUninitializedObject(typeof(VMGameObject));
        result.ObjectData = new short[80];
        result.EntryPoints = new OBJfFunctionEntry[33];
        return result;
    }

    private static VMGameObject NewStackClearingObject(
        out StackClearingRoutine routine)
    {
        var result = NewObject();
        routine = new StackClearingRoutine
        {
            ID = 4096,
            Instructions = new[] { new VMInstruction() },
        };
        var resource = (GameObjectResource)RuntimeHelpers.GetUninitializedObject(
            typeof(GameObjectResource));
        resource.RoutineCache = new Dictionary<ushort, object>
        {
            [routine.ID] = routine,
        };
        result.Object = new GameObject
        {
            OBJ = new OBJD(),
            Resource = resource,
        };
        result.EntryPoints[4] = new OBJfFunctionEntry
        {
            ActionFunction = routine.ID,
        };
        return result;
    }

    private static VMAvatar NewAvatar(Fixture fixture)
    {
        var result = (VMAvatar)RuntimeHelpers.GetUninitializedObject(typeof(VMAvatar));
        result.ObjectData = new short[80];
        result.EntryPoints = new OBJfFunctionEntry[33];
        SetPrivateField(result, "PersonData", new short[101]);
        result.SetPersonData(VMPersonDataVariable.Priority, 50);
        result.Thread = new VMThread(fixture.Context, result, 4);
        fixture.Context.ObjectQueries.Avatars.Add(result);
        fixture.VM.Entities.Add(result);
        return result;
    }

    private static VMQueuedAction NewAction(
        ushort uid,
        VMEntity callee,
        VMEntity stackObject = null,
        VMEntity iconOwner = null,
        TTABFlags flags = 0)
    {
        var result = new VMQueuedAction
        {
            UID = uid,
            Callee = callee,
            StackObject = stackObject ?? callee,
            Flags = flags,
            Mode = VMQueueMode.Normal,
            Priority = 50,
        };
        if (iconOwner != null) result.IconOwner = iconOwner;
        return result;
    }

    private static void SetActive(
        VMAvatar avatar,
        VMQueuedAction action,
        VMEntity frameCallee,
        VMEntity frameStackObject)
    {
        avatar.Thread.Queue.Add(action);
        avatar.Thread.ActiveQueueBlock = 0;
        avatar.Thread.Stack.Add(
            new VMStackFrame
            {
                Thread = avatar.Thread,
                Caller = avatar,
                Callee = frameCallee,
                StackObject = frameStackObject,
                ActionTree = true,
            });
    }

    private static VMPrimitiveExitCode Dispatch(
        Fixture fixture, VMEntity target, short temp0 = 12345)
    {
        var thread = new VMThread(fixture.Context, null, 1);
        thread.TempRegisters[0] = temp0;
        var frame = new VMStackFrame
        {
            Thread = thread,
            StackObject = target,
        };
        var exit = new VMGenericTS1Call().Execute(
            frame,
            new VMGenericTS1CallOperand
            {
                Call = VMGenericTS1CallMode.AbortInteractions,
            });
        if (thread.TempRegisters[0] != temp0)
        {
            throw new Exception(
                "mode 13 changed Temp0: expected " + temp0 + ", got "
                + thread.TempRegisters[0]);
        }
        return exit;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void RequireGracefulCancel(VMAvatar avatar, ushort uid, string label)
    {
        var action = avatar.Thread.Queue.SingleOrDefault(item => item.UID == uid);
        Require(action != null, label + ": action was removed instead of retained");
        Require(action.NotifyIdle, label + ": NotifyIdle was not set");
        Require(
            avatar.GetFlag(VMEntityFlags.InteractionCanceled),
            label + ": InteractionCanceled was not set");
        Require(
            avatar.GetPersonData(VMPersonDataVariable.Priority) == 0,
            label + ": priority was not cleared");
    }

    private static void VerifyObjectTargetMatrix()
    {
        var fixture = NewFixture();
        var target = NewObject();
        var decoy = NewObject();

        var queued = NewAvatar(fixture);
        queued.Thread.Queue.Add(
            NewAction(1, target, stackObject: decoy, iconOwner: decoy));
        queued.Thread.Queue.Add(NewAction(2, decoy, iconOwner: target));
        queued.Thread.Queue.Add(
            NewAction(
                3,
                target,
                stackObject: decoy,
                iconOwner: decoy,
                flags: TTABFlags.MustRun));
        queued.Thread.Queue.Add(NewAction(4, decoy, stackObject: target));
        queued.Thread.Queue.Add(NewAction(5, decoy));

        var active = NewAvatar(fixture);
        SetActive(
            active,
            NewAction(10, target, stackObject: decoy, iconOwner: decoy),
            decoy,
            decoy);

        var frameCallee = NewAvatar(fixture);
        SetActive(frameCallee, NewAction(20, decoy), target, decoy);

        var frameStack = NewAvatar(fixture);
        SetActive(frameStack, NewAction(30, decoy), decoy, target);

        var unrelated = NewAvatar(fixture);
        unrelated.Thread.Queue.Add(NewAction(40, decoy));

        var noThread = NewAvatar(fixture);
        noThread.Thread = null;

        var exit = Dispatch(fixture, target);
        Require(exit == VMPrimitiveExitCode.GOTO_TRUE, "object target: exit was " + exit);

        var queuedUIDs = queued.Thread.Queue.Select(item => item.UID).ToArray();
        Require(
            queuedUIDs.SequenceEqual(new ushort[] { 3, 4, 5 }),
            "queue filtering mismatch: " + string.Join(",", queuedUIDs));
        RequireGracefulCancel(queued, 3, "queued VM non-skippable callee match");
        Require(
            !queued.Thread.Queue.Single(item => item.UID == 4).NotifyIdle,
            "StackObject-only queued action was incorrectly cancelled");
        Require(
            !queued.Thread.Queue.Single(item => item.UID == 5).NotifyIdle,
            "unrelated queued action was incorrectly cancelled");

        RequireGracefulCancel(active, 10, "active callee match");
        RequireGracefulCancel(frameCallee, 20, "active frame.Callee match");
        RequireGracefulCancel(frameStack, 30, "active frame.StackObject match");
        Require(
            unrelated.Thread.Queue.Count == 1
            && unrelated.Thread.Queue[0].UID == 40
            && !unrelated.Thread.Queue[0].NotifyIdle
            && !unrelated.GetFlag(VMEntityFlags.InteractionCanceled)
            && unrelated.GetPersonData(VMPersonDataVariable.Priority) == 50,
            "unrelated avatar was modified");
    }

    private static void VerifyTargetAvatarExclusion()
    {
        var fixture = NewFixture();
        var target = NewAvatar(fixture);
        var decoy = NewObject();
        target.Thread.Queue.Add(NewAction(50, target));

        var other = NewAvatar(fixture);
        other.Thread.Queue.Add(NewAction(51, target));
        other.Thread.Queue.Add(NewAction(52, decoy));

        var exit = Dispatch(fixture, target, -2345);
        Require(exit == VMPrimitiveExitCode.GOTO_TRUE, "avatar target: exit was " + exit);
        Require(
            target.Thread.Queue.Count == 1
            && target.Thread.Queue[0].UID == 50
            && !target.Thread.Queue[0].NotifyIdle
            && !target.GetFlag(VMEntityFlags.InteractionCanceled)
            && target.GetPersonData(VMPersonDataVariable.Priority) == 50,
            "target avatar's own queue was not excluded");
        Require(
            other.Thread.Queue.Count == 1 && other.Thread.Queue[0].UID == 52,
            "other avatar's target action was not removed");
    }

    private static void VerifyQueueSkippedOrdering()
    {
        var fixture = NewFixture();
        var target = NewObject();
        var decoy = NewObject();
        var mutator = NewStackClearingObject(out var queueSkipped);
        var avatar = NewAvatar(fixture);
        SetActive(avatar, NewAction(70, decoy), decoy, target);
        avatar.Thread.Queue.Add(
            NewAction(71, mutator, stackObject: decoy, iconOwner: target));

        var exit = Dispatch(fixture, target);
        Require(exit == VMPrimitiveExitCode.GOTO_TRUE, "queue-skipped order: exit was " + exit);
        RequireGracefulCancel(avatar, 70, "active frame before Queue Skipped");
        Require(queueSkipped.Ran, "matching queue entry did not run Queue Skipped");
        Require(
            avatar.Thread.Stack.Count == 0,
            "Queue Skipped callback did not clear the actor stack");
        Require(
            avatar.Thread.Queue.All(action => action.UID != 71),
            "matching queued action survived Queue Skipped ordering probe");
    }

    private static int Main()
    {
        var previousUseWorld = VM.UseWorld;
        var previousGlobTS1 = VM.GlobTS1;
        try
        {
            VM.UseWorld = false;
            VM.GlobTS1 = true;
            VerifyObjectTargetMatrix();
            VerifyTargetAvatarExclusion();
            VerifyQueueSkippedOrdering();
            Console.WriteLine(
                "PASS: Generic Sims Call mode 13 actual dispatch verified: "
                + "isolated Callee/IconOwner queue filtering, VM non-skippable "
                + "graceful cancellation, active Callee and frame "
                + "Callee/StackObject unwind, unrelated-state preservation, "
                + "target-person exclusion, active-before-Queue-Skipped ordering, "
                + "Temp0 preservation, and true exit.");
            return 0;
        }
        finally
        {
            VM.UseWorld = previousUseWorld;
            VM.GlobTS1 = previousGlobTS1;
        }
    }
}
