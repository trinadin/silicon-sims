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
vers = Counter(); samp = []
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
            if ct == 'DGRP':
                if paysz >= 4:
                    v = struct.unpack_from('<H', dat, pos+76)[0]
                    vers[v] += 1
                    if len(samp) < 8:
                        samp.append((nm.split('\\')[-1].split('/')[-1], v, paysz))
            pos += 76 + paysz
print('DGRP versions:', dict(vers))
for s in samp: print('  sample', s)
