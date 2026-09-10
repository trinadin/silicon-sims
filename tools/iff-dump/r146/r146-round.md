# R146 — expansion buy bands + far-zoom black-terrain follow-up

User report (3 photos, session 2026-08-27 21:24):
1. "The button issue is still a thing" — bottom-right duplicate/overlapping
   buttons (photo 1: community lot, plaques floating with no band chrome).
2. "The left part still has issues too" (photo 2: the mobile touch grid +
   subsort row floating over the 3D view).
3. "Road and ground turns black when zoomed out. Sometimes pops back in."
   (photo 3.)

## Root causes

1+2: game.log 21:22:59 mounts `BuyCOne..Four` + `BuySubSortOther/All` — the
R122 MOBILE composition still serving the community lot's BUY on desktop.
The R145a gate (`bandLot = GetLotType == Normal`) had deliberately left
expansion lots on the mobile composition ("until their own decode round");
on a 1024x768 desktop that composition draws its full-width touch grid +
subsort row + function strip stacked over the 3D view with no band
background — exactly the photos.

3: far-zoom terrain failure — see zoom-black-notes.md (reproduced,
localized to the terrain render path, cumulative across zoom switches;
root fix deferred to a dedicated round).

## What shipped

**The original EXPANSION buy bands (all five lot types), fully decoded this
round** (expansion-buy-band-law.md):
- 5 mains per mode (slots 0-3 + Misc at 7; 4-6 hidden), downtown-pattern
  anchors (studio's 4th at 138,14), BuyBack<family> toggle art,
  BuySubSort<Family> subsort plaques (studio/magic reuse the downtown set —
  engine +0x308/+0x328), tooltips STR#150 [16/24/32/40 + i] mains and
  [8..15] subsorts (magic shares the Superstar block — verbatim engine law),
  Misc main = expansion-sort bit 0x80 (GetMagictownMask jump table decoded),
  sort LOCKED per lot type (no Objects-button toggle on expansion lots).
- `UIOriginalBuyChrome` generalized (LotMode + Exp* tables);
  `UIBuyBrowsePanel` BandMode on every desktop lot with an expansion
  InitCategory branch (whole-mode catalog + main-slot mask) and function
  subsorts; `UIMainPanel.SelectExpMain` (engine entry: main state kept,
  subcatalog 0 applied); mobile paging arrows now hidden on ALL desktop BUY.
- Fixed a latent band bug found on the way: `InitSubcategory` gated on
  `ChoosingSub` — every subsort click after the first was a silent no-op;
  the filter now always re-runs.
- Gate: NEW check **uiexpband** (86→87 checks) — 19 byte-exact art pins
  (R146ExpArt, incl. the Maxis art-reuse identities), chrome law for all
  five modes, live mask counts (community main0=364, Misc=428, downtown
  main0=386) and the sub-filter law. uibuy's old mobile-DT-row pin
  corrected to the band law (SelButtons==0 on desktop + CurrentSubCats).
- uisurvey: NEW state F (community band dump) + Z* zoom states with the
  world-scene capture harness (see zoom-black-notes.md); tree cap 220→400.

## Verification

- Narrowed gates green at each step; full gate result in r146-gate.txt.
- Survey dump uisurvey-expband.png shows the 5 community mains + grid.
- Engine-quirk verbatim disclosure: studio art/tip order crossed; magic
  reads superstar tips; ST/MT subsorts use downtown art — all per the
  binary, kept as-is.

## Residuals / next

- Far-zoom black terrain: root fix next round (harness lands this round).
- Plaque enable matrices (graying empty plaques) unported (both bands).
- Expansion band popup/enable behaviors inherit the r145 popup residuals.
