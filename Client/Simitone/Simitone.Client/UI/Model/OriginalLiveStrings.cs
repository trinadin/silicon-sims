using System;
using System.Collections.Generic;

namespace Simitone.Client.UI.Model
{
    /// <summary>
    /// ROUND-114/115: original-provenance strings from GameData/Live.iff — the ORIGINAL
    /// live-mode subpanel corpora the port was missing. Tables loaded (chunkID -> label,
    /// matched on BOTH as a duplicate-id guard):
    ///   130 'Motives' (289 entries): [0] 'Needs'; name+popup pairs — Hunger 1, Energy 3,
    ///     Comfort 5, Fun 7, Hygiene 9, Social 11, Bladder 13, Room 15.
    ///   131 'Personality' (714 entries): [0] 'Personality'; per-trait blocks of 7 — trait
    ///     names at 1 Neat, 8 Outgoing, 15 Active, 22 Playful, 29 Nice.
    ///   170 'Pet Personality' (42 English entries): the same five 7-string blocks for
    ///     Quiet/Friendly/Playful/Smart/Loyal.
    ///   180 'Pet Motives' (17 English entries): [0] 'Needs', then the same eight
    ///     name/description pairs used by the human motive controls.
    ///   132 'Relationships' (15 English entries): [0] panel title, then seven
    ///     relationship popup title/body pairs. The shipped English pairs are
    ///     visibly identical; their translator comments are not display text.
    ///   134 'ModeTips' (10 English entries): the seven People category quick
    ///     tips, followed by Skills, Grades, and Not Available to Pets.
    ///   136 'JobSubpanelLabels' (187 entries): [0] 'Job' title; [1]-[6] Cooking/Mechanical/
    ///     Charisma/Body/Logic/Creativity; [7] 'Unemployed'; [8]/[9] 'n/a'; [10] 'Need %d'.
    ///   137 'JobSubpanelPopupText' (544 entries): [12] 'Unemployed'; [14] 'Salary';
    ///     [16] 'Performance'; [23] the Hours/Carpool popup format.
    /// Until R114 the port read motive names from an FSO-authored _f102_motivestrings.cst
    /// and HARDCODED the personality + job-subpanel strings; this restores the original
    /// tables as the source. The loader walks the IFF container ITSELF (4CC + u32 BE size
    /// + u16 id + u16 flags + 64-byte label + (size-76) data, first chunk at 0x40 — the
    /// same rule the r111-r115 evidence parsers and the uilive/uijob gates use) and reads
    /// ONLY the wanted chunks: the general FSO IffFile parse of Live.iff throws on
    /// unrelated chunk types (r114p1). Format -3 (lang-pairs), English set = lang 1.
    /// </summary>
    public static class OriginalLiveStrings
    {
        public static bool Loaded = false;
        private static Dictionary<int, string[]> _tables = new Dictionary<int, string[]>();
        private static readonly Dictionary<int, string> Wanted = new Dictionary<int, string>
        {
            { 130, "Motives" },
            { 131, "Personality" },
            { 170, "Pet Personality" },
            { 180, "Pet Motives" },
            { 132, "Relationships" },
            { 134, "ModeTips" },
            { 136, "JobSubpanelLabels" },
            { 137, "JobSubpanelPopupText" },
            // R133: the House subpanel's OWN tables (the corpus that corrects the
            // R132 'House Rating'/'Friend Rating' reading — those STR# 154 entries
            // are neighborhood-scope; the House panel reads these instead).
            { 133, "HouseSubpanelLabels" },
            { 135, "HouseSubpanelPopupText" },
            { 138, "HouseSubpanelSize" },
            // R145: the Interest/Gift panels' tables (cWinPeople::
            // BuildInterestWindow @0x287010 / BuildGiftPanel @0x286820,
            // tools/iff-dump/r145/interest-gift-law.md): 140 'Interest Strings'
            // ([0] 'Interests' title + [1..19] topic names), 142 'Interest
            // Descriptions', 141 'Gifts' ([0] 'Inventory' title).
            { 140, "Interest Strings" },
            { 141, "Gifts" },
            { 142, "Interest Descriptions" },
            // R159: the ReportCard subpanel (cWinSubpanelReportCard 0x2a6180,
            // a live-popup overlay on the Job subpanel reusing its popup
            // composite — r159-scout-engine-decode.md): [0] 'Report Card'
            // title, [1] 'Grades' popup title, [2] the school description.
            { 139, "ReportCard" },
        };

        public static void EnsureLoaded()
        {
            if (Loaded) return;
            lock (typeof(OriginalLiveStrings))
            {
                if (Loaded) return;
                try
                {
                    var path = System.IO.Path.Combine(FSO.Content.Content.TS1HybridBasePath, "GameData/Live.iff");
                    if (!System.IO.File.Exists(path)) return;
                    var d = System.IO.File.ReadAllBytes(path);
                    int off = 0x40;
                    while (off + 76 <= d.Length)
                    {
                        int sz = (d[off + 4] << 24) | (d[off + 5] << 16) | (d[off + 6] << 8) | d[off + 7];
                        if (sz < 76 || off + sz > d.Length) break;
                        var cid = System.Text.Encoding.ASCII.GetString(d, off, 4);
                        int id = (d[off + 8] << 8) | d[off + 9];
                        if (cid == "STR#" && Wanted.ContainsKey(id))
                        {
                            var lbl = new byte[64];
                            System.Array.Copy(d, off + 12, lbl, 0, 64);
                            var label = System.Text.Encoding.ASCII.GetString(lbl).Split('\0')[0];
                            if (label == Wanted[id])
                            {
                                var vals = ParseEnglish(d, off + 76, sz - 76);
                                if (vals != null) _tables[id] = vals;
                            }
                        }
                        off += sz;
                    }
                    Loaded = _tables.Count == Wanted.Count;
                }
                catch (Exception)
                {
                    // leave partial tables - callers keep their existing fallback strings
                }
            }
        }

        private static string[] ParseEnglish(byte[] d, int body, int dsz)
        {
            int fc = (short)(d[body] | (d[body + 1] << 8));
            if (fc != -3) return null;
            int count = d[body + 2] | (d[body + 3] << 8);
            var english = new List<string>();
            int p = body + 4;
            for (int i = 0; i < count && p < d.Length; i++)
            {
                int lang = d[p]; p++;
                var val = ReadCString(d, ref p);
                ReadCString(d, ref p); // translator comment
                if (lang == 1) english.Add(val);
            }
            return english.ToArray();
        }

        private static string ReadCString(byte[] b, ref int p)
        {
            int e = p;
            while (e < b.Length && b[e] != 0) e++;
            var s = System.Text.Encoding.ASCII.GetString(b, p, e - p);
            p = e + 1;
            return s;
        }

        /// <summary>English entry of a loaded Live.iff table by chunkID, or null.</summary>
        public static string Entry(int tableId, int idx)
        {
            EnsureLoaded();
            string[] t;
            if (!_tables.TryGetValue(tableId, out t)) return null;
            return (idx >= 0 && idx < t.Length) ? t[idx] : null;
        }

        /// <summary>Motive NAME by Live.iff 'Motives' index (1 Hunger .. 15 Room).</summary>
        public static string Motive(int idx) { return Entry(130, idx); }

        /// <summary>Personality trait NAME by Live.iff 'Personality' index (1,8,15,22,29).</summary>
        public static string Trait(int idx) { return Entry(131, idx); }

        /// <summary>Pet motive entry by Live.iff STR#180 index.</summary>
        public static string PetMotive(int idx) { return Entry(180, idx); }

        /// <summary>Pet personality entry by Live.iff STR#170 index.</summary>
        public static string PetTrait(int idx) { return Entry(170, idx); }
    }
}
