namespace FSO.SimAntics.Model
{
    /// <summary>
    /// Engine-side mirror of the window-manager state that the TS1 tutorial
    /// event poll consumes. The native Tut_CheckForEvents (0x156550) reads
    /// cTSWinMgrW95/CPState/world state directly; in the port the client UI
    /// publishes this snapshot and <see cref="TutorialEventPoller"/> evaluates
    /// the jump-table conditions against it.
    ///
    /// Threading: single WRITER (the UI thread that owns the windows) publishes
    /// plain field writes; the READER is <see cref="TutorialEventPoller.Poll"/>,
    /// which must be ticked from the same thread that ticks the VM (the game
    /// update loop, the port's cDDDSimsView::Simulate). Fields are intentionally
    /// plain — every condition is re-evaluated from scratch on each poll
    /// (decode.md §2 "throttle/idempotence law"), so a torn/stale read costs at
    /// most one poll's worth of latency and no structural state. Do not cache
    /// derived state from these fields anywhere.
    /// </summary>
    public class TutorialUIState
    {
        /// <summary>
        /// True while the UI's control-panel state exists and its mode is 0
        /// (CPState::GetCPMode 0x20c9f0 = return +0x220, compared == 0 by
        /// event code 1).
        /// </summary>
        public bool CpModeIsZero;

        /// <summary>
        /// The person panel's current page caption, compared verbatim against
        /// the native literal captions (second jump table at data 0x4ab78,
        /// skeptic-corrected): event 2 = "motives", 9 = "rel", 11 = "job",
        /// 13 = "skill", 15 = "per.ity". Null means the person panel, its page
        /// button (+0x104) or its visible byte (+0xd8) is gone — the inverse
        /// condition event 14 tests. Use the exact tokens; the native compares
        /// StringBuffer equality (0x141e90).
        /// </summary>
        public string PersonPanelPage;

        /// <summary>
        /// Image/buffer id of the last-activated button (BSS 0x9777c, written
        /// by cTSWinBtn::TSOnMouseDownL 0x50bf40), -1 when none. The poll also
        /// mirrors this into simulator global 12 every tick (see
        /// TutorialEventPoller.MirrorLastButton); event 4 compares it with the
        /// awaited param.
        /// </summary>
        public int LastButtonClickImageId = -1;

        /// <summary>
        /// Image id of the button the cursor is over/activating with a nonzero
        /// state byte +0xd8 (cTSWinBtn::SetState 0x50b350 — an int in the
        /// native, skeptic C7), -1 when none. Event 8 compares it with the
        /// awaited param.
        /// </summary>
        public int HoverButtonImageId = -1;

        /// <summary>
        /// Neighbor id whose portrait buffer is currently the person-panel
        /// portrait (the native compares PersonFinder::GetPictureBuffer
        /// identity against the last-activated button's buffer, event 5);
        /// 0 or 0xFFFFFFFF when none. The port identifies the buffer by the
        /// neighbor it belongs to — buffer identity itself is UI-internal.
        /// </summary>
        public uint ShownPortraitNeighborId;

        /// <summary>
        /// Raw current value of the UI rotation/altitude indicator (world+0x84,
        /// the "altitude-level word" — event 6's change detector per BCON 4097
        /// usage). NOT a pre-computed edge: the poller keeps the native
        /// TOC-0x25a0 latch (statically -1, SHARED between events 6 and 7,
        /// skeptic C7) and detects the change itself.
        /// </summary>
        public int RotationEdge;

        /// <summary>
        /// Raw current value behind event 7's change detector
        /// (*(*(TOC-0x6fcc)) first word — singleton identity unresolved in the
        /// native, decode.md §0; the port maps it to the UI's scroll/zoom
        /// value per the BCON usage). Shares the poller's -1-initialized latch
        /// with RotationEdge, exactly as the native does.
        /// </summary>
        public int ScrollEdge;
    }
}
