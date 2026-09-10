# R132 — the People panel's remaining surfaces: follow-Sim + the ratings bars + the House tab

Round protocol: survey (Hermes x2, port side) → engine decode (original PPC binary,
byte-verbatim) → port → pin in the autotest gate → full gate green → this evidence.
Gate: **77/77 PASS** (`r132p2.log`; the honest first-run FAIL `track-ghost` is kept
in `r132p1.log` — the toggle worked, the assertion had assumed no avatar).

## 1. Engine decode — cluster map additions (all addresses in The Sims Complete)

Symbol scan (byte-by-byte, the R131 recipe) over 0x288000-0x292500 + 0x2a0000-0x2a8000:

| symbol | ends | body decoded |
|---|---|---|
| `.StopTracking__10cWinPeopleFv` | 0x2887ac | 0x288740-0x288798 |
| `.UpdateAtWorkStatus__10cWinPeopleFv` | 0x2885fc | (called from House TSPaint) |
| `.BuildRelationships / BuildInterests / BuildGifts` | 0x28a4e0 / 0x289584 / 0x289190 | — |
| `.SetPanel__10cWinPeopleFQ210cWinPeople5Panel` | 0x28aac8 | 0x28a510-0x28aac8 |
| `.SetPerson__10cWinPeopleFi` | 0x28bee4 | 0x28b400-0x28bee4 (r132-decode-setperson.txt) |
| `.TrackPerson__10cWinPeopleFP8cXPerson` | 0x28c068 | 0x28bf00-0x28c054 |
| `.TSOnCommand__10cWinPeopleFUlUl` | 0x28d234 | 0x28c068-0x28d234 (decoded, kept) |
| `cWinSubpanelHouse` Init / TSPaint | 0x2a1430 / 0x2a0a50 | 0x2a0bec-0x2a1430 / 0x2a03b4-0x2a0a50 |
| `cWinSubpanelJob` Init / TSPaint / SetupSummaryClient / SetFriendsNeeded | 0x2a4f50 / 0x2a3a78 / 0x2a2990 / 0x2a1f60 | Init 0x2a3d8c-0x2a4f50 (+ TSPaint, summary decoded) |
| `cWinSubpanelReportCard`, `cWinSweepMeter`, `cPerformanceBtn`(FlashyBtn) | 0x2a6af0+ | mapped only |

Plus the **HouseStats** class (symbol scan 0x8b900-0x8c200):
`GetObjectCount 0x8bba8, GetLotSize 0x8bbe8, GetNumBathrooms 0x8bc28,
GetNumBedrooms 0x8bc68, GetLayoutScore 0x8bca8, GetUpkeepScore 0x8bd70,
GetYardScore 0x8be04, GetFurnishingsScore 0x8bf68, GetSizeScore 0x8c0c8,
GetSquareFeet 0x8c108, ctor 0x8c174` — the House subpanel's 9-slot paint loop
walks THIS family. Score computation = R133 target.

## 2. TrackPerson / StopTracking — the follow-Sim crosshair

* `cWinPeople::TrackPerson(person)` (0x28bf00): if the gadget slot `this+580`
  is non-null, detach/destroy it (`0x509d50(this->580, 0, &rect0)`) and clear
  the slot. Then only when `person == *[TOC-28856]` (the global current-person)
  AND `person != 0`: resolve the person's index (`0x244290` → -1 check,
  `0x240640` flags, room chain reading `lha 1592(person)`), fetch the portrait
  gadget `this->240[index]`, store it at `this+580`, and attach it under
  parent `this+576` (`0x509d50(this->580, this->576, &rect0)`).
* `cWinPeople::StopTracking()` (0x288740): the pure teardown — same detach +
  slot clear + `0x47cb10(0)` trailer.
* The camera-follow itself lives in the engine's world layer, NOT in
  cWinPeople — the crosshair is the People-panel's tracked-person indicator.

## 3. The ratings bars — gadget canon + composition

RT ids (`0000_CPanel.h` / `0001_CPanel.RT`): `kHouseBars 4800 / kJobBars 4801 /
kRelBars 4802` (the PIE strips, `CPanel/Buttons/*.bmp` 63x26 — never appear as
code immediates; RT-referenced), `kHouseSubpanelBars 4900 / kJobSubpanelBars
4901 / kFameSubpanelBars 4822` (sheets), `kHouseSubBarsOff 4919 /
kJobSubBarsOff 4920 / kFameSubBarsOff 4823` (backdrops). Also mapped:
`kJobIconGeneric 4902` (Buttons/JobIconMultiButton.BMP), `kPersBkg 4903`,
`kJobPopupComposite 4905` (Backgrounds/JobIconMultiPopup.bmp),
`kUniversalCPBkg 4910` (Backgrounds/PanelBack.BMP), `kTallSubpanel 4918`
(Backgrounds/TallSubPanel.**TGA** — the one non-BMP member found),
`kSalaryBtnBMP 4981 / kPerformanceBtnBMP 4982`.

* `cWinSubpanelHouse::Init` 0x2a0ccc-0x2a0ce8:
  `factory(4900, &this+204, 4)` + `factory(4919, &this+208, 4)` (the R131
  gadget-factory recipe, 0x3b6190).
* `cWinSubpanelJob::Init` 0x2a3fb0-0x2a3ff0: **language-conditional** —
  English (`0x20c670()` returns 0) builds `4901/4920` (JOB art); the
  non-English branch builds `4900/4919` (HOUSE art). Then `factory(4905)` →
  helper `0x571c30(this+364, g)` and `factory(4902)` → `0x571c30(this+368, g)`.
* `cWinSubpanelHouse::TSPaint` 0x2a0458-0x2a06a0 — the 9-slot loop (point
  table `[TOC-20200] + slot*8`, 8-byte stride): the rating slot clamps the
  value to `[0,100]`, then `mulhw 0x6666_6667 + srawi 2` (signed **/10**) and
  `slwi 2` → **fill width = 4 x (value/10) px** — a 10-step bar, 4 px/step,
  40 px = the full sheet width (art confirms: 40x16 House / 40x11 Job / 80x16
  Fame). Draw order: the Off backdrop fill first (vtable+160), then the sheet
  region `(0,0,fillW,H)` over it, offset by the slot's point-table entry.
  Slots 5-8 are per-family-member mini bars (`this+276+(slot-5)*4` gadgets,
  float values via the `0x4330_0000`-idiom chain); the at-work status updater
  (`0x288630` = UpdateAtWorkStatus) is invoked from the English branch.

## 4. BRANCH-ENCODING CANON CORRECTION (methodological)

Architecturally (BO/BI decode): `beq=0x4182, bne=0x4082, blt=0x4180,
bgt=0x4181, ble=0x4081, bge=0x4080`. The R131 notes had **ble/bge swapped**
(`ble=0x4080/bge=0x4081`). R131's gauge conclusions used only the EQ/GT forms
(0x4182/0x4082/0x4181) — all correct under the true canon, so every R131 pin
stands; only the summary table was wrong. R132 used the corrected canon
(the [0,100] clamp only reads sanely with `0x4081`=ble, `0x4080`=bge).

## 5. Art ground truth (UIGraphics.far, byte-verbatim; gate rows R132RateArt)

| member | len | WxH | sha256 |
|---|---|---|---|
| cpanel\buttons\housebars.bmp | 2776 | 63x26 | a0d09dbd40080ff631fb81cb9e88ecc52921fde57e0fb6c22432c1f349a10d9a |
| cpanel\buttons\jobbars.bmp | 2764 | 63x26 | 50463f1f8e6db99513d55a0dc17d4eeeaa5b5a942d62a387df999a24739444a8 |
| cpanel\buttons\relbars.bmp | 2740 | 63x26 | b71beff520ff0888f8c31ff3f81f0a1b736d2ba407c4f818e40755b5800fff3f |
| cpanel\housesubbars.bmp | 1976 | 40x16 | 83a6513e05b431493f6678fbbc47e6399e0212cf932a31f914bb8045750057c9 |
| cpanel\jobsubbars.bmp | 1376 | 40x11 | 9321ea64c486488fb9b71be56f4d1143a7c96fadd1d283d02b25223bb6a83273 |
| cpanel\famesubbars.bmp | 1608 | 80x16 | 66256153d51d25d53658e3dea7209aad4e1483e62e0ef0bef58212895fd4702f |
| cpanel\backgrounds\housesubbarsoff.bmp | 1718 | 40x16 | 05d084b921497ca50e7a12aefef58dee152312087d1791af65640ed45329cf98 |
| cpanel\backgrounds\jobsubbarsoff.bmp | 1518 | 40x11 | 7a12bea361ce39fd52cd0112ff8b5e12c8a7de02ae2515f4e5d335bd800d8f68 |
| cpanel\backgrounds\famesubbarsoff.bmp | 2040 | 80x16 | b65257244c09578afe80d344526fe1271ffd10de4453caa102a00568d70d681f |

`cpanel\people\trackingtarget.bmp` (45x45,
24a8e71bc06e7855f1ceeae3cc07c7866918655c41bb0cdf19f1bda02fe60d83) re-verified
byte-identical to the R131 pin.

## 6. Port mapping

* **Follow-Sim** (`UIOriginalTrackButton` + `UIMotiveSubpanel.ToggleFollow`):
  the port's engine ALREADY had the follow mechanism dormant —
  `WorldState.ScrollAnchor` (re-centered every frame by `World.Update` →
  `CenterTo`; FreeSO's `UIVMPersonButton` is the reference user, with the
  same crosshair art) — but no Simitone UI ever SET it (only the manual-input
  clears at `UILotControl.cs:912/965`). The toggle sets/clears the anchor on
  `Game.SelectedAvatar.WorldUI`; the crosshair button dims at 0.35 alpha when
  inactive (disclosed interpretation — the engine mounts/dismounts the
  crosshair over a portrait instead).
* **Ratings bar** (`UIOriginalRatingBar`): Off backdrop + sheet fill
  `ComputeFill = 4*(clamp(v,0,100)/10)` — the decoded law, static and
  gate-pinned (0/49/50/100/-5/150 → 0/16/20/40/0/40).
* **Job tab**: the original Job Rating bar (STR#154 [14] 'Job Rating',
  4901/4920 art) beside the modern performance bar, `Value =
  JobPerformance` person data clamped to [0,100]; hidden on unemployment
  like the performance bar.
* **House tab** (NEW port surface, `UIHouseSubpanel`, live category 5,
  `kHouseBtn 4502` → cpanel\Buttons\House.bmp via the switcher's
  IFF-first OriginalName path): STR#154 [12] 'House Rating' / [13]
  'Friend Rating' captions on 40x16 HouseSubBars art. VALUES (disclosed):
  Friend = `FAMI.FamilyFriends` clamped to [0,100] (the engine's 0-100
  scale for it is undecoded); House = 0 — the HouseStats score computation
  is the R133 target, and the bar mounts empty rather than guessing.

## 7. Residuals banked

* HouseStats score COMPUTATION (the getters' inputs — how layout/upkeep/yard/
  furnishings/size scores derive from the lot) — R133's named target.
* The original House subpanel's full 9-bar layout (point-table offsets are
  TOC globals, statically unrecoverable) + per-member mini bars + at-work
  status; `cWinSubpanelReportCard` / `cWinSweepMeter` / Fame subpanel.
* The PIE strips (HouseBars/JobBars/RelBars 63x26) — art pinned this round;
  the pie-menu bar surface itself is unported.
* `kJobIconGeneric 4902` / `kJobPopupComposite 4905` gadgets (built in Job
  Init, slots this+368/364) — the job multi-icon button + popup composite.
* `kTallSubpanel 4918` (TallSubPanel.TGA) — a TGA member, unmapped surface.
* TrackPerson's helper chain (0x244290/0x240640/0x243f90/0xc4970) —
  person-index/room resolution, partially decoded.
