
import struct, os
ROOT = 'game-data/The Sims'

def s16(v):
    return v - 0x10000 if v >= 0x8000 else v

def passes(ff, bmt, dis, mid, si, ng):
    if not (ff > 0 or bmt > 0): return False
    if dis != 0: return False
    if not (mid != 0 or ng > 0): return False
    if not (mid == 0 or si == -1): return False
    return True

def far_members(path):
    data = open(path,'rb').read()
    if data[0:8] != b'FAR!byAZ': return {}
    man = struct.unpack('<I', data[12:16])[0]
    num = struct.unpack('<I', data[man:man+4])[0]
    out = {}
    off = man + 4
    for i in range(num):
        dlen, dlen2, doff = struct.unpack('<III', data[off:off+12])
        nlen = struct.unpack('<H', data[off+12:off+14])[0]
        nm = data[off+16:off+16+nlen].decode('latin1','replace')
        off += 16 + nlen
        if dlen2 == dlen and 0 < doff < len(data) and dlen < len(data):
            out[nm.upper()] = (nm, data[doff:doff+dlen])
    return out

def decode_objds(body):
    out = []
    pos = 64
    while pos + 76 <= len(body):
        ct = body[pos:pos+4].decode('latin1','replace')
        sz = struct.unpack_from('>I', body, pos+4)[0]
        cid = struct.unpack_from('>H', body, pos+8)[0]
        lbl = body[pos+12:pos+76]
        lbln = ''.join(chr(b) if b < 0x80 else '?' for b in lbl).rstrip('\0')
        paysz = sz - 76
        payload = body[pos+76:pos+76+paysz] if paysz > 0 else b''
        if ct == 'OBJD' and len(payload) >= 4 + 4:
            ver = struct.unpack_from('<I', payload, 0)[0]
            fld = struct.unpack_from('<%dH' % ((len(payload)-4)//2), payload, 4)
            out.append((cid, lbln, fld))
        pos += 76 + paysz
    return out

fars = []
for dp, dns, fns in os.walk(ROOT):
    for fn in fns:
        if fn.lower().endswith('.far'): fars.append(os.path.join(dp, fn))
fars.sort()

high = 0
high_eg = []
names = {}
for fp in fars:
    mem = far_members(fp)
    for key, (nm, body) in mem.items():
        if not nm.lower().endswith('.iff'): continue
        if 'globals' in nm.lower(): continue
        for (cid, lbln, fld) in decode_objds(body):
            guid = (fld[13] << 16) | fld[12]
            ff = fld[38]; bmt = fld[67]; dis = fld[14]
            mid = fld[8]; si = s16(fld[9]); ng = fld[2]
            if not passes(ff, bmt, dis, mid, si, ng): continue
            if any(ord(c) > 127 for c in lbln):
                high += 1
                if len(high_eg) < 5: high_eg.append((guid, lbln))
            names[guid] = lbln

print('eligible with high-byte label:', high, 'of', len(names))
for eg in high_eg: print('  high eg:', hex(eg[0]), repr(eg[1]))
for k in list(names.items())[:6]:
    print('  sample:', hex(k[0]), repr(k[1]))
print('  known 0xfdef7fc2:', repr(names.get(0xfdef7fc2)))
print('  known 0x9fb223ce:', repr(names.get(0x9fb223ce)))
