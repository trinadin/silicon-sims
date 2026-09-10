using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using FSO.LotView;
using FSO.LotView.Model;
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

    private static short Expected(short tileX, short tileY, int rotation)
    {
        int rotatedX;
        int rotatedY;
        switch (rotation)
        {
            case 1:
                rotatedX = 63 - tileY;
                rotatedY = tileX;
                break;
            case 2:
                rotatedX = 63 - tileX;
                rotatedY = 63 - tileY;
                break;
            case 3:
                rotatedX = tileY;
                rotatedY = 63 - tileX;
                break;
            default:
                rotatedX = tileX;
                rotatedY = tileY;
                break;
        }
        return (short)-(rotatedX + rotatedY);
    }

    private static int Main()
    {
        var previousUseWorld = VM.UseWorld;
        try
        {
            VM.UseWorld = false;

            var vm = (VM)RuntimeHelpers.GetUninitializedObject(typeof(VM));
            vm.TS1 = true;
            var context = (VMContext)RuntimeHelpers.GetUninitializedObject(typeof(VMContext));
            context.VM = vm;
            SetInternalProperty(vm, nameof(VM.Context), context);

            var state = (WorldState)RuntimeHelpers.GetUninitializedObject(typeof(WorldState));
            var world = (World)RuntimeHelpers.GetUninitializedObject(typeof(World));
            world.State = state;
            SetInternalProperty(context, nameof(VMContext.World), world);

            var target = (VMGameObject)RuntimeHelpers.GetUninitializedObject(typeof(VMGameObject));
            target.Position = LotTilePos.FromBigTile(0, 0, 1);
            var thread = new VMThread(context, null, 1);
            var frame = new VMStackFrame
            {
                Thread = thread,
                StackObject = target,
            };
            var operand = new VMGenericTS1CallOperand
            {
                Call = VMGenericTS1CallMode.GetDistanceToCameraInTemp0,
            };
            var handler = new VMGenericTS1Call();

            var verified = 0;
            for (var rotation = 0; rotation < 4; rotation++)
            {
                state.SilentRotation = (WorldRotation)rotation;
                for (short tileX = 0; tileX < 64; tileX++)
                {
                    for (short tileY = 0; tileY < 64; tileY++)
                    {
                        VM.UseWorld = false;
                        target.Position = LotTilePos.FromBigTile(tileX, tileY, 1);
                        VM.UseWorld = true;

                        const short sentinel = 12345;
                        thread.TempRegisters[0] = sentinel;
                        var exit = handler.Execute(frame, operand);
                        var expected = Expected(tileX, tileY, rotation);
                        var actual = thread.TempRegisters[0];
                        if (exit != VMPrimitiveExitCode.GOTO_TRUE || actual != expected)
                        {
                            throw new Exception(
                                $"r{rotation} ({tileX},{tileY}): expected true/{expected}, " +
                                $"got {exit}/{actual}");
                        }
                        verified++;
                    }
                }
            }

            // Port-only headless behavior: the original case reads the live world
            // rotation. Simitone uses orientation 0 when no rendered world exists so
            // non-visual interpreters remain deterministic.
            VM.UseWorld = false;
            target.Position = LotTilePos.FromBigTile(11, 23, 1);
            state.SilentRotation = WorldRotation.BottomRight;
            thread.TempRegisters[0] = 12345;
            var fallbackExit = handler.Execute(frame, operand);
            if (fallbackExit != VMPrimitiveExitCode.GOTO_TRUE
                || thread.TempRegisters[0] != Expected(11, 23, 0))
            {
                throw new Exception(
                    "no-world fallback: expected true/-34, got "
                    + fallbackExit + "/" + thread.TempRegisters[0]);
            }

            Console.WriteLine(
                "PASS: Generic Sims Call mode 12 actual dispatch verified for "
                + verified + " tile/rotation tuples; fixed 64x64 transform, Temp0 write, "
                + "true exit, and port-only no-world rotation-0 fallback.");
            return 0;
        }
        finally
        {
            VM.UseWorld = previousUseWorld;
        }
    }
}
