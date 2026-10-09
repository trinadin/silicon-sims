# AUTOTEST.md — headless gameplay test harness

**Purpose.** Behavioral and visual regression checks using original game data and independently recovered contracts. `-autotest` boots the engine, loads a real lot, exercises production interaction/rendering paths, and exits cleanly. Asset bytes and static traces alone do not establish UI parity; rendered review and workflow checks support visual claims.

## Run

    ./tools/run-autotest.sh        # runs dist/The Sims-arm64.app against game-data/The Sims

Flags (Simitone.Desktop/Program.cs):

- -autotest "<houses>" — lot(s) to load (house 5 = Goth, the standard fixture).
- -autotest-opts "<checks>" — comma list of checks; default is the full set below.
- -autotest-timeout <ms> — per-run budget. The headless SDL/Cocoa event loop can stall ~10–16 min before the first tick (environmental, not the engine); use -autotest-timeout 1800000 (30 min) for full suite runs.
- -autotest-noexit — stay open for interactive use.

Output: results to <UserDir>/autotest.log + stdio (GameLog.Write mirrors to stdout). Look for AUTOTEST RESULT PASS passed=N failed=0.

**Never launch via open --args** — LaunchServices delivers empty args. Run the binary directly:

    dist/The Sims-arm64.app/Contents/MacOS/TheSims -path"<gamedata>" -autotest 5

CAS editor follow-up extends existing `uicasorig` / CAS239 with biography
inset, wrap, selection, capacity and focus-transition checks. `ucasflow` now
enters six paragraphs using printable text plus Return key events, verifies
disk read-back, and checks that the desktop preview pose survives Update with
mobile carousel avatars hidden. Passing pose checks do not certify framing;
retained 800x600 / 1024x768 captures expose that remaining gap. Evidence:
`tools/iff-dump/cas-editor-followup/round.md`.

## Checks (from AutotestRunner.cs Config.Checks)

### Wave-4 gates (2026-10-08)

Default-suite additions (the battery is 156 checks with all of these):

- **`ui37`** — UI-37: column scrolling on the decoded animated law (ScrollTo ramp
  + 200 ms autorepeat), view-pie cell ladder/magnitude/commit clamps.
- **`aud19`** — AUD-19 round 1: the per-event sound trace (HITTrace), 10 native
  laws, the CAS deny sound, cross-level ×3/5 attenuation, dead native names.
- **`cc06`** — CC-06: corrupt/duplicate custom content through the real providers
  (duplicate-skeleton silent-replace, corrupt CFP skip/report, untranslated
  animations, unresolvable skeletons).
- **`uidtbar`** — UI-38/UI-38b: the UL community filter-toolbar live mount (strip
  as the community screen's top chrome, 7-button ladder, STR#171 labels,
  click/highlight/plaque laws) + the two production laws: the per-lot
  category census from the lots' own house files (pinned live n=1 values,
  snapshotted before any probe seeding) and the STR#6 slot-3 filter
  persistence round-trip (entry default-then-restore, save-on-change).
- **`aud20`** — AUD-20: the footsteps class on the decoded native law (anim-event
  triggers, SPR2-label surface classifier, 12-case name table, barefoot outfits,
  the TS1 zoom-volume byte-law).
- **`ssfame`** — EXP-16: gendered job titles on the native STR law across every
  consumer, fame-screen re-pins, the Studio Town object census.
- **`mmquest`** — EXP-17: the Magic Town quest lines (content census + native
  Choose Quest/Choose Reward + the $TokenNameLocal dialog law).
- **`ulpets`** — EXP-15 (+follow-up): Unleashed pet-AI depth — the owner pie rows,
  the training push (row 45 → action 8341), the show pedestal, the record-restore
  person-class law.

Opt-in takeovers (end the run at their verdict; run solo):

- **`trv05`** — TRV-05/TRV-06/TRV-06b: the vacation booking round-trip on the
  native Family+0x13C law (book → persist → travel → return → clear →
  persist), then the live-observation legs (AutotestTrv06, chained in the
  same gate): the score-controller chain (4126 spawn/restore → 4104
  accumulation → the 4108 threshold on the 0-based Tuning[3]=150) and the
  souvenir purchase/carry-home (CT 4116 mood-variant purchase; the carry-home
  runs as the REAL queued umbrella interaction — the engine's return-edge
  push, observed by the probe's automatic window at ~f=274 — and asserts
  BOTH bases placed in-world + the 240-frame persistence soak + both token
  sets consumed; the completion wait drains the member's thread stack of
  the carry-chain frames (the queue-name signal alone reads empty after the
  engine's transit cancel — TRV-06c).
- **`homepark`** — ENG-28b: the home-lot park restricted-tick law — a BUY-park
  soak on family-less lot 21 asserting the scheduler keeps waking
  (schedAdv≥30), street liveness advances, and the clock stays frozen (the
  only thing the native restricted tick freezes). The explicit-pause (0)
  half is deliberately not gated (retracted on hdserve evidence — see the
  PARITY ENG-28b bullet).
- **`hdserve`** — EXP-14: the Hot Date serve-choreography self-start — hands-off,
  the adult traveler's free-will draw must pick the podium's 'Eat Alone' row and
  the interaction's op-42 must create Controller-Restaurant-Eat (proven).
- **`petname`** — the adoption + naming chain on Old Town (needs the visit-session
  lifelift; see EXP-08's last-mile receipt for the visit-park law).
- **`magicbook`** — the magic-book mount flow on the magic lot (same visit-park
  class; the engine fix made the probe lifelift optional).

R252 adds opt-in `recordfmt`: the port now writes the ORIGINAL created-record
format — NBRS chunk-data version `0x3E`, record Version `0x4` (80-short
PersonData), `PersonMode 5`, `Name` = lowercase char-file stem, skin `pd[60]`
= lgt=1/drk=2/med=3 (was port `0x49`/`0xA`/256/PersonMode 9/`"iffname"`/skin
0-2), and FAMI Version `7` with no trailing zeros (was v9+16 zeros). The check
drives a real save (R246 isolation idiom), reads the written `Neighborhood.iff`
with a fresh `IffFile`, and asserts the original format fields + a measured
160-byte PersonData span + a write→read→re-write round-trip + the six
`skinmap` dialect assertions (original v4 and legacy v0xA maps). The `ucasflow`
format pins were repointed to the original format (strengthened, not
weakened). Evidence: `tools/iff-dump/r252-record-format/`.

R251 repoints the default `mood` check to the decoded weighted mood law
(was the equal-weight `/8` approximation) and adds opt-in `moodlaw`: the
native CalcHappy is a pure rate-limited recompute `Σ(wᵢ·mᵢ)/Σwᵢ` (no
smoothing), the STR#502 (adult) / STR#504 (child) Happy-Weight curves are
wired into the port's mood path with the child/adult table split, and
`moodlaw` drives a pinned motive state then a step change, asserting the
stored mood equals the weighted target and that it follows the change on the
next recompute (aggregation + no-smoothing together; PASS moodA=56→moodB=59).
`moodlaw` re-pins motives each frame, so it must be run WITHOUT `mood` in the
same soak (it mutates the live sim `mood` samples — a harness interaction,
not a defect). Evidence: `tools/iff-dump/r251-mood-cadence`.

R250 completes `freewillwin`: per-candidate test trees run at gather (the
native's two TestInteraction gather sites), the winner's cutoff score is
computed at hand-off from the tree-mutated motive ads (GetInteractionScore
exists only post-draw natively), and the queue cross-check is a hard
served-winner pass (identity + Autonomous priority matched per decision;
drops classify by test-BHAV with the mutation-window escape). Evidence:
`tools/iff-dump/r250`.

R249 adds opt-in `freewillwin`: the native free-will selection law is
implemented on the TS1 path (native gender-class admission, stratum filter,
mean-of-9-curve scoring, the original's rotate-left heapsort quirk, uniform
top-K draw with the captured seed, the sitting cutoff), and the battery
observes real decisions through the read-only decision seam, proving the
winner equals a pure-law predictor (heapsort transpile + xorshift seed
replay + cutoff law) on multi-candidate sets, with the free-will-OFF control
and restore proofs. Queue cross-check discloses hand-off drops (check-tree
serving residual). Evidence: `tools/iff-dump/r249`.

R248 completes `uitutorial-lifecycle`: the FAM import consumer implements
the decoded law (EXPi/uChr/Gtab/FINV codecs; eviction with net-worth
preservation; character files rebuilt from NBRS/uChr with the corrected
RelMatrix carry; the FAM atomically becomes Houses/HouseNN.iff; the tutorial
latch + immediate save), and the neighborhood poll auto-imports a staged
`Tutorial.FAM`. The battery's import tiers are active: file consumption,
the id law on a played neighborhood, eviction net-worth, fresh character
files, the tutorial latch, and a template-person restore proof after a real
import. Evidence: `tools/iff-dump/r248`.

R247 adds opt-in `uitutorial-lifecycle`: the tutorial lifecycle loop —
Options reset flow (confirm → Tutorial.FAM staged to Import/ fail-if-exists
→ second reset fails), the five-guard spawner (armed spawn; g26=0 / state-3 /
inhibit refusals), generic-call completion law (state transitions; live and
on-disk g26 clear), ESC cancel (tree + kill + highlighter clear), the
move-in refusal guard order, and the `tutorial`/`restore_tut` cheat polarity
matrix through the real console. UserDir is redirected per the R246
isolation pattern; game-data is pinned byte-identical. The import-law
assertions stay PENDING-ENGINE until the CheckForNewImports consumer lands.
Evidence: `tools/iff-dump/r247`.

R246 adds opt-in `ucasflow` (also accepted as `uicasflow`; NOT in the
default set): the real Create-A-Sim workflow — real neighborhood entry,
panels, dialogs and typing, the real persistence write, then fresh-from-disk
re-parses pinned against original created-record canon (FAMI/FAMs/NBRS/
character-IFF fields; personality pd[2..7] in generator order with Generous=0
— the regression pin for the repaired first-save crash; zodiac absent).
Isolation redirects UserDir to a fresh temp dir inside AutotestRunner.Begin
(game-data pinned byte-identical); a red ucasflow cannot fail the default
suite. Evidence: `tools/iff-dump/r246-casflow`.

R245 adds default `uitutorial-highlight`: opcode 34 "UI effect" flashes the
original 3-frame TutHigh arrow (167 ms ping-pong, native anchor and lot-button
shift, modal hide/restore, target-destroy clear) over controls resolved by an
original-image-id registry, and the per-tick poll runs the owner's real
"query wait event"/"got wait event" trees against the native condition table
(page captions, button press/hover by image id, shown portrait, shared
rotation/scroll edge latch) with a port-side re-entrancy guard. The battery
pins the on-disk BCON "ui events" codes, anchor/phase/cadence GPU captures,
prim-34 on/off and failure laws, the real-tree mailbox flush + heartbeat, the
skeptic-corrected ev 9="rel"/ev 11="job" captions, latch cross-poisoning, and
guard-blocks-poll. Evidence: `tools/iff-dump/r245-tut-highlight`.

R244 adds default `uicutaway`: wall cutaway must follow the recovered native
law — per-room masks are projected wall-extent overlap geometry, and the drawn
mask is history ∪ live-mode person room ∪ r26-gated cursor vicinity. The
battery pins per-room mask non-emptiness/localization on the real lot, the
production floor-change clear lifecycle, history insert/dup-no-recency/cap-3
eviction/off-lot reset, person room OR equality (other floor nothing; outside
k-probe exact 16 tiles), the cursor half-open rectangle including the r26 gate
and suppression, PIP same-floor/cross-floor mask draws with main-mask
reference+content restoration incl. an exception path, and a localized world
pixel diff. Diagonal side-distinct masks and the strict-overlap exact-touch
boundary are decode-proven opt-in residuals (no constructible headless
fixture). Evidence: `tools/iff-dump/r244-cutaway`; decode law:
`tools/iff-dump/r244-cutaway-geom`, `tools/iff-dump/r244-cutaway-state`.

R243 adds `uipip-fade` under `uipip`: the production PIP follows the original
333 ms transition, whole-window opacity threshold, asymmetric reversals and
repeat rules. Eighteen GPU captures test all three sizes against opaque and
empty references. Runtime assertions distinguish timer, Escape and explicit
close ownership, hidden skipped events, effects-option changes, registered
button handlers and disposal. Existing PIP tests explicitly select effects-off
and restore the prior setting. Handler tests do not claim physical mouse
injection. The original Mac compositor/clock/input contract is byte-pinned in
`tools/iff-dump/r243-fade`; serial package validation is in `tools/iff-dump/r243`.

R242 adds `uipip-original-import` under `uipip`: production OBJM conversion
restores phases only for a resolved opcode 35, preserves an actual House56
opcode-27 counterexample, and retains atomic stale-stack rejection. `uicasorig`
also draws the production CAS background over a varied GPU underlay and checks
all 22,000 preview pixels. The retained before/after size captures separately
bound changes to the existing preview rectangle and sampling edge. The CAS
capture is an isolated compositing fixture, not a real Sim-edit workflow.

R241 extends `uitutorial` with `uitutorial-action` and `uitutorial-render`.
The action check runs original named BHAV4099 and pause4146 in an isolated VM,
preserving the main stack and dialogs; it also checks TS1/TSO idle notifications,
sleep wake and command49 serialization. The render check routes an isolated VM's
dialogs through the actual lot-control sink and checks original geometry,
ownership/GUID rules, animation, modal/key behavior, five GPU captures and one
whole-window opacity composite. Eight viewport changes check bounded buffer
retention. Run these with `-autotest-opts lot,corpus,uitutorial`.
R241 scripts under `tools/iff-dump/r241` preserve settings bytes and run serially.

R240 adds default `uipip` and `uiclip`. The three PIP checks cover independent
opcode fixtures, per-frame save/nesting and legacy boundaries; original caption
pixels and wrapping; actual offscreen rendering at every size/zoom and rotation,
plus a Sim target, window controls, timers, options and both photograph routes.
The UI probe restores camera state, simulation speed, options and temporary
album/pending-capture state. `uiclip` exercises SDL Unicode clipboard text and
Ctrl/Command shortcuts, restoring and verifying every native pasteboard item,
format and data payload afterward. CAS checks use a private clipboard instead.
R240 extends `uicasorig` with all four outfit memories, skin clamping, session
resets and native typing/paste distinctions. Evidence and final results are in
`tools/iff-dump/r240/ui-restoration.md`.

R239 adds default `uitutorial` and `uicapture` gates. `uitutorial` uses an
isolated unticked VM to check ownership, deletion/dialog isolation, original
OBJM trailers, v38/v39 save boundaries, modality flags and compiled-module
version rejection. `uicapture` spans real frames: a visible magenta UI marker
must render above the world while the captured family JPEG excludes it and
retains native dimensions. The album and any pending capture are restored
on completion, failure or timeout. Existing `uiscrap` now checks original
camera framing/input, JPEG/thumbnail/legacy persistence, and native editor
keyboard/mouse rules. `uicasorig` adds isolated desktop command and hit-area
checks. Owner neighborhood and photo files are not written by these fixtures.
Evidence and final results: `tools/iff-dump/r239/ui-restoration.md`.

R238 restores the R237 UI findings and strengthens the existing gates rather
than adding another default check name. `uiglyph` now includes 30 independent
GPU alpha-hash fixtures for runtime font normalization. `uibudget`, `uihelp`,
`uiphone`, `uismall`, `uiinterest` and `uilive` assert the re-decoded native
layout and image-state laws. `uiscrap` exercises actual text-input/navigation
callbacks and file persistence in a temporary isolated album, with an isolated
clipboard handler. `uismall` covers viewport fitting and an isolated config
save/reload; `uisyschrome` renders opaque tiled interiors at four scales;
`uidtips` verifies the native 07 face at 2× and 1.7×.

Opt-in `uiaudit` captures Graphics/Sound/Play via direct transitions, redundant
refresh and production button clicks, plus Budget, Help, populated Phonebook,
Phonebook overflow, and a populated/selected Scrapbook caption. Run with
`-autotest-opts lot,corpus,uiaudit`. It writes 15 `uisurvey-audit-*.png` files;
`corpus` is required for dispatch. Capture success does not establish original
pixel parity. It never places a call or saves fixture photos. R238 evidence is
in `tools/iff-dump/r238/ui-restoration.md`; the historical R195 strip/10px row
and R161 fixed Budget board claims below are superseded.

    corpus,lot,motive,mood,load,savedthreads,relation,censor,rel-key,rel-mode,names,audio,jobs,npcinfo,persondata,travelinv,career,freewill,freewillvar,personality,motiveinit,skills,motiveact,relact,money,ttab,ttas,opcodes,genericcall12,genericcall13,genericcall14,callgraph,globalcalls,catalog,snd,iff,objd,ctss,strs,consts,bhvi,bhop,dgrp,slot,operand,opmx,chunks,brainlive,deathchain,savesim,uidump,uipal,loadscreen,carseek,uichrome,uitoolbar,uicur,uiglyph,uiboot,uilogo,uianim,uinbhd,uisplash,uidialog,uilotq,uilive,uijob,uivis,uicas,uidtips,uivfont,uimpanel,uiopts,uibuy,uiexpband,uibandlaw,uienamat,uiinterest,uiintvals,uiexpint,uiexprand,uiconv,uibrand,uidesc,uitt,uibuild,uibldt,uiinterest,uiroof,uigauge,uirate,uihouse,uivalue,uitext,uisurvey,uizoomcage,uicp,uidlgchrome,uibargeom,uiqueuegeom,uicasorig,uirel,uinav,uibudget,uihelp,uiscrap,uipie,uiphone,uismall,uiballoon,uisyschrome,uibigbtn

The default R168 gate contained **106 checks**. R168 added `genericcall14`,
which pins representative original stereo/piano callers, raw mode-14 operand
decode, opcode-1 registration, and the production handler's exact signed
Temp0-to-global-31 assignment. R169 added `uiqueuegeom`, so current source
enumerates 107 checks. R172 strengthens existing UI checks without changing
that count; the final exact-source packaged default passed all 107. R181
added `uivis` (runtime visibility contract — no derived `Draw` override may
paint through `Visible=false`), so the source enumerated 108 checks; the
R173–R189 rounds strengthened existing checks rather than adding names, and
the R190 session consolidated, batch-verified, and pushed them. R190 added
`uiscrap` (scrapbook/snapshot system: STR# 141/144 chunk shas + English
blocks, seven album art members, CPState dimension tables and clamp bounds,
ComputeCameraFrame + 1px-inset click law, the live dialog, the album
round-trip, camera-overlay mode gating), so the source enumerated 109
checks. R191 added `uipie` (the people pie: pieFace1 201x201 key-mask pin,
the decoded radius/color/slot constants, the 45-degree compass law, the
live pop + TrackPerson selection + close), so the source enumerated 110
checks. R192 added `uiphone` (the phonebook: STR# 180 sha + English block,
four art members, the window/board/list-rect constants, the live dialog
with census + selection law), so the source enumerated 111 checks. R193
added `uismall` (kTallSubpanel 600x150 + the ViewMenu 9x1/5x1 label
sheets with cell/hold-gating pins), so the source enumerated 112 checks.
R194 added `uiballoon` (speech-balloon chrome: the 9000/9001/9005 art
pins, the gzi-9005 resolution, the nine-slice builder law, the live
floater on balloon chrome). R195 added `uisyschrome` (the system-chrome
census: all nine Sys sheets + PopupInfoTiles 36x36 pinned by member+dims,
the decoded cell laws — WinChk 6x28x28, WinBtn 4x65x33, CloseBox/
MinimizeBox 4x12x11, WinScrol 12x17x20 in 6+6 groups, PopupInfoTiles
thirds — and the 3008-frame wiring of the help + phonebook dialogs) and
strengthened `uiphone`/`uihelp` (the 540x25 strip, the 10px RGB(0,0,57)
row law, the 19/22 clamps, the frame member, live strip/picker probes).
R196 added `uibigbtn` (the dialog-family big button on the original
WinBtn sheet on desktop: the art pin + a live construction pair proving
the 260x33 mount, ImageStates 4, White glyph captions, the mount
counter). R197 EXTENDED `uilotq` with the native move-in law (the
MoveInModeLotHandler guard-order truth table — tutorial outranks
community/occupied, occupied outranks the confirm — the
DesktopNativeMoveInSelections dispatch counter, and the STR# 132
title/message pairs 16/17, 12/13, 2/3, 14/15, 0/1, 6/7 pinned verbatim;
the check count is unchanged — no pins weakened, the old
moveInFallsBack pin replaced by the stronger native set). R198 EXTENDED
`uitt` with the tooltip color law (the cDefaultTTWindow model: default
pen BLACK, error slot (255,0,0), the only red consumer being the
unaffordable catalog product) and the disabled-object routing probe
(DisabledObjectTooltipText == the R129 ladder output + counter). R199
EXTENDED `uilive` with the same-mode click law (CPState::SetMode @0x210790
no-ops a force-0 same-mode request and every mode-button case passes 0, so
the already-active desktop mode button must do nothing — the probe drives
the real private `LiveButtonClicked` by reflection and pins steady-state
no-consumption, the user's fresh-entry reveal repro with zero mounted
UISwitchAvatarPanel children, and clean LIVE→BUY→LIVE round-trips). R200
EXTENDED `uirate` with the follow-sim law on its engine surface (the
disclosed replacement: the old motive-subpanel TrackButton+camera-toggle
pin retired WITH the desktop mount, superseded by the stronger set — the
45x45 kTrackingTarget art on its true surface, the retired-mount proof,
and the live cWinPeople tracking model through the real chrome+vm: webcam
click = select-when-different + toggle-track with the crosshair on the
tracked portrait, same-person click untracks, selection change and
camera-anchor loss detach via SyncTracking; the probe drives the
production RefreshWebcams synchronously), so current source enumerates
**115 checks**. R201 added the opt-in `simvis` diagnostic; R202 corrected
its diagnosis (the Vitaboy effect was never dead — see
tools/iff-dump/r202/r202-sim-visibility.md), fixed the real defect (the
PPX non-MRT depth-target leak that drew the avatar meshes into the depth
buffer), REWROTE the check framing-correct (per-sim projected-position
logs, camera panned onto a sim for the capture and restored after, bone/
mesh NaN+OOB census, manual-DrawAvatars mesh-pass aliveness count,
framed with/without live pixel diff; threshold >100 px, observed 23175),
and PROMOTED it into the default suite — current source enumerates
**116 checks** (full default gate 116/0 after the fix). R203 added `uidirt`
to the default suite — the dirt-tool error law (UIText STR# 149 DirtToolErrs,
sha-pinned, mounted live through GetString, plus the decoded code→text truth
table) — current source enumerates **117 checks**. R204 added `uistrfam` to the
default suite — the system string families (STR# 148 'Pause' + STR# 152
'DefaultDialogButtons' both sha-pinned; the system-dialog resolvers' truth
tables; a real constructed UIMobileAlert read off ButtonMap; the mounted Paused
blink label driven through the real vm pause state; the 142 object-table pin
rides along as a no-weakening companion) — current source enumerates
**118 checks**. R205 added `uifriend` to the default suite — the family friend
count (STR# 162 canon+mount, the UCP readout vs an independent walk of the
dedup law, below-money geometry, the 138[20] tooltip, the compare-then-set
write law, and the click dialog's exact 162 content) — current source
enumerates **119 checks**. R206 added `uipanelentry` (the control panel
composes permanently at desktop lot entry — live composed state + the
in-ctor exact-width construction probe) — current source enumerates
**120 checks**. R207 added `uicheat` (the cheat bar's engine law: geometry,
exact palette background sampled from the texture, white text, capacity, and
the two-chord ctrl+shift+C/B toggle) — current source enumerates
**121 checks**. R208-R211 added `uitrans`, `uivita`, `uitotal` (122-124)
— the standing gate at the BASE-GAME 1.0 declaration was **124 checks, 0
failed**. R212 added `uivitaplay` (the Vita idle PLAYBACK: content
resolution of every canon animation name, the builder's gender slot, the
sway math, and a real corpus-vm avatar driven through the full sequential
cycle with wrap, SetPerson reseed, child-terminator skip, private-channel
and sway-band pins) — and R213 added `uicheathelp` (the cheat help window:
roster canon, the prefix filter, the exact-or-first selection, and a fresh
production box driven through the real surface — full list on open, filtered
lists, the no-match label, Enter-completes-then-closes, Escape-closes-only,
OK-no-selection, the mounted frontend box) — and R218 added `uiviewpie` (the
view pie wired: the real open path, the engine item order/geometry, the ladder
cells, dedupe, selection dispatch, cancel cleanup) — current source enumerates
**127 checks**.
R165's `schoolreturn` full-day trace remains
opt-in and does not lengthen the default suite.
`savedthreads` continues to require an original `House05.iff` import, the exact
15 repaired continuation owners, their live replacement root mains, cleared
queues, and zero invalid live frames. `carreturn` is also intentionally opt-in
because it follows the fixture through a complete shift and 75 post-end
sim-minutes. `carseek` remains the shorter default morning soak. The source's
duplicate `uiinterest` entry is harmless and retained above verbatim. Full and
full-day gates need `-autotest-timeout 1800000`.

## Probe files (regenerate before gating)

Two checks read IFF-literalism probes out of /tmp: glob-fix (NPC_Vacation_Director.iff → 2×
GLOB "VacationDirectorGlobals") and chunk-reg (foodproc.iff → 2× preserved POSI chunks).
These are byte-verbatim slices of real original IFFs from the .far archives — not
synthesized. /tmp is volatile (cleared at boot), so regenerate before any gate run:

    python3 tools/iff-dump/make_probes.py

R163 also adds two standalone engine probes:

- `tools/jit_scope_probe` proves that JIT scopes 12/28/29 emit the same TreeAd
  read/write/compound-write mapping as `VMMemory`, with JIT cache version 2.
- `tools/neighborhood_data_probe` proves TS1-only, read-only, signed NGBH words
  1..15 and JIT delegation across TemplateUserData plus UserData1–8 (9/9).
  The companion corpus scan finds 30 reads, 0 writes, all at words 1/2.

R164 adds `tools/iff-dump/r164/r164-saved-frame-compat-scan.py`. Against the
owner's local House 5, Objects, and Global files it pins 245 instances / 522
saved frames, 12 missing-routine frames across 11 objects, four out-of-range
Counter frames, and the five current replacement-main families. The generated
report contains metadata/hashes only and is deterministic from any cwd.

R165 adds `tools/iff-dump/r165/r165-school-bus-canon-scan.py`. It pins the
owner's House 5, Objects, and Global hashes; Cassandra's object/GUID/neighbor,
age, job, and grade fixture; the 09:00–15:00 school tuning; both exact
`CarPortal.iff:4119:4` create call sites; the SchoolBus/CarGlobals relationship;
and the successful attendance and missed-school state laws. The generated
report contains metadata/hashes only, emits no original resource payload, and
is byte-identical from the repository, another cwd, and optimized Python.

R166 adds two deterministic metadata tools and one standalone actual-dispatch
probe. `r166-original-engine-decode.py` resolves mode 12 through the owned PPC
PEF jump table and pins the fixed-64 rotation law. The corrected production-
format IFF scanner finds 47 calls, 22 unique call-site signatures, and 21 unique
routine instruction-payload hashes. `tools/generic_ts1_call_probe` invokes the
real primitive handler for all 16,384 `(x,y,rotation)` combinations:

    dotnet run --project tools/generic_ts1_call_probe/generic_ts1_call_probe.csproj -c Release

R167 adds another deterministic PPC decoder and production-format corpus
scanner. `r167-original-engine-decode.py` follows mode 13 through
`CleanupPeople` and `cXPerson::Cleanup`; the scanner proves there are exactly
two owned-corpus callers, both PhoneGlobals routines that first set Stack
Object ID to self. `tools/generic_ts1_abort_probe` invokes the real primitive
handler and includes a synchronous Queue Skipped callback that mutates the
actor's stack, proving the required active-before-queue ordering:

    dotnet run --project tools/generic_ts1_abort_probe/generic_ts1_abort_probe.csproj -c Release

R168 adds `r168-original-engine-decode.py`, a production-format corpus scanner
with strict IFF termination, and `tools/generic_ts1_radio_probe`. The decode proves mode 14 is an
unconditional signed Temp0 write to global 31 with true exit. The scan finds
18 calls in 13 IFFs, 11 unique signatures, 11 routine hashes, and 22 exact
incoming Temp0 setup paths. The probe decodes raw byte `0x0e`, resolves the
registered opcode-1 handler, and checks signed boundaries plus preservation of
all other globals and all Temp registers:

    dotnet run --project tools/generic_ts1_radio_probe/generic_ts1_radio_probe.csproj -c Release

Gate logs also carry `[SimAnticsExc] …` lines from `VMThread.cs`, throttled to one
per 600 ticks. R164 closes the saved-continuation diagnostic class: the TS1 importer
atomically rejects any stack containing a missing/empty routine or out-of-range
instruction, clears its interaction state, and resets that entity once on the
current main before the first tick. The R164 10:15 soak contains zero
`SimAnticsExc` and zero `bad-routine-frame` records.

Families (each pins IFF canon or an IFF-factual runtime behavior — see ../PARITY.md rows):

- **Gameplay loop:** corpus (module BHAV counts), lot/load (Goth 5 loads, 3 avatars, 0 errors), motive/mood (decay + formula; R129 note: the sampler fails on any single sample beyond the ±3 tick-boundary tolerance — one observed race in r129p1 when uitt's heavier catalog pass shifted frame timing by one decay tick; green on re-run, check deliberately untouched), brainlive (original PersonGlobals idle/personality brain EXECUTES in the live VM — 9,013 PG frames / 16 ids, canonical 8303/8301/8229/8275/8219).
- **deathchain/savesim:** original death/motive-failure chain IFF-literal 20/20 + closure + wiring + engine-driven entry + PersonType bridge + shipped-save death persistence (8 IsGhost) + shipped-save career/skill/relationship persistence (R80/R81). Residuals: natural-death decision ids were not observed in the R77 window, so decision→ghost remains an end-to-end trace.
- **carseek / carreturn / schoolreturn:** `carseek` is the default morning soak and pins the original portal head, bookmark, outbound 4105/4102 path, and one `CarJunk.iff` multitile group created at `CarPortal.iff:4106:20`. Opt-in `carreturn` runs House 5 through the complete Science shift and 75 post-end sim-minutes; it requires exactly one outbound and one return create (`4121:19`), exact +460/+230 committed budget callbacks, and promotion level 0→1. Opt-in `schoolreturn` runs Cassandra through the original 09:00–15:00 school day and requires the two exact `SchoolBus.iff` creates at `CarPortal.iff:4119:4`, one outbound parent `4115:10`, one return parent `4116:27`, at most one visible bus group, natural removal after both legs, the original child hidden/out-of-world with PersonData[87]=1 at school, the same logical child visible/in-world with PersonData[87]=0 at home, and a successful-day grade delta within ±1. Chance Cards are now traced by the R222 `chancetrace` opt-in; the remaining alternate career/school outcome branches stay residuals.
- **R163 VM semantics:** `persondata` pins 101-word normalization, TS1 raw aliases, tail zero-extension, original-tree execution, and neighbor inheritance; `travelinv` pins GUID 10/11 replacement through production modes 5/6; `freewillvar` pins distinct dynamic autonomous candidates by identity, Param0, order, score, and callee.
- **R164 saved continuations:** `savedthreads` requires a real House 5 IFF import, exactly 15 atomic continuation repairs, exact owner GUIDs and replacement root mains at IP 0, reset queue/interrupt state, and no unresolved or out-of-range live frame. Generic TSO/network snapshot loading is unchanged.
- **R166 Generic Sims Call 12:** `genericcall12` pins the recovered 64x64 rotation law at five sample/corner tiles across all four rotations, then invokes the production handler against a live lot object and the current world rotation. Because this check is dispatched from `RunCorpus`, focused runs must include `-autotest-opts lot,corpus,genericcall12`. The no-rendered-world TopLeft fallback is deterministic port behavior, not recovered original semantics.
- **R167 Generic Sims Call 13:** `genericcall13` pins the exact two PhoneGlobals call sites and their self-target setup, then dispatches the production handler over isolated Callee-only, IconOwner-only, and unrelated queue records. It requires only the unrelated UID to remain, Temp0 preservation, and `GOTO_TRUE`. Focused runs must include `-autotest-opts lot,corpus,genericcall13`.
- **R168 Generic Sims Call 14:** `genericcall14` pins mounted `Stereos.iff` BHAV 4118's Param0 setup/mode-14 pair and `piano.iff` BHAV 4117's literal-2 setup/mode-14 pair. It decodes raw operand `0x0e`, verifies opcode-1 registration, dispatches eight signed values from -32768 through 32767, and requires only global 31 to change, every Temp register to survive, null/nonzero Stack Object independence, and `GOTO_TRUE`. Focused runs must include `-autotest-opts lot,corpus,genericcall14`.
- **Relationship store:** relation (NBRS 1:1 + write clamp −100..100), rel-key/rel-mode (UseNeighbour resolves NEIGHBOUR id, modes 0–3), relact (IFF relation-write sites).
- **Censorship:** censor — IFF-literal triggers {0,1,3,128}, live exec 0→3→0, render pair.
- **Names/audio:** names (saved names kept), audio (HITVM + 4479-event corpus + live HITTV thread), jobs (career table 1:1).
- **IFF-literal data pins** (the bulk — see ../PARITY.md): npcinfo,career,freewill,personality,motiveinit,skills,motiveact,money,ttab,ttas,opcodes,callgraph,globalcalls,catalog,snd,iff,objd,ctss,strs,consts,bhvi,bhop,dgrp,slot,operand,opmx,chunks.
- **UI parity:** uipal (ORIGINAL-UI 13-anchor palette) + uidump (original toolbar backdrop pinned at the widget-tree level — OriginalPanelBack present+sized (min(804,ScreenWidth-220),100)+visible; wobble-proof: never pin absolute rendered hex, render colors wobble run-to-run, IFF steel R observed 0x5F..0xA7; pixel steel-family counts are evidence only) + headless live-UI render to <UserDir>/uidump-lot.png + loadscreen (R83 — original start/loading 'Go' screens mount 1:1: member name + byte length + BMP dims, 19/19 from UIGraphics.far) + uichrome (R85 — original live-toolbar chrome: the 5 category buttons Mood/Job/Personality/Relationship/people + Greenbars/Redbars motive fills mount 1:1 IFF-literal (member + byte length + BMP dims, 7/7) and the rendered control TREE carries original-dimension art (category button 128x39, motive bar 27x25); the tree check discriminates IFF from the 65x65 modern fallback) + uitoolbar (R86 — original live-toolbar chrome: LIVE house tab, OPTIONS tab, PAUSE, EXIT, OBJECTS + the LiveGadget gauge mount 1:1 IFF-literal (member + byte length + BMP dims, 6/6) and the rendered control TREE carries original-dimension art (R141 CORRECTION: live-MODE button = people.bmp 200x50 4-state sheet self-cropped by UIButton — House.bmp 108x30 is the HOUSE TAB kHouseBtn, the R86-era pin codified the port's art misuse; gauge 108x100); discriminates IFF from the modern mmbbox fallback) + uicur (R87 — all 51 ORIGINAL UIGraphics.far .cur cursors, raw FAR stored name + byte length + sha256, byte-verbatim, AND the ORIGINAL LivePerson.cur (326 bytes) is IFF-first live-mounted into the runtime CursorManager on every platform (macOS GameFacade.Linux skip removed); discriminates IFF from the never-mounted Arrow fallback) + uiglyph (R87 — all 45 ORIGINAL .ffn glyph-tables, raw FAR stored name + byte length + sha256, byte-verbatim; original glyph-table bytes pinned; R96 — the pinned tables must also PARSE and RENDER: the default font is captured canon-verified on its first FAR read, parsed as FNTF (224 glyphs, 256x224 4-bit atlas, exact 'A'/'e' char records), Measure("The Sims")==88, and rendered headlessly to a target with real ink and >=8 antialias alpha levels — the ORIGINAL text render-style; R97 adds the SECOND live table: the caption font Fonts\variablesans_10.ffn (224 glyphs, 256x121 atlas, exact 'L'/'o' records, Measure("Lot 20")==43) with its own headless caption render — the table the neighborhood hover lot captions draw with; R98 adds the THIRD live table: the bold money font Fonts\variablesans_12_bs.ffn (224 glyphs, 256x204 atlas, the '§' U+00A7 record, Measure("§20,500")==71, headless render) — the table the lot-screen money readouts (UIMoneyPanel + UIDesktopUCP) draw with; R99 adds the FOURTH live table: the small toolbar font Fonts\variablesans_11.ffn (224 glyphs, 256x152 atlas, '1'/':' records, Measure("12:05 AM")==69, headless render) — the table the time/friends/floor readouts draw with on both Desktop and touch paths; R100 adds the FIFTH live table: the dialog font Fonts\variablesans_09.ffn (224 glyphs, 256x115 atlas, 'S'/'!' records, the engine string "Sorry only one Nessie at a time." measuring 201 under the shipped 5px-space convention, headless render) — the table the cheat-bar response messages draw with; R106 adds the SIXTH live table: the title font Fonts\variablesans_08.ffn (224 glyphs, 256x107 atlas, 'T'/'N' records, Measure("The Sims")==54 under the shipped 5px-space convention, headless render) — the table the options-panel free-will title draws with) + uiboot (R88 — the ORIGINAL boot/logo screen kSimsLogo (Res_Other.RT id 9003 = Other\setup.bmp + 17 language variants, 800x600x24 with the plumbob+wordmark art baked in) pins 1:1 (name+bytes+sha256+dims) AND IFF-mounts LIVE as the boot loading screen: Simitone logo/diagonal stripes/elastic bar hidden, engine-drawn original-style progress bar active — the original templates declare NO bar or logo-control asset, so the original bar is engine-drawn, and so is this one; the R83 kNeighborhoodGo GO screen then mounts at load-complete; R118 STRENGTHENED: the Simitone loader chrome is DELETED outright (musical-notes bar/wordmark/diagonal stripes/MSDF status labels/update-check dialog removed, window title 'The Sims') and the boot art mounts DIRECTLY from UIGraphics.far IN THE LoadingScreen CTOR — frame one, no pre-mount window — with the extracted member bytes required sha256-IDENTICAL to the R88 canon (directFrameOne; the only fallback is a plain navy #000029 fill, never Simitone art)) + uilogo (R88 — the ORIGINAL "The Sims" logo stamps kNghSimsLogoEn/SP/FR/GR/JP + kDowntownCredits + kStudiotownCredits pin 1:1) + uianim (R88 — the ORIGINAL animated frame families pin 1:1: Unleashed waves + NonUS, TS1.0 frame-deltas + Loc, Downtown/Magicland/Vacation water, Magicland fog + balloon, Studiotown car — 74 frames) + uinbhd (R89 — the ORIGINAL neighborhood screens are LIVE-ANIMATED: the 11 screen members (6 backdrops + vacation port/trees + nessie kNess1-3) pin 1:1 (R89ScreenCanon, parsed from Res_Nbhd.RT by tools/iff-dump/make_r89_canon.py) AND every animation family — 12 families, 71 cycled frames — mounts with canon frame counts + live texture dims and CYCLES through UINeighborhoodAnimationLayer, the same control the screens use (proof = per-family AdvancesByFamily delta after StepFrame, which is what the live Update drives; the startup Community panel runs the completed families live, incl. nessie). R90 engine-scan: the original placement/timing was RECOVERED from the original engine binary itself (game-data/The Sims/The Sims Complete, PPC PEF; anchor scan tools/iff-dump/scan_pef_ppc.py, recipe + tables in tools/iff-dump/r90/RE-ENGINE-CONSTANTS.md): nessie is a cheat easter egg (DoNessie x3 classes, "Usage: nessie. Same as SimCity.") — the UL CheatCallback spawn (118,519) is LIVE-mounted on the Community screen and ENGINE-pinned in this check with binary-offset provenance (cross-validated against the R89 art river band); Magicland waves position (0,62) ENGINE-CONFIRMED (cMagiclandSprites::DoWater, frame+1 mod 6 every 5th tick) and also ENGINE-pinned; engine-derived pins are a separate evidence class from IFF canon. Car (6 carLaneInfo lanes, 13..5 cars, world larger than the backdrop) / balloon (wind-walk RNG) / fog (21 drifting clouds) have their original constants DOCUMENTED; R91 ports the car system LIVE — the Studiotown screen now runs UINeighborhoodCarLaneLayer: all SIX engine lanes (54 cars) at engine-literal coordinates, each car's bitmap picked once via Random()%18 (the 20 frames are STATIC variants, not a cycling animation — variants 18/19 never randomly drawn), movement driven by the engine's rare-event structure (%10/%40 gates; base-1px interpretation disclosed); this check pins the lane table by literal re-declaration + the %18 modulus + the 20-frame mount + movement invariants (phase advances along dir, cars stay in-lane) + the 54-car total. R92 ports the balloon/fog systems LIVE — the Magicland screen now runs UINeighborhoodCloudLayer (all 21 engine CloudInfo clouds: interval-gated drift x-=drift/y+1, wrap at x<=0 to the column with a fresh rand%3 fog bitmap, frame sentinel wobble +2/-3 at x>100/x<30) and UINeighborhoodBalloonLayer (the 16 balloon bitmaps are the WIND-STATE frames: seed%35 resets wind to 2, ladder caps 6/10/14/16, clamp 15; draw at (300+sway, tick) with sway=int(trig(seed%1440)) — the /35 and /1440 magics pinned, the MathLib amplitude modeled+disclosed); this check re-declares the cloud table (Y ladders 60+16j / 60+16k / 60+10k for records 0-7 / 10-15 / 17-20, defaults for 8/9/16) + drift/wrap invariants (wrapped=21/21) + balloon init{seed 1, wind 400}/walk/x-band pins. R95 pins the last provisional WATER positions: the Downtown water + Vacation waves families carry explicit Vector2(0,0) — engine-literal from the ctor li-0 stores into the TSPaint position fields (DT this+308/+312 @0x400308/0x400314; VI this+316/+320 @0x442cc0/0x442ccc) — checked via expectPos alongside Magicland's (0,62); UL waves + TS1.0 deltas stay disclosed-unresolved (self-drawing SpriteSlots). R93 ports the nessie CHEAT live: the R89/R90 always-on Community family is replaced by UINeighborhoodNessieLayer — dormant until the panel's Ctrl+Shift+C cheat bar submits "nessie" (engine CheatCallback 0x4652c0: once-only flag, "Sorry only one Nessie at a time.", spawn li pair 118/519) — then the DoNessie 0x462090 machine runs: kNess1, sfx+kNess2, kNess3, swim left-2/down-1 per tick until x==60 (29 ticks), dive kNess2->kNess1, END clears the flag (cheat re-fires); this check walks the whole machine literally (state sequence, frame mapping 0/1/2/1/0, swim endpoint (60,548), once-only + end-clear semantics) + uidtips (R117 — the ORIGINAL UCP tooltips: UIText.iff STR# 138 'VCtlTips' (chunkSize 28762, 432 raw / 24 English entries, format -3, full-chunk sha256 d3dc987e…; ALL 24 English entries pinned byte-verbatim incl. the double-space 'Zoom In  +'/'Zoom Out  -' and the keyboard hints 'Live Mode - F1'/'Pause - P'/'Normal Speed - 1' the port paraphrases dropped) disk-pins AND the UIDesktopUCP ctor assigns all 19 tooltips (15 buttons + 4 speeds) from that table — constructed through its own ctor, post-uidump; the eyedropper stays port-authored (no original — zero corpus hits); the tooltip renderer was FSO.UI-side modern at R117 (OriginalGlyphFont cannot live in the submodule) — CLOSED R119: the OriginalVectorFont subclass lives in the client and dispatches through the MSDFFont base, so tooltips now draw original glyphs (see uivfont); 240/241 sort-button tooltips are mapped-UNUSED: the port's relationship/inventory subpanels assign no tooltips at all). + uivfont (R119 — the ENGINE-WIDE DEFAULT TEXT RENDERER is the ORIGINAL variablesans .ffn family: GameFacade.VectorFont is OriginalVectorFont, an MSDFFont subclass in the client — the framework grew additive hooks (Draw/MeasureString/GetAtlas virtual, protected parameterless ctor, virtual SelectForSize with base identity; TextStyle.Size re-selects VFont) so MSDF/TSO behavior is unchanged; one instance per regular table 07/08/09/10/11/12/14/16/18/20 with VectorScale=11.5/px → Scale==1.0 EXACTLY on ladder sizes (native bitmaps), a root instance delegating direct callers to a _12 reference table (0.72→0.690); every label/button/dialog/list/text-edit/tooltip/headline renders original glyphs — the R117 tooltip-renderer residual closed via base-class dispatch; ladder ties→smaller, >20→scaled _20 (_48 is a 16-glyph no-digits wordmark font), bold/_bs + _s stay per-panel, MSDF remains only for Edith; ladder metrics pinned from the R87 canon (r119-vfont-canon.txt; corrects r96's stale _07 256x84→256x83); check = root-type + 19-point ladder + Size-10 routing at exact Scale 1.0 + 'The Sims' widths across all 10 tables + defaults routed + headless draws through the engine override) + uimpanel (R120 — desktop bottom-bar composition: the open MainPanel must sit at/right of the DesktopUCP's right edge (UCP-left/catalog-right, the original's one-bottom-bar composition; the old fixed X=179 put the open panel 136px INSIDE the UCP over the speed pills/time/options — reproduced headlessly at 1136x908 in the gate's own uidump), BOTH surfaces fully on-screen (the clipped-UCP class), and the BUFFER INVARIANT ScreenWidth x DPIScaleFactor == Viewport.Width/Height — the resize-path drift class; Window_ClientSizeChanged now derives everything from the actually-allocated viewport and always recomputes logical units). LOOK residual: original layout montage in-lot is still modern-placed (text RENDER-STYLE is now original engine-wide via uivfont; per-panel twins keep exact original placements). + uibuy (R122 — the ORIGINAL buy-mode catalog: UIText.iff STR# 150 'BuyModeCatalogSortTips' disk-pins chunk-sha256-verbatim (960 raw = 48 English = six 8-entry sort blocks: home rooms [0-7] + home functions [8-15] in EXACTLY the kBuyR*/kBuyF* plaque orders, Downtown/Vacation/OldTown/StudioTown [16-47] with their Unused slots) + STR# 200-207 function-subsort tables (shas + entries [0-3] + the EMPTY reserved [4]/[5]) + STR# 210 extras (Back/Other/All/Pets/Magic) + STR# 154 [0]/[1] paging pair; all 80 cpanel\Catalog art members the port mounts pin name+len+dims+sha256 with FULL lowercase-path keys (subsort basenames repeat across the 8 family dirs) — 16 main plaques, 8 RoomSubsort sheets, 32 family subsort sheets + Pets/Magic/Other/All, 16 expansion plaques per the res_cpanel.RT aliases (kBuySTfood→BuyDDining.BMP etc.), ScrollLeft/Right.BMP, ThumbTemplate + 1Frame; pins REGENERATED from the FAR bytes by tools/iff-dump/r122/regen_gate_shas.py, never hand-typed; LIVE: BUY mode must carry the canon function order with original plaque art and caption TOOLTIPS (the table's own label calls them sort tips), the subsort row reads STR# 200 names + Other/All on cropped 36x36 sheet frames, the room/function page toggle works (rooms = 150 [0-7] plaques; the room panel filters on the ORIGINAL RoomFlags bits — internal order Kitchen,Bedroom,Bathroom,Living,Misc,Dining,Outside,Study, DATA-DERIVED from live-catalog anchors 'Stove'r0x01/'Bed'r0x02/'Toilet'r0x04/'Bookshelf'r0x80, map CanonRoomToFlagBit pinned empirically: Kitchen contains appliances, Bathroom plumbing — and shows the 8 canon function subsorts), the Downtown row mounts the RT-mapped plaques with captions 150 [16-19], and item thumbnails use the 45x45 ThumbTemplate frames; prior mode restored. Two latent engine bugs fixed via this check's first runs: IffFile.InitHash crashed on lazily-loaded chunks (null length arg; some chunks carry NEITHER buffer) and UISubpanel.Kill NRE'd on never-parented panels. Disclosed residuals: BuyBack.bmp (kCatalogBck) has no member on this disk, paging placement/2-page model/item-cell composition are engine choices, build-mode subcat buttons still port icons, STR# 160 'CatalogRatings' archived for a future description round). + uiopts (R121 — the ORIGINAL options screen: UIText.iff STR# 145 'optionstrs' disk-pins chunk-sha256-verbatim (84 English entries, format -3) INCLUDING the original's own '(x;y)' LAYOUT DIRECTIVES (mains Save(29,6)/Neighborhood(88,6)/Quit(159,6)/Graphics(29,58)/Sound(89,58)/Play(151,58), popup anchors (200,20)/(200,28), radio anchors (480,30)/(485,30), (521,24) — all parsed from the DISK table); all 30 control-art members pin name+len+dims+sha256 in the TS1Global mount (6 four-frame RLE8 main buttons + checkbox/radio/slider/tutreset/options-back + all 18 PopupOpt*.bmp About panels incl. PopupExportHTML/PopupResetTutorial — the RT-mapped names for kPopupOptHtml/kPopupOptReset; pins REGENERATED from the FAR bytes by tools/iff-dump/r121/regen_gate_shas.py, never hand-typed); LIVE: OPTIONS mode must mount UIOriginalOptionsPanel whose 6 buttons carry disk-verbatim captions at the parsed canon positions (live relative deltas == canon deltas) on pinned-dims original art; the Graphics/Sound/Play sub-screens carry their canon rows (Low/Med/High shared labels), the sound sliders track the REAL GlobalSettings volumes, Free Will tracks VM.FreeWillEnabled and toggling it flips VM+setting, toggling Edge Scrolling flips the live setting (both restored), and the About popup opens at the canon directive with title+body verbatim; LIVE mode restored after. Port-wide fix discovered by this check: the FAR EntriesByName lookup is CASE-SENSITIVE vs a chaotically-cased manifest — UIOriginal.EnsureResolved now falls back OrdinalIgnoreCase via GetFarEntries. Disclosed residuals: persistence-only option keys pending engine readers (Shadows/Lighting/InterfaceFX/detail/AutoCenter/SimInBack/QuickTips/AutoSnap/LivePIP/ExportHTML), OptionsBack.bmp has no member on this disk (subpanel chrome stays), sub-screen placements/caption placement/popup scale/slider fill are engine-side choices). + uidesc (R124 — the ORIGINAL catalog item DESCRIPTION panel: UIText.iff STR# 160 'CatalogRatings' disk-pins chunk-sha256-verbatim (400 raw = 20 English x 20 langs; ALL 20 entries pinned — [0..6] 'X: %d' motive formats, [7..13] '+ Skill', [14..19] usage flags with NO corpus data — the r124 negative-space finding: MiscFlags all-zero corpus-wide, no OBJD field encodes usage restrictions, CTSS = exactly name+description; pins REGENERATED by tools/iff-dump/r124/regen_gate_shas.py, never hand-typed) + both plaque art members (kCatalogPopupBack = cpanel\Backgrounds\PopupInfo.BMP 559x127 + kCatalogPopupBackTiles = PopupInfoTiles.bmp 36x36, the border-carrying vertical body fill derived from the RLE8 pixel structure) pin name+len+dims+sha256 and mount at canon dims; LIVE through the REAL UIBuyBrowsePanel.Selected flow (driving the real eyedropper-path subsort-row collapse): name == CTSS[0] (the catalog's own DisplayName read), price == '§'+OBJD price, a motive line == the DISK STR# 160 format with %d substituted VERBATIM (the old code stripped ': %d' and re-appended '{0}'), skill lines == '+ Skill' for the set OBJD RatingSkillFlags bits (corpus-validated: chess=Logic 0x0004, guitar/easel=Creativity 0x0010, mirrors=Charisma 0x0020, computers=Study 0x0040; ratings fields 80-86 are SIGNED — coffee Bladder -8), NONE of [14..19] appears, description non-empty, FullHeight grows past the 127px plaque into the tile fill. Rating fields 80-86 sit in exactly the caption order; STR# 159 'ObjectTTs' decoded + archived — live-mode hover tooltips, a future round.) + uitt (R125 — the ORIGINAL live-mode OBJECT HOVER TOOLTIPS: UIText.iff STR# 159 'ObjectTTs' disk-pins chunk-sha256-verbatim (180 raw = 9 English x 20 langs; ALL 9 entries pinned by tools/iff-dump/r125/regen_gate_shas.py, never hand-typed); the hover shows the entity's NAME (VMEntity.ToString = MultitileGroup.Name -> CTSS[0] -> OBJD label) when interactions are available, else the classified REASON — pure TTAB data (TS1NoChild 0x10 / TS1NoAdult 0x40 / TS1AllowCats 0x200 / TS1AllowDogs 0x400, the exact bits VMThread.CheckTS1Action rejects on) over the RAW tree tables (GetPieMenu drops flag-rejected entries); zero-interaction clicks show the reason too (dead click removed); LIVE check drives created out-of-world entities of the self-verified corpus anchors ('Persian Plush Pet Bed' -> [6] pets, 'Critter Condo' doghouse -> [4] dogs, 'Scratcheriffic Scratching Post' -> [5] cats) through the ENGINE's own TTAB decoder + the active avatar -> [3] + the CTSS name path; kids-only = ZERO masters corpus-wide (survey's ToyBox row corrected — no such file/chunk exists), so [2] stays wired but anchorless; R129 closes the loop ENGINE-EXACT: the classifier is the decoded ladder (cObjPickerTool::Release + the ObjSelector audience predicates, tools/iff-dump/r129/) — fixed rung order over OWN-table aggregates (Debug 0x80 entries skipped, ≥1 live entry required), [3] = pet self-hover, [8] pet-only, [0] the human terminal default; the check adds a catalog-scanned adults-only -> [1] and plain -> [0] live pair plus the engine-proven human-self impossibilities ([3]/[8]).) + uibuild (R126 — the ORIGINAL build-mode catalog subcategory buttons: STR# 139 'BldTips' disk-pins chunk-sha256-verbatim (288 raw = 16 English x 18 langs; ALL 16 entries pinned by tools/iff-dump/r126/regen_gate_shas.py, never hand-typed); the 11 tool plaques cpanel\Buttons\toolBtn*.bmp (kToolBtn 64..77) pin name+len+dims+sha in the live FAR mount and load as SINGLE-frame FULL images (toolBtnTerrain 156x39 = 4x39 would be mis-cropped by Frame() — ResolveSubSortArt is mode-aware); LIVE through the REAL ctor->InitCategory button loop: all 3 build categories' rows carry the STR# 139 tooltips and pinned plaque dims per slot; [11..13] Hand/Undo/Redo canon-pinned but surfaceless in the port; BuildBack.bmp missing from disk and no canon plaque for the 3 main build tabs — both disclosed; the port 3-category hierarchy kept (original evidence suggests a flat paged tool list). + uibldt (R127 — the ORIGINAL build-mode terrain tool items: the 8 mounted members (4 kBldSbTl* cell plaques + 4 kBldPopup* thumbs, RT verbatim) pin name+len+dims+sha through the live FAR mount, replacing the TSO-only UIFileIDs provider whose IDs cannot resolve on TS1 data (cells rendered blank 1x1) and whose Grass cell mounted a wrong-asset placeholder; LIVE via the REAL InitCategory(18, build:true) flow: FOUR tools in RT popup order — the port's combined raise/lower tool split back into the original Up/Down pair (FreeSO UITerrainRaiser honors parameters[0]==1 = lower mode, TSO behavior unchanged) — each on the canon provider with plaque dims 144x33/144x34/156x33/144x32 and 145x103 popup thumbs; plaques render at NATURAL size (provider-typed NaturalIcon) with the fake price hidden; names stay port-authored (r127 scan: no tool-name strings in ANY UIText STR#, plaques pictorial per RLE8 reads); roof template/pitch family corpus-documented for future rounds — CLOSED R130 + uiroof: STR# 147 'roofpanelstrs' disk-pins chunk-sha256-verbatim (18 English entries: the 4 pitch names, layout directives, pager captions, page titles, lore — the ORIGINAL panel strings) + the 7 roof members (kRoofPatternTemplate 180x45 4-frame sheet, 4 kBldSbTlRoof* plaques, both popups) pin through the live FAR mount; LIVE via the REAL InitCategory(17, true) flow: the 4 PITCH sub-tools lead in the engine panel's display order (Steep, Medium, Shallow, Flat — the STR# 147 directives' descending column) on UIRoofPitcher with RT ids 3902/3901/3900/3904, engine-mapped pitches 0.99/0.66/0.495/0.0 (the engine's exact slope ratios 1 : 4/3 : 2, medium anchored at the port default — engine decodes: float table 0x5a3bbc via TOC -20260, RenderRoofPolys DIVIDES by pitch so shallow is the largest engine value), then the 18 pattern swatches on UIRoofer with 32x32 icons composed inside the ROOF'S OWN 45x45 RoofPatternTemplate frames, 145x103/133x103 popup thumbs, STR# 147 names+lore). R131 EXTENDS uiroof with the engine's PAGED roof panel (cWinRoofPanel SetCurPage; kCatalogPrevPage 100 / kCatalogNextPage 101 = cpanel\Buttons\ScrollLeft/ScrollRight.bmp 36x49): page 0 'Roof Pitch' (STR# 147 [14]) = the 4 sub-tools, page 1 'Roof Patterns' ([16]) = the swatches, buttons clamped + disabled at the bounds; FullCategory keeps the flat order for names/search while FilterCategory picks the page. + uigauge (R131 — the ORIGINAL live-tab MOOD GAUGE, engine id kLiveModeGauge 4911 decoded from cWinPeople::Init 0x28eee0 + TSPaint 0x28d5fc-0x28da68 in The Sims Complete): STR# 154 'MiscStrings' disk-pins chunk-sha256-verbatim + the ratings family [12..15] House/Friend/Job/Mood Rating; 9 art members pin name+len+dims+sha (LiveGadget 108x100 backdrop, Greenbars/Redbars 27x25 — 24bpp, bpp now sha-pinned, TrackingTarget 45x45 corpus, ScrollLeft/Right, Lev1/Lev2/LevRoof view-level corpus); LIVE: UIMotiveSubpanel hosts UIOriginalLiveGauge left of the needs with the [15] 'Mood Rating' caption, art at engine dims, and the ENGINE fill mapping verified through the eased value — height = min(|mood|,100)/4 px (25 px = full bar art height), polarity green >= 0 / red < 0 (the engine's rating is sign-flipped: base − value), cap at |mood| = 100. Disclosed: the easing constants live in TOC globals (unrecoverable statically; port eases 0.15/frame), the gauge's second channel source + kTrackingTarget surface + LiveBack.bmp (missing from disk, same family as BuyBack) + the kLevel1/2/Roof view-level toolbar remain residuals; port-chosen pixel positions within the engine composition.; R135 REPLACES the eased-fill interpretation with the EXACT ENGINE LAW recovered from the binary's const pool via TOC resolution (file 0x5a45ec): rating = clamp(symround(11*(100+mood)/201 - 5), -5, +5), rating<0 red else green, height = |rating|*5 px — pinned at 10 check points; the 'easing constants' residual is CLOSED (the engine chain is memoryless — no ease state exists).) + uirate (R132 — the People-panel RATINGS family: the 3 pie strips kHouseBars 4800/kJobBars 4801/kRelBars 4802 (63x26) + the subpanel bar sheets kHouseSubpanelBars 4900 (40x16)/kJobSubpanelBars 4901 (40x11)/kFameSubpanelBars 4822 (80x16) + their k*SubBarsOff backdrops 4919/4920/4823 pin name+len+dims+sha256 through the live FAR mount (pins REGENERATED by tools/iff-dump/r132/regen_gate_shas.py, never hand-typed; TrackingTarget stays R131-pinned); the ENGINE fill law — clamp [0,100] then 4*(value/10) px, integer division (cWinSubpanelHouse::TSPaint 0x2a0458-0x2a06a0) — asserted as pure math AND end-to-end through the control (Value=73 → FillWidth 28); LIVE: the restored original House tab (kHouseBtn 4502, live category 5) mounts STR#154 [12]/[13] 'House Rating'/'Friend Rating' captions on 40x16 art, the Job tab mounts the original [14] 'Job Rating' bar on 40x11 art, and the follow-Sim crosshair toggle (UIOriginalTrackButton, 45x45 pinned art) carries real on/off semantics against WorldState.ScrollAnchor (toggle → IsTracking true, toggle again → false — the r132p1 honest FAIL proved the lot has an avatar and the toggle works; manual camera input cancels, engine-faithful). Disclosed residuals: the HouseStats score COMPUTATION (0x8bba8-0x8c174) undecoded — House Rating mounts at 0, Friend uses FamilyFriends clamped as a port-side proxy; pie-menu bar surface + Fame/ReportCard/SweepMeter subpanels + kJobIconGeneric/kJobPopupComposite gadgets unported.) R133 CORRECTS the uirate house-caption reading: STR# 154 [12]/[13] are neighborhood-scope — the House panel's captions moved to 'uihouse' on the canon tables. + uihouse (R133 — the House tab on the original's own terms: Live.iff STR# 133 'HouseSubpanelLabels' + 135 'HouseSubpanelPopupText' + 138 'HouseSubpanelSize' disk-pin byte-verbatim (full-chunk sha256s + all 10 labels + the 9 popup titles + Small/Medium/Large; pins REGENERATED by tools/iff-dump/r133/regen_gate_shas.py); the DECODED lot-size ladder (House::GetHouseStats 0x8c29c-0x8c2cc: dimension <40 Small / <50 Medium / >=50 Large) asserted as pure math; LIVE: the subpanel's header/five score labels/value-line formats equal the disk canon, the four value lines carry REAL computed numbers — square feet = the sum of enclosed room areas in the port's flood-fill room map, bedrooms/bathrooms = enclosed rooms whose entities hold an OBJD RoomFlags bit-1/bit-2 object (the corpus's own stated rules), lot word = 138[ladder(Architecture.Width)] — cross-checked against an independent Compute + room-map presence. The five score bars mount at 0: the engine's score constants (TOC globals + gamestate ladder tables) are statically unrecoverable — disclosed residual.; R134 UPGRADES the disclosure to a PROOF — the exhaustive ladder hunt found no static copy of the score tables anywhere in the binary (no monotone small-int array >= 7 exists), and EXTENDS the check with the FillInObjectStats aggregate invariants: Working+Broken == ObjectCount under the same deduped filter, the indoor/outdoor value split >= 0, and FurnishingsValue/YardValue/BrokenObjects/HasPool identical on an independent recompute. The aggregates follow the decoded object walk: value = MultitileGroup.InitialPrice (the engine's GetCurrentValue; TS1 wear never advances), broken = the engine's own RepairState >= 600 repair threshold.; R135 wires the UPKEEP BAR LIVE on the decoded law int(100*clamp(dividend/2^31,0,1)) — the r=0 normalization disclosed — with gate equality against the independent compute and the nothing-broken=>100 invariant. R136 wires the LAYOUT BAR LIVE on the route-history law decoded from the GetHouseStats tail via a symbolic interpreter (tools/iff-dump/r136/): House+48..+92 = six (total,flagged) movement-sample generations (AddLayoutTick 0x8c4f0, one call site 0x10c2d8 every 10th movement tick per Sim; the rotation in EnterLiveMode case 220; ClearRouteHistory 0x8c430 from the house-rebuild paths), and the exact law layoutScore = (int)(100.0*clamp((den-sub)/den,0,1)) with den = the bit-31-based single-slot magic and sub = the flagged sum — the seed global is BSS-proven unrecoverable, so the port normalizes by the totals under the engine's own max(sum,1) division guard, disclosed: (int)(100.0*clamp(1f-2f*flagged/max(1,total),0,1)); the math block pins the law over synthetic feeds (fresh=>100, (10,0)=>100, (10,2)=>60, (10,5)=>0, (10,8)=>0, the max-guard, Clear resets) and the live block samples the real lot, recomputes, and requires ScoreBars[4]==Stats.LayoutScore==an independent inline recompute within [0,100]. R137 CORRECTS the flag channel to the decoded semantics (cXPerson::Simulate 0x10bff0: the flag = GetCurrentRoute()!=NULL — the Sim currently holds a route, gated by awake+visible+Motives[11]>=0, sampled every 10th Sim-tick): each sample pass counts every live avatar once (population) and flags those moving (VMAvatar.Velocity!=0), with a VM-clock gate (pause = no Simulate = no samples; awake/visible/motive gates approximated by liveness, disclosed); re-pins: flagged<=total and a same-frame second Sample adds nothing.) + uivalue (R137 — the LIVE house value: UIText.iff STR# 146 'budgetstrs' + uitext (R138 — the RENDERED-PIXEL law of the original .ffn text path; user-reported garbled/unreadable text across the board exposed a 19-round blind spot: every prior glyph pin asserted the ALPHA channel only, while OriginalGlyphFont.FromFFN uploaded the atlas white-RGB/STRAIGHT-alpha and all draw paths blend with BlendState.AlphaBlend (MonoGame's PREMULTIPLIED convention: One/InverseSourceAlpha) — any pixel with alpha>=1/15 saturated to full ink, collapsing every glyph to a solid tinted rectangle (evidence tools/iff-dump/r138/: -textdiag harness BEFORE log — alpha art clean AA'd glyphs, rgb art solid blocks; AFTER log — rgb==alpha on every probe). Fix: premultiply the atlas ink (nib*17 in rgb AND a). The check renders through the real paths into an offline target and pins: (1) direct OriginalGlyphFont.Draw, (2) the full UILabel->DrawLocalString->OriginalVectorFont UI path — on both, rgb==alpha on >=95% of inked pixels, the saturation-collapse signature (mid-alpha pixels with saturated rgb) absent, the AA ramp >=100 px, bbox sane; (3) ink composited over an opaque background — intermediate blend shades must exist (>=100 px strictly between bg and tint). Corpus facts pinned alongside: 13 English tables, 224 contiguous codes 32..255, atlas dims, flags 9 vs 11 (bold/condensed), nibble ceiling 15 vs 13 (bold ink tops at 221/255 — engine's exact /13-vs-/15 scaling UNDECODED, data-literal nib*17 kept, disclosed); STR# strings arrive byte-coded (Latin-1-style IoBuffer read) and the tables are byte-keyed Windows-1252 — canon strings render correctly; true-Unicode literals >0xFF silently drop (none ship, grep-verified). R139 RESIDUALS RESOLVED on the engine decode (cTSFont family, tools/iff-dump/r139/): (1) the nibble law is NIBBLE-AS-IS — RebuildColorTable 0x4b31d0 and the global blend LUT [TOC-28288] (BSS-proven, 42 refs shared with Spr2Decoder/AlphaBlend/water) take NO per-font input, so the flags=11 tables' ink ceiling 13*17=221/255 is CANON and the bold table's render maxAlpha is PINNED == 221; (2) the char table is 256 slots indexed by (c & 0xFF) (TSCharWidth 0x4acff0) — a CP1252 boundary map (U+201C->0x93 etc., the 27 standard equivalents) renders port-authored Unicode exactly as the original rendered the byte, pinned by MapChar equality + Measure equality + a pixel-identical render probe; (3) TSDrawChar 0x4b3420 blits native pixel extents — the engine NEVER scales bitmap glyphs, so off-ladder px requests now render the snapped table at Scale==1.0 (crisp, unscaled; MeasureString line height unchanged), pinned at sizes 13/6/12. Residual disclosed: the blend LUT's exact 5-bit per-level curve is BSS-unrecoverable (the port renders 8-bit linear coverage, same ceiling). carseek flake guard (R139): the p1 run hit sim nondeterminism — the carpool took the last sim at ~8:00, the lot unloaded, the stale vm's clock froze and the soak waited silently (no SUMMARY, ~10-min exit stall; r139p1.log kept honest) — StateSample now detects the lot unload, logs SAMPLE-ABORT and fails carseek loudly; the same-binary rerun (r139p2.log) passed 80/80. R139b (user follow-up 'another language' garble): the garbled surface was the tooltip (Size = 8*DPIScaleFactor = 16 on Retina) rendering through variablesans_16.ffn — the ONE internally inconsistent table in the corpus (char records straddle glyph boundaries; 'Handgloves' renders fragments from _16 but clean text from every other size; hypotheses eliminated: localized archives, neighborhood strings, nibble order, row width, u/v swap, constant shift, code-order/_14-order packing, blank-column segmentation, connected-component self-test — evidence tools/iff-dump/r139/r139-16-table.md + the _16/_18/_20 render captures). Fix: LadderPx drops 16 (15/16->_14, 17->_18); uitext now PINS the ladder composition (16 must stay out), the 16->14 and 17->18 snaps, and runs a per-table 'Handgloves' render probe for every ladder table (mount + ink >= 100 + measure agreement logged). R140 CORRECTION (engine decode — see r140/r140-ffn-law.md): _16 was NEVER broken; the port's atlas stream base was 4 bytes early since R96 (FromFFN used shapeOff+12; the engine's MacGIMEX_read samples at record+16 and len == shapeOff+16+W*H/2 exactly for every table) — every glyph cell sampled its neighbour's strokes 8px left-of-truth, fragments that still read as text; R139b's cell-straddling analysis was performed under that shifted base. Under +16 all tables incl. _16 render clean letterforms. FromFFN corrected; LadderPx restored to the full 10 tables; uitext pins the ladder composition (10 entries WITH 16), the 16->16 and 17->16 snaps (ties-smaller; the engine factory's explicit 15->16 clamp documented — the port keeps 15->14 nearest-ties-smaller, no 15px requesters), and the per-table probes now cover all 10 tables (probe16 ink=822); uivfont snap table + wantSims back to 10 entries (_16=91; advances never touch pixels so measurements carried over unchanged). + 134 'NghRollover' + 131 'EvictModeStrs' disk-pin byte-verbatim — labels, full-chunk sha256s incl. the 76-byte header, and the verbatim value entries [30] 'Household Account Total: %s' / [34] 'Household Net Worth: %s' / [35] 'Value of house and belongings' / 134[0] the neighborhood net-worth rollover / 131[3] the evict payout wording — pins REGENERATED by tools/iff-dump/r137/make_r137_value_canon.py, never hand-typed; no § glyph exists in any table (the engine prepends it); LIVE: cFixedWorld::ComputeArchValue 0x15ea70 decoded — wallpaper FULL price, flooring and wall STRUCTURE at HALF value (return = patterns + trunc(styles/2)), roof tiles at the [TOC-28568] = 100 file constant — the port's VMArchitectureStats.GetArchValue implements that law (FreeSO ed70a02b, fixing the diagonal branch's style-ids-into-GetFloorPrice bug; roof contributes 0, no port-side roof-tile storage, disclosed) and TS1GameScreen.RefreshArchValue keeps FAMI.ValueInArch == GetArchValue + build-mode objects (the UpdateSIMI formula, mirrored into SIMI.ArchitectureValue) on WallsChanged + every 600 frames + at Save — the check asserts the equality against an independent recompute, positivity on the built lot, and the SIMI mirror.)

## Adding or extending a check

1. Add CheckXxx() in AutotestRunner.cs + dispatch in the check loop; register the name in Config.Checks.
2. Log AUTOTEST <name>: PASS/FAIL; keep IFF canon in AutotestCatalogCanon.cs or embedded.
3. Save evidence to tools/iff-dump/ (tracked) and git add it.
4. Never weaken an existing pin — extend it and keep the suite green.

## Limitations

Headless-only: no pixel-veracity proof for censored mosaics, and no audible LISTEN-run on real hardware (the per-event trace and its native laws are gated — AUD-19/AUD-20 — but nobody has yet sat at speakers and confirmed the mix). Desktop personality allocation, pet naming (adoption + editor chain, `petname`), and the freewill gather/winner law (`freewillwin`) are covered. The career/school state traces cover the successful outbound/return/pay/promotion and SchoolBus attendance/return branches; their UI presentation, Chance Cards, and alternate outcomes remain. Touch CAS remains. Takeover probes (e.g. `trv05`, `hdserve`, `nbr06`) end the run at their verdict — run them solo, never combined in one opts string.

### R141 additions — the control panel on original engine art
- **uicp (NEW):** the desktop UCP composes the ORIGINAL engine art per Res_CPanel.RT —
  UniversalBack.TGA plate 220x183 mounted flush at (0,SH-183) via the byte-faithful png
  twin orig_ucp_back.png; the four per-mode patches (kLiveModePatch.. 213x153/156x148/
  107x132/37x100) with exactly-one-visible per mode; every mode/camera/wall/floor
  button carries its original frame dims (people 200x50 sheet, objects 180x47, arch
  156x39, options 92x23; zoom/rot 27x27 frames; LevRoof 22x19, nocut/dynacut 21x21,
  nowall 22x15, Lev2 22x15, Lev1 21x11); speed SINGULARITY (kPause 30x30 + kSpeed1-3
  11/20/32x15 at the toolbar's right end — the UCP pills and static strip are
  compile-gone); artifact absence on desktop (no hide button, no catalog search box);
  the R141 motive fix (all 8 bars fully on-screen in the live subpanel).
- **uisurvey (NEW, evidence harness):** stabilizes the HUD in 3 states (live open /
  UCP island / buy) and dumps full-UI PNGs + an absolute visible-tree to <UserDir>.
  Draws CurrentUIScreen + a PreDraw pass explicitly — the historical uidump rendered
  only the screen-manager stack (the loading screen), so pre-R141 'HUD' pixel reads
  were loading-screen pixels.
- **uidtips (R141):** the speed tooltips [15..18] are asserted at the toolbar's
  right-end cluster (UIMainPanel.PauseButton/SpeedButtons) where the controls now
  live; the eyedropper BUTTON is removed (absence is compile-level; the E-key tool
  stays).

## R142 additions

- **uidlgchrome (NEW):** the original system-dialog + pie canon on the live build.
  GenDlg.bmp resolves at 30x30 and WinBtn.bmp at 260x33 from UIGraphics.far;
  FillColor is exactly RGB(0,0,82); a REAL two-button UIMobileAlert constructed
  through the same path every modal uses must run OriginalChrome with BoxWidth>=200,
  carry the WinBtn sheet on both buttons, and match the decoded PositionButtons row
  law numerically (lefts == ButtonLefts(w,2,maxW), top == ButtonTop(h,33), ±1);
  the title glyph twin must mount; PieButt.bmp and ViewMenuBackground.bmp resolve at
  17x17; the slice-count band law is pinned on 8 inputs {0,1,2,3,4,5,8,12} ->
  {1,3,4,7,8,13,16,20}. Also renders the dialog into <UserDir>/uisurvey-dialog.png
  as eyes-on evidence.
- **uibargeom (NEW):** the original bottom-bar composition — MainPanel at exactly
  (220, ScreenHeight-100); OriginalPanelBack at min(804,SW-220) x 100; the active
  subpanel's right edge EXACTLY ScreenWidth with height 100; the floor readout at
  (156,167) plate-local.
- **uimpanel (R142 correction, disclosed):** bar height 128 -> 100 (PanelBack.bmp's
  own height; the desktop bar sits flush at the bottom, Y = SH-100 exactly) — a
  strengthening, not a weakening.
- **uisurvey (R142 hardening):** AbsWalk prunes invisible/transparent subtrees and
  prints element captions + texture sheet dims (trees are now self-identifying);
  state C detaches killed subpanels before dumping (UISubpanel.Kill removes after a
  300ms tween — the old harness resurrected the dying LIVE panel by forcing opacity,
  producing a phantom 'motive panel in buy mode' reading).
- **uicp / uidtips (R142b, engine anchor law):** pause 15x15 (4x2 grid of the 60x30
  pause.bmp, cropped via the new UIOriginal.Rect) at (85,135) and kSpeed1/2/3 at
  (105/120/141,135) are pinned ON THE UCP PLATE with the full 0x2b8230 anchor table
  (mode cascade, camera diamond, wall row y77..110); the toolbar speed cluster must be
  ABSENT on desktop (mobile-only); uidtips' tooltips [15..18] re-pinned to the plate
  buttons.
- **uidump (R142 correction, disclosed):** backdrop pin corrected from the old
  full-width x128 stretch to PanelBack's natural min(804,SW-220) x 100.

## Round 143 (uicasorig) — the original create-a-family flow

- **uicasorig (NEW, 85 checks total):** the decoded engine law of all three CAS
  screens (r143/cas-layout-law.md + nbhd-layout-law.md): 8 BI_RLE8 far members
  resolve at full dims (CreateACharBack/DsgnFamBkg/PickBkg 800x600, PersLED
  59x25, CACPointRemainingBars 149x25, FamilyPickItemBkg 690x204, PAFMoveIn
  656x62, NbhdTileBtn 364x62) — the RLE8 family mounts through the new
  FSO.Files.BmpRLE8 decoder (FreeSO submodule 8beea8e3); the TRANSPARENCY LAW
  is pinned by pixel probes (PickBkg (0,0) alpha==255 — palette index 0 is an
  opaque navy fill; AddPersonBtn (0,0) alpha==0 — magic-pink palette entries
  are the key); the full DesignChar 13-button anchor + per-state-width table,
  name/bio/family-name rects with canon capacities (25/2048/24), system-button
  positions; the personality LED law (rows at (228,131+32i); AdjustTrait pool
  down-counter 25; lit width = value*6 -> 3 points = 18px, clamp at 10 = 59px;
  points bar width = pool*6 -> 22 = 132px; plus-zone DENIED at pool 0; draw
  pitch 6 vs click pitch 5 const pins); the zodiac law (nearest-archetype
  table: Aries vector -> 0, Taurus -> 1, all-zero -> 11/Pisces); the
  DesignFamily anchors + 8-slot vertical-first grid arithmetic (111+col*120,
  121+row*140); the PickAFamily anchors (Add/Delete 92x62 (322/414,530),
  MoveIn 164x62 (506,530), name tile (100,529)). Renders all three screens
  into <UserDir>/uisurvey-casorig-{cac,fam,paf}.png as eyes-on evidence
  (art top-left per the engine quirk, unpainted margins, Vita hole cut).

## Round 144 (main-UI accuracy pass)

- **uicp (CORRECTED to engine law, same 85 checks):** section 6 now pins the
  r144 people-composition: the motive HOST at MainPanel-local (300,0); 8
  gauges 100x20 at host-local (0/100, 21+19r) with the fill law probed as a
  pure function (raw -100/0/+100 -> 0/30/60 px); the 8-webcam grid at
  (9+45c, 5+45r); the 7 tab anchors (202,31)+(269/237, 4/35/66); the
  LiveGadget plaque 108x100 at (193,0); the PLATE readouts (money
  right-anchored <=178 @158, clock centered in 95..151 @118); and that NO
  time/money readouts remain on the toolbar.
- **uilive (CORRECTED):** desktop motive-name twins compare against the
  engine's grid order (STR# 130 idx {1,3,5,7,9,11,13,15} via
  UIMotiveSubpanel.GaugeStringIndex), the mobile bar order still pins
  R114MotiveBarIndex.
- **uichrome/uitoolbar tree phase (CORRECTED):** kGreenbars 27x25 and
  LiveGadget 108x100 are pinned on their new engine mounts (people-chrome
  trend arrows / tab plaque) — same texture dims, corrected ownership.
- **uisurvey (hardened in R144a):** warm-up pass + RT re-assert + per-child
  try/catch — the live/ucp/buy dumps are now real evidence (the first dump
  after an RT switch used to paint nothing; a throwing child used to abort
  the whole explicit draw silently).
- **GATE: 85/85 PASS** (r144/r144-gate.txt).

## Round 145 (buy/build toolbars + Interest/Gift panels)

- **uibuy (live section REWRITTEN to the r145 engine band law, disclosed
  correction — art/STR# pins untouched):** SetMode(BUY) applies the engine
  entry (ROOM main, Living, subsort 0 applied to the grid, toggle hidden);
  the 8 plaques pin the room-main anchor table + STR# 150 [0..7] tooltips +
  cell = sheet/4 against the live art (the plaque arts are VARIABLE-WIDTH
  4-state naturals, not uniform 36x36); the band grid pins room-Living
  filtering + cells 45x45 from (195,5); page arrows pin res 102 prev (hidden
  at page 0) + 154 tooltips; the Objects-toggle law flips to function mains
  (STR# 150 [8]); a main click lands the subsort state (STR# 200 family
  slots + Other/All from 210, family subsorts uniform 36 cells, Other 31x29).
- **uibuild (live section REWRITTEN to the cWinArch law):** all 12 tools at
  the static anchor table with STR# 139 [0..11] tooltips + 4-state cells
  (cell*4 == sheet width); undo/redo at (11,21)/(11,53) 30-wide with
  STR# 139 [12]/[13]; SelectBuildTool(2) lands the band grid at (338,5) with
  the next arrow shown; SelectBuildTool(10) swaps in the roof panel (pitch
  icons 26-wide cells, shallow at y50, no subpanel).
- **uiinterest (NEW, gate 85 -> 86):** Live.iff STR# 140 'Interest Strings'
  (21 entries, 'Interests' + Travel..Romance — 'Money' at [2] names the
  Violence icon slot) / 141 'Gifts' ('Inventory') / 142 'Interest
  Descriptions' pin label + full-chunk sha + the runtime English set; 24 art
  members pin name+len+dims+sha through the live FAR mount (19 kInterest*
  75x25 icons, kInterestBars/Background 40x16, 3 kInventory*Sort 80x20);
  live pins the fill law clamp(raw/100,0,10)*4, the 3x3 column-major grid at
  pitch 128x27 with 9+6 paging + arrow visibility, the TabCategory 6/7
  wiring, and the gift panel's title + filter stack at host x=2.
- **uisurvey:** new states D (build band, Wall tool selected), D2 (roof
  panel), E (interest panel); state C re-pinned to the engine entry
  (SetMode applies the composition; no manual category select).

## Round 146 (expansion buy bands + zoom-terrain harness)

- **uiexpband (NEW, gate 86 -> 87):** 19 expansion art members pin
  name+len+dims+sha through the live FAR mount (BuyCOne/BuyCFour/BuyCMisc,
  BuyDDining/BuyDOutdoor/BuyDMisc, BuyVOne/BuyVMisc, BuySTSpa/BuySTStudio,
  BuyMOutdoor/BuyMTmagic, BackCOne/BackCMisc/BackDMisc, and the
  Community/Downtown/Vacation SubSortSeating icons) — including the Maxis
  art-reuse identities the pins prove byte-exact (BuyCOne == BuyDDining,
  Buy{C,D,V}Misc == BuyFMisc, one shared SubSortSeating bitmap across all
  three families). Chrome law pins ALL FIVE modes: mains visible at slots
  0-3+7 and HIDDEN at 4-6, downtown-pattern anchors with studio's 4th main
  at (138,14), tooltips STR# 150 [16/24/32/40+i] (magic sharing the
  Superstar block), subsort row = 8 function sorts at the 38/48 grid with
  tooltips [8..15] and the family art (ST/MT = downtown set), toggle =
  BuyBack<family>[0] visible in sub state. Live pins the mask law: community
  main0 = bit 0x01 (364 items), MISC = bit 0x80 (428), downtown main0 = 386,
  full-mode catalog = 659, and the sub-click filter (main bit AND function
  category).
- **uibuy (DT sub-check corrected, disclosed):** the old pin asserted the
  R122 MOBILE subsort row (SelButtons == 6 with STR#150 [16..19] + 210
  Other/All); the R146 band replaces it on desktop — the pin now asserts
  BandMode, NO SelButtons, and the panel's 8 function-sub cats with the
  downtown family art + FuncId mapping.
- **uisurvey:** NEW state F (community band: SetLotMode + a community-mode
  browse panel + RelayoutSub dump) and Z* ZOOM STATES — zoomDump forces
  WorldState zoom/precise through the real invalidators, renders
  world.PreDraw into the PPX backbuffer and snapshots it (the survey's UI
  pass does not contain the world scene); dumps zoomcur/far/med/dec1/dec2/
  far2 + dark censuses are the far-zoom black-terrain repro evidence
  (r146/zoom-black-notes.md). Tree cap raised 220 -> 400 lines (the new
  states pushed state E+ out of the truncated tree).

## Round 147 (far-zoom black terrain root fix: camera cage)

- **uizoomcage (NEW, 87→88).** The original TS1 lot view cages the camera to
  the terrain — no zoom or scroll position may reveal the beyond-terrain
  void (the "road and ground turns black" report; the old BoundView
  boundfactors 1.20/1.05/0.5 × lot width never bounded a 52-tile TS1 lot).
  The check slams CenterTile to 5 far off-lot extremes at Near/Medium/Far,
  runs the real `world.PreDraw` (BoundView clamps inside), and asserts the
  clamped position in rotated view coords u=(x−y), v=(x+y−lotW) satisfies
  the terrain-covering cage. 15/15 positions pinned at all 3 zooms.
- **uisurvey Z-battery REMOVED (disclosed).** The R146 zoom states and the
  R147 diagnostic battery forced `PreciseZoom` 0.25/0.5 states the live game
  never enters — the live game keeps PreciseZoom=1.0 and only switches the
  zoom enum (UILotControl never writes PreciseZoom). The battery's "black
  terrain" readings were the beyond-lot void seen through unreachable
  framing; superseded evidence kept under r146/ and r147/. No pins weakened:
  the battery was log-only.
- **uisurvey: NEW 'zoomlive' live-frame probe** (arms after the survey, runs
  ~320 real game frames via the StateSample tick): baseline → TargetZoom
  0.25 far tween → 0.5 medium return → scroll toward the front-left corner,
  censusing the LIVE PPX backbuffer (dark/green) + zoom/pz/bbscale/
  DrawImmediate/ssDirty every 10 frames. This is the user-flow evidence
  harness for camera/zoom regressions. Post-cage reference: far centered
  dark≈32% (roads counted), medium return ≈1.8%, scroll saturates ≈41% (the
  original's edge-sliver at max scroll; was 89% pre-fix).

## Round 148 (UI deep-dive: buy-band interaction chrome)

- **uibandlaw (NEW, 88→89).** The user-facing round target was "the UI still
  isn't right — something is missing". The skeptic audit + in-round tracing
  found the missing layer was INTERACTION, not composition. Pins:
  (1) the icon fit law — engine ProductButton::FGBlt (0x20b960-0x20bb54)
  blits thumbnails at NATURAL size (src==dest, no scaling); FitSize pins
  (33,21)→(33,21), (80,60)→(41,30.75), (20,20)→(20,20) — never upscale,
  fit-down only past the 41px interior;
  (2) the three cell frames mount at 45×45 (kCatalogTemplate1Frame normal,
  kCatalogTemplate frame 1 selected, frame 2 hover — the loader now RETRIES
  instead of one-shot; a failed mount draws the navy fallback face, never
  invisible cells);
  (3) the toggle law — BackButtons sheets are 132×26 = 4×(33×26) cells; the
  engine SetArea keeps the CELL size at (-2,(100−26)/2) = (-2,37) (the port
  had 32, and before this round Size.Y was permanently 1);
  (4) FULL CLICK REGIONS — UIButton's Size setter only assigns Width, so
  every band button built on the 1×1 white base texture had Size.Y/Region
  == 1 (a 1px-tall hit strip — the band was nearly unclickable by mouse).
  Sheet buttons now construct on their REAL 4-frame sheet (ImageStates=4
  derives w/4 × h natively) and Remount re-images through the Texture
  setter; cells construct on the 45×45 frame. The check reflects into the
  protected ClickHandler and asserts Region ⊇ Size for plaques, toggle,
  page arrows and a grid cell;
  (5) HOVER — UIButton's own MouseOver/MouseOut dispatcher drives
  cell.Hovered (frame 2) + the catalog popup enable (engine cmd 0x13 →
  EnablePopup 0x26a76c) and disable on exit (TSOnMouseExitChild 0x26a8c0);
  the popup anchor follows BuildMyBuffer law x=btnX−559 (clamped ≥0,
  disclosed) y=btnY−127;
  (6) POPUP LIFECYCLE — panel.Kill() detaches the popup from the MainPanel
  (it used to stick on screen across BUY→LIVE/BUILD mode switches).
- **FreeSO submodule 1a977e88 (committed first):** tooltip text color =
  the TS1 green RGB(31,124,31) (cWinCatalog Init 0x26b458 law) in
  UITooltipHandler.
- **Runner note:** the first r148 gate run exited through OnExiting without
  printing the AUTOTEST RESULT summary (an intermittent mac SDL teardown
  race — the "Engine-exit fix" re-arm path in SimitoneGame.OnExiting); the
  per-check PASS/FAIL lines are authoritative. Second run confirmed below.

## Round 149 (plaque enable matrices)

- **uienamat (NEW, 89→90).** The original GRAYS OUT subsort category plaques
  whose (current main, sub) cell has no items — engine cWinCatalog::Init
  builds seven 8×8 byte matrices from the catalog product list
  (roomFunc[room*8+func], one per expansion [mainSlot*8+func] with Misc slot 7
  = bit 0x80 and slots 4-6 never written, and functionSub[func*8+subsort] —
  the function is the ROW there, the column in the other six); LoadBooks
  writes each subsort plaque's enable from the current main's row, 0 cell =
  the ghosted frame-3 plaque, click dead. The port builds the same matrices
  (UIOriginalEnableMatrices) and grays exactly those plaques.
  Pins: all 448 cells equal an independent recomputation over
  WorldCatalog.All(); the R122 stove anchor roomFunc[kitchen×appliances] is
  SET; every matrix non-empty and sparse (counts logged per matrix); LIVE
  wiring — the community main-0 row and the home kitchen row gray exactly the
  empty cells, a disabled plaque's click no-ops (exercised through UIButton's
  own MouseDown/MouseUp dispatcher; magic-town main-0 fallback drive),
  RelayoutMain resets all plaques enabled.
- **In-round corrections (disclosed).** (1) The check's own funcSub cell
  recomputation was transposed ([func*8+subsort] means function=row) — the
  BUILDER was engine-exact; fixed in the checker. (2) Targeted -autotest-opts
  runs must include 'corpus': the UI checks dispatch inside RunCorpus, and a
  lot-only list finishes right after the lot check (silent skip, honest
  passed=1). No product-code correction was needed for either.
- **Harness race fix (disclosed; no pins weakened).** The first r149 full-gate
  run failed 'mood' (89/90): the R77 death-reach collapse fired at elapsed>=6 —
  INSIDE the mood gate's 10-minute window — and when the engine's death chain
  executed before the gate's final sample it legitimately zeroed avatar0's
  stored Mood (the sim is dying), flipping the gate on harness timing alone
  (R148 had passed the same race by ~4 seconds; both runs collapsed at the
  same minute 37). The collapse now waits for MoodGateDone (no behavior change
  when 'mood' isn't enabled; the death chain still fires and still reaches —
  just sequenced after the mood window).

## Round 150 (interest values: the init-traits tree law)

- **uiintvals (NEW, 90→91).** The Interest panel's bars were empty because of
  a SCALE bug, not missing data. Decoded from game data (character file
  User00000.iff 'Bob Newbie', BHAV 4100 'init traits'): the original rolls
  ten NextRandom(11) values into person words 46..55, bucketed 1×0 /
  3×1-3 / 3×4-6 / 3×7-10, RAW 0..10 script scale — and the probe proved the
  port VM already executes these trees (the gate avatars carried live rolls).
  The panel byte-split the words (TSO packing) ×4 and then ÷100 — max display
  0.4 → 0. Now: the panel reads the FULL word and ×100s it into the display
  scale; topics 8-11 share 0-3's words (the decoded cWinInterest widget
  table); the townie generator's ×100 convention corrected to the tree's raw
  rolls (FreeSO ee612e89, committed first); VMTS1Activator mirrors the tree
  for avatars that load zeroed (NEW VMInterestTraits). Pins per avatar:
  words 46-55 ∈ {0}∪[1..10]; the bucket shape exact (1×0, 3×1-3, 3×4-6,
  3×7-10); the widget-share map; at least one avatar with nonzero interests.
- **Disclosure:** the r145 residual "nothing writes words 46-53" is
  superseded — the trees always wrote them; the r145 TopicWord byte-packing
  reading is corrected to the script-scale full-word law. The uiinterest
  FillWidthForValue pins (0..1000 display law) are unchanged and still pass.

## Round 151 (expansion interests: all 19 widgets on the decoded low-index words)

- **THE DECODE (r151-expansion-interest-decode.md).** The R150 residual's
  premise ("the seven topics live in a runtime block outside the word run")
  is BUSTED by the binary: person word N = cXPerson + 0x58c + 2·N, bounds
  0..255 — ONE array. Technology/Romance = +0x5f8/+0x5fa = words 54/55 (the
  init-traits tree's rolls #9/#10, already stored since R150);
  Exercise/Food/Parties/Style/Hollywood = +0x5a6/+0x5a8/+0x5ac/+0x5b4/
  +0x5c0 = WORDS 13/14/16/20/26 — low indices the port's TSO-era enum names
  (NetworkID/OnlineStatus/CustomOutfitIndex/AllowedSocialAndPuppeteering/
  ChatBaloonOn) had squatted on; all five TSO-era names are dead code in
  the TS1 port (zero readers/writers outside the enum). WRITERS (whole-
  corpus scan, 65 FARs + loose IFFs, 1212 person-data writes): every
  character file's 'init traits' tree opens with MyPersonData[20] = 5
  (Style = 5 for every tree-initialized sim); NOTHING writes 13/14/16/26;
  NOTHING writes >= 56; the engine's own cXPerson::RandomizeAllInterests
  @0x111650 (all 15 topics, mulli-100 ×100 scale, bucket counters {4,3,3}
  and {1,2,3} over ranges {0..3,4..6,7..10}) fires only behind an all-10
  unset sentinel and is NOT ported (disclosed). Scale tension (§2.4, the
  honest open residual): trees/NBR records write raw 0..10, the engine
  randomizer writes ×100, the panel divides by 100 — how the shipped game
  reconciles tree sims' near-zero bars needs runtime emulation.
- **uiexpint (NEW, 91→92).** The panel mounts ALL 19 topics (TopicsShown
  19, three pages 9/9/1) with the four remaining ORIGINAL plaques
  (InterestStyle 75x25; InterestCharisma/Health/Romance 75x26 — the R145
  FAR pins; the engine's internal art names: Hollywood=charisma,
  Technology=health). Storage = the decoded words (enum aliases
  Interest_Exercise/Food/Parties/Style/Hollywood = 13/14/16/20/26);
  ChatBaloonOn's two writers (Message set/timeout) are TS1-gated so
  Hollywood's word stays clean; Technology/Romance bars fill from the
  sims' own words 54/55. uiexpint pins: STR# 140 [13..19] verbatim at
  runtime; words 13/14/16/26 == 0 on every avatar (the no-writer law,
  drift-detecting); Style (20) in {0,5} with >=1 avatar at 5 (the tree
  law — first run: style5=3/3); a word-13 write (7) survives a REAL
  VMAvatarMarshal serialize/deserialize round-trip then restores;
  cells 12..18 mount their plaques at pinned dims; >=1 avatar carries a
  nonzero Technology/Romance roll.
- **uiintvals extended (never weakened):** the map pin grows 15→19 entries
  (12-16 = 13/14/16/20/26, 17/18 = 54/55; entries 0-11 unchanged); the
  per-avatar 46-55 bucket law untouched.
- **uiinterest extended (never weakened):** the live grid pin grows to 19
  cells (page 1 = 9 at the same slots); page 2 now expects PageNext alive
  (9 visible, topics 9-17) and NEW page 3 pins the last cell (Romance)
  alone at (13,17) with PageNext dead. Art canon untouched (the four
  plaques were already byte-pinned in R145InterestArt).
- **In-round honesty notes.** (1) An interim stopgap (port-storage words
  101-105) was built, gated 92/92, and then REPLACED by the decode's
  words before commit — the decode agent's binary evidence (Reset's
  256-word zero sweep, InterpValue's lha 1420 + bounds 256, the
  RandomizeAllInterests index switch) is the disproof; nothing shipped
  on the stopgap. (2) The first baseline run hit the documented
  environmental early-exit flake (90 PASSed, clean exit-0, no RESULT
  line, mid-carseek-soak) — same-binary rerun 91/91 completed
  (r151-baseline-run2.log). BONUS: the r151 gate soak observed
  commute-tail ids 4102 ('At Work') and 4106 ('Create Car') live for the
  first time (carseek RESULT ids, car-entity-max=2) — R84's residual
  narrowed further, unpinned.
- **Full gate: 92/92 PASS, clean exit-probe chain** (r151-gate-run2.log;
  targeted run2 9/9: style5=3/3, round=True, plaques=True —
  r151-targeted-run2.log; run1 = the superseded 101-105 build, which had
  also passed 92/92 before the remap).

## Round 152 (the engine creation dialect: RandomizeAllInterests ported)

- **uiexprand (NEW, 92→93).** The R151 residual "RandomizeAllInterests
  unported" is closed: FreeSO `VMInterestRandomizer` ports the engine's
  creation-time randomizer verbatim — loop 1 words 46..55 with counters
  {4,3,3}, loop 2 words 13/14/16/20/26 with {1,2,2} (CORRECTS the r151
  doc's "{1,2,3}" misread: the raw words at 0x11176c-0x11178c store
  li-1 at stack[64] and li-2 at BOTH stack[68]/stack[72] — {1,2,3} summed
  to 6 against 5 stores; erratum appended to the r151 decode doc), ranges
  {0..3,4..6,7..10}, every store mulli-100 (x100 dialect), re-roll on
  exhausted buckets, fresh draw per value. The all-10 sentinel gate
  (words 46..53 all == 10, Reset/Initialize 0x111bc8/0x112014) is wired
  in VMTS1Activator beside the R150 IsZeroed tree mirror, and the
  GENERATOR (the CAS/townie path) now applies the same law — replacing
  the pre-decode guesses (pd[13]/[14]=500..800, pd[16]=pd[26]=600) and
  the R150 tree-dialect loop; generated sims get ALL 15 topics (the old
  code never wrote Style/word 20). Pins: generator emits the x100
  dialect with both loop shapes; sentinel true/false probe; Apply on a
  LIVE avatar writes both shapes and clears the sentinel with every
  touched word finally-restored and verified; the panel
  BridgeToDisplayScale pins (raw ×100, x100 pass-through — total on both
  domains since raw max 10 < 100). First targeted run all-True
  (r152-targeted-run1.log).
- **uiintvals extended (never weakened).** Per-avatar dialect split: the
  raw domain keeps the exact R150/R151 tree-shape law; the x100 domain
  pins the engine loop-1 shape (4×[0..3], 3×[4..6], 3×[7..10], multiples
  of 100 in 0..1000). Gate-lot avatars log dialect=raw (character files).
- **The §2.4 scale tension resolved port-side (disclosed):** two original
  writer dialects now coexist (trees raw 0..10; engine creation x100) and
  the panel bridges at read — the ported resolution of the r151 decode's
  open residual; the original's own load-time bridge (if any) remains
  unemulated.
- **Full gate:** 93/93 PASS, clean exit chain (OnExiting cancel True/False →
  after Run → after Dispose → AUTOTEST_WRAPPER_EXIT=0) —
  tools/iff-dump/r152/r152-gate-run1.log (run finished 12:07:44, no flakes).

## Round 153 (the sentinel verdict: gate-polarity correction)

Target: the R152-disclosed residual — "who writes the all-10 sentinel?"
Verdict (two parallel decode agents + this round's own raw-word re-verification
+ independent script-side corroboration): **nobody — the sentinel never
existed.** The gate branches at 0x1119f8-0x111a6c / 0x111fa0-0x112004 are
raw `0x4181` = **bgt**: the gate passes when all eight words 46..53 are
**<= 10**, and the ctor/Reset ZERO SWEEP itself arms RandomizeAllInterests.
Census: 0 of 203 shipped NBR records carry all-10; no code `sth` of 10 into
the gate region exists; PersonGlobals BHAV 9761 'Convert Interests to
0-1000' fronts the SAME <=10 gate (independent polarity corroboration).

Changes:
- **FreeSO `VMInterestRandomizer`**: `IsUnsetSentinel` (== 10, the misread)
  → `NeedsRandomize` (the decoded <= 10 law). Doc comment rewritten.
- **FreeSO `VMTS1Activator`**: the R152 sentinel hook REMOVED (its state has
  no writer — byte-proven; firing post-load would re-randomize tree sims
  every session, diverging from the original's creation-time gate). The R150
  IsZeroed safety net stays. Behavior-neutral: the hook could never fire.
- **Client `uiexprand`**: the gate probe is CORRECTED and STRENGTHENED 2→5
  cases — all-10 → true, one word 7 (still raw-scale) → TRUE (this case pins
  the polarity: false would mean == 10), one word 11 (×100 present) → false,
  all zeros → true, one word 500 → false. The old "one broken → false" pin
  encoded the misread law and is superseded per the correct-when-disproven
  rule — the new pin set is strictly stronger (every old-true case stays
  pinned true, plus three new cases). Post-Apply assertion: the gate no
  longer passes (deterministic — counters {4,3,3} force ≥ 6 words ≥ 400).
  Stale comments at uiexpint header / dispatch / panel doc also corrected.

Unchanged by construction: all uiintvals/uiexpint value pins (avatars keep
their raw tree dialect — the hook removal touches a provably-dead path);
generator pins (ApplyTo law untouched).

- **Targeted run:** 10/10 PASS first try —
  `uiexprand: genShape=True gate=True (all10=True raw7=True fail11=True
  zeros=True fail500=True) liveShape=True restored=True bridge=True`
  (tools/iff-dump/r153/r153-targeted-run1.log).
- **Full gate:** 93/93 PASS, clean exit chain (OnExiting cancel True/False →
  after Run → after Dispose → AUTOTEST_WRAPPER_EXIT=0) —
  tools/iff-dump/r153/r153-gate-run1.log (no flakes; corrected gate probe
  passed inside the full run, avatars still dialect=raw).

## Round 154 (the interest-scale saga resolved: the migration pipeline is wired)

Target: trace who calls the script-side 'Convert Interests to 0-1000' (the
R153-recommended residual). The research agents first returned "dead script,
nothing calls it" — built on byte-swapped ids (9761/39200 = big-endian
misreads of the little-endian rsmp u16s; true ids 8486/8345). The new
`uiconv` gate check FAILED against the real data (`idRange=8192..8664`) and
exposed the misread before it could be recorded as fact — the gate caught
the bad decode. Corrected verdict (r154/r154-scale-saga-closed.md):
**the Hot-Date-era migration pipeline is REAL and WIRED** — the five armed
expansion NPC globals (SalesClerk, VacationCarnie, VacationDirector,
VacationMascot, Waiter) call 8345 'Add Hot Date Interests' then 8486
'Convert Interests to 0-1000' from 'person main loop' (8283) instr[38]/[39],
and 8345 from 'load tree' (8244) instr[17]; idempotent via the callees'
own gates (all-five-HD-words-zero; all-eight-base-words-≤10 — the same law
as the engine gate, r153). PersonGlobals carries 8486 armed-but-uncalled
and 8345 disarmed; residents never migrate. Engine binary: 0 references
with the true ids (engine hardcodes no tree id at all). Decoded TTABs: only
the Cat/DogGlobals 8345 'Interaction - Hug TEST' id collision. Port: no
change (fresh NPCs get ×100 from the R152 generator; restored downtown NPC
threads re-execute the saved main-loop frames — convergent; display-neutral
via the R150 bridge; disclosed).

- NEW check `uiconv` (93 → 94): pins PersonGlobals BHAV 8486 (23 ins,
  ≤10 gate ins0 T1/F254 w46/lit10, ins1 → w47), PersonGlobals BHAV 8345
  (38 ins, disarmed ins0 T254/F254 on word 13), and SalesClerkGlobals
  BHAV 8283 instr[38]=8345 / instr[39]=8486 (the wired sequence).
- **Targeted run:** 11/11 PASS — `uiconv: conv=True hdDisarmed=True
  wired=True (ins38=8345 ins39=8486)` (r154-targeted-run5.log; runs 1-4
  document the canary FAIL → id rescue → fix chain).
- **Full gate:** 94/94 PASS, clean exit chain (OnExiting cancel True/False →
  after Run → after Dispose → AUTOTEST_WRAPPER_EXIT=0) —
  tools/iff-dump/r154/r154-gate-run1.log (no flakes).

## Round 155 (career commute tail: misattribution corrected, probe owner-precise)

Target: pin the commute tail (4102 'At Work' + 4106 'Create Car', believed
observed in four soaks). Finding: **the observation was a misattribution** —
the probe's "owning IFF filename contains car" test matched Paper**Car**rier;
the live 4102/4106 were the paper carrier's 'special drop (tabloid) function',
car-entity-max=2 was CarPortal + the paper carrier, and **no carpool car has
ever spawned in any soak**. The genuine CarPortal-owned live set is
{4096 'main', 280 'idle', 4100 'process', 4125 'process warnings car pools'}
(identical across r151-r154 first-seen lines). Same failure class as R154's
byte-swap: id matching without owner/label attribution.

Changes (AutotestRunner carseek):
- frames attributed per owner IFF; RESULT splits portal-owned / paper-owned;
- portalEnt / portal-entity-max counted (CarPortal.iff entities only);
- discovery label-anchors all four CarPortal routine labels;
- **pass law extended (strictly stronger than R84's — never weakened):**
  old law = 4100 live + label 'process'; new law additionally requires
  4096+280+4125 CarPortal-OWNED, the four-label anchor, and portal
  presence. The commute tail (4105/4106/4102/4103/4104/4119) remains an
  open residual — correctly stated;
- NEW diagnostics at discovery: CarPortal Attr[0..8] + per-avatar
  JobType/PersonType/word68/word69 (the 4100 guard-chain inputs —
  decoded this round from CarPortal.iff with the port's own operand
  schema — so the next soak can name the failing condition).

PARITY career gap text corrected (the r151 narrowing retracted; residual =
the full tail + decoded guard chain).

- **Targeted run:** 7/7 PASS — `carseek RESULT ... portal-owned=[280,4096,
  4100,4125] paper-owned=[280,281,368,4096,4099,4102,4106] ... labels4=True`
  (r155-targeted-run2.log; run1 = the labels4=False FAIL that exposed the
  missing-chunk-280 nuance).
- **Full gate:** 94/94 PASS, clean exit chain (OnExiting cancel True/False →
  after Run → after Dispose → AUTOTEST_WRAPPER_EXIT=0) —
  tools/iff-dump/r155/r155-gate-run1.log (no flakes; the attribution-fixed
  RESULT format live in the full run).

## Round 156 (commute window fixed + native branding)

Two halves, one round (user request). **(a)** The R155 window math held:
'process' triggers Create-Car at Global[0] == Attr[0]-2 (two hours before
work start) — the fixture loaded at 8:00 with a 9:00 job, so the 7:00
window was ALWAYS missed. CarpoolDiscovery now rewinds the VM clock to
(start-125 min) when the load time is past it (logged as `clock-rewind
(harness)`; no engine change). Window hit — car still un-fired; the
residual narrows to 4100's per-person iteration (a JobType=-1 sim
bookmarks Attr[0]=0; the working sim's branch never completes — see
r156-window-run2 per-minute attrs + commute-inputs with objVar34). Static
suspects eliminated: JobData scope implemented; GetJobData(9,0,12) maps to
CARR.StartTime; VMSetToNext solid. The carseek PASS law is UNCHANGED
(R155's), still green under the rewind. **(b)** Native branding: the
Simitone fork's user-visible branding is gone — bundle `The Sims-arm64.app`,
executable/assembly TheSims ("The Sims" product), Info.plist identity
(TheSims / The Sims / com.thesims.macos, zero simitone substrings),
dialogs/OOM/console strings, and the APP ICON: a programmatic plumbob
(the original ships no standalone plumbob asset — MM boot art verified,
70x52 stamps too small) drawn by tools/make_native_icon.py at pack time,
deterministic (sha f04d652f…), gitignored (no proprietary bytes
committed). Kept functional: ~/Documents/Simitone/ user-data dir (holds
saves), FSOV chunk label, code namespaces. Docs/harness paths updated.

- NEW check `uibrand` (94 → 95): pins Info.plist fields + no-simitone,
  TheSims executable, the deterministic icon sha + icns magic, window
  title "The Sims".
- **Targeted run:** 8/8 PASS — `uibrand: plist=True exe=True icon=True
  (len=83420 sha=f04d652f…) title=[The Sims]` + carseek green under the
  rewind (r156-targeted-run1.log).
- **Full gate:** 96/96 PASS, clean exit chain (OnExiting cancel True/False →
  after Run → after Dispose → AUTOTEST_WRAPPER_EXIT=0) —
  tools/iff-dump/r156/r156-gate-run1.log (no flakes; first attempt hit a
  harness-only path bug — run-autotest.sh briefly pointed at a
  non-existent bundle name — fixed and rerun same binary).

## R159 — 'uirel' (95 → 96): the people-panel completion

Disk-pins UIText.iff STR# 240 'Relationship Panel Sort Button Tooltip Text'
(sha ac8b98b6…, all 4 values) + STR# 164 'signs' (sha 39de1694…, zodiac names
[0..11]) + Live.iff STR# 139 'ReportCard' (sha f110a54a…, 3 values); art dims
for the 4 relationship sort plaques (Rel*Sort.bmp 80x20), the 3 marker bitmaps
(smiley/heart/heartdeep 9x9), SkillsHilite (4x11), ThumbBack (25x25), the
salary (52x11) + performance (52x12) caption plaques; LIVE: the relationship
subpanel constructed through its real ctor with all 4 original sorts mounted
and tooltips == disk canon; the marker priority law (deep-heart > heart >
smiley on synthetic flag words — NBRS value[1] is the flag word, the fixture's
married pair carries [100,1]); the child law (PersonsAge 27→adult, 9→child);
zodiac names read from the table; clock AM/PM from STR# 138 [21]/[22];
SkillsHilite pips. Targeted runs must use `-autotest-opts` (not
`-autotest-checks` — the arg is silently ignored, the default list runs).

## R160 — 'uinav' (96 → 97): the neighborhood navbar

Pins the R143-decoded engine navbar law end-to-end (evidence r160/):

- **Strings:** STR# 151 'NghbBtnTips' chunk sha256 4ff94459…daf9 (26294
  bytes, 280 entries = 20 langs × 14) + the English block [0..13]
  verbatim; the destination screens' own tooltip tables STR# 169
  (Downtown) / 170 (Vacation) / 173 (Studio Town) / 174 (Magicland) pin
  entry 0 == 'Return to Neighborhood View' ([1] Exchange, [2] Credits,
  [3] Import, [4] Bulldoze are the destination button set).
- **Art:** 24 members name+dims in the live TS1Global mount — the 14
  toolbar sheets (212x52 / InetBtn 156x52 / Previous+Next 104x52 /
  TheSimsLogo 70x52), both banners (800x52), Current (23x30), CreditBtnEn
  (232x58), the four Return sheets (212x52), Downtown Import/Bulldoze,
  and the surround Downtown/LargeBack.bmp (1024x768).
- **Law:** EngineLaw (the static table the port mounts from) == the r143
  constants — member/x/y/cols/tip/idx for all 14 slots.
- **LIVE:** headless strips (Panel=null, clicks never fired): home mode 4
  mounts all 14 buttons AT the law anchors with engine cell sizes
  (W/cols x H/rows: 53/39/26/58/70 wide), OriginalMounted, and tooltips
  == disk STR# 151 strings (logo tip empty, 151[13]); banner_neighborhood
  + kNghCurrent + title 'Old Town'. Mode 2 swaps banner_downtown, per-
  screen Import/Bulldoze art, hides the current destination + the 5
  home-only slots, and adds Return at (200,0) with tooltip 169[0] (9
  buttons). moveIn mode hides MoveIn + destinations, Exit reachable.
  strips=3 buttons=32.

Runs inside RunCorpus — a targeted run needs
`-autotest-opts "uinav,corpus"` (opts=uinav alone never enters the
method; the run1 0/0 artifact documents this).

## R161 — 'uibudget' (97 → 98): the budget window

Pins the cWinBudgetDlg decode end-to-end (evidence r161/):

- **Strings:** STR# 146 'budgetstrs' chunk sha256 48abf0be…ab61 (49317
  bytes, 648 = 18 langs × 36) + ALL 36 English entries verbatim — the
  '(40;25)'/'(40;100)' anchor directives, OK/title/'3 Days'/'Today', and
  the 15 label+tooltip pairs incl. the three '%s' totals.
- **Art:** kBudgetBkg BudgetBack.bmp 800x600 + kBudgetOK BudgetOK.bmp
  868x52 name+dims in the live mount.
- **LIVE:** the dialog through its real ctor on the autotest game screen
  — 15 rows at the anchor law ((40,100)+i*20; headers unindented, items
  +16), the uniform max-width column law actually measured (labelW=327
  on the fixture), the Account Total line == 'Household Account Total:
  §<ActiveFamily.Budget>' from the REAL family, Net Worth ==
  Budget+ValueInArch, OK button 217x52 at (291,274) (engine both-axes
  centering) with tooltip 146[2], row tooltips == disk.

Runs inside RunCorpus like uinav — targeted runs use
`-autotest-opts "uibudget,corpus"`.

## R162 — 'uihelp' (98 → 99): the help window

Pins the cWinHelp decode (evidence r162/):

- **Strings:** STR# 166 'Help' chunk sha256 c30b13f6…f41 (473342 bytes,
  1598 entries) + the English 47 TOPIC PAIRS (title even / body odd)
  spot-pinned at first/mid/last titles and two full bodies incl. a
  \r\n TIP entry.
- **Art:** HelpButton.BMP 40x15 (the r144 law: 4x10x15 states at (5,164))
  + TutHigh.bmp 150x50 present in the mount.
- **LIVE:** the dialog through its real ctor — 47 topics == disk, the
  Select/scroll-clamp/LastSelected-persistence law, bodies == disk, and
  the live desktop UCP carries kHelpBtn at (5,164) with the 10x15 cell.
- The canonDetail diagnostic line is permanent gate evidence: targeted-
  run1 + gate-run1 kept as the honest-FAIL artifacts for a gate-side
  missing-`!` spot-pin (the port was correct).

Runs inside RunCorpus — targeted runs use
`-autotest-opts "uihelp,corpus"`.

## R163 — VM semantics sweep + natural career return (99 → 102)

- **persondata:** every House 5 avatar has 101 words; TS1 words
  21/70/74/80/81/84/100 round-trip, the fixture's word 98 remains zero,
  original Global 488 and PersonGlobals 8645 complete, and a real NBRS
  neighbor zero-extends through `InheritNeighbor`.
- **travelinv:** original PhoneGlobals anchors plus production modes 5/6
  prove GUID 10/11 replacement uses the latest Temp 0 neighbor id.
- **freewillvar:** production aggregation retains two distinct dynamic
  variants with Param0 `[111,222]`, preserving identity/order/score/callee.
- **carreturn (opt-in):** the natural 09:00–15:00 Science shift produces
  exactly one outbound create at 4106:20 and one return at 4121:19,
  level 0→1, and committed +460/+230 pay. Attributed pay is 690; the
  household net is 680 because an unrelated fridge expense is −10.

Packaged evidence in `tools/iff-dump/r163/`: focused 13/13, carreturn
11/11, full default gate **102/102**, all with clean exit. The TreeAd JIT
probe passes, and the NeighborhoodData probe passes all nine fixtures.

## R164 — atomic TS1 saved-continuation repair (102 → 103)

- **savedthreads:** requires an actual `House05.iff` source rather than a cached
  serialized lot; pins the exact repair ids
  `10,114,115,116,117,123,131,133,134,173,174,191,192,194,226` and the exact
  owner GUID pairs: `10/6b264238`, `114-117/91767a36`, `123/28975923`,
  `131/df77ee12`, `133/d1cf9596`, `134/fbe586b7`, `173/d5ca2d74`,
  `174/24e26386`, `191/c59350c6`, `192/23a15547`, `194/fd746ad2`, and
  `226/7d2089b6`.
- Every repaired object must start its current entry-point main at root IP 0
  with a non-action self caller/callee frame, an empty queue, active queue block
  -1, and interrupt cleared. All other live ordinary frames must have a code
  owner, a non-empty routine, and an in-range instruction pointer.
- The import repair is TS1-only and whole-stack atomic: any invalid restored
  frame discards the complete stack and pending action state. After `VM.Load`,
  free-id update, and missing-controller creation, the importer records the
  sorted ids in transient, non-serialized
  `VMTS1LotState.SanitizedThreadObjectIDs` and resets those objects in that
  order before the first tick. There is no per-thread repair bit. Rejected
  avatars also clear priority, non-interruptibility, cancellation, and—after
  Reset—abandoned motive deltas before normal play resumes.

Evidence in `tools/iff-dump/r164/`: the owned-data scan passes its 245-instance /
522-frame census; final focused run 2 (`r164-savedthreads-run2.log`) is **2/2**
with all 15 current root mains pinned; the prior clock-driven carseek soak
(`r164-threadsoak-run1.log`) is **3/3** through 10:15 with zero
`SimAnticsExc` and zero `bad-routine-frame` records.

The final packaged default gate (`r164-gate-run2.log`) is **103 passed, 0
failed, 0 skipped** with `AUTOTEST_WRAPPER_EXIT=0`, clean `after Run` / `after
Dispose` shutdown, and zero `SimAnticsExc` or `bad-routine-frame` records. Its
SHA-256 is
`403a2b113b2acf10be7cee731dd87f026140d484ab65df0316cf55ce7813d58d`.

The gate launcher `tools/run-autotest.sh` now exports `NSUnbufferedIO=YES` and
passes `-ApplePersistenceIgnoreState YES`. A sampled superseded run had been
blocked by an invisible macOS restored-state modal; these are test-harness
launch controls only and do not change product or UI behavior.

## R165 school-bus round

`schoolreturn` is an opt-in House 5 trace; it is deliberately absent from the
103-check default list because it follows the VM from before the 08:00 outbound
creation through the return bus's natural 17:00 cleanup.

The fixture is accepted only when the loaded source is `House05.iff` and its
sole child is the exact original Cassandra object: runtime object 16, GUID
`c1207913`, NeighborId 32, PersonType 0, age 9, no adult job, initial grade 4.
Mounted CarPortal BCON 4097 must provide school hours 09:00–15:00 and mounted
master `SchoolBus.iff` must have GUID `50ac15bf`. The static companion scanner
hard-pins those facts before the runtime trace is interpreted.

The runtime gate requires all of the following:

- exactly two SchoolBus creations, both at `CarPortal.iff:4119:4`, with one
  outbound parent `4115:10` at 08:00 and one return parent `4116:27` at 15:00;
- no more than one visible SchoolBus multitile group, with the outbound group
  observed and naturally gone before the return, then the return group observed
  and naturally gone;
- live SchoolBus private frames 4096 and 4121, CarGlobals drive-away frame 8205,
  and CarPortal's At School 4116 frame;
- the original logical child, tracked by NeighborId rather than a transient
  ObjectID alone, reaching `Hidden=1`, out-of-world, PersonData[87]=1 at school;
  no duplicate or clone can satisfy the final gate; and
- the same original child returning visible/in-world with PersonData[87]=0,
  the At School frame inactive, and final grade within initial ±1. The separate
  canonical missed-school branch is `CarPortal.iff:4117:4`, which adds 3 to the
  grade slot and therefore cannot pass this success law.

House 5 can load with an authored autonomous action already active ahead of the
new SchoolBus interaction. For fixture determinism only, after the exact
outbound create the harness finds the natural queued action by all of its
identity fields: routine 4098, interaction 0, SchoolBus code owner, and the
SchoolBus master callee. It requests normal cancellation only for a different,
cancellable, non-`MustRun` active blocker. A one-shot hard abort is permitted
only if that exact blocker survives three sim-minutes and the bus action remains
queued; the production game never enters this `-autotest` branch. The decisive
run needed one normal ToyBox cancellation and zero hard aborts.

Evidence in `tools/iff-dump/r165/`:

- `r165-school-bus-canon-scan.txt`: PASS; SHA-256
  `6b6acda99789aa372c80c52e4816af4c307390d021ec2384065bbc87cf951a9a`;
- `r165-schoolreturn-run1.log`: honest diagnostic miss—one exact outbound bus,
  no school latch or return bus, and grade 4→7 through the canonical missed-day
  penalty; this disproved the initial assumption that bus creation alone meant
  attendance;
- `r165-schoolreturn-run3.log`: decisive packaged `lot,schoolreturn` run,
  **2 passed, 0 failed, 0 skipped**. It records creates at 08:00 and 15:00,
  school latch at 09:02, outbound removal at 11:00, Cassandra home with grade
  3 by 15:10, return removal at 17:00, maximum one bus group, one normal
  cancellation, zero hard aborts, and clean shutdown; SHA-256
  `48046cae7930f2334e12d07cf395ce666fea9b0c2e7814d64a2cd8e60445196a`;
- `r165-gate-run1.log`: final packaged default gate, **103 passed, 0 failed, 0
  skipped**, wrapper exit 0, clean Run/Dispose, and zero `SimAnticsExc` or
  `bad-routine-frame`; SHA-256
  `456912f8853c3a266c1f4931997d9569bd24124251d6a1d87b938177db5e3a43`.

R165 changes no production engine or UI behavior. Its only code change is the
opt-in evidence harness; no layout, sizing, spacing, hit target, gesture,
animation, control placement, presentation, or proprietary asset changed.

## R166 Generic Sims Call 12 round

`genericcall12` closes the previously falling-through
`GetDistanceToCameraInTemp0` case. The owned original PPC engine proves a fixed
64x64 lookup, independent of current lot or viewport dimensions:

```text
r0=(x,y)  r1=(63-y,x)  r2=(63-x,63-y)  r3=(y,63-x)
Temp0=-(rotatedX+rotatedY); exit=true
```

The companion corpus scanner follows production IFF endianness and exact chunk
sizes. It finds 47 calls in 22 signatures / 21 distinct routine instruction
payload hashes across travel and vehicle code, including `SchoolBus.iff` BHAV
4121 instruction 9. No original data or code bytes are emitted.

Focused packaged invocation must include `corpus`:

```sh
NSUnbufferedIO=YES 'dist/The Sims-arm64.app/Contents/MacOS/TheSims' \
  "-path$PWD/game-data/The Sims" -autotest 5 \
  -autotest-opts lot,corpus,genericcall12 -autotest-timeout 1800000
```

Evidence in `tools/iff-dump/r166/`:

- `r166-original-engine-decode.txt`: deterministic PPC metadata/formula report,
  SHA-256 `68184fb0f3c0b484a23fe8bc3e9161d3e55a79daa7bc6e7d2148af1521b8e33b`;
- `r166-generic-call12.txt`: deterministic 47-caller report, SHA-256
  `1ee57c4e2d762e9adaad90f802d46ced584a49699e41289cc07ac350aa17a7d4`;
- `r166-genericcall12-run1.log`: clean focused segment, **7 passed, 0 failed,
  0 skipped**, live expected/actual `-42`, SHA-256
  `0718795c96e094e6bf4a5c30c4c6c64ea02c656233739c7103931c2799755273`;
- `r166-gate-run1.log`: final packaged default gate, **104 passed, 0 failed,
  0 skipped**, wrapper exit 0, clean Run/Dispose, and zero `SimAnticsExc` or
  `bad-routine-frame`, SHA-256
  `541f2c50863413299dbecd80df307cde83c616ad2f266332b8669c125b7b8448`.

The standalone probe passes all 16,384 actual handler dispatches. The original
reads live world rotation; the probe separately labels the non-rendered-world
TopLeft fallback as port-authored deterministic behavior. R166 changes no UI
layout, sizing, spacing, hit target, gesture, animation, control placement, or
presentation.

## R167 Generic Sims Call 13 round

`genericcall13` closes the valid owned-corpus path for
`AbortInteractions`. Original PPC recovery proves that mode 13 resolves Stack
Object ID, visits every object except that target, and sends each person
`Cleanup(target)`. People match the current object-use/stack state before the
queue and match queued interaction TargetID or Icon fields; matching records
are removed through Queue Skipped and the generic call exits true.

The complete owned-corpus scan finds exactly two sites: PhoneGlobals BHAV 8252
`import lot` instruction 1 and BHAV 8284 `main` instruction 1. Both routines'
entry expression is exactly `Stack Object ID := My Object.ObjectId`. The port
therefore implements the valid self-target law and throws on an unresolved id.
The original's error-10-then-`CleanupPeople(null)` aftermath is a disclosed
broad-cleanup residual, not guessed destructive behavior.

Focused packaged invocation must include `corpus`:

```sh
NSUnbufferedIO=YES 'dist/The Sims-arm64.app/Contents/MacOS/TheSims' \
  "-path$PWD/game-data/The Sims" -autotest 5 \
  -autotest-opts lot,corpus,genericcall13 -autotest-timeout 1800000
```

Evidence in `tools/iff-dump/r167/`:

- `r167-original-engine-decode.txt`: deterministic PPC metadata/law report,
  SHA-256 `d392328ebc078d502e9aec8aa081be224bd71b59a9d7917b8f4df9298c0c3b57`;
- `r167-generic-call13.txt`: deterministic exact two-caller report, SHA-256
  `6d94afa3f98267c7738a3d8344be987427d1d8148b9679fcab8091d9f8b75440`;
- `r167-genericcall13-run3.log`: clean focused segment, **7 passed, 0 failed,
  0 skipped**, remaining queue `[3]`, Temp0 12345, true exit, SHA-256
  `67fd583ab4d02d0f46cc9e2521698165f43fe4caa06b3e4315ac30bb48f1ce2e`;
- `r167-gate-run2.log`: final packaged default gate, **105 passed, 0 failed,
  0 skipped**, wrapper exit 0, clean Run/Dispose, and zero `SimAnticsExc` or
  `bad-routine-frame`, SHA-256
  `4949be1fee6ed303557247aae9dc1b5c2ff2df86aaf83f6676676f3eae0a7e2f`.

The standalone actual-dispatch probe isolates Callee and IconOwner queue
matches, active/frame matches, target exclusion, unrelated-state preservation,
non-skippable graceful cancellation, and Temp0/exit behavior. Its real Queue
Skipped fixture clears the actor stack synchronously and proves active cleanup
must happen first. R167 changes no UI layout, sizing, spacing, hit target,
gesture, animation, control placement, or presentation.

## R168 Generic Sims Call 14 round

`genericcall14` closes `HouseRadioStationEqualsTemp0`. The original PPC case
does not perform the comparison implied by that name: it loads signed Temp0,
calls `cSimulator::SetGlobal(31, Temp0)`, ignores Stack Object, and takes the
common true return. FreeSO's existing two-line behavior already matched, so
the nested engine change is comment-only.

The complete-corpus scan uses strict IFF termination while walking 8,871 files,
2,238 IFFs, and 28,246 BHAVs. It finds 18 calls in 13 IFFs, with 11 unique call-site signatures and
11 unique routine hashes. Every operand is `0e 00 00 00 00 00 00 00`, every
false pointer is 253, and all 22 incoming paths assign Temp0: 9 parameter
sites, 4 literal-2 sites, and 5 tuning sites.

Focused packaged invocation must include `corpus`:

```sh
NSUnbufferedIO=YES 'dist/The Sims-arm64.app/Contents/MacOS/TheSims' \
  -ApplePersistenceIgnoreState YES \
  "-path$PWD/game-data/The Sims" -autotest 5 \
  -autotest-opts lot,corpus,genericcall14 -autotest-timeout 1800000
```

Evidence in `tools/iff-dump/r168/`:

- `r168-original-engine-decode.txt`: deterministic PPC metadata/law report,
  SHA-256 `c3a8e31f8a90bacae3585399e71166c08b1df6609a3d5d842f63828e88ce3aa7`;
- `r168-generic-call14.txt`: deterministic strict 18-caller report, SHA-256
  `e611272da1d0a45e66074d65b85b6104f7f54129650092a0ad4b0dd3af05635e`;
- `r168-genericcall14-run2.log`: clean focused segment, **7 passed, 0 failed,
  0 skipped**, registered/raw-decoded production dispatch over eight signed
  values, clean Run/Dispose, SHA-256
  `b6e0645b958830c367c62fb1528d230b3ee5aa898342aa560e4f07481b80540f`;
- `r168-gate-run3.log`: final exact-source packaged default gate, **106
  passed, 0 failed, 0 skipped**, wrapper exit 0, natural 10:15 soak, clean
  Run/Dispose, and zero `SimAnticsExc` or `bad-routine-frame`, SHA-256
  `6a40e6e61dafc023b8bb71dfac324d1089dd88fd62734370937e5ecf740b4776`.

The standalone probe checks the raw operand, registered production handler,
eight signed values, exact global-31 overwrite, every other global, the full
Temp array, alternating null/nonzero Stack Object fixtures, and true exit. An
initial full attempt passed mode 14 but hit the already documented mood
sampler's one-decay-tick boundary; an unchanged rerun passed 106/106, and the
final evidence above comes from the subsequently hardened exact source. R168
changes no UI layout, sizing, spacing, hit target, gesture, animation, control
placement, or presentation.

## R169 desktop UI geometry repair

This is the user-authorized repair of the visibly broken desktop UI. It changes
protected layout and presentation only where the owned executable or original
art supplies a law. Touch composition remains unchanged.

Permanent checks for the exact-source build are:

- **uicp:** exact mode-patch offsets, full state sheets/cells (Pause 4x2),
  captured speed handlers, no fabricated floor ordinal, and the Lev2
  enable/force-down truth table;
- **uibargeom:** fixed 804x100 `PanelBack` at natural size, plus the custom
  people controls' exact reported/mouse geometry: eight webcam buttons at
  45x45, Mood at 32x39, and six tabs at 27x30. It also pins webcam indices
  0..7; production click routing reads the sender's immutable index instead of
  capturing the completed `for` variable;
- **uinav:** centered 800x600 artboard at large sizes, `(0,0)` clipping origin
  below 800x600, shifted banner/current/title/credits, and raw top controls;
- **uidlgchrome:** correct nine-slice scale multipliers plus the decoded
  `cWinPictureDialog` law: 36x36 `PopupInfoTiles` thirds, content `(21,21)`,
  native corners and tiled edges/center, native 120x120 object image, the
  separate 133x103 `SimStub` with its native 45x45 image centered, regular font
  slots 14/12 at exact 27/23px heights and default RGB(195,205,205),
  font-measured exclusion wrapping, adaptive 2:1 layout search, and two
  differently sized buttons proving independent widths (each minimum 100) and
  inside-body spacing; its inclusive ±0.05 aspect band accepts 1.95/2.05 and
  1.97/2.03, then rejects 1.94/2.06;
- **uiqueuegeom** (new default): 45x45 cells, `(32+50*i,32)` targets, pie-source
  inset 16, 500 ms movement, source rectangles, hit boxes, `IconFrame` and
  `QueueCancel`; and
- **uisurvey:** a single manager-tree draw, avoiding duplicate composition.

The original queue `RampGenerator` curve, the Lev2 engine-global-to-port state
mapping, the neighborhood title placement, and the picture-dialog
window-centering / measured-space wrapping / pre-minimum button-caption width /
automatic-icon pixel-composition bridges remain disclosed. The dialog font
resources and 23/27px heights are proved. The decoded picture law has no fixed
420px wrap, screen-minus-80 clamp, or 200px minimum.

Focused packaged invocation after publish:

```sh
NSUnbufferedIO=YES 'dist/The Sims-arm64.app/Contents/MacOS/TheSims' \
  -ApplePersistenceIgnoreState YES \
  "-path$PWD/game-data/The Sims" -autotest 5 \
  -autotest-opts lot,corpus,uicp,uibargeom,uiqueuegeom,uinav,uinbhd,uidlgchrome,uisurvey \
  -autotest-timeout 600000
```

Final exact-source publish/package completed with 0 build errors (expected
package-restore warnings only). The packaged focused invocation above passed
**13 passed, 0 failed, 0 skipped** at 2026-08-29 22:34:54 CDT. `uibargeom`
reported `peopleHit=8x45x45+32x39+6x27x30`. Its final
`uisurvey-dialog`, UCP, live, buy, and build images were visually clean: the
dialog was centered/on-screen with the tiled frame, the UCP had no ordinal, and
the bottom bars retained natural art. The wrapper reached clean `after Run` and
`after Dispose`; there were zero `SimAnticsExc` / `bad-routine-frame` records
and one known self-recovering startup `splashprogress mount` null reference.
Transient `/tmp/r169-authoritative-focused.log` has 699 lines and SHA-256:
`aa9a03104ae13d15ea1d953387ab042932c9da9ca1fe98d246966700a301d4de`.

The final exact-source packaged default passed **107 passed, 0 failed, 0
skipped** at 2026-08-29 22:43:04 CDT, including the same people-hit pin. It
reached clean `after Run` and `after Dispose`, with zero `SimAnticsExc` /
`bad-routine-frame` records and only the known self-recovering startup
`splashprogress mount` null reference. The 1,611-line transient log is
`/tmp/r169-authoritative-default-rerun.log`, SHA-256
`708e8a9d85985319d5f10f9710db07f11ae14c81f3c4270e9238fb1bddecbc72`.
R169's 107/107 was therefore that round's packaged full-default baseline.

One prior unchanged packaged attempt was manually aborted after it stopped
making progress in the known runtime-dialog harness path. No source or package
change preceded the passing exact rerun; the earlier attempt is an
infrastructure wedge, not a product failure.

## R170 primary UI composition repair

R170 is the user-authorized follow-up for the gray neighborhood surround,
paired/blurry product imagery, overflowing catalog descriptions, and LIVE
gauges/icons leaking into BUILD. It changes desktop layout/presentation only in
those reported paths; touch composition is unchanged.

The permanent checks are strengthened as follows:

- **uinav:** the live neighborhood background must resolve to the original
  1024x768 `Downtown\largeback.bmp` despite manifest-case differences, be an
  actual screen child, and sit at the exact screen-centered origin. The existing
  centered 800x600 aperture remains pinned.
- **uibandlaw:** runs its static law even without a live fixture; pins the
  74x37→37x37 horizontal-state crop and natural-size/down-fit behavior; checks
  actual `UIOriginalParagraph` children at x=0 and `i*LineHeight` inside the
  362px text column; proves price placement, measured height/anchor, all twelve
  complete control hit regions, owned/borrowed texture replacement/removal, and
  base-vs-overridden `GetThumb` dispatch (floor/wall reuse their cell icon, real
  dedicated popup providers retain their path).
  In the live BUY fixture it requires an ordinary object popup icon that is
  non-null, is not the catalog pair sheet, is not 74x37 or 37x37, and came from
  the object-iconic compositor.
- **uibuild:** distinguishes mode-boundary replacement from same-mode category
  fades. It requires both the incoming constructor fade and later opacity hold
  to execute, every older retiring subpanel to become invisible, and ordinary
  same-mode fade behavior to remain. Render-target positive controls prove that
  visible People/Arch custom chrome paints pixels and hidden chrome paints none;
  prior bindings and viewport are restored.
- **uisurvey:** requires graphics/main-panel/popup/neighborhood fixtures,
  chooses a popup item by original-font wrapped line count, requires a visible
  559px popup taller than 127px, composites the separate 3D world layer before
  UI, and suppresses the pre-first-update architecture touch helper. Capture
  exceptions are logged and attributed instead of silently producing a gray or
  missing artifact.

Focused packaged invocation after the exact-source publish:

```sh
NSUnbufferedIO=YES 'dist/The Sims-arm64.app/Contents/MacOS/TheSims' \
  -ApplePersistenceIgnoreState YES \
  "-path$PWD/game-data/The Sims" -autotest 5 \
  -autotest-opts lot,corpus,uinav,uibandlaw,uibuild,uibargeom,uisurvey \
  -autotest-timeout 600000
```

The focused package passed **11 passed, 0 failed, 0 skipped**, with clean
`after Run` / `after Dispose`. It produced required 1024x768 neighborhood,
buy-popup, and BUILD surveys: original blue neighborhood surround, a single
crisp object-iconic image with all description lines contained, and no stale
LIVE gauges/icons in BUILD. Focused log metadata:
`/tmp/r170-focused-sealed.log`, 694 lines, SHA-256
`396dcaf9d648d624745d004880bf80e2274e04a772b98e017729fd32a5d8435f`.

The final packaged default passed **107 passed, 0 failed, 0 skipped** at
2026-08-30 00:04:47 CDT, with clean `after Run` / `after Dispose`, zero
`SimAnticsExc` / `bad-routine-frame`, and only the known self-recovering startup
`splashprogress mount` null reference. `/tmp/r170-full-sealed.log` has 1,615
lines and SHA-256
`53cb538e777ea14b7d310e56e262f6705ee25fd98b1e08a2164a0c242055c690`.
Release build/publish/package completed with zero errors (77 existing
warnings). See
`tools/iff-dump/r170/r170-primary-ui-round.md` for the binary addresses, full
2,257-image corpus result, implementation/lifetime law, and disclosed
residuals.

## R171 remaining primary UI panel repair

R171 strengthens existing checks; the default remains 107. It is the
user-authorized follow-up for duplicated/misaligned LIVE children, empty-slot
heads, `Go Here` overflow, blank-blue action queue cells, the desktop Job
layout, and repeated build-subtool state sheets.

- **uilive:** performs a real desktop Mood → Job replacement, applies both
  queued tween writes, and requires the outgoing child to be invisible and
  render zero pixels, the incoming child to remain fully opaque, and exactly
  one desktop `UISubpanel` to be visible.
- **uijob:** requires the decoded 427x100 composition inside the 504x100 live
  host: title `(35,2)`, six skill rows at x=93/157 with 15px pitch, summary
  rows y=4/45/65/85, 28x22 career state, exactly one visible performance bar,
  zero modern duplicate labels, and live job/skill/friend values.
- **uidlgchrome:** constructs an original-font `Go Here` pie button through the
  production path and requires the caption advance plus the 16px total safety
  margin to fit both the painted button and its hit bounds.
- **uiqueuegeom:** keeps the existing 45x45/placement law and adds a render
  sentinel that can survive only when the opaque frame is painted before the
  action icon.
- **uibargeom:** requires an empty webcam to resolve the blank 45x45
  `PeopleTemplate` normal frame. It also pins the original 52x100 `BackPatch`
  at main-local `(-52,0)`, screen x=168..220, ordered after `PanelBack`.
- **uibuild:** pins equal-state crop rectangles (terrain 144x33→36x33,
  level/roof-related 156x33→39x33, pool/water 180x45→45x45, wall
  74x37→37x37), the live terrain cells, and exactly two pool/water sentinel
  products with original 45x45 cells and 145x103 popup art.
- **uisurvey:** adds separate Job, terrain, pool/water, and wall images while
  retaining LIVE, roof, neighborhood, and UCP artifacts. All are local-only
  under `~/Documents/Simitone/`.

Focused packaged invocation after the exact-source publish:

```sh
NSUnbufferedIO=YES 'dist/The Sims-arm64.app/Contents/MacOS/TheSims' \
  -ApplePersistenceIgnoreState YES \
  "-path$PWD/game-data/The Sims" -autotest 5 \
  -autotest-opts lot,corpus,uinav,uibandlaw,uibuild,uibargeom,uisurvey,uilive,uijob,uidlgchrome,uiqueuegeom,uicp \
  -autotest-timeout 600000
```

The focused package passed **16 passed, 0 failed, 0 skipped**, with clean
`after Run` / `after Dispose` at 2026-08-30 00:57:25 CDT. The 730-line
transient `/tmp/r171-focused-final.log` has SHA-256
`ea408b1f761249af9b3276039490f7ca52cbac0842f3cb2cfaa688b5409944ab`.

The final packaged default passed **107 passed, 0 failed, 0 skipped**, wrapper
exit 0, clean `after Run` / `after Dispose` at 2026-08-30 01:01:32 CDT, zero
`SimAnticsExc` / `bad-routine-frame`, and only the known self-recovering
startup `splashprogress mount` null reference. The 1,624-line transient
`/tmp/r171-full-final.log` has SHA-256
`3f07a16673639df622402d495db9000ee1cd51b08308f6a8b87b81794f85ce9a`.

Release build/publish/package completed with zero errors (78 existing
warnings). Publish and packaged `Simitone.Client.dll` are byte-identical at
SHA-256
`0f12cc7444d74ed56c86e779d3f8b49feb7601cd57dd9e821beb682e7fc48faf`.
See `tools/iff-dump/r171/r171-ui-panel-round.md` for the complete evidence and
residuals.

## R172 tooltip and catalog-popup composition repair

R172 strengthens four existing checks and extends the local survey; the
default remains 107.

- **uidtips:** renders the production tooltip path into transparent targets at
  1x and pins the recovered `cDefaultTTWindow` law: VariableSans 07,
  `textWidth+6` by `charHeight+2`, x=3, y=1 plus the raw-FFN two-pixel cache
  normalization. `Plant Tool` must produce box `(32,17)..(95,32)`, ink
  `(35,21)..(90,28)`, and `LastTablePx==7`. A two-line probe additionally
  requires box `(32,20)..(95,48)`, 13px-separated ink bands y=24..31 and
  y=37..44, and no ink in the intervening rows.
- **uibandlaw:** after a real cell MouseOver, requires the hover popup to be
  the topmost `MainPanel` child above the browse panel. Both ordinary and
  selected-cell MouseOut must hide it; the existing icon, anchor, ownership,
  dynamic-height, and teardown contracts remain pinned.
- **uidesc:** drives a real selected catalog item through full → hidden → full
  and requires the desktop plaque to preserve `Size.Y==FullHeight`,
  `ShowPCT==1`, and its full-height bottom anchor in every state. Hidden is
  wholly non-rendering (`Visible=false`, opacity 0), never a 45px crop.
- **uibargeom:** retains every R171 `BackPatch` size/position/local-order pin
  and additionally requires exactly one `MainPanel`, exactly one
  `DesktopUCP`, and frontend sibling order `MainPanel < DesktopUCP`, matching
  original parent-background-before-child composition.
- **uisurvey:** keeps the hover popup artifact and adds
  `uisurvey-buy-query.png` plus `uisurvey-buy-query-hidden.png`, captured
  through the real product-click path. The first must show the complete
  selected plaque above the toolbar; the second must contain no residual
  query strip. `uidtips` emits one- and two-line tooltip artifacts. All remain
  local under `~/Documents/Simitone/`.

Focused packaged invocation after the exact-source publish:

```sh
NSUnbufferedIO=YES 'dist/The Sims-arm64.app/Contents/MacOS/TheSims' \
  -ApplePersistenceIgnoreState YES \
  "-path$PWD/game-data/The Sims" -autotest 5 \
  -autotest-opts lot,corpus,uinav,uibandlaw,uibuild,uibargeom,uisurvey,uilive,uijob,uidlgchrome,uiqueuegeom,uicp,uidtips,uidesc \
  -autotest-timeout 600000
```

The focused package passed **18 passed, 0 failed, 0 skipped**, wrapper exit 0
and clean `after Run` / `after Dispose` at 2026-08-30 01:35:31 CDT. The
737-line transient `/tmp/r172-focused-final.log` has SHA-256
`5cadc3d5cf022c21c53dc51cba6074458dd1238d7ecb433816a64ff0b2a85097`.

The final packaged default passed **107 passed, 0 failed, 0 skipped**, wrapper
exit 0, clean `after Run` / `after Dispose` at 2026-08-30 01:39:48 CDT, zero
`SimAnticsExc` / `bad-routine-frame`, and only the known self-recovering
startup `splashprogress mount` null reference. The 1,627-line transient
`/tmp/r172-full-final.log` has SHA-256
`00e44ad612dddd9ec7c4412a7b45b787066a94eaf53f8c122f4efd497afc0fe7`.

Release build/publish/package completed with zero errors (78 existing
warnings). Publish and packaged `Simitone.Client.dll` are byte-identical at
SHA-256
`1c8b42e915dd1595462da014bc9ea8197513787963958c906d9aa02e692c29b9`.
See `tools/iff-dump/r172/r172-ui-tooltip-popup-round.md` for the original
binary/font evidence, implementation causes, and residuals.

## R202 — simvis corrected, fixed, promoted (115 → 116)

R201's `simvis` failed with "Vitaboy effect dead on GL." R202 disproved that
diagnosis with a full exoneration chain (the exact shipped vsVitaboy GLSL
compiles, links, and executes correctly on this Mac's GL 2.1 — 65536/65536 px
at bone indices 0/1/49 in a pure-GL FBO; the xnb loads through the exact
MonoGame 3.8.4 runtime with all constant buffers resolving; in-engine axis
bisection renders the real meshes at 79185 px with identity matrices and
64491 px with the real 29-bone skeleton) and located the real defect:
`PPXDepthEngine.RenderPPXDepth`'s non-MRT branch (UseMRT=false on this GL
path) leaves the render target bound to the DEPTH target, so the sprite
segments preceding `WorldEntities.DrawAvatars` in `World.InternalDraw` left
the avatar meshes drawing into the depth buffer — invisible in the composed
frame while every sprite-based surface rendered fine. The one-line restore
(mirroring the SoftwareDepth branch) took the framed live diff from 0 to
23175 px. The check itself was also corrected: the original unframed diff
compared a view where the sims project below the bottom edge (screen y
1512–1608 of 768), so it could never see them; the rewritten check pans
`State.CenterTile` onto a sim, logs every sim's projected screen position,
counts a manual `DrawAvatars` mesh-pass draw (21755 px) as an independent
aliveness signal, and restores the camera afterwards. `simvis` is now in the
default suite: **116 checks**, full default gate 116/0, clean exit probes.
Evidence: `tools/iff-dump/r202/` (probes: `glsl_probe.c`,
`glsl_render_probe.c`, `tools/r202_effect_probe/`).

## R203 — uidirt (dirt-tool error law, 116 → 117)

`uidirt` pins the terrain family's error surface decoded from the engine:
UIText.iff STR# 149 'DirtToolErrs' must exist verbatim (chunkID 149, 126
entries, 7 English, sha256 `8de848c01abe…`, exact 7-string sequence), must
mount live through `GameFacade.Strings.GetString("149", …)` (the tools' real
read path), and the ported law must hold — `TerrainToolErrors.Text` maps the
three live engine codes (1 → [0] 'Tile cannot be modified', 5 → [4] "Can't
divide a multi-tile object", 6 → [5] 'Insufficient funds') with Text(0)=null,
and `CostText` formats the engine's "$N"/"-$N" drag readout. The code = STR
index + 1 binding was proven by the money path (cLevelDirtTool::Drag stores
the cost, applies, then fails when SetFunds leaves funds below it → code 6).
All dirt-tool messages ride the shared BLACK tooltip (the R198 color law); the
port's invented DarkRed cost tooltip is retired. Evidence:
`tools/iff-dump/r203/`.

## Round 204 (uistrfam) — system string families + the close-out charter

Charter: the campaign switches from open-ended "as close to 1:1 as possible" to
CLOSE IT OUT — finish the enumerated inventory, one final hunt round, then
declare base-game 1.0 with the disclosure list as the official delta document.

`uistrfam` pins two families the port had wrong or missing:

- **STR# 148 'Pause'** — engine `cDDDSimsView::DrawPause` @0x214000 +
  `TSOnTimerMsg` @0x213830 decode the pause indicator as a top-left label that
  SHOWS on pause, HIDES when a timer fires (period `this+0x188`), and is
  re-shown by the next CPState update while still paused. STR# 148 supplies
  both halves: [0] 'Paused' (text), [1] '3000' (blink period ms). The new
  `UIOriginalPauseLabel` implements the law (system slot 11 + OriginalSystemTextColor
  via the shared InitSimsColors palette kinship); TS1GameScreen mounts it at
  both frontend sites.
- **STR# 152 'DefaultDialogButtons'** — system dialog buttons (typed
  OK/Yes/No/Cancel through UIMobileAlert and FSO.UI UIAlert, which includes the
  R197 native move-in AskDialogs) now read 152 [0]OK/[1]Cancel/[2]Yes/[3]No.
  Object dialogs (phone book, call neighbor, clothing/pet select) KEEP their
  142 'ObjDialogs' sourcing — the uidialog 142 canon pin and the uijob 142
  provenance pin both still pass (no pin weakened). The old FSO.UI named keys
  ("142","ok button") could never resolve in the index-only format −3 table —
  latent miss, fixed by the same swap.

Ledger corrections riding this round: Tier B's "165 SignPopups open" was stale
(already ported + live-pinned by uipeople's zodiacPopupLaw — retired with
citation); STR# 162 FriendCountDlg is fully decoded (cWinViewControl
PostChildDraw readout + VCtlTips[20] tooltip + the 162 About dialog) and ports
in R205.

Gate evidence: targeted soaks 6/0 (uistrfam+corpus) and 9/0
(uistrfam+uidialog+uijob+uitt+corpus); full default gate **118 passed, 0
failed**; clean after-Run/after-Dispose exit probes; AUTOTEST_WRAPPER_EXIT=0;
dist DLLs byte-match publish (Simitone.Client 84fe44ca, FSO.UI e467557b,
FSO.Client d4a04ab3, FSO.SimAntics 47be793f, FSO.Common f6ee1a07).

## Round 205 (uifriend) — the family friend count

`uifriend` closes the STR# 162 Tier B item: the engine's family-friend readout
(cWinViewControl::PostChildDraw compare-then-set, dedup law from
GetFamilyFriendCount) mounts on the desktop UCP directly below the money text,
carries the last-unused r117 tooltip (138[20] 'Family Friend Count'), and its
click opens the About dialog fed from STR# 162 top to bottom (title/body/OK).
This round also CORRECTS r144's gadget attribution: +0x1a8 is the friend-count
readout, not the clock (clock digits paint at TSPaint 0x2b5978).

Gate evidence: soak 9/0 (with uibudget/uidtips/uistrfam unregressed); full
default gate **119 passed, 0 failed**; clean exit probes; WRAPPER_EXIT=0; dist
byte-match 5/5 (Simitone.Client 28ba427e, FSO.UI 9f799351, FSO.Client
9c138b72, FSO.SimAntics 4cfa2c18, FSO.Common 5c8d3066).

## Round 206 (uipanelentry) — the permanent lot-entry control panel

Closes R199's disclosed residual: the original composes the control panel from
the first frame (RebuildControlPanel + EnteringHouse→SetMode); the port's
hidden-until-first-click reveal is retired on desktop via
UIMainPanel.ComposeAtLotEntry in the frontend ctor's LIVE branch.

Gate evidence: soak 8/0 (uilive/uifriend unregressed); full default gate
**120 passed, 0 failed** (one honest first-run FAIL on the exact-width live pin
— a one-frame X-drift race — replaced by the range pin with the exact-width law
kept on the deterministic ctor probe); clean exit probes; WRAPPER_EXIT=0; dist
byte-match 5/5 (Simitone.Client 66397436, FSO.UI 9f799351, FSO.Client
9c138b72, FSO.SimAntics 4cfa2c18, FSO.Common 5c8d3066).

## Round 207 (uicheat) — the cheat bar + ledger retirements

The cheat bar lands on the FinishSlowInit/TSOnKeyDown decode: (20,20) 200x21,
opaque RGB(64,93,95) with WHITE text, capacity 255, font 10, and BOTH toggle
chords (ctrl+shift+C and ctrl+shift+B). Two R159 census items retired as
stale with citations (the navbar law is landed via UINeighbourhoodSwitcher +
uinav; the select-sim overlay became desktop-unreachable in R199).

Gate evidence: soak 6/0 after two honest probe corrections; full default gate
**121 passed, 0 failed**; clean exit probes; WRAPPER_EXIT=0; dist byte-match
5/5 (Simitone.Client d492a114, FSO.UI 9f799351, FSO.Client 9c138b72,
FSO.SimAntics 4cfa2c18, FSO.Common 5c8d3066).

## Round 208 (uitrans) — the screen-switch law

The engine's base-game screen switches are direct constructions (HandleButton
0x2d8f30 → cWinDesignFamily ctor 0x2d2150, no wipe); the only engine transit
art is the expansion taxi family. The port's diagonal-stripe UITransDialog
wipes (trans_cas/trans_normal) were inventions and are retired: both call
sites switched to direct calls, the class deleted, ScreenSwitchLaw pins the
decode. `uitrans` joins the default suite — **122 checks**.

Gate evidence: soak 8/0 (uinav/uicas unregressed); full default gate
**122 passed, 0 failed**; clean exit probes; WRAPPER_EXIT=0; dist byte-match
5/5 (Simitone.Client b15ce58c, FSO.UI 9f799351, FSO.Client 9c138b72,
FSO.SimAntics 4cfa2c18, FSO.Common 5c8d3066).

## Round 209 (uivita) — the Vita idle canon

The four CAS Vita idle animation lists recovered byte-verbatim from the engine
data section (PEF sec1 unpack + TOC resolution), with the order-as-weight law,
the SetPerson gender split, and the 10000ms idle-sway SineGenerator. Pinned in
`UIOriginalVitaIdleLaw` and gated. Residuals disclosed: the pick virtual's
target (CFM-glued vtable) and the port-side playback integration.

Gate evidence: soak 6/0 after one honest child-weight recount; full default
gate **123 passed, 0 failed**; clean exit probes; WRAPPER_EXIT=0; dist
byte-match 5/5 (Simitone.Client e5ef7ad4, FSO.UI 9f799351, FSO.Client
9c138b72, FSO.SimAntics 4cfa2c18, FSO.Common 5c8d3066).

## Round 210 (uitotal) — the Tier C enforcement gate

The ui-total walker is live in the default suite: every painting desktop
texture must be original-registered, individually disclosed, a render target,
or a solid; hidden subtrees pruned; touch-only ancestors whitelisted. It
caught three real provenance gaps on bring-up (PanelBack's mount path, the
icon-cache renders, the ObjectDialog private BMPs) — all fixed by registering
the original-data paths. **124 checks**.

Gate evidence: full default gate **124 passed, 0 failed**; clean exit probes;
WRAPPER_EXIT=0; dist byte-match 5/5 (Simitone.Client 9ada5309, FSO.UI
9f799351, FSO.Client 9c138b72, FSO.SimAntics 4cfa2c18, FSO.Common 5c8d3066).

## Round 211 (the final hunt round) — BASE-GAME 1.0 DECLARED

The charter's terminus round: ported nothing, swept the 74-table UIText census
(all literal-unread candidates audited: dynamic reads, already-disclosed
absent systems, expansion-era, dev debris — no find above disclosed-grade),
the art axis (uitotal + the per-state pins), and the residual audit. The
declaration + delta-document index now head ../PARITY.md. The standing gate:
**124 checks, 0 failed**.

### R212 — uivitaplay (Vita idle playback)

The post-1.0 retirement of the R209 playback disclosure. The engine law:
`cWinVitaBtnSolo::AnimatePet` (0x2dac20) is a SEQUENTIAL cycle — the
channel-finished flag advances the cursor (this+0x210) with wrap at the list
count; the repeated breathe entries are the weights. `SetPerson` arms the
one-shot trigger (cursor −1 + trigger byte), `SetOutfit` does not re-arm,
and `TSPaint` drives the advance per paint through the window's own channel
(not the simulator). The check pins: every canon name resolving through the
real provider, the adult gender slot (loop2 male / loop1 female), the
10000ms sway sine (amplitude stays the disclosed BSS substitute), and a real
out-of-world avatar on the corpus vm driven through the real
`UIOriginalVitaIdlePlayer` — seed, 60-frame advance, the full 10-entry male
cycle in order with wrap, mid-cycle SetPerson reseed landing the female
slot, the child cycle (13 plays + exactly 1 empty-terminator skip, wrapped
to c2o-heyyou1), the avatar's own Animations list untouched, and the live
facing inside the sway band. Default suite 124 → **125**; two honest
first-run FAILs (un-armed ctor; cumulative counters) were fixed in the port
and the probe respectively.

### R213 — uicheathelp (the cheat help window)

The post-1.0 retirement of the R207 help-list disclosure (plus the SetMode
correction: 0x52fd62 is `SetInvalEveryFrame`, an 8-byte caret-repaint setter —
no edit mode exists). The engine law: `cTSWinCheatHelp` is the
registered-command autocomplete — edit event 1 opens the FULL registered list,
event 2 re-opens it prefix-filtered, Enter completes the selected name into
the bar and closes the help (a second Enter submits), Escape closes the help
only, and focus never leaves the bar. The check pins the 92-name engine roster
byte-verbatim (TOC[-0x5180] = 0x534d8 +0xb95..+0xf2c), the filter/selection
laws, and the live surface through a fresh `UICheatTextbox`. Default suite
125 → **126**; first soak run green, `uicheat` unregressed.

### R214 — uistrfam re-pin + uitotal paused fold (the pause label law)

The post-1.0 retirement of the R204 re-show-cadence disclosure. The decoded
engine law: `cDDDSimsView::DrawPause(show, subscribe)` cancels any pending
timer on EVERY call, and `TSOnTimerMsg` is a ONE-SHOT (hide + unsubscribe,
never resubscribes) — the symmetric blink never existed. LIVE is a steady
indicator (`CPState::Pause` and the latched world-sync both pass
`subscribe=false` with a mode==LIVE gate); BUY/BUILD/CAMERA entry shows the
label with the one 3000ms blink-out; OPTIONS shows steady; returning to LIVE
tracks the pause flag. `uistrfam` re-pins the law on the real mounted label
through the real vm (steady show across a crossed period — the old blink pin
now asserted ABSENT — unpause hides, BUY arms + one-shot hides + stays
hidden, CAMERA re-arm, OPTIONS cancel + steady, LIVE both ways, and the
counter deltas arms+2 / fires+1 / cancels+1). `uitotal` additionally folds
the PAUSED state into the provenance walk (pause the real vm, show the
label, walk the same tree again — presence + zero violations; the label's
glyphs paint through the atlas pipeline with no `Texture` member).
Default suite count unchanged at **126** (both are strengthenings of
existing checks, the R197 stronger-pin precedent); first soak run green.

### R215 — uiopts / uifriend re-pins + the uibldt floor-rim law (the user-defect round)

Four user-reported UI defects fixed on decoded engine law. `uiopts` now pins
the slider's 4-piece composition (`PieceWidthForProbe == 11` — caps and
thumb at natural size, only the middle stretches; no fractional fill) and
ONE shared Low/Med/High header row (`RadioValueLabels.Count == 3`, all at
y=7 — the engine has a single cWinTriText construction for the whole
screen; the per-radio second row that landed on the first radio is gone).
`uifriend` pins the friend digit's FIXED worst-case money anchor
(178 − "$9,999,999", the engine's once-at-Init rect) instead of the live
money left edge. `uibldt` gains a live floor-rim law: a whole-lot
FLOOR_RECT through the REAL preview path (`SimulateCommands` → `VisFloors`,
with a REAL catalog floor pattern — the dispatch silently drops unknown
pattern ids, caught on the first soak run) must leave the outer ring
untouched while painting an interior tile. Default suite count unchanged
at **126** (all three are strengthenings); two honest first-run probe
fixes.

### R216 — uiopts / uifriend / uibldt re-pins (the R215 residual round)

Three corrections to the R215 laws. `uiopts` adds the middle piece's scale
law: `MiddleScaleXForProbe × PieceWidthForProbe == 108` — DrawLocalTexture's
scale is a SpriteBatch MULTIPLIER, and R215 passed the pixel width 108 as
the multiplier (an 11px sample stretched to 1188px, the whole-screen
slider). `uifriend`'s geometry pin drops the `+ LineHeight` vertical offset:
the creation-block decode (0x2b7240–0x7608, arg order pinned by the money
gadget's own SetArea at 0x2b72f4) reads `SetArea(moneyLeft−w, moneyTop,
moneyLeft, moneyTop+fontH)` — the digit paints ON the money line at the
fixed worst-case anchor, so the pin is `Y == MoneyOriginal.Y` (the R144
"moneyBottom" prose was a misread of the friend gadget's own bottom).
`uibldt`'s floorRim law is re-pinned as the TWO-LAYER law: (a)
`ClipFloorRectToFloorable(whole-lot) == (1,1,w−3,h−3)` (the TOOL-layer port
of TileIsFloorable's rim rejection, applied in UIFloorPainter before the
command exists), (b) an UNCLIPPED whole-lot rect through the real preview
path PAINTS the rim — the primitive stays unrestricted
(RestoreTerrain writes full-lot strips through it; R215's clip left a stale
border), (c) the clipped rect leaves the rim untouched and paints the
interior. Default suite count unchanged at **126**; targeted soak
uiopts/uifriend/uibldt + corpus 8/8 green FIRST RUN.

### R217 — uismall re-pin (the phantom view-menu flyout retired)

User report: extra rotate/zoom buttons pop up next to the real ones while
holding the mouse on them — the R193 ViewMenu plaque mounts. The corrected
decode (r217/r217-viewmenu-pie-law.md): the four sheets are cTSPieMenu
ITEMS (cDDDSimsView::Init registers them with InsertString into the pie at
view+0x13c; ViewMenuBackground.bmp 17x17 = member 825, the popup chip;
UpdateViewMenu @0x215b42 refreshes the item ladder cells), rendered only
inside the pie popup — while the panel buttons' own law is an animated
step (DoSpeedTransitionSound), no flyout at all. `uismall` now pins the
art family INCLUDING ViewMenuBackground 17x17, and the live label pins are
replaced by an ABSENCE assertion (R214 precedent): zero ViewMenu-sized
cells anywhere in the desktop UCP subtree, the four small zoom/rotate
buttons still mounted. Default suite count unchanged at **126**; targeted
soak uismall + corpus 6/6 green FIRST RUN.

### R218 — uiviewpie added (the view pie wired; 126 → 127)

User: "I would like everything wired up." The engine's view pie
(cDDDSimsView+0x13c cTSPieMenu, four ViewMenu sheet items — the r217
decode) is ported as `UIOriginalViewPie` and torn off the small zoom/rotate
buttons by the R218 gesture (press-drag ≥ DragThreshold opens the radial at
the button; a plain click still steps — the engine's button path is
step-only, proven). The check drives the REAL open path
(`OpenViewPieForProbe`): 4 items in the engine's InsertString order
(ZoomOut/RotateRight/ZoomIn/RotateLeft) with the original cell dims
(42x84, 84x42) on the radius-90 even spokes; the ladder cells
(`CellFor`: zoom base = level*2 of 9, rotate ±1 of center 2) + the consts
(radius 90, threshold 10); dedupe (one pie at a time); selection dispatch
through the UCP's own step handlers via the pie's real completion
(`CompleteForProbe` — Rotation pinned with the port-native camera hold-gate
honestly tolerated); cancel/close cleanup + re-open. The FIRST soak run
caught a real bug (cancel left the UCP's ViewPie reference stale,
suppressing the step handlers forever) — fixed with the always-fired
onClose callback; both runs kept in targeted-soak.log. Default suite count
126 → **127**; uiviewpie/uismall/uipie + corpus 8/8; FULL GATE **127/0**.

### R219 — deathtrace added (opt-in; the natural-death runtime trace; count stays 127)

The last full non-expansion PARITY `[GAP]` (relationship→death, runnable-but-
never-caught since R77) closed. Decode first: the R77 probe collapsed motives
to +3, but the real `check motive failure`(8299) gate is a literal
Energy/Hunger/Bladder **< -98** (FreeSO `VMExpressionOperand` grammar; opcodes
≥256 are Global.iff ROUTINE calls, not primitives). The full law:
8283 ins5 → 8299 → (hunger) `failure hunger`(8198) → Global 393
`kill person for good` → Global 316 `do grim reaper`; 393 ins8 creates the
UrnStone.iff tombstone (5A6FA529), ins26 the reaper NPC (4B15D4B0), and sets
PersonData[68]:=1. The new state-4 soak (`StateDeathTrace`) collapses avatar0's
Hunger only, dense-samples the canon death ids, polls the dead flags, and
observes creates via the `ObjectCreated` event. PASS = handler + kill + dead
state — all observed at exact decoded instruction sites in three runs.

Two honest artifacts, both documented in the r219 law doc: (1) the death
notice is a **blocking modal owned by the tombstone** (UrnStone.iff) —
untouched soaks froze at the latch (looked like a hang; the game loop was
turning, only the VM clock stopped); the probe now names the latch owner and
releases it as the player would (same release the LOT-READY site uses). The
7:00 latch that fired first was the morning carpool dialog (CarPortal.iff,
the r157 family) — coincidence, unrelated to death. (2) The starvation grace
is nominally 200 ticks but the engine's `VMSleep` semantics (elapsed-idle
decrement + interrupts) spend ~10 sim-min in practice — identical with and
without a player present. Ghost night-haunt loop (8641) stays disclosed:
hour-gated, outside the post-death window. deathtrace is opt-in like
carreturn/schoolreturn — default suite count unchanged at **127**; untouched
targeted soak 6/0; FULL GATE **127/0**.

### R220 — ghosttrace added (opt-in; the ghost night-haunt; count stays 127)

The R219 residual closed one round later. Decode: UrnStone.iff (the tombstone
the kill creates) runs `main`(4096) whose ins5 gate is `Global[0] >= 22`
(read live — `VM.GetGlobalValue(0)` returns `Context.Clock.Hours`; night =
22:00–01:59); per ~30-sim-min night pass it rolls `rnd(0..8)` (BCON4097[3])
and on 0 calls `generate ghost`(4100) — create + place + push-interaction +
sound, capped at 3 concurrent dead persons. The ghost LOOP is person-side:
`person main` ins38 → 8641 `Ghost - Main Loop` on the dead sim's OWN entity
(the dead person IS the ghost — matching the R80 shipped-save IsGhost
residents). The urn also ships its own `Force Ghost` debug tree (4109).

`ghosttrace` rides the deathtrace state machine: after the death settle the
clock is set to 21:55 so the urn's OWN gate crosses naturally at 22:00; the
urn entity comes from the tombstone create event; ghost ids are matched with
IFF attribution (UrnStone 4100 collides with CarPortal 4100 'process'); the
walk list refreshes for a newly materialized ghost avatar and also walks the
urn's thread; a lot-unload guard evaluates honestly; if no spawn by night+45
the urn's own Force-Ghost TTAB interaction is pushed (disclosed). Observed
untouched: the loop executed at 01:48 — 8641 + 8638 at the exact decoded
call site, on obj16 itself. Disclosed residuals (r220 law doc): the one-tick
4100 frame fell between samples (the nightfall avatar join attributes it),
and Ghost Scare/haunt-sound didn't execute in-window. Run 1 caught a harness
bug (ghosttrace wasn't in the deathtrace entry condition — fixed same
round; log kept). Untouched soak 7/0; FULL GATE **127/0**.

### R221 — censorpixel added (the censor blur's pixel level; 127 → 128, default suite)

The last unverified stage of the censorship pipeline. Two discoveries drove
the design: (1) the R36 `GetLotThumb` render pair was evidence-free — it
differs by exactly 0 pixels (that path never rasterizes the avatar censor
overlay; measured on the committed r36 artifacts), so the check captures the
LIVE composition instead (simvis grab idiom, flags forced 0 then 3); (2) the
analysis needed four honest iterations — raw diff bbox is inflated by
animation drift between grabs, the mosaic palette matches the sim's own skin,
and the mosaic draws at the PELVIS ~125px above the sim's ground-projected
point — final law: slide a 96×112 window over the sim's vicinity, take the
densest diff cluster. Passing numbers: 97% of ALL diff pixels inside the
window, density 0.54, palette fraction 0.80, flat-run median 7 (the port's
8×8 grid cells over the zoom rect). The ORIGINAL's law decoded from the
binary for the disclosure: HouseViewer::Censor @0x1ce894 (per-cell MEAN of
the underlying pixels + a ±8-per-channel random jitter + clamp — the r141
symbol index is +2 off in this region, found via prologue scan) and
RenderCensoredBlocks @0x1ce5b4 (the 1×3/2×6/4×12 zoom cell ladder). The
port's static 12-color palette texture is the disclosed approximation.
Promoted to the default suite on the simvis precedent: **127 → 128**;
targeted soak 7/0 (runs 1/3/4 kept as the analysis-evolution record);
FULL GATE **128/0**.

### R222 — chancetrace added (opt-in; the career Chance Card; count stays 128)

The R163-named residual closed. Decode (from the staged CarPortal.iff; r222
law doc + annotated listings in `tools/iff-dump/r222/`): `get paid` (4103)
rolls `min(rnd(0..99), rnd(0..99))` at ins39-42 and forks at ins43
`MyPD[63] > Local[6]` — TRUE tries 4104 `test promotion` (whose failure
also falls through), FALSE calls 4134 `Chance Cards 3` directly. 4134's
entry gates on `PD[24]==0` (the shown-latch, reset by the pay sequence)
then rolls **`rnd(0..99) < Tuning[135]` = BCON 4097[7] = 12 — a 12% card
chance per pay** (the probability gate was found live: run 2 rolled it
honestly and missed). The cascade dispatches careers 17-21 (4134) / 1-10
(4124) / 12-16 (4133), each card body raising a BLOCKING STR#301 dialog
(title `$Me`, named icon `job $Local:9`) whose response applies
budget/skill/sound effects and sets PD[24]=1.

The check rides the carreturn state machine on House 5 with three DISCLOSED
probe state-sets (harness-side only): job level 0→5 (Science level 5 → the
known Science02 card), PD[63]→−50 at end−1h (the fork deterministically
false), and TuningReplacement (4097,7)=100 on the roll's Callee — the
CarPortal (run 3's worker-side-only override proved VMMemory's scope split:
`MyPersonData` reads `frame.Caller`, `Tuning` reads `frame.Callee`). A new
worker-stack tracer (the R157 portal instrument pointed at the worker's
thread) recorded the exact live chain
`[4102:13][4103:54][4134:120][4124:55]`; the probe answered the card dialog
through the exact `VMNetDialogResponseCmd` TS1 law (ResponseCode=0), and the
effects landed: `4124:56 = +5000`, PD[12] +100, PD[24]=1, then the salary
`4103:9 = +540` after the welcome-home dialog. PASS = fixture + pay window +
live cascade + answered card + post-response effects. Opt-in like
carreturn/schoolreturn — default suite count unchanged at **128**; targeted
soak PASS 6/0 (runs 1-3 kept as the honest evolution record: harness
ordering bug, the 12% gate, the Callee split); FULL GATE **128/0**.
