# R98 — the third live .ffn table: the bold money font

`Fonts\variablesans_12_bs.ffn` (28,624 bytes, R87 canon) now renders the
LOT-SCREEN MONEY READOUTS (both toolbar paths):

- UIMoneyPanel (the touch/panel path): the modern UILabel is hidden and a
  UIOriginalText twin draws "§<money>" centered in the 128px panel.
- UIDesktopUCP (the Desktop control-panel path): same twin beside
  MoneyLabel (7,241 138x30 area, centered), with the cached container
  invalidated on every money update so the text re-renders.

Table facts (same FNTF layout; see ../r96/r96-format-notes.md):
- 224 glyphs, atlas 256x204, 4-bit grayscale (pool 26,116 = 26,112 + 4 pad)
- pin records: '§' U+00A7 (w=11 h=15 u=50 v=83 t=(0,0,0)) — the actual
  currency glyph the code renders; '$' (12,14,180,36)
- Measure("§20,500") = 11+10+11+6+11+11+11 = 71 (gate-pinned; advance =
  w+t0 like _14)

Gate: uiglyph parses ALL THREE live tables canon-verified-on-first-read and
renders "The Sims" (_14), "Lot 20" (_10) and "§20,500" (_12_bs) headlessly
with ink + antialias assertions. r98p1: 59/59, uiglyph render=True
fontsParsed=5 textsMounted=2 moneyUpdates=1 (the money readout drew live
during boot), clean exit.
