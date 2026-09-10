# R202 — Sim visibility RESOLVED: PPX non-MRT depth-target leak (R201 diagnosis corrected)

**Round:** R202 · **Outcome:** sims rasterize in the live composition; `simvis`
promoted into the default gate (115 → **116 checks**); 1-file FreeSO engine fix.

## The R202 correction, stated plainly

R201 concluded "the Vitaboy avatar effect draws zero fragments on GL — the
effect is the failure point." **That diagnosis was wrong.** Two independent
probes exonerated the effect completely, and the true defect was a one-line
render-target leak in the PPX depth pipeline that only manifests when MRT is
unavailable (always on this GL desktop path):

```
TSOClient/tso.common/Utils/PPXDepthEngine.cs, RenderPPXDepth, non-MRT branch
    gd.SetRenderTarget(color); proc(false);
    gd.SetRenderTarget(depth); proc(true);      // ← ends with DEPTH bound
    // (no restore — unlike the SoftwareDepth branch, which restores color)
```

`World.InternalDraw` order is: sprite passes (Architecture/Static — each goes
through `RenderPPXDepth`) **then** `WorldEntities.DrawAvatars` (direct 3D mesh
draws into whatever target is current). On the non-MRT path the sprite segment
leaves the render target bound to the **depth** target, so the sims' meshes
were drawn *into the depth buffer* and never reached the color target. Every
sprite-based thing (terrain, objects, floors, UI) re-bound its own targets and
rendered fine — which is exactly why only avatars vanished.

**Fix (6 lines incl. comment):** restore the color target after the depth
pass, mirroring the SoftwareDepth branch. After the fix the framed live diff
goes 0 → **23175 px** and the sims are visible in play.

## The exoneration chain (skeptic-grade, each step measured)

| Step | Probe | Result |
|---|---|---|
| 1 | Extract the exact vsVitaboy GLSL from the shipped `Content/OGL/Effects/Vitaboy.xnb` (MGFX v10; MojoShader output `uniform vec4 vs_uniforms_vec4[212]` + `vs_a0` dynamic indexing) | full GLSL recovered (vsVitaboy-extracted.glsl) |
| 2 | Compile it on this Mac's GL 2.1 (game context profile) — `glsl_probe.c` | **COMPILE SUCCESS, LINK SUCCESS**, location(vs_uniforms_vec4)=1 |
| 3 | **Execute** it in pure GL (FBO, hand-set uniforms) at bone indices 0/1/49 — `glsl_render_probe.c` | **65536/65536 px at all three indices** — dynamic uniform array indexing works; uniform readback correct |
| 4 | Load the xnb through the exact MonoGame 3.8.4 DesktopGL runtime standalone (`tools/r202_effect_probe`) | effect loads; cbuffers resolve (`vs_uniforms_vec4` 3392B = 212 vec4); attribute table sane; 6/7 techniques render a synthetic triangle (CullNone or CW) |
| 5 | In-engine axis bisection (`simvis` diagnostic, targeted soaks run1–run10) | A: identity W/V/P + real mesh = **79185 px** · B: engine VP = 107 px (a *correct far-zoom render at the origin*) · C: real 29-bone SkelBones = **64491 px** · E/F: real World matrix = 0 px (sims **below the viewport**) |
| 6 | Framing check | avatars projected to screen y 1512–1608 on a 768-px viewport — **off-camera** in the test view; this is what produced R201's "0 diff" |
| 7 | Manual `WorldEntities.DrawAvatars` into a clean framed RT (upstream engine code, `CullCounterClockwise` intact) | **21755 px — the engine mesh pass renders all three sims** |
| 8 | Apply the PPX restore; framed live diff | **23177 px, simvis PASS** |

## What each earlier suspicion turned out to be

- *"Vitaboy.fx dead on GL / SkelBindings[50] + dynamic indexing"* — refuted
  (steps 2–4). The iOS variant being just `#define SIMPLE 1` was the first
  hint the shader family was never GL-incompatible.
- *"degenerate bone matrices"* — refuted (skelbones census: 29 bones, 0 NaN;
  axis C renders with them).
- *"culling inverts on GL (posFixup Y-flip)"* — tested by flipping
  `DrawAvatars` to CullNone: no change (run2). **Reverted** — upstream state
  restored; step 7 renders under the original `CullCounterClockwise`.
- *"NoSSAA technique broken standalone"* — probe artifact (unset samplers);
  in-engine NoSSAA renders 79131 px (axis A2).
- R201's `BasicEffect control coloredPx=113` — an invalid control: it drew
  with `World=Identity`, putting the mesh at the world origin (off-view
  except strays). Not evidence of anything.

## The permanent check (`simvis`, now default)

- census of every visibility input (positions, rooms, blueprint, level gates,
  hidden flags, bindings/geometry);
- bone + mesh data census (NaN/OOB-bone-index scans, capacity 50);
- **framing**: pans `State.CenterTile` onto the first sim, logs each sim's
  projected screen position (must be INSIDE), restores camera after;
- manual `DrawAvatars` mesh-pass aliveness count (21755 px);
- framed with/without pixel diff through the live PPX composition
  (threshold >100 px; observed 23175);
- `PASS` at >100. Fails loudly otherwise.

## Verification

- Targeted soak `CHECKNAME,simvis,corpus`: PASS (targeted-soak-run11.log).
- Full default gate: **passed=116 failed=0**, carseek PASS, exit probes clean
  (`after Run`, `after Dispose`), AUTOTEST_WRAPPER_EXIT=0 (gate-run.log).
- Dist DLLs byte-match publish: Simitone.Client 984caffa · FSO.SimAntics
  6b8d6403 · FSO.Client 2b83283d · FSO.Common 2f15c4fb.
- FreeSO submodule diff: **only** `PPXDepthEngine.cs` (+6 lines);
  `WorldEntities.cs` speculative cull change reverted to upstream.

## Files

- `glsl_probe.c`, `glsl_render_probe`, `vsVitaboy-extracted.glsl` — GL 2.1
  compile + execution proofs.
- `tools/r202_effect_probe/` — standalone MonoGame 3.8.4 harness (loads the
  shipped xnb, dumps cbuffers/attributes, synthetic draws incl. cull matrix).
- `targeted-soak-run{1..11}.log` — the investigation sequence; run11 = fix.
- `gate-run.log` — the full 116/0 default gate with simvis promoted.

## Residuals (disclosed)

- `SkelBones` is null until first draw (lazy `ReloadSkeleton`) — the census
  reads pre-draw and reports count=0; harmless, kept visible in the log.
- The framed diff counts *any* pixel change at the framed camera; a fully
  occluded sim could theoretically hide behind walls at that exact framing —
  the manual-DrawAvatars count (step 7) covers the mesh pass independently.
- MonoGame's `posFixup` Y-flip on backbuffer targets remains a real
  winding-mirror on GL vs DX (proved in the standalone cull matrix); nothing
  in the current engine depends on it now, but it is the first place to look
  if a future pass adds culling.

No proprietary payload in any probe or log.
