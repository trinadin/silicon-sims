using FSO.Client;
using FSO.Common;
using FSO.Common.Utils;
using FSO.Files.Formats.IFF.Chunks;
using FSO.LotView.Model;
using FSO.SimAntics;
using FSO.SimAntics.Engine.TSOTransaction;
using FSO.SimAntics.Model;
using FSO.SimAntics.Model.TS1Platform;
using FSO.SimAntics.NetPlay.Drivers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Color = Microsoft.Xna.Framework.Color;
using Path = System.IO.Path;

namespace Simitone.Client.UI.Model
{
    /// <summary>
    /// ENG-27: the original at-save family web page exporter (the named
    /// GetHouseStats consumer, R133). Runs after every successful save when
    /// Options → Export HTML is on. Token templates ship with the game
    /// (UserData/Web Templates/{English,French}); this generator writes:
    ///
    ///   Web Pages/
    ///     main.html (the family directory — ShowAll() iframes familyN.html),
    ///     neighborhood.html, addressbook.html (bare frameset, nothing spliced),
    ///     navigation/blank/loading/publish.html, familyN.html per family,
    ///     NeighborhoodGFX + root assets
    ///     <FamilyName>_<id>/            (folder name = familyX link law)
    ///       familyhome.html, familymemberK.html, house.html,
    ///       scrapbookK.html + scrapbook_popupK.html, lot-template assets
    ///       FamilyGFX/familyK_face.jpg (45x45 portrait), familyK-face.jpg
    ///         (the directory's hyphen variant), familyK_full.jpg (100x220
    ///         vita-rect render), house-exterior.jpg + house-floor1.jpg +
    ///         house-floor2.jpg (the native 432x288 canvases on the decoded
    ///         0x77,0xA5,0x8B backdrop, JPEG quality 90; captured per floor
    ///         across frames), house-thumb.jpg (99x60), scrapbookK.jpg +
    ///         scrapbookK_thumb.jpg
    ///
    /// AUD-18/R2 decoded and applied: the display words come from UIText.iff
    /// STR#158 'WebPageStrings' (the native InitLocalizedStrings table —
    /// Unemployed/Male/Female/.../Adult/Kid/human/dog/cat, per language);
    /// children's career/job = 'School' (STR#154[17]) and performance = the
    /// report-card grade (work.iff STR#4097); lang codes are the native
    /// en-us/fr; sims_game_edition is the numeric edition bitmask (255 for
    /// this Complete Collection build); square feet converts to square
    /// meters for non-English locales; the family folder and familyX ids
    /// use the HOUSE NUMBER (Family+0x110).
    ///
    /// Undecoded-and-disclosed choices: the house renders capture the live
    /// view per floor (the native renders offscreen via HouseViewer.
    /// MakeWebPageThumbnail with a ShrinkWrap crop + a neighborhood overlay
    /// on the thumb — the overlay resource is not ported); the player's
    /// wall/cutaway mode is kept for interiors; the faminfo_&lt;id&gt;.ini/.pub teleport metadata (a 64 KB
    /// htmlEncrypt-scrambled dependency inventory written to Export/ by
    /// MakePublishingInfo) is not ported — it serves TheSims.com teleport,
    /// not the web pages. Job titles are gendered since EXP-16
    /// (cJob::GetName(GetGender==1) @ 0x1021d798; STR law in
    /// evidence/EXP-16/gendered-job-titles-law.md). All materials are original (templates,
    /// .ffn-rendered data, engine renders); no proprietary bytes enter the
    /// repo.
    /// </summary>
    public static class OriginalWebExporter
    {
        public static int Exports;          // completed exports (probe)
        public static int ExportFailures;   // caught failures (probe)
        public static int UnresolvedTokens; // any ^^^^ left in output (probe; want 0)

        private const string PendingHouseFile = "@housecapture"; // marker under the family dir

        /// <summary>AUD-18: the templates declare charset iso-8859-1 and the
        /// French corpus is latin-1 bytes; the native copies bytes verbatim.
        /// Encoding.Default is UTF-8 on .NET 9 and would mojibake every
        /// accented byte on the read/write roundtrip — use latin-1.</summary>
        private static readonly Encoding TemplateEncoding = Encoding.Latin1;

        // ------------------------------------------------------------------
        // Entry — called after a successful save.
        // ------------------------------------------------------------------
        public static void Export(Screens.TS1GameScreen game)
        {
            try
            {
                var vm = game?.vm;
                var family = game?.ActiveFamily;
                if (vm == null || family == null || family.FamilyGUIDs == null) return;

                var provider = FSO.Content.Content.Get()?.Neighborhood;
                if (provider == null) return;

                var stats = OriginalHouseStats.Compute(vm);
                var members = RenderMembers(family); // renders + names, scratch world

                string famName = null;
                try { famName = family.ChunkParent.Get<FSO.Files.Formats.IFF.Chunks.FAMs>(family.ChunkID)?.GetString(0); } catch { }
                if (string.IsNullOrEmpty(famName)) famName = "Family";

                var templates = TemplateRoot();
                if (templates == null) throw new DirectoryNotFoundException("Web Templates not found");

                // family folder: <NameNoSpaces>_<houseNumber> — AUD-18 (decode):
                // the native ExportHTML builds it from Family+0x110, the SAME
                // field GenerateHouseData prints for sims_house_address (the
                // lot number) — not the family id (Family+0x10c, used only by
                // the teleport faminfo files). Homeless fallback: chunk id.
                var id = (family.HouseNumber != 0) ? family.HouseNumber : family.ChunkID;
                var famDirName = new string(famName.Where(char.IsLetterOrDigit).ToArray()) + "_" + id;
                var root = Path.Combine(FSOEnvironment.UserDir, "Web Pages");
                var famRoot = Path.Combine(root, famDirName);
                Directory.CreateDirectory(famRoot);
                Directory.CreateDirectory(Path.Combine(famRoot, "FamilyGFX"));

                // ---- lot template folder: <lot>_Sim_Lane, else LotTemplates ----
                var lot = family.HouseNumber;
                var lotTpl = Path.Combine(templates, lot + "_Sim_Lane");
                if (!Directory.Exists(lotTpl)) lotTpl = Path.Combine(templates, "LotTemplates");
                if (!Directory.Exists(lotTpl)) throw new DirectoryNotFoundException("lot template folder");

                // ---- copy assets (everything that is not .html) ----
                CopyTree(lotTpl, famRoot);
                CopyTree(templates, root, recursive: false); // css, jpgs at root only
                // AUD-18: the root pages reference NeighborhoodGFX/... from
                // the template root (main.html address art, publish.html
                // iagree/idisagree buttons, navigation) — copy it too.
                var ngfx = Path.Combine(templates, "NeighborhoodGFX");
                if (Directory.Exists(ngfx)) CopyTree(ngfx, Path.Combine(root, "NeighborhoodGFX"));

                // ---- shared token values ----
                var charset = "iso-8859-1";
                // native GetWebLanguageCode table (r2 blob at the charset pool):
                // en-us, en-gb, fr, de, it, es, nl, dn, sv, nr, fn, pt, ja,
                // pl, zh-cn, zh-tw, th, ko — lang ids 1 en-us, 2 en-gb, 3 fr.
                var langCode = "en-us";
                if (STR.DefaultLangCode == STRLangCode.French) langCode = "fr";
                // AUD-18 (decode): sims_game_edition substitutes the EDITION
                // BITMASK ushort (singleton+0x66) as a bare decimal — the
                // publish.html template uses it as an unquoted JS literal.
                // Pinned bits: 0x20 Unleashed, 0x40 Superstar, 0x80 Makin'
                // Magic (overlay/fame/magic-town gates); those force exactly
                // five lower product bits (LL/HD/HV/Vacation/Deluxe) — this
                // Complete Collection build has every product => 0xFF = 255.
                var gameEdition = "255";
                // AUD-18: sims_house_address is the BARE LOT NUMBER, not
                // "N Sim Lane" — the templates use it in JS truthiness
                // (if (^^^^sims_house_address^^^^)), inside the neighborhood
                // image name (nh^^^^sims_house_address^^^^.jpg, and the lot
                // folders really ship nh<lot>.jpg), and always followed by a
                // literal " Sim Lane" ("Number ^^^^...^^^^ Sim Lane:").
                var address = lot.ToString();

                var baseTokens = new Dictionary<string, string>
                {
                    { "sims_web_character_set", charset },
                    { "sims_web_language_code", langCode },
                    { "sims_game_edition", gameEdition },
                    { "sims_house_address", address },
                    { "sims_family_name", famName },
                    { "sims_family_nummembers", members.Count.ToString() },
                    { "sims_family_cashbalance", family.Budget.ToString() },
                    { "sims_family_daysexist", (VMTS1LotState.Active?.DaysRunning ?? 0).ToString() },
                    { "sims_family_numfriends", Simitone.Client.UI.Panels.Desktop.UIDesktopUCP.ComputeFamilyFriendCount(game).ToString() },
                    { "sims_neighborhood_numfamilies", CountFamilies().ToString() },
                    { "sims_house_numfloors", CountFloors(vm).ToString() },
                    { "sims_house_numbedrooms", stats.Bedrooms.ToString() },
                    { "sims_house_numbathrooms", stats.Bathrooms.ToString() },
                    // AUD-18 (decode): GenerateHouseData emits raw square feet
                    // only for lang ids 1/2 (en-us/en-gb); every other locale
                    // converts to square meters — (int)(sqft * 0.09290304)
                    // (the float32 constant is in the binary at 0x5a46e0).
                    { "sims_house_squarefeet", (STR.DefaultLangCode == STRLangCode.French)
                        ? ((int)(stats.SquareFeet * 0.09290304f)).ToString()
                        : stats.SquareFeet.ToString() },
                    { "sims_house_value", (family.Budget + family.ValueInArch).ToString() },
                    { "sims_house_ratings_size", stats.SizeScore.ToString() },
                    { "sims_house_ratings_furnishings", stats.FurnishingsScore.ToString() },
                    { "sims_house_ratings_upkeep", stats.UpkeepScore.ToString() },
                    { "sims_house_ratings_layout", stats.LayoutScore.ToString() },
                    { "sims_house_ratings_yard", stats.YardScore.ToString() },
                };
                for (int i = 1; i <= 8; i++)
                    baseTokens["sims_familymember" + i + "_name"] = (i <= members.Count) ? members[i - 1].Name : "";

                // ---- scrapbook ----
                // AUD-18: the shipped templates carry captions 1..200 (the
                // grid's c1..c200 JS vars in scrapbookX.html) — no index 0.
                // The native GenerateFamilyAndScrapbookData defaults all 200
                // caption slots to the empty string, so missing pages are "".
                var pages = OriginalSnapshotAlbum.Pages;
                baseTokens["sims_scrapbook_numpages"] = pages.Count.ToString();
                for (int i = 1; i <= 200; i++)
                    baseTokens["sims_scrapbook" + i + "_caption"] = (i <= pages.Count) ? (pages[i - 1].Description ?? "") : "";

                // ---- pages from the lot template ----
                WritePage(Path.Combine(famRoot, "familyhome.html"), ReadText(lotTpl, "familyhome.html"), baseTokens);
                WritePage(Path.Combine(famRoot, "house.html"), ReadText(lotTpl, "house.html"), baseTokens);

                var memberTpl = ReadText(lotTpl, "familymemberX.html");
                for (int i = 1; i <= members.Count; i++)
                {
                    var tok = new Dictionary<string, string>(baseTokens) { { "sims_familymemberX_number", i.ToString() } };
                    foreach (var kv in members[i - 1].Tokens) tok[kv.Key] = kv.Value;
                    WritePage(Path.Combine(famRoot, "familymember" + i + ".html"), memberTpl, tok);
                }

                var scrapTpl = ReadText(lotTpl, "scrapbookX.html");
                var popupTpl = ReadText(lotTpl, "scrapbook_popupX.html");
                for (int i = 1; i <= pages.Count; i++)
                {
                    // AUD-18: the popup template's per-page tokens are
                    // sims_scrapbookX_snapshot / X_caption / currpagenum
                    // (there is no X_number token in any template).
                    var tok = new Dictionary<string, string>(baseTokens)
                    {
                        { "sims_scrapbookX_caption", pages[i - 1].Description ?? "" },
                        { "sims_scrapbookX_snapshot", "scrapbook" + i + ".jpg" },
                        { "sims_scrapbook_currpagenum", i.ToString() },
                    };
                    WritePage(Path.Combine(famRoot, "scrapbook" + i + ".html"), scrapTpl, tok);
                    WritePage(Path.Combine(famRoot, "scrapbook_popup" + i + ".html"), popupTpl, tok);
                }

                // ---- FamilyGFX ----
                for (int i = 0; i < members.Count; i++)
                {
                    if (members[i].Face != null)
                    {
                        SaveJpeg(Path.Combine(famRoot, "FamilyGFX", "family" + (i + 1) + "_face.jpg"), members[i].Face, 90);
                        File.Copy(Path.Combine(famRoot, "FamilyGFX", "family" + (i + 1) + "_face.jpg"),
                                  Path.Combine(famRoot, "FamilyGFX", "family" + (i + 1) + "-face.jpg"), true); // the directory's hyphen variant
                    }
                    if (members[i].Full != null)
                        SaveJpeg(Path.Combine(famRoot, "FamilyGFX", "family" + (i + 1) + "_full.jpg"), members[i].Full, 90);
                }
                for (int i = 0; i < pages.Count; i++)
                {
                    if (pages[i].Photo != null)
                    {
                        // AUD-18: the album grid (scrapbookX.html) references
                        // FamilyGFX/scrapbook<i>_thumb.jpg (popup <img> uses the
                        // full-size scrapbook<i>.jpg via X_snapshot). The grid
                        // cell is 100 wide; the album's own native thumbnail
                        // law (R239) is 100x75 — same size here.
                        SaveJpeg(Path.Combine(famRoot, "FamilyGFX", "scrapbook" + (i + 1) + ".jpg"), pages[i].Photo, 90);
                        using (var img = LoadTexture(pages[i].Photo))
                        {
                            img.Mutate(x => x.Resize(100, 75));
                            using (var fs = File.Create(Path.Combine(famRoot, "FamilyGFX", "scrapbook" + (i + 1) + "_thumb.jpg")))
                                img.SaveAsJpeg(fs, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = 90 });
                        }
                    }
                }

                // ---- root pages ----
                // AUD-18 (re-derived from the corpus + binary): addressbook.html
                // is a bare FRAMESET (main=loading.html, nav=navigation.html) with
                // no <body> — nothing is spliced into it. The family directory is
                // main.html itself: its ShowAll() emits
                // <IFRAME SRC='family<a>.html'> for a=1..sims_neighborhood_numfamilies,
                // so the per-family blocks are familyN.html files in the ROOT
                // (native MakeWebPageX instantiates familyX.html the same way).
                GenerateFamilyPages(templates, root, baseTokens);
                foreach (var name in new[] { "main", "neighborhood", "navigation", "blank", "loading", "publish", "addressbook" })
                {
                    var tpl = ReadText(templates, name + ".html");
                    if (tpl == null) continue;
                    WritePage(Path.Combine(root, name + ".html"), tpl, baseTokens);
                }
                WriteIndexRedir(root, baseTokens); // index.html → main.html (convenience; the original's host served main)

                // ---- house renders: arm the next-frame backbuffer captures ----
                // AUD-18/R2 (decode, ExportHTML @0x10218070 tail): the native
                // writes FOUR FamilyGFX JPEGs via HouseViewer::MakeWebPageThumbnail
                // — house-thumb.jpg, house-exterior.jpg, house-floor1.jpg (level
                // 1, always) and house-floor2.jpg (level 2, only when the
                // family HasBeenTo2ndLevel = state byte +0x80). All renders are
                // 0x1b0x0x120 = 432x288 canvases, ShrinkWrap-cropped on the
                // 0x77,0xA5,0x8B backdrop, JPEG quality 0x5a = 90. Port law:
                // capture the live view per floor across frames (the R190
                // readback discipline; the native renders offscreen on a
                // HouseViewer — a dedicated renderer the port does not have),
                // letterboxed onto the native 432x288 backdrop canvas. The
                // floor-2 gate uses the port's structural equivalent (the lot
                // has a second story); the player's wall/cutaway mode is kept
                // as-is for interiors.
                HouseCaptureRequest = famRoot;
                HouseCapturePath = Path.Combine(famRoot, "FamilyGFX", "house-exterior.jpg");
                HouseCaptureIsExterior = true;
                CaptureWorld = vm.Context.World;
                if (CaptureWorld != null)
                {
                    CaptureRestoreLevel = CaptureWorld.State.Level;
                    PendingShots = new Queue<(string path, sbyte level)>();
                    PendingShots.Enqueue((Path.Combine(famRoot, "FamilyGFX", "house-floor1.jpg"), 1));
                    if (CountFloors(vm) >= 2)
                        PendingShots.Enqueue((Path.Combine(famRoot, "FamilyGFX", "house-floor2.jpg"), 2));
                }

                Exports++;
            }
            catch (Exception e)
            {
                ExportFailures++;
                Simitone.Client.GameLog.Write("webexport: EXC " + e.GetType().Name + " " + e.Message + " | " + e.StackTrace.Replace("\n", " "));
            }
        }

        // ------------------------------------------------------------------
        // House capture delivery (called by OriginalWebExportScene next frame).
        // Multi-frame state machine: frame N captures the current view (the
        // exterior); OnHouseCaptured then flips World.State.Level for the
        // next shot, the world redraws on frame N+1, and the scene captures
        // that — one frame per shot, restoring the player's level at the end.
        // ------------------------------------------------------------------
        internal static string HouseCaptureRequest;   // non-null = capture wanted
        internal static string HouseCapturePath;
        internal static bool HouseCaptureIsExterior;  // exterior also writes the 99x60 thumb
        internal static FSO.LotView.World CaptureWorld;
        internal static sbyte CaptureRestoreLevel;
        internal static Queue<(string path, sbyte level)> PendingShots;

        /// <summary>The native render canvas: 0x1b0 x 0x120 = 432x288, on the
        /// decoded 0x77,0xA5,0x8B backdrop (letterboxed port approximation of
        /// the native ShrinkWrap crop).</summary>
        private const int HouseShotW = 432, HouseShotH = 288;

        internal static void OnHouseCaptured(Texture2D captured)
        {
            var path = HouseCapturePath;
            var isExterior = HouseCaptureIsExterior;
            if (captured == null || path == null)
            {
                // failed read (or unarmed): clear everything and restore
                HouseCaptureRequest = null;
                HouseCapturePath = null;
                PendingShots = null;
                RestoreCaptureLevel();
                return;
            }
            HouseCapturePath = null;
            try
            {
                using (var img = LoadTexture(captured))
                {
                    // native canvas law: fit onto 432x288 over the native
                    // backdrop color, JPEG quality 90 (SaveGimex 0x5a)
                    using (var canvas = img.Clone(ctx => ctx.Resize(new SixLabors.ImageSharp.Processing.ResizeOptions
                    {
                        Mode = SixLabors.ImageSharp.Processing.ResizeMode.Pad,
                        Position = SixLabors.ImageSharp.Processing.AnchorPositionMode.Center,
                        PadColor = new SixLabors.ImageSharp.Color(new SixLabors.ImageSharp.PixelFormats.Rgba32(0x77, 0xA5, 0x8B)),
                        Size = new SixLabors.ImageSharp.Size(HouseShotW, HouseShotH),
                    })))
                    using (var fs = File.Create(path))
                        canvas.SaveAsJpeg(fs, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = 90 });

                    if (isExterior)
                    {
                        // house-thumb: the directory's declared 99x60 (the
                        // native additionally composites a neighborhood
                        // overlay resource — not ported, disclosed)
                        img.Mutate(x => x.Resize(99, 60));
                        using (var fs = File.Create(Path.Combine(Path.GetDirectoryName(path), "house-thumb.jpg")))
                            img.SaveAsJpeg(fs, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = 90 });
                    }
                }
            }
            catch (Exception e)
            {
                ExportFailures++;
                Simitone.Client.GameLog.Write("webexport: house capture EXC " + e.GetType().Name + " " + e.Message);
            }
            finally
            {
                captured.Dispose(); // we own the backbuffer copy from here
            }

            // advance to the next floor shot (the world redraws with the new
            // level on the following frame, before this scene reads again)
            if (PendingShots != null && PendingShots.Count > 0 && CaptureWorld != null)
            {
                var next = PendingShots.Dequeue();
                HouseCapturePath = next.path;
                HouseCaptureIsExterior = false;
                try { CaptureWorld.State.Level = next.level; }
                catch (Exception e)
                {
                    Simitone.Client.GameLog.Write("webexport: floor level EXC " + e.GetType().Name + " " + e.Message);
                    HouseCaptureRequest = null; PendingShots = null; RestoreCaptureLevel();
                    return;
                }
            }
            else
            {
                HouseCaptureRequest = null; // queue done — stop capturing
                PendingShots = null;
                RestoreCaptureLevel();
            }
        }

        private static void RestoreCaptureLevel()
        {
            var world = CaptureWorld;
            CaptureWorld = null;
            if (world != null)
            {
                try { world.State.Level = CaptureRestoreLevel; } catch { }
            }
        }

        // ------------------------------------------------------------------
        // Member renders — scratch world (the UIHouseFamilyList pattern)
        // ------------------------------------------------------------------
        private sealed class MemberRender
        {
            public string Name;
            public Texture2D Face;
            public Texture2D Full;
            public readonly Dictionary<string, string> Tokens = new Dictionary<string, string>();
        }

        // ------------------------------------------------------------------
        // AUD-18/R2: the web display words come from UIText.iff STR#158
        // 'WebPageStrings' — the native InitLocalizedStrings (0x1021ff70)
        // loads exactly this table (set 0x9e, indices 1..13) into the
        // globals the exporter reads: 1 Unemployed, 2 Male, 3 Female,
        // 4..6 lot sizes, 7 Adult, 8 Kid, 9 human, 10 dog, 11 cat,
        // 12 Fame, 13 N/A. Language runs are NUL-terminated value+comment
        // pairs; run 1 = English, run 3 = French (matches the native lang
        // ids: 1 en-us, 2 en-gb, 3 fr — the template-folder law and the
        // square-feet branch use the same ids). NOTE the French species
        // entry is "Humain"/"Chien"/"Chat" — the native substitutes the
        // localized word verbatim, and the shipped French templates'
        // JS ('... == "human"') then hides the human-only rows; reading
        // the table reproduces the native behavior exactly.
        // STR#154 [17] is the child career/job word ("School"/"Ecole"),
        // the other InitLocalizedStrings global (set 0x9a).
        // ------------------------------------------------------------------
        private static string[] _webWords;   // index 0 unused, 1..13 as above
        private static string _schoolWord;

        private static string[] WebWords
        {
            get
            {
                if (_webWords == null) LoadWebWordTables();
                return _webWords;
            }
        }

        private static string SchoolWord
        {
            get
            {
                if (_webWords == null) LoadWebWordTables();
                return _schoolWord;
            }
        }

        private static readonly string[] WebWordsFallback =
        {
            null, "Unemployed", "Male", "Female", "Small", "Medium", "Large",
            "Adult", "Kid", "human", "dog", "cat", "Fame", "N/A",
        };

        private static void LoadWebWordTables()
        {
            var words = (string[])WebWordsFallback.Clone();
            string school = (STR.DefaultLangCode == STRLangCode.French) ? "Ecole" : "School";
            try
            {
                var path = Path.Combine(FSO.Content.Content.TS1HybridBasePath, "GameData/UIText.iff");
                if (!File.Exists(path)) { _webWords = words; _schoolWord = school; return; }
                var d = File.ReadAllBytes(path);
                int langWanted = (STR.DefaultLangCode == STRLangCode.French) ? 3 : 1;
                // container walk: same rule as OriginalLiveStrings (4CC + u32 BE
                // size + u16 id + u16 flags + 64-byte label, first chunk at 0x40)
                int off = 0x40, guard = 0;
                while (off + 76 <= d.Length && guard++ < 4096)
                {
                    var cc = d[off..(off + 4)];
                    var size = BitConverter.ToInt32(new byte[] { d[off + 7], d[off + 6], d[off + 5], d[off + 4] }, 0);
                    if (size <= 76 || off + size > d.Length) break;
                    var cid = (d[off + 8] << 8) | d[off + 9];
                    if (cc[0] == (byte)'S' && cc[1] == (byte)'T' && cc[2] == (byte)'R' && cc[3] == (byte)'#' && (cid == 158 || cid == 154))
                    {
                        var data = new byte[size - 76];
                        Array.Copy(d, off + 76, data, 0, data.Length);
                        if (cid == 158)
                        {
                            var run = ParseWebStringRun(data, langWanted);
                            if (run != null)
                                for (int i = 1; i <= 13 && i < run.Count; i++)
                                    if (!string.IsNullOrEmpty(run[i])) words[i] = run[i];
                        }
                        else
                        {
                            var run = ParseWebStringRun(data, langWanted);
                            if (run != null && run.Count > 17 && !string.IsNullOrEmpty(run[17]))
                                school = run[17];
                        }
                    }
                    off += size;
                }
            }
            catch { /* fall back to the decoded defaults above */ }
            _webWords = words;
            _schoolWord = school;
        }

        /// <summary>Parses a format -3 UIText STR# chunk: NUL-terminated
        /// (value, comment) pairs grouped into per-language runs (lang byte,
        /// value, comment, ...). Returns the 1-based-indexed run (slot 0
        /// null) for the requested language, or null.</summary>
        private static List<string> ParseWebStringRun(byte[] data, int langWanted)
        {
            try
            {
                if (data.Length < 4 || data[0] != 0xFD || data[1] != 0xFF) return null;
                var runs = new Dictionary<int, List<string>>();
                int p = 4, n = 0;
                while (p + 1 < data.Length && n++ < 2048)
                {
                    int lang = data[p++];
                    if (lang == 0) break;
                    int e = Array.IndexOf(data, (byte)0, p);
                    if (e < 0) break;
                    int len = e - p;
                    var val = TemplateEncoding.GetString(data, p, len);
                    p = e + 1;
                    e = Array.IndexOf(data, (byte)0, p);
                    if (e < 0) break;
                    p = e + 1; // comment (ignored)
                    if (!runs.TryGetValue(lang, out var list)) runs[lang] = list = new List<string>();
                    list.Add(val);
                }
                if (!runs.TryGetValue(langWanted, out var run)) run = runs.TryGetValue(1, out var en) ? en : null;
                if (run == null) return null;
                var result = new List<string> { null }; // index 0 unused (1-based table)
                result.AddRange(run);
                return result;
            }
            catch { return null; }
        }

        // The word tables now come from UIText.iff (see WebWords above) —
        // the native law (InitLocalizedStrings reads STR#158/154), replacing
        // the earlier hardcoded maps.

        private static List<MemberRender> RenderMembers(FSO.Files.Formats.IFF.Chunks.FAMI family)
        {
            var result = new List<MemberRender>();
            var world = new FSO.LotView.World(GameFacade.GraphicsDevice);
            VM vm = null;
            try
            {
                world.Initialize(GameFacade.Scenes);
                var context = new VMContext(world);
                vm = new VM(context, new VMServerDriver(new VMTS1GlobalLinkStub()), new VMNullHeadlineProvider());
                vm.Init();
                var blueprint = new FSO.LotView.Model.Blueprint(1, 1);
                context.Blueprint = blueprint;
                context.Architecture = new VMArchitecture(1, 1, blueprint, vm.Context);

                foreach (var guid in family.FamilyGUIDs)
                {
                    var grp = vm.Context.CreateObjectInstance(guid, LotTilePos.OUT_OF_WORLD, Direction.NORTH, true);
                    if (grp == null) continue;
                    var ava = grp.BaseObject as VMAvatar;
                    if (ava == null) { grp.BaseObject.Delete(true, vm.Context); continue; }
                    try
                    {
                        ava.Tick();
                        var face = Simitone.Client.UI.Panels.CAS.OriginalPersonPortraitRenderer.Generate(GameFacade.GraphicsDevice, ava.Avatar);
                        var full = RenderFullBody(ava);
                        var member = new MemberRender { Name = grp.BaseObject.Name ?? ("Member" + result.Count), Face = face, Full = full };
                        FillMemberTokens(member, ava);
                        result.Add(member);
                    }
                    finally { grp.BaseObject.Delete(true, vm.Context); }
                }
            }
            finally { world.Dispose(); }
            return result;
        }

        private static void FillMemberTokens(MemberRender m, VMAvatar ava)
        {
            var words = WebWords; // UIText.iff STR#158 (native InitLocalizedStrings)
            var gRaw = ava.GetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.Gender);
            bool dog = (gRaw & 8) != 0, cat = (gRaw & 16) != 0;
            bool female = (gRaw & 1) != 0;
            var age = ava.GetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.PersonsAge);
            bool child = age > 0 && age < 18;

            string bio = "";
            try
            {
                bio = ava.Object.Resource?.Get<CTSS>(2000)?.GetString(1);
                if (string.IsNullOrEmpty(bio)) bio = "";
            }
            catch { }

            m.Tokens["sims_familymemberX_name"] = m.Name;
            m.Tokens["sims_familymemberX_description"] = bio;
            // native GenerateFamilyMemberData: gender word = idx2 (male) /
            // idx3 (female); kidadult = idx7 (adult) / idx8 (kid); species =
            // idx9/10/11 — always the localized word (no empty-for-male
            // variant; the earlier R1 reading was a misdecode of the
            // cTSString char-buffer dereference).
            m.Tokens["sims_familymemberX_gender"] = words[female ? 3 : 2];
            m.Tokens["sims_familymemberX_kidadult"] = words[child ? 8 : 7];
            m.Tokens["sims_familymemberX_species"] = words[dog ? 10 : cat ? 11 : 9];

            m.Tokens["sims_familymemberX_zodiac"] = Simitone.Client.UI.Panels.CAS.UIOriginalDesignChar
                .ZodiacName(Math.Max(0, (int)ava.GetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.TS1Zodiac) - 1));

            m.Tokens["sims_familymemberX_skill_cooking"] = Skill(ava, FSO.SimAntics.Model.VMPersonDataVariable.CookingSkill);
            m.Tokens["sims_familymemberX_skill_mechanical"] = Skill(ava, FSO.SimAntics.Model.VMPersonDataVariable.MechanicalSkill);
            m.Tokens["sims_familymemberX_skill_charisma"] = Skill(ava, FSO.SimAntics.Model.VMPersonDataVariable.CharismaSkill);
            m.Tokens["sims_familymemberX_skill_body"] = Skill(ava, FSO.SimAntics.Model.VMPersonDataVariable.BodySkill);
            m.Tokens["sims_familymemberX_skill_logic"] = Skill(ava, FSO.SimAntics.Model.VMPersonDataVariable.LogicSkill);
            m.Tokens["sims_familymemberX_skill_creativity"] = Skill(ava, FSO.SimAntics.Model.VMPersonDataVariable.CreativitySkill);

            m.Tokens["sims_familymemberX_personality_neat"] = Pers(ava, FSO.SimAntics.Model.VMPersonDataVariable.NeatPersonality);
            m.Tokens["sims_familymemberX_personality_outgoing"] = Pers(ava, FSO.SimAntics.Model.VMPersonDataVariable.OutgoingPersonality);
            m.Tokens["sims_familymemberX_personality_active"] = Pers(ava, FSO.SimAntics.Model.VMPersonDataVariable.ActivePersonality);
            m.Tokens["sims_familymemberX_personality_playful"] = Pers(ava, FSO.SimAntics.Model.VMPersonDataVariable.PlayfulPersonality);
            m.Tokens["sims_familymemberX_personality_nice"] = Pers(ava, FSO.SimAntics.Model.VMPersonDataVariable.GenerousPersonality);

            try
            {
                var jobType = ava.GetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.JobType);
                var job = FSO.Content.Content.Get().Jobs.GetJob((ushort)jobType);
                var level = Math.Max(0, (int)ava.GetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.JobPromotionLevel));
                if (job != null && level >= 0 && level < job.JobLevels.Length)
                {
                    var jl = job.JobLevels[level];
                    m.Tokens["sims_familymemberX_career"] = job.ChunkLabel ?? "";
                    m.Tokens["sims_familymemberX_job"] = FamilyMemberJobTitle(ava, jobType, level, jl);
                    m.Tokens["sims_familymemberX_salary"] = jl.Salary.ToString();
                }
                else
                {
                    m.Tokens["sims_familymemberX_career"] = "";
                    m.Tokens["sims_familymemberX_job"] = words[1]; // Unemployed
                    m.Tokens["sims_familymemberX_salary"] = "0";
                }
            }
            catch
            {
                m.Tokens["sims_familymemberX_career"] = "";
                m.Tokens["sims_familymemberX_job"] = words[1]; // Unemployed
                m.Tokens["sims_familymemberX_salary"] = "0";
            }
            // native child law (GenerateFamilyMemberData GetAge!=0 branch):
            // career/job = the School word (STR#154[17]), performance = the
            // report-card grade (work.iff STR#4097, the R180 law), salary =
            // GetSalary of the (school) job as above.
            if (child)
            {
                m.Tokens["sims_familymemberX_career"] = SchoolWord;
                m.Tokens["sims_familymemberX_job"] = SchoolWord;
                m.Tokens["sims_familymemberX_performance"] =
                    Simitone.Client.UI.Panels.LiveSubpanels.UIJobSubpanel.GradeForIndex(
                        ava.GetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.JobPromotionLevel));
            }
            else
            {
                m.Tokens["sims_familymemberX_performance"] =
                    ava.GetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.JobPerformance).ToString();
            }
        }

        /// <summary>
        /// EXP-16: the native web-export job-title law, extracted pure for the
        /// ssfame gate. GenerateFamilyMemberData @ 0x1021d410 substitutes
        /// cJob::GetName(GetGender(person)==1) (call @ 0x1021d798) — the
        /// gendered STR title with the empty-female-falls-back-to-male law,
        /// not the single CARR JobName (disclosure retired).
        /// </summary>
        public static string FamilyMemberJobTitle(VMAvatar ava, int jobType, int level,
            FSO.Files.Formats.IFF.Chunks.JobLevel jl)
        {
            var female = (ava.GetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.Gender) & 1) == 1;
            return FSO.Content.Content.Get().Jobs.JobTitle((short)jobType, level, female)
                ?? jl?.JobName ?? "";
        }

        private static string Skill(VMAvatar ava, FSO.SimAntics.Model.VMPersonDataVariable v)        {
            return Math.Max(0, ava.GetPersonData(v) / 100).ToString();   // 0..1000 -> 0..10
        }

        private static string Pers(VMAvatar ava, FSO.SimAntics.Model.VMPersonDataVariable v)
        {
            // AUD-18 (decode): the native web export renders personality as
            // raw/100 (GenerateFamilyMemberData: itostr(short/100) on the five
            // personality slots) — the game's 0..10 trait scale, NOT 0..100.
            return Math.Max(0, ava.GetPersonData(v) / 100).ToString();    // 0..1000 -> 0..10
        }

        /// <summary>The 100x220 vita-rect full-body render (UIOriginalVitaPreview's
        /// decoded CAS-02 camera + DrawGeometry pattern, standalone).</summary>
        private static Texture2D RenderFullBody(VMAvatar ava)
        {
            var gd = GameFacade.GraphicsDevice;
            using (var target = new RenderTarget2D(gd, 100, 220, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8, 0, RenderTargetUsage.DiscardContents))
            using (var effect = FSO.Vitaboy.Avatar.Effect.Clone())
            {
                var camera = Simitone.Client.UI.Panels.CAS.UIOriginalVitaPreview.CreateNativeCamera(gd);
                var targets = gd.GetRenderTargets();
                try
                {
                    gd.SetRenderTarget(target);
                    gd.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.Transparent, 1, 0);
                    gd.BlendState = BlendState.AlphaBlend;
                    gd.DepthStencilState = DepthStencilState.Default;
                    gd.RasterizerState = RasterizerState.CullNone;
                    gd.SamplerStates[0] = SamplerState.LinearClamp;
                    camera.ProjectionDirty();
                    effect.CurrentTechnique = effect.Techniques[0];
                    effect.Parameters["View"].SetValue(camera.View);
                    effect.Parameters["Projection"].SetValue(camera.Projection);
                    effect.Parameters["World"].SetValue(Microsoft.Xna.Framework.Matrix.CreateRotationY(MathHelper.Pi));
                    effect.Parameters["AmbientLight"].SetValue(Vector4.One);
                    var avatar = ava.Avatar;
                    var lights = avatar.LightPositions;
                    avatar.LightPositions = null;
                    avatar.DrawGeometry(gd, effect);
                    avatar.LightPositions = lights;
                    var data = new Color[100 * 220];
                    target.GetData(data);
                    var tex = new Texture2D(gd, 100, 220);
                    tex.SetData(data);
                    return tex;
                }
                finally { gd.SetRenderTargets(targets); }
            }
        }

        // ------------------------------------------------------------------
        // helpers
        // ------------------------------------------------------------------
        private static string TemplateRoot()
        {
            var baseDir = Path.Combine(FSO.Content.Content.TS1HybridBasePath, "UserData/Web Templates");
            var langDir = (STR.DefaultLangCode == STRLangCode.French) ? "French" : "English";
            if (Directory.Exists(Path.Combine(baseDir, langDir))) return Path.Combine(baseDir, langDir);
            if (Directory.Exists(baseDir)) return baseDir;
            return null;
        }

        private static int CountFamilies()
        {
            try
            {
                return FSO.Content.Content.Get().Neighborhood.MainResource.List<FSO.Files.Formats.IFF.Chunks.FAMI>().Count;
            }
            catch { return 0; }
        }

        private static int CountFloors(VM vm)
        {
            try
            {
                var bp = vm.Context.Blueprint;
                int floors = 1;
                for (var l = 1; l < bp.Stories && l < bp.Walls.Length; l++)
                {
                    var level = bp.Walls[l];
                    if (level != null && level.Length > 0) floors = l + 1; // any wall array allocated on this level
                }
                return floors;
            }
            catch { return 1; }
        }

        private static string ReadText(string dir, string file)
        {
            var p = Path.Combine(dir, file);
            return File.Exists(p) ? File.ReadAllText(p, TemplateEncoding) : null;
        }

        private static void WritePage(string path, string template, IReadOnlyDictionary<string, string> tokens)
        {
            if (template == null) return;
            foreach (var kv in tokens)
                template = template.Replace("^^^^" + kv.Key + "^^^^", kv.Value);
            // residual-token census (probe wants zero; per-member loops leave none)
            if (template.Contains("^^^^sims_")) UnresolvedTokens++;
            File.WriteAllText(path, template, TemplateEncoding);
        }

        /// <summary>AUD-18: the directory law decoded from the corpus —
        /// main.html's ShowAll() iframes familyN.html (N=1..numfamilies) from
        /// the site root, each instantiated from familyX.html with the
        /// per-family tokens. Nothing is spliced into addressbook.html (it is
        /// a bare frameset).</summary>
        private static void GenerateFamilyPages(string templates, string root, IReadOnlyDictionary<string, string> baseTokens)
        {
            var famis = FSO.Content.Content.Get().Neighborhood.MainResource.List<FSO.Files.Formats.IFF.Chunks.FAMI>();
            var tpl = ReadText(templates, "familyX.html");
            if (tpl == null || famis.Count == 0) return;
            int n = 0;
            foreach (var fam in famis)
            {
                n++;
                string name = null;
                try { name = fam.ChunkParent.Get<FSO.Files.Formats.IFF.Chunks.FAMs>(fam.ChunkID)?.GetString(0); } catch { }
                if (string.IsNullOrEmpty(name)) name = "Family";
                var id = fam.FamilyNumber != 0 ? fam.FamilyNumber : fam.ChunkID;
                var famTokens = new Dictionary<string, string>(baseTokens)
                {
                    { "sims_neighborhood_familyX_name", name },
                    { "sims_neighborhood_familyX_name_nospaces", new string(name.Where(char.IsLetterOrDigit).ToArray()) },
                    // AUD-18 (decode): familyX_id/targetURL use the HOUSE
                    // NUMBER (Family+0x110) — the folder is <Name>_<lot>, so
                    // targetURL must be too for the directory links to work.
                    { "sims_neighborhood_familyX_id", ((int)fam.HouseNumber).ToString() },
                    { "sims_neighborhood_familyX_lot", ((int)fam.HouseNumber).ToString() },
                    { "sims_neighborhood_familyX_nummembers", (fam.FamilyGUIDs?.Length ?? 0).ToString() },
                    { "sims_neighborhood_familyX_targetURL", new string(name.Where(char.IsLetterOrDigit).ToArray()) + "_" + (int)fam.HouseNumber },
                };
                var page = tpl;
                foreach (var kv in baseTokens) page = page.Replace("^^^^" + kv.Key + "^^^^", kv.Value);
                foreach (var kv in famTokens) page = page.Replace("^^^^" + kv.Key + "^^^^", kv.Value);
                File.WriteAllText(Path.Combine(root, "family" + n + ".html"), page, TemplateEncoding);
            }
        }

        private static void WriteIndexRedir(string root, IReadOnlyDictionary<string, string> tokens)
        {
            File.WriteAllText(Path.Combine(root, "index.html"),
                "<html><head><meta charset=\"" + tokens["sims_web_character_set"] + "\">" +
                "<meta http-equiv=\"refresh\" content=\"0;url=main.html\"></head><body></body></html>",
                TemplateEncoding);
        }

        private static void CopyTree(string from, string to, bool recursive = true)
        {
            foreach (var file in Directory.GetFiles(from))
            {
                if (Path.GetExtension(file).Equals(".html", StringComparison.OrdinalIgnoreCase)) continue;
                var dest = Path.Combine(to, Path.GetFileName(file));
                Directory.CreateDirectory(to);
                File.Copy(file, dest, true);
            }
            if (!recursive) return;
            foreach (var dir in Directory.GetDirectories(from))
            {
                var name = Path.GetFileName(dir);
                if (name.Equals("LotTemplates", StringComparison.OrdinalIgnoreCase)) continue;
                CopyTree(dir, Path.Combine(to, name));
            }
        }

        private static void SaveJpeg(string path, Texture2D tex, int quality)
        {
            using (var img = LoadTexture(tex))
            using (var fs = File.Create(path))
                img.SaveAsJpeg(fs, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = quality });
        }

        private static Image<Rgba32> LoadTexture(Texture2D tex)
        {
            var data = new Color[tex.Width * tex.Height];
            tex.GetData(data);
            var pixels = new Rgba32[data.Length];
            for (int i = 0; i < data.Length; i++) pixels[i] = new Rgba32(data[i].R, data[i].G, data[i].B, 255);
            return Image.LoadPixelData<Rgba32>(pixels, tex.Width, tex.Height);
        }
    }
}
