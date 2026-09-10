# R142 — The "TSUI template database" mystery: SOLVED (and dissolved)

Round mandate (from r141 residual): "TSUI template database (panel ids 100/380/480)
undecoded → UCP-internal button positions are art-anchored interpretation, not
engine rects. Next-round target: decode 0x591440's template store."

## Headline: 0x591440 is `operator new`, NOT a template fetcher

The r141 hypothesis was wrong. `0x591440` is the Metrowerks MSL pool allocator's
`operator new(size_t)`:

```
0x591440 operator new(size)          <- OOM retry loop with new-handler
  -> 0x592da0  new core: lazy pool init (0x5927e0) then dispatch by size
      -> 0x592b40  size-class dispatch: size <= 0x4C -> small-bin (0x592830),
                  else free-list (0x592430)
      -> 0x5923b0  pool segment creation, min 64KB (0x597900 = raw segment alloc)
      -> 0x591eb0  free-list take/split (block hdr: [blk+4]=next, [blk+0x10]=size,
                   returned ptr = blk+8)
```

The "resource ids" 100/380/480/396/900 are **sizeof(class)** arguments, each
followed immediately by the matching constructor:

| "template id" | actual meaning | ctor | evidence |
|---|---|---|---|
| 100 | `sizeof(cTSBuffer)`   | `cTSBuffer::cTSBuffer()` @0x48ee70 | symbol 0048ee72 |
| 380 | `sizeof(cWinArch)`    | `cWinArch::cWinArch(CPState*, cTSBuffer*)` @0x262970 | 00262972 |
| 480 | `sizeof(cWinViewControl)` | `cWinViewControl::cWinViewControl(CPState*)` @0x2b80a0 | CW trailer found in-gap @0x2b8200: `.__ct__15cWinViewControlFP7CPState`, size 0x15C |
| 900 | `sizeof(cWinPeople)`  | `cWinPeople::cWinPeople(CPState*, cTSBuffer*)` @0x292220 | 00292222 |
| 396 | `sizeof(cTSWinBtn)`   | `cTSWinBtn::cTSWinBtn()` @0x50d720 | 0050d722 |

There is **no TSUI template store, resource, or in-binary gadget-rect table**.
`grep TSUI` over the binary finds nothing relevant; no data file holds it.

## How buttons actually get their positions (the real law)

Every control-panel button is a `cTSWinBtn` (396 B). In
`cWinViewControl::Init` (0x2b5f30) each button gets:

1. `SetImage(cTSBuffer* art, int cols, int rows)` — virtual at vtable+0x1a4,
   impl `cTSWinBtn::SetImage` @0x50c0f0:
   - stores art at +0xD0, cols at +0xE8, rows at +0xE4
   - per-state size: `W = buf->width(art+0x1C) / cols`, `H = buf->height(art+0x20) / rows`
   - stores W,H at +0xFC,+0x100, then re-`SetArea`s the rect around its top-left.
2. The caller then re-anchors it:
   `SetArea(anchor.x, anchor.y, anchor.x + W, anchor.y + H)` — virtual at
   vtable+0x68, impl chain `cTSWinBtn::SetArea` @0x50af10 -> `cTSWin::SetArea`
   @0x504860 which stores **+0x74=left, +0x78=top, +0x7c=right, +0x80=bottom**.

So the final engine rect of every UCP button is:

```
RECT = (ax, ay, ax + sheetW/cols, ay + sheetH/rows)
```

where `(ax, ay)` comes from a hardcoded anchor and the sheet geometry from
UIGraphics.far art.

## The anchor database (hardcoded in code, not data)

The anchors are 8-byte BSS globals {s32 x, s32 y} reached through the CFM TOC
(**r2 = data-section base + 0x8000** — derived from the cTSWinBtn vtable TOC
slot at data 0x1EE8 holding 0x74608). A layout-init function at **0x2b8230**
(sandwiched between `cWinViewControl::__ct` and the next trailer) writes them
all with immediate stores. Full symbolic dump in `tsui/anchors.py` output and
`tsui/anchor-writer-disasm.txt`. Mapping (TOC offset -> value):

| TOC slot | BSS addr | value |
|---|---|---|
| -0x4E58 | 0x93B48 | (61, 91) [not read elsewhere - legacy] |
| -0x4E5C | 0x93B4C | (95, 118) + quad (95,118,151,125) [write-only] |
| -0x4E60 | 0x93B50 | (103, 158) + quad (103,158,188,174) [write-only] |
| -0x4E64 | 0x93B54 | (83, 153) + quad (83,153,171,156) [write-only] |
| -0x4E54 | 0x93B64 | (178, 158) |
| -0x4E50 | 0x93B74 | (103, 158) |
| -0x4E4C | 0x93B78 | (5, 164) — kHelpBtn |
| -0x4E48 | 0x93B7C | (141, 135) — kSpeed3 |
| -0x4E44 | 0x93B80 | (120, 135) — kSpeed2 |
| -0x4E40 | 0x93B84 | (105, 135) — kSpeed1 |
| -0x4E3C | 0x93B88 | (85, 135) — kPause |
| -0x4E38 | 0x93B8C | (175, 95) — kCameraBtn |
| -0x4E34 | 0x93B90 | (193, 125) — kOptions |
| -0x4E30 | 0x93B94 | (117, 45) — kArch |
| -0x4E2C | 0x93B98 | (69, 20) — kObjects |
| -0x4E28 | 0x93B9C | (10, 6) — kPeople |
| -0x4E24 | 0x93BA0 | (103, 95) — kNoWall |
| -0x4E20 | 0x93BA4 | (80, 85) — kDynaCut |
| -0x4E1C | 0x93BA8 | (56, 80) — kNoCut |
| -0x4E18 | 0x93BAC | (26, 149) — kZoomOut |
| -0x4E14 | 0x93BB0 | (26, 105) — kZoomIn |
| -0x4E10 | 0x93BB4 | (9, 127) — kRotLeft |
| -0x4E0C | 0x93BB8 | (43, 127) — kRotRight |
| -0x4E08 | 0x93BBC | (32, 78) — kLevelRoof |
| -0x4E04 | 0x93BC0 | (5, 77) — kLevel1 |
| -0x4E00 | 0x93BC4 | (5, 92) — kLevel2 |

(The three 4-value "quad" anchors are decoration rects — toothpick/stripe
regions; they are written but never read back anywhere in code: likely dead.)

## The UCP table (engine ground truth)

From `tsui/ucp_table.py` (also `tsui/ucp-layout.txt`); sheet dims measured
from the real UIGraphics.far BMPs in `uigr-orig/`:

| id | const | art | sheet | cols,rows | button WxH | anchor | RECT (l,t,r,b) |
|---|---|---|---|---|---|---|---|
| 2010 | kPeople | people.bmp | 200x50 | 4,1 | 50x50 | (10,6) | **(10,6,60,56)** |
| 2008 | kObjects | objects.bmp | 180x47 | 4,1 | 45x47 | (69,20) | **(69,20,114,67)** |
| 2003 | kArch | arch.bmp | 156x39 | 4,1 | 39x39 | (117,45) | **(117,45,156,84)** |
| 2009 | kOptions | options.bmp | 92x23 | 4,1 | 23x23 | (193,125) | **(193,125,216,148)** |
| 2024 | kLevel1 | Lev1.bmp | 84x11 | 4,1 | 21x11 | (5,77) | **(5,77,26,88)** |
| 2023 | kLevel2 | Lev2.bmp | 88x15 | 4,1 | 22x15 | (5,92) | **(5,92,27,107)** |
| 2025 | kLevelRoof | LevRoof.bmp | 88x19 | 4,1 | 22x19 | (32,78) | **(32,78,54,97)** |
| 2006 | kNoCut | nocut.bmp | 84x21 | 4,1 | 21x21 | (56,80) | **(56,80,77,101)** |
| 2004 | kDynaCut | dynacut.bmp | 84x21 | 4,1 | 21x21 | (80,85) | **(80,85,101,106)** |
| 2007 | kNoWall | nowall.bmp | 88x15 | 4,1 | 22x15 | (103,95) | **(103,95,125,110)** |
| 2034 | kCameraBtn | camera.bmp | 116x29 | 4,1 | 29x29 | (175,95) | **(175,95,204,124)** |
| 2013 | kZoomIn | zoomin.bmp | 108x27 | 4,1 | 27x27 | (26,105) | **(26,105,53,132)** |
| 2014 | kZoomOut | zoomout.bmp | 108x27 | 4,1 | 27x27 | (26,149) | **(26,149,53,176)** |
| 2011 | kRotLeft | rotleft.bmp | 108x27 | 4,1 | 27x27 | (9,127) | **(9,127,36,154)** |
| 2012 | kRotRight | rotright.bmp | 108x27 | 4,1 | 27x27 | (43,127) | **(43,127,70,154)** |
| 2026 | kPause | pause.bmp | 60x30 | 4,2 | 15x15 | (85,135) | **(85,135,100,150)** |
| 4705 | kSpeed1 | Speed1.bmp | 44x15 | 4,1 | 11x15 | (105,135) | **(105,135,116,150)** |
| 4706 | kSpeed2 | Speed2.bmp | 80x15 | 4,1 | 20x15 | (120,135) | **(120,135,140,150)** |
| 4707 | kSpeed3 | Speed3.bmp | 128x15 | 4,1 | 32x15 | (141,135) | **(141,135,173,150)** |
| 4993 | kHelpBtn | HelpButton.bmp | 40x15 | 4,1 | 10x15 | (5,164) | **(5,164,15,179)** |

All 20 rects fit the 220x183 UniversalBack plate (max right 216 <= 220, max
bottom 179 <= 183). Sanity checks pass:
- **Camera diamond**: zoom (26,105)/(26,149) vs rotate (9,127)/(43,127) —
  four 27x27 buttons in a diamond centered ~(38,140).
- **Mode cascade**: kPeople (10,6) -> kObjects (69,20) -> kArch (117,45)
  stepping down-right across the plate top; kOptions alone at right (193,125).
- **Speed row**: kPause+kSpeed1..3 share y=135 with 15px row height.

Extras in Init: an invisible capture gadget (member 0x100) with
`SetImage(NULL,4,0)` + `SetCaptureBkg(1)`, area from anchors (103,158) and
(178,158); the four mode patches (4803 Live 213x153, 4804 Buy 156x148,
4805 Build 107x132, 4806 Options 37x100) + 2035 are loaded as plain art
objects (no SetImage gadget) and composited separately.

### Corrections vs r141's art-anchored interpretation
- The wall-view row (nocut/dynacut/nowall) is at y 80..110 (plate upper
  middle), NOT in the bottom dark recess (125..182) as r141 guessed.
- kPause is **15x15** (pause.bmp 60x30 = 4 cols x 2 rows of 15x15), not 30x30.
- kSpeed buttons at x=105/120/141 on the PLATE (y=135), not on the toolbar's
  right end.
- kHelpBtn (4993, HelpButton.bmp 40x15 -> 10x15) at (5,164) is a plate button
  the port doesn't render.
- kCameraBtn (2034, 29x29) at (175,95) exists as its own button.

## Where the toolbar (804x100 PanelBack) is built

`cWinCPanel::Init` (0x270170) constructs cTSBuffer/cWinViewControl/cWinArch/
cWinPeople and loads arts 2036, 4910 (kUniversalCPBkg = PanelBack.bmp 804x100),
4918 — but performs **no SetImage/SetArea** itself. The toolbar row's own
button layout lives in the child panels' code (cWinPeople/cWinArch etc.) and
was not extracted this round; it is NOT driven by any 100/380/480 "template".

## Tooling discoveries (needed for the above, kept for reuse)

1. **ppc_decode.py mis-decodes X-form `or`**: it prints `or rD, rA, rB` in
   encoding-field order, but PPC X-form logicals are `or rA, rS, rB` — dest is
   the MIDDLE operand. Every "or r3, r29, r3" in r141 dumps is actually
   `mr r29, r3`. (Its eq/ne branch-swap bug is separate and still present.)
   `tsui/capdis.py` = capstone-based decoder used for R142 (venv capstone
   5.0.7 installed at /tmp/capvenv).
2. **PEF packed-data opcode fix** (`tsui/unpack_data.py`): the interleave ops
   (3/4) take the COMMON size from the opcode byte's count field, then
   [varint customSize][varint blockCount]; op4's common is ZEROS; output =
   common,(block,common)*N. The old `pef_unpack.py` mis-parses and desyncs.
   Validated: consumes container 0x6d834 -> exactly 0x7bf80 bytes.
3. **Full loader-relocation decode** (`tsui/pef_reloc_final.py`): one reloc
   group, section 1 (data), stream loader[0x948..0x6e64). Validated by zero
   double-touches and clean end (word 0x1d3d3 <= initialized end 0x1efe0).
   dskip encoding: `00|skipCount(5: bits13-9)|relocCount-1(4: bits8-5)` —
   empirically proven (all other splits collide).
4. **CFM layout of this binary**: data section starts with the import TOC
   (data[0..0x854)), then a table of data-section pointer offsets at 0x854+;
   **r2 (TOC base) = data + 0x8000**; vtables are arrays of TVector pointers
   (each virtual = 8-byte {code,TOC} pair, code word pre-stored as a
   section-relative code offset, +code base at load); the cross-TOC call glue
   is `0x5a29e0: lwz r0,0(r12); stw r2,20(r1); mtctr r0; lwz r2,4(r12); bctr`.
   18 cTSWinBtn-family vtables recovered (`tsui/vt-runs.txt.gz`,
   `tsui/reloc-kinds.json`), incl. subclasses ProductButton, cCustButton,
   cWinMotive, cWinPersonality, cWinRelationship, cPerformanceBtn, cFlashyBtn,
   cPickFamilyItem, cWinDesignFamPeopleBtn, cWinPersonalityWidget,
   cWinVitaBtn(Solo), cWinInterest, cWinGift, cTSSystemButton, cPickSpellItem.
   cTSWinBtn vtable key slots: 0x14 Init, 0x1c dtor, 0x68 SetArea, 0x1a4
   SetImage, 0x1a8 SetEntered.

## Files
- `tsui/tsui-template-law.md` — this document (also at r142/ root).
- `tsui/ucp-layout.txt`, `tsui/ucp_table.py` — final table + generator.
- `tsui/ucp_extract.py`, `tsui/cpanel_extract.py` — SetImage/SetArea/anchor
  cross-referencers for ViewControl::Init / CPanel::Init.
- `tsui/anchors.py`, `tsui/anchor-writer-disasm.txt` — anchor-writer
  symbolic extraction + raw disasm (0x2b8230..0x2b84a4).
- `tsui/unpack_data.py`, `tsui/data-sec1-unpacked.bin` — data section unpack.
- `tsui/pef_reloc_final.py`, `tsui/reloc-kinds.json`, `tsui/vt-runs.txt.gz` —
  relocation engine + outputs.
- `tsui/capdis.py` — capstone PPC disassembler helper.

## Residuals (honest)
- Toolbar (PanelBack 804x100) button layout: built inside cWinPeople /
  cWinArch / other child panels; not yet extracted (needs the same
  anchor-writer hunt inside those classes). The r141 port guesses for the
  toolbar row remain unverified against engine code.
- The mode patches' (4803-4806) compose position is not decoded (loaded, but
  where they blit is in paint code).
- The three write-only quad anchors are presumed dead/legacy; not proven dead.
- Symbol-trailer "2-byte slop": engine function starts are 4-aligned symbol
  addresses (index entries end in 2); confirmed on cWinViewControl ctor but
  assumed elsewhere.
