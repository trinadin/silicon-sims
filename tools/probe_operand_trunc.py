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
fars = sorted([os.path.join(dp, fn) for dp, dns, fns in os.walk(ROOT) for fn in fns if fn.lower().endswith('.far')])
trunc = 0; truncb = 0; chunks = 0; instr = 0
samp = []
for fp in fars:
    data = open(fp,'rb').read()
    for nm, doff, dlen in far_members(fp):
        if not nm.lower().endswith('.iff'): continue
        dat = data[doff:doff+dlen]
        pos = 64
        seen = set()
        while pos + 76 <= len(dat):
            ct = dat[pos:pos+4].decode('latin1','replace')
            sz = struct.unpack_from('>I', dat, pos+4)[0]
            cid = struct.unpack_from('>H', dat, pos+8)[0]
            paysz = sz - 76
            if paysz < 0: break
            key = (ct, cid)
            if key not in seen:
                seen.add(key)
                if ct == 'BHAV':
                    payload = dat[pos+76:pos+76+paysz]
                    if len(payload) < 4: continue
                    ver = struct.unpack_from('<H', payload, 0)[0]
                    n = 0; body = 12
                    if ver in (0x8000, 0x8001, 0x8002): n = struct.unpack_from('<H', payload, 2)[0]
                    elif ver == 0x8003:
                        if len(payload) < 11: continue
                        n = struct.unpack_from('<I', payload, 9)[0]
                    else: continue
                    chunks += 1
                    for i in range(n):
                        seg = payload[body+12*i+4:body+12*i+12]
                        if len(seg) < 8:
                            trunc += 1; truncb += 8 - len(seg)
                            if len(samp) < 5: samp.append((nm.split('\\')[-1].split('/')[-1], cid, len(seg)))
            pos += 76 + paysz
print('chunks', chunks, 'truncated-instrs', trunc, 'missing bytes', truncb)
for s in samp: print('  ', s)
print('missing%8?', truncb % 8)
