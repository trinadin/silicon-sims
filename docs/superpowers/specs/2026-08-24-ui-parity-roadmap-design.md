# R86 Sub-project: Complete Live-Toolbar Original Chrome (design)

> Status: design accepted by user directive ("continue to bring parity to the game, starting with the complete UI, until it is completed").
> This is the FIRST sub-project of the full-UI-parity roadmap. IFF-literalism is the verification standard; nothing is claimed without a read/write trace.

## Roadmap (decomposition — each slice is one round with IFF-literal pins)

1. **R86 — Complete live-toolbar chrome.** Mount the ORIGINAL remaining live-toolbar chrome as controls: the LIVE/BUY/BUILD/OPTIONS mode tabs + EXIT + PAUSE + PEOPLE/INVENTORY button + MONEY display patch, IFF-first with png fallback, and place all mounted chrome at the ORIGINAL layout coordinates (layout montage). (R85 already mounted: backdrop, speed strip, 5 category buttons, Greenbars/Redbars.)
2. **R87 — Original fonts + cursors.** ffn glyph-table (VariableSans) + .cur live cursors mount.
3. **R88 — Original subpanel chrome + pie radial.** Motive/Job/Personality/Relationship/Inventory subpanel art + the original interaction pie.
4. **R89 — BUY/BUILD/OPTIONS panels.** Original catalog + tool + options chrome.
5. **R90 — Neighborhood UI.** NghUI / Nbhd original screens.
6. **R91+ — Residual UI.** Personality-allocation (design-a-person) UI; text-style parity; any remaining modern sprites.

Each slice: canon-scan first (member name + DataLength + BMP dims, engine-independent), mount IFF-first with PNG fallback, add IFF-literal autotest pin + wobble-proof widget-tree gate, keep the full gate green. Never weaken a pin.

## IFF ground truth for R86 (verified this session, engine-independent scan of game-data/The Sims/UIGraphics/UIGraphics.far)

Original resource-template header `Res_CPanel.h`/`Res_CPanel.RT` maps CPanel resource IDs to BMP members. Verified cpanel members relevant to the live in-lot toolbar:

| member | index | bytes | notes |
|---|---|---|---|
| `cpanel\Backgrounds\PanelBack.bmp` | 119 | 25122 | original toolbar backdrop (mounted R69) |
| `cpanel\Backgrounds\LiveGadget.bmp` | 113 | 6612 | kLiveModeGauge — gauge patch behind motive bars |
| `cpanel\Backgrounds\LivePatch.bmp` | 114 | 3936 | live-mode tab patch |
| `cpanel\Backgrounds\BuyPatch.bmp` | 100 | 3708 | buy-mode tab patch |
| `cpanel\Backgrounds\BuildPatch.bmp` | 97 | 3342 | build-mode tab patch |
| `cpanel\Backgrounds\OptionsPatch.bmp` | 116 | 2328 | options-mode tab patch |
| `cpanel\Buttons\House.bmp` | 171 | 3900 | live-mode tab (house) |
| `cpanel\Buttons\objects.bmp` | 211 | 6008 | people/inventory big button |
| `cpanel\Buttons\options.bmp` | 215 | 2632 | options button |
| `cpanel\Buttons\OptExit.bmp` | 213 | 3162 | exit button |
| `cpanel\Buttons\pause.bmp` | 224 | 2438 | pause button |
| `cpanel\Buttons\Mood.bmp` | 206 | 5846 | 128x39 category (mounted R85) |
| ... | | | Job/Personality/Relationship/people, Greenbars/Redbars (all mounted R85) |

The EXACT resource-ID -> member resolution for the four mode tabs (kLiveMode/kBuyMode/kBuildMode/kOptionsMode) MUST be confirmed by canon scan of `Res_CPanel.RT` as R86 Step 1 (same method R83/R85 used); the table above is the verified cpanel surface, not an assertion of template IDs not yet parsed.

## R86 scope (what this slice does and does not do)

DOES:
- Mount the ORIGINAL LIVE/BUY/BUILD/OPTIONS mode tabs, EXIT, PAUSE, and people/inventory button as in-lot controls, IFF-first, png fallback (same-source-png pattern as R85).
- Move the category switcher + mounted chrome to the ORIGINAL layout coordinates so the toolbar reads as the original montage (coordinates are number-verifiable via PIL, not screenshot-verified).
- Add `uitoolbar` IFF-literal pin (member+bytes+dims) + extended uidump tree gate (mode-tab dims + original toolbar layout horizontal span).
- Keep full gate green (52 -> 53).

DOES NOT (residual, kept honest):
- ffn/.cur fonts+cursors (R87), subpanel chrome + pie radial (R88), BUY/BUILD/OPTIONS panels (R89), neighborhood UI (R90), personality/text (R91+).

## Constraints (global, from R85 plan + SOP)

- IFF-literalism: every claimed mount verifies to raw canon before [DONE]; evidence in tools/iff-dump/r86/ (tracked).
- Never weaken existing pins (uipal/uidump/loadscreen/uichrome + all others); suite stays 52/52 before this ships as 53.
- Never pin absolute rendered hex (colors wobble) — pin IFF families, byte lengths, BMP dims, widget tree.
- Mount on the UI thread; try/catch every IFF read; fallback to png so worst case = today's look.
- Run gate with -autotest-timeout 1800000; never kill a quiet run; never launch via open --args.
- No screenshots: PIL/text verification only.
