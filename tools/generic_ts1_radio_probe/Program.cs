using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using FSO.SimAntics;
using FSO.SimAntics.Engine;
using FSO.SimAntics.Model;
using FSO.SimAntics.Primitives;

internal static class Program
{
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

    private static int Main()
    {
        var vm = (VM)RuntimeHelpers.GetUninitializedObject(typeof(VM));
        vm.TS1 = true;
        vm.GlobalState = new short[38];
        var context = (VMContext)RuntimeHelpers.GetUninitializedObject(typeof(VMContext));
        context.VM = vm;
        SetInternalProperty(vm, nameof(VM.Context), context);

        var thread = new VMThread(context, null, 1);
        var decoy = (VMGameObject)RuntimeHelpers.GetUninitializedObject(
            typeof(VMGameObject));
        decoy.ObjectID = 321;
        var frame = new VMStackFrame
        {
            Thread = thread,
            StackObject = null,
        };
        VMContext.InitVMConfig(true);
        var registration = VMContext.Primitives[1];
        if (registration?.Opcode != 1
            || registration.Name != "generic_sims_call"
            || registration.OperandModel != typeof(VMGenericTS1CallOperand)
            || registration.GetHandler() is not VMGenericTS1Call)
        {
            throw new Exception("TS1 opcode 1 registration is not generic_sims_call");
        }
        var operand = new VMGenericTS1CallOperand();
        operand.Read(new byte[] { 14, 0, 0, 0, 0, 0, 0, 0 });
        if (operand.Call != VMGenericTS1CallMode.HouseRadioStationEqualsTemp0
            || (byte)operand.Call != 14)
        {
            throw new Exception("raw mode-14 operand did not decode to the expected call");
        }
        var handler = registration.GetHandler();
        var values = new short[]
        {
            short.MinValue,
            -32767,
            -1,
            0,
            1,
            2,
            30,
            short.MaxValue,
        };

        for (var caseIndex = 0; caseIndex < values.Length; caseIndex++)
        {
            for (var global = 0; global < vm.GlobalState.Length; global++)
            {
                vm.GlobalState[global] = (short)(1000 + global);
            }
            var before = (short[])vm.GlobalState.Clone();
            var value = values[caseIndex];
            for (var temp = 0; temp < thread.TempRegisters.Length; temp++)
            {
                thread.TempRegisters[temp] = (short)(-20000 + temp);
            }
            thread.TempRegisters[0] = value;
            thread.TempRegisters[1] = -12345;
            var tempsBefore = (short[])thread.TempRegisters.Clone();
            frame.StackObject = (caseIndex & 1) == 0 ? null : decoy;

            var exit = handler.Execute(frame, operand);
            if (exit != VMPrimitiveExitCode.GOTO_TRUE)
            {
                throw new Exception(value + ": exit was " + exit);
            }
            if (vm.GlobalState[31] != value)
            {
                throw new Exception(
                    value + ": global 31 was " + vm.GlobalState[31]);
            }
            if (!thread.TempRegisters.SequenceEqual(tempsBefore))
            {
                throw new Exception(value + ": temp registers changed unexpectedly");
            }
            var expectedStackObjectID = (short)(((caseIndex & 1) == 0) ? 0 : 321);
            if (frame.StackObjectID != expectedStackObjectID)
            {
                throw new Exception(value + ": stack object fixture ID was not retained");
            }
            if (Enumerable.Range(0, vm.GlobalState.Length)
                .Any(index => index != 31 && vm.GlobalState[index] != before[index]))
            {
                throw new Exception(value + ": a non-31 global changed");
            }
        }

        Console.WriteLine(
            "PASS: Generic Sims Call mode 14 actual dispatch verified for "
            + values.Length + " signed Temp0 values through raw operand 0e and the "
            + "registered opcode-1 handler; exact Global[31] overwrite, full Temp "
            + "preservation, null/nonzero Stack Object independence, other-global "
            + "preservation, and true exit.");
        return 0;
    }
}
