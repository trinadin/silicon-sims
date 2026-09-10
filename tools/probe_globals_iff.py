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
for want in ('Personglobals.iff', 'Catglobals.iff'):
    print('==', want)
    for fp in fars:
        for nm, doff, dlen in far_members(fp):
            if nm.lower() == want.lower():
                print('  in', os.path.basename(fp))
    print('  scan .iff members with basename globals-ish')
    for fp in fars:
        for nm, doff, dlen in far_members(fp):
            b = nm.replace('\\','/').rsplit('/',1)[-1].lower()
            if b.endswith('.iff') and ('globals' in b):
                print('   GLOB', b)
                break
        else:
            continue
        break
