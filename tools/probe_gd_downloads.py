import struct, os
def globs(p):
    dat = open(p,'rb').read()
    out = []
    pos = 64
    while pos + 76 <= len(dat):
        ct = dat[pos:pos+4].decode('latin1','replace')
        sz = struct.unpack_from('>I', dat, pos+4)[0]
        cid = struct.unpack_from('>H', dat, pos+8)[0]
        paysz = sz - 76
        if paysz < 0: break
        if ct.upper() == 'GLOB': out.append(ct)
        pos += 76 + paysz
    return out
for root, dirs, files in os.walk('game-data/The Sims'):
    for fn in files:
        if fn.lower().endswith('.iff'):
            p = os.path.join(root, fn)
            g = globs(p)
            if g:
                print(os.path.relpath(p), len(g))
