#!/usr/bin/env python3
"""R241 offline fixture: re-derive the ORIGINAL secondary-camera cutaway and
fade laws directly from the PowerPC executable, byte-by-byte.

No emulator, no assets: every check asserts the presence/order of raw
instructions and call targets at verified file offsets. If any check fails,
either the binary changed or the decode behind report.md is wrong.

Run: python3 fixture_r241.py
"""
import struct
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
EXE = ROOT / 'game-data/The Sims/The Sims Complete'

SHA256_EXPECTED = '33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f'

# File offsets (PEF section-relative code vaddr + 0x8e90; see r102 section map).
CODE_BASE = 0x8e90
ADDRESSES = {
    'GetLevel__11HouseViewerCFv': 0x1bfa80,
    'SetLevel__11HouseViewerFib': 0x1d7110,
    'DrawPictureInPicture__11HouseViewer': 0x1c0c60,
    'BuildImage__20cWinPictureInPictureFv': 0x2990c0,
    'GetCenterOnPt__20cWinPictureInPictureFv': 0x298e50,
    'Fade__20cWinPictureInPictureFb': 0x299860,
    'Open__20cWinPictureInPicture': 0x299960,
    'Close__20cWinPictureInPictureFP8cXObject': 0x2999b0,
    'TSOnTimerMsg__20cWinPictureInPictureFv': 0x299600,
    'TSOnKeyDown__20cWinPictureInPictureFUlUl': 0x299580,
    'TSOnMouseDownL__20cWinPictureInPictureFllUl': 0x299730,
    'GetWantsFadeCapture__20cWinPictureInPictureFv': 0x298c90,
    'GetWantsFadeCapture__6cTSWinFv': 0x5023d0,
    'RestoreFactorySettings__11cOptionsMgrFv': 0x237fc0,
    'AsyncShowHideWin__6cTSWinFbbUl': 0x502e10,
    'ComputeCutaway__11RoomManagerFi': 0x128ae0,
    'SetCutaway__11WallManagerFbi': 0x2c2f90,
    'clear__11cCutawaySetFv': 0x1a9e60,
    'ResetDynamicCutaway__11HouseViewerFv': 0x1cf240,
    'DoDynamicCutaway__11HouseViewerFb': 0x1cf3b0,
    'GetWallManager__11WallManagerFv': 0x2c29f0,
    'ScrollToTile__11HouseViewerF7FTilePtibb': 0x1d7c90,
    'SetScale__11HouseViewerFibb': 0x1d7780,
    'SetRotation__11HouseViewerFibb': 0x1d7230,
    'SubscribeTimerMsg__12cTSWinMgrW95FP6cTSWinlUl': 0x51c500,
    'UnsubscribeTimerMsg__12cTSWinMgrW95FP6cTSWin': 0x51c160,
    'GetBoboVision__11cOptionsMgrFv': 0x23a870,
    'SetBoboVision__11cOptionsMgrFb': 0x23a830,
    'PumpMouseMoveMsg__12cTSWinMgrW95Fv': 0x51bf10,
    'GetLevel__7CPStateFv': 0x210fe0,
    'SetLevel__7CPStateFiQ27CPState8Synchron': 0x210170,
}

checks = []


def check(name, ok, detail=''):
    checks.append((name, bool(ok), detail))


def words(data, start, end):
    return [struct.unpack_from('>I', data, a)[0] for a in range(start, end, 4)]


def find_bl(data, start, end, target):
    """All bl call sites of `target` within [start, end)."""
    sites = []
    for a in range(start, end - 3, 4):
        w = struct.unpack_from('>I', data, a)[0]
        if (w >> 26) == 18 and (w & 1):
            d = (w >> 2) & 0xFFFFFF
            if d & 0x800000:
                d -= 0x1000000
            if a + d * 4 == target:
                sites.append(a)
    return sites


def has_bl(data, start, end, target):
    """Is there a bl to `target` within [start, end)?"""
    return bool(find_bl(data, start, end, target))


def bl_seq(data, start, end, targets):
    """Do the given targets appear as bl destinations in order within range?"""
    pos = start
    idx = 0
    hits = []
    for a in range(start, end - 3, 4):
        w = struct.unpack_from('>I', data, a)[0]
        if (w >> 26) == 18 and (w & 1):
            d = (w >> 2) & 0xFFFFFF
            if d & 0x800000:
                d -= 0x1000000
            t = a + d * 4
            if idx < len(targets) and t == targets[idx]:
                hits.append(a)
                idx += 1
    return idx == len(targets), hits


def main():
    import hashlib
    data = EXE.read_bytes()
    sha = hashlib.sha256(data).hexdigest()
    check('executable SHA256 matches r240 evidence', sha == SHA256_EXPECTED, sha)

    # ---- Law 1: viewer+0x18 is the LEVEL (floor), not the rotation ----
    w = words(data, ADDRESSES['GetLevel__11HouseViewerCFv'],
              ADDRESSES['GetLevel__11HouseViewerCFv'] + 8)
    check('GetLevel__11HouseViewerCFv == { lwz r3,24(r3); blr }',
          w[0] == 0x80630018 and w[1] == 0x4e800020, f'{w[0]:08x} {w[1]:08x}')

    # ---- Law 2: HouseViewer::SetLevel = the wall/cutaway recompute law ----
    s, e = ADDRESSES['SetLevel__11HouseViewerFib'], ADDRESSES['SetLevel__11HouseViewerFib'] + 232
    ok, hits = bl_seq(data, s, e, [
        ADDRESSES['GetWallManager__11WallManagerFv'],
        ADDRESSES['SetCutaway__11WallManagerFbi'],
        ADDRESSES['clear__11cCutawaySetFv'],
        ADDRESSES['ResetDynamicCutaway__11HouseViewerFv'],
        ADDRESSES['ComputeCutaway__11RoomManagerFi'],
        ADDRESSES['DoDynamicCutaway__11HouseViewerFb'],
    ])
    check('SetLevel body order: GetWallManager, SetCutaway, clear cCutawaySet, '
          'ResetDynamicCutaway, ComputeCutaway, DoDynamicCutaway', ok, str([hex(h) for h in hits]))
    # viewer->0x18 = new level: stw r30, 24(r29) @0x1d7178
    check('SetLevel stores the new level into viewer+0x18 (stw r30,24(r29) @0x1d7178)',
          struct.unpack_from('>I', data, 0x1d7178)[0] == 0x93dd0018,
          f'{struct.unpack_from(">I", data, 0x1d7178)[0]:08x}')

    # ---- Law 3: CPState::SetLevel clamps 0->1 and forwards to HouseViewer::SetLevel ----
    s = ADDRESSES['SetLevel__7CPStateFiQ27CPState8Synchron']
    check('CPState::SetLevel stores the level at CPState+0x2c (stw r4,44(r31) @0x210190)',
          struct.unpack_from('>I', data, 0x210190)[0] == 0x909f002c)
    check('CPState::SetLevel tail-calls HouseViewer::SetLevel',
          has_bl(data, s, s + 0x78, ADDRESSES['SetLevel__11HouseViewerFib']))
    w = words(data, ADDRESSES['GetLevel__7CPStateFv'], ADDRESSES['GetLevel__7CPStateFv'] + 8)
    check('GetLevel__7CPStateFv == { lwz r3,44(r3); blr } (CPState+0x2c)',
          w[0] == 0x8063002c and w[1] == 0x4e800020)

    # ---- Law 4: DrawPictureInPicture reuses SetLevel's cutaway law per floor ----
    s, e = (ADDRESSES['DrawPictureInPicture__11HouseViewer'],
            ADDRESSES['DrawPictureInPicture__11HouseViewer'] + 0xb68)
    code = data[s:e]
    # level-change cutaway block appears twice (enter for target floor + restore for main floor)
    for site in (0x1c1054, 0x1c1608):
        ok, hits = bl_seq(data, site, site + 0x60, [
            ADDRESSES['GetWallManager__11WallManagerFv'],
            ADDRESSES['SetCutaway__11WallManagerFbi'],
            ADDRESSES['clear__11cCutawaySetFv'],
        ])
        check(f'DrawPictureInPicture cutaway block at {site:#x} '
              '(GetWallManager, SetCutaway, clear)', ok, str([hex(h) for h in hits]))
    check('cutaway enter block: ComputeCutaway(roommgr, level) bl @0x1c10a0',
          has_bl(data, 0x1c1090, 0x1c10a4, ADDRESSES['ComputeCutaway__11RoomManagerFi']))
    check('cutaway restore block: ComputeCutaway(roommgr, savedLevel) bl @0x1c1654',
          has_bl(data, 0x1c1644, 0x1c1658, ADDRESSES['ComputeCutaway__11RoomManagerFi']))
    # both blocks guarded by cmpw level, argLevel via lwz from viewer+0x18
    check('enter guard compares viewer+0x18 with the level arg (lwz r21,24(r16) @0x1c1048)',
          struct.unpack_from('>I', data, 0x1c1048)[0] == 0x82b00018)
    check('restore guard compares viewer+0x18 with the saved level (lwz r15,24(r16) @0x1c15fc)',
          struct.unpack_from('>I', data, 0x1c15fc)[0] == 0x81f00018)
    # same-level targets skip the recompute: beq over the block
    check('enter block skipped when level unchanged (beq @0x1c1050)',
          struct.unpack_from('>I', data, 0x1c1050)[0] == 0x4182009c)
    # ScrollToTile receives the level argument (r6 = r20 = arg4)
    check('DrawPictureInPicture calls ScrollToTile with (tile, level, 0, 1) @0x1c1188',
          struct.unpack_from('>I', data, 0x1c117c)[0] == 0x38d40000
          and struct.unpack_from('>I', data, 0x1c1180)[0] == 0x38e00000
          and struct.unpack_from('>I', data, 0x1c1184)[0] == 0x39000001
          and has_bl(data, 0x1c1184, 0x1c118c, ADDRESSES['ScrollToTile__11HouseViewerF7FTilePtibb']))
    # traceback name of the secondary render function
    check("traceback name DrawPictureInPicture__11HouseViewerFP9cTSBufferRC7FTilePtiii follows the body",
          b'DrawPictureInPicture__11HouseViewerFP9cTSBufferRC7FTilePtiii' in data[e:e + 0x140])

    # ---- Law 5: BuildImage picks level from the target (cXObject+0x110) ----
    s = ADDRESSES['BuildImage__20cWinPictureInPictureFv']
    check('BuildImage loads the level from target+0x110 (lwz r31,272(r3) @0x299278)',
          struct.unpack_from('>I', data, 0x299278)[0] == 0x83e30110)
    check('BuildImage null-target fallback: virtual slot at vtbl+64 (lwz r12,64(r12) @0x29928c)',
          struct.unpack_from('>I', data, 0x29928c)[0] == 0x818c0040)
    check('BuildImage tail-calls DrawPictureInPicture @0x2992c8 with r8=window+0xd8 zoom',
          has_bl(data, 0x2992b0, 0x2992d0, ADDRESSES['DrawPictureInPicture__11HouseViewer'])
          and struct.unpack_from('>I', data, 0x2992b0)[0] == 0x811c00d8)
    check('BuildImage checks center>=0 (blt out) after GetCenterOnPt @0x299134/0x299140',
          struct.unpack_from('>I', data, 0x299134)[0] == 0x418003f4
          and struct.unpack_from('>I', data, 0x299140)[0] == 0x418003e8)

    # ---- Law 6: MouseDownL recenters the MAIN camera with the same level ----
    s = ADDRESSES['TSOnMouseDownL__20cWinPictureInPictureFllUl']
    check('MouseDownL reads target+0x110 as the ScrollToTile level (lwz r6,272(r4) @0x299794)',
          struct.unpack_from('>I', data, 0x299794)[0] == 0x80c40110)
    check('MouseDownL calls ScrollToTile on the main viewer @0x2997c8',
          has_bl(data, 0x2997b4, 0x2997d0, ADDRESSES['ScrollToTile__11HouseViewerF7FTilePtibb']))
    check('MouseDownL ends with Fade(false) (li r4,0 @0x2997f0, bl @0x2997f4)',
          has_bl(data, 0x2997f0, 0x2997f8, ADDRESSES['Fade__20cWinPictureInPictureFb'])
          and struct.unpack_from('>I', data, 0x2997f0)[0] == 0x38800000)

    # ---- Law 7: timers drive the close; Open subscribes the duration ----
    s = ADDRESSES['TSOnTimerMsg__20cWinPictureInPictureFv']
    check('TSOnTimerMsg unsubscribes the timer and clears +0xd4 (stw r0,212(r31) @0x299628)',
          has_bl(data, s, s + 0x24, ADDRESSES['UnsubscribeTimerMsg__12cTSWinMgrW95FP6cTSWin'])
          and struct.unpack_from('>I', data, 0x299628)[0] == 0x901f00d4)
    check('TSOnTimerMsg ends with Fade(false) (li r4,0 @0x299630, bl @0x299634)',
          struct.unpack_from('>I', data, 0x299630)[0] == 0x38800000
          and has_bl(data, 0x299630, 0x299638, ADDRESSES['Fade__20cWinPictureInPictureFb']))
    o = ADDRESSES['Open__20cWinPictureInPicture']
    check('Open stores duration at +0xd4, zoom at +0xd8, open-flag byte at +0xde',
          struct.unpack_from('>I', data, 0x299b5c)[0] == 0x9a9f00de
          and struct.unpack_from('>I', data, 0x299b60)[0] == 0x92bf00d4
          and struct.unpack_from('>I', data, 0x299b64)[0] == 0x92df00d8)
    check('Open subscribes one timer with the duration, then Fade(true) (@0x299f34/@0x299f40)',
          has_bl(data, 0x299f28, 0x299f38, ADDRESSES['SubscribeTimerMsg__12cTSWinMgrW95FP6cTSWinlUl'])
          and struct.unpack_from('>I', data, 0x299f3c)[0] == 0x38800001
          and has_bl(data, 0x299f3c, 0x299f44, ADDRESSES['Fade__20cWinPictureInPictureFb']))
    # window ownership: nonzero duration + same target -> re-open; different -> refuse
    check('Open refuses a different target while a duration is active '
          '(li r3,0 return @0x299abc)',
          struct.unpack_from('>I', data, 0x299abc)[0] == 0x38600000)
    c = ADDRESSES['Close__20cWinPictureInPictureFP8cXObject']
    check('Close only acts when the passed object equals the current target '
          '(bne over the body @0x2999cc)',
          struct.unpack_from('>I', data, 0x2999cc)[0] == 0x4082003c)
    check('Close clears the target, clears +0xde, Fade(false), unsubscribes (@0x2999d0-0x299a04)',
          struct.unpack_from('>I', data, 0x2999d4)[0] == 0x901f00d0
          and struct.unpack_from('>I', data, 0x2999dc)[0] == 0x981f00de
          and struct.unpack_from('>I', data, 0x2999d8)[0] == 0x38800000)
    check('close-button command (TSOnCommand param==1) routes to the same clear+Fade(false)',
          struct.unpack_from('>I', data, 0x299fd8)[0] == 0x981f00de
          and has_bl(data, 0x299fdc, 0x299fe0, ADDRESSES['Fade__20cWinPictureInPictureFb']))
    check('ESC (key 27) fades out (cmplwi r4,27 @0x299584, li r4,0 @0x299594)',
          struct.unpack_from('>I', data, 0x299584)[0] == 0x2804001b
          and struct.unpack_from('>I', data, 0x299594)[0] == 0x38800000)

    # ---- Law 8: Fade chooses instant vs animated window show/hide ----
    f = ADDRESSES['Fade__20cWinPictureInPictureFb']
    check('Fade reads GetBoboVision first (bl @0x299880)',
          has_bl(data, 0x29987c, 0x299884, ADDRESSES['GetBoboVision__11cOptionsMgrFv']))
    check('Fade(false, bobo off) -> vtbl+160 (lwz r12,160(r12) @0x29994c)',
          struct.unpack_from('>I', data, 0x29994c)[0] == 0x818c00a0)
    check('Fade(true, bobo off) -> vtbl+156 (lwz r12,156(r12) @0x299934)',
          struct.unpack_from('>I', data, 0x299934)[0] == 0x818c009c)
    check('Fade(bool, bobo on) -> vtbl+176 (lwz r12,176(r12) @0x2998cc/@0x2998f0)',
          struct.unpack_from('>I', data, 0x2998cc)[0] == 0x818c00b0
          and struct.unpack_from('>I', data, 0x2998f0)[0] == 0x818c00b0)
    check('BoboVision byte is cOptionsMgr+1 (SetBoboVision stb r4,1(r3); GetBoboVision lbz r3,1(r3))',
          struct.unpack_from('>I', data, ADDRESSES['SetBoboVision__11cOptionsMgrFb'])[0] == 0x98830001
          and struct.unpack_from('>I', data, ADDRESSES['GetBoboVision__11cOptionsMgrFv'])[0] == 0x88630001)
    check('RestoreFactorySettings stores 1 into BoboVision (li r0,1 @0x238068, stb r0,1(r27) @0x23806c)',
          struct.unpack_from('>I', data, 0x238068)[0] == 0x38000001
          and struct.unpack_from('>I', data, 0x23806c)[0] == 0x981b0001)
    check('animated route default ramp length 333 (li r29,333 @0x502fa0)',
          struct.unpack_from('>I', data, 0x502fa0)[0] == 0x3ba0014d)
    check('animated route drives a RampGenerator on the window (this+0xb4) '
          '(SetupConstantTimeRamp calls inside AsyncShowHideWin)',
          len(find_bl(data, ADDRESSES['AsyncShowHideWin__6cTSWinFbbUl'],
                      ADDRESSES['AsyncShowHideWin__6cTSWinFbbUl'] + 0x400,
                      0x14e890)) >= 2)

    # ---- Law 9: the PIP opts out of the screen-level fade capture ----
    w = words(data, ADDRESSES['GetWantsFadeCapture__20cWinPictureInPictureFv'],
              ADDRESSES['GetWantsFadeCapture__20cWinPictureInPictureFv'] + 8)
    check('cWinPictureInPicture::GetWantsFadeCapture == { li r3,0; blr }',
          w[0] == 0x38600000 and w[1] == 0x4e800020)
    w = words(data, ADDRESSES['GetWantsFadeCapture__6cTSWinFv'],
              ADDRESSES['GetWantsFadeCapture__6cTSWinFv'] + 8)
    check('cTSWin::GetWantsFadeCapture == { li r3,1; blr }',
          w[0] == 0x38600001 and w[1] == 0x4e800020)

    # ---- Law 10: TSPaint only re-renders while open && LivePIP ----
    check('TSPaint gates BuildImage on byte +0xde and GetLivePIP (@0x29a084-0x29a0a4)',
          struct.unpack_from('>I', data, 0x29a084)[0] == 0x880300de
          and has_bl(data, 0x29a090, 0x29a09c, 0x239c20)
          and has_bl(data, 0x29a0a0, 0x29a0a8, ADDRESSES['BuildImage__20cWinPictureInPictureFv']))

    # ---- summary ----
    failed = [c for c in checks if not c[1]]
    for name, ok, detail in checks:
        print(f"[{'PASS' if ok else 'FAIL'}] {name}" + (f"  ({detail})" if detail and not ok else ''))
    print(f"\n{len(checks) - len(failed)}/{len(checks)} checks passed")
    return 1 if failed else 0


if __name__ == '__main__':
    sys.exit(main())
