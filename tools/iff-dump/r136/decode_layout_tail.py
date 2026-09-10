#!/usr/bin/env python3
"""R136: symbolic decode of House::GetHouseStats's LayoutScore tail.

The R133 dump printed the tail's FP ops as .long (ppc_decode.py has no FP
support) and hand-decoding kept producing nonsense. This tool:

  1. re-reads the tail words straight from the binary,
  2. decodes the FP ops with spec-correct fields (frD/frA/frB/frC via the
     (w>>21)/(w>>16)/(w>>11)/(w>>6) extraction; XO=(w>>1)&0x3FF),
  3. SYMBOLICALLY EXECUTES the whole tail (GPR + FP + stack slots), so the
     layoutScore formula falls out with zero hand-transcription risk,
  4. dumps the two const pools ([TOC-23512] floats / [TOC-23516] doubles)
     and resolves [TOC-29460] (ClearRouteHistory's init constant) with the
     R135 TOC recipe,
  5. scans the binary for bl callers of AddLayoutTick / ClearRouteHistory /
     GetHouseStats and decodes context around each.

Usage: python3 decode_layout_tail.py
"""
import os
import struct
import sys

IFFDUMP = "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/tools/iff-dump"
BIN = sys.argv[1] if len(sys.argv) > 1 else \
    os.path.join(IFFDUMP, "../../game-data/The Sims/The Sims Complete")
SEC1 = "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/tools/iff-dump/r104/sec1-unpacked.bin"

data = open(BIN, "rb").read()
sec1 = open(SEC1, "rb").read()


def w_at(a):
    return struct.unpack(">I", data[a:a + 4])[0]


def f32_at(a):
    return struct.unpack(">f", data[a:a + 4])[0]


def f64_at(a):
    return struct.unpack(">d", data[a:a + 8])[0]


# ---------------------------------------------------------------- FP decode
A59 = {18: ("fdivs", "AB"), 20: ("fsubs", "AB"), 21: ("fadds", "AB"),
       22: ("fsqrts", "B"), 24: ("fres", "B"), 25: ("fmuls", "AC"),
       28: ("fmsubs", "ACB"), 29: ("fmadds", "ACB"),
       30: ("fnmsubs", "ACB"), 31: ("fnmadds", "ACB")}
A63 = {18: ("fdiv", "AB"), 20: ("fsub", "AB"), 21: ("fadd", "AB"),
       22: ("fsqrt", "B"), 25: ("fmul", "AC"), 26: ("fsel", "ACB"),
       28: ("fmsub", "ACB"), 29: ("fmadd", "ACB"),
       30: ("fnmsub", "ACB"), 31: ("fnmadd", "ACB")}
X63 = {12: ("frsp", "B"), 14: ("fctiw", "B"), 15: ("fctiwz", "B"),
       72: ("fmr", "B"), 136: ("fneg", "B"), 264: ("fabs", "B"),
       32: ("fcmpo", "CMP")}


def fp_str(w, addr):
    op = w >> 26
    frD = (w >> 21) & 31
    frA = (w >> 16) & 31
    frB = (w >> 11) & 31
    frC = (w >> 6) & 31
    xo = (w >> 1) & 0x3FF
    xo5 = (w >> 1) & 0x1F  # A-form XO occupies only bits 26-30
    if op in (48, 50, 52, 54):  # lfs/lfd/stfs/stfd
        names = {48: "lfs", 50: "lfd", 52: "stfs", 54: "stfd"}
        d = struct.unpack(">h", w.to_bytes(4, "big")[2:])[0]
        return f"{names[op]} f{frD}, {d}(r{frA})"
    if op == 59 and xo5 in {v for v in A59}:
        n, ops = A59[xo5]
        argmap = {"A": f"f{frA}", "B": f"f{frB}", "C": f"f{frC}"}
        srcs = [argmap[c] for c in ops]
        return f"{n} f{frD}, " + ", ".join(srcs)
    if op == 63 and xo5 in {v for v in A63}:
        n, ops = A63[xo5]
        argmap = {"A": f"f{frA}", "B": f"f{frB}", "C": f"f{frC}"}
        srcs = [argmap[c] for c in ops]
        return f"{n} f{frD}, " + ", ".join(srcs)
    if op == 63 and xo in X63:
        n, ops = X63[xo]
        if ops == "CMP":
            bf = (w >> 23) & 7
            return f"fcmpo cr{bf}, f{frA}, f{frB}"
        return f"{n} f{frD}, f{frB}"
    if op == 63 and xo == 583:
        return f"mffs f{frD}"
    return None


CRBIT = {0: "LT", 1: "GT", 2: "EQ", 3: "SO"}


def branch_str(w, addr):
    bo = (w >> 21) & 31
    bi = (w >> 16) & 31
    d = struct.unpack(">h", w.to_bytes(4, "big")[2:])[0] & 0xFFFC
    d = struct.unpack(">h", struct.pack(">H", w & 0xFFFC))[0]
    tgt = addr + d
    cr = bi // 4
    bit = CRBIT.get(bi % 4, bi % 4)
    if bo == 12:
        cond = f"cr{cr}.{bit} set"
    elif bo == 4:
        cond = f"cr{cr}.{bit} clear"
    else:
        cond = f"BO{bo}"
    return f"b{cond} -> {tgt:#x}"


# ------------------------------------------------- symbolic abstract interp
class Sym:
    """Symbolic value: a python string expression."""

    def __init__(self, s):
        self.s = s


def run_tail(start, end, r28="House", r29="stats", r2="TOC"):
    gpr = {i: None for i in range(32)}
    fpr = {i: None for i in range(32)}
    stack = {}  # offset -> Sym
    gpr[28] = Sym(r28)
    gpr[29] = Sym(r29)
    gpr[2] = Sym(r2)
    gpr[31] = Sym("[TOC-23512]")  # loaded at 0x8c19c, before the tail window
    lines = []
    cr = {}  # (bit) -> Sym condition text

    def s(x):
        return x.s if isinstance(x, Sym) else x

    a = start
    while a < end:
        w = w_at(a)
        op = w >> 26
        txt = fp_str(w, a)
        note = ""
        if op == 32:  # lwz
            rD = (w >> 21) & 31
            rA = (w >> 16) & 31
            d = struct.unpack(">h", struct.pack(">H", w & 0xFFFF))[0]
            base = s(gpr[rA])
            if base == r2:
                gpr[rD] = Sym(f"[TOC{d}]")
                note = f"r{rD} = [TOC{d}]"
            else:
                gpr[rD] = Sym(f"MEM({base}+{d})")
                note = f"r{rD} = {gpr[rD].s}"
        elif op == 36:  # stw
            rD = (w >> 21) & 31
            rA = (w >> 16) & 31
            d = struct.unpack(">h", struct.pack(">H", w & 0xFFFF))[0]
            base = s(gpr[rA]) if gpr[rA] else f"r{rA}?"
            if base == "r1" or rA == 1:
                stack[d] = gpr[rD]
                note = f"[sp{d}] = {s(gpr[rD])}"
            else:
                note = f"MEM({base}+{d}) = {s(gpr[rD])}"
        elif op in (14, 15):  # addi/addis
            rD = (w >> 21) & 31
            rA = (w >> 16) & 31
            imm = struct.unpack(">h", struct.pack(">H", w & 0xFFFF))[0]
            if op == 15:
                imm <<= 16
            if rA == 0:
                gpr[rD] = Sym(f"{imm & 0xFFFFFFFF:#x}")
            else:
                gpr[rD] = Sym(f"({s(gpr[rA])} + {imm & 0xFFFFFFFF:#x})")
            note = f"r{rD} = {gpr[rD].s}"
        elif op in (26, 27):  # xori/xoris
            rD = (w >> 21) & 31
            rA = (w >> 16) & 31
            imm = w & 0xFFFF
            if op == 27:
                imm <<= 16
            gpr[rD] = Sym(f"({s(gpr[rA])} ^ {imm:#x})")
            note = f"r{rD} = {gpr[rD].s}"
        elif op == 31:
            xo = (w >> 1) & 0x3FF
            rD = (w >> 21) & 31
            rA = (w >> 16) & 31
            rB = (w >> 11) & 31
            if xo == 266:  # add
                gpr[rD] = Sym(f"({s(gpr[rA])} + {s(gpr[rB])})")
                note = f"r{rD} = {gpr[rD].s}"
            elif xo == 40:  # subf
                gpr[rD] = Sym(f"({s(gpr[rB])} - {s(gpr[rA])})")
                note = f"r{rD} = {gpr[rD].s}"
            elif xo == 444:  # or (mr)
                gpr[rD] = gpr[rA]
                note = f"r{rD} = r{rA}"
        elif op == 21:  # rlwinm
            rS = (w >> 21) & 31
            rA = (w >> 16) & 31
            SH = (w >> 11) & 31
            MB = (w >> 6) & 31
            ME = (w >> 1) & 31
            if SH == 1 and MB == 0 and ME == 30:
                gpr[rA] = Sym(f"(2*({s(gpr[rS])})) & 0x7fffffff")
            elif SH == 0 and MB == 24 and ME == 31:
                gpr[rA] = Sym(f"({s(gpr[rS])}) & 0xff")
            else:
                gpr[rA] = Sym(f"rlw({s(gpr[rS])},{SH},{MB},{ME})")
            note = f"r{rA} = {gpr[rA].s}"
        elif op == 11:  # cmpwi
            rA = (w >> 16) & 31
            imm = struct.unpack(">h", struct.pack(">H", w & 0xFFFF))[0]
            cr[0] = f"{s(gpr[rA])} ?= {imm}"
            note = f"cmpwi cr0, {s(gpr[rA])}, {imm}"
        elif op == 16:
            note = branch_str(w, a)
        elif op == 18:
            d = (w >> 2) & 0xFFFFFF
            d = (d - 0x1000000 if d & 0x800000 else d) * 4
            note = f"{'bl' if w & 1 else 'b'} {a + d:#x}"
        # ---- FP ops
        elif op == 48:  # lfs
            frD = (w >> 21) & 31
            d = struct.unpack(">h", struct.pack(">H", w & 0xFFFF))[0]
            rA = (w >> 16) & 31
            fpr[frD] = Sym(f"(float)MEM({s(gpr[rA])}+{d})")
            note = f"f{frD} = {fpr[frD].s}"
        elif op == 50:  # lfd
            frD = (w >> 21) & 31
            d = struct.unpack(">h", struct.pack(">H", w & 0xFFFF))[0]
            rA = (w >> 16) & 31
            if rA == 1 and d in stack:
                fpr[frD] = Sym(f"dbl{{HI={[s(stack.get(d))]}, LO={[s(stack.get(d+4))]} }}")
            else:
                fpr[frD] = Sym(f"(double)MEM({s(gpr[rA])}+{d})")
            note = f"f{frD} = {fpr[frD].s}"
        elif op == 54:  # stfd
            frD = (w >> 21) & 31
            d = struct.unpack(">h", struct.pack(">H", w & 0xFFFF))[0]
            stack[d] = Sym(f"HI({s(fpr[frD])})")
            stack[d + 4] = Sym(f"LO({s(fpr[frD])})")
            note = f"[sp{d}..] = dbl({s(fpr[frD])})"
        elif op in (59, 63):
            xo = (w >> 1) & 0x3FF   # X-form (10-bit) XO
            xo5 = (w >> 1) & 0x1F   # A-form (5-bit) XO
            frD = (w >> 21) & 31
            frA = (w >> 16) & 31
            frB = (w >> 11) & 31
            frC = (w >> 6) & 31
            tbl = A59 if op == 59 else A63
            if xo5 in tbl:
                n, ops = tbl[xo5]
                if n in ("fsubs", "fsub"):
                    fpr[frD] = Sym(f"({s(fpr[frA])} - {s(fpr[frB])})")
                elif n in ("fdivs", "fdiv"):
                    fpr[frD] = Sym(f"({s(fpr[frA])} / {s(fpr[frB])})")
                elif n in ("fmuls", "fmul"):
                    fpr[frD] = Sym(f"({s(fpr[frA])} * {s(fpr[frC])})")
                elif n in ("fadds", "fadd"):
                    fpr[frD] = Sym(f"({s(fpr[frA])} + {s(fpr[frB])})")
                elif n in ("fmsubs",):
                    fpr[frD] = Sym(f"({s(fpr[frA])}*{s(fpr[frC])} - {s(fpr[frB])})")
                elif n in ("fmadds",):
                    fpr[frD] = Sym(f"({s(fpr[frA])}*{s(fpr[frC])} + {s(fpr[frB])})")
                note = f"f{frD} = {fpr[frD].s}"
            elif op == 63 and xo in X63:
                n, o = X63[xo]
                if n == "fmr":
                    fpr[frD] = fpr[frB]
                    note = f"f{frD} = f{frB}"
                elif n == "fneg":
                    fpr[frD] = Sym(f"(-{s(fpr[frB])})")
                    note = f"f{frD} = {fpr[frD].s}"
                elif n == "fctiwz":
                    fpr[frD] = Sym(f"(int){s(fpr[frB])}")
                    note = f"f{frD} = {fpr[frD].s}"
                elif n == "fctiw":
                    fpr[frD] = Sym(f"(intround){s(fpr[frB])}")
                    note = f"f{frD} = {fpr[frD].s}"
                elif n == "fcmpo":
                    cr[0] = f"{s(fpr[frA])} ?f {s(fpr[frB])}"
                    note = f"fcmpo {s(fpr[frA])} vs {s(fpr[frB])}"
        raw = data[a:a + 4].decode("latin1")
        asc = "".join(c if 32 <= ord(c) < 127 else "." for c in raw)
        lines.append(f"{a:#08x}: {w:08x}  {(txt or '.long'):36s} | {note}")
        a += 4
    return lines


# ------------------------------------------------------------------- TOC
def toc_read(x):
    off = 0x8000 - x
    v = struct.unpack(">I", sec1[off:off + 4])[0]
    out = [f"[TOC{x}] @sec1+{off:#x} = {v:#x}"]
    if v >= 0x7bf80:
        fa = 0x8e90 + (v - 0x7bf80) if False else 0x8e90 + v - 0x7bf80
        # R135 recipe: V is section-relative; >= 0x7bf80 means code-relative
        fa = 0x8e90 + (v & 0x7FFFFF) if v >= 0x800000 else 0x8e90 + (v - 0x7bf80)
        out.append(f"   raw at code-rel {fa:#x}: ...")
    else:
        try:
            f = f32_at_sec1(v)
            out.append(f"   sec1+{v:#x}: f32={f}")
        except Exception:
            pass
    return "\n".join(out)


def f32_at_sec1(off):
    return struct.unpack(">f", sec1[off:off + 4])[0]


print("=" * 78)
print("SECTION 1: the const pools (raw bytes from file)")
print("=" * 78)
for base, n, kind in ((0x5a2fb4, 4, "f"), (0x5a2fc0, 4, "d")):
    print(f"pool @{base:#x}:")
    for i in range(n):
        if kind == "f":
            print(f"  +{i*4}: 0x{w_at(base+i*4):08x} = {f32_at(base+i*4)}")
        else:
            print(f"  +{i*8}: 0x{w_at(base+i*8):08x}{w_at(base+i*8+4):08x} = {f64_at(base+i*8)}")

print()
print("=" * 78)
print("SECTION 2: symbolic execution of GetHouseStats tail 0x8c2e0-0x8c3e0")
print("=" * 78)
for ln in run_tail(0x8c2e0, 0x8c3e0):
    print(ln)

print()
print("=" * 78)
print("SECTION 3: ClearRouteHistory 0x8c430 (init constant via [TOC-29460])")
print("=" * 78)
for ln in run_tail(0x8c430, 0x8c4bc, r28="x"):
    print(ln)
# TOC-29460 resolution (R135 recipe: V >= 0x7bf80 -> code file 0x8e90+V)
off = 0x8000 - 29460
v = struct.unpack(">I", sec1[off:off + 4])[0]
print(f"\n[TOC-29460] @sec1+{off:#x} = {v:#x}")
if v >= 0x7bf80:
    fa = 0x8e90 + v
    print(f"  code-rel file {fa:#x}: u32=0x{w_at(fa):08x}  bytes={data[fa:fa+16].hex()}")
elif v > 0:
    print(f"  sec1+{v:#x}: u32=0x{struct.unpack('>I', sec1[v:v+4])[0]:#x} "
          f"first16={sec1[v:v+16].hex()}")
else:
    print("  empty slot (BSS)")

print()
print("=" * 78)
print("SECTION 4: bl callers of AddLayoutTick(0x8c4f0) / ClearRouteHistory(0x8c430)")
print("=" * 78)
targets = {0x8c4f0: "AddLayoutTick", 0x8c430: "ClearRouteHistory"}
callers = {t: [] for t in targets}
for a in range(0x8e90, len(data) - 3, 4):
    w = w_at(a)
    if (w >> 26) == 18 and (w & 1):
        d = (w >> 2) & 0xFFFFFF
        d = (d - 0x1000000 if d & 0x800000 else d) * 4
        if a + d in targets:
            callers[a + d].append(a)
for t, name in targets.items():
    print(f"{name}: {len(callers[t])} callers")
    for c in callers[t][:40]:
        print(f"  call at {c:#x}")
