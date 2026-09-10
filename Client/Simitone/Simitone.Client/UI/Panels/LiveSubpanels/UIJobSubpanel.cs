using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Simitone.Client.UI.Screens;
using Simitone.Client.UI.Model;
using Simitone.Client.UI.Controls;
using FSO.Client.UI.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FSO.Content;
using FSO.Files.Formats.IFF.Chunks;
using FSO.SimAntics;
using FSO.SimAntics.Model;
using FSO.SimAntics.Entities;
using FSO.Client.UI.Framework;
using FSO.Common.Rendering.Framework.IO;
using FSO.Common.Rendering.Framework.Model;
using FSO.Client;

namespace Simitone.Client.UI.Panels.LiveSubpanels
{
    public enum UIDesktopJobMode
    {
        Job,
        ReportCard,
        DogSkills,
        CatSkills,
        Fame
    }

    public enum UIOriginalCaptionAlignment
    {
        Left,
        Center,
        Right
    }

    /// <summary>
    /// The original Job-family labels are cTSWinBtn captions: their rectangles
    /// determine alignment and hit testing, but TSDrawText receives no clip
    /// rectangle. This control intentionally keeps that single-line/no-clip
    /// behavior instead of UILabel wrapping or ellipsis.
    /// </summary>
    public class UIOriginalCaptionControl : UIOriginalText
    {
        public static readonly Color NativeCaptionColor = new Color(0xC3, 0xCD, 0xCD, 0xFF);
        public UIOriginalCaptionAlignment Alignment;
        public float TextOffsetX;
        public bool Underline;
        public bool SelectedState;
        public bool HoverState;
        public bool DownState;
        public bool DisabledState;
        public Action Activated;
        private readonly UIMouseEventRef Mouse;

        public static readonly Color SelectedCaptionColor = new Color(0x00, 0xFF, 0xFF, 0xFF);
        public static readonly Color HoverCaptionColor = Color.White;
        public static readonly Color DisabledCaptionColor = new Color(0x40, 0x5D, 0x5F, 0xFF);

        public override Vector2 Size
        {
            get { return base.Size; }
            set
            {
                base.Size = value;
                if (Mouse != null)
                    Mouse.Region = new Rectangle(0, 0, Math.Max(1, (int)value.X), Math.Max(1, (int)value.Y));
            }
        }

        public UIOriginalCaptionControl(string text, OriginalGlyphFont font,
            UIOriginalCaptionAlignment alignment = UIOriginalCaptionAlignment.Left,
            Action activated = null) : base(text, font)
        {
            Alignment = alignment;
            Activated = activated;
            if (activated != null)
                Mouse = ListenForMouse(new Rectangle(0, 0, 1, Math.Max(1, font?.LineHeight ?? 1)), MouseEvent);
            FSO.Client.Utils.UIUtils.GiveTooltip(this);
        }

        private void MouseEvent(UIMouseEventType type, UpdateState state)
        {
            if (DisabledState) return;
            if (type == UIMouseEventType.MouseOver) HoverState = true;
            else if (type == UIMouseEventType.MouseDown) DownState = true;
            else if (type == UIMouseEventType.MouseOut)
            {
                HoverState = false;
                DownState = false;
            }
            else if (type == UIMouseEventType.MouseUp)
            {
                if (DownState) Activated?.Invoke();
                DownState = false;
            }
            Invalidate();
        }

        protected float LineX(string text)
        {
            var width = Font?.Measure(text ?? "") ?? 0;
            if (Alignment == UIOriginalCaptionAlignment.Right) return Size.X - width;
            if (Alignment == UIOriginalCaptionAlignment.Center)
                return TextOffsetX + (Size.X - TextOffsetX - width) / 2f;
            return TextOffsetX;
        }

        protected void DrawString(UISpriteBatch batch, string text, float x, float y)
        {
            if (Font == null || Font.Atlas == null || string.IsNullOrEmpty(text)) return;
            var start = x;
            var stateColor = DisabledState ? DisabledCaptionColor
                : (SelectedState || DownState) ? SelectedCaptionColor
                : HoverState ? HoverCaptionColor : Color;
            var color = stateColor * Opacity;
            foreach (var ch0 in text)
            {
                var ch = OriginalGlyphFont.MapChar(ch0);
                OriginalGlyphFont.Glyph glyph;
                if (Font.ByChar.TryGetValue(ch, out glyph) && glyph.W > 1)
                {
                    DrawLocalTexture(batch, Font.Atlas,
                        new Rectangle(glyph.U, glyph.V, glyph.W, glyph.H),
                        new Vector2(x, y + Font.GlyphY(glyph)), Vector2.One, color);
                }
                x += Font.Advance(ch);
            }
            if (Underline && x > start)
                DrawLocalTexture(batch, FSO.Common.Utils.TextureGenerator.GetPxWhite(GameFacade.GraphicsDevice),
                    null, new Vector2(start, y + Font.LineHeight - 1), new Vector2(x - start, 1), color);
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible || Font == null || Font.Atlas == null || string.IsNullOrEmpty(Text)) return;
            DrawString(batch, Text, LineX(Text), 0);
            UIOriginalText.StringsDrawn++;
        }
    }

    /// <summary>
    /// The four native Job summary buttons own large, partly overlapping hit
    /// rectangles while their icon and caption are painted by separate port
    /// controls. This no-paint control restores those exact cTSWinBtn regions
    /// and forwards its state so the existing art can use the native four
    /// button frames without moving any proven pixels.
    /// </summary>
    public class UIJobPopupHitControl : UIElement
    {
        public Action Activated;
        public Action StateChanged;
        public bool SelectedState;
        public bool HoverState;
        public bool DownState;
        private Vector2 _Size;
        private readonly UIMouseEventRef Mouse;

        public override Vector2 Size
        {
            get { return _Size; }
            set
            {
                _Size = value;
                if (Mouse != null)
                    Mouse.Region = new Rectangle(0, 0, Math.Max(1, (int)value.X), Math.Max(1, (int)value.Y));
            }
        }

        public int NativeFrame
        {
            get { return (SelectedState || DownState) ? 1 : HoverState ? 2 : 0; }
        }

        public UIJobPopupHitControl(Rectangle rectangle, Action activated, Action stateChanged)
        {
            Position = new Vector2(rectangle.X, rectangle.Y);
            Size = new Vector2(rectangle.Width, rectangle.Height);
            Activated = activated;
            StateChanged = stateChanged;
            Mouse = ListenForMouse(new Rectangle(0, 0, rectangle.Width, rectangle.Height), MouseEvent);
        }

        private void MouseEvent(UIMouseEventType type, UpdateState state)
        {
            if (type == UIMouseEventType.MouseOver) HoverState = true;
            else if (type == UIMouseEventType.MouseDown) DownState = true;
            else if (type == UIMouseEventType.MouseOut)
            {
                HoverState = false;
                DownState = false;
            }
            else if (type == UIMouseEventType.MouseUp)
            {
                if (DownState) Activated?.Invoke();
                DownState = false;
            }
            StateChanged?.Invoke();
            Invalidate();
        }

        public override void Draw(UISpriteBatch batch) { }
    }

    /// <summary>
    /// cPerformanceBtn::CaptionDraw is the one Job caption exception. A single
    /// line uses the normal left-aligned caption path. For two lines the native
    /// painter places them one font height apart and right-aligns the shorter
    /// first line to the second; if line one is wider it falls back to the
    /// ordinary unwrapped caption.
    /// </summary>
    public class UIJobCareerCaption : UIOriginalCaptionControl
    {
        public UIJobCareerCaption(string text, OriginalGlyphFont font)
            : base(text, font, UIOriginalCaptionAlignment.Left) { }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible || Font == null || Font.Atlas == null || string.IsNullOrEmpty(Text)) return;
            if (Font.Measure(Text) <= Size.X)
            {
                DrawString(batch, Text, 0, 0);
                UIOriginalText.StringsDrawn++;
                return;
            }

            var split = -1;
            for (int i = 0; i < Text.Length; i++)
                if (char.IsWhiteSpace(Text[i]) && Font.Measure(Text.Substring(0, i)) <= Size.X)
                    split = i;
            if (split <= 0)
            {
                DrawString(batch, Text, 0, 0);
                UIOriginalText.StringsDrawn++;
                return;
            }

            var first = Text.Substring(0, split).TrimEnd();
            var second = Text.Substring(split).TrimStart();
            var firstWidth = Font.Measure(first);
            var secondWidth = Font.Measure(second);
            if (string.IsNullOrEmpty(second) || secondWidth > Size.X || firstWidth > secondWidth)
            {
                DrawString(batch, Text, 0, 0);
                UIOriginalText.StringsDrawn++;
                return;
            }
            DrawString(batch, first, secondWidth - firstWidth, 0);
            DrawString(batch, second, 0, Font.LineHeight);
            UIOriginalText.StringsDrawn++;
        }
    }

    public class UIJobSkillBar : UIOriginalRatingBar
    {
        public int RequiredPoints;
        public const int NativeWidth = 40;
        public const int NativeHeight = 11;
        public static readonly Color NativeGuideColor = new Color(0xC3, 0xCD, 0xCD, 0xFF);
        private Vector2 _size = new Vector2(NativeWidth, NativeHeight);

        public override Vector2 Size
        {
            get { return _size; }
            set { _size = value; }
        }

        public UIJobSkillBar() : base(
            "cpanel\\Backgrounds\\JobSubBarsOff.bmp", "cpanel\\JobSubBars.bmp",
            "rating_job_off.png", "rating_job_on.png") { }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            base.Draw(batch);
            var required = Math.Max(0, Math.Min(10, RequiredPoints));
            if (required == 0) return;

            // Both Job and Fame TSPaint draw the same promotion guide after
            // the skill fills: a vertical line at 4*required-1, then a
            // horizontal line from 16px left of the bar to that point.
            var px = FSO.Common.Utils.TextureGenerator.GetPxWhite(GameFacade.GraphicsDevice);
            var markerX = 4 * required - 1;
            DrawLocalTexture(batch, px, null, new Vector2(markerX, 0),
                new Vector2(1, NativeHeight), NativeGuideColor * Opacity);
            DrawLocalTexture(batch, px, null, new Vector2(-16, NativeHeight - 1),
                new Vector2(markerX + 17, 1), NativeGuideColor * Opacity);
        }
    }

    public class UIFameRatingBar : UIElement
    {
        public Texture2D BackdropTex;
        public Texture2D BarsTex;
        public int Level;
        public int FillWidth { get { return 8 * Math.Max(0, Math.Min(10, Level)); } }
        private Vector2 _size = new Vector2(80, 16);

        public override Vector2 Size
        {
            get { return _size; }
            set { _size = value; }
        }

        public UIFameRatingBar()
        {
            BackdropTex = UIOriginal.EnsureResolved("cpanel\\Backgrounds\\FameSubBarsOff.bmp")?.Get(GameFacade.GraphicsDevice);
            BarsTex = UIOriginal.EnsureResolved("cpanel\\FameSubBars.bmp")?.Get(GameFacade.GraphicsDevice);
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            if (BackdropTex != null) DrawLocalTexture(batch, BackdropTex, null, Vector2.Zero, Vector2.One);
            var width = FillWidth;
            if (BarsTex != null && width > 0)
                DrawLocalTexture(batch, BarsTex, new Rectangle(0, 0, width, BarsTex.Height),
                    Vector2.Zero, Vector2.One);
        }
    }

    /// <summary>
    /// The report-card control is a text-only cFlashyBtn in the original. Its
    /// control rectangle spans the complete report-card child; the grade is
    /// centered inside that rectangle with font[18]. Keeping the hit rectangle
    /// on the same element prevents the port's former label/popup divergence.
    /// </summary>
    public class UIReportGradeControl : UIElement
    {
        public string Text = "";
        public OriginalGlyphFont Font;
        public Color Color = new Color(0xFF, 0xFF, 0xF0, 0xFF);
        public Action Activated;
        private Vector2 _size;
        private readonly UIMouseEventRef Mouse;
        private bool Down;

        public override Vector2 Size
        {
            get { return _size; }
            set
            {
                _size = value;
                if (Mouse != null)
                    Mouse.Region = new Rectangle(0, 0, Math.Max(1, (int)value.X), Math.Max(1, (int)value.Y));
            }
        }

        public int TextX
        {
            get { return Math.Max(0, ((int)Size.X - (Font?.Measure(Text ?? "") ?? 0)) / 2); }
        }

        public UIReportGradeControl(OriginalGlyphFont font, Action activated)
        {
            Font = font;
            Activated = activated;
            Mouse = ListenForMouse(new Rectangle(0, 0, 1, Math.Max(1, font?.LineHeight ?? 1)), MouseEvent);
            FSO.Client.Utils.UIUtils.GiveTooltip(this);
        }

        private void MouseEvent(UIMouseEventType type, UpdateState state)
        {
            if (type == UIMouseEventType.MouseDown) Down = true;
            else if (type == UIMouseEventType.MouseOut) Down = false;
            else if (type == UIMouseEventType.MouseUp)
            {
                if (Down) Activated?.Invoke();
                Down = false;
            }
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible || Font == null || Font.Atlas == null || string.IsNullOrEmpty(Text)) return;
            float x = TextX;
            var color = Color * Opacity;
            foreach (var ch0 in Text)
            {
                var ch = OriginalGlyphFont.MapChar(ch0);
                OriginalGlyphFont.Glyph glyph;
                if (Font.ByChar.TryGetValue(ch, out glyph) && glyph.W > 1)
                {
                    DrawLocalTexture(batch, Font.Atlas,
                        new Rectangle(glyph.U, glyph.V, glyph.W, glyph.H),
                        new Vector2(x, Font.GlyphY(glyph)), Vector2.One, color);
                }
                x += Font.Advance(ch);
            }
            UIOriginalText.StringsDrawn++;
        }
    }

    public class UIJobSubpanel : UISubpanel
    {
        public UILabel PerformanceTitle;
        public UIOriginalRatingBar PerformanceBar;
        public UILabel JobTitle;
        public UILabel SalaryTitle;
        public UIButton CareerButton;
        public UIOriginalText HostTitle;
        public UIOriginalText FriendsTwin;
        public UIImage SalaryPlaque;
        public UIImage PerformancePlaque;
        public UIImage FriendsPlaque;

        // cWinSubpanelJob's single-byte layout is recovered verbatim from
        // __sinit_:WinSubpanelJob_cpp + Init: six 62x16 skill-name buttons at
        // (93,6+15*i), six pip origins at (157,7+15*i), and the four job-summary
        // rows at x=3 / y={4,45,65,85}. The original window is 427x100 at
        // 1024-wide resolution; all actual content remains in its first 200px.
        public const int DESKTOP_JOB_W = 427;
        public const int SKILL_LABEL_X = 93;
        public const int SKILL_PIP_X = 157;
        public const int SKILL_LABEL_Y0 = 6;
        public const int SKILL_PIP_Y0 = 7;
        public const int SKILL_PITCH_Y = 15;
        public const int SKILL_LABEL_W = 62;
        public const int SKILL_LABEL_H = 16;
        public const int HOST_TITLE_X = 35;
        public const int HOST_TITLE_Y = 3;
        public const int JOB_CAPTION_X = 3;
        public const int JOB_CAPTION_Y = 26;
        public const int JOB_CAPTION_W = 95;
        public const int JOB_CAPTION_H = 23;
        public const int CAREER_ICON_CELL_W = 28;
        public const int CAREER_ICON_CELL_H = 22;
        public const int CAREER_ICON_ROWS = 22;
        public static readonly Rectangle[] ADULT_SUMMARY_POPUP_RECTS =
        {
            new Rectangle(3, 4, 95, 45),
            new Rectangle(3, 45, 95, 15),
            new Rectangle(3, 65, 95, 15),
            new Rectangle(3, 85, 95, 15),
        };
        public static readonly uint[][] ADULT_SKILL_POPUP_OBJECT_GUIDS =
        {
            new uint[] { 0x5CDC712Fu, 0u },
            new uint[] { 0x5CDC712Fu, 0u },
            new uint[] { 0xC042ED5Bu, 0u },
            new uint[] { 0xDDEDD497u, 0u },
            new uint[] { 0x94AA32F4u, 0u },
            new uint[] { 0x9FB223CEu, 0x112039A0u },
        };
        public const int JOB_POPUP_CELL_W = 84;
        public const int JOB_POPUP_CELL_H = 63;
        public const int JOB_POPUP_ROWS = 22;
        private int LastCareerIconRow = -1;
        private readonly Texture2D[][] SummaryPlaqueStates = new Texture2D[4][];

        // R180: cWinSubpanelReportCard::Init constructs exactly one full-width
        // cFlashyBtn, centered vertically at its font[18] character height.
        // Its text is cJob::GetGrade(PersonFinder::GetJobPerformance(child)):
        // the child accessor reads person word 57 (JobPromotionLevel), then
        // cJob indexes work.iff STR#4097 "Grade Strings". Clicking the grade
        // opens the shared live popup with Live.iff STR#139 [1]/[2] and row 11
        // of JobIconMultiPopup. It is not the old three-block port layout.
        public static bool IsChild(int personsAge) { return personsAge > 0 && personsAge < 18; }
        public bool ReportCardMode;
        public static int ReportCardShown;
        public UIOriginalText ReportTitle;
        public UIOriginalText ReportGrades;
        public UIOriginalText ReportDesc;
        public UIReportGradeControl ReportGradeControl;
        public UIOptionAboutPopup ReportPopup;

        // R132: the ORIGINAL job rating bar — STR# 154 'MiscStrings' [14]
        // 'Job Rating' + the engine's kJobSubpanelBars 4901 sheet over the
        // kJobSubBarsOff 4920 backdrop (cWinSubpanelJob::Init 0x2a3fb0-0x2a3ff0,
        // English branch; 40x11 art, fill = 4*(value/10) px — see
        // UIOriginalRatingBar). Value: the JobPerformance person data
        // (short, the port's existing performance source) clamped to the
        // engine's [0,100] bar range.
        public UILabel RatingTitle;
        public UIOriginalRatingBar RatingBar;

        // ROUND-115: job-subpanel strings from the ORIGINAL Live.iff tables — the port had
        // all of these HARDCODED. 136 'JobSubpanelLabels': [1]-[6] the six skill names,
        // [7] 'Unemployed', [8] 'n/a' (the salary line when unemployed - the port cleared
        // it); 137 'JobSubpanelPopupText': [14] 'Salary', [16] 'Performance'. The
        // "Salary: §n (h-h)" line stays port-composed (137[14] provides the word, the
        // hours format is the popup string 137[23], not an inline format). Fallbacks keep
        // the old literals for a load failure. Twins sync every Update because
        // JobTitle/SalaryTitle mutate on job change/unemployment.
        // Desktop font selection is engine-decoded: the six skill buttons use
        // font[7], while the four summary rows use {7,8,7,7} for career,
        // salary, performance and friends respectively. Mobile keeps its
        // existing caption-font presentation.
        public static int JobStringsTwinned = 0;
        public UIOriginalText PerformanceTwin;
        public UIOriginalText JobTwin;
        public UIOriginalText SalaryTwin;
        public UIOriginalText[] SkillTwins;
        private UILabel[] SkillLabels;
        public UIJobSkillBar[] SkillBars;

        // The original People-window router owns five mutually exclusive
        // surfaces behind the Job tab. They share the same 427x100 child area.
        public UIDesktopJobMode DesktopMode = UIDesktopJobMode.Job;
        public UIOriginalCaptionControl PetTitle;
        public UIOriginalCaptionControl[] PetSkillLabels;
        public UIJobSkillBar[] PetSkillBars;
        public UIOriginalCaptionControl FameTitle;
        public UIOriginalCaptionControl[] FameSkillLabels;
        public UIJobSkillBar[] FameSkillBars;
        public UIFameRatingBar FameBar;
        public UIOriginalCaptionControl FameLevelLabel;
        public UIOriginalCaptionControl FameFriendsLabel;
        public UIOriginalCaptionControl FameScoreLabel;
        public UIImage FameFriendPlaque;
        public UIOptionAboutPopup JobFamilyPopup;
        public string JobFamilyPopupKey;
        public UIJobPopupHitControl[] SummaryPopupControls;
        public int JobFamilyPopupSummaryIndex = -1;
        public int JobFamilyPopupSkillIndex = -1;
        public int JobFamilyPopupArtRow = -1;
        public readonly uint[] JobFamilyPopupObjectGuids = new uint[2];
        private readonly Dictionary<uint, Texture2D> JobPopupObjectThumbs =
            new Dictionary<uint, Texture2D>();

        private static readonly int[] FameFriendsNeeded =
            { 0, 0, 0, 0, 2, 4, 7, 11, 14, 18, 0 };
        private static readonly int[][] FameSkillsNeeded =
        {
            new [] { 0, 0, 0, 0, 0, 0 },
            new [] { 0, 0, 1, 0, 0, 1 },
            new [] { 0, 0, 2, 1, 0, 2 },
            new [] { 0, 0, 3, 2, 0, 3 },
            new [] { 0, 0, 4, 3, 0, 4 },
            new [] { 0, 0, 6, 4, 0, 4 },
            new [] { 0, 0, 6, 5, 0, 6 },
            new [] { 0, 0, 7, 6, 0, 7 },
            new [] { 0, 0, 8, 7, 0, 8 },
            new [] { 0, 0, 10, 8, 0, 9 },
            new [] { 0, 0, 0, 0, 0, 0 },
        };

        private static readonly string[] FameFallback =
        {
            "Fame", "Nobody", "Stepping Stone", "Insider", "Name Dropper",
            "Studio Fly", "Sell Out", "Trendsetter", "Player", "Talk Of The Town",
            "Celebrity", "Superstar", "%s (Need %d)",
        };
        private static readonly string[] PetSkillFallback =
            { "Pet Skills", "House Breaking", "Tricks", "Obedience", "Hunting" };
        private static readonly string[] PetPopupFallback =
        {
            "House Breaking Skill",
            "Training a pet to do its business outside can be quite a challenge. However, if you want to avoid the messy cleanups in the house, it's a must. Be sure to praise your pet when it goes where you want it to and, if necessary, scold them for going in the wrong place. ",
            "Tricks Skill",
            "A well trained pet can be as entertaining as a circus in your own backyard. The trick skill determines the types of tricks your pet can do, and how well they perform them.",
            "Obedience Skill",
            "Teaching your pet to Sit & Stay is the first step toward better obedience. Improving obedience will make your pet more responsive to commands and more receptive to new tricks.",
            "Hunting Skill",
            "While dogs have excellent hunting skills, around the house they don't find much use for them. Cats, on the other hand, corral all manner of household pests into submission. From mice and roaches, to bunnies and gophers, there's no pest a well-trained cat cannot thwart.",
        };
        private static readonly Dictionary<string, string[]> JobFamilyTables = new Dictionary<string, string[]>();

        private JobLevel LastJobLevel;

        private UISkillDisplay[] Skills;
        private string[] SkillNames = new string[]
        {
            "Cooking",
            "Mechanical",
            "Charisma",
            "Body",
            "Logic",
            "Creativity"
        };

        private VMPersonDataVariable[] SkillInd = new VMPersonDataVariable[]
        {
            VMPersonDataVariable.CookingSkill,
            VMPersonDataVariable.MechanicalSkill,
            VMPersonDataVariable.CharismaSkill,
            VMPersonDataVariable.BodySkill,
            VMPersonDataVariable.LogicSkill,
            VMPersonDataVariable.CreativitySkill
        };

        private void InitDesktop()
        {
            var gd = FSO.Client.GameFacade.GraphicsDevice;

            // State-holder labels remain present for the existing string/data
            // contracts; original .ffn twins below own the visible text.
            PerformanceTitle = new UILabel
            {
                Caption = OriginalLiveStrings.Entry(137, 16) ?? "Performance",
                Position = new Vector2(20, 65),
                Visible = false
            };
            InitLabel(PerformanceTitle);

            PerformanceBar = UIOriginalRatingBar.JobBar();
            PerformanceBar.Position = new Vector2(20, 66);
            PerformanceBar.Size = new Vector2(40, 11);
            Add(PerformanceBar);

            JobTitle = new UILabel
            {
                Caption = "Subway Musician",
                Position = new Vector2(JOB_CAPTION_X, JOB_CAPTION_Y)
            };
            InitLabel(JobTitle);

            SalaryTitle = new UILabel { Caption = "§90", Position = new Vector2(20, 45) };
            InitLabel(SalaryTitle);
            SalaryTitle.CaptionStyle.Color = UIStyle.Current.BtnActive;

            // JobIconMultiButton is a 4 x 22 sheet (112x484): 28x22 per
            // state/career row. Crop one career row while retaining all four
            // normal/pressed/hover/disabled columns.
            var career = UIOriginal.Rect("cpanel\\Buttons\\JobIconMultiButton.BMP",
                0, 0, CAREER_ICON_CELL_W * 4, CAREER_ICON_CELL_H);
            CareerButton = new UIButton(career ?? FSO.Common.Utils.TextureGenerator.GetPxWhite(gd))
            {
                ImageStates = career != null ? 4 : 1,
                Position = new Vector2(3, 4)
            };
            // The native cPerformanceBtn hit child is 95x45, not the 28x22
            // bitmap. SummaryPopupControls owns that complete rectangle.
            CareerButton.DeregisterHandler();
            Add(CareerButton);

            // These are 4-state 13px-wide plaques (52px-wide sheets), not
            // 52px single images. Drawing the whole sheets produced four tiny
            // repeated icons in each summary row.
            LoadSummaryPlaqueStates(1, "cpanel\\Buttons\\SalaryButton.bmp", 13, 11);
            var salaryPlaque = SummaryPlaqueStates[1]?[0];
            if (salaryPlaque != null)
            {
                SalaryPlaque = new UIImage(salaryPlaque) { Position = new Vector2(3, 45) };
                Add(SalaryPlaque);
            }
            LoadSummaryPlaqueStates(2, "cpanel\\Buttons\\PerformanceButton.bmp", 13, 12);
            var perfPlaque = SummaryPlaqueStates[2]?[0];
            if (perfPlaque != null)
            {
                PerformancePlaque = new UIImage(perfPlaque) { Position = new Vector2(3, 65) };
                Add(PerformancePlaque);
            }
            LoadSummaryPlaqueStates(3, "cpanel\\Buttons\\JobFriendSmiley.bmp", 10, 11);
            var friendsPlaque = SummaryPlaqueStates[3]?[0];
            if (friendsPlaque != null)
            {
                FriendsPlaque = new UIImage(friendsPlaque) { Position = new Vector2(3, 85) };
                Add(FriendsPlaque);
            }

            // SetFriendsNeeded targets summary child 3 and replaces its
            // caption with the required friend count. Keep the number beside
            // the fourth-row smiley instead of omitting that decoded row.
            var friendsFont = OriginalGlyphFont.LoadByIndex(7, gd);
            if (friendsFont != null)
            {
                FriendsTwin = new UIOriginalCaptionControl("", friendsFont)
                {
                    Position = new Vector2(20, 85),
                    Color = UIOriginalCaptionControl.NativeCaptionColor
                };
                Add(FriendsTwin);
            }

            // Kept for the R132 art/data contract, but the original subpanel
            // does not draw a second, separately-captioned Job Rating block.
            RatingTitle = new UILabel
            {
                Caption = GameFacade.Strings.GetString("154", "14"),
                Position = new Vector2(0, 0),
                Visible = false
            };
            InitLabel(RatingTitle);
            RatingBar = UIOriginalRatingBar.JobBar();
            RatingBar.Visible = false;
            Add(RatingBar);

            SkillBars = new UIJobSkillBar[6];
            SkillLabels = new UILabel[6];
            for (int i = 0; i < 6; i++)
            {
                SkillBars[i] = new UIJobSkillBar
                {
                    Position = new Vector2(SKILL_PIP_X, SKILL_PIP_Y0 + i * SKILL_PITCH_Y),
                    Size = new Vector2(UIJobSkillBar.NativeWidth, UIJobSkillBar.NativeHeight)
                };
                Add(SkillBars[i]);

                var name = new UILabel
                {
                    Caption = OriginalLiveStrings.Entry(136, i + 1) ?? SkillNames[i],
                    Position = new Vector2(SKILL_LABEL_X, SKILL_LABEL_Y0 + i * SKILL_PITCH_Y),
                    Visible = false
                };
                InitLabel(name);
                SkillLabels[i] = name;
            }

            // Live.iff STR#136[0] is copied into cWinSubpanelJob's internal
            // state string, but the owned executable never paints it. There is
            // deliberately no desktop HostTitle child here.
            HostTitle = null;
        }

        public UIJobSubpanel(TS1GameScreen game) : base(game)
        {
            if (game.Desktop)
            {
                InitDesktop();
                return;
            }

            PerformanceTitle = new UILabel();
            PerformanceTitle.Caption = OriginalLiveStrings.Entry(137, 16) ?? "Performance";
            PerformanceTitle.Position = new Vector2(79, 16);
            InitLabel(PerformanceTitle);

            // R159: the performance gauge is the ORIGINAL Job bar family
            // (kJobSubpanelBars 4901 sheet over kJobSubBarsOff 4920 — the
            // same cWinSubpanelJob::Init art the R132 rating bar uses),
            // with the kPerformanceBtnBMP 4982 (52x12) caption plaque.
            PerformanceBar = UIOriginalRatingBar.JobBar();
            PerformanceBar.Position = new Vector2(79, 41);
            Add(PerformanceBar);
            var perfPlaque = UIOriginal.EnsureResolved("cpanel\\Buttons\\PerformanceButton.bmp")?.Get(FSO.Client.GameFacade.GraphicsDevice);
            if (perfPlaque != null)
            {
                PerformancePlaque = new UIImage(perfPlaque) { Position = new Vector2(79 - perfPlaque.Width - 2, 16) };
                Add(PerformancePlaque);
            }
            var salaryPlaque = UIOriginal.EnsureResolved("cpanel\\Buttons\\SalaryButton.bmp")?.Get(FSO.Client.GameFacade.GraphicsDevice);
            if (salaryPlaque != null)
            {
                SalaryPlaque = new UIImage(salaryPlaque) { Position = new Vector2(18 - 2, 94 + (11 - salaryPlaque.Height) / 2) };
                Add(SalaryPlaque);
            }

            JobTitle = new UILabel();
            JobTitle.Caption = "Subway Musician";
            JobTitle.Position = new Vector2(18, 71);
            InitLabel(JobTitle);

            SalaryTitle = new UILabel();
            SalaryTitle.Caption = "Salary: §90";
            SalaryTitle.Position = new Vector2(18, 94);
            InitLabel(SalaryTitle);
            SalaryTitle.CaptionStyle.Color = UIStyle.Current.BtnActive;

            CareerButton = new UITwoStateButton(Content.Get().CustomUI.Get("blank_blue.png").Get(GameFacade.GraphicsDevice));
            CareerButton.Position = new Vector2(20, 15);
            Add(CareerButton);

            // R132: the original-styled Job Rating bar, right of the
            // performance block (performance bar spans x=79..229; skills
            // start at x=334 — this slot keeps clear of both).
            RatingTitle = new UILabel();
            RatingTitle.Caption = GameFacade.Strings.GetString("154", "14"); // 'Job Rating'
            RatingTitle.Position = new Vector2(240, 16);
            InitLabel(RatingTitle);

            RatingBar = UIOriginalRatingBar.JobBar();
            RatingBar.Position = new Vector2(240, 44);
            Add(RatingBar);

            Skills = new UISkillDisplay[6];
            SkillLabels = new UILabel[6];
            for (int i=0; i<6; i++)
            {
                Skills[i] = new UISkillDisplay();
                Skills[i].Position = new Vector2(334 + (i%3)*140, 35 + 60*(i/3));
                Add(Skills[i]);

                var name = new UILabel();
                name.Caption = OriginalLiveStrings.Entry(136, i + 1) ?? SkillNames[i];
                name.Position = new Vector2(332 + (i % 3) * 140, 11 + 60 * (i / 3));
                InitLabel(name);
                SkillLabels[i] = name;
            }
        }

        public int LastPerformance;

        public override void Update(UpdateState state)
        {
            base.Update(state);

            // mount FIRST and BEFORE any early-return (the R114 lesson: UpdateMotives'
            // no-avatar return used to skip the twin mount entirely).
            if (PerformanceTwin == null)
            {
                var gd = FSO.Client.GameFacade.GraphicsDevice;
                var captionFont = Simitone.Client.UI.Controls.OriginalGlyphFont.LoadCaption(gd);
                var performanceFont = Game.Desktop
                    ? Simitone.Client.UI.Controls.OriginalGlyphFont.LoadByIndex(7, gd)
                    : captionFont;
                var jobFont = Game.Desktop
                    ? Simitone.Client.UI.Controls.OriginalGlyphFont.LoadByIndex(7, gd)
                    : captionFont;
                var salaryFont = Game.Desktop
                    ? Simitone.Client.UI.Controls.OriginalGlyphFont.LoadByIndex(8, gd)
                    : captionFont;
                var skillFont = Game.Desktop
                    ? Simitone.Client.UI.Controls.OriginalGlyphFont.LoadByIndex(7, gd)
                    : captionFont;
                var s0 = OriginalLiveStrings.Entry(136, 1);
                if (performanceFont != null && performanceFont.Atlas != null
                    && jobFont != null && jobFont.Atlas != null
                    && salaryFont != null && salaryFont.Atlas != null
                    && skillFont != null && skillFont.Atlas != null && s0 != null)
                {
                    var twinColor = Game.Desktop
                        ? UIOriginalCaptionControl.NativeCaptionColor : UIStyle.Current.Text;
                    PerformanceTwin = new UIOriginalText(PerformanceTitle.Caption, performanceFont) { Color = twinColor };
                    PerformanceTwin.Position = PerformanceTitle.Position;
                    JobTwin = Game.Desktop
                        ? (UIOriginalText)new UIJobCareerCaption(JobTitle.Caption, jobFont) { Color = twinColor }
                        : new UIOriginalText(JobTitle.Caption, jobFont) { Color = UIStyle.Current.Text };
                    JobTwin.Position = JobTitle.Position;
                    if (Game.Desktop)
                        JobTwin.Size = new Vector2(JOB_CAPTION_W, JOB_CAPTION_H);
                    SalaryTwin = Game.Desktop
                        ? (UIOriginalText)new UIOriginalCaptionControl(SalaryTitle.Caption, salaryFont)
                        : new UIOriginalText(SalaryTitle.Caption, salaryFont);
                    SalaryTwin.Color = Game.Desktop
                        ? UIOriginalCaptionControl.NativeCaptionColor : UIStyle.Current.BtnActive;
                    SalaryTwin.Position = SalaryTitle.Position;
                    SkillTwins = new UIOriginalText[6];
                    for (int i = 0; i < 6; i++)
                    {
                        var index = i;
                        SkillTwins[i] = Game.Desktop
                            ? (UIOriginalText)new UIOriginalCaptionControl(SkillLabels[i].Caption, skillFont,
                                UIOriginalCaptionAlignment.Right, () => ToggleAdultSkillPopup(index)) { Color = twinColor }
                            : new UIOriginalText(SkillLabels[i].Caption, skillFont) { Color = UIStyle.Current.Text };
                        SkillTwins[i].Position = SkillLabels[i].Position;
                        SkillTwins[i].Size = new Vector2(SKILL_LABEL_W, SKILL_LABEL_H);
                        Add(SkillTwins[i]);
                        SkillLabels[i].Visible = false;
                    }
                    Add(PerformanceTwin);
                    Add(JobTwin);
                    Add(SalaryTwin);
                    if (Game.Desktop) EnsureAdultSummaryPopupControls();
                    PerformanceTitle.Visible = false;
                    JobTitle.Visible = false;
                    SalaryTitle.Visible = false;
                    JobStringsTwinned++;
                }
            }
            if (PerformanceTwin != null)
            {
                // sync the mutating captions + the unemployment visibility toggles. The modern
                // label's Visible field is the STATE HOLDER (the Update branches below write
                // it); the twin consumes it and the modern label is re-hidden - otherwise the
                // employed branch's Visible=true would draw both.
                PerformanceTwin.Text = PerformanceTitle.Caption;
                // cPerformanceBtn owns the native two-line career-caption
                // exception; UIJobCareerCaption reproduces it without clipping.
                JobTwin.Text = JobTitle.Caption;
                SalaryTwin.Text = SalaryTitle.Caption;
                PerformanceTwin.Visible = !Game.Desktop && PerformanceTitle.Visible;
                PerformanceTitle.Visible = false;
                for (int i = 0; i < 6; i++) SkillTwins[i].Text = SkillLabels[i].Caption;
            }

            var sel = Game.SelectedAvatar;
            if (sel == null) return;

            if (Game.Desktop)
            {
                UpdateDesktop(sel);
                return;
            }

            // R180: children swap the adult job controls for the executable's
            // single centered grade button. PersonFinder reads child word 57,
            // not the adult performance word 63.
            var age = sel.GetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.PersonsAge);
            bool child = IsChild(age);
            if (child != ReportCardMode)
            {
                SetReportCardMode(child);
            }
            if (ReportCardMode)
            {
                var gradeIndex = sel.GetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.JobPromotionLevel);
                var grade = GradeForIndex(gradeIndex);
                if (Game.Desktop)
                {
                    EnsureDesktopReportCard();
                    if (ReportGradeControl != null) ReportGradeControl.Text = grade;
                    RefreshReportGeometry();
                }
                else if (ReportGrades != null)
                {
                    ReportGrades.Text = (OriginalLiveStrings.Entry(139, 1) ?? "Grades") + ": " + grade;
                }
                SetAdultControlsVisible(false);
                return;
            }
            SetAdultControlsVisible(true);

            var type = sel.GetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.JobType);
            var level = sel.GetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.JobPromotionLevel);
            var performance = sel.GetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.JobPerformance);

            var job = Content.Get().Jobs.GetJob((ushort)type);
            if (job == null)
            {
                if (LastPerformance != -200)
                {
                    JobTitle.Caption = OriginalLiveStrings.Entry(136, 7) ?? "Unemployed";
                    // the ORIGINAL shows 'n/a' (136[8]) rather than clearing the line
                    SalaryTitle.Caption = OriginalLiveStrings.Entry(136, 8) ?? "";
                    PerformanceBar.Visible = false;
                    PerformanceTitle.Visible = false;
                    RatingBar.Visible = false;
                    RatingTitle.Visible = false;
                    RatingBar.Value = 0;

                    LastJobLevel = null;
                    LastPerformance = -200;
                }
                if (Game.Desktop)
                {
                    SetCareerIconRow(0);
                    if (FriendsTwin != null) FriendsTwin.Text = "";
                }
            }
            else
            {
                var myLevel = job.JobLevels[level];

                if (myLevel != LastJobLevel)
                {
                    JobTitle.Caption = myLevel.JobName;
                    SalaryTitle.Caption = Game.Desktop
                        ? "§" + myLevel.Salary
                        : (OriginalLiveStrings.Entry(137, 14) ?? "Salary") + ": §" + myLevel.Salary + " (" + ToTime(myLevel.StartTime) + "-" + ToTime(myLevel.EndTime) + ")";

                    if (Game.Desktop) SetCareerIconRow(type);

                    LastJobLevel = myLevel;
                }

                if (LastPerformance != performance)
                {
                    PerformanceBar.Visible = !ReportCardMode;
                    PerformanceTitle.Visible = !ReportCardMode;
                    PerformanceBar.Value = performance;
                    LastPerformance = performance;
                }
                // R132: the original Job Rating bar tracks the same
                // performance data (engine bar range [0,100]).
                RatingBar.Visible = true;
                RatingTitle.Visible = true;
                RatingBar.Value = performance;
                if (Game.Desktop && FriendsTwin != null)
                    FriendsTwin.Text = myLevel.MinRequired[0].ToString();
                for (int i = 0; i < 6; i++)
                    Skills[i].Needed = myLevel.MinRequired[i + 1] / 100;
            }

            for (int i = 0; i < 6; i++)
            {
                Skills[i].Value = sel.GetPersonData(SkillInd[i]) / 100;
            }

            // The backing UILabel captions above may change during this very
            // update (job change or unemployment). Mirror them again before
            // paint so the original-font controls never trail by one frame.
            if (PerformanceTwin != null)
            {
                PerformanceTwin.Text = PerformanceTitle.Caption;
                JobTwin.Text = JobTitle.Caption;
                SalaryTwin.Text = Game.Desktop
                    ? Fit(SalaryTwin.Font, SalaryTitle.Caption, 70)
                    : SalaryTitle.Caption;
                for (int i = 0; i < 6; i++)
                    SkillTwins[i].Text = SkillLabels[i].Caption;
            }

            if (Game.Desktop)
            {
                // The decoded desktop surface has one performance bar. Keep
                // the legacy R132 probe objects alive but never draw their
                // duplicate caption/bar over the six-row skill grid.
                PerformanceTitle.Visible = false;
                if (PerformanceTwin != null) PerformanceTwin.Visible = false;
                RatingTitle.Visible = false;
                RatingBar.Visible = false;
            }
        }

        public static UIDesktopJobMode ResolveDesktopMode(int age, bool isDog, bool isCat,
            int fameScore, bool hasCareer)
        {
            // cWinPeople::SetPanel resolves these in this exact order. Do not
            // collapse dog/cat into one pet test: the two panels differ in row
            // zero and in its popup pair.
            if (IsChild(age)) return UIDesktopJobMode.ReportCard;
            if (isDog) return UIDesktopJobMode.DogSkills;
            if (isCat) return UIDesktopJobMode.CatSkills;
            // CPState::GetShowFameUIPanel reads GetData()+0xa0: person word
            // 80 (the fame score). Word 81 is deliberately separate and only
            // drives the visible star-power bar/level inside the Fame panel.
            if (fameScore > 0 && !hasCareer) return UIDesktopJobMode.Fame;
            return UIDesktopJobMode.Job;
        }

        private void UpdateDesktop(VMAvatar sel)
        {
            var type = sel.GetPersonData(VMPersonDataVariable.JobType);
            var job = Content.Get()?.Jobs?.GetJob((ushort)type);
            var mode = ResolveDesktopMode(
                sel.GetPersonData(VMPersonDataVariable.PersonsAge),
                sel.IsDog, sel.IsCat,
                sel.GetPersonData(VMPersonDataVariable.TS1FameScore),
                job != null);
            SetDesktopMode(mode);

            switch (mode)
            {
                case UIDesktopJobMode.ReportCard:
                    EnsureDesktopReportCard();
                    if (ReportGradeControl != null)
                        ReportGradeControl.Text = GradeForIndex(
                            sel.GetPersonData(VMPersonDataVariable.JobPromotionLevel));
                    RefreshReportGeometry();
                    return;
                case UIDesktopJobMode.DogSkills:
                case UIDesktopJobMode.CatSkills:
                    UpdatePetSkills(sel, mode == UIDesktopJobMode.CatSkills);
                    return;
                case UIDesktopJobMode.Fame:
                    UpdateFame(sel);
                    return;
                default:
                    UpdateDesktopJob(sel, type, job);
                    return;
            }
        }

        private void UpdateDesktopJob(VMAvatar sel, int type, CARR job)
        {
            var level = sel.GetPersonData(VMPersonDataVariable.JobPromotionLevel);
            var performance = sel.GetPersonData(VMPersonDataVariable.JobPerformance);
            JobLevel myLevel = null;
            if (job != null && level >= 0 && level < job.JobLevels.Length)
                myLevel = job.JobLevels[level];

            if (myLevel == null)
            {
                JobTitle.Caption = OriginalLiveStrings.Entry(136, 7) ?? "Unemployed";
                SalaryTitle.Caption = OriginalLiveStrings.Entry(136, 8) ?? "n/a";
                PerformanceBar.Visible = false;
                if (FriendsTwin != null) FriendsTwin.Text = "";
                SetCareerIconRow(0);
                LastJobLevel = null;
                LastPerformance = -200;
                if (SkillBars != null)
                    for (int i = 0; i < SkillBars.Length; i++)
                    {
                        if (SkillBars[i] != null) SkillBars[i].RequiredPoints = 0;
                        SetSkillUnderline(i, false);
                    }
            }
            else
            {
                if (myLevel != LastJobLevel)
                {
                    JobTitle.Caption = myLevel.JobName;
                    SalaryTitle.Caption = "§" + myLevel.Salary;
                    SetCareerIconRow(type);
                    LastJobLevel = myLevel;
                }
                PerformanceBar.Visible = true;
                PerformanceBar.Value = performance;
                LastPerformance = performance;
                if (FriendsTwin != null) FriendsTwin.Text = myLevel.MinRequired[0].ToString();
            }

            if (SkillBars != null)
                for (int i = 0; i < 6; i++)
                {
                    var rawSkill = (int)sel.GetPersonData(SkillInd[i]);
                    SkillBars[i].Value = SkillBarValue(rawSkill);
                    var requiredRaw = myLevel == null ? 0 : myLevel.MinRequired[i + 1];
                    var needed = requiredRaw > rawSkill;
                    SkillBars[i].RequiredPoints = needed ? Math.Max(0, requiredRaw / 100) : 0;
                    SetSkillUnderline(i, needed);
                }

            if (PerformanceTwin != null)
            {
                PerformanceTwin.Text = PerformanceTitle.Caption;
                PerformanceTwin.Visible = false;
                JobTwin.Text = JobTitle.Caption;
                SalaryTwin.Text = SalaryTitle.Caption;
                for (int i = 0; i < 6; i++) SkillTwins[i].Text = SkillLabels[i].Caption;
            }
            RatingTitle.Visible = false;
            RatingBar.Visible = false;
        }

        private void SetSkillUnderline(int index, bool underline)
        {
            if (SkillTwins == null || index < 0 || index >= SkillTwins.Length) return;
            var caption = SkillTwins[index] as UIOriginalCaptionControl;
            if (caption != null) caption.Underline = underline;
        }

        private void EnsureAdultSummaryPopupControls()
        {
            if (SummaryPopupControls != null) return;
            SummaryPopupControls = new UIJobPopupHitControl[ADULT_SUMMARY_POPUP_RECTS.Length];
            for (int i = 0; i < SummaryPopupControls.Length; i++)
            {
                var index = i;
                SummaryPopupControls[i] = new UIJobPopupHitControl(
                    ADULT_SUMMARY_POPUP_RECTS[i],
                    () => ToggleAdultSummaryPopup(index),
                    () => ApplySummaryVisualStates())
                {
                    Visible = DesktopMode == UIDesktopJobMode.Job
                };
                Add(SummaryPopupControls[i]);
            }
            ApplySummaryVisualStates();
        }

        public void ToggleAdultSkillPopup(int index)
        {
            if (!Game.Desktop || DesktopMode != UIDesktopJobMode.Job
                || index < 0 || index >= ADULT_SKILL_POPUP_OBJECT_GUIDS.Length) return;
            var guids = ADULT_SKILL_POPUP_OBJECT_GUIDS[index];
            bool paired0;
            bool paired1;
            var art0 = PopupObjectArt(guids[0], out paired0);
            var art1 = PopupObjectArt(guids[1], out paired1);
            ToggleJobFamilyPopup("job-skill-" + index,
                OriginalLiveStrings.Entry(137, index * 2) ?? SkillNames[index] + " Skill",
                OriginalLiveStrings.Entry(137, index * 2 + 1) ?? "",
                art0, art1, "", -1, index, -1, guids[0], guids[1],
                paired0, paired1);
        }

        public void ToggleAdultSummaryPopup(int index)
        {
            if (!Game.Desktop || DesktopMode != UIDesktopJobMode.Job
                || index < 0 || index >= ADULT_SUMMARY_POPUP_RECTS.Length) return;

            var sel = Game.SelectedAvatar;
            var type = sel == null ? 0 : (int)sel.GetPersonData(VMPersonDataVariable.JobType);
            var level = sel == null ? -1 : (int)sel.GetPersonData(VMPersonDataVariable.JobPromotionLevel);
            var performance = sel == null ? 0 : (int)sel.GetPersonData(VMPersonDataVariable.JobPerformance);
            var job = Content.Get()?.Jobs?.GetJob((ushort)type);
            JobLevel jobLevel = null;
            if (job != null && level >= 0 && level < job.JobLevels.Length)
                jobLevel = job.JobLevels[level];
            var employed = jobLevel != null;
            string title;
            string body;
            Texture2D art = null;
            string member = "";
            var artRow = -1;

            switch (index)
            {
                case 0:
                    if (!employed)
                    {
                        title = OriginalLiveStrings.Entry(137, 12) ?? "Unemployed";
                        body = OriginalLiveStrings.Entry(137, 13) ?? "";
                        artRow = 0;
                    }
                    else
                    {
                        title = CareerPopupName(job, type, level,
                            sel.GetPersonData(VMPersonDataVariable.Gender) == 1);
                        body = BuildCareerPopupBody(jobLevel.StartTime, jobLevel.EndTime,
                            CareerPopupDescription(type, level));
                        artRow = Math.Max(0, Math.Min(JOB_POPUP_ROWS - 1, type));
                    }
                    art = UIOriginal.Rect("cpanel\\Backgrounds\\JobIconMultiPopup.bmp",
                        0, artRow * JOB_POPUP_CELL_H, JOB_POPUP_CELL_W, JOB_POPUP_CELL_H);
                    member = "cpanel\\Backgrounds\\JobIconMultiPopup.bmp";
                    break;
                case 1:
                    title = OriginalLiveStrings.Entry(137, 14) ?? "Salary";
                    body = OriginalLiveStrings.Entry(137, employed ? 15 : 24) ?? "";
                    break;
                case 2:
                    title = OriginalLiveStrings.Entry(137, 16) ?? "Performance";
                    var topLevel = employed && level + 1 >= job.JobLevels.Length;
                    body = OriginalLiveStrings.Entry(137,
                        PerformancePopupStringIndex(performance, topLevel, employed)) ?? "";
                    break;
                default:
                    title = OriginalLiveStrings.Entry(137, 18) ?? "Family Friends Needed";
                    body = OriginalLiveStrings.Entry(137, 19) ?? "";
                    break;
            }

            ToggleJobFamilyPopup("job-summary-" + index, title, body, art, null, member,
                index, -1, artRow, 0, 0);
        }

        public static int PerformancePopupStringIndex(int performance, bool topLevel, bool employed)
        {
            if (!employed) return 22;
            if (topLevel)
            {
                if (performance < -60) return 27;
                if (performance < -30) return 28;
                if (performance < 30) return 29;
                if (performance < 60) return 30;
                return 31;
            }
            if (performance < -60) return 17;
            if (performance < -30) return 20;
            if (performance < 30) return 21;
            if (performance < 60) return 25;
            return 26;
        }

        public static string FormatJobPopupTime(int hour)
        {
            hour = ((hour % 24) + 24) % 24;
            var suffix = hour < 12 ? "am" : "pm";
            var display = hour % 12;
            if (display == 0) display = 12;
            // cTSLanguageManager::MakeTimeString is called with both flags
            // false. English therefore uses "%2u:%.2u %s" and lowercase
            // am/pm; the leading pad on a one-digit hour is intentional.
            return display.ToString().PadLeft(2, ' ') + ":00 " + suffix;
        }

        public static string BuildCareerPopupBody(int startTime, int endTime, string description)
        {
            var template = OriginalLiveStrings.Entry(137, 23)
                ?? "Hours: $Start-$End.\r\nCarpool arrives: $CarTime.";
            var body = template
                .Replace("$Start", FormatJobPopupTime(startTime))
                .Replace("$End", FormatJobPopupTime(endTime))
                .Replace("$CarTime", FormatJobPopupTime((startTime + 23) % 24));
            if (!string.IsNullOrEmpty(description)) body += "\n" + description;
            return body;
        }

        private static string CareerPopupName(CARR job, int type, int level, bool female)
        {
            if (job == null || level < 0 || level >= job.JobLevels.Length) return "";
            if (female)
            {
                var alternate = Content.Get()?.Jobs?.JobStrings((short)type)?.GetString(23 + level);
                if (!string.IsNullOrEmpty(alternate)) return alternate;
            }
            return job.JobLevels[level].JobName ?? "";
        }

        private static string CareerPopupDescription(int type, int level)
        {
            if (level < 0) return "";
            return Content.Get()?.Jobs?.JobStrings((short)type)?.GetString(4 + level * 2) ?? "";
        }

        private Texture2D PopupObjectArt(uint guid, out bool pairedBitmap)
        {
            pairedBitmap = false;
            if (guid == 0) return null;
            Texture2D thumb;
            if (JobPopupObjectThumbs.TryGetValue(guid, out thumb)) return thumb;
            thumb = UIOriginalPopupObjectRenderer.Render(Game, guid);
            if (thumb != null)
            {
                JobPopupObjectThumbs[guid] = thumb;
                return thumb;
            }
            try
            {
                var obj = Content.Get()?.WorldObjects?.Get(guid);
                var fallback = obj?.Resource?.Get<BMP>(obj.OBJ.CatalogStringsID)
                    ?.GetTexture(GameFacade.GraphicsDevice);
                pairedBitmap = fallback != null && fallback.Width == fallback.Height * 2;
                return fallback;
            }
            catch { return null; }
        }

        private static int SkillBarValue(int rawSkill)
        {
            // PersonData stores skills in hundredths of a point. JobSubBars is
            // a ten-step 40px sheet; UIOriginalRatingBar consumes 0..100.
            return Math.Max(0, Math.Min(100, rawSkill / 10));
        }

        private void SetDesktopMode(UIDesktopJobMode mode)
        {
            var changed = mode != DesktopMode;
            if (changed)
            {
                CloseReportPopup();
                CloseJobFamilyPopup();
                LastJobLevel = null;
                LastPerformance = int.MinValue;
            }

            if (mode == UIDesktopJobMode.ReportCard) EnsureDesktopReportCard();
            if (mode == UIDesktopJobMode.DogSkills || mode == UIDesktopJobMode.CatSkills)
                EnsurePetControls();
            if (mode == UIDesktopJobMode.Fame) EnsureFameControls();

            if (changed && mode == UIDesktopJobMode.ReportCard) ReportCardShown++;
            DesktopMode = mode;
            ReportCardMode = mode == UIDesktopJobMode.ReportCard;
            SetAdultControlsVisible(mode == UIDesktopJobMode.Job);
            SetPetControlsVisible(mode == UIDesktopJobMode.DogSkills || mode == UIDesktopJobMode.CatSkills);
            SetFameControlsVisible(mode == UIDesktopJobMode.Fame);
            if (ReportGradeControl != null) ReportGradeControl.Visible = ReportCardMode;
            // UIJobSubpanel is cached as one native host. A selected-person
            // change can swap Job/Report Card/Dog/Cat/Fame without replacing
            // that host, so visibility changes must invalidate the cached
            // compositor or the previous surface remains frozen on screen.
            if (changed) Invalidate();
        }

        private void EnsurePetControls()
        {
            if (PetTitle != null) return;
            var gd = GameFacade.GraphicsDevice;
            var titleFont = OriginalGlyphFont.LoadByIndex(11, gd);
            var labelFont = OriginalGlyphFont.LoadByIndex(7, gd);
            if (titleFont == null || titleFont.Atlas == null || labelFont == null || labelFont.Atlas == null) return;

            PetTitle = new UIOriginalCaptionControl(PetString(0), titleFont)
            {
                Position = new Vector2(13, 8),
                Size = new Vector2(120, titleFont.LineHeight),
                Color = UIOriginalCaptionControl.NativeCaptionColor,
                Visible = false
            };
            Add(PetTitle);
            PetSkillLabels = new UIOriginalCaptionControl[3];
            PetSkillBars = new UIJobSkillBar[3];
            for (int i = 0; i < 3; i++)
            {
                var index = i;
                PetSkillLabels[i] = new UIOriginalCaptionControl("", labelFont,
                    UIOriginalCaptionAlignment.Right, () => TogglePetPopup(index))
                {
                    Position = new Vector2(103, 25 + 15 * i),
                    Size = new Vector2(102, 16),
                    Color = UIOriginalCaptionControl.NativeCaptionColor,
                    Visible = false
                };
                Add(PetSkillLabels[i]);
                PetSkillBars[i] = new UIJobSkillBar
                {
                    Position = new Vector2(207, 27 + 15 * i),
                    Size = new Vector2(UIJobSkillBar.NativeWidth, UIJobSkillBar.NativeHeight),
                    Visible = false
                };
                Add(PetSkillBars[i]);
            }
        }

        private void UpdatePetSkills(VMAvatar sel, bool cat)
        {
            EnsurePetControls();
            if (PetTitle == null) return;
            PetTitle.Text = PetString(0);
            PetSkillLabels[0].Text = PetString(cat ? 4 : 1);
            PetSkillLabels[1].Text = PetString(2);
            PetSkillLabels[2].Text = PetString(3);
            var vars = new[]
            {
                VMPersonDataVariable.CookingSkill,
                VMPersonDataVariable.MechanicalSkill,
                VMPersonDataVariable.CharismaSkill
            };
            for (int i = 0; i < 3; i++)
                PetSkillBars[i].Value = SkillBarValue(sel.GetPersonData(vars[i]));
        }

        private void SetPetControlsVisible(bool visible)
        {
            if (PetTitle != null) PetTitle.Visible = visible;
            if (PetSkillLabels != null)
                foreach (var label in PetSkillLabels) if (label != null) label.Visible = visible;
            if (PetSkillBars != null)
                foreach (var bar in PetSkillBars) if (bar != null) bar.Visible = visible;
        }

        private void EnsureFameControls()
        {
            if (FameTitle != null) return;
            var gd = GameFacade.GraphicsDevice;
            var titleFont = OriginalGlyphFont.LoadByIndex(11, gd);
            var font = OriginalGlyphFont.LoadByIndex(7, gd);
            if (titleFont == null || titleFont.Atlas == null || font == null || font.Atlas == null) return;

            FameTitle = new UIOriginalCaptionControl(FameString(0), titleFont)
            {
                Position = new Vector2(5, 0), Size = new Vector2(130, titleFont.LineHeight),
                Color = UIOriginalCaptionControl.NativeCaptionColor, Visible = false
            };
            Add(FameTitle);

            FameSkillLabels = new UIOriginalCaptionControl[6];
            FameSkillBars = new UIJobSkillBar[6];
            for (int i = 0; i < 6; i++)
            {
                var index = i;
                FameSkillLabels[i] = new UIOriginalCaptionControl(
                    OriginalLiveStrings.Entry(136, i + 1) ?? SkillNames[i], font,
                    UIOriginalCaptionAlignment.Right, () => ToggleFameSkillPopup(index))
                {
                    Position = new Vector2(145, 6 + 15 * i),
                    Size = new Vector2(62, 16),
                    Color = UIOriginalCaptionControl.NativeCaptionColor, Visible = false
                };
                Add(FameSkillLabels[i]);
                FameSkillBars[i] = new UIJobSkillBar
                {
                    Position = new Vector2(210, 7 + 15 * i),
                    Size = new Vector2(UIJobSkillBar.NativeWidth, UIJobSkillBar.NativeHeight),
                    Visible = false
                };
                Add(FameSkillBars[i]);
            }

            FameBar = new UIFameRatingBar
            {
                Position = new Vector2(3, 30), Size = new Vector2(80, 16), Visible = false
            };
            Add(FameBar);
            FameLevelLabel = new UIOriginalCaptionControl("", font,
                UIOriginalCaptionAlignment.Left, ToggleFameLevelPopup)
            {
                Position = new Vector2(3, 45), Size = new Vector2(102, 16),
                Color = UIOriginalCaptionControl.NativeCaptionColor, Visible = false
            };
            Add(FameLevelLabel);

            var friendArt = UIOriginal.Rect("cpanel\\Buttons\\FameFriendSmiley.bmp", 0, 0, 22, 20);
            if (friendArt != null)
            {
                FameFriendPlaque = new UIImage(friendArt) { Position = new Vector2(3, 75), Visible = false };
                Add(FameFriendPlaque);
            }
            FameFriendsLabel = new UIOriginalCaptionControl("", font,
                UIOriginalCaptionAlignment.Left, ToggleFameFriendsPopup)
            {
                Position = new Vector2(3, 75), Size = new Vector2(120, 15),
                TextOffsetX = 25,
                Color = UIOriginalCaptionControl.NativeCaptionColor, Visible = false
            };
            Add(FameFriendsLabel);

            // The executable constructs and updates this diagnostic cTSWinText
            // at (250,3), then explicitly hides it before Init returns.
            FameScoreLabel = new UIOriginalCaptionControl("", font)
            {
                Position = new Vector2(250, 3), Size = new Vector2(40, font.LineHeight),
                Color = UIOriginalCaptionControl.NativeCaptionColor, Visible = false
            };
            Add(FameScoreLabel);
        }

        private void UpdateFame(VMAvatar sel)
        {
            EnsureFameControls();
            if (FameTitle == null) return;
            var level = Math.Max(0, Math.Min(10,
                (int)sel.GetPersonData(VMPersonDataVariable.TS1FameStarPower)));
            FameTitle.Text = FameString(0);
            FameBar.Level = level;
            FameLevelLabel.Text = FameString(level + 1);
            FameScoreLabel.Text = sel.GetPersonData(VMPersonDataVariable.TS1FameScore).ToString();
            FameScoreLabel.Size = new Vector2(FameScoreLabel.Font.Measure(FameScoreLabel.Text),
                FameScoreLabel.Font.LineHeight);

            for (int i = 0; i < 6; i++)
            {
                FameSkillLabels[i].Text = OriginalLiveStrings.Entry(136, i + 1) ?? SkillNames[i];
                var rawSkill = (int)sel.GetPersonData(SkillInd[i]);
                FameSkillBars[i].Value = SkillBarValue(rawSkill);
                var required = FameSkillsNeeded[level][i];
                var needed = required > 0 && rawSkill < required * 100;
                FameSkillBars[i].RequiredPoints = needed ? required : 0;
                FameSkillLabels[i].Underline = needed;
            }

            var friendRaw = FamousFriendStarPower(sel);
            var friendText = (friendRaw / 2).ToString() + ((friendRaw & 1) == 0 ? ".0" : ".5");
            var friendsNeeded = FameFriendsNeeded[level];
            FameFriendsLabel.Text = friendRaw * 0.5f >= friendsNeeded
                ? friendText
                : FameString(12).Replace("%s", friendText).Replace("%d", friendsNeeded.ToString());
        }

        private int FamousFriendStarPower(VMAvatar sel)
        {
            try
            {
                var provider = Content.Get()?.Neighborhood;
                var selected = provider?.GetNeighborByID(
                    sel.GetPersonData(VMPersonDataVariable.NeighborId));
                if (selected == null || selected.Relationships == null) return 0;
                const int familyWord = (int)VMPersonDataVariable.TS1FamilyNumber;
                var selectedFamily = selected.PersonData != null
                    && selected.PersonData.Length > familyWord ? selected.PersonData[familyWord] : 0;
                var total = 0;
                foreach (var other in provider.Neighbors.Entries)
                {
                    if (other == null || other == selected || other.PersonData == null
                        || other.PersonData.Length <= (int)VMPersonDataVariable.TS1FameStarPower
                        || other.Relationships == null) continue;
                    var otherFamily = other.PersonData[familyWord];
                    // Neighborhood::GetFamousFriendCount rejects homeless
                    // neighbors and members of the selected Sim's family.
                    if (otherFamily == 0 || otherFamily == selectedFamily) continue;
                    List<short> forward;
                    List<short> reverse;
                    if (!selected.Relationships.TryGetValue(other.NeighbourID, out forward)
                        || !other.Relationships.TryGetValue(selected.NeighbourID, out reverse)
                        || forward.Count == 0 || reverse.Count == 0
                        || forward[0] < 50 || reverse[0] < 50) continue;
                    total += Math.Max(0,
                        (int)other.PersonData[(int)VMPersonDataVariable.TS1FameStarPower]);
                }
                return total;
            }
            catch { return 0; }
        }

        private void SetFameControlsVisible(bool visible)
        {
            if (FameTitle != null) FameTitle.Visible = visible;
            if (FameSkillLabels != null)
                foreach (var label in FameSkillLabels) if (label != null) label.Visible = visible;
            if (FameSkillBars != null)
                foreach (var bar in FameSkillBars) if (bar != null) bar.Visible = visible;
            if (FameBar != null) FameBar.Visible = visible;
            if (FameLevelLabel != null) FameLevelLabel.Visible = visible;
            if (FameFriendsLabel != null) FameFriendsLabel.Visible = visible;
            if (FameFriendPlaque != null) FameFriendPlaque.Visible = visible;
            // FameScoreLabel stays hidden: that is an explicit native action.
            if (FameScoreLabel != null) FameScoreLabel.Visible = false;
        }

        private void TogglePetPopup(int index)
        {
            var cat = DesktopMode == UIDesktopJobMode.CatSkills;
            var pair = (cat && index == 0) ? 6 : index * 2;
            ToggleJobFamilyPopup("pet-" + pair, PetPopupString(pair), PetPopupString(pair + 1), null);
        }

        private void ToggleFameSkillPopup(int index)
        {
            ToggleJobFamilyPopup("fame-skill-" + index,
                OriginalLiveStrings.Entry(137, index * 2) ?? SkillNames[index] + " Skill",
                OriginalLiveStrings.Entry(137, index * 2 + 1) ?? "", null);
        }

        private void ToggleFameLevelPopup()
        {
            var level = FameBar?.Level ?? 0;
            var art = UIOriginal.Rect("cpanel\\Backgrounds\\FameIconMultiPopup.bmp", 0, 0, 84, 63);
            ToggleJobFamilyPopup("fame-level-" + level,
                FameString(13 + level * 2), FameString(14 + level * 2), art);
        }

        private void ToggleFameFriendsPopup()
        {
            var art = UIOriginal.Rect("cpanel\\Backgrounds\\FameIconMultiPopup.bmp", 0, 0, 84, 63);
            ToggleJobFamilyPopup("fame-friends", FameString(37), FameString(38), art);
        }

        private void ToggleJobFamilyPopup(string key, string title, string body, Texture2D art)
        {
            ToggleJobFamilyPopup(key, title, body, art, null, "", -1, -1, -1, 0, 0);
        }

        private void ToggleJobFamilyPopup(string key, string title, string body,
            Texture2D art, Texture2D art2, string member, int summaryIndex,
            int skillIndex, int artRow, uint objectGuid0, uint objectGuid1,
            bool artIsPairedBitmap = false, bool art2IsPairedBitmap = false)
        {
            if (JobFamilyPopup != null && JobFamilyPopupKey == key)
            {
                CloseJobFamilyPopup();
                return;
            }
            if (JobFamilyPopup == null)
            {
                var row = new UIOriginalOptionsPanel.OptRow
                {
                    Caption = title,
                    AboutTitle = title,
                    AboutBody = body,
                    PopupMember = member ?? ""
                };
                JobFamilyPopup = new UIOptionAboutPopup(null, row, art);
                DynamicOverlay.Add(JobFamilyPopup);
            }

            if (objectGuid0 != 0 || objectGuid1 != 0)
                JobFamilyPopup.SetObjectContent(title, body,
                    art, objectGuid0, art2, objectGuid1, 0, 0,
                    artIsPairedBitmap, art2IsPairedBitmap);
            else
                JobFamilyPopup.SetContent(title, body, art, member ?? "");

            JobFamilyPopup.Position = new Vector2(Size.X - JobFamilyPopup.Size.X, -JobFamilyPopup.Size.Y);
            JobFamilyPopup.CanonPos = new Point((int)JobFamilyPopup.X, (int)JobFamilyPopup.Y);
            JobFamilyPopupKey = key;
            JobFamilyPopupSummaryIndex = summaryIndex;
            JobFamilyPopupSkillIndex = skillIndex;
            JobFamilyPopupArtRow = artRow;
            JobFamilyPopupObjectGuids[0] = objectGuid0;
            JobFamilyPopupObjectGuids[1] = objectGuid1;
            ApplyJobFamilyPopupSelection();
        }

        public void CloseJobFamilyPopup()
        {
            if (JobFamilyPopup == null) return;
            DynamicOverlay.Remove(JobFamilyPopup);
            JobFamilyPopup = null;
            JobFamilyPopupKey = null;
            JobFamilyPopupSummaryIndex = -1;
            JobFamilyPopupSkillIndex = -1;
            JobFamilyPopupArtRow = -1;
            JobFamilyPopupObjectGuids[0] = 0;
            JobFamilyPopupObjectGuids[1] = 0;
            ApplyJobFamilyPopupSelection();
        }

        private void ApplyJobFamilyPopupSelection()
        {
            if (SkillTwins != null)
                for (int i = 0; i < SkillTwins.Length; i++)
                {
                    var caption = SkillTwins[i] as UIOriginalCaptionControl;
                    if (caption != null)
                    {
                        caption.SelectedState = i == JobFamilyPopupSkillIndex;
                        caption.Invalidate();
                    }
                }
            if (SummaryPopupControls != null)
                for (int i = 0; i < SummaryPopupControls.Length; i++)
                    SummaryPopupControls[i].SelectedState = i == JobFamilyPopupSummaryIndex;
            if (PetSkillLabels != null)
                for (int i = 0; i < PetSkillLabels.Length; i++)
                    PetSkillLabels[i].SelectedState = JobFamilyPopupKey == "pet-" +
                        ((DesktopMode == UIDesktopJobMode.CatSkills && i == 0) ? 6 : i * 2);
            if (FameSkillLabels != null)
                for (int i = 0; i < FameSkillLabels.Length; i++)
                    FameSkillLabels[i].SelectedState = JobFamilyPopupKey == "fame-skill-" + i;
            if (FameLevelLabel != null)
                FameLevelLabel.SelectedState = JobFamilyPopupKey != null
                    && JobFamilyPopupKey.StartsWith("fame-level-");
            if (FameFriendsLabel != null)
                FameFriendsLabel.SelectedState = JobFamilyPopupKey == "fame-friends";
            ApplySummaryVisualStates();
        }

        private static string PetString(int index)
        {
            return JobFamilyString("Live.iff", 171, index,
                index >= 0 && index < PetSkillFallback.Length ? PetSkillFallback[index] : "");
        }

        private static string PetPopupString(int index)
        {
            return JobFamilyString("Live.iff", 172, index,
                index >= 0 && index < PetPopupFallback.Length ? PetPopupFallback[index] : "");
        }

        private static string FameString(int index)
        {
            return JobFamilyString("fame.iff", 1, index,
                index >= 0 && index < FameFallback.Length ? FameFallback[index] : "");
        }

        private static string JobFamilyString(string file, int chunkId, int index, string fallback)
        {
            var key = file + "#" + chunkId;
            string[] values;
            if (!JobFamilyTables.TryGetValue(key, out values))
            {
                values = LoadJobFamilyStrings(file, chunkId);
                if (values != null) JobFamilyTables[key] = values;
            }
            return values != null && index >= 0 && index < values.Length
                ? values[index] ?? fallback : fallback;
        }

        private static string[] LoadJobFamilyStrings(string file, int chunkId)
        {
            try
            {
                var path = System.IO.Path.Combine(FSO.Content.Content.TS1HybridBasePath,
                    "GameData", file);
                if (!System.IO.File.Exists(path)) return null;
                var data = System.IO.File.ReadAllBytes(path);
                var offset = 0x40;
                while (offset + 76 <= data.Length)
                {
                    var size = data[offset + 4] << 24 | data[offset + 5] << 16
                        | data[offset + 6] << 8 | data[offset + 7];
                    if (size < 76 || offset + size > data.Length) break;
                    var id = data[offset + 8] << 8 | data[offset + 9];
                    if (data[offset] == (byte)'S' && data[offset + 1] == (byte)'T'
                        && data[offset + 2] == (byte)'R' && data[offset + 3] == (byte)'#'
                        && id == chunkId)
                    {
                        var body = offset + 76;
                        var format = (short)(data[body] | data[body + 1] << 8);
                        if (format != -3) return null;
                        var count = data[body + 2] | data[body + 3] << 8;
                        var cursor = body + 4;
                        var end = offset + size;
                        var english = new List<string>();
                        for (int i = 0; i < count && cursor < end; i++)
                        {
                            var language = data[cursor++];
                            var value = ReadJobFamilyCString(data, ref cursor, end);
                            ReadJobFamilyCString(data, ref cursor, end);
                            if (language == 1) english.Add(value);
                        }
                        return english.ToArray();
                    }
                    offset += size;
                }
            }
            catch { }
            return null;
        }

        private static string ReadJobFamilyCString(byte[] data, ref int cursor, int end)
        {
            var finish = cursor;
            while (finish < end && data[finish] != 0) finish++;
            // STR# stores Windows-1252 byte keys. Keep a one-byte-to-one-char
            // mapping here: OriginalGlyphFont.MapChar consumes those same
            // byte-range keys (not Unicode replacement question marks).
            var chars = new char[finish - cursor];
            for (int i = 0; i < chars.Length; i++) chars[i] = (char)data[cursor + i];
            var value = new string(chars);
            cursor = Math.Min(end, finish + 1);
            return value;
        }

        public static string GradeForIndex(int index)
        {
            if (index < 0) return "";
            var jobs = Content.Get()?.Jobs;
            var grades = jobs?.JobResource?.Get<STR>((ushort)4097);
            if (grades == null || index >= grades.Length) return "";
            return grades.GetString(index) ?? "";
        }

        private void EnsureDesktopReportCard()
        {
            if (ReportGradeControl != null) return;
            var font = OriginalGlyphFont.LoadByIndex(18, GameFacade.GraphicsDevice);
            if (font == null || font.Atlas == null) return;
            ReportGradeControl = new UIReportGradeControl(font, ToggleReportPopup)
            {
                Color = UIOriginalCaptionControl.NativeCaptionColor,
                Tooltip = OriginalLiveStrings.Entry(139, 0) ?? "Report Card"
            };
            Add(ReportGradeControl);
            RefreshReportGeometry();
        }

        private void EnsureMobileReportCard()
        {
            if (ReportTitle != null) return;
            var font = OriginalGlyphFont.LoadCaption(GameFacade.GraphicsDevice);
            if (font == null || font.Atlas == null) return;
            // Mobile retains its established touch layout. It now uses the
            // original grade table, while the decoded desktop path above is
            // the executable's one-control geometry.
            ReportTitle = new UIOriginalText(OriginalLiveStrings.Entry(139, 0) ?? "Report Card", font)
            {
                Position = new Vector2(18, 71), Color = UIStyle.Current.Text
            };
            ReportGrades = new UIOriginalText("", font)
            {
                Position = new Vector2(18, 94), Color = UIStyle.Current.Text
            };
            ReportDesc = new UIOriginalText(OriginalLiveStrings.Entry(139, 2) ?? "", font)
            {
                Position = new Vector2(280, 71), Size = new Vector2(460, 60), Color = UIStyle.Current.Text
            };
            Add(ReportTitle); Add(ReportGrades); Add(ReportDesc);
        }

        private void SetReportCardMode(bool child)
        {
            ReportCardMode = child;
            if (child)
            {
                ReportCardShown++;
                if (Game.Desktop) EnsureDesktopReportCard();
                else EnsureMobileReportCard();
            }
            else
            {
                CloseReportPopup();
                LastJobLevel = null;
                LastPerformance = int.MinValue;
            }
            if (ReportGradeControl != null) ReportGradeControl.Visible = child && Game.Desktop;
            if (ReportTitle != null) ReportTitle.Visible = child && !Game.Desktop;
            if (ReportGrades != null) ReportGrades.Visible = child && !Game.Desktop;
            if (ReportDesc != null) ReportDesc.Visible = child && !Game.Desktop;
        }

        private void SetAdultControlsVisible(bool visible)
        {
            if (CareerButton != null) CareerButton.Visible = visible;
            if (HostTitle != null) HostTitle.Visible = visible;
            if (JobTwin != null) JobTwin.Visible = visible;
            if (SalaryTwin != null) SalaryTwin.Visible = visible;
            if (FriendsTwin != null) FriendsTwin.Visible = visible;
            if (SalaryPlaque != null) SalaryPlaque.Visible = visible;
            if (PerformancePlaque != null) PerformancePlaque.Visible = visible;
            if (FriendsPlaque != null) FriendsPlaque.Visible = visible;
            if (SkillTwins != null)
                foreach (var twin in SkillTwins) if (twin != null) twin.Visible = visible;
            if (SkillBars != null)
                foreach (var bar in SkillBars) if (bar != null) bar.Visible = visible;
            if (SummaryPopupControls != null)
                foreach (var control in SummaryPopupControls) if (control != null) control.Visible = visible;
            if (Skills != null)
                foreach (var skill in Skills) if (skill != null) skill.Visible = visible;
            if (!visible)
            {
                if (PerformanceBar != null) PerformanceBar.Visible = false;
                if (PerformanceTwin != null) PerformanceTwin.Visible = false;
                if (RatingBar != null) RatingBar.Visible = false;
                if (RatingTitle != null) RatingTitle.Visible = false;
            }
        }

        private void RefreshReportGeometry()
        {
            if (JobFamilyPopup != null)
                JobFamilyPopup.Position = new Vector2(Size.X - JobFamilyPopup.Size.X, -JobFamilyPopup.Size.Y);
            if (ReportGradeControl == null || ReportGradeControl.Font == null) return;
            var height = ReportGradeControl.Font.LineHeight;
            ReportGradeControl.Size = new Vector2(Size.X, height);
            ReportGradeControl.Position = new Vector2(0, ((int)Size.Y - height) / 2);
            if (ReportPopup != null)
                ReportPopup.Position = new Vector2(Size.X - ReportPopup.Size.X, -ReportPopup.Size.Y);
        }

        public void ToggleReportPopup()
        {
            if (!ReportCardMode || !Game.Desktop) return;
            if (ReportPopup != null)
            {
                CloseReportPopup();
                return;
            }
            const int rowWidth = 84;
            const int rowHeight = 63;
            const int reportCardRow = 11;
            var art = UIOriginal.Rect("cpanel\\Backgrounds\\JobIconMultiPopup.bmp",
                0, reportCardRow * rowHeight, rowWidth, rowHeight);
            var row = new UIOriginalOptionsPanel.OptRow
            {
                Caption = OriginalLiveStrings.Entry(139, 0) ?? "Report Card",
                AboutTitle = OriginalLiveStrings.Entry(139, 1) ?? "Grades",
                AboutBody = OriginalLiveStrings.Entry(139, 2) ?? "",
                PopupMember = "cpanel\\Backgrounds\\JobIconMultiPopup.bmp"
            };
            ReportPopup = new UIOptionAboutPopup(null, row, art);
            ReportPopup.Position = new Vector2(Size.X - ReportPopup.Size.X, -ReportPopup.Size.Y);
            ReportPopup.CanonPos = new Point((int)ReportPopup.X, (int)ReportPopup.Y);
            DynamicOverlay.Add(ReportPopup);
        }

        public void CloseReportPopup()
        {
            if (ReportPopup == null) return;
            DynamicOverlay.Remove(ReportPopup);
            ReportPopup = null;
        }

        public override void GameResized()
        {
            base.GameResized();
            RefreshReportGeometry();
        }

        public override void Kill()
        {
            CloseReportPopup();
            CloseJobFamilyPopup();
            foreach (var thumb in JobPopupObjectThumbs.Values)
            {
                try { thumb?.Dispose(); }
                catch { }
            }
            JobPopupObjectThumbs.Clear();
            base.Kill();
        }

        private void LoadSummaryPlaqueStates(int index, string member, int width, int height)
        {
            var states = new Texture2D[4];
            for (int frame = 0; frame < states.Length; frame++)
                states[frame] = UIOriginal.Rect(member, frame * width, 0, width, height);
            SummaryPlaqueStates[index] = states;
        }

        private void ApplySummaryVisualStates()
        {
            if (SummaryPopupControls == null) return;
            for (int i = 0; i < SummaryPopupControls.Length; i++)
            {
                var control = SummaryPopupControls[i];
                if (control == null) continue;
                var frame = control.NativeFrame;
                if (i == 0 && CareerButton != null) CareerButton.ForceState = frame;
                else
                {
                    var image = i == 1 ? SalaryPlaque : i == 2 ? PerformancePlaque : FriendsPlaque;
                    var states = SummaryPlaqueStates[i];
                    if (image != null && states != null && frame >= 0 && frame < states.Length
                        && states[frame] != null) image.Texture = states[frame];
                }

                var caption = (i == 0 ? JobTwin : i == 1 ? SalaryTwin : i == 3 ? FriendsTwin : null)
                    as UIOriginalCaptionControl;
                if (caption != null)
                {
                    caption.SelectedState = control.SelectedState;
                    caption.HoverState = control.HoverState;
                    caption.DownState = control.DownState;
                    caption.Invalidate();
                }
            }
            Invalidate();
        }

        private void SetCareerIconRow(int jobType)
        {
            var row = Math.Max(0, Math.Min(CAREER_ICON_ROWS - 1, jobType));
            if (row == LastCareerIconRow || CareerButton == null) return;
            var crop = UIOriginal.Rect("cpanel\\Buttons\\JobIconMultiButton.BMP",
                0, row * CAREER_ICON_CELL_H, CAREER_ICON_CELL_W * 4, CAREER_ICON_CELL_H);
            if (crop != null)
            {
                CareerButton.Texture = crop;
                CareerButton.ImageStates = 4;
                LastCareerIconRow = row;
            }
        }

        private static string Fit(OriginalGlyphFont font, string text, int maxWidth)
        {
            if (font == null || string.IsNullOrEmpty(text)) return text;
            while (text.Length > 0 && font.Measure(text) > maxWidth)
                text = text.Substring(0, text.Length - 1);
            return text;
        }

        private string ToTime(int time)
        {
            return ((time > 12) ? (time - 12) : time) + ((time >= 12) ? "pm" : "am");
        }

        private void InitLabel(UILabel label)
        {
            label.CaptionStyle = label.CaptionStyle.Clone();
            label.CaptionStyle.Color = UIStyle.Current.Text;
            label.CaptionStyle.Size = 15;
            Add(label);
        }
    }
}
