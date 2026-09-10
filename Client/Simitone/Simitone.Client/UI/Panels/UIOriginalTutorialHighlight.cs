using System;
using FSO.Client;
using FSO.Client.UI.Framework;
using FSO.SimAntics.Model;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simitone.Client.UI.Model;
using Simitone.Client.UI.Screens;

namespace Simitone.Client.UI.Panels
{
    /// <summary>
    /// R245: the cTSWinTutorialHighlight port (decode.md §4/§5 + skeptic
    /// corrections). One instance, mounted as a sibling of the tutorial
    /// presenter on the game screen, owning the winmgr highlighter slot
    /// (cTSWinMgrW95+0x20c) laws:
    ///
    /// - art: kTutorialHighlightBMP 4980 = cpanel\TutHigh.bmp 150x50, three
    ///   50x50 frames (SetBuffer(buf, 3, 6.0)); the window itself is 50x50.
    /// - FLASH ANIMATION: ShowWindow resets phase 0 / direction +1 and arms a
    ///   timer at trunc(1000/6+0.5) = 167 ms (period 6.0 s, TSOnTimerMsg
    ///   0x538d40); every step ping-pongs the phase over 0..2..0 and re-arms.
    ///   It NEVER auto-hides (skeptic: the only stop is HideWindow / release).
    /// - PAINT: TSPaint 0x539020 blits slice[phase] of the strip at the window
    ///   position, 50x50, integer positions, no scaling, no alpha — the
    ///   magenta key applied at load carries the transparency.
    /// - HILITE LAW (HiliteForTutorial__6cTSWinFb 0x5034c0): on=true moves the
    ///   window over the target with x = target.x + trunc((targetW-50)/2)
    ///   (skeptic C6: plain C# integer division is exact), y = target.y - 50 - 3,
    ///   then ShowWindow + PullToFront; an invisible target hides instead. The
    ///   cWinLotBtn override (0x2d2ae0) shifts the shown highlight +50 px down
    ///   (y = target.y - 3). on=false hides.
    /// - MODAL AUTO-HIDE + RESTORE (skeptic C2, DoModalWin 0x51ca60): a modal
    ///   dialog hides the highlight for its whole duration and ShowWindow
    ///   restores it afterwards (a fresh phase 0 ping-pong, like the engine's
    ///   unsubscribe/resubscribe cycle).
    /// - CLEANUP (skeptic C2, CleanUpWindowReferences 0x51dd60): a destroyed
    ///   target clears the flash; Shutdown/Dispose leave a clean slot.
    /// </summary>
    public sealed class UIOriginalTutorialHighlight : UIContainer, IDisposable
    {
        /// <summary>kTutorialHighlightBMP: cpanel\TutHigh.bmp 150x50 (r142/rt-inventory.txt).</summary>
        public const int ArtID = 4980;
        public const int FrameSize = 50;      // 150x50 strip / 3 frames
        public const int Frames = 3;          // SetBuffer(buf, 3, 6.0)
        /// <summary>fctiwz(1000 * (1/6) + 0.5) = 167 ms per phase step.</summary>
        public const int PhaseIntervalMs = 167;
        /// <summary>cWinLotBtn override: SetArea(l, t+0x32, r, b+0x32).</summary>
        public const int LotButtonShiftPx = 50;
        /// <summary>Base anchor: y = target.y - hlH - 3.</summary>
        public const int AnchorGapPx = 3;

        private Texture2D Strip;
        internal Func<long> Clock = () => Environment.TickCount64;
        private bool TimerSubscribed;              // +0xe0
        private long PhaseStarted;                 // last re-arm point
        internal int Phase { get; private set; }   // +0xdc
        internal int Direction { get; private set; } // +0xd8
        internal Rectangle AnchorRect { get; private set; }   // the window position (+0x1c/+0x20)
        internal bool ObscuredByModal { get; private set; }   // inside a DoModalWin loop
        private bool RestoreAfterModal;            // remembered GetFlag(1) at modal entry
        internal UIElement TargetElement;          // the flashed control (death tracking)

        internal int KeyedPixels;              // load-time color-key census (probe)

        public UIOriginalTutorialHighlight()
        {
            Visible = false;                       // starts hidden (view-init step 4)
            try
            {
                var source = UIOriginal.EnsureResolvedByID(ArtID)?.Get(GameFacade.GraphicsDevice);
                if (source != null)
                {
                    Strip = ApplyMagentaKey(source);
                    // null source name: this is the byte-derived KEYED COPY, not
                    // the far-mounted cache — it must not claim a mount name.
                    UIArtProvenance.NoteOriginal(Strip, null);
                }
            }
            catch { Strip = null; } // A missing highlighter must never break a lesson.
        }

        /// <summary>
        /// The engine applies the magenta color key at load (view-init step 6:
        /// buf->vt+0x70(*(TOC-0x7208))); the TS1 far codec mounts raw BMPs
        /// opaque, so the port keys the strip copy here — every (≥FE, ≤02, ≥FE)
        /// pixel (the UIGraphicsProvider mask family) goes transparent. TSPaint
        /// then blits with no alpha: the keyed pixels own the transparency.
        /// </summary>
        private Texture2D ApplyMagentaKey(Texture2D source)
        {
            var pixels = new Color[source.Width * source.Height];
            source.GetData(pixels);
            for (int i = 0; i < pixels.Length; i++)
            {
                var c = pixels[i];
                if (c.A != 0 && c.R >= 0xFE && c.G <= 0x02 && c.B >= 0xFE)
                {
                    pixels[i] = new Color(c.R, c.G, c.B, (byte)0);   // keep RGB: no black fringe on magnified sampling
                    KeyedPixels++;
                }
            }
            var keyed = new Texture2D(GameFacade.GraphicsDevice, source.Width, source.Height);
            keyed.SetData(pixels);
            return keyed;
        }

        /// <summary>
        /// HiliteForTutorial(true) with a resolved target: the base law anchors
        /// a 50x50 window over the rect and pulls the window to the front;
        /// lot buttons take the cWinLotBtn +50 px shift.
        /// </summary>
        public bool Flash(Rectangle targetRect, UIElement target, bool lotButton)
        {
            TargetElement = target;
            // "If NOT this->GetFlag(1) (target invisible): hl->HideWindow(); return."
            if (target != null && !target.Visible)
            {
                HideWindow();
                return false;
            }
            int x = targetRect.X + (targetRect.Width - FrameSize) / 2;   // srawi idiom, skeptic C6
            int y = targetRect.Y - FrameSize - AnchorGapPx;
            if (lotButton) y += LotButtonShiftPx;                        // cWinLotBtn override
            AnchorRect = new Rectangle(x, y, FrameSize, FrameSize);
            ShowWindow();
            PullToFront();                                               // slot 0x4c every show
            return true;
        }

        /// <summary>Tut_FlashButtonByImageID (0x1569e0): first control in the
        /// main-window tree whose original image id matches.</summary>
        public bool FlashControl(TS1GameScreen game, int imageId)
        {
            Rectangle rect;
            if (!TutorialControlMap.TryResolveRect(game, imageId, out rect)) return HideAndFail();
            bool lotButton;
            UIElement element;
            TutorialControlMap.DescribeRect(game, imageId, out element, out lotButton);
            return Flash(rect, element, lotButton);
        }

        /// <summary>
        /// Tut_FlashPersonPanelButton (0x156440): the person id resolves a
        /// portrait buffer; the flashed control is the one whose buffer IS
        /// that portrait. The port's portrait identity lives on the people
        /// chrome webcams, so the webcam whose avatar matches is flashed.
        /// </summary>
        public bool FlashPersonPanel(TS1GameScreen game, int personId)
        {
            Rectangle rect;
            UIElement webcam;
            if (!TutorialControlMap.TryResolvePersonPanel(game, personId, out rect, out webcam)) return HideAndFail();
            return Flash(rect, webcam, false);
        }

        /// <summary>
        /// Tut_FlashRelButton (0x156370, skeptic C4): the person id gates
        /// EXISTENCE only — the first relationship panel window itself is
        /// flashed (no row-button matching by person id exists natively).
        /// </summary>
        public bool FlashRelPanel(TS1GameScreen game, int personId)
        {
            Rectangle rect;
            UIElement panel;
            if (!TutorialControlMap.TryResolveRelPanel(game, personId, out rect, out panel)) return HideAndFail();
            return Flash(rect, panel, false);
        }

        /// <summary>HiliteForTutorial(on=false) / any-hide path.</summary>
        public void Clear()
        {
            TargetElement = null;
            HideWindow();
        }

        private bool HideAndFail()
        {
            // "On failure they just return (no highlight change)" — the flash
            // helpers never hide an existing flash when the target is missing.
            return false;
        }

        /// <summary>
        /// ShowWindow (0x538c40): already flag-visible → just re-show (the
        /// animation keeps running, no phase reset). Otherwise reset the
        /// phase/direction and arm the timer (interval 167 ms).
        /// </summary>
        private void ShowWindow()
        {
            if (!Visible)
            {
                Phase = 0;                       // +0xdc = 0
                Direction = 1;                   // +0xd8 = 1
                ArmTimer();
                Visible = true;
            }
        }

        /// <summary>HideWindow (0x538b90): unsubscribe, then hide. Never
        /// auto-invoked by the animation itself.</summary>
        private void HideWindow()
        {
            TimerSubscribed = false;
            Visible = false;
        }

        private void ArmTimer()
        {
            PhaseStarted = Clock();
            TimerSubscribed = true;              // +0xe0 = 1
        }

        /// <summary>
        /// The DoModalWin law (skeptic C2): modal entry remembers a showing
        /// highlighter and hides it; after the modal loop exits it is restored
        /// through ShowWindow (a fresh phase-0 ping-pong).
        /// </summary>
        public void SetModalObscured(bool modal)
        {
            if (modal && !ObscuredByModal)
            {
                ObscuredByModal = true;
                RestoreAfterModal = Visible;
                if (Visible) HideWindow();
            }
            else if (!modal && ObscuredByModal)
            {
                ObscuredByModal = false;
                if (RestoreAfterModal)
                {
                    RestoreAfterModal = false;
                    ShowWindow();
                    PullToFront();
                }
            }
        }

        /// <summary>TSOnTimerMsg (0x538d40) steps, driven by the clock. Each
        /// tick re-arms explicitly; the phase ping-pongs 0..N-1..0 forever
        /// until HideWindow stops it (skeptic C7: the up-flip needs dir&lt;0
        /// strictly — ShowWindow sets dir=+1, so dir==0 is unreachable).</summary>
        internal void Advance()
        {
            if (!TimerSubscribed || ObscuredByModal) return;
            long step = PhaseIntervalMs;
            while (Clock() - PhaseStarted >= step)
            {
                PhaseStarted += step;            // re-subscribe with the same interval
                if (Direction > 0 && Phase == Frames - 1) Direction = -1;
                else if (Direction < 0 && Phase == 0) Direction = 1;
                Phase += Direction;              // +0xdc, then invalidate + re-arm
            }
        }

        /// <summary>
        /// CleanUpWindowReferences law: a destroyed target leaves a clean slot.
        /// Death is polled (an element carries no disposal event); a target
        /// removed from its container clears the flash on the next tick.
        /// </summary>
        private void CheckTargetAlive()
        {
            if (TargetElement == null) return;
            // This UI framework never nulls Parent on removal (UIContainer
            // .Remove keeps its null-out commented out), so Parent==null only
            // catches never-parented targets. Real detachment is membership:
            // a target is gone when it has no parent or its parent's child
            // list no longer contains it.
            if (TargetElement.Parent == null
                || !TargetElement.Parent.GetChildren().Contains(TargetElement))
            {
                Clear();
            }
        }

        public override void Update(FSO.Common.Rendering.Framework.Model.UpdateState state)
        {
            Advance();
            CheckTargetAlive();
            base.Update(state);
        }

        /// <summary>
        /// TSPaint (0x539020): blit slice[phase] of the 150x50 strip at the
        /// window position — 50x50 logical, integer position, no scaling, no
        /// alpha (the loader's magenta key owns transparency).
        /// </summary>
        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible || Strip == null) return;
            base.Draw(batch);
            var source = new Rectangle(Phase * FrameSize, 0, FrameSize, FrameSize);
            DrawLocalTexture(batch, Strip, source, new Vector2(AnchorRect.X, AnchorRect.Y));
        }

        /// <summary>PullToFront (slot 0x4c): UIContainer.Add re-appends an
        /// already-contained child at the topmost position.</summary>
        private void PullToFront()
        {
            Parent?.Add(this);
        }

        /// <summary>Shutdown (0x5391a0) + slot release: a clean winmgr slot.</summary>
        public void Dispose()
        {
            Clear();
            Strip = null;
        }
    }
}
