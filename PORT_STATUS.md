# Silicon Sims — Native macOS (Apple Silicon) Port

**Status: playable on Apple Silicon; base-game parity work continues.**
Current verified build, counts, integration decisions and remaining work are
maintained in [HANDOFF.md](HANDOFF.md). Consult that summary before using the
build commands below; historical round results are not current-state claims.

This repository (**Silicon Sims**, branch `main`) is a native Apple
Silicon port of **Simitone** (alexjyong fork, v0.8.20-forked), the open-source re-implementation
of The Sims 1 (engine only; it loads the original game's data files). The `FreeSO` engine lives
in-repo at `FreeSO/` (full history, merged as a subtree — see PROVENANCE.md). Not affiliated with
the Simitone team.

## What the port changes

1. **Eto.Forms 2.9.0 alignment** — `Simitone.Desktop.csproj`, `Simitone.Shared.csproj`,
   `Simitone.Windows.csproj` pinned to 2.9.0 (the 2.8.4 pins conflicted with the 2.9.0 NuGet
   actually resolved and dropped Eto.dll from the macOS output, crashing startup).
2. **RuntimeIdentifiers** — `osx-x64;osx-arm64` on the Desktop project (required by
   Eto.Mac64's BundleDotNetCore.targets).
3. **NuGet cache path fix** — `CopyMonoGameDLLs` now respects `NUGET_PACKAGES`.
4. **User-data fallback** — `Program.cs`: if `~/Documents/Simitone` can't be created, fall back
   to the app bundle directory instead of crashing.
5. **Packaging** — `packmac.sh` + `Info.plist` produce a self-contained `dist/The Sims-arm64.app`
   (and DMG).
6. **Headless/remote-session fix** — `SynchronizeWithVerticalRetrace` disabled so the loop never
   parks in a vsync wait; still frame-limited by `IsFixedTimeStep` + `TargetElapsedTime`.
7. **Engine fidelity fixes** — each IFF-grounded, committed in the FreeSO submodule
   (`mac-port-rel`). See PARITY.md → "Verified — engine fixes" for the list and evidence.

## Verified on Apple Silicon (macOS 26.5, arm64)

- [x] Compiles and publishes self-contained osx-arm64 (`dotnet publish` / `packmac.sh`).
- [x] Boots to gameplay; imports original user neighbourhoods/houses; live GUI window.
- [x] Rendering pixel-confirmed (textured scene, not black).
- [x] Headless `-autotest` suite passes with a clean exit (see AUTOTEST.md).

## Known latent issues

- The macOS **build** rail does not emit Eto.dll — only the **publish** rail does. Use
  `dotnet publish` / `packmac.sh` outputs, not raw `bin/Debug` framework-dependent runs.
- NU1605/NU1701 warnings from the FreeSO AOT/server projects are suppressed via
  `/p:TreatWarningsAsErrors=false /p:WarningsAsErrors=""` (matches the fork's own CI).

## Required: game data

Simitone needs The Sims 1 **Complete Collection / Legacy Collection** data: a directory
containing `GameData/.../Behavior.iff` and the `UserData` layout. Detection
(`MacOSLocator.cs`): portable `../The Sims/`, the Steam directory, Wine prefixes, the install
dialog, or `-path"<path>"`. Selected path persists in `config.ini` (`TS1HybridPath`).

- **Staged:** `game-data/The Sims/` holds GameData + UserData6/7/8 + ExpansionPack2–7 + ExpansionShared + ExpansionPack (complete
  collection). This is the corpus every IFF-literal pin and the harness verify against.
- **Legal acquisition:** The Sims: Legacy Collection (Steam app 3314060 / EA App / Epic) or the
  owned Complete Collection DVD ISO (archive.org). Data files are platform-neutral IFF.
- **Fetch:** `fetch-game-data.sh` downloads the ISO from archive.org and extracts GameData —
  only needed if you lack a local copy.

## Build / package (run from the repository root)

    ROOT="$PWD"
    NUGET_PACKAGES="$ROOT/.nuget-packages" DOTNET_CLI_HOME="$ROOT/.dotnet-cli" \
      dotnet publish Client/Simitone/Simitone.Desktop/Simitone.Desktop.csproj \
      -c Release -r osx-arm64 --self-contained true -o "$ROOT/publish/osx-arm64" \
      /p:TreatWarningsAsErrors=false /p:WarningsAsErrors="" -p:NoWarn=NU1605
    ./packmac.sh arm64            # -> dist/The Sims-arm64-app

`NUGET_PACKAGES`/`DOTNET_CLI_HOME` must be absolute paths: the .NET 10 SDK refuses
relative `NUGET_PACKAGES` at restore time (verify with `dotnet --version`; if it reports
10.x, use the absolute form above).

Run: `dist/The Sims-arm64.app/Contents/MacOS/TheSims -path"<gamedata>" -autotest 5`.

## iPad / iOS (future)

Engine is shared; only the Eto install-dialog + DesktopGL backend are macOS-specific. `FSO.iOS`
is a legacy Xamarin.iOS / MonoGame v3.0 project that will not build under .NET 9; an iPad target
needs a modernized `net9.0-ios` shell + MonoGame's iOS backend + a path picker instead of the
Eto dialog. Assessed in Round 6. Not started.
