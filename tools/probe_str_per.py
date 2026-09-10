import struct, os
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
fars = [os.path.join(dp, fn) for dp, dns, fns in os.walk(ROOT) for fn in fns if fn.lower().endswith('.far')]
fars.sort()
best = {}
for fp in fars:
    data = open(fp,'rb').read()
    for nm, doff, dlen in far_members(fp):
        if not nm.lower().endswith('.iff'): continue
        dat = data[doff:doff+dlen]
        k = nm.replace('\\','/').rsplit('/',1)[-1]
        c = 0; t = 0
        pos = 64
        while pos + 76 <= len(dat):
            ct = dat[pos:pos+4].decode('latin1','replace')
            sz = struct.unpack_from('>I', dat, pos+4)[0]
            paysz = sz - 76
            if paysz < 0: break
            if ct == 'STR#': c += 1
            elif ct == 'TTAs': t += 1
            pos += 76 + paysz
        if c: best.setdefault(k, [c, 0])[0] = c
        if t: best.setdefault(k, [0, t])[1] = t
# Python: same basename last-wins
per_s = {k: v[0] for k, v in best.items() if v[0]}
per_t = {k: v[1] for k, v in best.items() if v[1]}
open('/tmp/r58-iff-strsharp.txt','w').write('\n'.join('%s;%d' % (k, v) for k, v in sorted(per_s.items())) + '\n')
open('/tmp/r58-iff-ttas.txt','w').write('\n'.join('%s;%d' % (k, v) for k, v in sorted(per_t.items())) + '\n')
print('iff per-member: STR#', sum(per_s.values()), 'TTAs', sum(per_t.values()))
