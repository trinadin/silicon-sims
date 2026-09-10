#!/usr/bin/env python3
"""R92: full PPC instruction decoder for the ORIGINAL engine binary dump.

Companion to scan_pef_ppc.py (R90). The scanner decodes only immediate ops;
for the cloud/balloon drift port I need every word — the %N idioms live in
mulhw/srawi/rlwinm/subf sequences the scanner prints as .long.

Branch decode (R129 CORRECTION of the R91/R92 "byte-delta" rule):
  * op 16 (bc): 14-bit BD field (bits 2-15) x4. The old s16(w & 0xFFFC)
    form is exactly this and was always correct.
  * op 18 (b/bl): 24-bit LI field (bits 2-25) x4, sign-extended. The old
    rule reused the s16 form, silently dropping LI bits >= 14 and wrapping
    targets mod 64KB. Ground truth (R129 symbol map): the rung calls in
    cObjPickerTool::Release target ObjSelector::AdultsOnly & co at
    0x103870-0x1041b0; the old rule mis-addressed them +0x80000 into
    unrelated bodies (R128's "outlined fragments" were these mis-decodes).
    Validated: bl word 0x4BF7EF61 -> LI -0x20428 -> target 0x1041b0.

Usage:
  python3 ppc_decode.py <binary> 0xSTART 0xEND
"""
import struct
import sys


def s16(v):
    return v - 0x10000 if v & 0x8000 else v


XO31 = {
    266: "add", 40: "subf", 10: "addc", 8: "subfc", 235: "mullw",
    75: "mulhw", 11: "mulhwu", 491: "divw", 100: "cntlzw",
    24: "slw", 536: "srw", 792: "sraw", 824: "srawi",
    28: "and", 444: "or", 390: "xor", 124: "nor", 476: "nand",
    284: "eqv", 60: "andc", 202: "orc",
    23: "lwzx", 55: "lwzux", 87: "lbzx", 119: "lbzux", 151: "stwx",
    183: "stwux", 215: "stbx", 279: "lhzx", 341: "lhax", 407: "sthx",
    20: "lwarx", 150: "stwcx.", 0: "cmp", 32: "cmpl",
    954: "extsb", 922: "extsh", 19: "mfcr", 339: "mfspr", 467: "mtspr",
    310: "eieio", 598: "sync", 854: "eieio",
}
XO19 = {16: "bclr", 528: "bcctr", 257: "crand", 449: "cror", 193: "crnor",
        225: "crandc", 33: "crxor", 289: "creqv", 417: "crorc", 81: "crnand"}
SPRS = {256: "lr", 9: "ctr", 8: "xer"}
DFORM = {14: "addi", 12: "addic", 13: "addic.", 15: "addis", 7: "mulli",
         24: "ori", 25: "oris", 26: "xori", 27: "xoris", 28: "andi.", 29: "andis.",
         11: "cmpwi", 10: "cmplwi", 32: "lwz", 33: "lwzu", 34: "lbz", 35: "lbzu",
         36: "stw", 37: "stwu", 38: "stb", 39: "stbu", 40: "lhz", 41: "lhzu",
         42: "lha", 43: "lhau", 44: "sth", 45: "sthu", 46: "lmw", 47: "stmw",
         48: "lfs", 49: "lfsu", 50: "lfd", 51: "lfdu", 52: "stfs", 53: "stfsu",
         54: "stfd", 55: "stfdu"}
# (R223) A-form float mnemonics, primary 59 (single) and 63 (double/arith).
XO59 = {18: "fdivs", 20: "fsubs", 21: "fadds", 22: "fsqrts", 24: "fres",
        25: "fmuls", 28: "fmsubs", 29: "fmadds", 30: "fnmsubs", 31: "fnmadds"}
XO63 = {18: "fdiv", 20: "fsub", 21: "fadd", 22: "fsqrt", 24: "fre", 25: "fmul",
        28: "fmsub", 29: "fmadd", 30: "fnmsub", 31: "fnmadd", 12: "frsp",
        14: "fctiw", 15: "fctiwz", 26: "fneg", 32: "fcmpo", 0: "fcmpu",
        38: "mtfsb1", 40: "fnabs", 72: "fmrin" if False else "fmr", 136: "fnegabs" if False else "fnabs"}
XO31X = {535: "lfsx", 567: "lfsux", 599: "lfdx", 631: "lfdux",
         663: "stfsx", 695: "stfsux", 727: "stfdx", 759: "stfdux", 851: "fmrx"}
# s16-immediate D-form ops (rA may be 0 => li/symbolic constants)
S16 = {14, 12, 13, 7, 11, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47}

BO_NAMES = {12: "", 4: "", 16: "ctr!=0 ", 18: "ctr==0 ", 8: "ctr!=0 ",
            0: "ctr==0 "}


def rlwinm_str(rS, SH, MB, ME):
    if SH == 0 and MB == 0 and ME == 31:
        return "copy"
    if SH == 0 and MB == 31 and ME == 31:
        return "&1"
    if SH == 0 and MB == 0 and ME == 0:
        return "&0x80000000"
    if SH == 1 and MB == 31 and ME == 31:
        return "bit31(rS)"
    if MB == 0 and ME == 31 - SH and SH:
        return f"<<{SH}"
    if SH and MB == 32 - SH and ME == 31:
        return f">>{SH}"
    if SH == 24 and MB == 24 and ME == 31:
        return "<<24"
    return f"rotl{SH},m[{MB}-{ME}]"


def freg(i):
    return f"f{i}"


def decode(w, addr):
    op = w >> 26
    # (R223) float D-form loads/stores
    if op in (48, 49, 50, 51, 52, 53, 54, 55):
        rD = (w >> 21) & 31
        rA = (w >> 16) & 31
        v = s16(w & 0xFFFF)
        return f"{DFORM[op]} {freg(rD)}, {v}({rA if rA else '0'})"
    # (R225 CORRECTED, capstone-verified) A-form float arithmetic, primaries
    # 59/63. Field order: frD(6-10), frA(11-15), frB(16-20), frC(21-25),
    # XO(26-30, FIVE bits), Rc(31). R223's second cut had frB/frC swapped —
    # capstone 5.0.7 disassembly of Room::ComputeRoom 0x126894 (fsubs f5,f1,f2
    # with src2 in bits 16-20) is the ground truth; only the 5-bit XO fix from
    # R223 was right.
    if op in (59, 63):
        xo = (w >> 1) & 0x1F
        tbl = XO59 if op == 59 else XO63
        name = tbl.get(xo)
        if name is None:
            return None
        fD = (w >> 21) & 31
        fA = (w >> 16) & 31
        fB = (w >> 11) & 31
        fC = (w >> 6) & 31
        rc = "." if w & 1 else ""
        if name in ("fcmpu", "fcmpo"):
            crd = (w >> 23) & 7
            return f"{name} cr{crd}, {freg(fA)}, {freg(fB)}"
        if name in ("frsp", "fctiw", "fctiwz", "fneg", "fmr", "fnabs", "fabs"):
            return f"{name}{rc} {freg(fD)}, {freg(fB)}"
        if name in ("fdivs", "fdiv", "fsubs", "fsub", "fadds", "fadd", "fsqrts", "fsqrt", "fre", "fres"):
            return f"{name}{rc} {freg(fD)}, {freg(fA)}, {freg(fB)}"
        if name in ("fmuls", "fmul"):
            return f"{name}{rc} {freg(fD)}, {freg(fA)}, {freg(fC)}"
        # madd/msub family: fD = fA*fC + fB (fmsubs: -fB; fnmadds: negate)
        return f"{name}{rc} {freg(fD)}, {freg(fA)}, {freg(fC)}, {freg(fB)}"
    if op == 18:
        d = (w >> 2) & 0xFFFFFF
        d = (d - 0x1000000 if d & 0x800000 else d) * 4
        lk = "l" if w & 1 else ""
        return f"b{lk} {addr + d:#x}"
    if op == 16:
        BO = (w >> 21) & 31
        BI = (w >> 16) & 31
        d = s16(w & 0xFFFC)
        aa = "a" if w & 2 else ""
        cond = {0: f"lt({BI})", 8: f"gt({BI})", 12: f"ne({BI})", 4: f"eq({BI})"}.get(BO, f"BO{BO}/{BI} ")
        return f"b{aa}{cond[:-1] if cond.endswith(' ') and BO in (0,8,12,4) else cond.strip()} {addr + d:#x}"
    if op == 19:
        xo = (w >> 1) & 0x3FF
        if xo in XO19:
            BO = (w >> 21) & 31
            name = XO19[xo]
            if name in ("bclr", "bcctr"):
                if xo == 16 and BO == 20 and (w & 1):
                    return "blr"
                if xo == 528 and BO == 20 and (w & 1):
                    return "bctrl"
                if BO == 20:
                    return name.rstrip("r") + "r"
                cond = {12: "ne", 4: "eq", 0: "lt", 8: "gt"}.get(BO, f"BO{BO}")
                return f"{name} {cond}"
            return f"{name} {(w >> 21) & 31}, {(w >> 16) & 31}, {(w >> 11) & 31}"
        return None
    if op == 31:
        xo = (w >> 1) & 0x3FF
        rD = (w >> 21) & 31
        rA = (w >> 16) & 31
        rB = (w >> 11) & 31
        name = XO31.get(xo) or XO31X.get(xo)
        if name is None:
            return None
        if name in XO31X.values():
            return f"{name} {freg(rD)}, r{rA}, r{rB}"
        if name == "mfspr":
            spr = ((w >> 11) & 0x3E0) | ((w >> 16) & 0x1F) >> 0 if False else (((w >> 6) & 0x1F) << 5) | ((w >> 16) & 0x1F) >> 0
            spr = (((w >> 11) & 31) << 5) | ((w >> 16) & 31)
            return f"mf{SPRS.get(spr, f'spr{spr}')} r{rD}"
        if name == "mtspr":
            spr = (((w >> 11) & 31) << 5) | ((w >> 16) & 31)
            return f"mt{SPRS.get(spr, f'spr{spr}')} r{rD}"
        if name == "srawi":
            return f"srawi r{rA}, r{rD}, {rB}"
        if name == "cmp":
            return f"cmp cr{rD}, r{rA}, r{rB}"
        if name == "cmpl":
            return f"cmpl cr{rD}, r{rA}, r{rB}"
        if name in ("add", "subf", "mullw", "mulhw", "mulhwu", "divw", "and", "or",
                    "xor", "nor", "slw", "srw", "sraw", "andc", "orc", "eqv",
                    "addc", "subfc", "extsb", "extsh", "cntlzw"):
            dot = "." if (w & 1) and name[-1] != "." else ""
            return f"{name}{dot} r{rD}, r{rA}, r{rB}"  # rD = rA <op> rB
        # loads/stores X-form: op rD, off(rA) with index rB
        return f"{name} r{rD}, r{rB}, r{rA}"
    if op == 21:
        rS = (w >> 21) & 31
        rA = (w >> 16) & 31
        SH = (w >> 11) & 31
        MB = (w >> 6) & 31
        ME = (w >> 1) & 31
        m = rlwinm_str(rS, SH, MB, ME)
        if m in ("copy", "&1", "&0x80000000", "bit31(rS)") or m.startswith(("<<", ">>")):
            return f"rlwinm r{rA}, r{rS} {m}"
        return f"rlwinm r{rA}, r{rS}, {SH}, {MB}, {ME}"
    if op in DFORM:
        name = DFORM[op]
        rD = (w >> 21) & 31
        rA = (w >> 16) & 31
        imm = w & 0xFFFF
        v = s16(imm) if op in S16 else imm
        if name in ("lwz", "lwzu", "lbz", "lbzu", "stw", "stwu", "stb", "stbu",
                    "lhz", "lhzu", "lha", "lhau", "sth", "sthu"):
            return f"{name} r{rD}, {v}(r{rA})"
        if name == "stmw":
            return f"stmw r{rD}, {v}(r{rA})"
        if name == "lmw":
            return f"lmw r{rD}, {v}(r{rA})"
        return f"{name} r{rD}, r{rA}, {v}"
    if op == 3:  # twi
        return f"twi {(w >> 21) & 31}, r{(w >> 16) & 31}, {s16(w & 0xFFFF)}"
    return None


def main():
    data = open(sys.argv[1], "rb").read()
    start, end = int(sys.argv[2], 0), int(sys.argv[3], 0)
    for a in range(start, min(end, len(data) - 3), 4):
        w = struct.unpack(">I", data[a:a + 4])[0]
        d = decode(w, a)
        txt = bytes(data[a:a + 4])
        asc = "".join(chr(c) if 32 <= c < 127 else "." for c in txt)
        print(f"{a:#08x}: {w:08x}  {d if d else '.long':42s} |{asc}|")


if __name__ == "__main__":
    main()
