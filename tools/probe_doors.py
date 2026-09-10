import struct, os
def far_members(path):
    data = open(path,'rb').read()
    if data[0:8] != b'FAR!byAZ': return []
    man = struct.unpack('<I', data[12:16])[0]
    num = struct.unpack('<I', data[man:man+4])[0]
    out = []; o = man+4
    for i in range(num):
        dlen,dlen2,doff = struct.unpack('<III', data[o:o+12])
        nlen = struct.unpack('<H', data[o+12:o+14])[0]
        nm = data[o+16:o+16+nlen].decode('latin1','replace')
        o += 16+nlen
        if dlen2==dlen and 0<doff<len(data) and dlen<len(data): out.append((nm, doff, dlen))
    return out
ROOT = 'game-data/The Sims'
fars = sorted([os.path.join(dp, fn) for dp, dns, fns in os.walk(ROOT) for fn in fns if fn.lower().endswith('.far')])
for fp in fars:
    data = open(fp,'rb').read()
    for nm, doff, dlen in far_members(fp):
        if 'doorsmagic.iff' == nm.lower().replace('\\','/').rsplit('/',1)[-1] or 'doorssuperstar.iff' == nm.lower().replace('\\','/').rsplit('/',1)[-1]:
            seg = data[doff:doff+dlen]
            print(fp.rsplit('/',2)[-2:], nm, 'dlen', dlen)
            print('  id60:', seg[:60])
            print('  id-text:', seg[0:56].decode('latin1','replace'))
            print('  pos60-64:', seg[60:64])
            print('  first chunk ct:', seg[64:68], 'sz:', struct.unpack_from('>I', seg, 68)[0])
