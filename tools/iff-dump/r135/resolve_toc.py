#!/usr/bin/env python3
"""R135: the TOC resolver (the R134 recipe, toolized).

PEF layout (established): sec0 CODE at file 0x8e90 (uncompressed);
sec1 DATA PACKED, unpacked size 0x7bf80 + bss zero-fill to 0x989a4
(r104/sec1-unlocked.bin = sec1-unpacked.bin). r2 = TOC base + 0x8000
where TOC base = sec1 start. A `[TOC - X]` reference therefore reads the
TOC ENTRY at sec1_unpacked[0x8000 - X]; that entry's u32 value V is a
section-relative pointer:
  * V == 0            -> empty slot
  * V >= 0x7bf80      -> bss (runtime-only, unrecoverable)
  * else AMBIGUOUS code-rel vs sec1-rel: dump BOTH candidates (code file
    offset 0x8e90+V as raw; sec1_unpacked[V]) and let the reader judge.
Dumps 16 bytes around each target as u32s, f32s and f64 (both alignments).
"""
import struct, os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
IFFDUMP = os.path.dirname(HERE)
BIN = os.path.join(IFFDUMP, '../../game-data/The Sims/The Sims Complete')
SEC1 = os.path.join(IFFDUMP, 'r104/sec1-unpacked.bin')

code = open(BIN, 'rb').read()
sec1 = open(SEC1, 'rb').read()
SEC1_SIZE = 0x7bf80
CODE_OFF = 0x8e90
TOC_R2 = 0x8000  # r2 = sec1_base + 0x8000

WANT = [
    # label, X (the [TOC - X] magnitude)
    ('upkeepA_FillIn', 29328),
    ('upkeepB_FillIn', 29332),
    ('gaugeLimits', 20376),
    ('gauge_20380', 20380),
    ('gaugeOffsets', 20400),
    ('dblK_23512', 23512),
    ('dblK_23516', 23516),
    ('houseInit_lang_a', 20228),
    ('houseInit_lang_b', 20236),
    ('segTable_22604', 22604),
    ('trackGlobal_28856', 28856),
    ('yardGlobal_29456', 29456),
    ('yardTable_29516', 29516),
    ('yardTable_29520', 29520),
    ('poolTable_29524', 29524),
    ('gamestateRoot_30604', 30604),
    ('gamestate_30608', 30608),
    ('gamestate_30632', 30632),
    ('limit_30624', 30624),
]


def fmt_region(buf, off, n=4):
    out = []
    for i in range(n):
        w = int.from_bytes(buf[off + 4 * i:off + 4 * i + 4], 'big')
        f = struct.unpack('>f', buf[off + 4 * i:off + 4 * i + 4])[0]
        out.append('    +%02x: %08x  u32=%-10d f32=%.6g' % (4 * i, w, w if w < 0x80000000 else w - (1 << 32), f))
    d = struct.unpack('>d', buf[off:off + 8])[0]
    out.append('    f64@+0 = %.17g' % d)
    return '\n'.join(out)


print('== TOC resolution (entry at sec1+0x8000-X; V = section-rel ptr) ==')
# R135 disambiguation verdicts (see r135-toc-resolution.txt for the raw dumps):
#  * V >= 0x7bf80 read CODE-REL (file 0x8e90+V) is the right interpretation
#    when the target region is a FLOAT/DOUBLE CONST POOL (clean data words).
#    Four hits: [TOC-20376] -> file 0x5a45ec (the GAUGE pool: +0x14 0.0,
#    +0x18 0.5, +0x1c 11, +0x20 100, +0x24 201, +0x28 5, plus 0.75/8/15/
#    -25/25/176 earlier), [TOC-20380] -> file 0x5a4618 (2.0f, 1/3f + two
#    int->double magic pairs), [TOC-23512] -> 0x5a2fb4 (0/1.0/100.0/2.25),
#    [TOC-23516] -> 0x5a2fc0 (double 4.0 + magic pair).
#  * V >= 0x7bf80 pointing at INSTRUCTION STREAMS when read code-rel
#    (mid-prologue nonsense like '93e1fffc 7c0802a6') proves the slot is
#    BSS (runtime-only): upkeepA/B [TOC-29328/29332], gaugeOffsets
#    [TOC-20400], trackGlobal [TOC-28856], yard [TOC-29456/29516/29520/
#    29524], gamestate root [TOC-30604/30608], HouseInit lang tables.
for label, X in WANT:
    eo = TOC_R2 - X
    if eo < 0 or eo + 4 > len(sec1):
        print('%-22s [TOC-%d]: entry OUT OF RANGE' % (label, X))
        continue
    V = int.from_bytes(sec1[eo:eo + 4], 'big')
    print('%-22s [TOC-%d] entry@sec1+%04x V=0x%08x (%d)' % (label, X, eo, V, V))
    if V == 0:
        print('    EMPTY SLOT')
        continue
    if V >= SEC1_SIZE:
        print('    BSS (>= 0x7bf80) — runtime-only')
        continue
    # candidate A: code-relative
    co = CODE_OFF + V
    if co + 16 <= len(code):
        print('  [code-rel: file 0x%x]' % co)
        print(fmt_region(code, co))
    # candidate B: sec1-relative
    if V + 16 <= len(sec1):
        print('  [sec1-rel: sec1+0x%x]' % V)
        print(fmt_region(sec1, V))
    print()
