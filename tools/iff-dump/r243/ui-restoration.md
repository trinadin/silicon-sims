# R243: original PIP transition and ownership restoration

The maintained Simitone fork now uses the existing Graphics / UI effects
checkbox for picture-in-picture transitions. The original Mac executable's
normal PIP window refuses background capture, so its generic compositor skips
intermediate opacity buckets. This round reproduces that path locally: logical
visibility begins immediately, the complete image/caption/closebox appears at
324 ms of the 333 ms opening, and closing stops drawing at 10 ms while retaining
logical visibility through 333 ms. Turning effects off uses direct show/hide.
This is not a smooth alpha fade.

The independent reviewer traced the real PIP virtual table, constructor flags,
private-buffer ownership, parent attachment, compositor branches, UI-effects
checkbox identity, millisecond clock calibration and float ramp. The exact
contract and byte-pinned offline fixture are in
[the fade recovery](../r243-fade/contract.md). Normal construction does not enter
the alternative fixed-alpha paths. Reversals preserve the original asymmetric
complement law; repeated requests do not restart transitions, even for a dormant
opening hidden by an effects-off operation. Toggling the option alone does not
cancel an active transition.

## Ownership and lifetime

Duration ownership is now separate from visibility and the event request.
A skipped, hidden request can own the window without subscribing a timer.
Same-owner retries clear prior duration before validation. Timer expiry and
explicit close release duration immediately; Escape and image activation keep
it until hiding completes. Hide retains the target; explicit close clears it
before starting the transition. The last image remains available throughout
closing. Disposal synchronously clears the transition, target and renderer.

The skeptic caught three implementation issues before final packaging: timer
and explicit-close release timing, the dormant-opening repeat guard, and an
invalid-target update that could accidentally convert an active close to an
immediate hide after changing the option. All three have dedicated regression
coverage. Full managed review is retained in the fade contract.

## Blast radius and limits

Production changes are confined to UIOriginalPictureInPicture. Existing window
sizes, positions, controls, captions, world-camera projection, snapshot framing,
shared opacity machinery and cutaway rendering are unchanged. The new test runs
inside the existing isolated album/camera fixture, restores option values and
GPU state, and writes only local validation captures. Original game data stays
read-only. No new setting or proprietary asset is distributed.

The original executable was disassembled, not run for native visual comparison.
Its retained parent damage can affect stale pixels; the port redraws its parent
and gates the whole PIP Draw call. Monotonic milliseconds and advancing visible
transitions before managed input/drawing are explicit scheduling adaptations.
The new input checks exercise production handlers and visibility eligibility;
they are not an end-to-end physical mouse injection test. Native hit traversal
was separately traced and has no opacity gate.

## Cutaway investigation

The second reviewer independently rejected the proposed target-room shortcut.
Original ComputeCutaway takes a floor filter and computes projected wall-extent
masks, including terrain and diagonal sides. Dynamic cutaway separately combines
room history, selected/tracked-person state, cursor and strategy inputs. The
secondary renderer temporarily clears the tracked object; it does not substitute
the PIP target. Twenty-nine executable instruction pins and six packed-floor
fixtures pass. [The cutaway report](../r243-cutaway/review.md) records the complete
finding, unresolved geometry/state details and required future fixtures.

Cross-floor empty cutaways and fixed-ray room masks remain approximations.
Implementing selected-room or target-room shortcuts would not establish parity.
The next cutaway round needs the remaining geometry and state contract before
changing main or secondary wall visibility.

## Validation

The final package passes **139/139** default regression checks with wrapper
exit0, and **17/17** focused checks. Each of the three display
runs passes **24/24**, with no failures or skips and clean Run/Dispose. Requested
800×600 at2× produces physical1600×1022, logical939×600 and effective DPI1.7033333
on this Mac; it is not claimed to be a true2× render. The normal800×600 and
1280×800 runs use1×. Package/runtime results are recorded in validation.json and
accompanying logs.
The new uipip-fade check uses the production window and its real world image,
caption and closebox. Eighteen GPU captures cover all three sizes at the
323/324 ms opening and 9/10 ms closing boundaries, with full-buffer equality to
an opaque reference and exact underlay preservation in skipped buckets.
Additional checks cover short positive timers, negative and hidden ownership,
both reversals, same-direction repeats, Escape, close-button handlers, image
activation, option changes and synchronous disposal. Existing PIP tests run
with UI effects explicitly off and restore the prior option, retaining their
original immediate-path coverage.

The first focused run retained one failing test fixture: its multi-part object
could not establish complete sprite bounds for the skip-if-visible route. The
revised fixture explicitly selects an original single-member object with known
nonempty damage, controls/restores the viewport rectangle, and passes through
the production skip path. All other assertions passed in that first run; the
final package was rebuilt and the complete focused set rerun successfully.

Visual review includes all three PIP sizes, the fractional-DPI rendering and
live HUD captures. `live-hud-comparison.json` records zero changed pixels in
the Needs interior for every display configuration. The entire bottom100px
strip matches at800×600. At1280×800, strip differences occur only in world pixels
right of the existing1024px toolbar; at fractional DPI they occupy only the
last physical row where the UI blends with the changing world. These captures
do not assert full-screen equality or original wide-toolbar parity.

All five final runs have zero failures/skips and clean Run/Dispose. Their426
retained captures have verified SHA256 manifests. All nine published/package
assembly hashes match; the owner config is restored byte-for-byte and the
original executable retains its pinned SHA256. These are owner-local artifacts,
not redistributed game assets. The nested FreeSO revision remains be61f98c;
this round changes no maintained engine source.
