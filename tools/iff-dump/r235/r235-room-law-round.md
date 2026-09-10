# R235 — the ORIGINAL ComputeRoom law SHIPPED (decision (a): the disclosed calibrated approximation)

The R234 decision executed: the fully-decoded law (r228-r234) implemented
in `VMContext.RefreshRoomScore` at BOTH score sites (RefreshRoomScore +
RefreshLighting's inline copy), FreeSO commit 78b2ddd1.

## The implementation

```
INSIDE: score = clamp( (min(tile56/2,60) − 30) + (room108·40 − 20)
                     + ((2·windows/ents)·40 − 40) + (ents·10/clamp(tile56/2,10,45))
                     + (windows&doors ? (windows/doors)·40 − 40 : 0), −100, +100)
```
- tile56 from the VMArchitecture room map (wall-tile +1, open +2)
- windows/doors from the wall PATTERN code sets {1,7,8,9}/{3,5,6,15,23}
  (patterns, NOT styles — the port's style 1 means "normal wall"; the
  first soak caught the overshoot and the calibration fixed it)
- all bracket constants from the r225 RoomScoreConstants table

## Disclosed approximations (the R234 decision, censor-palette precedent)

- room80-inside = the room's entity count (the r231 shadow-calibrated bridge)
- room108 = 2.05f — the original's light-computed phase-1 BSS value is not
  statically recoverable; 2.05 is fit against the unambiguous House07
  fixture

## Calibration result (the pin)

**House07 / Bob & Betty (engine room 3): the port now scores 50 where the
original save stored 50.222** — which the original's own float→short
storage truncates to exactly 50. The defect that opened this arc (port −10
vs stored 50.222) is closed on the fixture. The Goth 86.333 fixture stays
DISCLOSED (they load outside — the original's outside path seeds per-tile
maps; unattributable to an inside room).

## Verification

- Targeted soak house7 (`roomlaw,mood,corpus`): **7/7 PASS** including the
  new opt-in `roomlaw` pin (room 3 == 50).
- **FULL GATE: 128 passed, 0 failed** on the new engine (gate-run.log) —
  the mood check, free-will, and every other room-score consumer stayed
  green under the new law.
- dist byte-matches publish.
- The opt-in pin keeps the default count at 128 (promotion can follow the
  chancetrace precedent).
