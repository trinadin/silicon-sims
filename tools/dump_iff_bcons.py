#!/usr/bin/env python3
# dump_iff_bcons.py - print BCON constant tables (raw) from an IFF file.
# Usage: dump_iff_bcons.py <file.iff> [bconId ...]
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
        if ctype == 'BCON' and (not ids or cid in ids):
            n = payload[0]
            flags = payload[1]
            consts = struct.unpack('<%dH' % min(n, (paysz-2)//2), payload[2:2+min(n,(paysz-2)//2)*2])
            print('BCON id=%d label=%r n=%d flags=%d consts=%s' % (cid, label, n, flags, list(consts)))
        off += 76 + paysz
    return 0

if __name__ == '__main__':
    sys.exit(main())

