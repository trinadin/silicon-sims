# R238 independent skeptical Budget review

The R161 fixed 800×600 Budget board, fixed 20px pitch, 16px indent, value
right alignment, 8px gutters, and centered-y OK button are contradicted by
the owned original executable. R237 correctly rejected the board and OK
placement, but its description still called `(40,100)` the row-list origin.
It is the right/bottom sizing margin, including the 100px footer.

`recover-budget.py` reads the owner's executable in place, checks its SHA-256,
unpacks its data section, and emits only metadata/disassembly. Its independent
output is in `recovered-budget.txt`; no original art is copied.

## Layout recovered from instructions

- Init `0x263efc` loads resource **3008**, the 36×36 PopupInfoTiles frame.
  Init `0x263f10–24` establishes a temporary **100×100** rectangle.
  BudgetBack resource 4965 is not loaded by this function.
- The 20 pointer slots at dialog `+0xd4` are row windows, including the title,
  value headings, three spacers, and three totals. `0x264238` multiplies the
  maximum measured row height by the **count 20** for the preliminary tiler;
  it does not establish a 20px pitch.
- Every row `SetFont` at `0x265360–3cc` sets its height to the selected font's
  virtual `+0x60`, `TSCharHeight`, with **no padding**. The current
  `OriginalGlyphFont.LineHeight` derives this from the same FFN metrics.
- `0x264400–54` stacks rows 1 through 19 at the preceding row's bottom,
  retaining each row's individual height and sharing its x position. Row 0
  begins at STR146[0], `(40,25)`.
- The font table is DATA `0x554c0`, through TOC `-0x6ae4`; its entries point
  at the font globals `0x92900+4*index`. Indent units are DATA `0x55510`,
  through TOC `-0x6ae0`. See the exact per-row table in the generated output.
- `ComputeWidths` `0x264f30–26501c` returns zero for all three widths on a
  centered row. Other rows return `measure(label)+indent*12`,
  `measure(today)`, `measure(threeDays)`. Headings participate in these maxima.
- `SetWidths` `0x264ea0–ef0` makes every row width
  `maxLabel+maxToday+maxThreeDays+120`.
- Ordinary row text is **left aligned**: label x=`rowX+30+indent*12`,
  Today x=`rowX+60+maxLabel`, 3 Days x=`rowX+90+maxLabel+maxToday`.
  Paint `0x2655e4–61c` establishes these three starts. There is no
  right-align subtraction of each value's width in its paint calls.
- Centered title/totals use `(rowWidth-measure(text))/2`; their paint y is
  the row's y. The centered flags are rows 0,17,18,19. Formatted totals are
  constructed on paint from the stored format and value strings.
- Final Init `0x264bd4–c10` sets window width=`row3.Right+40` and
  height=`row19.Bottom+100`. Thus horizontal margins are 40 each around the
  row windows. With the owned English fonts, final height is **528**.
- The OK button is resource 4966, 4×1 cells, **217×52**. Font factory call
  `0x263f40–60` acquires type 1, size **20**, flags 0, language 0; SetFont
  `0x26403c–44` applies it. STR146[2] is assigned as rendered caption at
  `0x26447c–8c`. Final `0x264c18–88` places it at
  `((windowWidth-217)/2, windowHeight-100+(100-52)/2)`, hence **y=452**.

## Row semantics

| Row | STR146 | Content |
|---:|---:|---|
| 0 | 3 | Budget, centered |
| 1 | 5 / 4 | Today / 3 Days in value columns |
| 2 | 6 | Income |
| 3 | 8 | Job |
| 4 | 10 | Miscellaneous |
| 5 | — | Empty spacer |
| 6 | 12 | Expenses |
| 7 | 14 | Bills Paid |
| 8 | 16 | Food |
| 9 | 18 | Repair/Cleaning/Gardening |
| 10 | 20 | New Purchases |
| 11 | 22 | Household Items |
| 12 | 24 | Architecture/Landscaping |
| 13 | 26 | Miscellaneous Expenses |
| 14 | — | Empty spacer |
| 15 | 28 | Cash Flow |
| 16 | — | Empty spacer |
| 17 | 30 | Household Account Total, centered |
| 18 | 34 | Household Net Worth, centered |
| 19 | 32 | Days Since Move-In, centered |

Rows 2,6,10 explicitly have their two value strings cleared at
`0x2649d4–a18`. The New Purchases heading must therefore not gain a §0
value merely because the old port treated only Income/Expenses as headings.
The totals' original order differs from the source string-table order: days
are displayed last. The three spacers still retain their font-derived heights.

## Required verification

Check native 800×600 and larger captures, 528px fixed window height,
full title/totals/OK captions, visible gaps around columns, native pixel
frame composition, and the reordered totals. Do not preserve an old broken
fixed-coordinate assertion as evidence. Financial accumulators and simulator
blocking remain separate existing behavioral gaps; this decode does not
prove those are restored by a layout correction.
