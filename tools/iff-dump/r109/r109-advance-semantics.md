# R109 — per-font advance semantics across the mounted .ffn tables

The FNTF char record's t0 byte (offset +8 of the 11-byte record) means
different things per table family. Measured samples below come from the
R87-extracted originals via make_r109_advance.py.

| table | live surface | w+t0 samples (w + t0 = adv) | t0 alone |
|---|---|---|---|
| `variablesans_14` | neighborhood titles | 'A' 12+2=14, 'T' 13+2=15, 'e' 11+1=12 | 'A':2, 'T':2, 'e':1 |
| `variablesans_12_bs` | lot money readout | '§' 11+0=11, '0' 11+0=11, '$' 12+0=12 | '§':0, '0':0, '$':0 |
| `variablesans_10` | hover lot captions | 'A' 8+1=9, 'L' 8+0=8, 'o' 7+0=7 | 'A':1, 'L':0, 'o':0 |
| `variablesans_11` | toolbar time/floor/friends | 'T' 11+1=12, ':' 3+1=4, '1' 6+1=7 | 'T':1, ':':1, '1':1 |
| `variablesans_09` | cheat-bar response (engine string) | 'S' 8+1=9, '!' 2+1=3, 't' 6+0=6 | 'S':1, '!':1, 't':0 |
| `variablesans_08` | options-panel title | 'T' 9+0=9, 'N' 9+0=9, 'h' 6+1=7 | 'T':0, 'N':0, 'h':1 |
| `variablesans_07` | (R92 reference decode, not mounted) | — | 'A':1 'e':1 'M':1 (w: 6/5/8) |

## Findings

1. **CORRECTION of the R97-era note: _07 ALSO follows w + t0.** The
   earlier session claim of a 't0-as-advance' family was a misreading:
   measured, _07 'A' = (w=6, t0=1) -> advance 7, 'e' = (5,1) -> 6,
   'M' = (8,1) -> 9 — t0 is a small bearing like every other table,
   and the quoted 'A:7' was the w+t0 SUM, not t0. The convention is
   UNIFORM across all seven English variablesans tables measured.
2. **The six mounted tables (R96-R106): advance = w + t0**, t0 a small
   bearing (0-2). The size-consistency check: _14 'A' = 12+2 = 14 — a
   14pt cap advancing exactly 14; _11 '1' = 6+1 = 7; _09 'S' = 8+1 = 9;
   _08 'T' = 9+0 = 9; _12_bs '§' = 11+0 = 11; _10 'A' = 8+1 = 9.
3. **Space (0x20) is a degenerate 1x1 cell in every table** — its
   advance is not record-derivable; the shipped renderer uses a fixed
   5px space. Both gate measure discrepancies (R100 195-vs-201, R106
   53-vs-54) were exactly one-space-difference class — the convention
   is now pinned in the gate comments.
4. **t2 is the y draw-offset everywhere** (1-2 caps, 4-6 x-height)
   — the vertical bearing, uniform across families.

## Port state

`OriginalGlyphFont.Advance` implements `w + t0` + the 5px space —
measured-correct for ALL tables in the corpus (every gate Measure pin
since R96 is computed under this convention, and this round's
measurement retired the last doubt about _07). No special cases.
