#!/usr/bin/env python3
"""UI-25 — pins for the four cutaway-orphan decodes.

Read-only on the owner executable; writes nothing. Exit 0 = all pins hold.

Covers:
  1. BuildRotationLookup__7CTilePtFv (file 0x60f20): the BSS 0x7F0B8 generator —
     instruction pins + full simulation of the decoded entry law.
  3. The identity: outside-person remap (0x1cf7bc) reads the SAME base
     (TOC-0x73E4 -> BSS 0x7F0B8) with page term world+0x84 (rotation), while
     BuildMaxAltsTable (0x1c6b70) builds unrelated 128-byte viewer arrays.
  2. Base cTool::AdjustCutawayForTool (0x191910) / cMoveTool (0x174100) pins.
  4. DrawPictureInPicture state save/restore pins (0x1c0c64...).
"""
import struct, sys

BIN = '/Users/nathannoom/Developer/Games/The Sims/simitone-fork/game-data/The Sims/The Sims Complete'
EXPECT_SHA = '33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f'
import hashlib
data = open(BIN, 'rb').read()
sha = hashlib.sha256(data).hexdigest()
assert sha == EXPECT_SHA, f'sha mismatch: {sha}'
print(f'sha256 ok: {EXPECT_SHA}')

fails = []
def word(addr):
    return struct.unpack_from('>I', data, addr)[0]

def pin(name, addr, expect, mask=0xFFFFFFFF):
    got = word(addr) & mask
    ok = got == (expect & mask)
    if not ok:
        fails.append(name)
    print(f'  {"ok " if ok else "FAIL"} {name} @{addr:08x}: {got:08x} (want {expect & mask:08x})')

print('== item 1: BuildRotationLookup (BSS 0x7F0B8 generator) ==')
pin('base load TOC-0x73e4',        0x60f24, 0x80c28c1c)
pin('outer page loop cmpwi r11,3', 0x61064, 0x2c0b0003)
pin('page advance +=0x2000',       0x61060, 0x38c62000)
pin('rot==2 case (0x60f94)',       0x60f94, 0x39030000)  # addi r8, r3, 0   (r3 = 63-x)
pin('rot==2 y (0x60f98)',          0x60f98, 0x20e9003f)  # subfic r7, r9, 63 (63-y)
pin('rot==1 x (0x60f88)',          0x60f88, 0x38ea0000)  # addi r7, r10, 0  (x)
pin('rot==1 y (0x60f8c)',          0x60f8c, 0x2109003f)  # subfic r8, r9, 63 (63-y)
pin('rot==3 x (0x60fa0)',          0x60fa0, 0x38e30000)  # addi r7, r3, 0  (63-x)
pin('rot==3 y (0x60fa4)',          0x60fa4, 0x39090000)  # addi r8, r9, 0  (y)
pin('entry store b0 (0x60fa8)',    0x60fa8, 0x99040000)  # stb r8, 0(r4)
pin('entry store b1 (0x60fac)',    0x60fac, 0x98e40001)  # stb r7, 1(r4)
pin('oob store 0xff (0x60fb4)',    0x60fb4, 0x380000ff)
pin('sole bl caller (SetSize)',    0x164910, (18 << 26) | ((0x60f20 - 0x164910) & 0x03FFFFFC) | 1)

def build_lut():
    """Simulation of the decoded BuildRotationLookup law: 3 pages, 64x64x2."""
    pages = []
    for p in range(3):
        page = {}
        rot = p + 1
        for x in range(64):
            for y in range(64):
                if rot == 1:   xp, yp = 63 - y, x
                elif rot == 2: xp, yp = 63 - x, 63 - y
                else:          xp, yp = y, 63 - x
                page[(x, y)] = (xp, yp)
        pages.append(page)
    return pages

pages = build_lut()
# law checks: pure function of constants; 180-degree involution; rot1/rot3 inverse pair
for x in range(64):
    for y in range(64):
        assert pages[1][(x, y)] == (63 - x, 63 - y)
        fx, fy = pages[0][(x, y)]            # rot1 forward
        assert pages[2][(fx, fy)] == (x, y)  # rot3 undoes rot1
print('  ok  LUT simulation: rot2 involution, rot1 o rot3 = identity, domain 0..63')

print('== item 3: identity (outside-person remap vs BuildMaxAltsTable) ==')
pin('r22 = TOC-0x73e4 (DoDynamicCutaway)', 0x1cf3c0, 0x82c28c1c)
pin('read world+0x84 (rotation)',          0x1cf7c0, 0x80030084)
pin('page term rot<<13',                   0x1cf7d0, 0x54046824)  # slwi r4, r0, 0xd
pin('x<<7',                                0x1cf7e0, 0x54633830)  # slwi r3, r3, 7
pin('y<<1',                                0x1cf7ec, 0x5400083c)  # slwi r0, r0, 1
pin('read entry b0 (base-0x2000)',         0x1cf7f4, 0x8864e000)  # lbz r3, -0x2000(r4)
pin('read entry b1',                       0x1cf7f8, 0x8804e001)  # lbz r0, -0x1fff(r4)
pin('BuildMaxAltsTable: init b=64',        0x1c6ba0, 0x39600040)  # li r11, 0x40
pin('BuildMaxAltsTable: ptr arrayA',       0x1c6b98, 0x809801ec)  # lwz r4, 0x1ec(r24)
pin('BuildMaxAltsTable: ptr arrayB',       0x1c6bb4, 0x809801f0)  # lwz r4, 0x1f0(r24)
pin('BuildMaxAltsTable: bl GetPackedAlt',  0x1c6d04, (18 << 26) | ((0x161e00 - 0x1c6d04) & 0x03FFFFFC) | 1)
pin('BuildMaxAltsTable: diagonal add',     0x1c6d28, 0x7c050214)  # add r0, r5, r0
pin('SetRotation bl BuildMaxAltsTable',    0x1d74f4, (18 << 26) | ((0x1c6b70 - 0x1d74f4) & 0x03FFFFFC) | 1)
# no cutaway function appears in the viewer+0x1EC/0x1F0 accessor scan
for lo, hi, name in ((0x125130, 0x1259b0, 'ComputeCutawayMatrix'),
                     (0x1cef30, 0x1cf064, 'GetTheoreticalWallExtent'),
                     (0x1cf240, 0x1d0100, 'DoDynamicCutaway'),
                     (0x1d0310, 0x1d0660, 'MouseTrack')):
    hit = False
    for off in range(lo & ~3, hi, 4):
        w = word(off)
        if (w >> 26) in (32, 34, 36, 38) and (w & 0xFFFF) in (0x1EC, 0x1F0):
            hit = True
    print(f'  {"ok " if not hit else "FAIL"} {name}: no viewer+0x1EC/0x1F0 access')
    if hit: fails.append(name + ' maxalt access')

print('== item 2: AdjustCutawayForTool (slot 18) ==')
pin('dispatch lwz r12, 0x28(r3)',   0x1d0038, 0x81830028)
pin('slot 18 = TVector 0x48(r12)',  0x1d0044, 0x818c0048)
pin('base: gate tool+0x24',         0x191934, 0x88030024)
pin('base: bl GetWall',             0x1919c8, (18 << 26) | ((0x127d90 - 0x1919c8) & 0x03FFFFFC) | 1)
pin('base: set bit (mask helper)',  0x1919dc, (18 << 26) | ((0x591b10 - 0x1919dc) & 0x03FFFFFC) | 1)
pin('base: wall&1 -> +tool6/7',     0x191a1c, 0x887f0006)
pin('base: wall&2 -> +tool0/1',     0x191a9c, 0x887f0000)
pin('cMoveTool: reads LUT global',  0x174108, 0x83628c1c)
pin('cMoveTool: gate tool+0x3c',    0x174138, 0x8018003c)
pin('cMoveTool: tile >>4 (x)',      0x1741a8, 0x7c602670)
pin('cMoveTool: floor +0x110',      0x1741a4, 0x80a50110)
pin('ModifyCursorPos base = blr',   0x1579d0, 0x4e800020)

print('== item 4: DrawPictureInPicture state save/restore ==')
pin('enter: save tool global',        0x1c0e48, 0x901f0164)  # stw r0, 0x164(r31)
pin('enter: floor-diff branch',       0x1c1050, 0x4182009c)  # beq -> skip clear
pin('enter: clear history',           0x1c1068, (18 << 26) | ((0x1a9e60 - 0x1c1068) & 0x03FFFFFC) | 1)
pin('enter: set target floor',        0x1c1088, 0x92900018)
pin('restore: SetRotation',           0x1c15f4, (18 << 26) | ((0x1d7230 - 0x1c15f4) & 0x03FFFFFC) | 1)
pin('restore: saved-floor compare',   0x1c1600, 0x7c0f8800)
pin('restore: floor-diff branch',     0x1c1604, 0x4182009c)  # beq -> skip recompute
pin('restore: clear history',         0x1c161c, (18 << 26) | ((0x1a9e60 - 0x1c161c) & 0x03FFFFFC) | 1)
pin('restore: ResetDynamicCutaway',   0x1c1638, (18 << 26) | ((0x1cf240 - 0x1c1638) & 0x03FFFFFC) | 1)
pin('restore: restore floor',         0x1c163c, 0x92300018)
pin('restore: bl ComputeCutaway',     0x1c1654, (18 << 26) | ((0x128ae0 - 0x1c1654) & 0x03FFFFFC) | 1)
pin('restore: bl DoDynamicCutaway',   0x1c166c, (18 << 26) | ((0x1cf3b0 - 0x1c166c) & 0x03FFFFFC) | 1)
pin('restore: SetTrackedObject',      0x1c16fc, (18 << 26) | ((0x47cb10 - 0x1c16fc) & 0x03FFFFFC) | 1)
pin('restore: saved tool',            0x1c1714, 0x801f0164)  # lwz r0, 0x164(r31)
pin('restore: tool global var',       0x1c1718, 0x80629048)  # lwz r3, -0x6fb8(r2)
pin('restore: store tool global',     0x1c171c, 0x90030000)  # stw r0, 0(r3)

print()
if fails:
    print(f'FAILED pins: {fails}')
    sys.exit(1)
print(f'ALL PINS PASS ({len(fails) == 0})')
