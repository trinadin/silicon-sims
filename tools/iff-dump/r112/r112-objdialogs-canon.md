# R112 — Dialog base classes in original .ffn type: title, body, buttons

**Slice**: every dialog/message-box in the port renders through `UIMobileDialog`
/ `UIMobileAlert` / `UIBigButton` with modern MSDF text (UILabel size-37 title,
TextRenderer body, UIButton caption). The STRINGS were already original
(button labels come from `GameFacade.Strings` table "142"); this round makes
the RENDERED GLYPHS original .ffn data via the dialog-family table
(`variablesans_09`, the same table R94's cheat response and the six-live-font
set established for dialog text).

## Canon (raw)

- `GameData/UIText.iff` STR# **`ObjDialogs`, chunkID 142**:
  file 0xc231, chunk size 10,292,
  sha256 `fa35753e3a5a6d9c8cfd11261ee66d11f500530c058706e09b4e5489f849c3d5`,
  format −3 (lang-pairs), 90 entries.
- English button labels (byte-verbatim, entries 0–3, translator comment
  `9 Object Dialog: Button: Label`):
  `[0] OK  [1] Cancel  [2] Yes  [3] No`
- The port already reads these (`UIMobileAlert.AddButton` →
  `GetString("142", "0".."3")`) — the port-side strings were provenance-correct;
  only the rendering was modern.
- Scope note: table 142's remaining 86 entries are per-object dialog texts
  (baby naming etc.) consumed elsewhere; this round pins the chunk + the four
  button labels.

## Port (role-mapped to the established live-font set)

1. **`UIMobileDialog.Caption`** — a `UIOriginalText` twin in `variablesans_09`
   at the title band (modern UILabel hidden once the twin mounts; the modern
   label remains the pre-IFF fallback, same pattern as R88/R111). Position
   (50, 26) is a DISCLOSED interpretation (the original dialog chrome geometry
   is engine-drawn, not IFF data).
2. **`UIMobileAlert` body** — word-wrapped lines rendered as `UIOriginalText`
   twins in `variablesans_09` (wrap width = the existing TextRenderer margin
   math; line height 16px, disclosed). The modern `TextRenderer` pass still
   computes the layout bounding box (sizing logic unchanged) but its draw is
   suppressed once twins mount.
3. **`UIBigButton` captions** — `UIButton.Draw`'s modern caption is suppressed
   per-frame (`CaptionStyle` nulled only around the base call — the :420 draw
   guard requires it non-null) and the caption is re-drawn centered in
   `variablesans_09`. UIBigButton is used ONLY by dialog-family panels
   (UIMobileAlert, UIHouseSelectPanel, UICallNeighborAlert, UISelectSkinAlert),
   so this converts every dialog button caption without touching non-dialog
   buttons. Fallback: if the .ffn font is not yet mounted (pre-content window),
   the modern caption renders unchanged.
- Gate-readable statics: `UIMobileDialog.TitlesTwinned`,
  `UIMobileAlert.BodiesTwinned`, `UIOriginalText.DialogTitlesDrawn` (draw-time
  counter), `UIBigButton.OriginalCaptionsDrawn`.

## Gate

- New check **`uidialog`** (gate 60 → 61): disk-pins STR# 142 (chunkID/90
  entries/sha256/4 button labels verbatim), then constructs a real
  `UIMobileAlert` headlessly (title + long body + OK/Cancel buttons — the same
  construction path the live game uses) and requires title/body/button twins
  to have mounted and drawn in the original table.
