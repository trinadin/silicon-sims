import struct, os, collections
# iff_operand_byopcode.py - Round 66 IFF BHAV per-opcode operand-byte census. Exact mirror of
# engine BHAV.Read (identical to tools/iff_bhav_operands.py Round 64): version/count LITTLE-ENDIAN,
# 0x8000/0x8001/0x8002 count=u16@2, 0x8003 count=u32@9; instructions of 12 bytes at body=12; operand
# seg = payload[off+4:off+12] (ReadBytes(8) truncates at stream end). Per member basename LAST-wins;
# within member chunks distinct by (type,id). Matrix: for each opcode, histogram over 256 operand
# byte values. IFF data - which operand bytes ride with which opcodes in the VM input surface.
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
        if off + 12 > len(payload): break
        op = struct.unpack_from('<H', payload, off)[0]
        seg = payload[off+4:off+12]
        ops.append((op, seg))
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
        best[k] = mem_ops
mat = collections.Counter()
opcodes = set()
for ops in best.values():
    for op, seg in ops:
        opcodes.add(op)
        for by in seg:
            mat[(op, by)] += 1
print('distinct opcodes:', len(opcodes))
print('nonzero pairs:', len(mat))
print('total operand bytes:', sum(mat.values()))
top = sorted(mat.items(), key=lambda x: -x[1])[:12]
for (op, ob), c in top: print('  op=%x byte=%d count=%d' % (op, ob, c))
open('tools/iff-dump/r66-operand-byopcode.txt','w').write('opcodes %d pairs %d total %d\n' % (len(opcodes), len(mat), sum(mat.values())) + '\n'.join('%x:%d=%d' % (op, ob, c) for (op, ob), c in sorted(mat.items())) + '\n')
print('written tools/iff-dump/r66-operand-byopcode.txt')
