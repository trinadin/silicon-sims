// Probe: parse the staged original Neighborhood.iff NBRS chunk through the ENGINE's own
// IffFile parser (FSO.Files), so we observe exactly what the runtime loads -- including
// whether per-neighbour RELATIONSHIP records survive the parse.
using System;
using System.IO;
using System.Linq;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;

class Prog
{
    static int Main(string[] args)
    {
        var path = args.Length > 0 ? args[0] : null;
        if (path == null || !File.Exists(path)) { Console.WriteLine("usage: nbrs_probe <Neighborhood.iff>"); return 1; }
        try
        {
            var iff = new IffFile(path);
            var nbrs = iff.List<NBRS>();
            if (nbrs == null || nbrs.Count == 0) { Console.WriteLine("NBRS entries=0"); return 0; }
            var nb = nbrs[0];
            Console.WriteLine("NBRS version=" + nb.Version + " Entries=" + nb.Entries.Count + " ByID=" + nb.NeighbourByID.Count);
            int withRels = 0, relVars = 0;
            foreach (var e in nb.Entries.Take(60))
            {
                var rcount = e.Relationships?.Count ?? 0;
                if (rcount > 0) withRels++;
                relVars += rcount;
                var sample = rcount > 0 ? string.Join("/", e.Relationships.Take(2).Select(kv => kv.Key + ":" + string.Join(",", kv.Value))) : "";
                Console.WriteLine("  nb id=" + e.NeighbourID + " guid=" + e.GUID.ToString("x8") + " name=" + e.Name + " pm=" + e.PersonMode + " rels=" + rcount + " " + sample);
            }
            Console.WriteLine("neighbors-with-relationships=" + withRels + " relationship-records=" + relVars);
        }
        catch (Exception ex)
        {
            Console.WriteLine("ERR " + ex);
        }
        return 0;
    }
}
