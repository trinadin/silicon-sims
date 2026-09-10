# R99 — the fourth live .ffn table: the small toolbar-readout font

`Fonts\variablesans_11.ffn` (21,968 bytes, R87 canon) renders the remaining
lot-toolbar readouts, ALL FIVE surfaces converted:

Desktop path (UIDesktopUCP — twins created AFTER the modern labels exist;
the first attempt created them before and NRE'd the whole panel, killing
the uitoolbar/uidump/uichrome tree gates — honest FAIL r99p1):
- TimeOriginal (157,221 114x16 centered) — mirrors TimeLabel each clock change
- FriendsOriginal (176,184) — the friends count
- FloorOriginal (22,184 24x15 centered) — mirrors FloorLabel each floor change
- modern labels + their shadows hidden; cached container invalidated per update

Touch path:
- UIMainPanel FloorOriginal (80,64 51x18 centered) — mirrors FloorLabel
- UIClockPanel TimeOriginal (148,6 133x38 centered, cached container
  invalidated per clock change)

Table facts: 224 glyphs, atlas 256x152, 4-bit grayscale; pin records
'1' (6,12,50,33,t2=1) and ':' (3,8,90,9,t2=4 — the colon hangs low);
Measure("12:05 AM") = 7+9+4+10+9+5+11+14 = 69.

Gate: uiglyph parses ALL FOUR live tables canon-verified-on-first-read with
parse pins + headless renders ("The Sims"/_14, "Lot 20"/_10, "§20,500"/_12_bs,
"12:05 AM"/_11). r99p2: 59/59, uiglyph render=True fontsParsed=7
textsMounted=5 moneyUpdates=1 (toolbarReadouts=0 at boot — the counter ticks
on clock/floor CHANGES; the twins draw their initial text from creation),
clean exit.
