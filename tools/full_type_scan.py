
import os, struct, collections

def read_cstr(buf, off, n):
    raw = buf[off:off+n].replace(bytes([0]), b'')
    return raw.rstrip(b' 	'), off + n

def iff_types(buf):
    if len(buf) < 64: return {}
    ident, off = read_cstr(buf, 0, 60)
    if ident not in (b'IFF FILE 2.5:TYPE FOLLOWED BY SIZE JAMIE DOORNBOS & MAXIS 1', b'IFF FILE 2.0:TYPE FOLLOWED BY SIZE JAMIE DOORNBOS & MAXIS 1'):
        return {}
    rsmp = struct.unpack_from('>I', buf, off)[0]; off += 4
    types = collections.Counter()
    while len(buf) - off >= 2:
        if len(buf) - off < 8: break
        ct = buf[off:off+4].decode('latin1'); off += 4
        csize = struct.unpack_from('>I', buf, off)[0]; off += 4
        if len(buf) - off < 4: break
        cid = struct.unpack_from('>H', buf, off)[0]; off += 2
        cflags = struct.unpack_from('>H', buf, off)[0]; off += 2
        clabel, off = read_cstr(buf, off, 64)
        data_sz = csize - 76
        if data_sz < 0: break
        if off + data_sz > len(buf): break
        types[ct] += 1
        off += data_sz
    return types

def far_entries(path):
    data = open(path, 'rb').read()
    if not data[:4].startswith(b'FAR'): return []
    man = struct.unpack('<I', data[12:16])[0]
    num = struct.unpack('<I', data[man:man+4])[0]
    off = man + 4
    out = []
    for i in range(num):
        rec = data[off:off+14]
        if len(rec) < 14: break
        dlen = struct.unpack('<I', rec[0:4])[0]
        dlen2 = struct.unpack('<I', rec[4:8])[0]
        doff = struct.unpack('<I', rec[8:12])[0]
        nlen = struct.unpack('<H', rec[12:14])[0]
        nm = data[off+16:off+16+nlen].decode('latin1','replace')
        off += 16 + nlen
        if dlen2 == dlen and doff < len(data):
            out.append((nm, data[doff:doff+dlen]))
    return out

root = '/Users/nathannoom/Developer/The Sims/simitone-fork/game-data/The Sims/'
tot = collections.Counter()
files = []
for dirpath, _, names in os.walk(root):
    for n in names:
        if n.lower().endswith(('.iff','.package','.far')): files.append(os.path.join(dirpath, n))
print('files walked:', len(files))
for p in files:
    if p.lower().endswith('.far'):
        for nm, body in far_entries(p):
            for t, c in iff_types(body).items(): tot[t] += c
    else:
        body = open(p, 'rb').read()
        for t, c in iff_types(body).items(): tot[t] += c
print('--- ALL TYPES IN STAGED DATA ---')
for t, c in tot.most_common(85):
    print('%8d %s' % (c, t))
