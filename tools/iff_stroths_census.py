import struct, os
# iff_stroths_census.py - Round 58 IFF STR-family census. Per member basename LAST-wins; per-member chunk
# sets are DISTINCT by (chunk type, chunk id) - mirror of the engine IFF parser, which stores chunks in
# ByChunkId[type] (re-adding the same (type,id) drops the earlier row), so IFF patch overlays that
# duplicate a chunk id in one file are IFF-literally counted once. STR.Read mirror: fmt 0/-1/-2 all
# blocks; fmt -3 only language-code 0/1 items. IFF data, engine-independent.
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
def blocks(payload):
    if len(payload) < 4: return 0
    fmt = struct.unpack_from('<h', payload, 0)[0]
    if fmt in (0, -1, -2):
        return struct.unpack_from('<H', payload, 2)[0]
    if fmt == -3:
        n = struct.unpack_from('<H', payload, 2)[0]
        p = 4; b = 0
        for i in range(n):
            if p >= len(payload): break
            lang = payload[p]; p += 1
            e0 = payload.find(b'\x00', p); e1 = payload.find(b'\x00', e0 + 1) if e0 >= 0 else -1
            if e1 < 0: break
            p = e1 + 1
            if lang == 0 or lang == 1: b += 1
        return b
    return 0
best = {}  # basename-lower -> [str#c, str#b, ttas_c, ttas_b] distinct-by-id, LAST-wins per basename
for fp in fars:
    data = open(fp,'rb').read()
    for nm, doff, dlen in far_members(fp):
        if not nm.lower().endswith('.iff'): continue
        dat = data[doff:doff+dlen]
        k = nm.replace('\\','/').rsplit('/',1)[-1].lower()
        c = [0, 0, 0, 0]
        seen = set()
        pos = 64
        while pos + 76 <= len(dat):
            ct = dat[pos:pos+4].decode('latin1','replace')
            sz = struct.unpack_from('>I', dat, pos+4)[0]
            cid = struct.unpack_from('>H', dat, pos+8)[0]
            paysz = sz - 76
            if paysz < 0: break
            if ct in ('STR#', 'TTAs'):
                key = (ct, cid)
                if key in seen:
                    pass
                else:
                    seen.add(key)
                    if ct == 'STR#':
                        c[0] += 1; c[1] += blocks(dat[pos+76:pos+76+paysz])
                    else:
                        c[2] += 1; c[3] += blocks(dat[pos+76:pos+76+paysz])
            pos += 76 + paysz
        if c[0] or c[2]:
            best[k] = c
sh = sum(v[0] for v in best.values()); shb = sum(v[1] for v in best.values())
tt = sum(v[2] for v in best.values()); ttb = sum(v[3] for v in best.values())
print('STR# chunks', sh, 'blocks', shb, '| TTAs chunks', tt, 'blocks', ttb, '| members', len(best))
with open('tools/iff-dump/r58-str-census.txt','w') as g:
    g.write('distinct-by-id STR# chunks=%d blocks=%d TTAs chunks=%d blocks=%d members=%d\n' % (sh, shb, tt, ttb, len(best)))
lines = ['// Round 58 IFF-LITERAL STR-family census: tools/iff_stroths_census.py over game-data/The Sims.',
         '// Per member basename LAST-wins; chunk sets DISTINCT by (type,id) - engine IFF parser mirror',
         '// (ByChunkId holds one row per (type,id)); STR.Read mirror fmt 0/-1/-2 all, fmt -3 lang 0/1.',
         'public const int StrSharpBlockCensus = %d;' % shb,
         'public const int StrSharpChunkCensus = %d;' % sh,
         'public const int TTAsBlockCensus = %d;' % ttb,
         'public const int TTAsChunkCensus = %d;' % tt]
open('tools/iff-dump/r58-str-census.cs','w').write('\n'.join(lines) + '\n')
print('canon written tools/iff-dump/r58-str-census.cs')
