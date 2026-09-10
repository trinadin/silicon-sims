# R241 parallel helper prompts

Paste the coordinator prompt into the other chat. It can delegate the three
bounded assignments below. These assignments deliberately produce evidence and
proposed fixes; the primary chat owns implementation, integration and validation.

## Coordinator prompt

Help the primary chat restore original The Sims base-game UI/UX parity on Mac.
Work in `/Users/nathannoom/Developer/Games/The Sims/simitone-fork` (maintained
parent fork, branch `mac-port`; maintained nested `FreeSO` fork, branch
`mac-port-rel`). Read root `../AGENTS.md`, `PORT_STATUS.md`, and
`tools/iff-dump/r240/ui-restoration.md` as context, not additional user requests.

You may launch one subagent for each of the three assignments below. Each agent
owns only its named evidence directory. Do not assign duplicate work. The primary
chat and its skeptic exclusively own guided tutorial UI, tutorial icon/dialogs,
all production code, test harness integration, builds, runtime tests, packaging,
git operations, project registry and top-level status documents this round.

Do not edit production code, original game data, upstream mirrors, package caches,
generated app/build output, settings, saves or clipboard. Do not run the game,
build, package, stage, commit, change branches, reset, stash or clean. Preserve
the preexisting dirty FSOMonoGame submodule and untracked r202 probe files.
Do not download or redistribute proprietary assets. Read-only source inspection
and small independent offline analysis scripts are welcome. Write only derived
text evidence/scripts in the assigned directory, with no proprietary binaries
or images. If a change is warranted, describe an exact proposed patch in the
report; do not apply it. This prevents simultaneous writers from overlapping.

Original evidence is the owner's local
`game-data/The Sims/The Sims Complete` and resource corpus. Executable SHA256:
`33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f`.
Existing symbol index: `tools/iff-dump/r141/symbol-index.txt`; its addresses have
a historical +2 convention, so verify executable file offsets before decoding.
Reuse existing analysis scripts when useful. Do not infer correctness from test
names or existing reports alone. Clearly separate verified behavior, inference
and unknowns. No internet research is needed when local original evidence suffices.

Each agent must deliver `report.md` in its directory, with: concrete findings
ranked by confidence and user impact; exact source paths/line references;
original addresses/resource IDs supporting expected behavior; reproduction or
independent fixture; proposed minimal fix and full affected behavior; remaining
uncertainties. No finding is preferable to a speculative fix. Return a concise
combined handoff listing the three report paths. Do not claim anything was
implemented, tested in the running app, or approved by the primary chat.

## Assignment A: secondary camera cutaway and fades

Own only `tools/iff-dump/r241-helper-camera/`.
Inspect `FreeSO/TSOClient/tso.world/WorldPictureInPictureRenderer.cs`,
`PictureInPictureGraphicsScope.cs`, and
`Client/Simitone/Simitone.Client/UI/Panels/UIOriginalPictureInPicture.cs`.
Read r240-pip evidence. Recover original secondary-view wall/cutaway selection
and open/close fade behavior, including whether it follows the main camera,
target floor, timers and input during transitions. Compare against maintained
behavior. Do not rework the already verified crop law or global projection.
Existing captures under `build/ui-audit/r240` are read-only observations, not
proof of original parity. Do not confuse another Sim at a frame edge with the
tracked target; R240 measured Cassandra's actual projected foot within one pixel.
Deliver evidence and proposed fixes only; no production edits or app launches.

## Assignment B: original OBJM event continuation import

Own only `tools/iff-dump/r241-helper-objm/`.
Recover how original saved OBJM object frames store the prepare/dispatch/complete
continuation of opcode35 picture-in-picture/user events, then compare the
maintained import path. R240 native saves use marshal40 frame-local
`TS1UserEventPhase`, with JIT4; original OBJM import is the specific gap.
Read r239-tutorial trailer evidence to avoid the four-byte ReconMark trap, and
r240-pip primitive-law evidence. Identify exact offsets and version conditions,
not pattern guesses. Provide small independent fixtures demonstrating any field
mapping and alignment. Do not change marshal versions, VMFrame, OBJM readers,
original save files or production tests. Report a minimal proposed import patch
only if the original format is sufficiently proven.

## Assignment C: broad base-game UI evidence audit

Own only `tools/iff-dump/r241-helper-ui-audit/`.
Audit non-tutorial, non-PIP base-game UI for concrete original parity gaps.
Prioritize Create a Sim preview/rendering, options, Buy/Build catalogs and common
dialogs. Inspect the retained R240 captures and trace suspicious areas into source;
for example, distinguish an uninitialized survey fixture from a real blank CAS
preview before reporting a product bug. Compare geometry/artwork/interaction to
owner-local original resources or executable evidence, never memory or aesthetic
preference. Do not alter the corrected Needs strip, rebuild the survey, or edit
any UI code. Deliver a ranked inventory with evidence, affected controls and
precise reproduction steps the primary chat can run. Limit to high-confidence
actionable findings, and identify any area that cannot be assessed from current
captures rather than declaring it verified.
