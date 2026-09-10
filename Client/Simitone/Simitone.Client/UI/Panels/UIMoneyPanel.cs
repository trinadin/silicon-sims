using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Common.Utils;
using Simitone.Client.UI.Model;
using Simitone.Client.UI.Screens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FSO.Common.Rendering.Framework.Model;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using FSO.Content;

namespace Simitone.Client.UI.Panels
{
    public class UIMoneyPanel : UIContainer
    {
        public int LastMoney = 0;
        private TS1GameScreen Game;
        private UILabel MoneyLabel;
        // R98: the ORIGINAL-styled money readout (bold .ffn table) — replaces
        // the modern label's drawing; the modern label stays for style fallback.
        private Simitone.Client.UI.Controls.UIOriginalText MoneyOriginal;
        private Texture2D Bg;

        public UIMoneyPanel(TS1GameScreen game) : base()
        {
            Game = game;
            LastMoney = GetMoney();

            MoneyLabel = new UILabel();
            MoneyLabel.CaptionStyle = MoneyLabel.CaptionStyle.Clone();
            MoneyLabel.CaptionStyle.Size = 15;
            MoneyLabel.CaptionStyle.Color = UIStyle.Current.Text;
            MoneyLabel.Alignment = FSO.Client.UI.Framework.TextAlignment.Center | FSO.Client.UI.Framework.TextAlignment.Middle;
            MoneyLabel.Size = new Microsoft.Xna.Framework.Vector2(128, 24);
            MoneyLabel.Visible = false;   // R98: drawn by the original glyph text below
            var boldFont = Simitone.Client.UI.Controls.OriginalGlyphFont.LoadBold(GameFacade.GraphicsDevice);
            if (boldFont != null)
            {
                MoneyOriginal = new Simitone.Client.UI.Controls.UIOriginalText("", boldFont);
                MoneyOriginal.Y = 4;
                Add(MoneyOriginal);
            }
            Add(MoneyLabel);

            Bg = Content.Get().CustomUI.Get("money_bg.png").Get(GameFacade.GraphicsDevice);

            UpdateMoneyDisplay();
        }

        // R115: money-change FLOATERS in original glyphs (the most-flashed modern text left;
        // the "+/-§n" composition is port-side — the original corpus has no '§' string — but
        // the GLYPHS are the original bold money table _12_bs, same as the readout twin).
        public static int FloatersTwinned = 0;

        public void DisplayChange(int change)
        {
            var text = ((change > 0) ? "+" : "-") + "§" + Math.Abs(change);
            if (MoneyOriginal != null && MoneyOriginal.Font != null && MoneyOriginal.Font.Atlas != null)
            {
                var twin = new Simitone.Client.UI.Controls.UIOriginalText(text, MoneyOriginal.Font)
                {
                    Color = (change > 0) ? UIStyle.Current.PosMoney : UIStyle.Current.NegMoney
                };
                twin.Y = -20f;
                twin.X = System.Math.Max(0, 128 - twin.Font.Measure(text)); // right-align in the 128px panel
                Add(twin);
                GameFacade.Screens.Tween.To(twin, 1.5f, new Dictionary<string, float>() { { "Y", -50 }, { "Opacity", 0 } });
                GameThread.SetTimeout(() => { Remove(twin); }, 1500);
                FloatersTwinned++;
                return;
            }
            var newLabel = new UILabel();
            newLabel.Y = -20f;
            newLabel.CaptionStyle = MoneyLabel.CaptionStyle.Clone();
            newLabel.CaptionStyle.Size = 15;
            newLabel.CaptionStyle.Color = (change > 0) ? UIStyle.Current.PosMoney : UIStyle.Current.NegMoney;
            newLabel.Alignment = FSO.Client.UI.Framework.TextAlignment.Right | FSO.Client.UI.Framework.TextAlignment.Middle;
            newLabel.Size = new Microsoft.Xna.Framework.Vector2(128, 24);

            newLabel.Caption = text;
            Add(newLabel);

            GameFacade.Screens.Tween.To(newLabel, 1.5f, new Dictionary<string, float>() { { "Y", -50 }, { "Opacity", 0 } });
            GameThread.SetTimeout(() => { Remove(newLabel); }, 1500);
        }

        private void UpdateMoneyDisplay()
        {
            var text = "§" + LastMoney.ToString("##,#0");
            MoneyLabel.Caption = text;
            if (MoneyOriginal != null)
            {
                MoneyOriginal.Text = text;
                // center inside the 128px panel like the old label was
                MoneyOriginal.X = System.Math.Max(0, (128 - MoneyOriginal.Font.Measure(text)) / 2);
                Simitone.Client.UI.Controls.UIOriginalText.MoneyUpdatesDrawn++;
            }
        }

        private int GetMoney()
        {
            return Game.ActiveFamily?.Budget ?? 0;
        }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            Visible = Game.LotControl.ActiveEntity != null;
            var money = GetMoney();
            if (LastMoney != money)
            {
                DisplayChange(money - LastMoney);
                LastMoney = money;
                UpdateMoneyDisplay();
            }
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            DrawLocalTexture(batch, Bg, new Rectangle(0, 0, 12, 24), Vector2.Zero, Vector2.One, UIStyle.Current.Bg);
            DrawLocalTexture(batch, Bg, new Rectangle(12, 0, 12, 24), new Vector2(12, 0), new Vector2(8.666667f, 1), UIStyle.Current.Bg);
            DrawLocalTexture(batch, Bg, new Rectangle(24, 0, 12, 24), new Vector2(116, 0), Vector2.One, UIStyle.Current.Bg);
            base.Draw(batch);
        }
    }
}
