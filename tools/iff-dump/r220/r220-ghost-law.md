# R220 — the ghost night-haunt trace (ghosttrace; the R219 residual closed)

R219's residual was the ghost loop (`Ghost - Main Loop` 8641) — hour-gated,
outside the post-death window. This round decoded the tombstone's own law and
caught the ghost in-window.

## The UrnStone.iff law (decoded with the r219 decoder)

`UrnStone.iff` is extracted on demand from `GameData/Objects/Objects.far`
(**not committed** — EA-owned bytes). BHAV family:

```
4096 'main' (the urn's engine-dispatched loop):
  ins5  Global[0] >= 22 ? ->12 : ->21        the NIGHT GATE — live hour
  ins21 Global[0] <= 1  ? ->12 : ->20        night = 22:00..01:59
  ins12 Local[1] = rnd(0..Tuning[131])       Tuning[131] = BCON4097[3] = 8
        Local[1]==0 -> ins6                   a 1-IN-8 roll per pass
  ins6  idle(0x708=1800 ticks ~ 30 sim-min)  the pass cadence
  ins7  CALL 4100 'generate ghost'
  ins20/19 day branch: wander idles (Temp[0] += 880 etc.)

4100 'generate ghost': create + find-location + push-interaction +
  play-sound 0x13b; caps concurrent dead persons at Tuning[133] =
  BCON4097[5] = 3 (counts persons with PersonData[68]==1).
4109 'Force Ghost': the urn's OWN debug tree (TTAB action, test 4110) —
  the deterministic fallback.
4103 'Ghost Walk' / 4111 'Ghost Scare' / 4112 'wander' — urn-side haunting.
```

`Global[0]` IS the live hour — `VM.GetGlobalValue(0)` returns
`Context.Clock.Hours` (also verified: no port defect; the gate reads the
clock directly).

The PERSON-side loop (PersonGlobals, decoded R219): `person main` ins38 calls
8641 `Ghost - Main Loop` on the DEAD SIM'S OWN ENTITY once the ins11
`PersonData[68]==1` dead branch runs — the ghost does not strictly need the
urn's spawn: the dead person IS the ghost (matches the R80 shipped-save
IsGhost residents).

## The probe (`ghosttrace`, opt-in; rides the deathtrace state machine)

Same soak as deathtrace (Hunger collapse to -100) with a ghost phase after
the death settle: the clock is set to **21:55** so the urn's OWN gate at 22:00
crosses naturally (the minimal jump — nothing but the hour is moved), the urn
entity is captured from the tombstone create event, ghost ids are watched with
**IFF attribution** (UrnStone 4100 collides with CarPortal 4100 'process'),
the walk list refreshes so a newly materialized ghost avatar is sampled, the
urn's OWN thread is walked, and a lot-unload guard evaluates honestly. If no
spawn is seen by night+45, the urn's own `Force Ghost` TTAB interaction is
pushed (`FSOSkipPermissions`) — the engine's debug tree, DISCLOSED in the log.

## Observed (targeted-soak.log, untouched run)

```
7:24  DEAD-FLAG PersonData[68]=1, entity.Dead (obj16 — Cassandra)
21:55 night approach armed; urn=218; UrnStone 'main'(4096) seen running
21:55 new avatar joined obj=254 (nightfall — the natural spawn path; the
      short-lived 4100 frame fell between the per-update samples)
22:40 FORCE-GHOST fallback pushed (disclosed; in hindsight redundant)
 1:48 8641 'Ghost - Main Loop' + 8638 'Ghost - Wander' executing:
        on obj16 itself:  stack=[4096:1, 8193:38, 8641:8, 8638:6, 8638:0]
        plus ghost-scope frames under User00023.iff
      — person main ins38 -> 8641 is the EXACT decoded dead-branch call site
deathtrace summary: handler/kill/dead all true, ghostSeen=True
ghosttrace summary: ghostSpawned=True (via ghostLoopSeen) -> PASS 7/0
```

The blocking-dialog stand-in stayed necessary through the night (a
User00010.iff household dialog latched at ~21:56 and was released).

## Honest residuals

- The `4100 'generate ghost'` FRAME was never caught (its create is a
  one-tick event; the new avatar at nightfall is the attributed evidence).
  The natural-vs-fallback split is therefore logged conservatively:
  `naturalSpawn=False forcePushed=True` in the summary line, with this note
  as the attribution.
- The 1-in-8 roll was never observed FIRING inside a single window — the
  ghost manifested at 1:48, three hours after both the nightfall avatar and
  the 22:40 push, so which path created the ghost is not distinguishable
  after the fact. The PASS law rests on the ghost LOOP executing, which is
  path-independent.
- Ghost Scare (8640/4111) and the haunt sound (8642) did not execute
  in-window (no living sim was routing near the ghost at 1:48).

## Round result

- Targeted soak `CHECKNAME,ghosttrace,corpus`: **passed=7 failed=0** (run 1
  `targeted-soak-run1-entrymiss.log` kept — the ghosttrace name wasn't in the
  deathtrace entry condition, fixed same round).
- Full default gate: `gate-run.log` — 127 checks, 0 failed (ghosttrace is
  opt-in; the default count is unchanged).
- PARITY: the R219 death row's residual upgrades — the ghost loop observed
  live at the decoded call site; remaining disclosed: the 4100 frame catch,
  scare/haunt-sound in-window, and the natural-vs-forced attribution above.
