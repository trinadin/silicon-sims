# r257-subpie — regenerable disassembly captures for UI-23

Law doc: coordination/evidence/UI-23/r257-people-subpie-law.md.

Regenerate any file with:
  python3 tools/iff-dump/ppc_decode.py "game-data/The Sims/The Sims Complete" 0xSTART 0xEND

Files:
- GetPieBackBuffer_GetPeopleSubPie_GetPeoplePie.asm  0x28b310-0x28b402 (accessors)
- Init_pie_blocks.asm        0x28eeb0-0x28f244 (twin ring construction)
- DoMenu_head.asm            0x184058-0x1844a0 (cache/show/active-ring)
- CancelPieMenu.asm          0x2125e0-0x212640 (both-ring dismissal)
- DeletingObject_pie_part.asm 0x183810-0x183930 (dismiss + reset)
- TSOnCommand_pie_keys.asm   0x28cd8c-0x28d030 (sender identity + key nudges)
- MenuItemSelected.asm       0x183eb0-0x184045 (click -> action)
- TrackPerson.asm            0x28bf00-0x28c058 (tracking marker)
