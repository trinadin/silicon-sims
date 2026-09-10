import struct, os
ROOT = 'game-data/The Sims'
def s16(v): return v - 0x10000 if v >= 0x8000 else v
def passes(ff, bmt, dis, mid, si, ng):
    return ((ff>0 or bmt>0) and dis==0 and (mid!=0 or ng>0) and (mid==0 or si==-1))
def cat(ff, bmt):
    import math
    return int(math.log(ff,2)) if ff>0 else bmt+7
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
        if dlen2==dlen and 0<doff<len(data) and dlen<len(data): out.append((nm, data[doff:doff+dlen]))
    return out
def decode_objds(body):
    out = []; pos = 64
    while pos+76 <= len(body):
        ct = body[pos:pos+4].decode('latin1','replace')
        sz = struct.unpack_from('>I', body, pos+4)[0]
        paysz = sz-76
        payload = body[pos+76:pos+76+paysz] if paysz>0 else b''
        if ct=='OBJD' and len(payload)>=4+68*2:
            fld = struct.unpack_from('<%dH'%((len(payload)-4)//2), payload, 4)
            out.append(fld)
        pos += 76+paysz
    return out
fars=[]
for dp,dns,fns in os.walk(ROOT):
    for fn in fns:
        if fn.lower().endswith('.far'): fars.append(os.path.join(dp,fn))
fars.sort()
seen = {}
order = []
for fp in fars:
    for (nm, body) in far_members(fp):
        if not nm.lower().endswith('.iff') or 'globals' in nm.lower(): continue
        for fld in decode_objds(body):
            guid = (fld[13]<<16)|fld[12]
            ff=fld[38]; bmt=fld[67]; dis=fld[14]; mid=fld[8]; si=s16(fld[9]); ng=fld[2]
            if passes(ff,bmt,dis,mid,si,ng):
                seen.setdefault(guid, []).append(nm)
                order.append((cat(ff,bmt), guid, nm))
print('dup guids:', {hex(g): v for g,v in seen.items() if len(v)>1})
idx = {g:i for i,(c,g,m) in enumerate(order)}
for g in (0x7c21272e, 0x0fe5fa64, 0x2e9fdca5, 0x2b49a3f4, 0x6bf6d402, 0x61b7982e, 0x431d46c8, 0x4bd0b785, 0x2be7cbba, 0x0d94642f, 0xd4588282, 0xe60e754c):
    print(hex(g), 'iff-pos=', idx.get(g), 'cat=', next((c for c,gg,m in order if gg==g), None))
print('first 6 els:', [(hex(g), hex(c)) for (c,g,m) in order[:6]])
print('total elements (with dups):', len(order), 'distinct:', len(seen))
