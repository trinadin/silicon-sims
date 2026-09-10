# R104 — the PEF unpacker COMPLETE + the sway float block recovered

## The unpacker (pef_unpack semantics, now exact)

Ghidra's SectionHeader.getUnpackedData (sparse-cloned, read verbatim) gave
the two corrections R103 needed:
1. op2/3/4 take their BLOCK/COMMON SIZE from the 5-bit count field (NOT a
   varint); only customSize/repeatCount are varints.
2. The section-header field order is nameOffset, defaultAddress,
   totalLength, unpackedLength, containerLength, containerOffset — sec1's
   UNPACKED size is 0x7bf80 (0x989a4 is totalLength = unpacked + zero-fill).

Result: sec1 unpacks to EXACTLY 0x7bf80 consuming the ENTIRE packed stream
(0x6d834 bytes) — 100%, first try after the fix. r104/sec1-unpacked.bin.

## The sway float block (recovered through the unpacked TOC)

The TOC lives at the start of sec1; r2 = TOC base + 0x8000 (CFM). The
AnimateBalloon load `lwz r31, -17052(r2)` reads the TOC entry at
sec1+0x3d64, whose stored (section-relative) value is 0x59c4b8 — an offset
into the CODE section (the read-only literal pool). Absolute file:
0x8e90 + 0x59c4b8 = 0x5a5348. The block:

    +0x00 0.785                  (= pi/4 — the ANGLE SCALE)
    +0x08 180.0                  (the DEGREES DIVISOR)
    +0x10 0.0
    +0x18 1.5  +0x20 3.0  +0x28 4.5  +0x30 5.0  +0x38 6.0   (WIND GATES)
    +0x40 4503601774854144.0     (the int->double 0x4330/0x8000 magic)
    +0x48 64.0

Instruction chain (float-decoded, r104 sway trace in r103-sway-float-decode
plus this round's precise pass): angle = double(seed%1440); fmul by 0.785;
fdiv by 180.0 -> wind * pi/720 EXACTLY (0.785/180 == pi/720); MathLib
sin/cos at 0x581bc0/0x581ba8; the wind-walk gates compare computed floats
against 1.5/3.0/4.5/5.0/6.0.

## Ported

UINeighborhoodBalloonLayer: Sway() now uses EnginePiOver4 (0.785f) /
EngineDegrees (180.0f) — engine-literal constants replacing the equivalent
formula — and EngineWindGates {1.5, 3.0, 4.5, 5.0, 6.0} declared with
provenance. The AMPLITUDE multiply past the sin call is still not fully
resolved (path B squares its input — likely part of the gate math, not the
draw), so amplitude 8 remains a DISCLOSED model. Gate: uinbhd pins the
angle constants and all five gates.

## Cloud tables

The lwz table bases (282/342/462) are loader-relocated pointers; the
obvious shape scans (stride 32/48 ladders in [2..900]) found no clean match
in sec0 or the unpacked sec1 — resolving them needs the loader-section
RELOCATION stream (sec2), not just the unpack. Documented as the remaining
trace; the R92 142/262 model stays disclosed.
