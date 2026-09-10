#!/usr/bin/env python3
"""R154: scan the original engine PEF ('The Sims Complete', PPC) for any
engine-side reference to tree ids 9761 / 39200.

Vectors checked:
  1. direct-call xref of cXObject::RunTree (0x0efd30) and
     Behavior::GetTree(short id) (0x0407b0) — every bl/b site + enclosing symbol.
  2. whole sec0 scan for PPC load-immediates of the constants:
     addi/li rD,r0,imm (op 14) and ori rD,rX,imm (op 24), imm in {9761, 39200}.
  3. whole-file raw scan for the 32-bit big-endian constants 0x2621 / 0x9920.

Usage: r154-engine-constscan.py   (prints to stdout + r154-engine-constscan.txt)
"""
import bisect
import json
import os
import struct

HERE = os.path.dirname(__file__)
BIN = os.path.normpath(os.path.join(HERE, '..', '..', '..', 'game-data', 'The Sims', 'The Sims Complete'))
SYMS = os.path.normpath(os.path.join(HERE, '..', 'r153', 'r153-symbols.json'))
SEC0_OFF, SEC0_LEN = 0x8E90, 0x5B9458

b = open(BIN, 'rb').read()
syms = json.load(open(SYMS))
m = {s['start']: s['name'] for s in syms}
starts = sorted(m)


def fn(off):
    i = bisect.bisect_right(starts, off) - 1
    return '<pre>' if i < 0 else (m[starts[i]] + ('' if starts[i] == off else '+%#x' % (off - starts[i])))


out = open(os.path.join(HERE, 'r154-engine-constscan.txt'), 'w')

def P(s=''):
    print(s)
    out.write(s + '\n')


CONSTS = {8486: 0x2126, 8345: 0x2099}
RUNTREE = 0x0efd30
GETTREE = 0x0407b0

# --- 1. xref RunTree / GetTree ---
n = SEC0_LEN // 4
words = struct.unpack_from('>%dI' % n, b, SEC0_OFF)
xref = {RUNTREE: [], GETTREE: []}
for i, w in enumerate(words):
    if (w >> 26) != 18:
        continue
    li = (w >> 2) & 0xFFFFFF
    if li & 0x800000:
        li -= 0x1000000
    tgt = SEC0_OFF + 4 * i + li * 4
    if tgt in xref:
        xref[tgt].append((SEC0_OFF + 4 * i, w, w & 1))

for t, nm in ((RUNTREE, 'cXObject::RunTree(Behavior*,char const*,short*)'), (GETTREE, 'Behavior::GetTree(short)')):
    P('== xref %s @%#x: %d sites' % (nm, t, len(xref[t])))
    for a, w, lk in xref[t]:
        P('   %s from %#08x in %s' % ('bl' if lk else 'b ', a, fn(a)))
    P('')

# --- 2. load-immediates of the constants in sec0 ---
P('== load-immediate scan (addi/li + ori) for %s' % list(CONSTS))
hits = 0
for i, w in enumerate(words):
    op = w >> 26
    if op in (14, 24):
        imm = w & 0xFFFF
        if imm in CONSTS:
            a = SEC0_OFF + 4 * i
            d = (w >> 21) & 31
            hits += 1
            P('   %#08x %s r%d, %d (%s) in %s' % (a, 'li/addi' if op == 14 else 'ori', d, imm, hex(imm), fn(a)))
P('   total: %d' % hits)
P('')

# --- 2b. context around each addi hit (10 instrs back) ---
P('== context (8 words) before each li site')
for i, w in enumerate(words):
    op = w >> 26
    if op in (14, 24) and (w & 0xFFFF) in CONSTS:
        a = SEC0_OFF + 4 * i
        P('   at %#08x (%s):' % (a, fn(a)))
        for j in range(max(0, i - 8), i + 2):
            P('     %#08x: %08x' % (SEC0_OFF + 4 * j, words[j]))
P('')

# --- 3. raw 32-bit constants anywhere in the file ---
P('== raw 32-bit BE constant scan (whole file)')
for c in CONSTS.values():
    pat = struct.pack('>I', c)
    cnt = 0
    idx = b.find(pat)
    locs = []
    while idx != -1 and cnt < 40:
        locs.append(idx)
        cnt += 1
        idx = b.find(pat, idx + 1)
    P('   0x%08x: %d raw hits' % (c, cnt))
    for l in locs[:40]:
        insec = 'sec0/code' if SEC0_OFF <= l < SEC0_OFF + SEC0_LEN else 'other'
        P('     @%#09x (%s) ctx=%s' % (l, insec, b[l-8:l+12].hex(' ')))
P('')

# --- 4. any li of the constants near RunTree call sites ---
P('== RunTree call sites with constant li within 12 instrs before')
for t in (RUNTREE, GETTREE):
    for (a, w, lk) in xref[t]:
        i = (a - SEC0_OFF) // 4
        near = [words[j] for j in range(max(0, i - 12), i)]
        if any((x >> 26) in (14, 24) and (x & 0xFFFF) in CONSTS for x in near):
            P('   %#08x in %s' % (a, fn(a)))
P('(end)')
out.close()
print('wrote', out.name)
