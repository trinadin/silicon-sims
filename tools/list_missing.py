import struct, os, collections
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
def member_counts(dat):
    h = collections.Counter()
    pos = 64
    while pos + 76 <= len(dat):
        ct = dat[pos:pos+4].decode('latin1','replace')
        sz = struct.unpack_from('>I', dat, pos+4)[0]
        paysz = sz - 76
        if paysz < 0: break
        if ct: h[ct] += 1
        pos += 76 + paysz
    return h
best = {}
for fp in fars:
    data = open(fp,'rb').read()
    for nm, doff, dlen in far_members(fp):
        if not nm.lower().endswith('.iff'): continue
        k = nm.replace('\\','/').rsplit('/',1)[-1].lower()
        h = member_counts(data[doff:doff+dlen])
        if h: best[k] = h
eng = set()
dump = open('/var/folders/sg/3fjlktk972j1zkbssc8dfcrw0000gn/T/r65-inv.txt').read().split('\n')
for line in dump:
    if ' | ' not in line: continue
    eng.add(line.split(' | ', 1)[0].lower())
miss = sorted(set(eng) - set(best))
print('engine-only (not in iff census):')
for m in miss: print('  ', m)
