using System;
using FSO.SimAntics.Engine.Scopes;
using FSO.SimAntics.JIT.Translation.CSharp;
using FSO.SimAntics.JIT.Translation.CSharp.Engine;

class Program
{
    private static void Equal(string expected, string actual, string name)
    {
        if (actual != expected)
        {
            throw new Exception(name + " mismatch: expected `" + expected + "`, got `" + actual + "`");
        }
    }

    static int Main()
    {
        var context = new CSTranslationContext
        {
            CurrentClass = new CSTranslationClass()
        };

        Equal("VMMemory.GetTreeAd(context, 1, 5)",
            CSScopeMemory.GetExpression(context, VMVariableScope.TreeAdRange, 5, false),
            "TreeAdRange read");
        Equal("VMMemory.SetTreeAd(context, 1, 5, 42);",
            CSScopeMemory.SetStatement(context, VMVariableScope.TreeAdRange, 5, "=", "42", false),
            "TreeAdRange write");
        Equal("VMMemory.SetTreeAd(context, 1, 5, (short)(VMMemory.GetTreeAd(context, 1, 5) + 7));",
            CSScopeMemory.SetStatement(context, VMVariableScope.TreeAdRange, 5, "+=", "7", false),
            "TreeAdRange compound write");

        Equal("VMMemory.GetTreeAd(context, 2, 7)",
            CSScopeMemory.GetExpression(context, VMVariableScope.TreeAdPersonalityVar, 7, false),
            "TreeAdPersonalityVar read");
        Equal("VMMemory.SetTreeAd(context, 2, 7, 0);",
            CSScopeMemory.SetStatement(context, VMVariableScope.TreeAdPersonalityVar, 7, "=", "0", false),
            "TreeAdPersonalityVar write");

        Equal("VMMemory.GetTreeAd(context, 0, 3)",
            CSScopeMemory.GetExpression(context, VMVariableScope.TreeAdMin, 3, false),
            "TreeAdMin read");
        Equal("VMMemory.SetTreeAd(context, 0, 3, -10);",
            CSScopeMemory.SetStatement(context, VMVariableScope.TreeAdMin, 3, "=", "-10", false),
            "TreeAdMin write");

        if (CSTranslator.JITVersion != 2)
        {
            throw new Exception("JIT cache version must be 2 after the TreeAd emitter change.");
        }

        Console.WriteLine("PASS: TreeAd JIT emitter matches VMMemory type mapping for scopes 12/28/29.");
        return 0;
    }
}
