# R239 desktop CAS behavior restoration

Scope: maintained `Simitone.Client` CAS sources. Original game files are read
in place; no game assets, neighborhood data or upstream sources are changed.
The source changes restore the already approved original interface. Native
anchors and background layouts remain fixed.

## Confirmed failures and corrections

1. `CAS.UIOriginalSheetButton` constructed its `UIButton` from a 1x1 white
   texture and selected one image state. The full artwork was drawn separately,
   leaving a 1x1 mouse rectangle. It now constructs from the original sheet and
   selects four columns, so input uses the same per-state width and height as
   the artwork. R143 native CAS Init (`0x2ce928..0x2ce96c`) explicitly installs
   that full rectangle; family Init uses the same image law. This affects the
   age, gender, skin, head/body arrows, family Add/Delete/Edit, and family-list
   Add/Delete/Move In controls. Disabled state takes precedence over selection.
2. `TS1CASScreen` returns from its desktop constructor before creating the
   mobile `FamiliesPanel`, but Delete Family unconditionally read that mobile
   panel. Selection now comes from the active desktop/mobile family list,
   using a shared bounds-checked resolver also used by Move In.
3. Draft member deletion lacked the original confirmation and retained the
   deleted index. Fresh disassembly in `family-command.txt` verifies the
   confirmation at `0x2d07dc..0x2d0810`, and the explicit selection reset to
   minus one at `0x2d0860..0x2d0864`. The UI command now asks before deletion,
   clears selection after Yes, and rejects invalid/stale indices. Internal
   cleanup still removes draft previews directly.
4. Clicking a draft member changed its stored index but never recolored its
   existing caption. Selection refresh now updates those labels and clamps
   against the current member count. Add remains disabled at eight members.
5. Done in Create a Sim bypassed the unspent-personality warning. Native
   `0x2cd414..0x2cd464` checks remaining points and requires an affirmative
   response before keeping the Sim. The production command now uses the
   original STR#130 entries 13/14 and keeps the draft on No.
6. Empty Create a Family unnecessarily asked to discard a family. Native
   `0x2d0b48..0x2d0b8c` bypasses this prompt when member count is zero. Drafts
   with members retain the original STR#129 entries 7/8 confirmation.
7. Confirmation logic formerly treated any open dialog as implicit acceptance
   in recursive command handlers. One confirmation dispatcher now keeps a
   pending latch and a one-answer guard; only its affirmative continuation
   performs the requested action.
8. Skin filtering removed unavailable head entries directly from the shared
   content collection. Returning to another skin could therefore permanently
   lose valid choices for that session. Head/body lists are copied before
   filtering; this changes collection ownership, not the filtering policy.
9. The three name/bio fields requested font 14 despite native font 10 at
   `0x2cf3f0`, `0x2cf56c`, and `0x2d14f8`. `TextStyle.Size = 10` uses the
   existing native bitmap font adapter. Bio now uses `MaxLines = -1`, matching
   native unlimited lines and keeping capacity 2048. The previous four-line
   limit silently joined subsequent paragraphs in `UITextEdit` validation.

## Isolation and validation boundary

`TS1CASScreen` exposes internal callback seams for confirmation presentation,
family census, family deletion/creation persistence, and neighborhood
navigation. Defaults retain the production implementations. Tests can supply
an isolated census and writers, trigger the same UI continuations, and avoid
owner neighborhood writes or screen transitions. The selected-family resolver,
accept/back commands, requested member modification, and current mode are
internally accessible for that purpose.

No game launch or build was performed by this subtask. Source whitespace checks
pass; the root task owns integrated compilation and behavioral/visual tests.

## Remaining gaps

- Native age/gender changes remember head/body choices in a four-type suit
  table; that table is not yet reproduced.
- Name and bio retain existing `UITextBox`/`UITextEdit` interaction mechanics.
  Font and bio line capacity are corrected, but their caret, clipping, margin,
  and keyboard behavior are not claimed identical to `cTSWinTextEdit2`.
- Family filename character filtering is not yet reproduced.
- Family member portrait sheets and wrapped caption geometry retain earlier
  approximations; this round fixes selection feedback only.
- Family eligibility/order and the family creation/deletion model retain their
  existing implementation beyond the corrected desktop selection route.
- The preview camera and desktop screen-transition animation are not changed.

These corrections establish concrete working paths; they do not certify
complete Create a Sim / Create a Family parity.
