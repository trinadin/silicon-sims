# R210 — The Tier C 'ui-total' enforcement gate (the closer)

Tier C: "final 'ui-total' gate — walk the live desktop UI tree per state and
fail on any texture sourced from modern Content paths (whitelist = the
disclosed no-canon items below)." This round builds that machine and turns it
on.

## The provenance registry (`UIArtProvenance`)

Every texture mounted through the ORIGINAL pipeline registers:
- `UIOriginal.EnsureResolved` (the FAR resolver — every .bmp member; also the
  cache-hit path so early mounts register),
- `ResolveOrPng`'s byte-faithful png-twin branch (the FAR fallback stays
  modern and unregistered),
- `UIOriginal.Rect` crops (derived from original sheets),
- the original glyph font atlases (`OriginalGlyphFont.FromFFN`),
- (added during bring-up, each a REAL gap the gate caught) the UIIconCache
  renders (avatar heads from original outfits, object DGRP icons),
- the ObjectDialog private-BMP icons (the object's OWN IFF data,
  `ApplyPrivateDialogBitmap`),
- a DISCLOSED set (`NoteDisclosed`) for port additions with no original
  counterpart — currently one: the Go Here interaction icon
  (`int_gohere.png`; the engine draws a ground marker, not a catalog icon).

## The walker (`uitotal`, default suite, runs LAST)

Recursive walk of the live game screen (after every earlier check has mounted
its surfaces). Per texture-bearing element (reflection over a `Texture`
property/field, covering UIImage/UIButton/UICatButton/…):
- original-registered → OK;
- individually disclosed → whitelisted;
- RenderTarget2D → scene buffers, OK;
- 1x1 → solid fills, OK;
- under a whitelisted TOUCH-ONLY ancestor → OK (UIArchTouchHelper hides
  itself every Update on desktop; UIPickupPanel lives at Opacity 0 when not
  holding — mobile-by-design per the charter);
- invisible subtrees pruned (what the law covers is what PAINTS);
- otherwise → VIOLATION, check FAILS.

## What the gate caught on its way in (three real finds)

1. **PanelBack** mounted via the CustomUI png twin instead of the FAR resolver
   → re-routed to `EnsureResolved("cpanel\Backgrounds\PanelBack.bmp")`
   (original bytes, self-registering; png twin now the fallback).
2. **The icon-cache renders** (heads/DGRP icons) and **the ObjectDialog
   private BMPs** — original game data through unregistered paths → registered.
3. Probe hygiene: uistrfam/uidialog now Close their constructed probe alerts
   so the walker sees no probe residue.

Final census on the green run: elements=98 original=28 solids=15
whitelisted=6 violations=0 registry=326.

## Scope disclosure

The walk covers the LIVE game-screen state (the always-on surface). The mode
panels (buy/build catalog, CAS, neighborhood) are pinned by their own checks
(uibuy/uibuild/uicasorig/uinav/…); folding every state's tree into one walk is
future work — the machine and whitelist are in place for it.

Gate: `uitotal` in the default suite (123→**124 checks**); FULL DEFAULT GATE
**124 passed, 0 failed**, clean exit probes, WRAPPER_EXIT=0; dist byte-match
(Simitone.Client 9ada5309, FSO.UI 9f799351, FSO.Client 9c138b72,
FSO.SimAntics 4cfa2c18, FSO.Common 5c8d3066). No proprietary payload.
