import struct, os
from collections import Counter
# iff_objd_census.py - Round 56 IFF-LITERAL OBJD-corpus census. Counts every IFF OBJD chunk across all
# .far packs (same IFF chunk decode as tool(s): chunk type 'OBJD', 76-byte IFF chunk header, payload at
# +76; version = u32 LE payload[0:4]; the OBJD corpus is IFF data).
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
        if dlen2==dlen and 0<doff<len(data) and dlen<len(data): out.append((nm, data[doff:doff+dlen]))
    return out
ROOT = 'game-data/The Sims'
fars = []
for dp, dns, fns in os.walk(ROOT):
    for fn in fns:
        if fn.lower().endswith('.far'): fars.append(os.path.join(dp, fn))
fars.sort()
def decode_objds(body):
    out = []
    pos = 64
    while pos + 76 <= len(body):
        ct = body[pos:pos+4].decode('latin1','replace')
        sz = struct.unpack_from('>I', body, pos+4)[0]
        cid = struct.unpack_from('>H', body, pos+8)[0]
        paysz = sz - 76
        if paysz < 0: break
        payload = body[pos+76:pos+76+paysz] if paysz > 0 else b''
        if ct == 'OBJD' and len(payload) >= 4 + 68*2:
            ver = struct.unpack_from('<I', payload, 0)[0]
            out.append((cid, ver))
        pos += 76 + paysz
    return out
vers = Counter()
total = 0
for fp in fars:
    for nm, dat in far_members(fp):
        if not nm.lower().endswith('.iff'): continue
        if 'globals' in nm.lower(): continue
        for cid, ver in decode_objds(dat):
            total += 1
            vers[ver] += 1
print('IFF OBJD chunks (non-globals .iff):', total)
print('by version:', dict(vers))
with open('tools/iff-dump/r56-objd-census.txt','w') as g:
    g.write('IFF OBJD chunks: %d by version: %r\n' % (total, dict(vers)))
