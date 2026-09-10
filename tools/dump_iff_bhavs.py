#!/usr/bin/env python3
# dump_iff_bhavs.py - print raw BHAV instruction records from an IFF file.
# Usage: dump_iff_bhavs.py <file.iff> [bhavId ...]
# IFF chunk header: 4-byte type, u32 size, u16 id, u16 flags, 64-byte label (76 bytes);
# BHAV payload little-endian, version 0x8002: u16 ver, u16 count, then per instruction
# u16 opcode + u8 true + u8 false + 8-byte operand.
import struct, sys

def main():
    path = sys.argv[1]
    ids = [int(x) for x in sys.argv[2:]] if len(sys.argv) > 2 else []
    data = open(path, 'rb').read()
    off = 64
    while off + 2 <= len(data):
        ctype = data[off:off+4].decode('latin1')
        csize = struct.unpack('>I', data[off+4:off+8])[0]
        cid = struct.unpack('>H', data[off+8:off+10])[0]
        label = data[off+12:off+76].split(bytes([0]))[0].decode('latin1', 'replace')
        paysz = csize - 76
        payload = data[off+76:off+76+paysz]
        if ctype == 'BHAV' and (not ids or cid in ids):
            ver = struct.unpack('<H', payload[0:2])[0]
            cnt = struct.unpack('<H', payload[2:4])[0]
            print('### BHAV id=%d label=%r ver=0x%04x count=%d' % (cid, label, ver, cnt))
            p = 12
            for i in range(cnt):
                op, tp, fp = struct.unpack('<HBB', payload[p:p+4])
                opd = payload[p+4:p+12]
                print('  ins%d op=%d t=%d f=%d opd=%s' % (i, op, tp, fp, opd.hex()))
                p += 12
        off += 76 + paysz
    return 0

if __name__ == '__main__':
    sys.exit(main())

