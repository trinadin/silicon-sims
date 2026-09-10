# R144 work log — in-lot toolbar contents

## Path
1. Symbol sweep of cWin* classes -> full method map (people/arch/motive/cpanel/
   viewcontrol/campanel/sweeppeter/disposepopup + subpanels).
2. cWinCPanel::Init 0x270170 -> child inventory + per-child SetArea law; anchor
   hunt: TOC -0x5094 scan (2 hits) -> `__sinit_:WinCPanel_cpp` @0x270e10 ->
   anchor {220,50} + quads. Who creates cpanel: `cSimsApp::WindowSetup` @0x255bf0
   (bottom-150!); `RebuildControlPanel` @0x24de40 is bottom-100 but its only caller
   is a cheat callback (AppCheatCallback @0x24fb1c) — canon = WindowSetup.
3. cWinPeople::Init 0x28ee40 (2678 lines disasm): webcams, category tabs, 4 host
   windows, 8+8 motives, subpanels, texts, LivePopup. Static tables via CW trailer
   walk (`__sinit_:WinPeople_cpp` @0x292c70; trailer walker had to be written —
   name-length field is at trailer+0x10, name at +0x12).
4. Clock/money hunt: GetFunds/GetTime callers -> both inside
   cWinViewControl::UpdateViewFromCPState @0x2b3dc0 -> VC members 0x100/0x168/0x1a8;
   geometry in VC::Init 0x2b5f30 + TSPaint 0x2b58a0 (clock draw, centered text).
5. Mode patches: `li r3, 4803..4806/2035` scan -> VC::Init load block -> blit site
   in BlitPrivateBufferToParent 0x2b5460 with per-mode offsets.
6. UCP anchor writer 0x2b8230 re-traced; found r142's TOC->BSS slot mapping skew
   (verified against unpacked data words); all r142 VALUES re-confirmed, several
   BSS addresses corrected (kPause really 0x93B18 etc.). Money anchor 0x93B58
   (178,158) + clock rect 0x93B40 (95,118,151,125) newly decoded.
7. cWinMotive ctor/Init/TSPaint: 100x(H) gauge, font[8], fill 0..60px law,
   DeltaMeter arrows; flow layout from people-Init => 2x4 grid at (0,21)/(200,105).
8. cWinPeople::TSPaint: LiveGadget at (193,50); trend arrows Green/Red at
   (205,53)/(205,123) with 5px/unit reveal. NOTE: capstone stops at 0x28d6dc —
   wrote skip-invalid chunk disassembler (data-in-code or table); rest recovered.
9. cWinArch::Init: 12 tools (ids DATA 0x55290), flow (338,5) pitch 45; undo/redo/
   page buttons; roof panel (314,5,W,100); toothpicks 4912/4913.

## Dead ends
- Grepping people-Init for LoadBuffer clock/money ids: nothing — they're on the
  ViewControl. GetFunds(cSimulator) callers are all simulation-side; the UI reads
  CPState::GetFunds (single caller = VC::UpdateViewFromCPState).
- VitaBtn classes: only created in CAS/subpanels (0x2cf304, 0x40a038...), not on
  the toolbar; the "face" is the webcam custom draw.
- kGreenbars/kRedBars are NOT personality bars — they're the mood trend arrows.
- r142 anchors.py TOC mapping: off by slots (values were still right); re-derive
  from the writer instead.

## Tools written (r144/)
- scan_toc.py (TOC-load scanner), find_bl.py (direct-bl finder), find_li.py
  (`li rD, imm` finder), vt.py (vtable dumper; LAW: slot rawcode + 0x8E90 = file),
  trailer walker inline in shell, skip-invalid chunk disassembler (inline).

## Evidence files
toolbar-disasm-cpanel-{init,postinit,tspaint,setpanel,ctor,anchor-writer}.txt,
-rebuildcontrolpanel.txt, -windowsetup.txt, -dddview-create.txt, -simsapp-init.txt,
-people-{init,sinit,tspaint,tspaint-rest,runtime}.txt, -motive-{ctor,init,setval,
tspaint,tspaint-tail}.txt, -viewcontrol-{init,tspaint,update,blit}.txt,
-ucp-anchor-writer.txt, -arch-{init,update}.txt.
