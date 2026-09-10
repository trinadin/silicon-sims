using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simitone.Client.UI.Controls;
using Simitone.Client.UI.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FSO.Common.Rendering.Framework.Model;

namespace Simitone.Client.UI.Panels
{
    public class UIMobileAlert : UIMobileDialog
    {
        private UIAlertOptions m_Options;

        // R205: gate-readable dialog content (the 162 About dialog pins its
        // title/message verbatim; uidialog covers titles via twin counters).
        public string TitleTextForProbe { get { return m_Options != null ? m_Options.Title : null; } }
        public string MessageTextForProbe { get { return m_Options != null ? m_Options.Message : null; } }
        private TextRendererResult m_MessageText;
        private TextStyle m_TextStyle;
        private readonly bool OriginalPictureDialogKind;
        private readonly double? TutorialAspect;
        private RenderTarget2D TutorialBuffer;
        private readonly List<RenderTarget2D> TutorialBuffers = new List<RenderTarget2D>();
        internal int TutorialBufferCountForProbe => TutorialBuffers.Count;

        private UIImage Icon;
        private UIImage IconStub;
        // The requested-size composition belongs to this alert. A caller may
        // separately transfer ownership of the source texture used to make it.
        private Texture2D OwnedOriginalIcon;
        private Texture2D OwnedOriginalSourceIcon;
        private Vector2 IconSpace;

        // cWinPictureDialog uses PopupInfoTiles.bmp (36x36 thirds), not GenDlg.
        // Its 12px client origin plus the decoded 9px inner gutter puts content
        // at outer-local (21,21); text yields imageWidth+18 on its first lines.
        private const int OriginalPictureFrameInset = UIOriginalDialogChrome.PictureBorder;
        private const int OriginalPictureInnerMargin = 9;
        private const int OriginalContentInset = OriginalPictureFrameInset + OriginalPictureInnerMargin;
        private const int OriginalImageGap = 18;
        private const int OriginalMessageLineHeight = 16;
        private const int OriginalMessageMaxTextWidth = 420;
        internal const double OriginalPictureTargetAspect = 2.0;
        internal const double OriginalPictureAspectTolerance = 0.05;
        internal static readonly Color OriginalPictureTitleColor = new Color(195, 205, 205);
        internal static readonly Color OriginalPictureBodyColor = new Color(195, 205, 205);

        private int OriginalTextWidth;
        private int OriginalBodyTop;
        private int OriginalBodyHeight;
        private int OriginalButtonTop;
        private int BodyTwinsHeight;

        private sealed class OriginalPictureLayout
        {
            public int BodyWidth;
            public int OuterWidth;
            public int OuterHeight;
            public int BodyTop;
            public int BodyHeight;
            public int ReservedWidth;
            public int ReservedHeight;
            public int LineHeight;
            public int ButtonTop;
        }

        private bool IsOriginalPictureDialog
        {
            get
            {
                // cWinPictureDialog is a constructor/class choice, not a state
                // inferred from whether somebody later supplies an image.
                return OriginalChrome && OriginalPictureDialogKind;
            }
        }

        private List<UIButton> Buttons;
        private UIButton TutorialCloseBox;
        internal UIButton TutorialCloseBoxForProbe => TutorialCloseBox;
        internal Action<byte> TutorialKeyResponse;
        internal byte TutorialEscapeResponse;
        internal bool TutorialSpacePrimary = true;

        internal void HandleTutorialKeys(UpdateState state)
        {
            if (TutorialKeyResponse == null || !Visible || !state.WindowFocused) return;
            // The topmost visible dialog owns these keys; typing a space in
            // an editor must continue to insert text.
            if (GameFacade.Screens.TopVisibleDialog != this) return;
            Microsoft.Xna.Framework.Input.Keys? selected = null;
            byte response = 0;
            if (state.NewKeys.Contains(Microsoft.Xna.Framework.Input.Keys.Escape))
            { selected = Microsoft.Xna.Framework.Input.Keys.Escape; response = TutorialEscapeResponse; }
            else if (state.NewKeys.Contains(Microsoft.Xna.Framework.Input.Keys.Enter))
                selected = Microsoft.Xna.Framework.Input.Keys.Enter;
            else if (TutorialSpacePrimary && state.InputManager?.GetFocus() == null && state.NewKeys.Contains(Microsoft.Xna.Framework.Input.Keys.Space))
                selected = Microsoft.Xna.Framework.Input.Keys.Space;
            if (!selected.HasValue) return;
            state.NewKeys.RemoveAll(x => x == selected.Value);
            TutorialKeyResponse(response);
        }
        private readonly Dictionary<UIButton, int> OriginalButtonWidths = new Dictionary<UIButton, int>();
        private UITextBox TextBox;

        // Gate-readable: message bodies whose lines render as ORIGINAL glyph
        // twins (desktop generic slot 11, picture slot 12, touch slot 9).
        public static int BodiesTwinned = 0;
        public static int BodyLinesTwinned = 0;
        private List<UIOriginalText> BodyTwins;

        internal UIImage OriginalIconForProbe { get { return Icon; } }
        internal UIImage OriginalIconStubForProbe { get { return IconStub; } }
        internal IReadOnlyList<UIOriginalText> OriginalBodyLinesForProbe { get { return BodyTwins; } }
        internal bool OriginalPictureLayoutForProbe { get { return IsOriginalPictureDialog; } }
        internal int OriginalTextWidthForProbe { get { return OriginalTextWidth; } }
        internal int OriginalBodyTopForProbe { get { return OriginalBodyTop; } }
        internal int OriginalBodyHeightForProbe { get { return OriginalBodyHeight; } }

        internal static bool OriginalPictureOutsideAspectTolerance(double ratio)
        {
            // Compare against the band endpoints directly. Math.Abs(ratio-2)
            // makes the exactly representable input literal 1.95 subtract to
            // 0.050000000000000044 in double precision and incorrectly rejects
            // the decoded inclusive lower boundary.
            return ratio < OriginalPictureTargetAspect - OriginalPictureAspectTolerance
                || ratio > OriginalPictureTargetAspect + OriginalPictureAspectTolerance;
        }

        public string ResponseText
        {
            get
            {
                return (TextBox == null) ? null : TextBox.CurrentText;
            }
            set
            {
                if (TextBox != null) TextBox.CurrentText = value;
            }
        }

        public UIMobileAlert(UIAlertOptions options, bool originalPictureDialog = false,
            double? tutorialAspect = null) : base(true)
        {
            this.m_Options = options;
            OriginalPictureDialogKind = originalPictureDialog;
            TutorialAspect = tutorialAspect;

            m_TextStyle = TextStyle.DefaultLabel.Clone();
            m_TextStyle.Size = 19;
            m_TextStyle.Color = Color.White;

            Caption = options.Title;
            if (IsOriginalPictureDialog)
            {
                // ObjectDialog constructs cWinPictureDialog even when no icon
                // was supplied. SetTitle always selects font_table[14]; tying
                // this upgrade to SetIcon made picture-less VM headlines fall
                // back to the tiny black generic/mobile title.
                SetOriginalTitleAppearance(
                    OriginalGlyphFont.LoadByIndex(14, GameFacade.GraphicsDevice),
                    OriginalPictureTitleColor);
            }

            IconStub = new UIImage();
            IconStub.Visible = false;
            Add(IconStub);

            Icon = new UIImage();
            Icon.Visible = false;
            Icon.Position = new Vector2(32, 32);
            Icon.SetSize(0, 0);
            Add(Icon);

            /** Determine the size **/
            ComputeText();

            /** Add buttons **/
            Buttons = new List<UIButton>();

            foreach (var button in options.Buttons)
            {
                string buttonText = "";
                if (button.Text != null) buttonText = button.Text;
                else buttonText = DefaultButtonCaption(button.Type);
                var btnElem = AddButton(buttonText, button.Type, button.Handler == null);
                Buttons.Add(btnElem);
                if (button.Handler != null) btnElem.OnButtonClick += button.Handler;
            }

            if (options.TextEntry)
            {
                TextBox = new UITextBox();
                TextBox.MaxChars = options.MaxChars;
                this.Add(TextBox);
            }

            /** Position buttons **/
            RefreshSize();
        }

        internal void AddTutorialCloseBox(bool owned, Action close)
        {
            if (!IsOriginalPictureDialog || TutorialCloseBox != null) return;
            var sheet = UIOriginal.EnsureResolvedByID(owned ? 31 : 30)?.Get(GameFacade.GraphicsDevice);
            TutorialCloseBox = new UIButton(sheet) { ImageStates = 4 };
            TutorialCloseBox.OnButtonClick += _ => close();
            Add(TutorialCloseBox);
            PositionTutorialCloseBox();
        }

        private void PositionTutorialCloseBox()
        {
            if (TutorialCloseBox != null)
                TutorialCloseBox.Position = new Vector2(BoxWidth-TutorialCloseBox.Size.X-5, 5);
        }

        // R204: system dialog buttons read STR# 152 'DefaultDialogButtons'
        // ([0] OK, [1] Cancel, [2] Yes, [3] No) — every typed-button alert
        // incl. the R197 native move-in AskDialogs. Object dialogs (phone
        // book, call neighbor, clothing/pet select) keep their ObjDialogs
        // 142 sourcing; see tools/iff-dump/r204/r204-str-family-law.md.
        public static string DefaultButtonCaption(UIAlertButtonType type)
        {
            switch (type)
            {
                case UIAlertButtonType.OK: return GameFacade.Strings.GetString("152", "0");
                case UIAlertButtonType.Yes: return GameFacade.Strings.GetString("152", "2");
                case UIAlertButtonType.No: return GameFacade.Strings.GetString("152", "3");
                case UIAlertButtonType.Cancel: return GameFacade.Strings.GetString("152", "1");
                default: return "";
            }
        }

        public override void GameResized()
        {
            base.GameResized();
            // Width is screen-relative, so both the hidden fallback and the
            // visible original-glyph lines must be rebuilt before auto-sizing.
            ComputeText();
            RefreshSize();
            InterpolatedAnimation = InterpolatedAnimation;
        }

        public void RefreshSize()
        {
            if (OriginalChrome)
            {
                if (IsOriginalPictureDialog)
                {
                    LayoutOriginalPictureDialog();
                    return;
                }

                int maxWindowWidth = Math.Max(200, Width - 80);
                int h;
                // cTSWinMsgBox's compact desktop composition. TextRenderer's
                // BoundingBox omits the final line height, so the mounted
                // original-glyph paragraph is authoritative when available.
                BoxWidth = Math.Max(200, Math.Min(maxWindowWidth,
                    OriginalTextWidth + 48));
                h = Math.Max(m_Options.Height,
                    OriginalBodyTop + OriginalBodyHeight + 16);

                if (Buttons.Count > 0) h += 33 + 32 + 8;
                else h += 16;

                if (m_Options.TextEntry)
                {
                    TextBox.X = 16;
                    TextBox.SetSize(BoxWidth - 32, 25);
                    h += 45;
                    TextBox.Y = h - 33 - 32 - 45;
                }
                OriginalButtonTop = 0;

                h = ResetButtons(h, true);
                if (Height != h) SetHeight(h);
                // BoxWidth can change without Height changing (notably after a
                // resize), so refresh the centered window position unconditionally.
                InterpolatedAnimation = InterpolatedAnimation;
                return;
            }

            var w2 = Width;
            var h2 = m_Options.Height;

            h2 = Math.Max(h2, Math.Max((int)IconSpace.Y - 25, m_MessageText == null ? 0 : m_MessageText.BoundingBox.Height) + 105);

            if (Buttons.Count > 0)
            {
                h2 += 175;
            }
            else
            {
                h2 += 32;
            }

            if (m_Options.TextEntry)
            {
                TextBox.X = 32;
                TextBox.Y = h2 - 54;
                TextBox.SetSize(w2 - 64, 25);
                h2 += 45;
            }

            h2 = ResetButtons(h2, true);

            if (Height != h2)
            {
                SetHeight(h2);
            }
            //update bg with height
        }

        private int ResetButtons(int h, bool setY)
        {
            if (OriginalChrome)
            {
                if (IsOriginalPictureDialog)
                {
                    // Picture-dialog buttons are laid out together with the
                    // adaptive body width. Do not run cTSWinMsgBox's uniform-row
                    // law over them afterward.
                    return h;
                }

                // R142: cTSWinMsgBox::PositionButtons @ 0x5234b0 — uniform maxW row,
                // spacing = (w - 32 - n*maxW)/(n+1) clamped to >= 16, first at
                // 16+spacing, bottom-aligned 32px inset.
                int n = Buttons.Count;
                if (n == 0) return h;
                int maxW = 65;
                foreach (var b in Buttons) maxW = Math.Max(maxW, (int)b.Width);
                foreach (var b in Buttons) b.Width = maxW;
                var lefts = Simitone.Client.UI.Controls.UIOriginalDialogChrome.ButtonLefts(BoxWidth, n, maxW);
                int top = Simitone.Client.UI.Controls.UIOriginalDialogChrome.ButtonTop(h, 33);
                for (int bi = 0; bi < n; bi++)
                {
                    Buttons[bi].X = lefts[bi];
                    if (setY) Buttons[bi].Y = top;
                    Buttons[bi].Visible = true;
                }
                return h;
            }
            var btnX = Width/2;
            var btnY = h - 125;
            var totalBtnWidth = Buttons.Sum(x => x.Width);
            int runningWidth = 0;
            int start = 0;
            int i = 0;
            for (i=0; i<Buttons.Count; i++)
            {
                var btn = Buttons[i];
                
                if (runningWidth == 0 || (Width-50) - runningWidth > btn.Width)
                {
                    btn.X = btnX + runningWidth;
                } else
                {
                    btnY += 120;
                    h += 120;
                    //center buttons
                    runningWidth -= 25;
                    for (int j=start; j<i; j++)
                    {
                        Buttons[j].X -= runningWidth / 2;
                    }

                    runningWidth = 0;
                    start = i;
                    btn.X = btnX + runningWidth;
                }
                runningWidth += (int)btn.Width + 25;
                if (setY) btn.Y = btnY;
            }
            runningWidth -= 25;
            for (int j = start; j < i; j++)
            {
                Buttons[j].X -= runningWidth / 2;
            }
            return h;
        }

        private float TargetIX;

        public void SetIcon(Texture2D img, int width, int height)
        {
            SetIcon(img, width, height, false);
        }

        internal void SetOwnedIcon(Texture2D img, int width, int height)
        {
            try
            {
                SetIcon(img, width, height, true);
            }
            catch
            {
                // takeOwnership is a strong transfer contract. If SetIcon
                // failed before recording the input, release it here; if the
                // alert already owns it, Removed() remains the single owner.
                if (img != null && !OwnsIconTexture(img)
                    && (Icon == null || !ReferenceEquals(Icon.Texture, img)))
                    img.Dispose();
                throw;
            }
        }

        public void SetIcon(Texture2D img, int width, int height, bool takeOwnership)
        {
            if (img == null || img.Height < 4 || img.Width < 1)
            {
                if (takeOwnership && img != null && !OwnsIconTexture(img)
                    && (Icon == null || !ReferenceEquals(Icon.Texture, img)))
                    img.Dispose();
                return;
            }

            // A desktop cTSWinMsgBox does not turn into cWinPictureDialog when
            // given a texture. Ignore accidental icon injection and release a
            // transferred input immediately; the generic layout stays generic.
            if (OriginalChrome && !IsOriginalPictureDialog)
            {
                bool inputWasOwned = OwnsIconTexture(img);
                ReleaseOwnedIconTextures();
                Icon.Texture = null;
                Icon.Visible = false;
                Icon.SetSize(0, 0);
                IconSpace = Vector2.Zero;
                if (IconStub != null) IconStub.Visible = false;
                if (takeOwnership && !inputWasOwned) img.Dispose();
                return;
            }

            // Preserve ownership if a caller remounts the exact texture already
            // owned by this alert, otherwise release both prior allocations.
            bool inputOwnedAlready = OwnsIconTexture(img);
            ReleaseOwnedIconTextures(img);
            if (takeOwnership || inputOwnedAlready) OwnedOriginalSourceIcon = img;

            // Desktop cWinPictureDialog paints the image immediately. Mobile keeps
            // its existing wipe-controlled reveal in Update().
            if (OriginalChrome) Icon.Visible = true;

            if (IsOriginalPictureDialog)
            {
                // cWinPictureDialog::SetTitle loads font-table slot 14 and never
                // calls cTSWinText::SetTextColor. InitSimsColors initializes that
                // font to RGB(195,205,205), so the black mobile-title style is not
                // inherited by the desktop picture dialog. Evidence: SetTitle
                // 0x2974b8/0x2974dc and 0x297514-0x2975a0; InitSimsColors
                // 0x25d67c-0x25d6a0 and 0x25d9fc-0x25da30.
                SetOriginalTitleAppearance(
                    OriginalGlyphFont.LoadByIndex(14, GameFacade.GraphicsDevice),
                    OriginalPictureTitleColor);
            }

            if (IsOriginalPictureDialog)
            {
                // PersonFinder supplies a native 45x45 picture even when the
                // port's cached head render is a larger source texture. The
                // requested cWinPictureDialog dimensions identify this branch;
                // testing the source dimensions dropped every live avatar icon.
                bool stubImage = width == 45 && height == 45;
                if (IconStub != null)
                {
                    IconStub.Visible = false;
                    if (stubImage)
                    {
                        try
                        {
                            IconStub.Texture = UIOriginal.EnsureResolved("cpanel\\SimStub.bmp")?.Get(GameFacade.GraphicsDevice);
                        }
                        catch { IconStub.Texture = null; }
                        if (IconStub.Texture != null)
                        {
                            IconStub.SetSize(IconStub.Texture.Width, IconStub.Texture.Height);
                            IconStub.Position = new Vector2(OriginalContentInset, OriginalContentInset);
                            IconStub.Visible = true;
                        }
                    }
                }

                Texture2D display = img;
                if (img.Width != width || img.Height != height)
                {
                    // The port's world thumbnail/head cache is a tight source,
                    // whereas Product::DrawIcon and PersonFinder hand the native
                    // dialog already-composed 120x120/45x45 buffers. Compose the
                    // requested buffer once; cWinPictureDialog paints it 1:1.
                    OwnedOriginalIcon = ComposeOriginalImage(img,
                        Math.Max(1, width), Math.Max(1, height));
                    display = OwnedOriginalIcon ?? img;
                }

                Icon.Texture = display;
                Icon.SetSize(display.Width, display.Height);
                IconSpace = stubImage
                    ? new Vector2(133, 103)
                    : new Vector2(display.Width, display.Height);
                Icon.Position = new Vector2(
                    OriginalContentInset + (IconSpace.X - Icon.Width) / 2f,
                    OriginalContentInset + (IconSpace.Y - Icon.Height) / 2f);
            }
            else
            {
                Icon.Texture = img;
                float scale = Math.Min(3, Math.Min((float)height / img.Height, (float)width / img.Width));
                if (scale * img.Height + 20 < height) height = (int)(scale * img.Height + 20);
                IconSpace = new Vector2(width + 30, height);
                Icon.SetSize(img.Width * scale, img.Height * scale);
                Icon.Position = new Vector2(50 + width / 2 - Icon.Width / 2,
                    110 + height / 2 - Icon.Height / 2);
            }
            TargetIX = Icon.Position.X;

            ComputeText();
            RefreshSize();
        }

        private bool OwnsIconTexture(Texture2D texture)
        {
            return texture != null && (ReferenceEquals(OwnedOriginalIcon, texture)
                || ReferenceEquals(OwnedOriginalSourceIcon, texture));
        }

        private void ReleaseOwnedIconTextures(Texture2D preserve = null)
        {
            var composed = OwnedOriginalIcon;
            var source = OwnedOriginalSourceIcon;
            OwnedOriginalIcon = null;
            OwnedOriginalSourceIcon = null;

            if (composed != null && !ReferenceEquals(composed, preserve))
                composed.Dispose();
            if (source != null && !ReferenceEquals(source, preserve)
                && !ReferenceEquals(source, composed))
                source.Dispose();
        }

        private Texture2D ComposeOriginalImage(Texture2D source, int width, int height)
        {
            var gd = GameFacade.GraphicsDevice;
            if (gd == null) return null;
            RenderTarget2D result = null;
            try
            {
                result = new RenderTarget2D(gd, width, height, false,
                    SurfaceFormat.Color, DepthFormat.None);
                lock (gd)
                {
                    var previous = gd.GetRenderTargets();
                    try
                    {
                        gd.SetRenderTarget(result);
                        gd.Clear(Color.Transparent);
                        using (var spriteBatch = new SpriteBatch(gd))
                        {
                            float scale = Math.Min(width / (float)source.Width,
                                height / (float)source.Height);
                            int drawWidth = Math.Max(1, (int)Math.Round(source.Width * scale));
                            int drawHeight = Math.Max(1, (int)Math.Round(source.Height * scale));
                            var destination = new Rectangle((width - drawWidth) / 2,
                                (height - drawHeight) / 2, drawWidth, drawHeight);
                            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                                SamplerState.LinearClamp, DepthStencilState.None,
                                RasterizerState.CullNone);
                            spriteBatch.Draw(source, destination, Color.White);
                            spriteBatch.End();
                        }
                    }
                    finally
                    {
                        if (previous.Length == 0) gd.SetRenderTarget(null);
                        else gd.SetRenderTargets(previous);
                    }
                }
                return result;
            }
            catch
            {
                if (result != null) result.Dispose();
                return null;
            }
        }

        /// <summary>
        /// Map of buttons attached to this message box.
        /// </summary>
        public Dictionary<UIAlertButtonType, UIButton> ButtonMap = new Dictionary<UIAlertButtonType, UIButton>();

        /// <summary>
        /// Adds a button to this message box.
        /// </summary>
        /// <param name="label">Label of the button.</param>
        /// <param name="type">Type of the button to be added.</param>
        /// <param name="InternalHandler">Should the button's click be handled internally?</param>
        /// <returns></returns>
        private UIButton AddButton(string label, UIAlertButtonType type, bool InternalHandler)
        {
            UIButton btn;
            if (OriginalChrome)
            {
                // R142: the ORIGINAL system push button — SMCtrlMgrRes id 17
                // shared\sys\WinBtn.bmp 260x33 = 4 states of 65x33. The engine
                // stretches one state buffer to the button rect; UIButton's
                // 4-state self-crop gives the 65x33 base, Width stretches it.
                var sheet = Simitone.Client.UI.Controls.UIOriginalDialogChrome.GetWinBtn();
                btn = new UIButton(sheet)
                {
                    Caption = label,
                    Visible = true
                };
                btn.CaptionStyle = btn.CaptionStyle.Clone();
                btn.CaptionStyle.Color = Microsoft.Xna.Framework.Color.White;
                btn.CaptionStyle.Size = 12;
                int maxW = Math.Max(65, label.Length * 9 + 24);   // uniform per-dialog width comes in ResetButtons
                btn.Width = maxW;
                OriginalButtonWidths[btn] = maxW;
            }
            else
            {
                btn = new UIBigButton(type == UIAlertButtonType.OK || type == UIAlertButtonType.Yes);
                btn.Visible = false;
                btn.Caption = label;
                if (btn.Width < 275) btn.Width = 275;
            }

            if (InternalHandler)
                btn.OnButtonClick += new ButtonClickDelegate(x =>
                {
                    HandleClose();
                });

            ButtonMap.Add(type, btn);

            this.Add(btn);
            return btn;
        }

        private void HandleClose()
        {
            Close();
        }

        private bool m_TextDirty = false;
        public override void CalculateMatrix()
        {
            base.CalculateMatrix();
            m_TextDirty = true;
        }

        private void ComputeText()
        {
            var message = m_Options.Message ?? "";
            var mountedBodyFont = OriginalChrome
                ? OriginalGlyphFont.LoadByIndex(11, GameFacade.GraphicsDevice)
                : OriginalGlyphFont.LoadDialog(GameFacade.GraphicsDevice);
            var mountedBodyColor = OriginalChrome
                ? UIMobileDialog.OriginalSystemTextColor
                : UIStyle.Current.DialogText;
            int mountedLineHeight = OriginalChrome && mountedBodyFont != null
                ? mountedBodyFont.LineHeight
                : OriginalMessageLineHeight;
            int margin;
            int bodyY;
            int wrapW;
            Vector2 wrapAround;

            if (IsOriginalPictureDialog)
            {
                // RefreshSize owns cWinPictureDialog's adaptive 2:1 layout.
                // Avoid feeding it through TextRenderer's screen-relative cap.
                m_TextDirty = false;
                return;
            }
            else if (OriginalChrome)
            {
                // cTSWinMsgBox::Init places both text controls at x=16. With
                // a title, the message begins eight pixels below its fitted
                // font[11] line; otherwise it starts at y=16.
                margin = 16;
                bodyY = HasOriginalTitle ? 16 + OriginalTitleHeight + 8 : 16;
                wrapW = Math.Max(1, Math.Min(OriginalMessageMaxTextWidth,
                    Width - 80 - margin * 2));
                wrapAround = Vector2.Zero;
                OriginalTextWidth = wrapW;
                OriginalBodyTop = bodyY;
                SetOriginalTitlePosition(new Vector2(16, 16));
            }
            else
            {
                // Preserve the mobile/touch composition: the visible original-
                // glyph twins still use the full body width, while the hidden
                // TextRenderer pass retains its historic icon wrap for sizing.
                margin = IconSpace.X > 0 ? 50 : 80;
                bodyY = 105;
                wrapW = Width - margin * 2;
                wrapAround = IconSpace;
            }

            m_MessageText = TextRenderer.ComputeText(message, new TextRendererOptions
            {
                Alignment = TextAlignment.Left | TextAlignment.Top,
                MaxWidth = wrapW,
                Position = new Microsoft.Xna.Framework.Vector2(margin, bodyY),
                Scale = _Scale,
                TextStyle = m_TextStyle,
                WordWrap = true,
                TopLeftIconSpace = wrapAround
            }, this);

            m_TextDirty = false;

            // Desktop cTSWinMsgBox's DefaultLabel body uses system font slot 0
            // (font_table[11]) and #C3CDCD. Touch keeps the established
            // variablesans_09/UIStyle composition.
            MountBodyTwins(margin, bodyY, wrapW,
                0, 0,
                mountedBodyFont,
                mountedBodyColor,
                mountedLineHeight);

            if (OriginalChrome)
            {
                int fallbackHeight = string.IsNullOrEmpty(message)
                    ? 0
                    : (m_MessageText == null ? 0
                        : m_MessageText.BoundingBox.Height + mountedLineHeight);
                OriginalBodyHeight = BodyTwins != null && BodyTwins.Count > 0
                    ? BodyTwinsHeight
                    : Math.Max((int)wrapAround.Y, fallbackHeight);
            }
        }

        private void LayoutOriginalPictureDialog()
        {
            var bodyFont = OriginalGlyphFont.LoadByIndex(12, GameFacade.GraphicsDevice);
            var titleFont = OriginalGlyphFont.LoadByIndex(14, GameFacade.GraphicsDevice);
            SetOriginalTitleAppearance(titleFont, OriginalPictureTitleColor);

            int lineHeight = bodyFont != null ? bodyFont.LineHeight : OriginalMessageLineHeight;
            int titleHeight = HasOriginalTitle
                ? (titleFont != null ? titleFont.LineHeight : Math.Max(1, OriginalTitleHeight))
                : 0;
            bool hasImage = IconSpace.X > 0 && IconSpace.Y > 0;
            int imageWidth = Math.Max(0, (int)IconSpace.X);
            int reservedWidth = hasImage ? imageWidth + OriginalImageGap : 0;

            // DoLayout starts from outer 300x185, which leaves B=258 after the
            // two (12+9) side gutters. TryLayout then enforces enough width for
            // the image/title and sum(buttonWidth+30) constraints.
            int bodyWidth = TutorialAspect.HasValue ? (hasImage ? imageWidth : 100) : 300 - 2 * OriginalContentInset;
            if (HasOriginalTitle)
            {
                // The title lower bound always keeps the decoded 18px lane
                // margin, even though the no-image body has no exclusion.
                int titleLane = hasImage ? reservedWidth : OriginalImageGap;
                bodyWidth = Math.Max(bodyWidth, titleLane + OriginalTitleWidth);
            }
            if (Buttons != null && Buttons.Count > 0)
            {
                int buttonMinimum = 0;
                foreach (var button in Buttons)
                {
                    int natural;
                    if (!OriginalButtonWidths.TryGetValue(button, out natural)) natural = (int)button.Width;
                    buttonMinimum += Math.Max(100, natural) + 30;
                }
                bodyWidth = Math.Max(bodyWidth, buttonMinimum);
            }

            int minimumWidth = bodyWidth;
            bodyWidth = Math.Max(300 - 2 * OriginalContentInset, bodyWidth);
            var low = EvaluateOriginalPictureLayout(bodyWidth, bodyFont, lineHeight, titleHeight);
            var best = low;
            double initialRatio = low.OuterWidth / (double)Math.Max(1, low.OuterHeight);
            double bestError = Math.Abs(initialRatio - OriginalPictureTargetAspect);

            // Original DoLayout doubles B until the 2:1 target is bracketed,
            // then performs at most ten midpoint trials (stopping at a <=20px
            // bracket). Preserve that search instead of imposing a screen cap.
            if (TutorialAspect.HasValue)
            {
                // Native DoLayout brackets in both directions. Short lessons
                // with only a closebox must be allowed to shrink below300px.
                double target = TutorialAspect.Value;
                Func<int, OriginalPictureLayout> evaluate = w => EvaluateOriginalPictureLayout(
                    Math.Max(minimumWidth, w), bodyFont, lineHeight, titleHeight);
                Func<OriginalPictureLayout, double> ratio = p => p.OuterWidth / (double)Math.Max(1, p.OuterHeight);
                if (Math.Abs(ratio(best)-target) > OriginalPictureAspectTolerance)
                {
                    int highWidth = bodyWidth, lowWidth = bodyWidth;
                    var high = best;
                    while (ratio(high) < target && highWidth < 16384)
                    { highWidth = high.BodyWidth * 2; high = evaluate(highWidth); }
                    low = evaluate(lowWidth);
                    for (int trial = 1; trial < 10 && ratio(low) > target; trial++)
                    { lowWidth = low.BodyWidth / 2; low = evaluate(lowWidth); }
                    for (int trial = 0; trial < 10 && highWidth-lowWidth > 20; trial++)
                    {
                        int middleWidth = (highWidth+lowWidth)/2;
                        var middle = evaluate(middleWidth);
                        if (ratio(middle) < target) { lowWidth = middle.BodyWidth; low = middle; }
                        else { highWidth = middle.BodyWidth; high = middle; }
                    }
                    best = Math.Abs(ratio(low)-target) < Math.Abs(ratio(high)-target) ? low : high;
                }
            }
            else if (OriginalPictureOutsideAspectTolerance(initialRatio)
                && initialRatio < OriginalPictureTargetAspect)
            {
                int lowWidth = bodyWidth;
                int highWidth = bodyWidth;
                OriginalPictureLayout high = low;
                do
                {
                    lowWidth = highWidth;
                    low = high;
                    highWidth = Math.Min(16384, highWidth * 2);
                    high = EvaluateOriginalPictureLayout(highWidth, bodyFont, lineHeight, titleHeight);
                    double error = Math.Abs(high.OuterWidth / (double)Math.Max(1, high.OuterHeight)
                        - OriginalPictureTargetAspect);
                    if (error < bestError) { best = high; bestError = error; }
                }
                while (highWidth < 16384
                    && high.OuterWidth / (double)Math.Max(1, high.OuterHeight)
                        < OriginalPictureTargetAspect);

                for (int iteration = 0; iteration < 10 && highWidth - lowWidth > 20; iteration++)
                {
                    int middleWidth = lowWidth + (highWidth - lowWidth) / 2;
                    var middle = EvaluateOriginalPictureLayout(middleWidth, bodyFont, lineHeight, titleHeight);
                    double ratio = middle.OuterWidth / (double)Math.Max(1, middle.OuterHeight);
                    double error = Math.Abs(ratio - OriginalPictureTargetAspect);
                    if (error < bestError) { best = middle; bestError = error; }
                    if (ratio < OriginalPictureTargetAspect) { lowWidth = middleWidth; low = middle; }
                    else { highWidth = middleWidth; high = middle; }
                }
            }

            OriginalTextWidth = best.BodyWidth;
            OriginalBodyTop = best.BodyTop;
            OriginalBodyHeight = best.BodyHeight;
            OriginalButtonTop = best.ButtonTop;
            BoxWidth = best.OuterWidth;

            Icon.Position = new Vector2(
                OriginalContentInset + ((int)IconSpace.X - Icon.Width) / 2f,
                OriginalContentInset + ((int)IconSpace.Y - Icon.Height) / 2f);
            TargetIX = Icon.X;

            if (HasOriginalTitle)
            {
                if (hasImage)
                {
                    int narrowWidth = best.BodyWidth - best.ReservedWidth;
                    SetOriginalTitlePosition(new Vector2(
                        OriginalPictureFrameInset + (int)IconSpace.X + OriginalImageGap
                            + (narrowWidth - OriginalTitleWidth) / 2f,
                        OriginalContentInset));
                }
                else
                {
                    // TryLayout's no-image branch centers the fitted title in
                    // the complete outer window, not in an image-relative lane.
                    SetOriginalTitlePosition(new Vector2(
                        (best.OuterWidth - OriginalTitleWidth) / 2f,
                        OriginalContentInset));
                }
            }

            MountBodyTwins(OriginalContentInset, best.BodyTop, best.BodyWidth,
                best.ReservedWidth, best.ReservedHeight, bodyFont,
                OriginalPictureBodyColor, best.LineHeight);

            if (m_Options.TextEntry && TextBox != null)
            {
                TextBox.X = OriginalContentInset;
                TextBox.Y = best.BodyTop + best.BodyHeight + OriginalPictureInnerMargin;
                TextBox.SetSize(best.BodyWidth, 36);
            }

            if (Buttons != null && Buttons.Count > 0)
            {
                int totalWidth = 0;
                foreach (var button in Buttons)
                {
                    int natural;
                    if (!OriginalButtonWidths.TryGetValue(button, out natural)) natural = (int)button.Width;
                    button.Width = Math.Max(100, natural);
                    totalWidth += (int)button.Width;
                }
                int spacing = (best.BodyWidth - totalWidth) / (Buttons.Count + 1);
                if (spacing < 0) spacing = OriginalPictureInnerMargin;
                int priorWidth = 0;
                for (int i = 0; i < Buttons.Count; i++)
                {
                    var button = Buttons[i];
                    button.X = OriginalContentInset + spacing * (i + 1) + priorWidth;
                    button.Y = best.ButtonTop;
                    button.Visible = true;
                    priorWidth += (int)button.Width;
                }
            }

            if (Height != best.OuterHeight) SetHeight(best.OuterHeight);
            else InterpolatedAnimation = InterpolatedAnimation;
            PositionTutorialCloseBox();
            m_TextDirty = false;
        }

        private OriginalPictureLayout EvaluateOriginalPictureLayout(
            int bodyWidth, OriginalGlyphFont bodyFont, int lineHeight, int titleHeight)
        {
            bodyWidth = Math.Max(1, bodyWidth);
            lineHeight = Math.Max(1, lineHeight);
            bool hasImage = IconSpace.X > 0 && IconSpace.Y > 0;
            int imageHeight = Math.Max(0, (int)IconSpace.Y);
            int reservedWidth = hasImage
                ? Math.Max(0, (int)IconSpace.X) + OriginalImageGap
                : 0;
            int rawReservedHeight = !hasImage ? 0 : (HasOriginalTitle
                ? Math.Max(0, imageHeight - titleHeight)
                : imageHeight + OriginalImageGap);
            int lowerReservedHeight = (rawReservedHeight / lineHeight) * lineHeight;
            int upperReservedHeight = lowerReservedHeight + lineHeight;
            int reservedHeight = (rawReservedHeight - lowerReservedHeight
                > upperReservedHeight - rawReservedHeight)
                ? upperReservedHeight
                : lowerReservedHeight;
            int bodyTop = HasOriginalTitle
                ? OriginalContentInset + titleHeight + OriginalImageGap
                : OriginalContentInset;

            var lines = WrapOriginalBodyLines(bodyFont, bodyWidth,
                reservedWidth, reservedHeight, lineHeight);
            int bodyHeight = Math.Max(reservedHeight, lines.Count * lineHeight);
            int cursor = bodyTop + bodyHeight;

            if (m_Options.TextEntry) cursor += OriginalPictureInnerMargin + 36;
            int buttonTop = 0;
            if (Buttons != null && Buttons.Count > 0)
            {
                buttonTop = cursor + OriginalImageGap;
                cursor = buttonTop + 33 + OriginalPictureInnerMargin;
            }
            else cursor += OriginalImageGap;

            return new OriginalPictureLayout
            {
                BodyWidth = bodyWidth,
                OuterWidth = bodyWidth + 2 * OriginalContentInset,
                OuterHeight = cursor + OriginalPictureFrameInset,
                BodyTop = bodyTop,
                BodyHeight = bodyHeight,
                ReservedWidth = reservedWidth,
                ReservedHeight = reservedHeight,
                LineHeight = lineHeight,
                ButtonTop = buttonTop
            };
        }

        private List<string> WrapOriginalBodyLines(OriginalGlyphFont font,
            int maxWidth, int reservedWidth, int reservedHeight, int lineHeight)
        {
            var lines = new List<string>();
            if (font == null || string.IsNullOrEmpty(m_Options.Message)) return lines;

            foreach (var para in m_Options.Message.Replace("\r\n", "\n").Split('\n'))
            {
                var line = "";
                foreach (var word in para.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var candidate = line.Length == 0 ? word : line + " " + word;
                    int lineWidth = maxWidth -
                        (lines.Count * lineHeight < reservedHeight ? reservedWidth : 0);
                    lineWidth = Math.Max(1, lineWidth);
                    if (line.Length == 0 || font.Measure(candidate) <= lineWidth) line = candidate;
                    else { lines.Add(line); line = word; }
                }
                lines.Add(line);
            }
            return lines;
        }

        private void MountBodyTwins(int bodyX, int bodyY, int maxWidth,
            int reservedWidth, int reservedHeight, OriginalGlyphFont font,
            Color color, int lineHeight)
        {
            if (BodyTwins != null)
            {
                foreach (var t in BodyTwins) Remove(t);
                BodyTwins = null;
            }
            BodyTwinsHeight = 0;

            if (font == null || font.Atlas == null || string.IsNullOrEmpty(m_Options.Message)) return;

            maxWidth = Math.Max(1, maxWidth);
            var lines = WrapOriginalBodyLines(font, maxWidth,
                reservedWidth, reservedHeight, lineHeight);
            BodyTwins = new List<UIOriginalText>();
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i] == "") continue;
                var twin = new UIOriginalText(lines[i], font) { Color = color };
                int lineOffset = i * lineHeight < reservedHeight
                    ? reservedWidth
                    : 0;
                twin.Position = new Vector2(bodyX + lineOffset,
                    bodyY + i * lineHeight);
                Add(twin);
                BodyTwins.Add(twin);
            }
            BodyTwinsHeight = Math.Max(reservedHeight,
                lines.Count * lineHeight);
            BodiesTwinned++;
            BodyLinesTwinned += BodyTwins.Count;
        }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            if (OriginalChrome) { Icon.Visible = Icon.Texture != null; return; }   // R142: no wipe on the original chrome
            var off = ((Closing) ? 1 : -1) * (1 - InterpolatedAnimation) * Width;
            var newIX = TargetIX + off;
            if (Icon.X != newIX)
            {
                Icon.Visible = true;
                Icon.X = newIX;
                ResetButtons(Height, false);
                foreach (var btn in Buttons)
                {
                    btn.X += off;
                    btn.Visible = true;
                }
            }
        }

        protected override void DrawOriginalWindow(UISpriteBatch batch)
        {
            if (IsOriginalPictureDialog)
                UIOriginalDialogChrome.DrawPictureWindow(this, batch, 0, 0, BoxWidth, Height);
            else
                base.DrawOriginalWindow(batch);
        }

        public override void Removed()
        {
            ReleaseOwnedIconTextures();
            foreach (var buffer in TutorialBuffers) buffer.Dispose();
            TutorialBuffers.Clear();
            TutorialBuffer = null;
            base.Removed();
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            if (OriginalChrome && OriginalOpacityMultiplier < 1 && TutorialBuffer != null)
            {
                batch.Draw(TutorialBuffer, Vector2.Zero, Color.White * Opacity);
                return;
            }
            DrawContents(batch);
        }

        public override void PreDraw(UISpriteBatch batch)
        {
            base.PreDraw(batch);
            if (Visible && OriginalChrome && OriginalOpacityMultiplier < 1)
                PrepareTutorialComposite(batch);
        }

        private void PrepareTutorialComposite(UISpriteBatch batch)
        {
            // Native cWinPictureDialog owns a private buffer. Children paint
            // opaque into it before one GetOpacity/BlitPrivateBufferToParent.
            // A viewport-sized transparent buffer preserves the exact physical
            // glyph positions, including fractional DPI, without moving controls.
            var gd = batch.GraphicsDevice;
            batch.Pause();
            var targets = gd.GetRenderTargets(); var viewport = gd.Viewport;
            var blend = gd.BlendState; var depth = gd.DepthStencilState;
            var rasterizer = gd.RasterizerState; var scissor = gd.ScissorRectangle;
            var sampler = gd.SamplerStates[0]; var texture = gd.Textures[0];
            var opacity = Opacity;
            try
            {
                if (TutorialBuffer == null || TutorialBuffer.Width != viewport.Width || TutorialBuffer.Height != viewport.Height)
                {
                    TutorialBuffer = new RenderTarget2D(gd, viewport.Width, viewport.Height,
                        false, SurfaceFormat.Color, DepthFormat.None);
                    // Keep old dimensions alive while the previous UI batch's
                    // texture binding can still reference them during restoration.
                    TutorialBuffers.Add(TutorialBuffer);
                }
                // Only the current target and a still-bound predecessor need
                // to survive. Repeated live resizes must not retain a full
                // viewport texture for every intermediate window dimension.
                foreach (var retired in TutorialBuffers.Where(buffer => buffer != TutorialBuffer &&
                    !ReferenceEquals(buffer, texture) && !targets.Any(binding => ReferenceEquals(binding.RenderTarget, buffer))).ToArray())
                {
                    TutorialBuffers.Remove(retired);
                    retired.Dispose();
                }
                gd.SetRenderTarget(TutorialBuffer); gd.Clear(Color.Transparent);
                Opacity = 1;
                PrimeOpacity(this);
                using (var offscreen = new UISpriteBatch(gd, 0))
                {
                    offscreen.UIBegin(BlendState.AlphaBlend, SpriteSortMode.Immediate);
                    DrawContents(offscreen);
                    offscreen.End();
                }
            }
            finally
            {
                Opacity = opacity;
                PrimeOpacity(this);
                gd.SetRenderTargets(targets); gd.Viewport=viewport; gd.ScissorRectangle=scissor;
                gd.BlendState=blend; gd.DepthStencilState=depth; gd.RasterizerState=rasterizer;
                gd.SamplerStates[0]=sampler; gd.Textures[0]=texture;
                batch.Resume();
            }
        }

        private static void PrimeOpacity(UIElement element)
        {
            _ = element.BlendColor;
            if (element is UIContainer container)
                foreach (var child in container.GetChildren()) PrimeOpacity(child);
        }

        private void DrawContents(UISpriteBatch batch)
        {
            var visibleBodyColor = IsOriginalPictureDialog
                ? OriginalPictureBodyColor
                : (OriginalChrome
                    ? UIMobileDialog.OriginalSystemTextColor
                    : UIStyle.Current.DialogText);
            m_TextStyle.Color = visibleBodyColor * InterpolatedAnimation;
            base.Draw(batch);

            if (m_TextDirty)
            {
                ComputeText();
            }

            // ROUND-112: with original .ffn body twins mounted, the modern TextRenderer
            // draw is suppressed (its ComputeText pass still sizes the dialog).
            if (BodyTwins == null || BodyTwins.Count == 0)
            {
                if (m_MessageText != null)
                    TextRenderer.DrawText(m_MessageText.DrawingCommands, this, batch);
            }
            else
            {
                var c = visibleBodyColor * InterpolatedAnimation;
                foreach (var t in BodyTwins) t.Color = c;
            }
        }
    }
}
