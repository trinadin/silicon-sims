
import struct, collections, json
NL = chr(10)
path = 'game-data/The Sims/GameData/Global/Global.far'
data = open(path, 'rb').read()
man = struct.unpack('<I', data[12:16])[0]
num = struct.unpack('<I', data[man:man+4])[0]
off = man + 4

by_id = collections.defaultdict(list)  # bhav chunk id -> [(file, label)]
for i in range(num):
    rec = data[off:off+14]
    dlen = struct.unpack('<I', rec[0:4])[0]
    dlen2 = struct.unpack('<I', rec[4:8])[0]
    doff = struct.unpack('<I', rec[8:12])[0]
    nlen = struct.unpack('<H', rec[12:14])[0]
    nm = data[off+16:off+16+nlen].decode('latin1','replace')
    off += 16 + nlen
    if not (nm.lower().endswith('.iff') and dlen2 == dlen and doff < len(data)):
        continue
    body = data[doff:doff+dlen]
    pos = 64
    while pos + 76 <= len(body):
        ct = body[pos:pos+4].decode('latin1','replace')
        sz = struct.unpack_from('>I', body, pos+4)[0] - 76
        cid = struct.unpack_from('>H', body, pos+10)[0]
        clabel = body[pos+16:pos+80].replace(bytes([0]), b'').decode('latin1','replace').strip()
        pos += 76
        if ct == 'BHAV':
            by_id[cid].append((nm, clabel))
        pos += sz

top_ops = [281, 280, 321, 365, 320, 357, 363, 265, 424, 379, 290, 355, 446, 309, 287, 306, 469, 393, 288, 271]
for op in top_ops:
    hits = by_id.get(op)
    print('global-call opcode %d (0x%04x):' % (op, op), hits if hits else 'NOT FOUND as BHAV id in globals')
