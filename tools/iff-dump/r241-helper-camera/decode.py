#!/usr/bin/env python3
"""R241: annotated PPC decode of the ORIGINAL executable for the secondary
camera wall/cutaway and fade questions.

Addresses are RAW FILE OFFSETS into the PEF (verified against r240-pip
evidence: symbol-index addresses are +2; this tool takes file offsets).
Call targets are annotated from r141/symbol-index.txt using the +2
convention (symbol address - 2 = file offset).

Usage: python3 decode.py 0xSTART 0xEND [--data]
"""
import re
import struct
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
BIN = ROOT / 'game-data/The Sims/The Sims Complete'
IDX = ROOT / 'tools/iff-dump/r141/symbol-index.txt'
sys.path.insert(0, str(ROOT / 'tools/iff-dump'))
import ppc_decode  # noqa: E402


def load_symbols():
    syms = {}
    for line in IDX.read_text().splitlines():
        m = re.match(r'^([0-9a-f]{8})\s+(\d+)\s+(\.\S+)', line.strip())
        if m:
            syms[int(m.group(1), 16) - 2] = (int(m.group(2)), m.group(3)[1:])
    return syms


def main():
    start, end = int(sys.argv[1], 0), int(sys.argv[2], 0)
    syms = load_symbols()
    data = BIN.read_bytes()
    for a in range(start, min(end, len(data) - 3), 4):
        w = struct.unpack_from('>I', data, a)[0]
        d = ppc_decode.decode(w, a)
        note = ''
        if (w >> 26) == 18 and (w & 1):  # bl
            t = int(d.split()[-1], 16)
            if t in syms:
                note = f"  ; .{syms[t][1]}"
            else:
                note = '  ; .sub_%x' % t
        elif (w >> 26) == 18:
            t = int(d.split()[-1], 16)
            if t in syms:
                note = f"  ; -> .{syms[t][1]}"
            else:
                note = '  ; -> %x' % t
        txt = bytes(data[a:a + 4])
        asc = ''.join(chr(c) if 32 <= c < 127 else '.' for c in txt)
        print(f"{a:#08x}: {w:08x}  {(d if d else '.long'):42s}{note} |{asc}|")


if __name__ == '__main__':
    main()
