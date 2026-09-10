# R139b — the "another language" garble: variablesans_16.ffn is internally inconsistent

## The user report

"The text is now technically readable, but it looks to be in another
language I don't recognize." (screenshot attached; the analyzer read the
tooltip as mixed Latin/Cyrillic/math glyphs — inverted question marks,
epsilon-like forms — i.e. WRONG GLYPHS from the English table's high
codepoints, not a translation and not the R138 premultiply bug).

## Where it comes from

The garbled surface is the TOOLTIP (UILayer.DrawTooltip), which sets
`style.Size = (int)(8 * DPIScaleFactor)` — on a Retina display that is
16 px, i.e. the `_16` table. Direct-cell comparison proves
`Fonts\variablesans_16.ffn` is the ONE internally inconsistent table in
the corpus:

- 'E' claimed (u=226, v=92, w=12): the cell catches the RIGHT HALF of a
  capital E plus the next glyph's left stem — the true E sits ~11 px
  further right (visible as a clean E at u~237 in the raw atlas).
- 'i' claimed (40, 38): the cell mixes the dot of one glyph row with the
  bottom bar of another.
- 'L' claimed (170, 92): fragments of two different glyph parts.
- "Handgloves" rendered under the port's exact interpretation:
  GARBAGE FRAGMENTS from _16, CLEAN READABLE TEXT from _18 and _20
  (and every other size — 07..14 were already proven by the R96-R119
  renders and the R138/R139 gates).
- The file's structure is otherwise normal (ver 101, rec 0x7a, next=0,
  224 sequential codes 32..255, atlas 256x316, byte count exact,
  256-nibble row periodicity confirmed by zero-run analysis). The
  char-record coordinates simply do not line up with the atlas content.

Hypotheses tested and ELIMINATED: localized-font/archive precedence
(only UIGraphics.far carries .ffn; the user's saved neighborhood is
byte-identical to the template; config LanguageCode=1), neighborhood
string decode (names are empty — captions fall back to "Lot N"),
nib-order swap, row-width mismatch (nibble count forces 256x316),
u/v field swap, constant u-shift (+11 only partially fixes), code-order
packing, _14-order packing, blank-column segmentation (glyphs touch),
connected-component self-test (cannot discriminate — brush strokes
fragment at every size), shape-matching recovery against _14 templates
(confidence too low on the brush font).

## The fix (this round)

`OriginalVectorFont.LadderPx` drops 16: requests 15/16 snap to _14 and
17 to _18 (nearest, ties smaller). The only 16-px callers are
DPI-baked tooltip sizes (8*2), which now render the clean _14 table
native — slightly smaller, fully readable. Gate pins: the ladder
composition (16 must stay out), the 16->14 and 17->18 snaps, and a
per-table "Handgloves" render probe for every ladder table.

## Residual (disclosed)

The engine's own rasterization of these tables (cTSFont::InitBitmapped
0x4b3f00 + the Gimex shape calls at 0x34fa80/0x34fd20/0x34fcc0/
0x34fc60/0x350d60) is not fully decoded; whether the original engine
recovers _16 via ink-extent re-derivation (its runtime records store
x-extents, [entry+8]-[entry+0], per TSCharWidth 0x4acff0) or simply
never requests the 16 px size is undetermined. If exact 16 px rendering
ever matters, that decode is the path.
