# R244 skeptic round — adversarial re-derivation of the cutaway-geometry decode

Independent re-disassembly of every cited range from `game-data/The Sims/The Sims Complete`
(SHA256 re-verified `33c76da2…06a5f`), with a from-scratch PPC decoder; all r244 pins
re-read from raw bytes (21 spot-checked one by one, all 111 re-verified by execution).
Container facts re-proven: PEF section table (code file 0x8E90..0x5C22E8, packed data
at 0x5C22F0 unpacking to 0x7BF80, BSS to 0x989A4); all nine cited TOC slots hold the
claimed values (data 0xC18→0x7F040, 0xC1C→0x7F0B8, 0xFB8→0x907D0, 0xCB0→0x85C48,
0x874→0x8510C, 0xDEC→0x93C38, 0xCAC→0x85C44, 0x2870→0x4AD40, 0x27BC→0x59A698).

## Verdict per headline claim

1. Direction table + sweep law — CONFIRMED (two prose nits, see C5).
2. TileToPoint 0x1D3850 — projection formula CONFIRMED; zoom-rescale law CORRECTED (C1)
   and "persistent side effect" CORRECTED (C2).
3. GetTheoreticalWallExtent 0x1CEF30 — CONFIRMED in full.
4. Room::ComputeCutawayMatrix rectangle law — CONFIRMED; matrix bit-mask helper needs
   caveat C3.
5. ResolveDiagonal 0x128360 — CONFIRMED.
6. RoomManager::ComputeCutaway floor filter + room+0x34 — CONFIRMED.

## C1 (HIGH — must fix before port). Zoom-rescale direction is inverted in decode.md §B

decode.md says `delta = argZoom − globalZoom; if delta > 0: origin /= (1 << delta); else multiply`.
The binary is the opposite:

* 0x1D3950 `subf. r6, r30, r3` → **r6 = globalZoom − argZoom** (r3 = `lwz 8(r31)` global
  zoom at 0x1D3944; r30 = argZoom; PPC `subf rD,rA,rB` = rB − rA).
* 0x1D3960 `ble 0x1D3980`: r6 ≤ 0 (argZoom ≥ globalZoom) → multiply path:
  0x1D3980 `neg r5, r6`, 0x1D3988 `slw r5, r1, r5` (1 << (argZoom−globalZoom)),
  0x1D398C/0x1D3990 `mullw` both origins.
* r6 > 0 (argZoom < globalZoom) → divide path: 0x1D3968 `slw r5, r5, r6`
  (1 << (globalZoom−argZoom)), 0x1D396C/0x1D3970 `divw` both origins.

Correct law: `d = oldZoom − argZoom; if d > 0: origin = SDiv(origin, 1<<d) else origin *= 1<<(-d)`.
Sanity: asking for a higher zoom multiplies the pixel origin; decode.md's version would
shrink it. verify.py's `zoom_rescale` fixture encodes the inverted law (`sdiv(-7, 4)` for a
zoom increase) — it passes only because it re-derives decode.md's own error.

## C2 (HIGH — must fix before port). The global-viewer mutation is temporary, not persistent

decode.md: "(this is a persistent side effect on the global viewer …)". Actually:

* 0x1D3944 `lwz r3, 8(r31)` = old zoom; 0x1D3958/0x1D395C `lwz r4/r0, 0/4(r31)` = old origins.
* None of r3/r4/r0 is reassigned anywhere between those loads and the stores at
  **0x1D39F0 `stw r3, 8(r31)` / 0x1D39F4 `stw r4, 0(r31)` / 0x1D39F8 `stw r0, 4(r31)`**
  (checked instruction-by-instruction across both rescale branches and the projection block;
  no `bl` intervenes). The projection itself consumes the *updated* values via the re-loads
  at 0x1D399C (zoom), 0x1D39C8/0x1D39D0 (rescaled origins).

Net effect: TileToPoint temporarily overrides zoom+origins to the requested zoom, projects,
and **restores the previous global state before returning**. The C# contract as written
would permanently corrupt CurrentViewer on every off-zoom call. Correct port: compute local
scaled origin/zoom, never touch the global.

## C3 (MEDIUM — decide before port). Matrix bit helper is NOT a clean 64-bit shift

decode.md §A.2: "64-bit mask 1LL << (x & 63) via helper 0x591B10 … the helper is a 64-bit
left shift". For its only-ever argument pattern (r3=0, r4=1, r5=x — verified at **all 24
call sites**: 0x125260/0x125550/0x1255FC/0x1256D4/0x125780/0x125858/0x125904, 0x1919DC,
0x191A54, 0x191AD4, 0x1CC3EC, 0x1CC4B8, 0x1CF2C4, 0x1CF978, 0x1CFEE0, 0x1D01DC, 0x1D2E2C,
0x1D2F10, 0x2BDEA4, 0x2BDF7C, 0x2BDFF4, 0x2BE0C0, 0x2BE160, 0x2BE1F0), exact simulation of
0x591B10..0x591B2C gives:

* x ∈ 0..31 → (r3, r4) = (1 << x, 1 << x)
* x ∈ 33..63 → (1 << (x−32), 1 << (x−32)); x = 32 → (2, 1) (wartenfall)

i.e. the "mask" sets bit (x & 31) in **both** 32-bit words. Since the seed (0x125260),
bit-test (0x125574..0x125588) and bit-set (0x1255FC..) all use the same pair, the native
matrix behaves as 64 rows × **32 aliased columns** (columns x and x+32 collide; x=32 is a
mixed wart), not 64 distinct columns. decode.md's port pseudocode
`room.Matrix[(sbyte)y] |= 1UL << ((sbyte)x & 63)` diverges from native for any x ≥ 32.
Native-faithful form: `row |= 0x0000000100000001UL << (x & 31)` (plus the x=32 exception
if that cell is reachable). Also note native rows are indexed by `(sbyte)y * 8` with no
bounds check (negative y would write before room+0x7C); the C# port must decide that case.

## C4 (LOW, labeling). The altitude-permutation table at "INIT 0x4AD40"

The bytes are real but 0x4AD40 is an address in the **unpacked initialized-data image**
(inside the packed data section, below BSS base 0x7BF80), not a raw file offset and not a
separate section: unpacked data[0x4AD40:0x4AD50] = `00 01 02 03 03 00 01 02 02 03 00 01
01 02 03 00` — exactly the claimed rot0..rot3 rows — followed at +0x10 by `00000004
00000018` and the RTTI name `cRotatableWorld` (data 0x4AD58). The code-pointer literal
0x59A698 does map to file 0x5A3528 (`7fffffff 7fffffff 80000000 80000000` verified) and
GetPackedAlt loads it via TOC −0x5790 (`lwzx` at 0x161E98..0x161EB8 confirms record[k] =
colEntry[ty*4 + perm[rot][k]], so byte 1 = colBase[1]/[0]/[3]/[2] for rot 0..3 — confirmed).

## C5 (LOW, prose). Rotation-LUT page naming

All three LUT consumers index `pageTerm*8192 + x*128 + y*2 − 8192`, so the physically
accessed page is pageTerm−1: ComputeCutawayMatrix 0x1251F8..0x125220 uses term = rot
(page rot−1), HasWalls 0x1259F8.. / GetPackedAlt 0x161E1C.. use term = (4−rot)&3
(page 3−rot). The formulas in decode.md include the −8192, so the arithmetic is right, but
the prose "page rot" and "page 2 self-inverse for 180°" mislabel the physical pages (under
rot=2 both consumers hit page 1). No port impact while the generator stays unresolved.

## Smaller items

* decode.md C# contract typo: `altOut` is undefined in `TileToPoint` (parameter is `altRec`).
* verify.py docstring says "118 instruction words"; the script actually pins and prints 111
  (decode.md's 111 is correct). Cosmetic.
* 0x1296E4 (the sixth `stw ..,0x34` in 0x124000..0x12A000, decoded as RoomManager::__ct)
  stores **0** — so even if its object classification were wrong, the room+0x34 ∈ {0,1}
  law and the five-writer list for value 1 are unaffected. Byte-scan reproduced exactly:
  0x125B38=1, 0x127384=0, 0x128800=1, 0x1288AC=1, 0x128D28=1, 0x1296E4=0(other object).
* `AbsorbNewRoomList` identity confirmed: mangled name string
  `.AbsorbNewRoomList__4RoomFRCQ23std44ve…` sits at ~0x125B6C.
* Direction-table initializer 0x610B0..0x61250 re-verified entry-by-entry from raw words;
  the `CTilePt` helper at 0x590A30 only links 12-byte nodes at BSS 0x7F058..0x7F0AC
  (TOC −0x5C2C..−0x5C48 all resolve to that range) and never writes the 24 delta bytes at
  0x7F040..0x7F057.

## verify.py audit

* Pins: all real instructions at the cited file offsets (21 hand-checked against raw
  bytes; all 111 checked by execution — script exits 0). No pin overlaps r243's 29-pin set
  (verified from `r243-cutaway/verified-inputs.json`; those 29 also still match the file).
  Nothing is pinned that decode.md does not justify.
* Fixtures: `direction_table` is genuinely meaningful (re-executes the pinned init stream).
  `zoom_rescale` is a **tautology of a wrong law** (see C1) — the fixture asserts the
  opposite of what its own neighboring pins 0x1D3950/0x1D3968/0x1D396C prove. Fixtures
  tile_to_point / wall_extent / overlap / side_rotation / floor_filter are self-consistent
  transcriptions (fine as regression pins, but they add no independent evidence); the
  overlap fixture does exercise the strict-inequality boundary (touch-only = false), which
  is good. `floor_filter`'s two "independent" formulas are algebraically the same reading
  of the rlwinm, and the actual gate/filter words (0x128B0C, 0x128B18-0x128B30) are not
  pinned.
* Missing pins (claims rest on unpinned words): the restore stores 0x1D39F0/F4/F8 (absent,
  consistent with the missed persistence error), branch polarity words 0x1D394C/0x1D3960,
  side-rotation sequence 0x125330..0x125344, shift-helper body beyond its first word.
  I verified all of these by hand — they support the decode except where C1–C3 say otherwise.

## Corrections that MUST reach the C# port (severity order)

1. C1 — invert the zoom-rescale direction in `TileToPoint` (divide when the *current*
   global zoom is higher than the requested zoom, multiply when lower).
2. C2 — do not persist the rescale: restore/scope origin+zoom locally; the native call
   leaves `CurrentViewer` untouched.
3. C3 — use the mirrored 32-bit mask law (`(1UL << (x & 31)) * 0x100000001`) instead of
   `1UL << (x & 63)`, or first prove x never exceeds 31 in rotated space; decide the
   negative-(sbyte)y row case.
4. C4/C5 — relabel the perm-table container and LUT page names (documentation only).
