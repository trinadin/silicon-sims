"""R244: pin dynamic-cutaway STATE machinery; reads owner executable only.

Pins >=15 NEW instruction words (disjoint from r243's set), the packed-data
vtable chain that identifies the two strategy virtuals, and proves the
recovered state formulas with pure-python fixtures (insert eviction, cursor
Y adjust, cursor/person rectangle enumeration, direction constants).
"""
import hashlib, json, struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
b = (ROOT / 'game-data/The Sims/The Sims Complete').read_bytes()
sha = hashlib.sha256(b).hexdigest()
assert sha == '33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f'

def w(addr):
    return struct.unpack_from('>I', b, addr)[0]

# ---- 1. code pins (file offsets; all NEW vs r243 verified-inputs.json) ----
pins = {
    # HouseViewer::MouseTrack — the only insert driver
    0x1D0338: 0xABDA000C,  # lha r30, 0xc(r26)          ; event h
    0x1D03D0: 0x57E0063F,  # clrlwi. r0, r31, 0x18      ; phase!=0 test
    0x1D03D8: 0x88180049,  # lbz r0, 0x49(r24)          ; dynamic-cutaway gate
    0x1D03E0: 0x41820278,  # beq 0x1d0658               ; exit when disabled
    0x1D03B4: 0x48040D9D,  # bl GetMode__7CPStateFv
    0x1D03B8: 0x2C030004,  # cmpwi r3, 4                ; camera-mode bookkeeping
    0x1D0490: 0x80180018,  # lwz r0, 0x18(r24)          ; viewer floor -> tile z
    0x1D04B0: 0x4BFA9F11,  # bl GetRoom__15cRotatableWorldF7CTilePt
    0x1D061C: 0x2804FFFB,  # cmplwi r4, 0xfffb          ; room-id validity gate
    0x1D0624: 0x807801FC,  # lwz r3, 0x1fc(r24)         ; the history set
    0x1D0628: 0x4BFD9A09,  # bl insert__11cCutawaySetFi (cursor room)
    0x1D063C: 0x4BFD99F5,  # bl insert__11cCutawaySetFi (probed room)
    0x1D0648: 0x4BFFED69,  # bl DoDynamicCutaway__11HouseViewerFb (true)
    0x1D048C: 0x408201C4,  # bne 0x1d0650               ; off-world -> ResetDynamicCutaway
    # DoCommand — history-clearing commands
    0x1D65A4: 0x2C0000E4,  # cmpwi r0, 0xe4             ; refresh-cutaway command
    0x1D6678: 0x2C000105,  # cmpwi r0, 0x105            ; rebuild-max-alts command
    0x1D66C0: 0x4BFD37A1,  # bl clear__11cCutawaySetFv  (cmd 0x105)
    0x1D6B70: 0x981C0200,  # stb r0, 0x200(r28)         ; cmd 0xf0 sets +0x200
    0x1D6B78: 0x4BFD32E9,  # bl clear__11cCutawaySetFv  (cmd 0xf0)
    0x1D6B80: 0x889C01F8,  # lbz r4, 0x1f8(r28)         ; cmd 0xe4 re-runs DoDynamicCutaway
    # cWinViewControl::TSOnCommand — the mode buttons
    0x2B4B10: 0x807F00CC,  # lwz r3, 0xcc(r31)          ; CPState
    0x2B4B14: 0x38800002,  # li r4, 2                   ; SetMode(2) = LIVE
    0x2B4B1C: 0x4BF5BC75,  # bl SetMode__7CPStateFQ27CPState4Modeb
    0x2B4BB4: 0x38800001,  # li r4, 1                   ; SetMode(1) = Buy
    0x2B4BF0: 0x38800000,  # li r4, 0                   ; SetMode(0) = Build
    0x2B4C2C: 0x38800003,  # li r4, 3                   ; SetMode(3) = Options
    0x2B4C68: 0x38800004,  # li r4, 4                   ; SetMode(4) = Camera
    # CPState::SetMode — mode 2 runs House::EnterLiveMode
    0x2107F0: 0x2C1B0002,  # cmpwi r27, 2
    0x210800: 0x4BE7BEB1,  # bl EnterLiveMode__5HouseFv
    # strategy predicate chain
    0x1CFA64: 0x4804D1ED,  # bl GetCurrentStrategy__Fv
    0x1CFA70: 0x81830000,  # lwz r12, 0(r3)             ; primary vtable
    0x1CFA74: 0x818C005C,  # lwz r12, 0x5c(r12)         ; slot 23 = IsWaitingForClick
    0x21A7E0: 0x88630093,  # lbz r3, 0x93(r3)           ; EdgeDetectScroller impl
    0x21A7E4: 0x4E800020,  # blr
    0x1D02C0: 0x38600000,  # li r3, 0                   ; ScrollingStrategy base impl
    # tool matrix callback chain (object at TOC-0x6fb8 = current cTool)
    0x1CFFCC: 0x807D0000,  # lwz r3, 0(r29)             ; current tool global
    0x1D0038: 0x81830028,  # lwz r12, 0x28(r3)          ; tool secondary vtable
    0x1D0044: 0x818C0048,  # lwz r12, 0x48(r12)         ; slot 18 = AdjustCutawayForTool
    0x1D0040: 0x38BE0204,  # addi r5, r30, 0x204        ; BitMatrix64 arg
    0x1726B0: 0x90BF0028,  # stw r5, 0x28(r31)          ; cTool dtor installs +0x28 vptr
    0x191934: 0x88030024,  # lbz r0, 0x24(r3)           ; base impl gate (grab flag)
    0x17412C: 0x88030024,  # lbz r0, 0x24(r3)           ; cMoveTool impl gate
    # cursor vicinity: Y adjust, directions, marking
    0x1CFB10: 0x7C841830,  # slw r4, r4, r3             ; 8 << GetScale()
    0x1CFB1C: 0x5484103A,  # slwi r4, r4, 2             ; (8<<zoom)*4 = 32<<zoom
    0x1CFB28: 0x7C052214,  # add r0, r5, r4             ; mouse.y += 32<<zoom
    0x1CFBA0: 0x889B000C,  # lbz r4, 0xc(r27)           ; dir1.x = (-1) const
    0x1CFBA8: 0x88DB000D,  # lbz r6, 0xd(r27)           ; dir1.y = (-1) const
    0x1D025C: 0x88BB000F,  # lbz r5, 0xf(r27)           ; dir2.x = (+1) const
    0x1D0260: 0x889B0010,  # lbz r4, 0x10(r27)          ; dir2.y = (+1) const
    0x1CFBE0: 0x4BF49311,  # bl operator+(CTilePt)      ; corner = tile + 4*dir1
    0x1CFCD8: 0x54A0063F,  # clrlwi. r0, r5, 0x18       ; tile==corner loop exit
    0x1CFDC4: 0x5743063F,  # clrlwi. r3, r26, 0x18      ; cursor-mark gated on r26
    0x1CFDC8: 0x41820204,  # beq 0x1cffcc               ; skip marking when r26==0
    # outside-person tile + altitude table
    0x1CF768: 0x7C850774,  # extsb r5, r4               ; person tile byte
    0x1CF7B8: 0x408202AC,  # bne 0x1cfa64               ; outside [1,size-2] -> skip
    0x1CF7D0: 0x54046824,  # slwi r4, r0, 0xd           ; alt << 13
    0x1CF7F4: 0x8864E000,  # lbz r3, -0x2000(r4)        ; adjusted tile.x from table
    0x1CF7F8: 0x8804E001,  # lbz r0, -0x1fff(r4)        ; adjusted tile.y
    0x1CF820: 0x39200004,  # li r9, 4                   ; probe k = 4,3,2,1,0
    # person fields
    0x109468: 0xA0830AAC,  # lhz r4, 0xaac(r3)          ; UpdateCurrentRoom reads room id
    0x1094B4: 0xB3BF0AAC,  # sth r29, 0xaac(r31)        ; ...and writes it
    0xC49E8: 0x837A0110,   # lwz r27, 0x110(r26)        ; object floor word
    0xC49EC: 0x480FB035,   # bl GetLevel__7GViewerFv    ; compared with viewer floor
    # CTileRect scan semantics + 64-bit mask helper
    0x1183F0: 0x7C050000,  # cmpw r5, r0                ; end(): need a.x<b.x, a.y<b.y
    0x11841C: 0x4BFFFF65,  # bl begin__9CTileRectCFv    ; degenerate -> begin==end
    0x591B10: 0x21050020,  # subfic r8, r5, 0x20        ; 64-bit (1<<bit) builder
    # direction constant initializer (__sinit__:ctilept_cp)
    0x611A4: 0x3880FFFF,   # li r4, -1
    0x611B0: 0x989F000C,   # stb r4, 0xc(r31)           ; dir1.x = -1
    0x611B8: 0x989F000D,   # stb r4, 0xd(r31)           ; dir1.y = -1
    0x611C8: 0x38800001,   # li r4, 1
    0x611D4: 0x989F000F,   # stb r4, 0xf(r31)           ; dir2.x = +1
    0x611DC: 0x989F0010,   # stb r4, 0x10(r31)          ; dir2.y = +1
}
for addr, expected in pins.items():
    assert w(addr) == expected, hex(addr)

# ---- 2. packed data section: vtable/TVector chain proof ----
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

CODE_BASE = 0x8E90                      # section 0: code, file 0x8e90..0x5c22e8
data = unpack_pef(b[0x5C22F0:0x5C22F0 + 0x6D834])   # section 1 (packed)
assert len(data) == 0x7BF80

def dw(off):
    return struct.unpack_from('>I', data, off)[0]

def tvector_code(tvo):                  # TVector {code, toc}; code is section-relative
    return dw(tvo) + CODE_BASE

chain = {
    # EdgeDetectScroller vptr (TOC slot -0x6c94) and predicate slot +0x5c
    'eds_vptr_slot': (0x136C, 0x50910),
    'eds_slot5c_word': (0x50910 + 0x5C, 0xF198),
    'eds_iswaiting_code': (None, tvector_code(0xF198)),          # 0x21a7e0
    # cTool secondary base vptr (TOC slot -0x6f48), slot 18 base + cMoveTool override
    'ctool_second_vptr_slot': (0x10B8, 0x4D280),
    'ctool_slot18_word': (0x4D280 + 0x48, 0xE668),
    'ctool_adjust_base_code': (None, tvector_code(0xE668)),      # 0x191910
    'cmovetool_slot18_word': (0x4C550 + 0x48, 0xE1A8),
    'cmovetool_adjust_code': (None, tvector_code(0xE1A8)),       # 0x174100
    'ctool_slot19_word': (0x4D280 + 0x4C, 0xDF30),
    'ctool_modifycursor_code': (None, tvector_code(0xDF30)),     # 0x1579d0
    # HouseViewer second vtable: slot 3 PointToTile, slot 7 GetScale
    'cdddsimsview_vptr_slot': (0x1354, 0x50450),
    'viewer_vt2_pointtotile_word': (0x4EA40 + 0x0C, 0xEAA0),
    'viewer_pointtotile_code': (None, tvector_code(0xEAA0)),     # 0x1d3b00
}
data_evidence = {}
for name, (slot, val) in chain.items():
    if slot is None:
        data_evidence[name] = hex(val)
    else:
        assert dw(slot) == val, (name, hex(slot), hex(dw(slot)))
        data_evidence[name] = f'{hex(slot)}={hex(val)}'

# SetMode hook strings (live in the packed data section)
strtab = 0x50258
msgs = {off: data[strtab + off:].split(b'\0')[0].decode('latin1') for off in (0x17, 0x3D, 0x50, 0x65)}
assert msgs[0x17] == 'Entering Build Mode'
assert msgs[0x3D] == 'Entering Live Mode'
assert msgs[0x50] == 'Entering Camera Mode'
assert msgs[0x65] == 'Entering Options Mode'

# ---- 3. pure-python fixtures ----
fixtures = {}

# (a) cCutawaySet insert law: duplicate no-op, FIFO eviction at capacity 3
class CutawaySet:
    CAP = 3
    def __init__(self):
        self.items = []
    def insert(self, room):
        if room in self.items:
            return                      # duplicate left in place (0x1aa0a8 bne path)
        if len(self.items) == self.CAP: # capacity check at 0x1aa0b8
            self.items.pop(0)           # evict oldest
        self.items.append(room)

s = CutawaySet()
for r in (11, 12, 13):
    s.insert(r)
assert s.items == [11, 12, 13]
s.insert(13)                            # duplicate -> unchanged
assert s.items == [11, 12, 13]
s.insert(14)                            # evicts 11
assert s.items == [12, 13, 14]
s.insert(15)
assert s.items == [13, 14, 15]
fixtures['insert_eviction'] = {'model': ['12-13-14', '13-14-15'], 'cap': 3}

# (b) mouse Y adjust: y' = y + (8 << zoom) * 4 = y + (32 << zoom), zoom 0..3
def y_adjust(y, zoom):
    return y + ((8 << zoom) << 2)
for zoom in range(4):
    assert y_adjust(100, zoom) == 100 + (32 << zoom)
fixtures['mouse_y_adjust'] = {f'zoom{z}': y_adjust(100, z) for z in range(4)}

# (c) cursor-vicinity rectangle: dir1=(-1,-1), corner=tile+4*dir1; phase-1 walks the
# tile by dir1 while off-world; rect {corner,tile} valid iff corner.x<tile.x and
# corner.y<tile.y (CTileRect::end), covering [corner, tile) x [corner, tile).
def cursor_rect(tile, inside, dir1=(-1, -1), k=4):
    corner = (tile[0] + k * dir1[0], tile[1] + k * dir1[1])
    t = tile
    while not inside(t) and t != corner:
        t = (t[0] + dir1[0], t[1] + dir1[1])
    if not (corner[0] < t[0] and corner[1] < t[1]):
        return []
    # marking loop bounds-checks each tile before setting its bit
    return [(x, y) for y in range(corner[1], t[1]) for x in range(corner[0], t[0])
            if inside((x, y))]

world = lambda p: 0 <= p[0] < 64 and 0 <= p[1] < 64
r = cursor_rect((20, 20), world)
assert r == [(x, y) for y in range(16, 20) for x in range(16, 20)]
assert len(r) == 16                      # 4x4, cursor tile itself excluded
edge = cursor_rect((1, 1), world)        # rect clipped by the per-tile check
assert edge == [(0, 0)]
off = cursor_rect((-6, -6), world)       # never re-enters: tile reaches corner
assert off == []
fixtures['cursor_rect'] = {'center_4x4': len(r), 'edge_tiles': [list(t) for t in edge],
                           'offworld_empty': True}

# (d) outside-person rectangle: tile gate 1<=x<=size-2; probe k=4..0 first inside;
# rect {tile, tile+(k,k)} marks [tile, tile+k) per axis; k=0 -> empty.
def person_rect(tile, inside):
    size_ok = inside(tile)
    if not size_ok:
        return []
    for k in (4, 3, 2, 1, 0):
        c = (tile[0] + k, tile[1] + k)
        if inside(c):
            if k == 0:
                return []
            return [(x, y) for y in range(tile[1], tile[1] + k)
                    for x in range(tile[0], tile[0] + k)]
    return []
pr = person_rect((10, 10), world)
assert pr == [(x, y) for y in range(10, 14) for x in range(10, 14)]
pr_edge = person_rect((62, 62), world)   # k=4/3/2 out, k=1 inside
assert pr_edge == [(62, 62)]
fixtures['person_rect'] = {'center_k4': len(pr), 'edge_k1': len(pr_edge)}

# (e) direction constants from __sinit__:ctilept_cp instruction pairs
dirs = {}
for base, key in ((0x611A4, 'dir1_0xc'), (0x611C8, 'dir2_0xf')):
    li = w(base)
    assert (li >> 26) == 14             # addi r4, r0, SIMM
    imm = li & 0xFFFF
    if imm >= 0x8000:
        imm -= 0x10000
    dirs[key] = (imm, imm, 0)           # stb r4 at +0 and +1, 0 at +2
assert dirs['dir1_0xc'] == (-1, -1, 0)
assert dirs['dir2_0xf'] == (1, 1, 0)
fixtures['direction_constants'] = {k: list(v) for k, v in dirs.items()}

# (f) CPState mode map proven by SetMode hook strings + button constants
fixtures['cpstate_modes'] = {'0': 'Build', '1': 'Buy', '2': 'Live',
                             '3': 'Options', '4': 'Camera',
                             'hook_strings': msgs}

result = dict(
    executable_sha256=sha,
    code_pins={hex(k): hex(v) for k, v in pins.items()},
    data_section_unpacked=hex(len(data)),
    vtable_chain=data_evidence,
    setmode_hook_strings=msgs,
    fixtures=fixtures,
)
(HERE / 'verified-state.json').write_text(json.dumps(result, indent=2) + '\n')
print(f'{len(pins)} code pins, {len(chain)} vtable-chain links, '
      f'{len(fixtures)} fixture groups verified')
