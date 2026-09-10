# Design: In-lot UI LOOK — mount original UIGraphics chrome (slice 1: artwork/fonts/cursors)

Date: 2026-08-24 · Status: proposed · Round target: R85

## Purpose

Narrow the `[GAP-partial] UI fidelity — LOOK` row in PARITY.md. Today the in-lot live
toolbar renders Simitone's modern sprites and flat navy rectangles. This slices the
fix into the part that is byte-for-byte IFF-falsifiable (mount the original
`UIGraphics.far` chrome art, fonts, cursors as live controls) and explicitly keeps
the part that needs human eyes (the original layout montage) as a written residual.
No engine change. Pure Simitone.Client UI.

## Scoped finding (why "re-skin" is two separate pieces)

Full `UIGraphics.far` man=794 names were dumped from the manifest (engine-independent).
The original in-lot toolbar chrome exists and is mountable by name via the proven
IFF-mount path from `LoadingGameScreen.cs` (`ResolveOriginal`: `ts1.Get(<member>)` →
`ITextureRef`, try/catch, fallback to the existing CustomUI `png`).

Control → member mapping established for the LIVE toolbar chrome:

| Simitone control today | Original UIGraphics member | member# |
|---|---|---|
| pause + speed strip (already mounted) | cpanel\Buttons\pause.bmp, Speed1/2/3.bmp | 224, 248–250 |
| live toolbar backdrop (already mounted) | cpanel\Backgrounds\PanelBack.bmp | 119 |
| motives category button | cpanel\Buttons\Mood.bmp | 206 |
| job category button | cpanel\Buttons\Job.bmp | 198 |
| personality category button | cpanel\Buttons\Personality.bmp | 227 |
| relationships category button | cpanel\Buttons\Relationship.BMP | 230 |
| inventory/people category button | cpanel\Buttons\people.bmp | 225 |
| motive bar fills | cpanel\Greenbars.BMP / Redbars.bmp | 47, 72 |
| help + options camera cluster | cpanel\Buttons\HelpButton.bmp / Camera.bmp / zoomin / zoomout | 170, 155, 265, 266 |
| fonts | Fonts\variablesans_07..48.ffn (incl. Polish/Russian) | 450–464, 465–494 |
| in-lot cursors | Shared\cursors\Live*.cur (6 files) | 678–683 |

Correction to PARITY wording (data disproves it): the backdrop (0119) and the
pause/speed cluster (224/248–250) ARE already mounted; the accurate statement is
"the category buttons, motive bars, fonts, and cursors are still Simitone's modern
sprites" — none of the remaining chrome is, and none forks the layout.

The original toolbar is a horizontal bottom-bar *montage* (901×100 region in 1024
mode). Simitone's `UIMainPanel` is a bottom panel with a diagonal/divider structure.
Faithfully reproducing the original *layout* is a second, independent piece of work
that cannot be verified headlessly and needs eyes. It is NOT in this slice.

## Scope (what this slice does and only this slice)

1. **Live category buttons.** In `UIMainPanel`, swap the 5 live-category
   `UICategory.IconName` entries (live_motives/job/personality/relationships/
   inventory) to IFF-mounted original art (Mood/Job/Personality/Relationship/people)
   via `ResolveOriginal`-style mount on the UI thread, falling back to the current
   CustomUI png on any IFF failure (never crash, never block the first tick).
2. **Motive bar fills.** In `UIMotiveSubpanel`, use Greenbars/Redbars for the
   positive/negative fills instead of flat tints, IFF-mounted with fallback.
3. **Fonts + cursors.** Make best-faith effort to expose variablesans_*.ffn and the
   Live*.cur set; if mounting the ffn glyph tables into the existing text stack is a
   larger lift, align the in-lot label sizes/styles to the original and record the
   ffn mount as a residual — do not hold the slice on it.
4. **Backdrop.** In LIVE mode stop over-drawing the modern `WhitePx`/`Div` navy
   rectangle when `OriginalPanelBack` is mounted (UIMainPanel.Draw line ~434–445);
   let the original 804×100 navy chrome read as the bar.

## Data flow / architecture

- New `UIMainPanel.MountOriginalChrome(memberName)` and a tiny
  `UIOriginal.EnsureResolved(name)` helper (mirror LoadingGameScreen.ResolveOriginal:
  UI-thread gate, ITextureRef via `Content.Get().TS1Global.Get(name)`, try/catch,
  cached, nullable original-ref + bool "fell back").
- Each control keeps Simitone's drawn png as fallback so worst case is today's look.
- No FreeSO submodule edit. No new content PNGs staged — IFF-mount replaces
  pre-conversion for the swapped controls only; existing CustomUI pngs stay for
  fallback and for the untouched BUY/BUILD/OPTIONS chrome.

## Testing / verification

- **New -autotest check `uichrome`** (extend, don't weaken — suite must stay green):
  IFF-literal pin, engine-independent style, mirroring `loadscreen` (R83): for each
  swapped member assert member name + byte length + BMP dims equal raw canon; PLUS
  a widget-tree pin that the mounted chrome is present + visible in the headless
  live render. Never pin absolute rendered hex (render colors wobble run-to-run,
  ERRORS.md).
- **Visual/eyes gate:** regenerate `uidump-lot.png` and inspect it (I can Read the
  PNG); keep PARITY's LOOK row `[GAP-partial]` with the artwork subset marked
  `[DONE-ish]` ONLY when I am confident in the render — full layout montage stays
  `[GAP]`.
- Evidence to `tools/iff-dump/r85/` (tracked), committed normally.

## Residuals (explicitly not in this slice; written down, not lost)

- Original LAYOUT montage (bar geometry, button positions, money/needs placement
  to mirror the 901×100 bottom bar) — needs human eyes, separate later slice.
- BUY/BUILD/OPTIONS sub-panel chrome, pie interaction radial, personality-allocation
  UI, sound-fidelity, full text-style parity.

## Guardrails

- Never weaken uipal / uidump / loadscreen pins; keep 51/51 green.
- IFF-literalism: every claimed mount verifies to raw canon before [DONE].
- Content-thread race: mount on UI thread exactly like ResolveOriginal.
- No `open --args`, absolute `-path`, `-autotest-timeout 1800000` on the gate run.
