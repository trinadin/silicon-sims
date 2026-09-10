# R110 — the sway amplitude: the multiply structure decoded, constant open

## What the standard-encoding re-decode established (r108 correction applied)

- The MathLib calls at 0x57c3e0/0x57c418 resolve (standard 24-bit LI<<2) to
  0x5a1bc0/0x5a1ba8 — a pair of TOC-import thunks (imported MathLib
  functions, the sin/cos pair).
- The three mystery float ops (0xFC230072 / 0xFC000072 / 0xFC220072) decode
  with the CORRECT A-form field split (5-bit XO, frC = bits 21-25) as **FMUL
  with frC = f1** — the session's earlier "XO57" label was a 6-bit XO
  extraction that swallowed frC's LSB. The chain:

      f1 = double(seed % 1440)            ; int->double via the 0x4330 magic
      f1 = 0.785 * f1                     ; FMUL f3*f1   (f3 = [r31+0])
      f31 = f1 / 180.0                    ; fdiv        -> wind*PI/720
      f1 = sin-or-cos(f31)                ; MathLib #1
      x  = double(<int r3-lineage>)       ; second int->double
      f0 = x * f1                         ; FMUL f0*f1  <-- THE SWAY MULTIPLY
      sway = (int)f0                      ; fctiwz @0x57c40c; lwz r30 @0x57c414
      ... second MathLib call ...
      gate = (int)(y * f1)                ; FMUL @0x57c448 -> wind-walk compare

## The amplitude conclusion

**sway = (int)(x × trig(wind×π/720))** — the engine multiplies the trig
result by a runtime int (r3-lineage), NOT by a dedicated float constant.
The recovered float block (file 0x5a5348) is now FULLY accounted except one
value: 0.785 (angle scale), 180.0 (angle divisor), 0.0 (the fcmpu baseline),
1.5/3.0/4.5/5.0/6.0 (the wind gates), the 0x4330 int->double magic — and
**64.0 ([r31+0x48]), the only unaccounted constant**, with no lfs of it in
this window (it belongs to another routine).

The r3 lineage at the second conversion (x) is not resolvable without an
emulating decompiler — x is the seed-derived int reloaded after the call.
The port's amplitude-8 model therefore REMAINS DISCLOSED, now with the
multiply structure engine-decoded and the 64.0 flagged as the corpus's
single unaccounted float.

## Decode-rule additions (for the residual doc)

- A-form float XO is 5 bits (26-30); a 10-bit extraction silently eats
  frC's LSB and mislabels fmul as "XO57". ppc_decode.py's float support
  should be fixed before any further float work (noted, not patched —
  the session's float windows are complete).
