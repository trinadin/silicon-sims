# R192 — The phonebook dialog (CWinPhoneBook)

Date: 2026-08-31

## Evidence boundary

Owner's local `game-data/The Sims/The Sims Complete` PPC executable
(SHA-256 `33c76da2…c06a5f`), `UIGraphics.far`, `UIText.iff`. Behavior,
constants, and hashes only.

## Decode

### CWinPhoneBook::Init (0x4592f0, 1904 B)

- Template `0xbc0` (3008, the shared standard modal) loaded first; the
  window's own SetArea expands to **652x426** (`addi 0x28c` / `0x1aa`).
- Resources: **80 kPhoneBookBkg** → `cpanel\backgrounds\phonebookbkg.bmp`
  **620x356**, **81 kPhoneBookIcon** → `cpanel\backgrounds\phoneicon.bmp`
  **33x23**.
- String set `0xb4` (180) registered via the 0x87f60(9) / 0x25f440 chain.
- Two modal buttons (dlg+0x110/+0x114) with the template `SetImage(8192,1)`
  call — the standard modal OK/Cancel pair.
- FillFamilyList 0x458830 / FillFamilyMembers 0x4584c2 / IsFamilyAvailable
  0x4586c2 build the two lists; GetSelectedNeighborID 0x458250 is the
  dialog result. SetIconToCaller 0x458080 / SetIconToNeighbor 0x458160
  swap the portrait.

### The anchor points

Init loads seven TOC slots (r2−0x4844..−0x482c). Their targets live in
BSS 0x96500-0x96528; the values are written by the static initializer at
**0x459c60** (recovered by disassembling it — the same BSS-table pattern
as the R190 snapshot dims):

| BSS | value | use |
|---|---|---|
| 0x96528 | (50, 50) | header/scroll origin (first SetArea pair) |
| 0x96520 | (625, 300) | member list far corner |
| 0x96518 | (34, 77) | family list origin |
| 0x96510 | (270, 275) | family list far corner |
| 0x96508 | (351, 77) | member list origin |
| 0x96500 | (75, 16) | the icon (in the modal title band) |

So (window-local): family list **(34,77)-(270,275)**, member list
**(351,77)-(625,300)**, icon at **(75,16)**, and the 620x356 board sits at
(16,35) within the 652x426 modal window.

### STR# 180 'Phonebook Dialog' (60 = 20 langs × 3)

sha256(chunk) = `ff31b7176a1edbc78f87c1414e44cbbdcd61dbbf96d5f734a5d36224b98990df`

English block: [0] `Call`, [1] `Call`, [2] `doesn't know anyone.` — the
title/Call-button caption and the empty-album message ("<caller> doesn't
know anyone.").

### Art (UIGraphics.far, byte-verified)

| member | dims |
|---|---|
| `cpanel\backgrounds\phonebookbkg.bmp` | 620x356 |
| `cpanel\backgrounds\phoneicon.bmp` | 33x23 |
| `Shared\Sys\ListBack.bmp` | 136x22 |
| `Shared\Sys\WinBtn.bmp` | 260x33 (4x65x33) |

## Port

- `UI/Panels/UIOriginalPhoneBookDialog.cs` — desktop phonebook: the
  template-3008 modal frame (R142 chrome law) at 652x426, the board at
  (16,35), the icon at (75,16), two glyph-text lists on ListBack 136x22
  row chrome clipped to the decoded rects, Call/Cancel as WinBtn cells on
  the modal's bottom-right row (Call disabled until a member is chosen),
  the census grouped by TS1FamilyNumber with pets filtered (the
  FillFamilyList/FillFamilyMembers law), and the empty state.
- `UILotControl`'s `TS1PhoneBook` branch routes desktop to the original
  dialog; touch keeps `UICallNeighborAlert` unchanged.

## Residuals (disclosed)

- The list rows are clipped, non-scrolling text (the engine cTSListCtrl
  scroll internals are not decoded; ListBack row chrome is exact).
- The modal buttons render as WinBtn cells; the template's `SetImage(8192)`
  button art identity was not resolved.
- The portrait-swap icons (SetIconToCaller/Neighbor) are not ported.

## Gate

`uiphone` pins: STR# 180 chunk sha + English block; the four art members;
the window/board/icon/list-rect constants; the live dialog (centered
652x426 position, Call disabled until selection, census + member selection
law, empty state). Targeted soak `uiphone,corpus`: PASS 6/0. Full default
gate: see PARITY.md R192 row.
