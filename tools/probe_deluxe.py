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
for fp in fars:
    data = open(fp,'rb').read()
    for nm, doff, dlen in far_members(fp):
        if nm.lower() != 'bedssingledeluxe.iff': continue
        dat = data[doff:doff+dlen]
        print('MEMBER', nm, 'len', len(dat), 'header', dat[0:4])
        pos = 64; i = 0
        while pos + 76 <= len(dat):
            ct = dat[pos:pos+4].decode('latin1','replace')
            sz = struct.unpack_from('>I', dat, pos+4)[0]
            cid = struct.unpack_from('>H', dat, pos+8)[0]
            paysz = sz - 76
            if paysz < 0: break
            if ct in ('STR#','TTAs','CTSS'):
                print('   ', i, ct, 'id', cid, 'paysz', paysz, 'fmt', struct.unpack_from('<h', dat, pos+76)[0])
            i += 1
            pos += 76 + paysz
        print('total chunks', i)
        break
    else:
        continue
    break
