import struct, os
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
        if dlen2==dlen and 0<doff<len(data) and dlen<len(data): out.append(nm)
    return out
print('total far files:', len(fars))
for fp in fars:
    lowbase = os.path.basename(fp).lower()
    if 'sound' in lowbase or 'deluxe' in lowbase or lowbase == 'expansionpack.far':
        ms = members(fp)
        birdy = [m for m in ms if 'birdy' in m.lower()]
        xa = [m for m in ms if m.lower().endswith('.xa')]
        print(len(ms), 'members |', len(xa), 'xa |', len(birdy), 'birdy |', fp)
