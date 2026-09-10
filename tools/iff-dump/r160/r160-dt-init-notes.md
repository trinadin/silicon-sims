# R160 — cWinDowntown::Init scan notes (deferral evidence)

Purpose: decide whether the destination screens' own toolbars could be
ported this round. Conclusion: DEFERRED — the Downtown screen's chrome is
the community FILTER toolbar + the PAYPHONE, an expansion system (Hot
Date) outside the base-game-first scope.

- Symbol: `Init__12cWinDowntownFv` @0x3ff2f2 (start 0x3ff2f0), 3688 bytes
  (r141 symbol-index; disasm: r160-disasm-downtown-init.txt).
- Resource loads: 5320 kDowntownBackground (0x14c8), 5432 kDTDlgBkg2,
  logo language set {5427 SP, 5428 FR, 5429 GR, 5430 JP}.
- Two winW>800-only adornment buttons (@0x3ff954 cmpwi r0,0x320 gate):
  this+0x180 (image from this+0x120 buffer, SetImage(1,1), positioned at
  this+0x134/+0x138 = offX/offY-relative) and this+0x17c (image from
  this+0x11c). Mirrors the UL large-mask law (r143 §1).
- The 5-button FILTER toolbar loop @0x3ffd78-0x3ffee8 (`cmpwi r17,5`):
  buttons at this+0x164 stride 4, anchor pairs from a table at r30
  stride 8, per-button SetImage(art, i==2 ? 1 : 4, 1) — i==2 is the
  single-state button. Strings via LoadUIString set 0xa9 = 169.
- Tooltip/label law confirmed shared: 0x87f60(9) + 0x25f440(0xa9=169,
  blob, label, 0) @0x3ffd40-0x3ffd58 — same mechanism as VC::Init's
  (225,151) pair (see r160-round.md decode facts).
- Main-screen RETURN is not a toolbar button here — the payphone
  (kDTPhone 5426 dtphone.bmp 70x135) is the leave-town control; its
  placement/decode is future expansion-round work.
