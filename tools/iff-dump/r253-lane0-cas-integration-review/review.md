# Lane 0 — CAS-preview/focus integration review (read-only)

2026-09-08. Lane 0 "reviewed integrations" of the deferred CAS-preview/focus
work (Lane C candidate) from `simitone-cas-support` `cas-r252-integration`.
This is a static blast-radius/dependency assessment only — **no production
code was changed**, and no UI/behavior change was made (UI changes require
scoped user approval per the work plan §Isolation rules).

## Candidate commits (support branch, base `42519f7`)

- `0529f98` "Render original CAS preview in an isolated fixed-size surface"
- `6102a8b` "Restore CAS dialog entry focus and hide the duplicate staging Sim"

Both are already merged on `cas-r252-integration` and are part of the
111/0 support-branch CAS baseline (main is 94/0).

## Files touched (source only)

| File | 0529f98 | 6102a8b | Note |
| --- | --- | --- | --- |
| `…/UI/Panels/CAS/UIOriginalVitaPreview.cs` | +129 | – | **new** render-target based CAS avatar preview (100x220) |
| `…/UI/Panels/CAS/UIOriginalCAS.cs` | ~8 | – | exposes `VitaSurface`, mounts it over `VITA_RECT` |
| `…/UI/Screens/TS1CASScreen.cs` | ~12 | ~41 | binds `VitaSurface`, hides the staging sim on larger windows; desktop name-focus management (`UpdateDesktopNameFocus`/`ClearDesktopNameFocus`), confirmation/mode/cleanup focus clearing |
| `…/AutotestCASFlow.cs` | ~63 | ~22 | `CheckPreviewSurface` + child/adult preview assertions; focus-aware `TypeInitiallyFocused`, entry-visibility gates, staging-avatar-hidden assertion |
| `…/AutotestCAS240.cs` | – | +202 | **new** synthetic no-VM CAS harness (`uicasorig` workflow240) |
| `…/AutotestRunner.cs` | – | ~1 | `uicasorig` check calls `AutotestCAS240.Check` |

## Dependency set / seam exposure

- `UIOriginalVitaPreview.cs` is self-contained (renders a posed avatar into an
  isolated `RenderTarget2D`). Depends on `VMAvatar`/`Avatar.DrawGeometry`/
  `WorldCamera` (existing).
- `AutotestCAS240.cs` exercises the `TS1CASScreen` focus management added by
  `6102a8b`; it is a no-VM unit harness (no renderer, no writer, no screen
  transition, no live focus manager).
- **Shared-file overlap:** both commits touch `TS1CASScreen.cs` and
  `AutotestCASFlow.cs`, which on main currently carry only the R253 audit-fix.
  The audit-fix modified `SetFamilies` (~L1040) and the recordfmt verdict block
  (`AutotestCASFlow`); the CAS commits modify `Update`/`SetMode`/
  `ShowConfirmation`/`CleanupLastWorld` and the autotest phase methods. These
  regions appear **non-overlapping**, but a real integration must re-verify both
  recordfmt verdict and CAS signature after the merge (`AutotestRunner` is a
  Lane 0 shared file — the `uicasorig` cas240 call is a shared-seam addition).
- `AutotestCAS240` increases the CAS check surface (support 111/0 vs main 94/0);
  default-suite check string is unchanged (both are `uicasorig`/opt-in style).

## Integration recommendation

**Do not land automatically.** This is a user-visible CAS behavior change
(render-target preview replaces the in-lot preview composition; dialog-entry
focus rules change) and requires the work plan's scoped user approval plus
visual validation across 800x600/1024x768. It should be integrated by Lane C
(as the CAS owner) with a minimal selective patch over main's current
audit-fix version, then lane-0-verified (combined `ucasflow+recordfmt` and the
default suite). The R253 serialization/audit-fix (`d7accdf`/`46495bb3`) must
not be disturbed; `AutotestRunner.cs` seam registration goes through lane 0.
