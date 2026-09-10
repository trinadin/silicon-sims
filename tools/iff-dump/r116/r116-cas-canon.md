# R116 — CAS screens in original strings + glyphs

**Slice**: the Create-A-Sim / Create-A-Family screens (Agent-B Hermes survey):
`UISimCASPanel` (10 hardcoded surfaces), `UIFamiliesCASPanel` (heading),
`UIFamilyCASPanel` (last-name label), and TS1CASScreen's delete-family
confirmation dialog.

## Canon (raw — UIText.iff, 76-byte-header walk)

- **STR# `PickFamilyStrs`, chunkID 128**: chunk size 42,455, format −3,
  400 entries, sha256 `dce5d06dda063180895de85efc2325021c774a6fe0285ee7a3204c49cdd781f0`.
  `[6] 'Delete Family?'`, `[7] 'Are you sure you want to delete the "%s" family?'`,
  `[8] 'SELECT A FAMILY'` (the original heading is uppercase).
- **STR# `DesignFamilyStrs`, chunkID 129**: chunk size 27,857, format −3,
  270 entries, sha256 `b0cb46b704d065935fbd101543b76e1ede383c716c759496aabd60cf5177a9d6`.
  `[12] 'Enter Last Name:'`.
- **STR# `DesignCharStrs`, chunkID 130**: chunk size 52,289, format −3,
  504 entries, sha256 `66b9ee45eb561390262c4256c6a2a65802cad834653567e663c014225bc84b93`.
  `[17]-[21]` Neat/Outgoing/Active/Playful/Nice, `[24] 'BIO'`,
  `[26] 'Enter First Name:'`.

## Port

1. **Values** (all via `GameFacade.Strings` numeric tables — original data):
   - First Name label → 130[26] ('Enter First Name:' — the port had shortened it).
   - Five trait labels → 130[17]-[21] (canon carries NO colons; the port's
     "Neat:" style drops them).
   - Bio label → 130[24] 'BIO'.
   - Families heading → 128[8] 'SELECT A FAMILY' (uppercase in canon).
   - Last Name label → 129[12] 'Enter Last Name:'.
   - Delete-family dialog → title 128[6], message 128[7] with the built-in %s
     substitution ("This cannot be undone." was a port addition — dropped).
   - Kept port-authored (NO original exists — verified zero hits; disclosed in
     code): "Gender", "Age", "Skin Color" (130 has the VALUES Female/Male,
     Adult/Child, Light/Medium/Dark, but no group labels).
2. **Glyphs**: `UIOriginalText` twins in the caption table **_10**, mounted
   on first Update (font retry; captions are static after construction):
   first-name + bio + five right-aligned trait labels (measure-based X) in
   UISimCASPanel (new Update override — the panel had none); centered
   families heading; centered last-name label in the family panel (its
   nested list panel already had a Draw override the mount now lives beside).
   Modern labels stay as pre-IFF fallback, hidden once twins mount.
- Gate-readable: `CASTwinsMounted`/`FirstNameTwin`/`TraitTwins`/`BioTwin`,
  `FamiliesTitleTwinned`/`TitleTwin`, `LastNameTwinned`/`LastNameTwin`.

## Gate

- New check **`uicas`** (64 → 65): disk-pins chunks 128/129/130 (labels/
  counts/sha256 + the verbatim entries used), constructs all three panels
  through their own ctors + one Update tick each (the panels build
  unconditionally at screen setup; texture loads need only the graphics
  device, available headless), requires every twin == disk canon, and pins
  the delete-dialog wiring incl. the %s substitution through the runtime
  string tables.

## Honest build notes

The first insertion pass mangled class structure three times (twin blocks
landing in nested classes / after class closes — UISimCASPanel's nested
`UICASPersonalityBar` shares the Update signature, and the family panel's
nested `UIAvatarListPanel` owns the nearby Draw). All repaired before the
gate ran; compile errors never reached a gate run.
