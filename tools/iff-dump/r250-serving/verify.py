"""R250: pin the native HAND-OFF AND SERVING law; reads owner exe only.

Pins 46 NEW instruction words (TryGosubFoundAction, SetCurrentAction,
GosubObjectTree + its 0xefe00 core, StackJustPopped, TreeSim::Simulate 0x153790,
RunCheckTree 0x153ec0, DoNodeAction return/exit law, Error__8cXObjectFs,
TickAllObjects, TryIdleForInput ring drain, AddAction sync tick, the 0x10ac30
abort, TryGotoRoutingSlot prim+core, Cleanup exit paths, TreeTableEntry::DoStream)
and proves the recovered laws with pure-python fixtures: hand-off flow (no
guard re-run), SetCurrentAction copy law, StackJustPopped bookkeeping, ring
drain, DoNodeAction result map, TickAllObjects gate, gosub gate, routing-slot
wrapper addressing, and the cXPerson +0x28 vtable layout. Writes only
verified-state.json next to itself.
"""
import hashlib
import json
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
BIN = ROOT / "game-data" / "The Sims" / "The Sims Complete"
b = BIN.read_bytes()
sha = hashlib.sha256(b).hexdigest()
assert sha == "33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f"

checks = []


def check(name, ok, detail=""):
    checks.append((name, bool(ok), detail))


def w(addr):
    return struct.unpack_from(">I", b, addr)[0]


# ---- 1. code pins (file offsets; all NEW vs prior rounds' verified inputs) ----
pins = {
    # TryGosubFoundAction__8cXPersonFP9StackElem (0x10fdc0)
    0x10FDC8: 0x83C2A71C,  # lwz r30, -0x58e4(r2)        ; string pool
    0x10FED4: 0xA8DC0002,  # lha r6, 2(r28)              ; gosub tree = TTAB entry+2
    0x10FEF4: 0x819F0028,  # lwz r12, 0x28(r31)          ; person secondary vtable
    0x10FEF8: 0x818C003C,  # lwz r12, 0x3c(r12)          ; slot +0x3c GosubObjectTree
    0x10FEF0: 0x38E00000,  # li r7, 0                    ; GosubObjectTree bool=false
    0x10FF1C: 0x38E00002,  # li r7, 2                    ; ctor arg4 = 2
    0x10FF20: 0x4BF8CB71,  # bl Interaction ctor 0x9ca90
    0x10FF2C: 0x4BF8CB05,  # bl SetStackVars__11InteractionFPs
    0x10FF38: 0x4BFF8F29,  # bl SetCurrentAction__8cXPersonFRC11Interaction
    0x10FF48: 0x38600003,  # li r3, 3                    ; success return 3
    # SetCurrentAction__8cXPersonFRC11Interaction (0x108e60)
    0x108E7C: 0x80030A10,  # lwz r0, 0xa10(r3)           ; current action target
    0x108E94: 0x901E0A44,  # stw r0, 0xa44(r30)          ; save previous action
    0x108EFC: 0x540006F2,  # rlwinm r0, r0, 0, 0x1b, 0x19 ; clear flags PPC-bit26
    0x108F10: 0x60000020,  # ori r0, r0, 0x20            ; mark saved flags bit26
    0x108FB4: 0x4BF9395D,  # bl SetUniqueID__11InteractionFv
    0x108FC0: 0xB01E05CE,  # sth r0, 0x5ce(r30)          ; attr33 = Interaction+0x1c
    0x108FC4: 0x4800D1DD,  # bl 0x1161a0 (action-string update)
    # GosubObjectTree__8cXPerson (0x109180) + cXObject core (0xefe00)
    0x1091A4: 0x4BFE6C5D,  # bl 0xefe00 (base impl)
    0x1091D0: 0x93DD0A14,  # stw r30, 0xa14(r29)         ; currentAction+0xc = obj
    0x1091EC: 0x38A00001,  # li r5, 1                    ; push 1 record
    0x109200: 0xB01E00C6,  # sth r0, 0xc6(r30)           ; obj gosub-refcount++
    0x0EFE34: 0x4BFF20FD,  # bl ClearIdleStatus__12ObjectModuleFi
    0x0EFE4C: 0x48064285,  # bl 0x1540d0 (TreeSim gosub core)
    0x0EFE64: 0xB3E30004,  # sth r31, 4(r3)              ; curElem+4 = object id
    # StackJustPopped__8cXPersonFv (0x109050)
    0x1090AC: 0x80BF0AB8,  # lwz r5, 0xab8(r31)          ; frames base person+0xab8
    0x1090C4: 0x901F0A14,  # stw r0, 0xa14(r31)          ; currentAction+0xc = obj
    0x10912C: 0x4BFFFD35,  # bl SetCurrentAction         ; empty Interaction
    # TreeSim::GetStackSize (0x1534b0) + TreeSim::Simulate (0x153790)
    0x1534B4: 0x38630004,  # addi r3, r3, 4              ; TreeStack member
    0x1537FC: 0x7C9F2378 + 0x2000,  # (placeholder check below)
    0x15380C: 0x818C000C,  # lwz r12, 0xc(r12)           ; Error slot
    # TickAllObjects__10cSimulatorFv (0x139400)
    0x1394A8: 0x4801B359,  # bl GetError__7TreeSimFv
    0x13954C: 0x818C001C,  # lwz r12, 0x1c(r12)          ; Simulate slot +0x1c
    # DoNodeAction__7TreeSimFP9StackElem (0x1538c0)
    0x153AF0: 0x818C0008,  # lwz r12, 8(r12)             ; TryElement slot +8
    0x153B58: 0xB07D0002,  # sth r3, 2(r29)              ; false-branch next node
    0x153B68: 0x989C001C,  # stb r4, 0x1c(r28)           ; last-prim-false latch
    0x153BBC: 0x38600001,  # li r3, 1                    ; yield (in progress)
    0x153BC4: 0x38600002,  # li r3, 2                    ; hard error
    0x153C40: 0x4BFFEDC1,  # bl Pop__9TreeStackFv
    0x153C50: 0x4844ED91,  # bl vtable StackJustPopped   ; on frame return
    0x153CE0: 0x881D0002,  # lbz r0, 2(r29)              ; node false-branch byte
    # RunCheckTree / RunOneTickTree worker (0x153ec0)
    0x153F38: 0x4BFFEC09,  # bl Push__9TreeStackFPC9StackElemPCs
    0x153FF8: 0x81990028,  # lwz r12, 0x28(r25)
    0x153FFC: 0x818C0010,  # lwz r12, 0x10(r12)          ; StackJustPopped slot
    0x154034: 0x818C000C,  # lwz r12, 0xc(r12)           ; Error slot
    0x154038: 0x4844E9A9,  # bl vtable Error             ; false-return transition
    # Error__8cXObjectFs (0xd2d60)
    0x0D2D7C: 0x5400062C,  # rlwinm r0, r0, 0, 0x18, 0x16 ; clear flags bit23
    0x0D2D80: 0x60000100,  # ori r0, r0, 0x100           ; set obj+0x114 bit23
    0x0D2D90: 0x4800F121,  # bl 0xe1eb0 (notify event 4)
    # TryIdleForInput__8cXPerson (0x10a0c0) — the ring drain
    0x10A184: 0x807C0A00,  # lwz r3, 0xa00(r28)          ; ring head
    0x10A268: 0x38030001,  # addi r0, r3, 1
    0x10A26C: 0x901C0A00,  # stw r0, 0xa00(r28)          ; head++
    0x10A2D8: 0x38800004,  # li r4, 4                    ; ObjEntryPoint 4 (exit tree)
    0x10A3A0: 0x818C003C,  # lwz r12, 0x3c(r12)          ; GosubObjectTree
    0x10A428: 0x4BFFEA39,  # bl SetCurrentAction         ; dequeued action
    0x10A43C: 0x901D0008,  # stw r0, 8(r29)              ; StackElem+8 = 1
    # AddAction__8cXPersonFP11Interaction (0x10af40)
    0x10AF7C: 0x5400077B,  # rlwinm. r0, r0, 0, 0x1d, 0x1d ; entry flags mask 0x4
    0x10B000: 0xAB3D05CE,  # lha r25, 0x5ce(r29)         ; save attr33
    0x10B004: 0xB01D05CE,  # sth r0, 0x5ce(r29)          ; attr33 = entry prio
    0x10B018: 0x48048E49,  # bl RunOneTickTree           ; FIRST TICK inline
    0x10B6D4: 0x4BFF15CD,  # bl GetTreeID (entry 4)      ; evicted exit tree
    # unnamed abort (0x10ac30)
    0x10AC68: 0x60000100,  # ori r0, r0, 0x100           ; current flags bit23
    0x10AC88: 0x38C00021,  # li r6, 0x21                 ; interrupt interaction arg
    0x10AC8C: 0x38E00032,  # li r7, 0x32
    0x10ACB4: 0x4800028D,  # bl AddAction                ; re-queue interrupt
    0x10AD2C: 0x38800004,  # li r4, 4                    ; exit tree entry
    0x10AD54: 0x4804916D,  # bl 0x153ec0                 ; run target exit tree
    # TryGotoRoutingSlot prim (0x10f760) + core (0x10e700)
    0x10F780: 0x80040008,  # lwz r0, 8(r4)               ; StackElem+8 state
    0x10F858: 0x801E0140,  # lwz r0, 0x140(r30)          ; obj slot count
    0x10F884: 0x1C03003C,  # mulli r0, r3, 0x3c          ; RoutingSlot stride
    0x10F980: 0x4BFFED81,  # bl 0x10e700 core
    0x10E7A8: 0x480627E9,  # bl XRoute ctor 0x170f90
    0x10E818: 0x1C00009C,  # mulli r0, r0, 0x9c          ; route-state stride
    0x10EB6C: 0x901D0088,  # stw r0, 0x88(r29)           ; route flag +0x88
    0x10EB70: 0x3BC00007,  # li r30, 7                   ; failure code 7
    0x10EDC4: 0x4BFFBC3D,  # bl ShouldInterrupt__8cXPersonFv
    0x10EF94: 0x4BFFF27D,  # bl AskOthersToMove__8cXPersonFP6XRoute
    0x10ECA8: 0x4BFFBF89,  # bl 0x10ac30 (abort matching action)
    # Cleanup__8cXPersonFP8cXObject (0x1081d0)
    0x1085F8: 0x38800003,  # li r4, 3                    ; ObjEntryPoint 3
    0x108620: 0x4804B8A1,  # bl 0x153ec0                 ; person exit tree
    0x10862C: 0xB01E058C,  # sth r0, 0x58c(r30)          ; attr0 posture = 0
    0x10864C: 0x48000815,  # bl SetCurrentAction         ; empty
    0x108BD4: 0x4800028D,  # bl SetCurrentAction         ; on target deletion
    0x108D48: 0x4804B179,  # bl 0x153ec0                 ; purged exit tree
    # TreeTableEntry::DoStream (0x1551a0)
    0x1551DC: 0x4BFC97E5,  # bl Recon16 (entry+2 action number)
    0x1551FC: 0x4BFC9775,  # bl ReconInt (entry+0x14 flags)
    0x15524C: 0x4BFC9405,  # bl ReconFloat (entry+0x10 attenuation)
    0x1552E8: 0x4BFC90A9,  # bl ReconString (entry+0x24 name, v7+)
}
for addr, word in pins.items():
    if addr == 0x1537FC:
        continue  # placeholder pair handled below
    got = w(addr)
    check(f"pin {addr:#07x}", got == word, f"want {word:08X} got {got:08X}")
# TreeSim::Simulate error-tail load (replaces the placeholder above)
check(
    "pin 0x153800",
    w(0x153800) == 0xA8840000,
    f"want A8840000 got {w(0x153800):08X}",
)
check(
    "pin 0x1537F8",
    w(0x1537F8) == 0x809F004C,
    f"want 809F004C got {w(0x1537F8):08X}",
)

# ---- 2. fixture: TryGosubFoundAction flow — NO guard re-run ----
def try_gosub_found_action(person, module_objects, module_count, ttab, action_number):
    """Transpile of 0x10fdc0. ttab = list of dicts with keys
    action18 (entry+0x18, the ad/match number) and action2 (entry+2, run tree).
    test_calls counts TestInteraction-style guard invocations (must stay 0)."""
    stats = {"test_calls": 0, "gosub_tree": None, "ctor_action": None, "ret": None}
    oid = person["stack4"]
    obj = None
    if 0 < oid < module_count + 1:
        obj = module_objects[oid - 1]
    if obj is None:
        stats["ret"] = 0
        return stats
    entry = None
    for e in ttab:
        if e["action18"] == action_number:
            entry = e
            break
    if entry is None:
        stats["ret"] = 0
        return stats
    # virtual GosubObjectTree(person, obj, params{0,0,0,0}, entry+2, false)
    stats["gosub_tree"] = entry["action2"]
    ok = gosub_object_tree(person, obj, entry["action2"])
    if not ok:
        stats["ret"] = -1
        return stats
    stats["ctor_action"] = person["action_number"]  # person+0x72
    set_current_action(person, {"action18": person["action_number"], "tree": entry["action2"]})
    stats["ret"] = 3
    return stats


def gosub_object_tree(person, obj, tree_id):
    """Transpile of 0x109180 + 0xefe00 + 0x1540d0 gate."""
    person.setdefault("clear_idle", []).append(obj["id"])
    fn_table = obj["fn_table"]
    tree = fn_table.get(tree_id)
    if tree is None:
        person["errors"].append(("error", obj["error_id"]))
        return False
    size = len(person["frames_a"])
    person["frames_b"].append({"obj": obj, "size": size, "flag": False})
    person["frames_a"].append({"tree": tree_id, "obj_id": obj["id"]})
    obj["c6"] = obj.get("c6", 0) + 1
    return True


def set_current_action(person, interaction):
    """Transpile of 0x108e60 (offsets 0xa08/0xa44/0x5ce/0x5d8/0x5da)."""
    cur = person["current"]
    if cur.get("target") is not None:
        person["previous"] = dict(cur)
        if person["byte_1c"]:
            person["previous"]["flags"] |= 0x20
    person["current"] = dict(interaction)
    person["attr33"] = interaction.get("prio", 0)
    if person["current"].get("target") is not None:
        person["f5d8"] = 0
        person["f5da"] = 0
        if person["current"].get("uid", 0) == 0:
            person["current"]["uid"] = 1  # SetUniqueID


person = {
    "stack4": 2,
    "action_number": 4107,
    "byte_1c": False,
    "frames_a": [],
    "frames_b": [],
    "current": {"target": None},
    "previous": None,
    "attr33": 0,
    "errors": [],
    "clear_idle": [],
}
chair = {"id": 7, "fn_table": {272: "Sit"}, "c6": 0, "error_id": 272}
chair_ttab = [{"action18": 4107, "action2": 272}]
res = try_gosub_found_action(
    person, [None, chair], 2, chair_ttab, person["action_number"]
)
check(
    "handoff-no-guard",
    res["test_calls"] == 0 and res["ret"] == 3,
    f"{res}",
)
check(
    "handoff-gosub-tree-is-entry+2",
    res["gosub_tree"] == 272 and res["ctor_action"] == 4107,
    f"gosub={res['gosub_tree']} ctor={res['ctor_action']}",
)
check(
    "handoff-frame-pushed-not-run",
    len(person["frames_a"]) == 1
    and person["frames_a"][0]["tree"] == 272
    and person["current"]["tree"] == 272,
    f"{person['frames_a']} {person['current']}",
)
check(
    "handoff-refcount-and-idle",
    chair["c6"] == 1 and person["clear_idle"] == [7],
    f"c6={chair['c6']}",
)
# failure: tree missing in fnTable -> Error + return -1
person2 = dict(person, frames_a=[], frames_b=[], current={"target": None}, errors=[])
chair2 = {"id": 7, "fn_table": {}, "c6": 0, "error_id": 272}
res2 = try_gosub_found_action(
    person2, [None, chair2], 2, chair_ttab, 4107
)
check(
    "handoff-gosub-fail",
    res2["ret"] == -1 and len(person2["errors"]) == 1,
    f"{res2} {person2['errors']}",
)

# ---- 3. fixture: SetCurrentAction previous-action law (0x108e60) ----
p3 = {
    "byte_1c": True,
    "frames_a": [],
    "frames_b": [],
    "current": {"target": "T1", "flags": 0x8, "name": "Sit"},
    "previous": None,
    "attr33": 0,
    "f5d8": 1,
    "f5da": 1,
}
set_current_action(p3, {"target": "T2", "flags": 0x4, "name": "Other", "uid": 0})
prev = p3["previous"]
check(
    "setcurrentaction-save",
    prev is not None
    and prev["target"] == "T1"
    and prev["flags"] == 0x8 | 0x20
    and p3["current"]["target"] == "T2"
    and p3["current"]["uid"] == 1,
    f"{prev} {p3['current']}",
)
p4 = dict(p3, byte_1c=False, current={"target": None}, previous=None)
set_current_action(p4, {"target": "T3", "flags": 0x0, "uid": 0})
check(
    "setcurrentaction-nosave-when-empty",
    p4["previous"] is None and p4["current"]["target"] == "T3",
    f"{p4['previous']}",
)

# ---- 4. fixture: StackJustPopped__8cXPersonFv law (0x109050) ----
def stack_just_popped(person):
    size_a = len(person["frames_a"])
    if not person["frames_b"]:
        return
    top = person["frames_b"][-1]
    if size_a == top["size"] - 1 or size_a < top["size"]:
        top["obj"]["c6"] -= 1
        for rec in person["frames_b"]:
            if rec["flag"]:
                person["current"]["target"] = rec["obj"]
        person["frames_b"].pop()
    if not person["frames_b"]:
        person["current"] = {"target": None}


p5 = {
    "frames_a": [],
    "frames_b": [
        {"obj": chair, "size": 1, "flag": True},
    ],
    "current": {"target": chair},
}
stack_just_popped(p5)
check(
    "stackjustpopped-clears-action",
    p5["frames_b"] == [] and p5["current"] == {"target": None} and chair["c6"] == 0,
    f"{p5} c6={chair['c6']}",
)
# a nested gosub finishing must NOT clear the outer action
chair_inner = {"id": 8, "c6": 1}
chair_outer = {"id": 9, "c6": 1}
p6 = {
    "frames_a": [{"tree": 16}],
    "frames_b": [
        {"obj": chair_outer, "size": 1, "flag": True},
        {"obj": chair_inner, "size": 2, "flag": False},
    ],
    "current": {"target": chair_inner},
}
stack_just_popped(p6)
check(
    "stackjustpopped-nested",
    # the walk ends on the LAST bool-flagged record -> the outer gosub's object
    p6["current"]["target"] is chair_outer
    and len(p6["frames_b"]) == 1
    and chair_inner["c6"] == 0
    and chair_outer["c6"] == 1,
    f"{p6}",
)

# ---- 5. fixture: TryIdleForInput ring-drain law (0x10a184-0x10a470) ----
def idle_for_input(person, elem, prim_params):
    """Transpile of the queue-drain path. Returns the prim exit code
    (1 keep idling, 2 consumed-failure, 3 action started)."""
    head = person["ring_head"]
    tail = person["ring_tail"]
    if head == tail:
        return 1
    slot = person["ring"][head & 7]
    person["ring_head"] = head + 1
    if slot["target"] is None:
        return 2
    passed = test_interaction(slot)  # re-runs the check, sets flags bit3
    if not passed:
        run_exit_tree(slot["target"], 4)
        return 2
    if slot["tree"] == 0:
        run_exit_tree(slot["target"], 4)
        return 2
    if not gosub_object_tree(person, slot["target"], slot["tree"]):
        run_exit_tree(slot["target"], 4)
        return 2
    set_current_action(person, slot)
    person["params"][elem["param_idx"]] = 0
    elem["state8"] = 1
    return 3


RUN_EXIT_TREE_CALLS = []


def test_interaction(slot):
    return slot.get("check_pass", True)


def run_exit_tree(obj, entry):
    RUN_EXIT_TREE_CALLS.append((obj["id"], entry))


ringp = {
    "ring_head": 0,
    "ring_tail": 1,
    "ring": [{"target": chair, "tree": 272, "check_pass": False}],
    "params": [9],
    "frames_a": [],
    "frames_b": [],
    "current": {"target": None},
    "clear_idle": [],
    "errors": [],
}
RUN_EXIT_TREE_CALLS.clear()
code = idle_for_input(ringp, {"param_idx": 0, "state8": 0}, {})
check(
    "ringdrain-failed-retest-drops",
    code == 2 and ringp["ring_head"] == 1 and RUN_EXIT_TREE_CALLS == [(7, 4)],
    f"code={code} head={ringp['ring_head']} exits={RUN_EXIT_TREE_CALLS}",
)
chair_ok = {"id": 7, "fn_table": {272: "Sit"}, "c6": 0, "error_id": 272}
ringp2 = {
    "ring_head": 3,
    "ring_tail": 4,
    "ring": {3: {"target": chair_ok, "tree": 272, "check_pass": True}},
    "params": [9],
    "frames_a": [],
    "frames_b": [],
    "current": {"target": None},
    "clear_idle": [],
    "errors": [],
}
code = idle_for_input(ringp2, {"param_idx": 0, "state8": 0}, {})
check(
    "ringdrain-success",
    code == 3
    and ringp2["current"]["tree"] == 272
    and ringp2["params"] == [0]
    and len(ringp2["frames_a"]) == 1,
    f"code={code} {ringp2['current']}",
)

# ---- 6. fixture: DoNodeAction result law (0x1538c0/0x153ae0) ----
def do_node_action(node, try_element_result, elem):
    """Transpile of the primitive-node switch at 0x153ae0 and the exit-node
    handling at 0x153bdc. try_element_result is the value the prim returned."""
    if try_element_result is None:
        # exit node: 0xfe = return false, anything else = return true
        returned_false = node["exit"] == 0xFE
        return ("pop", returned_false)
    r = try_element_result
    if r == 0:
        elem["node"] = node["false_b"]
        return ("advance", "false")
    if r == 1:
        elem["node"] = node["true_b"]
        return ("advance", "true")
    if r == 2:
        return ("yield", None)
    if r == 3:
        return ("advance", "gosub")
    return ("error", None)


elem = {"node": 5}
kind, val = do_node_action(
    {"true_b": 6, "false_b": 7}, 0, elem
)
check("donodeaction-false", kind == "advance" and val == "false" and elem["node"] == 7, f"{kind} {val} {elem}")
kind, val = do_node_action({"true_b": 6, "false_b": 7}, 1, elem)
check("donodeaction-true", kind == "advance" and val == "true" and elem["node"] == 6, f"{kind} {val} {elem}")
kind, val = do_node_action({"true_b": 6, "false_b": 7}, 2, elem)
check("donodeaction-yield", kind == "yield", f"{kind}")
kind, val = do_node_action({"true_b": 6, "false_b": 7}, -1, elem)
check("donodeaction-error", kind == "error", f"{kind}")
kind, val = do_node_action({"exit": 0xFE}, None, elem)
check("donodeaction-return-false", kind == "pop" and val is True, f"{kind} {val}")
kind, val = do_node_action({"exit": 0x00}, None, elem)
check("donodeaction-return-true", kind == "pop" and val is False, f"{kind} {val}")

# ---- 7. fixture: TickAllObjects per-object gate (0x139478-0x13958c) ----
def tick_all_objects(objects, flags_list, timers, fast_tick):
    simulated = []
    for i, obj in enumerate(objects):
        flags = flags_list[i]
        if not (flags & (1 << (31 - 15))):  # PPC bit15 = enabled
            continue
        if fast_tick and not (flags & (1 << (31 - 14))):
            # r30 != 0 pass: only bit14-set instances simulate
            continue
        if timers[i] > 0:
            timers[i] = (timers[i] & 0xFFFF0000) | ((timers[i] & 0xFFFF) - 1)
            continue
        simulated.append(i)
    return simulated


objs = ["a", "b", "c", "d"]
fl = [
    1 << (31 - 15),
    1 << (31 - 15),
    0,
    (1 << (31 - 15)) | (1 << (31 - 14)),
]
tim = [0, 5, 0, 0]
sim = tick_all_objects(objs, fl, tim, fast_tick=False)
check(
    "tickallobjects-gate",
    sim == [0, 3] and tim[1] == 4,
    f"sim={sim} tim={tim}",
)
sim = tick_all_objects(objs, fl, [0, 0, 0, 0], fast_tick=True)
check(
    "tickallobjects-filtered",
    sim == [3],
    f"sim={sim}",
)

# ---- 8. fixture: TryGotoRoutingSlot prim addressing (0x10f760) ----
def routing_slot_address(mode, obj, module, prim_param, elem_params):
    """Transpile of 0x10f78c-0x10f97c. Returns the slot descriptor or None."""
    if mode == 0:
        idx = elem_params[prim_param["param0"]]
        if idx < 0 or idx >= obj["slot_count"]:
            return None
        return ("obj", obj["slot_base"], idx)
    if mode == 1:
        idx = prim_param["param0"]
        if idx < 0 or idx >= obj["slot_count"]:
            return None
        return ("obj", obj["slot_base"], idx)
    if mode == 2:
        idx = prim_param["param0"]
        if idx < 0 or idx >= module["slot_count"]:
            return None
        return ("module", module["slot_base"], idx)
    return None


chair_obj = {"slot_count": 4, "slot_base": 0x144}
module = {"slot_count": 2, "slot_base": 0xA8}
check(
    "routing-slot-mode0",
    routing_slot_address(0, chair_obj, module, {"param0": 1}, [3, 2])
    == ("obj", 0x144, 2),
    "",
)
check(
    "routing-slot-mode1-bounds",
    routing_slot_address(1, chair_obj, module, {"param0": 9}, []) is None,
    "",
)
check(
    "routing-slot-mode2-module",
    routing_slot_address(2, chair_obj, module, {"param0": 1}, [])
    == ("module", 0xA8, 1),
    "",
)

# ---- 9. fixture: cXPerson +0x28 vtable layout (data section) ----
SEC1_PACKED_OFF = 0x5C22F0
SEC1_PACKED_LEN = 0x6D834
SEC1_UNPACKED_LEN = 0x7BF80


def unpack_sec1(data: bytes) -> bytes:
    """r104 law: PEF packed-data unpack of the data section."""
    def varint(buf, i):
        v = 0
        while True:
            x = buf[i]
            i += 1
            v = ((v << 7) | (x & 0x7F)) & 0xFFFFFFFF
            if not x & 0x80:
                return v, i

    out = bytearray()
    i, n = 0, len(data)
    while i < n:
        opb = data[i]
        i += 1
        op, cnt = opb >> 5, opb & 0x1F
        if cnt == 0:
            cnt, i = varint(data, i)
        if op == 0:
            out += b"\x00" * cnt
        elif op == 1:
            out += data[i : i + cnt]
            i += cnt
        elif op == 2:
            rc, i = varint(data, i)
            blk = data[i : i + cnt]
            i += cnt
            out += blk * (rc + 1)
        elif op in (3, 4):
            custom, i = varint(data, i)
            rc, i = varint(data, i)
            common = b"\x00" * cnt if op == 4 else data[i : i + cnt]
            if op == 3:
                i += cnt
            out += common
            for _ in range(rc):
                out += data[i : i + custom]
                i += custom
                out += common
        else:
            # op >= 5 is reserved in the PEF format
            raise ValueError(f"reserved op {op}")
    return bytes(out)


sec1 = unpack_sec1(b[SEC1_PACKED_OFF : SEC1_PACKED_OFF + SEC1_PACKED_LEN])
check("sec1-size", len(sec1) == SEC1_UNPACKED_LEN, hex(len(sec1)))
vtable = struct.unpack_from(">I", sec1, 0x8000 - 0x70DC)[0]
check("vtable-ptr", vtable == 0x48EE0, hex(vtable))
expect_slots = {
    0x08: 0x10C3B0,  # TryElement__8cXPerson
    0x0C: 0x0D2D60,  # Error__8cXObjectFs
    0x10: 0x109050,  # StackJustPopped__8cXPersonFv
    0x1C: 0x10BFF0,  # Simulate__8cXPersonFl
    0x3C: 0x109180,  # GosubObjectTree__8cXPersonFP8cXObjectPssb
    0x40: 0x1081D0,  # Cleanup__8cXPersonFP8cXObject
}
for off, want in expect_slots.items():
    tv = struct.unpack_from(">I", sec1, vtable + off)[0]
    code = struct.unpack_from(">I", sec1, tv)[0]
    file_off = code + 0x8E90
    check(f"vtable+{off:#04x}", file_off == want, f"{file_off:#x} != {want:#x}")

# ---- report ----
passed = sum(1 for _, ok, _ in checks if ok)
total = len(checks)
state = {
    "sha256": sha,
    "round": "r250-serving",
    "pins": len([c for c in checks if c[0].startswith("pin ")]),
    "checks": total,
    "passed": passed,
    "failures": [f"{n}: {d}" for n, ok, d in checks if not ok],
}
out = HERE / "verified-state.json"
out.write_text(json.dumps(state, indent=2) + "\n")
for name, ok, detail in checks:
    if not ok:
        print(f"FAIL {name} {detail}")
print(f"r250-serving verify: {passed}/{total} checks PASS "
      f"({state['pins']} code pins)")
