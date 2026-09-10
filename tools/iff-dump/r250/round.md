# R250: free-will winners serve — the hand-off law corrected and the serving myth retired

R249's battery disclosed that every drawn autonomy winner "dropped at
hand-off", with the working hypothesis of an unported chair/slot serving
path. R250's cross-examination retires that hypothesis and lands the real
finding: **the drops were lawful native-identical guard failures, the
battery had misclassified them, and the port's gather was missing the
native's per-candidate test — which, once restored, makes winners serve.**
The focused run now shows every drawn winner enqueued with identity and
priority matched, zero drops.

## What the cross-examination established

* **The misclassification.** Act 4107 is not a chair: it is UrnStone.iff
  "Mourn" (two TTAB entries, tests 4108/4113, gated by the urn's
  ghost-window attribute 4). The R249 drops were: the urn's mourn window
  closed, the wall phone not ringing (its one-instruction ring-latch test),
  and a magic social gated by token FindToken — all **native-identical
  TreeFalse failures** (the port auditor traced each winner's test tree
  statically and named the failing instruction). The port's slot/routing
  serving is implemented and serves user sits; there was no serving lesion.
* **The gather-test adjudication.** The two research agents disagreed on
  whether the native runs the test tree per candidate at gather or once at
  hand-off. A maintainer scan of the raw binary settled it: TestInteraction
  has six call sites, **two of which sit inside AppendInteractionsForAuto**
  (0x106038 object-target arm, 0x106128 person-person arm), plus the
  winner re-test inside TryFindBestAction (0x109d1c) immediately followed by
  GetInteractionScore (0x109d48). The R249 repair had over-removed the
  port's gather tests; without them the pool contains test-failing
  candidates whose winners then lawfully drop at hand-off.
* **The score law.** GetInteractionScore has exactly two call sites in the
  binary — post-draw at 0x109d48 (on the tree-mutated candidate copy) and
  one unrelated. The native never scores candidates at gather: the broken
  heapsort (R249's rotate-left finding) makes ordering irrelevant, the draw
  is uniform over gather order, and the winner's score — computed at
  hand-off from the tree-mutated ads (MotiveAdChanges applied) — exists
  only for the sitting cutoff (accept iff score ≥ FCNS 1e-6).

## Implementation

* Engine (`VMFindBestAction`): per-candidate test trees restored in the TS1
  gather (both native arms cited; one uniform port path with the
  person-person ctor difference disclosed); per-candidate scoring removed
  (native-faithful); the hand-off now applies the re-test's MotiveAdChanges
  to a copy of the entry's motive ads and computes the winner's hand-off
  score there, feeding the cutoff; hand-off re-test unchanged. (The
  shared-temps nuance at gather is documented as a future adjudication.)
* Battery (`freewillwin`): the 4107 misclassification removed — drops
  classify by check outcome with the winner's test-BHAV identified; (g) is
  now a hard pass requiring a served winner (identity + Autonomous priority
  matched per decision, mutation-window scoped N..N+1); the score assertion
  rewritten to the hand-off law; the AttemptPush auto=0 re-check divergence
  disclosed; a UID==0 filter bug fixed (each avatar's first insertion was
  invisible to the old tracking).

## Validation

* Focused packaged run `-autotest-opts
  freewillwin,brainlive,freewill,freewillvar`: **AUTOTEST RESULT PASS
  passed=4 failed=0** (`focused-final.log`) — `freewillwin` 11/0 including
  **4 served winners** (av21 obj18#act8490, av16 obj25#act4097, av21
  obj246#act4102, av18 obj133#act8196 — each enqueued within one frame of
  its decision with matched identity and priority; 0 drops, 0 violations).
  `Simitone.Client.dll` SHA-256
  `54bd8f2a58cf91b406493f0142f1784f00c3e9e0776d07411d7dfe2827ab0145`
  (publish == dist).
* Full default suite: result recorded in `default-final.log` (final HANDOFF).
* The adversarial review confirmed the gather restore's fidelity, exception
  isolation, per-tree bounds, the unchanged hand-off, and per-decision
  battery scoping before the score-law repair.

## Disclosed residuals

* Every observed decision remains harness-served (the natural brain gate did
  not open in-window — pre-existing disclosure); no ENGAGED-cutoff decision
  has been captured (standing law only).
* The shared-temps nuance: the port's gather test trees see the actor's live
  TempRegisters (~170–340 trees per decision); native per-candidate temp
  isolation is the named future adjudication if a divergence is observed.
* The AttemptPush auto=0 re-check can lawfully remove a just-served winner
  (would present as a drop; log-and-fail with disclosure).
* Person-person gather arm uses the uniform test path (the native's
  person-person ctor arm difference has no port consumer).
