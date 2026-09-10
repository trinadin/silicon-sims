using FSO.Client;
using FSO.Common.Rendering.Framework.Model;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Simitone.Client.UI.Controls
{
    /// ROUND-214 rework (tools/iff-dump/r214/r214-pause-label-law.md): the full
    /// DrawPause machine decoded — a STEADY indicator, not a blinker.
    /// cDDDSimsView::DrawPause(show, subscribe) @0x214000 first cancels any
    /// pending timer (the f181 unsubscribe path), stores the show flag, arms
    /// the STR#148[1] timer only when BOTH bools are set, then Shows/Hides the
    /// label. TSOnTimerMsg @0x213830 is a ONE-SHOT: hide + unsubscribe, never
    /// resubscribe — the label stays hidden until the next DrawPause call.
    /// Decoded call sites: CPState::Pause @0x2100d0 and the latched
    /// UpdateCPStateFromWorld sync @0x2114c4 pass (paused, false) with a
    /// mode==LIVE gate (steady show/hide); CPState::SetMode's five arms pass
    /// BUILD/BUY/CAMERA -> (true, true) — show + the one-shot 3000ms hide —
    /// OPTIONS -> (true, false), LIVE -> (pausedFlag, false). R204's
    /// symmetric-blink substitute and its re-show-cadence disclosure are
    /// retired. The engine drives those sites on command events; the port
    /// reads the pause edge per frame in LIVE mode — identical visible
    /// outcome (nothing else hides the label in LIVE). Face/color stay the
    /// R175 system-slot decode (font_table[11], RGB 195,205,205).
    public class UIOriginalPauseLabel : FSO.Client.UI.Framework.UIContainer
    {
        // Gate-readable DrawPause accounting: timer arms, one-shot fires,
        // and cancel-on-next-DrawPause (the f181 law).
        public static int OneShotArms = 0;
        public static int OneShotFires = 0;
        public static int TimerCancels = 0;

        public readonly UIOriginalText TextLabel;
        public readonly int PeriodMs;

        private bool _timerArmed; // engine f181 (timer subscribed)
        private double _timerMs;
        private bool _livePaused; // CPState f30 tracking for the LIVE pause edge

        public bool LabelVisibleForProbe { get { return TextLabel.Visible; } }
        public bool TimerArmedForProbe { get { return _timerArmed; } }

        public UIOriginalPauseLabel(GraphicsDevice gd)
        {
            var text = GameFacade.Strings.GetString("148", "0");
            int period;
            if (!int.TryParse(GameFacade.Strings.GetString("148", "1"), out period) || period <= 0)
                period = 3000; // canon fallback; [1] is data, not a literal law
            PeriodMs = period;

            TextLabel = new UIOriginalText(text, OriginalGlyphFont.LoadByIndex(11, gd))
            {
                Position = new Vector2(0, 0),
                Color = UIMobileDialog.OriginalSystemTextColor,
                Visible = false
            };
            Add(TextLabel);
        }

        /// cDDDSimsView::DrawPause(show, subscribe) @0x214000 verbatim:
        /// cancel any pending timer, set the show flag, arm the one-shot
        /// period timer iff both bools, then Show/Hide the label.
        public void DrawPause(bool show, bool subscribe)
        {
            if (_timerArmed)
            {
                _timerArmed = false;
                _timerMs = 0;
                TimerCancels++;
            }
            TextLabel.Visible = show;
            if (show && subscribe)
            {
                _timerArmed = true;
                _timerMs = 0;
                OneShotArms++;
            }
        }

        /// CPState::SetMode's five DrawPause arms @0x2108e0/0x210ad0/0x210b68/
        /// 0x210c50/0x210ca4, keyed by the port's 1:1 mode names.
        public void OnModeChanged(Simitone.Client.UI.Panels.UIMainPanelMode mode)
        {
            switch (mode)
            {
                case Simitone.Client.UI.Panels.UIMainPanelMode.BUY:
                case Simitone.Client.UI.Panels.UIMainPanelMode.BUILD:
                case Simitone.Client.UI.Panels.UIMainPanelMode.CAMERA:
                    // Clock-stopping modes: show + the one-shot 3000ms hide.
                    DrawPause(true, true);
                    break;
                case Simitone.Client.UI.Panels.UIMainPanelMode.OPTIONS:
                    // Steady show, no timer.
                    DrawPause(true, false);
                    break;
                default:
                    // LIVE: the current pause flag, no timer.
                    DrawPause(PausedNow(), false);
                    break;
            }
        }

        private static bool PausedNow()
        {
            var game = GameFacade.Screens.CurrentUIScreen as Simitone.Client.UI.Screens.TS1GameScreen;
            return game != null && game.vm != null && game.vm.SpeedMultiplier == 0;
        }

        private static Simitone.Client.UI.Panels.UIMainPanelMode ModeNow()
        {
            var game = GameFacade.Screens.CurrentUIScreen as Simitone.Client.UI.Screens.TS1GameScreen;
            var panel = game == null || game.Frontend == null ? null : game.Frontend.MainPanel;
            return panel == null ? Simitone.Client.UI.Panels.UIMainPanelMode.LIVE : panel.Mode;
        }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            // CPState::Pause @0x2100d0 (mode==LIVE gate) plus the latched
            // UpdateCPStateFromWorld sync @0x2114c4: in LIVE mode the pause
            // flag drives the label directly — steady show/hide, no timer.
            // Outside LIVE the engine's Pause() never touches the label, so
            // only the edge bookkeeping runs (SetMode carries the flag back).
            var paused = PausedNow();
            if (ModeNow() == Simitone.Client.UI.Panels.UIMainPanelMode.LIVE)
            {
                if (paused != _livePaused)
                {
                    _livePaused = paused;
                    DrawPause(paused, false);
                }
            }
            else
            {
                _livePaused = paused;
            }
            // TSOnTimerMsg @0x213830: ONE-SHOT hide at the period — the label
            // stays hidden until the next DrawPause call re-shows it.
            if (_timerArmed && state.Time != null)
            {
                _timerMs += state.Time.ElapsedGameTime.TotalMilliseconds;
                if (_timerMs >= PeriodMs)
                {
                    _timerArmed = false;
                    _timerMs = 0;
                    TextLabel.Visible = false;
                    OneShotFires++;
                }
            }
        }
    }
}
