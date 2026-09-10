# R162 — the HELP window + kHelpBtn (UI-100% round 4)

Round mandate: campaign item 4 — the help/tutorial system.

## Engine decode (this round; symbols r141, disasm in this directory)

- **kHelpBtn 4993**: CPanel/Buttons/HelpButton.BMP, a 40x15 sheet = 4 states
  of 10x15, mounted on the control panel at **(5,164)** (the r144
  toolbar-law anchor — canon since r144, never ported before this round).
- **The handler** (r159 scout §5, confirmed): creates (once) a
  `cWinHelp(cITSFont*)` (ctor 0x278470, 384 B) cached at UCP this+448, calls
  `SetSelected(GetSelected())` (0x2777c2/0x277822 — the page PERSISTS
  across opens), then `SetBlockSimulator(true)` -> DoSimsModalDialog ->
  resume — the same modal flow as the budget window (r161).
- **cWinHelp::Init @0x277cc0** (1728 B): loads WINDOW TEMPLATE 3008 through
  the same 0x3b6190 by-id loader as cWinBudgetDlg — the standard engine
  modal template family (in the port: the R142 dialog chrome law — GenDlg
  nine-slice + RGB(0,0,82) fill + WinBtn row). Builds the topic list +
  body text windows (cTSWinText ctor family 0x537680) + a 272-B scroller.
- **cWinHelp::TSPaint @0x277b20**: draws the topic-list scroll thumb with
  the /3 proportional arithmetic (0x55555556 mulhw chain) over the
  scroller's track rect.
- **STR# 166 'Help'** (r162-helpstrings-canon.txt, sha pinned in the gate):
  1598 entries; English block [0..93] = **47 TOPIC PAIRS** (title even,
  body odd) — 'Auto-Snapshot' .. 'Windows - Delete', incl. embedded \r\n
  TIP paragraphs.
- **The TUTORIAL system is separate and deferred** (disclosed): TutHigh
  4980 (CPanel/TutHigh.bmp 150x50) is the lesson HIGHLIGHT art;
  `TryTutorial` @0xf7082 is a SimAntics BHAV primitive (the lesson machine
  lives in object scripts, not window code); UserData/Tutorial.FAM is the
  tutorial household save. Porting it = the guided-lesson flow, a later
  round.

## What shipped

1. **kHelpBtn on the plate** (UIDesktopUCP): the 10x15 four-state sheet at
   the engine anchor (5,164), click -> the help modal. The "?" returns to
   the control panel for the first time.
2. **UIOriginalHelpDialog** (UI/Panels/UIOriginalHelpDialog.cs): the R142
   chrome law window (GenDlg nine-slice + navy fill + the WinBtn
   PositionButtons row for Close), a 47-topic scrollable list (12 visible,
   18px pitch, wheel + arrow keys — the engine's proportional thumb is
   disclosed-undrawn), the wrapped body via R113's UIOriginalParagraph,
   cyan selection (the engine's highlight family), Up/Down/Esc key law
   (TSOnKeyDown), and the SetSelected/GetSelected persistence law
   (LastSelected survives closes).
3. **'uihelp' check 98->99**: STR# 166 chunk sha + label + count + spot
   pins (first/last titles, two bodies, one mid title), art dims
   (HelpButton 40x15 + TutHigh 150x50 present), LIVE dialog ctor (47
   topics == disk, Select/scroll-clamp/persistence, bodies == disk) AND
   the live desktop UCP carrying kHelpBtn at (5,164) with the 10x15 cell.

## Honest log

- targeted-run1 + gate-run1 FAILs kept (the artifacts): the gate's OWN
  spot-pin had a missing `!` (`strs[13].Contains(...)` un-negated ->
  canon unconditionally false — the port was correct; found via the
  canonDetail diagnostic line, which stays in the check as evidence).
  The first "non-deterministic canon" theory was wrong — it was
  deterministic gate-side, same root cause in both runs.
- FULL GATE (run2): 99/99 PASS, clean exit chain (after Run / after
  Dispose / AUTOTEST_WRAPPER_EXIT=0), carseek intact (spawnedCars=1),
  uibudget/uinav intact.
- dist DLL byte-match: Simitone.Client e623177a…, FSO.SimAntics 903db62a…
  (unchanged).

## Residuals (disclosed, not pinned)

- The template-3008 pane rects are not recovered — window size 460x400,
  the 160px topic column, 12-visible/18px list are the port's on the
  pinned chrome law; the scrollbar thumb is not drawn (wheel/keys only).
- The window title 'Help' is a literal (engine reads [TOC-29048]+48).
- The tutorial/lesson machine (TutHigh highlights + TryTutorial BHAV +
  Tutorial.FAM) is deferred — the help WINDOW is this round's scope.

## Push-integrity fix (post-round, user "commit and push" audit)

The R159-R162 snapshot chain's engine gitlink (local mac-port-rel tip
412daf45) was DANGLING on the private engine repo: that repo carried only
the single squash f038d0eb, and the local FreeSO clone is SHALLOW — a
direct push of mac-port-rel fails with "did not receive expected object"
(the shallow boundary breaks the pack; the full unshallow push would be
~600MB of public upstream history). Fixed surgically:

- Engine repo main fast-forwarded to **e94b3aeb** — a commit with the
  IDENTICAL tree (d6f73359 — tree(f038d0eb) == tree(412daf45),
  verified) parented on the squash. Kilobyte push; the engine content
  every snapshot references is now fetchable BY HASH.
- The R162 snapshot was rebuilt once with gitlink e94b3aeb
  (3ce6f7e -> 31c2b8f, force-with-lease; identical tree except the
  engine entry) — `git clone --recurse-submodules` of the snapshot tip
  now resolves.
- DISCLOSED: the R159-R161 snapshots (e606412/473874d/8e01c6d) still
  carry gitlink 412daf45 — the same tree, available as engine
  mac-port-rel/main; only historical checkouts of those exact commits
  alias instead of resolve.
- GOING FORWARD: whenever a round advances the engine pointer, publish
  the new engine tree as a commit on the private repo (github remote now
  configured in the submodule) and build that round's Simitone snapshot
  with the PUBLISHED engine hash.

## Branch consolidation (user follow-up: one branch per repo)

- Engine repo: `mac-port-rel` deleted, `main` fast-forwarded to e94b3aeb
  (direct child of the squash — no rewrite). ONE branch.
- Simitone repo: the stale remote `mac-port` (8ddd93f, a pre-audit-era
  push whose history embeds the EA bytes the audit later stripped; fully
  preserved in local history) deleted after retargeting the default
  branch. ONE branch: `mac-port-snapshot`.
- LOCAL layout (unchanged, deliberate): `mac-port` = the single working
  branch (full 160+-round history; old commits embed EA bytes, so it can
  NEVER be pushed) + `simitone-forked` = the upstream mirror used to
  pull upstream changes. The remote snapshot chain is GENERATED from
  mac-port each round (tree-identical, one commit per round) — a
  publication artifact, never worked on.
