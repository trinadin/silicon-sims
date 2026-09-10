# R239 camera and family album: corrected native law

Evidence is decoded from the owner's local PPC executable, SHA-256
`33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f`.
`recover.py` reproduces the disassemblies and TOC string metadata with the
existing Capstone helper. The full R153 symbol map resolves function names;
prior round prose was treated as a hypothesis, not executable evidence.

## Material corrections to R190

1. **The camera frame is above the pointer**, with the pointer at its bottom
   center. `ComputeCameraFrame` 0x1c4f90 writes left=`mx-w/2-1`,
   right=`mx+w/2-1`, top=`my-h-2`, bottom=`my`. Calls to 0x37c70 are
   `OffsetRect_Win32`, which moves the entire frame into view before the
   final edge clamps. The prior centered-and-clipped interpretation was wrong.
   Right/bottom overflow translates to one pixel short of the viewport edge.
2. `TSOnMouseDownL` insets each frame edge by one before requesting the photo.
   Thus a normal 200x150 preset produces a **198x150** photograph and a
   200x152 outline. Larger presets analogously produce 398x300 and 598x400.
   The source arithmetic is preserved rather than forcing symmetric dimensions.
3. `TSOnKeyDown` 0x21774c..0x217770 changes the current camera dimensions by
   **4px per key event**: Left subtracts width, Right adds width, Up adds
   height, Down subtracts height. Ctrl is required and the pointer must belong
   to the game view. Shift does not change the step. The existing continuous
   per-render-frame 2/8px update and reversed vertical direction were wrong.
4. The frame paint path 0x1d4b0c..24 passes opaque white to the outline drawer.
   `MouseEnteredChild` 0x1c4f40 clears the frame-visible state; a world overlay
   should disappear when a UI child owns the pointer. The overlay now maintains
   an actual viewport-sized mouse listener across resizes.
5. `TakeAsyncSnapshot` 0x1c4e10 queues the inset rectangle and plays
   `ui_camera_photo`. The request retains chosen dimensions and JPEG quality
   until the following scene readback.

Fixed preset and custom clamp tables remain 200x150,400x300,600x400;
`CamSetSize(int,int)` clamps each axis independently and reclassifies an exact
preset match. `CamGetQualityValue` 0x20ca20 returns 20/50/90 for low/medium/high.

## Family identity and persistence

The original uses **one PhotoAlbum directory per neighborhood**, not a directory
per family. `Family::GetScrapbook` 0x75fd0 appends `PhotoAlbum/` to the neighborhood
path. `GetExportName` 0x760b0 constructs family name + `_` + field0x10c.
`LoadFamily` 0x76810 proves this field is the **FAMI resource ID**. It is not
`FamilyNumber`: `DoStream` places HouseNumber at0x110 and that distinct persistent
number at0x114.

`cScrapbook::AddPhoto` 0x249520 creates these files:

- `<family name>_<FAMI ID>_<sequence D4>.jpg`
- the same basename plus `.txt` for the description
- the same basename plus `_thumb.jpg` for its thumbnail

The sequence starts at zero and advances monotonically within the loaded album.
Loading continues after the highest stored suffix, including orphan caption
filenames; deletion does not recycle a number during that session. Native load
filters the family export name and excludes thumbnails. The port now orders
numeric suffixes, including values beyond four digits, rather than lexical paths.

`cPage::Save` 0x249ff0 writes the full JPEG only for a new-image page. Caption
edits must not reencode an existing image. It creates a thumbnail fitted inside
100x75, keeping aspect ratio, and applies the same selected JPEG quality. Native
AddPhoto saves then reloads the JPEG, so the displayed page is the compressed
result. The implementation now follows these operations using the already
referenced ImageSharp dependency; no new package was introduced.

The writable port adaptation remains `UserDir/PhotoAlbum`; original installed
assets and albums are not modified. Path separators/control characters in player
family names are replaced to keep new files within the writable album. Existing
shared `photo-N.png` pages remain visible after the family-specific JPEGs. Each
retains its original source path and shared identity; caption edits do not convert,
move, reassign or reencode it. This legacy compatibility layer is explicit port
behavior, not a claim that native albums shared photos between families.

## Rendering and test boundaries

Capture reads the actual scene backbuffer after World and before UI. Logical
capture edges are converted to physical pixels using the effective DPI, then the
readback is reduced to the native logical photo dimensions. This fixes the
previous high-DPI top-left/size mismatch. It does not alter GPU render targets,
viewports, shared scene ownership or the global camera. The album owns a captured
texture after transfer; failed transfer disposes it in `finally`.

`AutotestCamera239.Check` exercises independently transcribed numeric edge cases,
actual key events and child/focus gating, preset transitions, integer/fractional
DPI conversion, JPEG quantization changes, thumbnail aspect, family isolation,
shared legacy access, caption byte stability, persistence and deletion. All disk
operations use a unique temporary album and restore the exact original model
state, including sequence and family context.

The asynchronous `BeginCaptureCheck` / `FinishCaptureCheck` test uses the real
scene pass and a temporary solid magenta UI marker over the capture rectangle.
It requires that marker to draw while the persisted photograph excludes it,
contains varying world pixels and keeps the original output dimensions. The
previous pending request, counters, owner album and panel selection are restored;
`CancelCaptureCheck` is idempotent. Parent integration owns runtime execution and
visual validation; these source additions were not built or launched by this
agent.

## Explicit residuals

- PIP is still absent. `pip-assessment.md` recovers native placement, sizes and
  lifecycle, and identifies the existing opcode35 primitive which discards its
  behavior. Authentic offscreen-event PIP requires a separately centered renderer
  and simulation-to-UI event integration; a crop of the main view would not do it.
- ImageSharp JPEG at the recovered quality numbers is not a claim of byte-identical
  GIMEX encoding or identical resampling kernels. The high-DPI reduction is a port
  adaptation absent from the original fixed-pixel renderer.
- Orphan caption filenames advance numbering but caption-only pages are not
  materialized in the port. Original load handles those separately.
- Original AddPhoto removes a newly added page if its initial Save fails; the port
  retains a failed-save page in memory and logs the error so it can be retried.
- Description sidecars retain the port's UTF-8 storage; full original localized
  byte-encoding and filesystem error-dialog parity are not claimed.
