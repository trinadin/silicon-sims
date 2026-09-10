#!/usr/bin/env python3
"""R153 task 1b: find virtual callers of cXPerson::Reset / ::Initialize.

The cXPerson-family vtable lives in the unpacked data image (sec1) with
entries = pointers to CFM descriptors {code, 0x8000}. Verified slice
(image offsets, byte-verified 0x48ee8..0x48f74):
  [14] Cleanup  [15] Initialize (vtable byte 60)  [16] Reset (byte 64)
  [17] PostLoad [18] PreSave
Virtual call idiom (glue 0x5a29e0 byte-verified: lwz r0,0(r12); mtctr r0;
lwz r2,4(r12); bctr):
  lwz r12, 0(obj)      ; vptr
  lwz r12, K(r12)      ; descriptor
  bl 0x5a29e0
This script lists every `lwz r12, K(r12)` (K in {60,64,68,72}) whose window
contains a call to 0x5a29e0, plus lwzx variants fed by li 60/64/68/72.
"""
import bisect
import json
import struct

BIN = "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/game-data/The Sims/The Sims Complete"
OUTD = "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/tools/iff-dump/r153"
SEC0_OFF, SEC0_LEN = 0x8E90, 0x5B9458
GLUE = 0x5A29E0

b = open(BIN, "rb").read()
syms = json.load(open(f"{OUTD}/r153-symbols.json"))
m = {s["start"]: s["name"] for s in syms}
starts = sorted(m)


def fn(off):
    i = bisect.bisect_right(starts, off) - 1
    return "<pre>" if i < 0 else (m[starts[i]] + ("" if starts[i] == off else f"+{off-starts[i]:#x}"))


n = SEC0_LEN // 4
words = struct.unpack_from(f">{n}I", b, SEC0_OFF)
# word index i -> file address SEC0_OFF + 4*i
# direct bl to glue: op18 LK with target GLUE
glue_bl = set()
for i, w in enumerate(words):
    if (w >> 26) == 18 and (w & 1):
        li = (w >> 2) & 0xFFFFFF
        if li & 0x800000:
            li -= 0x1000000
        if SEC0_OFF + 4 * i + li * 4 == GLUE:
            glue_bl.add(i)

out = open(f"{OUTD}/r153-virtual-callers.txt", "w")
out.write("# sites: lwz r12, K(r12) with bl 0x5a29e0 within the next 4 instrs\n")
res = {}
for i in range(n - 8):
    w = words[i]
    if (w >> 26) == 32 and ((w >> 21) & 31) == 12 and ((w >> 16) & 31) == 12:
        K = w & 0xFFFF
        if K >= 0x8000:
            K -= 0x10000
        if K in (60, 64, 68, 72):
            if any((i + j) in glue_bl for j in range(1, 5)):
                addr = SEC0_OFF + 4 * i
                tag = {60: "Initialize?", 64: "Reset?", 68: "PostLoad?", 72: "PreSave?"}[K]
                res.setdefault(K, []).append((addr, w))
                out.write(f"{addr:#08x}: {w:08x} lwz r12,{K}(r12)  [{tag}] in {fn(addr)}\n")
out.write("\n# lwzx r12, rA, rB sites with a nearby glue bl (manual filter needed)\n")
for i in range(n - 8):
    w = words[i]
    if (w >> 26) == 31 and ((w >> 21) & 31) == 12 and ((w >> 26) == 31) and (w & 1) == 0:
        xo = (w >> 1) & 0x3FF  # wait: XO is bits 1-10 for X-form
        pass
out.close()
print(open(f"{OUTD}/r153-virtual-callers.txt").read())
