# R119 — the engine-wide default text renderer becomes the ORIGINAL .ffn glyph family (`uivfont`)

## The issue this round closes

"You can't read the text" (R118 screenshot) had TWO causes. R118 removed the
Simitone loader chrome; the surviving cause was the renderer itself: every
UILabel/UIButton/UIDialog/UIListBox/tooltip/text-edit drew through
`GameFacade.VectorFont = MSDFFont(Content/Fonts/mobile)`, whose `TextStyle.Size`
formula (`Scale = px/11.5`) blew the bitmap atlas up ~1.7-3.2x at UI sizes,
rendering as gray/white blocks on this GL path. Rounds 96-115 patched AROUND
this per-panel (32 `UIOriginalText`/`UIOriginalParagraph`/`UIBigButton` twin
sites drawing `.ffn` glyphs directly); everything else stayed MSDF. R119 swaps
the DEFAULT, so the whole engine renders original glyphs.

## Renderer map (why the swap is total)

The renderer choice happens at exactly four branch points, all keyed on
`TextStyle.VFont != null`:

| branch | file:line | consumers |
|---|---|---|
| `UIElement.DrawLocalString` | FreeSO/TSOClient/FSO.UI/Framework/UIElement.cs:820 | UILabel, UIButton captions, UIDialog titles, UIProgressBar, UIListBox rows, UICustomTooltip, pie-menu labels |
| `TextDrawCmd_Text.Draw` | FreeSO/TSOClient/FSO.UI/Controls/UITextEdit.cs:1489 | UITextEdit/UITextBox + TextRenderer (UIMobileAlert body) |
| `UILayer.DrawTooltip` | FreeSO/TSOClient/FSO.UI/UILayer.cs:383 | every hover tooltip (R117's unreachable residual — now closed) |
| direct `VFont.Draw` | UIHeadlineRenderer.cs:187-196, UIMoneyHeadline.cs:65 (Simitone + FreeSO copies), MapPainterPlugin.cs:190 | world-space skill/speed/money headlines |

`TextStyle.VFont` is populated from `GameFacade.VectorFont` at TextStyle.cs:218
(script-parsed styles) and UILayer.cs:136/146/156 (DefaultTitle/Button/Label) —
one assignment site feeds all of it: SimitoneGame.cs LoadContent.

## The port

1. **Framework (additive; MSDF/TSO behavior unchanged):**
   - `MSDFFont.Draw`, `MeasureString`, `GetAtlas` are now `virtual`;
     a `protected MSDFFont()` ctor allows subclassing without a FieldFont;
     `virtual MSDFFont SelectForSize(int)` returns `this` in the base.
   - `TextStyle.Size` setter re-selects `VFont = VFont.SelectForSize(px)` before
     computing Scale (mirrors the old SpriteFont `Font.GetNearest` branch).
2. **Client: `UI/Controls/OriginalVectorFont.cs`** — an MSDFFont family:
   - one instance per regular table `Fonts\variablesans_{07,08,09,10,11,12,14,16,18,20}.ffn`
     (all byte-pinned by `uiglyph` since R87/R96), lazily loaded through
     `OriginalGlyphFont.Load` (retries until the TS1Global FAR mount exists;
     pre-content draws are no-ops, never crashes);
   - `VectorScale = 11.5/px` so the TextStyle formula yields `Scale == 1.0`
     EXACTLY on ladder sizes — native bitmap rendering, no scaling; off-ladder
     sizes scale by `px/tablePx` (13→12 at 1.083, 25→20 at 1.25, 37→20 at 1.85);
   - a ROOT instance for direct `GameFacade.VectorFont` callers: delegates to a
     _12 reference table converted into base units (0.72 → 0.695 glyph scale),
     keeping the Measure/Draw pair self-consistent exactly like the MSDF font;
   - draws via its own internal SpriteBatch (`AlphaBlend`, `CullNone`, the
     caller's matrix passed as `transformMatrix` — full scroll-transform
     support), safe because every existing call site already sits between
     `batch.End()`/`batch.Begin()`.
3. **SimitoneGame.cs:392** — `GameFacade.VectorFont = OriginalVectorFont.CreateRoot()`.
   MSDF remains only for `EdithVectorFont` (the debug/Edith tool font).

### Engine-derived choices (disclosed — the data is canon, these are ours)
- Nearest-table ladder with ties to the SMALLER size: 13→12, 15→14, 17→16, 19→18;
  below 7 → 07; above 20 → 20.
- **`_48` is NOT in the ladder**: it has 16 glyphs (letters+space only — NO
  digits; `'1234567'` measures 0). It is the wordmark display font; sizes >20
  render as scaled `_20` instead.
- Bold (`_12_bs`, `_14_bs`) and condensed (`_11_s`) tables stay on their explicit
  per-panel loaders (money, R98/R115) — the default family is regular-weight,
  like the single-weight MSDF font it replaces.
- Polish/Russian `_bs`-style variants (FAR 465-494) remain unused (corpus is
  English-localized; established R87).

## Canon (tools/iff-dump/r119/make_r119_vfont_canon.py → r119-vfont-canon.txt)

Metrics computed byte-verbatim from the R87 canon extraction
(`uigr-orig/0450..0464`), using R109's uniform advance convention (w+t0,
space=5px — identical to `OriginalGlyphFont.Advance`). Cross-checks reproduce
every prior pin: `_08 "The Sims"=54` (R106), `_10 "Lot 20"=43` (R97),
`_11 "12:05 AM"=69` (R99), atlas dims 256x107/115/121/152/224 (R97-R106).
`r96-format-notes.md` quotes `_07` as 256x84 — stale prose: the bytes AND the
shipping parser (UIOriginalText.cs:89-90) both read **256x83**; `_07` was never
gate-pinned. New pins: `'The Sims'` = 50/54/58/61/70/71/88/91/103/115 for
07/08/09/10/11/12/14/16/18/20.

## Gate (`uivfont`, check 67 of 67)

- `GameFacade.VectorFont` IS the family root;
- 19-point ladder selection table (incl. 5→07, 37→20, 60→20);
- TextStyle routing: `Size=10` → `_10` table, `Scale == 1.0` exact,
  `MeasureString("Lot 20") == 43`;
- per-table `'The Sims'` canon widths across all 10 ladder tables;
- engine defaults (DefaultLabel/Button/Title) carry the family;
- headless draws through the engine override: root 0.72 → _12 at 0.690 glyph
  scale; table draw at native 1.0; counters increment.

## Residuals

- Off-ladder sizes render scaled bitmaps (13,15,17,19,25,37) — mild resampling;
  the mobile size ladder (12/15/19/37) was designed for SpriteFonts that don't
  exist on disk (SimitoneGame.cs:383-386 commented out since upstream).
- The 32 per-panel twins (R96-R115) remain in place — they do exact original
  placement (e.g. money `_12_bs` centered in the plaque) and hide their modern
  labels; the default family covers everything they don't.
- MSDF still renders the Edith debug tool (`EdithVectorFont`).
- `UIMoneyHeadline` (world-space § floaters) now renders native `_12` regular
  through the default family (was MSDF size 12); the panel floaters (R115)
  remain `_12_bs` twins.

## Verification (r119p2.log)

- `AUTOTEST uivfont original .ffn default renderer: ok=True tablesMounted=10/10 draws=12 glyphs=42 lastTable=10@1` → PASS
  (draws=12 = 10 live engine strings already rendered through the family before
  the check ran + the check's root and table draws — the swap is LIVE, not
  constructed-for-test)
- `AUTOTEST RESULT PASS passed=67 failed=0` (66 prior checks unchanged-green)
- r119p1 honest FAIL (kept as evidence): gate-side constant — I pinned root
  glyph scale 0.695 for 0.72×11.5/12, which is exactly **0.690**; the port was
  fully correct in that run too (tablesMounted=10/10, engine had drawn 10
  strings through the family, `drew=True tableDrew=True`).
