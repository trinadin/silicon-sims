#!/usr/bin/env python3
"""R153 task 1/d foundation: build the function symbol map from the CodeWarrior
traceback records embedded in sec0 of the PEF.

File layout (verified this round):
  sec0 = Code,  raw,            file [0x8E90 .. 0x5C22E8)   (doc addresses)
  sec1 = Data (PID), packed,    file [0x5C22F0 ..) -> r104/sec1-unpacked.bin
  sec2 = Debug,                 file [0x80 .. 0x8E84)
After each function's code, a trailing record holds its mangled name:
  ... <hdr bytes> <L> '.'name<L-1 chars> <NUL> <zero pad> <next function>
e.g. 0x1118e1: L=0x23 ".RandomizeAllInterests__8cXPersonFv" then zeros,
then Reset__8cXPersonFUc begins at 0x111910 (byte-verified).

Usage: r153-pef-symbols.py            (writes r153-symbols.txt/.json)
"""
import json
import re
import sys

BIN = "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/game-data/The Sims/The Sims Complete"
OUTD = "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/tools/iff-dump/r153"
SEC0_OFF = 0x8E90
SEC0_LEN = 0x5B9458

b = open(BIN, "rb").read()

recs = []  # (len_byte_pos, name)
p = SEC0_OFF
end = SEC0_OFF + SEC0_LEN
while p < end - 8:
    L = b[p]
    if 8 <= L <= 200 and b[p + 1] == 0x2E:  # '.'-prefixed pascal-ish string
        s = b[p + 2 : p + 1 + L]
        if len(s) == L - 1:  # ctor records may lack the trailing NUL
            if all(0x21 <= c <= 0x7E for c in s):
                name = s.lstrip(b".").decode("ascii")
                if re.match(r"^[A-Za-z_~][A-Za-z0-9_]*", name):
                    recs.append((p, name))
                    p += 1 + L
                    continue
    p += 1

# function map: record i names the code that ENDS at its record; the next
# function starts at the first nonzero byte after the record's NUL.
funcs = []  # (start, name, record_pos)
for i, (rp, name) in enumerate(recs):
    L = b[rp]
    after = rp + 1 + L  # position of NUL
    q = after + 1
    while q < end and b[q] == 0:
        q += 1
    funcs.append((q, name, rp))

# function start sanity: must be 4-aligned code-ish start; and start of the
# function named by record i is q from record i-1.
starts = []
for i, (q, name, rp) in enumerate(funcs):
    starts.append((q, name))

# Also: the FIRST function starts at SEC0_OFF (no preceding record).
# Build name -> start via previous record.
sym = []
for i, (q, name, rp) in enumerate(funcs):
    # record i's own function body = [funcs[i-1].end_pad .. rp]
    if i == 0:
        start = SEC0_OFF
    else:
        start = funcs[i - 1][0]
    sym.append((start, rp, name))

with open(f"{OUTD}/r153-symbols.json", "w") as f:
    json.dump([{"start": s, "rec": r, "name": n} for s, r, n in sym], f)

with open(f"{OUTD}/r153-symbols.txt", "w") as f:
    f.write(f"# {len(sym)} symbol records found in sec0 [{SEC0_OFF:#x}..{SEC0_OFF+SEC0_LEN:#x})\n")
    f.write("# start = first byte of function (after previous record's pad); rec = position of its trailing name record\n")
    for s, r, n in sym:
        f.write(f"{s:#08x}  rec@{r:#08x}  {n}\n")

print(f"{len(recs)} records; wrote r153-symbols.txt/.json")
# sanity anchors from prior rounds
anchors = {
    0x111650: "RandomizeAllInterests",
    0x111910: "Reset__8cXPersonFUc",
    0x112014: "Initialize__8cXPersonFv",
    0x112200: "ctor __ct__8cXPerson",
    0x0B4A80: "LoadInterestData",
    0x0A6410: "ChangeSimBaseType",
    0x1411D0: "LCG",
    0x405B10: "cWinInterest::SetInterest",
}
m = {}
for s, r, n in sym:
    m.setdefault(s, n)
for a, want in anchors.items():
    got = m.get(a, "<none>")
    flag = "OK " if want.split("__")[0].split("::")[0][:8].lower() in got.lower() else "???"
    print(f"  {flag} {a:#x} -> {got[:70]}  (expect ~{want})")
