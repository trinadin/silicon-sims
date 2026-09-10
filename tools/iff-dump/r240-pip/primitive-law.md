# R240 — independent user-event/PIP contract

Evidence is the owner's original PowerPC executable, SHA-256
`33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f`.
Addresses below are file offsets. Decodes contain instructions/metadata only;
no original graphics or executable distribution is added.

## Primitive 35

`TryUserEvent` at `fac70..faf74` uses StackElem+8 as a phase latch:

1. Zero: set simulator global 11 to 1, set phase 1, return 2 (yield).
2. One: set phase 2, resolve StackObject, error 23 if missing, evaluate the
   request and dispatch it, then return 2 (yield).
3. Any other phase: return 1 (true).

LivePIP option suppression and caller ineligibility return true during step 2
without the second yield. The target check precedes option filtering.

Operand inputs, independently decoded by `scan.py`:

| Bytes | Law |
|---|---|
| 0–1 | signed 16-bit seconds; flag 0x20 instead interprets this as a local-variable index (scope 25), whose signed value supplies seconds |
| 2 | unsigned size: 0=100, 1=200, >=2=300 square pixels |
| 3 | unsigned original zoom: 1=Far, 2=Medium, 3=Near; 0 is half-Far |
| 4 bit 0x01 | open; clear requests close on ordinary PIP route |
| 4 bit 0x04 | clear means skip if target damage is fully within the main viewport |
| 4 bit 0x08 | main-camera route; bypasses LivePIP/caller eligibility restrictions |
| 4 bit 0x10 | automatic snapshot requested, also gated by the global option |
| 4 bit 0x20 | local timeout scope |
| 4 bit 0x40 | suppress GlobalDispatch(0x10c,0); receiver meaning not recovered |
| 5 | unsigned one-based STR#305 caption in the executing tree's resource; zero is no caption |
| 6–7 | unused by original primitive; preserved on operand round-trip |

Bits 0x02 and 0x80 are not read by this primitive. Seconds are multiplied by
1000 after signed interpretation. Literal -1 therefore requests -1000 ms;
it must not become 65,535 seconds. Original negative duration does not subscribe
a timer and still owns the PIP window against replacement by another target.

Eligibility (`fad7c..fae20`): zoning type 1 or main-camera flag bypasses caller
filtering. GetZoningType(0) returns 1 (`aaa20..28`); missing nonzero lot entries
return residential type 0 (`aaa6c`). Otherwise the caller must be a person whose
person-data 61 equals global 9, or global 9 must be zero and person-data 77 nonzero.
Caption strings are expanded by original ParseUIString. The port uses its
existing ParseDialogString, retaining that parser's broader parity limitations.

## Corpus and verification

The independent owned-data walk finds **380** opcode-35 sites: zoom 1 has 9,
zoom 2 has 346, zoom 3 has 25. Size 1 has 372 and size 2 has 8. No local-timeout
sites occurred. This is the complete mounted owned corpus, including expansion
files; these counts are not a claim that every site belongs to the base game.
`corpus.json` records resource/BHAV/instruction and raw eight-byte operands.
`operand-fixtures.json` includes synthetic edge cases outside corpus coverage.

`AutotestPIP240.Check` exercises independent operand bytes, signed/local timeout,
raw unsigned fields, reserved-byte preservation, target errors, household and
community eligibility, options suppression, the prepare/dispatch/complete
sequence, save/restore between phases, exact frame payload bytes, legacy frame/thread
boundaries, nested-frame independence, unrelated dialog-state isolation, and unchanged TSO no-op. The root task runs this helper in
the packaged app; this reviewer did not build or launch a second game process.

## Implementation and compatibility

VMSpecialEffect dispatches VMTS1PIPEvent rather than directly moving the main
camera. A synchronous listener can set Suppressed when LivePIP is off, preserving
the original early final return. Main-camera routes are not filtered by LivePIP.
The typed metadata property is deliberately named SuppressPreDispatchNotification;
there is no guessed queue mutation for the unrecovered event 0x10c.

VMStackFrame.TS1UserEventPhase models the original frame-local latch. Save
version 40 appends one byte to VMStackFrameMarshal; routing/direct-control frame
Save paths copy the field too. Version-38/39 readers do not consume this byte and
initialize the phase to zero. Every version-40 frame, including TSO frames,
contains the additional byte (zero outside this TS1 primitive); existing TSO
behavior and older TSO payload reads are unchanged. The earlier experimental
async type 4 was removed before release. No thread BlockingState is read or
written by this primitive.

JIT version 4 and the yielding-primitive registration invalidate old compiled
modules that returned synchronously. Existing R239 AssemblyStore version checks
preserve compatible module loading. Independent tests resume actual saved
VMStackFrames at phases 1 and 2, run interleaved nested frames, and retain an
unrelated VMDialogResult object and its wait counter throughout.

Residuals: imported original object stack phase is not separately reconstructed
here. Option filtering is reported through the client listener after the port
creates/parses the caption, whereas original filtering precedes caption parsing.
The notification receiver for event 0x10c is unrecovered and is not implemented
under an invented name.

## Camera/window and independent review findings

Original GViewer1bef00 reorders arguments without zoom conversion. SetScale
`1d79f4..1d7a2c` computes tile dimensions `16<<zoom`, `8<<zoom`, `4<<zoom`;
there is no clamp of raw zero. Normal zoom limits are 1..3. Renderer supports
zero with Far resources projected at half scale, a port adaptation rather than
proof of identical original resampling.

PIP BuildImage `299160..299268` centers above the target. Object signed Y offset:
`-(damageHeight >> (mainZoom-requestZoom+3))`. Avatar constants are read from
TOC[-0x4f58] -> code+0x59b7a0 (file0x5a4630), floats
`[1/3,0,1/16,16,0.5]`, and TOC[-0x4f5c] -> code+0x59b7b8
(file0x5a4648), doubles `[2,6,int-to-double magic]`. sqrt import0x5a1b78 is
independently named by the std::sqrt(float) wrapper1a4790. The float sequence is
`f31=float(float((1/3)*6)*float(2*sqrt(6)))`, then offset is
`-truncate(truncate(f31*2^(mainZoom+1))/2)`: -9,-19,-39,-78 for main zoom0..3.
It uses main zoom, not requested zoom, in the avatar branch.

Main-camera dispatch `213120..213584` checks its route before Open. Open=false
still recenters and optionally snapshots. AutoCenter=false disables snapshot;
there is no ordinary PIP fallback. The snapshot footprint is the union of
projected integer target tiles (including multi-tile parts), using terrain
altitude and target floor. Its native minimum comes from the fixed SMALL camera
table 200x150, not current user camera settings:

```
divisor = 1 << (3-currentZoom)
W = max(200/divisor, 200)
H = max(300/divisor, 150)
cx = truncate((U.left+U.right)/2)
cy = truncate((U.top+U.bottom)/2)
rect = (cx-W/2, cy+U.height-H, W, H)
```

Independent review caught the initial port multiplying already physical world
coordinates by UI DPI a second time. World.GameResized gets PPX dimensions / SSAA,
and PPX.InitScreenTargets explicitly uses scale=1; the deferred main-camera photo
now passes `logicalCoordinates:false`. Camera-tool UI capture retains its logical
coordinate path. Exact integer tile-top mapping versus managed interpolated /
container WorldUI.Position is tracked as a separate review item, not concealed
by the rectangle algebra test.

Original full-visibility check compares GetLastDamage(3) against HouseViewer
+0x28..34 (`299bac..299c20`). Standard ViewControl mode0 sets viewport height to
`fullHeight-(panelHeight-50)` (`2b3c34..84`), excluding 100 English / 150 double-byte
pixels; hidden/mini mode uses full height. `viewport-height.txt` proves the write
of those exact viewer bounds. A full backbuffer visibility test incorrectly
counts targets behind the bottom HUD as visible. Avatar damage bounds require
original-backed reconstruction; no guessed mesh bound is treated as parity.

### Packaged check observed 2026-09-04 23:38 CDT

`/tmp/r240-focused.log` reports `uipip-opcode: PASS` for all independent core
fixtures, and `uipip-render: PASS` for twelve sequential size/zoom captures and
both automatic-photo routes. The remaining caption ink-boundary failure belongs
to the separate UI validation; these results are not a claim that the whole
focused suite passed.

Visual inspection of `Documents/Simitone/ui-audit/r240/pip-300-zoom2.png` shows a
populated independent target view (architecture, objects and avatar), rather
than a crop of the main camera. `Documents/Simitone/uisurvey-pip.png` shows native
PIP dimensions/placement and visible caption above the HUD. Review found a real
close-button placement error: UIButton.Width is the optional resize override
(default zero), while Size.X returns the natural sprite-frame width. Using Width
placed the close X beyond the PIP/right screen edge. The fix is to use Size.X;
root owns integration and a fresh visual after this correction.

A renderer resource review found a potential disposed-texture restoration path
when resizing PIP after the graphics scope captured old texture bindings. The
renderer now retains its three native size targets until Dispose, so restoring
an earlier bound target cannot rebind an already disposed target. This bounded
ownership correction is separate from visual fidelity claims.

### Frame-local phase follow-up

After the first packaged pass, the root approved eliminating the thread-level
async-state adaptation. The final source now stores and serializes the phase on
each VMStackFrame; the independent helper was strengthened accordingly. Earlier
23:38 pass evidence predates this strengthening; a new packaged run is required
for the final source. This distinction prevents using an old passing result as
validation of the changed save format.
