# R219 — the natural relationship→death runtime trace (deathtrace)

**The last full non-expansion `[GAP]` in PARITY.md closed.** The ledger row said:
"Residual: the natural-death decision ids (8299/8340/8305/8306/8641) were NOT observed
in live frames during R70–R81 soaks (R77 probe: motives collapsed to 3, decision ids
not seen in-window). The decision→ghost trace is runnable; it has not been caught
in-window yet." This round caught it.

## Why R77 never fired (root cause)

R77 collapsed the decaying motives to **+3**. The actual engine gate — decoded this
round from the staged `PersonGlobals.iff` BHAV 8299 `check motive failure` — is a
literal comparison against the motive floor:

```
8299 ins0  StackObjMotives[Energy]  < Lit[-98] ? t=13 : ->ins1
8299 ins1  StackObjMotives[Hunger]  < Lit[-98] ? t=15 : ->ins7
8299 ins7  StackObjMotives[Bladder] < Lit[-98] ? t=17 : RETURN_TRUE
```

(operand grammar = FreeSO `VMExpressionOperand`: `LhsData:i16 RhsData:i16
IsSigned:u8 Operator:u8 LhsOwner:u8 RhsOwner:u8`, LE; motives range -100..100.)
+3 never crosses -98, so the failure branch was never entered in any R70–R81 soak.

## The decoded law (tools in this dir)

- `decode_bhav.py` — TS1 BHAV pretty-printer using the FreeSO expression grammar
  + private/global call labels. Pointer semantics per `VMThread.MoveToInstruction`:
  254=RETURN_TRUE, 255=RETURN_FALSE, 253=follow the other (non-253) path.
- `PersonGlobals.iff` / `Global.iff` are **not committed** (EA-owned bytes; extract
  on demand from `GameData/Global/Global.far` — 16-byte-entry V3 FAR records, LE,
  same reader shape as `_farnames_scan.py`; the manifest is at bytes 12..16).
- Opcodes ≥ 256 are **Global.iff routine calls** (`VMThread.ExecuteInstruction`:
  `opcode >= 256 -> ExecuteSubRoutine(frame.Global.Resource.GetRoutine(opcode))`),
  not primitives — this is how 393/316/280 resolve.
- Tuning scope: `tableID = data>>7, keyID = data&0x7F`; mode-2 tables add offset 256
  (`VMMemory.GetTuningVariable` + `TableIDOffsets`). `Tuning[16896/16897/16903]` =
  Global.iff **BCON 260 'Person Types'** = 0 / 1 / 7 (0=normal person, 7=clone);
  `PersonData[32]` is the person-type switch read by `person main` ins3/ins47.

The full chain (every call site verified by instruction index):

```
person main (8193, engine-dispatched — zero IFF callers, R74/R78 census)
  ins4 -> person main loop (8283)
    ins5 -> check motive failure (8299)          [every main-loop pass]
      gate: Energy/Hunger/Bladder < -98
      energy  -> PersonData[71]:=1 -> failure sleep (8197)     [pass-out]
      hunger  -> PersonData[71]:=1 -> get me out of pool (8310)
                 -> balloon(3) -> failure hunger (8198)
      bladder -> PersonData[71]:=1 -> [in pool? PersonData[64]==1]
                   yes: swim bladder failure (8309)
                   no:  failure bladder (8199)
      hunger path: 8198 'stand up'(8200) -> idle(200) -> sound 0x13d
                   -> ins4 Global 393 'kill person for good'
      Global 393:  balloon(dead) -> Global 316 'do grim reaper'
                   -> create urn/tombstone  GUID 0x5A6FA529   (ins8)
                   -> create grim reaper    GUID 0x4B15D4B0   (ins26)
                   -> MyPersonData[68]:=1 (dead), MyObject[34]:=1
                   -> remove-object-instance
      Global 316:  reaper gate Global[20] flag 1; zeroes personality
                   PersonData[2..7]; Hunger/Energy:=0; Hygiene/Comfort:=-100
      Global 280 'idle' = one `sleep` primitive whose operand selects
                   Args[0] (the caller's Param[0] — 200 from 8198 ins3):
                   decrements per tick, wake at <= -1 OR Thread.Interrupt
after death:   next person main pass ins11 PersonData[68]==1
                   -> motives reset to 75 (ins43-46)
                   -> Ghost - Main Loop (8641) on the dead avatar
```

## The probe (`deathtrace`, opt-in like carreturn/schoolreturn)

`AutotestRunner` state 4 (`StateDeathTrace`): collapses avatar0's **Hunger only**
to -100 at soak minute 2 (single-variable starvation trace), then dense per-frame
stack sampling for the canon death ids, per-minute polls of `PersonData[68]/[71]/[32]`,
`entity.Dead`, hunger, and a `VMCreateObjectInstance.ObjectCreated` observer filtered
to the two death GUIDs + any create framed by routine 393. PASS law (never invented):
**failure handler ran** (8198/8197/8199/8309 — only reachable past the -98 gate)
**AND kill ran** (Global 393/316 frame or death-artifact create) **AND dead state
landed** (PersonData[68]==1 / entity.Dead / entity removed). The ghost loop (8641)
is logged evidence, not a pass condition — it is night-gated in the original.

## Observed (run 1 user-present; runs 3/5 untouched; run 6 untouched + dialog stand-in)

Every run: the path executed **at the exact decoded instruction sites**:

```
6:32 collapse (avatar0 obj16, age 9 — the fixture child, PersonData[32]=0)
6:36 8299 first seen: stack=[4096:1, 8193:4, 8283:5, 8299:4, 8198:2]   <- ins5->8299, ins4->8198
     PersonData[71] 0->1 (failure code)
6:46 Global 393:     stack=[4096:1, 8193:4, 8283:5, 8299:4, 8198:4, 393:23]  <- 8198 ins4 -> 393
6:47 Global 316:     stack=[..., 8198:4, 393:1, 316:6, 280:0]          <- 393 ins1 -> 316, idling via 280
7:26  create-event User00011.iff:393:8 created UrnStone.iff guid=0x5A6FA529 base=218 caller=16
7:27  PersonData[68]=1 + entity.Dead=true
SUMMARY: handlerSeen=True killSeen=True deadConfirmed=True  -> PASS
```

(`run-6 timings shown; runs 1/3 match within a minute.`) `User00011.iff:393:8` is the
global 393 routine executing under the per-house patched-globals scope resource —
the create site matches the original IFF byte-for-byte (ins8, GUID 5A6FA529).

### The grace period

The IFF stages a nominal 200-tick idle (8198 ins3 -> Global 280 with Param[0]=200)
between the failure handler and the kill. **Observed: ~10 sim-minutes in all three
runs** (6:36->6:46, 6:37->6:47, 6:42->6:51). The engine's `VMSleep` decrements
`Args[0]` by the ticks elapsed since the thread's last idle start and wakes on
`Thread.Interrupt`, so a thread with idle history spends far less than the nominal
budget — the engine's own semantics, identical with and without a player present.

### The blocking modals (the "hang" that wasn't)

Untouched runs (3/5) froze at 7:00 with the game loop still turning — the VM clock
stopped, which only looked like a hang because the probe logs on minute-changes.
Two TS1 blocking dialogs latch during the sequence and need a player click:

1. **7:00 — the morning carpool dialog** (`CarPortal.iff` obj8, the known r157
   4125 dialog family) — coincidence of timing, unrelated to death.
2. **7:26 — the death notice itself** (`UrnStone.iff` obj218, the just-created
   tombstone's own dialog), plus a follow-up household dialog (User00010.iff).

Run 1 completed only because the player (a human, this time) clicked them away.
The probe now acts as the player: `StateDeathTrace` names the latch owner and
releases it (the same release the LOT-READY site performs on stale latches),
re-releasing every 10s if the tree raises another. With that, the untouched run
completes end-to-end in ~1.1 real minutes and PASSES.

## Round result

- Targeted soak: `CHECKNAME,deathtrace,corpus` — **passed=6 failed=0**, exit 0,
  untouched (run 6, `targeted-soak.log` — the passing untouched run with the dialog
  stand-in; run 1 `targeted-soak-run1-user-present.log` kept for the record, runs 3/5
  `targeted-soak-run3-hang.log` / `run5` document the pre-fix dialog-latch freeze).
  The R77-era `deathchain` static canon stays in the default suite unchanged.
- Full default gate: see `gate-run.log` — 127 checks, 0 failed (deathtrace is
  opt-in, so the default count is unchanged).
- PARITY `[GAP] relationship→death runtime end-to-end` closes: the decision chain
  (8299 gate < -98 -> 8198 -> Global 393 -> 316) observed live at exact instruction
  sites, the tombstone create (UrnStone.iff 5A6FA529 at 393:8) observed, the dead
  flags (PersonData[68]=1, entity.Dead) observed. Residual narrowed to: the ghost's
  night-haunt loop (8641) not observed in the post-death window — it is hour-gated
  in the original; and the blocking death-notice dialog is released by the probe
  rather than clicked (the original requires a player click — the probe logs the
  latch owner: UrnStone.iff).
