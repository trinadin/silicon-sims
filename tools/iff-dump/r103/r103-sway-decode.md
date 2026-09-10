# R103 — the balloon sway: instruction-level decode + the PEF unpacker status

## The float path (AnimateBalloon 0x57c38c..0x57c458, file offsets)

Decoded with float-op awareness (r103-sway-float-decode.txt):

    lfs f2, 64(r31)   ; the int->double magic double (0x43300000_80000000-
    ...               ; family — the xoris 0x8000 / addis 0x4330 idiom stores
                      ; the int, lfd loads the biased double, fsub unbiases)
    lfs f3, 0(r31)    ; ANGLE-SCALE constant (slot 0 of the TOC float block)
    lfs f0, 8(r31)    ; constant (slot 8)
    f1 = double(seed % 1440) - magic
    f1 = f1 * f3                       ; angle = wind * scale
    fdiv f31, f1, f0                   ; (or divide by slot-8 constant)
    bl 0x581bc0                        ; MathLib #1 (sin)
    ... second int->double conversion ...
    f0 = dbl - magic ; XO57 f0 (multiply family)
    fctiwz ; stfd ; lwz r30, 84(r1)    ; r30 = (int) RESULT — sway component 1
    bl 0x581ba8                        ; MathLib #2 (cos)
    ... third conversion with lfs f0, 16(r31) (slot 16) ...
    fcmpu (XO0) + XO57 multiply
    fctiwz ; lwz r0, 100(r1)           ; r0 = (int) RESULT — sway component 2
    branch-on-r0                       ; the R92 "float threshold gates"

Findings that CHANGE the R92 model's understanding:
- The sway is TWO MathLib calls (sin + cos components), not one trig call.
- The wind-walk "float band gates" are comparisons on these computed values.
- The constants live at TOC float-block slots [r31+0] (angle scale),
  [r31+8], [r31+16]; [r31+64] is the int->double conversion magic (NOT an
  amplitude).
- The AMPLITUDE/scale constants are in the PACKED data section — still
  unresolved; the port's 8*sin model stays DISCLOSED.

## The PEF sec1 unpacker (pef_unpack.py, committed)

Spec-correct for ops 0-4 (Apple RTArch-94: 3-bit op + 5-bit count, base-128
varint extension). Decodes 0x7f9d3 of 0x989a4 output bytes (81%) before a
stream desync at packed offset 0x28d9 — the interleave (op 3/4) argument
semantics in this file differ from all six argument-order permutations
tested (r103 trace), so one op family still consumes incorrectly. The tool
+ the desync analysis are the deliverable; completing it needs the exact
interleave encoding (candidate: the real PEFBinaryFormat.h enum, which
mirrors have been elusive).
