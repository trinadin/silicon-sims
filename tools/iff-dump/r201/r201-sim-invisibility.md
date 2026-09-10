# R201 — why SIMs are invisible (user question, diagnosis round)

User: *"Is there a reason SIMs are invisible in our tests?"*

## Short answer

Yes — and it is not test-only. The world's 3D avatar mesh pass draws **zero
fragments** on this OpenGL backend: every sim's data is fully loaded and
registered, but the custom Vitaboy skinning effect (`Vitaboy.fx`,
`AvatarEffect`) produces no pixels through ANY of its seven techniques. The
test captures use the exact live-frame composition path
(`World.PreDraw → InternalDraw → PPX backbuffer`), so the live game window is
affected identically — sims are invisible in live play too, and (almost
certainly) always have been on this port. The UI-focused gate never caught it
because its probes render panels and portraits (the icon-cache 2D path), not
world avatar meshes.

## The evidence chain (all from the new opt-in `simvis` check)

House05 / Goth, corpus soak, `CHECKNAME,simvis,corpus`:

1. **Engine state clean** — global32=0, speed=1, mailbox present,
   wallsMode=1 (dynamic cutaway), drawRoofs=False, worldLevel=1. All three
   sims (Cassandra/Bella/Mortimer) ON-LOT at tiles ~(26-28, 22-25), level 1,
   room=outside, not OUT_OF_WORLD, not hidden (ObjectData[Hidden]=0), not
   dead, `WorldUI` = AvatarComponent, `Visible=true`, `Avatar.Skeleton` ok.
2. **Blueprint registration clean** — `Blueprint.Avatars.Count=3`, each
   `Level=1 <= state.Level=1` (the `DrawAvatars` cull passes), positions
   correct.
3. **Geometry clean** — mesh bindings populated with real TS1 geometry and
   textures: bodies v404/v367/v428, hands v32x2, feet v77/v62/v77, all
   `/tex` (textures resolved), primitives present.
4. **The world composition rasterizes NOTHING for them** — with/without
   avatar `Visible` pixel-diff of the live composition = **0/2,359,296**
   (1024x768 RGB, native viewport).
5. **A forced direct avatar pass** (engine's own technique selection +
   View/Projection + `AvatarComponent.Draw`, per technique) = **0 colored
   pixels for all seven techniques** (NoSSAA, ObjIDMode, AdvancedLighting,
   SSAA, ShadowTech, AdvancedLightingDirection, HeadObject).
6. **The control: a stock `BasicEffect`** drawing the same body mesh's
   vertex/index buffers = **113 colored pixels** — the vertex path, buffers,
   declarations, and draw-call machinery all WORK. Only the Vitaboy effect
   path yields nothing.

## Root cause (isolated, fix pending)

The failure point is the Vitaboy effect itself on the GL backend —
`tso.content/ContentSrc/Effects/Vitaboy.fx`, selected in
`WorldContent.LoadEffects` as `"Effects/Vitaboy" + EffectSuffix` where
`EffectSuffix` is "" on desktop GL ("iOS" only for GLES2). Prime suspect:
the vertex-shader skinning — `float4x4 SkelBindings[50]` (200 vec4
uniforms) with **dynamic array indexing**
(`SkelBindings[int(v.params.x)]`) — silently mis-binding/collapsing on this
MonoGame-GL build (every skinned vertex degenerates → zero fragments, no
exception). The mesh/vertex/GPU-buffer layer is proven good by the
BasicEffect control.

Fix directions for the dedicated round (FreeSO submodule work — the
published tree-alias would move): reduce the bone array (TS1 skeletons need
far fewer than 50; TSO was the 50-bone case), port/enable the GLES2
`VitaboyiOS.fx` variant on desktop GL, or replace the dynamic uniform
indexing. The `simvis` check is the acceptance test: it turns PASS the
moment sims rasterize, then joins the default suite.

## Gate

`simvis` added as a PERMANENT OPT-IN check (like carreturn/schoolreturn —
intentionally NOT in the default suite while the gap is open, so the honest
FAIL documents it without blocking the 115). It logs the full census +
diff + technique sweep + BasicEffect control and FAILS while sims don't
rasterize. The default gate is unchanged: **115 passed, 0 failed**, carseek
PASS, clean Run/Dispose exit-probe chain, WRAPPER_EXIT=0; dist DLLs
byte-match publish (Simitone.Client 2a74de53, FSO.SimAntics 6b8d6403,
FSO.Client 2b83283d). No pins weakened; one new opt-in check added.

## Residuals

- The precise GL failure mechanism (uniform array limit vs dynamic-index
  codegen) is not yet proven at the shader level — the fix round's first
  task; the effect-vs-vertex-path isolation IS proven.
- Whether pets/NPC meshes share the failure (same effect — almost
  certainly yes).
- Historical note: R35/R36's censor captures went through the icon/thumb
  path, not the world pass — they never proved world avatar rendering.
- No proprietary payload in this round's evidence.
