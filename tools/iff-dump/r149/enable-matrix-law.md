# R149 — PLAQUE ENABLE MATRICES (cWinCatalog::Init 0x26bd3c-0x26c374, decoded)

Round target (the R148 recommendation): the original GRAYS OUT subsort category
plaques whose (current main, sub) cell has no items — the "plaque enable
matrices" residual from r145/r146. Binary: `game-data/The Sims/The Sims Complete`
(PPC PEF, file offsets). Evidence: r145/disasm-catalog-init.txt lines 592-990.

## 1. The seven matrices (DECODED — the builder loop)

For EVERY catalog product (outer loop; r14 = product, r17 = stride 4):

| matrix | BSS (TOC) | build law | gate test(s) |
|---|---|---|---|
| roomFunc | −0x6a60 | if HasRoom(0x20b400): `m[room*8 + func] = 1` with room = 0x268970(product), func = 0x268830(product) | 0x20b400 |
| downtown | −0x6a7c | if DowntownProduct(0x20ae80): for slot i ∈ {0,1,2,3,7}: if FlagTest_i(0x20b0f0) → `m[i*8 + func] = 1`; slots 4-6 NEVER written (jump-table skip — same shape as the hidden plaque art) | 0x20ae80 + 0x20b0f0 |
| vacation | −0x6a80 | same shape (0x20ae20 + 0x20b070) | |
| community | −0x6a84 | same shape (0x20adc0 + 0x20aff0) | |
| studio | −0x6a88 | same shape (0x20ad60 + 0x20af60) | |
| magic | −0x6a8c | same shape (0x20ad00 + 0x20aee0) | |
| functionSub | −0x6a68 | if 0x20b1e0 AND 0x20b170: `m[func*8 + sub] = 1` with sub = 0x2686f0(product) (the OBJD FunctionSubsort value) | 0x20b1e0 + 0x20b170 |

FlagTest_i semantics (slot i ∈ {0→bit1, 1→bit2, 2→bit4, 3→bit8, 7→bit0x80}):
the product's expansion sort byte carries slot i's bit. So every expansion
matrix cell (m, f) = "the catalog has ≥1 product with that main-slot bit whose
function is f". The bit constants are inline immediates in the jump bodies
(`li r4, 1/2/4/8/0x80` at 0x26bdcc-0x26be74 etc.).

Key immediates (all DECODED):
- `slwi r3, r3, 3` + `stbx r19, r18, r0` (0x26bd74-0x26bd7c and
  0x26c358-0x26c364): the row*8 stride and the byte store — the matrices are
  64-byte (8x8) BSS arrays.
- `addi r19, r19, 8` per slot iteration (0x26be94 etc.): r19 walks the matrix
  rows; r18 (0x268830 = function/sub index) is the column offset.
- functionSub store (0x26c358): `slwi r3,r3,3` on r3 = 0x268830 (function) —
  **[func*8 + subsort]** orientation, i.e. the function is the ROW (unlike the
  six other matrices where the function is the COLUMN).

## 2. Consumption (DECODED — LoadBooks)

r145 buy-catalog-law.md section 1: in the subsort branch of the plaque loop,
`btn[i]+0x160 = enable-mask` and vt+0x170 repaints when it changes. A 0 cell =
the plaque repaints AS DISABLED — the ghosted frame-3 cell of the 4-state
sheet (art census: frame 3 = dim ghost; frame 0/1/2 = up/selected/hover).
MAIN-state plaques never gray (masks are subsort-mode only); expansion main
slots 4-6 stay HIDDEN in main state.

## 3. Port (all Simitone-side; no engine change)

- NEW `UIOriginalEnableMatrices` (static): builds all seven byte[64] matrices
  from `WorldCatalog` using the port's own field decodings — RoomSort =
  OBJD RoomFlags bitfield (display room -> bit via CanonRoomToFlagBit),
  Category = buy category (canon func via CanonFuncToCatalogID), the five
  expansion sort bytes (slots 0-3 + 0x80), Subsort = OBJD FunctionSubsort.
  Content-null-safe (retries until the catalog exists). `Rebuild()` for
  determinism proof.
- `UIOriginalSheetButton.Disabled`: draws frame 3 (ghost), ignores clicks
  (guard in the chrome's shared plaque handler).
- `UIOriginalBuyChrome.RelayoutSub`: per plaque — expansion: row =
  CurrentMain slot (0-3 or 7), matrix per LotMode; room mode: row =
  CurrentMain (room display), RoomFunc; function mode: row = CurrentMain
  (function canon), functionSub with the port-MaskBit -> engine-subsort
  translation (0-3->0-3, Pets 5->4, Magic 6->5, Other 7->6, All 8->7).
  `RelayoutMain` + the expansion main branch reset all plaques enabled.

## 4. Gate

NEW **uienamat** (89 -> 90):
- all 448 cells of the 7 matrices equal an INDEPENDENT recomputation over
  `WorldCatalog.All()` (different iteration path than the builder's
  GetItemsByCategory; the funcSub transpose is guarded explicitly);
- data anchors: roomFunc[kitchen-display-4 x appliances] is SET (the R122
  stove bit-0x01 anchor), every matrix has enabled cells, the sparse
  expansions have zero cells (community 37/64, magic 37/64, funcSub 27/64,
  roomFunc 39/64, downtown 34/64, vacation 37/64, studio 35/64);
- LIVE wiring through the real chrome: community main-0 row and the home
  kitchen row gray EXACTLY the empty cells; a disabled plaque's click
  (MouseDown+MouseUp through UIButton's own dispatcher) no-ops; a magic-town
  main-0 fallback drive if the kitchen row is fully populated;
  RelayoutMain resets everything enabled.

## 5. Residuals (disclosed)

- The two functionSub gate tests (0x20b1e0 AND 0x20b170) are approximated by
  "valid OBJD FunctionSubsort value" (0-7) — the exact flag meanings are
  undecoded.
- The engine's func/room/sub index functions (0x268970/0x268830/0x2686f0) read
  OBJD fields the port already decodes; the port assumes identity with its own
  mappings (validated by the 448-cell cross-check and the wiring pins).
- Function-mode subsort graying (functionSub) is wired but not live-pinned
  (the home band's function-mode subsort art path was pinned in r145; the
  enable cell equality is pinned at matrix level).
