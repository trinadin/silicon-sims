# R166 — Generic Sims Call 12: distance to camera

## Result

R166 closes TS1 Generic Sims Call mode 12,
`GetDistanceToCameraInTemp0`, from the owner's original Complete Collection
engine and data. The previously unimplemented FreeSO case now applies the
original fixed-64 tile rotation law, writes Temp0, and exits true. This is a
non-UI engine correction: it reads the current world rotation but does not
move the camera, alter rendering, or change any control geometry.

## Original-engine recovery

`r166-original-engine-decode.py` reads the owner's original PPC PEF in place;
it does not copy any proprietary payload into the repository. It unpacks the
PEF data section, resolves the original `TryGenericSimCall` jump table, and
pins the decisive instructions in both the call and the startup rotation-table
builder.

- original engine SHA-256:
  `33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f`;
- packed data at file `0x5c22f0`, length `0x6d834`, unpacked to `0x7bf80`;
- unpacked-data SHA-256:
  `f740c1dfa1dea8187b12ebf222ca1a39223365d26ad5d2f2e353d5efba247be3`;
- `CTilePt::BuildRotationLookup` starts at file `0x60f20`;
- `cXObject::TryGenericSimCall` starts at file `0xf1f20`;
- TOC `-0x59a0` resolves to jump table data `0x485a8`; entry 12 resolves to
  code virtual `0xe9778`, file `0xf2608`;
- mode-12 code `[0xf2608,0xf26d8)` SHA-256:
  `acc62561f9a0c5c63afc2a26b53a6066dfa16b5f56180c71335c2647d60ef228`;
- rotation-builder code `[0x60f20,0x61070)` SHA-256:
  `c25d81d41eb662c33ce65865de4c03573015e25fb53c4e61c46b032e1b76af80`.

For byte-valued tile coordinates in the recovered/validated `0..63` domain,
the original table is:

```text
r0 = (x, y)
r1 = (63-y, x)
r2 = (63-x, 63-y)
r3 = (y, 63-x)
Temp0 = -(rotatedX + rotatedY)
```

The PPC uses `lbz` followed by `extsb`; the object's level byte is copied but
not used. Rotation zero uses the raw coordinates. The common mode return is
true. An invalid stack-object id takes an original diagnostic path and then
continues into low-address byte reads; it does not contain a supported
"unchanged Temp0, true" branch, so R166 does not invent one. Every recovered
corpus caller supplies an ordinary stack object.

The generated metadata report is deterministic from the repository, `/tmp`,
and optimized Python. Its SHA-256 is
`68184fb0f3c0b484a23fe8bc3e9161d3e55a79daa7bc6e7d2148af1521b8e33b`.

## Owned-corpus callers

`r166-generic-call12-scan.py` follows the production IFF format: chunk headers
are big-endian, chunk payloads begin after the 76-byte header, chunks advance
by their exact declared size, and BHAV payload fields are little-endian. It
walks loose IFF/FAM files, FAR members, and one nested FAR level.

The scan walks 8,871 files, parses 2,238 IFFs and 28,246 BHAV chunks, and finds:

- 47 mode-12 call instances;
- 22 unique call-site signatures;
- 21 unique routine instruction-payload hashes.

The callers are the travel/vehicle placement path:

- BHAV 4098, `Send Downtown/Studio`;
- BHAV 4105, `Get In Cab/Wagon`;
- BHAV 4108, the follower variant; and
- BHAV 4121, `send person to work`.

The last group includes `CarSquad`, standard/junk/limo cars, NPC vehicle
families, and `SchoolBus.iff` itself at instruction 9. That corrects the stale
R165 handoff inference that SchoolBus 4121 did not call mode 12.

The scanner report is byte-identical from the repository and `/tmp`, including
under `python -O`; SHA-256:
`1ee57c4e2d762e9adaad90f802d46ced584a49699e41289cc07ac350aa17a7d4`.

## Port implementation

Nested engine commit `8ad2eaaf` adds only the mode-12 implementation in
`VMGenericTS1Call.cs`:

- `GetDistanceToCamera` expresses the recovered four-rotation, fixed-64 law;
- the handler reads `StackObject.Position` and the live rendered-world
  rotation, writes Temp0, and returns `GOTO_TRUE`;
- a non-rendered interpreter uses a deterministic, port-authored rotation-zero
  fallback. That fallback is headless behavior, not a claim about the original
  game's no-world state.

No lot dimension, screen dimension, projection transform, or camera position
is substituted for the original constant 63.

## Verification

### Exhaustive actual-dispatch probe

`tools/generic_ts1_call_probe` constructs a minimal VM/world/frame and calls
the production `VMGenericTS1Call.Execute`, not a duplicated public façade. It
passes all `64 * 64 * 4 = 16,384` tile/rotation tuples, checking the exact
Temp0 value and `GOTO_TRUE`, plus the explicitly port-authored no-world
rotation-zero fallback.

```text
PASS: Generic Sims Call mode 12 actual dispatch verified for 16384
tile/rotation tuples; fixed 64x64 transform, Temp0 write, true exit, and
port-only no-world rotation-0 fallback.
```

### Packaged integration

The release client builds with 0 errors, then a self-contained `osx-arm64`
publish and `packmac.sh arm64` produce the tested 2.1 GB app bundle.

The focused packaged command must include `corpus`, because the check dispatch
lives in `RunCorpus`:

```sh
NSUnbufferedIO=YES 'dist/The Sims-arm64.app/Contents/MacOS/TheSims' \
  -ApplePersistenceIgnoreState YES \
  "-path$PWD/game-data/The Sims" -autotest 5 \
  -autotest-opts lot,corpus,genericcall12 -autotest-timeout 1800000
```

`r166-genericcall12-run1.log` is a clean 40-line run segment: **7 passed, 0
failed, 0 skipped**. Its live object at tile `(41,1)`, rotation TopLeft,
produced expected/actual `-42`, returned `GOTO_TRUE`, and all 20 pinned sample
and corner values matched. SHA-256:
`0718795c96e094e6bf4a5c30c4c6c64ea02c656233739c7103931c2799755273`.

The regenerated-probe full packaged gate is **104 passed, 0 failed, 0
skipped**, wrapper exit 0, clean Run/Dispose, and contains no `SimAnticsExc` or
`bad-routine-frame`. The natural default commute soak reaches original
CarPortal 4105 and CarJunk 4121, spawns and removes the real car group, and
passes at 10:15. `r166-gate-run1.log` SHA-256:
`541f2c50863413299dbecd80df307cde83c616ad2f266332b8669c125b7b8448`.

## Boundaries and next target

No UI layout, spacing, sizing, hit target, gesture, animation, control
placement, or presentation changed. No original asset or machine-code payload
was committed; reports contain only metadata, hashes, decoded formulas, and
owned-corpus filenames.

The adjacent efficient engine residual is Generic Sims Call mode 13,
`AbortInteractions`: decode its original PPC case, census every owned-corpus
caller, implement only the recovered semantics, and add an actual-dispatch
probe. Chance Cards and the natural death decision-to-ghost trace remain larger
runtime residuals. Camera panel/scrapbook remains protected UI work requiring a
read-only blast-radius proposal, explicit approval, and visual validation
before implementation.
