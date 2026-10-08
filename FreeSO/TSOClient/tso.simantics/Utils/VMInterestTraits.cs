using FSO.SimAntics.Model;
using System;

namespace FSO.SimAntics.Utils
{
    /// <summary>
    /// R150: the ORIGINAL 'init traits' interest law, decoded from the
    /// character-file BHAV 4100 (every TS1 sim's init tree calls it — e.g.
    /// UserData/Characters/User00000.iff 'Bob Newbie', verified this round):
    ///
    ///   Temps[0] = 46;
    ///   loop { Parameters[0] = NextRandom(11);           // roll 0..10
    ///          if roll == 0 && zeroLeft   -> accept, zeroLeft--
    ///          elif roll  < 4 && lowLeft  -> accept, lowLeft--
    ///          elif roll  < 7 && medLeft  -> accept, medLeft--
    ///          elif roll >= 7 && highLeft -> accept, highLeft--
    ///          else re-roll;
    ///          PersonData[Temps[0]] = roll; Temps[0]++; } until Temps[0] == 56;
    ///
    /// with Local counters {high=3, med=3, low=3, zero=1} — exactly one topic
    /// at 0, three each in 1-3 / 4-6 / 7-10, across person words 46..55.
    /// The port VM EXECUTES these trees (verified live in the gate lot: the
    /// words already held the raw rolls), so the port stores the tree's raw
    /// 0..10 — SimitoneNeighbourGenerator now writes the same scale, and the
    /// Interest panel multiplies by 100 at read (the engine's runtime
    /// halfwords are 0..1000; cWinInterest divides them by 100 for display).
    /// </summary>
    public static class VMInterestTraits
    {
        /// the init-traits write range: person words 46..55 (TSO names 46-53;
        /// 54/55 are the two further interest words the tree writes).
        public static readonly int[] InterestWords = { 46, 47, 48, 49, 50, 51, 52, 53, 54, 55 };

        public static void ApplyInitTraits(VMAvatar av, Random rand)
        {
            var need = new int[] { 3, 3, 3, 1 };   // high, med, low, zero (Local[0..3])
            foreach (var w in InterestWords)
            {
                int roll;
                while (true)
                {
                    roll = rand.Next(11);
                    int bucket = (roll == 0) ? 3 : (roll < 4) ? 2 : (roll < 7) ? 1 : 0;
                    if (need[bucket] > 0) { need[bucket]--; break; }
                }
                av.SetPersonData((VMPersonDataVariable)w, (short)roll);
            }
        }

        /// has this avatar's interest block ever been initialized?
        public static bool IsZeroed(VMAvatar av)
        {
            foreach (var w in InterestWords)
                if (av.GetPersonData((VMPersonDataVariable)w) != 0) return false;
            return true;
        }
    }
}
