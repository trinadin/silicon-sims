
import struct, collections
NL = chr(10)
path = 'game-data/The Sims/GameData/Global/Global.far'
data = open(path, 'rb').read()
man = struct.unpack('<I', data[12:16])[0]
num = struct.unpack('<I', data[man:man+4])[0]
off = man + 4
cats = collections.Counter()
global_ids = collections.Counter()
prim_ids = collections.Counter()
files_with = {}
for i in range(num):
    rec = data[off:off+14]
    dlen = struct.unpack('<I', rec[0:4])[0]
    dlen2 = struct.unpack('<I', rec[4:8])[0]
    doff = struct.unpack('<I', rec[8:12])[0]
    nlen = struct.unpack('<H', rec[12:14])[0]
    nm = data[off+16:off+16+nlen].decode('latin1','replace')
    off += 16 + nlen
    if nm.lower().endswith('.iff') and dlen2 == dlen and doff < len(data):
        body = data[doff:doff+dlen]
        c = collections.Counter(); g = collections.Counter(); p = collections.Counter()
        pos = 64
        while pos + 76 <= len(body):
            ct = body[pos:pos+4].decode('latin1','replace')
            sz = struct.unpack_from('>I', body, pos+4)[0] - 76
            pos += 76
            if ct == 'BHAV' and sz > 12 and pos + sz <= len(body):
                b = body[pos:pos+sz]
                ver = struct.unpack_from('<H', b, 0)[0]
                if ver == 0x8002 and len(b) >= 12:
                    cnt = struct.unpack_from('<H', b, 2)[0]
                    for k in range(cnt):
                        s = 12 + k*12
                        if s+12 > len(b): break
                        op = struct.unpack_from('<H', b, s)[0]
                        if op < 256: p[op] += 1
                        elif op < 4096: g[op] += 1
                        else: c[op] += 1
            pos += sz
        cats['prim'] += sum(p.values()); prim_ids.update(p)
        cats['global'] += sum(g.values()); global_ids.update(g)
        cats['other'] += sum(c.values())
        files_with[nm] = {'prim': sum(p.values()), 'global': sum(g.values()), 'other': sum(c.values())}

print('INSTRUCTION CATEGORIES (all global files):', dict(cats))
print(NL + 'Per-file instruction counts:')
for nm, v in sorted(files_with.items()):
    if v['global'] or v['prim']:
        print('  %-26s prim=%-6d global=%-6d other=%d' % (nm, v['prim'], v['global'], v['other']))
print(NL + 'TOP GLOBAL-CALL OPCODES (maxis global subroutines):')
for op, cnt in global_ids.most_common(60):
    print('  opcode=%-5d count=%d (0x%04x)' % (op, cnt, op))
print(NL + 'TOP PRIMITIVE OPCODES (<256):')
for op, cnt in prim_ids.most_common(60):
    print('  op=%-4d count=%d' % (op, cnt))
