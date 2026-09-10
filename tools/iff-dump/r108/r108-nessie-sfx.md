# R108 — nessie_sfx: the sound-table entry ENGINE-RECOVERED

## The chain (through the R104 unpacked sec1 image)

DoNessie state 0 (0x462264..0x46226c):
    lwz r3, -18180(r2)     ; TOC entry (r2 = sec1 + 0x8000 -> sec1+0x38fc)
    addi r3, r3, 3         ; skip 3 bytes
    bl <PlaySound__FPCc>   ; standard-encoding target 0x30a290 (mflr prologue),
                           ; dispatcher 0x3093e0 — takes the sound NAME cstring

The UNPACKED sec1 image (r104/sec1-unpacked.bin — the true stored pointers;
see the note below on the relocated image) holds:
    TOC[0x38fc] = 0x6c6e0
    sec1+0x6c6e0 = "%d\0nessie_sfx\0UI_Nhood_click\0Lot Position..."

so the +3 skips the 3-byte "%d\0" prefix and the engine calls
PlaySound("nessie_sfx") — the R93 by-name guess is ENGINE-EXACT.

## Two decode-rule notes for future rounds

1. LONG bl targets: the session's "signed-low-16 byte-delta" quirk rule
   (validated on short loop back-edges) mis-hits long calls — 0x46226c's bl
   decoded via the quirk to string data at 0x45a290, but the STANDARD PPC
   encoding (LI<<2, 24-bit signed) lands on the real mflr prologue at
   0x30a290. Function maps built from symbol strings/anchors are unaffected
   (they never used bl arithmetic); bl-derived targets should be re-checked
   with the standard encoding when they land on non-code.
2. The R105 relocation simulator's TOC-entry values drift (running C/D
   counters mis-track somewhere); the UNPACKED image's stored pointers are
   the reliable source for static data chasing (this round proved it:
   unpacked=0x6c6e0 -> the strings; relocated=0x2a88 -> a pointer table).

## Port + pin

UINeighborhoodNessieLayer: EngineSoundEvent = "nessie_sfx" (const with the
provenance above) played at state 0; the uinbhd gate pins the constant.
r108p1: 59/59 FIRST-TRY (uinbhd nessie=True), clean exit.
