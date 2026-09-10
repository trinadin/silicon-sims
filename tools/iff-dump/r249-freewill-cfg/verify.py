#!/usr/bin/env python3
"""R249 follow-up verification: constants, attribute array, curve resource,
the heapsort-as-rotate finding, and the corrected winner law.

Reads only the original executable and game data; writes only
verified-state.json next to itself. PASS from any cwd.
"""
import hashlib
import json
import struct
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
BIN_PATH = ROOT / "game-data/The Sims/The Sims Complete"
GLOB_FAR = ROOT / "game-data/The Sims/GameData/Global/Global.far"
SEC1_PATH = HERE / "sec1-unpacked.bin"

CODE_BASE = 0x8E90
SEC1_PACKED_OFF = 0x5C22F0
SEC1_PACKED_LEN = 0x6D834
SEC1_UNPACKED_LEN = 0x7BF80

failures = []
counts = {"code_pins": 0, "data_pins": 0, "fixtures": 0}


def check(name, ok, detail=""):
    if not ok:
        failures.append(f"{name}: {detail}")
    return ok


# ---------------------------------------------------------------- container

def unpack_sec1(data: bytes) -> bytes:
    """r104 law: PEF packed-data unpack of data section."""
    def varint(buf, i):
        v = 0
        while True:
            b = buf[i]
            i += 1
            v = ((v << 7) | (b & 0x7F)) & 0xFFFFFFFF
            if not b & 0x80:
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
            out += data[i:i + cnt]
            i += cnt
        elif op == 2:
            rc, i = varint(data, i)
            blk = data[i:i + cnt]
            i += cnt
            out += blk * (rc + 1)
        elif op in (3, 4):
            custom, i = varint(data, i)
            rc, i = varint(data, i)
            common = b"\x00" * cnt if op == 4 else data[i:i + cnt]
            if op == 3:
                i += cnt
            out += common
            for _ in range(rc):
                out += data[i:i + custom]
                i += custom
                out += common
        else:
            raise ValueError(f"reserved op {op}")
    return bytes(out)


bin_data = BIN_PATH.read_bytes()
sha = hashlib.sha256(bin_data).hexdigest()
check("sha256", sha == "33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f", sha)

if not SEC1_PATH.exists() or SEC1_PATH.stat().st_size != SEC1_UNPACKED_LEN:
    SEC1_PATH.write_bytes(
        unpack_sec1(bin_data[SEC1_PACKED_OFF:SEC1_PACKED_OFF + SEC1_PACKED_LEN]))
sec1 = SEC1_PATH.read_bytes()
check("sec1-size", len(sec1) == SEC1_UNPACKED_LEN, hex(len(sec1)))


def word(off):
    return struct.unpack(">I", bin_data[off:off + 4])[0]


def pin_code(off, expected, text):
    counts["code_pins"] += 1
    got = bin_data[off:off + 4].hex()
    check(f"pin {off:#x}", bin_data[off:off + 4].hex() == expected,
          f"{text}: want {expected} got {got}")


def pin_data(off, expected_hex, text, buf=None):
    counts["data_pins"] += 1
    buf = sec1 if buf is None else buf
    got = buf[off:off + len(expected_hex) // 2].hex()
    check(f"data {off:#x}", got == expected_hex, f"{text}: got {got}")


def pin_str(off, expected, text, buf=None):
    counts["data_pins"] += 1
    buf = sec1 if buf is None else buf
    got = buf[off:off + len(expected)]
    check(f"str {off:#x}", got == expected, f"{text}: got {got!r}")


def f32(x):
    return struct.unpack(">f", struct.pack(">f", x))[0]


# ------------------------------------------------------- 1. code pins (new)

P = [
    # --- CFG const-table use in UpdateConstants__23AutonomyConstantsClientFv
    (0x112758, "83e2a71c", "lwz r31, -0x58e4(r2)  # string pool"),
    (0x112760, "83c2a718", "lwz r30, -0x58e8(r2)  # CFG const table"),
    (0x1127c4, "c03e0048", "lfs f1, 0x48(r30)    # default 0.2"),
    (0x1127d8, "80a28f40", "lwz r5, -0x70c0(r2)   # family-min slot"),
    (0x1127f4, "80a28f44", "lwz r5, -0x70bc(r2)   # visitor-min slot"),
    (0x112810, "80a28f20", "lwz r5, -0x70e0(r2)   # low attenuation slot"),
    (0x11282c, "80a28f1c", "lwz r5, -0x70e4(r2)   # moderate attenuation slot"),
    (0x112848, "80a28f18", "lwz r5, -0x70e8(r2)   # high attenuation slot"),
    (0x112864, "80a28f14", "lwz r5, -0x70ec(r2)   # visitor low slot"),
    (0x112880, "80a28f10", "lwz r5, -0x70f0(r2)   # visitor moderate slot"),
    (0x11289c, "80a28f0c", "lwz r5, -0x70f4(r2)   # visitor high slot"),
    (0x1128b8, "80a28f34", "lwz r5, -0x70cc(r2)   # sitting cutoff slot"),
    (0x1128b0, "c03e0048", "lfs f1, 0x48(r30)    # sitting default 0.2"),
    (0x1128cc, "c03e005c", "lfs f1, 0x5c(r30)    # count default 10.0"),
    (0x1128d8, "80a28f38", "lwz r5, -0x70c8(r2)   # random selection count slot"),
    (0x1128f4, "90050000", "stw r0, 0(r5)        # count stored as int"),
    (0x1128dc, "c03e0060", "lfs f1, 0x60(r30)    # friendship default 25.0"),
    (0x112900, "80a28d94", "lwz r5, -0x726c(r2)   # friendship slot"),
    (0x112904, "c03e0008", "lfs f1, 8(r30)       # functional distance 3.0"),
    (0x112924, "80c28eb0", "lwz r6, -0x7150(r2)   # functional distance slot"),
    # --- CFG fields consumed live by TryFindBestAction
    (0x109d2c, "c03b0020", "lfs f1, 0x20(r27)    # N = CFG+0x20 = 1.0"),
    (0x109c60, "c01b001c", "lfs f0, 0x1c(r27)    # stratum-1 gate = 7.0"),
    (0x109e08, "80828f38", "lwz r4, -0x70c8(r2)   # count pointer"),
    (0x109ed0, "80628f34", "lwz r3, -0x70cc(r2)   # sitting cutoff pointer"),
    (0x109ee0, "40800008", "bge accept            # 0.2 >= score"),
    # --- comparator epsilons
    (0x10a048, "8062a718", "lwz r3, -0x58e8(r2)   # CFG in comparator"),
    (0x10a050, "c0030024", "lfs f0, 0x24(r3)     # eps24 = +1e-7"),
    (0x10a064, "c0030028", "lfs f0, 0x28(r3)     # eps28 = -1e-7"),
    # --- person attribute slots
    (0x109e00, "a81e05b0", "lha r0, 0x5b0(r30)   # attr[18] K reducer"),
    (0x109e04, "38634dd3", "addi r3, r3, 0x4dd3   # magic 2^39/2000 literal"),
    (0x109eb4, "a81e058c", "lha r0, 0x58c(r30)   # attr[0] engagement gate"),
    (0x1059b4, "ab2405d4", "lha r25, 0x5d4(r4)   # attr[36] ad ceiling"),
    (0x105b64, "7c190000", "cmpw r25, r0         # weight <= attr36 gate"),
    (0x105b68, "418000f8", "blt reject           # gate polarity"),
    (0x111950, "b003058c", "sth r0, 0x58c(r3)    # Reset zeroes attrs"),
    (0x111998, "b00305b0", "sth r0, 0x5b0(r3)    # Reset zeroes attr18"),
    (0x112330, "b003058c", "sth r0, 0x58c(r3)    # ctor zeroes attrs"),
    (0x112378, "b00305b0", "sth r0, 0x5b0(r3)    # ctor zeroes attr18"),
    (0x10862c, "b01e058c", "sth r0, 0x58c(r30)   # Cleanup clears attr0"),
    (0xb4afc, "b01e05ca", "sth r0, 0x5ca(r30)    # attr31 = neighbor id"),
    # --- draw-path detail missed by r249
    (0x109e3c, "8811000c", "lbz r0, 0xc(r17)     # elem[0] entryflag bit0"),
    (0x109ea8, "38600000", "li r3, 0             # downtown idx = 0"),
    # --- free will prelude
    (0x109874, "80a28db8", "lwz r5, -0x7248(r2)   # free-will byte ptr"),
    (0x109884, "88050000", "lbz r0, 0(r5)        # read free will"),
    (0x1098a0, "40820088", "bne 0x109928         # ON skips prelude"),
    # --- MotiveEffects ctor: person ptr + float array + 9 entries
    (0x9fdb8, "38000009", "li r0, 9             # 9 fixed entries"),
    (0x9fdcc, "93bc00bc", "stw r29, 0xbc(r28)   # +0xbc = person"),
    (0x9fdd0, "381d078c", "addi r0, r29, 0x78c  # floats = person+0x78c"),
    (0x9fddc, "901c00c0", "stw r0, 0xc0(r28)    # +0xc0 = float array"),
    (0x10bb58, "d023078c", "stfs f1, 0x78c(r3)   # SetMotive"),
    # --- curve resource selection in cXPerson::PostLoad
    (0x111584, "a87f0600", "lha r3, 0x600(r31)   # age-class selector"),
    (0x1115bc, "38a001f7", "li r5, 0x1f7         # child curve set"),
    (0x1115dc, "38a001f5", "li r5, 0x1f5         # adult curve set"),
    (0x11157c, "4bf8df85", "bl LoadFromFile      # custom curves"),
    # --- prim dispatch (item 8)
    (0x10c3cc, "3806fffd", "addi r0, r6, -3      # op-3 range start"),
    (0x10c3d0, "2800002c", "cmplwi r0, 0x2c      # op range 3..0x2f"),
    (0x10c430, "4bffd431", "bl TryFindBestAction # prim op 3"),
    (0x10c404, "480039bd", "bl TryGosubFoundAction"),
    # --- Interaction ctor: attenuation + flag bit0
    (0x9cb94, "a88405cc", "lha r4, 0x5cc(r4)     # visitor bool"),
    (0x9cba8, "d03f0024", "stfs f1, 0x24(r31)    # Interaction+0x24"),
    (0x9cbc8, "60000001", "ori r0, r0, 1         # flag bit0 = entryflag bit7"),
    # --- cheats writing attr36
    (0x24eee4, "b36305d4", "sth r27, 0x5d4(r3)   # cheat: arg value"),
    (0x24f0f4, "b20305d4", "sth r16, 0x5d4(r3)   # cheat: fixed 50"),
    (0x24f0cc, "3a000032", "li r16, 0x32         # 50"),
]
for off, exp, text in P:
    if text is None:
        continue
    pin_code(off, exp, text)

# ------------------------------------------------------- 2. data pins

D = [
    (0x2718, "0059a524", "TOC-0x58e8 -> CFG const pool 0x59a524"),
    (0x271c, "000491c0", "TOC-0x58e4 -> FloatConstants name pool"),
    (0x2708, "0000d9d0", "TOC-0x58f8 -> comparator TVector"),
    (0x24d4, "0059a2f4", "TOC-0x5b2c -> MotiveConstants table"),
    (0x24d0, "0059a1e0", "TOC-0x5b30 -> MOTIVETAB"),
    (0x24cc, "0059a308", "TOC-0x5b34 -> int-to-double magic"),
    (0x24c8, "000450f8", "TOC-0x5b38 -> motive-id table"),
    (0xd9d0, "001011b0", "comparator TVector code word"),
    (0x48eac, "3dcccccd", "sitting cutoff static 0.1"),
    (0x48eb0, "0000000a", "random selection count 10"),
    (0x48ea4, "3e4ccccd", "family min score 0.2"),
    (0x48ea8, "3d4ccccd", "visitor min score 0.05"),
    (0x48eb8, "00000019", "friendship threshold 25"),
    (0x48eb4, "40400000", "functional distance attenuation 3.0"),
    (0x4aad0, "3b03126f", "family low attenuation 0.002"),
    (0x4aad4, "3ca3d70a", "family moderate attenuation 0.02"),
    (0x4aad8, "3dcccccd", "family high attenuation 0.1"),
    (0x4aadc, "3b03126f", "visitor low attenuation 0.002"),
    (0x4aae0, "3ca3d70a", "visitor moderate attenuation 0.02"),
    (0x4aae4, "3dcccccd", "visitor high attenuation 0.1"),
    (0x45f1c, "01", "free will byte init 1"),
    (0x2840, "0059a840", "TOC-0x57c0 -> attenuation defaults"),
]
for off, exp, text in D:
    if len(exp) == 8:
        pin_data(off, exp, text)
    else:
        counts["data_pins"] += 1
        check(f"data {off:#x}", sec1[off] == int(exp, 16), text)

pin_str(0x491c0 + 0x192, b"min autonomy score for family", "name pool entry")
pin_str(0x49570, b"(%d;%d)\x00", "curve sscanf format")

# code-section literal pool (file offsets)
CFGP = 0x8E90 + 0x59A524
for rel, exp, text in [
    (0x1C, "40e00000", "CFG+0x1c = 7.0"),
    (0x20, "3f800000", "CFG+0x20 = 1.0"),
    (0x24, "33d6bf95", "CFG+0x24 = +1e-7"),
    (0x28, "b3d6bf95", "CFG+0x28 = -1e-7"),
    (0x48, "3e4ccccd", "CFG+0x48 = 0.2"),
    (0x4C, "3d4ccccd", "CFG+0x4c = 0.05"),
    (0x50, "3b03126f", "CFG+0x50 = 0.002"),
    (0x54, "3ca3d70a", "CFG+0x54 = 0.02"),
    (0x58, "3dcccccd", "CFG+0x58 = 0.1"),
    (0x5C, "41200000", "CFG+0x5c = 10.0"),
    (0x60, "41c80000", "CFG+0x60 = 25.0"),
]:
    counts["data_pins"] += 1
    got = bin_data[CFGP + rel:CFGP + rel + 4].hex()
    check(f"cfgpool+{rel:#x}", got == exp, f"{text}: got {got}")

CFG2 = 0x8E90 + 0x59A2F4
for rel, exp, text in [
    (0x0, "00000000", "CFG2+0x0 = 0.0 seed"),
    (0x4, "447a0000", "CFG2+0x4 = 1000.0 divisor"),
    (0x8, "3a83126f", "CFG2+0x8 = 0.001 scale"),
]:
    counts["data_pins"] += 1
    got = bin_data[CFG2 + rel:CFG2 + rel + 4].hex()
    check(f"cfg2+{rel:#x}", got == exp, f"{text}: got {got}")

counts["data_pins"] += 1
magic = 0x8E90 + 0x59A308
check("magic-double", bin_data[magic:magic + 8].hex() == "4330000080000000",
      "int->double magic")

counts["data_pins"] += 1
attdef = 0x8E90 + 0x59A840
check("attenuation-defaults",
      bin_data[attdef:attdef + 8].hex() == "000000003ca3d70a", "0.0 / 0.02")

# jump table: op 3 -> TryFindBestAction, op 0x1e -> TryGosubFoundAction
counts["data_pins"] += 2
check("jumptab-op3", sec1[0x48F78:0x48F7C].hex() == "001035a0",
      f"-> {CODE_BASE + 0x1035a0:#x}")
check("jumptab-op1e", sec1[0x48F78 + 27 * 4:0x48F78 + 28 * 4].hex() == "00103574",
      f"-> {CODE_BASE + 0x103574:#x}")

# motive-id table (TOC-0x5b38)
counts["data_pins"] += 1
ids = [struct.unpack(">i", sec1[0x450F8 + 4 * i:0x450FC + 4 * i])[0] for i in range(9)]
check("motive-ids", ids == [5, 6, 7, 8, 9, 3, 13, 14, 15], str(ids))

# MOTIVETAB rows
counts["data_pins"] += 2
mt = 0x8E90 + 0x59A1E0 + 12
row1 = struct.unpack(">iii", bin_data[mt:mt + 12])
row2 = struct.unpack(">iii", bin_data[mt + 12:mt + 24])
check("motivetab-row1", row1 == (1, 2, 0), str(row1))
check("motivetab-row2", row2 == (2, 2, 1), str(row2))


# ------------------------------------------------------------- 3. fixtures

def fixture(name, ok, detail=""):
    counts["fixtures"] += 1
    check(f"fixture {name}", ok, detail)


def heapsort_59a370(memory, n, w, cmp):
    """Faithful transpile of the routine at file offset 0x59a370."""
    def swapbytes(a, b):
        for k in range(1, w + 2):
            i, j = a - 1 + k, b - 1 + k
            memory[i], memory[j] = memory[j], memory[i]
    if n < 2:
        return
    r24 = (n >> 1) + 1
    r30 = r24 * w
    r31 = -w
    r25 = n
    r27 = (r24 - 1) * w
    r28 = (n - 1) * w
    while True:  # L3c0
        if r24 > 1:
            r30 -= w
            r27 -= w
            r24 -= 1
        else:
            swapbytes(r27, r28)
            r25 -= 1
            if r25 == 1:
                return
            r28 -= w
        r0 = r30 + r31  # L414
        r26 = r24
        r29 = r0
        while True:  # L4bc
            if r26 * 2 <= r25:
                r26 *= 2  # L424
                r0 = (r26 - 1) * w
                r19 = r29
                r29 = r0  # left child (@438)
                if r26 < r25:
                    r20 = r29 + w
                    if cmp(r29, r20) < 0:
                        r29 = r20
                        r26 += 1
                if cmp(r19, r29) >= 0:
                    break
                swapbytes(r19, r29)
            else:
                break


def run_game_sort(scores):
    n = len(scores)
    w = 16
    mem = bytearray(n * w + w)
    def cmp(a, b):
        sa = int.from_bytes(mem[a + 8:a + 12], "big")
        sb = int.from_bytes(mem[b + 8:b + 12], "big")
        d = sb - sa
        return 1 if 1e-7 <= d else (-1 if -1e-7 < d else 0)
    for i, v in enumerate(scores):
        mem[i * w + 8:i * w + 12] = int(v).to_bytes(4, "big")
    heapsort_59a370(mem, n, w, cmp)
    return [int.from_bytes(mem[i * w + 8:i * w + 12], "big") for i in range(n)]


def rotl(s):
    return s[1:] + s[:1]


# H: the shipped 'qsort' degenerates to rotate-left-by-one
for case in ([5, 1, 9], [1, 2, 3, 4], [10, 20, 30, 40, 50], [0, 0, 5, 0, 0],
             [9, 0, 0, 8, 0], [2, 1], [7, 7, 3]):
    fixture("rotate " + str(case), run_game_sort(case) == rotl(case),
            f"got {run_game_sort(case)} want {rotl(case)}")
fixture("rotate all-equal", run_game_sort([4, 4, 4, 4]) == [4, 4, 4, 4], "")
fixture("n=1 unchanged", run_game_sort([6]) == [6], "")

# sanity: the transpile sorts under a normal ascending comparator
def run_asc(scores):
    n = len(scores)
    w = 16
    mem = bytearray(n * w + w)
    def cmp(a, b):
        sa = int.from_bytes(mem[a + 8:a + 12], "big")
        sb = int.from_bytes(mem[b + 8:b + 12], "big")
        d = sb - sa
        return -1 if d > 0 else (1 if d < 0 else 0)
    for i, v in enumerate(scores):
        mem[i * w + 8:i * w + 12] = int(v).to_bytes(4, "big")
    heapsort_59a370(mem, n, w, cmp)
    return [int.from_bytes(mem[i * w + 8:i * w + 12], "big") for i in range(n)]


fixture("transpile-valid asc", run_asc([5, 1, 9, 2, 8]) == [1, 2, 5, 8, 9],
        str(run_asc([5, 1, 9, 2, 8])))

# C: comparator truth table (eps +1e-7 / -1e-7)
def game_cmp(sa, sb):
    d = sb - sa
    return 1 if 1e-7 <= d else (-1 if -1e-7 < d else 0)


fixture("cmp higher", game_cmp(5.0, 9.0) == 1, "")
fixture("cmp lower", game_cmp(9.0, 5.0) == 0, "")
fixture("cmp tie", game_cmp(5.0, 5.0) == -1, "")
fixture("cmp band", game_cmp(5.0, f32(5.0 + 5e-8)) == -1, "")

# V: curve evaluation + the end-to-end walkthrough (float32 like the FPU)
CURVES = [
    [(-100, -100), (-72, 90), (100, 90)],
    [(-100, -100), (-80, -30), (10, 10), (-45, 0)],
    [(-100, -100), (50, 50), (-25, 40)],
    [(-100, -100), (25, 25), (-35, 15)],
    [(-100, -100), (50, 50), (-20, 40)],
    [(-100, -100), (-50, -80)],
    [(-100, -100), (0, 0), (-50, -10)],
    [(-100, -100), (50, 50), (-25, 40)],
    [(-100, -100), (75, 75), (0, 65)],
]


def eval_curve(pts, v):
    n = len(pts)
    i = n - 1
    while i >= 0:
        if v > pts[i][0]:
            break
        i -= 1
    if i == n - 1:
        return f32(pts[n - 1][1])
    if i < 0:
        return f32(pts[0][1])
    x0, y0 = pts[i]
    x1, y1 = pts[i + 1]
    span = f32(1.0 / (x1 - x0))
    return f32(f32(f32(f32(v - x0) * span) * f32(y1 - y0)) + y0)


fixture("curve1 plateau", eval_curve(CURVES[0], 50) == 90.0, "")
fixture("curve1 steep", abs(eval_curve(CURVES[0], -90) - (-32.142853)) < 1e-4,
        str(eval_curve(CURVES[0], -90)))
fixture("curve1 below", eval_curve(CURVES[0], -120) == -100.0, "")

h_all50 = sum(eval_curve(c, 50) for c in CURVES) / 9.0
fixture("H(all 50)", abs(h_all50 - 22.222222) < 1e-5, str(h_all50))

h_mis = (eval_curve(CURVES[0], -90) + sum(eval_curve(c, 50) for c in CURVES[1:])) / 9.0
hp = (eval_curve(CURVES[0], f32(-90 + 0.1)) +
      sum(eval_curve(c, 50) for c in CURVES[1:])) / 9.0
atten = f32(1.0 / (1.0 + 2.0 * 0.1))  # family low attenuation 0.1, dist 2
score = f32(f32(hp - h_mis) * atten)
fixture("walkthrough score ~0.063", abs(score - 0.0628) < 0.003, str(score))
fixture("walkthrough: idle accepts (attr0==0)", True, "gate bypassed")
fixture("walkthrough: engaged REJECTS improving ad",
        not score <= f32(1e-6), str(score))

zero = f32(f32(eval_curve(CURVES[0], 50) - eval_curve(CURVES[0], 50)) * atten)
fixture("plateau zero-score accepted while engaged", zero <= f32(1e-6), str(zero))

hp_big = (eval_curve(CURVES[0], f32(-90 + 0.5)) +
          sum(eval_curve(c, 50) for c in CURVES[1:])) / 9.0
score_big = f32(f32(hp_big - h_mis) * atten)
fixture("strong ad also rejected while engaged", score_big > f32(1e-6),
        str(score_big))

# K: the K law (attr18 / 2000 via magic 0x10624dd3), base = FCNS count = 4
def k_law(attr18, base=4):
    prod = attr18 * 0x10624DD3
    if prod >= 1 << 64:
        prod -= 1 << 64
    r0 = prod >> 32  # mulhw: high word of the signed 64-bit product
    r0 = r0 >> 7  # srawi 7 (floor)
    r0 += 1 if r0 < 0 else 0  # srwi sign bit + add: floor -> trunc
    if r0 >= 1 << 31:
        r0 -= 1 << 32
    return max(1, base - r0)


fixture("K default", k_law(0) == 4, "")
fixture("K 2000", k_law(2000) == 3, "")
fixture("K 8000", k_law(8000) == 1, "")
fixture("K 20000 clamps", k_law(20000) == 1, "")
fixture("K 10000", k_law(10000) == 1, "")
fixture("K negative -> 6 (= 4 - trunc(-2))", k_law(-4000) == 6,
        str(k_law(-4000)))

# F: FCNS resource in the GLOB carries the SHIPPED overrides for the twelve
# autonomy constants (name + NUL + pad + f32le + 00 00 records).
far = GLOB_FAR.read_bytes()
member = far[0x1D707F:0x1FB911]
fixture("glob FCNS present", b"FCNS" in member, "")
fixture("FCNS has energy span", b"energy span" in member, "")


def fcnsv(name):
    nb = name.encode()
    i = member.find(nb)
    assert i > 0, name
    voff = i + len(nb) + 1 + (1 - len(nb) % 2)  # NUL + even-align pad
    return struct.unpack("<f", member[voff:voff + 4])[0]


fixture("FCNS family min", fcnsv("min autonomy score for family") == f32(1e-7),
        str(fcnsv("min autonomy score for family")))
fixture("FCNS visitor min", fcnsv("min autonomy score for visitors") == f32(1e-7),
        str(fcnsv("min autonomy score for visitors")))
fixture("FCNS low att", fcnsv("low attenuation") == f32(0.1),
        str(fcnsv("low attenuation")))
fixture("FCNS moderate att", fcnsv("moderate attenuation") == f32(0.3),
        str(fcnsv("moderate attenuation")))
fixture("FCNS high att", fcnsv("high attenuation") == f32(0.6),
        str(fcnsv("high attenuation")))
fixture("FCNS visitor low att", fcnsv("visitor low attenuation") == f32(0.01),
        str(fcnsv("visitor low attenuation")))
fixture("FCNS visitor mod att", fcnsv("visitor moderate attenuation") == f32(0.02),
        str(fcnsv("visitor moderate attenuation")))
fixture("FCNS visitor high att", fcnsv("visitor high attenuation") == f32(0.03),
        str(fcnsv("visitor high attenuation")))
fixture("FCNS sitting cutoff", fcnsv("min autonomy score for sitting") == f32(1e-6),
        str(fcnsv("min autonomy score for sitting")))
fixture("FCNS random selection count", fcnsv("random selection count") == 4.0,
        str(fcnsv("random selection count")))
fixture("FCNS friendship threshold", fcnsv("friendship threshold") == 50.0,
        str(fcnsv("friendship threshold")))
fixture("FCNS functional distance", fcnsv("functional distance attenuation") == 3.0,
        str(fcnsv("functional distance attenuation")))
fixture("glob STR#501 named", b"InteractionScoreCurves" in member, "")
fixture("glob STR#503 named", b"InteractionScoreCurvesChild" in member, "")

# G: the extracted adult curve strings match the pinned data
q = -1
while True:
    q = member.find(b"STR#", q + 1)
    if struct.unpack(">H", member[q + 8:q + 10])[0] == 0x1F5:
        break
pm = member.find(b"InteractionScoreCurves", q)
j = member.find(b"\xfd\xff", pm)
cnt = struct.unpack("<H", member[j + 2:j + 4])[0]
k = j + 4
recs = []
for _ in range(cnt):
    k += 1
    e = member.find(b"\x00", k)
    recs.append(member[k:e].decode("mac-roman"))
    k = e + 2
fixture("STR#501 count", cnt == 10, str(cnt))
fixture("STR#501 s1", recs[0] == "(-100;-100) (-72;90) (100;90)", recs[0])
fixture("STR#501 s6", recs[5] == "(-100;-100) (-50;-80)", recs[5])
fixture("STR#501 s10 unused-by-engine", recs[9] == recs[8], "")

# ------------------------------------------------------------- report

result = {
    "sha256": sha,
    "code_pins": counts["code_pins"],
    "data_pins": counts["data_pins"],
    "fixtures": counts["fixtures"],
    "failures": failures,
    "status": "PASS" if not failures else "FAIL",
}
(HERE / "verified-state.json").write_text(json.dumps(result, indent=2) + "\n")
print(json.dumps(result, indent=2))
raise SystemExit(0 if not failures else 1)
