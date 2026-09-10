using System;

namespace Simitone.Client.UI.Panels
{
    /// ROUND-208 'screen switch law' (tools/iff-dump/r207/r208-screen-switch-law.md):
    /// the engine's base-game screen switches are DIRECT constructions — no
    /// wipe, no transit effect. cWinPickFamily::HandleButton 0x2d8f30 builds
    /// cWinDesignFamily (new + ctor 0x2d2150 + AddChild virtuals,
    /// r208-disasm-pickfamily-handlebutton.txt); cWinDesignFamily::TSOnCommand
    /// 0x2d0658/0x2d08a4 builds cWinDesignCharacter the same way; the
    /// done-path runs LoadGame directly. The ONLY full-screen transit art in
    /// the engine is the taxi/eTransitScreens family
    /// (cSimsApp::CreateTaxiDialog 0x24bb50 + cWinPictureSplashDialog ctor
    /// 0x40abf0 — its single ctor caller), which serves the EXPANSION
    /// destinations (downtown/vacation/studio/magic), not base-game CAS.
    /// The port's diagonal-stripe UITransDialog (trans_cas/trans_normal) was
    /// a Simitone invention (R159 census finding 4) — retired R208.
    public static class ScreenSwitchLaw
    {
        public const string PickFamilyHandleButton = "0x2d8f30";
        public const string DesignFamilyCtor = "0x2d2150";
        public const string DesignFamilyTSOnCommand = "0x2d0658/0x2d08a4";
        public const string TaxiDialogCtor = "0x24bb50";
        public const string PictureSplashCtor = "0x40abf0";
        public const bool BaseGameTransitArtExists = false;
    }
}
