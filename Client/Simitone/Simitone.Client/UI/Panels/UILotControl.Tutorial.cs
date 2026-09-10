using FSO.Client.UI.Framework;
using FSO.SimAntics;
using FSO.SimAntics.Model;
using FSO.SimAntics.Primitives;
using System.Collections.Generic;
using FSO.Common;
using FSO.Common.Rendering.Framework.Model;
using FSO.SimAntics.NetPlay.Model.Commands;
using Simitone.Client.UI.Screens;

namespace Simitone.Client.UI.Panels
{
    public partial class UILotControl
    {
        private VMContext TutorialContext;
        internal UIOriginalTutorial TutorialPresenter { get; private set; }
        internal UIOriginalTutorialHighlight TutorialHighlight { get; private set; }
        private readonly Dictionary<VMEntity, List<UIMobileAlert>> TutorialDialogs =
            new Dictionary<VMEntity, List<UIMobileAlert>>();
        private bool TutorialEffectsBound;

        internal static bool IsNonmodalTutorial(VMDialogInfo info) =>
            info?.Operand != null && (info.Operand.Type == VMDialogType.Sims1Tutorial ||
                (info.Caller != null && info.Caller.Thread?.Context?.TutorialObject == info.Caller)) &&
                (info.Operand.Flags & VMDialogFlags.NewEngageContinue) != 0;

        private void BindTutorialContext()
        {
            if (TutorialPresenter == null && !FSOEnvironment.SoftwareKeyboard && Parent != null)
            {
                TutorialPresenter = new UIOriginalTutorial(() => vm.SendCommand(new VMNetTS1TutorialInfoCmd()));
                Parent.Add(TutorialPresenter);
                // R245: the cTSWinTutorialHighlight sibling — added AFTER the
                // presenter so it mounts topmost, and PullToFront-ed again on
                // every show (the engine attaches it to the view parent and
                // pulls it to the front of the window tree on each flash).
                TutorialHighlight = new UIOriginalTutorialHighlight();
                Parent.Add(TutorialHighlight);
            }
            if (!TutorialEffectsBound && vm != null)
            {
                vm.OnTutorialUIEffect += TutorialUIEffect;
                TutorialEffectsBound = true;
            }
            TutorialPresenter?.SetOwner(vm.Context.TutorialObject);
            if (TutorialPresenter != null) TutorialPresenter.Visible = Visible;
            if (TutorialHighlight != null) TutorialHighlight.Visible = TutorialHighlight.Visible && Visible;
            if (TutorialContext == vm.Context) return;
            if (TutorialContext != null)
            {
                TutorialContext.TutorialObjectChanged -= TutorialObjectChanged;
                foreach (var owner in new List<VMEntity>(TutorialDialogs.Keys))
                    DismissTutorialDialogs(owner);
            }
            TutorialContext = vm.Context;
            if (TutorialContext != null)
                TutorialContext.TutorialObjectChanged += TutorialObjectChanged;
        }

        /// <summary>
        /// R245 poll driver (Tut_CheckForEvents 0x156550, its only call site
        /// cDDDSimsView::Simulate 0x21715c — the port's Update tick): publish
        /// the TutorialUIState mirror, mirror the last-activated button image
        /// id, then Poll once per tick. The poller owns the re-entrancy guard
        /// (skeptic C3) and refuses to run while blocked.
        /// </summary>
        private void TickTutorialPoller(UpdateState state)
        {
            if (vm == null || !vm.TS1) return;
            var poller = vm.TutorialEvents;
            var ui = poller.State;
            var game = Parent as TS1GameScreen;
            var panel = game?.Frontend?.MainPanel;
            var chrome = panel?.PeopleChrome;

            // ev 1: CPState exists and GetCPMode()==0 — the port's LIVE
            // control-panel mode with the panel bar open.
            ui.CpModeIsZero = panel != null && panel.PanelActive && panel.Mode == UIMainPanelMode.LIVE;

            // ev 2/9/11/13/15 + 14: the person-panel page caption (skeptic
            // C1: 9="rel", 11="job"). The port's LIVE tab categories map to
            // the five native captions; house/interest/gift hold a real page
            // with a non-native caption ("" matches none of the literals);
            // null = the panel or its page button is gone (ev 14's condition).
            // The native "skill" page has no port tab — unreachable here.
            ui.PersonPanelPage = PersonPanelPageCaption(panel, chrome);

            // ev 5: the shown portrait's neighbor identity (the port's
            // person-panel portrait is the selected avatar).
            // DISCLOSURE (dropped conjunction, P2 #3): native case 5 compares
            // the shown portrait buffer against the LAST-CLICKED button's
            // buffer too — it only fires when the shown portrait is also the
            // button that was last activated. The port publishes the shown
            // identity alone (no conjunction with LastButtonClickImageId), so
            // ev 5 can fire EARLIER than native; fires-early-only, benign.
            var shown = game?.SelectedAvatar;
            ui.ShownPortraitNeighborId = shown?.PersistID ?? 0;

            // ev 6/7 change detectors: raw rotation and zoom/scroll words
            // (world+0x84 law); the poller keeps the shared -1 edge latch.
            ui.RotationEdge = World?.State != null ? (int)World.State.Rotation : 0;
            ui.ScrollEdge = World?.State != null ? (int)World.State.Zoom : 0;

            // ev 8: cursor over a registered control (cTSWinBtn state word).
            ui.HoverButtonImageId = game == null ? -1
                : TutorialControlMap.ResolveHoverImageId(game, state);

            // The native mirror runs BEFORE the owner guard, every tick, and
            // is never blocked (simulator global 12 = last button's image id).
            poller.MirrorLastButton(TutorialControlMap.LastButtonImageIdForMirror);

            // R247 ESC cancel (r247-tut-lifecycle §D + skeptic correction 4):
            // cDDDSimsView::TSOnKeyDown's switch matches key 0x1b — ESC — and
            // runs CancelTutorial (owner's "cancel tutorial" tree, then the
            // owner kill) followed by the cTSWinTutorialHighlight HideWindow.
            // The native case does NOT set the consumed flag its sibling cases
            // set, so ESC still propagates: the port's own ESC consumers (the
            // build/buy tool release below, PIP hiding) stay live after this.
            // The modal-dialog skip is the port's stand-in for the native
            // handler head's child-window check.
            if (BlockingDialog == null && state.NewKeys.Contains(
                Microsoft.Xna.Framework.Input.Keys.Escape))
            {
                var escOwner = vm.Context.TutorialObject;
                if (escOwner != null)
                {
                    Simitone.Client.Utils.TutorialEngine247.RequestTutorialCancel(vm.Context);
                    TutorialHighlight?.Clear();
                }
            }

            if (!poller.IsReentrancyBlocked) poller.Poll();

            // DoModalWin window (skeptic C2/C3): the tracked blocking dialog
            // both auto-hides the highlight and blocks nested polls.
            var modal = BlockingDialog != null;
            poller.ModalDialogTracked = modal;
            TutorialHighlight?.SetModalObscured(modal);

            if (!poller.IsReentrancyBlocked) poller.Poll();
        }

        private static string PersonPanelPageCaption(UIMainPanel panel, UIOriginalPeopleChrome chrome)
        {
            if (panel == null || chrome == null || !chrome.Visible || !panel.PanelActive
                || panel.Mode != UIMainPanelMode.LIVE) return null;
            var tab = chrome.ActiveTab;
            if (tab < 0 || tab >= UIOriginalPeopleChrome.TabCategory.Length) return null;
            switch (UIOriginalPeopleChrome.TabCategory[tab])
            {
                case 0: return "motives";
                case 3: return "rel";
                case 1: return "job";
                case 2: return "per.ity";
                default: return "";   // house/interest/gift: real page, non-native caption
            }
        }

        /// <summary>
        /// Primitive 34 (TryElement 0x22) sink: the engine dispatches the
        /// decoded flash request; the UI owns the target resolution and the
        /// highlighter lifecycle (decode.md §6, skeptic C4/C5).
        /// </summary>
        private void TutorialUIEffect(int subOp, int id, bool on)
        {
            if (TutorialHighlight == null) return;
            var game = Parent as TS1GameScreen;
            if (!on)
            {
                // Native law (HiliteForTutorial on=false, P2 #2): the helper
                // re-finds the named target; if it RESOLVES, the ONE
                // highlighter is hidden unconditionally — regardless of what
                // is currently flashed. An unresolvable target is a no-op
                // (the flash helpers never hide an existing flash on failure).
                if (TutorialTargetResolves(game, subOp, id)) TutorialHighlight.Clear();
                return;
            }
            switch (subOp)
            {
                case 0: TutorialHighlight.FlashControl(game, id); break;
                case 1: TutorialHighlight.FlashPersonPanel(game, id); break;
                case 2: TutorialHighlight.FlashRelPanel(game, id); break;
            }
        }

        /// <summary>
        /// The on=false hide law's only gate: does primitive-34 sub-op/id
        /// re-resolve to a live target through the same lookups the flash
        /// path uses?
        /// </summary>
        private static bool TutorialTargetResolves(TS1GameScreen game, int subOp, int id)
        {
            switch (subOp)
            {
                case 0: return TutorialControlMap.TryResolveRect(game, id, out _);
                case 1: return TutorialControlMap.TryResolvePersonPanel(game, id, out _, out _);
                case 2: return TutorialControlMap.TryResolveRelPanel(game, id, out _, out _);
                default: return false;
            }
        }

        private void TutorialObjectChanged(VMEntity previous, VMEntity current)
        {
            if (previous != null) DismissTutorialDialogs(previous);
            TutorialPresenter?.SetOwner(current);
        }

        private void TrackTutorialDialog(UIMobileAlert dialog, VMDialogInfo info)
        {
            if (info.Caller == null || !IsOriginalObjectDialog(info)) return;
            if (info.Caller != vm.Context.TutorialObject &&
                info.Operand.Type != VMDialogType.Sims1Tutorial) return;
            // TryDialog aborts a still-open previous dialog for this caller
            // before opening the next one (f17b4-f17d4). No other caller closes.
            DismissTutorialDialogs(info.Caller);
            TutorialDialogs[info.Caller] = new List<UIMobileAlert> { dialog };
        }

        private void DismissTutorialDialogs(VMEntity owner)
        {
            if (!TutorialDialogs.TryGetValue(owner, out var dialogs)) return;
            foreach (var dialog in dialogs)
            {
                TutorialPresenter?.Forget(dialog);
                UIScreen.RemoveDialog(dialog);
                if (BlockingDialog == dialog)
                {
                    BlockingDialog = null;
                    LastDialogID = 0;
                }
            }
            TutorialDialogs.Remove(owner);
        }

        public override void Removed()
        {
            if (TutorialContext != null)
                TutorialContext.TutorialObjectChanged -= TutorialObjectChanged;
            if (TutorialEffectsBound)
            {
                if (vm != null) vm.OnTutorialUIEffect -= TutorialUIEffect;
                TutorialEffectsBound = false;
            }
            foreach (var owner in new List<VMEntity>(TutorialDialogs.Keys))
                DismissTutorialDialogs(owner);
            TutorialContext = null;
            if (TutorialPresenter != null)
            {
                TutorialPresenter.Parent?.Remove(TutorialPresenter);
                TutorialPresenter.Dispose();
                TutorialPresenter = null;
            }
            if (TutorialHighlight != null)
            {
                // CleanUpWindowReferences (skeptic C2): a destroyed
                // highlighter leaves the winmgr slot clean.
                TutorialHighlight.Parent?.Remove(TutorialHighlight);
                TutorialHighlight.Dispose();
                TutorialHighlight = null;
            }
            base.Removed();
        }
    }
}
