# R170 — primary UI composition repair

## Scope and evidence boundary

R170 is the user-authorized repair of three visible desktop defects: the gray
neighborhood surround, paired/blurry buy-catalog imagery with overflowing hover
text, and LIVE-mode gauges/icons leaking behind BUILD. Touch composition is not
changed.

The executable used for recovery is the owner's local PowerPC PEF binary:

- `game-data/The Sims/The Sims Complete`
- SHA-256 `33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f`

The original UIGraphics/ObjectData corpus is read locally at build/test time.
This round commits only source, tests, and this metadata/evidence narrative. It
does not commit or redistribute original art, object data, screenshots, or any
other proprietary payload.

## 1. Neighborhood: the gray border was a failed original-art mount

R169 had already established the desktop aperture law: an unscaled 800x600
neighborhood artboard is centered inside the 1024x768 screen, producing offset
`(112,84)`. The map geometry in the reported screenshot was therefore correct.
The gray area was the screen clear color, not a border that should be removed by
stretching the map.

The shipped FAR manifest stores the surround as
`Downtown\largeback.bmp`. `UISimitoneBg.ResolveNeighborhoodSurround` requested
`Downtown\LargeBack.bmp` through the case-sensitive `TS1Provider.Get` path, so
the original 1024x768 background was never resolved on macOS. The repair routes
the request through `UIOriginal.EnsureResolved`, whose exact lookup is followed
by the same case-insensitive manifest-name fallback used by the other original
UI mounts.

Permanent `uinav` coverage now requires all of the following from the live
screen tree:

- a resolved 1024x768 `Bg` texture;
- the exact background object mounted as a screen child;
- its origin at the screen-centered coordinate; and
- the existing 800x600 artboard/aperture law unchanged.

The post-fix local survey shows the original dark-blue surround around the
correctly inset neighborhood map. It is a local visual artifact only:
`~/Documents/Simitone/uisurvey-neighborhood.png`.

## 2. ProductButton cells: select one 37x37 state, never the 74x37 sheet

### Original code law

`ProductButton::ForegroundBlt` at file offsets `0x20ba24–0x20bb54` tests the
button image-state count, halves the source width for a multi-state product
image, and advances to the second half for the alternate state. Its centering
path blits the selected cell at natural size. This is not a scale-to-fill law.

### Owned-corpus result

A metadata-only traversal of the owner's shipped object corpus reused the
project's production-format FAR/IFF readers and associated priced OBJDs with
their catalog BMP resources. It found:

- 55 content FAR archives;
- 1,203 parsed IFFs;
- 2,837 priced OBJDs;
- 580 priced definitions without a catalog BMP; and
- 2,257 priced catalog BMPs, all exactly 74x37.

The complete dimension histogram is `{(74, 37): 2257}`. Thus every shipped
priced object sheet in scope is two horizontal 37x37 states. This result is a
corpus fact, not a filename heuristic.

### Port repair

`UICatalogItem.ProductIconSource` is now the shared crop law:

- ordinary product sheet: `(0,0,37,37)` normally;
- ordinary alternate/selected state: `(37,0,37,37)`;
- special build/roof provider: its complete single-image rectangle.

Both catalog-cell implementations center the selected source at natural size,
shrinking only when an axis exceeds the 41px frame interior. They never upscale
the 37x37 state. This removes the paired side-by-side image and the blur caused
by resampling the complete 74x37 sheet into a square.

## 3. Hover popup: object iconic render plus measured description box

### Icon path recovered from the original

The popup and grid cell do not share an image pipeline:

- `Product::DrawIcon` takes its type-1/default branch at
  `0x20a954→0x20a968` and calls `DrawDefaultObjectIcon`;
- `DrawDefaultObjectIcon` at `0x237ddc` obtains the object thumbnail,
  `0x237df0` calls `SetupDrawIconic`, and `0x237e28` calls
  `cXObject::DrawIconic`;
- the popup is invoked at `0x26d924–0x26d930`; and
- its placement/measurement path is `0x26dd98–0x26dea4`.

An earlier candidate `/3` sequence at `0x26d7b8–0x26d81c` is a control-manager
buffer operation, not an icon crop. R170 explicitly rejects that
misattribution.

For an ordinary object, `UIBuyBrowsePanel` now creates a temporary out-of-world
ghost from the product GUID, gathers every multipart `ObjectComponent` and its
base position, asks production `World.GetObjectThumb` for the composed iconic
view, and deletes the ghost in `finally`. The resulting standalone texture is
cached per GUID for the browse panel's lifetime. The popup borrows this cache;
the panel owns and disposes it. Dedicated special-provider thumbnails remain
supported and are owned by the popup only when that provider's `DoDispose()`
contract says so. Replacing, clearing, removing, or killing the popup/panel
cannot double-dispose a borrowed catalog texture or leak an owned thumbnail.
Providers that merely inherit base `GetThumb` reuse the cell's borrowed icon:
the base method calls `GetIcon`, and shipped floor/wall providers create a fresh
GPU copy there while declaring `DoDispose=false`. Avoiding that call closes a
per-hover leak without suppressing providers that override `GetThumb` with real
popup art. The static gate pins both dispatch classes.

### Exact popup geometry

The recovered composition law is:

| property | value/law |
|---|---|
| width | 559px |
| minimum height | 127px |
| icon pane | x=0..168 |
| text left | x=178 |
| text right | x=540 |
| description width | 362px |
| name | y=12, original font slot 10 |
| price | x=`name measured width + 4`, same baseline/font |
| description | y=`12 + name font line height`, original font slot 8 |
| measured height | `max(127, descTop + lineCount*lineHeight + 12)` |
| anchor | `x=cellX-559`, `y=cellY-measuredHeight` |

`UIOriginalCatalogPopup` now backs `Size` itself because the base
`UIElement.Size` implementation does not retain assignments. Name and price
are separate measured glyph runs. The description uses `UIOriginalParagraph`
at exactly 362px, and the surface, separator, and above-band anchor all consume
the measured height. The live shipped fixture wraps to eight contained lines
and produces a 559x171 popup with a single crisp rendered object.

The port retains one disclosed safety deviation: the original column-zero
anchor can extend left of the screen, while this port clamps popup x to zero.
The flat dark popup surface is also still a visual approximation until an
exact original surface-art path is proved; its geometry and content law are
pinned.

Permanent `uibandlaw` coverage now pins the static dimensions, crop/fit laws,
actual font-wrapped paragraph child positions, 362px/right-edge/bottom-gutter
containment, name-to-price spacing, dynamic height/anchor, all twelve live
control regions, owned/borrowed texture lifetime, base-vs-dedicated special
thumbnail dispatch, and an ordinary live popup icon that is neither the 74x37
cell sheet nor a 37x37 cell.

## 4. BUILD composition: mode changes are atomic

The overlap had two independent causes:

1. `UISubpanel.Kill` deliberately retained an outgoing category panel for its
   300ms fade. The same path was used across LIVE/BUY/BUILD mode boundaries, so
   an outgoing LIVE panel could remain drawable behind the BUILD band. Rapid
   same-mode swaps could leave more than one older retiring panel mounted.
2. `UIOriginalPeopleChrome.Draw` and `UIOriginalArchChrome.Draw` performed
   manual post-`base.Draw` blits. Although an invisible container makes
   `UIContainer.Draw` return, those later blits still painted LIVE trend bars or
   BUILD seams.

`UIMainPanel.SetMode` now treats a real mode boundary as an atomic replacement:

- every mounted retiring `UISubpanel` is hidden immediately;
- People/Arch/Buy chrome is pre-cleared before the selected branch is enabled;
- the outgoing current subpanel is hidden before its normal `Kill` lifecycle;
- the incoming subpanel is forced to opacity 1; and
- a later 1→1 300ms hold tween is inserted so it is the final opacity writer
  after the constructor's pre-existing 0→1 tween.

Ordinary same-mode category changes never enter that boundary path and keep
their fade. The people/architecture custom `Draw` overrides now return before
all painting when `Visible == false`.

Permanent `uibuild` coverage exercises both incoming opacity writers, preserves
the same-mode fade, requires every retiring subpanel to be hidden at a mode
boundary, and uses render-target positive controls: visible People/Arch chrome
must paint pixels, while hidden instances must paint zero. The render probe
restores the caller's render targets and viewport in all cases.

The post-fix local survey shows a clean BUILD band with no green/red mood bars
or stale LIVE icons: `~/Documents/Simitone/uisurvey-build.png`.

## 5. Survey/test-harness hardening

The UI survey is evidence, not production composition. R170 nevertheless fixes
several false-pass/false-picture routes:

- neighborhood capture is required and delayed only when `uisurvey` is active;
- missing graphics, main panel, popup, or required capture is a failure;
- the popup fixture is selected by actual original-font line count, and the
  expensive object compositor is invoked only for the chosen fixture;
- the popup must be visible, exactly 559px wide, and taller than 127px;
- lot captures explicitly compose the separate 3D `World` layer before UI;
- the default-visible architecture touch helper is hidden in the pre-first-
  update survey path; and
- capture exceptions are attributed and logged rather than aborting before the
  check can report the missing artifact.

This matters because an earlier survey could show a gray lot behind otherwise
correct UI even though gameplay rendering was fine. The final local buy-popup
survey contains the real lot render and one composed red chair:
`~/Documents/Simitone/uisurvey-buy-popup.png`.

## 6. Validation

Exact current source was published self-contained for `osx-arm64`, packaged by
`packmac.sh`, and run through the app binary at
`dist/The Sims-arm64.app/Contents/MacOS/TheSims`.

Focused package command:

```sh
NSUnbufferedIO=YES 'dist/The Sims-arm64.app/Contents/MacOS/TheSims' \
  -ApplePersistenceIgnoreState YES \
  "-path$PWD/game-data/The Sims" -autotest 5 \
  -autotest-opts lot,corpus,uinav,uibandlaw,uibuild,uibargeom,uisurvey \
  -autotest-timeout 600000
```

Final focused result: **11 passed, 0 failed, 0 skipped**, clean `after Run` and
`after Dispose` at 2026-08-30 00:00:39 CDT. Transient
`/tmp/r170-focused-sealed.log` has 694 lines and SHA-256
`396dcaf9d648d624745d004880bf80e2274e04a772b98e017729fd32a5d8435f`.

Final full-default result: **107 passed, 0 failed, 0 skipped**, clean `after
Run` and `after Dispose` at 2026-08-30 00:04:47 CDT. Transient
`/tmp/r170-full-sealed.log` has 1,615 lines and SHA-256
`53cb538e777ea14b7d310e56e262f6705ee25fd98b1e08a2164a0c242055c690`.

The Release client build completed with zero errors (77 existing warnings),
and the final app bundle is approximately 2.1GB. The only expected startup
diagnostic in passing runs is the already documented self-recovering
`splashprogress mount` null reference; there are no product
`SimAnticsExc`/`bad-routine-frame` records.

## 7. Honest residuals

- The popup's x=0 screen clamp is port-authored safety behavior; the original
  column-zero placement can extend off-screen.
- The flat popup surface chrome remains an approximation pending an exact art
  or paint-path recovery. Content geometry and object rendering are recovered.
- The object-thumbnail cache is keyed by GUID. If one browse-panel instance is
  ever retained while the world renderer's visual mode changes, its cached
  thumbnail may require a renderer-mode key. Current mode replacement destroys
  the panel and cache.
- Survey composition is a verification harness and does not prove every
  resolution, locale, custom object, or GPU backend. The live laws and local
  1024x768 packaged artifacts are the decisive scope for this report.
