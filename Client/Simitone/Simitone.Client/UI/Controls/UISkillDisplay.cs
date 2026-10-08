using FSO.Client;
using FSO.Client.UI.Framework;
using FSO.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simitone.Client.UI.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FSO.Common.Rendering.Framework.Model;

namespace Simitone.Client.UI.Controls
{
    /// <summary>
    /// R159: skill pips on the ORIGINAL kSkillHilite art (CPanel/SkillsHilite.bmp
    /// 4x11, res 4511 — the engine's own skill pip, replacing the mobile
    /// skill.png + UIStyle tints). Filled pips draw the art; "needed" pips draw
    /// it at half alpha; empty slots draw a dark pip silhouette. Alpha choice
    /// for the needed tier is our disclosed reading (the engine tints via its
    /// palette LUT; only the hilite bitmap ships).
    /// </summary>
    public class UISkillDisplay : UIElement
    {
        public static Texture2D HiliteTexture;

        // UI-37: art-anchored — SkillsHilite.bmp's own interior navy #000050
        // (dominant dark of the shipped 4x11 bitmap, 9/44 px; decoded from
        // UIGraphics.far @0x3165b9). The engine's exact palette-LUT index
        // stays un-decoded (ORIG-02 ui33 #3 verdict re-verified).
        public static readonly Microsoft.Xna.Framework.Color EmptyPipTint = new Microsoft.Xna.Framework.Color(0, 0, 80);

        public UISkillDisplay() : base()
        {
            if (HiliteTexture == null)
                HiliteTexture = UIOriginal.EnsureResolved("cpanel\\SkillsHilite.bmp")?.Get(GameFacade.GraphicsDevice);
        }

        private int _Value;
        public int Value
        {
            get
            {
                return _Value;
            }
            set
            {
                if (value != _Value) Invalidate();
                _Value = value;
            }
        }
        private int _Needed;
        public int Needed
        {
            get
            {
                return _Needed;
            }
            set
            {
                if (value != _Needed) Invalidate();
                _Needed = value;
            }
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            for (int i = 0; i < 10; i++) {
                Color color;
                float alpha;
                if (i < Value) { color = Color.White; alpha = 1f; }
                else if (i < Needed) { color = Color.White; alpha = 0.45f; }
                else { color = EmptyPipTint; alpha = 1f; }
                if (HiliteTexture != null)
                    DrawLocalTexture(batch, HiliteTexture, null, new Microsoft.Xna.Framework.Vector2(i * 8, 0), Vector2.One, color * alpha);
                else
                    DrawLocalTexture(batch, FSO.Common.Utils.TextureGenerator.GetPxWhite(GameFacade.GraphicsDevice), null,
                        new Microsoft.Xna.Framework.Vector2(i * 8, 0), new Vector2(4, 11), color);
            }
        }
    }
}
