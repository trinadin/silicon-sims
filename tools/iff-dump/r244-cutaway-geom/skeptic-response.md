# R244 — response to independent skeptic review (verdict: FIX FIRST)

Dispositions below were decided against the binary, not against the review
text. Two corrections were verified and applied in full; one was partially
applied with its material claim REJECTED on instruction-level counter-evidence;
two were editorial and applied as requested. All changes live in this
directory only. Re-verification: `verify.py` pins 125 words and runs 8 fixture
groups; `ruff check` clean.

## C1 — TileToPoint zoom-rescale direction (ACCEPTED, material)

The skeptic is right. `subf. r6, r30, r3` at 0x1D3950 computes
`r6 = globalZoom - argZoom`, so with the `ble` branch at 0x1D3960 the DIVIDE
path (0x1D3968..0x1D3970, `slw 1<<r6` + `divw`) runs when
`globalZoom > argZoom`, and the MULTIPLY path (0x1D3980..0x1D3990) when
`globalZoom <= argZoom` — the opposite of the direction decode.md originally
printed. Fixed in decode.md §B prose and in the C# contract (`d = gz - zoom`).

## C2 — rescale is not persistent (ACCEPTED, material)

Confirmed: 0x1D39F0/0x1D39F4/0x1D39F8 store `r3`/`r4`/`r0` back to
global+8/+0/+4, and those registers still hold the PRE-call zoom/originX/originY
(loaded 0x1D3944/0x1D3958/0x1D395C; the rescale writes go to `r5`/`r6` only).
The rescaled origin exists transiently — the projection loads it from the
globals (0x1D39C8/0x1D39D0) after the rescale stores — and is restored before
return. The "persists into the global viewer" claim is removed from decode.md;
the C# contract now computes a local effective origin and never mutates
`CurrentViewer`. New pins 0x1D39F0/0x1D39F4/0x1D39F8, branch polarity pins
0x1D394C/0x1D3960, and the `subf` itself at 0x1D3950. The zoom_rescale fixture
was rewritten against this law (it previously asserted the inverted one).

## C3 — matrix bit helper (documentation ACCEPTED; mirror claim REJECTED on evidence)

Accepted and fixed: the idealized one-line `1LL << (x & 63)` contract is
replaced by the native two-word form — decode.md §A.2 now documents helper
0x591B10's exact `(hi, lo)` result, the separate OR into row+0/row+4, and the
unchecked sign-extended `(sbyte)y` row indexing (native has no per-row bounds
check beyond the world-bounds test). The C# contract now uses a two-word
`uint[64][2]` matrix with an exact `Mask64(x)`.

Rejected with counter-evidence: the proposed law "`(0,1,x)` returns
`(1<<(x&31), 1<<(x&31))`, mirroring the bit into both row words" does not match
the pinned instruction stream 0x591B10..0x591B2C (now pinned in full — all
eight words) under PPC semantics (`slw`/`srw` zero the result when bit 26 of
the count register is set, i.e. count&64):

* x=5: `srw 1, 27` = 0, `slw 1, (5-32&63)=37` = 0, `slw 1, 5` = 32 →
  **(0, 32)**, not (32, 32).
* x=40: `srw 1, (32-40&63)=56` = 0, `slw 1, 8` = 256, `slw 1, 40` = 0 →
  **(256, 0)**, not (256, 256).
* x=32: `srw 1, 0` = 1, `slw 1, 0` = 1, `slw 1, 32` = 0 → **(1, 0)** — high
  word bit 0 only; no low-word aliasing.

Note the proposed law is also internally inconsistent: its stated x=32 wart
"(2, 1)" is not what its own mirror formula predicts for x=32 ((1, 1)).
The helper is a faithful 64-bit left shift for every integer argument — the
native matrix is genuinely 64 distinct columns — so applying the mirror form
would set two bits per column and diverge from the original engine everywhere.
The new `matrix_mask` fixture encodes the exact helper model (9 sample points
including x=32, x=40, x=-33) and asserts the two non-mirror properties, so the
record shows the check was run against the pinned body, not assumed.

## C4 — labeling (ACCEPTED, editorial)

* The GetPackedAlt permutation table is now labeled as an offset into the
  **unpacked data image** (data-virtual 0x4AD40, TOC -0x5790), not "INIT
  section"/raw file wording (§0 table and §C.2).
* Rotation LUT naming fixed to the effective physical page: because of the
  `-0x2000` displacement, ComputeCutawayMatrix's term `rot` addresses
  physical page `rot - 1` with in-page offset `x*128 + y*2` (pages 0..2 for
  rot 1..3; only three of the four pages are addressed in the 64x64 domain),
  and the callees' term `((4-rot)&3)` addresses page `((4-rot)&3) - 1`
  (§A.6, §C.1, §C.2, UNRESOLVED 1).

## C5 — verify.py and contract hygiene (ACCEPTED)

* zoom_rescale fixture rewritten against the binary's actual law (see C1/C2),
  including the divw truncation case (`sdiv(-50, 8) == -6`, python `//` gives
  -7) and an equality case.
* Added pins: restore stores 0x1D39F0/0x1D39F4/0x1D39F8; branch polarity
  0x1D394C/0x1D3960 (plus `subf` 0x1D3950); side rotation 0x125330..0x125344
  (6 words); floor gate 0x128B0C/0x128B24 (these two also exist in r243's set —
  kept here so this round's file is self-contained).
* Docstring pin count corrected (now 125).
* decode.md C# `TileToPoint` referenced an undefined `altOut`; the parameter
  is now consistently named and typed (`byte[] altOut = null`, native reads
  `*r9`, i.e. `altOut[0]`).

## Resulting state

* decode.md: §B zoom law rewritten (C1+C2), §A.2 mask law in native two-word
  form with negative-y note (C3 accepted part), §A.6/§C/§0 LUT and data-image
  labeling (C4/C5), pseudocode updated, Reproduction updated.
* verify.py: 132 pins, 8 fixture groups, passes from an arbitrary cwd;
  `ruff check` clean.
* Nothing outside this directory was modified.

## Provenance note

Mid-fix, the reviewer's full write-up landed in this directory as
`skeptic-corrections.md`; it was checked line-by-line before finalizing.
Corroborations from it folded into the round record: the mangled name
`.AbsorbNewRoomList__4RoomFRCQ23std44ve...` at ~0x125B6C (confirms the fifth
room+0x34 writer's identity); 0x1296E4 stores 0 (RoomManager's own +0x34);
the perm-table indexing equivalence `record[k] = colEntry[ty*4 + perm[rot][k]]`
(identical address algebra to decode.md's `colBase + perm[rot][k]`); and the
`bl 0x590A30` direction-table helper only linking nodes at BSS 0x7F058..0x7F0AC
(never writing the 24 delta bytes at 0x7F040..0x7F057). Its C5 worked example
(under rot=2 both consumers address physical page 1) matches the applied
effective-page naming. Its C3 is the one material disagreement, resolved above
on instruction-level evidence with the full helper body now pinned.
