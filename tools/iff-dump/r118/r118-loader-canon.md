# R118 — the REAL loading screen only, from frame one (Simitone loader plumbing deleted)

## What the user saw and why (2-session Hermes survey)

Screenshot symptoms mapped to code, all in `LoadingScreen` (UI/Screens/LoadingGameScreen.cs):

| Symptom | Element (pre-R118) |
|---|---|
| macOS title bar "Simitone" | `SimitoneGame.cs:457 Window.Title = "Simitone"` |
| green→dark gradient band | `UISimitoneBg` default art `load_static_bg.png` (unconditional from frame 1) |
| "Simitone" wordmark | `UISimitoneLogo` = `load_logo.png` (941x159), tweened to screen-top |
| bouncing musical notes | `UILoadProgress` — elastic segmented bar whose art is a musical staff (`load_bar_bg.png`: treble clef + sharps) with eighth-note slices (`load_bar_content.png`) tweened `TweenElastic.EaseOut` |
| unreadable text (white/gray blocks) | `UISimitoneLoadLabel` — MSDF VectorFont labels at Size 37 (≈3.2x scale blow-up; the MSDF shader renders as blocks on this GL path) |
| black bands | the two `UIDiagonalStripe`s |
| "A new version of Simitone" dialog | `Services/UpdateChecker.cs`, called from TS1GameScreen ctor |

ROOT CAUSE of the residue window: R88/R111 mounted the original art in `Update()` only after
the content thread resolved TS1Global (~1.5s typical, longer cold). Until then ALL the chrome
above was fully visible. The survey also proved the window title and the update dialog were
unconditional Simitone-branding surfaces.

## What R118 did

1. **The REAL boot art from frame one.** `MountBootOriginalDirect()` runs in the ctor,
   synchronously: `FAR1Archive` over `TS1HybridBasePath/UIGraphics/UIGraphics.far` (v1a
   manifest — 32-bit filename lengths, proven by the r83/r88 canon tools' own python walk,
   `tools/iff-dump/_farnames_scan.py`; the v1b flag desyncs the manifest, r118p1), extracts
   `Other\setup.bmp` (kSimsLogo, 1,440,056 bytes) byte-verbatim, decodes via
   `Texture2D.FromStream`, shows it immediately. Published state: `BootDirectMounted`,
   `BootDirectSha`, `BootDirectLen`; log line "DIRECT FAR mount ... (from frame one)".
   FAR1 members are stored UNCOMPRESSED, so this is one file read + decode — no
   content-thread dependency, no pre-mount window.
2. **Plumbing DELETED** (not hidden): `UILoadProgress.cs` (the notes bar) removed from disk;
   `UISimitoneLogo` + `UISimitoneLoadLabel` nested classes and the `LoadText` joke strings
   ("Reticulating Splines...", replaced by the R111 canon scroll long ago) deleted;
   `InterpolatedAnimation` tween machinery and both `UIDiagonalStripe`s dropped from the
   loader (`UIDiagonalStripe` itself stays — UIMainPanel/UIHouseSelectPanel/UIFamilyCASPanel/
   UITransDialog use it); `Close()` is now a simple fade of the original art.
   `UISimitoneBg(loadModernFallback: false)` — the loader never touches `load_static_bg.png`;
   the ONLY fallback if the direct read ever fails is a plain navy (#000029, uipal anchor)
   fill — Simitone art can never appear on the loading screen again.
3. **Update-check dialog removed**: `Services/UpdateChecker.cs` deleted + its TS1GameScreen
   call ("A new version of Simitone is available!" — MSDF text + mint pill button). The
   original game has no update plumbing.
4. **Window title** → `"The Sims"` (SimitoneGame.cs).

Kept exactly as-is: the R83 GO screen at load-complete, the R88 engine-drawn bar (the
original declares no bar asset — proven by template absence), the R111 splash strings in
original glyphs (their font still needs the TS1Global mount, so text appears ~0.15s after
the art — art first, text shortly after; never modern text).

## Gate (uiboot STRENGTHENED, no new check; suite stays 66)

`CheckUIBoot` now additionally requires `BootDirectMounted && BootDirectLen == 1440056 &&
BootDirectSha == R88BootCanon["Other\setup.bmp"].Sha256` — the ctor-time direct read must be
sha256-identical to the pinned canon member ("directFrameOne=True" in the log). The old
conditions (18-locale canon 1:1, BootLogoMounted, 800x600, SimitoneChromeHidden — now true
because the chrome does not exist, engine bar active) all still apply. uisplash/loadscreen
untouched and green.

## Honest FAIL log

- r118p1 CRASH (Abort trap 6, port-side, both bugs mine): (a) creating the bar in the ctor
  called `GetBarWidth()` → `GameFacade.Screens.CurrentUIScreen` which is NULL mid-AddScreen
  (RemoveCurrent runs first) → NRE out of the ctor; fixed by positioning from the screen's
  own metrics + a null-safe GetBarWidth. (b) `FAR1Archive(v1b: true)` desynced the manifest
  (negative DataLength −3072) — UIGraphics.far is v1a; the caught fallback meant the run
  showed navy + crashed later from (a).
- r118p2: **66/66 PASS**, full soak, clean exit. Log: DIRECT mount at +0.03s,
  sha=8ecf950019fb86bed4be7d02e1e72ff2217e8ea1d5b44da6c6a0deec2d934ae6, uiboot PASS with
  directFrameOne=True; one transient splash-mount NRE at +0.038s (font not yet resolvable —
  the pre-existing retry window), mounted at +0.168s.

## Residuals (disclosed)

- The splash-tip FONT still resolves via TS1Global (~0.15s after art) — original art shows
  immediately, original text a moment later. A direct FAR read of variablesans_10.ffn could
  close this too (future round if wanted).
- MSDF (modern vector font) remains the default text style engine-wide — dialogs carry R112
  .ffn twins, but any un-twinned surface still risks the blocks-on-GL rendering. The loader
  itself no longer draws any MSDF text at all.
- macOS app-menu name stays "Simitone" (Info.plist CFBundleName / dist bundle name — the
  run-autotest tooling targets the .app path; window TITLE is now "The Sims").
- TS1GameScreen still constructs a default `UISimitoneBg` (modern gradient) as the
  neighborhood screen's base layer beneath the original neighborhood mounts — post-loader
  scope, documented for a future round.
