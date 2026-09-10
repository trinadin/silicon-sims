"""Pin R245 tutorial lesson-highlight / UI event-poll decode; reads owner executable only.

Pins the instruction words relied on this round (original decode + the
skeptic-repair sites: winmgr +0x20c accessors, OnIdle re-entrancy dispatch,
TreeSim +0x24 latch, opcode-0x23 handler, ev->caption addi words) plus key
data words (jump tables, tree-name string table, flash float table,
highlighter vptr/vtable slots).
Fixtures prove the decoded formulas in pure python: the flash-timer interval
law (int(1000/period + 0.5) ms when period > 0), the TSOnTimerMsg phase
ping-pong, SetBuffer's SetArea sizing, TSPaint's phase-slice source/destination
rect math, HiliteForTutorial's centering arithmetic (srwi sign-bit +
srawi truncate, incl. the cWinLotBtn +50px shift), the Tut_CheckForEvents
jump-table decode and ev->caption mapping, and the primitive 0x22 sub-op
dispatch.
"""
import hashlib
import json
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
b = (ROOT / 'game-data/The Sims/The Sims Complete').read_bytes()
sha = hashlib.sha256(b).hexdigest()
assert sha == '33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f'

# address -> expected big-endian instruction word (file offsets)
pins = {
    # Tut_CheckForEvents 0x156550: owner fetch, sim global 12 write, tree names,
    # event code/param reads, jump tables, change-cache, reaction tree
    0x156574: 0x80790000, 0x156580: 0x4BF8C801, 0x1565B0: 0x3880000C,
    0x1565B8: 0x4BFE2669, 0x1565DC: 0x4BF99755, 0x1565F4: 0xAAFA003A,
    0x1565FC: 0xAB9A003C, 0x156600: 0x2817000F, 0x156608: 0x8062A848,
    0x156630: 0x480B63C1, 0x1568C4: 0x80628CB0, 0x1568C8: 0x8002DA60,
    0x1568D4: 0x80630084, 0x156938: 0x483C72C9, 0x156940: 0x48000C01,
    0x15695C: 0x7C1C0000, 0x156974: 0x5760063F, 0x156984: 0x38DF002F,
    0x156994: 0x4BF9939D, 0x15699C: 0x9002DA60,
    # Flash helpers: FindRelButton / FindButton(buffer) / FindButton(int) then
    # vtable slot byte 0x168 = HiliteForTutorial(bool)
    0x1563D0: 0x480006C1, 0x1563E4: 0x818C0168, 0x1564D4: 0x4800119D,
    0x1564E8: 0x818C0168, 0x156A1C: 0x48000B25, 0x156A30: 0x818C0168,
    # SetTutorialHighlighter: AddRef new (slot +0xc), Release old (slot +0x10),
    # store at cTSWinMgrW95 +0x20c
    0x51B688: 0x818C000C, 0x51B6A4: 0x818C0010, 0x51B694: 0x807E020C,
    0x51B6B0: 0x93FE020C,
    # cTSWinTutorialHighlight ctor: vptr slot TOC-0x60b0, default period = P[0x10]
    0x5393D8: 0x80A29F50, 0x5393EC: 0xC0040010, 0x5393F8: 0xD01F00D4,
    # ShowWindow: GetFlag(1), phase reset, period test, fdivs/fmadds/fctiwz,
    # SubscribeTimerMsg
    0x538C94: 0xC03E00D4, 0x538C98: 0xC01F0000, 0x538CB4: 0xEC200824,
    0x538CC4: 0xEC02007A, 0x538CC8: 0xFC00001E, 0x538CD4: 0x4BFE382D,
    # TSOnTimerMsg: direction flip at N-1 and 0, phase += dir, InvalidateSelf,
    # re-arm
    0x538D98: 0x3803FFFF, 0x538D9C: 0x7C040000, 0x538DA8: 0x901D00D8,
    0x538DC0: 0x38000001, 0x538DC4: 0x901D00D8, 0x538DD4: 0x7C040214,
    0x538DD8: 0x901D00DC, 0x538DE0: 0x818C0170, 0x538E34: 0x4BFE36CD,
    # SetBuffer: buffer swap + delete, store (buf, N, period), SetArea math
    0x538ECC: 0x806300CC, 0x538EE8: 0x818C0008, 0x538EF4: 0x93DD00CC,
    0x538EFC: 0x93FD00D0, 0x538F00: 0xD3FD00D4, 0x538F2C: 0x7C002BD6,
    0x538F38: 0x818C0068, 0x538F3C: 0x7CC02214, 0x538F60: 0x7C060050,
    0x538FA0: 0x7CE02A14,
    # TSPaint: step = right/N, src slice [l + p*step, right/N + p*step],
    # dst offset by window left/top
    0x539060: 0x801F00D0, 0x539064: 0x7C8303D6, 0x539078: 0x7C072050,
    0x539084: 0x7C0301D6, 0x539088: 0x7C670214, 0x53908C: 0x7C040214,
    0x5390A8: 0x7CA74214, 0x5390AC: 0x7C844214, 0x539138: 0x818C0178,
    # HiliteForTutorial(cTSWin): highlighter = winmgr+0x20c, GetFlag(1) gate,
    # width/height subf, round-toward-zero half divide, TSWinMoveTo,
    # ShowWindow + PullToFront, cursor compare, PumpMouseMoveMsg, HideWindow
    0x5034E8: 0x83C4020C, 0x503520: 0x7CA62850, 0x503530: 0x7CC50050,
    0x503538: 0x54C00FFE, 0x50354C: 0x7C000E70, 0x503554: 0x7C840214,
    0x503558: 0x4BD5D649, 0x503564: 0x818C009C, 0x503578: 0x818C004C,
    0x5035A8: 0x54000FFF, 0x5035D4: 0x4801893D, 0x5035E4: 0x818C00A0,
    # cWinLotBtn::HiliteForTutorial: super-call + SetArea shifted +50px,
    # standard cursor 4
    0x2D2B20: 0x38A40032, 0x2D2B2C: 0x38E70032, 0x2D2B48: 0x481C4789,
    0x2D2B54: 0x482313AD,
    # cDDDSimsView::Init region: LoadBuffer(0x1374=4980), magenta key holder
    # TOC-0x7208, SetBuffer(buf, 3, *(TOC-0x5398)), SetFlag(0x10000),
    # SetTutorialHighlighter
    0x2193BC: 0x482EA465, 0x2193D8: 0x38601374, 0x2193E0: 0x4819CDB1,
    0x219418: 0xC0260000, 0x21941C: 0x4831FA85, 0x219428: 0x3C800001,
    0x219444: 0x4830221D,
    # cDDDSimsView::Simulate: the single unconditional poll call site
    0x21715C: 0x4BF3F3F5, 0x217188: 0x4BF21889,
    # cXObject::TryElement: primitive 0x22 dispatch and the three Flash calls,
    # plus the interpreter local-store write to object +0x3a and the
    # dialog-completion writer at 0xf1830
    0xF0444: 0x28000033, 0xF044C: 0x80A2A668, 0xF04A8: 0x48066539,
    0xF04C8: 0x48065F79, 0xF04E8: 0x48065E89, 0xF0C04: 0xB003003A,
    0xF1830: 0xB098003A,
    # skeptic-repair pins (r245 skeptic-response.md C2/C3/C4/C5):
    # winmgr ctor init of +0x20c; DoModalWin hide/restore slots;
    # CleanUpWindowReferences null-without-Release; winmgr Shutdown
    # Release+null; OnIdle slot 0xe0 dispatch; TreeSim +0x24 latch
    # gate/store/clear; opcode 0x23 handler
    0x51FF68: 0x909F020C, 0x51CAB8: 0x8075020C, 0x51CAC4: 0x818C00A0,
    0x51CD9C: 0x8075020C, 0x51CDA4: 0x818C009C, 0x51DDE8: 0x801E020C,
    0x51DDF8: 0x901E020C, 0x51FB54: 0x807F020C, 0x51FB64: 0x818C0010,
    0x4B6D24: 0x818C00E0, 0x153EEC: 0xA8030024, 0x15402C: 0xB0990024,
    0x154370: 0xB01D0024, 0xF0C6C: 0x7F65DB78,
    # case 2/9/11/13/15 caption offsets (addi r4, r31, off at the five
    # secondary-table handlers) — pins the ev -> caption mapping exactly
    0x1566CC: 0x389F0011,   # ev 2 -> "motives"
    0x156704: 0x389F0019,   # ev 11 -> "job"
    0x15673C: 0x389F001D,   # ev 9 -> "rel"
    0x156774: 0x389F0021,   # ev 13 -> "skill"
    0x1567AC: 0x389F0027,   # ev 15 -> "per.ity"
}
for address, expected in pins.items():
    actual = struct.unpack_from('>I', b, address)[0]
    assert actual == expected, hex(address)


# ---- data-section evidence: packed unpack + TOC-slot words ----
def varint(d, i):
    v = 0
    while True:
        x = d[i]; i += 1
        v = ((v << 7) | (x & 0x7F)) & 0xFFFFFFFF
        if not (x & 0x80):
            return v, i


def unpack_pef(data):
    out = bytearray(); i = 0; n = len(data)
    while i < n:
        opb = data[i]; i += 1
        op = opb >> 5; cnt = opb & 0x1F
        if cnt == 0:
            cnt, i = varint(data, i)
        if op == 0:
            out += b'\0' * cnt
        elif op == 1:
            out += data[i:i + cnt]; i += cnt
        elif op == 2:
            rc, i = varint(data, i)
            blk = data[i:i + cnt]; i += cnt
            out += blk * (rc + 1)
        elif op in (3, 4):
            custom, i = varint(data, i)
            rc, i = varint(data, i)
            common = b'\0' * cnt if op == 4 else data[i:i + cnt]
            if op == 3:
                i += cnt
            out += common
            for _ in range(rc):
                out += data[i:i + custom]; i += custom
                out += common
        else:
            raise ValueError('reserved pef op')
    return bytes(out)


data = unpack_pef(b[0x5C22F0:0x5C22F0 + 0x6D834])
assert len(data) == 0x7BF80


def dw(off):
    return struct.unpack_from('>I', data, off)[0]


def slot_word(toc_delta):
    return dw(0x8000 + toc_delta)


data_pins = {
    # Tut_CheckForEvents TOC loads
    'sim_holder_slot_-0x778c': (slot_word(-0x778C), 0x0008510C),
    'jt_main_ptr_-0x57b8': (slot_word(-0x57B8), 0x0004ABB8),
    'jt_secondary_ptr_-0x57bc': (slot_word(-0x57BC), 0x0004AB78),
    'tree_names_ptr_-0x57b4': (slot_word(-0x57B4), 0x0004ABF8),
    'cpstate_holder_slot_-0x720c': (slot_word(-0x720C), 0x000934E8),
    'last_btn_holder_-0x6fc8': (slot_word(-0x6FC8), 0x0009777C),
    'world_holder_-0x7350': (slot_word(-0x7350), 0x00085C48),
    'change_cache_-0x25a0': (dw(0x8000 - 0x25A0), 0xFFFFFFFF),
    'highlight_vptr_slot_-0x60b0': (slot_word(-0x60B0), 0x00076C30),
    'period_ptr_-0x5398': (slot_word(-0x5398), 0x0059B360),
    'floattable_ptr_-0x4348': (slot_word(-0x4348), 0x0059C3E0),
}
for name, (actual, expected) in data_pins.items():
    assert actual == expected, (name, hex(actual))

# main jump table decode: event-code -> handler
JT = 0x4ABB8
jt_main = {k: dw(JT + 4 * k) + 0x8E90 for k in range(16)}
expected_jt = {0: 0x156974, 1: 0x15661C, 2: 0x156644, 3: 0x156974,
               4: 0x156820, 5: 0x15684C, 6: 0x1568C4, 7: 0x1568F4,
               8: 0x156920, 9: 0x156644, 10: 0x156974, 11: 0x156644,
               12: 0x156974, 13: 0x156644, 14: 0x1567E0, 15: 0x156644}
assert jt_main == expected_jt
JT2 = 0x4AB78
jt_sec = {k: dw(JT2 + 4 * k) + 0x8E90 for k in range(16)}
expected_jt2 = {0: 0x1567C4, 1: 0x1567C4, 2: 0x1566B0, 3: 0x1567C4,
                4: 0x1567C4, 5: 0x1567C4, 6: 0x1567C4, 7: 0x1567C4,
                8: 0x1567C4, 9: 0x156720, 10: 0x1567C4, 11: 0x1566E8,
                12: 0x1567C4, 13: 0x156758, 14: 0x1567C4, 15: 0x156790}
assert jt_sec == expected_jt2

# tree-name string table at data 0x4abf8
st = 0x4ABF8


def dstr(off):
    return data[off:].split(b'\0')[0].decode('latin1')


assert dstr(st) == 'query wait event'
assert dstr(st + 0x11) == 'motives'
assert dstr(st + 0x19) == 'job'
assert dstr(st + 0x1D) == 'rel'
assert dstr(st + 0x21) == 'skill'
assert dstr(st + 0x27) == 'per.ity'
assert dstr(st + 0x2F) == 'got wait event'

# flash float table P (stored code-section-relative 0x59c3e0 -> file 0x5a5270)
P = 0x59C3E0 + 0x8E90
pfloats = [struct.unpack_from('>f', b, P + 4 * k)[0] for k in range(5)]
assert pfloats == [0.0, 0.5, 1000.0, 1.0, 6.0]
# the Init-period constant (TOC-0x5398 -> file 0x5a41f0) is 6.0 seconds
init_period = struct.unpack_from('>f', b, 0x59B360 + 0x8E90)[0]
assert init_period == 6.0

# highlighter vtable slot columns (vtable at data 0x76c30)
VT = 0x76C30


def vt_code(off):
    return dw(dw(VT + off)) + 0x8E90


assert vt_code(0x14) == 0x539250       # Init
assert vt_code(0x18) == 0x5391A0       # Shutdown
assert vt_code(0x1C) == 0x539310       # dtor
assert vt_code(0x9C) == 0x538C40       # ShowWindow
assert vt_code(0xA0) == 0x538B90       # HideWindow
assert vt_code(0x120) == 0x538D40      # TSOnTimerMsg
assert vt_code(0x16C) == 0x539020      # TSPaint
assert vt_code(0x168) == 0x5034C0      # HiliteForTutorial (cTSWin base)
# base cTSWin columns via the cActionIcon-derived vtable (data 0x4fc20):
# the AddRef (slot +0xc) / Release (slot +0x10) / SetArea (+0x68) /
# GetFlag (+0x94) / get_init_flag (+0xd0) columns are shared word-for-word
B = 0x4FC20
for col in (0x0C, 0x10, 0x68, 0x94, 0xD0, 0x170):
    assert dw(VT + col) == dw(B + col), hex(col)
assert struct.unpack_from('>I', data, 0x5C908)[0] == 0x10168  # cWinLotBtn override


# ---- fixtures ----
fixtures = {}


# (a) flash timer interval law (ShowWindow 0x538c94..0x538cd4 /
#     TSOnTimerMsg 0x538df8..0x538e34): if period > P[0]=0:
#     interval = trunc(1000.0 * (1.0/period) + 0.5)  [fdivs, fmadds, fctiwz]
def trunc(x):
    import math
    return math.floor(x) if x >= 0 else -math.floor(-x)


def interval_ms(period, p=None):
    p = p or [0.0, 0.5, 1000.0, 1.0]
    if not period > p[0]:
        return None
    rate = p[3] / period
    return trunc(p[2] * rate + p[1])


assert interval_ms(6.0) == 167          # shipped: Init passes period 6.0
assert interval_ms(2.0) == 500
assert interval_ms(4.0) == 250
assert interval_ms(0.0) is None          # period <= P[0] -> no timer
assert interval_ms(0.25) == 4000
assert interval_ms(3.0) == 333           # fmadds then fctiwz truncates 333.33+0.5
fixtures['flash_interval'] = {
    'law': 'period>0 -> SubscribeTimerMsg every trunc(1000/period + 0.5) ms; '
           'period<=0 -> no timer (static frame); shipped Init period = 6.0s',
    'period_6': interval_ms(6.0), 'period_2': interval_ms(2.0),
    'period_3': interval_ms(3.0), 'period_0': interval_ms(0.0),
}

# (b) TSOnTimerMsg phase ping-pong (0x538d84..0x538dd8): dir +0xd8 s32,
#     phase +0xdc; dir>0 and phase == N-1 -> dir=-1; dir<=0 and phase==0 ->
#     dir=1; phase += dir. Never unsubscribes here (HideWindow does).


def phase_track(n, ticks):
    dirn, phase, out = 1, 0, []
    for _ in range(ticks):
        if dirn > 0 and phase == n - 1:
            dirn = -1
        elif dirn < 0 and phase == 0:
            dirn = 1
        phase += dirn
        out.append(phase)
    return out


assert phase_track(3, 8) == [1, 2, 1, 0, 1, 2, 1, 0]
assert phase_track(2, 5) == [1, 0, 1, 0, 1]
# N=1 is degenerate per the literal decode (flips dir then steps to -1);
# the shipped N is 3.
assert phase_track(1, 3) == [-1, -2, -3]
fixtures['phase_pingpong'] = {
    'law': 'phase += dir; dir flips at N-1 (down) and 0 (up); N = SetBuffer int',
    'n3_track': phase_track(3, 8),
    'n2_track': phase_track(2, 5),
    'n1_track_literal': phase_track(1, 3),
}

# (c) SetBuffer (0x538ea0..0x538fb0) sizing via SetArea(l, t, r, b) with the
#     cTSWin area fields 0x74/0x78/0x7c/0x80 and cTSBuffer rect
#     {left=0x14, top=0x18, right=0x1c, bottom=0x20}:
#     N != 0: SetArea(l, t, l + (R-L)/N, t + (B-T))   [divw trunc]
#     N == 0: SetArea(l, t, l + (R-L),   t + (B-T))


def set_area(l, t, buf, n):
    bl, tp, rr, bb = buf
    w = rr - bl
    h = bb - tp
    if n != 0:
        return (l, t, trunc(w / n) + l, h + t)
    return (l, t, w + l, h + t)


a = set_area(100, 60, (0, 0, 150, 50), 3)
assert a == (100, 60, 150, 110)          # 150/3 = 50-wide window, 50 tall
a0 = set_area(100, 60, (0, 0, 150, 50), 0)
assert a0 == (100, 60, 250, 110)         # full art width, N=0
fixtures['setbuffer_area'] = {
    'law': 'window <- (l, t, l + artW/N, t + artH); divw truncates',
    'n3': a, 'n0': a0,
}

# (d) TSPaint (0x539020..0x539124) slice math for phase p of N:
#     step = R/N (left assumed 0 via subf r7); src = (l + p*step, t,
#     R/N + p*step, b); dst = src shifted by window left/top (+0x1c/+0x20)


def paint_rects(buf, n, p, win_x, win_y):
    l, t, rr, bb = buf
    step = trunc(rr / n) - l
    sx = l + p * step
    sr = rr / n + p * step
    src = (sx, t, sr, bb)
    dst = (sx + win_x, t + win_y, sr + win_x, bb + win_y)
    return src, dst


src, dst = paint_rects((0, 0, 150, 50), 3, 0, 200, 300)
assert src == (0, 0, 50, 50) and dst == (200, 300, 250, 350)
src, dst = paint_rects((0, 0, 150, 50), 3, 2, 200, 300)
assert src == (100, 0, 150, 50) and dst == (300, 300, 350, 350)
fixtures['tspaint_slices'] = {
    'law': 'phase p draws horizontal slice [p*step,(p+1)*step] of the art into '
           'the window offset by (win.x, win.y); 150x50 art + N=3 = 3 frames',
    'frame0': [list(src) for src in (paint_rects((0, 0, 150, 50), 3, 0, 0, 0))],
    'frames': [list(paint_rects((0, 0, 150, 50), 3, p, 0, 0)[0]) for p in range(3)],
}

# (e) HiliteForTutorial(cTSWin) centering (0x50350c..0x503558):
#     d = targetW - hlW; r0 = srwi(d, 31) [LOGICAL shift: 1 for d<0, else 0];
#     r0 = d + r0; r0 = srawi(r0, 1) [trunc toward zero];
#     x = target.anchorX + r0. y = hl.anchorY - hlH - 3. cWinLotBtn override
#     shifts SetArea by +0x32 in both vertical args (0x2d2b20/0x2d2b2c).


def srawi(x, n):
    """PPC srawi: arithmetic shift RIGHT truncating toward zero."""
    q = abs(x) >> n
    return -q if x < 0 else q


def center_x(target_anchor_x, target_w, hl_w):
    d = target_w - hl_w
    sign = (d >> 31) & 1            # srwi r0, r6, 0x1f: logical -> 0 or 1
    return target_anchor_x + srawi(d + sign, 1)


assert center_x(200, 80, 50) == 215
assert center_x(200, 50, 80) == 186      # d=-30: srawi(-29,1) = -14
assert srawi(-29, 1) == -14 and -29 >> 1 == -15  # python floor != srawi
fixtures['hilite_center'] = {
    'law': 'x = target.anchorX + round-toward-zero((targetW - hlW)/2); '
           'y = hl.anchorY - hlH - 3; cWinLotBtn then SetArea(t+50, b+50)',
    'case_wider_target': center_x(200, 80, 50),
    'case_wider_hl': center_x(200, 50, 80),
    'lotbtn_shift_px': 0x32,
}

# (f) primitive 0x22 sub-op dispatch (TryElement 0xf0460..0xf04ec):
#     byte0: 0 -> FlashButtonByImageID(InterpValue(s16@2), bit0(byte4))
#            1 -> FlashPersonPanelButton(...), 2 -> FlashRelButton(...)


def flash_target(sub_op):
    if sub_op == 0:
        return 'Tut_FlashButtonByImageID'
    if sub_op == 1:
        return 'Tut_FlashPersonPanelButton'
    if sub_op == 2:
        return 'Tut_FlashRelButton'
    return None


assert [flash_target(k) for k in range(4)] == [
    'Tut_FlashButtonByImageID', 'Tut_FlashPersonPanelButton',
    'Tut_FlashRelButton', None]
fixtures['primitive_0x22'] = {
    'opcode': 0x22,
    'index_law': '(s16)opcode & 0x7fff (rlwinm clears bit 0x8000), '
                 'cmplwi 0x33 default; 0x23 -> TryUserEvent (0xf0c6c)',
    'subops': {0: 'image-id button', 1: 'person panel button',
               2: 'relationship button'},
    'arg_layout': 's16@operand+2 via InterpValue, bool = operand byte4 bit0',
}

# (g) ev -> person-panel page caption law (skeptic repair C1): the secondary
#     jump table maps ev to a handler whose pinned `addi r4, r31, off`
#     selects the string-table offset. (An earlier draft swapped ev 9/11.)
caption_off = {
    0x1566CC: 0x11, 0x156704: 0x19, 0x15673C: 0x1D,
    0x156774: 0x21, 0x1567AC: 0x27,
}
handler_off = {}
for addr, off in caption_off.items():
    w = struct.unpack_from('>I', b, addr)[0]
    assert w >> 16 == 0x389F and (w & 0xFFFF) == off, hex(addr)
    handler_off.setdefault(off, addr)
ev_caption = {}
for ev, handler in expected_jt2.items():
    if handler == 0x1567C4:
        continue
    w = struct.unpack_from('>I', b, handler + 0x1C)[0]
    assert w >> 16 == 0x389F, hex(handler)
    off = w & 0xFFFF
    ev_caption[ev] = data[0x4ABF8 + off:].split(b'\0')[0].decode('latin1')
assert ev_caption == {2: 'motives', 9: 'rel', 11: 'job',
                      13: 'skill', 15: 'per.ity'}
fixtures['page_captions'] = ev_caption

result = {
    'executable_sha256': sha,
    'pin_count': len(pins),
    'data_pin_count': len(data_pins),
    'pins': {hex(k): hex(v) for k, v in sorted(pins.items())},
    'data_pins': {k: hex(v[0]) for k, v in data_pins.items()},
    'jump_table_main': {str(k): hex(v) for k, v in jt_main.items()},
    'jump_table_secondary': {str(k): hex(v) for k, v in jt_sec.items()},
    'float_table': pfloats,
    'init_period_float': init_period,
    'fixtures': fixtures,
}
(HERE / 'verified-tut-highlight.json').write_text(json.dumps(result, indent=2) + '\n')
print(f'{len(pins)} instruction words, {len(data_pins)} data pins and '
      f'{len(fixtures)} fixture groups verified')
