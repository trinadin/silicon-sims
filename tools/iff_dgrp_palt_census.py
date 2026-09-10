import struct, os
# iff_dgrp_palt_census.py - Round 62 IFF DGRP/PALT census. Exact mirror of engine DGRP.Read and
# PALT.Read; per member basename LAST-wins; within each member chunks distinct by (type,id) and each
# such chunk counts as ONE INSTANCE (engine IffFile.List<DGRP>/List<PALT> counts chunks, not members).
# DGRP chunk instance -> sprite refs (imageCount u16 <20003 else u32, spriteCount u16 <20003 else u32,
# sprite bodies walked with exact per-version sizes); PALT chunk instance -> numEntries (u32 at +4).
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
def sprite_size(ver):
    if ver == 20001: return 16
    if ver == 20002: return 12
    if ver == 20003: return 24
    if ver == 20004: return 32
    if ver == 20000: return 12  # default branch (like 20002)
    return 12
def dgrp_refs(payload):
    if len(payload) < 4: return 0
    ver = struct.unpack_from('<H', payload, 0)[0]
    if ver < 20003:
        if len(payload) < 6: return 0
        imgs = struct.unpack_from('<H', payload, 2)[0]
        pos = 4
        refs = 0
        for i in range(imgs):
            if pos + 4 > len(payload): break
            sc = struct.unpack_from('<H', payload, pos)[0]
            pos += 4
            refs += sc
            pos += sc * sprite_size(ver)
        return refs
    else:
        if len(payload) < 8: return 0
        imgs = struct.unpack_from('<I', payload, 2)[0]
        pos = 6
        refs = 0
        for i in range(imgs):
            if pos + 12 > len(payload): break
            sc = struct.unpack_from('<I', payload, pos + 8)[0]
            pos += 12
            refs += sc
            pos += sc * sprite_size(ver)
        return refs
def palt_entries(payload):
    if len(payload) < 16: return 0
    return struct.unpack_from('<I', payload, 4)[0]
best = {}  # basename -> (dgrp_count, dgrp_refs, palt_count, palt_entries)
for fp in fars:
    data = open(fp,'rb').read()
    for nm, doff, dlen in far_members(fp):
        if not nm.lower().endswith('.iff'): continue
        k = nm.replace('\\','/').rsplit('/',1)[-1].lower()
        dat = data[doff:doff+dlen]
        pos = 64
        seen = set()
        dgc = dgr = pc = pe = 0
        while pos + 76 <= len(dat):
            ct = dat[pos:pos+4].decode('latin1','replace')
            sz = struct.unpack_from('>I', dat, pos+4)[0]
            cid = struct.unpack_from('>H', dat, pos+8)[0]
            paysz = sz - 76
            if paysz < 0: break
            key = (ct, cid)
            if key not in seen:
                seen.add(key)
                if ct == 'DGRP':
                    r = dgrp_refs(dat[pos+76:pos+76+paysz])
                    if r > 0:
                        dgc += 1; dgr += r
                elif ct == 'PALT':
                    e = palt_entries(dat[pos+76:pos+76+paysz])
                    if e > 0:
                        pc += 1; pe += e
            pos += 76 + paysz
        if dgc or pc:
            best[k] = (dgc, dgr, pc, pe)  # LAST-wins per basename
DGC = sum(v[0] for v in best.values()); DGR = sum(v[1] for v in best.values())
PC = sum(v[2] for v in best.values()); PE = sum(v[3] for v in best.values())
print('DGRP chunk instances', DGC, 'refs', DGR)
print('PALT chunk instances', PC, 'entries', PE)
open('tools/iff-dump/r62-dgrp-palt.txt','w').write('DGRP chunks=%d refs=%d PALT chunks=%d entries=%d\n' % (DGC, DGR, PC, PE))
lines = ['// Round 62 IFF-LITERAL DGRP/PALT census: tools/iff_dgrp_palt_census.py over game-data/The Sims.',
         '// DGRP/PALT chunk INSTANCES (engine IffFile.List<DGRP>/List<PALT> counts chunks, not members);',
         '// per member basename LAST-wins; within member distinct (type,id); refs/entries from the',
         '// engine DGRP.Read (20004+) / PALT.Read mirrors. IFF data - the sprite linkage and palette',
         '// entries the renderer consumes.',
         'public const int DgrpChunkCensus = %d;' % DGC,
         'public const int DgrpSpriteRefCensus = %d;' % DGR,
         'public const int PaltChunkCensus = %d;' % PC,
         'public const int PaltEntryCensus = %d;' % PE]
open('tools/iff-dump/r62-dgrp-palt-census.cs','w').write('\n'.join(lines) + '\n')
print('canon written tools/iff-dump/r62-dgrp-palt-census.cs')
