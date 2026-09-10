#!/usr/bin/env python3
"""R154: scan the original engine binary for immediate anchors of tree ids
9761 ('Convert Interests to 0-1000') and 39200 ('Add Hot Date Interests').

Task 1 of the round. Scan space:
  * sec0 (raw code, file [0x8E90 .. 0x8E90+0x5B9458)): every 4-aligned word
    decoded as a D-form instruction whose 16-bit immediate field equals
    9761 (0x2621) or 39200 (0x9920). STRONG forms = the field is consumed
    as a value/compare (li/addi rA=0, lis, ori, oris, xori, xoris, andi,
    andis, cmpwi, cmplwi, mulli, addic); WEAK forms = D-form load/store
    displacement fields (may be struct offsets by coincidence).
  * 32-bit forms: lis rX,HI followed by ori rX,rX,LO whose composite
    equals the target (targets are < 0x10000, so only lis rX,0/1 shapes).
  * sec1 unpacked image (tools/iff-dump/r104/sec1-unpacked.bin): raw
    big-endian halfwords 0x2621/0x9920 and 32-bit words 0x00002621 /
    0x00009920 (pointer/table contexts), with neighbor halfwords dumped
    (id-run detection: dispatch tables store id RUNS).

Every sec0 hit gets a disassembled window with enclosing function and
resolved bl targets (via the r153 symbol table).

Usage: r154-anchor-scan.py [--out DIR]   (writes r154-anchor-9761.txt)
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

TARGETS = {8486: "BHAV 8486 'Convert Interests to 0-1000'",
           8345: "BHAV 8345 'Add Hot Date Interests'"}
# raw 16-bit field patterns (8486 = 0x2126; 8345 = 0x2099; signed view of
# 0x2099 is +0x2099; R154-RESCAN: true ids, the earlier 9761/39200 were
# byte-swapped misreads of the LE rsmp u16s)
RAW = {8486: 0x2126, 8345: 0x2099}

STRONG_OPS = {3, 7, 10, 11, 12, 13, 14, 15, 24, 25, 26, 27, 28, 29}
WEAK_OPS = set(range(32, 48))  # D-form loads/stores

b = open(BIN, "rb").read()
syms = json.load(open(f"{R153}/r153-symbols.json"))
NAME = {s["start"]: s["name"] for s in syms}
STARTS = sorted(NAME)


def fn(off):
    i = bisect.bisect_right(STARTS, off) - 1
    return "<pre>" if i < 0 else (NAME[STARTS[i]] + ("" if STARTS[i] == off else f"+{off - STARTS[i]:#x}"))


def words(a0, n):
    return [SEC0_OFF + 4 * (a0 + k) for k in range(n)]


nwords = SEC0_LEN // 4
W = struct.unpack_from(f">{nwords}I", b, SEC0_OFF)

hits = []  # (idx, value, kind, word)
for i, w in enumerate(W):
    op = w >> 26
    imm = w & 0xFFFF
    if imm in (0x2126, 0x2099):
        if op in (16, 18):
            kind = "BRANCH-disp"  # op16 bc / op18 b-bl: low bits are displacement
        elif op in STRONG_OPS:
            kind = "STRONG"
        elif op in WEAK_OPS:
            kind = "WEAK-disp"
        else:
            kind = "other-op"
        hits.append((i, imm, kind, w))

# word-aligned literal-pool scan (sec0 may hold data words between functions)
pool = []
for off in range(SEC0_OFF, SEC0_OFF + SEC0_LEN - 3, 4):
    w = struct.unpack_from(">I", b, off)[0]
    if w in (0x00002621, 0x00009920):
        pool.append((off, w))

# lis+ori 32-bit composite scan (op15 lis / op24 ori back-pairing)
pairs = []
for i in range(1, nwords):
    w1, w0 = W[i - 1], W[i]
    if (w1 >> 26) == 15 and (w0 >> 26) == 24:
        rd1, ra1 = (w1 >> 21) & 31, (w1 >> 16) & 31
        rd0, ra0 = (w0 >> 21) & 31, (w0 >> 16) & 31
        if ra0 == rd0 and ra1 == 0 and rd0 == rd1:
            v = ((w1 & 0xFFFF) << 16) | (w0 & 0xFFFF)
            if v in TARGETS:
                pairs.append((i, v, w1, w0))

out = []
out.append(f"# r154 anchor scan RESCAN: immediates == 8486 (0x2126) / 8345 (0x2099) - true ids")
out.append(f"# sec0 file range [0x{SEC0_OFF:x}..0x{SEC0_OFF + SEC0_LEN:x}), {nwords} words")
out.append(f"# raw-field hits: {len(hits)}  |  lis+ori composite hits: {len(pairs)}  |  word-literal pool hits: {len(pool)}")
for v, label in TARGETS.items():
    byk = {}
    for h in hits:
        if h[1] == RAW[v]:
            byk[h[2]] = byk.get(h[2], 0) + 1
    out.append(f"#   {label}: {byk}")
if pool:
    for off, w in pool:
        out.append(f"# pool word {w:08x} @ 0x{off:08x} in {fn(off)}")
else:
    out.append("# pool words: none")

for i, v, kind, w in hits:
    a = SEC0_OFF + 4 * i
    out.append(f"\n===== hit {kind} imm={v} @ file 0x{a:08x}  word {w:08x}  in {fn(a)} =====")
    lo, hi = max(0, i - 14), min(nwords, i + 15)
    for k in range(lo, hi):
        aa = SEC0_OFF + 4 * k
        ww = W[k]
        d = ppc_decode.decode(ww, aa)
        mark = ""
        if k == i:
            mark = "   <=== ANCHOR"
        elif (ww >> 26) == 18:
            li = (ww >> 2) & 0xFFFFFF
            if li & 0x800000:
                li -= 0x1000000
            t = aa + li * 4
            mark = f"   -> {fn(t)}"
        out.append(f"  0x{aa:08x}: {ww:08x}  {d if d else '.long':44s}{mark}")

for i, v, w1, w0 in pairs:
    a = SEC0_OFF + 4 * (i - 1)
    out.append(f"\n===== lis+ori composite {v} @ 0x{a:08x} in {fn(a)} =====")

# ---------------- sec1 data scan ----------------
d1 = open(SEC1, "rb").read()
out.append(f"\n\n# sec1 unpacked image: {len(d1):#x} bytes (r104/sec1-unpacked.bin)")
hw_hits = []
for off in range(0, len(d1) - 1, 2):
    if d1[off] == 0 and False:
        continue
    v = (d1[off] << 8) | d1[off + 1]
    if v in (0x2126, 0x2099):
        hw_hits.append((off, v))
out.append(f"# sec1 halfword hits: {len(hw_hits)}")
for off, v in hw_hits:
    lo = max(0, off - 16)
    ctx = " ".join(f"{((d1[p] << 8) | d1[p + 1]):04x}" for p in range(lo, min(len(d1) - 1, off + 18), 2))
    # 4-byte view too
    woff = off & ~3
    w = struct.unpack_from(">I", d1, woff)[0]
    out.append(f"  sec1+0x{off:06x}: hw {v:#06x} (u32@4align {w:#010x})  hw[{lo // 2}:..] = {ctx}")

w32_hits = []
for off in range(0, len(d1) - 3, 4):
    w = struct.unpack_from(">I", d1, off)[0]
    if w in (0x00002621, 0x00009920):
        w32_hits.append((off, w))
out.append(f"# sec1 32-bit BE word hits (0x00002621/0x00009920): {len(w32_hits)}")
for off, w in w32_hits:
    out.append(f"  sec1+0x{off:06x}: {w:08x}")

text = "\n".join(out)
open(f"{OUTD}/r154-anchor-9761.txt", "w").write(text)
print(text)
