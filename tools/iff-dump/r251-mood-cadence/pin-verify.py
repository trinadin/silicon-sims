#!/usr/bin/env python3
"""R251 final PIN round: assert the exact facts that could be pinned from the
original Mac PPC binary + the STR#502 IFF resource, and report the runtime-only
weight-table data that could NOT be pinned statically.

Run:  python3 tools/iff-dump/r251-mood-cadence/pin-verify.py
Exit 0 = every assertion holds. Exit 1 = a word/value diverged.
"""
import hashlib
import os
import re
import struct
import sys
import tempfile

BIN = '/Users/nathannoom/Developer/Games/The Sims/simitone-fork/game-data/The Sims/The Sims Complete'
GLOBAL_FAR = '/Users/nathannoom/Developer/Games/The Sims/simitone-fork/game-data/The Sims/GameData/Global/Global.far'

EXPECTED_SHA256 = '33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f'

# The 7 mood-weight curves (Global.far!Global.iff, STR# 502 'HappyWeightCurves'),
# knot (value, weight) floats, read from the IFF by parse_str502() below and
# compared against this authoritative reference (R223 decoded them; re-read here).
REF_CURVES = [
    [(-100.0, 15.0), (-60.0, 5.0), (-40.0, 3.0), (0.0, 1.0), (100.0, 1.0)],   # 0
    [(-100.0, 10.0), (-80.0, 3.0), (-40.0, 1.0), (100.0, 1.0)],               # 1
    [(-100.0, 5.0), (-80.0, 3.0), (-40.0, 1.0), (100.0, 1.0)],                # 2
    [(-100.0, 10.0), (-80.0, 3.0), (-60.0, 1.0), (100.0, 1.0)],               # 3
    [(-100.0, 5.0), (-40.0, 2.0), (40.0, 2.0), (100.0, 5.0)],                 # 4
    [(-100.0, 2.0), (0.0, 1.0), (100.0, 2.0)],                                # 5
    [(-100.0, 5.0), (-40.0, 2.0), (40.0, 2.0), (100.0, 3.0)],                 # 6
]

# ---------------------------------------------------------------- binary reads
def u32(data, a):
    return struct.unpack('>I', data[a:a + 4])[0]

def f32(data, a):
    return struct.unpack('>f', data[a:a + 4])[0]

# ------------------------------------------------------- IFF / FAR extraction
ID2 = bytes('IFF FILE 2.5:TYPE FOLLOWED BY SIZE JAMIE DOORNBOS & MAXIS 1', 'ascii')


def extract_iff(far_path, want_name):
    """Return the raw bytes of the file named `want_name` inside a FAR1 archive."""
    data = open(far_path, 'rb').read()
    assert data[0:8] == b'FAR!byAZ', 'not a FAR archive'
    man = struct.unpack('<I', data[12:16])[0]
    num = struct.unpack('<I', data[man:man + 4])[0]
    off = man + 4
    for _ in range(num):
        dlen = struct.unpack('<I', data[off:off + 4])[0]
        dlen2 = struct.unpack('<I', data[off + 4:off + 8])[0]
        doff = struct.unpack('<I', data[off + 8:off + 12])[0]
        nlen = struct.unpack('<I', data[off + 12:off + 16])[0]
        name = data[off + 16:off + 16 + nlen].decode('latin1', 'replace')
        off += 16 + nlen
        if name == want_name:
            return data[doff:doff + dlen]
    raise LookupError('file %r not in %s' % (want_name, far_path))


def find_str_chunk(iff, chunk_type, chunk_id):
    off = 64  # 60-byte ident + 4-byte rsmp
    while len(iff) - off >= 2:
        ct = iff[off:off + 4].decode('latin1', 'replace')
        csize = struct.unpack('>I', iff[off + 4:off + 8])[0]
        cid = struct.unpack('>H', iff[off + 8:off + 10])[0]
        off += 76
        data_sz = csize - 76
        if data_sz < 0 or off + data_sz > len(iff):
            break
        if ct == chunk_type and cid == chunk_id:
            return iff[off:off + data_sz]
        off += data_sz
    raise LookupError('chunk %s#%d not found' % (chunk_type, chunk_id))


def parse_str502(iff):
    body = find_str_chunk(iff, 'STR#', 502)
    assert struct.unpack('<h', body[0:2])[0] == -3, 'STR#502 is not format -3'
    n = struct.unpack('<h', body[2:4])[0]
    i = 4
    curves = []
    for _ in range(n):
        i += 1  # lang byte
        j = body.index(b'\x00', i)
        val = body[i:j].decode('latin1')
        i = j + 1
        j = body.index(b'\x00', i)
        i = j + 1  # comment (empty)
        knots = []
        for m in re.finditer(r'\(([-0-9.]+);([-0-9.]+)\)', val):
            knots.append((float(m.group(1)), float(m.group(2))))
        curves.append(knots)
    return curves


# --------------------------------------------------------------- assertions
def main():
    status = 0
    data = open(BIN, 'rb').read()

    # 1. executable SHA256
    sha = hashlib.sha256(data).hexdigest()
    if sha != EXPECTED_SHA256:
        print('FAIL SHA256\n  got   %s\n  expect %s' % (sha, EXPECTED_SHA256))
        status = 1
    else:
        print('OK SHA256 %s' % sha)

    # 2. STR#502 curves (authoritative knot data the runtime weight table is
    #    built from) match the reference.
    export_dir = tempfile.mkdtemp(prefix='iffpin-')
    global_iff = extract_iff(GLOBAL_FAR, 'Global.iff')
    try:
        curves = parse_str502(global_iff)
    except LookupError as e:
        print('FAIL reading STR#502:', e)
        status = 1
        curves = []
    if curves == REF_CURVES:
        print('OK STR#502: 7 curves parsed and match reference (exact floats)')
    else:
        print('FAIL STR#502 mismatch:\n  got %r\n  ref %r' % (curves, REF_CURVES))
        status = 1

    # 3. CalcHappy instruction words at the addresses cited in decode.md
    #    (cXPerson::CalcHappy @ file 0x10b9c0).
    CALCHAPPY = [
        (0x10b9c0, 0x8102a718, 'lwz r8, -0x58e8(r2)   -> r8 floats table base'),
        (0x10b9c8, 0xa8830600, 'lha r4, 1536(r3)      -> person type (type-split)'),
        (0x10b9f0, 0x8082a704, 'lwz r4, -0x58fc(r2)   -> weight table A (type in [1,17])'),
        (0x10b9f8, 0x8082a700, 'lwz r4, -0x5900(r2)   -> weight table B (type 0 or >=18)'),
        (0x10ba10, 0x80040010, 'lwz r0, 16(r4)        -> motive index at entry+16'),
        (0x10ba14, 0x80c40008, 'lwz r6, 8(r4)         -> knot count at entry+8'),
        (0x10ba24, 0x7c83042e, 'lfsx f4, r3, r0       -> f4 = person[1932 + 4*idx]'),
        (0x10ba2c, 0xc0280004, 'lfs f1, 4(r8)         -> [r8+4] = constant weight (knot count 0)'),
        (0x10bad4, 0xa8c3060e, 'lha r6, 1550(r3)      -> person+1550 = suit index'),
        (0x10bad8, 0x38000001, 'li r0, 1              -> (NOT addi r0,r0,1)'),
        (0x10baf4, 0xc0080030, 'lfs f0, 0x30(r8)      -> THE GATE CONSTANT'),
        (0x10baf8, 0xfc002000, 'fcmpu cr0, f0, f4'),
        (0x10bafc, 0x4182000c, 'beq 0x10bb08          -> drop motive if gate==motive'),
        (0x10bb00, 0xecc4307a, 'fmadds f6, f4, f1, f6 -> f6 += m*w'),
        (0x10bb04, 0xeca5082a, 'fadds f5, f5, f1      -> f5 += w'),
        (0x10bb14, 0xec062824, 'fdivs f0, f6, f5      -> mood = sum(m*w)/sum(w)'),
        (0x10bb18, 0xd0030798, 'stfs f0, 1944(r3)     -> person[1944] = mood'),
    ]
    for off, exp, note in CALCHAPPY:
        w = u32(data, off)
        if w != exp:
            print('FAIL 0x%08x  got 0x%08x  expect 0x%08x  %s' % (off, w, exp, note))
            status = 1
        else:
            print('OK   0x%08x  0x%08x  %s' % (off, w, note))

    # 4. The r8 float table (located via the TOC slot at runtime
    #    TOC-0x58e8=0x5bbb78; its value is the image-base-relative runtime
    #    address 0x59a524 -> file 0x5a33b4). Pin [r8+4] and [r8+0x30].
    R8_FILE = 0x5a33b4
    r8_4 = f32(data, R8_FILE + 4)
    r8_30 = f32(data, R8_FILE + 0x30)
    if r8_4 == 0.0:
        print('OK   r8 float table @ file 0x%x: [r8+4] = 0.0 (accumulator init / no-curve weight)' % R8_FILE)
    else:
        print('FAIL [r8+4] = %r != 0.0' % r8_4)
        status = 1
    if r8_30 == 13.0:
        print('OK   r8 float table @ file 0x%x: [r8+0x30] = 13.0  <-- GATE CONSTANT (not 0.0)' % R8_FILE)
    else:
        print('FAIL [r8+0x30] = %r != 13.0' % r8_30)
        status = 1

    # 5. CalcHappy iteration order (motive indices) -- the static rodata copy
    #    the runtime weight table's +16 fields are built from.
    ORDER = [u32(data, 0x5a45b8 + i * 4) for i in range(8)]
    if ORDER == [7, 5, 6, 15, 8, 14, 9, 13]:
        print('OK   CalcHappy motive order @ file 0x5a45b8 = (7,5,6,15,8,14,9,13) [Hunger,Energy,Comfort,Fun,Hygiene,Social,Bladder,Room]')
    else:
        print('FAIL motive-order array = %r' % ORDER)
        status = 1

    # 6. Entry-initializer (0x117d70) -- PROVES the knot arrays are runtime
    #    HEAP, not static. It allocates  (bl 0x591570)  ptrA = count*8 bytes and
    #    ptrB = (count-1)*4 bytes into the entry, so a static image search for
    #    the (value,weight) floats can never succeed.
    INIT = [
        (0x117dcc, 0x57e31838, 'rlwinm r3, r31<<3  (count*8 -> ptrA size)'),
        (0x117dd0, 0x484797a1, 'bl 0x591570       (heap alloc ptrA)'),
        (0x117ddc, 0x3803ffff, 'addi r0, r3, -1   (count-1)'),
        (0x117de0, 0x5403103a, 'rlwinm r3, r0,2,0,28  ((count-1)*4 -> ptrB size)'),
        (0x117de4, 0x4847978d, 'bl 0x591570       (heap alloc ptrB)'),
    ]
    for off, exp, note in INIT:
        w = u32(data, off)
        if w != exp:
            print('FAIL 0x%08x  got 0x%08x  expect 0x%08x  %s' % (off, w, exp, note))
            status = 1
        else:
            print('OK   0x%08x  0x%08x  %s' % (off, w, note))

    print('\n--- NOT statically pin-able (documented finding) ---')
    print('The CalcHappy weight tables (tableA/B) and their knot arrays are RUNTIME state:')
    print('  * tableA/B slots resolve to the DATA section BSS (offsets 0x858b8/0x85958')
    print('    are beyond the initialized data, which unpacks to ~0x7bf80).')
    print('  * entry +0/+4 (knot arrays) are heap-allocated by 0x117d70 and filled from')
    print('    STR#502 at runtime. Therefore the curve->motive mapping and room weight')
    print('    are NOT present as static (value,weight) floats and cannot be pinned from')
    print('    static analysis. The STR#502 curves above are the authoritative source.')

    print('\nRESULT:', 'PASS' if status == 0 else 'FAIL')
    sys.exit(status)


if __name__ == '__main__':
    main()
