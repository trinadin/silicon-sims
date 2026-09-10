# R181 — UI visibility and visual-fixture state contract

## The renderer boundary

`UIElement.Visible` is a public field; `UIElement` does not enforce it. The
framework's `UIContainer.Draw` returns when its own container is hidden, but a
derived `Draw` override has already started executing by the time it calls
`base.Draw`. Any texture, glyph, fallback background, or explicit child draw
performed before or after that base call can therefore paint through
`Visible=false`.

The R181 rule is consequently strict:

```csharp
public override void Draw(UISpriteBatch batch)
{
    if (!Visible) return;
    // custom painting and/or base.Draw(batch)
}
```

The visibility test must be the first executable statement. Combining it with
other early-out conditions is valid, but relying on `base.Draw`, guarding only
one paint branch, or testing `Visible` after custom work is not. This applies
equally to leaf controls and containers, including overrides that call a child
control's `Draw` explicitly.

The correction changes only paint suppression. It does not alter layout,
frames, padding, hit areas, transitions, input gestures, or the normal visible
composition.

## Static coverage

A source-wide scan of
`Client/Simitone/Simitone.Client/**/*.cs` selected every
`public override void Draw(UISpriteBatch ...)`, skipped leading whitespace and
comments, and required the first executable statement to begin with
`if (!Visible...)`. The final result is **76/76 guarded, 0 unguarded**. The
similarly named `OriginalVectorFont.Draw(GraphicsDevice, ...)` is not a
`UIElement` override and is outside this contract.

The working correction adds 43 missing early returns across the production
control, live-panel, CAS, lot-control, dialog, neighborhood, and screen
families. The last syntactic exception, `UIMobileDialog.Draw`, already avoided
hidden custom chrome and relied on `UIContainer.Draw` for its children, but was
still normalized to the same immediate-return contract. That uniform form is
intentional: future painting inserted into any override must remain behind the
same first-line guard.

This static sweep closes breadth; it does not prove that the guards suppress
real GPU output. The `uivis` runtime gate supplies that independent proof.

## Runtime `uivis` gate

`AutotestRunner.CheckUIVisibilityContract` renders production elements in
isolation into a transparent full-viewport render target. For each probe it
recalculates the complete parent matrix chain, draws once with `Visible=true`
and once with `Visible=false`, reads the target back, and counts nonzero-alpha
pixels. A probe passes only when its visible draw emits at least one pixel and
its hidden draw emits exactly zero. The positive half prevents an empty or
off-screen control from passing vacuously.

The expanded packaged integration run reported:

| production probe | visible alpha pixels | hidden alpha pixels |
|---|---:|---:|
| `UIBigButton` | 11,932 | 0 |
| `UIVertGrad` | 800 | 0 |
| `UIValueBar` | 1,144 | 0 |
| `UIOriginalLiveGauge` | 4,861 | 0 |
| `UIInteraction` | 2,001 | 0 |
| `UIInventoryDisplay` | 2,408 | 0 |
| `UIRelationshipDisplay` | 646 | 0 |
| `UICatalogItem` | 2,203 | 0 |
| `UIFamiliesCASPanel` | 82,642 | 0 |
| live `UIMainPanel` | 105,802 | 0 |

These probes cover original-glyph/button painting, gradients and value bars,
mood display, interaction-queue art, inventory and relationship cells,
catalog cells, CAS composition, and the live bottom panel. They specifically
exercise the renderer families behind the observed stale mood/job pixels,
queue/catalog art, inventory cells, relationship portraits, CAS chrome, and
dialog captions.

### `UIMainPanel`

`UIMainPanel.Draw` can paint the search-field background and the authored
fallback panel/seam before `base.Draw`. Hiding the panel therefore was not
enough when its override lacked an early return. The runtime probe uses the
live production instance because constructing another panel reparents the
lot's shared Query/Pickup panels. It snapshots `Visible`, `CurWidth`, and
`OriginalPanelBack`, temporarily selects the fallback-paint branch by clearing
the public backdrop reference and ensuring a paintable width, tests visible
and hidden output, and restores all three fields in `finally`.

### `UIFamiliesCASPanel`

`UIFamiliesCASPanel.Draw` updates transition geometry, calls `base.Draw`, then
paints the full-width background and explicitly draws the title. That
post-container work could paint even after the base container rejected a
hidden panel. Its immediate guard now suppresses the whole override. The gate
sets a newly constructed production panel to the normal `TitleI=1` endpoint so
the positive draw is meaningful, then requires the hidden draw to be empty.

## Survey and dialog-fixture state isolation

Visual survey artifacts must show the requested surface, not an ObjectDialog
that happened to be open in the loaded save. They also must not answer that
dialog or disturb the simulation merely to obtain a clean screenshot.

`SuppressPreexistingDialogsForVisualArtifacts` is called before `uisurvey` and
again before `uidlgchrome`. Its contract is:

1. Enumerate the current screen's attached `UIMobileDialog` children.
2. On first sight only, store each dialog and its exact prior `Visible` value
   in `_visualArtifactDialogVisibility`.
3. Set only previously visible dialogs to `Visible=false`. Already-hidden
   dialogs are tracked as `false` and remain hidden.
4. Do not close, remove, or answer a dialog. In particular, do not call the VM
   dialog-response path: that would alter `UILotControl`'s blocking-dialog
   ownership, `VM.GlobalBlockingDialog`, its owner thread, and simulation
   speed.
5. Snapshot the `GlobalBlockingDialog` object reference and
   `SpeedMultiplier` immediately around suppression and log
   `vm-state-preserved=True` only when both are unchanged.

The dictionary deliberately survives the transition from `uisurvey` to
`uidlgchrome`. A second suppression pass cannot mistake the harness-hidden
value for the original value, and synthetic probe dialogs are created only
after the preexisting-dialog snapshot.

`Finish` calls `RestoreVisualArtifactDialogs` before the final summary. Every
still-attached tracked dialog receives its exact saved boolean, the restore
count is logged, and the dictionary is cleared. A dialog legitimately detached
by a production VM update is not resurrected. Thus dialog visibility is
restored exactly where the original object still exists, while VM latch,
thread, and speed state are preserved rather than mutated and reconstructed.

## Packaged and local verification evidence

The files below are local verification artifacts, not source inputs or
redistributable game data. They are intentionally not copied into the
repository.

The evidence boundary is deliberately split. The sealed R181 broad run proves
`uivis` and `uidlgchrome` together but predates dialog suppression; the final
R181 dialog-fixture run proves suppression/restoration but does not enable
`uivis`. No single post-fixture R181 log is represented as covering both. The
later R183 continuity runs are the combined regression proof for the expanded
gate and final package.

- `/private/tmp/r181-broad-sealed.log`: 814 lines, SHA-256
  `3dfc237fd4a55631854093ee3ce3a74d01e1532514baeb10289efd456019d0c9`.
  The packaged broad gate passed **24/24**, including the original eight-probe
  `uivis` gate, `uisurvey`, and `uidlgchrome`, and reached both `after Run` and
  `after Dispose`.
- `/private/tmp/r181-dialog-build.log`: 169 lines, SHA-256
  `1f0cf8d60f2e6920f647fffc0ae0df608a9ce16cf9fa4096ebcc0d9731587f02`;
  the release build ended with 75 warnings and **0 errors**.
- `/private/tmp/r181-dialog-publish.log`: 515 lines, SHA-256
  `3e45540dc74e5bb9f2ce1186c35d9fbfd9c914827e4d4037efb3aaeb4604becf`;
  it published the `osx-arm64` application.
- `/private/tmp/r181-dialog-pack.log`: 4 lines, SHA-256
  `abd79af669503b21fb52fd50403fae6b46afaf48273152e6888c58626bc15ac8`;
  it records `Built dist/The Sims-arm64.app`.
- `/private/tmp/r181-dialog-fixture-focused-final.log`: 737 lines, SHA-256
  `43db5821528ac246c6adfcf761855292dcb9246d590f086dd3a1b022dec35240`.
  The post-pack focused run passed **8/8** and recorded
  `vm-state-preserved=True`, `uidlgchrome: PASS`,
  `restored-dialogs=1/1`, and clean `Run`/`Dispose` exit markers.
- `/private/tmp/r183-focus-rerun.log`: 807 lines of later integration evidence
  carrying the expanded `UIFamiliesCASPanel` and `UIMainPanel` probes; SHA-256
  `47fd4bda95190241c60d4d3693d365f9844a2ee59e424e5dc6171bf74c115799`.
  The immediately preceding `/private/tmp/r183-pack-rerun.log` records the
  rebuilt app package (4 lines, SHA-256
  `abd79af669503b21fb52fd50403fae6b46afaf48273152e6888c58626bc15ac8`).
  The run passed **13/13** with all ten visible/hidden pixel pairs above,
  logged `vm-state-preserved=True` for both fixture phases,
  `restored-dialogs=1/1`, and clean `Run`/`Dispose` exit markers.
- `/private/tmp/r183-broad-final.log`: 1,883 lines, SHA-256
  `a97338bdb0c37de3e5afd95330e7f1a3100421315979a1e6ccbba79d3cf7b3b8`.
  This latest post-package continuity run followed
  `/private/tmp/r183-pack-final2.log` (4 lines, SHA-256
  `abd79af669503b21fb52fd50403fae6b46afaf48273152e6888c58626bc15ac8`)
  and passed the broad gate **108/108**. It repeated all ten `uivis` pixel
  pairs, suppressed one preexisting dialog during `uisurvey`, preserved VM
  state in both fixture phases, restored `1/1`, and reached clean
  `Run`/`Dispose` exit markers.

The passing logs retain the known self-recovering startup
`splashprogress mount` `NullReferenceException`; none of the key passing logs
contains `SimAnticsExc` or `bad-routine-frame`.

Together, the 76/76 source scan, non-vacuous pixel probes, live panel probes,
and state-preserving visual fixture close the R181 parity defect without
changing the established visible UI geometry.
