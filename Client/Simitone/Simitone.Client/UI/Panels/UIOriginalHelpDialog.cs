using FSO.Client;
using FSO.Client.UI.Framework;
using FSO.Common.Rendering.Framework.Model;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Simitone.Client.UI.Controls;
using System;
using System.Collections.Generic;

namespace Simitone.Client.UI.Panels
{
    // cWinHelp::Init 0x277cc0 and sinit 0x278530: a font[12] topic list
    // measured to its widest title +25, beside a 340px system-font label.
    // The 12-row list initializes to 12*(TSCharHeight+3), then determines
    // the body and dialog height. No separate title is painted. See
    // tools/iff-dump/r238-budget/help-layout-law.md for the recovered law.
    public class UIOriginalHelpDialog : UIContainer
    {
        public const int VisibleTopics = 12, TopicCount = 47;
        public const int TopicX = 20, TopicY = 20, BodyWidth = 340;
        public const int PaneGap = 20, TextGutter = 2;
        public const string FrameMember = "cpanel\\Backgrounds\\PopupInfoTiles.bmp";
        public const int FrameCell = 12;
        public static int DialogsMounted, HelpButtonOpens, LastSelected;

        public readonly List<string> Topics = new List<string>();
        public readonly Dictionary<int, string> Bodies = new Dictionary<int, string>();
        public readonly List<UIOriginalText> TopicLabels = new List<UIOriginalText>();
        public readonly UIOriginalTextList TopicList;
        public readonly UIBigButton CloseButton;
        public readonly UIOriginalText CloseLabel;
        private readonly Vector2 CloseLabelOrigin;
        private readonly ClippedHelpBody Body;
        public readonly Rectangle BodyRect, CloseRect;
        public readonly int WindowW, WindowH, TopicColumnW, TopicPitch;
        public int Selected { get { return TopicList.SelectedIndex; } }
        public int Scroll { get { return TopicList.TopRow; } }
        public int BodyLineCount { get { return Body.LineCount; } }
        public override Vector2 Size
        {
            get { return new Vector2(WindowW, WindowH); }
            set { }
        }
        public override Rectangle GetBounds() { return new Rectangle(0, 0, WindowW, WindowH); }

        public UIOriginalHelpDialog()
        {
            var font = OriginalGlyphFont.LoadByIndex(12, GameFacade.GraphicsDevice);
            var systemFont = OriginalGlyphFont.LoadByIndex(11, GameFacade.GraphicsDevice);
            int widest = 0;
            for (int i = 0; i < TopicCount; i++)
            {
                var topic = S166(i * 2, "");
                Topics.Add(topic);
                Bodies[i] = S166(i * 2 + 1, "");
                widest = Math.Max(widest, font.Measure(topic));
            }
            TopicColumnW = widest > 0 ? widest + 25 : 250;
            TopicList = new UIOriginalTextList(font, TopicColumnW, VisibleTopics)
            { Position = new Vector2(TopicX, TopicY), KeyboardActive = true };
            // Original Init samples the center pixel of the frame for the
            // list background (+64); it is not the row's text color.
            var tiles = UIOriginalDialogChrome.GetPictureTiles();
            if (tiles != null)
            {
                var pixel = new Color[1];
                tiles.GetData(0, new Rectangle(tiles.Width / 2, tiles.Height / 2, 1, 1), pixel, 0, 1);
                TopicList.BackgroundColor = pixel[0];
            }
            TopicPitch = TopicList.RowHeight;
            TopicList.SetItems(Topics);
            TopicLabels.AddRange(TopicList.Rows);
            TopicList.OnSelectionChange += Select;
            Add(TopicList);

            BodyRect = new Rectangle(TopicX + TopicColumnW + PaneGap, TopicY,
                BodyWidth, VisibleTopics * TopicPitch);
            Body = new ClippedHelpBody(systemFont, BodyWidth - 2 * TextGutter,
                BodyRect.Height - 2 * TextGutter)
            {
                Position = new Vector2(BodyRect.X + TextGutter, BodyRect.Y + TextGutter),
                MaxWidth = BodyWidth - 2 * TextGutter,
                LineHeight = systemFont.LineHeight,
                TextColor = new Color(195, 205, 205)
            };
            Add(Body);

            // GetButtonLabel(3) is OK. DefaultPushBtn establishes a 100px
            // minimum width on the original 33px-high WinBtn art.
            WindowW = BodyRect.Right + 20;
            WindowH = BodyRect.Bottom + 2 * 33;
            CloseRect = new Rectangle((WindowW - 100) / 2, BodyRect.Bottom + 33 / 2, 100, 33);
            var caption = GameFacade.Strings.GetString("152", "0");
            if (string.IsNullOrEmpty(caption) || caption.Contains("MISSING")) caption = "OK";
            CloseButton = new UIBigButton(false)
            { Caption = "", Width = CloseRect.Width, Position = new Vector2(CloseRect.X, CloseRect.Y), Tooltip = caption };
            CloseButton.OnButtonClick += _ => Close();
            Add(CloseButton);
            CloseLabel = new UIOriginalText(caption, systemFont)
            {
                Position = new Vector2(CloseRect.X + (CloseRect.Width - systemFont.Measure(caption)) / 2,
                    CloseRect.Y + systemFont.ButtonCaptionY(CloseRect.Height)),
                Color = new Color(195, 205, 205)
            };
            CloseLabelOrigin = CloseLabel.Position;
            Add(CloseLabel);
            Select(Math.Max(0, Math.Min(TopicCount - 1, LastSelected)));
            DialogsMounted++;
            UpdatePosition();
        }

        public void Select(int index)
        {
            index = Math.Max(0, Math.Min(TopicCount - 1, index));
            if (TopicList.SelectedIndex != index) TopicList.Select(index);
            LastSelected = index;
            Body.Text = Bodies[index].Replace("\r\n", "\n").Replace('\r', '\n');
            Body.WrapIfNeeded();
        }

        public void ScrollBy(int delta) { TopicList.ScrollBy(delta); }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            CloseLabel.Color = CloseButton.IsDown ? Color.Cyan : CloseButton.Hovered ? Color.White : new Color(195, 205, 205);
            CloseLabel.Position = CloseLabelOrigin + (CloseButton.IsDown ? new Vector2(2, 2) : Vector2.Zero);
            if (state.WindowFocused && (state.NewKeys.Contains(Keys.Escape) || state.NewKeys.Contains(Keys.Enter))) Close();
        }

        public void UpdatePosition()
        {
            ScaleX = ScaleY = 1;
            X = (GlobalSettings.Default.GraphicsWidth - WindowW) / 2;
            Y = (GlobalSettings.Default.GraphicsHeight - WindowH) / 2;
        }

        public override void GameResized() { UpdatePosition(); }
        public void Close() { UIScreen.RemoveDialog(this); }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            UIOriginalDialogChrome.DrawPictureWindow(this, batch, 0, 0, WindowW, WindowH);
            base.Draw(batch);
        }

        // cTSWinText paints into its gutter-inset text rectangle. Clip glyph
        // source cells locally so unusual/localized text cannot paint onto
        // the neighboring list or footer; other paragraph consumers are untouched.
        private sealed class ClippedHelpBody : UIOriginalParagraph
        {
            private readonly int WidthPixels, HeightPixels;
            public int LineCount { get { WrapIfNeeded(); return Children.Count; } }
            public ClippedHelpBody(OriginalGlyphFont font, int width, int height) : base(font)
            { WidthPixels = width; HeightPixels = height; }
            public override void Draw(UISpriteBatch batch)
            {
                if (!Visible || Font?.Atlas == null) return;
                WrapIfNeeded();
                foreach (var child in Children)
                {
                    var line = child as UIOriginalText;
                    if (line == null || !line.Visible) continue;
                    int x = (int)line.X;
                    foreach (char original in line.Text)
                    {
                        char c = OriginalGlyphFont.MapChar(original);
                        OriginalGlyphFont.Glyph glyph;
                        if (Font.ByChar.TryGetValue(c, out glyph) && glyph.W > 1)
                        {
                            var target = new Rectangle(x, (int)line.Y + Font.GlyphY(glyph), glyph.W, glyph.H);
                            var clip = Rectangle.Intersect(target, new Rectangle(0, 0, WidthPixels, HeightPixels));
                            if (clip.Width > 0 && clip.Height > 0)
                                DrawLocalTexture(batch, Font.Atlas,
                                    new Rectangle(glyph.U + clip.X - target.X, glyph.V + clip.Y - target.Y, clip.Width, clip.Height),
                                    new Vector2(clip.X, clip.Y), Vector2.One, line.Color * Opacity);
                        }
                        x += Font.Advance(c);
                        if (x >= WidthPixels) break;
                    }
                    UIOriginalText.StringsDrawn++;
                }
            }
        }

        private static string S166(int index, string fallback)
        {
            var text = GameFacade.Strings.GetString("166", index.ToString());
            return string.IsNullOrEmpty(text) || text.Contains("MISSING") ? fallback : text;
        }
    }
}
