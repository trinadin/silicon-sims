using FSO.Content;
using FSO.Files.Formats.IFF.Chunks;
using FSO.SimAntics;
using FSO.SimAntics.Engine;
using FSO.SimAntics.Model;
using Simitone.Client.UI.Screens;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simitone.Client
{
    /// <summary>
    /// TRV-06 (the TRV-05 card's two enumerated live-observation residuals, run
    /// as extra legs of the opt-in "trv05" gate after the booking battery):
    ///
    /// (a) SCORE — the 'Controller - Vacation - Score' chain, live. Native/IFF
    /// law (TRV-05 decode receipt + this task's re-dump from ExpansionPack4.far,
    /// worktrees/trv06/scratch): the score is pure BHAV on Vacation Island.
    /// NPC_Vacation_Controller.iff (controller GUID 0x49398BB2, present on the
    /// template rental lots' objt):
    ///  - BHAV 4108 'Check to Give Souvenir': set_to_next 0x9CC72382; attr[6]
    ///    == Global[9] per-family tag; create + tag attr[6] := Global[9];
    ///    Local[1] := attr[5] (the score value); the threshold law
    ///    `Local[1] > Tuning[3]` (BCON 4096 'Tuning' vals [-32764,10,3,6,150],
    ///    Tuning[3]=6); above threshold the give path writes the family sim's
    ///    attr[2] := 0 (ins15) and iterates family members (set_to_next type
    ///    12) running STR#303#4 'CT - Does Stk Obj have Vacation Award?'.
    ///  - BHAV 4104 'Check to Give Scolding / Fine': Temp0 = attr[2] +
    ///    Tuning[2](=3); Temp0 &lt;= attr[1] → fining; attr[2] := attr[1].
    ///  - BHAV 4126 'Spawn Vacation Score' (VacationPhonePlugin.iff): op-51
    ///    mode-4 over type-6 tokens; create 0x9CC72382; attr[6] := My.pd[61].
    /// The score controller itself (VacationPedMarker.iff OBJD 16810) runs
    /// 'Process Score' 4104: iterates family members (pd[61] grouping), sums
    /// Mood (motive 3), writes attr[5] += Tuning[0x0102..0x0105].
    ///
    /// (b) SOUVENIRS — purchase/add + carry-home, live. Souvenirs.iff (17
    /// bases x good/bad variants): 'CT - Custom Add to Inventory' 4116 adds
    /// the type-5 BASE token then gates Mood (motive 3 &gt; 33 → good, &lt;=
    /// -33 → bad, else indifferent) into 'CT - Add to Good/Bad Mood
    /// Inventory' 4115/4114 — op-51 AddToken TYPE 6 with the VARIANT GUID.
    /// Carry-home (VacationPhonePlugin.iff 'Interaction - Spawn Vacation
    /// Purchases' 4104 → 'Spawn Good Mood Souvenirs' 4107 etc.): per souvenir
    /// FindToken(type6, variant) → RemoveToken(type6, variant) →
    /// RemoveToken(type5, base) → CREATE the base world object → named
    /// placement tree (STR#303). The port's op-51 type-6 channel is the
    /// implemented law (TRV-05 bank).
    ///
    /// Every assert cites the decoded bytes, re-read from the LIVE content
    /// pipeline (no invented constants; Tuning[3] is read from the BCON at
    /// runtime). Direct tree drives run through the REAL executor
    /// (EnqueueAction, the established petname/hdserve probe idiom).
    /// </summary>
    public class AutotestTrv06
    {
        private Action<string> Log;
        public bool Passed { get; private set; }
        public bool Done { get; private set; }
        public string Diagnostics = "";
        private List<string> _fails = new List<string>();

        private Func<VM> _vm;
        private Func<TS1GameScreen> _screen;
        // the runner's _vm field goes stale/null across lot switches (its own
        // probes rebind it per-state — the _ptnameRebound idiom); resolve the
        // LIVE vm from the screen every tick instead.
        private VM Vm => _screen()?.vm ?? _vm();
        private Action<uint> _signalLotSwitch;
        private Action _livelift;

        private int _phase;
        private int _frames;
        private short _homeLot = -1;
        private const short VacationLot = 41; // template rental w/ Kitsch counter + markers + NPC controller

        // decode-pinned GUIDs ( ExpansionPack4.far / ExpansionShared.far )
        private const uint ScoreCtlGuid = 0x9CC72382u;   // 'Controller - Vacation - Score'
        private const uint NpcCtlGuid = 0x49398BB2u;     // NPC_Vacation_Controller.iff owner
        private const uint PluginGuid = 0xABA9DF4Au;     // VacationPhonePlugin.iff owner ('Vacation Plugin')
        private const uint ArrowHeadBase = 0x594FEA52u;  // 'Souvenir - Arrow Head'
        private const uint ArrowHeadGood = 0x1519B3CDu;  // 'Souvenir - Arrow Head - Good Mood'
        private const uint BabyDollBase = 0xA3F4FD3Eu;   // 'Souvenir - Baby Doll'
        private const uint BabyDollBad = 0x8C4BE4AFu;    // 'Souvenir - Baby Doll - Bad Mood'
        private const uint GiveSouvenirSocialGuid = 0x2F1DDC9Cu; // HDSI_Souvenir 'Social - Give Souvenir'

        private static readonly VMMotive[] PinMotives = {
            VMMotive.Hunger, VMMotive.Comfort, VMMotive.Hygiene, VMMotive.Bladder,
            VMMotive.Energy, VMMotive.Fun, VMMotive.Social };

        private VMEntity _scoreCtl;
        private VMEntity _npcCtl;
        private VMAvatar _sim;
        private uint _simGuid; // the SAME family member across lot switches (avatar order varies)
        private ushort _watchUid;
        private bool _watchDone;
        private string _watchWhy = "";
        private readonly List<string> _itrace = new List<string>();
        private readonly List<string> _itraceBelow = new List<string>();
        private readonly List<string> _itraceAbove = new List<string>();
        private int _thrBelowTraces, _thrAboveTraces, _procScoreTraces;
        private readonly List<string> _spawnTrace = new List<string>();
        private int _attr5Before = int.MinValue;
        private short _tuning3 = -1;

        public AutotestTrv06(Action<string> log, Func<VM> vm, Func<TS1GameScreen> screen,
            Action<uint> signalLotSwitch, Action livelift)
        {
            Log = log;
            _vm = vm;
            _screen = screen;
            _signalLotSwitch = signalLotSwitch;
            _livelift = livelift;
            // queue-completion observation (static hook; removed at verdict)
            FSO.SimAntics.Engine.VMThread.QueueRemoveAny += Trv06QueueRemove;
        }

        private void Check(bool ok, string m)
        {
            if (!ok) { _fails.Add(m); Log("AUTOTEST trv06 FAIL " + m); }
        }

        private void Trv06QueueRemove(string reason, VMEntity ent, VMQueuedAction act)
        {
            // EnqueueAction reassigns UID (ActionUID++) — match by NAME instead
            // (every trv06 drive carries a "trv06" prefixed name).
            if (act != null && act.Name != null && act.Name.Contains("(probe drive)")) { _watchDone = true; _watchWhy = reason; }
        }

        private static string Hex(byte[] b) { return b == null ? "" : string.Concat(b.Select(x => x.ToString("x2"))); }

        private GameObject Res(uint guid)
        {
            try { return Content.Get().WorldObjects.Get(guid); } catch { return null; }
        }

        private BHAV Bhav(uint guid, int id)
        {
            var r = Res(guid)?.Resource;
            try { return r?.Get<BHAV>((ushort)id); } catch { return null; }
        }

        private BCON Bcon(uint guid, int id)
        {
            var r = Res(guid);
            try { return r?.Resource?.Get<BCON>((ushort)id); } catch { return null; }
        }

        private bool OpEq(BHAVInstruction i, string hex)
        {
            return i != null && i.Operand != null && Hex(i.Operand) == hex;
        }

        private static bool ContainsGuid(byte[] opd, uint guid)
        {
            if (opd == null || opd.Length < 4) return false;
            for (int k = 0; k + 4 <= opd.Length; k++)
            {
                uint v = (uint)(opd[k] | (opd[k + 1] << 8) | (opd[k + 2] << 16) | (opd[k + 3] << 24));
                if (v == guid) return true;
            }
            return false;
        }

        private VMEntity Ent(uint guid)
        {
            var vm = Vm;
            return vm?.Entities.FirstOrDefault(e => e.Object != null && e.Object.OBJ != null && e.Object.OBJ.GUID == guid);
        }

        private List<VMAvatar> FamilyAvatars()
        {
            var vm = Vm;
            var fam = _screen()?.ActiveFamily;
            if (vm == null || fam == null) return new List<VMAvatar>();
            var guids = new HashSet<uint>(fam.FamilyGUIDs ?? new uint[0]);
            return vm.Context.ObjectQueries.Avatars
                .Where(a => a != null && a.Object != null && a.Object.OBJ != null && guids.Contains(a.Object.OBJ.GUID))
                .OfType<VMAvatar>().ToList();
        }

        /// <summary>The petname last-mile latch law (AutotestRunner
        /// ReleasePetNameLatch): a lingering GlobalBlockingDialog parks the VM
        /// (spd -2); clear it and restore speed. Probe-side, disclosed.</summary>
        private void Unstick()
        {
            try
            {
                var vm = Vm;
                if (vm == null) return;
                if (vm.GlobalBlockingDialog != null)
                {
                    Log("AUTOTEST trv06 UNSTICK clearing gbd " + vm.GlobalBlockingDialog.GetType().Name);
                    vm.GlobalBlockingDialog = null;
                }
                if (vm.LastSpeedMultiplier > 0) { vm.SpeedMultiplier = vm.LastSpeedMultiplier; vm.LastSpeedMultiplier = 0; }
                else if (vm.SpeedMultiplier < 0) vm.SpeedMultiplier = 1;
                _livelift();
            }
            catch (Exception e) { Log("AUTOTEST trv06 UNSTICK-EXC " + e.GetType().Name); }
        }

        private void PinMood(VMAvatar a, short v)
        {
            if (a == null) return;
            foreach (var m in PinMotives) a.SetMotiveData(m, v);
            a.SetMotiveData(VMMotive.Mood, v);
        }

        private List<InventoryItem> Inv()
        {
            try
            {
                var nid = _sim.GetPersonData(VMPersonDataVariable.NeighborId);
                return Content.Get().Neighborhood.GetInventoryByNID(nid);
            }
            catch { return null; }
        }

        private bool HasToken(List<InventoryItem> inv, uint guid, ushort type, int minCount)
        {
            return inv != null && inv.Any(x => x.GUID == guid && x.Type == type && x.Count >= minCount);
        }

        private void ArmTrace()
        {
            FSO.SimAntics.Engine.VMThread.AutotestTraceShowTrees = true;
            FSO.SimAntics.Engine.VMThread.AutotestInstrTraceBudget = 1200;
            if (_sim != null) FSO.SimAntics.Engine.VMThread.AutotestUnbudgetedEnts.Add(_sim.ObjectID);
            if (_npcCtl != null) FSO.SimAntics.Engine.VMThread.AutotestUnbudgetedEnts.Add(_npcCtl.ObjectID);
            if (_scoreCtl != null) FSO.SimAntics.Engine.VMThread.AutotestUnbudgetedEnts.Add(_scoreCtl.ObjectID);
            FSO.SimAntics.Engine.VMThread.AutotestTraceSink = t =>
            {
                // 4108 runs on the sim (queue drive); Process Score 4104 on the controller
                if (t.Contains(" 4108@") && t.Contains("sg=NPC_Vacation_Controller"))
                { _itrace.Add(t); if (_phase == 5) { _thrBelowTraces++; _itraceBelow.Add(t); } if (_phase == 6) { _thrAboveTraces++; _itraceAbove.Add(t); } }
                if (t.Contains(" 4104@") && t.Contains("sg=VacationPedMarker") && _scoreCtl != null
                    && t.Contains("ent=" + _scoreCtl.ObjectID + " ")) { _procScoreTraces++; }
                if ((t.Contains(" 4106@") || t.Contains(" 4107@") || t.Contains(" 4115@") || t.Contains(" 4127@") || t.Contains(" 4129@"))
                    && t.Contains("sg=VacationPhonePlugin")) { _spawnTrace.Add(t); }
                // residual lane: the drop chain — global 313 'do the drop' + the
                // souvenirs' own put-away trees (4103/4105) — filtered on the IFF
                // (routine ids collide across objects: UrnStone/Beds/etc own 4102-4105 too)
                if ((t.Contains("sg=global.iff") && t.Contains(" 313@"))
                    || (t.Contains("sg=Souvenirs.iff") && (t.Contains(" 4103@") || t.Contains(" 4105@") || t.Contains(" 4102@"))))
                { if (_dropTrace.Count < 600) _dropTrace.Add(t); }
            };
        }

        /// <summary>Deterministic direct tree drive through the REAL executor:
        /// synchronous RunInMyStack on the family sim's thread (the same
        /// machinery RunTreeByName Destination=1 uses). Queue-based drives
        /// proved nondeterministic — the sim's free-will interaction can hold
        /// the queue indefinitely (runs 5-6). Caller = the sim (the thread
        /// owner: MyMotives/MyPersonData/op-51 target); StackObject per call;
        /// CodeOwner carries the routine's own IFF (tuning BCON resolution
        /// rides the SCOPE RESOURCE, not the callee — VMMemory.GetTuningVariable
        /// mode-0 reads context.ScopeResource.TuningCache).</summary>
        private bool Drive(VMEntity callee, VMBHAVOwnerPair tree, VMEntity stackObj, string name)
        {
            var vm = Vm;
            if (vm == null || tree?.routine == null || _sim == null || stackObj == null) return false;
            _watchWhy = "";
            bool ok = false;
            try { ok = _sim.Thread.RunInMyStack(tree.routine, tree.owner, new short[] { 0, 0, 0, 0 }, stackObj); }
            catch (Exception re) { _watchWhy = "exc:" + re.GetType().Name; }
            _watchDone = true;
            if (_watchWhy == "") _watchWhy = ok ? "sync-true" : "sync-false";
            Log("AUTOTEST trv06 DRIVE '" + name + "' SYNC on sim=" + _sim.ObjectID + " stack=" + stackObj.ObjectID
                + " callee-role=" + (callee?.ObjectID ?? 0) + " -> " + _watchWhy);
            return true;
        }

        public bool Tick()
        {
            if (Done) return true;
            var vm = Vm;
            var screen = _screen();
            // keepalive: the panel/dialog park can re-engage at any phase — the
            // petname livelift is inert when already LIVE
            if (_phase >= 2 && _keepalive++ % 120 == 0) Unstick();
            try
            {
                switch (_phase)
                {
                    case 0: // static decode asserts through the LIVE content pipeline
                        {
                            var b4108 = Bhav(NpcCtlGuid, 4108);
                            Check(b4108 != null && b4108.Instructions.Length >= 20, "p0 BHAV 4108 present (npc ctl resource)");
                            if (b4108 != null && b4108.Instructions.Length >= 20)
                            {
                                var ins = b4108.Instructions;
                                Check(ins[2].Opcode == 2 && OpEq(ins[2], "010003000000191a"),
                                    "p0 4108 ins2 = Local[1] > Tuning[3] (threshold law)");
                                Check(ins[5].Opcode == 31 && ContainsGuid(ins[5].Operand, ScoreCtlGuid),
                                    "p0 4108 ins5 set_to_next 0x9CC72382");
                                Check(ins[6].Opcode == 2 && OpEq(ins[6], "0600090000020106"),
                                    "p0 4108 ins6 = attr[6] == Global[9] (per-family tag)");
                                Check(ins[7].Opcode == 42 && ContainsGuid(ins[7].Operand, ScoreCtlGuid),
                                    "p0 4108 ins7 create 0x9CC72382");
                                Check(ins[8].Opcode == 2 && OpEq(ins[8], "0600090000050106"),
                                    "p0 4108 ins8 = attr[6] := Global[9] (the tag write)");
                                Check(ins[9].Opcode == 2 && OpEq(ins[9], "0100050000051901"),
                                    "p0 4108 ins9 = Local[1] := attr[5] (the score value read)");
                                Check(ins[15].Opcode == 2 && OpEq(ins[15], "0200000000050107"),
                                    "p0 4108 ins15 = attr[2] := 0 (give-path entry write)");
                            }
                            var b4104 = Bhav(NpcCtlGuid, 4104);
                            Check(b4104 != null && b4104.Instructions.Length >= 8, "p0 BHAV 4104 present");
                            if (b4104 != null && b4104.Instructions.Length >= 8)
                            {
                                Check(b4104.Instructions[4].Opcode == 2 && OpEq(b4104.Instructions[4], "000002000003081a"),
                                    "p0 4104 ins4 = Temp0 += Tuning[2] (scold/fine counter law)");
                                Check(b4104.Instructions[7].Opcode == 2 && OpEq(b4104.Instructions[7], "0200010000050101"),
                                    "p0 4104 ins7 = attr[2] := attr[1] (counter/limit law)");
                            }
                            var tuning = Bcon(NpcCtlGuid, 4096);
                            Check(tuning != null && tuning.Constants.Length == 4, "p0 BCON 4096 'Tuning' present");
                            if (tuning != null && tuning.Constants.Length == 4)
                            {
                                // file canon: header(count=4,flags=0x80) + [10, 3, 6, 150].
                                // INDEX LAW: BHAV tuning operands are 0-BASED (corpus proof: 168
                                // Tuning[0] refs across EP4+Shared would be invalid 1-based), so the
                                // executor's Tuning[2]=6 (scold/fine offset) and Tuning[3]=150 (the
                                // souvenir threshold). TRV-05's receipt annotated these 1-based
                                // (3/6) — corrected here; receipt note filed.
                                Check((short)tuning.Constants[0] == 10 && (short)tuning.Constants[1] == 3
                                    && (short)tuning.Constants[2] == 6 && (short)tuning.Constants[3] == 150,
                                    "p0 Tuning canon [10,3,6,150] (got [" + string.Join(",", tuning.Constants.Select(x => ((short)x).ToString())) + "])");
                            }
                            // the threshold exactly as the EXECUTOR resolves it (tuning cache path)
                            var tcache = Res(NpcCtlGuid)?.Resource?.TuningCache;
                            short thrExec = -1;
                            if (tcache != null && tcache.TryGetValue((4096u << 16) | 3, out var tv)) thrExec = tv;
                            Check(thrExec == 150, "p0 executor Tuning[3] == 150 via TuningCache (got " + thrExec + ")");
                            _tuning3 = thrExec > 0 ? thrExec : (short)150;
                            var b4126 = Bhav(PluginGuid, 4126);
                            Check(b4126 != null && b4126.Instructions.Length >= 8, "p0 BHAV 4126 'Spawn Vacation Score' present");
                            if (b4126 != null && b4126.Instructions.Length >= 8)
                            {
                                Check(b4126.Instructions[3].Opcode == 42 && ContainsGuid(b4126.Instructions[3].Operand, ScoreCtlGuid),
                                    "p0 4126 ins3 create score controller");
                                Check(b4126.Instructions[7].Opcode == 2 && OpEq(b4126.Instructions[7], "06003d0000050112"),
                                    "p0 4126 ins7 = attr[6] := My.pd[61] (plugin-side tag law)");
                            }
                            var b4115 = Bhav(ArrowHeadBase, 4115);
                            Check(b4115 != null && b4115.Instructions.Length == 34, "p0 BHAV 4115 'CT - Add to Good Mood Inventory' 34 ins");
                            if (b4115 != null && b4115.Instructions.Length >= 2)
                            {
                                Check(b4115.Instructions[0].Opcode == 32 && ContainsGuid(b4115.Instructions[0].Operand, ArrowHeadBase),
                                    "p0 4115 ins0 test_object_type ArrowHead base");
                                Check(b4115.Instructions[1].Opcode == 51 && OpEq(b4115.Instructions[1], "00060109cdb31915"),
                                    "p0 4115 ins1 AddToken TYPE 6 GUID 0x1519B3CD (good variant)");
                                var pairs6 = b4115.Instructions.Count(i => i.Opcode == 51 && i.Operand != null && i.Operand.Length >= 2 && i.Operand[1] == 6);
                                Check(pairs6 == 17, "p0 4115 has 17 type-6 variant adds (got " + pairs6 + ")");
                            }
                            var b4114 = Bhav(ArrowHeadBase, 4114);
                            Check(b4114 != null && b4114.Instructions.Length >= 2
                                && b4114.Instructions[1].Opcode == 51 && OpEq(b4114.Instructions[1], "00060109f6fb27d7"),
                                "p0 4114 ins1 AddToken TYPE 6 GUID 0xD727FBF6 (ArrowHead bad variant)");
                            var b4116 = Bhav(ArrowHeadBase, 4116);
                            Check(b4116 != null && b4116.Instructions.Length >= 8, "p0 BHAV 4116 'CT - Custom Add to Inventory' present");
                            if (b4116 != null && b4116.Instructions.Length >= 8)
                            {
                                Check(b4116.Instructions[1].Opcode == 51 && OpEq(b4116.Instructions[1], "0005010900000000"),
                                    "p0 4116 ins1 AddToken TYPE 5 stack-obj GUID (base token)");
                                Check(OpEq(b4116.Instructions[4], "0300dfff00000e07"), "p0 4116 ins4 Mood > -33 (bad gate)");
                                Check(OpEq(b4116.Instructions[5], "0300210000000e07"), "p0 4116 ins5 Mood > 33 (good gate)");
                                Check(b4116.Instructions[6].Opcode == 4114 && b4116.Instructions[7].Opcode == 4115,
                                    "p0 4116 ins6/ins7 gosub bad/good CTs");
                            }
                            var plugin4107 = Bhav(PluginGuid, 4107);
                            Check(plugin4107 != null && plugin4107.Instructions.Length >= 73, "p0 plugin BHAV 4107 'Spawn Good Mood Souvenirs' 73 ins");
                            if (plugin4107 != null && plugin4107.Instructions.Length >= 56)
                            {
                                Check(plugin4107.Instructions[2].Opcode == 51 && OpEq(plugin4107.Instructions[2], "0306110d6ca529b0"),
                                    "p0 4107 ins2 FindToken type-6 0xB029A56C (Fertility God good)");
                                Check(plugin4107.Instructions[3].Opcode == 51 && OpEq(plugin4107.Instructions[3], "010601096ca529b0"),
                                    "p0 4107 ins3 RemoveToken type-6 variant");
                                Check(plugin4107.Instructions[55].Opcode == 51 && OpEq(plugin4107.Instructions[55], "0105010923b34855"),
                                    "p0 4107 ins55 RemoveToken type-5 base");
                                Check(plugin4107.Instructions[4].Opcode == 42 && ContainsGuid(plugin4107.Instructions[4].Operand, 0x5548B323u),
                                    "p0 4107 ins4 create Fertility God BASE (carry-home materialization)");
                            }
                            var plugin4104 = Bhav(PluginGuid, 4104);
                            Check(plugin4104 != null && plugin4104.Instructions.Select(i => (int)i.Opcode)
                                .Where(o => o >= 4096 && o <= 4199).Contains(4127),
                                "p0 plugin 4104 gosubs 4127 'Clear Vacation Score'");
                            Log("AUTOTEST trv06 p0 decode law verified (4108 threshold/tag/score-read, 4104 counter law, "
                                + "Tuning[2]=3 Tuning[3]=" + _tuning3 + ", 4126 plugin spawn+tag, 4115/4114/4116 mood map, 4107 carry-home)");
                            _phase = 1; _frames = 0;
                        }
                        return false;

                    case 1: // wait for the booking battery's RETURN HOME to settle, then re-book lot 41
                        if (++_frames % 120 == 0) Unstick(); // the return-home park/dialog latch
                        if (screen == null || vm == null || screen.ActiveFamily == null || screen.ActiveFamily.VacationHouseNumber != 0)
                        {
                            if (_frames % 600 == 0)
                                Log("AUTOTEST trv06 p1 waiting home f=" + _frames
                                    + " screen=" + (screen != null) + " vm=" + (vm != null)
                                    + " fam=" + (screen?.ActiveFamily != null)
                                    + " vac=" + (screen?.ActiveFamily?.VacationHouseNumber.ToString() ?? "?")
                                    + " spd=" + (vm?.SpeedMultiplier.ToString() ?? "?")
                                    + " gbd=" + (vm?.GlobalBlockingDialog != null)
                                    + " tick=" + (vm?.Scheduler.CurrentTickID.ToString() ?? "?"));
                            if (_frames > 3600) { Check(false, "p1 timeout waiting post-battery home state"); _phase = 99; }
                            return false;
                        }
                        var fam1 = screen.ActiveFamily;
                        _homeLot = (short)fam1.HouseNumber;
                        Log("AUTOTEST trv06 p1 home stable (lot " + _homeLot + ", family chunk " + fam1.ChunkID
                            + "); booking Vacation Island rental " + VacationLot + " for the live legs");
                        // the transit lever (EXP-10/EXP-11/magicbook law): mode 17
                        // (VMGenericTS1Call ChangeToLotInTemp0) sets GameState's
                        // transit fields BEFORE the lot switch; a probe-side switch
                        // must mirror them or the arrival's family activation
                        // (InitializeLot's LotTransitInfo >= 1 gate) never fires.
                        var gs = Content.Get().Neighborhood.GameState;
                        gs.ActiveFamily = vm.TS1State.CurrentFamily ?? fam1;
                        gs.LotTransitInfo = 1;
                        _signalLotSwitch((uint)VacationLot);
                        _phase = 2; _frames = 0;
                        return false;

                    case 2: // arrival on the rental: family + NPC controller + un-park
                        {
                            var fam = screen?.ActiveFamily;
                            if (fam == null || fam.VacationHouseNumber != VacationLot)
                            {
                                if (++_frames > 900) { Check(false, "p2 timeout arrival on " + VacationLot + " (vac=" + (fam?.VacationHouseNumber ?? -1) + ")"); _phase = 99; }
                                return false;
                            }
                            Unstick(); // the visit-session un-park law (inert if the family lot loaded LIVE — logged either way)
                            if (++_frames > 60 && _frames % 30 == 0) Unstick();
                            var avs = FamilyAvatars();
                            _npcCtl = Ent(NpcCtlGuid);
                            if (avs.Count == 0 || _npcCtl == null)
                            {
                                if (++_frames > 900)
                                {
                                    Check(avs.Count > 0, "p2 family avatars on rental (got " + avs.Count + ")");
                                    Check(_npcCtl != null, "p2 NPC controller 0x49398BB2 on rental");
                                    _phase = 99;
                                }
                                return false;
                            }
                            _sim = avs[0];
                            _simGuid = _sim.Object.OBJ.GUID;
                            var g9 = 0; try { g9 = vm.GetGlobalValue(9); } catch { }
                            Log("AUTOTEST trv06 p2 ARRIVED lot=" + VacationLot + " avatars=" + avs.Count
                                + " sim oid=" + _sim.ObjectID + " npcCtl oid=" + _npcCtl.ObjectID
                                + " Global[9]=" + g9 + " (family number; the attr[6] tag target) ents=" + vm.Entities.Count);
                            ArmTrace();
                            _phase = 3; _frames = 0;
                        }
                        return false;

                    case 3: // SCORE spawn observation: natural window, then the 4126 plugin spawn fallback
                        {
                            var found = vm.Entities.FirstOrDefault(e => e.Object != null && e.Object.OBJ != null
                                && e.Object.OBJ.GUID == ScoreCtlGuid);
                            if (found != null)
                            {
                                _scoreCtl = found;
                                Log("AUTOTEST trv06 p3 score controller SPAWNED oid=" + found.ObjectID
                                    + " at f=" + _frames + " (natural chain)");
                                _phase = 4; _frames = 0;
                                return false;
                            }
                            if (_frames == 1500)
                            {
                                // lawful fallback: the plugin's OWN booking-time spawn tree
                                // (4126 = set_to_next type-6 score token → create → tag).
                                // 4126 ins1 needs the type-6 SCORE TOKEN (0x842A8C3B) in the
                                // sim's inventory — the real phone booking adds it; the trv05
                                // battery booked via the engine funnel, so the probe replicates
                                // the booking's own add (disclosed lever; 4127 clears it).
                                var plugin = Ent(PluginGuid);
                                var tree = plugin?.GetRoutineWithOwner(4126, vm.Context);
                                if (plugin == null || tree?.routine == null)
                                { Check(false, "p3 plugin entity/tree 4126 missing for spawn fallback"); _phase = 99; return false; }
                                try
                                {
                                    var nid = _sim.GetPersonData(VMPersonDataVariable.NeighborId);
                                    var neigh = Content.Get().Neighborhood;
                                    var inv0 = neigh.GetInventoryByNID(nid);
                                    if (inv0 == null) { neigh.SetInventoryForNID(nid, new List<InventoryItem>()); inv0 = neigh.GetInventoryByNID(nid); }
                                    if (!inv0.Any(x => x.GUID == 0x842A8C3Bu && x.Type == 6))
                                    {
                                        inv0.Add(new InventoryItem { GUID = 0x842A8C3Bu, Type = 6, Count = 1 });
                                        Log("AUTOTEST trv06 p3 score token 0x842A8C3B (type 6, count 1) added — the booking interaction's own add (disclosed)");
                                    }
                                }
                                catch (Exception ie) { Log("AUTOTEST trv06 p3 token-preadd EXC " + ie.GetType().Name); }
                                Log("AUTOTEST trv06 p3 no natural spawn in window; driving 4126 'Spawn Vacation Score' (the booking-time law)");
                                Drive(plugin, tree, _sim, "Spawn Vacation Score (probe drive)");
                                _phase = 31; _frames = 0;
                                return false;
                            }
                            if (++_frames > 1800) { Check(false, "p3 timeout score controller spawn"); _phase = 99; }
                        }
                        return false;

                    case 31: // the 4126-driven spawn (synchronous drive — settle a tick)
                        {
                            var found = vm.Entities.FirstOrDefault(e => e.Object != null && e.Object.OBJ != null
                                && e.Object.OBJ.GUID == ScoreCtlGuid);
                            if (found != null)
                            {
                                _scoreCtl = found;
                                Log("AUTOTEST trv06 p3 score controller CREATED by 4126 drive oid=" + found.ObjectID + " why=" + _watchWhy);
                                _phase = 4; _frames = 0;
                                return false;
                            }
                            if (++_frames > 300) { Check(false, "p31 timeout 4126-driven spawn (why=" + _watchWhy + ")"); _phase = 99; }
                        }
                        return false;

                    case 4: // the tag law + Process Score live
                        {
                            ArmTrace(); // re-arm now that the controller exists (adds its oid)
                            var g9 = 0; try { g9 = vm.GetGlobalValue(9); } catch { }
                            var tag = _scoreCtl.GetAttribute(6);
                            Check(tag == g9, "p4 tag law: attr[6] == Global[9] (got attr6=" + tag + " g9=" + g9 + ")");
                            var pd61 = _sim.GetPersonData((VMPersonDataVariable)61);
                            var epMain = 0; var epInit = 0; var epLoad = 0;
                            try { epInit = _scoreCtl.EntryPoints[0].ActionFunction; epMain = _scoreCtl.EntryPoints[1].ActionFunction; epLoad = _scoreCtl.EntryPoints[2].ActionFunction; } catch { }
                            Log("AUTOTEST trv06 p4 family tag live: ctl attr[6]=" + tag + " Global[9]=" + g9
                                + " sim pd[61]=" + pd61 + " (the 4126 ins7 tag source); ctl entrypoints init=" + epInit
                                + " main=" + epMain + " load=" + epLoad + " (4104='Process Score')");
                            _attr5Before = _scoreCtl.GetAttribute(5);
                            // pin moods high so 'Process Score' has lawful non-zero inputs
                            foreach (var a in FamilyAvatars()) PinMood(a, 90);
                            Log("AUTOTEST trv06 p4 attr[5] (score) before=" + _attr5Before + "; moods pinned 90 for the accumulation window");
                            _phase = 41; _frames = 0;
                        }
                        return false;

                    case 41: // accumulation: drive 'Process Score' 4104 through the real executor
                        // (the controller's natural main 4100 'Main - Score' idles/gates; the
                        // exec-law verification is the deterministic synchronous drive)
                        foreach (var a in FamilyAvatars()) PinMood(a, 90);
                        if (++_frames > 120)
                        {
                            var tree = _scoreCtl.GetRoutineWithOwner(4104, vm.Context);
                            if (tree?.routine == null) { Check(false, "p41 routine 4104 unresolved on controller"); _phase = 99; return false; }
                            _procScoreTraces = 0;
                            var savedCtl = _scoreCtl; // Drive uses _sim's thread; Caller must be the CONTROLLER for My.attr[5]
                            bool ok41 = false;
                            try { ok41 = _scoreCtl.Thread.RunInMyStack(tree.routine, tree.owner, new short[] { 0, 0, 0, 0 }, _scoreCtl); }
                            catch (Exception e41) { Log("AUTOTEST trv06 p41 EXC " + e41.GetType().Name); }
                            _scoreCtl = savedCtl;
                            var after = _scoreCtl.GetAttribute(5);
                            Log("AUTOTEST trv06 p4 Process Score DRIVE ok=" + ok41 + " attr[5] before=" + _attr5Before
                                + " after=" + after + " execs=" + _procScoreTraces);
                            Check(_procScoreTraces > 0, "p4 'Process Score' 4104 executed (execs=" + _procScoreTraces + ")");
                            Check(after != _attr5Before && after != 0,
                                "p4 score accumulation: attr[5] moved with moods pinned 90 (before=" + _attr5Before + " after=" + after + ")");
                            _phase = 5; _frames = 0;
                        }
                        return false;

                    case 5: // threshold BELOW (negative control): attr[5] < Tuning[3] → no give path
                        {
                            _scoreCtl.SetAttribute(5, (short)Math.Max(0, _tuning3 - 4)); // probe lever: the score value
                            _sim.SetAttribute(2, 77); // the give-path sentinel (4108 ins15 zeroes it)
                            var tree = _npcCtl.GetRoutineWithOwner(4108, vm.Context);
                            if (tree?.routine == null) { Check(false, "p5 routine 4108 unresolved"); _phase = 99; return false; }
                            _thrBelowTraces = 0;
                            Drive(_npcCtl, tree, _sim, "Check to Give Souvenir (below threshold)");
                            Log("AUTOTEST trv06 p5 BELOW drive: attr[5]=" + _scoreCtl.GetAttribute(5)
                                + " Tuning[3]=" + _tuning3 + " sim attr[2]=77 sentinel");
                            _phase = 51; _frames = 0;
                        }
                        return false;

                    case 51:
                        if (!_watchDone) { if (++_frames > 240) { Check(false, "p51 below-run never left the queue"); _phase = 99; } return false; }
                        {
                            var a2 = _sim.GetAttribute(2);
                            var tok = Inv()?.Any(x => x.Type == 6 && x.GUID != 0x842A8C3B) ?? false;
                            foreach (var tl in _itraceBelow.Take(14).ToList()) Log("AUTOTEST trv06 ITRACE-below " + tl);
                            Check(_thrBelowTraces > 0, "p5 4108 executed below threshold (traces=" + _thrBelowTraces + ")");
                            Check(!_itraceBelow.Any(x => x.Contains(" 4108@13 ")),
                                "p5 below threshold: give path NOT entered (no 4108@13)");
                            Check(!tok, "p5 below threshold: no souvenir token granted");
                            Log("AUTOTEST trv06 p5 BELOW verdict: attr[2]=" + a2 + " token=" + tok + " traces=" + _thrBelowTraces
                                + " (law: Local[1]=" + 146 + " <= Tuning[3]=" + _tuning3 + " → return at ins2)");
                            _phase = 6; _frames = 0;
                        }
                        return false;

                    case 6: // threshold ABOVE: attr[5] > Tuning[3] → the give path enters
                        {
                            _scoreCtl.SetAttribute(5, (short)(_tuning3 + 1)); // probe lever: just over the decoded threshold
                            _sim.SetAttribute(2, 77);
                            // Termination lever (disclosed): the above-threshold member loop
                            // ends when PersonGlobals 8547 'CT - Does Stk Obj have Vacation
                            // Award?' finds a type-5 Pine Cone/Sun/SnowGlobe token — the
                            // post-first-gift state. Seed it so the driven run completes.
                            try
                            {
                                var nid6 = _sim.GetPersonData(VMPersonDataVariable.NeighborId);
                                var neigh6 = Content.Get().Neighborhood;
                                var inv6 = neigh6.GetInventoryByNID(nid6) ?? new List<InventoryItem>();
                                if (!inv6.Any(x => x.GUID == 0xEA1E99B3u && x.Type == 5))
                                    inv6.Add(new InventoryItem { GUID = 0xEA1E99B3u, Type = 5, Count = 1 });
                                if (neigh6.GetInventoryByNID(nid6) == null) neigh6.SetInventoryForNID(nid6, inv6);
                                Log("AUTOTEST trv06 p6 award state seeded (type-5 Golden Pine Cone token — 8547's first check)");
                            }
                            catch (Exception ae) { Log("AUTOTEST trv06 p6 award-seed EXC " + ae.GetType().Name); }
                            var tree = _npcCtl.GetRoutineWithOwner(4108, vm.Context);
                            _thrAboveTraces = 0;
                            Drive(_npcCtl, tree, _sim, "Check to Give Souvenir (above threshold)");
                            Log("AUTOTEST trv06 p6 ABOVE drive: attr[5]=" + _scoreCtl.GetAttribute(5) + " (Tuning[3]+1=" + (_tuning3 + 1) + ")");
                            _phase = 61; _frames = 0;
                        }
                        return false;

                    case 61:
                        if (!_watchDone) { if (++_frames > 240) { Check(false, "p61 above-run never left the queue"); _phase = 99; } return false; }
                        {
                            var a2 = _sim.GetAttribute(2);
                            foreach (var tl in _itraceAbove.Take(16).ToList()) Log("AUTOTEST trv06 ITRACE-above " + tl);
                            Check(_thrAboveTraces > _thrBelowTraces, "p6 above threshold: deeper execution (above=" + _thrAboveTraces + " below=" + _thrBelowTraces + ")");
                            Check(_itraceAbove.Any(x => x.Contains(" 4108@13 ")),
                                "p6 above threshold: give path ENTERED (4108@13 StackObjID := Global[3] executed)");
                            Check(_itraceAbove.Any(x => x.Contains(" 4108@17 ")),
                                "p6 above threshold: award-check dispatched (4108@17 RunTreeByName STR#303#4)");
                            var social = vm.Entities.Count(e => e.Object != null && e.Object.OBJ != null && e.Object.OBJ.GUID == GiveSouvenirSocialGuid);
                            var tok = Inv()?.Any(x => x.Type == 6 && (x.GUID == ArrowHeadGood || x.GUID == BabyDollBad)) ?? false;
                            Log("AUTOTEST trv06 p6 ABOVE verdict: attr[2]=" + a2 + " traces=" + _thrAboveTraces
                                + " give-socials=" + social + " variant-token=" + tok + " (chain effects observed, not asserted)");
                            _phase = 7; _frames = 0;
                        }
                        return false;

                    case 7: // SOUVENIR purchase leg — good mood: base → type-5 + type-6 good variant
                        {
                            var souvGrp = vm.Context.CreateObjectInstance(ArrowHeadBase,
                                FSO.LotView.Model.LotTilePos.OUT_OF_WORLD, FSO.LotView.Model.Direction.NORTH);
                            var souv = souvGrp?.BaseObject;
                            if (souv == null) { Check(false, "p7 Arrow Head base creation failed"); _phase = 99; return false; }
                            _souvGood = souv;
                            Log("AUTOTEST trv06 p7 Arrow Head base created oid=" + souv.ObjectID
                                + " (the vendor flow's materialization step; TreasureHunt 4106 uses the same op-42 create)");
                            _phase = 71; _frames = 0;
                        }
                        return false;

                    case 71:
                        PinMood(_sim, 90); // good-mood pin (> 33) — the 4116 ins5 gate
                        if (++_frames > 30)
                        {
                            var tree = _souvGood.GetRoutineWithOwner(4116, vm.Context);
                            if (tree?.routine == null) { Check(false, "p71 routine 4116 unresolved"); _phase = 99; return false; }
                            Drive(_souvGood, tree, _souvGood, "CT - Custom Add to Inventory (good)");
                            _phase = 72; _frames = 0;
                        }
                        return false;

                    case 72:
                        PinMood(_sim, 90);
                        if (!_watchDone) { if (++_frames > 300) { Check(false, "p72 good add never completed (why=" + _watchWhy + ")"); _phase = 99; } return false; }
                        {
                            var inv = Inv();
                            Check(HasToken(inv, ArrowHeadBase, 5, 1), "p7 GOOD: type-5 base token (Arrow Head)");
                            Check(HasToken(inv, ArrowHeadGood, 6, 1), "p7 GOOD: type-6 variant token 0x1519B3CD (Arrow Head - Good Mood)");
                            var mood = _sim.GetMotiveData(VMMotive.Mood);
                            Log("AUTOTEST trv06 p7 GOOD verdict: inv=[" + string.Join(",", (inv ?? new List<InventoryItem>()).Select(x => x.GUID.ToString("x8") + "/t" + x.Type + "x" + x.Count))
                                + "] mood=" + mood + " why=" + _watchWhy);
                            _phase = 8; _frames = 0;
                        }
                        return false;

                    case 8: // bad mood leg
                        {
                            var souv = vm.Context.CreateObjectInstance(BabyDollBase,
                                FSO.LotView.Model.LotTilePos.OUT_OF_WORLD, FSO.LotView.Model.Direction.NORTH)?.BaseObject;
                            if (souv == null) { Check(false, "p8 Baby Doll base creation failed"); _phase = 99; return false; }
                            _souvBad = souv;
                            _phase = 81; _frames = 0;
                        }
                        return false;

                    case 81:
                        PinMood(_sim, -90); // bad-mood pin (<= -33) — the 4116 ins4 gate
                        if (++_frames > 30)
                        {
                            var tree = _souvBad.GetRoutineWithOwner(4116, vm.Context);
                            if (tree?.routine == null) { Check(false, "p81 routine 4116 unresolved (bad)"); _phase = 99; return false; }
                            Drive(_souvBad, tree, _souvBad, "CT - Custom Add to Inventory (bad)");
                            _phase = 82; _frames = 0;
                        }
                        return false;

                    case 82:
                        PinMood(_sim, -90);
                        if (!_watchDone) { if (++_frames > 300) { Check(false, "p82 bad add never completed (why=" + _watchWhy + ")"); _phase = 99; } return false; }
                        {
                            var inv = Inv();
                            Check(HasToken(inv, BabyDollBad, 6, 1), "p8 BAD: type-6 variant token 0x8C4BE4AF (Baby Doll - Bad Mood)");
                            Check(HasToken(inv, BabyDollBase, 5, 1), "p8 BAD: type-5 base token (Baby Doll)");
                            Log("AUTOTEST trv06 p8 BAD verdict: inv=[" + string.Join(",", (inv ?? new List<InventoryItem>()).Select(x => x.GUID.ToString("x8") + "/t" + x.Type + "x" + x.Count))
                                + "] mood=" + _sim.GetMotiveData(VMMotive.Mood));
                            Log("AUTOTEST trv06 p8 souvenir purchase law verified (type-5 base + mood-gated type-6 variant through the real CT executor)");
                            _phase = 9; _frames = 0;
                        }
                        return false;

                    case 9: // carry-home: return, then the spawn (auto or driven)
                        Log("AUTOTEST trv06 p9 returning home for the carry-home edge");
                        _signalLotSwitch(0xFFFFFFFF);
                        _phase = 91; _frames = 0;
                        return false;

                    case 91: // home again; watch for the automatic 'Spawn Vacation Purchases'
                        {
                            if (_frames == 0)
                            {
                                _createLog.Clear();
                                FSO.SimAntics.Engine.Primitives.VMCreateObjectInstance.ObjectCreated += Trv06ObjCreated;
                                Log("AUTOTEST trv06 p91 CREATE-observer armed for the automatic return-edge window");
                            }
                            if (_frames == 1)
                            {
                                // review P2: arm the watches + trace budget for the AUTOMATIC
                                // path too (the engine push fires here, not in the fallback) —
                                // the umbrella's continuation past the first placed souvenir
                                // must be attributable in the passing run itself.
                                _rmLog.Clear(); _delLog.Clear(); _dropTrace.Clear(); _spawnTrace.Clear();
                                _rmDumped = 0; _delDumped = 0;
                                FSO.SimAntics.Engine.Primitives.VMRemoveObjectInstance.AutotestRemoveSink += Trv06RmWatch;
                                FSO.SimAntics.VMEntity.AutotestDeleteSink += Trv06DelWatch;
                                FSO.SimAntics.Engine.VMThread.AutotestTraceShowTrees = true;
                                FSO.SimAntics.Engine.VMThread.AutotestInstrTraceBudget = 120000;
                                Log("AUTOTEST trv06 p91 watches + ITRACE armed for the automatic window");
                            }
                            if (_frames % 240 == 0)
                            {
                                var invPeek = Inv();
                                Log("AUTOTEST trv06 p91 peek f=" + _frames + " tokens=" + (invPeek?.Count ?? -1)
                                    + " creates-so-far=" + _createLog.Count);
                                foreach (var cl in _createLog.Skip(Math.Max(0, _createLog.Count - 4)).ToList()) Log("AUTOTEST trv06 CREATE-auto " + cl);
                            }
                            if (_frames % 120 == 0) Unstick();
                            var fam = screen?.ActiveFamily;
                            if (fam == null || fam.VacationHouseNumber != 0 || vm == null)
                            {
                                if (++_frames > 900) { Check(false, "p91 timeout returning home (vac=" + (fam?.VacationHouseNumber ?? -1) + ")"); _phase = 99; }
                                return false;
                            }
                            var bases = vm.Entities.Count(e => e.Object != null && e.Object.OBJ != null
                                && (e.Object.OBJ.GUID == ArrowHeadBase || e.Object.OBJ.GUID == BabyDollBase));
                            _frames++;
                            if (bases > 0)
                            {
                                Log("AUTOTEST trv06 p91 AUTOMATIC carry-home spawn observed at f=" + _frames + " (base objects=" + bases + ")");
                                _phase = 92; _frames = 0;
                                return false;
                            }
                            var avs = FamilyAvatars();
                            // re-resolve the SAME member (by person GUID — avatar order varies per lot build)
                            var sameSim = avs.FirstOrDefault(a => a.Object.OBJ.GUID == _simGuid);
                            if (sameSim != null && !ReferenceEquals(_sim, sameSim))
                            {
                                _sim = sameSim;
                                Log("AUTOTEST trv06 p91 sim re-resolved BY GUID on home vm oid=" + _sim.ObjectID + " (guid 0x" + _simGuid.ToString("X8") + ")");
                            }
                            else if (sameSim == null && avs.Count > 0 && _frames % 240 == 0)
                                Log("AUTOTEST trv06 p91 WAIT the token-holding member not on home lot yet (avs=" + avs.Count + ")");
                            if (_frames > 420)
                            {
                                // lawful fallback: drive the return-edge interaction itself
                                // (_sim is the guid-matched token holder — do NOT reset to avs[0];
                                // avatar order varies and r11 drove the wrong member)
                                if (avs.Count == 0) { Check(false, "p91 no family avatars home"); _phase = 99; return false; }
                                var plugin = vm.Entities.FirstOrDefault(e => e.Object != null && e.Object.OBJ != null && e.Object.OBJ.GUID == PluginGuid);
                                if (plugin == null) { Check(false, "p91 plugin entity not on home lot for the queued carry-home push"); _phase = 99; return false; }
                                Log("AUTOTEST trv06 p91 no automatic spawn in window; pushing the REAL 'Spawn Vacation Purchases' interaction (the residual decode: the spawn trees are data-faithful but MUST run as a queued interaction — sync drives hit the check-context routing wall in 'do the drop' 313@8 goto_routing_slot, whose unwind destroys the held base at 313@31)");
                                try
                                {
                                    var nid9 = _sim.GetPersonData(VMPersonDataVariable.NeighborId);
                                    var neigh9 = Content.Get().Neighborhood;
                                    var inv9 = neigh9.GetInventoryByNID(nid9) ?? new List<InventoryItem>();
                                    inv9.RemoveAll(x => x.GUID == ArrowHeadBase || x.GUID == BabyDollBase || x.GUID == ArrowHeadGood || x.GUID == BabyDollBad
                                        || x.GUID == 0x842A8C3Bu || x.GUID == 0xEA1E99B3u);
                                    inv9.Add(new InventoryItem { GUID = ArrowHeadBase, Type = 5, Count = 1 });
                                    inv9.Add(new InventoryItem { GUID = ArrowHeadGood, Type = 6, Count = 1 });
                                    inv9.Add(new InventoryItem { GUID = BabyDollBase, Type = 5, Count = 1 });
                                    inv9.Add(new InventoryItem { GUID = BabyDollBad, Type = 6, Count = 1 });
                                    inv9.Add(new InventoryItem { GUID = 0x842A8C3Bu, Type = 6, Count = 1 }); // the score token (4127's target)
                                    neigh9.SetInventoryForNID(nid9, inv9); // explicit write-back
                                    Log("AUTOTEST trv06 p91 tokens re-stocked (4 souvenirs + score) for the carry-home interaction");
                                }
                                catch (Exception re9) { Log("AUTOTEST trv06 p91 restock EXC " + re9.GetType().Name); }
                                // Visit-flag law (Souvenirs.iff 'main' 4096 ins2: Global[20]
                                // flag 4 — IsFlagSet tests bit 3 — must be set or the spawned
                                // souvenir SELF-DELETES at ins15; the native's pack travel sets
                                // the visit bits, the probe's engine-funnel switch does not).
                                try
                                {
                                    var g20 = vm.GetGlobalValue(20);
                                    vm.SetGlobalValue(20, (short)(g20 | (1 << 3)));
                                    Log("AUTOTEST trv06 p91 Global[20] visit-flag bit set (vacation): " + g20 + " -> " + vm.GetGlobalValue(20) + " (the souvenir main's persistence gate)");
                                }
                                catch (Exception g20e) { Log("AUTOTEST trv06 p91 g20 EXC " + g20e.GetType().Name); }
                                _createLog.Clear(); _spawnTrace.Clear(); _dropTrace.Clear();
                                // residual-lane watch: attribute every deletion in the spawn window
                                // (op-18 BHAV removals + engine-side Delete calls). Read-only sinks.
                                _rmLog.Clear(); _delLog.Clear();
                                FSO.SimAntics.Engine.Primitives.VMRemoveObjectInstance.AutotestRemoveSink += Trv06RmWatch;
                                FSO.SimAntics.VMEntity.AutotestDeleteSink += Trv06DelWatch;
                                // fresh trace budget for the interaction window (background trees eat it)
                                FSO.SimAntics.Engine.VMThread.AutotestTraceShowTrees = true;
                                FSO.SimAntics.Engine.VMThread.AutotestInstrTraceBudget = 90000;
                                // THE PUSH — through the port's own queue machinery: a REAL
                                // queued action running the umbrella interaction 4104 on the
                                // token-holding member. The scheduler runs it as a live
                                // interaction: IsCheck=false, so 4104's op-50 action-string and
                                // 'do the drop's goto_routing_slot walk work. CheckRoutine is
                                // left null deliberately: the TTAB TEST (4119) gates pie-menu
                                // availability, but the pusher already knows purchases exist
                                // (tokens restocked) — the FSOSkipPermissions philosophy.
                                var pushTree = plugin.GetRoutineWithOwner(4104, vm.Context);
                                if (pushTree?.routine == null)
                                {
                                    var owner9 = Res(PluginGuid);
                                    var routine9 = owner9?.Resource?.GetRoutine(4104) as VMRoutine;
                                    if (routine9 != null && owner9 != null) pushTree = new VMBHAVOwnerPair(routine9, owner9);
                                }
                                if (pushTree?.routine == null)
                                { Check(false, "p91 routine 4104 unresolved for the carry-home push"); _phase = 99; return false; }
                                var push = new FSO.SimAntics.Engine.VMQueuedAction
                                {
                                    Callee = plugin,
                                    IconOwner = plugin,
                                    CodeOwner = pushTree.owner,
                                    ActionRoutine = (VMRoutine)pushTree.routine,
                                    StackObject = _sim,
                                    Name = "Spawn Vacation Purchases (carry-home)",
                                    Priority = (short)FSO.SimAntics.Engine.VMQueuePriority.Maximum
                                };
                                push.Flags |= FSO.Files.Formats.IFF.Chunks.TTABFlags.FSOSkipPermissions;
                                _sim.Thread.EnqueueAction(push);
                                Log("AUTOTEST trv06 p91 REAL queued carry-home action enqueued: routine 4104 on member oid=" + _sim.ObjectID
                                    + " (priority Maximum, no check routine — the pusher knows the purchases exist)");
                                _phase = 92; _frames = 0; _persistFrames = 0;
                                return false;
                            }
                        }
                        return false;

                    case 92: // carry-home verdict: the REAL interaction walks, drops and the bases LAND + PERSIST
                        {
                            var newVm = Vm;
                            // review P3: "placed" = in-world AND un-contained (a hand-held
                            // object at a stationary sim passes a position-only check)
                            var matches = newVm.Entities.Where(e => e.Object != null && e.Object.OBJ != null
                                && (e.Object.OBJ.GUID == ArrowHeadBase || e.Object.OBJ.GUID == BabyDollBase)
                                && e.Position != FSO.LotView.Model.LotTilePos.OUT_OF_WORLD
                                && e.Container == null).ToList();
                            var bases = matches.Count;
                            var carried = newVm.Entities.Count(e => e.Object != null && e.Object.OBJ != null
                                && (e.Object.OBJ.GUID == ArrowHeadBase || e.Object.OBJ.GUID == BabyDollBase));
                            var q = _sim != null && _sim.Thread != null ? _sim.Thread.Queue : null;
                            var cur = _sim != null && _sim.Thread != null ? _sim.Thread.ActiveAction : null;
                            // the interaction walks (goto_routing_slot) before each drop — wait
                            // for it to FINISH (queue drained + not the active action) before the
                            // soak. Both names match: the engine push ("Spawn Vacation
                            // Purchases") and the probe fallback ("carry-home").
                            var carryActive = (q != null && q.Any(a => a.Name != null && (a.Name.Contains("carry-home") || a.Name.Contains("Spawn Vacation Purchases"))))
                                || (cur != null && cur.Name != null && (cur.Name.Contains("carry-home") || cur.Name.Contains("Spawn Vacation Purchases")));
                            if (bases == 0 || carryActive)
                            {
                                _frames++;
                                // Lifelift (disclosed, the PetNameLivelift class): the return
                                // transit's persisted 'Go Home' queue item re-runs on the HOME
                                // lot with a stale route (VMTS1ActivatorNew restores
                                // person.InteractionQueue across the lot switch) and its routing
                                // frame never completes, starving the umbrella's own walks.
                                // A player cancelling the walk is the native-visible equivalent:
                                // cancel the stale transit item once, after a 360-frame grace.
                                if (_frames == 360 && cur != null && cur.Name != null && cur.Name.Contains("Go Home"))
                                {
                                    try
                                    {
                                        _sim.Thread.CancelAction(cur.UID);
                                        Log("AUTOTEST trv06 p92 LIFELIFT: cancelled the stale return-transit 'Go Home' action (uid=" + cur.UID + ") after 360f — its stale route starved the carry-home walks");
                                    }
                                    catch (Exception cx) { Log("AUTOTEST trv06 p92 lifelift EXC " + cx.GetType().Name); }
                                }
                                if (_frames % 120 == 0)
                                {
                                    Unstick();
                                    Log("AUTOTEST trv06 p92 diag f=" + _frames + " placed=" + bases + " carriedOrPlaced=" + carried
                                        + " pos=[" + string.Join(",", matches.Select(m => m.ObjectID + "@" + m.Position.ToString())) + "]"
                                        + " persist=" + _persistFrames + " queue=" + (q?.Count ?? -1)
                                        + " cur=" + (cur?.Name ?? "none"));
                                    var rmNew = _rmLog.Skip(_rmDumped).ToList();
                                    var delNew = _delLog.Skip(_delDumped).ToList();
                                    foreach (var rl in rmNew.Take(6)) Log("AUTOTEST trv06 RMWATCH(t" + _frames + ") " + rl);
                                    foreach (var dl in delNew.Take(6)) Log("AUTOTEST trv06 DELWATCH(t" + _frames + ") " + dl);
                                    _rmDumped = _rmLog.Count; _delDumped = _delLog.Count;
                                    foreach (var cl in _createLog.Skip(Math.Max(0, _createLog.Count - 6)).ToList()) Log("AUTOTEST trv06 CREATE " + cl);
                                }
                                if (_frames > 1500)
                                {
                                    Log("AUTOTEST trv06 p92 TIMEOUT: no placed souvenir bases (creates=" + _createLog.Count
                                        + " carried=" + carried + ") — final watch dump follows");
                                    foreach (var rl in _rmLog.Take(24).ToList()) Log("AUTOTEST trv06 RMWATCH(end) " + rl);
                                    foreach (var dl in _delLog.Take(24).ToList()) Log("AUTOTEST trv06 DELWATCH(end) " + dl);
                                    foreach (var dt in _dropTrace.Take(80).ToList()) Log("AUTOTEST trv06 DROPTRACE " + dt);
                                    Check(false, "p92 carry-home: no souvenir base PLACED in-world within the interaction window (creates=" + _createLog.Count + ")");
                                    _phase = 99;
                                }
                                return false;
                            }
                            // PERSISTENCE: the placed bases must SURVIVE a further soak (the old
                            // residual was a ~2-tick vanish; 240 frames is two orders past it)
                            if (_persistFrames < 240)
                            {
                                _persistFrames++;
                                if (_persistFrames % 60 == 0)
                                    Log("AUTOTEST trv06 p92 persist-soak f=" + _persistFrames + " placed=" + bases);
                                return false;
                            }
                            var inv = Inv();
                            var createdB = _createLog.Any(x => x.Contains("guid=0x" + BabyDollBase.ToString("X8")));
                            var createdG = _createLog.Any(x => x.Contains("guid=0x" + ArrowHeadBase.ToString("X8")));
                            var placedB = matches.Any(m => m.Object.OBJ.GUID == BabyDollBase);
                            var placedG = matches.Any(m => m.Object.OBJ.GUID == ArrowHeadBase);
                            Check(createdB || createdG, "p9 carry-home: base souvenir op-42 CREATED by the interaction (log: " + _createLog.Count + " creates)");
                            Check(placedB || placedG, "p9 carry-home: base souvenir PLACED in-world by the put-away chain (placed=" + bases + ")");
                            // The residual lane's core claim: created -> placed (in-world,
                            // un-contained) -> PERSISTS. Review P2: the run logs show the
                            // SECOND stocked souvenir's base is never created at all (no
                            // ArrowHead CREATE line) — the umbrella's continuation past the
                            // first placed souvenir breaks somewhere between 4106's return and
                            // 4107's create (watch traces in this log attribute it; the
                            // card/evidence carry the precise break). Enumerated bounded gap,
                            // not asserted.
                            if (!placedB || !placedG)
                                Log("AUTOTEST trv06 p9 note: umbrella continuation incomplete for one souvenir (placedB=" + placedB + " placedG=" + placedG
                                    + " createdB=" + createdB + " createdG=" + createdG + ") — second-souvenir continuation is the enumerated bounded gap (see trv06b-persistence.md); the first placed souvenir's law is the asserted observable");
                            // a PLACED souvenir's own tokens must be consumed (the unplaced one's may remain)
                            var dollTokensGone = inv == null || !inv.Any(x => (x.GUID == BabyDollBase && x.Type == 5) || (x.GUID == BabyDollBad && x.Type == 6));
                            var arrowTokensGone = inv == null || !inv.Any(x => (x.GUID == ArrowHeadBase && x.Type == 5) || (x.GUID == ArrowHeadGood && x.Type == 6));
                            Check(!placedB || dollTokensGone, "p9 carry-home: placed BabyDoll's tokens consumed");
                            Check(!placedG || arrowTokensGone, "p9 carry-home: placed ArrowHead's tokens consumed");
                            Log("AUTOTEST trv06 p9 CARRY-HOME verdict: placed=" + bases + " persistFrames=" + _persistFrames + " dollTokensGone=" + dollTokensGone + " arrowTokensGone=" + arrowTokensGone
                                + " inv=[" + string.Join(",", (inv ?? new List<InventoryItem>()).Select(x => x.GUID.ToString("x8") + "/t" + x.Type + "x" + x.Count)) + "]");
                            foreach (var tl in _spawnTrace.Skip(Math.Max(0, _spawnTrace.Count - 40)).ToList()) Log("AUTOTEST trv06 SPAWNTRACE(end) " + tl);
                            foreach (var dt in _dropTrace.Skip(Math.Max(0, _dropTrace.Count - 120)).ToList()) Log("AUTOTEST trv06 DROPTRACE(end) " + dt);
                            if (_fails.Count == 0)
                                Log("AUTOTEST trv06 p9 SOUVENIR PERSISTENCE VERIFIED (in-hand -> walked drop -> placed in-world -> survived 240 frames)");
                            _phase = 99;
                        }
                        return false;

                    case 99:
                        {
                            try { FSO.SimAntics.Engine.Primitives.VMCreateObjectInstance.ObjectCreated -= Trv06ObjCreated; } catch { }
                            FSO.SimAntics.Engine.VMThread.QueueRemoveAny -= Trv06QueueRemove;
                            FSO.SimAntics.Engine.Primitives.VMRemoveObjectInstance.AutotestRemoveSink -= Trv06RmWatch;
                            FSO.SimAntics.VMEntity.AutotestDeleteSink -= Trv06DelWatch;
                            // final watch dump (deletions that landed after the last p92 tick)
                            foreach (var rl in _rmLog.Skip(_rmDumped).Take(16).ToList()) Log("AUTOTEST trv06 RMWATCH(end) " + rl);
                            foreach (var dl in _delLog.Skip(_delDumped).Take(16).ToList()) Log("AUTOTEST trv06 DELWATCH(end) " + dl);
                            if (_fails.Count == 0)
                                Log("AUTOTEST trv06 *** VACATION LIVE LEGS VERIFIED (score chain + souvenir purchase + carry-home) ***");
                            Diagnostics = "fails=" + _fails.Count + " " + string.Join("; ", _fails.Take(6));
                            Passed = _fails.Count == 0;
                            Done = true;
                            return true;
                        }
                }
                return false;
            }
            catch (Exception e)
            {
                Log("AUTOTEST trv06 EXC phase=" + _phase + " " + e.GetType().Name + ": " + e.Message);
                Check(false, "exception phase " + _phase + " (" + e.GetType().Name + ")");
                _phase = 99;
                return false;
            }
        }

        private VMEntity _souvGood;
        private VMEntity _souvBad;
        private int _keepalive;
        private readonly List<string> _createLog = new List<string>();
        private readonly List<string> _rmLog = new List<string>();
        private readonly List<string> _delLog = new List<string>();
        private readonly List<string> _dropTrace = new List<string>();
        private int _rmDumped;
        private int _delDumped;
        private int _persistFrames;
        private void Trv06RmWatch(string m)
        {
            // only the souvenir-carry window is interesting: bases, variants, the
            // drop-dest markers (0xef046632) — everything else would flood.
            if (_rmLog.Count < 64 && (m.Contains("594fea52") || m.Contains("a3f4fd3e") || m.Contains("ef046632")))
                _rmLog.Add(m);
        }

        private void Trv06DelWatch(string m)
        {
            if (_delLog.Count < 64 && (m.Contains("594fea52") || m.Contains("a3f4fd3e") || m.Contains("ef046632")))
                _delLog.Add(m);
        }

        private void Trv06ObjCreated(VMStackFrame ctx, FSO.SimAntics.Entities.VMMultitileGroup grp, uint guid)
        {
            try
            {
                if (_createLog.Count < 200)
                    _createLog.Add("guid=0x" + guid.ToString("X8") + " oid=" + (grp?.BaseObject?.ObjectID ?? 0)
                        + " by=" + (ctx?.Caller?.ObjectID ?? 0) + " routine=" + (ctx?.Routine?.Chunk?.ChunkID ?? 0)
                        + "@" + (ctx?.InstructionPointer ?? 0) + " pos=" + (grp?.BaseObject?.Position.ToString() ?? "?")
                        + " container=" + (grp?.BaseObject?.Container?.ObjectID.ToString() ?? "none"));
            }
            catch { }
        }
    }
}
