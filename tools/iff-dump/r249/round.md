# R249: free-will winner priority — the native selection law decoded, implemented, and traced live

The last open live-play residual from the free-will family is closed: the port
now selects autonomous winners by the original's own law, and a new opt-in
`freewillwin` check observes real multi-candidate decisions in the live VM and
proves the winner matches a pure-law predictor fed only the captured
candidate set and RNG seed. Getting there surfaced a chain of genuine
discoveries — including two decoding errors that only adversarial
cross-examination caught, a broken-sort quirk in the original engine itself,
and a port gender-encoding divergence.

## Honesty correction to the standing record

The HANDOFF "honest residuals" section (R168-era) lists the natural death
chain and chance cards as open; the PARITY ledger supersedes it: the death
chain and ghost were traced live end-to-end in R219/R220, and chance cards in
R222. This round's roadmap item therefore narrowed to the one genuinely open
live-play gap — free-will winner priority — and the stale HANDOFF section is
corrected in this round's documentation commit.

## Decode phase (two passes, one skeptic, one maintainer adjudication)

[`r249-freewill/decode.md`](../r249-freewill/decode.md) located the native
machinery: the "Find Best Action" primitive (`TryFindBestAction` 0x109860,
dispatched from `TryElement` op 3), the gatherer
(`AppendInteractionsForAuto` 0x105900 + `TestInteraction` 0x1062d0), scoring
(`GetCurrentScore` 0x9fc20 / `GetInteractionScore` 0x9f9d0), the ordering
"qsort", winner insertion (`TryGosubFoundAction` → `SetCurrentAction`), and
the free-will byte (data 0x45f1c, mirrored into SimAntics global 30 by
`SetFreeWill` 0xc9d40).

The skeptic re-derived everything from raw words and caught two critical
errors — capstone silently drops the `fcmpo` words, which is exactly where
the first decode misread the comparator and the cutoff. The follow-up decode
([`r249-freewill-cfg/decode.md`](../r249-freewill-cfg/decode.md)) resolved the
constants (the CFG object is a code-section literal pool; runtime values come
from the GLOB's FCNS FloatConstants: sitting cutoff 1e-6, random selection
count 4, attenuation sets) and made the round's most surprising finding: the
native "sort" at 0x59a370 is a **heapsort whose one-sided comparator never
swaps strictly-different scores — its net effect is exactly a
rotate-left-by-one** (proven by a faithful transpile validated against a
normal comparator). Score's only live role in winner choice is the accept
cutoff; the draw picks uniformly among the first K entries of the rotated
gather order (K = max(1, count − trunc(attr18/2000))).

The maintainer adjudicated the final load-bearing polarity from the raw words
at 0x109ec0–0x109f14: **accept iff score ≥ cutoff** (FCNS 1e-6) when the sim
is engaged (attribute slot 0 ≠ 0); standing sims accept unconditionally —
resolving the second branch-polarity disagreement between the agents. All
adjudications are recorded in
[`skeptic-corrections.md`](../r249-freewill/skeptic-corrections.md) so a
future round cannot "fix" back.

## Implementation

Engine (nested `FreeSO`): `VMFindBestAction` now dispatches TS1 (opcode 3) to
`ExecuteTS1Native` — the TSO path (opcode 65) is byte-identical to the old
body. The native law, step-mapped with decode addresses in-code: always-apply
gates, the free-will-gated prelude, backward-TTAB gathering with the
target-conditioned actor gate and the native gender-class adapter
(word 65 = sex bit + species bits — the port's own encoding diverged), the
stratum filter, mean-of-9-curves scoring (GLOB STR# 501/503, MOTIVETAB
recovered from the binary), the H′==0 drop, the faithful heapsort transpile,
the uniform top-K draw with seed capture, the adjudicated cutoff, and
winner insertion. `VM.FreeWillEnabled` mirrors into global 30 (and
re-applies after a save restore). Supporting: FCNS structured codec +
`WorldGlobalProvider.AutonomyFCNS`, `CheckTS1Action` optional args, and the
`TS1DecisionObserved`/`LastDecision` observation seam (exception-isolated,
read-only).

Client (parent): new opt-in `freewillwin` battery — motive scenario pinning
(Fun=0 competition), the seam observer, a **pure-law predictor** (heapsort
transpile + xorshift seed replay + cutoff law) fed only captured inputs,
queue cross-check, free-will-OFF control, and full state restore with a
restore-proof assertion.

## The gate-caught repairs (the round's bug ledger)

The first focused run honestly failed with **zero candidates**: 723 of 819
weight-positive entries died at the gender gate. Root-cause re-derivation from
the binary showed the gender law was right but the real killer was the
**target-conditioned actor gate** (skip a Sim-typed *target* when the actor is
human — the landing had tested the actor's own OBJD and broken the whole
scan), plus three further genuine engine bugs found and fixed: the port's
gender encoding in word 65 diverges from the native species encoding;
`MotiveTabInv` had 13 of 23 rows (skill-selector ads threw, silently aborting
the primitive); and TS1 trees must treat unknown opcodes ≥256 as FALSE, not
abort — the original 'try autonomy' tree depends on that fall-through. After
the fixes: **21 candidates at the decision** (was 0), winner accepted, and
the law checks pass.

The adversarial review then issued FIX-FIRST on three P1s, all repaired: a
save-restore clobber of the free-will mirror (a save made with free will off
would silently dead the toggle), the battery poisoning its own restore
snapshot, and an undocumented shared-interpreter behavior change (the check
yield law now detects yields explicitly — "a check that yields is a failed
check" — with the caller audit recorded). Cheap P2s fixed: the child skip on
the adult-block gate, a logged catch-all, thread-safe live-VM mirror, stale
comments, and the true gather-order disclosure.

## Validation

* Baseline before work: 142/0.
* Focused packaged run `-autotest-opts freewillwin,brainlive,freewill,freewillvar`:
  **AUTOTEST RESULT PASS passed=4 failed=0** (`focused-final.log`) —
  `freewillwin` 11/0 sub-assertions: multi-candidate decisions 2/2, positive
  scores, lawful drawn indices (2/2), seed-replay matches (2/2), winner
  identities match the pure-law predictor (2/2 + transpile self-test), cutoff
  law 2/2, queue cross-check with 4 disclosed hand-off drops and zero
  violations, OFF-gate control, restore proofs (incl. attr18), game-data hash
  unchanged. Publish and package carry the same `Simitone.Client.dll`
  SHA-256 `47627a31cea4d70a7e3cb497ac371444fa89da75abf741bd9eb8ea12d3eb493f`.
* Full default suite: **PASS passed=142 failed=0** — the
  interpreter yield law and the native selection path provably do not disturb
  the existing checks. One environment lesson recorded: the first default run
  of this round accounted 140/0 because the `/tmp` IFF probe files consumed
  by `glob-fix`/`chunk-reg` had been reaped; with `make_probes.py`
  regenerated (the documented HANDOFF step) both checks verdict again.
  Observed weakness (pre-existing, noted for a hygiene round): a missing
  probe logs `probe-missing` and silently skips its Pass/Fail instead of
  failing loudly.

## Disclosed residuals (next slices)

* **Check-tree serving**: every drawn winner currently drops at hand-off
  (chair-sit 4107, sofa-family 8208, person-person 8662) — the port's
  synchronous check-tree serving and slot routing is the next gameplay
  serving gap, now precisely named with drop sites in the battery log. The
  native drops check-failing winners too; the gap is the serving, not the
  selection.
* Gather order matches the native module order only while object IDs are
  monotonic (ID reuse reorders; disclosed).
* Stratum-1 distance cap hardcoded 20.0f (CFG+0x1c unrecoverable); downtown
  branch is a constant false (no downtown lots mounted).
* The natural-reach window and engaged-cutoff cases are environment facts
  recorded by the battery's disclosures.
* HANDOFF's R168-era residual section is superseded by the PARITY ledger
  (death chain R219/R220, chance cards R222) — corrected in this round's
  documentation commit.
