# R106 — the sixth live .ffn table: the title font (option captions)

`Fonts\variablesans_08.ffn` (16,208 bytes, R87 canon) renders the OPTIONS
PANEL caption surface: the free-will option title in UIMainPanel now shows
"Free Will" as a UIOriginalText twin (mounted lazily on first updateLabel,
above the modern wrapped caption at (labelX-77+18, 92)).

Note: this round first attempted the UIDialog Caption twin in FreeSO's
FSO.UI project — reverted: FSO.UI cannot reference Simitone.Client
(CS0246), so the conversion targets Simitone-side surfaces only.

Table facts: 224 glyphs, atlas 256x107, 4-bit grayscale; pin records
'T' (9,9,140,17,t2=1) and 'N' (9,9,104,17); Measure("The Sims") = 54 under
the shipped 5px-space convention (raw t0-space sum 53 — the same one-space
discrepancy class as R100, honest FAIL r106p1 before the pin fix).

Gate: uiglyph parses ALL SIX live tables canon-verified-on-first-read with
parse pins + headless renders ("The Sims"/_14, "Lot 20"/_10, "§20,500"/_12_bs,
"12:05 AM"/_11, the engine string/_09, "The Sims"/_08 ink>100).
r106p2: 59/59, uiglyph render=True fontsParsed=9 textsMounted=5
(dialogTitles=0 headless — the options panel isn't opened during boot;
the title mounts on first updateLabel), clean exit.
