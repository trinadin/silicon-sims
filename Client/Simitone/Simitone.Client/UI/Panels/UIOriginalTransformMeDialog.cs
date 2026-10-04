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
    /// The original TransformMe form browser (TS1 dialog type 14,
    /// cWinTransformMeDlg), recovered from the PPC binary — ENG-11 decode
    /// (transformme-wizard-decode.md). Native law: 8 tabs (tab index wraps
    /// 0..7 on prev/next); tab → SAnimator::Outfit enum via LookupCostume
    /// keyed off the target's gender (pd[65]==1 female) and child iff
    /// 0 &lt; pd[58] &lt; 18 — adult male {48,58,44,45,39,46,47,40}, adult
    /// female {48,43,44,49,39,50,51,52}, child even tab 53 (boy) / 54 (girl)
    /// and odd tab 60. OK posts (form&lt;&lt;16)|0xFFFE, Cancel |0xFFFF; the
    /// tree's dialog consumes only the branch (TRUE/FALSE) with
    /// Temp0 = the outfit enum on BOTH edges — the pick never feeds any
    /// conversion natively (the wizard commits NOTHING; §C/§F of the decode),
    /// so this port surface is preview + answer only, byte-faithful.
    /// Fidelity notes (documented for review): the native previews the target
    /// live on cWinVitaBtnSolo (MakeNewOutOfWorldObject from the selector);
    /// the port has no in-UI VitaBoy, so the preview degrades to the target's
    /// name + the current outfit slot (disclosed on the card). The native's
    /// labels come from UIText table 273 rows 0-4 + GetButtonLabel(3) for OK;
    /// the port uses the tree's own STR#301 captions (info.Yes/info.Cancel),
    /// which carry the same localized text on every shipped caller.
    /// </summary>
    public class UIOriginalTransformMeDialog : UIContainer
    {
        public const int WindowW = 420, WindowH = 240;
        public override Vector2 Size { get => new Vector2(WindowW, WindowH); set { } }
        public override Rectangle GetBounds() => new Rectangle(0, 0, WindowW, WindowH);
        public const int MsgX = 26, MsgY = 20, MsgW = 368, MsgLines = 3;
        public const int PrevX = 60, NextX = 260, ArrowY = 100, ArrowW = 100;
        public const int OkX = 60, CancelX = 260, ButtonY = 180, ButtonW = 100;

        // probe surface (UI-35 gate)
        public static int DialogsMounted, Confirms, Cancels;
        public static int LastEnum = -1;
        public static string LastClass = "";
        public static UIOriginalTransformMeDialog LastMounted;

        /// <summary>null = cancelled; otherwise the picked SAnimator outfit enum.</summary>
        public event Action<int> OnResult;

        private readonly UIBigButton PrevButton, NextButton, OkButton, CancelButton;
        private readonly UIOriginalText OkLabel, CancelLabel, PreviewLine, TabLine;
        private readonly Vector2 OkLabelOrigin, CancelLabelOrigin;
        private readonly OriginalGlyphFont Font;
        private readonly bool Female, Child;
        private readonly string TargetName;
        private int Tab;

        /// <summary>
        /// LookupCostume (decode §B) as a pure function — tab 0..7 to the
        /// SAnimator::Outfit enum, keyed off the target class. Probe-pinned.
        /// </summary>
        public static int TabOutfit(int tab, bool female, bool child)
        {
            if (child)
            {
                // signed-parity idiom at 0x57d518-0x57d560 (r2-verified):
                // even tab -> boy 53 / girl 54; odd tab -> 60
                if ((tab & 1) != 0) return 60;
                return female ? 54 : 53;
            }
            var table = female
                ? new[] { 48, 43, 44, 49, 39, 50, 51, 52 }   // TOC[-17044], 0x57d5f0+8i
                : new[] { 48, 58, 44, 45, 39, 46, 47, 40 };  // TOC[-17040], 0x57d588+8i
            if (tab < 0 || tab > 7) return 0;                // cmplwi index,7; >7 -> 0
            return table[tab];
        }

        public UIOriginalTransformMeDialog(string title, string message, string okCaption,
            string cancelCaption, FSO.SimAntics.VMAvatar target)
        {
            Size = new Vector2(WindowW, WindowH);
            UpdatePosition();
            Font = OriginalGlyphFont.LoadByIndex(11, GameFacade.GraphicsDevice);

            // class from the REAL target (the native's charm-path StackElem quirk
            // is deliberately NOT replicated — decode §E, port wiring note 5)
            Female = target != null && (target.GetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.Gender) & 1) == 1;
            var age = (target != null) ? target.GetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.PersonsAge) : (short)0;
            Child = age > 0 && age < 18;
            TargetName = target?.Name ?? "";
            LastClass = (Child ? "child " : "adult ") + (Female ? "female" : "male");

            if (!string.IsNullOrEmpty(title))
            {
                var titleLabel = new UIOriginalText(title, Font)
                { Position = new Vector2(MsgX, 6), Color = new Color(195, 205, 205) };
                Add(titleLabel);
            }
            int y = MsgY;
            foreach (var line in Wrap(message ?? "", MsgW, MsgLines))
            {
                var label = new UIOriginalText(line, Font)
                { Position = new Vector2(MsgX, y), Color = new Color(195, 205, 205) };
                Add(label);
                y += Font.LineHeight + 2;
            }

            PrevButton = new UIBigButton(false) { Caption = "", Width = ArrowW, Position = new Vector2(PrevX, ArrowY) };
            NextButton = new UIBigButton(false) { Caption = "", Width = ArrowW, Position = new Vector2(NextX, ArrowY) };
            OkButton = new UIBigButton(false) { Caption = "", Width = ButtonW, Position = new Vector2(OkX, ButtonY) };
            CancelButton = new UIBigButton(false) { Caption = "", Width = ButtonW, Position = new Vector2(CancelX, ButtonY) };
            Add(PrevButton); Add(NextButton); Add(OkButton); Add(CancelButton);

            ArrowLabel("<", PrevButton);
            ArrowLabel(">", NextButton);
            if (string.IsNullOrEmpty(okCaption)) okCaption = "OK";           // native: GetButtonLabel(3)
            if (string.IsNullOrEmpty(cancelCaption)) cancelCaption = "Cancel"; // UIText 273 row 3
            OkLabel = ButtonLabel(okCaption, OkButton);
            CancelLabel = ButtonLabel(cancelCaption, CancelButton);
            OkLabelOrigin = OkLabel.Position;
            CancelLabelOrigin = CancelLabel.Position;

            PreviewLine = new UIOriginalText("", Font)
            { Position = new Vector2(MsgX, ArrowY + 38), Color = new Color(195, 205, 205) };
            TabLine = new UIOriginalText("", Font)
            { Position = new Vector2(MsgX, ArrowY + 12), Color = new Color(195, 205, 205) };
            Add(PreviewLine); Add(TabLine);

            PrevButton.OnButtonClick += _ => { Tab = (Tab <= 0) ? 7 : Tab - 1; Refresh(); }; // 0x57d7b0
            NextButton.OnButtonClick += _ => { Tab = (Tab >= 7) ? 0 : Tab + 1; Refresh(); }; // 0x57d800
            OkButton.OnButtonClick += _ => Submit();
            CancelButton.OnButtonClick += _ => Cancel();

            Refresh();
            LastMounted = this;
            DialogsMounted++;
        }

        private void Refresh()
        {
            var outfit = TabOutfit(Tab, Female, Child);
            TabLine.Text = "Form " + (Tab + 1) + " of 8";
            PreviewLine.Text = (TargetName == "" ? "" : TargetName + " — ") + "outfit slot " + outfit;
        }

        private UIOriginalText ArrowLabel(string caption, UIBigButton button)
        {
            var text = new UIOriginalText(caption, Font)
            { Position = button.Position + new Vector2((ArrowW - Font.Measure(caption)) / 2, Font.ButtonCaptionY(33)),
                Color = new Color(195, 205, 205) };
            Add(text);
            return text;
        }

        private UIOriginalText ButtonLabel(string caption, UIBigButton button)
        {
            var text = new UIOriginalText(caption, Font)
            { Position = button.Position + new Vector2((ButtonW - Font.Measure(caption)) / 2, Font.ButtonCaptionY(33)),
                Color = new Color(195, 205, 205) };
            Add(text);
            return text;
        }

        /// <summary>The current tab's outfit enum — the form value the native
        /// result carries on BOTH the confirm and the cancel edge (§D).</summary>
        public int CurrentEnum => TabOutfit(Tab, Female, Child);

        /// <summary>Probe/drive submit: the same path as the OK button
        /// (confirms the CURRENT tab's outfit enum).</summary>
        public void SubmitViaProbe()
        {
            Submit();
        }

        private void Submit()
        {
            Confirms++;
            LastEnum = TabOutfit(Tab, Female, Child);
            OnResult?.Invoke(LastEnum);
            UIScreen.RemoveDialog(this);
        }

        private void Cancel()
        {
            Cancels++;
            LastEnum = TabOutfit(Tab, Female, Child); // cancel also carries the form (decode §D)
            OnResult?.Invoke(-1);
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
            if (state.NewKeys.Contains(Keys.Escape)) Cancel();
            OkLabel.Color = OkButton.IsDown ? Color.Cyan : OkButton.Hovered ? Color.White : new Color(195, 205, 205);
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
