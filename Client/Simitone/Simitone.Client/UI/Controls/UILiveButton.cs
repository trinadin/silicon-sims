using FSO.Client;
using FSO.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FSO.Client.UI.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using FSO.Common.Rendering.Framework.Model;
using Simitone.Client.UI.Screens;
using FSO.SimAntics;
using Simitone.Client.UI.Model;

namespace Simitone.Client.UI.Controls
{
    public class UILiveButton : UIElasticButton
    {
        public float MotiveLevel; //-1 to 1;
        private Texture2D PlumbPlus;
        private Texture2D PlumbNeg;
        private TS1GameScreen Game;

        private Texture2D AvatarHead;
        private Texture2D SwitchIcon;
        public bool Switching;
        private VMAvatar Avatar;

        public UILiveButton(TS1GameScreen screen) : base(
            // R141: the live-mode button art is kPeople people.bmp (4x 50x50), NOT
            // House.bmp — that is the kHouseBtn HOUSE TAB (Res_CPanel.RT).
            UIOriginal.EnsureResolved("cpanel\\Buttons\\people.bmp")?.Get(GameFacade.GraphicsDevice)
                ?? UIOriginal.ResolveOrPng("cpanel\\Buttons\\House.bmp", "btn_live.png", Content.Get().CustomUI.Get("plumb_bg.png").Get(GameFacade.GraphicsDevice)))
        {
            var ui = Content.Get().CustomUI;
            PlumbPlus = ui.Get("plumb_plus.png").Get(GameFacade.GraphicsDevice);
            PlumbNeg = ui.Get("plumb_neg.png").Get(GameFacade.GraphicsDevice);
            SwitchIcon = ui.Get("mode_live.png").Get(GameFacade.GraphicsDevice);
            Game = screen;
        }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            var sel = Game.SelectedAvatar;
            MotiveLevel = (sel?.GetMotiveData(FSO.SimAntics.Model.VMMotive.Mood) ?? 0) / 100f;

            if (Avatar != sel)
            {
                Avatar = sel;
                // AUD-17 A-6: GenHeadTex ran a fresh 3D render + texture per
                // avatar switch and never disposed the previous one. The icon
                // cache already keys heads by outfit+texture (bounded by the
                // neighborhood's distinct heads, cleared at lot exit).
                AvatarHead = (Avatar == null) ? null : UIIconCache.GetObject(Avatar);
            }
        }

        public override void Draw(UISpriteBatch SBatch)
        {
            if (!Visible) return;
            if (Switching)
            {
                DrawLocalTexture(SBatch, SwitchIcon, new Vector2(SwitchIcon.Width, SwitchIcon.Height) / -2);
            }
            else
            {
                base.Draw(SBatch);
                DrawLocalTexture(SBatch, PlumbPlus, new Rectangle(0, 0, (int)(Math.Round(Math.Max(0, MotiveLevel * 22))), PlumbPlus.Height), new Vector2(30, -30), Vector2.One);
                int negWidth = (int)Math.Round(Math.Max(0, MotiveLevel * -22));
                DrawLocalTexture(SBatch, PlumbNeg, new Rectangle(22 - negWidth, 0, negWidth, PlumbPlus.Height), new Vector2(-30 - negWidth, -30), Vector2.One);
                if (AvatarHead != null)
                {
                    DrawLocalTexture(SBatch, AvatarHead, new Vector2(AvatarHead.Width / -2, AvatarHead.Height / -2));
                }
            }
        }
    }
}
