using FSO.Client;
using FSO.Client.UI.Framework;
using FSO.Common.Rendering.Framework.Model;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Simitone.Client.UI.Controls;
using Simitone.Client.UI.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simitone.Client.UI.Panels
{
    /// <summary>
    /// The original single-line name entry for TS1 TextEntry dialogs (dialog
    /// type 3), on the CAS-02 UIOriginalPersonNameBox idiom. Native law
    /// (EXP-08 fix card 1, displayfloorpetpen.iff 4113 @7/@8): the pet pen's
    /// 'Buy a Pet' asks 'Please give your new pet a name:' (STR#301 logical
    /// [2], 1-based id 3) through dialog_private type 3 — the response text
    /// lands on the StackObject's name, and make_new_character (VMTS1MakeNewCharacter
    /// line 'info.Name = context.StackObject.Name') names the adopted pet
    /// from it. The engine side of that law already existed; this is the
    /// missing original-editor surface (desktop; touch keeps the alert).
    /// </summary>
    public class UIOriginalNameEntryDialog : UIContainer
    {
        public const int WindowW = 420, WindowH = 210;
        public override Vector2 Size { get => new Vector2(WindowW, WindowH); set { } }
        public override Rectangle GetBounds() => new Rectangle(0, 0, WindowW, WindowH);
        public const int BoxX = 30, BoxY = 118, BoxW = 360, BoxH = 25;
        public const int OkX = 60, CancelX = 260, ButtonY = 155, ButtonW = 100;
        public const int MsgX = 26, MsgY = 20, MsgW = 368, MsgLines = 4;

        // probe surface (EXP-08 fix card 1 gate)
        public static int DialogsMounted, Submits;
        public static string LastSubmitted = "";
        public static UIOriginalNameEntryDialog LastMounted;

        /// <summary>null = cancelled; otherwise the typed name.</summary>
        public event Action<string> OnResult;
        private readonly Simitone.Client.UI.Panels.CAS.UIOriginalPersonNameBox NameBox;
        private readonly UIBigButton OkButton, CancelButton;
        private readonly UIOriginalText OkLabel, CancelLabel;
        private readonly Vector2 OkLabelOrigin, CancelLabelOrigin;
        private readonly OriginalGlyphFont Font;

        public UIOriginalNameEntryDialog(string title, string message, string okCaption, string cancelCaption)
        {
            Size = new Vector2(WindowW, WindowH);
            UpdatePosition();
            Font = OriginalGlyphFont.LoadByIndex(11, GameFacade.GraphicsDevice);

            if (!string.IsNullOrEmpty(title))
            {
                var titleLabel = new UIOriginalText(title, Font)
                { Position = new Vector2(MsgX, 6), Color = new Color(195, 205, 205) };
                Add(titleLabel);
            }
            int y = MsgY;
            foreach (var line in Wrap(message ?? "", MsgW, 3))
            {
                var label = new UIOriginalText(line, Font)
                { Position = new Vector2(MsgX, y), Color = new Color(195, 205, 205) };
                Add(label);
                y += Font.LineHeight + 2;
            }

            // CAS-02 single-line law (r143 §3.3): transparent frame, capacity
            // 25 (16 when the language byte is 15), font-table slot 10.
            NameBox = new Simitone.Client.UI.Panels.CAS.UIOriginalPersonNameBox() { Position = new Vector2(BoxX, BoxY) };
            NameBox.SetSize(BoxW, BoxH);
            NameBox.MaxLines = 1;
            NameBox.MaxChars = (int)FSO.Files.Formats.IFF.Chunks.STR.DefaultLangCode == 15 ? 16 : 25;
            NameBox.BackgroundTextureReference = null;
            NameBox.TextMargin = new Rectangle(2, 2, 2, 2);
            NameBox.TextStyle = NameBox.TextStyle.Clone();
            NameBox.TextStyle.Color = Color.White;
            NameBox.TextStyle.Size = 10;
            NameBox.OnReturn += () => Submit();
            Add(NameBox);

            OkButton = new UIBigButton(false) { Caption = "", Width = ButtonW, Position = new Vector2(OkX, ButtonY) };
            CancelButton = new UIBigButton(false) { Caption = "", Width = ButtonW, Position = new Vector2(CancelX, ButtonY) };
            Add(OkButton);
            Add(CancelButton);
            if (string.IsNullOrEmpty(okCaption)) okCaption = "OK";
            if (string.IsNullOrEmpty(cancelCaption)) cancelCaption = "Cancel";
            OkLabel = ButtonLabel(okCaption, OkButton);
            CancelLabel = ButtonLabel(cancelCaption, CancelButton);
            OkLabelOrigin = OkLabel.Position;
            CancelLabelOrigin = CancelLabel.Position;
            OkButton.OnButtonClick += _ => Submit();
            CancelButton.OnButtonClick += _ => Cancel();
            OkButton.Disabled = true;

            LastMounted = this;
            DialogsMounted++;
        }

        private UIOriginalText ButtonLabel(string caption, UIBigButton button)
        {
            var text = new UIOriginalText(caption, Font)
            { Position = button.Position + new Vector2((ButtonW - Font.Measure(caption)) / 2, Font.ButtonCaptionY(33)),
                Color = new Color(195, 205, 205) };
            Add(text);
            return text;
        }

        /// <summary>Probe/drive submit: the same path as the OK button and
        /// Enter (OnResult with the current text).</summary>
        public void SubmitViaProbe(string text)
        {
            if (!string.IsNullOrEmpty(text)) NameBox.CurrentText = text;
            Submit();
        }

        private void Submit()
        {
            // gate on the TEXT (not the button's stale Disabled state — a probe
            // submit between frames sets the text after the last Update)
            if (string.IsNullOrEmpty(NameBox.CurrentText)) return;
            Submits++;
            LastSubmitted = NameBox.CurrentText ?? "";
            OnResult?.Invoke(LastSubmitted);
            UIScreen.RemoveDialog(this);
        }

        private void Cancel()
        {
            OnResult?.Invoke(null);
            UIScreen.RemoveDialog(this);
        }

        private List<string> Wrap(string text, int width, int maxLines)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text)) return lines;
            var current = "";
            foreach (var word in text.Replace("\r\n", "\n").Split('\n'))
            {
                foreach (var part in word.Split(' '))
                {
                    var candidate = current == "" ? part : current + " " + part;
                    if (Font.Measure(candidate) <= width || current == "") current = candidate;
                    else { lines.Add(current); current = part; }
                }
                if (current != "") lines.Add(current);
                current = "";
                if (lines.Count >= maxLines) break;
            }
            return lines;
        }

        public void UpdatePosition()
        {
            var screen = GameFacade.Screens.CurrentUIScreen;
            Position = new Vector2((screen.ScreenWidth - WindowW) / 2f, (screen.ScreenHeight - WindowH) / 2f);
        }
        public override void GameResized() { UpdatePosition(); base.GameResized(); }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            OkButton.Disabled = string.IsNullOrEmpty(NameBox.CurrentText);
            if (state.NewKeys.Contains(Keys.Escape)) Cancel();
            // focus the editor on mount (click-to-refocus is the box's own)
            if (state.InputManager != null && state.InputManager.GetFocus() == null)
                state.InputManager.SetFocus(NameBox);
            OkLabel.Color = OkButton.Disabled ? new Color(64, 93, 95)
                : OkButton.IsDown ? Color.Cyan : OkButton.Hovered ? Color.White : new Color(195, 205, 205);
            CancelLabel.Color = CancelButton.IsDown ? Color.Cyan : CancelButton.Hovered ? Color.White : new Color(195, 205, 205);
            OkLabel.Position = OkLabelOrigin + (OkButton.IsDown ? new Vector2(2, 2) : Vector2.Zero);
            CancelLabel.Position = CancelLabelOrigin + (CancelButton.IsDown ? new Vector2(2, 2) : Vector2.Zero);
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            UIOriginalDialogChrome.DrawPictureWindow(this, batch, 0, 0, WindowW, WindowH);
            base.Draw(batch);
        }
    }
}
