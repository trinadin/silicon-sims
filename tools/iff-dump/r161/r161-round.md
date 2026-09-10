# R161 — the BUDGET window on the cWinBudgetDlg law (UI-100% round 3)

Round mandate: campaign item 3 — the budget window, the biggest base-game
screen never touched by any round before r159's scout census.

## Engine decode (this round; symbol map r141, disasm in this directory)

- **The button**: the control panel's FUNDS readout. cWinViewControl::
  TSOnCommand @0x2b48c0 dispatches sender == this+256 (0x2b5094-0x2b50fc)
  -> `new cWinBudgetDlg` -> centered-rect math -> `SetBlockSimulator(true)`
  -> `cSimsApp::DoSimsModalDialog(1,0)` -> `SetBlockSimulator(false)` ->
  DestroyWindow. (Guarded by app->28->+80 and Neighborhood::GetZoningType.)
- **cWinBudgetDlg::Init** @0x263e70 (3668 B; r161-disasm-budgetdlg-init.txt):
  - `House::PrepareForBudgetWindow` @0x8c540 first (copies House budget
    accumulators +0x10c..+0x138 — the per-category data source).
  - SetArea(this, L, T, L+0x64, T+0x64) — the dialog rect GROWS by 100 on
    each axis (the 800x600 board; base rect from the caller's centering).
  - OK button @0x263f64-0x264034: `new cTSWinBtn` + load 0x1366 = 4966
    kBudgetOK (CPanel/Buttons/BudgetOK.bmp 868x52), SetImage(art, 4, 1) ->
    **217x52 states**, btn+0x163=1, then the centering SetArea (avg/2 on
    BOTH axes) -> **(291,274)** on the 800x600 board.
  - STR# 146 loaded through the 0x87f60/0x25f440 LoadUIString pair
    (set 0x92=146) @0x264178-0x264194 — the same mechanism identified
    in R160.
  - The two '(x;y)' strings parse into anchor pairs: dlg+0x124 = (40,25),
    dlg+0x12c = (40,100) @0x2641a8-0x264224.
  - **The row array**: 20 slots, `new(0x100)` each -> cWinBudgetRow ctor
    @0x265a50 (256 B = cTSWin base + 4 CTGStrings at row+0xec/f0/f8/fc =
    label/today/3day/tooltip; flag byte row+0xdc=1), stored dlg+0xd4
    stride 4, AddChild, `SetIndent(row, [TOC table])`, SetArea width 460,
    `SetFont(row, [TOC table])` (loop 0x2640ac-0x264168).
  - **Row tiling**: from the (40,100) anchor, pitch 20 (`mulli count,0x14`
    @0x264238+), two variants (left/right anchored) chosen by flag 0x1b8.
  - **The column law** @0x264b10-0x264bd0: ComputeWidths EVERY row
    (0x264f30 -> three int outs), keep the running MAX per column, then
    SetWidths ALL rows with the three maxima — uniform max-width columns.
  - Labels/values set in the 0x264400-0x264a20 region: row skips at
    indices 5/14/16 in the value loop (section headers take no values;
    the '%s' totals are formatted whole lines); SetCurText 0x2650c0 /
    Set3DayText 0x265142 / SetCurValue 0x2651c0 / Set3DayValue 0x265272.
- **cWinBudgetRow::SetWidths** @0x264ea0: w1/w2/w3 stored row+0xe0/e4/e8;
  row SetArea width = w1+w2+w3+**120**.
- **Art**: 4965 kBudgetBkg CPanel/Backgrounds/BudgetBack.bmp 800x600 —
  a plain navy gradient, NO baked text (pixel-verified this round: the
  whole surface is a smooth (0,~20,~106) family); all text is
  engine-drawn. BudgetOK 868x52 = 4x217x52 states.
- **STR# 146 'budgetstrs'** (r161-budgetstrings-canon.txt, sha pinned in
  the gate): 648 = 18 langs x 36; English block = the two anchors, OK,
  title, two column headers, 15 label+tooltip pairs; [30]/[32]/[34] are
  '%s' totals.

## What shipped

1. **UIOriginalBudgetDialog** (UI/Panels/UIOriginalBudgetDialog.cs): the
   800x600 navy board on the original art, title at the (40,25) anchor,
   'Today'/'3 Days' headers right-aligned over their columns, the 15 rows
   at (40, 100+i*20) with header rows unindented and item rows +16
   (SetIndent value disclosed), row tooltips from the interleaved table
   entries, the UNIFORM MAX-WIDTH COLUMN LAW ported literally (measure
   every label/value, max per column, right-align values at the column
   edges), the '%s' totals formatted from REAL family data (Account
   Total = ActiveFamily.Budget; Net Worth = Budget+ValueInArch — the
   EvictLot precedent), and the OK button 217x52 at (291,274) (engine
   both-axes centering) on the 4-state BudgetOK sheet (UIOriginalNavbar
   Button reused).
2. **The funds readout is the button** (UIDesktopUCP): a hotspot over the
   plate money zone (around the r144 anchor (178,158)) opens the modal
   dialog (GlobalShowDialog modal) — the engine interaction (click your
   money to see the budget), which the port never had in any form.
3. **'uibudget' check 97->98**: STR# 146 chunk sha + all 36 English
   entries verbatim, art dims (800x600 + 868x52), LIVE ctor on the
   autotest's game screen (15 rows at the anchor law, measured columns,
   Account-Total text == "§<Budget>" from the real family, OK cell
   217x52 centered, tooltips == disk).

## Disclosed interpretations (not pinned)

- Row indent 16 (engine SetIndent TOC table values undecoded) and the
  header-row skip semantics mapped onto the string-table row order.
- Value-column right-alignment at the uniform column edges; +8 gutters.
- The OK button sits at the engine's both-axes center (291,274) per the
  decode — visually mid-board; kept as decoded.
- Data: the port lacks the engine's 3-day window and per-category
  accumulators — Today shows what the port knows (summed member salaries
  via Jobs.GetJob(JobType).JobLevels[JobPromotionLevel].Salary, funds,
  net worth), untracked categories show §0, 3-Days only on the
  daily-rate rows (Job/Cash Flow = 3x), Days Since Move-In = 0.
- The VM runs behind the modal (the engine blocks the simulator; the
  port's modals — quit/evict — do not pause either).

## Verification

- Targeted `-autotest-opts "uibudget,corpus"`: 6/6 PASS first run
  (/tmp/r161-targeted-run1.log kept as r161-targeted-run1.log).
- FULL GATE 98/98 PASS (r161-gate-run1.log): uinav intact, carseek
  intact (minute=6:41 natural), clean exit chain (after Run / after
  Dispose / AUTOTEST_WRAPPER_EXIT=0), uibrand intact.
- dist DLL byte-match: Simitone.Client 278d27f6…, FSO.SimAntics
  903db62a… (unchanged), FSO.Client unchanged.
