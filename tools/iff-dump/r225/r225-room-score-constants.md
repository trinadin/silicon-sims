# R225 — the room-score constants RECOVERED + the pipeline unblocked (evidence round; no engine change)

The R224 blocker is resolved and the constants chain is fully decoded. The
fix scope is now PROVEN to be a porting project (collectors + law), not a
constant swap — so per the R128 rule nothing was changed blind.

## The R224 blocker resolved

**capstone 5.0.7 (PPC) as the reference disassembler** settled it: R223's
second decoder "correction" had **frB/frC swapped**. The real A-form field
order is frD(6-10), frA(11-15), **frB(16-20), frC(21-25)**, XO(26-30, 5
bits). Ground truth: `fsubs f5, f1, f2` at 0x126894 (0xECA11028) with src2
in bits 16-20; `fmuls f5, f5, f3` (0xECA500F2) with src2 in bits 21-25.
`ppc_decode.py` fixed (the R223 5-bit-XO part was correct; the swap was
not). With that, the "impossible" 0x126890-0x1268c0 pipeline is the classic
**int→double conversion idiom** (`0x4330` magic + `fsubs` + `xoris 0x8000`
sign fix) plus **min/max clamp ladders** (`fcmpu` + `ble/bge` + `fmr`) —
the words capstone flagged as "data islands" were mostly `fcmpu` ops the
linear scan skipped. The full 695-instruction disassembly of
`Room::ComputeRoom` @0x1267d0 is banked (capstone, skipdata).

## The constants chain (fully decoded)

```
RoomManager::ComputeRooms(int) @0x128ba2
  └─ RoomScoreConstants::UpdateConstants() @0x129a72
       ├─ provider struct r31 = TOC[-0x583c] — a 22-float table
       ├─ names r30 = TOC[-0x5848]
       ├─ FloatConstants::Set(name, value) @0x85f82 — a NAME→float map
       │    (0x861e0 lookup; plain stfs store; the cheat console can
       │     override these by name — that is the map's purpose)
       └─ stfs the values into the TOC block r2−0x2a00..−0x2a44
            (4 slots DERIVED by subtraction: −0x2a08, −0x2a10, −0x2a38, −0x2a40)
```

**THE PROVIDER TABLE @file 0x5a3538 (22 floats, verbatim):**

```
+0x00 0.8    +0x04 255    +0x08 0.1875  +0x0c 3.0
+0x10 5.0    +0x14 0.0    +0x18 1.0     +0x1c 0.5
+0x20 100.0  +0x24 0.01   +0x28 -100.0  +0x2c 0.0625
+0x30 30.0   +0x34 60.0   +0x38 -30.0   +0x3c -20.0
+0x40 20.0   +0x44 10.0   +0x48 45.0    +0x4c 2.0
+0x50 -50.0  +0x54 -40.0
```

The UpdateConstants slot mapping (r31 field → TOC slot):
`0x30→−0x2a80(30, matching the TOC literal already there)`, `0x34→−0x2a00`,
`0x38→−0x2a04`, `0x3c→−0x2a0c`, `0x44→−0x2a14/−0x2a1c`, `0x48→−0x2a18`,
`0x4c→−0x2a20`, `0x18→−0x2a24`, `0x0c→−0x2a28`, `0x14→−0x2a2c`,
`0x3c→−0x2a30`, `0x50→−0x2a34`, `0x54→−0x2a3c/−0x2a44`; derived:
`−0x2a08 = [0x30]−[−0x2a04]`, `−0x2a10 = [0x40]−[−0x2a0c]`,
`−0x2a38 = [0x14]−[−0x2a3c]`, `−0x2a40 = [0x14]−[−0x2a44]`.

This settles r134's definitive-negative ladder hunt: **the room constants
are RUNTIME TUNING (a cheat-addressable FloatConstants map seeded from a
static table)** — there is no second static copy in the binary.

## ComputeRoom's law (shape, from the coherent disasm)

- OUTSIDE rooms (first IsOutside branch): `room+104 = K2a20·(room+80) +
  K2a24·(room+84)` — the wear/pool collector stats, straight linear.
- INSIDE rooms: `walls_half = (room+56 + sign)>>1` → int→double →
  `clamp vs K` → `÷K` → `×K'` → `+K''` (a NORMALIZED, clamped wall-density
  term), then multiplications of stored stats (`room+108` × K2a10), min/max
  ladders vs the ±10/±20/±40/±45 bracket constants, and window/door terms
  (`room+96/+100`) — final score stored to `room+108` as float.
- The exact final wiring of ~6 slots remains one focused session on the
  banked 695-line disasm (the mapping candidate set is fully enumerated
  above — 23 slots, 22 + 4 derived values).

## Why no fix shipped (the scope proof)

The original's INSIDE score needs **per-room collector stats the port does
not compute**: walls per room (`room+56` from CollectTileStats), windows
(`room+96`), doors (`room+100`), wear/pool (`room+80/+84` from
CollectObjectStats). The port's `RefreshRoomScore` has only area and
Σ RoomImpact. A faithful fix = porting the collectors over VMArchitecture's
wall/window/door model + the law — a multi-round feature, not a constant
swap (R224's two-point data already proved no constant-only law fits: imp≈1
rooms score 50–86 in the original). The port's approximation stays
disclosed with THIS document as the implementation map.

## Follow-up (R226 candidate)

1. Finish the exact inside-law wiring (one session on the banked disasm).
2. Port the tile/object collectors for room stats (walls/windows/doors per
   room) — the prerequisite for any faithful RefreshRoomScore.
3. Then implement + pin (a roomlaw gate comparing the computed vs the
   stored original values on the two fixtures).
