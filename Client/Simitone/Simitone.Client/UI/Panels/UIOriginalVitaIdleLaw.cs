using System.Collections.Generic;

namespace Simitone.Client.UI.Panels
{
    /// ROUND-209 'uivita' (tools/iff-dump/r209/r209-vita-law.md): the CAS Vita
    /// preview's idle animation canon, recovered byte-verbatim from the engine
    /// data section (TOC[-0x4cd4] = 0x5d670; the cWinVitaBtnSolo list builders
    /// 0x2da500/0x2da6a0/0x2da860/0x2da9e0 copy these names IN ORDER into
    /// this+0x218 — the order is the WEIGHT, breathing dominates). SetPerson
    /// 0x2db640 dispatches by creature type with a GENDER split at
    /// person->0x60e, then calls the vtable+0x170 pick and resets the cursor
        /// (+0x210 = -1). The idle sway = a SineGenerator (this+0x1e8) with a
        /// 10000 ms period and infinite duration; r258 recovered the amplitude
        /// (static code pools, pi/4 — see SwayAmplitudeIsRuntimeOnly below).
    public static class UIOriginalVitaIdleLaw
    {
        public const int SwayPeriodMs = 10000;
        /// R258 (r258-law.md item 4; UI-31): RECOVERED, superseding the r209
        /// "runtime-only" disclosure — the amplitudes are STATIC code-section
        /// float pools (gA file 0x5a4dc8 = pi, gB file 0x5a4830 = 0.25/0.5...),
        /// pi/4 at the Init sites (SetAmplitude gA[0]*gB[0]) and pi/2 at the
        /// ctors (gA[0]*gB[+8], initial rotation matrix). The values live on
        /// UIOriginalVitaIdlePlayer (EnginePiF / InitAmplitudeRad /
        /// CtorAmplitudeRad); the old ±0.05 rad port substitute is retired.
        public const bool SwayAmplitudeIsRuntimeOnly = false;

        /// [3] is the gender slot: male (person->0x60e == 0) takes
        /// MaleName, else FemaleName.
        public static readonly string[] AdultList =
        {
            "a2o-heyyou1",
            "a2o-idle-armsdown-breathe",
            "a2o-mirror-admire-self-start",
            "a2o-mirror-admire-self-loop2", // gender slot (male)
            "a2o-mirror-admire-self-approve",
            "a2o-idle-armsdown-breathe",
            "a2o-idle-armsdown-breathe",
            "a2o-celebrate-short",
            "a2o-idle-armsdown-breathe",
            "a2o-idle-armsdown-breathe",
        };
        public const string AdultGenderAlt = "a2o-mirror-admire-self-loop1";

        public static readonly string[] ChildList =
        {
            "c2o-heyyou1",
            "c2o-idle-armsdown-breathe",
            "c2o-idle-armsdown-breathe",
            "c2o-mirror-admire-self-start",
            "c2o-mirror-admire-self-loop2",
            "c2o-mirror-admire-self-approve",
            "c2o-idle-armsdown-breathe",
            "c2o-idle-armsdown-breathe",
            "c2o-happy-clap",
            "c2o-idle-armsdown-breathe",
            "c2o-idle-armsdown-breathe",
            "c2o-idle-armsdown-breathe",
            "", // the builder's empty 13th copy at +0x13e (terminator quirk)
        };

        public static readonly string[] CatList =
        {
            "k2o-stand-tailswish-loop-fast",
            "k2o-hiss-start",
            "k2o-hiss-loop",
            "k2o-hiss-sizing-up",
            "k2o-hiss-rhow-angry-one",
            "k2o-hiss-rhow-angry-long-low",
            "k2o-hiss-rhow-angry-long-high",
            "k2o-hiss-stop",
            "k2o-napfloor-stand-trans-sit",
            "k2o-sit-chase-tail-start",
            "k2o-sit-chase-tail-slow-loop",
            "k2o-sit-chase-tail-stop",
        };

        public static readonly string[] DogList =
        {
            "d2o-sit-wag",
            "d2o-sit-wag-loop-slow",
            "d2o-sit-wag-stop",
            "d2o-sit-bark-start",
            "d2o-sit-bark",
            "d2o-sit-bark-stop",
            "d2o-napfloor-sit-trans-stand",
            "d2o-circle-start",
            "d2o-circle-fascinated",
            "d2o-circle-stop",
            "d2o-napfloor-stand-trans-sit",
            "d2o-sit-chase-tail-start",
            "d2o-sit-chase-tail-slow-loop",
            "d2o-sit-chase-tail-stop",
            "d2o-sit-scratchears-l-start",
            "d2o-sit-scratchears-l-loop-fast",
            "d2o-sit-scratchears-l-loop-flick",
            "d2o-sit-scratchears-l-stop",
        };

        public static int BreatheWeight(IList<string> list)
        {
            var n = 0;
            foreach (var s in list) if (s.EndsWith("-idle-armsdown-breathe")) n++;
            return n;
        }
    }
}
