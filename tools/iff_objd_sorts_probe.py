
import struct, collections, math, os

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
        lbl = body[pos+12:pos+76].split(bytes([0]))[0].decode('latin1','replace').strip()
        paysz = sz - 76
        payload = body[pos+76:pos+76+paysz] if paysz > 0 else b''
        if ct == 'OBJD' and len(payload) >= 4 + 68*2:
            ver = struct.unpack_from('<I', payload, 0)[0]
            fld = struct.unpack_from('<%dH' % ((len(payload)-4)//2), payload, 4)
            out.append((cid, lbl, ver, fld))
        pos += 76 + paysz
    return out

def num_fields(ver):
    # Round 47: v138 corpus carries 106 fields (IFF-literal mirror of corrected FSO OBJD.Read).
    if ver == 138: return 106
    if ver == 139: return 96
    if ver == 140: return 97
    if ver == 141: return 97
    if ver == 142: return 105
    return 80

def sorts(fld, ver):
    nf = num_fields(ver)
    fs = fld[92] if len(fld) > 92 and nf > 92 else 0
    dt = fld[93] if len(fld) > 93 and nf > 93 else 0
    kk = fld[94] if len(fld) > 94 and nf > 94 else 0
    va = fld[95] if len(fld) > 95 and nf > 95 else 0
    co = fld[97] if len(fld) > 97 and nf > 95 else 0
    st = fld[101] if len(fld) > 101 and nf > 95 else 0
    mt = fld[102] if len(fld) > 102 and nf > 95 else 0
    return (fld[37] & 0xFF, fs, dt, va, co, st, mt, kk)

fars = []
for dp, dns, fns in os.walk(ROOT):
    for fn in fns:
        if fn.lower().endswith('.far'):
            fars.append(os.path.join(dp, fn))
fars.sort()

eligible = []
versions = collections.Counter()
roomhist = collections.Counter()
subhist = collections.Counter()
nonzero_subsort = collections.Counter()
total_objd = 0
for fp in fars:
    mem = far_members(fp)
    for key, (nm, body) in mem.items():
        if not nm.lower().endswith('.iff'): continue
        if 'globals' in nm.lower(): continue
        for (cid, lbl, ver, fld) in decode_objds(body):
            total_objd += 1
            guid = (fld[13] << 16) | fld[12]
            price = fld[16]; ff = fld[38]; bmt = fld[67]; dis = fld[14]
            mid = fld[8]; si = s16(fld[9]); ng = fld[2]
            if not passes(ff, bmt, dis, mid, si, ng): continue
            versions[ver] += 1
            rs, fs, dt, va, co, stw, mt, kk = sorts(fld, ver)
            roomhist[rs] += 1
            for lab, v in (('Subsort',fs),('DT',dt),('Vac',va),('Comm',co),('ST',stw),('MT',mt),('KeepBuy',kk)):
                if v != 0: nonzero_subsort[lab] += 1
            eligible.append(guid)

print('total OBJDs:', total_objd)
print('eligible:', len(eligible))
print('version histogram (eligible):', dict(sorted(versions.items())))
print('RoomSort(=RoomFlags low byte) value histogram:', dict(roomhist.most_common()))
print('eligible with RoomFlags low byte != 0:', sum(1 for v in roomhist.elements() if v != 0) if False else len(eligible) - roomhist[0])
print('nonzero subsort counts (IFF-literal gated read):', dict(nonzero_subsort.most_common()))
