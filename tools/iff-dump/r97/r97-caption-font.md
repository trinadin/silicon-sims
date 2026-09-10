# R97 — the second live .ffn table: the caption font

`Fonts\variablesans_10.ffn` (18,000 bytes, R87 canon) now renders the
neighborhood hover lot captions alongside the R96 title table
(variablesans_14). Same FNTF layout (see ../r96/r96-format-notes.md):

- 224 glyphs, char table at 32, shape at 2496
- atlas 256x121, 4-bit grayscale (pool 15,492 = 15,488 + 4 pad)
- pin records: 'L' (w=8 h=12 u=162 v=42 t=(0,0,1)), 'o' (w=7 h=8 u=168
  v=8 t=(0,0,4)) — note this table's t0 = 0 (advance = width directly;
  the third distinct advance convention after _07's t0 and _14's w+t0 —
  still disclosed in OriginalGlyphFont.Advance).
- Measure("Lot 20") = 8+7+7+5+8+8 = 43 (gate-pinned)

Live surface: UINeighborhoodHouseButton hover -> the panel shows
UIOriginalText at (14, 566) with GetHouseNameDesc(n).Item1 (the
neighborhood STR lot/family name — the same records the original screens
read), falling back to "Lot N". The caption mounts lazily on first hover
(headless flow: hoverCaptions=0 is expected).

Gate: uiglyph now parses BOTH tables canon-verified-on-first-read and
renders "The Sims" (_14) and "Lot 20" (_10) headlessly with ink +
antialias-level assertions. r97p1: 59/59 FIRST-TRY.
