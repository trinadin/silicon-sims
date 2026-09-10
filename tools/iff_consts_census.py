import struct, os
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
def blocks(ct, payload):
    if len(payload) < 2: return 0
    if ct.upper() == 'BCON':
        return payload[0]
    if ct.upper() == 'TPRP':
        if len(payload) < 20: return 0
        p = struct.unpack_from('<i', payload, 12)[0]
        l = struct.unpack_from('<i', payload, 16)[0]
        return max(0, p) + max(0, l)
    return 1
best = {}
for fp in fars:
    data = open(fp,'rb').read()
    for nm, doff, dlen in far_members(fp):
        if not nm.lower().endswith('.iff'): continue
        dat = data[doff:doff+dlen]
        k = nm.replace('\\','/').rsplit('/',1)[-1].lower()
        c = [0,0,0,0,0]
        seen = set()
        pos = 64
        while pos + 76 <= len(dat):
            ct = dat[pos:pos+4].decode('latin1','replace')
            cu = ct.upper()
            sz = struct.unpack_from('>I', dat, pos+4)[0]
            cid = struct.unpack_from('>H', dat, pos+8)[0]
            paysz = sz - 76
            if paysz < 0: break
            if cu in ('BCON','TPRP','GLOB'):
                if cu == 'GLOB':
                    c[4] += 1
                else:
                    key = (cu, cid)
                    if key not in seen:
                        seen.add(key)
                        payload = dat[pos+76:pos+76+paysz]
                        if cu == 'BCON': c[0] += 1; c[1] += blocks(ct, payload)
                        else: c[2] += 1; c[3] += blocks(ct, payload)
            pos += 76 + paysz
        if c[0] or c[2] or c[4]:
            best[k] = c
bc = sum(v[0] for v in best.values()); bb = sum(v[1] for v in best.values())
tc = sum(v[2] for v in best.values()); tb = sum(v[3] for v in best.values())
gc = sum(v[4] for v in best.values())
print('BCON chunks', bc, 'blocks', bb, '| TPRP chunks', tc, 'blocks', tb, '| GLOB chunks', gc, '| members', len(best))
lines = ['// Round 59 IFF-LITERAL constant-table census: tools/iff_consts_census.py over game-data/The Sims.',
         '// BCON = engine BCON.Read blocks (num bytes; distinct ids), TPRP = pCount+lCount (distinct ids),',
         '// GLOB = INSTANCE count (both "GLOB" and lowercase "glob" - engine IffFile registers both and',
         '// List counts instances, e.g. NPC_Superstar_PA/NPC_Vacation_Director carry GLOB+glob id 128).',
         '// Basename LAST-wins; IFF data, engine-independent.',
         'public const int BconChunkCensus = %d;' % bc,
         'public const int BconBlockCensus = %d;' % bb,
         'public const int TprpChunkCensus = %d;' % tc,
         'public const int TprpBlockCensus = %d;' % tb,
         'public const int GlobChunkCensus = %d;' % gc]
open('tools/iff-dump/r59-const-census.cs','w').write('\n'.join(lines) + '\n')
print('canon written tools/iff-dump/r59-const-census.cs')
