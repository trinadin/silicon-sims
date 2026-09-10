#!/usr/bin/env python3
"""R153 tasks 2+3: exhaustive store inventory + sentinel hunt over sec0.

TASK 2 — every store whose displacement could land in cXPerson +0x5e8..+0x5f6:
  (a) D-form stores (stw 36 stwu 37 stb 38 stbu 39 sth 44 sthu 45 stmw 47)
      with disp in [0x5d0,0x610] (any width covering any byte of the region);
  (b) every X-form store (stwx 151 stwux 183 stbx 215 stbux 247 sthx 407
      sthux 439) grouped by enclosing function (register-walking writers);
  (c) addi/addis materializing a +0x58c..+0x78b interior pointer (base+offset
      style writers).
TASK 3 — sentinel writers:
  (a) li rX,10 / addi rX,rY,10 followed within 12 instrs by a store of rX;
  (b) 0x000A000A-style packed constants (oris/ori/addis with 10) + store;
  (c) backward branches whose body stores a constant-10 register.
"""
import bisect
import json
import struct

BIN = "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/game-data/The Sims/The Sims Complete"
OUTD = "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/tools/iff-dump/r153"
SEC0_OFF, SEC0_LEN = 0x8E90, 0x5B9458

b = open(BIN, "rb").read()
syms = json.load(open(f"{OUTD}/r153-symbols.json"))
m = {s["start"]: s["name"] for s in syms}
starts = sorted(m)


def fn(off):
    i = bisect.bisect_right(starts, off) - 1
    return ("<pre>" if i < 0 else m[starts[i]]), (0 if i < 0 else starts[i])


n = SEC0_LEN // 4
words = struct.unpack_from(f">{n}I", b, SEC0_OFF)
A = lambda i: SEC0_OFF + 4 * i
s16 = lambda v: v - 0x10000 if v & 0x8000 else v

DFORM_ST = {36: "stw", 37: "stwu", 38: "stb", 39: "stbu", 44: "sth", 45: "sthu", 47: "stmw"}
XFORM_ST = {151: "stwx", 183: "stwux", 215: "stbx", 247: "stbux", 407: "sthx", 439: "sthux"}

out = open(f"{OUTD}/r153-store-inventory.txt", "w")
out.write("# R153 task 2: store inventory touching cXPerson +0x5e8..+0x5f6\n\n")

out.write("## (a) D-form stores, disp in [0x5d0,0x610] (rA likely person base)\n")
for i, w in enumerate(words):
    op = w >> 26
    if op in DFORM_ST:
        d = s16(w & 0xFFFF)
        if 0x5D0 <= (w & 0xFFFF) <= 0x610 or 0x5D0 <= d <= 0x610:
            rS = (w >> 21) & 31
            rA = (w >> 16) & 31
            name, st = fn(A(i))
            out.write(f"{A(i):#08x}: {w:08x}  {DFORM_ST[op]} r{rS}, {d:#x}(r{rA})   in {name}+{A(i)-st:#x}\n")

out.write("\n## (b) all X-form indexed stores (grouped by function)\n")
byfn = {}
for i, w in enumerate(words):
    if (w >> 26) != 31:
        continue
    xo = (w >> 1) & 0x3FF
    if xo in XFORM_ST and (w & 1) == 0:
        rS = (w >> 21) & 31
        rA = (w >> 16) & 31
        rB = (w >> 11) & 31
        name, st = fn(A(i))
        byfn.setdefault(name, []).append((A(i), w, XFORM_ST[xo], rS, rA, rB))
for name in sorted(byfn):
    lst = byfn[name]
    # only keep functions with >=1 hit near person region or in person fns
    keep = [t for t in lst]
    out.write(f"\n== {name} ({len(keep)} indexed stores)\n")
    for a, w, mn, rS, rA, rB in keep:
        out.write(f"  {a:#08x}: {w:08x}  {mn} r{rS}, r{rA}, r{rB}\n")

out.write("\n## (c) addi rD, rA, imm materializing interior pointers (imm in [0x58c,0x78c] even)\n")
for i, w in enumerate(words):
    op = w >> 26
    if op == 14:
        imm = s16(w & 0xFFFF)
        rA = (w >> 16) & 31
        if rA != 0 and 0x58C <= imm <= 0x78C and imm % 2 == 0:
            rD = (w >> 21) & 31
            name, st = fn(A(i))
            out.write(f"{A(i):#08x}: {w:08x}  addi r{rD}, r{rA}, {imm:#x}   in {name}+{A(i)-st:#x}\n")
out.close()
print("task2 done ->", f"{OUTD}/r153-store-inventory.txt")

# ---- task 3 sentinel hunt ----
out = open(f"{OUTD}/r153-sentinel-hunt.txt", "w")
out.write("# R153 task 3: who writes value 10 into stores\n\n")

STORE_OPS = set(DFORM_ST) | {31}
out.write("## (a) li rX,10 / addi rX,rY,10 stored within 12 instrs\n")
hits = 0
for i, w in enumerate(words):
    op = w >> 26
    if op == 14 and (w & 0xFFFF) == 10:
        rD = (w >> 21) & 31
        rA = (w >> 16) & 31
        for j in range(1, 13):
            if i + j >= n:
                break
            w2 = words[i + j]
            o2 = w2 >> 26
            stored = False
            if o2 in DFORM_ST and ((w2 >> 21) & 31) == rD:
                stored = True
                d = s16(w2 & 0xFFFF)
                detail = f"{DFORM_ST[o2]} r{rD}, {d:#x}(r{(w2>>16)&31})"
            elif o2 == 31 and ((w2 >> 21) & 31) == rD:
                xo = (w2 >> 1) & 0x3FF
                if xo in XFORM_ST and (w2 & 1) == 0:
                    stored = True
                    detail = f"{XFORM_ST[xo]} r{rD}, r{(w2>>16)&31}, r{(w2>>11)&31}"
            if stored:
                name, st = fn(A(i))
                out.write(f"{A(i):#08x}: li/addi r{rD},r{rA},10 -> {A(i+j):#08x}: {words[i+j]:08x} {detail}   in {name}+{A(i)-st:#x}\n")
                hits += 1
                break
out.write(f"# {hits} sites total (see analysis for person-region subset)\n")

out.write("\n## (b) packed 0x000A000A builders (addis/oris 10 then store)\n")
cnt = 0
for i, w in enumerate(words):
    op = w >> 26
    if op in (25, 15) and (w & 0xFFFF) == 10:  # oris/addis imm==10
        rD = (w >> 21) & 31
        for j in range(1, 9):
            w2 = words[i + j] if i + j < n else 0
            if (w2 >> 26) in DFORM_ST and ((w2 >> 21) & 31) == rD:
                name, st = fn(A(i))
                out.write(f"{A(i):#08x}: {w:08x} (imm10 high) -> store {A(i+j):#08x} {w2:08x} disp {s16(w2&0xFFFF):#x}   in {name}+{A(i)-st:#x}\n")
                cnt += 1
                break
out.write(f"# {cnt} sites\n")
out.close()
print("task3 part1 done ->", f"{OUTD}/r153-sentinel-hunt.txt")
