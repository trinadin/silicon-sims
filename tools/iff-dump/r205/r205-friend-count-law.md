# R205 — The family friend count: UCP readout + About dialog (STR# 162 + 138[20])

Extends r204/r204-str-family-law.md §162 (the decode started there).

## The readout (cWinViewControl)

- `cWinViewControl::PostChildDraw(bool)` @ **0x2b57c0** — when the bool is set:
  `count = CPState::GetFamilyFriendCount(this+0xcc)` → `cTSString::FromInt`
  → **compare** against the gadget's caption slot (this+0x1a8 **+0x54**) via
  0x4f4600 → only if DIFFERENT, set the caption through gadget vtable+0xa4.
  The readout refreshes every redraw but only writes on change.
- `CPState::GetFamilyFriendCount` @ **0x20c770** (348 B): builds a DEDUPLICATED
  set (StringSet-like: 0x1417e0 builder with capacity 0x100, inserts through
  0x37000) walking the family's friends, caches it at CPState+0x224, returns the
  set's count. The dedup law is stated verbatim by STR# 162[1]: "Friends are
  only counted once even if more than one Sim considers them a friend."
- Gadget creation in `cWinViewControl::Init` @0x2b5f30, block **0x2b74c0-0x2b7608**:
  `new(0x18c)` + `cTSWinBtn` ctor (0x50d720) — a BUTTON, font[10] (r144 recorded
  the font at 0x2b74f8) — text-measured auto-size, rect computed **relative to the
  money readout**: (moneyLeft−w, moneyBottom, moneyLeft, moneyBottom+h) — directly
  BELOW the money text, right-aligned to its left edge. **r144 misattributed this
  gadget to the clock** ("clock button chrome"); the clock digits actually paint
  in TSPaint @0x2b5978 centered at the plate dial (95,118..151,125). This round's
  PostChildDraw caption decode corrects the attribution: +0x1a8 = friend count.
- Tooltip: STR# 138 'VCtlTips' [20] 'Family Friend Count' — the one r117 swap
  that never had a port control to land on.

## The About dialog (the click law)

`cWinViewControl::TSOnCommand` @ **0x2b48c0**, branch @ **0x2b5114**:

```
sender == this+0x1a8 && (s16)ctx->0x1c->0x50 == 0:
    dlg = new(0x140) + cWinPictureDialog()   (ctor 0x297a20)
    dlg vtable+0x14 (Init)                    must succeed
    StringSetBase(&stk)                       (0x1460f0)
    0x25f440(0xa2, GetAnimationManager(), &set, 0)   // ← STR# 162, LITERAL
    set[1] → cWinPictureDialog::SetTitle (0x2974b0)  // 162[0] 'About Family Friend Count'
    set[2] → 0x296ab0 (SetMessage)                    // 162[1] body
    set[3] → AddButton(text, id=0) (0x297230)         // 162[2] 'OK' — ONE button
    → window-manager add + SetBlockSimulator + modal run
```

`li r3, 0xa2` = **162** in the instruction stream — the dialog is fed from
STR# 162 top to bottom, the same modal picture-dialog pattern as the R161
budget dialog. (The set is 1-based: set[1] == table [0].)

## Port

`UIDesktopUCP` (desktop): `FriendOriginal` (UIOriginalText, font[10], the money
face) refreshed from `ComputeFamilyFriendCount` every Update with the engine's
compare-then-set; geometry derived from the live money rect (X = money.X − w,
Y = 158 + money line height); Tooltip = 138[20]; ListenForMouse zone below the
money hotspot opens `BuildFriendCountDialog()` — a UIMobileAlert whose
Title/Message/button caption are 162[0]/[1]/[2] (explicit button text from the
SAME table, per the disasm; not the 152 system default) through GlobalShowDialog
modal, mirroring the R161 funds→budget wiring.

`ComputeFamilyFriendCount(game)`: distinct neighbors N where ANY family member M
has the port's established friendship predicate — mutual DAILY ≥ 50 both
directions (fwd[0]/rev[0], the UIJobSubpanel fame law), N not a family member.
The fame-only homeless rejection (otherFamily == 0) is NOT applied — that gate
belongs to Neighborhood::GetFamousFriendCount; townies count as family friends.
Disclosed: the 0x1417e0 set-builder internals (the exact engine-side friend
test) are undecoded; the port uses the port's standing engine-derived predicate.

## Gate

`uifriend` (default suite): STR# 162 disk canon sha-pinned (3 English entries
verbatim) + live mounts (162[0..2], 138[20]); the REAL UCP readout — text equals
the gate's own independent walk of the same law, geometry derived from the live
money rect, tooltip string; the compare-then-set law (double-update no-op,
forced-change rewrite); the constructed dialog's exact 162 content.

## Evidence

r205-disasm-viewcontrol-tsoncommand.txt (0x2b48c0, the 0x2b5114 branch);
r204/r204-disasm-postchilddraw.txt, r204-disasm-familyfriendcount.txt,
r204-disasm-viewcontrol-init.txt (the readout decode); r204/r204-str-tables-full.txt
(the 162 canon dump). No proprietary payload.
