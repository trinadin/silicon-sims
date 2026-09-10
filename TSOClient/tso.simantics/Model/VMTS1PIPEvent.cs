namespace FSO.SimAntics.Model
{
    /// <summary>A TS1 user-event camera request, independent of the renderer.</summary>
    public sealed class VMTS1PIPEvent
    {
        public VMEntity Target { get; }
        public short ObjectID { get; }
        public VMEntity Caller { get; }
        public bool Open { get; }
        public bool SkipIfVisible { get; }
        public int DurationMilliseconds { get; }
        public int SizeIndex { get; }
        public int ZoomIndex { get; }
        public bool MainCameraRoute { get; }
        public bool AutoSnapshot { get; }
        public string Caption { get; }
        public bool SuppressPreDispatchNotification { get; }

        // Original LivePIP filtering occurs before dispatch's second yield.
        // The synchronous client listener can report that options suppressed it.
        public bool Suppressed { get; set; }

        public VMTS1PIPEvent(VMEntity target, bool open, bool skipIfVisible,
            int durationMilliseconds, int sizeIndex, int zoomIndex,
            bool mainCameraRoute, bool autoSnapshot, string caption,
            bool suppressPreDispatchNotification = false, VMEntity caller = null)
        {
            Target = target;
            ObjectID = target?.ObjectID ?? 0;
            Caller = caller;
            Open = open;
            SkipIfVisible = skipIfVisible;
            DurationMilliseconds = durationMilliseconds;
            SizeIndex = sizeIndex;
            ZoomIndex = zoomIndex;
            MainCameraRoute = mainCameraRoute;
            AutoSnapshot = autoSnapshot;
            Caption = caption ?? "";
            SuppressPreDispatchNotification = suppressPreDispatchNotification;
        }
    }
}
