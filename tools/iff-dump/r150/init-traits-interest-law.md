# R150 — INTEREST VALUES: THE 'init traits' TREE LAW (decoded from character data)

Round target (the R149 recommendation): real values in the Interest panel —
"the bars sit at 0 because nothing writes the TSO-packed interest words 46-53"
(the standing R145 residual).

## 1. Where TS1 interests actually come from (DECODED — from game data, not the binary)

The r145 assumption "import from SDSC chunks" was WRONG: house IFFs carry no
sim data chunks (House05.iff resource walk: SIMI/HOUS/ARRY/BMP/FLRm/WALm/objt/
ObjM/THMB/rsmp only) and character files (UserData/Characters/User00000.iff
'Bob Newbie') carry their interests as a **script** — BHAV 4100 'init traits',
called by the character's own 'init tree' (4097: op 8192 GenericTS1Call, then
op 4100 = run-tree-4100 with unset parameters) and 'load tree' (4098, same).

The tree (21 instructions, fully decoded via FreeSO's operand models —
op 2 = VMExpression [Lhs i16, Rhs i16, signed, operator, LhsScope, RhsScope];
op 8 = VMRandomNumber [dest i16, destScope u16, range i16, rangeScope u16]):

```
MyPersonData[20] = 5                       // ins0 (a personality-era field)
Local[0..3] = {3, 3, 3, 1}                 // ins1-4: bucket counters high/med/low/zero
Temps[0] = 46                              // ins5
loop (ins6..ins20):
    Parameters[0] = NextRandom(11)         // ins6: roll 0..10
    if roll == 0: if Local[3] > 0 {Local[3]=0; write} else re-roll     // one zero
    elif roll < 4: if Local[2]-- > 0 write else re-roll                 // three 1-3
    elif roll < 7: if Local[1]-- > 0 write else re-roll                 // three 4-6
    else:          if Local[0]-- > 0 write else re-roll                 // three 7-10
    write: MyPersonDataByTemp[0] = Parameters[0]   // pd[Temps[0]] = roll
    Temps[0]++  until Temps[0] == 56       // words 46..55 INCLUSIVE (10 writes)
```

**Scale: RAW 0..10** (the roll is stored verbatim — no x100 anywhere in the
tree). The topics: words 46-53 are the eight base-game interests (Travel/
Money-alias/Politics/The-60s/Weather/Sports/Music/Outdoors); 54-55 are two
further interest words the tree writes (the engine's expansion-era fields sit
in a separate runtime block). The cWinInterest panel's per-widget ÷100 display
law applies to the RUNTIME halfwords (0..1000) — the engine's script-slot →
runtime bridge carries the x100; script slots hold 0..10.

Also decoded from r145 §2.5 (re-confirmed as the port read law): widgets 8-11
(Toys/Aliens/Pets/School) deliberately SHARE widgets 0-3's person halfwords
(pet-topic polymorphism) — for humans those four cells MIRROR Travel/Money/
Politics/The-60s.

## 2. The probe that busted the r145 assumption

Headless gate lot, avatar person words 46..55 — BEFORE any port import code:
```
av16 [1,2,10,4,0,9,3,9,4,4]  av18 [5,10,3,6,4,2,10,3,9,0]  av21 [3,0,6,2,1,6,7,10,10,4]
```
Live rolls at RAW 0..10 with the exact bucket shape (1×0, 3×1-3, 3×4-6,
3×7-10). **The port VM already executes the sims' own init trees** — values
were never missing. The bars showed 0 because of a SCALE bug in the panel:
it byte-split the words (TSO packing) ×4 and then applied the engine's ÷100
display law to a 0..10 value (max display = 40/100 = 0).

## 3. Port changes

- Panel (`UIOriginalInterestGiftSubpanels.cs`): TopicWord -> single-word map
  {topics 0-7 = words 46-53; topics 8-11 = words 46-49 (the widget-share
  law); 12-14 wordless (expansion-era runtime block, disclosed residual)};
  Update reads the FULL word and x100s it into the display scale (the
  engine's bridge, disclosed) — FillWidthForValue's pinned 0..1000 law
  untouched.
- `SimitoneNeighbourGenerator` (FreeSO ee612e89): wrote roll*100 — corrected
  to the tree's raw roll (under the full-word read, x100 flat-clamped every
  townie bar to 10).
- `VMTS1Activator` + NEW `VMInterestTraits` (FreeSO ee612e89): avatars whose
  interest block loads zeroed (character files without an init-traits tree)
  get the exact tree law mirrored at lot load (same buckets, raw scale) —
  safety net; the live path stays the sims' own trees.

## 4. Gate

NEW **uiintvals** (90 -> 91), per avatar on the live lot:
- words 46..55 all in {0} U [1..10];
- the bucket shape: exactly one 0, three [1..3], three [4..6], three [7..10];
- the panel map pins (46-53 / widget-share / wordless tail);
- at least one avatar carries nonzero interests (import liveness).

## 5. Residuals (disclosed)

- Exercise/Food/Parties/Style/Hollywood/Tech/Romance sit in the expansion-era
  runtime block (+0x5a6..+0x5c0) with no port person word — those five cells
  render 0 (the r145 residual stands, now precisely scoped).
- Interests re-roll every session init (the port's TS1 lots reset state per
  load — standing disclosed model); persistence = the VM save path (fsov),
  which serializes person data.
- Words 54/55 carry two further interests with no display widget in the base
  human set (the tree writes them; the port stores them).
