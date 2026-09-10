# R200 — the follow-sim law (cWinPeople::TrackPerson / StopTracking)

The Tier B item: "follow-sim visibility conflict (R131 vs R144 — resolve via
the TrackPerson decode)". R131 read kTrackingTarget 4601 (45x45 crosshair,
`cpanel\People\TrackingTarget.bmp`) as "the follow-Sim BUTTON, not a gauge
part" but banked its surface; R144 correctly removed it from the toolbar but
guessed "cWinLivePopup backdrop" — leaving desktop follow-sim with NO visible
toggle (the motive subpanel mounted the crosshair button with
`Visible = false`).

## The decode

**`cWinPeople::TrackPerson(cXPerson*)` @0x28bf00** (symbol 0x28bf02, 344B):

- Pre-clear: if a tracked gadget exists (`this+0x244`) detach it
  (0x509d50(gadget, 0, {0,0})) and null the slot.
- Same-person branch: if the person equals the global tracked person
  (TOC-0x70b8 → deref) → `0x47cb10(0)` — camera untrack — and return.
  **Clicking the tracked sim's portrait again STOPS tracking.**
- Pet/child guard ladder (0x240640 flag → person field +0x638 family-array
  resolve → 0xc4970(0,1)) — species edge cases, then return.
- Otherwise: `idx = 0x243f90(webcamIndexOf(person))`, bounds-checked against
  the webcam count (`this+0xec`), then
  `this->0x244 = this->0xf0[idx]` — **the tracked person's WEBCAM PORTRAIT
  button** — and it is reparented into the tracking container (`this+0x240`)
  at `{0,0}` (0x509d50), composing the 45x45 crosshair over the 45x45
  portrait. Finally `0x47cb10(person)` — the camera follows.

**`cWinPeople::StopTracking()` @0x288740** (92B): detach the tracked portrait
gadget + `0x47cb10(0)`.

**The trigger surfaces** (r195/callers.py scan):

1. **Webcam click** — `cWinPeople::TSOnCommand`: the +0x224 branch
   (0x28c298) SELECTS the clicked person only when different
   (`CPState::SetSelectedPerson` — which itself calls StopTracking at
   +0xac), then the +0xfcc sender loop (0x28d038) scans the webcam array
   (`this+0xf0`) for the command source and calls `TrackPerson(person)` on
   EVERY webcam click. Net: click unselected = select + track; click
   selected = toggle (same person again = untrack).
2. **RETURN** — the local cDDDSimsView hotkey dispatcher (the unnamed
   0x217554..0x217e50 function; a Windows-VK switch: 0x09 Tab → wall
   cutaway ladder, 0x1B Esc, 0x21-0x28 navigation/arrow-scroll,
   0x70-0x7A F-keys → CPState mode/wall/level calls): `cmpwi r30, 0xd`
   (VK_RETURN) → 0x217a3c, gated `CPState::GetMode() == 2` (**BUILD**) →
   `0x243ea0` (selected index) → `0x244690` (resolve) →
   `TrackPerson(peopleWindow, person)`. Enter in build mode tracks the
   selected sim; Enter again untracks.
3. **Stops** — `cObjPickerTool::OnChar` (Esc in the pick tool),
   `CPState::SetSelectedPerson+0xac` (selection change),
   `EdgeDetectScroller::OnKeyDown/OnMouseUpR/OnMouseMove` (manual camera
   input), `cWinViewControl::TSOnCommand+0x64/+0x9c` — **the UCP LEVEL
   buttons** (`SetLevel(1/2)`, skipped when the level is unchanged; NOT the
   mode buttons), `cWinPictureInPicture::TSOnMouseDownL`, `SAnimator` dtor.
   **Mode switches do NOT stop tracking** — the follow outlives the people
   window's visibility.

Resolution: R131 was half right (the crosshair is real and is the follow-sim
mark) but the surface is the WEBCAM PORTRAIT, not a button on the gauge;
R144's "popup backdrop" reading is retired.

## The port

- `UIOriginalPeopleChrome` (the cWinPeople port) gains the model:
  `TrackedAvatar` (the engine's single global), `TrackingTarget` (the 45x45
  reticle, lazily resolved), `TrackPerson(av)` (same-person branch →
  `StopTracking`; else attach + `WorldState.ScrollAnchor = av.WorldUI` =
  the port's camera-follow), static `StopTracking()` (clear crosshair +
  anchor), and `SyncTracking(game)` — the stop laws polled every frame from
  the always-mounted frontend (selection change = SetSelectedPerson law;
  anchor cleared = EdgeDetectScroller/level-button law; the live-only
  chrome also polls from its own Update).
- `SelectWebcam` now follows the engine click order: select when different,
  then toggle-track. `SelectWebcam` is public for the gate.
- `UIOriginalWebcamButton.Draw` composes the crosshair over the tracked
  portrait at {0,0}; `CrosshairVisibleForProbe` exposes the same resolver.
- `UIDesktopUCP` (desktop keys): RETURN gated to BUILD mode →
  `TrackPerson(SelectedAvatar)` — next to the F-key mode shortcuts.
- The desktop `UIMotiveSubpanel` crosshair-button mount is RETIRED (the
  hidden TrackButton was the R144-era placeholder); the mobile/touch ctor
  keeps its button by design; `IsTracking`/`ToggleFollow` remain (mobile +
  the semantic contract).

## Gate

`uirate` EXTENDED/replaced (still 115 checks; no pins weakened — the
disclosed replacement: the old "motive subpanel TrackButton + camera
toggle" pin retired WITH the desktop mount, superseded by the stronger
engine-surface set): the 45x45 crosshair art pin on its true surface
(`TrackingTarget`), the retired-mount proof (`sp.TrackButton == null` on
desktop), and the live law through the real chrome + vm — click 1 selects +
tracks (MyUID, TrackedAvatar, crosshair visible on that portrait, camera
anchor = the avatar's component, attach counter), click 2 same-person
untracks (crosshair gone, anchor null), selection-change detach
(SetSelectedPerson law via SyncTracking), anchor-clear detach (manual camera
input law), TrackPerson(null) safety. The probe drives the production
`RefreshWebcams()` synchronously (the 30-frame cadence must not gate the
law probe).

Targeted soak uirate+corpus PASS (art/math/live all True); uilive+uigauge
re-verified PASS alongside; FULL DEFAULT GATE **115 passed, 0 failed**,
carseek PASS, clean Run/Dispose exit-probe chain, WRAPPER_EXIT=0; dist DLLs
byte-match publish (Simitone.Client ba5a5769, FSO.SimAntics 6b8d6403,
FSO.Client 2b83283d). One honest intermediate FAIL during development
(track-no-avatar — the probe originally depended on the 30-frame webcam
refresh cadence; fixed by driving the same public refresh synchronously).

## Residuals

- TrackPerson's pet/child guard ladder (0x240640/0xc4970) not decoded to
  the field level; the port treats family avatars uniformly (the webcam
  strip itself is family-scoped, so the guard is inert here). Disclosed.
- The crosshair's exact compose mode over the portrait (the engine
  reparents the button into the tracking container; the port draws the
  reticle at {0,0} over the cell) — same net geometry, mechanism differs.
- The dispatcher's other VK branches (Tab wall ladder, F-key mode/wall/level
  mapping beyond the ported F1-F5) are not fully enumerated; only RETURN is
  ported this round. The port's Space-to-cycle-sims remains a Simitone
  convenience (the original dispatcher has no VK_SPACE case — it now stops
  tracking via the selection-change law, engine-consistent).
- No proprietary payload in this round's evidence.
