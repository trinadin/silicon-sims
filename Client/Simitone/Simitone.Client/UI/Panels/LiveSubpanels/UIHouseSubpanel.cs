using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Common.Rendering.Framework.IO;
using FSO.Common.Rendering.Framework.Model;
using Microsoft.Xna.Framework;
using Simitone.Client.UI.Controls;
using Simitone.Client.UI.Model;
using Simitone.Client.UI.Screens;
using System;

namespace Simitone.Client.UI.Panels.LiveSubpanels
{
    /// <summary>Native text-only cTSWinBtn used by all nine House items.</summary>
    public class UIOriginalHouseTextButton : UIContainer
    {
        public static readonly Color NormalColor = new Color(0xC3, 0xCD, 0xCD, 0xFF);
        public static readonly Color SelectedColor = new Color(0x00, 0xFF, 0xFF, 0xFF);
        public static readonly Color HoverColor = Color.White;
        public static readonly Color DisabledColor = new Color(0x40, 0x5D, 0x5F, 0xFF);
        public UIOriginalText Label;
        public Action Activated;
        public bool SelectedState;
        public bool DisabledState;
        private bool Down;
        private bool Hover;
        private Vector2 _Size;

        public override Vector2 Size { get { return _Size; } set { _Size = value; } }

        public UIOriginalHouseTextButton(string caption, OriginalGlyphFont font, int width, Action activated)
        {
            Activated = activated;
            Size = new Vector2(width, 16);
            Label = new UIOriginalText(caption ?? "", font)
            {
                Size = Size,
                Color = NormalColor
            };
            Add(Label);
            ListenForMouse(new Rectangle(0, 0, width, 16), Mouse);
            FSO.Client.Utils.UIUtils.GiveTooltip(this);
        }

        private void Mouse(UIMouseEventType type, UpdateState state)
        {
            if (DisabledState) return;
            if (type == UIMouseEventType.MouseOver) Hover = true;
            else if (type == UIMouseEventType.MouseDown) Down = true;
            else if (type == UIMouseEventType.MouseOut)
            {
                Hover = false;
                Down = false;
            }
            else if (type == UIMouseEventType.MouseUp)
            {
                if (Down) Activated?.Invoke();
                Down = false;
            }
            ApplyStateColor();
            Invalidate();
        }

        public void SetCaption(string caption, bool rightAlign)
        {
            Label.Text = caption ?? "";
            Label.Position = new Vector2(rightAlign && Label.Font != null
                ? Math.Max(0, Size.X - Label.Font.Measure(Label.Text)) : 0, 0);
            ApplyStateColor();
        }

        private void ApplyStateColor()
        {
            Label.Color = DisabledState ? DisabledColor
                : (SelectedState || Down) ? SelectedColor
                : Hover ? HoverColor : NormalColor;
        }
    }

    /// <summary>
    /// R184: cWinSubpanelHouse's exact 427x100 English composition. The modern
    /// UILabel fields remain hidden state holders for existing callers; every
    /// visible caption is a native .ffn text button at the executable's rect.
    /// </summary>
    public class UIHouseSubpanel : UISubpanel
    {
        // Compatibility/state-holder fields used by existing checks and data code.
        public UILabel Header;
        public UILabel SqFtLine;
        public UILabel BedLine;
        public UILabel BathLine;
        public UILabel LotLine;
        public UILabel[] ScoreLabels;
        public UIOriginalRatingBar[] ScoreBars;

        public UIOriginalText HeaderTwin;
        public UIOriginalHouseTextButton[] ScoreButtons;
        public UIOriginalHouseTextButton[] FactButtons;
        public UIOptionAboutPopup CurrentPopup;
        public int SelectedItem = -1;
        public int PopupItem = -1;

        public OriginalHouseStats.Result Stats;

        public UIHouseSubpanel(TS1GameScreen game) : base(game)
        {
            var gd = GameFacade.GraphicsDevice;
            var titleFont = OriginalGlyphFont.LoadByIndex(11, gd);
            var itemFont = OriginalGlyphFont.LoadByIndex(8, gd);

            Header = StateLabel(OriginalLiveStrings.Entry(133, 0) ?? "House");
            SqFtLine = StateLabel(OriginalLiveStrings.Entry(133, 6) ?? "Sq. Ft.: %d");
            BedLine = StateLabel(OriginalLiveStrings.Entry(133, 7) ?? "Bedrooms: %d");
            BathLine = StateLabel(OriginalLiveStrings.Entry(133, 8) ?? "Bathrooms: %d");
            LotLine = StateLabel(OriginalLiveStrings.Entry(133, 9) ?? "Lot: %s");

            HeaderTwin = new UIOriginalText(Header.Caption, titleFont)
            {
                Position = new Vector2(5, 0),
                Size = new Vector2(95, 20)
            };
            Add(HeaderTwin);

            // Five right-aligned score captions share right edge x=154.
            ScoreLabels = new UILabel[5];
            ScoreButtons = new UIOriginalHouseTextButton[5];
            ScoreBars = new UIOriginalRatingBar[5];
            int[] labelY = { 7, 25, 43, 61, 79 };
            int[] barY = { 6, 24, 42, 60, 79 };
            for (int i = 0; i < 5; i++)
            {
                var index = i;
                var caption = OriginalLiveStrings.Entry(133, 1 + i) ?? "?";
                var captionWidth = Math.Max(1, itemFont?.Measure(caption) ?? caption.Length * 7);
                ScoreLabels[i] = StateLabel(caption);
                var button = new UIOriginalHouseTextButton(caption, itemFont, captionWidth, () => SelectItem(index))
                {
                    // Native creates the English score controls at x=85, then
                    // moves each measured caption so its right edge is x=154.
                    // The hit window is that measured rectangle, not the full
                    // 154-pixel lane to its left.
                    Position = new Vector2(154 - captionWidth, labelY[i]),
                    Tooltip = OriginalLiveStrings.Entry(135, i * 2)
                };
                button.SetCaption(caption, true);
                Add(button);
                ScoreButtons[i] = button;

                var bar = UIOriginalRatingBar.HouseBar();
                bar.Position = new Vector2(157, barY[i]);
                Add(bar);
                ScoreBars[i] = bar;
            }

            // Four 80x16 fact controls at y 18/38/58/78.
            var facts = new[] { SqFtLine, BedLine, BathLine, LotLine };
            FactButtons = new UIOriginalHouseTextButton[4];
            for (int i = 0; i < 4; i++)
            {
                var index = 5 + i;
                var button = new UIOriginalHouseTextButton(facts[i].Caption, itemFont, 80, () => SelectItem(index))
                {
                    Position = new Vector2(0, 18 + i * 20),
                    Tooltip = OriginalLiveStrings.Entry(135, index * 2)
                };
                button.SetCaption(facts[i].Caption, false);
                Add(button);
                FactButtons[i] = button;
            }

            RecomputeStats();
        }

        private UILabel StateLabel(string caption)
        {
            var label = new UILabel { Caption = caption, Visible = false };
            Add(label);
            return label;
        }

        public override void Update(UpdateState state)
        {
            // Native calls House::GetHouseStats every paint. Recompute every
            // update so construction/renovation is reflected without a lag.
            RecomputeStats();
            base.Update(state);
        }

        public void RecomputeStats()
        {
            Stats = OriginalHouseStats.Compute(Game.vm);
            var fmt = OriginalLiveStrings.Entry;
            SqFtLine.Caption = Format(fmt(133, 6) ?? "Sq. Ft.: %d", "%d", Stats.SquareFeet.ToString());
            BedLine.Caption = Format(fmt(133, 7) ?? "Bedrooms: %d", "%d", Stats.Bedrooms.ToString());
            BathLine.Caption = Format(fmt(133, 8) ?? "Bathrooms: %d", "%d", Stats.Bathrooms.ToString());
            var lotWord = fmt(138, Stats.LotSizeIndex) ?? "?";
            LotLine.Caption = Format(fmt(133, 9) ?? "Lot: %s", "%s", lotWord);

            if (FactButtons != null)
            {
                FactButtons[0].SetCaption(SqFtLine.Caption, false);
                FactButtons[1].SetCaption(BedLine.Caption, false);
                FactButtons[2].SetCaption(BathLine.Caption, false);
                FactButtons[3].SetCaption(LotLine.Caption, false);
            }

            // The five decoded native live scores.
            ScoreBars[0].Value = Stats.SizeScore;
            ScoreBars[1].Value = Stats.FurnishingsScore;
            ScoreBars[2].Value = Stats.YardScore;
            ScoreBars[3].Value = Stats.UpkeepScore;
            ScoreBars[4].Value = Stats.LayoutScore;
            Invalidate();
        }

        private static string Format(string format, string token, string value)
        {
            return (format ?? "").Replace(token, value ?? "");
        }

        public void SelectItem(int index)
        {
            if (index < 0 || index > 8) return;
            SelectedItem = index;
            for (int i = 0; i < ScoreButtons.Length; i++)
            {
                ScoreButtons[i].SelectedState = i == index;
                ScoreButtons[i].SetCaption(ScoreLabels[i].Caption, true);
            }
            for (int i = 0; i < FactButtons.Length; i++)
            {
                FactButtons[i].SelectedState = 5 + i == index;
                FactButtons[i].SetCaption(new[] { SqFtLine.Caption, BedLine.Caption, BathLine.Caption, LotLine.Caption }[i], false);
            }
            OpenPopup(index);
            Invalidate();
        }

        private void OpenPopup(int index)
        {
            var row = new UIOriginalOptionsPanel.OptRow
            {
                Caption = index < 5 ? ScoreLabels[index].Caption
                    : new[] { SqFtLine.Caption, BedLine.Caption, BathLine.Caption, LotLine.Caption }[index - 5],
                AboutTitle = OriginalLiveStrings.Entry(135, index * 2) ?? "",
                AboutBody = OriginalLiveStrings.Entry(135, index * 2 + 1) ?? ""
            };
            PopupItem = index;
            if (CurrentPopup == null)
            {
                CurrentPopup = new UIOptionAboutPopup(null, row, null);
                DynamicOverlay.Add(CurrentPopup);
            }
            // cWinSubpanelHouse::Init passes null for both selectors, both
            // bitmaps, and both special scales when it constructs this client.
            // RebuildBuffer therefore takes the full-width no-pane branch.
            CurrentPopup.SetContent(row.AboutTitle, row.AboutBody, null, "");
            CurrentPopup.Position = new Vector2(Size.X - CurrentPopup.Size.X, -CurrentPopup.Size.Y);
            CurrentPopup.CanonPos = new Point((int)CurrentPopup.X, (int)CurrentPopup.Y);
        }

        public override void GameResized()
        {
            base.GameResized();
            if (CurrentPopup == null) return;
            CurrentPopup.Position = new Vector2(Size.X - CurrentPopup.Size.X, -CurrentPopup.Size.Y);
            CurrentPopup.CanonPos = new Point((int)CurrentPopup.X, (int)CurrentPopup.Y);
        }

        public override void Kill()
        {
            if (CurrentPopup != null)
            {
                DynamicOverlay.Remove(CurrentPopup);
                CurrentPopup = null;
            }
            PopupItem = -1;
            base.Kill();
        }
    }
}
