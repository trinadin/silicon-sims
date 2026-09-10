# R209 — The Vita idle canon: animation lists recovered verbatim + the sway law

## The lists (byte-verbatim from the engine data section)

TOC[-0x4cd4] = 0x5d670 (sec1) = the static animation-name string block.
`cWinVitaBtnSolo::Build{Adult,Child,Cat,Dog}AnimationList` (0x2da500/0x2da6a0/
0x2da860/0x2da9e0) copy the names in ORDER into this+0x218 (0x104-byte
cTSString slots) — the ORDER IS THE WEIGHT (breathing repeats dominate).
`SetPerson` (0x2db640) dispatches by type, then calls the window's vtable+0x170
pick virtual and resets the cursor this+0x210 = -1.

- **ADULT (10 slots)**: heyyou1, breathe, mirror-admire-self-start,
  **GENDER SLOT** (person->0x60e == 0 → mirror-admire-self-loop2, else loop1),
  mirror-admire-self-approve, breathe, breathe, celebrate-short, breathe,
  breathe → breathing weight 5/10.
- **CHILD (13 slots)**: heyyou1, breathe×2, mirror-admire-self-start,
  mirror-admire-self-loop2, mirror-admire-self-approve, breathe×2, happy-clap,
  breathe×3, '' (an empty 13th copy at +0x13e — terminator/builder quirk,
  disclosed) → breathing weight 7/13.
- **CAT (12)**: stand-tailswish-loop-fast, hiss-start/loop/sizing-up/
  rhow-angry-one/rhow-angry-long-low/rhow-angry-long-high/stop,
  napfloor-stand-trans-sit, sit-chase-tail-start/slow-loop/stop.
- **DOG (18)**: sit-wag, sit-wag-loop-slow, sit-wag-stop, sit-bark-start,
  sit-bark, sit-bark-stop, napfloor-sit-trans-stand, circle-start,
  circle-fascinated, circle-stop, napfloor-stand-trans-sit,
  sit-chase-tail-start/slow-loop/stop, sit-scratchears-l-start/loop-fast/
  loop-flick/stop.

## The idle sway (cWinVitaBtnSolo Init 0x2dbc00 / ctor 0x2dbd80)

this+0x1e8 = a **SineGenerator**: SetAmplitude(gA[0] × gB[0]) in Init,
SetAmplitude(gA[0] × gB[8]) in the ctor (gA = TOC[-0x713c] = 0x59bf38,
gB = TOC[-0x4ce4] = 0x59b9a0 — BOTH beyond sec1's unpacked size = BSS
runtime-initialized floats, statically unrecoverable, disclosed),
**SetPeriod(10000 ms)**, SetDuration(0x7FFFFFFF), Start(callback). The sine
value feeds `UpdateTransform` 0x2db250 (SineGenerator::GetVal 0x14e4d0 +
RotationTf/mat4 chain) — the preview's subtle idle sway.

## Residual (disclosed)

The pick virtual (vtable+0x170; vtable TOC[-0x68b0] = 0x5d1d8 — CFM-glued
layout, slot mapping unreliable statically) and the human one-shot scheduler
cadence are unrecovered; only `AnimatePet` 0x2dac22 statically consumes the
lists (the pet path). The port does not yet play named idle animations in the
CAS world — the canon lists + weights + the sway period are RECOVERED AND
GATE-PINNED (below), so the data side of this item is closed; the playback
integration is the disclosed delta.

## Gate

`uivita` (default suite): the four lists pinned verbatim from
`UIOriginalVitaIdleLaw` (names, order/weights, the gender split, the child
terminator), the sway period 10000, and the BSS-amplitude disclosure flag.

Evidence: r209-disasm-vita-adultanimlist.txt / -setperson.txt /
-updatetransform.txt / -solo-tspaint.txt / -startvitaboy.txt + the TOC
resolution (sec1 unpacked via tools/iff-dump/pef_unpack.py). No proprietary
payload (animation FILE NAMES are engine identifiers, not EA art bytes).
