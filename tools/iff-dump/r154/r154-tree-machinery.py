#!/usr/bin/env python3
"""R154 task 2/3: engine tree-invocation machinery audit.

Enumerates EVERY engine call site of the tree-running routines and extracts
hardcoded (immediate) arguments, to produce the complete list of
engine-hardcoded tree ids and their events.

Runners audited (sec0 file offsets, from r153 symbols):
  RunTree__8cXObjectFP8BehaviorsPCcPs        0x0efd30  (by NAME)
  GosubObjectTree__8cXObjectFP8cXObjectPssb  0x0efe00  (by ID, virtual)
  GosubObjectTree__8cXPersonFP8cXObjectPssb  0x109180  (override, delegates)
  RunOneTickTree__7TreeSimFP8BehaviorssPs    0x153e60  (by ID)
  RunCheckTree__7TreeSimFP8BehaviorssPs      0x153ec0  (by ID)
  Reset__7TreeSimFP8BehaviorssPs             0x154320  (by ID, resets+runs)
  TreeSim helper 0x1540d0                    (RunCheckTree inner)

Id-resolvers audited (data-driven path — id from ObjFnTable resource):
  GetTreeID__10ObjFnTableF13ObjEntryPoint    0x0fcca0
  GetTreeID__8cXObjectF13ObjEntryPoint       0x170870
  GetFunction__10ObjFnTableF13ObjEntryPoint  0x0fca50

For each call site: scan backward up to N instructions for li/addi rA, r0, K
(addi with rA field 0) and record (reg, value) pairs = candidate hardcoded
arguments. Also record the EP constants (r4) at GetTreeID sites -> EVENT MAP.

Virtual dispatch: find the vtable slot holding GosubObjectTree__8cXPerson
(sec1 image cXPerson vtable @0x48ee0, r153-verified) and census every
`lwz r12, K(rX)` ... `bl 0x5a29e0` (virtual-glue) site with that K.

Writes r154-engine-trees.txt.
"""
import bisect
import json
import struct
import sys

sys.path.insert(0, "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/tools/iff-dump")
import ppc_decode  # noqa: E402

BIN = "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/game-data/The Sims/The Sims Complete"
SEC1 = "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/tools/iff-dump/r104/sec1-unpacked.bin"
R153 = "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/tools/iff-dump/r153"
OUTD = "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/tools/iff-dump/r154"
SEC0_OFF, SEC0_LEN = 0x8E90, 0x5B9458

RUNNERS = {
    0x0EFD30: "cXObject::RunTree(Beh*,name,params) [BY NAME]",
    0x0EFE00: "cXObject::GosubObjectTree(obj,id,s,b) [BY ID]",
    0x109180: "cXPerson::GosubObjectTree(obj,id,s,b) [BY ID]",
    0x153E60: "TreeSim::RunOneTickTree(Beh*,s,s*) [BY ID]",
    0x153EC0: "TreeSim::RunCheckTree(Beh*,s,s*) [BY ID]",
    0x154320: "TreeSim::Reset(Beh*) [Reset+run]",
}
RESOLVERS = {
    0x0FCCA0: "ObjFnTable::GetTreeID(ObjEntryPoint) [DATA-DRIVEN id]",
    0x170870: "cXObject::GetTreeID(ObjEntryPoint) [DATA-DRIVEN id]",
    0x0FCA50: "ObjFnTable::GetFunction(ObjEntryPoint) [builder]",
}

b = open(BIN, "rb").read()
syms = json.load(open(f"{R153}/r153-symbols.json"))
NAME = {s["start"]: s["name"] for s in syms}
STARTS = sorted(NAME)


def fn(off):
    i = bisect.bisect_right(STARTS, off) - 1
    return "<pre>" if i < 0 else (NAME[STARTS[i]] + ("" if STARTS[i] == off else f"+{off - STARTS[i]:#x}"))


nwords = SEC0_LEN // 4
W = struct.unpack_from(f">{nwords}I", b, SEC0_OFF)


def bl_target(i):
    w = W[i]
    if (w >> 26) != 18 or not (w & 1):
        return None
    li = (w >> 2) & 0xFFFFFF
    if li & 0x800000:
        li -= 0x1000000
    return SEC0_OFF + 4 * i + li * 4


def li_value(i):
    """If W[i] is addi rD, r0, K (li), return (rD, K) else None."""
    w = W[i]
    if (w >> 26) != 14:
        return None
    rD = (w >> 21) & 31
    rA = (w >> 16) & 31
    if rA != 0:
        return None
    imm = w & 0xFFFF
    return rD, imm - 0x10000 if imm & 0x8000 else imm


def mr_pairs(i):
    """If W[i] is or rD, rS, rS (mr), return (dst_ppc, src_ppc)."""
    w = W[i]
    if (w >> 26) != 31 or ((w >> 1) & 0x3FF) != 444:
        return None
    rs = (w >> 21) & 31
    ra = (w >> 16) & 31
    rb = (w >> 11) & 31
    if rs == rb:
        return ra, rs  # ppc: or rA(dst), rS, rS
    return None


out = []
out.append("# r154 engine tree-invocation machinery audit")
out.append(f"# sec0 [0x{SEC0_OFF:x}..0x{SEC0_OFF + SEC0_LEN:x}); runners {sorted(RUNNERS)}")

# ---------------- 1. direct call-site census + argument immediates ----------
site_info = []  # (site, target, label, win)
for i in range(nwords):
    t = bl_target(i)
    if t in RUNNERS or t in RESOLVERS:
        site_info.append((SEC0_OFF + 4 * i, t))

by_target = {}
for site, t in site_info:
    by_target.setdefault(t, []).append(site)

out.append(f"\n## direct-call census")
for t in sorted(by_target):
    label = RUNNERS.get(t) or RESOLVERS.get(t)
    out.append(f"  {t:#x} {label}: {len(by_target[t])} direct bl site(s)")

# vtable slot for GosubObjectTree ------------------------------------------
# CFM model: sec1 holds 8-byte tvectors {code ptr, toc}; vtables hold tvector
# ADDRESSES (sec1-image values); virtual dispatch loads vptr, then
# lwz r12,K(r12) -> tvector, then glue 0x5a29e0 (lwz r0,0(r12); mtctr;
# lwz r2,4(r12); bctr).
d1 = open(SEC1, "rb").read()
GOSUB_CXP = 0x109180 - SEC0_OFF   # cXPerson:: version, sec0-relative
GOSUB_CXO = 0x0EFE00 - SEC0_OFF   # cXObject:: version
out.append(f"\n## tvector/vtable discovery")
tvec_offs = {}
for off in range(0, len(d1) - 3, 4):
    v = struct.unpack_from(">I", d1, off)[0]
    if v in (GOSUB_CXP, GOSUB_CXO):
        tvec_offs.setdefault(v, []).append(off)
for v, offs in sorted(tvec_offs.items()):
    who = "cXPerson::" if v == GOSUB_CXP else "cXObject::"
    out.append(f"  tvector for {who}GosubObjectTree (code {v:#x}) at sec1: {[f'{o:#x}' for o in offs]}")

# vtable slots = sec1 offsets whose u32 equals a tvector offset
GOSUB_SLOT = None
for tv_off, offs in tvec_offs.items():
    for tvo in offs:
        refs = [o for o in range(0, len(d1) - 3, 4)
                if struct.unpack_from(">I", d1, o)[0] == tvo]
        for r in refs:
            out.append(f"    tvector {tvo:#x} referenced at sec1+{r:#x}")
# the cXPerson vtable base is 0x48ee0 (r153-verified vptr); its Gosub slot:
VT = 0x48EE0
for tv_off, offs in tvec_offs.items():
    if tv_off == GOSUB_CXP:
        for tvo in offs:
            for r in range(0, len(d1) - 3, 4):
                if struct.unpack_from(">I", d1, r)[0] == tvo and VT <= r < VT + 0x400:
                    GOSUB_SLOT = r - VT
out.append(f"# GosubObjectTree virtual slot: +{GOSUB_SLOT:#x} ({GOSUB_SLOT})" if GOSUB_SLOT is not None
           else "# GosubObjectTree slot NOT FOUND")

# virtual-glue census for the slot ------------------------------------------
GLUE = 0x5A29E0
vcall_sites = []
if GOSUB_SLOT is not None:
    for i in range(nwords):
        if bl_target(i) != GLUE:
            continue
        # scan back up to 8 insns for lwz r12, K(rX) chains; track base reg
        k_off = None
        for j in range(i - 1, max(0, i - 9), -1):
            w = W[j]
            if (w >> 26) == 32:  # lwz
                rD = (w >> 21) & 31
                rA = (w >> 16) & 31
                imm = w & 0xFFFF
                k = imm - 0x10000 if imm & 0x8000 else imm
                if rD == 12 and (k_off is None or rA == 12):
                    if k_off is None:
                        k_off = k
                    elif rA == 12:
                        k_off = ("2step", k, k_off)
            elif (w >> 26) == 18:
                break
        if k_off is not None:
            vcall_sites.append((SEC0_OFF + 4 * i, k_off))

out.append(f"\n## virtual-glue (0x{GLUE:x}) sites loading r12 (first lwz r12,K(rX) backward): {len(vcall_sites)}")
gosub_vcalls = []
kcount = {}
for site, k in vcall_sites:
    kk = k if isinstance(k, int) else (k[2] if isinstance(k[2], int) else k)
    kcount[kk] = kcount.get(kk, 0) + 1
    if kk == GOSUB_SLOT:
        gosub_vcalls.append(site)
out.append(f"# distinct final-slot K values: {len(kcount)}; GosubObjectTree-slot(60) sites: {len(gosub_vcalls)}")
for site in gosub_vcalls:
    out.append(f"  GOSUB-VIRTUAL {site:#010x} in {fn(site)}")

# ---------------- 2. per-site windows + li extraction -----------------------
def window_dump(site, target, back=14, fwd=8):
    i = (site - SEC0_OFF) // 4
    lo, hi = max(0, i - back), min(nwords, i + fwd)
    lines = []
    for k in range(lo, hi):
        a = SEC0_OFF + 4 * k
        d = ppc_decode.decode(W[k], a)
        mark = ""
        if k == i:
            mark = "   <== CALL"
        lv = li_value(k)
        if lv and lv[0] in (3, 4, 5, 6, 7):
            mark += f"   [li r{lv[0]}, {lv[1]}]"
        lines.append(f"    0x{a:08x}: {W[k]:08x}  {d if d else '.long':42s}{mark}")
    return "\n".join(lines)


out.append(f"\n\n## per-site windows (li r3..r7 marked)")
hard_ids = []  # (site, fn, runner, regvals)
for site, t in sorted(site_info):
    label = RUNNERS.get(t) or RESOLVERS.get(t)
    i = (site - SEC0_OFF) // 4
    args = {}
    for j in range(i - 16, i):
        lv = li_value(j)
        if lv and lv[0] in (3, 4, 5, 6, 7):
            args[lv[0]] = lv[1]
    out.append(f"\n--- {site:#010x} in {fn(site)}  -> {t:#x} {label}")
    out.append(f"    li'd arg regs in window: {args}")
    if t in RUNNERS:
        hard_ids.append((site, fn(site), label, dict(args)))
    out.append(window_dump(site, t))

open(f"{OUTD}/r154-engine-trees.txt", "w").write("\n".join(out))
print("\n".join(out[:40]))
print(f"\n[wrote r154-engine-trees.txt] sites: {len(site_info)}, vcall sites: {len(vcall_sites)}, gosub vcalls: {len(gosub_vcalls)}")
print("runner call-site functions:")
for t in sorted(by_target):
    label = RUNNERS.get(t) or RESOLVERS.get(t)
    fns = sorted(set(fn(s).split('+')[0] for s in by_target[t]))
    print(f"  {t:#x} {label}")
    for f in fns:
        print(f"     {f}")
