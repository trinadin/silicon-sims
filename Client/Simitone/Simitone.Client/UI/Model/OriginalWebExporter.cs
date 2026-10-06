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
    ///     main.html (frameset), neighborhood.html, addressbook.html,
    ///     navigation/blank/loading/publish.html, familyN.html per family,
    ///     NeighborhoodGFX + root assets
    ///     <FamilyName>_<id>/            (folder name = familyX link law)
    ///       familyhome.html, familymemberK.html, house.html,
    ///       scrapbookK.html + scrapbook_popupK.html, lot-template assets
    ///       FamilyGFX/familyK_face.jpg (45x45 portrait), familyK-face.jpg
    ///         (the directory's hyphen variant), familyK_full.jpg (100x220
    ///         vita-rect render), house-exterior.jpg (640x480 backbuffer
    ///         capture), house-thumb.jpg (99x60), scrapbookK.jpg
    ///
    /// Undecoded-and-disclosed choices: the addressbook family-list splice
    /// point, the familyX output naming (familyN.html), the house capture's
    /// view (the current lot view at save, 640x480), member full-body pose
    /// (fresh scratch-avatar bind pose). All materials are original (templates,
    /// .ffn-rendered data, engine renders); no proprietary bytes enter the repo.
    /// </summary>
    public static class OriginalWebExporter
    {
        public static int Exports;          // completed exports (probe)
        public static int ExportFailures;   // caught failures (probe)
        public static int UnresolvedTokens; // any ^^^^ left in output (probe; want 0)

        private const string PendingHouseFile = "@housecapture"; // marker under the family dir

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

                // family folder: <NameNoSpaces>_<id> (the familyX link law)
                var id = family.FamilyNumber != 0 ? family.FamilyNumber : family.ChunkID;
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

                // ---- shared token values ----
                var charset = "iso-8859-1";
                var langCode = "en";
                if (STR.DefaultLangCode == STRLangCode.French) langCode = "fr";
                var address = lot + " Sim Lane";

                var baseTokens = new Dictionary<string, string>
                {
                    { "sims_web_character_set", charset },
                    { "sims_web_language_code", langCode },
                    { "sims_game_edition", "The Sims Complete Collection" },
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
                    { "sims_house_squarefeet", stats.SquareFeet.ToString() },
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
                var pages = OriginalSnapshotAlbum.Pages;
                baseTokens["sims_scrapbook_numpages"] = pages.Count.ToString();
                for (int i = 0; i < 100; i++)
                    baseTokens["sims_scrapbook" + i + "_caption"] = (i < pages.Count) ? (pages[i].Description ?? "") : "";

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
                    var tok = new Dictionary<string, string>(baseTokens)
                    {
                        { "sims_scrapbookX_number", i.ToString() },
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
                        SaveJpeg(Path.Combine(famRoot, "FamilyGFX", "scrapbook" + (i + 1) + ".jpg"), pages[i].Photo, 90);
                }

                // ---- root pages ----
                foreach (var name in new[] { "main", "neighborhood", "navigation", "blank", "loading", "publish", "addressbook" })
                {
                    var tpl = ReadText(templates, name + ".html");
                    if (tpl == null) continue;
                    if (name == "addressbook") tpl = SpliceFamilyList(tpl, templates, root, baseTokens);
                    WritePage(Path.Combine(root, name + ".html"), tpl, baseTokens);
                }
                WriteIndexRedir(root, baseTokens); // index.html → main.html (convenience; the original's host served main)

                // ---- house exterior: arm the next-frame backbuffer capture ----
                HouseCaptureRequest = famRoot;
                HouseCapturePath = Path.Combine(famRoot, "FamilyGFX", "house-exterior.jpg");

                Exports++;
            }
            catch (Exception e)
            {
                ExportFailures++;
                Simitone.Client.GameLog.Write("webexport: EXC " + e.GetType().Name + " " + e.Message + " | " + e.StackTrace.Replace("\n", " "));
            }
        }

        // ------------------------------------------------------------------
        // House capture delivery (called by OriginalWebExportScene next frame)
        // ------------------------------------------------------------------
        internal static string HouseCaptureRequest;   // non-null = capture wanted
        internal static string HouseCapturePath;

        internal static void OnHouseCaptured(Texture2D captured)
        {
            var path = HouseCapturePath;
            HouseCaptureRequest = null;
            HouseCapturePath = null;
            if (captured == null || path == null) return;
            try
            {
                SaveJpeg(path, captured, 90);
                // house-thumb: the directory's declared 99x60
                using (var img = LoadTexture(captured))
                {
                    img.Mutate(x => x.Resize(99, 60));
                    using (var fs = File.Create(Path.Combine(Path.GetDirectoryName(path), "house-thumb.jpg")))
                        img.SaveAsJpeg(fs, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = 90 });
                }
            }
            catch (Exception e)
            {
                ExportFailures++;
                Simitone.Client.GameLog.Write("webexport: house capture EXC " + e.GetType().Name + " " + e.Message);
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

        private static readonly Dictionary<string, string> WebWordsEn = new Dictionary<string, string>
        {
            { "male", "Male" }, { "female", "Female" }, { "adult", "Adult" }, { "child", "Child" },
            { "human", "Human" }, { "dog", "Dog" }, { "cat", "Cat" }, { "unemployed", "Unemployed" },
        };
        private static readonly Dictionary<string, string> WebWordsFr = new Dictionary<string, string>
        {
            { "male", "Homme" }, { "female", "Femme" }, { "adult", "Adulte" }, { "child", "Enfant" },
            { "human", "Humain" }, { "dog", "Chien" }, { "cat", "Chat" }, { "unemployed", "Sans emploi" },
        };

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
            var words = (STR.DefaultLangCode == STRLangCode.French) ? WebWordsFr : WebWordsEn;
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
            m.Tokens["sims_familymemberX_gender"] = words[female ? "female" : "male"];
            m.Tokens["sims_familymemberX_kidadult"] = words[child ? "child" : "adult"];
            m.Tokens["sims_familymemberX_species"] = words[dog ? "dog" : cat ? "cat" : "human"];

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
                    m.Tokens["sims_familymemberX_job"] = jl.JobName ?? "";
                    m.Tokens["sims_familymemberX_salary"] = jl.Salary.ToString();
                }
                else
                {
                    m.Tokens["sims_familymemberX_career"] = "";
                    m.Tokens["sims_familymemberX_job"] = words["unemployed"];
                    m.Tokens["sims_familymemberX_salary"] = "0";
                }
            }
            catch
            {
                m.Tokens["sims_familymemberX_career"] = "";
                m.Tokens["sims_familymemberX_job"] = words["unemployed"];
                m.Tokens["sims_familymemberX_salary"] = "0";
            }
            m.Tokens["sims_familymemberX_performance"] =
                ava.GetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.JobPerformance).ToString();
        }

        private static string Skill(VMAvatar ava, FSO.SimAntics.Model.VMPersonDataVariable v)
        {
            return Math.Max(0, ava.GetPersonData(v) / 100).ToString();   // 0..1000 -> 0..10
        }

        private static string Pers(VMAvatar ava, FSO.SimAntics.Model.VMPersonDataVariable v)
        {
            return Math.Max(0, ava.GetPersonData(v) / 10).ToString();    // 0..1000 -> 0..100
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
            return File.Exists(p) ? File.ReadAllText(p, Encoding.Default) : null;
        }

        private static void WritePage(string path, string template, IReadOnlyDictionary<string, string> tokens)
        {
            if (template == null) return;
            foreach (var kv in tokens)
                template = template.Replace("^^^^" + kv.Key + "^^^^", kv.Value);
            // residual-token census (probe wants zero; per-member loops leave none)
            if (template.Contains("^^^^sims_")) UnresolvedTokens++;
            File.WriteAllText(path, template, Encoding.Default);
        }

        private static string SpliceFamilyList(string addressbook, string templates, string root, IReadOnlyDictionary<string, string> baseTokens)
        {
            // ENG-27 disclosed: the addressbook family-list splice law is
            // undecoded — familyX blocks are appended before </body> as
            // familyN.html links (the familyX page carries its own JS).
            var famis = FSO.Content.Content.Get().Neighborhood.MainResource.List<FSO.Files.Formats.IFF.Chunks.FAMI>();
            var tpl = ReadText(templates, "familyX.html");
            if (tpl == null || famis.Count == 0) return addressbook;
            var links = new StringBuilder();
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
                    { "sims_neighborhood_familyX_id", id.ToString() },
                    { "sims_neighborhood_familyX_lot", ((int)fam.HouseNumber).ToString() },
                    { "sims_neighborhood_familyX_nummembers", (fam.FamilyGUIDs?.Length ?? 0).ToString() },
                    { "sims_neighborhood_familyX_targetURL", new string(name.Where(char.IsLetterOrDigit).ToArray()) + "_" + id },
                };
                var page = tpl;
                foreach (var kv in baseTokens) page = page.Replace("^^^^" + kv.Key + "^^^^", kv.Value);
                foreach (var kv in famTokens) page = page.Replace("^^^^" + kv.Key + "^^^^", kv.Value);
                File.WriteAllText(Path.Combine(root, "family" + n + ".html"), page, Encoding.Default);
                links.AppendLine("<p><a href=\"family" + n + ".html\">" + name + "</a></p>");
            }
            var idx = addressbook.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
            return idx >= 0 ? addressbook.Insert(idx, links.ToString()) : addressbook + links.ToString();
        }

        private static void WriteIndexRedir(string root, IReadOnlyDictionary<string, string> tokens)
        {
            File.WriteAllText(Path.Combine(root, "index.html"),
                "<html><head><meta charset=\"" + tokens["sims_web_character_set"] + "\">" +
                "<meta http-equiv=\"refresh\" content=\"0;url=main.html\"></head><body></body></html>",
                Encoding.Default);
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
