import struct, os
def chunks_with_glob(path):
    try:
        dat = open(path,'rb').read()
    except Exception:
        return []
    out = []
    pos = 64
    while pos + 76 <= len(dat):
        ct = dat[pos:pos+4].decode('latin1','replace')
        sz = struct.unpack_from('>I', dat, pos+4)[0]
        cid = struct.unpack_from('>H', dat, pos+8)[0]
        paysz = sz - 76
        if paysz < 0: break
        if ct.upper() == 'GLOB':
            out.append((ct, cid))
        pos += 76 + paysz
    return out
found = []
for root, dirs, files in os.walk('/Users/nathannoom/Documents/Simitone/UserData'):
    for fn in files:
        if fn.lower().endswith('.iff'):
            p = os.path.join(root, fn)
            c = chunks_with_glob(p)
            if c:
                found.append((p, c))
print('user iff with GLOB/glob:', len(found))
for p, c in found[:8]: print('  ', p, c)
