import struct, os
# iff_bhav_operands.py - Round 64 IFF BHAV OPERAND census. Exact mirror of engine BHAV.Read: count
# from chunk header (0x8000/0x8001/0x8002: u16 at payload+2; 0x8003: u32 at payload+9), instructions
# of 12 bytes at body=12. Per member basename LAST-wins (engine BuildDictionary mount); within member
# chunks distinct by (type,id). Operand seg = payload[off+4:off+12] (engine ReadBytes(8) truncates
# at stream end). Histogram over 256 byte values = IFF data - the literal operand bytes the behavior
# VM reads per instruction.
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
def walk(payload):
    if len(payload) < 4: return []
    ver = struct.unpack_from('<H', payload, 0)[0]
    n = 0; body = 12
    if ver in (0x8000, 0x8001, 0x8002):
        n = struct.unpack_from('<H', payload, 2)[0]
    elif ver == 0x8003:
        if len(payload) < 11: return []
        n = struct.unpack_from('<I', payload, 9)[0]
    else:
        return []
    ops = []
    for i in range(n):
        off = body + 12 * i
        seg = payload[off+4:off+12]
        ops.append(seg)
    return ops
best = {}
for fp in fars:
    data = open(fp,'rb').read()
    for nm, doff, dlen in far_members(fp):
        if not nm.lower().endswith('.iff'): continue
        k = nm.replace('\\','/').rsplit('/',1)[-1].lower()
        dat = data[doff:doff+dlen]
        pos = 64
        seen = set()
        mem_ops = []
        while pos + 76 <= len(dat):
            ct = dat[pos:pos+4].decode('latin1','replace')
            sz = struct.unpack_from('>I', dat, pos+4)[0]
            cid = struct.unpack_from('>H', dat, pos+8)[0]
            paysz = sz - 76
            if paysz < 0: break
            key = (ct, cid)
            if key not in seen:
                seen.add(key)
                if ct == 'BHAV':
                    mem_ops.extend(walk(dat[pos+76:pos+76+paysz]))
            pos += 76 + paysz
        best[k] = mem_ops  # LAST-wins per basename
hist = [0]*256
for ops in best.values():
    for seg in ops:
        for by in seg:
            hist[by] += 1
tot = sum(hist)
print('operand bytes', tot, 'distinct', sum(1 for h in hist if h))
lines = ['// Round 64 IFF-LITERAL BHAV operand-byte census: tools/iff_bhav_operands.py over game-data/The',
         '// Sims. Per member basename LAST-wins (engine BuildDictionary mount; shadowed duplicate members',
         '// excluded); within member chunks distinct by (type,id). Engine BHAV.Read mirror: 8-byte',
         '// operand per instruction (ReadBytes(8) truncates at stream end). Histogram over 256 byte',
         '// values = IFF data - the literal operand bytes the behavior VM reads per instruction.',
         'public static readonly int[] BhavOperandByteCensus = new int[] { %s };' % ', '.join(str(h) for h in hist)]
open('tools/iff-dump/r64-bhav-operands.cs','w').write('\n'.join(lines) + '\n')
open('tools/iff-dump/r64-bhav-operands.txt','w').write('operand bytes=%d distinct=%d\n' % (tot, sum(1 for h in hist if h)))
print('canon written tools/iff-dump/r64-bhav-operands.cs')
