0x28cd8c: 801f00d4  lwz r0, 212(r31)                           |....|
0x28cd90: 3afe0000  addi r23, r30, 0                           |:...|
0x28cd94: 7c170040  cmpl cr0, r23, r0                          ||..@|
0x28cd98: 41820010  bne(2) 0x28cda8                            |A...|
0x28cd9c: 801f00d8  lwz r0, 216(r31)                           |....|
0x28cda0: 7c170040  cmpl cr0, r23, r0                          ||..@|
0x28cda4: 40820468  beq(2) 0x28d20c                            |@..h|
0x28cda8: 807a0000  lwz r3, 0(r26)                             |.z..|
0x28cdac: 38800006  addi r4, r0, 6                             |8...|
0x28cdb0: 80630018  lwz r3, 24(r3)                             |.c..|
0x28cdb4: 4bee587d  bl 0x172630                                |K.X}|
0x28cdb8: 80a29640  lwz r5, -27072(r2)                         |...@|
0x28cdbc: 38800028  addi r4, r0, 40                            |8..(|
0x28cdc0: 80c29340  lwz r6, -27840(r2)                         |...@|
0x28cdc4: 38e00000  addi r7, r0, 0                             |8...|
0x28cdc8: 48304469  bl 0x591230                                |H0Di|
0x28cdcc: 7ee4bb78  or r23, r4, r23                            |~..x|
0x28cdd0: 4bef70e1  bl 0x183eb0                                |K.p.|
0x28cdd4: 48000438  b 0x28d20c                                 |H..8|
0x28cdd8: 807f00d8  lwz r3, 216(r31)                           |....|
0x28cddc: 81830000  lwz r12, 0(r3)                             |....|
0x28cde0: 818c01fc  lwz r12, 508(r12)                          |....|
0x28cde4: 48315bfd  bl 0x5a29e0                                |H1[.|
0x28cde8: 80410014  lwz r2, 20(r1)                             |.A..|
0x28cdec: 5460063f  rlwinm r0, r3, 0, 24, 31                   |T`.?|
0x28cdf0: 41820020  bne(2) 0x28ce10                            |A.. |
0x28cdf4: 807f00d8  lwz r3, 216(r31)                           |....|
0x28cdf8: 3880ffff  addi r4, r0, -1                            |8...|
0x28cdfc: 81830000  lwz r12, 0(r3)                             |....|
0x28ce00: 818c0274  lwz r12, 628(r12)                          |...t|
0x28ce04: 48315bdd  bl 0x5a29e0                                |H1[.|
0x28ce08: 80410014  lwz r2, 20(r1)                             |.A..|
0x28ce0c: 48000400  b 0x28d20c                                 |H...|
0x28ce10: 807f00d4  lwz r3, 212(r31)                           |....|
0x28ce14: 81830000  lwz r12, 0(r3)                             |....|
0x28ce18: 818c01fc  lwz r12, 508(r12)                          |....|
0x28ce1c: 48315bc5  bl 0x5a29e0                                |H1[.|
0x28ce20: 80410014  lwz r2, 20(r1)                             |.A..|
0x28ce24: 5460063f  rlwinm r0, r3, 0, 24, 31                   |T`.?|
0x28ce28: 418203e4  bne(2) 0x28d20c                            |A...|
0x28ce2c: 807f00d4  lwz r3, 212(r31)                           |....|
0x28ce30: 3880ffff  addi r4, r0, -1                            |8...|
0x28ce34: 81830000  lwz r12, 0(r3)                             |....|
0x28ce38: 818c0274  lwz r12, 628(r12)                          |...t|
0x28ce3c: 48315ba5  bl 0x5a29e0                                |H1[.|
0x28ce40: 80410014  lwz r2, 20(r1)                             |.A..|
0x28ce44: 480003c8  b 0x28d20c                                 |H...|
0x28ce48: 2c1e0020  cmpwi r0, r30, 32                          |,.. |
0x28ce4c: 41820038  bne(2) 0x28ce84                            |A..8|
0x28ce50: 4080001c  beq(0) 0x28ce6c                            |@...|
0x28ce54: 2c1e001b  cmpwi r0, r30, 27                          |,...|
0x28ce58: 4182011c  bne(2) 0x28cf74                            |A...|
0x28ce5c: 408003b0  beq(0) 0x28d20c                            |@...|
0x28ce60: 2c1e0009  cmpwi r0, r30, 9                           |,...|
0x28ce64: 418200a0  bne(2) 0x28cf04                            |A...|
0x28ce68: 480003a4  b 0x28d20c                                 |H...|
0x28ce6c: 2c1e007a  cmpwi r0, r30, 122                         |,..z|
0x28ce70: 41820174  bne(2) 0x28cfe4                            |A..t|
0x28ce74: 40800398  beq(0) 0x28d20c                            |@...|
0x28ce78: 2c1e005a  cmpwi r0, r30, 90                          |,..Z|
0x28ce7c: 41820168  bne(2) 0x28cfe4                            |A..h|
0x28ce80: 4800038c  b 0x28d20c                                 |H...|
0x28ce84: 807a0000  lwz r3, 0(r26)                             |.z..|
0x28ce88: 80630000  lwz r3, 0(r3)                              |.c..|
0x28ce8c: 80630014  lwz r3, 20(r3)                             |.c..|
0x28ce90: 4be55251  bl 0xe20e0                                 |K.RQ|
0x28ce94: 807f00d8  lwz r3, 216(r31)                           |....|
0x28ce98: 81830000  lwz r12, 0(r3)                             |....|
0x28ce9c: 818c01fc  lwz r12, 508(r12)                          |....|
0x28cea0: 48315b41  bl 0x5a29e0                                |H1[A|
0x28cea4: 80410014  lwz r2, 20(r1)                             |.A..|
0x28cea8: 5460063f  rlwinm r0, r3, 0, 24, 31                   |T`.?|
0x28ceac: 41820020  bne(2) 0x28cecc                            |A.. |
0x28ceb0: 807f00d8  lwz r3, 216(r31)                           |....|
0x28ceb4: 3880fffe  addi r4, r0, -2                            |8...|
0x28ceb8: 81830000  lwz r12, 0(r3)                             |....|
0x28cebc: 818c0274  lwz r12, 628(r12)                          |...t|
0x28cec0: 48315b21  bl 0x5a29e0                                |H1[!|
0x28cec4: 80410014  lwz r2, 20(r1)                             |.A..|
0x28cec8: 48000344  b 0x28d20c                                 |H..D|
0x28cecc: 807f00d4  lwz r3, 212(r31)                           |....|
0x28ced0: 81830000  lwz r12, 0(r3)                             |....|
0x28ced4: 818c01fc  lwz r12, 508(r12)                          |....|
0x28ced8: 48315b09  bl 0x5a29e0                                |H1[.|
0x28cedc: 80410014  lwz r2, 20(r1)                             |.A..|
0x28cee0: 5460063f  rlwinm r0, r3, 0, 24, 31                   |T`.?|
0x28cee4: 41820328  bne(2) 0x28d20c                            |A..(|
0x28cee8: 807f00d4  lwz r3, 212(r31)                           |....|
0x28ceec: 3880fffe  addi r4, r0, -2                            |8...|
0x28cef0: 81830000  lwz r12, 0(r3)                             |....|
0x28cef4: 818c0274  lwz r12, 628(r12)                          |...t|
0x28cef8: 48315ae9  bl 0x5a29e0                                |H1Z.|
0x28cefc: 80410014  lwz r2, 20(r1)                             |.A..|
0x28cf00: 4800030c  b 0x28d20c                                 |H...|
0x28cf04: 807f00d8  lwz r3, 216(r31)                           |....|
0x28cf08: 81830000  lwz r12, 0(r3)                             |....|
0x28cf0c: 818c01fc  lwz r12, 508(r12)                          |....|
0x28cf10: 48315ad1  bl 0x5a29e0                                |H1Z.|
0x28cf14: 80410014  lwz r2, 20(r1)                             |.A..|
0x28cf18: 5460063f  rlwinm r0, r3, 0, 24, 31                   |T`.?|
0x28cf1c: 41820020  bne(2) 0x28cf3c                            |A.. |
0x28cf20: 807f00d8  lwz r3, 216(r31)                           |....|
0x28cf24: 3880fffd  addi r4, r0, -3                            |8...|
0x28cf28: 81830000  lwz r12, 0(r3)                             |....|
0x28cf2c: 818c0274  lwz r12, 628(r12)                          |...t|
0x28cf30: 48315ab1  bl 0x5a29e0                                |H1Z.|
0x28cf34: 80410014  lwz r2, 20(r1)                             |.A..|
0x28cf38: 480002d4  b 0x28d20c                                 |H...|
0x28cf3c: 807f00d4  lwz r3, 212(r31)                           |....|
0x28cf40: 81830000  lwz r12, 0(r3)                             |....|
0x28cf44: 818c01fc  lwz r12, 508(r12)                          |....|
0x28cf48: 48315a99  bl 0x5a29e0                                |H1Z.|
0x28cf4c: 80410014  lwz r2, 20(r1)                             |.A..|
0x28cf50: 5460063f  rlwinm r0, r3, 0, 24, 31                   |T`.?|
0x28cf54: 418202b8  bne(2) 0x28d20c                            |A...|
0x28cf58: 807f00d4  lwz r3, 212(r31)                           |....|
0x28cf5c: 3880fffd  addi r4, r0, -3                            |8...|
0x28cf60: 81830000  lwz r12, 0(r3)                             |....|
0x28cf64: 818c0274  lwz r12, 628(r12)                          |...t|
0x28cf68: 48315a79  bl 0x5a29e0                                |H1Zy|
0x28cf6c: 80410014  lwz r2, 20(r1)                             |.A..|
0x28cf70: 4800029c  b 0x28d20c                                 |H...|
0x28cf74: 807f00d8  lwz r3, 216(r31)                           |....|
0x28cf78: 81830000  lwz r12, 0(r3)                             |....|
0x28cf7c: 818c01fc  lwz r12, 508(r12)                          |....|
0x28cf80: 48315a61  bl 0x5a29e0                                |H1Za|
0x28cf84: 80410014  lwz r2, 20(r1)                             |.A..|
0x28cf88: 5460063f  rlwinm r0, r3, 0, 24, 31                   |T`.?|
0x28cf8c: 41820020  bne(2) 0x28cfac                            |A.. |
0x28cf90: 807f00d8  lwz r3, 216(r31)                           |....|
0x28cf94: 3880ffff  addi r4, r0, -1                            |8...|
0x28cf98: 81830000  lwz r12, 0(r3)                             |....|
0x28cf9c: 818c0274  lwz r12, 628(r12)                          |...t|
0x28cfa0: 48315a41  bl 0x5a29e0                                |H1ZA|
0x28cfa4: 80410014  lwz r2, 20(r1)                             |.A..|
0x28cfa8: 48000264  b 0x28d20c                                 |H..d|
0x28cfac: 807f00d4  lwz r3, 212(r31)                           |....|
0x28cfb0: 81830000  lwz r12, 0(r3)                             |....|
0x28cfb4: 818c01fc  lwz r12, 508(r12)                          |....|
0x28cfb8: 48315a29  bl 0x5a29e0                                |H1Z)|
0x28cfbc: 80410014  lwz r2, 20(r1)                             |.A..|
0x28cfc0: 5460063f  rlwinm r0, r3, 0, 24, 31                   |T`.?|
0x28cfc4: 41820248  bne(2) 0x28d20c                            |A..H|
0x28cfc8: 807f00d4  lwz r3, 212(r31)                           |....|
0x28cfcc: 3880ffff  addi r4, r0, -1                            |8...|
0x28cfd0: 81830000  lwz r12, 0(r3)                             |....|
0x28cfd4: 818c0274  lwz r12, 628(r12)                          |...t|
0x28cfd8: 48315a09  bl 0x5a29e0                                |H1Z.|
0x28cfdc: 80410014  lwz r2, 20(r1)                             |.A..|
0x28cfe0: 4800022c  b 0x28d20c                                 |H..,|
0x28cfe4: 386000d8  addi r3, r0, 216                           |8`..|
0x28cfe8: 38800000  addi r4, r0, 0                             |8...|
0x28cfec: 4bdd3cb5  bl 0x60ca0                                 |K.<.|
0x28cff0: 4800021c  b 0x28d20c                                 |H...|
0x28cff4: 801f00d4  lwz r0, 212(r31)                           |....|
0x28cff8: 3afe0000  addi r23, r30, 0                           |:...|
0x28cffc: 7c170040  cmpl cr0, r23, r0                          ||..@|
0x28d000: 41820010  bne(2) 0x28d010                            |A...|
0x28d004: 801f00d8  lwz r0, 216(r31)                           |....|
0x28d008: 7c170040  cmpl cr0, r23, r0                          ||..@|
0x28d00c: 40820200  beq(2) 0x28d20c                            |@...|
0x28d010: 807a0000  lwz r3, 0(r26)                             |.z..|
0x28d014: 38800006  addi r4, r0, 6                             |8...|
0x28d018: 80630018  lwz r3, 24(r3)                             |.c..|
0x28d01c: 4bee5615  bl 0x172630                                |K.V.|
0x28d020: 7ee4bb78  or r23, r4, r23                            |~..x|
0x28d024: 4bef69cd  bl 0x1839f0                                |K.i.|
0x28d028: 480001e4  b 0x28d20c                                 |H...|
0x28d02c: 3ae00000  addi r23, r0, 0                            |:...|
