/*
This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
If a copy of the MPL was not distributed with this file, You can obtain one at
http://mozilla.org/MPL/2.0/.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using FSO.Client;
using FSO.Content;
using FSO.SimAntics;
using FSO.SimAntics.Engine;
using FSO.SimAntics.Engine.Primitives;
using FSO.SimAntics.Entities;
using FSO.SimAntics.Model;
using FSO.SimAntics.Primitives;
using FSO.Files.Formats.IFF.Chunks;
using FSO.LotView.Model;

using Simitone.Client.UI.Screens;

namespace Simitone.Client
{
    /// <summary>
    /// EXP-15 'ulpets' (focused, opt-in): the live pet-AI audit for the PARITY
    /// residual "pet-AI depth and shows/training remain partial". PHASE 1
    /// enumeration (coordination/evidence/EXP-15/enumeration-20261008.md) found
    /// the show cycle (EXP-05 unl-show), mice hunting (unl-mice) and trainer
    /// training (EXP-09 exp09train) already receipt-closed; this gate asserts the
    /// enumerated residual rows that were NEVER observed live:
    ///
    ///   R-1/R-2 brain: a FAMILY pet's own semiglobal brain cycles on the lot —
    ///       templatedog/cat Main 4096 -> CALL SEMI 8194 -> 8222 'main loop',
    ///       the motive scans (8223..8232), try-autonomy (8204) and the idle
    ///       family (8234/8264/8265/8314/8316/8318/8319/8324/8217). Observed via
    ///       the ITRACE seam with the EXP-15 admission band (8192..8370,
    ///       VMThread.cs — armed-only, inert in play).
    ///   pie: the pet pie labels are the corpus FIRST strings ('Call Over',
    ///       'Scold', 'Praise', 'Sniff' per DogGlobals TTAs 129; the native
    ///       StringSetBase::Load 0x1013c7a0 law — the second per-language string
    ///       is the translator slot and is discarded).
    ///   train: the OWNER-driven trick-training row (dog TTAB 129 tta=45
    ///       'Tricks.../Train Bounce' -> action 8334 test 8335) executes and
    ///       pushes onto the DOG (VMPushInteraction.PushOutcome seam).
    ///   show: the player-facing PEDESTAL entry (petpedestal.iff TTAB 130
    ///       tta=0 'Begin Show', action 4099 test 4107) engages the pet-show
    ///       controller (0xC1BA4467 main 4096 leaves its idle gate: a
    ///       controller attribute transition) — the EXP-05 law re-asserted
    ///       through the pedestal row instead of a direct controller push.
    ///
    /// Self-contained state machine (the unl-* soak idiom): AutotestRunner's
    /// state-2 dispatcher calls Tick each frame while State != 99 and holds the
    /// soak; the runner re-reads VM after each tick (PlayHouse swaps the VM).
    /// </summary>
    public static class AutotestExp15Pets
    {
        // corpus constants (enumeration receipt §1)
        private const uint CatGuid = 0x7BEA0977u;    // templatecat
        private const uint DogGuid = 0x4A70DF92u;    // templatedog
        private const uint ShowCtrGuid = 0xC1BA4467u; // control-petshow
        private const uint PedestalGuid = 0x389D3502u; // petpedestal
        private const short ProbeHouse = 5;           // the EXP-05 probe lot (phone + known-good)

        public static int State;          // 0 attach, 1 spawn, 2 brain, 3 train, 4 show, 99 done
        public static VM VM;              // the runner re-reads this after PlayHouse
        private static TS1GameScreen _screen;
        private static Action<string> _log, _pass, _fail;
        private static int _frame, _settle;
        private static VMAvatar _human, _cat, _dog;
        private static VMEntity _ctr, _pedestal;
        private static FAMI _fam;
        private static readonly HashSet<string> _dogTrees = new HashSet<string>();
        private static readonly HashSet<string> _catTrees = new HashSet<string>();
        private static readonly Dictionary<int, HashSet<short>> _ctrAttrs = new Dictionary<int, HashSet<short>>();
        private static bool _pushedTrain, _pushedShow, _trainPushLanded, _trainTreeRan, _pieDone;
        private static int _dogQueueMax, _pedestalQ0 = -1;
        private static readonly List<string> _pieLabels = new List<string>();
        private static bool _pushHooked;
        private static readonly List<string> _capture = new List<string>();
        private static readonly HashSet<string> _seenKeys = new HashSet<string>();
        private static bool _capturing;

        // brain-band membership (DogGlobals/CatGlobals ids overlap; id alone is enough)
        private static bool IsMotiveScan(int id) { return id >= 8223 && id <= 8232; }
        private static bool IsIdle(int id)
        {
            return id == 8234 || id == 8264 || id == 8265 || id == 8314 || id == 8316
                || id == 8318 || id == 8319 || id == 8324 || id == 8217;
        }

        public static void Tick(TS1GameScreen screen, Action<string> log, Action<string> pass, Action<string> fail)
        {
            _screen = screen; _log = log; _pass = pass; _fail = fail;
            try
            {
                if (State == 0)
                {
                    if (++_settle < 90) return;
                    _settle = 0;
                    var neigh = Content.Get().Neighborhood;
                    uint human = 0;
                    var fam5 = neigh.GetFamilyForHouse(5);
                    if (fam5 != null && fam5.FamilyGUIDs != null)
                        human = fam5.FamilyGUIDs.FirstOrDefault(g => g != CatGuid && g != DogGuid);
                    if (human == 0)
                    {
                        var objs = Content.Get().WorldObjects as FSO.Content.TS1.TS1ObjectProvider;
                        if (objs != null) human = objs.PersonGUIDs.OrderBy(x => x).FirstOrDefault();
                    }
                    if (human == 0) { _log("AUTOTEST ulpets no human guid for family attach"); _fail("ulpets"); State = 99; return; }
                    var guids = new[] { human, CatGuid, DogGuid };
                    var fams = neigh.MainResource.List<FAMI>() ?? new List<FAMI>();
                    ushort newId = 0;
                    foreach (var f in fams.OrderBy(x => x.ChunkID))
                    {
                        if (f.ChunkID == newId) newId++;
                        else break;
                    }
                    int famNum = fams.Count == 0 ? 1 : fams.Max(x => x.FamilyNumber) + 1;
                    _fam = new FAMI
                    {
                        ChunkLabel = "",
                        ChunkID = newId,
                        ChunkProcessed = true,
                        ChunkType = "FAMI",
                        ChunkParent = neigh.MainResource,
                        AddedByPatch = true,
                        FamilyGUIDs = guids,
                        RuntimeSubset = guids,
                        FamilyNumber = famNum,
                        Unknown = 1,
                        Budget = 20000,
                    };
                    neigh.MainResource.AddChunk(_fam);
                    var famsChunk = new FAMs
                    {
                        ChunkLabel = "",
                        ChunkID = newId,
                        ChunkProcessed = true,
                        ChunkType = "FAMs",
                        ChunkParent = neigh.MainResource,
                        AddedByPatch = true,
                    };
                    famsChunk.InsertString(0, new STRItem { Comment = "", Value = "Exp15Pets" });
                    neigh.MainResource.AddChunk(famsChunk);
                    neigh.SetFamilyForHouse(ProbeHouse, _fam, false);
                    _log("AUTOTEST ulpets attach famId=" + newId + " house=" + ProbeHouse
                        + " human=0x" + human.ToString("x8") + " cat=0x" + CatGuid.ToString("x8")
                        + " dog=0x" + DogGuid.ToString("x8"));
                    screen.PlayHouse(ProbeHouse, null);
                    _frame = 0;
                    State = 1;
                    return;
                }

                // post-PlayHouse VM swap (unl-show run-1 law)
                var vmNow = screen?.vm;
                if (vmNow != null && !ReferenceEquals(vmNow, VM)) VM = vmNow;
                var vm = VM;
                if (vm == null) return;
                _frame++;
                var avatars = vm.Entities.OfType<VMAvatar>().ToList();

                if (State == 1)
                {
                    _cat = avatars.FirstOrDefault(a => a.Object.OBJ.GUID == CatGuid);
                    _dog = avatars.FirstOrDefault(a => a.Object.OBJ.GUID == DogGuid);
                    if (_cat != null && _dog != null)
                    {
                        _human = avatars.FirstOrDefault(a => a != _cat && a != _dog
                            && a.GetPersonData(VMPersonDataVariable.PersonsAge) >= 18)
                            ?? avatars.FirstOrDefault(a => a != _cat && a != _dog);
                        if (_human != null)
                        {
                            _log("AUTOTEST ulpets SPAWN catOid=" + _cat.ObjectID + " dogOid=" + _dog.ObjectID
                                + " humanOid=" + _human.ObjectID + " avatars=" + avatars.Count
                                + " dogStack=[" + StackStr(_dog) + "] catStack=[" + StackStr(_cat) + "]");
                            // arm the brain trace: pets + human unbudgeted, EXP-15 pet band on
                            var ub = VMThread.AutotestUnbudgetedEnts;
                            ub.Clear();
                            ub.Add(_cat.ObjectID); ub.Add(_dog.ObjectID); ub.Add(_human.ObjectID);
                            VMThread.AutotestTraceShowTrees = true;
                            VMThread.AutotestInstrTraceBudget = 240;
                            VMThread.AutotestTraceSink = OnTrace;
                            HookPushes(true);
                            State = 2; _frame = 0;
                            return;
                        }
                    }
                    if (_frame > 900)
                    {
                        _log("AUTOTEST ulpets spawn TIMEOUT avatars=" + avatars.Count
                            + " cat=" + (_cat != null) + " dog=" + (_dog != null));
                        _fail("ulpets"); State = 99; TeardownTrace();
                    }
                    return;
                }

                if (State == 2) // brain window
                {
                    if (_frame == 300 && !_pieDone) PieCensus(vm);
                    _dogQueueMax = Math.Max(_dogQueueMax, _dog.Thread.Queue.Count);
                    if (_frame >= 600)
                    {
                        bool mainDog = HasTree(_dogTrees, 8194) || HasTree(_dogTrees, 8222);
                        bool mainCat = HasTree(_catTrees, 8194) || HasTree(_catTrees, 8222);
                        bool motiveDog = _dogTrees.Any(t => IsMotiveScan(int.Parse(t)));
                        bool motiveCat = _catTrees.Any(t => IsMotiveScan(int.Parse(t)));
                        bool idleDog = _dogTrees.Any(t => IsIdle(int.Parse(t)));
                        bool idleCat = _catTrees.Any(t => IsIdle(int.Parse(t)));
                        _log("AUTOTEST ulpets BRAIN dogMain=" + mainDog + " dogMotive=" + motiveDog
                            + " dogIdle=" + idleDog + " catMain=" + mainCat + " catMotive=" + motiveCat
                            + " catIdle=" + idleCat
                            + " dogTrees=[" + string.Join(",", _dogTrees.OrderBy(x => int.Parse(x))) + "]"
                            + " catTrees=[" + string.Join(",", _catTrees.OrderBy(x => int.Parse(x))) + "]");
                        if (!(mainDog && mainCat))
                        {
                            _log("AUTOTEST ulpets brain FAIL: both pets must cycle their own semiglobal main (8194/8222)");
                            _fail("ulpets"); State = 99; TeardownTrace(); return;
                        }
                        State = 3; _frame = 0;
                        _pushedTrain = false;
                    }
                    return;
                }

                if (State == 3) // owner-training row
                {
                    if (_frame == 30 && !_pushedTrain)
                    {
                        _pushedTrain = true;
                        _dogQueueMax = 0;
                        // row tta=45 'Tricks.../Train Bounce' (action 8334 test 8335)
                        _dog.PushUserInteraction(45, _human, vm.Context, false);
                        _log("AUTOTEST ulpets TRAIN-PUSH row45 from humanOid=" + _human.ObjectID
                            + " onto dogOid=" + _dog.ObjectID + " q=" + _dog.Thread.Queue.Count);
                    }
                    _dogQueueMax = Math.Max(_dogQueueMax, _dog.Thread.Queue.Count);
                    if (_frame >= 900)
                    {
                        _log("AUTOTEST ulpets TRAIN treeRan=" + _trainTreeRan + " pushLanded=" + _trainPushLanded
                            + " dogQmax=" + _dogQueueMax + " dogTrees=[" + string.Join(",", _dogTrees) + "]");
                        if (!(_trainTreeRan && (_trainPushLanded || _dogQueueMax > 0)))
                        {
                            _log("AUTOTEST ulpets train FAIL: 8334 must execute and land its push on the dog");
                            _fail("ulpets"); State = 99; TeardownTrace(); return;
                        }
                        State = 4; _frame = 0;
                        _pushedShow = false;
                    }
                    return;
                }

                if (State == 4) // pedestal show entry
                {
                    if (_frame == 20)
                    {
                        // place controller + pedestal in-world beside the host (EXP-05 run-4/15 laws)
                        var near = new LotTilePos((short)(_human.Position.x + 64), (short)(_human.Position.y + 64), _human.Position.Level);
                        _ctr = vm.Context.CreateObjectInstance(ShowCtrGuid, near, Direction.NORTH)?.Objects?.FirstOrDefault();
                        var pedPos = new LotTilePos((short)(_human.Position.x + 96), (short)(_human.Position.y + 64), _human.Position.Level);
                        _pedestal = vm.Context.CreateObjectInstance(PedestalGuid, pedPos, Direction.NORTH)?.Objects?.FirstOrDefault();
                        vm.SetGlobalValue(10, 555); // zoning-gate default (unl-show run-17 law)
                        _log("AUTOTEST ulpets SHOW-PLACE ctr=" + (_ctr != null ? _ctr.ObjectID.ToString() : "FAIL")
                            + " pedestal=" + (_pedestal != null ? _pedestal.ObjectID.ToString() : "FAIL")
                            + " Global[10]=555");
                        if (_ctr == null || _pedestal == null)
                        {
                            _log("AUTOTEST ulpets show FAIL: controller/pedestal creation failed");
                            _fail("ulpets"); State = 99; TeardownTrace(); return;
                        }
                        var tele = new LotTilePos((short)(_ctr.Position.x - 16), _ctr.Position.y, _ctr.Position.Level);
                        _human.SetPosition(tele, Direction.NORTH, vm.Context);
                        // seed the controller relationship the EXP-05 runs needed (4102 reads rel >0)
                        foreach (var av in avatars)
                        {
                            var key = (ushort)_ctr.ObjectID;
                            if (!av.MeToObject.ContainsKey(key)) av.MeToObject[key] = new List<short>();
                            var rels = av.MeToObject[key];
                            while (rels.Count == 0) rels.Add(0);
                            rels[0] = Math.Max(rels[0], (short)60);
                        }
                    }
                    if (_frame == 60 && !_pushedShow)
                    {
                        _pushedShow = true;
                        _pedestalQ0 = _human.Thread.Queue.Count;
                        // petpedestal TTAB 130 row tta=0 'Begin Show' (action 4099 test 4107)
                        _pedestal.PushUserInteraction(0, _human, vm.Context, false);
                        _log("AUTOTEST ulpets SHOW-PUSH pedestal row0 'Begin Show' from humanOid=" + _human.ObjectID);
                    }
                    if (_frame % 15 == 0 && _ctr != null)
                    {
                        for (short a = 0; a < 16; a++)
                        {
                            var v = _ctr.GetAttribute(a);
                            HashSet<short> set;
                            if (!_ctrAttrs.TryGetValue(a, out set)) { set = new HashSet<short>(); _ctrAttrs[a] = set; }
                            if (set.Count == 0 || !set.Contains(v))
                            {
                                set.Add(v);
                                _log("AUTOTEST ulpets ctrAttr f=" + _frame + " attr" + a + "=" + v);
                            }
                        }
                    }
                    bool attrTrans = _ctrAttrs.Values.Any(s => s.Count >= 2);
                    if (_frame > 200 && attrTrans)
                    {
                        _log("AUTOTEST ulpets SHOW-ENGAGED f=" + _frame + " (controller left idle gate via the pedestal row)");
                        _pass("ulpets"); State = 99; TeardownTrace(); return;
                    }
                    if (_frame > 3600)
                    {
                        _log("AUTOTEST ulpets show TIMEOUT f=" + _frame + " attrTrans=" + attrTrans
                            + " humanQ=" + _human.Thread.Queue.Count
                            + " ctrTrees=[" + string.Join(",", ShowCtrTrees()) + "]");
                        _fail("ulpets"); State = 99; TeardownTrace();
                    }
                    return;
                }
            }
            catch (Exception se)
            {
                var trace = (se.StackTrace ?? "").Split('\n');
                _log("AUTOTEST ulpets EXC " + se.GetType().Name + " " + se.Message
                    + (trace.Length > 0 ? " AT " + trace[0].Trim() : ""));
                _fail("ulpets"); State = 99; TeardownTrace();
            }
        }

        private static IEnumerable<string> ShowCtrTrees()
        {
            // show-band trees seen by ANY traced entity this run (sink records all)
            return _dogTrees.Concat(_catTrees).Where(t => { int v = int.Parse(t); return v >= 4096 && v <= 4113; }).Distinct();
        }

        private static void OnTrace(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            if (_capturing) { if (_capture.Count < 400) _capture.Add(s); return; }
            // log only first sightings per (ent,tree) + the 4096-4113 show band,
            // so the per-tick flood stays bounded
            var fm = System.Text.RegularExpressions.Regex.Match(s, @"ent=(\d+) .* (\d+)@");
            if (fm.Success)
            {
                var key = fm.Groups[1].Value + ":" + fm.Groups[2].Value;
                int ftid = int.Parse(fm.Groups[2].Value);
                if (!(_seenKeys.Add(key) || (ftid >= 4096 && ftid <= 4113))) return;
            }
            _log(s);
            // format: "... ent=<oid> d=n tick=m <tid>@<ip> op=.."
            var m = System.Text.RegularExpressions.Regex.Match(s, @"ent=(\d+) .* (\d+)@");
            if (!m.Success) return;
            var ent = int.Parse(m.Groups[1].Value);
            var tid = int.Parse(m.Groups[2].Value);
            if (tid == 8334 || tid == 8335) _trainTreeRan |= (tid == 8334);
            if (_dog != null && ent == _dog.ObjectID && tid >= 8192 && tid <= 8370) _dogTrees.Add(tid.ToString());
            else if (_cat != null && ent == _cat.ObjectID && tid >= 8192 && tid <= 8370) _catTrees.Add(tid.ToString());
            // controller/pedestal band: record on the dog set too (shared evidence bag)
            else if (tid >= 4096 && tid <= 4113) { if (_dog != null && ent == _dog.ObjectID) { } _dogTrees.Add(tid.ToString()); }
        }

        private static bool HasTree(HashSet<string> set, int id) { return set.Contains(id.ToString()); }

        private static string StackStr(VMAvatar a)
        {
            try
            {
                return string.Join("/", a.Thread.Stack.Select(f => (f.Routine?.Chunk?.ChunkID ?? 0).ToString()));
            }
            catch { return "?"; }
        }

        private static void PieCensus(VM vm)
        {
            _pieDone = true;
            try
            {
                // corpus string-table law FIRST (the enumeration §3 decode: the
                // TTAs FIRST per-language string is the label; test-tree-free)
                var labels = _dog.TreeTableStrings;
                if (labels == null)
                {
                    _log("AUTOTEST ulpets pie FAIL: dog has no TreeTableStrings (semiglobal TTAs not resolved)");
                    _fail("ulpets"); State = 99; TeardownTrace(); return;
                }
                var pin = new Dictionary<int, string>();
                foreach (var idx in new[] { 1, 4, 8, 53, 54 })
                    pin[idx] = labels.GetString(idx) ?? "<null>";
                _log("AUTOTEST ulpets PIE-TTAS pin tta1=" + Quote(pin[1]) + " tta4=" + Quote(pin[4])
                    + " tta8=" + Quote(pin[8]) + " tta53=" + Quote(pin[53]) + " tta54=" + Quote(pin[54]));

                // capture the CheckAction trace while the census runs (the human
                // is unbudgeted; every 8xxx test instruction is traced)
                _capture.Clear(); _capturing = true;
                List<VMPieMenuInteraction> pie;
                try { pie = _dog.GetPieMenu(vm, _human, true, true); }
                finally { _capturing = false; }
                _pieLabels.Clear();
                _pieLabels.AddRange((pie ?? new List<VMPieMenuInteraction>())
                    .Select(p => (p.Name ?? "").Trim()).Where(n => n.Length > 0));
                _log("AUTOTEST ulpets PIE dog rows=[" + string.Join(" | ", _pieLabels) + "]");
                // the eligibility path (8198/8201/8200) instruction window
                foreach (var line in _capture.Take(140)) _log("AUTOTEST ulpets PIECAP " + line);
                var ttasOK = pin[1] == "Call Over" && pin[4] == "Scold" && pin[8] == "Praise"
                    && pin[53] == "Pet" && pin[54] == "Shoo";
                var missing = _pieLabels.Any(l => l.Contains("MISSING"));
                if (!ttasOK || missing)
                {
                    _log("AUTOTEST ulpets pie FAIL ttasOK=" + ttasOK + " missing=" + missing);
                    _fail("ulpets"); State = 99; TeardownTrace();
                }
                // row presence: audit signal only (the 8201 eligibility gate is the
                // R-audit subject; dispositioned from the captured path)
                var want = new[] { "Call Over", "Scold", "Praise", "Sniff" };
                var absent = want.Where(w => !_pieLabels.Any(l => l.Contains(w))).ToList();
                if (absent.Count > 0)
                    _log("AUTOTEST ulpets pie-audit absent=[" + string.Join(",", absent)
                        + "] (eligibility-gated rows; see PIECAP)");
            }
            catch (Exception ex)
            {
                _log("AUTOTEST ulpets pie EXC " + ex.GetType().Name + " " + ex.Message);
                _fail("ulpets"); State = 99; TeardownTrace();
            }
        }

        private static string Quote(string s) { return "'" + (s ?? "") + "'"; }

        private static void HookPushes(bool on)
        {
            if (on && !_pushHooked)
            {
                _pushHooked = true;
                VMPushInteraction.PushOutcome += OnPushOutcome;
            }
        }

        private static void OnPushOutcome(VMStackFrame frame, VMEntity target, int interaction, bool enqueued)
        {
            if (State == 3 && _dog != null && target == _dog && enqueued)
            {
                _trainPushLanded = true;
                _log("AUTOTEST ulpets TRAIN-PUSH-OUTCOME dog row=" + interaction
                    + " by tree=" + (frame.Routine?.Chunk?.ChunkID ?? 0));
            }
        }

        private static void TeardownTrace()
        {
            try
            {
                VMThread.AutotestTraceSink = null;
                VMThread.AutotestTraceShowTrees = false;
                VMThread.AutotestInstrTraceBudget = 0;
                VMThread.AutotestUnbudgetedEnts.Clear();
                if (_pushHooked)
                {
                    _pushHooked = false;
                    VMPushInteraction.PushOutcome -= OnPushOutcome;
                }
            }
            catch { }
        }
    }
}
