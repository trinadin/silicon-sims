// Probe: parse a staged original TS1 house save (HouseNN.iff) through the engine's OWN
// parsers (FSO.Files OBJM/OBJT) and dump, for every instance: ObjectID, neighbor-id
// (PersonData[31]), whether it is a person, and every saved thread-frame StackObjectID.
// Purpose: determine whether, IN THE ORIGINAL SAVE DATA, an avatar's in-lot ObjectID
// equals its neighbour id (the NBRS relationship-matrix key the engine's UseNeighbor
// path looks up against). This is static evidence from the original game's own save.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;

class Prog
{
    static int Main(string[] args)
    {
        var path = args.Length > 0 ? args[0] : null;
        var dumpStacks = args.Skip(1).Any(x => x == "--stacks");
        var dumpMotives = args.Skip(1).Any(x => x == "--motives");
        if (path == null || !File.Exists(path)) { Console.WriteLine("usage: house_probe <HouseNN.iff>"); return 1; }
        try
        {
            var iff = new IffFile(path);
            var objtChunk = iff.List<OBJT>()?.FirstOrDefault();
            if (objtChunk == null) { Console.WriteLine("no OBJT"); return 0; }
            var objm = iff.List<OBJM>()?.FirstOrDefault();
            if (objm == null) { Console.WriteLine("no OBJM"); return 0; }
            objm.Prepare((ushort typeID) =>
            {
                var entry = objtChunk.Entries[typeID - 1];
                return new OBJMResource() { OBJD = null, OBJT = entry };
            });
            Console.WriteLine("OBJM version=" + objm.Version + " instances=" + objm.ObjectData.Count);
            foreach (var kv in objm.ObjectData.OrderBy(x => x.Key))
            {
                var m = kv.Value;
                var inst = m.Instance;
                var isPerson = inst.PersonData.HasValue;
                var nid = isPerson ? inst.PersonData.Value.PersonData[31] : (short)0;
                var gender = isPerson ? inst.PersonData.Value.PersonData[65] : (short)-1;
                var ptype = isPerson ? inst.PersonData.Value.PersonData[32] : (short)-1;
                var frames = inst.Stack?.Select(f => f.StackObjectID).ToArray() ?? new short[0];
                var frameSample = string.Join(",", frames.Take(12).Select(f => f.ToString()));
                Console.WriteLine("  objectID=" + m.ObjectID + " isPerson=" + isPerson +
                    " neighborID=" + nid + " gender=" + gender + " ptype=" + ptype +
                    " name=" + (m.Name ?? (isPerson ? inst.OBJT?.Name : inst.OBJT?.Name ?? "?")) +
                    " frames=" + frames.Length + " stackIDs=[" + frameSample + "]");
                if (dumpStacks && inst.Stack != null)
                {
                    for (var frameIndex = 0; frameIndex < inst.Stack.Length; frameIndex++)
                    {
                        var frame = inst.Stack[frameIndex];
                        var ownerIndex = frame.CodeOwnerObjType - 1;
                        var ownerName = (ownerIndex >= 0 && ownerIndex < objtChunk.Entries.Count)
                            ? objtChunk.Entries[ownerIndex].Name : "?";
                        var ownerGuid = (ownerIndex >= 0 && ownerIndex < objtChunk.Entries.Count)
                            ? objtChunk.Entries[ownerIndex].GUID.ToString("x8") : "????????";
                        Console.WriteLine("    frame[" + frameIndex + "] tree=" + frame.TreeID +
                            " node=" + frame.NodeID + " stackObject=" + frame.StackObjectID +
                            " codeOwnerType=" + frame.CodeOwnerObjType + " ownerGuid=" + ownerGuid +
                            " owner=" + ownerName +
                            " args=[" + string.Join(",", frame.Parameters ?? new short[0]) + "]" +
                            " locals=[" + string.Join(",", frame.Locals ?? new short[0]) + "]");
                    }
                }
                if (isPerson && dumpMotives)
                {
                    // (R223) the ORIGINAL saved motive arrays, straight from the OBJM person
                    // record BEFORE any port engine tick: MotiveDataOld[16] + MotiveData[16]
                    // (two consecutive original-engine states). VMMotive layout: 0-2 happy*,
                    // 3=Mood, 5=Energy, 6=Comfort, 7=Hunger, 8=Hygiene, 9=Bladder, 11=Sleep,
                    // 13=Room, 14=Social, 15=Fun.
                    var per = inst.PersonData.Value;
                    Console.WriteLine("    motivesOld=[" + string.Join(",", per.MotiveDataOld.Select(v => ((int)Math.Round(v)).ToString())) + "]");
                    Console.WriteLine("    motives=[" + string.Join(",", per.MotiveData.Select(v => ((int)Math.Round(v)).ToString())) + "]");
                    Console.WriteLine("    motivesF=[" + string.Join(",", per.MotiveData.Select(v => v.ToString("F3"))) + "]");
                    foreach (var md in per.MotiveDeltas)
                        Console.WriteLine("    motiveDelta m=" + md.Motive + " tick=" + md.TickDelta + " stopAt=" + md.StopAt);
                    Console.WriteLine("    firstFloats=[" + string.Join(",", per.FirstFloats.Select(v => v.ToString("F3"))) + "]");
                    Console.WriteLine("    motivesOldF=[" + string.Join(",", per.MotiveDataOld.Select(v => v.ToString("F3"))) + "]");
                }
                if (isPerson)
                {
                    var pd = inst.PersonData.Value.PersonData;
                    // engine job vars: VMPersonDataVariable JobType=56 JobPromotionLevel=57
                    // JobPerformance=63 TS1AtWorkSchool=87; guard length for safety
                    Console.WriteLine("    pdlen=" + pd.Length +
                        " jobType=" + (pd.Length > 56 ? pd[56].ToString() : "?") +
                        " jobLevel=" + (pd.Length > 57 ? pd[57].ToString() : "?") +
                        " jobPerf=" + (pd.Length > 63 ? pd[63].ToString() : "?") +
                        " atWorkSchool=" + (pd.Length > 87 ? pd[87].ToString() : "?"));
                    var nz = new List<string>();
                    for (int k = 0; k < pd.Length; k++) if (pd[k] != 0 && k != 31) nz.Add(k + "=" + pd[k]);
                    Console.WriteLine("    nonzero=" + string.Join(",", nz));
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("ERR " + ex);
        }
        return 0;
    }
}
