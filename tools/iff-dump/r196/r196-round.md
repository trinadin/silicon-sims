# R196 — UIBigButton → WinBtn on the desktop path (Tier B item 1)

R159 census finding 31: `UIBigButton` still bound the mobile
`button.png`/`greenbutton.png` art on the DESKTOP path for the
dialog-family panels — `UIHouseSelectPanel` (EnterLot/More/option rows),
`UICallNeighborAlert` (Call/Cancel), `UISelectSkinAlert` (OK/Cancel).
The captions were already original glyphs on the original ObjDialogs
strings (R112); the ART was the gap.

## Law

No new decode — the R142 dialog law already covers it: the system push
button is SMCtrlMgrRes id 17 `shared\sys\WinBtn.bmp` 260x33 = 4 states of
65x33 (normal/hilite/pressed/disabled); the engine stretches one state
buffer to the button rect. The port's `UIButton` already implements the
4-state cell selection (disabled=3, selected=1, ForceState) with the
3-slice edge-preserving stretch, so the port is the art swap on the
desktop predicate (`!FSOEnvironment.SoftwareKeyboard`, the
`UICategorySwitcher.OriginalChrome` convention):

```
Texture = UIOriginalDialogChrome.GetWinBtn();  // 260x33
ImageStates = 4;
CaptionStyle.Color = Color.White;              // glyph captions on WinBtn
```

The original has ONE system button art: the 'green' variant keeps no art
distinction (caption emphasis only) — disclosed. Widths set by the call
sites (275px alert buttons, the wide EnterLot) stretch through the
3-slice law; buttons without an explicit width render at WinBtn's natural
65px. Touch keeps the mobile pngs unchanged.

## Gate

`uibigbtn`: the WinBtn art pin + a live construction pair (green and
plain) proving the sheet mount (260x33, shared texture), ImageStates 4,
White caption color, and the mount counter. uiphone/uidlgchrome
re-verified (targeted soak 8/8); FULL DEFAULT GATE **115 passed,
0 failed** with carseek PASS and the clean Run/Dispose exit-probe chain;
dist DLLs byte-match publish (Simitone.Client c2c38f65, FSO.SimAntics
4aa42502, FSO.Client 647abf9c).

## Residuals (disclosed)

- No green-art distinction in the original system button (both variants
  share WinBtn).
- HouseSelect option-row buttons without explicit widths render at the
  natural 65px (port layout, engine button law).
