# R139 — the text-residuals round (resolving every R138 disclosure)

User directive: "resolve the residuals" — all three R138 disclosures:

1. flags=11 nibble scaling (bold ink ceiling) — undecoded
2. chars > 0xFF silently dropping (0 advance)
3. off-ladder px sizes rendering linearly-filtered (soft)

## The engine decode (The Sims Complete, PPC)

The CW end-of-function symbol trailers name the whole font subsystem.
Function start = (len-field address) − 16 − size(u32 at −4):

| function | A | start | size |
|---|---|---|---|
| cTSFont::TSCharWidth | 0x4ad020 | 0x4acff0 | 0x20 |
| cTSFont::IsCharValid | 0x4acf50 | 0x4acf10 | 0x30 |
| cTSFont::GetCharDims | 0x4acc84 | 0x4acc10 | 0x64 |
| cTSFont::TSDrawText | 0x4adf1c | 0x4adc60 | 0x2ac |
| cTSFont::TSDrawChar | 0x4b3d44 | 0x4b3420 | 0x914 |
| cTSFont::RebuildColorTable | 0x4b33f8 | 0x4b31d0 | 0x218 |
| cTSFont::InitBitmapped | 0x4b48cc | 0x4b3f00 | 0x9bc |
| Blend555_2(u32,u32,u32) | 0x44b4a0 | 0x44b440 | 0x60 |
| Blend565_2(u32,u32,u32) | 0x451de0 | 0x451d80 | 0x60 |
| InitAlphaZSpriteBlit | 0x1a0ce8 | 0x1a0cd0 | 0x4d0 |

### Residual 1 — the nibble law (RESOLVED: nibble-as-is, nib*17 is faithful)

- TSCharWidth 0x4acff0: `entry = this[16] + (c & 0xFF) * 28; width =
  [entry+8] − [entry+0]` — the runtime char table is 256 slots of 28
  bytes, indexed DIRECTLY by the byte value. The engine only ever looks
  at `c & 0xFF`; there is no Unicode anywhere in its text path.
- RebuildColorTable 0x4b31d0: rebuilds this[64] (a color translation
  array, count cached at this[60] = X[36]) from this[20] (the CURRENT
  FONT COLOR, set by SetFontColor) and a global LUT at [TOC−28288].
  Entries whose two source bytes are equal map to the SOLID font color;
  unequal pairs go through the LUT blend path. NO font-table field — no
  flags, no nibble max, no glyph data — enters this function.
- The LUT at [TOC−28288] is BSS-proven: the TOC entry V=0x864a0 is
  ambiguous-range, and the code-rel candidate at file 0x8f330 decodes as
  PPC instruction stream (branches/calls) — runtime-built table.
- The LUT's consumers (42 lwz references, mapped to containing
  functions): Spr2Decoder{Flip,Clip}{555,565}, WaterSpr2Decoder*,
  PalettedBlend_*, MorphBlend*, AlphaBlend_{555,565}_P5,
  Blend555_2/Blend565_2, ApplyLogo, Compose2, cWinRelationship::TSPaint,
  cWinGift::TSPaint, Credit::Draw, InitAlphaZSpriteBlit — i.e. the ONE
  GLOBAL blend table shared by sprites, water, alpha and the FONT path.
- Blend555_2 0x44b440 (24 instructions, pure): per 5-bit channel,
  `out_c = LUT[amount<<10 ...][...]` with the result bytes reassembled
  as a 15-bit color — the blend law is a two-endpoint per-channel table
  per amount, shared engine-wide.

CONCLUSION: the engine CANNOT normalize per font — the 16 blend levels
are global. A flags=11 table's nibble 13 renders as blend level 13 of
15; its ink ceiling (13*17 = 221/255 in the port's linear coverage
mapping) is CANON, not a bug. The port's nib*17 stands as the
engine-faithful interpretation; the exact per-level 5-bit quantization
curve lives in the BSS LUT and is unrecoverable (disclosed) — the port
renders 8-bit linear coverage, strictly finer than the engine's 5-bit
quantized curve, with the same ceiling.

### Residual 2 — chars > 0xFF (RESOLVED: CP1252 boundary conversion)

The engine's table is 256 slots indexed by (c & 0xFF): every byte maps
to a record; unmapped bytes are blank zero-width slots (width = 0−0).
The engine's strings were Windows-1252 BYTES (IoBuffer raw byte→char on
the port side matches: byte 0x93 = the curly-quote glyph).

Port fix (engine-faithful): OriginalGlyphFont.MapChar maps the 27
standard CP1252 Unicode equivalents (U+201C→0x93, U+2019→0x92,
U+2026→0x85, ...) at the font boundary, so a port-authored Unicode curly
quote renders exactly the glyph the original drew for byte 0x93. Chars
with no CP1252 byte keep the engine's zeroed-slot behavior: blank,
zero advance.

### Residual 3 — off-ladder softness (RESOLVED: native rendering)

TSDrawChar 0x4b3420 blits the glyph records' own pixel extents
(mulli r26, ch, 28; reads [entry+24]; clip rect from the cTSRect arg) —
the engine NEVER scales bitmap glyphs. Every engine surface renders its
table 1:1.

Port fix: OriginalVectorFont.SelectForSize now returns a wrapper whose
VectorScale = 11.5/pxRequest, so TextStyle.Size computes Scale == 1.0
EXACTLY for every request — on-ladder requests are bit-identical to
before (11.5/table == 11.5/request), off-ladder requests render the
snapped table CRISP AND UNSCALED (no filtering), and
MeasureString().Y * Scale keeps the same (tablePx+3)*px/11.5 line
height as before, so panel layouts do not move.

## Evidence files

- r139-ischarvalid.txt — IsCharValid + TSCharWidth decodes
- r139-rebuildcolortable.txt — the color-translation rebuild
- r139-blend555.txt / r139-blend565.txt — the pure blend-law functions
- r139-initalphaz.txt / r139-initspr2.txt — LUT consumer context
- r139-tsdrawchar.txt / r139-initbitmapped.txt — the blit and FNTF load
- r139-textdiag.txt — the -textdiag harness rerun on the fixed build
  (unicode specials now render: ink 612 vs 523 before the boundary map;
  UILabel size=13 native: 204 solid pixels vs 8 when scaled)
- r139p1.log — the full gate

## Gate (uitext v2, still 80 checks — the check EXTENDS in place)

Added to CheckText:
- bold ink ceiling: the _12_bs render's max alpha must be 13*17 = 221
  (±2 GPU rounding) — machine-pins the nibble-as-is law
- CP1252 boundary: MapChar pins (U+201C→0x93, U+2019→0x92, identity),
  Measure equality between the Unicode and byte-coded strings, and a
  pixel-equality render probe of "\u201CHand\u201D" vs "\x93Hand\x94"
- native scale: Size=13 → table 12 @ Scale 1.0; Size=6 → table 7
  (nearest, ties smaller); Size=12 → table 12 @ 1.0

## Residuals remaining (disclosed)

- The blend LUT's exact per-level 5-bit curve (BSS, unrecoverable).
- The 4 shape-record header bytes at shapeOff+8..11 are zero in every
  English table; their intended meaning is unnamed (the port skips
  them, as does the engine's rasterizer per the nibble-stream layout).
