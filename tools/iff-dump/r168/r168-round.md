# R168 — Generic Sims Call 14: house radio station

## Result

R168 closes TS1 Generic Sims Call mode 14,
`HouseRadioStationEqualsTemp0`, with original-engine, complete owned-corpus,
actual-dispatch, and packaged evidence. The enum name is misleading: the
original case does not compare anything. It unconditionally assigns signed
Temp0 to global 31 and returns true, without inspecting Stack Object.

FreeSO's existing production implementation was already exact. The only
nested-engine change is an explanatory comment at that case. R168 adds
deterministic evidence tools, a standalone production-dispatch probe, and a
permanent packaged regression check. It changes no UI or gameplay behavior.

## Original-engine recovery

`r168-original-engine-decode.py` reads the owner's original Complete
Collection PPC PEF in place and emits only addresses, hashes, decoded law, and
selected instruction words. No executable bytes or proprietary asset payload
are copied into the repository.

- original engine SHA-256:
  `33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f`;
- packed data at file `0x5c22f0`, length `0x6d834`, unpacked to `0x7bf80`;
- unpacked-data SHA-256:
  `f740c1dfa1dea8187b12ebf222ca1a39223365d26ad5d2f2e353d5efba247be3`;
- `TryGenericSimCall` dispatch entry 14 resolves to virtual `0xe98ac`, file
  `0xf273c`; entry 15 begins at file `0xf2754`;
- mode-14 code `[0xf273c,0xf2754)` SHA-256:
  `c6a1b6222721ef0a9f8d090fb71c5e4ae24471f6214d3804d35b320577424a44`;
- `cSimulator::SetGlobal` `[0x138c20,0x138c34)` SHA-256:
  `0f5525dcf5c237f281b90a7cb88ea76e1e468711f4b0c8159ea501a30faf6e6e`.

The dispatch prologue, receiver and module constructor stores, case body,
`SetGlobal`, and common return establish the exact law:

1. The receiver is the current `cXObject`; the StackElem argument is unused.
2. Load ObjectModule from receiver `+0xdc`, then cSimulator from module
   `+0x80`.
3. Load signed Temp0 from receiver `+0x3a`.
4. Call `cSimulator::SetGlobal(index=31, value=Temp0)`, which stores at
   simulator `+0x10 + 2*index`.
5. Take the generic call's common true return.

There is no read of global 31, comparison, conditional result, or Stack Object
dependency. The deterministic report SHA-256 is
`c3a8e31f8a90bacae3585399e71166c08b1df6609a3d5d842f63828e88ce3aa7`.
It is byte-identical under normal and optimized Python; the script passes
`ruff check` and Python compilation.

## Owned-corpus callers

`r168-generic-call14-scan.py` uses the production FAR/BHAV reader and a strict
IFF walker. It scans loose IFF/FAM files, top-level FAR members, and one nested
FAR level; every IFF must terminate exactly, and the scan asserts that no
deeper nested FAR exists.

Across 8,871 files, 2,238 IFFs, and 28,246 BHAV chunks, it finds:

- 18 mode-14 calls in 13 IFFs;
- 11 unique call-site signatures and 11 unique routine payload hashes;
- the exact operand `0e 00 00 00 00 00 00 00` at every call;
- false pointer 253 at every call, with only the true continuation used;
- 22 exact incoming Temp0 assignment paths: 9 parameter sites, 4 literal-2
  sites, and 5 tuning sites.

The callers cover stereos, wall stereos, speakers, the DJ booth, jukebox, and
pianos. Every path initializes Temp0 before mode 14; none uses it as a
comparison. The deterministic report SHA-256 is
`e611272da1d0a45e66074d65b85b6104f7f54129650092a0ad4b0dd3af05635e`.
It also reproduces byte-for-byte under normal and optimized Python.

## Port implementation

Nested engine commit `a428effe` documents the already-correct case in
`VMGenericTS1Call.cs`:

```csharp
context.VM.SetGlobalValue(31, context.Thread.TempRegisters[0]);
return VMPrimitiveExitCode.GOTO_TRUE;
```

There is no behavior delta. The permanent `genericcall14` packaged check
instead guards the complete path:

- mounted `Stereos.iff` BHAV 4118 pins Param0-to-Temp0 at instruction 1 and
  mode 14 at instruction 8;
- mounted `piano.iff` BHAV 4117 pins literal-2-to-Temp0 at instruction 24 and
  mode 14 at instruction 25;
- raw operand bytes are decoded through `VMGenericTS1CallOperand.Read`;
- opcode 1 must remain registered as `generic_sims_call` with the production
  operand model and handler;
- eight signed values from -32768 through 32767 dispatch through that handler;
- only global 31 may change, all Temp registers must be preserved, alternating
  null/nonzero Stack Object fixtures must be irrelevant, and every exit must
  be `GOTO_TRUE`;
- globals and the complete Temp array are restored in a `finally` block.

## Verification

### Standalone actual-dispatch probe

`tools/generic_ts1_radio_probe` initializes TS1 primitive registration,
decodes raw operand byte `0x0e`, and invokes the registered production handler.
It uses eight signed boundary/sample values and alternates null with ObjectID
321 as the Stack Object decoy.

```sh
dotnet run --project \
  tools/generic_ts1_radio_probe/generic_ts1_radio_probe.csproj -c Release
```

```text
PASS: Generic Sims Call mode 14 actual dispatch verified for 8 signed Temp0
values through raw operand 0e and the registered opcode-1 handler; exact
Global[31] overwrite, full Temp preservation, null/nonzero Stack Object
independence, other-global preservation, and true exit.
```

### Packaged integration

The Release client builds with zero errors. A self-contained `osx-arm64`
publish and `packmac.sh arm64` produce the tested 2.1 GB application bundle.

```sh
NSUnbufferedIO=YES 'dist/The Sims-arm64.app/Contents/MacOS/TheSims' \
  -ApplePersistenceIgnoreState YES \
  "-path$PWD/game-data/The Sims" -autotest 5 \
  -autotest-opts lot,corpus,genericcall14 -autotest-timeout 1800000
```

`r168-genericcall14-run2.log` is a clean 224-line focused segment: **7 passed,
0 failed, 0 skipped**, clean Run/Dispose; SHA-256
`b6e0645b958830c367c62fb1528d230b3ee5aa898342aa560e4f07481b80540f`.

The final exact-source packaged default gate is **106 passed, 0 failed, 0
skipped**, wrapper exit 0, clean Run/Dispose, and contains no `SimAnticsExc`,
`bad-routine-frame`, or failed check. Its natural soak reaches 10:15, executes
CarPortal 4105 and CarJunk 4121, and creates then removes the original
`CarJunk.iff` multitile group. The 1,609-line `r168-gate-run3.log` SHA-256 is
`6a40e6e61dafc023b8bb71dfac324d1089dd88fd62734370937e5ecf740b4776`.

An initial pre-hardening default attempt passed mode 14 but finished 105/106
when the existing mood sampler landed one decay tick outside its +/-3
boundary (`storedMood=72`, `computed=76`). An unchanged rerun completed
106/106. The dispatch and evidence checks were then independently reviewed and
hardened to cover raw operand decoding, opcode registration, a nonzero Stack
Object, complete Temp restoration, dispatch provenance, and strict IFF
termination. The final run above is from that hardened exact source bundle.

## Boundaries and next target

R168 closes mode 14 without a semantic residual: all known owned callers and
the full signed input domain behavior are covered by the recovered law and
production dispatch tests. This does not claim broader parity for unrelated
Generic Sims Call modes.

No original asset, executable slice, or machine-code payload was committed.
No UI layout, spacing, sizing, hit target, gesture, animation, control
placement, or presentation changed.

The most efficient adjacent R169 target is mode 15,
`MyRoutingFootprintEqualsTemp0`. Its PPC case begins at file `0xf2754`, while
the port currently leaves it in the unimplemented/default path. Decode the
exact read/compare/exit law, census every owned caller, implement only the
proven behavior, and repeat the raw-dispatch plus packaged-gate discipline.
