# R189 — Hermes recovery and Build/Gift parity closure

Date: 2026-08-31

## Outcome

This round repaired the recurring unattended-Hermes failure and used fresh
PowerPC cross-checks to close three visible desktop UI mismatches:

- the Roof pattern chooser now uses the shipped two-row flow;
- the Roof next-page arrow now uses the shipped 800/non-800 coordinates;
- Build tool cells and Gift pager edges are protected by exact native-size
  regression assertions.

## Hermes repair

The dual-Spark DeepSeek service and its authenticated Mac tunnel were healthy.
The actual provider failure was local: two provider-plugin symlinks still
targeted the removed `~/Developer/devkit` tree, so Hermes could not load the
`deepseek-local` profile. They were retargeted to the live
`~/Developer/Tools/dev-tools` tree.

An intermediate diagnosis incorrectly treated that broken provider load as a
dead inference endpoint and changed the default to Copilot. That change was
reverted. The persistent configuration is again `deepseek-local/deepseek` at
`http://localhost:8799/v1`. Both an explicit provider probe and a plain
unattended one-shot completed against DeepSeek through the existing
`com.nathannoom.spark2-deepseek` SSH tunnel.

Hermes' terminal tool did not consistently honor the process cwd, even when
launched with `--in`. `~/.local/bin/hermes-batch` now supplies `--in` and
prepends the absolute project root to every lane prompt, requiring absolute
paths for tool work. This avoids the repeated false “file not found” audit
failures without changing project code.

## Native geometry evidence

### Roof panel

`build-toolbar-law.md` and the raw `cWinRoofPanel` listings establish:

- `screenWidth == 800`: 8 patterns/page and next arrow x=253;
- every other desktop width: 16 patterns/page and next arrow x=433;
- pattern origin `(70,1)`, pitch 45, with half the page on each row;
- 800: 4 columns x 2 rows; other widths: 8 columns x 2 rows.

The previous port put all visible patterns on one row and placed the wide next
arrow at x=456. `UIOriginalRoofPanel` now centralizes the exact page count,
arrow x, and slot-position laws, reflows on resize, and mirrors the selected
WorldRoofs style into the pattern-button state.

### Build tool cells

The 12 shipped 4-state cells are pinned in engine order:

`39x39, 25x38, 31x40, 32x40, 32x38, 29x35, 35x39, 43x28,
27x41, 29x39, 39x33, 30x35`.

The live gate now asserts position, tooltip, cell width, rendered width and
rendered height for every tool. A whole-sheet-width check alone could not catch
vertical or hit-region drift.

### Gift pager

At `0x286dfc`, `add r6,r4,r8` computes the right edge from the already-computed
left edge. The shipped Gift right arrow is therefore fully inside the host:

- 504px host: x `493..502`;
- 280px host: x `269..278`.

The production `Size.X - 11` placement was already correct. The gate now pins
both exact edge pairs. Font slot 6 has a 13px line height, closing the remaining
title/filter-origin residual.

## Verification

- `dotnet build Client/Simitone/Simitone.Client/Simitone.Client.csproj -c Release --no-restore`
  — succeeded, 0 errors (existing package-compatibility warnings only).
- Focused harness:
  `corpus,uibuild,uiroof,uiinterest,uisurvey` — PASS, 9 passed / 0 failed.
- Broad player-UI harness:
  `corpus,uilive,uibuild,uiroof,uibuy,uiinterest,uisurvey` — PASS,
  11 passed / 0 failed.
- Fresh Roof capture shows the expected 8x2 wide pattern grid and corrected
  right pager; Build icons retain their authored natural dimensions; the empty
  Gift panel keeps its title and filter stack separated.

The umbrella solution still contains unrelated Windows-target projects that do
not build on macOS (`NETSDK1100`/`NETSDK1150`); the maintained modified client
target is the authoritative build result above.
