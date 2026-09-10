# R140 — the .ffn load law engine-decoded (GIMEX module), and the atlas-stream correction

## The question (R139b residual)

R139b excluded `variablesans_16.ffn` from the font ladder as "internally
inconsistent" and disclosed the residual: does the ORIGINAL engine recover
the table (ink-extent re-derivation) or does it use the file's u/v as the
port does? The path named for this round: decode `cTSFont::InitBitmapped`
(0x4B3F00) + the Gimex calls (0x34FA80/0x34FD20/0x34FCC0/0x34FC60/0x350D60).

## Where the GIMEX code actually lives

The five "Gimex calls" are CFM import-style STUBS (TVector table at
[TOC−19452], tail-call through the CFM trampoline 0x5A29E0), but the
implementations are STATICALLY LINKED in the same binary — the
`MacGIMEX_*` module at 0xB0A0–0xC7D0, recovered via CW symbol trailers:

    0xB0A0 MacOLDGIMEX_open   0xB9B0 MacGIMEX_read(GINSTANCE*,GINFO*,char*,int)
    0xB0F0 ...installcallbacks  0xBD50 MacGIMEX_info(GINSTANCE*,int)
    0xB450 MacGIMEX_write      0xC210 MacGIMEX_open(GINSTANCE**,GSTREAM*,const char*,int)
    0xB790 MacGIMEX_wopen      0xC690 MacGIMEX_is(GSTREAM*)
    0xB8A0 MacGIMEX_close      0xC770 MacGIMEX_restore / 0xC7A0 MacGIMEX_init

`loadgimexlibrary(GIMEXROUTINES*)` (0x34FEF0) fills the 21-word routine
table from TOC slots; the stubs call through it. (PPC decode note: this
round also proved ppc_decode.py renders BO=12/BI=2 as "ne" when the
instruction is `beq` — branch senses for eq/ne are swapped in its output.)

## The decoded .ffn file law (all engine-proven)

`MacGIMEX_open` (0xC210), FNTF branch:
- char table = header + header[20]; entries are 11 BYTES, little-endian;
  open byte-swaps the u16s at entry +0/+4/+6 in an unrolled stride-11 loop
  (`addi r6, r6, 11` at 0xC420 — the port's entry model verbatim).
- shape record = header + header[28]; open swaps its u16@+4 (atlasW),
  u16@+6 (atlasH), u32@+8, u32@+12 — a **16-byte record header**.
- GINSTANCE = {data, shapeOff+24-ish, charCount@+8 (header field@10), 0, stream}.

`MacGIMEX_info` (0xBD50), FNTF branch — GINFO fields from entry `i`
(pointer = charTable + i*11):
- GINFO+16 = entry.w (u8@+2), GINFO+20 = entry.h (u8@+3)
- GINFO+1096 = −t1 (i8@+9 negated), GINFO+1100 = −t2 (i8@+10 negated)
- GINFO+1116 = the char code (u16@+0) rendered as a DECIMAL STRING —
  which cTSFont::InitBitmapped sscanf's back to get the entry index.
- **GINFO carries NO u/v** — the file's u/v never leave the reader.

`MacGIMEX_read` (0xB9B0), FNTF branch — per pixel (x,y) of glyph i:
- re-reads entry.u (u16@+4) and entry.v (u16@+6) from the char table
- sample = nibble at `record + 16 + (v+y)*(atlasW/2) + (u+x)/2`
- even (u+x) = HIGH nibble, odd = LOW nibble
- **the stream base is record + 16** (0x4BA08: `addi r9, r6, 16`)

`InitBitmapped` (0x4B3F00) then builds the engine's own runtime glyph
sheet (256×28 char records: pen positions, advances, ink extents, the
−t1/−t2 fields) and blits each decoded glyph into it — an engine-internal
cache irrelevant to file fidelity.

The engine's font factory (~0x4B0200, caller of the cTSFont ctor) builds
`"VariableSans_%02d" + ".ffn"` by sprintf from the requested size and
maps BOTH size 15 and size 16 to the 16px table (explicit `cmpwi 15 → 16`
clamp in the dispatch). The full shipped ladder is canon.

## THE CORRECTION — the port's atlas stream was 4 bytes early

The port (since R96) read the nibble stream from `shapeOff + 12`.
Engine truth: `shapeOff + 16`. Three independent proofs:

1. **Engine decode**: MacGIMEX_read samples at record+16 (above).
2. **Byte count**: for every table, `len == shapeOff + 16 + atlasW*atlasH/2`
   EXACTLY (e.g. _12: 25552 = 0x9C0+16+23040). The +12 base reads the
   zero u32@+12 as pixels and leaves 4 bytes unused at EOF.
3. **Letterforms**: under +16 the alphabet renders as perfect ABCDEFG
   letterforms; under +12 every glyph cell samples its NEIGHBOUR's
   strokes 8px left-of-truth — fragments that still read as "brush-style"
   text (see r140-render-12-old-law.txt vs r140-render-12-engine-law.txt).

Consequences:
- **variablesans_16.ffn was never broken.** R139b's cell-straddling
  analysis was performed under the shifted base. Under +16, _16 renders
  flawless "Handgloves" and ABCDEFG (r140-render-16-engine-law.txt).
- 16 is RESTORED to `OriginalVectorFont.LadderPx`; 16→16, 17→16
  (ties-smaller), 15 stays →14 under the port's nearest-ties-smaller
  fallback (no 15px requesters; the engine's own 15→16 clamp is
  documented in the ladder comment).
- Glyph ADVANCES (w+t0) never touched pixels, so every measurement pin
  (wantSims, mLot20=43) carries over unchanged.
- The R139b tooltip diagnosis stands HALF-corrected: the garble the user
  saw at 16px WAS real port output — it was the 8px shift breaking worst
  on _16's tighter atlas packing, not a broken file.

## Ported

- `UIOriginalText.FromFFN`: stream base `shapeOff+16` (+ bounds check and
  format doc updated, with the engine citation).
- `OriginalVectorFont.LadderPx`: {7,8,9,10,11,12,14,16,18,20}.
- Gate: uitext native-scale pins 16→16/17→16, ladder composition pin
  (10 entries, 16 present), per-table render probes for all 10 tables;
  uivfont snap table + wantSims back to 10 entries (_16=91).
- tools/iff-dump/r138/ffn_ascii_dump.py patched to +16 with this note.

## Residuals (disclosed)

- The engine's own glyph-sheet layout law (InitBitmapped's pen
  accumulation, the 1024-wide wrap, the post-loop baseline adjustments
  at 0x4B4554+ incl. the char-160 special case) is decoded but not
  ported — it describes an engine-internal cache, not file semantics.
- The GINFO defaults block (+40..+103) and the parse of shape-record
  u32s@+8/+12 (always 0 in shipped tables) remain unlabeled fields.
- Why the +12 renders still read as text for 44 rounds: the brush
  letterforms are dense enough that 8px-shifted neighbour fragments
  still suggest glyphs. Not further analyzed — the +16 law is
  engine-proven and data-exact.
