// Probe: dump OBJD name/id refs and the STR/CTSS string tables they point at,
// parsed through the ENGINE's own IFF parsers (FSO.Files), so we see exactly what
// the runtime reads for object names and avatar/pet name lists.
using System;
using System.IO;
using System.Linq;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;

class Prog
{
    static void DumpTable(IffChunk c, string tag, int max)
    {
        if (c is STR str)
        {
            for (int i = 0; i < Math.Min(str.Length, max); i++)
                Console.WriteLine("    " + tag + "[" + i + "]=" + (str.GetString(i) ?? "<null>"));
        }
        else if (c is CTSS ctss)
        {
            for (int i = 0; i < Math.Min(ctss.Length, max); i++)
                Console.WriteLine("    " + tag + "[" + i + "]=" + (ctss.GetString(i) ?? "<null>"));
        }
    }

    static int Main(string[] args)
    {
        var path = args.Length > 0 ? args[0] : null;
        if (path == null || !File.Exists(path)) { Console.WriteLine("usage: str_probe <File.iff>"); return 1; }
        try
        {
            var iff = new IffFile(path);
            var objds = iff.List<OBJD>();
            Console.WriteLine("OBJD count=" + (objds?.Count ?? 0));
            foreach (var o in objds ?? new System.Collections.Generic.List<OBJD>())
            {
                Console.WriteLine("OBJD id=" + o.ChunkID + " label=" + o.ChunkLabel + " body=" + o.BodyStringID + " catalog=" + o.CatalogStringsID + " type=" + o.ObjectType);
                var body = iff.Get<STR>(o.BodyStringID);
                var cat = iff.Get<CTSS>(o.CatalogStringsID);
                DumpTable(body, "BODY", 60);
                DumpTable(cat, "CTSS", 4);
            }
            var objts = iff.List<OBJT>();
            if (objts != null && objts.Count > 0)
                foreach (var t in objts.First().Entries.Take(12))
                    Console.WriteLine("OBJT typeID=" + t.TypeID + " name=" + t.Name + " guid=" + t.GUID.ToString("x8"));
        }
        catch (Exception ex)
        {
            Console.WriteLine("ERR " + ex);
        }
        return 0;
    }
}
