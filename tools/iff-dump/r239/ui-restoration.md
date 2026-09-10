# R239 base-game UI and interaction restoration

This round continues the owner's approved restoration and independent skeptical
review. Maintained source changes belong to the `mac-port` Simitone fork and
its `mac-port-rel` FreeSO dependency. Generated app bundles and original game
data are not maintained source. Original assets were read locally, not edited
or committed.

## Restored behavior

| Area | Confirmed failure and correction | Evidence |
| --- | --- | --- |
| Create a Sim / Family | Full artwork had one-pixel mouse targets; desktop Delete Family dereferenced a mobile-only panel. Native targets, selection, confirmation flows and original font/capacity are restored. Skin choice filtering no longer mutates shared collections. | [CAS review](../r239-cas/restoration-review.md) |
| Camera | Frame was centered and shrunk at edges; original places it above the pointer and translates it intact. Restored four-pixel key events, white outline, HUD gating and physical/logical readback conversion. | [Camera law](../r239-camera/corrected-law.md) |
| Photo album | New captures now use family resource-ID prefixes, JPEG quality 20/50/90, numeric sequences and thumbnails. Caption edits preserve photo bytes. Existing shared PNGs retain their paths and remain accessible. | [Camera/album law](../r239-camera/corrected-law.md) |
| Tutorial | Opcode10 ownership was unimplemented and original-save ownership discarded. Restored exclusive acquire/release, owner deletion/dialog cleanup, original import and version 39 persistence with version 38 read compatibility. Native bit7 drives nonmodal tutorial-owned messages. | [Tutorial review](../r239-tutorial/restoration-review.md) |
| Scrapbook editor | Restored active-endpoint arrows, native word boundaries/double-click selection and viewport-only Ctrl scrolling. | [Editor review](../r239-editor/restoration-review.md) |
| Loading | Early native-font lookup dereferenced a FAR index before initialization. The loader now waits for resources without an exception. | `uiglyph` cold-provider check and startup log |

The shared UI changes were traced before implementation. CAS sheet construction
affects age/gender/skin/arrows/family actions; edit-font changes affect only its
three text fields. Camera changes affect camera mode and photo persistence.
Tutorial state changes affect original-save import, TS1 save payloads, opcode 10
and dialogs belonging to the tutorial owner. Font readiness affects early
resource access; no font dimensions or glyph drawing changed this round.
The Needs panel's prior restoration remains intact: the tall language-specific
background is absent in English and the people band stays 100 logical pixels.

## Validation

The initial packaged focused run passed 14 checks with clean Run/Dispose.
`development-1.log` records it, and its 56 fresh UI captures are retained
owner-locally in `build/ui-audit/r239/development-1`. Camera/frame, Needs and
Scrapbook screenshots were visually inspected. CAS artifacts here are isolated
panel renders over the lot; their empty preview area is not evidence of the
full live Create-a-Sim preview workflow.

The final focused package passed **12/0/0** (passed/failed/skipped), including
15 native CAS regions, 60 corner clicks, original confirmation paths, five
hand-encoded OBJM trailer fixtures, version 38/39 boundaries, native editor
movement and a real asynchronous camera readback excluding a rendered UI
marker. See [focused-final.log](focused-final.log).

Each size run passed **16/0/0**, with **56 fresh captures**:

| Requested window | Actual physical viewport | Logical viewport | Effective scale |
| --- | --- | --- | --- |
| 800x600, DPI1 | 800x600 | 800x600 | 1 |
| 1280x800, DPI1 | 1280x800 | 1280x800 | 1 |
| 800x600, DPI2 | 1600x1022 | 939x600 | 1.7033333 |

The last viewport is host-clamped; the existing uniform fit keeps the original
600px minimum logical height. The requested config was restored byte-for-byte.
See [image checks](size-image-checks.json), the three size logs and image hash
manifests. Native dialog fit and Options panel RGB identity passed in all
three runs. Representative Needs, Camera, CAS and Scrapbook renders were
visually inspected. Screenshots are owner-local under `build/ui-audit/r239`;
proprietary-derived pixels are not committed.

The final app is `dist/The Sims-arm64.app` (2.1GB). Six maintained assemblies
match the self-contained publish byte-for-byte; see
[package hashes](package-assemblies.sha256) and [package log](package.log).
The package script reported ad-hoc signing skipped; this is not a signed or
notarized release. The loader's pre-initialization font lookup now passes and
no loading-screen exception appears in the final focused/size runs.

The exact-source packaged full default run passed **131/0/0**, with
`AUTOTEST_WRAPPER_EXIT=0` and clean `after Run` / `after Dispose` markers.
See [final-default.log](final-default.log). Its 45 fresh captures are retained in
`build/ui-audit/r239/final-default`, with a committed hash manifest. No
production source changed after the final focused, size and full runs.

## Remaining work toward complete parity

This round does not certify complete base-game or UI parity. The next substantive
workflows remain:

- [Original picture-in-picture](../r239-camera/pip-assessment.md): typed event
  routing plus an independently centered world renderer, timing and lifecycle.
- Tutorial icon/highlights, lesson progression/reset/completion and original
  dialog placement/close controls beyond the ownership and modality restored here.
- CAS age/gender outfit memory, original edit-control metrics, family-name
  filtering and full preview/creation roundtrip verification.
- Editor multibyte-language behavior and configurable double-click timing.
- Previously disclosed world/rendering/gesture/localization differences in
  `PARITY.md`; historical DONE labels are not visual-equivalence certificates.

The JPEG codec and image resampling kernel are modern substitutes; exact
compressed bytes are not claimed identical to the original encoder. Legacy
PNG pages remain shared compatibility pages and are not silently reassigned
to the currently active family. Unreadable album directories are handled
without aborting lot entry.
