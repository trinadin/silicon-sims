# R90 — Recovering engine-side placement / timing (reverse-engineering guide)

## Why this exists

The IFF corpus (UIGraphics.far, Res_*.RT templates) declares neighborhood-screen
**bitmaps only** — `{BITMAP|RLEBMP,id,"path"}` rows. Sprite positions, frame
timing, movement logic and spawn rules are **engine code**, not data. They are
recoverable byte-verbatim from the original engine binary that ships with the
game install on this machine.

## The source binary

    game-data/The Sims/The Sims Complete   (6,486,820 bytes, PowerPC PEF)

Magic `Joy!peffpwpc`, formatVersion 1, 3 sections. `L'intégrale Les Sims` is the
French-locale twin. This is the Mac (Aspyr) Complete Collection port of the
original Maxis C++ engine — logic and constants carried over 1:1; only the ABI
is Classic Mac PPC (args r3..r10, TOC in r2).

## Method (the recipe, tooling: `../scan_pef_ppc.py`)

1. **Anchor scan.** Parse the id map from the R83 extract
   `uigr-orig/0007_Nbhd.h` (`const Sint32 kName = value;`). Walk the binary as
   4-byte-aligned big-endian words; decode immediate-bearing PPC instructions
   (`addi`/`li` op 14, `addic` 12, `mulli` 7, `ori` 24, `oris` 25, `cmpwi` 11).
   A hit whose immediate equals a known id is an **anchor**.
   * **strong** = `addi rD, r0, imm` (rA=0): a true literal the code passes as
     an argument — these are the real creation sites.
   * **weak** = `addi rD, rY≠0, imm`: base+offset coincidences (jump tables,
     struct offsets, relocation streams). Ignore unless clustered.
   Result this round: 356 anchors, 256 strong.
2. **Creation-call shape.** The engine builds each animated sprite as
   `li r3, RESID; addi r4, obj, slotOffset; li r5, TICKS; bl helper` —
   e.g. `kMagictownFogA: li r3,5717; addi r4,r30,136; li r5,4; bl`.
   `SpriteSlot::ActivateForTicks(int)` (symbol string at 0x13b9b1) gives the
   `TICKS` argument its semantics: **ticks per frame**.
3. **Function names are IN the binary.** CodeWarrior mangled symbol strings sit
   **at the end of each function's code** (string offset ≈ function end):
   `"loadWater__17cMagiclandSpriteFv"` at 0x57ce19 names the loop at 0x57cdb0;
   `"DoNessie__12cWinDowntownFv"` at 0x3face2; full screen-class maps below.
   Grep printable ASCII ≥8 chars; anything matching `Name__NclassFargs` is a
   function name.
4. **Coordinate-store idiom.** Fixed positions appear as
   `li rA, X; li rB, Y; stw rA, K(rObj); stw rB, K+4(rObj)`.
   Hunt adjacent `stw` pairs to consecutive offsets of one base register whose
   stored registers were just loaded with `li` constants.
5. **Magic-number division.** `addis r?, r0, 0x66666667` + `mulhw` = unsigned
   ÷5. (DoWater uses it: the frame advances only when a tick counter mod 5
   wraps.) Recognize `0x66666667`→÷5, `0x51EB851F`→÷100 etc. when reading
   timing loops.
6. **Cross-validate against the art** (`analyze_r89_positions.py`, R89).
   Engine coordinates must land inside art-derived regions. Both validations
   this round passed on the first try (see Findings).

## Findings — recovered engine constants (file offsets into `The Sims Complete`)

### cMagiclandSprites (0x57be00..0x57d2fa, embedded in cWinMagicland)
| routine | offset (string) | recovered behavior |
|---|---|---|
| LoadWater | 0x57ce19 | loop i=0..5: id = kMagictownWater0+i, slot = this+100+i*4, ticks=4 |
| LoadClouds (fog) | 0x57cec5 | kMagictownFogA/B/C → slots this+136/132/128, ticks=4 each |
| DoWater | 0x57c76e | **draws at (0, 62)**; frame = (frame+1) mod 6, advanced only when tick-counter mod 5 == 0; enable byte at this+904 |
| DoClouds | 0x57c869 | iterates **21 CloudInfo records, stride 36**, calls AnimateClouds on each |
| InitClouds | 0x57cac5 | **randomized placement**: x = rand()+60 base; fixed columns 142 and 262; seeds indices 10..15 and 17..20 of the 21-array |
| InitBalloons | 0x57cb3d | literal seed struct {4:-100, 8:0, 12:300, 16:400, 20:1, 24:100, 28:1, 32:1} (off-screen spawn + range) |
| AnimateBalloon | 0x57c685 | RNG wind-walk (`seed*35 - 24341` chain, `*1440` spread), wind thresholds {6,10,14,16,15,2,15}, then DrawBalloon(frame+1, wind) |
| DrawWater/DrawBalloon | 0x57c069/0x57c1a9 | explicit (x, y, frame) args |

**Validation:** DoWater's (0, 62) is exactly the coordinate Simitone's
UINeighbourhoodSelectionPanel already used for the Magicland waves —
independently arrived at, now engine-confirmed.

**R92 update:** the InitClouds/AnimateBalloon rows above were the first-pass
reading; the R92 section below decodes both functions fully and CORRECTS the
AnimateBalloon "seed\*35−24341 LCG" (it is `seed % 35` — 0xEA0FA0EB is the
÷35 magic) and the "x = rand()+60" note (the 60s are the literal Y-ladder
bases 60+16j / 60+10k; the x columns are 142/262 + spreads).

### Nessie is a cheat easter egg (not a screen fixture)
Strings in the binary: `"Usage: nessie. Same as SimCity."`,
`"Sorry only one Nessie at a time."`, `"nessie_sfx"`.
`DoNessie()` exists in THREE screen classes (symbol strings):
`cWinDowntown` 0x3face2, `cWinVacation` 0x43d062, `cWinNeighborhoodUL` 0x462751.
Each class's `CheatCallback` stores the spawn point just before calling it
(coordinate-store idiom, step 4):

| screen | spawn (x, y) | evidence |
|---|---|---|
| Downtown | (531, 438) | 0x3fc540, inside CheatCallback__12cWinDowntown (string 0x3fc5e9) |
| Vacation | (200, 530) | 0x43e990, inside CheatCallback__12cWinVacation (string 0x43ea39) |
| Neighborhood UL (Old Town) | **(118, 519)** | 0x465310 (li r4,118; li r0,519), inside CheatCallback__18cWinNeighborhoodUL (string 0x465445) |

`DoNessie` then swims her along a path: two rects built from base fields
this+260/264 and this+308/312 with hotspot offsets (+12,+11,+30,+10) and
(+8,+9,+0,+3), plus sprite-bounds copies (sprite+20..32).

**Validation:** the R89 art scan (`r89/r89-position-analysis.txt`) found the UL
backdrop river at cx=175, y=525..599 — engine spawn (118, 519) sits at that
band's left edge (our art-derived guess was (121, 505); engine is 6px lower).
Art analysis and engine scan agree.

### cWinStudiotown::InitCars (~0x475dd4, symbol string 0x47611e)
Six `carLaneInfo` structs at this+344, stride 96. Literal stores recovered:

| lane | f0 | f1 | f2 | f4 (cars) | f5 (spacing) |
|---|---|---|---|---|---|
| 0 | 468 | 806 | 420 | 13 | 40 |
| 1 | 480 | 806 | 440 | 11 | 35 |
| 2 | 492 | 806 | 475 | 10 | 30 |
| 3 | 606 | 538 | 816 | 8 | 20 |
| 4 | 606 | 574 | 816 | 7 | 12 |
| 5 | 606 | 610 | 816 | 5 | 10 |

Direction field (lane+356) = −1 for lanes 0–2 / +1 at the lane-3 boundary —
the `AnimateCarNE` / `AnimateCarSW` pair (0x475889 / 0x475b99). Per-car
sub-entries stride 24; RNG `% 18`; car bitmap sets selected via TOC
r2+8180/8184/8188; ids resolved per-frame through the switch dispatcher at
0x2590e8 (`car18`, `balloon13` cases) — **car/balloon ids are computed
(base+index), which is why they have zero strong `li` anchors.**
f1/f2 values 806/816 exceed the 600px backdrop height: the Studiotown world is
larger than the art and cars travel through off-screen segments (the original
screen scrolled).

### R91 — the car system decoded (evidence: `../r91/r91-*.txt`)

Function map (symbol strings at each body end):

| function | body | notes |
|---|---|---|
| AnimateCarNE | 0x4755b0..0x475874 | args (this→r28, lane→r29, carIdx→r5); `carIdx==0` takes its own branch @0x4755bc |
| AnimateCarSW | 0x4758c0..0x475b84 | same shape, mirrored arithmetic |
| AnimateCarLane | 0x475bd0..0x475d84 | dispatcher: per car `if (lane+84 field <= 0) AnimateCarNE else AnimateCarSW`, then a shared draw call @~0x4754A0 |
| InitCars | 0x475dd0..0x476108 | outer loop `r29 += 96` while lane < 6 (@0x4760ec/0x4760f0); inner car loop `r23 += 4` while car < cars |

Struct semantics RESOLVED (the R90 f0/f1/f2 residual is closed): the two road
groups pack their 3 coordinates differently —
* lanes 0–2 (dir −1, upper road, right→left): `{xStart@0, xEnd@4 (=806), y@8}`
* lanes 3–5 (dir +1, lower road, left→right): `{xStart@0 (=606), y@4, xEnd@8 (=816)}`
i.e. two horizontal roads at y ∈ {420, 440, 475} and {538, 574, 610}; the
InitCars per-car init branches on dir (`cmpwi r0, r30, 0` @0x475ff0) and swaps
which field is the wrap bound. Cars: 13/11/10/8/7/5 = **54 cars**, spaced
40/35/30/20/12/10 px.

Additional engine facts pinned this round:
* **Bitmap choice = `Random() % 18`** — magic 0x38E38E39 (2^35/18 family) +
  `mulli ×18` idiom at 0x475c08..0x475c3c (AnimateCarLane) and 0x475fec..
  0x47600c (InitCars). The 20-bitmap set (kStudiotownCar0-19) therefore has
  variants 18/19 NEVER randomly drawn; the bitmap is picked ONCE per car and
  never cycled (frames 0-13 are 24x15 sedans in duplicate pairs, 14-19 are
  26x19 taxis — the pairs may be facing variants, unresolved).
* **Movement = rare-event driven**: AnimateCarNE/SW bodies are four
  `Random() % 10`/`Random() % 40` gated blocks (magics 0x66666667 + `mulli`
  ×10/×40 @0x475614..0x4757d8) adjusting lane-state fields (lane+32/lane+44)
  against the tick counter at this+852. Exact per-block operand semantics
  (speed counter vs delay vs position) remain open — the port models base
  1px/step in dir with the 1-in-10 and 1-in-40 events each adding one extra
  px (DISCLOSED INTERPRETATION; table/modulus/direction/counts/spacing are
  literal).
* Per-lane bitmap-set descriptors: six 12-byte blobs at this+272..344
  initialized from THREE TOC sets (r2+8180/8184/8188) — the car art is three
  families shared across the six lanes. Per-car handle arrays are 4-byte
  entries (car loop `r23 += 4`); a second per-car record uses stride 24
  (`mulli ×24` @0x476020/0x476050).
* Lane+56 is the lane's car-array pointer; lane+92/88/84/80/76/72 are
  runtime state set outside InitCars (AnimateCarLane reads them at entry).
* InitCars' next-door neighbor function (0x476140+) is an STR# consumer
  (magic "STR#" 0x53545223 @0x4762ac) — not car-related.

Live port (R91): `UINeighborhoodCarLaneLayer` (six lanes, engine table,
`Random()%18` bitmap picks with a disclosed deterministic per-lane seed,
rare-event movement, wrap preserving the cars×spacing rhythm over the engine
lane range) mounted by the Studiotown neighborhood config; gated in `uinbhd`
(table literal re-declaration + %18 modulus + 20-frame mount + movement
invariants + 54-car total).

### R92 — Magicland clouds + balloon decoded (evidence: `../r92/`, tool `../ppc_decode.py`)

New tool: `ppc_decode.py <binary> 0xSTART 0xEND` — full PPC decoder (the R90
scanner only decodes immediate ops; the %N idioms live in
mulhw/srawi/rlwinm/subf sequences it prints as `.long`). Two dump quirks it
encodes, both verified on loop back-edges that land exactly on their Random/
store tops and on bl sites converging on one Random target (0x57a0d0):

* b/bc displacement = the SIGNED LOW 16 BITS of the word, as a byte delta
  directly (NOT x4). Standard 24-bit LI decode yields nonsense here.
* `addi rD, r0, 0` in this compiler's output is `li rD, 0` (rA=0 literal) —
  proven by DoWater's frame reset (below) and InitClouds' r31=0.

Function map (strings at function END; code below the string):

| routine | body | facts |
|---|---|---|
| DrawCloud | 0x57be00..0x57bf08 | (this, x, y, slot, frame); slot clamped 0..2; slot indexes the fog handle array this+128+4s (LoadClouds put FogA→+136, B→+132, C→+128); frame sentinel clamps (keep −255..0, else 0) |
| DrawWater | 0x57bf50..0x57c054 | (this, x, y, frame); frame must be 0..5; indexes this+100+4f |
| DrawBalloon | 0x57c090..0x57c1a9 | (this, x, y, frame); frame must be 0..15; indexes this+36+4f → **the 16 balloon bitmaps are the wind-state frames** |
| AnimateClouds | 0x57c1d0..0x57c2e8 | (this, CloudInfo*) — see below |
| AnimateBalloon | 0x57c330..0x57c65c | (this, BalloonInfo*) — see below |
| DoWater | 0x57c6c0..0x57c758 | enable this+904; counter this+900 (ctor −1); every counter%5==0 frame++ (0x66666667 ÷5 magic); frame==6 → **li 0 reset @0x57c72c-38** (R90's "mod 6" now literal-confirmed); static path draws (0,62) |
| DoBalloon | 0x57c790..0x57c7b0 | calls AnimateBalloon(this, this+4) |
| DoClouds | 0x57c7f0..0x57c854 | enable **this+908** (≠ water's 904); loop 21 × AnimateClouds(this, this+144+36i) |
| InitClouds | 0x57c890..0x57cab0 | four seeding loops — see below |
| InitBalloons | 0x57caf0..0x57cb28 | literal seed {+4:-100, +8:0, +12:300, +16:400, +20:1, +24:100, +28:1, +32:1} |
| LoadWater/LoadClouds/LoadBalloons | 0x57cdb0+/0x57ce40+/0x57cf10+ | ids 5720-25 → this+100+4i; fog 5717/5718/5719 → this+136/132/128; balloons 5701-16 → this+36+4i; ticks 4 |
| ctor | 0x57d020+ | zeroes this+36..140, this+900=−1, this+904(byte)=0 |

**CloudInfo layout** (9 words / 36 bytes, 21 records at this+144..900):
`+0 x, +4 y, +8 bitmapSlot, +12 tick(starts 1), +16 frame(starts −110, reset −255),
+20 interval, +24 wrapX, +28 wrapY, +32 drift`.

**InitClouds** (the loop bases are CLOUD-ARRAY-relative: `stw v, 148(rBase)`
= record field+4 where record0 = this+144, so bases this / this+360 / this+612
= records 0 / 10 / 17 — R90's original "indices 10..15 and 17..20" note was
RIGHT; a first R92 port attempt misread the bases as record bases and the
gate caught it, `r92p1.log`):

* loop 1, all 21 (cmpwi 21 @0x57c8f4): defaults `x=0, y=0, slot=rand%3
  (0x55555556 ÷3 magic), tick=1, frame=−110, interval=rand%2+2, wrapX=0,
  wrapY=0, drift=0` (+32 untouched).
* loop 2, records 0..7: `y=60+16j`; `x=T342[j] + j×(rand%5)`; `wrapX=T462[j]`;
  `drift=2`; `wrapY=0`. Tables via `lwz 342/462(r26)`, stride 32 — bases are
  PEF load-time relocations, not statically resolvable.
* loop 3, records 10..15: `y=60+16k`; `x=T282[k] + counter(10..15)×(rand%5)`;
  `wrapX=counter×(rand%5)`; `drift=T462b[k]+3`; `wrapY=0` (stride-48 tables,
  same relocation problem).
* loop 4, records 17..20 — ALL LITERAL (@0x57ca10..0x57ca9c): `y=60+10k`;
  `x=142+k×(rand%5)`; `wrapX=262+k×(rand%5)`; `drift=rand%5+2`; `wrapY=0`.
* records 8, 9, 16 keep the loop-1 defaults (x=0, drift=0 → they sit at the
  origin and "wrap" every tick: x≤0 → x=wrapX=0, frame=−255, slot re-roll).

**AnimateClouds** (per tick, per cloud, enable this+908):

```
if (tick % interval == 0) { x -= drift; y += 1; }
if (x <= 0) { x = wrapX; y = wrapY; frame = -255; slot = Random()%3; }
if (x > 100) frame += 2; else if (x < 30) frame -= 3;
tick++;  DrawCloud(this, x, y, slot, frame);
```

So the fog drifts LEFT 2..6px every 2..3 ticks and DOWN 1px per step, wraps
to its column with a fresh fog bitmap, and the frame sentinel wobbles ±2/3
near the columns (x>100 / x<30).

**AnimateBalloon** (per tick; BalloonInfo at this+4): the R90 reading
"seed\*35−24341 LCG" is CORRECTED — 0xEA0FA0EB is the ÷35 magic
(`addis 0xEA0F` + `addi −24341` — the magic's low half, not an LCG increment),
so the gates are `seed % 35`; the spread is `seed % 1440` (0xB60B60B7 ÷1440
magic + mulli 1440 @0x57c3b4). The seed (field +28, starts 1) increments once
per call. Wind walk (all integer literals engine-exact):

```
if (seed % 35 == 0) wind = 2;                       // reset
else if (wind < 16) wind++;                         // ladder caps 6/10/14/16
else if (wind != 15) { wind = 0; wind++; if (wind > 2) wind = 2; }  // init(400)-only path
if (wind >= 15) wind = 15;                          // clamp → sticks at 15 until %35
sway = (int)(MathLib(seed % 1440, TOC float block))  // trig + amplitude unresolved
DrawBalloon(this, 300 + sway, seed + 1, wind);       // bitmap #wind, y walks 1px/tick
f0 = sway; f4 = seed+1+f8; f16 = wind; seed++;
```

The float band constants that gate each ladder step live in a TOC global
block (`lfs [r31+k]` @0x57c390..0x57c5d8, r31 = `lwz −17052(r2)`) and call
MathLib (0x581bc0/0x581ba8) — unresolved. The wind cycle in practice:
climbs 2→15, sticks at 15, resets to 2 on seed%35.

### R93 — the nessie cheat decoded end-to-end (evidence: `../r93/`)

The UL (Community/Old-Town) screen's cheat path, fully decoded with
`ppc_decode.py`:

**CheatCallback__18cWinNeighborhoodUL** (body 0x4652c0..0x465445, string at
0x465445): the typed command is compared against the string at [base+530]
(= "nessie") via bl 0x46ce40. On match:

```
if (flag[this+240] == 0) {
    flag = 1;            // stb 1 @0x465308
    this+244 = 118;      // li pair @0x465310/0x465318  (spawn x)
    this+248 = 519;      //                             (spawn y)
    this+252 = -1;       // state: "just spawned"
    this+260 = -1;       // last-tick sync
    return 1;
} else { ShowMessage("Sorry only one Nessie at a time."); return 0; }  // @0x4652f0
```

Other commands follow (string at [base+570] etc. — not nessie-related).

**DoNessie__18cWinNeighborhoodUL** (body 0x462090..0x46273c, string at
0x462754; state = this+252, x = this+244, y = this+248, sprite = this+256,
flag = this+240, tick gate = this+260 vs this+476):

```
state < 0 : state=0; sprite=kNess1(5050) ticks 4; sync tick; draw; return
tick gate: if lastTick == curTick just draw  (one machine step per timer tick)
state 0   : play the sound named by [TOC-18180(r2)]+3  ("nessie_sfx" string)
            state=1; sprite=kNess2(5051)
state 1   : state=2; sprite=kNess3(5052)
state 2   : x -= 2; y += 1; if (x == 60) state=3     // the swim: 29 ticks
state 3   : state=4; sprite=kNess2(5051)
state 4   : state=5; sprite=kNess1(5050)
state >=5 : flag=0; sprite=null                       // END — residual CLOSED
```

So nessie surfaces (kNess1), chuffs (kNess2 + sfx), swims left-down 2/1 px
per tick from (118,519) to (60,548), dives (kNess2 → kNess1) and is gone;
the flag clears so the cheat can fire again. The draw itself goes through
the shared sprite helper 0x4629e0 with sprite-bounds + path offsets
(this+304/308); those offsets are 0 at spawn (not in the CheatCallback
stores) — the port draws at raw (x, y).

**Port:** UINeighborhoodNessieLayer (dormant until the cheat) + the panel's
Ctrl+Shift+C cheat bar accepting "nessie" — the R89 always-on family is
replaced. Downtown (531,438) and Vacation (200,530) have the same machine
in their own CheatCallbacks (strings 0x3fc5e9/0x43ea39); those screens do
not mount her in Simitone (no river), so only the UL path is live.

### R94 — investigation traces (evidence: `../r94/`, no port this round)

Three threads opened with `ppc_decode.py`, documented for the next session:

**InitCars fully re-verified** (`r94-initcars-full.txt`): the R91 six-lane
table literals re-decoded exactly (xStart 468/480/492, y 420/440/475, xStart
606 + xEnd 816 + y 538/574/610, cars 13/11/10/8/7/5, spacing 40/35/30/20/12/10
— li register block 0x475dd8..0x475ec0); direction split cmpwi r25,3
@0x475f54 → dir −1/+1 stored lane+12 @0x475f6c; outer lane loop +96 while
r25<6 @0x4760ec/0x4760f0; inner car loop r24 < [lane+16] with r23 += 4
@0x4760d8-0x4760e4. NEW: the per-car tail @0x476000-0x476010 computes
`bmp = Random()%18` then **`srawi r6, bmp, 1` (bmp/2)** — a half-index the
engine passes (with the &r1+72 pointer) into the per-car builder
bl 0x476320 ×3 per car (r6 = TOC r2+8184 / r2+8188 and the computed slot);
the builder's own entry is mid-loop-rotated code (back-edges 0x4762E4 /
0x4762FC), where R91's "+3/+4 per-car constants" live (offsets 107/121/132
into the per-car record visible at 0x476334/0x4763a0/0x4763a4). Not worth
the call-graph dive for a 3-4px hotspot — kept as a residual.

**The other screens' water positions are INDIRECT** (the last provisional
positions): only cMagiclandSprites has DoWater/DrawWater. cWinDowntown
loads its six water sprites to this+460..480 (ids 5400-5405, ticks 4,
@0x3ff6cc — this window is the LOAD path, not TSPaint as R90 guessed) and
`TSPaint__12cWinDowntown` (body ends at the string 0x3fef28) draws them by
reading the sprite bounds rect (sprite+20..32) offset by **this+308 (x) and
this+312 (y)** (@0x3fedf4/0x3fede8 in `r94-dt-tspaint-full.txt`) — the same
rect+offset idiom as AnimateClouds/DoNessie. NEXT STEP for whoever picks
this up: find the initializer that stores this+308/312 in cWinDowntown
(likely the ctor or the screen-open path) — then repeat for cWinVacation
(TSPaint string 0x441808) and cWinNeighborhoodVC (string 0x473808, the
TS1.0 screen). TSPaint symbol-string map for all screens is in
`r94/` via a binary grep (full list above in this file's R90 section).

**bl target arithmetic note**: the mid-function-looking bl 0x476320 from
InitCars is consistent with the s16-low-16 byte-delta rule (three call
sites spaced 0x20 with immediates differing by 0x20 each converge on one
target) — the target is real; the function's loop rotation is what makes it
look mid-function.

### R95 — the water positions closed (evidence: `../r95/`)

The R94 next-step panned out for Downtown and Vacation; UL/TS1.0 turned out
to be a different mechanism:

**cWinDowntown — water position (0, 0) ENGINE-LITERAL.** The ctor
(`__ct__12cWinDowntownFb`, body ~0x400288..0x400400) runs `li r9, 0`
(0x4002a8) and stores it to the water position fields **this+308 (x)** and
**this+312 (y)** (0x400308 / 0x400314, `r95-dt-ctor.txt`). TSPaint
(string 0x3fef28) draws the six water sprites (loaded to this+460..480,
ids 5400-5405 ticks 4 @0x3ff6cc) through exactly those fields
(0x3fede8/0x3fedf4 in `r95-dt-full.txt`). The computed +308/+312 stores in
OnCommand (0x3fe94c/0x3fe964) and the ctor-adjacent 0x3ff914 site are
scroll/zoom recalcs of the same fields — the base position is the ctor's 0.

**cWinVacation — water position (0, 0) ENGINE-LITERAL.** VI's field layout
differs: the water frame counter is this+464 (TSPaint `cmpwi 5` = mod 5),
the five wave sprites this+468..484 (ids 5410-5414 @0x44207c), and the
position fields are **this+316 (x)** and **this+320 (y)** — the ctor
(`__ct__12cWinVacationFb`, `li r9, 0` @0x442c58) zeroes both
(0x442cc0/0x442ccc, `r95-vi-ctor.txt`); TSPaint (string 0x441808) draws
through them (0x4416d4/0x4416b4 in `r95-vi-tspaint.txt`). NOTE: VI's
this+308 is NOT a position — it is itself a sprite slot (id 5443
@0x442068); only DT uses +308/+312 as the water x/y.

**UL waves + TS1.0 deltas — different mechanism (disclosed-unresolved).**
cWinNeighborhoodUL's waves (this+484..504, ids 5305-07/5312-14 @0x46b920)
and cWinNeighborhoodVC's deltas (this+408..416, ids 5302-04/5309-11
@0x473eb8, inside `Init__18cWinNeighborhoodVCFv`) are **self-drawing
SpriteSlots**: UL's TSPaint (string 0x46b270) advances [this+480] mod 12,
halves it (frame 0..5 — the water advances every OTHER tick), picks
[this+484+4*frame] and calls a vtable draw on the sprite object this+400
with NO position arguments (0x46b1c8..0x46b230, `r95-ul-tspaint.txt`); in
ALL of VC's code only `Shutdown` touches the delta slots (frees them at
0x473a44). Their positions live inside the SpriteSlot objects (set by the
loader `ActivateForTicks` machinery), not in window-class fields —
extracting them needs the SpriteSlot internals, not more window scans.
Both families mount at (0,0) in Simitone (full-screen overlays), which
matches the sprite-bounds draw idiom but is NOT engine-pinned.

**Port:** the Downtown water + Vacation waves families now carry explicit
`Vector2(0, 0)` positions with provenance comments, and the uinbhd gate
pins both via expectPos ("1:0" / "2:0") — the last two PROVISIONAL water
positions are closed. Gate 59/59 FIRST-TRY (`r95p1.log`).

### Other strong sites (for future windows)
* Downtown water dscreen00-05: six `li`s at 0x3ff6cc..0x3ff71c — the LOAD
  path into this+460..480 (R94/R95: NOT inside TSPaint; TSPaint draws via
  the this+308/+312 position fields, see the R95 section).
* Vacation Island water visland_waves001-005: 0x44207c..0x4420bc (load into
  this+468..484; position fields this+316/+320, see R95).
* UL waves kNghFrameDelta1-6_UL + NonUS: 0x46b920..0x46b9d4 (12 ids; load
  into this+484..504 — self-drawing SpriteSlots, see R95).
* TS1.0 waves kNghFrameDelta1-3 + NonUS: 0x473eb8..0x473f0c (6 ids; load
  into this+408..416 inside Init__VC — self-drawing SpriteSlots, see R95).
* Screen classes recovered: cWinDowntown / cWinVacation / cWinNeighborhoodUL /
  cWinNeighborhoodVC / cWinStudiotown / cWinMagicland / cWinNeighborhood —
  each with TSPaint / TSOnTimerMsg / CheatCallback / ShowWindow names in the
  string pool (grep `cWin`).

## Honest residuals (traces not yet run)
* R92-R105 — the InitClouds loop-2/3 x/wrapX/drift TABLES: STATICALLY
  EXHAUSTED (r105/r105-reloc-exhausted.md). The complete relocation
  simulator (all Ghidra opcode classes, 40,470 actions) leaves sec1+282..720
  ZERO; no sec-0 displacement relocation touches the lwz sites; the
  architectural decode is closed (xor XO-316 @0x57c8f8, r31 = li 0, base
  absolute 342+32j). The tables are runtime-initialized by an undiscovered
  routine OR the loop-2 window is misparsed — either needs an emulating
  decompiler. The 142/262 model stays DISCLOSED; the relocated sec1 image
  (r105/sec1-relocated-partial.bin) is the as-loaded data section for
  future slices.
* R92-R110 — AnimateBalloon's MathLib sway: the float block is RECOVERED
  (r104) and the MULTIPLY STRUCTURE is decoded (r110/r110-sway-multiply.md):
  sway = (int)(x × trig(wind×pi/720)) where x is a runtime int (r3 lineage,
  emulation-needed) — there is NO dedicated engine amplitude constant in the
  multiply; the block is fully accounted {0.785, 180.0, 0.0, gates
  1.5/3.0/4.5/5.0/6.0, magic} except 64.0 ([r31+0x48], unattributed, no lfs
  in this window). The port's amplitude-8 model stays DISCLOSED with the
  structure engine-decoded. NOTE: A-form float XO is 5 bits — a 10-bit
  extraction mislabels fmul as 'XO57' by eating frC's LSB.
* R92 — the balloon's off-screen respawn bound was not in the decoded window;
  the port resets at y 600 to the literal init (seed 1, wind 400), DISCLOSED.
* AnimateCarNE/SW per-block operand semantics (which of lane+32/lane+44 is
  speed vs delay vs position, and the carIdx==0 branch's exact role) — the
  rare-event STRUCTURE (%10, %40 gates vs the this+852 tick) is pinned, the
  per-block arithmetic is modeled (see R91 section).
* ~~Car sprite hotspot: InitCars writes +3/+4 pixel constants into per-car
  records; the port draws at raw (x, y) top-left~~ — CLOSED BY REFUTATION in
  R101 (r101/r101-hotspot-refuted.md): the "+3/+4" are record FIELD OFFSETS
  (sprite-slot pointer @0x476274, rect pointers +107/+121/+132) into the
  per-car display record of the animation function 0x476150..0x4763E4; the
  final draw passes r6=r7=0 (no pixel offsets) — the R91 note was a scanner
  artifact, and the port's raw (x, y) draw already matches the engine.
* Sedan duplicate-pair semantics (frames 0-13 pair up by sha256) — probably
  facing variants; unresolved.
* ~~DoNessie end-state logic (dive/disappear conditions) not decoded~~ —
  CLOSED in R93 (state 5 clears the flag and frees the sprite; see the R93
* ~~nessie_sfx table entry~~ — CLOSED in R108 (r108/r108-nessie-sfx.md):
  the TOC entry (sec1+0x38fc -> 0x6c6e0) holds "%d\0nessie_sfx\0...";
  the engine's +3 skips "%d\0" and plays PlaySound("nessie_sfx") —
  the port's by-name choice is engine-exact.
* ~~BalloonInfo field semantics inferred; AnimateBalloon's LCG not reversed~~ —
  CLOSED in R92 (layout + %35/%1440 walk decoded; see R92 section).
* The Windows `Sims.exe` variant is not on disk; cross-arch confirmation
  (x86 immediates) would strengthen pins but is not required — the Mac port
  is the shipped original engine.
* kDowntownWater/kVIWater window reads (positions inside TSPaint) listed but
  not yet decoded.

## Applying findings (policy)
Engine-derived constants are **engine canon**, a separate evidence class from
IFF canon: pin them in the gate labeled engine-derived (with file-offset
evidence), disclose in PARITY.md, and never present them as IFF data. Where
the original behavior is randomized (cloud placement, car lanes), faithful
randomization or the engine's literal band constants are both acceptable;
state which is used.
