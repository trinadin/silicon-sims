
import struct
NL = chr(10)
path = 'game-data/The Sims/GameData/Global/Global.far'
data = open(path, 'rb').read()
man = struct.unpack('<I', data[12:16])[0]
num = struct.unpack('<I', data[man:man+4])[0]
off = man + 4
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
    ids = []
    pos = 64
    while pos + 76 <= len(body):
        ct = body[pos:pos+4].decode('latin1','replace')
        sz = struct.unpack_from('>I', body, pos+4)[0] - 76
        cid = struct.unpack_from('>H', body, pos+8)[0]
        if ct == 'BHAV':
            ids.append(cid)
        pos += 76 + sz
    if ids:
        print('%-28s bhav_ids=%d  range=0x%04x..0x%04x' % (nm, len(ids), min(ids), max(ids)))
