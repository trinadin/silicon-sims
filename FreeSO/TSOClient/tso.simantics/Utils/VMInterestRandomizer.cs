using FSO.SimAntics.Model;
using System;

namespace FSO.SimAntics.Utils
{
    /// <summary>
    /// R152: the ORIGINAL engine creation-time interest randomizer,
    /// cXPerson::RandomizeAllInterests @0x111650, decoded in
    /// tools/iff-dump/r151/r151-expansion-interest-decode.md section 2.1 and
    /// re-verified instruction-exact this round (r152 notes):
    ///
    ///   loop 1 (10 stores, r30 walking +0x5e8..+0x5fa): words 46..55 =
    ///     the 8 base topics + Technology/Romance; bucket counters {4,3,3}
    ///     (TOC blob file 0x5a33a8); value = bucket range, then
    ///     mulli r0,r0,100 -> the store is x100 (0..1000 scale).
    ///   loop 2 (5 stores, i=0..4 switch -> +0x5a6/+0x5a8/+0x5ac/+0x5b4/
    ///     +0x5c0): words 13/14/16/20/26 = Exercise/Food/Parties/Style/
    ///     Hollywood; counters {1,2,2} (li 1 -> stack[64], li 2 -> stack[68]
    ///     AND stack[72], 0x11176c-0x11178c — CORRECTS the r151 doc's
    ///     "{1,2,3}" misread, which summed to 6 against 5 words); x100.
    ///   roll law: rand % 3 picks the bucket (div-3 magic 0x55555556);
    ///   exhausted bucket -> re-roll; value = 0..3 / 4..6 / 7..10 by bucket
    ///   (bucket0 the rotate idiom r151 INTERP'd as %4; bucket1 4+%3;
    ///   bucket2 7+%4), each from a FRESH rand draw.
    ///
    /// Callers in the original: cXPerson::Reset (bl @0x111bc8, gate 2
    /// @0x111b34) and Initialize (@0x111cd0, bl @0x112014, gate
    /// @0x111fa0-0x112014) — both gate on words 46..53 ALL &lt;= 10.
    /// R153 CORRECTS the r151/r152 "== 10 sentinel" reading: the gate
    /// branches are 0x4181 bgt (fail when a word EXCEEDS 10), the flag
    /// is li r3,0 ... li r3,1, and the ctor/Reset zero sweep itself
    /// arms the gate — no sentinel state exists, and nothing (code or
    /// shipped data) ever writes all-10 (r153 census: 203 NBR records,
    /// 0 hits). PersonGlobals BHAV 9761 'Convert Interests to 0-1000'
    /// fronts the SAME &lt;=10 gate before multiplying x100 — the
    /// script-side twin of this law. This is the CREATION dialect
    /// (CAS/new sims): x100 scale, distinct from the character trees'
    /// raw 0..10 dialect (R150). The Interest panel bridges both
    /// (client-side BridgeToDisplayScale: raw 0..10 -> x100; total on
    /// both dialects — 10 &lt; 100 = the smallest nonzero x100 value).
    /// </summary>
    public static class VMInterestRandomizer
    {
        /// loop-2 words in the engine's switch order (i=0..4).
        public static readonly int[] ExpansionWords = { 13, 14, 16, 20, 26 };

        /// loop-1 words: 46..55 (base topics + Technology/Romance).
        public static readonly int[] BaseWords = { 46, 47, 48, 49, 50, 51, 52, 53, 54, 55 };

        /// the engine's randomizer gate (Reset gate 1 @0x1119f8, gate 2
        /// @0x111b34; Initialize @0x111fa0-0x112014): passes when all
        /// EIGHT base words 46..53 are &lt;= 10 on the raw halfword scale —
        /// i.e. the interests are NOT yet in the x100 dialect (zeros, raw
        /// tree rolls, or never set). R153: the r151/r152 "== 10 sentinel"
        /// was a bgt-polarity misread (see the class comment).
        public static bool NeedsRandomize(VMAvatar av)
        {
            for (int w = 46; w <= 53; w++)
                if (av.GetPersonData((VMPersonDataVariable)w) > 10) return false;
            return true;
        }

        /// the full creation law on a live avatar (x100 dialect).
        public static void Apply(VMAvatar av, Random rand)
        {
            RollWords((w, v) => av.SetPersonData((VMPersonDataVariable)w, v), rand);
        }

        /// the full creation law on a raw person-data array (the generator
        /// path — SimitoneNeighbourGenerator.MakePersonData).
        public static void ApplyTo(short[] pd, Random rand)
        {
            RollWords((w, v) => pd[w] = v, rand);
        }

        /// both loops verbatim: counters {4,3,3} over words 46..55, then
        /// {1,2,2} over 13/14/16/20/26; every accepted roll x100.
        private static void RollWords(Action<int, short> write, Random rand)
        {
            var need1 = new int[] { 4, 3, 3 };
            foreach (var w in BaseWords) write(w, RollWord(need1, rand));
            var need2 = new int[] { 1, 2, 2 };
            foreach (var w in ExpansionWords) write(w, RollWord(need2, rand));
        }

        /// one bucketed roll: rand%3 picks the bucket, exhausted -> re-roll
        /// (the bl back-edge re-draws the random); value from a FRESH draw
        /// inside the accepted bucket's block; x100 at the store (mulli 100).
        private static short RollWord(int[] need, Random rand)
        {
            int b;
            while (true)
            {
                b = rand.Next(3);
                if (need[b] > 0) { need[b]--; break; }
            }
            int v = (b == 0) ? rand.Next(4) : (b == 1) ? 4 + rand.Next(3) : 7 + rand.Next(4);
            return (short)(v * 100);
        }
    }
}
