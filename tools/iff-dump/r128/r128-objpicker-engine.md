# R128 — Engine decode: the STR# 159 'ObjectTTs' consumer (cObjPickerTool)

Engine-derived evidence class (like R90/R95): constants recovered from the
ORIGINAL PPC engine binary `game-data/The Sims/The Sims Complete`
(6,486,820 bytes, `Joy!peffpwpc`), decoded with `tools/iff-dump/ppc_decode.py`
(R92 recipe; branch-displacement quirk re-used as validated there). Raw decode
listings: `r128-objpicker-decode.txt`. **No port change this round** — the
structure below is instruction-literal, but two semantic labels stay
unresolved (disclosed); the R125 port classifier's corpus-tested anchors are
unchanged until they can be mapped without guesswork.

## Proven, instruction-literal

### 1. The consumer and the 1-based index map (constructor)

`__ct__14cObjPickerToolFv` (body 0x1863a9–0x1865a0; symbol at 0x1865b2):

- 0x186450: `li r3, 159` — the ONLY real `li 159` in the 6.5 MB binary
  (3 li-159 sites total; the other two are InitSkillLookup/DBCFont-dtor
  coincidences). cObjPickerTool is STR# 159's sole consumer.
- 0x186444/0x186458: fetch table handle + `bl 0x17f440` (table load,
  r3 = status); `rlwinm r0,r3,0,24,31; bne 0x186560` — the string load is
  SKIPPED entirely if the table load fails (nonzero status).
- 0x186464–0x18655c: unrolled fetches `GetString(buf, i, -1)` for engine
  indices **i = 1..9**, each result string-object copied to
  `this + 108 + 4*(i-1)` (this+108 … this+140).
- 0x186568: `stw r5default, 104(r30)` — **this+104 initialized to 5**
  (a mode/audience field; TryClick compares it against 31).
- **The engine string index is 1-based**: engine idx i ↔ UIText English
  entry [i-1]. Slot map:

| slot | engine idx | STR# 159 entry |
|---|---|---|
| this+108 | 1 | [0] 'There are no actions available' |
| this+112 | 2 | [1] 'adults' |
| this+116 | 3 | [2] 'kids' |
| this+120 | 4 | [3] 'This is the active character' |
| this+124 | 5 | [4] 'dogs' |
| this+128 | 6 | [5] 'cats' |
| this+132 | 7 | [6] 'pets' |
| this+136 | 8 | [7] 'people' |
| this+140 | 9 | [8] 'There are no user-directed actions available' |

### 2. The tooltip ladder (`TryClick`, body 0x1850f0–0x185af0)

Setup (0x185190–0x1851d4): `bl 0x182060` → r3 entity stored **this+184**
(nonzero → early-out at 0x18519c); entity+1536 read into the r17 gate
(`r17 = !(type==0 || type==18)`); entity+1550 bits → **r18 = bit0**, **r22 =
bit1**; `bl 0x182840` → **this+172** (the hovered entity; if nonzero at
0x1851e8 → straight to 'no actions' 0x1853c0).

The ladder (0x1851f0–0x185370), fixed priority, each rung =
`flags && entity+284==0 && helper()==0 && viewer(*r26)+102 bit5==0 → show`:

1. **'adults'** (112): r17 && r18 && !r22, helper 0x1841b0
2. **'kids'** (116): r17, helper 0x183fd0
3. **'dogs'** (124): r18, helper 0x183df0
4. **'cats'** (128): r22, helper 0x183c10
5. **'pets'** (132): r18 && r22, helper 0x183a40
6. **'people'** (136): !(r18&&r22), helper 0x183870

Fallthrough M (0x185374): `r18&&r22 → 'no actions'` (0x1853b0); else
`*r20(global)==0 → 'no actions'`; else `viewer bit5 set → 'no actions'`;
else **'no user-directed actions'** (140, 0x1853a0). Three separate paths
converge on 108 'no actions' (0x1853b0/0x1853c0/0x185428) — **[0] is the
terminal default; [8] is the narrow case**, gated by entity bits + a global +
the viewer bit.

### 3. [3] 'This is the active character' (0x1853d0–0x185404)

Shown only when `this+104 == 31` (the mode field — 0x1851dc compares it
against 31 BEFORE the ladder) **and** `this+172 != this+184` (hovered entity ≠
the other stored entity) **and** `this+52 != 0`. The port's simple
hover-self→[3] is close in spirit (an entity comparison) but the engine's
condition is mode-scoped — disclosed, not yet portable.

### 4. Cross-reference probes

- entity+1550 is a bitfield read at 49 sites; at 0x106488 (TestInteraction
  body, symbol 0x106649): bit1 of +1550 gates a check of **TreeTableEntry+20
  bit 10 = 0x400 = TS1AllowDogs** (the same bit VMThread.CheckTS1Action
  rejects on) — the +1550 bits pair with the TTAB audience flags.
- The rung helpers are compiler-outlined shared fragments (their bl targets
  land inside DoMenu/OnChar/MenuItemChanged bodies — CodeWarrior common-code
  factoring); the 'pets' fragment (0x183a40) walks 32-byte interaction-table
  entries (`rlwinm r0, r4<<5; lbz 12(r3); lwz 28(r3)`) — the helpers are
  interaction-table predicates.

## Disclosed unresolved (the round's residuals)

1. The semantic names of entity+1550 bit0/bit1 (r18/r22) and viewer+102
   bit5. The TTE-0x400 pairing proves bit1 relates to the dog audience, but
   two consistent-ish readings remain; the helpers' full decode (DoMenu body
   0x184068–0x184b90 + the TestSim family 0x1049dd–0x106649) is the next
   engine session.
2. What *r20 (global at TOC −28948) is (gates [8] vs [0]).
3. this+104's audience/mode semantics (init 5, compared 31).

## Port impact (why no change)

The R125 port classifier (`UILotControl.ObjectTooltipReason`) is
corpus-anchor-tested for pets/dogs/cats/self and stays. The engine evidence
adds: rung PRIORITY is fixed audience-ladder order (the port classifies
viewer-first), [0] is the engine's terminal default (the port reaches [8] in
several fallthroughs), and every rung carries a viewer-bit veto the port
lacks. None of these are safe to port until the helper semantics resolve —
recorded here so the next engine round starts from this map.
