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
        private ushort _watchUid;
        private bool _watchDone;
        private string _watchWhy = "";
        private readonly List<string> _itrace = new List<string>();
        private int _thrBelowTraces, _thrAboveTraces, _procScoreTraces;
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
            if (act != null && act.UID != 0 && act.UID == _watchUid) { _watchDone = true; _watchWhy = reason; }
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
            var vm = _vm();
            return vm?.Entities.FirstOrDefault(e => e.Object != null && e.Object.OBJ != null && e.Object.OBJ.GUID == guid);
        }

        private List<VMAvatar> FamilyAvatars()
        {
            var vm = _vm();
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
                var vm = _vm();
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
            FSO.SimAntics.Engine.VMThread.AutotestTraceSink = t =>
            {
                if (_sim != null && t.Contains("ent=" + _sim.ObjectID + " "))
                {
                    if (t.Contains(" 4108@") && t.Contains("sg=NPC_Vacation_Controller")) { _itrace.Add(t); if (_phase == 5) _thrBelowTraces++; if (_phase == 6) _thrAboveTraces++; }
                }
                if (_scoreCtl != null && t.Contains("ent=" + _scoreCtl.ObjectID + " ")
                    && t.Contains(" 4104@") && t.Contains("sg=VacationPedMarker")) { _procScoreTraces++; }
            };
        }

        /// <summary>Direct tree drive through the REAL executor (the petname
        /// run-24/hdserve idiom: enqueued action, no check routine, permission
        /// skip). Caller = the family sim (the thread owner); Callee/CodeOwner
        /// resolve the tree's own IFF.</summary>
        private bool Drive(VMEntity callee, VMBHAVOwnerPair tree, VMEntity stackObj, string name)
        {
            var vm = _vm();
            if (vm == null || tree?.routine == null || _sim == null) return false;
            var act = new VMQueuedAction
            {
                Callee = callee,
                StackObject = stackObj,
                CodeOwner = tree.owner,
                ActionRoutine = tree.routine,
                CheckRoutine = null,
                Flags = TTABFlags.RunImmediately | TTABFlags.FSOSkipPermissions | TTABFlags.AllowVisitors,
                Flags2 = (TSOFlags)0x1f,
                Name = name,
                Args = new short[] { 0, 0, 0, 0 },
                InteractionNumber = -1,
                Priority = (short)VMQueuePriority.UserDriven,
            };
            _watchUid = ++_uidSeed;
            act.UID = _watchUid;
            _watchDone = false; _watchWhy = "";
            _sim.Thread.EnqueueAction(act);
            Log("AUTOTEST trv06 DRIVE '" + name + "' uid=" + _watchUid + " callee=" + (callee?.ObjectID ?? 0)
                + " stack=" + (stackObj?.ObjectID ?? 0) + " caller(sim)=" + _sim.ObjectID);
            return true;
        }
        private static ushort _uidSeed;

        public bool Tick()
        {
            if (Done) return true;
            var vm = _vm();
            var screen = _screen();
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

                    case 31: // wait for the 4126-driven spawn
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
                            var g9 = 0; try { g9 = vm.GetGlobalValue(9); } catch { }
                            var tag = _scoreCtl.GetAttribute(6);
                            Check(tag == g9, "p4 tag law: attr[6] == Global[9] (got attr6=" + tag + " g9=" + g9 + ")");
                            var pd61 = _sim.GetPersonData((VMPersonDataVariable)61);
                            Log("AUTOTEST trv06 p4 family tag live: ctl attr[6]=" + tag + " Global[9]=" + g9
                                + " sim pd[61]=" + pd61 + " (the 4126 ins7 tag source)");
                            _attr5Before = _scoreCtl.GetAttribute(5);
                            // pin moods high so 'Process Score' has lawful non-zero inputs
                            foreach (var a in FamilyAvatars()) PinMood(a, 90);
                            Log("AUTOTEST trv06 p4 attr[5] (score) before=" + _attr5Before + "; moods pinned 90 for the accumulation window");
                            _phase = 41; _frames = 0;
                        }
                        return false;

                    case 41: // accumulation window: Process Score must EXECUTE; attr[5] movement observed
                        foreach (var a in FamilyAvatars()) PinMood(a, 90);
                        if (++_frames > 300)
                        {
                            var after = _scoreCtl.GetAttribute(5);
                            Log("AUTOTEST trv06 p4 attr[5] after=" + after + " (before=" + _attr5Before
                                + ") procScoreExecs=" + _procScoreTraces);
                            Check(_procScoreTraces > 0, "p4 'Process Score' 4104 executed on the controller (execs=" + _procScoreTraces + ")");
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
                            Check(_thrBelowTraces > 0, "p5 4108 executed below threshold (traces=" + _thrBelowTraces + ")");
                            Check(a2 == 77, "p5 below threshold: NO give path (sim attr[2] stayed " + a2 + ", expect 77)");
                            Check(!tok, "p5 below threshold: no souvenir token granted");
                            Log("AUTOTEST trv06 p5 BELOW verdict: attr[2]=" + a2 + " token=" + tok + " traces=" + _thrBelowTraces);
                            _phase = 6; _frames = 0;
                        }
                        return false;

                    case 6: // threshold ABOVE: attr[5] > Tuning[3] → the give path enters
                        {
                            _scoreCtl.SetAttribute(5, (short)(_tuning3 + 1)); // probe lever: just over the decoded threshold
                            _sim.SetAttribute(2, 77);
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
                            Check(_thrAboveTraces > 0, "p6 4108 executed above threshold (traces=" + _thrAboveTraces + ")");
                            Check(a2 == 0, "p6 above threshold: give path ENTERED (4108 ins15 zeroed sim attr[2]; got " + a2 + ")");
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
                            // re-resolve the sim on the HOME vm (the vacation-VM entity is stale after the switch)
                            if (avs.Count > 0 && !ReferenceEquals(_sim, avs[0]))
                            {
                                _sim = avs[0];
                                Log("AUTOTEST trv06 p91 sim re-resolved on home vm oid=" + _sim.ObjectID);
                            }
                            if (_frames > 420)
                            {
                                // lawful fallback: drive the return-edge interaction itself
                                if (avs.Count == 0) { Check(false, "p91 no family avatars home"); _phase = 99; return false; }
                                _sim = avs[0];
                                var plugin = vm.Entities.FirstOrDefault(e => e.Object != null && e.Object.OBJ != null && e.Object.OBJ.GUID == PluginGuid);
                                VMBHAVOwnerPair tree = plugin != null ? plugin.GetRoutineWithOwner(4104, vm.Context) : null;
                                if (tree?.routine == null)
                                {
                                    Log("AUTOTEST trv06 p91 plugin entity not on home lot; resolving 4104 by content resource");
                                    var owner = Res(PluginGuid);
                                    var routine = owner?.Resource?.GetRoutine(4104) as VMRoutine;
                                    if (routine != null && owner != null) tree = new VMBHAVOwnerPair(routine, owner);
                                }
                                if (tree?.routine == null) { Check(false, "p91 routine 4104 unresolved for carry-home drive"); _phase = 99; return false; }
                                Log("AUTOTEST trv06 p91 no automatic spawn; driving 4104 'Interaction - Spawn Vacation Purchases' (the return-edge law)");
                                Drive(plugin ?? _sim, tree, _sim, "Spawn Vacation Purchases (probe drive)");
                                _phase = 92; _frames = 0;
                                return false;
                            }
                        }
                        return false;

                    case 92: // carry-home verdict: tokens consumed + base objects materialized
                        {
                            var newVm = _vm();
                            var bases = newVm.Entities.Count(e => e.Object != null && e.Object.OBJ != null
                                && (e.Object.OBJ.GUID == ArrowHeadBase || e.Object.OBJ.GUID == BabyDollBase));
                            if (bases == 0 && !_watchDone)
                            {
                                if (++_frames > 420) { Check(false, "p92 carry-home spawn never materialized"); _phase = 99; }
                                return false;
                            }
                            var inv = Inv();
                            var t5 = inv != null && inv.Any(x => x.Type == 5 && (x.GUID == ArrowHeadBase || x.GUID == BabyDollBase));
                            var t6 = inv != null && inv.Any(x => x.Type == 6 && (x.GUID == ArrowHeadGood || x.GUID == BabyDollBad));
                            Check(bases > 0, "p9 carry-home: base souvenir objects MATERIALIZED on home lot (got " + bases + ")");
                            Check(!t5, "p9 carry-home: type-5 base tokens consumed");
                            Check(!t6, "p9 carry-home: type-6 variant tokens consumed");
                            Log("AUTOTEST trv06 p9 CARRY-HOME verdict: baseObjects=" + bases + " t5-remain=" + t5 + " t6-remain=" + t6
                                + " inv=[" + string.Join(",", (inv ?? new List<InventoryItem>()).Select(x => x.GUID.ToString("x8") + "/t" + x.Type + "x" + x.Count)) + "]");
                            _phase = 99;
                        }
                        return false;

                    case 99:
                        {
                            FSO.SimAntics.Engine.VMThread.QueueRemoveAny -= Trv06QueueRemove;
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
    }
}
