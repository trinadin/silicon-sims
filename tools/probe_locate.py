import struct, os
from collections import Counter
miss = [l.rstrip('\n') for l in open('tools/iff-dump/r54-missing-xa.txt') if l.strip()]
fars = []
for dp, dns, fns in os.walk('game-data/The Sims'):
    for fn in fns:
        if fn.lower().endswith('.far'): fars.append(os.path.join(dp, fn))
fars.sort()
def members(path):
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
        if dlen2==dlen and 0<doff<len(data) and dlen<len(data): out.append((nm, os.path.relpath(path, 'game-data/The Sims')))
    return out
index = {}
for fp in fars:
    for nm, rel in members(fp):
        index[nm] = rel
found = Counter()
unfound = []
for m in miss:
    if m in index:
        found[index[m]] += 1
    else:
        unfound.append(m)
print('found locations:')
for k, v in found.most_common():
    print('%5d  %s' % (v, k))
print('unfound:', len(unfound))
for m in unfound[:8]: print('  ?', m)
