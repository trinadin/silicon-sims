using System.Collections.Generic;

namespace Simitone.Client.UI.Model
{
    /// <summary>
    /// AUD-17 B-12: the native budget and help windows run under
    /// SetBlockSimulator + DoSimsModalDialog (UIDesktopUCP's own R161 decode
    /// of cWinViewControl::TSOnCommand) — the simulator pauses while they are
    /// open. This port's windows are UI-level modals that the engine does not
    /// know about, so the pause is applied here, keyed by dialog instance.
    /// Resume only restores when the engine is still at the pause we set
    /// (a lot exit or user pause in between wins).
    /// </summary>
    internal static class UIModalSimPause
    {
        private static readonly Dictionary<object, int> SavedSpeed = new Dictionary<object, int>();
        private static readonly Dictionary<object, FSO.SimAntics.VM> PausedVM = new Dictionary<object, FSO.SimAntics.VM>();

        public static void Pause(object key, FSO.SimAntics.VM vm)
        {
            if (vm == null || SavedSpeed.ContainsKey(key)) return;
            var speed = vm.SpeedMultiplier;
            SavedSpeed[key] = speed;
            PausedVM[key] = vm;
            if (speed > 0) vm.SpeedMultiplier = 0;
            // ENG-28b: natively these windows run under SetBlockSimulator —
            // the +0x111 pump gate that stops cSimulator::Simulate outright.
            // The counter freezes the pump even over a button pause (the
            // speed stays 0-with-flag; the flag resumes on Resume).
            vm.ModalPumpBlock++;
        }

        public static void Resume(object key)
        {
            if (!SavedSpeed.TryGetValue(key, out var speed)) return;
            SavedSpeed.Remove(key);
            var vm = PausedVM[key];
            PausedVM.Remove(key);
            if (vm != null)
            {
                if (vm.ModalPumpBlock > 0) vm.ModalPumpBlock--;
                if (vm.SpeedMultiplier == 0 && speed > 0) vm.SpeedMultiplier = speed;
            }
        }
    }
}
