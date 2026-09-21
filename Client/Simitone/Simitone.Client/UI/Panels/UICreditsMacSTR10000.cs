using System;

namespace Simitone.Client.UI.Panels
{
    // UI-30: the Mac-port credits block, ported from the ORIGINAL PPC engine's
    // InsertMacCredits law (coordination/evidence/UI-29/credits-law.md §4.1):
    // the native reads resource-fork 'STR#' id 10000 (0x59fd78/0x59f940) and
    // inserts each line FIRST on the shared credits timeline. This file is the
    // extracted TEXT ONLY (no art, no code) — parse of the classic resource map
    // of "The Sims Complete/..namedfork/rsrc" by
    // tools/iff-dump/r265-ui30/extract_str10000.py (47 Pascal strings,
    // MacRoman -> UTF-8). Lines that start with '-' are the native's
    // language-object alternates (TOC-0x508c+9); the port has no language
    // object, so UICreditsScreen renders them as blank separator lines
    // (disclosed model).
    public static class UICreditsMacSTR10000
    {
        public const int ResourceId = 10000;

        public static readonly string[] Lines = new string[]
        {
            "THE SIMS COMPLETE FOR MACINTOSH",
            "-",
            "MACINTOSH CONVERSION BY",
            "Westlake Interactive, Inc.",
            "and",
            "Aspyr Studios",
            "-",
            "-",
            "MAC OS PROGRAMMING",
            "Phil Sulak",
            "John Butler",
            "Mark Krenek",
            "-",
            "-",
            "-",
            "FOR ASPYR MEDIA",
            "-",
            "PROJECT MANAGER",
            "Amity Lesko",
            "-",
            "QA LEAD",
            "Karen Halloran",
            "-",
            "LEAD DESIGNER",
            "Shawn Rossi",
            "-",
            "ASSISTANT PUBLISHER",
            "Elizabeth Howard",
            "-",
            "DIRECTOR OF MARKETING",
            "Amy Torres",
            "-",
            "DIRECTOR OF PC & MAC DEVELOPMENT",
            "Glenda Adams",
            "-",
            "MAC PROGRAMMING",
            "Mark Krenek",
            "-",
            "THANKS TO",
            // byte-faithful to the resource ('G'+MacRoman 0xB8+'nther')
            "Will Wright, Linda Chaplin, Shannon G¸nther",
            "and everyone at Electronic Arts, Maxis",
            "Rich Hernandez, Apple Computer",
            "and all our beta testers",
            "-",
            "-",
            "ORIGINAL PC SOFTWARE CREDITS",
            "-",
        };
    }
}
