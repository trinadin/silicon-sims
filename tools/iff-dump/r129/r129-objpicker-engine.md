# R129 — Engine decode II: the STR# 159 consumer's exact semantics

Engine-derived evidence class (R90/R95/R128 lineage): constants recovered
from the ORIGINAL PPC binary `game-data/The Sims/The Sims Complete`
(6,486,820 bytes), decoded with `tools/iff-dump/ppc_decode.py`. Raw decode
listings: `r129-decode.txt`; symbol/field scan: `r129-scan.txt`
(`scan_r129.py`).

## 0. TOOLING CORRECTION (affects R128's conclusions)

**The R91/R92 "byte-delta" branch rule was wrong for `b`/`bl` (op 18).**
It kept only the low 14 bits of the 24-bit LI field (mod-64KB wrap). For
the ladder's long calls (true delta ≈ −0x81000) it lost exactly 0x80000:
R128's "helper fragments" at 0x183870–0x1841b0 were mis-addressed — the
REAL targets are 0x80000 lower and are named functions. Fixed in
`ppc_decode.py` (standard 24-bit LI ×4, validated: `bl` word 0x4BF7EF99 →
0x1041B0 = `ObjSelector::AdultsOnly` entry, 6/6 rung calls land on
symbol-named entries). Op-16 (`bc`) was always correct. Also noted while
reading: the decoder's eq/ne branch LABELS are inverted vs the
architecture (0x4182 = beq, 0x4082 = bne); targets were always right —
all R129 polarity statements below use the architectural semantics.

## 1. The named cast (symbols at function ends, R129 symbol map)

| addr | function | role |
|---|---|---|
| 0xe2060 | `ObjectModule::GetSelectedPerson()` | walks the module's person list, first with +276 bit1; **the viewer** |
| 0x192840 | `cTool::ToolGetObject(id)` | resolves the multitile-group leader of the hit object (**the hovered entity**, an ObjSelector-layout object) |
| 0x191c30 | tooltip shower | `ShowToolTip(str)`; NULL hides |
| 0x103870/0x103a40/0x103c10/0x103df0/0x103fd0/0x1041b0 | `ObjSelector::{People,Pets,Cats,Dogs,Children,Adults}Only` | the six rung helpers — REAL functions, not outlined fragments |
| 0x1850c4 | `cObjPickerTool::Release` body | R128's "TryClick" — the actual tooltip path |
| 0x185cec | `cObjPickerTool::Click` body | sets `this+104` (mode) and `this+184` (clicked person) |

## 2. The ObjSelector audience predicates (instruction-literal)

All six share one shape: lazily resolve the object's OWN interaction
table (`this+100` ← master `+96/+14` id via 0xfe6b0), then walk entries
(`[t+0]`=count, `[t+12]`=array, **entry+20 = the 32-bit TTAB flags**):

- **Debug skip**: entry flag bit 7 (0x80) set → entry ignored.
- Violations return the offending flag (0x200/0x400 — treated as FALSE by
  the ladder, which tests the low byte).
- Completion returns `(count != 0)` via the CW idiom
  `neg r0,r8; andc r0,r0,r8; rlwinm r3,r0,1,31,31` (= bit 30 of
  (−r8 & ~r8), 1 iff r8>0) — **an empty/all-debug table is FALSE**.

| predicate | TRUE iff every live entry… | and NO entry… |
|---|---|---|
| `AdultsOnly` | has NoChild (0x10) | has AllowCats (0x200) or AllowDogs (0x400) |
| `ChildrenOnly` | has NoAdult (0x40) | has AllowCats or AllowDogs |
| `PetsOnly` | has NoChild AND NoAdult | — |
| `DogsOnly` | has NoChild AND NoAdult | has AllowCats |
| `CatsOnly` | has NoChild AND NoAdult | has AllowDogs |
| `PeopleOnly` | (≥1 live entry) | has AllowCats or AllowDogs |

Note the ORIGINAL marks "pets-only" by excluding both human ages
(0x10+0x40), not by the Allow bits; AllowCats/AllowDogs only split the
pet audience. **Own table only — no global-tree merge.**

## 3. `Release` (the tooltip path), polarity-corrected

Setup: `GetSelectedPerson()` → `this+176`; **null → no tooltip at all**
(0x18519c → epilogue). `r17 = personType(+1536) ≠ 0 && < 18` (**human
person**; pets are ≥ 18). `r18 = person+1550 bit0` = **cat**;
`r22 = person+1550 bit1` = **dog** (TestInteraction 0x106488 pairs bit1
with TTE 0x400 = TS1AllowDogs). `ToolGetObject()` → `this+172` (hovered
leader). `r21 = hovered+284` (the OWN tree-table handle; **rungs require
r21 ≠ 0**). A game-level bit (`[TOC−30604]+102` bit 5) gates every
pet-related rung — read as **"pets enabled"**; constant-true on Complete.

`this+104` (mode, set by `Click`): **31** = normal menu (the ladder
runs ONLY here — R128 had this inverted); **29/42** = pet-click contexts
(29 when the resolved person has the cat bit; 42 otherwise) → the [3]/[0]
blocks; 28/30/43/5 → no tooltip. `this+52` = pending tooltip string
(**[3]/[0]-at-29/42 require it NULL**).

**The ladder (mode 31, hovered ≠ 0), fixed priority:**

1. `[1] adults` — viewer is a person (r17|cat|dog) && table && AdultsOnly
2. `[2] kids` — viewer NOT human && table && ChildrenOnly
3. `[4] dogs` — viewer not-cat && table && DogsOnly && petG
4. `[5] cats` — viewer not-dog && table && CatsOnly && petG
5. `[6] pets` — viewer not-cat && not-dog && table && PetsOnly && petG
6. `[7] people` — viewer is-cat|is-dog && table && PeopleOnly && petG
7. `[8] no user-directed` — viewer is pet && `*(ObjectModule global,
   TOC−28948) == 0` && petG (pets take no user direction; the global's
   first word is the debug pet-control flag — false in the port)
8. else `[0] no actions` — the terminal default (also: hovered == null)

**[3] 'This is the active character'** (NOT part of the ladder): pet-click
modes 29/42, `hovered == this+184` (the clicked selected person) and
`this+52 == 0`. I.e. **a pet viewer hovering itself**. A HUMAN self-hover
at mode 31 goes through the ladder like any object.

**[8] is pet-only** — a human viewer can NEVER get [8]; the human default
is [0]. (Resolves the R125 [0]/[8] interpretation and the R128 note that
the port over-shows [8].)

## 4. Corrections to R128 (all caused by the branch-rule + polarity labels)

- The six "outlined fragments" were mis-addressed long-call targets.
- The ladder runs at mode 31 (not ≠31); [3] is the mode-29/42 block.
- Rung gates were polarity-inverted (e.g. 'adults' gate is
  (r17|bit0|bit1)≠0, not r17&&bit0&&!bit1; rungs need helper==1, not 0;
  viewer bit must be SET, not clear).
- `this+52` must be NULL (not ≠0) for [3]/[0]-at-29/42.
- `+184` is the clicked-person slot (stored by Click 0x185d78); Release
  stores its own fetch at +176.
- R128's "viewer+102 bit5" is `[TOC−30604]+102` — a game-level pet gate,
  not a viewer field.

## 5. Port mapping (R129 change, disclosed)

`UILotControl.ObjectTooltipReason` now implements the ladder verbatim:
own-table aggregates (Debug skipped, non-empty required), rung order
1→2→4→5→6→7→8→0, `[3]` = pet self-hover, human default `[0]`, pet
default `[8]`. Dropped: the global-tree merge in the classifier (engine
uses the own table only — the pie builder still merges globals, and the
hover name/reason choice still uses that). Constant-true mappings
(Complete): petG, the −28948 debug flag (false → [8] reachable),
`this+52 == 0`, mode ≡ 31-or-pet-context.

## 6. Residuals (disclosed)

- `TOC−28948` identified only as an ObjectModule-adjacent global whose
  first word (a debug pet-control flag per behavior) gates [8] and a
  dog/tree-1988 special case in TestInteraction; not byte-mapped.
- Modes 28/30/43's exact UI contexts (non-tooltip paths) not decoded.
- TestInteraction's tree id 0x7613 (Click's mode-42 branch) unnamed.
