import struct, os
ROOT = 'game-data/The Sims'
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
fars = []
for dp,dns,fns in os.walk(ROOT):
    for fn in fns:
        if fn.lower().endswith('.far'): fars.append(os.path.join(dp, fn))
fars.sort()
for fp in fars:
    for nm, doff, dlen in far_members(fp):
        low = nm.lower()
        if low.endswith('.xa') and 'birdy' in low:
            print('BIRDY', nm, 'in', os.path.basename(fp))
            break
# also count .xa per far to see which far has 'Park' path
from collections import Counter
counts = Counter()
for fp in fars:
    for nm, doff, dlen in far_members(fp):
        if nm.lower().endswith('.xa'):
            counts[os.path.basename(fp)] += 1
for k, v in counts.most_common(8):
    print(v, k)
