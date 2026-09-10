using FSO.Client;
using FSO.Client.UI.Framework;
using FSO.Common.Rendering.Framework.Model;
using FSO.Common.Utils;
using Microsoft.Xna.Framework;
using Simitone.Client.UI.Screens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simitone.Client.UI.Panels.LiveSubpanels
{
    public class UISubpanel : UICachedContainer
    {
        public TS1GameScreen Game;

        public UISubpanel(TS1GameScreen game) : base()
        {
            Opacity = 0;
            var screenWidth = GameFacade.Screens.CurrentUIScreen.ScreenWidth;
            // The shared desktop band still reaches the right edge, but each
            // cWinPeople content child keeps its own native 280/504x100 host.
            // Non-People panels retain the full-band sizing law recovered in
            // R142. Touch keeps its authored 128-pixel column.
            var nativePeopleWidth = game.Desktop ? NativeDesktopPeopleWidth(this, screenWidth) : -1;
            Size = new Vector2(nativePeopleWidth > 0 ? nativePeopleWidth
                : screenWidth - (game.Desktop ? 520 : 342), game.Desktop ? 100 : 128);
            GameFacade.Screens.Tween.To(this, 0.3f, new Dictionary<string, float>() { { "Opacity", 1f } });
            Game = game;
        }

        /// <summary>
        /// Native cWinPeople content-host width. Mood and Personality remain
        /// 280 pixels; the other five standard families expand from 280 to
        /// 504 only in the original 1024-pixel logical mode.
        /// </summary>
        public static int NativeDesktopPeopleWidth(UISubpanel panel, int screenWidth)
        {
            if (panel is UIMotiveSubpanel || panel is UIPersonalitySubpanel)
                return 280;
            if (panel is UIRelationshipSubpanel || panel is UIJobSubpanel
                || panel is UIHouseSubpanel || panel is UIOriginalInterestSubpanel
                || panel is UIOriginalGiftSubpanel)
                return screenWidth == 1024 ? 504 : 280;
            return -1;
        }

        public override void GameResized()
        {
            var screenWidth = UIScreen.Current.ScreenWidth;
            var nativePeopleWidth = Game.Desktop ? NativeDesktopPeopleWidth(this, screenWidth) : -1;
            Size = new Vector2(nativePeopleWidth > 0 ? nativePeopleWidth
                : screenWidth - (Game.Desktop ? 520 : 342), Game.Desktop ? 100 : 128);
            base.GameResized();
        }

        public override void Update(UpdateState state)
        {
            // Native People panels repaint their live values every frame. The
            // port hosts them in UICachedContainer, so a clean cache would
            // otherwise freeze motives, skills, relationships, personality,
            // interests, and mode visibility after the first draw.
            if (Game.Desktop && NativeDesktopPeopleWidth(this,
                UIScreen.Current?.ScreenWidth ?? 800) > 0) Invalidate();
            base.Update(state);
        }

        public virtual void Kill()
        {
            GameFacade.Screens.Tween.To(this, 0.3f, new Dictionary<string, float>() { { "Opacity", 0f } });
            GameThread.SetTimeout(() =>
            {
                // R122: null-safe — panels constructed for introspection (never
                // Add()ed to a parent) also get Kill()ed, e.g. by the autotest.
                Parent?.Remove(this);
            }, 300);
        }
    }
}
