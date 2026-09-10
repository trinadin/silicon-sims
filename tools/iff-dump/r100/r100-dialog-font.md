# R100 — the fifth live .ffn table: the dialog font (cheat responses)

`Fonts\variablesans_09.ffn` (17,232 bytes, R87 canon) renders the CHEAT-BAR
RESPONSE surface: when the engine cheat refuses a second nessie, the panel
now shows the ENGINE'S OWN STRING — "Sorry only one Nessie at a time."
(recovered from the binary in R93, CheatCallback 0x4652f0) — as a
UIOriginalText at (12, 34), auto-clearing after 4s (ShowCheatMessage).

Table facts: 224 glyphs, atlas 256x115, 4-bit grayscale; pin records
'S' (8,10,62,40,t2=1), '!' (2,10,104,40); Measure("Sorry only one Nessie at
a time.") = 201 under the shipped renderer convention (the raw table sum
with t0-space is 195; OriginalGlyphFont.Advance uses a 5px space — the
6-space string adds 6px. Honest FAIL r100p1 pinned the raw 195 first and
the gate caught the discrepancy).

Gate: uiglyph parses ALL FIVE live tables canon-verified-on-first-read with
parse pins + headless renders ("The Sims"/_14, "Lot 20"/_10, "§20,500"/_12_bs,
"12:05 AM"/_11, and the ENGINE STRING via _09, ink>200). r100p2: 59/59,
uiglyph render=True fontsParsed=8 textsMounted=5 moneyUpdates=1
(cheatMessages=0 headless — the surface fires only on a refused cheat),
clean exit.
