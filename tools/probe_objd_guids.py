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
        if dlen2==dlen and 0<doff<len(data) and dlen<len(data): out.append((nm, data[doff:doff+dlen]))
    return out
ROOT = 'game-data/The Sims'
fars = []
for dp, dns, fns in os.walk(ROOT):
    for fn in fns:
        if fn.lower().endswith('.far'): fars.append(os.path.join(dp, fn))
fars.sort()
def objd_chunks(body):
    out = []
    pos = 64
    while pos + 76 <= len(body):
        ct = body[pos:pos+4].decode('latin1','replace')
        sz = struct.unpack_from('>I', body, pos+4)[0]
        paysz = sz - 76
        if paysz < 0: break
        payload = body[pos+76:pos+76+paysz] if paysz > 0 else b''
        if ct == 'OBJD' and len(payload) >= 4 + 68*2:
            ver = struct.unpack_from('<I', payload, 0)[0]
            fld = struct.unpack_from('<%dH' % ((len(payload)-4)//2), payload, 4)
            guid = (fld[13] << 16) | fld[12]
            out.append((guid, ver))
        pos += 76 + paysz
    return out
chunks = []
for fp in fars:
    for nm, dat in far_members(fp):
        if not nm.lower().endswith('.iff'): continue
        if 'globals' in nm.lower(): continue
        chunks += objd_chunks(dat)
guids = [g for g, _ in chunks]
print('chunks:', len(chunks), 'distinct GUIDs:', len(set(guids)))
dup = [g for g, c in Counter(guids).items() if c > 1]
print('dup GUIDs:', len(dup), dup[:6])
vers_by_guid = Counter(v for _, v in chunks)
print('chunk versions:', dict(vers_by_guid))
