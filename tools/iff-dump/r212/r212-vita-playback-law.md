# R212 — The Vita idle PLAYBACK: the AnimatePet cycle law, decoded and ported

Post-1.0 round #1 (the R209 disclosure "the CAS preview does not yet play the
named Vita idles" — canon was pinned, playback was the delta). This round
recovers the playback law statically and lands it.

## The decode (cWinVitaBtnSolo::AnimatePet 0x2dac20, 496B — r212-disasm-vita-animatepet.txt)

AnimatePet is the ONE static consumer of the canon lists, and the whole
playback law lives in it:

- **cursor this+0x210**: `-2` = stopped (early return); `-1` = idle/armed;
  `0..count-1` = the playing slot.
- **one-shot trigger byte this+0x18c** (set by SetPerson's tail, below): when
  set it clears itself, and if the cursor is `-1` RESEEDS it — `0`, or `8`
  when the cat flag (this+0x209) is set (cat[8] = napfloor-stand-trans-sit,
  the seated-start entry; pets are expansion scope on this port) — then plays.
- **the cycle is SEQUENTIAL, not random**: the channel-finished flag
  (channel+0x3c) drives `cursor++` with wrap at the list count (this+0x214,
  written by the builders: `cmpw r3, r0; ble keep; li r0,0; stw` → wrap when
  cursor > count-1). The repeated `*-idle-armsdown-breathe` entries ARE the
  engine's "weights" — breathing plays 5 of every 10 adult slots, 7 of 13
  child slots, in list order.
- the start path copies the cTSString at `this+0x218 + cursor*0x104` into
  this+0x190, resolves it (0x390160), plays it through the animator
  (0x38dbb0 → this+0x194 = the channel), resets the channel's frame word
  (channel+0x40 = 0) and the transform-mirror word (this+0x1e4 = 0), and
  installs the completion sink (0x38b7e0, `&this->0x1a4`).

## The driver and the person lifecycle

- **TSPaint 0x2db862** (r212-disasm-vita-solo-tspaint-full.txt) is the
  per-paint driver: world/camera setup → `UpdateTransform` (the sway sine)
  → a guard query (0x3e2460) → **AnimatePet** — note both arms of the
  `this+0x208` (dog-flag) branch call the SAME AnimatePet: the flag branch
  is dead; the advance law is type-agnostic.
- **SetPerson 0x2db640** (r212-disasm-vita-setperson.txt): resolves the
  person, creates the animator (this+0x198), dispatches the list builder by
  type (0x60e bit0 dog / bit1 cat / age 0<0x600<0x12 child / else adult —
  flags 0x208/0x209/0x20b/0x20a), calls the pick virtual (vtable+0x170),
  then `cursor = -1` and **trigger = 1** — the next paint seeds from the top.
- **SetOutfit 0x2daf32 is a separate entry point**: an outfit change does NOT
  re-arm the cycle (the port's spinner changes must not restart the idles).
- The engine plays through its own per-paint channel — the CAS simulator is
  not involved (the port's CAS vm ticks exactly once, in InitializeLot).

## The port

`UIOriginalVitaIdlePlayer` (Panels) mirrors the machine: ctor/SetPerson arm
(cursor −1 + trigger), `Update` = the per-paint sway+AnimatePet (advances a
PRIVATE VMAnimationState at the FSO 30-frames/sec convention and poses with
`Animator.RenderFrame`, whose return value IS the channel-finished flag; the
avatar's own Animations list stays empty — the engine's channel likewise
lives in the window, not the simulator). `TS1CASScreen` mounts it in the
SimEdit preview block: person change (avatar/age/gender) → SetPerson reseed;
spinner changes ride SetOutfit semantics and do not restart the cycle. The
idle sway composes `SwayOffset` into `RadianDirection` around the CAS facing
(10000ms period canon; amplitude stays the R209 BSS disclosure).

## Disclosed residuals

- The sway amplitude remains BSS-unrecoverable; the port's ±0.05 rad is a
  disclosed substitute (SwayAmplitudeRad).
- The empty 13th child slot is treated as a terminator and skipped at play
  time (an empty cTSString is not a reference — the raw lookup would resolve
  a stray empty-named animation in the data); the engine's own failure path
  for an unresolvable name is statically undecodable.
- The pick virtual (vtable+0x170) slot identity stays CFM-glued/unreliable
  (R209); the port reads the SetPerson tail as the reseed law, which
  AnimatePet's trigger path confirms.
- The human "one-shot scheduler" (whether the human path ever deviates from
  the sequential cycle) remains unrecovered; the cycle is the only statically
  visible consumer and is type-agnostic.

## Data (content feasibility, verified before porting)

Every canon name resolves through the port's provider on this install:
a2o/c2o-heyyou1 + a2o/c2o-idle-armsdown-breathe in GameData/Animation
(Animation.far), a2o-mirror-admire-self-* in GameData/Objects + Global,
a2o/c2o-celebrate-short in GameData/Global, c2o-happy-clap in
ExpansionShared (an expansion-supplied animation the Complete-era engine
list references). Pinned live by the gate's content pass.

## Gate

`uivitaplay` (default suite, 124 → **125 checks**): content resolution of
every canon name, the builder's gender slot (loop2 male / loop1 female), the
sway math (0/+amp/0/−amp at 0/2500/5000/7500ms, period 10000), and a REAL
out-of-world avatar on the corpus vm (the SetFamilyMember recipe) driven
through the real player: seed = heyyou1 frame 0, 60-frame advance, the full
10-entry male cycle in order + wrap, SetPerson mid-cycle reseed → the female
slot lands on loop1, the child cycle = 13 plays + exactly 1 terminator skip
wrapped to c2o-heyyou1, the avatar's own Animations list untouched, and the
live facing within the sway band.

Evidence: r212-disasm-vita-animatepet.txt / -setperson.txt /
-solo-tspaint-full.txt + targeted-soak.log (8/0) + gate-run.log (125/0,
WRAPPER_EXIT=0). No proprietary payload (animation FILE NAMES are engine
identifiers, not EA art bytes).
