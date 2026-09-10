# R187 — native motive / relationship delta-history law

This note closes the remaining `cAverageHistory` / `cWinDeltaMeter` questions
for the desktop Motives and Relationships panels in the owned **The Sims
Complete Collection** Macintosh executable.

| evidence | value |
|---|---|
| original executable | `game-data/The Sims/The Sims Complete` |
| size | 6,486,820 bytes |
| SHA-256 | `33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f` |
| code address convention | raw executable file offsets |

The decisive result is asymmetric:

* motive meters bind a persistent `cAverageHistory(10, 5)` and use the full
  five-arrow animated state machine described below;
* relationship cards allocate the same meter class, but never allocate or bind
  a history. Their `PumpSample` and `ResetHistory` calls are null-guarded no-ops.
  Relationship trend arrows are therefore dormant in this owned build.

This supersedes the unresolved relationship-history caveat in R185 and the
incorrect motive `meter width = 10` interpretation in R184.

## 1. `cAverageHistory`: exact record and averaging law

The recovered methods are:

| method | address |
|---|---:|
| `GetAverageDelta` | `0x2065d0` |
| `AddSample` | `0x2067b0` |
| `GetAverage` | `0x2069e0` |
| `Reset` | `0x206ae0` |
| constructor | `0x206c00` |

The object is 40 bytes:

| offset | field |
|---:|---|
| `+0x04` | `vector<int>` raw samples |
| `+0x10` | `vector<int>` rounded rolling means |
| `+0x1c` | raw-sample running sum |
| `+0x20` | maximum record count |
| `+0x24` | maximum absolute delta |

The constructor reserves both vectors to `maxCount`, stores both constructor
arguments, and starts with empty vectors and sum zero. Motive constructs it as
`cAverageHistory(10, 5)` at `0x27d9cc..d4` and `0x27dc44..4c`.

Define native integer rounding as symmetric nearest, with a half exactly away
from zero:

```text
roundAway(x) = trunc(x + 0.5)  when x >= 0
             = trunc(x - 0.5)  when x < 0
```

`GetAverage()` is exactly:

```text
if raw.empty(): return 0
return roundAway(float(rawSum) / float(raw.size()))
```

`AddSample(s)` is exactly:

```text
if raw.size() == maxCount:
    rawSum -= raw.front()
    raw.erase(raw.begin())
    means.erase(means.begin())

raw.push_back(s)
rawSum += s
means.push_back(GetAverage())
```

`GetAverageDelta()` takes `means.back() - means.front()`, passes the integer
difference through the same float / symmetric-round sequence, and clamps it to
`[-maxDelta, +maxDelta]`. There is no further dead zone or threshold:

```text
delta = clamp(means.back() - means.front(), -5, +5)
```

There is no empty-vector guard in `GetAverageDelta`; native only calls it after
adding at least one motive sample. One sample consequently produces delta zero.

During startup, mean `n` is the rounded average of all `n` samples collected so
far. In steady state, with sample number `t`, the meter compares:

```text
roundAway(avg(samples[t-9 .. t]))
  - roundAway(avg(samples[t-18 .. t-9]))
```

The two ten-sample windows overlap at `t-9`; equivalently, this is the current
10-sample rolling mean minus the rolling mean retained from nine sample calls
earlier. A result `+1..+5` displays that many green/right arrows; `-1..-5`
displays that many red/left arrows.

`Reset()` clears both vectors and the sum. Crucially, no motive path calls it.
The only executable caller is `cWinDeltaMeter::ResetHistory`, and the only
caller of that method is the relationship card, whose history pointer is null.

## 2. Motive ownership, cadence, and persistence

Each `cWinMotive` owns a vector of `cAverageHistory*` at `+0x1ac`, with one
entry per current `PersonFinder::CountPeopleInHouse()` index. Both `Pump` and
`SetPerson` grow it until its size reaches the current count; every new entry is
`cAverageHistory(10, 5)`. It never shrinks and is not keyed by person handle.

Every `cWinMotive::Pump` (`0x27d990`) does this before updating its selected
person:

1. iterate every current house-list index, without the portrait UI's eight-item
   cap;
2. resolve the person and its motive-float array;
3. read this widget's motive index (`cWinMotive +0x194`);
4. round the float with `roundAway`;
5. append it to that house-index history.

The People panel pumps all eight human motive widgets and all eight pet motive
widgets in each batch (`0x28b094..c4`). Thus every widget records every current
house member whether or not that widget or species panel is currently visible.

`SetPerson` grows the histories, converts the selected handle to the current
house-list index, and passes that entry to `cWinDeltaMeter::SetAverageHistory`.
An invalid person binds null. `SetAverageHistory` resets only the meter's visual
state; it does **not** reset the supplied history. Switching Sims, changing
subpanels, and returning to a Sim retain that index's samples until the owning
`cWinMotive` is destroyed.

The identity rule is literal and slightly fragile: if the house list removes or
reorders people, histories remain attached to vector positions, not handles;
stale tail entries can be reused if the list grows again. There is no hidden
re-key or shrink pass in these methods.

### Pulse cadence

`CPState` owns motive and relationship `PulseGenerator`s at `+0x1f0` and
`+0x200`. A pulse generator stores:

| offset | field |
|---:|---|
| `+0` | accumulated time |
| `+4` | period |
| `+8` | last timestamp |
| `+c` | maximum queued pulses, zero meaning unlimited |

Its constructor uses `(accumulator=0, period=100, last=0, max=0)`.
`AdvanceTime(now)` primes the first call with one whole period; later calls add
`now-last`. `ShouldPulse()` subtracts one period and returns true while the
accumulator is at least the period. CPState leaves `max=0`, so catch-up is not
capped. Changing speed changes the period without clearing or rescaling queued
time.

`CPState::SetSpeed` (`0x20ffb0`) computes the motive period from the integer
speed code:

```text
x = uint_trunc(0.5 + (172 - (speed / 1000.0f) * 171))
motivePeriod       = uint_trunc(0.5 + 1000 * (x / 42.0f))
relationshipPeriod = uint_trunc(0.5 + 3000 * (x / 42.0f))
```

The shipped speed-table constants at TOC `-0x4df0` produce:

| UI speed | speed code | `x` | motive period | relationship period |
|---|---:|---:|---:|---:|
| normal | 760 | 42 | 1000 | 3000 |
| fast | 924 | 14 | 333 | 1000 |
| ultra | 994 | 2 | 48 | 143 |

`CPState::Update` (`0x211d80`) advances both generators, consumes one due pulse
to set dirty flags, and dispatches the panel update. In
`cWinPeople::UpdateViewFromCPState` motive batches are gated by `dirty & 0x9`.
Once gated, it pumps all 16 widgets once and then repeats whole batches while
`ShouldPulse(CPState+0x1f0)` drains any remaining queued pulses
(`0x28b08c..d4`). A non-pulse dirty update therefore guarantees one batch; a
large queued interval can produce several batches in one UI update.

The timestamp is `TimeBase_Sims::Now`. This decode proves its integer use and
the periods above; whether that global timebase is frozen by every possible
pause/modal state is outside this focused law.

## 3. `cWinDeltaMeter`: exact five-slot state machine

Relevant methods:

| method | address |
|---|---:|
| `ResetHistory` | `0x274730` |
| `DrawArrow` | `0x274970` |
| `TriggerDeltas` | `0x274ab0` |
| `PumpSample` | `0x2751d0` |
| `SetAverageHistory` | `0x275270` |
| `TSPaint` | `0x275400` |
| `GetMeterWidth` | `0x275ab0` |
| `SetTopLefts` | `0x275af0` |
| constructor | `0x275da0` |

State begins at:

| offset | field |
|---:|---|
| `+0xd4` | active byte |
| `+0xd5` | stop-after-fades byte |
| `+0xd6` | signed current delta byte |
| `+0xd8/+0xdc` | left lane point |
| `+0xe0/+0xe4` | right lane point |
| `+0xe8` | `cAverageHistory*` |
| `+0xec` | five `cColorFader` records, stride `0x34` |

Each fader contains current RGB vector at record `+0`, target at `+0x0c`, RGB
difference at `+0x18`, and a `RampGenerator` at `+0x24`.

The constructor zeros active/stop/delta/history. `SetAverageHistory` stores the
pointer (or null), resets active and delta, and resets all five faders to hidden
RGB `#000052` with a zero-duration 0-to-1 ramp. Binding non-null clears the stop
byte; binding null sets it. It never calls `cAverageHistory::Reset`.

`ResetHistory` does reset a non-null history, then resets all of the same meter
state with stop zero. This method is never reached with a non-null pointer in
the owned panel code.

### Trigger and transition law

Let `old` be the prior signed delta byte and `new` be
`history.GetAverageDelta()`:

* If no history is bound, return.
* For nonzero `new`, set active, store `new`, and invalidate.
* For zero `new`, set stop-after-fades but leave the old delta byte unchanged.
* Treat zero-to/from-nonzero and opposite signs as a sign transition.
* If magnitude and sign state did not change, leave all ramps untouched.

The primary target is green `#00FF00` for positive and red `#FF0000` for
negative. All ordinary transitions last exactly 500 `TimeBase_Sims` units:

| transition | affected records | exact action |
|---|---|---|
| same sign, magnitude rises | `max(abs(old)-1,0) .. abs(new)-1` | current interpolated RGB -> primary over 500 |
| same sign, magnitude falls | `abs(old)-1` down through `abs(new)` | current interpolated RGB -> `#000052` over 500 |
| zero/sign crossing | `0 .. abs(new)-1` | force current to `#000052`, then fade to primary over 500 |
| zero/sign crossing | `abs(new) .. 4` | force hidden with duration zero |

The overlap at `abs(old)-1` when magnitude rises is native: the existing edge
arrow is deliberately re-brightened along with newly exposed records.
Transition to zero hides all five immediately; `TSPaint` later clears the
active/stop/delta bytes once all five zero-duration ramps report done.

There is also a literal native edge case: a later nonzero trigger does not clear
an already-set stop byte. Multiple catch-up samples in one UI update can
therefore carry the stop state across a zero-to-nonzero sequence before paint.
Do not silently normalize this state if strict parity is required.

### Paint-time colour decay

For each active paint, all five records are evaluated and drawn. Each RGB
channel is `round(current + difference*rampValue)` using `+0.5` then truncation.
When a computed colour reaches an exact primary endpoint, that same paint
re-arms a second 500-unit ramp:

```text
#FF0000 -> #D10000
#00FF00 -> #00CA39
```

The just-computed primary is still used for that draw. The muted endpoint then
remains stable until another magnitude/sign transition changes that record.
While any ramp is unfinished, the meter invalidates itself. When all five are
finished it does not continuously invalidate; if the stop byte is set, it also
clears active/delta/stop.

`RampGenerator::SetupConstantTimeRamp` (`0x14e890`) stores a linear slope and
an end timestamp `now+duration`; `GetVal` (`0x14e930`) returns the exact target
at or after that timestamp. Duration-zero hidden resets are safe because the
target branch is taken before the unused infinite/NaN slope can matter.

## 4. Five-arrow masks and exact positions

`DrawArrow` writes a procedural 3-by-5 mask into the locked 16-bit surface:

```text
left       right
..#        #..
.##        ##.
###        ###
.##        ##.
..#        #..
```

Record zero is nearest the center; each later record moves four pixels outward.
All records share one y coordinate.

`GetMeterWidth` is not an instance field and does not use the constructor
argument. Its whole body at `0x275ab0` is `li r3, 0x14; blr`: width is always
**20**. Both panel constructors happen to pass integer 10, but the
`cWinDeltaMeter` constructor ignores it. This corrects R184's earlier first-left
x=6 interpretation.

| panel | lane points | left record x positions | right record x positions | y |
|---|---|---|---|---:|
| motive (100x20) | `(0,12)`, `(81,12)` | `16,12,8,4,0` | `81,85,89,93,97` | 12 |
| relationship card | `(2,58)`, `(22,58)` | `18,14,10,6,2` | `22,26,30,34,38` | 58 |

The motive points come from `__sinit_:WinMotive_cpp`'s `(20,12)` origin and
`Init`'s `x-20` / `x+61` construction. Relationship uses the static `(2,52)`
point plus six y pixels for both lanes and plus 20 x pixels for the right lane.

## 5. Why relationship arrows are dormant

This is closed by several independent static facts:

1. The exhaustive direct-call census for `cAverageHistory` constructor has
   exactly two calls, both the motive vector-growth sites (`0x27d9d4` and
   `0x27dc4c`). Relationship code never constructs a history.
2. `cWinDeltaMeter` constructor explicitly stores null at `+0xe8`.
3. `cWinRelationship::Init` (`0x29bbb0`) allocates/initializes the meter and sets
   its lane points, but neither writes `+0xe8` nor calls `SetAverageHistory`.
4. The exhaustive direct-call census for nonvirtual `SetAverageHistory` has
   exactly two calls, both in `cWinMotive::SetPerson` (`0x27dc98/a8`). The
   method has no PEF function descriptor and is absent from the meter vtable,
   so there is no virtual-call path hiding another panel binding.
5. `cWinRelationship::SetRelationship` does call `ResetHistory` when endpoints
   change and `PumpSample` at `0x29a70c`, but both methods test `meter+0xe8` and
   return before clearing, appending, or triggering when it is null.

Consequently the relationship card's arrow geometry and draw code are reachable
only if some nonexistent binding were added; the shipped Complete Collection
panel never activates them. A parity port should retain its two relationship
score bars and markers, but must not invent relationship trend arrows from
those values.

## 6. Exact implementation target versus residuals

Exact enough to implement from this decode:

* motive raw rounding, ten-record window, rolling-mean comparison, clamp, sign,
  and arrow count;
* pulse-generator periods, catch-up behavior, and history persistence;
* all five masks, positions, RGB endpoints, 500-unit fades, re-brighten law,
  stop behavior, and paint invalidation;
* absence of relationship trend arrows in this owned build.

Deliberately still not asserted beyond the executable evidence:

* the actual 16-bit framebuffer word produced for each RGB request depends on
  the active surface format/colour manager; the requested RGB values are exact;
* `TimeBase_Sims::Now` use is exact, but this note does not generalize how every
  modal/pause path advances that global timebase;
* house-list histories are provably positional; whether normal gameplay keeps
  that list stable across every roster mutation belongs to `PersonFinder`, not
  this meter law.

## 7. Port integration boundary

`UIOriginalMotiveGauge` in `UI/Controls/UIValueBar.cs` now contains the literal
average-history and five-fader meter laws. A weak bank keyed by the current
`TS1GameScreen` gives the histories the lifetime of native `cWinPeople`, rather
than the shorter lifetime of Simitone's replaceable Mood subpanel. Its
game-thread hook continues pumping while another standard People panel is
visible. Replacing the screen's VM (loading a different lot) clears this bank,
matching destruction of the old lot's native widgets. Simitone speed
multipliers `1/3/10` bridge to the shipped native speed
codes `760/924/994`, hence periods `1000/333/48` milliseconds.

The closest port-side counterpart of the native positional house list is
`VMContext.ObjectQueries.Avatars` in its existing order. This deliberately does
not key histories by persistent ID and does not shrink the vectors. The native
positional law is exact; equality between every possible `PersonFinder` roster
ordering and every possible port `ObjectQueries` ordering is not independently
proven. Likewise, the bridge suppresses UI-wall-clock accumulation while the
port VM multiplier is nonpositive, because the port UI clock continues in
pause/build-buy; section 2's residual about every native TimeBase/modal state
still applies.

No relationship-card arrows were added. Section 5 proves that doing so would
create behavior absent from the owned Complete Collection executable.
