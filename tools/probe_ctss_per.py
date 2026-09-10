import struct, os, io
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
per = {}
for fp in fars:
    for nm, dat in far_members(fp):
        if not nm.lower().endswith('.iff'): continue
        c = 0
        pos = 64
        while pos + 76 <= len(dat):
            ct = dat[pos:pos+4].decode('latin1','replace')
            sz = struct.unpack_from('>I', dat, pos+4)[0]
            paysz = sz - 76
            if paysz < 0: break
            if ct == 'CTSS': c += 1
            pos += 76 + paysz
        if c: per[nm[0].upper() + nm[1:]] = c
out = ['%s;%d' % (k, v) for k, v in sorted(per.items())]
open('/tmp/r57-iff-ctss.txt','w').write('\n'.join(out) + '\n')
print('iff per-member chunks:', sum(per.values()), 'members:', len(per))
