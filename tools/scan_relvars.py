# RE tool: scan staged Sims 1 BHAVs for relationship primitives (opcodes 24 / 26)
# and cluster the RelVar operand byte usage by named function.
#
# Purpose: assert relationship slot semantics are defined by the ORIGINAL bytecode.
# The engine (VMRelationship.cs) passes RelVar through positionally (relToTarg[RelVar],
# no remap, clamp -100..100 on writes), and the game executes the original BHAVs, so
# whatever the original packs under each RelVar is used 1:1. This scan shows the
# original Global.iff packs friendship(_daily) at RelVar 0 and love(_lifetime) at
# RelVar 1 (named functions: "am I friends with temp 0" / "get my relationship to
# temp 0" vs "Do I Love temp 0?" / "does stack object love anyone?").
#
# Layout mirrors FSO.Files BHAV.Read: instruction = u16 opcode + u8 true + u8 false
# + 8-byte operand (12 bytes each); header offsets per filetype version.
import os, struct, sys, collections

ID2_5 = b'IFF FILE 2.5:TYPE FOLLOWED BY SIZE JAMIE DOORNBOS & MAXIS 1'
ID2_0 = b'IFF FILE 2.0:TYPE FOLLOWED BY SIZE JAMIE DOORNBOS & MAXIS 1'


def read_cstr(buf, off, n):
    return buf[off:off + n].replace(bytes([0]), b'').rstrip(bytes([32, 9])), off + n


def scan(path):
    buf = open(path, 'rb').read()
    if len(buf) < 64:
        return
    ident, off = read_cstr(buf, 0, 60)
    if ident not in (ID2_5, ID2_0):
        print('  not an IFF file')
        return
    off += 4
    found = collections.defaultdict(collections.Counter)
    total = 0
    while len(buf) - off >= 8:
        ct = buf[off:off + 4].decode('latin1')
        off += 4
        csize = struct.unpack_from('>I', buf, off)[0]
        off += 4
        cid = struct.unpack_from('>H', buf, off)[0]
        off += 2
        off += 2
        clabel, off = read_cstr(buf, off, 64)
        dsz = csize - 76
        if dsz < 0 or off + dsz > len(buf):
            break
        if ct == 'BHAV':
            body = buf[off:off + dsz]
            v2 = struct.unpack_from('<H', body, 0)[0]
            if v2 == 0x8002:
                n = struct.unpack_from('<H', body, 2)[0]
                for i in range(n):
                    base = 12 + i * 12
                    if base + 12 > dsz:
                        break
                    op = struct.unpack_from('<H', body, base)[0]
                    if op in (24, 26):
                        # op 24 (old_relationship): operand[1]=RelVar; op 26: operand[0]=RelVar
                        rv = body[base + 5] if op == 24 else body[base + 4]
                        total += 1
                        found[rv][clabel.decode('latin1', 'replace')] += 1
            # other BHAV versions (0x8000/0x8001/0x8003) not found in TS1 globals
        off += dsz
    print('  total relationship calls:', total)
    for rv in sorted(found):
        top = ' | '.join('%s=%d' % (k, v) for k, v in found[rv].most_common(14))
        sem = {0: 'friendship(daily)', 1: 'love(lifetime)', 2: 'social-score?', 3: 'social-score?'}.get(rv, '?')
        print('  RelVar %-2d [%s] :: %s' % (rv, sem, top))


if __name__ == '__main__':
    for p in sys.argv[1:]:
        print('=====', os.path.basename(p))
        try:
            scan(p)
        except Exception as ex:
            print('  ERR', repr(ex))
