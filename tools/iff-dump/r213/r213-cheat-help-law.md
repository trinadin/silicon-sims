# R213 — The cheat HELP window: the registered-command autocomplete, decoded and ported

Post-1.0 round #2 (the R207 disclosure "the engine's SetMode(1) edit mode and
the cheat-HELP window (the '?' cheat list) are unported"). Both halves close —
one of them by CORRECTION: there is no `SetMode`.

## The SetMode correction

The R207 doc read the FinishSlowInit call at 0x52fd60 as `SetMode(1)`. The
symbol index resolves 0x52fd62 as **`cTSWinTextEdit2::SetInvalEveryFrame(bool)`**
— an 8-byte setter (`stb r4, 0x14f(r3); blr`): the cheat bar is invalidated
every frame so the CARET keeps blinking through the opaque background. There
is no edit "mode 1"; the R207 attribution is corrected here (the R205
+0x1a8-correction precedent).

## cTSWinCheatHelp (0x50e8c0) — the help window

- **SetupHelpList 0x49e590**: new(0x128) + ctor(manager, font) at manager+0x4c
  (the main window's +0xec), Init, then immediately HIDDEN (vtable+0xa0).
- **Init 0x50e532**: three factory children via the ctrl-mgr slot lookup —
  the strings are data 0x74a2c+0x2b/+0x2e/+0x35 = **"OK" / "Cancel" /
  "<unknown>"** (the third slot carries the message label) — plus a 0x110 list
  (new + ctor 0x537680) at this+0x100. All window/button rects come from TOC
  pairs (0x97780..0x977b8) that resolve BEYOND sec1 = BSS runtime data
  (disclosed). The same data block's head is the verbatim
  **"Sorry, no cheats match the search string."**
- **Open(const char* prefix) 0x50da20**: Show; clear the list (0x536930);
  iterate the manager's registry (this+0x110, iterator 0x49f1f0) inserting
  every registered name that PREFIX-MATCHES case-insensitively (tolower copy
  0xfb260 + compare 0x35080/0x59ce40; prefix == null inserts EVERYTHING);
  track the EXACT match — at the end select it if present (select =
  count-1 at the exact hit, else index 0 via 0x5363e0); when nothing matched,
  the message label's caption is set from the block base (the "Sorry…" text).
- **CheatCodeMgrCallback 0x49e670** (the edit's event sink): event code 1 →
  `Open(null)` (the FULL list — the bar opening/empty), event code 2 →
  `Open(currentText)` (the typed prefix). After every Open the callback
  RE-FOCUSES the bar's edit through the main-window chain — focus never
  leaves the bar; the help rides the chain as an auxiliary window.
- **TSOnKeyDown 0x50de20**: Enter (0x0D) posts the OK button's command,
  Escape (0x1B) the Cancel button's; every other key passes through to the
  base (the edit keeps typing).
- **TSOnCommand 0x50dee0**: OK with a SELECTION → take the list's selected
  text (0x536350), copy+normalize, and **SetText it into the bar's edit
  (main-window vtable+0x1c8)**, re-select the edit (vtable+0x1cc with 1), and
  remove the help from the window chain (0x51c910 id 0x10) — completing is
  NOT submitting; the user presses Enter again to run the command. OK with no
  selection / Cancel / the close box: remove only.

## The registered roster (recovered verbatim)

`cSimsApp::RegisterCheats` 0x2515c0 loads its names from TOC[-0x5180] =
0x534d8 (a sec1 block); the registration names are the nul-terminated strings
at +0xb95..+0xf2c — **92 names, byte-verbatim, in registration order**
(interests, draw_routes, draw_origins, draw_floorable, write_routes,
write_destlist, sim_log, grow_grass, map_edit, edit_grass, move_objects,
lot_size, rotation, motives, autonomy, all_menus, preview_anims, lot,
dump_happy, dump_mc, allow_inuse, prepare_lot, swap_houses, lot_border,
sim_speed, set_hour, route_balloons, #import, #export, refresh_textures,
sound, music, obj_comp, ngh, cam_mode, reload_people, xyzzy, plugh,
porntipsguzzardo, visitor_control, soundevent, sev, restore_tut, crash,
debug_social, rebuild_cp, assert, refresh_faces, edit_char, draw_all_frames,
log_animations, log_mask, tile_info, memview, fam_test, water_tool,
shrink_text, tutorial, html, browser_failsafe, auto_level, sound_log,
core_dump, hist_add, history, auto_reset, sim_peek, flush, save, quit,
import, quats, report_assets, bubble_tweak, rosebud, ct, set_level,
pet_control, show_animations, show_sounds, make_dynamic, fame_ui, magic_book,
transform_me, age_me, person_me, cook_book, kid_me, cat_me, dog_me,
magic_tokens, show_bodystrings). The tail past "rosebud" is expansion-era;
the base/expansion boundary is not statically provable from the Complete
binary (disclosed) — the shipping engine registers them all, so the port's
help lists the full decoded roster.

## The port

`UIOriginalCheatHelpLaw` (new, Panels) pins the roster + the no-match string +
the Filter/SelectIndex laws. `UICheatTextbox` gains the help surface: an
opaque RGB(0,0,82) panel (the R142 system-dialog fill) under the bar at
(20,44), 8 original-glyph rows on font 10 (the cheat face) with cyan
selection, click-to-select, and the verbatim "Sorry, no cheats match the
search string." label. The Update wiring: chord-show → CheatHelpOpen(null)
(event 1); text change → CheatHelpOpen(text) (event 2); Enter with a
selection completes the text (`name + " "`, ready for parameters — the
engine's SetText) and closes the help; Enter with no selection or Escape
closes the help only; the bar's own hide also hides the help. After
Escape/OK the help stays dismissed until the bar re-opens (the engine removes
it from the focus chain; the re-show-on-typing-after-Escape cadence is
undecoded — disclosed).

## Gate

`uicheathelp` (default suite, 125 → **126 checks**): the no-match string +
roster canon (92 entries, first/move_objects/#import/rosebud/last), the
filter law (null = all, "move" = exactly move_objects, "s" family, "zzzz" =
empty), the selection law (exact-match index, else 0, −1 empty), and a fresh
production box driven through the real surface: Open(null) = 92 visible +
interests selected, prefix filters, the no-match label state, Enter = OK
(completion counter + `move_objects ` in the bar + help closed + bar stays),
Escape = Cancel (help only), OK-no-selection (closes, text untouched), and
the REAL mounted frontend box carrying the surface hidden. Targeted soak
uicheathelp+uicheat+corpus 7/0 (`uicheat` UNREGRESSED); FULL DEFAULT GATE
**126 passed, 0 failed**, clean exit probes, WRAPPER_EXIT=0; dist DLLs
byte-match publish (Simitone.Client, FSO.UI, FSO.Client, FSO.SimAntics,
FSO.Common all MATCH).

## Disclosed residuals

- The window/button rects are BSS (the port's (20,44) placement + row metrics
  are the disclosed substitute); the fill/row colors follow the R142 system
  dialog + selection conventions, not a recovered palette.
- The base/expansion roster boundary; the re-show cadence after Escape; the
  single-vs-double-click list behavior (cmd 0x15's exact trigger).
- The help rows render original glyphs (the R100 dialog-face family), while
  the bar's own careted input stays modern (the standing edit convention).

Evidence: r213-disasm-cheathelp-{ctor,init,open,keycmd-paint,oncommand}.txt /
cheatmgr-callback.txt / setuphelplist.txt / registercheats.txt +
targeted-soak.log + gate-run.log. No proprietary payload (cheat names and
window strings are engine identifiers).
