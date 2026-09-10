using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Content;
using Simitone.Client.UI.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

namespace Simitone.Client.UI.Controls
{
    /// <summary>
    /// ROUND-112: the dialog-family big button renders its caption in the ORIGINAL
    /// variablesans_09 .ffn table. UIButton.Draw's modern caption is suppressed
    /// per-frame (its draw guard requires CaptionStyle non-null - nulled ONLY around
    /// the base call, restored immediately so every other CaptionStyle reader, e.g.
    /// the internal measure, still sees it) and the caption is re-drawn centered in
    /// original glyphs. UIBigButton is used only by dialog-family panels
    /// (UIMobileAlert, UIHouseSelectPanel, UICallNeighborAlert, UISelectSkinAlert),
    /// and the label strings themselves already come from the original ObjDialogs
    /// string table ("142" -> OK/Cancel/Yes/No). Pre-IFF fallback: if the .ffn font
    /// is not yet mounted, the modern caption renders unchanged.
    ///
    /// ROUND-196 (Tier B item 1): on the DESKTOP path the button ART is the
    /// ORIGINAL system push button — SMCtrlMgrRes id 17 shared\sys\WinBtn.bmp
    /// 260x33 = 4 states of 65x33 (normal/hilite/pressed/disabled), drawn by
    /// UIButton's own 3-slice stretch (the R142 dialog law; the engine stretches
    /// one state buffer to the button rect). The original has ONE system button
    /// art: the 'green' variant keeps its caption emphasis only (glyph captions
    /// are White on WinBtn per the alert law). Touch keeps the mobile pngs.
    /// </summary>
    public class UIBigButton : UIButton
    {
        // gate-readable: caption draws that rendered original glyphs
        public static long OriginalCaptionsDrawn = 0;
        // R196: desktop buttons mounted on the original WinBtn sheet
        public static long WinBtnButtonsMounted = 0;
        public bool UsingWinBtn;

        public UIBigButton(bool green) : base()
        {
            CaptionStyle = CaptionStyle.Clone();
            CaptionStyle.Size = 37;
            CaptionStyle.Color = (green) ? UIStyle.Current.GreenBtnTxt : UIStyle.Current.BtnTxt;
            CaptionStyle.DisabledColor = UIStyle.Current.BtnDisable;
            Texture = Content.Get().CustomUI.Get(green ? "greenbutton.png" : "button.png").Get(GameFacade.GraphicsDevice);
            if (!FSO.Common.FSOEnvironment.SoftwareKeyboard)
            {
                var sheet = Simitone.Client.UI.Controls.UIOriginalDialogChrome.GetWinBtn();
                if (sheet != null)
                {
                    Texture = sheet;
                    ImageStates = 4;
                    CaptionStyle.Color = Microsoft.Xna.Framework.Color.White;
                    UsingWinBtn = true;
                    WinBtnButtonsMounted++;
                }
            }
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            var font = OriginalGlyphFont.LoadDialog(GameFacade.GraphicsDevice);
            if (font == null || font.Atlas == null || string.IsNullOrEmpty(Caption))
            {
                base.Draw(batch);
                return;
            }
            var keep = CaptionStyle;
            CaptionStyle = null; // suppress the modern caption draw only (UIButton draw guard)
            try { base.Draw(batch); }
            finally { CaptionStyle = keep; }
            var w = font.Measure(Caption);
            var b = GetBounds();
            var x = (b.Width - w) / 2f;
            var y = (b.Height - 13) / 2f;
            float cx = 0;
            foreach (var ch in Caption)
            {
                OriginalGlyphFont.Glyph g;
                if (font.ByChar.TryGetValue(ch, out g) && g.W > 1)
                {
                    DrawLocalTexture(batch, font.Atlas,
                        new Microsoft.Xna.Framework.Rectangle(g.U, g.V, g.W, g.H),
                        new Vector2(x + cx, y + font.GlyphY(g)), Vector2.One, keep.Color);
                }
                cx += font.Advance(ch);
            }
            OriginalCaptionsDrawn++;
        }
    }
}
