using System;
using System.Collections.Generic;
using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Common.Rendering.Framework.Model;
using FSO.Content;
using Microsoft.Xna.Framework;
using Simitone.Client.UI.Panels.LiveSubpanels;
using Simitone.Client.UI.Screens;

namespace Simitone.Client.UI.Panels
{
    /// <summary>
    /// R245: the id→control rect registry behind the original flash lookups.
    /// Tut_FlashButtonByImageID (0x1569e0) walks the ENTIRE main-window tree
    /// depth-first and flashes the first control whose image-buffer id
    /// matches (findbutton-tail.txt); Tut_FlashPersonPanelButton matches
    /// portrait-buffer identity; Tut_FlashRelButton (skeptic C4) tests
    /// existence of the neighbor plus the first relationship panel window.
    /// The port keeps a flat registry in construction order instead of a
    /// window tree — the first match wins, exactly like the native's
    /// parent-before-children walk over a tree holding one control per id.
    ///
    /// RECT SPACE: every resolved Rectangle is in the game screen's LOGICAL
    /// pixel space (UIScreen.Current.ScreenWidth/Height units — the same
    /// space UIOriginalTutorial/UIOriginalTutorialHighlight position and
    /// draw in). Logical position = the element's matrix-transformed
    /// top-left divided by UIScreen.Current.ScaleX/ScaleY (the only scale on
    /// these controls' parent chain is the screen's DPI factor); size = the
    /// element's own Size (button sheet cells / chrome cells).
    ///
    /// Registration happens per-UI-construction from the UIDesktopUCP and
    /// UIOriginalPeopleChrome ctors; entries hold weak references so retired
    /// lot UIs never leak, and dead entries are pruned on resolve.
    /// </summary>
    public static class TutorialControlMap
    {
        /// <summary>
        /// One registered control. Provenance pins every id to the original
        /// resource tables (r142/rt-inventory.txt rows; the people chrome
        /// anchors from r144/toolbar-law.md section 1).
        /// </summary>
        public sealed class RegisteredControl
        {
            public readonly int ImageId;
            public readonly bool LotButton;
            public readonly string Provenance;
            private readonly WeakReference<UIElement> Element;

            internal RegisteredControl(UIElement element, int imageId, bool lotButton, string provenance)
            {
                Element = new WeakReference<UIElement>(element);
                ImageId = imageId;
                LotButton = lotButton;
                Provenance = provenance;
            }

            internal bool TryGetElement(out UIElement element) => Element.TryGetTarget(out element);
        }

        private static readonly List<RegisteredControl> Registered = new List<RegisteredControl>();
        private static WeakReference<UIOriginalPeopleChrome> PeopleChromeRef;
        private static WeakReference<UIMainPanel> MainPanelRef;

        /// <summary>
        /// The native last-activated button (BSS 0x9777c, written by
        /// cTSWinBtn::TSOnMouseDownL 0x50bf40): the image id of the last
        /// clicked registered control, persisting until the next click. The
        /// client mirrors it into vm.TutorialEvents every tick.
        /// </summary>
        internal static int LastActivatedImageId;

        /// <summary>
        /// Registers a control with its ORIGINAL image resource id. Also
        /// hooks the last-activated-button law: a click on any registered
        /// button records its id (additive handler after the control's own
        /// OnButtonClick route, which stays untouched).
        /// </summary>
        public static void Register(UIElement element, int imageId, bool lotButton, string provenance)
        {
            if (element == null) return;
            Registered.Add(new RegisteredControl(element, imageId, lotButton, provenance));
            var button = element as UIButton;
            if (button != null)
            {
                var captured = imageId;
                button.OnButtonClick += (btn) => { ReportClick(captured); };
            }
        }

        /// <summary>Registers the people chrome hosting the portrait webcams
        /// (the port's person-panel button surface).</summary>
        public static void RegisterPeopleChrome(UIOriginalPeopleChrome chrome)
        {
            PeopleChromeRef = chrome == null ? null : new WeakReference<UIOriginalPeopleChrome>(chrome);
        }

        /// <summary>Registers the main panel hosting the LIVE subpanels (the
        /// port's cWinRelationship window surface).</summary>
        public static void RegisterMainPanel(UIMainPanel panel)
        {
            MainPanelRef = panel == null ? null : new WeakReference<UIMainPanel>(panel);
        }

        private static void ReportClick(int imageId)
        {
            LastActivatedImageId = imageId;
        }

        /// <summary>The current last-activated id for MirrorLastButton (0 when none).</summary>
        internal static int LastButtonImageIdForMirror => LastActivatedImageId;

        /// <summary>Probe view of the live registry (isolated batteries).</summary>
        public static IReadOnlyList<RegisteredControl> EntriesForProbe()
        {
            Prune();
            return Registered.AsReadOnly();
        }

        /// <summary>Resets the click mirror (isolated batteries; the registry
        /// itself only follows the live UI constructions).</summary>
        public static void ResetForProbe()
        {
            LastActivatedImageId = 0;
        }

        private static void Prune()
        {
            for (int i = Registered.Count - 1; i >= 0; i--)
            {
                UIElement element;
                if (!Registered[i].TryGetElement(out element) || element.Parent == null)
                    Registered.RemoveAt(i);
            }
        }

        /// <summary>
        /// Tut_FlashButtonByImageID lookup: the first live control whose
        /// original image id matches, in registration order.
        /// </summary>
        public static bool TryResolveRect(TS1GameScreen game, int imageId, out Rectangle logicalRect)
        {
            UIElement element;
            if (TryGetElement(imageId, out element))
            {
                logicalRect = LogicalRect(element);
                return true;
            }
            logicalRect = Rectangle.Empty;
            return false;
        }

        /// <summary>The matched control plus its lot-button classification.</summary>
        public static void DescribeRect(TS1GameScreen game, int imageId, out UIElement element, out bool lotButton)
        {
            lotButton = false;
            if (TryGetElement(imageId, out element))
            {
                Prune();
                foreach (var entry in Registered)
                {
                    UIElement live;
                    if (entry.ImageId == imageId && entry.TryGetElement(out live) && live == element)
                    {
                        lotButton = entry.LotButton;
                        return;
                    }
                }
            }
            element = null;
        }

        private static bool TryGetElement(int imageId, out UIElement element)
        {
            Prune();
            foreach (var entry in Registered)
            {
                if (entry.ImageId != imageId) continue;
                if (!entry.TryGetElement(out element) || element.Parent == null) continue;
                return true;
            }
            element = null;
            return false;
        }

        /// <summary>
        /// Tut_FlashPersonPanelButton (0x156440): the flashed control is the
        /// person-panel button whose image buffer IS the neighbor's portrait.
        /// The port's portrait identity lives on the people chrome webcams
        /// (UIOriginalWebcamButton.Avatar), so the webcam whose avatar's
        /// PersistID matches is resolved.
        /// </summary>
        public static bool TryResolvePersonPanel(TS1GameScreen game, int personId,
            out Rectangle logicalRect, out UIElement webcam)
        {
            UIOriginalPeopleChrome chrome = null;
            if (PeopleChromeRef != null && (!PeopleChromeRef.TryGetTarget(out chrome) || chrome.Parent == null))
                chrome = null;
            if (chrome != null && personId > 0)
            {
                foreach (var slot in chrome.Webcams)
                {
                    if (slot == null || slot.Avatar == null) continue;
                    if (slot.Avatar.PersistID == (uint)personId)
                    {
                        webcam = slot;
                        logicalRect = LogicalRect(slot);
                        return true;
                    }
                }
            }
            webcam = null;
            logicalRect = Rectangle.Empty;
            return false;
        }

        /// <summary>
        /// Tut_FlashRelButton (0x156370, skeptic C4): the person id gates
        /// EXISTENCE only — Neighborhood::FindNeighborByID over the NBRS
        /// table (the port's Content.Get().Neighborhood.Neighbours map); the
        /// target is the FIRST relationship panel window. The port's
        /// cWinRelationship equivalent is the mounted UIRelationshipSubpanel;
        /// with no rel panel the flash is a native no-op.
        /// </summary>
        public static bool TryResolveRelPanel(TS1GameScreen game, int personId,
            out Rectangle logicalRect, out UIElement relPanel)
        {
            UIMainPanel panel = null;
            if (MainPanelRef != null && (!MainPanelRef.TryGetTarget(out panel) || panel.Parent == null))
                panel = null;
            var neighbours = Content.Get()?.Neighborhood?.Neighbors;
            bool personExists = neighbours != null && personId > 0 &&
                neighbours.NeighbourByID.ContainsKey((short)personId);
            if (personExists && panel != null)
            {
                var rel = panel.SubPanel as UIRelationshipSubpanel;
                if (rel != null && rel.Visible)
                {
                    relPanel = rel;
                    logicalRect = LogicalRect(rel);
                    return true;
                }
            }
            relPanel = null;
            logicalRect = Rectangle.Empty;
            return false;
        }

        /// <summary>
        /// Event code 8 (mouse over/activating the button with image id): the
        /// native reads cTSWinBtn::SetState's +0xd8 state word; the port
        /// resolves the cursor against every registered control's hit box,
        /// first match in registration order.
        /// </summary>
        public static int ResolveHoverImageId(TS1GameScreen game, UpdateState state)
        {
            Prune();
            foreach (var entry in Registered)
            {
                UIElement element;
                if (!entry.TryGetElement(out element) || element.Parent == null) continue;
                var size = element.Size;
                if (size.X <= 0 || size.Y <= 0) continue;
                if (element.HitTestArea(state.MouseState,
                    new Rectangle(0, 0, (int)size.X, (int)size.Y), false))
                    return entry.ImageId;
            }
            return -1;
        }

        /// <summary>The logical-space rect of a live element.</summary>
        public static Rectangle LogicalRect(UIElement element)
        {
            // Refresh the parent chain's matrices so the resolved rect is
            // current even outside the draw pass (headless batteries).
            var chain = new List<UIElement>();
            for (var node = element; node != null; node = node.Parent) chain.Add(node);
            for (int i = chain.Count - 1; i >= 0; i--) chain[i].CalculateMatrix();
            var physical = element.LocalPoint(Vector2.Zero);
            var screen = UIScreen.Current;
            var x = (int)Math.Floor(physical.X / screen.ScaleX);
            var y = (int)Math.Floor(physical.Y / screen.ScaleY);
            var size = element.Size;
            return new Rectangle(x, y, (int)size.X, (int)size.Y);
        }
    }
}
