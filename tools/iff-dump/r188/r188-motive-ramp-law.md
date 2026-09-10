# R188 — motive bar colour state and People mood-band law

This note closes the remaining desktop colour-state question for
`cWinMotive`, and the adjacent `cWinPeople` overall-mood arrow question, from
the owned **The Sims Complete Collection** Macintosh executable.

| evidence | value |
|---|---|
| executable | `game-data/The Sims/The Sims Complete` |
| size | 6,486,820 bytes |
| SHA-256 | `33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f` |
| address convention | raw executable file offsets |

The key correction to R184 is that ordinary motive updates do **not** animate
between fixed mauve and green colours. Both colours are functions of the
current motive, and both normal `SetVal` branches install them immediately with
zero-duration ramps. `TSPaint` does contain one 500 ms pure-green sentinel
fade, but no ordinary method in the owned `cWinMotive` body creates that
sentinel.

## 1. Native objects and calls

| code | address |
|---|---:|
| `RampGenerator::SetupConstantTimeRamp` | `0x14e890` |
| `RampGenerator::GetVal` | `0x14e930` |
| `RampGenerator::IsDone` | `0x275980` |
| `cWinMotive::Pump` | `0x27d990` |
| `cWinMotive::GetGreenMagnitude` | `0x27dd00` |
| `cWinMotive::ResetHistory` | `0x27dd90` |
| `cWinMotive::SetVal` | `0x27e200` |
| `cWinMotive::TSPaint` | `0x27ea50` |
| `blend_sims` | `0x27f3c0` |
| `cWinPeople::TSPaint` | `0x28d510` |

The motive colour constant pool is at file `0x5a4478`. Relevant floats are
`0.5, 0, 100, 1, 1/255, 255, 95, -95, 64, 145, 93, -93, -38,
-64, 109, 60, 200`. These are direct operands of the calculations below, not
endpoint interpretations inferred from screenshots.

The two `cWinMotive` colour records are:

| record | start vec3 | target vec3 | difference | ramp |
|---|---:|---:|---:|---:|
| remainder | `+0x1b8` | `+0x1c4` | `+0x1d0` | `+0x1dc` |
| active | `+0x1ec` | `+0x1f8` | `+0x204` | `+0x210` |

`TSPaint` evaluates `start + difference * ramp.GetVal(Now())`, adds `0.5`
to each positive channel, truncates, and converts the resulting RGB through
the destination surface. It paints active first over the left `fill` pixels,
remainder over the rest, and then black over the final active column when
`fill > 1`. This retains R184's exact 60x5 geometry.

## 2. Exact settled colour law

Define:

```text
g = clamp((rawMotive + 50) / 100.0f, 0, 1)
q(x) = byte(trunc(0.5f + 255.0f * x))
blend(a,b,q) = byte(trunc(0.5f + a + (b-a) * (q / 255.0f)))
```

The byte quantization is material: native quantizes `g` before blending every
channel. The settled colours are:

```text
remainder = blendRGB(#5D405F, #00D100, q(1-g))
active    = blendRGB(#405D5F, #00CA39, q(g))
```

Representative exact results are:

| raw motive | active | remainder |
|---:|---:|---:|
| `<= -50` | `#405D5F` | `#00D100` |
| `-49` | `#3F5E5F` | `#01CF01` |
| `-25` | `#307855` | `#17AD18` |
| `0` | `#20944C` | `#2E892F` |
| `25` | `#10AF43` | `#466447` |
| `49` | `#01C939` | `#5C425E` |
| `>= 50` | `#00CA39` | `#5D405F` |

`GetGreenMagnitude` at `0x27dd00..0x27dd58` proves the `[-50,+50]` colour
clamp independently of the fill geometry's `[-100,+100]` range.

`ResetHistory` computes both settled colours, copies each colour into both its
start and target vectors, computes a zero difference, and calls
`SetupConstantTimeRamp(0,1,0,Now())` for both records.

`SetVal` returns immediately when the integer value is unchanged. On a change
it invalidates, stores the new value, and branches on the sign of `new-old`.
The two large branches (`0x27e244..0x27e628` and `0x27e62c..0x27ea10`) are
register/stack-renamed duplicates: both compute the same two settled colours,
copy current to target, and arm both ramps for duration `0`. There is no
rise/fall colour animation in this owned build.

`Pump` rounds each motive float away from zero for history collection. For the
selected person it independently rounds the current motive float and calls
`SetVal` (`0x27da7c..0x27dae0`), then triggers the already-documented delta
meter. Because normal bar ramps have duration zero, pump cadence cannot expose
an intermediate bar colour.

## 3. The two TSPaint sentinels

After painting the evaluated remainder colour, `TSPaint` checks that its ramp
is done and the unpacked RGB is exactly `#FF0000`
(`0x27ed04..0x27ed20`). It then computes the settled remainder twice, installs
identical start/target vectors, and arms duration `0`. The already-evaluated
red remains visible for that paint; the next paint is settled.

After painting the evaluated active colour, it checks for exact `#00FF00`
(`0x27f024..0x27f03c`). It installs pure green as start, the settled active
colour as target, and calls `SetupConstantTimeRamp(0,1,500,Now())`
(`0x27f150..0x27f204`). Thus a raw `>=50` probe yields:

| elapsed after re-arm | active RGB |
|---:|---:|
| `0 ms` | `#00FF00` |
| `250 ms` | `#00E51C` |
| `500 ms` | `#00CA39` |

All methods that can write these fields in the symbol-bounded `cWinMotive`
body were inspected: constructor, `Init`, `ResetHistory`, `SetVal`, and
`TSPaint`. Constructor initializes the two ramp generators; `Init` calls
`ResetHistory`; normal/reset colours never equal pure red or pure green.
Therefore both exact-primary checks are defensive/dormant under ordinary owned
execution. The port retains and separately probes them without manufacturing a
normal-game pulse.

The 250 ms blue channel is `0x1C`, not the algebraic half-up `0x1D`:
`RampGenerator::GetVal` uses the fused single-precision `fnmsubs` instruction,
so its midpoint is the preceding float below `0.5`; the later vec multiply and
add are separate single-precision operations. The port and probe preserve that
instruction-level rounding distinction.

## 4. People overall-mood arrows: no history

R144 called this an overall-mood “delta,” and the port implemented a two-sample
queue on an arbitrary 240-frame cadence. The executable has no such state in
this path.

At `0x28d6c4..0x28d714`, `cWinPeople::TSPaint` reads the selected person's
current motives float at offset `+0x0c` and rounds it away from zero. With no
selected person it substitutes zero. It then evaluates the constants at
`0x5a45ec + {0x1c,0x20,0x24,0x28}` = `{11,100,201,5}`:

```text
m = roundAway(overallMood)
trend = clamp(roundAway(((100 + m) * 11 / 201) - 5), -5, +5)
```

The sign selects red/green art and the magnitude reveals five pixels per unit,
as already recorded in R144. This is an absolute current-mood band, recomputed
on every paint; it has no old value, sampling interval, or cross-person
baseline.

## 5. Port and verification

Production changes are confined to:

* `Client/Simitone/Simitone.Client/UI/Controls/UIValueBar.cs`: exact quantized
  settled colours, zero-duration normal state, and the two paint sentinels;
* `Client/Simitone/Simitone.Client/UI/Panels/UIOriginalPeopleChrome.cs`: exact
  direct mood-band formula;
* `Client/Simitone/Simitone.Client/AutotestRunner.cs`: `uilive` render colours,
  pure colour tables, sentinel timing, and mood-band probes.

The existing delta-arrow state was not changed. The focused assertions pin the
three settled colour anchors, actual pixels at empty/partial/full fill, black
endpoint geometry, the red one-paint repair, green `0/250/500 ms` samples, and
mood-band endpoints/interiors.

Initial compile verification command:

```sh
dotnet build Client/Simitone/Simitone.Client/Simitone.Client.csproj \
  --no-restore -v:q -p:WarningLevel=0
```

Result: **0 errors, 42 existing NU1701 warnings**.

The current shared-tree snapshot was then published and synchronized into the
packaged application with:

```sh
NUGET_PACKAGES="$PWD/.nuget-packages" DOTNET_CLI_HOME="$PWD/.dotnet-cli" \
  dotnet publish Client/Simitone/Simitone.Desktop/Simitone.Desktop.csproj \
  -c Release -r osx-arm64 --self-contained true -o "$PWD/publish/osx-arm64" \
  /p:TreatWarningsAsErrors=false /p:WarningsAsErrors="" -p:NoWarn=NU1605
./packmac.sh arm64
cmp -s publish/osx-arm64/Simitone.Client.dll \
  "dist/The Sims-arm64.app/Contents/MacOS/Simitone.Client.dll"
```

Publish and package assembly exited zero. The `cmp` passed; both client DLLs
had SHA-256
`7e21f4f0916c9874ffa88bcecc9977ba8df200b01afb6bf0e1e4d11aa05f8fc9`.
After regenerating the two volatile corpus probes, the packaged binary passed:

* `corpus,uilive,uijob,uisurvey`: **8 passed, 0 failed**, process exit 0;
* `corpus,uilive,uijob,uirel,uihouse,uiinterest,uivis`: **11 passed,
  0 failed**, process exit 0.

The focused survey produced the fresh 1024x768 Needs capture at
`/Users/nathannoom/Documents/Simitone/uisurvey-live.png` (836,745 bytes,
2026-08-31 08:30:34 CDT), SHA-256
`c06178258b92855e2179d48adfef965b25b24b259c265701fa1018f652a89558`.
These results describe that exact packaged snapshot. Additional shared-tree
tab/tooltip work was expected afterward, so the owning integration pass must
perform the final authoritative rebuild and rerun.
