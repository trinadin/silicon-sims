# R152 — the engine creation dialect: cXPerson::RandomizeAllInterests ported

Round target (the R151 recommendation): port the engine's own interest
randomizer so CAS/townie sims carry randomized interests exactly like a
CAS-created original sim, and reconcile the two writer dialects at the
panel. Binary evidence: `../r151/r151-dis-randomizeallinterests.txt`
(re-read this round); all addresses are file offsets.

## 1. THE LOOP-2 COUNTER CORRECTION (r151 doc erratum)

The R151 decode recorded loop-2 bucket counters as `{1,2,3}` — impossible
(sum 6) against loop 2's FIVE stores. Re-reading the raw words settles it:

```
0x11176c: li r0,2          ; r0 = 2
0x111774: li r4,1          ; r4 = 1
0x111778: stw r0,68(r1)    ; counters[1] = 2
0x111780: stw r4,64(r1)    ; counters[0] = 1
0x11178c: stw r0,72(r1)    ; counters[2] = 2   (r0 STILL 2 — never clobbered)
```

**Loop-2 counters = {1, 2, 2}** (sum 5 ✓). The r151 "{1,2,3}" was a
misreading of the second `stw r0` ("li 1 / li 2 + the leftover 3" — there
is no third immediate; both trailing stores use r0=2). Loop-1's blob stays
{4,3,3} (TOC data, sum 10 ✓). A correction footnote is appended to the r151
decode doc.

## 2. THE PORTED LAW (verbatim structure)

`cXPerson::RandomizeAllInterests` (FreeSO `VMInterestRandomizer`):

- **Loop 1** — words 46..55 (8 base topics + Technology/Romance), counters
  {4,3,3}: four rolls land 0..3, three 4..6, three 7..10.
- **Loop 2** — words 13/14/16/20/26 (Exercise/Food/Parties/Style/Hollywood,
  the i=0..4 switch), counters {1,2,2}.
- **Roll law**: `rand % 3` picks the bucket (÷3 magic 0x55555556); an
  exhausted bucket re-rolls (the `bl` back-edge re-DRAWS the random);
  the value comes from a FRESH draw inside the accepted bucket's block
  (bucket0 the rotate idiom r151 INTERP'd as %4 → 0..3; bucket1 `4+%3`;
  bucket2 `7+%4`); every store is preceded by `mulli r0,r0,100`
  (0x111754 / 0x111878-0x1118a8) — the **x100 dialect** (0..1000).
- **Gate** (`Reset` 0x111bc8 / `Initialize` 0x112014): fires only when
  words 46..53 are ALL exactly 10 — the engine's "interests unset"
  sentinel. Ported as `IsUnsetSentinel`, wired in `VMTS1Activator`
  alongside the R150 `IsZeroed` tree mirror.

## 3. THE DUAL-DIALECT BRIDGE (the §2.4 reconciliation, ported)

Two original writer dialects now coexist in the port exactly as they do in
the original's data: **raw 0..10** (character-file trees + NBR records,
R150) and **x100 0..1000** (engine creation, this round). The panel's
display scale is x100 (`cWinInterest` ÷100), so
`UIOriginalInterestSubpanel.BridgeToDisplayScale` maps raw words ×100 and
passes x100 words through — **total on both domains**: the raw domain's
max (10) is below the smallest nonzero x100 value (100), so no word is
ambiguous. This is the ported resolution of the r151 §2.4 scale tension
(disclosed interpretation; the original's own load-time bridge, if any,
remains unemulated).

## 4. Generator replacement (the CAS/townie path)

`SimitoneNeighbourGenerator.MakePersonData` previously wrote pre-decode
guesses (`pd[13]/pd[14]=500..800`, `pd[16]=pd[26]=600`) + the R150
tree-dialect mirror for 46..55. Now a single `VMInterestRandomizer.ApplyTo`
call — the generator IS the port's `cXPerson::Initialize` (CAS families and
townies both flow through it), so generated sims speak the engine creation
dialect with ALL 15 topics randomized (Style included — the old code never
wrote word 20).

## 5. Gate

- **uiintvals extended (never weakened)**: per-avatar dialect split — raw
  domain keeps the exact R150/R151 tree-shape law; the x100 domain pins
  the loop-1 shape 4×[0..3] / 3×[4..6] / 3×[7..10] (multiples of 100 in
  0..1000). Gate lot = character-file avatars → dialect=raw logged.
- **uiexprand (NEW, 92→93)**: (1) generator — `MakePersonData` emits the
  x100 dialect with BOTH loop shapes ({4,3,3} on 46..55, {1,2,2} on
  13/14/16/20/26); (2) sentinel — all-10 words 46..53 → true, one broken
  → false; (3) live — `Apply` on a real avatar writes both shapes and
  clears the sentinel, every touched word finally-restored and verified;
  (4) bridge — raw ×100 / x100 pass-through / FillWidth(bridge) sanity.
  First targeted run: genShape=True sentinel=True liveShape=True
  restored=True bridge=True (r152-targeted-run1.log).

## 6. Residuals (disclosed)

- The original's own runtime reconciliation of tree-raw words for display
  (if any exists beyond the panel's ÷100) is still unemulated — the port
  bridges at read instead; both dialects display correctly either way.
- The all-10 sentinel's WRITER in the original was never found (r151
  residual #2) — the port's sentinel path exists and is gate-proven but
  no port writer produces the sentinel state.
- Bucket0's rotate idiom remains INTERPRETED as %4 (r151 residual #3);
  consistent with all bucket-counter sums.

---

**R153 ERRATUM (the sentinel gate law):** this doc's "all-10 sentinel"
(the `IsUnsetSentinel` predicate, the activator hook, and residual bullet
2 above) was built on the r151 polarity misread. R153 proved from the raw
words that the gate branches are `0x4181 bgt` — the gate passes when all
eight words 46..53 are **<= 10** (the zero sweep arms it; no all-10 state
exists anywhere in code or shipped data). Port consequence:
`IsUnsetSentinel` is corrected/renamed to `NeedsRandomize` (<= 10 law) and
the activator sentinel hook is REMOVED (its state has no writer; firing
post-load would re-randomize tree sims every session, diverging from the
original's creation-time gate). The randomizer LAW itself ({4,3,3}/{1,2,2},
x100 stores) is untouched — it was verified independently and stands. See
../r153/r153-sentinel-verdict.md.
