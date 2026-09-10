import struct, os
# Probe DGRP chunks in DoorsSuperstar.iff / DoorsMagic.iff with engine DGRP.Read semantics
# to find which chunk makes the engine's prepare() throw.
def far_members(path):
    data = open(path,'rb').read()
    if data[0:8] != b'FAR!byAZ': return []
    man = struct.unpack('<I', data[12:16])[0]
    num = struct.unpack('<I', data[man:man+4])[0]
    out = []; o = man+4
    for i in range(num):
        dlen,dlen2,doff = struct.unpack('<III', data[o:o+12])
        nlen = struct.unpack('<H', data[o+12:o+14])[0]
        nm = data[o+16:o+16+nlen].decode('latin1','replace')
        o += 16+nlen
        if dlen2==dlen and 0<doff<len(data) and dlen<len(data): out.append((nm, doff, dlen))
    return out
ROOT = 'game-data/The Sims'
fars = sorted([os.path.join(dp, fn) for dp, dns, fns in os.walk(ROOT) for fn in fns if fn.lower().endswith('.far')])
targets = {'doorssuperstar.iff', 'doorsmagic.iff'}
for fp in fars:
    data = open(fp,'rb').read()
    for nm, doff, dlen in far_members(fp):
        k = nm.replace('\\','/').rsplit('/',1)[-1].lower()
        if k not in targets: continue
        seg = data[doff:doff+dlen]
        pos = 64
        dgrps = []
        while pos + 76 <= len(seg):
            ct = seg[pos:pos+4].decode('latin1','replace')
            sz = struct.unpack_from('>I', seg, pos+4)[0]
            cid = struct.unpack_from('>H', seg, pos+8)[0]
            paysz = sz - 76
            if paysz < 0: break
            if ct == 'DGRP':
                dgrps.append((cid, sz, pos))
            pos += 76 + paysz
        print(k, 'total DGRP', len(dgrps))
        for cid, sz, pos in dgrps:
            body = seg[pos+76:pos+76+sz-76]
            if len(body) < 4:
                print('  cid', cid, 'sz', sz, 'body-short', len(body)); continue
            ver = struct.unpack_from('>H', body, 0)[0]
            print('  cid', cid, 'sz', sz, 'ver', ver, 'bodylen', len(body))
        print()
