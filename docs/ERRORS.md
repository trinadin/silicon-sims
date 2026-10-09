# ERRORS.md — hazards we've hit, kept short (so we don't hit them again)

Keep this concise. One line per hazard: what went wrong → how to avoid it. Add an entry only
when you actually observed it. Do not turn this into a novel; delete entries that stop mattering.

## Runtime / harness

- **Relative `-path` mis-resolves.** `-path"game-data/The Sims"` is resolved against the app
  bundle's `Contents/MacOS`, not the cwd → `DirectoryNotFoundException` on
  `.../Contents/MacOS/game-data/The Sims/GameData/UIText.iff` → SIGABRT during `Initialize`.
  Always pass absolute paths to `-path`.
- **Headless SDL/Cocoa event-wait stall.** After aborted runs the game can sit ~10–16 min in
  `SDL_WaitEventTimeout` before the first tick; the default 5-min `-autotest-timeout` then trips
  the TIMEOUT guard → `TIMEOUT state=1 -> FAIL` even though the engine is fine. Run the suite
  with `-autotest-timeout 1800000`, and don't kill a run just because it's been quiet for minutes.
- **game.log is not time-ordered.** It is appended from several buffered writers, so line order
  does not equal run order. Never reconstruct which check ran when from game.log alone; trust a
  single run's own stdout capture or the runner's SUMMARY.
- **GameLog stdout mirror is unreliable when redirected.** GameLog mirrors to Console + game.log;
  redirected stdout can stop after `exit-probe: Game.Run(Sync) enter`. Read
  `~/Documents/Simitone/game.log` (and the run's own stdout file) for the truth, and never rely
  on redirected stdout alone.
- **Don't kill a run before it ticks.** The stall is environmental, not a hang: `sample <pid>`
  shows the main thread parked in `SDL_WaitEventTimeout_REAL` while the process is healthy.

- **NU1605 warnings-as-errors on publish (SERVER projects).** `dotnet publish` of the Desktop
  project fails restore with "error NU1605: Warning As Error: Detected package downgrade" in
  FSO.Server.Common/Database/etc. when restore re-runs (cached restores hide it). Do NOT touch
  those csprojs; pass `-p:NoWarn=NU1605` to the publish command (suppresses the downgrade warning
  so restore succeeds). Warning stays a warning; IFF pins unaffected.
- **Relative `NUGET_PACKAGES` rejected by .NET 10 SDK.** Under `dotnet` 10.x, restore fails across
  every project with "'NUGET_PACKAGES' must contain an absolute path './.nuget-packages'" (the
  relative form worked on 9.x). Use absolute paths: `NUGET_PACKAGES="$PWD/.nuget-packages"\
  DOTNET_CLI_HOME="$PWD/.dotnet-cli"` (see PORT_STATUS.md).

- **Literal `\u` in Python/JS strings breaks.** `Content\uigraphics\live\foo.png` has `\u` + non-hex;
  Python source and JS template literals reject it. Escape it, or handle backslash paths by
  splicing the actual bytes from the file rather than retyping them.
- **`describe.py` (vision-helper) flaked (empty output / timeouts).** Working route: downscale
  with `sips -Z 480` (or convert BMP→PNG first — the endpoint rejects BMPs), then call the local
  model natively at `http://127.0.0.1:11436/api/chat` with `stream:false, think:false` and a
  ~280 s timeout. Model reads approximate colors only — treat byte-level pixel extraction as the
  authority.
- **`sample` needs the right PID.** `pgrep -f 'MacOS/TheSims'` can match the wrapping bash; pick
  the pid whose command is the `.../TheSims -path...` binary before sampling.
- **Tool stdout can come back empty on some invocations** — if a bash result has no stdout but
  exit 0, redirect to a file and read the file, don't retry "harder".
- **Headless render colors WOBBLE run-to-run.** Same binary, same source, separate runs render
  the SAME IFF texture at different brightnesses (IFF steel glimmer R observed 0x7E r68 / 0xA7
  r69#1 / 0x5F r69#2; navy varied ~12%). Never IFF-pin absolute rendered hex — pin the
  IFF *family* (e.g. B>=G>=R with IFF chroma deltas) or the widget tree. Tree-level pins are
  deterministic; pixel-family counts are evidence only.

## Docs / guardrails

- Evidence under `tools/iff-dump/` is tracked (not gitignored) — commit normally. Never weaken an IFF
  pin; extend it and keep the suite green (156 checks at time of writing).