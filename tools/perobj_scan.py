
import struct, collections

def far_entries(path):
    data = open(path, 'rb').read()
    man = struct.unpack('<I', data[12:16])[0]
    num = struct.unpack('<I', data[man:man+4])[0]
    off = man + 4
    out = []
    for i in range(num):
        rec = data[off:off+14]
        if len(rec) < 14: break
        dlen = struct.unpack('<I', rec[0:4])[0]
        dlen2 = struct.unpack('<I', rec[4:8])[0]
        doff = struct.unpack('<I', rec[8:12])[0]
        nlen = struct.unpack('<H', rec[12:14])[0]
        nm = data[off+16:off+16+nlen].decode('latin1','replace')
        off += 16 + nlen
        if nm.lower().endswith('.iff') and dlen2 == dlen and doff < len(data):
            out.append((nm, data[doff:doff+dlen]))
    return out

def chunks(body):
    pos = 64
    while pos + 76 <= len(body):
        ct = body[pos:pos+4].decode('latin1','replace')
        sz = struct.unpack_from('>I', body, pos+4)[0] - 76
        cid = struct.unpack_from('>H', body, pos+8)[0]
        yield ct, cid, body[pos+76:pos+76+sz]
        pos += 76 + sz

def glide(body):
    c = body[0]
    if c < 48:
        n = c
        return body[1:1+n].decode('latin1','replace') if n > 0 else ''
    s = bytearray()
    for b in body:
        if b == 0: break
        s.append(b)
    return s.decode('latin1','replace')

def bhav_ops(body):
    ops = []
    for (ct, cid, b) in chunks(body):
        if ct != 'BHAV' or len(b) < 12: continue
        ver = struct.unpack_from('<H', b, 0)[0]
        if ver != 0x8002: continue
        cnt = struct.unpack_from('<H', b, 2)[0]
        for k in range(cnt):
            s = 12 + k*12
            if s+12 > len(b): break
            ops.append(struct.unpack_from('<H', b, s)[0])
    return ops

glob_files = far_entries('game-data/The Sims/GameData/Global/Global.far')
module_names = set(nm.lower().replace('.iff','') for nm, _ in glob_files)
global_ids = set()
for nm, body in glob_files:
    if nm.lower() != 'global.iff': continue
    for (ct, cid, b) in chunks(body):
        if ct == 'BHAV': global_ids.add(cid)

obj = far_entries('game-data/The Sims/GameData/Objects/Objects.far')
print('objects scanned:', len(obj))
bad_glob = []
bad_priv = []
bad_glbid = []
for nm, body in obj:
    globs = [glide(b) for (ct, cid, b) in chunks(body) if ct == 'GLOB' and len(b) > 0]
    for g in globs:
        if g.lower().replace('.iff','') not in module_names:
            bad_glob.append((nm, g))
    local = set(cid for (ct, cid, b) in chunks(body) if ct == 'BHAV')
    for op in bhav_ops(body):
        if 4096 <= op < 8192:
            if op not in local: bad_priv.append((nm, op))
        elif 256 <= op < 4096:
            if op not in global_ids and nm.lower() != 'global.iff': bad_glbid.append((nm, op))
print('objects with unresolvable GLOB:', len(set(n for n,_ in bad_glob)))
for n in sorted(set(n for n,_ in bad_glob))[:30]:
    print('   GLOB?', n, [g for (nn,g) in bad_glob if nn == n][:1])
print('dangling private calls (4096-8191):', len(bad_priv))
for (nm, op) in bad_priv[:40]:
    print('   priv', nm, op, hex(op))
print('dangling global calls (256-4095):', len(bad_glbid))
for (nm, op) in bad_glbid[:40]:
    print('   glob', nm, op, hex(op))
