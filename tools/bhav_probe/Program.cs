using System;
using System.Linq;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;
class Prog
{
    static string[] OPS = { "GreaterThan","LessThan","Equals","PlusEquals","MinusEquals","Assign","MulEquals","DivEquals","IsFlagSet","SetFlag","ClearFlag","IncAndLessThan","ModEquals","AndEquals","GEQ","LEQ","NotEqual","DecAndGT","Push","Pop","SqrtRHS" };
    static string[] SCOPE = { "MyObjAttr","StackObjAttr","TgtObjAttr","MyObj","StackObj","TgtObj","Global","Literal","Temps","Params","StackObjID","TempByTemp","TreeAdRange","StackObjTemp","MyMotives","StackObjMotives","StackObjSlot","StackObjMotiveByTemp","MyPersonData","StackObjPersonData","MySlot","StackObjDef","StackObjAttrByParam","RoomByTemp0","NeighborInStackObj","Local","Tuning","DynSpriteFlag","TreeAdPersonality","TreeAdMin","MyPersonDataByTemp","StackObjPersonDataByTemp","NeighborPersonData","JobData","NeighborhoodData","StackObjFunction","MyTypeAttr","StackObjTypeAttr","NeighborsObjDef","Unused","LocalByTemp","StackObjAttrByTemp","TempXL","CityTime","TSOStdTime","GameTime","MyList","StackObjList","MoneyOverHead32","MyLeadTileAttr","StackObjLeadTileAttr","MyLeadTile","StackObjLeadTile","StackObjMasterDef","FeatureEnableLevel","","","","","","MyAvatarID" };
    static string S(byte b) { return (b < SCOPE.Length) ? SCOPE[b] : ("?"+b); }
    static int Main(string[] args)
    {
        var path = args[0];
        var mode = args.Length > 1 ? args[1] : "dump";
        // mode=scanmood: writes to MyMotives 3/13; mode=scancensor: writes to MyPersonData 30
        var iff = new IffFile(path);
        var bhavs = iff.List<BHAV>();

        if (mode == "callers")
        {
            // opcode >= 256 is a routine call; routine id = opcode % 4096.
            // kind: >=8192 semi-global, >=4096 private (same IFF), else global.
            var names = new System.Collections.Generic.Dictionary<ushort, string>();
            foreach (var b2 in bhavs) if (b2 != null) names[b2.ChunkID] = (b2.ChunkLabel ?? "").Trim();
            foreach (var b in bhavs)
            {
                var label = (b.ChunkLabel ?? "").Trim();
                if (b.Instructions == null) continue;
                for (int i = 0; i < b.Instructions.Length; i++)
                {
                    var ins = b.Instructions[i];
                    if (ins.Opcode >= 256)
                    {
                        var raw = ins.Opcode;
                        var kind = raw >= 8192 ? "semi" : raw >= 4096 ? "priv" : "glob";
                        var target = names.ContainsKey(raw) ? names[raw] : ("?" + raw);
                        Console.WriteLine(label + " :: ins" + i + " CALL[" + kind + "] raw=" + raw + " (" + target + ")");
                    }
                }
            }
            foreach (var k in names.OrderBy(x => x.Key)) Console.WriteLine("#id " + k.Key + " : " + k.Value);
            return 0;
        }

        if (mode == "disasm")
        {
            var names = new System.Collections.Generic.Dictionary<ushort, string>();
            foreach (var b2 in bhavs) if (b2 != null) names[b2.ChunkID] = (b2.ChunkLabel ?? "").Trim();
            var filter = args.Length > 2 ? args[2] : "";
            foreach (var b in bhavs)
            {
                var label = (b.ChunkLabel ?? "").Trim();
                if (filter != "" && label != filter) continue;
                if (b.Instructions == null) continue;
                Console.WriteLine("### " + label + " (id=" + b.ChunkID + ")");
                for (int i = 0; i < b.Instructions.Length; i++)
                {
                    var ins = b.Instructions[i];
                    if (ins.Opcode >= 256)
                    {
                        var raw = ins.Opcode;
                        var kind = raw >= 8192 ? "semi" : raw >= 4096 ? "priv" : "glob";
                        var target = names.ContainsKey(raw) ? names[raw] : ("?" + raw);
                        Console.WriteLine("  ins" + i + " CALL[" + kind + "] " + raw + " (" + target + ")");
                    }
                    else if (ins.Opcode == 2 && ins.Operand != null && ins.Operand.Length == 8)
                    {
                        var o = ins.Operand;
                        short lhs = (short)(o[0] | (o[1] << 8));
                        byte op = o[5]; byte ls = o[6]; byte rs = o[7];
                        string opn = (op < OPS.Length) ? OPS[op] : ("?"+op);
                        string rsval = (rs == 7) ? ((short)(o[2] | (o[3] << 8))).ToString() : ((short)(o[2] | (o[3] << 8))).ToString();
                        Console.WriteLine("  ins" + i + " EXPR " + opn + " lhs=" + lhs + "(" + S(ls) + ") rhs=" + rsval + "(" + S(rs) + ")");
                    }
                    else
                    {
                        Console.WriteLine("  ins" + i + " op=" + ins.Opcode + " t/f=" + ins.TruePointer + "/" + ins.FalsePointer + " opd=" + (ins.Operand == null ? "-" : string.Join(",", ins.Operand)));
                    }
                }
            }
            return 0;
        }

        if (mode == "objm")
        {
            var objt = iff.Get<OBJT>(0);
            var objm = iff.Get<OBJM>(1);
            if (objm == null) { Console.WriteLine("no OBJM"); return 0; }
            objm.Prepare((ushort typeID) =>
            {
                var entry = objt.Entries[typeID - 1];
                return new OBJMResource() { OBJT = entry };
            });
            foreach (var pair in objm.ObjectData)
            {
                var inst = pair.Value.Instance;
                if (inst.PersonData.HasValue)
                {
                    var pd = inst.PersonData.Value;
                    var idx = new int[] { 5,6,7,8,9,13,14,15,16 };
                    var vals = idx.Select(i => i < pd.PersonData.Length ? pd.PersonData[i].ToString() : "-");
                    Console.WriteLine("PERSON objId=" + pair.Value.ObjectID + " pd[5,6,7,8,9,13,14,15,16]={" + string.Join(",", vals) + "} MotiveData={" + string.Join(",", (pd.MotiveData ?? new float[0]).Select(f => f.ToString("0.##"))) + "} MotiveOld={" + string.Join(",", (pd.MotiveDataOld ?? new float[0]).Select(f => f.ToString("0.##"))) + "} MDeltas=" + (pd.MotiveDeltas == null ? "0" : pd.MotiveDeltas.Length.ToString()));
                }
            }
            return 0;
        }

        if (mode == "bc")
        {
            foreach (var bc in iff.List<BCON>())
            {
                Console.WriteLine("BCON id=" + bc.ChunkID + "[" + (bc.Constants == null ? "null" : bc.Constants.Length.ToString()) + "]: " + (bc.Constants == null ? "" : string.Join(",", bc.Constants.Select(x => x.ToString()))));
            }
            return 0;
        }

        foreach (var b in bhavs)
        {
            var label = (b.ChunkLabel ?? "").Trim();
            if (b.Instructions == null) continue;
            for (int i = 0; i < b.Instructions.Length; i++)
            {
                var ins = b.Instructions[i];
                if (ins.Opcode == 2 && ins.Operand != null && ins.Operand.Length == 8)
                {
                    var o = ins.Operand;
                    short lhs = (short)(o[0] | (o[1] << 8));
                    byte op = o[5]; byte ls = o[6]; byte rs = o[7];
                    int targetScope = (mode == "scancensor") ? 18 : 14; // MyPersonData / MyMotives
                    int targetData = (mode == "scancensor") ? 30 : (o[0] == 1 ? 13 : 3);
                    if (ls == targetScope && (mode == "scancensor" ? lhs == 30 : (lhs == 3 || lhs == 13)))
                    {
                        string opn = (op < OPS.Length) ? OPS[op] : ("?"+op);
                        Console.WriteLine(label + " :: ins" + i + " EXPR " + opn + " lhs=" + lhs + "(" + S(ls) + ") rhs=" + (short)(o[2] | (o[3] << 8)) + "(" + S(rs) + ")");
                    }
                }
            }
        }
        return 0;
    }
}
