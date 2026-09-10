# R156 — THE COMMUTE WINDOW FIXED + NATIVE BRANDING (user round)

Two halves, one round (user request): (a) the R155-recommended commute-tail
trace, (b) de-branding the app — remove Simitone branding, replace with
native The Sims identity, including the app icon. Baseline: main 85fc03c /
FreeSO e4a31fab, green via r155-gate-run1.log (94/94, clean exit).

## PART A — THE COMMUTE WINDOW (R156a)

**The R155 window math held and the fixture bug is fixed: CarPortal
'process' (4100) triggers 'Create Car' (4106) when Global[0] == Attr[0]-2 —
TWO hours before the bookmarked work start (ins12-14: Local[3] =
Attr[0]-2, staged via Attr[2]/Attr[3] across top-of-hour ticks, 'main'
gates process to minutes 0-4). The gate fixture loaded at 8:00 with a 9:00
job — the 7:00 spawn window was ALWAYS missed.** Fix (harness, no engine
change): CarpoolDiscovery rewinds the VM clock to (earliest StartTime -
125 min) when the load time is past it — logged explicitly as
`carseek clock-rewind (harness)`, Ticks adjusted to keep the clock
consistent; equivalent to the player setting the clock before a soak.

**Result: window hit — car STILL did not spawn. The residual narrows to
the per-person iteration inside 'process':**

* r156-window-run2 per-minute guard log at the exact 7:00-7:05 window:
  `attrs[0=0, …, 8=-1]` — Attr[0] bookmarked **0** (from a NON-working
  sim: obj=16 carries JobType=-1; GetJobData(0xFFFF, …) → 0 via the ??
  0 fallback) and Attr[8] stays -1 (the working sim obj=21's bookmark
  branch never completes).
* The per-person gates (decoded): JobType>0, PersonType==0, word69 != -1,
  StackObject VARIABLE 34 != 1 (objVar34=0 for all three avatars — NOT the
  skipper), then Local[4] = JobData[12]-1 vs Global[0).
* Static suspects eliminated this round: JobData scope IS implemented
  (VMMemory.cs:136-138); GetJobData(9,0,12) maps to CARR.StartTime
  (CARR.cs:44-46); VMSetToNext (the person iterator) fully implemented.
* NEXT TRACE (R157 candidate): instrument the 4100 frame per person —
  which gate line skips obj=21, or whether the iterator never reaches it
  (portal 'process' iterates ALL lot entities; the bookmark pollution by
  the JobType=-1 sim suggests the family filter differs from the
  original's).

The carseek PASS law is UNCHANGED (R155's portal-scoped 4096+280+4100+4125
+ label anchor + portal presence) — verified still green under the rewind
(r156-window-run1/2, 7/7). Diagnostics added (all logged, none gating):
clock-rewind line, per-minute portal attrs (minutes 0-5), commute-inputs
with objVar34.

## PART B — NATIVE BRANDING (R156b, user request)

"Remove Simitone branding. Replacing it with native. This includes the
app icon."

* **App icon**: the original ships NO standalone plumbob asset (the
  Complete Collection boot art is the Makin' Magic variant — verified by
  extraction + vision; the 70x52 logo stamps are RLE8 wordmarks — too
  small for an icon). The icon is therefore DRAWN programmatically as the
  game's own symbol: the green four-facet mood-crystal octahedron,
  4x-supersampled at every iconset size (16..1024). Generator:
  `tools/make_native_icon.py` (pure stdlib; deterministic — iconutil
  output sha256 f04d652fcf427e89f36f1d2c81203cb48e0f37a4f8744511fab184168233dc03).
  Generated at PACK time by packmac.sh into build/NativeIcon.icns
  (gitignored — no proprietary bytes read or committed; the old committed
  build/Icon.icns stays as a fallback only).
* **Bundle identity** (Info.plist): CFBundleName TheSims,
  CFBundleDisplayName The Sims, CFBundleExecutable TheSims, identifier
  com.thesims.macos; zero 'simitone' substrings.
* **Executable/assembly**: AssemblyName Simitone → TheSims (csproj +
  Product/AssemblyTitle "The Sims"); the bundle is
  `dist/The Sims-arm64.app` (packmac), executable `Contents/MacOS/TheSims`.
* **User-visible strings**: InstallationInfoDialog ("The Sims has been
  configured…", "Saves (macOS port):…", "this port uses separate save
  files…"), Program.cs OOM ("The Sims needs to close") + console help
  (`./TheSims -path…`).
* **Already native from earlier rounds**: window title "The Sims" + boot
  screen kSimsLogo + update-alert removed (R118); in-game UI is original
  art throughout (R67-R155 corpus).
* **Deliberately KEPT (functional, not branding)**: the user-data
  directory `~/Documents/Simitone/` (holds the fixture saves — renaming
  would orphan them); the FSOV save chunk label "Simitone Lot Data" (a
  save-format marker, never displayed); code namespaces (identifiers).
* **Docs/harness**: run-autotest.sh BIN → the new bundle path; PORT_STATUS/
  AUTOTEST/SOP/README/HANDOFF path references updated; SOP pkill hint →
  `pkill -9 -f TheSims`.
* **GATE: NEW `uibrand` (94 → 95)**: pins Info.plist (TheSims/The Sims/no
  simitone substring), the executable name, the deterministic plumbob
  icon sha (and icns magic), and the window title. First targeted run:
  `uibrand: plist=True exe=True icon=True (len=83420 sha=f04d652f…)
  title=[The Sims] PASS`.

## Files (tools/iff-dump/r156/)

r156-round.md (this doc), r156-window-run1.log (rewind active, tail still
un-fired, R155 law green 7/7), r156-window-run2.log (per-minute guard
attrs — THE narrowing evidence), r156-targeted-run1.log (renamed bundle +
uibrand PASS + carseek), r156-gate-run1.log (full suite, see PARITY).
Static side: /tmp/setup.bmp extraction (MM boot art — no plumbob; not
committed), make_native_icon.py in tools/.

## Residuals (honest)

1. The commute tail remains un-fired; the failing node is inside 4100's
   per-person iteration (Part A) — the trace is now one instrumented step
   away (per-person gate logging).
2. The plumbob icon is a programmatic rendering (no original high-res
   asset exists in the shipped data) — facet greens are classic, not
   byte-sampled from a sprite; disclosed as original-artwork-in-kind.
3. dist still contains the old Simitone-macOS-AppleSilicon.dmg (upstream
   artifact, not part of the port pipeline; left in place).

---
**R157 CORRECTION + CLOSURE:** Part A's two framings are corrected by r157:
(1) the trigger is Global[0] == Attr[0]-**1**, not -2 (the -2 sequence
ins12-14 is dead code — T34/F34 both-pointers-equal; see ../r157/), and
(2) the residual was never "4100's per-person iteration" — the person loop
never ran at all. 'process' froze at 4125 ins15 (a BLOCKING dialog) because
the port's TS1 GlobalBlockingDialog latch leaked at lot load
(HelpSystemMagic obj 296's help dialog + the harness's force-unpause, which
orphaned the latch the player's click would have released). r157 fixed the
engine (queued-dialog state + shared timeout + latch release on completion)
and the harness unpauses; the 8:00 run then bookmarked Attr[0]=9 and
'Create Car' spawned the carpool (18 cars, send-to-work/At-Work/At-School
all live). See ../r157/r157-round.md.
