# R115 — Job subpanel (original strings), money floaters (original glyphs), alert value fixes

**Slice**: three related fixes from the Hermes surveys (r115 session):
1. `UIJobSubpanel` — ALL its strings were hardcoded; every one is recoverable
   from the original Live.iff tables.
2. The money-change FLOATERS (`UIMoneyPanel.DisplayChange`,
   `UIDesktopUCP.DisplayChange`) — the most frequently flashed modern-rendered
   text in the game (every transaction, both UI modes).
3. Five exact original string VALUES for the lot-control alerts.

## Canon (raw — GameData/Live.iff, 76-byte-header walk)

- **STR# `JobSubpanelLabels`, chunkID 136**: file 0x2e65, chunk size 16,017,
  format −3, 187 entries,
  sha256 `3908a498315efe29380a685c613a75fe747b1d53a088b3d8133993f46c0a7a28`.
  English: `[0] 'Job'` (subpanel title, unused by the port), `[1] 'Cooking'
  [2] 'Mechanical' [3] 'Charisma' [4] 'Body' [5] 'Logic' [6] 'Creativity'`
  (the six skill names, same order the port happened to hardcode),
  `[7] 'Unemployed'`, `[8] 'n/a'` (the salary line when unemployed — the port
  CLEARED the line; the original shows n/a), `[9] 'n/a'`, `[10] 'Need %d'`.
- **STR# `JobSubpanelPopupText`, chunkID 137**: file 0xd9c9f, chunk size
  142,742, format −3, 544 entries,
  sha256 `2639aca61ecbbc33ff7e7d4af04e5534165c23d439b9fc54e34ae7c49dd9c7a8`.
  English: `[12] 'Unemployed'`, `[14] 'Salary'`, `[16] 'Performance'`,
  `[23] 'Hours: $Start-$End.\r\nCarpool arrives: $CarTime.'` (a popup format,
  not the inline salary line).

## Port

1. **`OriginalLiveStrings` generalized** to a table map ({130, 131, 136, 137},
   matched on id AND label) with a generic `Entry(tableId, idx)`.
2. **`UIJobSubpanel`**: skill names from 136[1..6]; 'Performance' from 137[16];
   the unemployed job title from 136[7] and its salary line from 136[8] ('n/a',
   canon-backed change from the port's empty string); the salary line prefix
   from 137[14] (the `§n (h-h)` composition stays port-side — no original
   inline format exists; disclosed). Nine `UIOriginalText` twins in the caption
   table **_10**, mounted BEFORE any early-return (the R114 lesson) and synced
   every Update (JobTitle/SalaryTitle mutate); the modern PerformanceTitle's
   Visible field is the STATE HOLDER consumed by the twin sync (so the
   employed branch's Visible=true doesn't draw both).
3. **Floaters**: both `DisplayChange` implementations now emit an
   `UIOriginalText` in the bold money table **_12_bs** (same font as the R98
   readout twin), right-aligned in the 128px panel, tweened Y→−50 +
   Opacity→0 (UIOriginalText.Draw is now Opacity-aware — existing surfaces
   never set Opacity≠1), removed after 1500 ms. Modern UILabel path kept as
   the pre-IFF fallback. The `+/-§n` composition is port-side (the corpus has
   no '§' string — verified zero hits).
4. **Value fixes** (strings were port-authored where originals exist):
   - `UICallNeighborAlert` Call button: "Call" → table **180 'Phonebook
     Dialog' [0]** (this exact dialog's own table); Cancel button: → **142
     'ObjDialogs' [1]**. The "Call Neighbour" HEADING has no verbatim original
     (nearest 180[0] 'Call') — kept port-authored, disclosed in code.
   - `UISelectSkinAlert` title: "Please Select Outfit" → **220 'Clothing
     Dialog Text' [4]** (exact); "Adopt a Pet" → **221 'Pet Dialog Strings'
     [4] 'Please Select Your Pet'** (the original pet dialog title).

## Gate

- New check **`uijob`** (63 → 64): disk-pins chunks 136/137 (labels/counts/
  sha256 + verbatim skill names/'Job'/'Unemployed'/'n/a'/'Salary'/
  'Performance'), constructs the job subpanel + one Update tick (twins ==
  disk canon), pins the four value-fix lookups against the runtime string
  tables, and fires a money change on a constructed panel requiring the
  original-glyph floater path.

## Survey residuals (documented for future rounds)

- Not found in the original corpora (verified zero hits): "No items found",
  all search strings, "ON"/"OFF", "1st".."5th", "Unknown", terrain tool names,
  "Subway Musician", "Eyedropper Tool (E)", "§", "Call Neighbour", "Adopt a Pet".
- UIDesktopUCP tooltips map ~1:1 onto UIText.iff 138 'VCtlTips' (14 exact) —
  a cheap future sweep; relationship/inventory sort-button tooltips exist in
  tables 240/241.
- UIButtonSubpanel values already original (145) — renderer converts with any
  future subpanel-title sweep.
