import struct, os
# iff_bhav_census.py - Round 60 IFF BHAV census. EXACT mirror of the engine BHAV.Read: instruction
# count comes from the IFF chunk itself (0x8000/0x8001: u16 LE at payload+2; 0x8002: u16 LE at payload+2
# with Type/Args/Locals/Version headers; 0x8003: u32 LE at payload+8). Per member basename LAST-wins;
# chunk sets distinct by (type,id) (engine ByChunkId[type]). IFF data - the BHAV instructions the
# behavior VM executes are IFF-literally IFF data.
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
def instr_count(payload):
    if len(payload) < 4: return 0
    ver = struct.unpack_from('<H', payload, 0)[0]
    if ver in (0x8000, 0x8001, 0x8002):
        return struct.unpack_from('<H', payload, 2)[0]
    if ver == 0x8003:
        if len(payload) < 12: return 0
        return struct.unpack_from('<I', payload, 8)[0]
    return 0
best = {}
memb = 0
for fp in fars:
    data = open(fp,'rb').read()
    for nm, doff, dlen in far_members(fp):
        if not nm.lower().endswith('.iff'): continue
        dat = data[doff:doff+dlen]
        k = nm.replace('\\','/').rsplit('/',1)[-1].lower()
        c = 0
        seen = set()
        pos = 64
        while pos + 76 <= len(dat):
            ct = dat[pos:pos+4].decode('latin1','replace')
            sz = struct.unpack_from('>I', dat, pos+4)[0]
            cid = struct.unpack_from('>H', dat, pos+8)[0]
            paysz = sz - 76
            if paysz < 0: break
            if ct == 'BHAV':
                key = (ct, cid)
                if key not in seen:
                    seen.add(key)
                    c += instr_count(dat[pos+76:pos+76+paysz])
            pos += 76 + paysz
        if c:
            best[k] = c
            memb += 1
tot = sum(best.values())
print('BHAV members', memb, 'instruction blocks', tot)
with open('tools/iff-dump/r60-bhav-census.txt','w') as g:
    g.write('BHAV basename-keyed members=%d instructions=%d\n' % (memb, tot))
lines = ['// Round 60 IFF-LITERAL BHAV census: tools/iff_bhav_census.py over game-data/The Sims.',
         '// BHAV instruction count from the IFF chunk header (engine BHAV.Read mirror: 0x8000/0x8001',
         '// u16 at payload+2; 0x8002 u16 at payload+2; 0x8003 u32 at payload+8). Basename LAST-wins;',
         '// distinct (type,id). The instructions the behavior VM executes are IFF-literally IFF data.',
         'public const int BhavInstructionCensus = %d;' % tot]
open('tools/iff-dump/r60-bhav-census.cs','w').write('\n'.join(lines) + '\n')
print('canon written tools/iff-dump/r60-bhav-census.cs')
