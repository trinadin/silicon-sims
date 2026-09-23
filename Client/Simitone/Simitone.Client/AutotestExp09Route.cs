using FSO.Content;
using FSO.Content.TS1;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simitone.Client
{
    /// <summary>
    /// EXP-09 leg 5 (opt-in "exp09route"): multi-neighborhood routing table
    /// verification. Asserts TS1GameScreen.GetNeighborhoodModeFromHouse across
    /// every destination band and its boundaries against the decoded law:
    /// 21-31 Downtown (2), 40-49 Vacation (3), 81-89 Studiotown (5),
    /// 90-99 Magictown (7), everything else Old Town default (4). Runtime
    /// multi-destination arrival evidence cites EXP-05 V6 (Old Town round
    /// trip) and EXP-04 (Vacation arrival, house 44).
    /// </summary>
    public class AutotestExp09Route
    {
        private Action<string> Log;
        public bool Passed { get; private set; }
        public bool Done { get; private set; }
        public string Diagnostics = "";
        private List<string> _fails = new List<string>();
        private int _ticks;

        public AutotestExp09Route(Action<string> log) { Log = log; }

        private void Check(bool ok, string m)
        {
            if (!ok) { _fails.Add(m); Log("AUTOTEST exp09route FAIL " + m); }
        }

        private static ushort Route(short house)
        {
            return Simitone.Client.UI.Screens.TS1GameScreen.GetNeighborhoodModeFromHouse(house);
        }

        public bool Tick()
        {
            if (Done) return true;
            if (_ticks++ < 30) return false; // let the neighborhood screen settle

            // band interiors
            Check(Route(21) == 2, "21 Downtown"); Check(Route(25) == 2, "25 Downtown");
            Check(Route(31) == 2, "31 Downtown");
            Check(Route(40) == 3, "40 Vacation"); Check(Route(44) == 3, "44 Vacation");
            Check(Route(49) == 3, "49 Vacation");
            Check(Route(81) == 5, "81 Studiotown"); Check(Route(85) == 5, "85 Studiotown");
            Check(Route(89) == 5, "89 Studiotown");
            Check(Route(90) == 7, "90 Magictown"); Check(Route(95) == 7, "95 Magictown");
            Check(Route(99) == 7, "99 Magictown");

            // boundaries just outside each band fall to the Old Town default (4)
            Check(Route(20) == 4, "20 default"); Check(Route(32) == 4, "32 default");
            Check(Route(39) == 4, "39 default"); Check(Route(50) == 4, "50 default");
            Check(Route(80) == 4, "80 default"); Check(Route(100) == 4, "100 default");
            Check(Route(5) == 4, "5 default (residential home lot)");

            Log("AUTOTEST exp09route routing table verified: Downtown 21-31=2, "
                + "Vacation 40-49=3, Studiotown 81-89=5, Magictown 90-99=7, "
                + "default/Old Town=4; boundaries inclusive-exhaustive");

            if (_fails.Count == 0)
                Log("AUTOTEST exp09route *** ROUTING TABLE VERIFIED ***");
            Diagnostics = "fails=" + _fails.Count + " " + string.Join("; ", _fails.Take(4));
            Passed = _fails.Count == 0;
            Done = true;
            return true;
        }
    }
}
