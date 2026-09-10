
import struct, os, collections
ROOT = 'game-data/The Sims'

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
        lbl = body[pos+12:pos+76].split(bytes([0]))[0].decode('latin1','replace').strip()
        paysz = sz - 76
        payload = body[pos+76:pos+76+paysz] if paysz > 0 else b''
        if ct == 'OBJD' and len(payload) >= 4 + 68*2:
            ver = struct.unpack_from('<I', payload, 0)[0]
            fld = struct.unpack_from('<%dH' % ((len(payload)-4)//2), payload, 4)
            out.append((cid, lbl, ver, fld, payload))
        pos += 76 + paysz
    return out

fars = []
for dp, dns, fns in os.walk(ROOT):
    for fn in fns:
        if fn.lower().endswith('.far'):
            fars.append(os.path.join(dp, fn))
fars.sort()

ver_fields = collections.Counter()
ver_missing93 = collections.Counter()
bed = None
count_dt_nonzero = 0
for fp in fars:
    mem = far_members(fp)
    for key, (nm, body) in mem.items():
        if not nm.lower().endswith('.iff'): continue
        if 'globals' in nm.lower(): continue
        for (cid, lbl, ver, fld, payload) in decode_objds(body):
            ver_fields[(ver, len(fld))] += 1
            fieldlen = (len(payload)-4)//2
            if fieldlen < 94: ver_missing93[ver] += 1
            guid = (fld[13] << 16) | fld[12]
            if len(fld) > 93 and (fld[93] & 0xFF) != 0:
                count_dt_nonzero += 1
            if guid == 0xfdef7fc2:
                bed = (nm, cid, lbl, ver, len(fld), [hex(x) for x in fld[89:99]], payload[4+92*2:4+100*2].hex())

print('OBJD version x field-length histogram:')
for (ver, fl), c in sorted(ver_fields.items()):
    print('  ver=%d len=%d count=%d %s' % (ver, fl, c, '<-- v138' if ver==138 else ''))
print('OBJDs with payload shorter than 94 fields by version:', dict(ver_missing93))
print('OBJDs with field 93 present and low byte nonzero:', count_dt_nonzero)
if bed:
    print('Beds 0xfdef7fc2:', bed)
