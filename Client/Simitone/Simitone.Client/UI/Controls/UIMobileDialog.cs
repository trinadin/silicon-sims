using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Content;
using Microsoft.Xna.Framework;
using Simitone.Client.UI.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simitone.Client.UI.Controls
{
    public class UIMobileDialog : UIContainer
    {
        public UIDiagonalStripe BackStripe;
        public UIDiagonalStripe FrontStripe;

        private UILabel TitleLabel;
        private UIImage TitleBg;
        private UIOriginalText TitleTwin;

        // R175: cTSWinCtrlMgr::DefaultLabel installs system font slot 0. The
        // original SetupWinCtrlMgr maps that slot to font_table[11], and
        // InitSimsColors gives every regular VariableSans face this default
        // RGB. cWinPictureDialog replaces the face with slot 14, but keeps the
        // same color. This is deliberately separate from the touch/mobile
        // title style, whose animation still uses UIStyle.DialogTitle.
        internal static readonly Color OriginalSystemTextColor = new Color(195, 205, 205);

        // Gate-readable: dialog titles carrying an ORIGINAL VariableSans glyph
        // twin (desktop generic slot 11, picture slot 14, touch slot 9).
        public static int TitlesTwinned = 0;

        // Generic GenDlg chrome is opt-in. UIMobileAlert has a decoded desktop
        // layout, while the phonebook and skin-picker subclasses still own 1030px
        // mobile compositions and must retain the stripe path on desktop too.
        // Touch builds always retain the mobile path regardless of the opt-in.
        public readonly bool OriginalChrome;

        public string Caption
        {
            set
            {
                TitleLabel.Caption = value;
                // R175: a desktop cTSWinMsgBox title comes from CtrlMgr's
                // DefaultLabel: system font slot 0 == font_table[11], with the
                // initialized default color #C3CDCD. Picture dialogs replace
                // this with slot 14 in UIMobileAlert. Touch keeps the historic
                // variablesans_09 twin and animated mobile color.
                if (TitleTwin == null)
                {
                    var font = OriginalChrome
                        ? OriginalGlyphFont.LoadByIndex(11, FSO.Client.GameFacade.GraphicsDevice)
                        : OriginalGlyphFont.LoadDialog(FSO.Client.GameFacade.GraphicsDevice);
                    if (font != null && font.Atlas != null)
                    {
                        TitleTwin = new UIOriginalText(value, font)
                        {
                            Color = OriginalChrome
                                ? OriginalSystemTextColor
                                : UIStyle.Current.DialogTitle
                        };
                        // cTSWinMsgBox::Init final SetArea is (16,16,...).
                        // The mobile band position remains (50,26).
                        TitleTwin.Position = OriginalChrome ? new Vector2(16, 16) : new Vector2(50, 26);
                        Add(TitleTwin);
                        TitleLabel.Visible = false;
                        TitlesTwinned++;
                    }
                }
                else TitleTwin.Text = value;
            }
        }

        protected bool HasOriginalTitle
        {
            get { return TitleTwin != null && !string.IsNullOrEmpty(TitleTwin.Text); }
        }

        protected int OriginalTitleWidth
        {
            get { return HasOriginalTitle && TitleTwin.Font != null ? TitleTwin.Font.Measure(TitleTwin.Text) : 0; }
        }

        protected int OriginalTitleHeight
        {
            get
            {
                if (!HasOriginalTitle || TitleTwin.Font == null) return 0;
                return TitleTwin.Font.LineHeight;
            }
        }

        protected void SetOriginalTitlePosition(Vector2 position)
        {
            if (TitleTwin != null) TitleTwin.Position = position;
        }

        protected void SetOriginalTitleAppearance(OriginalGlyphFont font, Color color)
        {
            if (TitleTwin == null) return;
            if (font != null) TitleTwin.Font = font;
            TitleTwin.Color = color;
        }

        internal Vector2 OriginalTitlePositionForProbe
        {
            get { return TitleTwin != null ? TitleTwin.Position : new Vector2(float.NaN, float.NaN); }
        }

        internal OriginalGlyphFont OriginalTitleFontForProbe
        {
            get { return TitleTwin != null ? TitleTwin.Font : null; }
        }

        internal Color OriginalTitleColorForProbe
        {
            get { return TitleTwin != null ? TitleTwin.Color : Color.Transparent; }
        }

        public int Width;
        public int Height;
        public int ScrHeight;

        // R142: the desktop window's own width (the box, not the screen). The mobile
        // Width stays full-screen; the desktop box law is content-sized, min 200
        // (cTSWinMsgBox::Init auto-size).
        public int BoxWidth = 200;

        private int BaseY
        {
            get { return (int)FrontStripe.Y; }
        }

        protected bool Closing;

        // Original tutorial-owned picture dialogs are placed and animated by
        // cTutorialIcon, not the shared dialog fade/centering path.
        internal bool TutorialPresentation;
        internal Action TutorialClose;
        internal float TutorialOpacity = 1;
        internal float OriginalOpacityMultiplier = 1;

        private float _i;
        public float InterpolatedAnimation
        {
            set
            {
                // R142: the original dialog has no wipe — desktop fades opacity and
                // keeps the centered box geometry the subclass computed.
                if (OriginalChrome)
                {
                    if (TutorialPresentation)
                    {
                        Opacity = TutorialOpacity;
                        _i = 1;
                        return;
                    }
                    Opacity = value * OriginalOpacityMultiplier;
                    _i = value;
                    Position = new Vector2((GameFacade.Screens.CurrentUIScreen.ScreenWidth - BoxWidth) / 2,
                                           (ScrHeight - Height) / 2);
                    if (value <= 0f && Closing) UIScreen.RemoveDialog(this);
                    return;
                }
                Position = new Vector2(0, (ScrHeight - Height) / 2);
                BackStripe.X = (Closing)?0:(Width * (1 - value));
                BackStripe.BodySize = new Point((int)(value * Width), ScrHeight);
                BackStripe.Y = -Position.Y;

                var t2 = Math.Max(0, value - 0.2f) / 0.8f;
                FrontStripe.X = (Closing)?(Width * (1 - t2)):0;
                FrontStripe.BodySize = new Point((int)(t2 * Width), Height);

                TitleBg.Y = 45 - 35 * t2;
                TitleLabel.Y = 15;
                TitleLabel.CaptionStyle.Color = UIStyle.Current.DialogTitle * t2;
                if (TitleTwin != null) TitleTwin.Color = UIStyle.Current.DialogTitle * t2;
                TitleBg.SetSize(Width, 70 * t2);
                _i = value;
                if (value == 0f && Closing) UIScreen.RemoveDialog(this);
            }

            get
            {
                return _i;
            }
        }

        public UIMobileDialog(bool useOriginalChrome = false) : base()
        {
            OriginalChrome = useOriginalChrome
                && !FSO.Common.FSOEnvironment.SoftwareKeyboard
                && Simitone.Client.UI.Controls.UIOriginalDialogChrome.Available();
            Width = GameFacade.Screens.CurrentUIScreen.ScreenWidth;
            ScrHeight = GameFacade.Screens.CurrentUIScreen.ScreenHeight;

            if (!OriginalChrome)
            {
                BackStripe = new UIDiagonalStripe(new Point(), UIDiagonalStripeSide.LEFT, new Color(0, 70, 140) * 0.33f);
                Add(BackStripe);
                FrontStripe = new UIDiagonalStripe(new Point(), UIDiagonalStripeSide.RIGHT, UIStyle.Current.DialogBg);
                Add(FrontStripe);

                TitleBg = new UIImage(Content.Get().CustomUI.Get("dialog_title_grad.png").Get(GameFacade.GraphicsDevice));
                TitleBg.SetSize(Width, 70);
                Add(TitleBg);
            }

            TitleLabel = new UILabel();
            TitleLabel.X = 50;
            TitleLabel.CaptionStyle = TitleLabel.CaptionStyle.Clone();
            TitleLabel.CaptionStyle.Size = 37;
            TitleLabel.CaptionStyle.Color = UIStyle.Current.DialogTitle;
            TitleLabel.Alignment = TextAlignment.Top | TextAlignment.Left;
            if (!OriginalChrome) Add(TitleLabel);   // R142: desktop hides the modern title label entirely

            InterpolatedAnimation = 0f;
            GameFacade.Screens.Tween.To(this, 0.5f, new Dictionary<string, float>() { { "InterpolatedAnimation", 1f} }, TweenQuad.EaseOut);
        }

        public override void GameResized()
        {
            base.GameResized();
            Width = GameFacade.Screens.CurrentUIScreen.ScreenWidth;
            ScrHeight = GameFacade.Screens.CurrentUIScreen.ScreenHeight;
            if (!OriginalChrome) TitleBg.SetSize(Width, 70);
            InterpolatedAnimation = InterpolatedAnimation;
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            // R142: the ORIGINAL frame + fill under everything on desktop. Children
            // (title/body/buttons) draw over it through base.Draw.
            if (OriginalChrome)
            {
                DrawOriginalWindow(batch);
            }
            base.Draw(batch);
        }

        protected virtual void DrawOriginalWindow(UISpriteBatch batch)
        {
            UIOriginalDialogChrome.DrawWindow(this, batch, 0, 0, BoxWidth, Height);
        }

        public void Close()
        {
            if (TutorialPresentation && TutorialClose != null)
            {
                TutorialClose();
                return;
            }
            if (!Closing)
            {
                Closing = true;
                if (OriginalChrome)
                {
                    GameFacade.Screens.Tween.To(this, 0.25f, new Dictionary<string, float>() { { "InterpolatedAnimation", 0f } }, TweenQuad.EaseIn);
                    return;
                }
                BackStripe.DiagSide = UIDiagonalStripeSide.RIGHT;
                FrontStripe.DiagSide = UIDiagonalStripeSide.LEFT;
                GameFacade.Screens.Tween.To(this, 0.3f, new Dictionary<string, float>() { { "InterpolatedAnimation", 0f } }, TweenQuad.EaseIn);
            }
        }

        public void SetHeight(int height)
        {
            Height = height;
            InterpolatedAnimation = _i;
        }
    }
}
