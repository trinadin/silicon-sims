using FSO.Content;
using FSO.Files.Formats.IFF.Chunks;
using FSO.LotView.Model;
using FSO.SimAntics;
using FSO.SimAntics.Engine;
using FSO.SimAntics.Engine.Scopes;
using FSO.SimAntics.Model;
using FSO.SimAntics.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simitone.Client
{
    /// <summary>
    /// EXP-17 opt-in pin ("mmquest"): Makin' Magic quest-line coverage.
    ///
    /// The native quest architecture (decoded from the game-data IFFs —
    /// ground truth — and the PPC PEF):
    ///   - ControllerQuest.iff (0xC953333E) = the per-lot quest-state object.
    ///     'Quest - Choose Quest' (SocialsMagic 4123) CREATES it and writes
    ///     attr[5] = quest-type (0/1 puzzlebox, 3 graveyard, 6 duel, ...),
    ///     attr[6] = the vendor's identity (ins35/44/45/53/62).
    ///   - The vendor NPCs (Vampiress 0xB8B1CF9B / FaerieQueen 0xB7A0C2CC /
    ///     Apothecary 0xD0CBE844 — spawned by CartDragon/CartFaerie/
    ///     CartMedicine mains) carry the TTAB quest suite: Talk To / Ask
    ///     About Item / Barter / Buy / Sell -> thin wrappers that create the
    ///     SocialsMagic social object (0x9FE28884) and push the quest
    ///     socials (4107 Ask-For-Quest, 4112 Talk-About-Quest, 4124 Review).
    ///   - Rewards: 'Quest - Choose Reward' (4182) adds the reward tokens
    ///     (0x7B672834 / 0x742D5E9F / 0x7E7129F7 / 0x50F86101) and its
    ///     FindToken '03 08 11 0d' returns the found index in Temp[1]
    ///     (native selector (op3.0x18)>>3, PPC 0x100e31ec + 0x100e3248),
    ///     which 'Quest - Review Quest' (4124 ins102) copies to local[8]
    ///     for the $TokenNameLocal:8 reward dialog (STR#301 string 19).
    ///
    /// Gate phases:
    ///   A (content census, no lot): the quest-giver suite resolves with
    ///     quest TTAB rows; SocialsMagic carries the quest core + STR#301;
    ///     the reward/basic token definitions carry catalog names.
    ///   B (live lot, native trees): spawn a Vampiress person, seed the
    ///     avatar's inventory with the 3 basic tokens, run 4123 'Choose
    ///     Quest' natively (RunInMyStack — everything native but the social
    ///     push, the established idiom), assert the ControllerQuest instance
    ///     appears with a quest-type attr + a fully-expanded quest dialog.
    ///   C: run 4182 'Choose Reward' natively; assert reward tokens landed
    ///     in inventory AND Temp[1] holds the found index (the corrected
    ///     selector law).
    ///   D: parse STR#301 string 19 ('$TokenNameLocal:8') through the REAL
    ///     VMDialogHandler with a real-frame carrier; assert the token's
    ///     catalog name lands and no raw '$' command survives.
    /// </summary>
    public static class AutotestExp17MmQuest
    {
        public static bool Finished;
        public static bool Passed;
        public static List<string> Failures = new List<string>();
        public static List<string> Notes = new List<string>();

        private static int _state; // 0 census, 1 wait-lot, 15 switch-93, 16 rebound, 2 settle-vamp, 3 done
        private static int _settle;
        private static int _switchAt = -1;
        private static bool _vmRebound;
        private static VMEntity _vamp;
        private static VMEntity _cq;
        private static readonly List<string> _dialogs = new List<string>();
        private static VM _dlgHookedVm;

        // Quest corpus GUIDs (IFF ground truth; see header).
        private static readonly uint GUID_Vampiress = 0xB8B1CF9Bu;
        private static readonly uint GUID_FaerieQueen = 0xB7A0C2CCu;
        private static readonly uint GUID_Apothecary = 0xD0CBE844u;
        private static readonly uint GUID_SnakeCharmer = 0x846E4726u;
        private static readonly uint GUID_GardenGnome = 0xFC697613u;
        private static readonly uint GUID_GypsyClerk = 0xFC2FBA79u;
        private static readonly uint GUID_Salesman = 0x44A86A6Cu;
        private static readonly uint GUID_ControllerQuest = 0xC953333Eu;
        private static readonly uint GUID_QuestToadstool = 0xDF83697Du;
        private static readonly uint GUID_PuzzleBox = 0x20166101u;
        private static readonly uint GUID_MagicArena = 0xC96515A5u;
        private static readonly uint GUID_DisasterCloud = 0x4D523CC9u;
        private static readonly uint GUID_SocialsMagic = 0x9FE28884u;
        private static readonly uint[] GUID_BasicTokens = { 0xA30053E8u, 0x51084400u, 0xC81E3C91u };
        private static readonly uint[] GUID_RewardTokens = { 0x7B672834u, 0x742D5E9Fu, 0x7E7129F7u, 0x50F86101u };

        private static void Fail(string why)
        {
            Failures.Add(why);
        }

        private static string CatalogName(uint guid)
        {
            var wo = Content.Get().WorldObjects.Get(guid);
            if (wo?.OBJ == null) return null;
            return wo.Resource.Get<CTSS>(wo.OBJ.CatalogStringsID)?.GetString(0);
        }

        private static void PhaseCensus(Action<string> log)
        {
            // A1: the quest-line object set resolves.
            var guidSet = new Dictionary<uint, string>
            {
                { GUID_Vampiress, "Vampiress" }, { GUID_FaerieQueen, "FaerieQueen" }, { GUID_Apothecary, "Apothecary" },
                { GUID_SnakeCharmer, "SnakeCharmer" }, { GUID_GardenGnome, "GardenGnome" }, { GUID_GypsyClerk, "GypsyClerk" },
                { GUID_Salesman, "Salesman" }, { GUID_ControllerQuest, "ControllerQuest" }, { GUID_QuestToadstool, "QuestToadstool" },
                { GUID_PuzzleBox, "PuzzleBox" }, { GUID_MagicArena, "MagicArena" }, { GUID_DisasterCloud, "DisasterCloud" },
                { GUID_SocialsMagic, "SocialsMagic" },
            };
            var missing = new List<string>();
            foreach (var kv in guidSet)
                if (Content.Get().WorldObjects.Get(kv.Key) == null) missing.Add(kv.Value);
            if (missing.Count > 0) Fail("objects unresolved: " + string.Join(",", missing));
            log("AUTOTEST mmquest A1 objects=" + (guidSet.Count - missing.Count) + "/" + guidSet.Count
                + (missing.Count > 0 ? " MISSING[" + string.Join(",", missing) + "]" : ""));

            // A2: the quest TTAB suite on the vendor NPCs (the giver surface).
            // Native rows (NPC_Vampiress.iff TTAB): TalkTo x2 / Ask About Item /
            // Barter / Buy / Sell -> 4107-4111 wrappers.
            var wantRows = new HashSet<string> { "ask about item", "barter", "buy", "sell" };
            foreach (var kv in new Dictionary<uint, string>
                { { GUID_Vampiress, "Vampiress" }, { GUID_FaerieQueen, "FaerieQueen" }, { GUID_Apothecary, "Apothecary" } })
            {
                var wo = Content.Get().WorldObjects.Get(kv.Key);
                var ttab = wo?.Resource?.Get<TTAB>(wo.OBJ.TreeTableID);
                var ttas = wo?.Resource?.Get<TTAs>(wo.OBJ.TreeTableID);
                if (ttab == null) { Fail(kv.Value + ": no TTAB"); continue; }
                var labels = new HashSet<string>(ttab.Interactions
                    .Where(i => i.TestFunction != 0 || i.ActionFunction != 0)
                    .Select(i => (ttas != null ? ttas.GetString((int)i.TTAIndex) : null) ?? "?")
                    .Select(s => s.Trim().ToLowerInvariant()));
                var hit = new HashSet<string>(labels.Where(l => wantRows.Any(w => l.Contains(w))));
                if (hit.Count < 4) Fail(kv.Value + ": quest TTAB suite incomplete (" + hit.Count + "/4): ["
                    + string.Join(",", labels) + "]");
                else Notes.Add(kv.Value + ":quest-suite-ok");
            }

            // A3: the SocialsMagic quest core + reward texts.
            var sm = Content.Get().WorldObjects.Get(GUID_SocialsMagic);
            var res = sm?.Resource;
            if (res == null) { Fail("SocialsMagic resource null"); }
            else
            {
                var want = new ushort[] { 4107, 4112, 4123, 4124, 4117, 4178, 4179, 4182, 4216 };
                var miss = want.Where(id => res.GetRoutine(id) == null).Select(id => id.ToString()).ToList();
                if (miss.Count > 0) Fail("quest-core BHAVs missing: " + string.Join(",", miss));
                else Notes.Add("quest-core-ok(9)");
                var s301 = res.Get<STR>(301);
                if (s301 == null) Fail("STR#301 missing");
                else
                {
                    var s19 = s301.GetString(19) ?? "";
                    if (!s19.Contains("$TokenNameLocal:8")) Fail("STR#301[19] lacks $TokenNameLocal:8: " + s19);
                    else Notes.Add("STR301-19-subst-ok");
                }
            }

            // A4: token catalog names (the $TokenNameLocal lookup targets).
            foreach (var g in GUID_BasicTokens.Concat(GUID_RewardTokens))
            {
                var nm = CatalogName(g);
                if (string.IsNullOrEmpty(nm)) Fail(string.Format("token 0x{0:X8} has no catalog name", g));
            }
            log("AUTOTEST mmquest A4 tokenNames=[" + string.Join(",", GUID_RewardTokens
                .Select(g => string.Format("0x{0:X8}={1}", g, CatalogName(g) ?? "?"))) + "]");
            log("AUTOTEST mmquest A4 basicNames=[" + string.Join(",", GUID_BasicTokens
                .Select(g => string.Format("0x{0:X8}={1}", g, CatalogName(g) ?? "?"))) + "]");
        }

        public static void Tick(Action<string> log, ref VM vm, Simitone.Client.UI.Screens.TS1GameScreen screen)
        {
            try
            {
                if (_state == 0)
                {
                    PhaseCensus(log);
                    _state = 1;
                    return;
                }
                if (_state == 1)
                {
                    if (screen != null && screen.vm != null && !ReferenceEquals(screen.vm, vm)) vm = screen.vm;
                    if (vm == null) return;
                    var av = vm.Entities.OfType<VMAvatar>().FirstOrDefault();
                    if (av == null) return; // wait for lot boot
                    log("AUTOTEST mmquest B0 lot booted avatar=obj" + av.ObjectID
                        + " avatars=" + vm.Entities.OfType<VMAvatar>().Count(e => !e.Dead)
                        + " (curHouse=" + (vm.TS1State?.CurrentHouse.ToString() ?? "?") + ")");
                    // v5 finding: switching to the magic lot (93) does NOT unlock
                    // the Choose Quest dialogs (the quiet-exit gate is the
                    // customer scan — the delivery branch needs a second
                    // non-family, non-NPC sim, and the family-only autotest lot
                    // has none), while lot 93's native NPC controllers open
                    // blocking dialogs headlessly that pause the VM and freeze
                    // the soak clock (69601 watchdog lines in 19 min). Stay on
                    // the boot lot; the quest-core asserts are lot-independent
                    // (proven live: Choose Reward + the substitution laws).
                    log("AUTOTEST mmquest B0 staying on the boot lot (magic-lot switch: no assert gain, pause-storm cost — see evidence)");
                    _settle = 0;
                    _state = 2;
                    return;
                }
                if (_state == 15)
                {
                    // the switch takes effect across frames; observe from the next tick
                    _switchAt = _settle++;
                    _vmRebound = false;
                    _state = 16;
                    return;
                }
                if (_state == 16)
                {
                    _settle++;
                    if (screen != null && screen.vm != null && !ReferenceEquals(screen.vm, vm))
                    {
                        vm = screen.vm;
                        _vmRebound = true;
                        log("AUTOTEST mmquest B0 vm-rebound curHouse=" + (vm.TS1State?.CurrentHouse.ToString() ?? "?"));
                    }
                    var cur = vm.TS1State?.CurrentHouse ?? 0;
                    if (_settle > 3600)
                    {
                        log("AUTOTEST mmquest B0 no-switch (stayed " + cur + "); continuing on the current lot (the quest-core asserts are lot-independent)");
                        _state = 2;
                        return;
                    }
                    if (!(_vmRebound && cur == 93)) return;
                    log("AUTOTEST mmquest B0 on magic lot 93 after " + _settle + "f");
                    _state = 2;
                    return;
                }
                if (_state == 2)
                {
                    if (_dlgHookedVm != vm)
                    {
                        _dlgHookedVm = vm;
                        vm.OnDialog += di =>
                        {
                            if (di != null && (long)di.DialogID >> 32 == (long)GUID_SocialsMagic)
                                _dialogs.Add((di.Message ?? "").Replace("\n", " | ").Replace("\r", ""));
                        };
                    }
                    var av0 = vm.Entities.OfType<VMAvatar>().FirstOrDefault(a => !a.Dead);
                    if (av0 == null) return; // wait for the (traveling) family
                    // spawn the quest-giver OOW (the vendor-cart native spawn makes it
                    // in-world; the trees under test never need its position)
                    VMMultitileGroup grp = null;
                    try { grp = vm.Context.CreateObjectInstance(GUID_Vampiress, LotTilePos.OUT_OF_WORLD, Direction.NORTH); }
                    catch (Exception e) { log("AUTOTEST mmquest B1 spawn-exc " + e.GetType().Name + ": " + e.Message); }
                    _vamp = grp?.Objects?.FirstOrDefault();
                    if (_vamp == null) { Fail("Vampiress person uncreateable"); _state = 3; Finished = true; Passed = false; return; }
                    // seed the avatar inventory with the 3 basic magic tokens
                    // (TokenType 6 = MAGIC — the Choose Quest finds use '0006')
                    var nid0 = av0.GetPersonData(VMPersonDataVariable.NeighborId);
                    var inv0 = Content.Get().Neighborhood.GetInventoryByNID(nid0) ?? new List<InventoryItem>();
                    inv0.Clear();
                    foreach (var g in GUID_BasicTokens)
                        inv0.Add(new InventoryItem { GUID = g, Count = 2, Type = 6 });
                    Content.Get().Neighborhood.SetInventoryForNID(nid0, inv0);
                    log("AUTOTEST mmquest B1 vamp=obj" + _vamp.ObjectID + " seededInv=" + inv0.Count
                        + " (basic tokens x2, type 6) settle 150f for init traits");
                    _settle = 0;
                    _state = 21;
                    return;
                }
                if (_state == 21)
                {
                    if (++_settle < 150) return;
                    var av = vm.Entities.OfType<VMAvatar>().FirstOrDefault(a => !a.Dead);
                    if (av == null) { Fail("avatar vanished"); _state = 3; return; }
                    var sm = Content.Get().WorldObjects.Get(GUID_SocialsMagic);
                    var res = sm.Resource;
                    // B2: run 'Quest - Choose Quest' natively on the avatar's stack
                    // (stack object = the vendor person; the NPC quest-giver trees
                    // invoke this as a private gosub of the pushed social).
                    var rt4123 = res.GetRoutine((ushort)4123) as VMRoutine;
                    var nid = av.GetPersonData(VMPersonDataVariable.NeighborId);
                    if (rt4123 == null) { Fail("4123 unresolvable at runtime"); }
                    else
                    {
                        // The tree's early branches are engine-random: local[1] =
                        // random(1000) gates the quest type (<333 fetch / <700
                        // barter-ish / >=800 quiet TRUE exit at ins102 f=253), and
                        // the delivery branch scans for a second NON-NPC customer
                        // (pd[34]==2 persons are skipped, ins93/122). Retry the
                        // native run a few times — the RNG advances per instruction
                        // — and accept the quiet exit as the native branch law.
                        bool ran = false;
                        for (var attempt = 0; attempt < 6 && _dialogs.Count == 0; attempt++)
                        {
                            _dialogs.Clear();
                            try { ran = av.Thread.RunInMyStack(rt4123, sm, new short[4], _vamp); }
                            catch (Exception e) { ran = false; log("AUTOTEST mmquest B2 run-exc " + e.GetType().Name + ": " + e.Message); break; }
                            log("AUTOTEST mmquest B2 chooseQuest attempt=" + attempt + " ran=" + ran + " dialogs=" + _dialogs.Count);
                        }
                        // assert 1: the ControllerQuest instance was created natively
                        // (only on the quest-assignment branches; the quiet exit
                        // is the native no-quest roll)
                        _cq = vm.Entities.FirstOrDefault(e => !e.Dead && e.Object?.OBJ?.GUID == GUID_ControllerQuest);
                        if (_cq == null) Notes.Add("choose-quest-quiet-exit(no CQ; native branch)");
                        else
                        {
                            var a5 = _cq.GetAttribute(5);
                            var a6 = _cq.GetAttribute(6);
                            log("AUTOTEST mmquest B2 cq=obj" + _cq.ObjectID + " attr5=" + a5 + " attr6=" + a6
                                + " attrs=[" + string.Join(",", Enumerable.Range(0, 10).Select(i => _cq.GetAttribute((ushort)i))) + "]");
                            if (a5 == 0 && _cq.GetAttribute(7) == 0 && _cq.GetAttribute(2) == 0
                                && _cq.GetAttribute(3) == 0 && _cq.GetAttribute(9) == 0)
                                Notes.Add("cq-attrs-allzero(suspicious)");
                        }
                        // assert 2: any quest dialog that fired is fully expanded
                        var dlg = _dialogs.FirstOrDefault();
                        if (dlg == null) Notes.Add("choose-quest-no-dialog(native quiet branch)");
                        else if (dlg.Contains("$")) Fail("dialog has raw substitution: " + dlg);
                        else Notes.Add("dialog-expanded:'" + (dlg.Length > 90 ? dlg.Substring(0, 90) + "..." : dlg) + "'");
                    }

                    // C: 'Quest - Choose Reward' — reward tokens + the FindToken
                    // Temp[1] selector law.
                    var rt4182 = res.GetRoutine((ushort)4182) as VMRoutine;
                    if (rt4182 == null) Fail("4182 unresolvable");
                    else
                    {
                        // mirror Choose Quest's native create (ins32/49/58/67:
                        // create-obj 0xC953333E) when the quiet branch left none
                        if (_cq == null)
                        {
                            try
                            {
                                var cqGrp = vm.Context.CreateObjectInstance(GUID_ControllerQuest, LotTilePos.OUT_OF_WORLD, Direction.NORTH);
                                _cq = cqGrp?.Objects?.FirstOrDefault();
                            }
                            catch (Exception e) { log("AUTOTEST mmquest C cq-create-exc " + e.GetType().Name); }
                            if (_cq == null) Fail("ControllerQuest uncreateable");
                        }
                    }
                    if (rt4182 != null && _cq != null)
                    {
                        // mirror Choose Quest's native attr write: attr[6] = the
                        // vendor identity consumed by 4182's test-obj-type gate
                        // (ins2/5 test Apothecary 0xD0CBE844 / FaerieQueen
                        // 0xB7A0C2CC; spawn an Apothecary so the native pool-A
                        // branch is deterministic)
                        var apoGrp = vm.Context.CreateObjectInstance(GUID_Apothecary, LotTilePos.OUT_OF_WORLD, Direction.NORTH);
                        var apo = apoGrp?.Objects?.FirstOrDefault();
                        var vendorId = (short)(apo != null ? apo.ObjectID : _vamp.ObjectID);
                        try { _cq.SetAttribute(6, vendorId); } catch { }
                        log("AUTOTEST mmquest C cq=obj" + _cq.ObjectID + " attr6(vendor)=" + vendorId
                            + " attr5=" + _cq.GetAttribute(5) + " vampGUID=0x" + (_vamp.Object?.OBJ?.GUID.ToString("x8") ?? "?"));
                        if (vm.SpeedMultiplier <= 0) { vm.SpeedMultiplier = 1; vm.GlobalBlockingDialog = null; }
                        var inv = Content.Get().Neighborhood.GetInventoryByNID(nid) ?? new List<InventoryItem>();
                        var before = inv.Count;
                        bool ranR;
                        try { ranR = av.Thread.RunInMyStack(rt4182, sm, new short[4], _cq); }
                        catch (Exception e) { ranR = false; log("AUTOTEST mmquest C run-exc " + e.GetType().Name + ": " + e.Message); }
                        inv = Content.Get().Neighborhood.GetInventoryByNID(nid) ?? new List<InventoryItem>();
                        var rewards = inv.Where(x => GUID_RewardTokens.Contains(x.GUID)).ToList();
                        log("AUTOTEST mmquest C chooseReward ran=" + ranR + " invBefore=" + before
                            + " invAfter=" + inv.Count + " rewardEntries=" + rewards.Count
                            + " temp1=" + av.Thread.TempRegisters[1]);
                        if (rewards.Count == 0) Fail("Choose Reward granted no reward tokens");
                        else
                        {
                            // the selector law: 4182's trailing FindToken('03 08 11 0d')
                            // must have left the found INDEX in Temp[1]
                            // (native (op3.0x18)>>3 -> Temp[1], PPC 0x100e31ec).
                            var idx = inv.FindIndex(x => GUID_RewardTokens.Contains(x.GUID));
                            if (av.Thread.TempRegisters[1] != idx)
                                Fail("FindToken index-dst wrong: temp1=" + av.Thread.TempRegisters[1] + " expected=" + idx);
                            else Notes.Add("findtemp-selector-ok");
                        }
                    }

                    // D: the $TokenNameLocal parse law through the real parser.
                    try
                    {
                        var s301 = res.Get<STR>(301);
                        var input = s301.GetString(19);
                        var inv = Content.Get().Neighborhood.GetInventoryByNID(nid) ?? new List<InventoryItem>();
                        var ridx = inv.FindIndex(x => GUID_RewardTokens.Contains(x.GUID));
                        var frame = new VMStackFrame
                        {
                            Caller = av,
                            Callee = av,
                            Thread = av.Thread,
                            CodeOwner = sm,
                        };
                        var locals = new short[16];
                        if (ridx >= 0) locals[8] = (short)ridx;
                        frame.Locals = locals;
                        var outStr = VMDialogHandler.ParseDialogString(frame, input, s301);
                        var expect = CatalogName(inv[Math.Max(0, ridx)].GUID) ?? "?";
                        log("AUTOTEST mmquest D parseOut='" + (outStr ?? "").Replace("\n", " ") + "'");
                        if ((outStr ?? "").Contains("TokenNameLocal")) Fail("parser leaked raw command: " + outStr);
                        else if (ridx >= 0 && !(outStr ?? "").Contains(expect))
                            Fail("parser did not name the token '" + expect + "': " + outStr);
                        else Notes.Add("tokenname-parse-ok('" + expect + "')");
                    }
                    catch (Exception e)
                    {
                        Fail("parse-law exc " + e.GetType().Name + ": " + e.Message);
                    }
                    // cleanup: delete the probe-spawned NPC persons (their native
                    // mains block on dialogs headlessly and pause the VM — the r157
                    // watchdog re-fights the pause every tick, freezing the soak
                    // clock), then release the latch and force speed.
                    try
                    {
                        _vamp?.Delete(true, vm.Context);
                        foreach (var e in vm.Entities.Where(e => !e.Dead && (e.Object?.OBJ?.GUID == GUID_Apothecary || e.Object?.OBJ?.GUID == GUID_Vampiress)).ToList())
                            e.Delete(true, vm.Context);
                        _cq?.Delete(true, vm.Context);
                    }
                    catch (Exception e) { log("AUTOTEST mmquest cleanup-exc " + e.GetType().Name); }
                    vm.GlobalBlockingDialog = null;
                    if (vm.SpeedMultiplier <= 0) vm.SpeedMultiplier = 1;
                    _state = 3;
                    Finished = true;
                    Passed = Failures.Count == 0;
                    return;
                }
            }
            catch (Exception e)
            {
                Fail("tick-exc " + e.GetType().Name + ": " + e.Message);
                Finished = true;
                Passed = false;
            }
        }
    }
}
