import struct, os
from collections import Counter
def far_members(path):
    data = open(path,'rb').read()
    if data[0:8] != b'FAR!byAZ': return []
    man = struct.unpack('<I', data[12:16])[0]
    num = struct.unpack('<I', data[man:man+4])[0]
    out = []; off = man+4
    for i in range(num):
        dlen,dlen2,doff = struct.unpack('<III', data[off:off+12])
        nlen = struct.unpack('<H', data[off+12:off+14])[0]
        nm = data[off+16:off+16+nlen].decode('latin1','replace')
        off += 16+nlen
        if dlen2==dlen and 0<doff<len(data) and dlen<len(data): out.append((nm, doff, dlen))
    return out
ROOT = 'game-data/The Sims'
fars = sorted([os.path.join(dp, fn) for dp, dns, fns in os.walk(ROOT) for fn in fns if fn.lower().endswith('.far')])
vers = Counter(); vers2 = Counter()
for fp in fars:
    data = open(fp,'rb').read()
    for nm, doff, dlen in far_members(fp):
        if not nm.lower().endswith('.iff'): continue
        dat = data[doff:doff+dlen]
        pos = 64
        while pos + 76 <= len(dat):
            ct = dat[pos:pos+4].decode('latin1','replace')
            sz = struct.unpack_from('>I', dat, pos+4)[0]
            paysz = sz - 76
            if paysz < 0: break
            if ct in ('SPR#', 'SPR2') and paysz >= 6:
                v1 = struct.unpack_from('<H', dat, pos+76)[0]
                v2 = struct.unpack_from('<H', dat, pos+76+2)[0]
                ver = ((v2 | 0xFF00) >> 8) | ((v2 & 0xFF) << 8) if v1 == 0 else v1
                (vers if ct == 'SPR#' else vers2)[ver] += 1
            pos += 76 + paysz
print('SPR# versions:', dict(vers))
print('SPR2 versions:', dict(vers2))
