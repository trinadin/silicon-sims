
import struct, collections, json
path = 'game-data/The Sims/GameData/Global/Global.far'
data = open(path, 'rb').read()
man = struct.unpack('<I', data[12:16])[0]
num = struct.unpack('<I', data[man:man+4])[0]
# id -> label for Global.iff
idname = {}
bodies = []
off = man + 4
for i in range(num):
    rec = data[off:off+14]
    dlen = struct.unpack('<I', rec[0:4])[0]
    dlen2 = struct.unpack('<I', rec[4:8])[0]
    doff = struct.unpack('<I', rec[8:12])[0]
    nlen = struct.unpack('<H', rec[12:14])[0]
    nm = data[off+16:off+16+nlen].decode('latin1','replace')
    off += 16 + nlen
    if not (nm.lower().endswith('.iff') and dlen2 == dlen and doff < len(data)):
        continue
    body = data[doff:doff+dlen]
    pos = 64
    while pos + 76 <= len(body):
        ct = body[pos:pos+4].decode('latin1','replace')
        sz = struct.unpack_from('>I', body, pos+4)[0] - 76
        cid = struct.unpack_from('>H', body, pos+8)[0]
        clabel = body[pos+12:pos+76].replace(bytes([0]), b'').decode('latin1','replace').strip()
        if ct == 'BHAV':
            if nm.lower() == 'global.iff':
                idname[cid] = clabel
        pos += 76 + sz
    bodies.append((nm, body))

global_calls = collections.Counter()
for (nm, body) in bodies:
    pos = 64
    while pos + 76 <= len(body):
        ct = body[pos:pos+4].decode('latin1','replace')
        sz = struct.unpack_from('>I', body, pos+4)[0] - 76
        pos += 76
        if ct == 'BHAV' and sz > 12:
            b = body[pos:pos+sz]
            ver = struct.unpack_from('<H', b, 0)[0]
            if ver == 0x8002 and len(b) >= 12:
                cnt = struct.unpack_from('<H', b, 2)[0]
                for k in range(cnt):
                    s = 12 + k*12
                    if s+12 > len(b): break
                    op = struct.unpack_from('<H', b, s)[0]
                    if 256 <= op < 4096:
                        global_calls[op] += 1
        pos += sz

rows = []
for op, count in global_calls.most_common():
    rows.append({'opcode': op, 'count': count, 'name': idname.get(op, '?')})
print('distinct global-call opcodes used:', len(rows))
print('of which named in Global.iff:', sum(1 for r in rows if r['name'] != '?'))
print()
for r in rows[:40]:
    print('  #%-4d x%-5d  %s' % (r['opcode'], r['count'], r['name']))
print()
print('unresolved in Global.iff:')
un = [r for r in rows if r['name'] == '?']
print('  ', ' '.join(str(r['opcode']) for r in un[:60]))
json.dump(rows, open('tools/iff-dump/global-calls.json','w'), ensure_ascii=False, indent=1)
print('saved tools/iff-dump/global-calls.json')
