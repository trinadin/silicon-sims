// PORT ADDITION — no TS1 counterpart: modern Simitone chrome (mobile-by-design per charter); its values are port-authored by design, not decoded (see PARITY.md disclosure conventions).
﻿using FSO.Client.UI.Controls;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FSO.Client.UI.Framework;

namespace Simitone.Client.UI.Controls
{
    public class UITwoStateButton : UIButton
    {
        public UITwoStateButton(Texture2D tex) : base(tex)
        {
            ImageStates = 2;
        }

        public override void Draw(UISpriteBatch SBatch)
        {
            if (!Visible) return;
            var oldOpacity = Opacity;
            if (Disabled) Opacity = 0.5f;
            var col = BlendColor;
            base.Draw(SBatch);
            if (Disabled) Opacity = oldOpacity;
        }
    }
}
