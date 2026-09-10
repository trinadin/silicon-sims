# R138 — the text-rendering round (user-reported garbled/unreadable text)

## The report

"We still have issues with garbled appearing text. Or text that doesn't
render properly. Across the board text is unreadable."

## Root cause (proven, not suspected)

`OriginalGlyphFont.FromFFN` (Client/Simitone/Simitone.Client/UI/Controls/
UIOriginalText.cs) uploaded the .ffn glyph atlas as **white RGB with straight
alpha** — `new Color(255, 255, 255, nib * 17)`.

Every text draw path in the client blends with `BlendState.AlphaBlend`,
which in MonoGame/XNA is the **premultiplied** convention
(ColorSourceBlend = One, ColorDestinationBlend = InverseSourceAlpha).

With a non-premultiplied white texture the blend becomes
`out.rgb = 255 + dst * (1 - A)`: any pixel with alpha >= 1/15 saturates to
(near-)full ink regardless of its actual coverage. Every glyph therefore
rendered as a SOLID TINTED RECTANGLE the size of its alpha footprint —
rows of blocks; unreadable text everywhere. This affects ALL text because
R119 made the .ffn family the engine-wide default renderer
(GameFacade.VectorFont / TextStyle.VFont) and all three default TextStyles
(DefaultTitle/DefaultButton/DefaultLabel) route through it.

Why 19 rounds of gates missed it: the existing pixel probes (uiglyph R96+,
uivfont R119) assert on the **alpha channel only** (`c.A > 0`, distinct
alpha levels). The alpha channel was always correct — the collapse happens
in RGB. The uitext check added this round pins RGB.

## Evidence

- `ffn_ascii_dump.py` — independent python decoder + ASCII-art renderer for
  .ffn tables (mirrors the port's exact placement rules). "Handgloves" and
  "The quick brown fox 0123" render as clean readable letters straight from
  disk bytes: the FORMAT interpretation (header, 11-byte char entries,
  u16code/u8w/u8h/u16u/u16v/i8t0/t1/t2, x += w+t0, y = t2, 4-bit high-first
  nibble ink) is CORRECT. The bug was purely in the texture upload.
- `r138-textdiag-BEFORE.txt` — the new `-textdiag` harness (renders through
  the real game paths into a RenderTarget2D, reads pixels back, prints
  ASCII art of alpha AND rgb + statistics) on the UNFIXED build:
  - "Handgloves 0123": ink=854, alphaSolid=222, alphaMid(AA ramp)=562,
    **rgbSaturated=784**, rgbEqAlpha=222 — i.e. 562 ramp pixels collapsed
    to full-ink RGB. The alpha art reads as clean text; the rgb art is
    solid `@` rectangles.
  - over an opaque background (40,40,60): 1794 saturated pixels vs 204
    after the fix.
- `r138-textdiag-AFTER.txt` — same harness after the one-line premultiply
  fix (`new Color(v, v, v, v)` with v = nib*17): rgbEqAlpha == ink on
  every probe (854/854, 1492/1492, ...), rgbSaturated == alphaSolid, and
  the rgb art is identical to the alpha art — readable antialiased glyphs
  through the direct path AND the full UILabel -> DrawLocalString ->
  OriginalVectorFont UI path, plus correct intermediate shades when
  composited over a background.
- `r138p1.log` — the full gate: 80/80 PASS first run, including the new
  `uitext` check (premultiply law, saturation-collapse detector, AA-ramp
  presence, bbox sanity, background-composite blend shades).

## The fix

One line in FromFFN (plus the comment): premultiply the white ink,
`v = nib * 17; px = new Color(v, v, v, v)`. Tinting still works
(SpriteBatch multiplies the tint by the texture color); alpha is
unchanged, so all existing alpha-channel pins and metrics
(Measure/Advance/T2 placement) are untouched.

## Corpus facts established this round (python, UIGraphics.far)

- 13 English .ffn tables: variablesans 07,08,09,10,11,12,14,16,18,20,
  12_bs, 11_s, 48 (+ Polish/ and Russian/ trees).
- All 224-glyph tables cover codes 32..255 CONTIGUOUSLY (no gaps);
  _48 has 16 glyphs (letters+space wordmark).
- Atlas dims: 07:256x83, 08:256x107, 09:256x115, 10:256x121, 11:256x152,
  12:256x180, 14:256x224, 16:256x316, 18:256x325, 20:256x427,
  12_bs:256x204, 11_s:256x174, 48:248x146.
- All headers: FNTF ver 101, record id 0x7a (uncompressed), next=0.
- **flags field**: 9 on regular tables, **11 on _12_bs and _11_s**.
- **Nibble ink ceiling**: 15 on regular tables (07 max 14), **13 on the
  flags=11 tables** — bold/condensed ink tops out at 13*17 = 221/255.

## STR# encoding note (verified)

TS1 STR# strings are read by FSO's IoBuffer.ReadNullTerminatedString as
raw byte -> char (Latin-1-style). The .ffn tables are BYTE-keyed
(Windows-1252 semantics: 0x85 = ellipsis shape, 0x93/0x94 = curly quotes,
0xA7 = the § simoleon). The two match: byte-coded strings render the
correct glyphs. True-Unicode literals (> 0xFF, e.g. U+201C) have no glyph
and silently drop with zero advance — no such literals ship in the client
(grep verified; the only non-ASCII literals are '§' = U+00A7, in-table).

## Residuals (disclosed)

1. **flags=11 ink ceiling**: whether the engine scales the _bs/_s nibbles
   by /13 (making 13 = full ink) or /15 (bold text at 221/255 max alpha)
   is UNDECODED. The port keeps the data-literal nib*17. Candidate decode
   target: the engine's font rasterizer (FNTF consumer in the PPC binary).
2. Chars > 0xFF in port-authored strings would silently drop (0 advance).
   Canon strings are all byte-coded, so this is port-input-only.
3. Off-ladder sizes (e.g. 13px) render linearly-filtered (soft); the
   ladder sizes render 1:1 native. The engine's own UI only requests the
   ladder sizes.
4. TextDiagnostics (-textdiag mode) is kept as a permanent diagnostic
   surface; it is inert unless the flag is passed.
