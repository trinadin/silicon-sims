using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using FSO.Content.TS1;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;
using FSO.SimAntics;
using FSO.SimAntics.Engine;
using FSO.SimAntics.Engine.Scopes;
using FSO.SimAntics.Engine.Utils;
using FSO.SimAntics.JIT.Translation.CSharp;
using FSO.SimAntics.JIT.Translation.CSharp.Engine;
using ContentManager = FSO.Content.Content;

internal static class Program
{
    private static void Equal<T>(T expected, T actual, string name)
    {
        if (!Equals(expected, actual))
        {
            throw new Exception($"{name}: expected {expected}, got {actual}");
        }
    }

    private static void ThrowsSimantics(Action action, string name)
    {
        try
        {
            action();
        }
        catch (VMSimanticsException)
        {
            return;
        }
        throw new Exception(name + ": expected VMSimanticsException");
    }

    private static int Main(string[] args)
    {
        if (args.Length != 1 || !File.Exists(args[0]))
        {
            Console.Error.WriteLine("usage: neighborhood_data_probe <original Neighborhood.iff>");
            return 2;
        }

        var iff = new IffFile(args[0]);
        var ngbh = iff.List<NGBH>()?.FirstOrDefault();
        if (ngbh?.NeighborhoodData == null)
        {
            throw new Exception("original fixture has no NGBH neighborhood-data words");
        }
        Equal(16, ngbh.NeighborhoodData.Length, "NGBH word count");

        var originalWord1 = ngbh.NeighborhoodData[1];
        var originalWord2 = ngbh.NeighborhoodData[2];
        if (originalWord1 == 0 && originalWord2 == 0)
        {
            throw new Exception("fixture must distinguish the NGBH-backed path from the former constant zero");
        }

        var instanceField = typeof(ContentManager).GetField(
            "INSTANCE", BindingFlags.Static | BindingFlags.NonPublic);
        if (instanceField == null) throw new Exception("Content.INSTANCE field not found");
        var previousContent = instanceField.GetValue(null);

        try
        {
            var content = (ContentManager)RuntimeHelpers.GetUninitializedObject(typeof(ContentManager));
            content.TS1 = true;
            content.Neighborhood = (TS1NeighborhoodProvider)RuntimeHelpers.GetUninitializedObject(
                typeof(TS1NeighborhoodProvider));
            content.Neighborhood.Neighborhood = ngbh;
            instanceField.SetValue(null, content);

            var vm = (VM)RuntimeHelpers.GetUninitializedObject(typeof(VM));
            vm.TS1 = true;
            var vmContext = (VMContext)RuntimeHelpers.GetUninitializedObject(typeof(VMContext));
            vmContext.VM = vm;
            var frame = new VMStackFrame
            {
                Thread = new VMThread(vmContext, null, 1)
            };

            Equal(originalWord1,
                VMMemory.GetVariable(frame, VMVariableScope.NeighborhoodData, 1),
                "interpreter word 1 from parsed NGBH");
            Equal(originalWord2,
                VMMemory.GetVariable(frame, VMVariableScope.NeighborhoodData, 2),
                "interpreter word 2 from parsed NGBH");

            ngbh.NeighborhoodData[1] = -1234;
            ngbh.NeighborhoodData[2] = 2345;
            Equal((short)-1234,
                VMMemory.GetVariable(frame, VMVariableScope.NeighborhoodData, 1),
                "signed word 1");
            Equal((short)2345,
                VMMemory.GetVariable(frame, VMVariableScope.NeighborhoodData, 2),
                "signed word 2");

            ThrowsSimantics(
                () => VMMemory.GetVariable(frame, VMVariableScope.NeighborhoodData, -1),
                "negative index");
            ThrowsSimantics(
                () => VMMemory.GetVariable(frame, VMVariableScope.NeighborhoodData, 0),
                "original-engine excluded word 0");
            ThrowsSimantics(
                () => VMMemory.GetVariable(frame, VMVariableScope.NeighborhoodData, 16),
                "upper bound");

            vm.TS1 = false;
            ThrowsSimantics(
                () => VMMemory.GetVariable(frame, VMVariableScope.NeighborhoodData, 2),
                "TS1-only scope");
            vm.TS1 = true;

            ThrowsSimantics(
                () => VMMemory.SetVariable(frame, VMVariableScope.NeighborhoodData, 2, 99),
                "read-only scope");
            Equal((short)2345, ngbh.NeighborhoodData[2], "read-only write preservation");

            var translationContext = new CSTranslationContext
            {
                TS1 = true,
                CurrentClass = new CSTranslationClass()
            };
            Equal(
                "VMMemory.GetVariable(context, VMVariableScope.NeighborhoodData, 2)",
                CSScopeMemory.GetExpression(
                    translationContext, VMVariableScope.NeighborhoodData, 2, false),
                "JIT expression delegates to interpreter contract");

            Console.WriteLine(
                $"PASS: NGBH v0x{ngbh.Version:x} words=16 original[1]={originalWord1} " +
                $"original[2]={originalWord2}; interpreter read + JIT delegation, bounds=1..15, writes=read-only.");
            return 0;
        }
        finally
        {
            instanceField.SetValue(null, previousContent);
        }
    }
}
