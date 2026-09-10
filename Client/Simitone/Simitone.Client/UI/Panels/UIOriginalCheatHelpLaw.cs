using System.Collections.Generic;

namespace Simitone.Client.UI.Panels
{
    /// ROUND-213 'uicheathelp' (tools/iff-dump/r213/r213-cheat-help-law.md):
    /// the cheat bar's HELP window, engine-decoded. cTSCheatCodeManager::
    /// SetupHelpList 0x49e590 creates cTSWinCheatHelp (0x50e8c0, hidden after
    /// Init). CheatCodeMgrCallback 0x49e670 drives it from edit events: code 1
    /// -> Open(null) (the FULL registered list), code 2 -> Open(typedText)
    /// (case-insensitive prefix filter). Open 0x50da20 clears the list widget,
    /// inserts every matching REGISTERED name in registry order, selects the
    /// exact match if present else index 0, and when nothing matches shows the
    /// verbatim "Sorry, no cheats match the search string." label (data
    /// 0x74a2c). TSOnKeyDown 0x50de20: Enter = the OK button, Escape = Cancel,
    /// every other key passes through to the edit (focus stays in the bar —
    /// the callback re-focuses it after every Open). OK 0x50dee0 with a
    /// selection copies the name into the bar's edit (vtable+0x1c8 SetText),
    /// re-selects it, and closes the help — submitting is a SECOND Enter; OK
    /// with no selection / Cancel just closes the help. The window's own rects
    /// are BSS runtime data (disclosed): the port places it directly under the
    /// (20,20)-(220,41) bar. The registered-name roster below is byte-verbatim
    /// from the cSimsApp::RegisterCheats name block (TOC[-0x5180] = 0x534d8,
    /// +0xb95..+0xf2c) in registration order; the tail past "rosebud" is
    /// expansion-era (the base/expansion boundary is not statically provable —
    /// disclosed; the shipping Complete engine registers them all).
    public static class UIOriginalCheatHelpLaw
    {
        public const string NoMatchText = "Sorry, no cheats match the search string.";

        public static readonly string[] EngineRegisteredCheats =
        {
            "interests", "draw_routes", "draw_origins", "draw_floorable",
            "write_routes", "write_destlist", "sim_log", "grow_grass",
            "map_edit", "edit_grass", "move_objects", "lot_size",
            "rotation", "motives", "autonomy", "all_menus",
            "preview_anims", "lot", "dump_happy", "dump_mc",
            "allow_inuse", "prepare_lot", "swap_houses", "lot_border",
            "sim_speed", "set_hour", "route_balloons", "#import",
            "#export", "refresh_textures", "sound", "music",
            "obj_comp", "ngh", "cam_mode", "reload_people",
            "xyzzy", "plugh", "porntipsguzzardo", "visitor_control",
            "soundevent", "sev", "restore_tut", "crash",
            "debug_social", "rebuild_cp", "assert", "refresh_faces",
            "edit_char", "draw_all_frames", "log_animations", "log_mask",
            "tile_info", "memview", "fam_test", "water_tool",
            "shrink_text", "tutorial", "html", "browser_failsafe",
            "auto_level", "sound_log", "core_dump", "hist_add",
            "history", "auto_reset", "sim_peek", "flush",
            "save", "quit", "import", "quats",
            "report_assets", "bubble_tweak", "rosebud", "ct",
            "set_level", "pet_control", "show_animations", "show_sounds",
            "make_dynamic", "fame_ui", "magic_book", "transform_me",
            "age_me", "person_me", "cook_book", "kid_me",
            "cat_me", "dog_me", "magic_tokens", "show_bodystrings",
        };

        /// Open's filter: null/empty prefix = everything (event code 1),
        /// else case-insensitive prefix match (event code 2).
        public static List<string> Filter(string prefix)
        {
            var result = new List<string>();
            foreach (var name in EngineRegisteredCheats)
            {
                if (string.IsNullOrEmpty(prefix)) { result.Add(name); continue; }
                if (name.Length >= prefix.Length &&
                    string.Compare(name, 0, prefix, 0, prefix.Length, true) == 0)
                    result.Add(name);
            }
            return result;
        }

        /// Open's selection law: the exact (case-insensitive) match if
        /// present, else index 0; -1 only when the list is empty.
        public static int SelectIndex(List<string> matches, string prefix)
        {
            if (matches == null || matches.Count == 0) return -1;
            if (!string.IsNullOrEmpty(prefix))
                for (int i = 0; i < matches.Count; i++)
                    if (string.Equals(matches[i], prefix, System.StringComparison.OrdinalIgnoreCase))
                        return i;
            return 0;
        }
    }
}
