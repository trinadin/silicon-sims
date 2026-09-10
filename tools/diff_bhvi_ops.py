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
def n_from(payload):
    if len(payload) < 4: return 0
    ver = struct.unpack_from('<H', payload, 0)[0]
    if ver in (0x8000, 0x8001, 0x8002):
        return struct.unpack_from('<H', payload, 2)[0]
    if ver == 0x8003:
        if len(payload) < 12: return 0
        return struct.unpack_from('<I', payload, 8)[0]
    return 0
per = {}
for fp in fars:
    data = open(fp,'rb').read()
    for nm, doff, dlen in far_members(fp):
        if not nm.lower().endswith('.iff'): continue
        dat = data[doff:doff+dlen]
        k = nm.replace('\\','/').rsplit('/',1)[-1].lower()
        c = 0
        seen = set()
        pos = 64
        while pos + 76 <= len(dat):
            ct = dat[pos:pos+4].decode('latin1','replace')
            sz = struct.unpack_from('>I', dat, pos+4)[0]
            cid = struct.unpack_from('>H', dat, pos+8)[0]
            paysz = sz - 76
            if paysz < 0: break
            if ct == 'BHAV':
                key = (ct, cid)
                if key not in seen:
                    seen.add(key)
                    c += n_from(dat[pos+76:pos+76+paysz])
            pos += 76 + paysz
        if c:
            per[k] = per.get(k, 0) + c
print('total (bhvi mirror)', sum(per.values()))
# now walk with per-member DISTINCT chunks only (last-wins on whole member, not summed twice)
per2 = {}
for fp in fars:
    data = open(fp,'rb').read()
    for nm, doff, dlen in far_members(fp):
        if not nm.lower().endswith('.iff'): continue
        dat = data[doff:doff+dlen]
        k = nm.replace('\\','/').rsplit('/',1)[-1].lower()
        c = 0
        seen = set()
        pos = 64
        while pos + 76 <= len(dat):
            ct = dat[pos:pos+4].decode('latin1','replace')
            sz = struct.unpack_from('>I', dat, pos+4)[0]
            cid = struct.unpack_from('>H', dat, pos+8)[0]
            paysz = sz - 76
            if paysz < 0: break
            if ct == 'BHAV':
                key = (ct, cid)
                if key not in seen:
                    seen.add(key)
                    c += n_from(dat[pos+76:pos+76+paysz])
            pos += 76 + paysz
        if c:
            per2[k] = c
print('total (member last-wins)', sum(per2.values()))
big = {k: abs(per.get(k, 0) - per2.get(k, 0)) for k in set(per) | set(per2)}
print('max member delta', max(big.values()) if big else 0)
for k, v in sorted(big.items(), key=lambda kv: -kv[1])[:5]:
    print('  delta', v, k, 'within-scan=', per.get(k, 0))
