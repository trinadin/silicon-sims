"""Pin R244 cutaway-geometry decode; reads owner executable only.

Pins 132 instruction words relied on this round (two floor-gate addresses are
also in r243-cutaway's set by skeptic request; the full 0x591B10 body is
pinned so the matrix-mask law is checkable instruction-by-instruction).
Fixtures prove the decoded
formulas with pure python: direction-table reconstruction from the pinned init
instruction stream, the native matrix-mask helper law (exact two-word form of
0x591B10, incl. the x=32 case and counter-evidence against two-word
mirroring), TileToPoint projection, the zoom rescale law exactly as at
0x1D3950..0x1D3998 (divide when globalZoom > argZoom, else multiply) with the
global-state restore of 0x1D39F0..0x1D39F8, GetTheoreticalWallExtent algebra,
the strict overlap predicate, sweep candidate/termination enumeration, side
rotation and the floor filter.
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
    # Room::ComputeCutawayMatrix: direction-table load, viewer, rotation, LUT,
    # sweep deltas, bounds, tile rect build, side adjust, overlap, termination
    0x12514c: 0x83e28c18, 0x125160: 0x83c50004, 0x125174: 0x7c1a0e70,
    0x12518c: 0x8062a7bc, 0x1251ec: 0x80030084, 0x125210: 0x7c6500ae,
    0x125230: 0x38a00020, 0x12526c: 0x90a10070, 0x1252a0: 0x7fc4f378,
    0x1252c4: 0x8061005c, 0x1252d8: 0x7c7a0050, 0x1252ec: 0x90c10088,
    0x125394: 0x80010050, 0x1253a4: 0x7c1a0050, 0x1253bc: 0x7c00d214,
    0x12541c: 0x80010078, 0x125498: 0x88df0003, 0x1254a8: 0x88610040,
    0x1254dc: 0x80830070, 0x125520: 0x8063007c, 0x125530: 0x38600001,
    0x125588: 0x408200b0, 0x1255ac: 0x7c852050, 0x1255bc: 0x7c630050,
    0x1255d4: 0x7ca83850, 0x1255e4: 0x7c800379, 0x1255e8: 0x41800050,
    0x125638: 0x893f0012, 0x1257bc: 0x893f0003, 0x125940: 0x2c170003,
    0x125944: 0x4080003c, 0x125954: 0x3ae00000, 0x125980: 0x3b7b0003,
    # HouseViewer::TileToPoint: args, altitude byte, zoom compare/rescale
    # (polarity + direction), floor height, projection multiplies, packed
    # point stores, and the restore of pre-call global state (0x1D39F0..F8)
    0x1d385c: 0x28090000, 0x1d3864: 0x80628cb0, 0x1d388c: 0x98e10042,
    0x1d3890: 0x91410044, 0x1d38f0: 0x5540063f, 0x1d3914: 0x4bf8e4ed,
    0x1d391c: 0x8881004d, 0x1d3944: 0x807f0008, 0x1d3948: 0x7c1e1800,
    0x1d394c: 0x418200d8, 0x1d3950: 0x7cde1851, 0x1d3954: 0x93df0008,
    0x1d3960: 0x40810020, 0x1d3968: 0x7ca53030, 0x1d39ac: 0x20ba0003,
    0x1d39c0: 0x80e50124, 0x1d39e4: 0x7d2ad030, 0x1d39f0: 0x907f0008,
    0x1d39f4: 0x909f0000, 0x1d39f8: 0x901f0004, 0x1d3a68: 0x7ce81830,
    0x1d3a98: 0xb061004a, 0x1d3a9c: 0xb1210048, 0x1d3aa4: 0x901c0000,
    # GetTheoreticalWallExtent: zoom, 3-zoom index, floor height, alt byte 1,
    # (tz-1)*F, px half-width, alt<<(z+1), q-F, q+V60, 16-byte staging copy
    0x1cef60: 0x881c0000, 0x1cef88: 0x817f0008, 0x1cef98: 0x200b0003,
    0x1cefc8: 0x81440124, 0x1cefe4: 0x88c10041, 0x1cf000: 0x7ce051d6,
    0x1cf010: 0x7c8b4050, 0x1cf020: 0x7cc02830, 0x1cf028: 0x7c0a4850,
    0x1cf02c: 0x7c691a14, 0x1cf038: 0xc8210050,
    # direction table static init (0x610B0): li sources and 24 stb stores
    0x610bc: 0x38c00000, 0x610cc: 0x3800ffff, 0x61128: 0x98df0000,
    0x6112c: 0x981f0001, 0x61130: 0x98df0002, 0x61140: 0x38000001,
    0x61144: 0x98df0003, 0x6114c: 0x981f0004, 0x61154: 0x98df0005,
    0x6115c: 0x38000000, 0x61164: 0x3880ffff, 0x61168: 0x981f0007,
    0x61170: 0x989f0006, 0x61178: 0x981f0008, 0x61180: 0x38000000,
    0x61188: 0x38800001, 0x6118c: 0x981f000a, 0x61194: 0x989f0009,
    0x6119c: 0x981f000b, 0x611a4: 0x3880ffff, 0x611b0: 0x989f000c,
    0x611b8: 0x989f000d, 0x611c0: 0x981f000e, 0x611c8: 0x38800001,
    0x611d4: 0x989f000f, 0x611dc: 0x989f0010, 0x611e4: 0x981f0011,
    0x611ec: 0x38000001, 0x611f4: 0x3880ffff, 0x611f8: 0x981f0012,
    0x611fc: 0x38000000, 0x61204: 0x989f0013, 0x6120c: 0x981f0014,
    0x61214: 0x3800ffff, 0x6121c: 0x38800001, 0x61220: 0x981f0015,
    0x61224: 0x38000000, 0x6122c: 0x989f0016, 0x61234: 0x981f0017,
    # room+0x34 writers: Clear=0; AbsorbNewRoomList/GetNewRoom/ComputeRooms=1
    0x127384: 0x901e0034, 0x125b38: 0x901e0034, 0x128800: 0x901f0034,
    0x1288ac: 0x901d0034, 0x128d28: 0x901b0034,
    # RoomManager gate + side rotation: skip bit test, floor filter, side math
    0x128b0c: 0x80030034, 0x128b24: 0x5404b7be,
    0x125330: 0x20000004, 0x125334: 0x540007be, 0x125338: 0x7c630214,
    0x12533c: 0x3803ffff, 0x125340: 0x540307be, 0x125344: 0x38630001,
    # ResolveDiagonal: floor-grid global, 0xfffb threshold, outB room, ret 1
    0x1283ac: 0x80a28cac, 0x1284ac: 0x2803fffb, 0x1284e4: 0x901d0000,
    0x128508: 0x38600001,
    # 64-bit shift helper used for matrix bit masks — the FULL body is pinned
    # so the single-bit law (and the mirroring counter-evidence) is checkable
    0x591b10: 0x21050020, 0x591b14: 0x3125ffe0, 0x591b18: 0x7c632830,
    0x591b1c: 0x7c8a4430, 0x591b20: 0x7c635378, 0x591b24: 0x7c8a4830,
    0x591b28: 0x7c635378, 0x591b2c: 0x7c842830,
}
for address, expected in pins.items():
    actual = struct.unpack_from('>I', b, address)[0]
    assert actual == expected, hex(address)


def sdiv(a, d):
    """PPC divw: signed division truncating toward zero."""
    q = abs(a) // abs(d)
    return -q if (a < 0) != (d < 0) else q


def to_i16(v):
    v &= 0xFFFF
    return v - 0x10000 if v >= 0x8000 else v


def rotl32(v, n):
    return ((v << n) | (v >> (32 - n))) & 0xFFFFFFFF


def tile_to_point(origin_x, origin_y, zoom, floor_h, alt_b1, tx, ty, tz):
    """HouseViewer::TileToPoint fast path (0x1D3A24..0x1D3A9C)."""
    x = origin_x + ((8 << zoom) * (tx - ty))
    y = origin_y + ((4 << zoom) * (tx + ty)) - ((tz - 1) * floor_h)
    y -= alt_b1 << (zoom + 1)
    return to_i16(x), to_i16(y)


def wall_extent(origin_x, origin_y, zoom, floor_h, alt_b1, v5c, v60, tx, ty, tz):
    """GetTheoreticalWallExtent (0x1CEF30): W0/W2 x-range, W1/W3 y-range."""
    px, q = tile_to_point(origin_x, origin_y, zoom, floor_h, alt_b1, tx, ty, tz)
    return [px - v5c - 1, q - floor_h, px + v5c - 1, q + v60]


def overlaps(r, w):
    """Strict overlap law (0x12559C..0x1255E8): all four differences >= 1."""
    return (r[2] >= w[0] + 1 and w[2] >= r[0] + 1
            and r[3] >= w[1] + 1 and w[3] >= r[1] + 1)


def rotate_side(side, rot):
    """Side rotation applied at 0x125330..0x125344 (side 0 never rotated)."""
    if side == 0:
        return 0
    return ((side - 1 + ((4 - rot) & 3)) & 3) + 1


fixtures = {}

# 1. direction table rebuilt from the pinned init instruction stream
regs = {0: 0, 4: 0, 6: 0}
table = {}
for address in sorted(pins):
    w = pins[address]
    op = w >> 26
    if op == 14 and (w >> 16) & 31 == 0:  # li rD, simm
        imm = w & 0xFFFF
        regs[(w >> 21) & 31] = imm - 0x10000 if imm >= 0x8000 else imm
    elif op == 38 and (w >> 16) & 31 == 31:  # stb rS, off(r31)
        table[w & 0xFFFF] = regs[(w >> 21) & 31] & 0xFF
expected_table = [0, 255, 0, 0, 1, 0, 255, 0, 0, 1, 0, 0,
                  255, 255, 0, 1, 1, 0, 1, 255, 0, 255, 1, 0]
assert [table[i] for i in range(24)] == expected_table
def to_i8(v):
    return v - 0x100 if v >= 0x80 else v


dir_entries = [tuple(to_i8(v) for v in expected_table[i:i + 2])
               for i in range(0, 24, 3)]
assert dir_entries == [(0, -1), (0, 1), (-1, 0), (1, 0),
                       (-1, -1), (1, 1), (1, -1), (-1, 1)]
fixtures['direction_table'] = {
    'entries_xy': [list(e) for e in dir_entries],
    'bytes_at_bss_7f040': expected_table,
}

# 2. TileToPoint projection on synthetic input (no wrap)
px, py = tile_to_point(1000, 2000, 2, 24, 3, 3, -2, 2)
assert (px, py) == (1160, 1968)
# 2b. int16 wrap and (tz-1) baseline sign (tz=0 adds +F)
px2, py2 = tile_to_point(32000, 0, 3, 10, 0, 100, 0, 0)
assert (px2, py2) == (-27136, 3210)
fixtures['tile_to_point'] = {
    'case_a': {'in': [1000, 2000, 2, 24, 3, 3, -2, 2], 'out': [px, py]},
    'case_b_wrap': {'in': [32000, 0, 3, 10, 0, 100, 0, 0], 'out': [px2, py2]},
}


# 2c. native matrix-mask helper law (0x591B10 with args (0,1,x)): a plain
# 64-bit left shift. PPC slw/srw zero the result when count & 64: the single
# bit lands in exactly one word. NOT mirrored into both words (a proposed
# mirror form is disproved below: x=5 -> hi=0, x=40 -> lo=0; x=32 -> (1,0),
# no low-word aliasing). Row = room+0x7C + 8*(sbyte)y, indexed unchecked
# beyond the world-bounds test (negative y would walk below the matrix).
def ppc_shift(v, c, left):
    c &= 63
    if c >= 32:
        return 0
    v &= 0xFFFFFFFF
    return ((v << c) & 0xFFFFFFFF) if left else (v >> c)


def mask64(x):
    r8 = (32 - x) & 0xFFFFFFFF
    r9 = (x - 32) & 0xFFFFFFFF
    hi = ppc_shift(0, x, True) | ppc_shift(1, r8, False) | ppc_shift(1, r9, True)
    lo = ppc_shift(1, x, True)
    return hi, lo


expected_masks = {0: (0, 1), 5: (0, 32), 31: (0, 0x80000000), 32: (1, 0),
                  33: (2, 0), 40: (256, 0), 63: (0x80000000, 0),
                  -1: (0x80000000, 0), -33: (0, 0x80000000)}
for xv, expected in expected_masks.items():
    assert mask64(xv) == expected, (xv, mask64(xv))
assert mask64(5)[0] == 0 and mask64(40)[1] == 0   # mirror form disproved
fixtures['matrix_mask'] = {
    'law': 'helper 0x591B10 (0,1,x) = single bit 1<<(x&63) in the matching '
           'word; x=32 -> (1,0); no two-word mirroring',
    'samples': {str(k): [v[0], v[1]] for k, v in expected_masks.items()},
}

# 3. zoom rescale law exactly as at 0x1D3950..0x1D3998:
#    r6 = globalZoom - argZoom; r6 > 0 -> DIVIDE origin by (1 << r6)
#    (divw, trunc toward zero); r6 <= 0 -> MULTIPLY by (1 << -r6).
#    The rescaled origin/zoom exist only for this projection: the pre-call
#    global values are written back at 0x1D39F0..0x1D39F8 (nothing persists).
def rescale(gz, ox, oy, arg):
    if arg == gz:
        return ox, oy, gz
    d = gz - arg
    if d > 0:
        return sdiv(ox, 1 << d), sdiv(oy, 1 << d), arg
    s = 1 << -d
    return ox * s, oy * s, arg


eff = rescale(1, -7, 5, 3)          # zoom up: multiply
assert eff == (-28, 20, 3)
eff = rescale(3, 100, -50, 0)       # zoom down: divide, divw truncation
assert eff == (12, -6, 0)
assert sdiv(-50, 8) != -50 // 8     # python floor division would give -7
eff = rescale(2, 77, 99, 2)         # equal zoom: untouched
assert eff == (77, 99, 2)
fixtures['zoom_rescale'] = {
    'law': 'r6 = globalZoom - argZoom; r6>0 divide (divw trunc), else multiply; '
           'globals restored at 0x1D39F0..F8 (non-persistent)',
    'up_1_to_3_eff': [-28, 20, 3],
    'down_3_to_0_eff': [12, -6, 0],
    'equal_untouched': [77, 99, 2],
}

# 4. wall extent box algebra (fixture A point, V5C=30, V60=20)
w = wall_extent(1000, 2000, 2, 24, 3, 30, 20, 3, -2, 2)
assert w == [1129, 1944, 1189, 1988]
assert w[2] - w[0] == 60 and w[3] - w[1] == 44
fixtures['wall_extent'] = {'box': w, 'width': w[2] - w[0], 'height': w[3] - w[1]}

# 5. strict overlap truth table (tile rect h=20 around fixture A point)
h = sdiv(41, 2)
r = [1160 - h + 1, 1968 + 1, 1160 + h - 2, 1968 + h - 2]
assert r == [1141, 1969, 1178, 1986]
assert overlaps(r, w) is True
touch = [w[0] + 49, w[1], w[2] + 49, w[3]]
assert overlaps(r, [r[2], w[1], w[2] + 10, w[3]]) is False  # touch-only edge
assert overlaps(r, [5000, 5000, 5001, 5001]) is False
fixtures['overlap'] = {'tile_rect': r, 'wall_box': w,
                       'overlap': True, 'touch_only': False}

# 6. sweep candidate enumeration and termination (Dir entries 1, 6, 1)
candidates = []
sx, sy = 10, 10
for step in (1, 6, 1, 1, 6, 1):
    sx = (sx + dir_entries[step][0]) & 0xFF
    sy = (sy + dir_entries[step][1]) & 0xFF
    candidates.append((sx, sy))
assert candidates == [(10, 11), (11, 10), (11, 11), (11, 12), (12, 11), (12, 12)]
# termination: bounds 0..3 inset by 1 -> in-bounds 1..2
min_x, max_x, min_y, max_y = 0, 3, 0, 3
sx, sy = 0, 0
visited = []
while True:
    oob = 0
    for step in (1, 6, 1):
        sx = (sx + dir_entries[step][0]) & 0xFF
        sy = (sy + dir_entries[step][1]) & 0xFF
        visited.append((sx, sy))
        inb = (min_x + 1 <= sx <= max_x - 1 and min_y + 1 <= sy <= max_y - 1)
        if not inb:
            oob += 1
    if oob == 3:
        break
assert visited == [(0, 1), (1, 0), (1, 1), (1, 2), (2, 1), (2, 2),
                   (2, 3), (3, 2), (3, 3)]
assert (sx, sy) == (3, 3)
fixtures['sweep'] = {
    'cells_two_sweeps_from_10_10': [list(c) for c in candidates],
    'termination_cells': [list(c) for c in visited],
    'stop_at': [sx, sy],
}

# 7. side rotation law and its inverse property
side_rot = {}
for rot in range(4):
    side_rot[rot] = [rotate_side(s, rot) for s in range(1, 5)]
assert side_rot[1] == [4, 1, 2, 3]
assert side_rot[2] == [3, 4, 1, 2]
for rot in range(4):
    for s in range(1, 5):
        assert rotate_side(rotate_side(s, rot), (4 - rot) & 3) == s
assert rotate_side(0, 2) == 0
fixtures['side_rotation'] = {str(k): v for k, v in side_rot.items()}

# 8. floor filter law (independent formulas), ids new vs r243
floor_fixtures = []
for room_id, expected in [(0x155, 1), (0x401, 2), (0x8FF, 3), (0xC33, 4)]:
    by_shift = ((room_id >> 10) & 3) + 1
    by_rotl = (rotl32(room_id, 22) & 3) + 1
    assert by_shift == by_rotl == expected
    floor_fixtures.append({'room_id': hex(room_id), 'floor': expected})
fixtures['floor_filter'] = floor_fixtures

result = {
    'executable_sha256': sha,
    'pin_count': len(pins),
    'pins': {hex(k): hex(v) for k, v in sorted(pins.items())},
    'fixtures': fixtures,
}
(HERE / 'verified-inputs.json').write_text(json.dumps(result, indent=2) + '\n')
print(f'{len(pins)} executable words and {len(fixtures)} fixture groups verified')
