#!/usr/bin/env python3
"""R102: layout-montage verification artifact — the ORIGINAL neighborhood-screen
composition (from the R90-R101 engine decodes, every value gate-pinned) laid
out against Simitone's live panel, as a text table. Regenerate:
  python3 make_r102_montage.py > r102/r102-layout-montage.txt
"""

# element: (screen, layer kind, name, original position/source, port position/source)
ROWS = [
    # ---- TS1.0 (cWinNeighborhoodVC) ----
    ("TS1.0", "backdrop", "NScreen.BMP", "(0,0) full-screen (VC ctor zeroes scroll)", "(0,0) panel Graphic"),
    ("TS1.0", "water", "DiffN1-N2/3/4_8 (3 frames)", "self-drawing SpriteSlots (R95: only Shutdown touches +408..416)", "(0,0) family, frame-cycled [disclosed]"),
    # ---- Downtown (cWinDowntown) ----
    ("Downtown", "backdrop", "DScreen.bmp", "(0,0) full-screen", "(0,0) panel Graphic"),
    ("Downtown", "water", "dscreen00-05 (6)", "ENGINE (0,0): ctor li-0 this+308/+312, TSPaint 0x3fedf4/0x3fede8", "(0,0) family [R95 engine-pin]"),
    # ---- Vacation (cWinVacation) ----
    ("Vacation", "backdrop", "visland.bmp", "(0,0) full-screen", "(0,0) panel Graphic"),
    ("Vacation", "water", "visland_waves001-005 (5)", "ENGINE (0,0): ctor li-0 this+316/+320, TSPaint 0x4416d4/0x4416b4, frame=this+464 mod 5", "(0,0) family [R95 engine-pin]"),
    ("Vacation", "decor", "port + trees", "static overlays (R89 art-informed)", "families at (0,0) default"),
    # ---- Old Town / UL (cWinNeighborhoodUL) ----
    ("OldTown", "backdrop", "NScreen_unleashed.bmp", "(0,0) full-screen, scale 2", "(0,0) panel Graphic, Scale=2"),
    ("OldTown", "water", "NScreen_unleashed_waves001-006 (6)", "self-drawing SpriteSlots (R95: vtable draw, no position args)", "(0,0) family [disclosed]"),
    ("OldTown", "nessie", "kNess1-3", "ENGINE cheat spawn (118,519); swims (-2,+1)/tick to x==60 (R93 DoNessie 0x462090)", "UINeighborhoodNessieLayer: same machine, cheat-triggered [R93 engine-pin]"),
    # ---- Studiotown (cWinStudiotown) ----
    ("StudioTown", "backdrop", "DScreen.bmp + top layer", "(0,0) full-screen", "(0,0) panel Graphic + top-layer family"),
    ("StudioTown", "cars", "kStudiotownCar0-19 (20 static variants)", "ENGINE six lanes (R91 InitCars 0x475dd0): y 420/440/475 dirs -1, x 468-806; y 538/574/610 dirs +1, x 606-816; 54 cars, bitmap=Random()%18 once, rare-event movement", "UINeighborhoodCarLaneLayer: same table [R91 engine-pin]"),
    # ---- Magicland (cWinMagicland/cMagiclandSprites) ----
    ("Magicland", "backdrop", "DScreen.bmp", "(0,0) full-screen", "(0,0) panel Graphic"),
    ("Magicland", "water", "DScreen_waves1-6 (6)", "ENGINE (0,62): DoWater 0x57c6dc, frame+1 mod 6 every 5th tick", "(0,62) family [R90 engine-pin]"),
    ("Magicland", "clouds", "mistA/B/C as 21 clouds", "ENGINE InitClouds 0x57c890: y=60+16j (rec 0-7), 60+16k (10-15), 60+10k (17-20); x=142/262+spread (literal); drift 2-6; AnimateClouds wrap/-255/%3 [loop-2/3 tables behind PEF relocation]", "UINeighborhoodCloudLayer: same ladders/columns, table values modeled 142/262 [R92, disclosed]"),
    ("Magicland", "balloon", "balloon1-16 as wind states", "ENGINE (300+sway, tick): AnimateBalloon 0x57c330, wind walk %35 caps 6/10/14/16 clamp 15, sway=int(trig(seed%1440)) [MathLib amplitude unresolved]", "UINeighborhoodBalloonLayer: same walk, sway modeled 8*sin [R92, disclosed]"),
]

WIDTHS = (10, 9, 26, 78, 58)

def main():
    hdr = ("SCREEN", "LAYER", "ELEMENT", "ORIGINAL (engine decode)", "SIMITONE PORT")
    print("LAYOUT MONTAGE — original vs live panel, every row gate-pinned unless marked [disclosed]/[modeled]")
    print("=" * 200)
    print("".join(h.ljust(w) for h, w in zip(hdr, WIDTHS)))
    print("-" * 200)
    delta_rows = 0
    for row in ROWS:
        print("".join(str(c).ljust(w) for c, w in zip(row, WIDTHS)))
        if "[disclosed]" in row[4] or "[modeled]" in row[4]:
            delta_rows += 1
    print("-" * 200)
    print(f"{len(ROWS)} elements; {delta_rows} carry disclosed/model caveats "
          f"(PEF-relocated cloud tables, SpriteSlot-internal positions, balloon sway amplitude, VC deltas); "
          f"the rest are ENGINE-PINNED in the uinbhd/uiglyph gates.")

if __name__ == "__main__":
    main()
