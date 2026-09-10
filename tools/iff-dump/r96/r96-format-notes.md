# R96 — the FNTF (.ffn) glyph-table format, decoded

Sources: the EA FFN 010 binary template (bartlomiejduda/Tools,
"EA_FONT_SFN_FFN_XFN_MFN.bt", GPL) for the field skeletons, then empirical
verification against the R87-extracted originals (r87/orig/ffn_*.ffn;
verification scripts + ASCII renders in the git history of this round).

## Header (32 bytes, LITTLE-endian)
    +0x00 char[4] "FNTF"
    +0x04 u32 total file size
    +0x08 u16 version (101 = 1.01 in the shipped tables)
    +0x0A u16 numChars
    +0x0C u32 flags
    +0x10 u8 center_x, u8 center_y, u8 ascent, u8 descent
    +0x14 u32 charTableOffset   (32 in every shipped table)
    +0x18 u32 kerningOffset     (0 — none shipped)
    +0x1C u32 shapeOffset       (2496 = 32 + 11*224 in every shipped table)

NOTE (the R96 port bug this round is evidence for): the shape offset is at
+0x1C, i.e. bytes 28-31 — NOT 24-27 (that is the kerning field, 0). Reading
it from 24 gives shapeOff=0 and the "atlas" parses as (fileSize x 0).

## Char table (numChars x 11 bytes, LE)
    +0 u16 charCode (0x20..0xFF in the shipped tables)
    +2 u8  width     (px; 1 for space's degenerate cell)
    +3 u8  height    (px)
    +4 u16 u         atlas x of the glyph cell
    +6 u16 v         atlas y of the glyph cell
    +8 i8  t0        (advance for the 07pt table; bearing-like for the 14pt
                     table where advance = width + t0 gives coherent metrics;
                     DISCLOSED interpretation)
    +9 i8  t1
    +10 i8  t2       y draw offset (1-2 caps, 5-6 x-height glyphs) — both fonts

Cell verification: ink starts exactly at (u,v) for 156/224 glyphs of the 07pt
table and 198/224 of the 14pt (the rest are natural side bearings).

## Shape record (at shapeOffset)
    +0 u8 recordId (0x7A = not compressed)
    +1 u8[3] nextAttachmentOffset
    +4 u16 atlasW  (+6 u16 atlasH)
    +8 i16 center_x, +10 i16 center_y
    +12 4-BIT grayscale atlas, high nibble first, rows byte-aligned
        (atlasW*atlasH/2 bytes + up to 4 pad). Value 0 = transparent,
        15 = full ink; the ramp is the antialiasing.
    e.g. variablesans_07: 256x84 (10,768B pool = 10,752 + pad)
         variablesans_14: 256x224 (28,676B pool)

Total pixel check: sum(w*h) over the table == pool size minus pad for both
samples (9,745 vs 10,768-... etc.) — the atlas contains ONLY glyph cells.

## Ported as
Simitone.Client.UI.Controls.OriginalGlyphFont (+UIOriginalText): parses the
table, builds a white-on-transparent Texture2D atlas (alpha = nibble*17),
measures/draws strings per-cell. Live surface: the neighborhood-screen
titles (UIOriginalText, placement new, style = original data). Gate: uiglyph
now parses the canon-bytes font (224 glyphs, atlas 256x224, exact 'A'/'e'
records, Measure("The Sims")==88) and renders "The Sims" headlessly
(ink>200px, >=8 alpha levels).
