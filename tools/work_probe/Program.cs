using System;
using System.IO;
using System.Linq;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;
class Prog
{
    static int Main(string[] args)
    {
        var path = args[0];
        var iff = new IffFile(path);
        var carrs = iff.List<CARR>();
        Console.WriteLine("CARR chunks=" + (carrs == null ? 0 : carrs.Count));
        if (carrs == null) return 0;
        foreach (var c in carrs.OrderBy(x => x.ChunkID))
        {
            int lv = (c.JobLevels == null) ? 0 : c.JobLevels.Length;
            var l0 = (lv > 0) ? c.JobLevels[0] : null;
            Console.WriteLine("  CARR id=" + c.ChunkID + " name=" + (c.Name ?? "") + " levels=" + lv +
                (l0 == null ? "" : " lvl0.salary=" + l0.Salary + " lvl0.job=" + (l0.JobName ?? "").Trim() +
                    " lvl0.suit=" + (l0.MaleUniformMesh ?? "").Trim()));
        }
        return 0;
    }
}
