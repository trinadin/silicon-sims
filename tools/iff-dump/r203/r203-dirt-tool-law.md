# R203 — the dirt-tool error law: UIText STR# 149 'DirtToolErrs'

**Round:** R203 · **Gap:** Tier B STR# 149 DirtToolErrs + the R198 disclosed
residual ("the terrain tools' disallowed→DarkRed belongs to the STR 149 item")
· **Gate:** new `uidirt` check, default suite **116 → 117**.

## The table (disk canon, sha-pinned)

`GameData/UIText.iff` STR# 149 'DirtToolErrs' — format −3, 126 entries (18
locales), 7 English (chunk sha256 `8de848c01abe…5b486abb`):

| idx | English |
|---|---|
| 0 | Tile cannot be modified |
| 1 | Altitude is already as high as possible |
| 2 | Altitude is already as low as possible |
| 3 | Object requires flat surface |
| 4 | Can't divide a multi-tile object |
| 5 | Insufficient funds |
| 6 | Must be on first story |

## The engine decode (PPC, capdis over the raw image)

**`cDirtTool::UpdateErrorMessage` @0x18d230** (928 B) is the family's single
error surface:

- head path: `this->0x2c == 0 && this->0x38 == 0 && this->0x30 != 0` → formats
  `this->0x30` (the drag's cost) into a string (`cTSString` ctor 0x4f4e70 +
  a virtual format at 0x5a29e0 taking the value and its sign) →
  `cTool::SetToolTip` — **the plain "$N"/"-$N" cost readout is a tooltip**.
- switch path: `this->0x38` (error code) 0..8 through the jump table at
  TOC−0x5610; each case fetches an entry from the table global (TOC−0x6ecc,
  entry array, string at +0xc) and routes it through **`cTool::SetToolTip`
  @0x191c30** — the shared tooltip window whose color law R198 already pinned:
  **it never recolors → every dirt-tool message is BLACK**. One case loads a
  static string at TOC−0x560c+0x2b (unresolvable statically; codes 2/3/7/8
  have no setter in the family — see Residuals).
- dedup: `this->0x3c` remembers the last shown code; the message only
  re-fires on change.

**Error-code setters across the family** (every store of `this->0x38`):

| code | where | condition |
|---|---|---|
| 0 | Drag 0x173b2c, Float, Select, ctors | clear before each attempt |
| 1 | `MayModifyTerrain` 0x18d610 ×3 (map bounds; `GetRoofLayer` 0x162ab0 ≠ 0; `HasFlatObjects` 0x18dba0 on both corners), `cLevelDirtTool::Apply` ×4 (HasFlatObjects corners), `ForceGrade` ×2, `cGrassTool::Float` | tile not modifiable |
| 5 | `cLevelDirtTool::Apply` 0x17349c | the height-divide loop |
| 6 | `cLevelDirtTool::Drag` 0x173b5c | `SetFunds(cost)` returned funds < cost |
| 6 | `cLevelDirtTool::Release` 0x173860 | after `cTool::Refund` |

**Code = STR index + 1**, proven by the money path: Drag stores
`0x30 = cost`, clears the code, calls Apply, then `cSimulator::SetFunds(cost)`
and compares the returned funds against the cost — short → code 6, and 6−1=5
is 'Insufficient funds'. Likewise 1−1=0 'Tile cannot be modified' (the
MayModifyTerrain failures) and 5−1=4 "Can't divide a multi-tile object" (the
level tool's height loop). `HasFlatObjects` itself decodes as: any object on
the tile with flag 0x4000 at object+0x9e (r203-disasm above; the flat-footprint
object flag).

## The port

- New `TerrainToolErrors` (in BOTH parallel trees — the Simitone game uses the
  FSO.UI LotControls copies; the FreeSO classic client keeps its tso.client
  copies; both patched identically): the code constants, `Text(code)` →
  `GameFacade.Strings.GetString("149", idx)`, and `CostText(cost)` = the
  engine's "$N"/"-$N" readout.
- The three terrain tools (Raiser / Flatten / GrassPaint, both trees):
  - **DarkRed cost tooltip RETIRED** (the R198 residual): the engine has no red
    on this family — the cost readout is BLACK, and an unaffordable drag
    becomes STR# 149[5] 'Insufficient funds', still BLACK, through the same
    tooltip.
  - the tso.client fail-reason branch (VMPlacementError.CantPlaceOnSlope /
    LocationOutOfBounds popups) replaced by STR# 149[0] 'Tile cannot be
    modified' on the shared tooltip — the engine's code-1 message for exactly
    those failures.
  - the Error sound on newly-disallowed drags retained (sound law not decoded
    this round, disclosed).

## The gate — `uidirt` (default suite, 117 checks)

1. disk canon: STR# 'DirtToolErrs' → chunkID 149, 126 entries, 7 English,
   sha256-verbatim, exact 7-string sequence;
2. live mount: `GameFacade.Strings.GetString("149", 0/4/5)` == disk (the
   tools' actual read path);
3. law: the truth table — Text(1)=[0], Text(5)=[4], Text(6)=[5],
   Text(0)=null, CostText(±50).

## Verification

Targeted soak `CHECKNAME,uidirt,uitt,corpus`: uidirt PASS
(canon=True/mount=True/law=True), uitt PASS (no tooltip-law regression).
FULL DEFAULT GATE **passed=117 failed=0**, carseek PASS, clean exit-probe
chain, AUTOTEST_WRAPPER_EXIT=0. Dist byte-match: Simitone.Client 9c07c1a3,
FSO.SimAntics f99d2205, FSO.Client 969445c4, FSO.Common d8d9ba66,
FSO.UI 86f2f538.

## Residuals (disclosed)

- codes 2/3/8 (altitude max/min, first story) have NO setter anywhere in the
  cDirtTool family (altitude clamping may live below the tool layer or the
  entries serve the shared table's other consumers); the port pins the three
  live codes plus the cost readout. `Text()` returns null for the unset codes
  exactly like the engine's clear-on-default.
- the static-string case (code 7?) could not be resolved without the runtime
  TOC; unused by any setter found.
- wall/floor painters (UIWallPainter, UIFloorPainter, UIWallPlacer) still use
  their own budget coloring — different tool families with their own (undecoded)
  engine tables; out of this round's scope.
- the jump table itself is CFM-relocated (not present in the raw image) — the
  code→case binding is established semantically via the setter conditions, with
  the money path as the anchor proof.

No proprietary payload (canon strings quoted are the error strings themselves,
the round's subject matter).
