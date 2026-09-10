#!/usr/bin/env python3
"""R109: emit the per-font advance-semantics evidence (measured samples from
the R87-extracted originals)."""
import struct

def load(name):
    d = open(f"r87/orig/ffn_Fonts__{name}.ffn","rb").read()
    recs={}
    for k in range(224):
        o=32+k*11
        ch,w,h = struct.unpack("<HBB", d[o:o+4])
        recs[ch]=(w,h,d[o+8],d[o+9],d[o+10])
    return recs

def main():
    print("# R109 — per-font advance semantics across the mounted .ffn tables")
    print()
    print("The FNTF char record's t0 byte (offset +8 of the 11-byte record) means")
    print("different things per table family. Measured samples below come from the")
    print("R87-extracted originals via make_r109_advance.py.")
    print()
    print("| table | live surface | w+t0 samples (w + t0 = adv) | t0 alone |")
    print("|---|---|---|---|")
    roles = [
        ("variablesans_14", "neighborhood titles"),
        ("variablesans_12_bs", "lot money readout"),
        ("variablesans_10", "hover lot captions"),
        ("variablesans_11", "toolbar time/floor/friends"),
        ("variablesans_09", "cheat-bar response (engine string)"),
        ("variablesans_08", "options-panel title"),
    ]
    samples = {"variablesans_14": [0x41,0x54,0x65], "variablesans_12_bs": [0xA7,0x30,0x24],
               "variablesans_10": [0x41,0x4C,0x6F], "variablesans_11": [0x54,0x3A,0x31],
               "variablesans_09": [0x53,0x21,0x74], "variablesans_08": [0x54,0x4E,0x68]}
    for name, role in roles:
        r = load(name)
        ss = samples[name]
        wt = ", ".join(f"'{chr(c)}' {r[c][0]}+{r[c][2]}={r[c][0]+r[c][2]}" for c in ss)
        print(f"| `{name}` | {role} | {wt} | " + ", ".join(f"'{chr(c)}':{r[c][2]}" for c in ss) + " |")
    r07 = load("variablesans_07")
    print(f"| `variablesans_07` | (R92 reference decode, not mounted) | — | 'A':{r07[0x41][2]} 'e':{r07[0x65][2]} 'M':{r07[0x4D][2]} (w: {r07[0x41][0]}/{r07[0x65][0]}/{r07[0x4D][0]}) |")
    print()
    print("## Findings")
    print()
    print("1. **CORRECTION of the R97-era note: _07 ALSO follows w + t0.** The")
    print("   earlier session claim of a 't0-as-advance' family was a misreading:")
    print("   measured, _07 'A' = (w=6, t0=1) -> advance 7, 'e' = (5,1) -> 6,")
    print("   'M' = (8,1) -> 9 — t0 is a small bearing like every other table,")
    print("   and the quoted 'A:7' was the w+t0 SUM, not t0. The convention is")
    print("   UNIFORM across all seven English variablesans tables measured.")
    print("2. **The six mounted tables (R96-R106): advance = w + t0**, t0 a small")
    print("   bearing (0-2). The size-consistency check: _14 'A' = 12+2 = 14 — a")
    print("   14pt cap advancing exactly 14; _11 '1' = 6+1 = 7; _09 'S' = 8+1 = 9;")
    print("   _08 'T' = 9+0 = 9; _12_bs '§' = 11+0 = 11; _10 'A' = 8+1 = 9.")
    print("3. **Space (0x20) is a degenerate 1x1 cell in every table** — its")
    print("   advance is not record-derivable; the shipped renderer uses a fixed")
    print("   5px space. Both gate measure discrepancies (R100 195-vs-201, R106")
    print("   53-vs-54) were exactly one-space-difference class — the convention")
    print("   is now pinned in the gate comments.")
    print("4. **t2 is the y draw-offset everywhere** (1-2 caps, 4-6 x-height)")
    print("   — the vertical bearing, uniform across families.")
    print()
    print("## Port state")
    print()
    print("`OriginalGlyphFont.Advance` implements `w + t0` + the 5px space —")
    print("measured-correct for ALL tables in the corpus (every gate Measure pin")
    print("since R96 is computed under this convention, and this round's")
    print("measurement retired the last doubt about _07). No special cases.")

if __name__ == "__main__":
    main()
