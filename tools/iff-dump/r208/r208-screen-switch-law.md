# R208 — The screen-switch law: the invented transition wipes retire

R159 census finding 4: "Neighborhood→CAS/downtown transition — UITransDialog
uses trans_{type}.png + UIDiagonalStripe wipe; the Simitone transition effect.
Canon: none named (engine transition effect undecoded)."

## Engine decode — base-game switches are DIRECT

- `cWinPickFamily::HandleButton(int)` @ **0x2d8f30** (776 B): the create-family
  path is `new(?)` + **cWinDesignFamily ctor 0x2d2150** + a run of AddChild
  virtuals — the next screen is CONSTRUCTED in place, no wipe, no splash
  (r208-disasm-pickfamily-handlebutton.txt).
- `cWinDesignFamily::TSOnCommand` @0x2d0658/0x2d08a4 constructs
  cWinDesignCharacter the same way (caller scan r208).
- The done-path runs LoadGame directly (the R197 move-in chain).
- The ONLY full-screen transit art in the engine is the taxi/eTransitScreens
  family — `cSimsApp::CreateTaxiDialog` 0x24bb50 → `cWinPictureSplashDialog`
  ctor 0x40abf0 (its single ctor caller) — serving the EXPANSION destinations
  (downtown/vacation/studio/magic per ShowXXXDialog), NOT base-game CAS.

## Port

- Neighborhood→CAS ("MoveIn" navbar slot): the `UITransDialog("cas", …)` wipe
  replaced with the direct `GameController.EnterCAS()`.
- CAS→game (SetMode ToNeighborhood): the `UITransDialog("normal", …)` wipe
  replaced with the direct CleanupLastWorld + EnterGameMode — the lot load
  shows the canon loading splash (uisplash surface).
- `UITransDialog.cs` DELETED (both call sites were its only users).
- `ScreenSwitchLaw` static class carries the decoded facts in the shipping
  source (gate-pinned).

## Gate

`uitrans` (default suite): the invented type is GONE
(`Type.GetType("…UITransDialog") == null` — an honest retirement pin), and the
law constants stand (PickFamilyHandleButton 0x2d8f30, DesignFamilyCtor
0x2d2150, BaseGameTransitArtExists == false).

## Deferred half

The Vita idle-animation cycling (cWinVitaBtnSolo::Build{Adult,Child,Cat,Dog}
AnimationList 0x2da500/0x2da6a0/0x2da860/0x2da9e0 — static global string lists
at -0x4cd4(r2) copied into this+0x218 with 0x104-byte cTSString stride,
gender-split at person->0x60e; the cycling/timer law in
UpdateTransform/AnimatePet) is R209 — a full subsystem round on its own.
r208-disasm-vita-adultanimlist.txt saves the list-builder head.

No proprietary payload.
