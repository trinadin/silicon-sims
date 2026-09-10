# R197 — Native move-in lot handler law (cWinNeighborhoodVC::MoveInModeLotHandler)

The last disclosed R174 residual was "Desktop move-in mode retains the validated
Simitone surface until its separate native handler is recovered." This round
recovers it: `MoveInModeLotHandler__18cWinNeighborhoodVCFi` @0x471d82 (884 B,
disasm in `r197-disasm-lotmodehandlers-vc.txt`), plus the surrounding
regular-mode law it completes.

## 1. The move-in click law (the port target)

HouseInfo is embedded at lotBtn+0x18c (ctor @0x2d5290 zeroes +0x18c..+0x1a8 and
sets +0x1b0 = −1; fields below are HouseInfo-relative). The clicked lot index
r4 ∈ [1,10] indexes `nbhd->0xec[lot-1]` (10 lot buttons).

```
MoveInModeLotHandler(lot):
  lotData = nbhd->0xec[lot-1]->0x18c            ; null → return
  if lotData->0x14 (TUTORIAL flag):             ; AskDialog 132[16]/[17], style 0
      "Tutorial House" / "This is a tutorial house.  Families may not move into it."
  if !FamilyQueryOK(nbhd->0x168):               ; 0x233a80 also fills the budget
      SetMode(0); return                        ; (exit move-in mode silently)
  if lotData->0x10 == 0 (precomputed CAN-AFFORD == false):
      AskDialog 132[2]/[3] "Sorry" / "You can't afford it."
  if lotData->0x18 != 0 (HOUSE BUILT):
      if lotData->0x24 != -1 (occupied):
          AskDialog 132[14]/[15] "House Occupied" / "...occupied by another family."
      elif lotData->0x1c (PRICE) > budget:
          AskDialog 132[2]/[3]
      else:
          ret = AskDialog 132[0]/[1] "Purchase House?" /
                "Are you sure you want to buy this house?"   ; style 2 = YesNo
          if ret == 4 (YES): SetMode(0); deselect 10 lot btns;
              DoFirstTimeTownies / RefillTownies(30) / DoFirstTimeTouristFamilies /
              RefillTouristFamilies(10); LoadGame(lot, family)
  else (EMPTY LOT):
      same structure, but the confirm is AskDialog 132[6]/[7]
      "Purchase Lot?" / "Do you want to buy this lot?"  → YES → move in + LoadGame
```

- AskDialog = 0x253f00 (inside the cSimsApp::MessageDialog 0x253cf0 family);
  arg r6 is the style (0 = OK row, 2 = YesNo row), YES returns 4. The port's
  R142 message-box law (GenDlg nine-slice + RGB(0,0,82) fill + WinBtn
  PositionButtons row) is this dialog's chrome — already live via UIMobileAlert.
- The string pointer array at TOC−0x46b0 maps LINEARLY onto STR# 132
  'MoveInModeStrs': offset/4 = string index (verified: +0x00→[0], +0x08→[2],
  +0x18→[6], +0x38→[14], +0x40→[16]).
- SetMode(0) call @0x471edc = 0x46fd70 (same SetMode the VC handlers share);
  LoadGame = cSimsApp::LoadGame 0x258680.

## 2. HouseInfo field map (as consumed by the mode handlers)

| off | meaning | evidence |
|-----|---------|----------|
| +0x10 | can-afford (precomputed, 0 = refuse) | read first at 0x471e30 |
| +0x14 | TUTORIAL lot flag | ALSO GetPopupIndex: movein→[8] "Can't move in to the tutorial house!", evict→[9], other→[6] "To learn how to play the game..." |
| +0x18 | house built on lot | picks "Purchase House" vs "Purchase Lot" captions |
| +0x1c | purchase price | compared > budget |
| +0x24 | family id living there, −1 = none | occupied branch + GetPopupIndex occupied variants |

The tutorial flag's data origin was not recovered (no direct store to
lotBtn+0x1a0 exists; the HouseInfo is filled via a struct copy). It is pinned
in the port's pure decision function but no live lot is flagged (the port's
data layer carries no tutorial field) — disclosed.

## 3. Surrounding law confirmed this round (context, already ported R174)

- `RegularModeLotHandler` @0x472142 (580 B): occupied lot → deselect 10 lot
  buttons, DoFirstTimeTownies/RefillTownies(30)/DoFirstTimeTouristFamilies/
  RefillTouristFamilies(10), `Neighborhood::GetFamily(houseId)`, then
  **cSimsApp::LoadGame directly** — no query panel. Empty lot (0x24 == −1) →
  `cWinPictureDialog` (ctor 0x297a20, 320 B): SetTitle/SetMessage from the
  string set, `AddButton(GetButtonLabel(3), 3)`, `SetImage(LoadBuffer(5305))`,
  `DoSimsModalDialog(true, 3)` — the STR# 133 refusal R174 ported.
- `RegularModeBtnHandler` @0x4711d2: jump-table command dispatch (14 cases) —
  one case creates `cWinPickFamily` (ctor 0x2da310) at the neighborhood rect
  (the family-bin button), NOT a lot surface.
- `cWinLotPopup::GetPopupIndex` @0x2d5870 (576 B): STR# 134 variant selector,
  returns 0..0x15 by mode (0=regular, 1=movein, 2=evict, 4=visit) ×
  tutorial/occupied/zoning/afford. The port's R174 rollover implements the
  variant families this selector feeds.
- `cWinLotPopup::Init` @0x2d7540 (520 B): SetArea(l, t, l+0xc8, t+0x64) =
  200×100 popup, LoadBuffer(0x13f3 = 5107 HotSpotPopupTiler 111×111), 39
  photo strings loaded from STR# 134 names — matches the R174 port.
- `cWinLotPopup::SetHouseInfo` @0x2d5b00 (5788 B): photo-file dispatch for the
  venue lots (0x15..0x1f, 0x28..0x31, 0x51..0x63 — expansion surfaces); the
  base residential rollover text path is the GetPopupIndex variant → STR# 134.

## 4. Port-side data validation (staged base neighborhood)

SIMI(1) of HouseNN.iff: `built = ObjectsValue > 0 || ArchitectureValue > 0`
(engine +0x18), `price = PurchaseValue` (engine +0x1c):

| lot | lot/obj/arch | purchase | built | occupied (FAMI) |
|-----|--------------|----------|-------|-----------------|
| 1 | 10500/0/0 | 10,500 | no | no |
| 2 | 11500/0/56110 | 50,777 | YES | no |
| 3 | 8000/0/23991 | 24,793 | YES | no |
| 4 | 5500/0/0 | 5,500 | no | no |
| 5 | 7000/14660/15277 | 32,353 | YES | Goth |
| 6 | 3500/2655/5788 | 10,206 | YES | no |
| 7 | 6000/6129/7281 | 17,225 | YES | Newbie |
| 8/9/10 | ... | 7,000/3,500/14,144 | no/no/YES | no |

Bin families (Bachelor/Roomies/Pleasant) carry budget 20,000 → house 2
(§50,777) refuses "You can't afford it.", house 6 (§10,206) confirms
"Purchase House?" — the gate pins this truth table.

## 5. Port

- `TS1GameScreen.NativeMoveInDecision` (pure, headless-testable): the engine
  guard ORDER (tutorial → family-invalid → afford → occupied → house/lot
  confirm) with the STR# 132 index pairs as the return value.
- Desktop move-in clicks now dispatch natively
  (`UINeighbourhoodSelectionPanel.DispatchNativeDesktopSelection` no longer
  falls back for `movingFamily`); the zoomed Simitone card never mounts on
  desktop. Touch keeps the card + its simple confirm.
- Expansion guard kept AHEAD of the native branch for Old Town community
  lots (engine UL uses 132[12]/[13] "Community Lot" / "You cannot move a
  family into a community lot." — those exact strings now shown).
- The old unconditional "Purchase House?" confirm is replaced by the branch
  outcome; YES → MoveInAndPlay (SetFamilyForHouse + PlayHouse = the port's
  LoadGame(lot, family)).

## 6. Residuals (disclosed)

- Tutorial flag data origin unrecovered; no live lot is flagged (the branch is
  pinned headlessly, inert in live data).
- Townie-maintenance calls (DoFirstTimeTownies etc.) are engine population
  generation; the port's neighborhood provider maintains its own — not run on
  the dialog path.
- The UL (Unleashed multi-neighborhood) variant of the handler is not ported;
  the VC law covers the base neighborhood the desktop move-in runs in.
