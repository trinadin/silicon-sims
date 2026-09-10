# R165 — original SchoolBus day traced end to end

Round mandate: close the ordinary House 5 school-bus lifecycle with evidence
from the owner's original data and a packaged runtime trace. This is an
opt-in harness round only: it changes no production VM, UI behavior, layout,
or proprietary data.

## 1. Canonical contract from the owned corpus

`r165-school-bus-canon-scan.py` reads the locally staged House 5, Objects, and
Global files and emits metadata plus hashes only. It hard-pins:

- `House05.iff` SHA-256
  `a6d18d81c5072c507b118b68598ee12ea57403634d6507c39b7a04ad4c6cdd99`;
- `Objects.far` SHA-256
  `b029f449a91289b0a789810ff226da0e3a578b0931f1b5bcd104433e316efbf0`;
- `Global.far` SHA-256
  `c301952612dc2adfc4a94b9d9e6e78c3cef5635b989e1cb4342b29989d13b5fb`;
- Cassandra's field-encoded House 5 fixture: object 16, GUID `c1207913`,
  NeighborId 32, PersonType 0, age 9, JobType -1, grade slot 4;
- CarPortal BCON 4097 tuning `[100,5,9,15,2,40,0,12]`, hence school
  09:00–15:00;
- `SchoolBus.iff` master GUID `50ac15bf`, 31 OBJDs, private main 4096,
  semiglobal `CarGlobals.iff`, and the relevant private/semiglobal BHAV
  inventories; and
- the exact creation and state-transition sites below.

The original outbound path is `CarPortal.iff:4100:28 → 4115`; instruction
`4115:10` calls 4119. The return path is the child's At School routine 4116;
instruction `4116:27` calls 4119. In both cases 4119 instruction 4 creates the
SchoolBus master GUID out of world after instruction 3 checks for an existing
object of that type.

Attendance is not equivalent to creation. SchoolBus 4121's primary route is
instruction `5 → 7 → 18 → 19 → 24 → 6`; instruction 6 sets the pushed Sim's
Hidden flag to 1. Its fallback begins at instruction 20 and can exit false at
23. CarPortal 4116 tests Hidden at instruction 31 and writes PersonData[87]=1
at instruction 59 while the child is away, then PersonData[87]=0 at instruction
60 on return. The separate missed-school handler 4117 instruction 4 adds 3 to
PersonData[57], the same slot holding the fixture's grade 4.

The report is reproducible byte-for-byte from the repository, from `/tmp`, and
under `python3 -O`. `ruff check` passes. Final report SHA-256:
`6b6acda99789aa372c80c52e4816af4c307390d021ec2384065bbc87cf951a9a`.

## 2. The first runtime trace corrected two assumptions

The honest first packaged trace (`r165-schoolreturn-run1.log`) observed one
exact outbound create at 08:00 and one visible bus group, but Cassandra never
entered the Hidden/out-of-world school state. The bus disappeared at 11:00,
there was no return create, and her grade changed 4→7.

That result is the original missed-school branch, not evidence that the engine
failed to return a successful child. The static decoder independently pins the
exact `4117:4 += 3` mutation. It also disproved the initial idea that ordinary
schooling uses a child clone: SchoolBus's nearby Find Clone call belongs to a
different car-global path, while the Makin' Magic clone BHAVs target person
type 6. The ordinary path changes Cassandra herself.

A second diagnostic run naturally reached the successful branch: Cassandra
became Hidden/out-of-world with PersonData[87]=1, the exact return bus appeared
at 15:00, and she returned visible with PersonData[87]=0 and grade 3. That run
also exposed an instrumentation mistake: a `VMRoutingFrame` copied its BHAV
parent's 4121 instruction pointer, making samples look like a second 4121
action and an impossible `5→0` BHAV loop. Routing frames are now excluded from
BHAV action/frame counts. The decoded 4121 CFG has no `5→0` edge.

## 3. Opt-in `schoolreturn` contract

The fixture is valid only for an actual `House05.iff` load with exactly the
child identity, starting grade, schedule, and mounted SchoolBus master pinned
above. The check remains outside the 103-check default set because it runs from
before the 08:00 outbound event until the return bus has naturally disappeared.

The gate requires:

1. exactly two SchoolBus creations, both at `CarPortal.iff:4119:4`, with one
   `4115:10` outbound parent at 08:00 and one `4116:27` return parent at 15:00;
2. at most one visible SchoolBus multitile group; outbound observed and gone
   before return, then return observed and gone naturally;
3. SchoolBus frames 4096 and 4121, CarGlobals drive-away frame 8205, and the At
   School 4116 frame;
4. the original logical child, tracked dynamically by NeighborId 32, reaching
   Hidden=1, out-of-world, PersonData[87]=1; and
5. that same original ObjectID as the sole live logical child at home,
   visible/in-world with Hidden=0 and PersonData[87]=0, At School inactive, and
   final grade within initial ±1. The canonical missed-day +3 cannot pass.

Creation tracing records creator scope/routine/IP, parent scope/routine/IP,
caller/callee/stack object, clock, GUID, base ObjectID, and attributed leg.
Bus lifecycle distinguishes an allocated out-of-world controller from an
actually visible multitile group. Child state logging is limited to relevant
SchoolBus and CarPortal 4116/4117 frames.

## 4. Fixture-only queue conditioning

House 5 can restore with an authored autonomous action already running ahead of
the newly queued Go to School action. Letting that unrelated action win produces
the real missed-school branch, which is useful evidence but not a deterministic
success fixture.

After—and only after—the exact outbound create, the harness identifies the
natural pending bus action by the full tuple: private routine 4098, interaction
0, `SchoolBus.iff` code owner, and callee master GUID `50ac15bf`. If a different
active action is Normal, cancellable, and not `MustRun`, the harness calls the
ordinary `CancelAction` API for that action's UID. It never clears or rebuilds
the queue and never cancels or resets the bus action.

If the exact same blocker remains for three sim-minutes, a one-shot
`AbortCurrentInteraction` fallback is allowed only while the bus action remains
queued; losing that action invalidates the fixture. The decisive run needed one
normal cancellation of ToyBox 4097 and **zero** hard aborts. This branch exists
only under `-autotest-opts schoolreturn`; normal play is untouched.

## 5. Decisive validation

The final packaged `lot,schoolreturn` run is
`r165-schoolreturn-run3.log`:

- exact outbound creation at 08:00, base group 245;
- one normal blocker cancellation at 08:01, zero hard aborts;
- primary 4121 route instructions 5 and 18 observed; no fallback/false exit;
- Cassandra Hidden=1, out-of-world, PersonData[87]=1 at 09:02;
- outbound group naturally gone at 11:00;
- exact return creation at 15:00, reusing base group 245;
- Cassandra visible/in-world with Hidden=0, PersonData[87]=0 and grade 3 by
  15:10; and
- return group naturally gone at 17:00, maximum visible bus groups 1.

Result: **2 passed, 0 failed, 0 skipped**, clean Run/Dispose. SHA-256:
`48046cae7930f2334e12d07cf395ce666fea9b0c2e7814d64a2cd8e60445196a`.

The final self-contained arm64 package then passed the unchanged default suite:
**103 passed, 0 failed, 0 skipped**, wrapper exit 0, clean Run/Dispose, and zero
`SimAnticsExc` or `bad-routine-frame` records. `r165-gate-run1.log` SHA-256:
`456912f8853c3a266c1f4931997d9569bd24124251d6a1d87b938177db5e3a43`.

Release build: success, 0 errors. Static scanner: reproducible PASS. `ruff` and
`git diff --check`: PASS.

## 6. Remaining parity work

This closes the ordinary successful SchoolBus lifecycle, not every branch.
Chance Cards, alternate career/school outcomes, live free-will winner priority,
natural death decision→ghost, expansion systems, custom animation behavior,
and the remaining UI campaign are still open. The UI items remain protected
geometry/interaction work: implementation requires a traced blast radius,
explicit approval for the proposed visual change, and visual validation.

No original game payload was committed. The scanner and logs contain only
metadata, hashes, decoded facts, and runtime observations.
