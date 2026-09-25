0x28bf00: 93e1fffc  stw r31, -4(r1)                            |....|
0x28bf04: 7c0802a6  mfxer r0                                   ||...|
0x28bf08: 93c1fff8  stw r30, -8(r1)                            |....|
0x28bf0c: 3bc40000  addi r30, r4, 0                            |;...|
0x28bf10: 93a1fff4  stw r29, -12(r1)                           |....|
0x28bf14: 7c7d1b78  or r3, r29, r3                             ||}.x|
0x28bf18: 90010008  stw r0, 8(r1)                              |....|
0x28bf1c: 9421ffa0  stwu r1, -96(r1)                           |.!..|
0x28bf20: 80630244  lwz r3, 580(r3)                            |.c.D|
0x28bf24: 28030000  cmplwi r0, r3, 0                           |(...|
0x28bf28: 41820024  bne(2) 0x28bf4c                            |A..$|
0x28bf2c: 38000000  addi r0, r0, 0                             |8...|
0x28bf30: 38a10040  addi r5, r1, 64                            |8..@|
0x28bf34: 90010040  stw r0, 64(r1)                             |...@|
0x28bf38: 38800000  addi r4, r0, 0                             |8...|
0x28bf3c: 90010044  stw r0, 68(r1)                             |...D|
0x28bf40: 4827de11  bl 0x509d50                                |H'..|
0x28bf44: 38000000  addi r0, r0, 0                             |8...|
0x28bf48: 901d0244  stw r0, 580(r29)                           |...D|
0x28bf4c: 80628f48  lwz r3, -28856(r2)                         |.b.H|
0x28bf50: 80030000  lwz r0, 0(r3)                              |....|
0x28bf54: 7c1e0040  cmpl cr0, r30, r0                          ||..@|
0x28bf58: 40820010  beq(2) 0x28bf68                            |@...|
0x28bf5c: 38600000  addi r3, r0, 0                             |8`..|
0x28bf60: 481f0bb1  bl 0x47cb10                                |H...|
0x28bf64: 480000d8  b 0x28c03c                                 |H...|
0x28bf68: 281e0000  cmplwi r0, r30, 0                          |(...|
0x28bf6c: 418200d0  bne(2) 0x28c03c                            |A...|
0x28bf70: 7fc3f378  or r30, r3, r30                            |...x|
0x28bf74: 4bfb831d  bl 0x244290                                |K...|
0x28bf78: 3be30000  addi r31, r3, 0                            |;...|
0x28bf7c: 2c1fffff  cmpwi r0, r31, -1                          |,...|
0x28bf80: 418200bc  bne(2) 0x28c03c                            |A...|
0x28bf84: 4bfb46bd  bl 0x240640                                |K.F.|
0x28bf88: 5460063f  rlwinm r0, r3, 0, 24, 31                   |T`.?|
0x28bf8c: 41820058  bne(2) 0x28bfe4                            |A..X|
0x28bf90: 80828874  lwz r4, -30604(r2)                         |...t|
0x28bf94: 38600000  addi r3, r0, 0                             |8`..|
0x28bf98: a8be0638  lha r5, 1592(r30)                          |...8|
0x28bf9c: 80840000  lwz r4, 0(r4)                              |....|
0x28bfa0: 7ca00735  extsh. r5, r0, r0                          ||..5|
0x28bfa4: 80840000  lwz r4, 0(r4)                              |....|
0x28bfa8: 80c40014  lwz r6, 20(r4)                             |....|
0x28bfac: 40810024  beq(1) 0x28bfd0                            |@..$|
0x28bfb0: 8086001c  lwz r4, 28(r6)                             |....|
0x28bfb4: 38040001  addi r0, r4, 1                             |8...|
0x28bfb8: 7c050040  cmpl cr0, r5, r0                           ||..@|
0x28bfbc: 40800014  beq(0) 0x28bfd0                            |@...|
0x28bfc0: 3805ffff  addi r0, r5, -1                            |8...|
0x28bfc4: 80660020  lwz r3, 32(r6)                             |.f. |
0x28bfc8: 5400103a  rlwinm r0, r0 <<2                          |T..:|
0x28bfcc: 7c63002e  lwzx r3, r0, r3                            ||c..|
0x28bfd0: 28030000  cmplwi r0, r3, 0                           |(...|
0x28bfd4: 41820068  bne(2) 0x28c03c                            |A..h|
0x28bfd8: 38800001  addi r4, r0, 1                             |8...|
0x28bfdc: 4be38995  bl 0xc4970                                 |K...|
0x28bfe0: 4800005c  b 0x28c03c                                 |H..\|
0x28bfe4: 7fe3fb78  or r31, r3, r31                            |...x|
0x28bfe8: 4bfb7fa9  bl 0x243f90                                |K...|
0x28bfec: 2c030000  cmpwi r0, r3, 0                            |,...|
0x28bff0: 4180004c  bne(0) 0x28c03c                            |A..L|
0x28bff4: 801d00ec  lwz r0, 236(r29)                           |....|
0x28bff8: 7c030040  cmpl cr0, r3, r0                           ||..@|
0x28bffc: 40800040  beq(0) 0x28c03c                            |@..@|
0x28c000: 809d00f0  lwz r4, 240(r29)                           |....|
0x28c004: 5460103a  rlwinm r0, r3 <<2                          |T`.:|
0x28c008: 7c04002e  lwzx r0, r0, r4                            ||...|
0x28c00c: 28000000  cmplwi r0, r0, 0                           |(...|
0x28c010: 901d0244  stw r0, 580(r29)                           |...D|
0x28c014: 41820020  bne(2) 0x28c034                            |A.. |
0x28c018: 38000000  addi r0, r0, 0                             |8...|
0x28c01c: 38a10048  addi r5, r1, 72                            |8..H|
0x28c020: 90010048  stw r0, 72(r1)                             |...H|
0x28c024: 9001004c  stw r0, 76(r1)                             |...L|
0x28c028: 807d0244  lwz r3, 580(r29)                           |.}.D|
0x28c02c: 809d0240  lwz r4, 576(r29)                           |...@|
0x28c030: 4827dd21  bl 0x509d50                                |H'.!|
0x28c034: 7fc3f378  or r30, r3, r30                            |...x|
0x28c038: 481f0ad9  bl 0x47cb10                                |H...|
0x28c03c: 80010068  lwz r0, 104(r1)                            |...h|
0x28c040: 38210060  addi r1, r1, 96                            |8!.`|
0x28c044: 83e1fffc  lwz r31, -4(r1)                            |....|
0x28c048: 83c1fff8  lwz r30, -8(r1)                            |....|
0x28c04c: 7c0803a6  mtxer r0                                   ||...|
0x28c050: 83a1fff4  lwz r29, -12(r1)                           |....|
0x28c054: 4e800020  bclr                                       |N.. |
