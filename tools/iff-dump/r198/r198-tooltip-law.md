# R198 — The tooltip window + color law (cDefaultTTWindow) and the STR# 159 routing fix

Tier B line: "tooltip box + error-color unification (the pinned green; STR# 159
out-of-range ids 16/21/22/24/27)". The box law was already canon (DrawTooltip's
geometry + face-1/size-7 font, pinned by the renderer probes); this round
settled the COLOR model and eliminated the out-of-range ids.

## 1. The tooltip window model (decoded)

Disasms in `r198-disasm-ttwindow.txt` (+ ProductButton/cTool excerpts in this doc).

- **ONE shared tooltip window.** `cDefaultTTWindow::ctor` 0x3b9aa0 has exactly
  ONE caller: `cTSWinMgrW95::GetDefaultTTWindow` 0x51c7d4 (r195/callers.py
  scan). Every tooltip in the game renders through this singleton.
- **Init 0x3b97f0**: `this->0xe4 = 0` (the NORMAL color slot — handle 0 = the
  default pen, BLACK) @0x3b9828; `this->0xe8 = palette->(0xFF,0,0)` (the ERROR
  slot = RED, stored 16-bit @0x3b985c-0x3b9860); the font is the factory's
  face 1 size 7 @0x3b9884-0x3b98a0 (the known law); SetToolTip sizes to
  textWidth+6 x charHeight+2, ink at (3,1).
- **SetColor 0x3b9770** ({0 = normal, 1 = error, other = normal}) delegates to
  the helper 0x3b9310 which selects 0xe4 or 0xe8 into the font and re-sizes.
  **Exactly ONE caller in the whole text section**:
  `ProductButton::GetToolTipsWindow` 0x20b880 —
  `funds < product->0xc (price) → SetColor(1)` = RED, else `SetColor(0)`.
  So the ONLY red tooltip in the original is the UNAFFORDABLE CATALOG PRODUCT.
- **The catalog green**: `cWinCatalog::Init` 0x26b458 creates RGB(31,124,31)
  into catalog+0x64 (the R145 decode; R148 mounted it in the port's
  UITooltipHandler). This is the CATALOG context's normal color — its exact
  consumer path was verified by R145 "only at the RGB level" and the +0x64
  loads found in the catalog region are stack-relative, so the install point
  into the shared window remains R145's reading (disclosed).
- **The live-mode path never recolors**: `cTool::SetToolTip` 0x191c30 (the
  tools that own the object-hover tooltips — cObjPickerTool et al.) calls the
  view's tooltip shower with no SetColor. The engine default pen (BLACK) is
  the law for object hover/reason tooltips. **This resolves the R159 finding
  "black error color vs the pinned green" IN FAVOR OF BLACK for live-mode
  tooltips** — the green is catalog-context-only, already correctly mounted
  by R148 where it applies.

## 2. The STR# 159 out-of-range ids (the port bug)

STR# 159 'ObjectTTs' has exactly **9 English entries** (0..8, sha-pinned by
`uitt` since R125). The port's old disabled-object branches called
`ShowErrorTooltip` with ids **16/21/22/24/27** — TSO EALand ids that DO NOT
EXIST in the TS1 table. The flags themselves
(`VMGameObjectDisableFlags.ForSale / LotCategoryWrong / TransactionIncomplete /
ObjectLimitExceeded / PendingRoommateDeletion`) are only ever set by TSO
netplay commands (VMChangePermissionsCmd / VMNetLockCmd / VMNetAsyncPriceCmd /
VMNetSimJoinCmd) — dead code on this port.

The engine's TS1 answer to a zero-action object is the R129 reason ladder
(`ObjectTooltipReason`), whose terminal is [0] "There are no actions
available".

## 3. Port

- `UILotControl`: `TooltipDefaultColor` (Black) + `TooltipErrorColor`
  (255,0,0) named constants with the decode; all three live tooltip color
  assignments now use the default constant; the five TSO branches replaced by
  the unified `DisabledObjectTooltipText` (→ the R129 ladder, `DisabledRouted`
  counter for the gate).
- `UIObjectHolder`: both tooltip color sites unified onto the named constant.
- No behavior change is observable on the TS1 path (the branches were dead);
  the change is law: if a disabled object ever appears, it gets the engine's
  ladder answer instead of an empty string.

## 4. Gate (`uitt` EXTENDED — no pins weakened)

- The R198 color-law pins: `TooltipDefaultColor == (0,0,0)`,
  `TooltipErrorColor == (255,0,0)` (headless).
- The disabled-routing probe: on a live corpus anchor,
  `DisabledObjectTooltipText(vm, obj, viewer) == ObjectTooltipReason(vm, obj,
  viewer)` and `DisabledRouted` advances — pinning the unified routing.
- The existing `e159.Count == 9` pin is the standing proof the retired ids
  were out of range.

## 5. Residuals (disclosed)

- The green's exact install point into the shared window (R145 reading kept).
- The error-red is unreachable in this port: the catalog product tooltip
  surface was replaced by the R124 description panel (kCatalogPopupBack), so
  no ProductButton-equivalent hover exists to color. Constant pins the law.
- The terrain tools' `disallowed → DarkRed` (FreeSO submodule, 4 files) is a
  port heuristic; the engine tools never recolor. It belongs to the STR# 149
  DirtToolErrs Tier B item (the tools' own error strings), left for that
  round.
