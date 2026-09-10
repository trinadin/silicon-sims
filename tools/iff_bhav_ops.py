import struct, os
# iff_bhav_ops.py - Round 61 IFF BHAV opcode census.
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
def member_hist(dat):
    h = {}
    pos = 64
    seen = set()
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
                payload = dat[pos+76:pos+76+paysz]
                if len(payload) < 4: continue
                ver = struct.unpack_from('<H', payload, 0)[0]
                if ver in (0x8000, 0x8001, 0x8002):
                    n = struct.unpack_from('<H', payload, 2)[0]
                elif ver == 0x8003:
                    if len(payload) < 13: continue
                    n = struct.unpack_from('<I', payload, 9)[0]
                else: continue
                for i in range(n):
                    off = 12 + 12 * i
                    if off + 2 <= len(payload):
                        op = struct.unpack_from('<H', payload, off)[0]
                        h[op] = h.get(op, 0) + 1
        pos += 76 + paysz
    return h
best = {}
for fp in fars:
    data = open(fp,'rb').read()
    for nm, doff, dlen in far_members(fp):
        if not nm.lower().endswith('.iff'): continue
        k = nm.replace('\\','/').rsplit('/',1)[-1].lower()
        h = member_hist(data[doff:doff+dlen])
        if h: best[k] = h
hist = {}
for h in best.values():
    for op, c in h.items():
        hist[op] = hist.get(op, 0) + c
tot = sum(hist.values())
flat = []
for op, c in sorted(hist.items()):
    flat.append('%d' % op); flat.append('%d' % c)
lines2 = []
cur = ''
for tok in flat:
    if cur and len(cur) + len(tok) + 2 > 95:
        lines2.append(cur.rstrip(', '))
        cur = ''
    cur += tok + ', '
if cur: lines2.append(cur.rstrip(', '))
out = ['// Round 61 IFF-LITERAL BHAV opcode census: tools/iff_bhav_ops.py over game-data/The Sims.',
       '// Opcode histogram of the IFF BHAV instruction stream; per member basename LAST-wins; chunks',
       '// distinct by (type,id); BHAV.Read mirror (0x8002 u16 at payload+2, 12-byte instructions).',
       'public const int BhavOpcodeDistinctCensus = %d;' % len(hist),
       'public static readonly int[] BhavOpcodePairCensus = new int[] {']
for x in lines2:
    out.append('' + x + ',')
out.append('};')
open('tools/iff-dump/r61-bhav-ops-census.cs','w').write('\n'.join(out) + '\n')
print('distinct opcodes', len(hist), 'instructions', tot, '| pairs:', len(flat)//2)
