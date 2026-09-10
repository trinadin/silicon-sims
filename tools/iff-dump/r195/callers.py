#!/usr/bin/env python3
"""R195: PPC bl caller-scan. Scans the whole text section for `bl` sites
hitting a set of function starts and prints the enclosing symbol.

The displacement formula (verified against capstone on 0x4bffe47d ->
0x257a60 and 0x48349441 -> 0x5a29e0): for a b/l-form word w,
  d = w & 0x03FFFFFC           (the LI field stays in place, LSB bit 2)
  if d & 0x02000000: d -= 0x04000000   (26-bit sign extension)
  target = site + d
No shifts. Earlier rounds' "shift then mask" variants are wrong.

Symbol-index quirk: some entries are start+2, some are the real start.
Candidate start = addr & ~3 (capdis-friendly)."""
import bisect
import sys

BIN = '/Users/nathannoom/Developer/Games/The Sims/simitone-fork/game-data/The Sims/The Sims Complete'
IDX = '/Users/nathannoom/Developer/Games/The Sims/simitone-fork/tools/iff-dump/r141/symbol-index.txt'
TEXT_END = 0x5c22f0   # start of the PEF data-section container


def load_syms():
    syms = []
    for line in open(IDX):
        p = line.split()
        if len(p) >= 3:
            try:
                syms.append((int(p[0], 16) & ~3, int(p[1]), p[2]))
            except ValueError:
                pass
    syms.sort()
    return syms


def main(targets):
    """targets: {aligned_start: label}"""
    data = open(BIN, 'rb').read()
    syms = load_syms()
    starts = [s[0] for s in syms]

    def enc(a):
        i = bisect.bisect_right(starts, a) - 1
        if i >= 0 and a < syms[i][0] + syms[i][1] + 2:
            return f'{syms[i][2]}+0x{a - syms[i][0]:x}'
        return f'?+0x{a:x}'

    for a in range(0, TEXT_END, 4):
        w = int.from_bytes(data[a:a + 4], 'big')
        if (w & 0xFC000003) == 0x48000001:
            d = w & 0x03FFFFFC
            if d & 0x02000000:
                d -= 0x04000000
            t = a + d
            if t in targets:
                print(f'{targets[t]:24s} <- {enc(a)} @0x{a:x}')


if __name__ == '__main__':
    # default: the R195 widget-factory scan (no direct callers — all
    # cITSWinGenDlg dispatch is virtual)
    main({
        0x519520: 'GenDlg::AddCheck',
        0x5197e0: 'GenDlg::Scrollbar',
        0x519970: 'GenDlg::AddSlider',
        0x50f530: 'Mgr::DefScrollbarH',
        0x50f670: 'Mgr::DefScrollbarV',
        0x50f7b0: 'Mgr::DefSliderH',
        0x50f8f0: 'Mgr::DefSliderV',
        0x50fd20: 'Mgr::DefCheckBox',
    })
