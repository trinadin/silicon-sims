# RE tool: scan ALL staged Sims 1 BHAVs for relationship primitives (op 24 old-relation,
# op 26 relation) and cluster by UseNeighbor flag (Flags bit 1 = 0x02) and Mode, by BHAV label.
#
# Purpose: confirm whether the engine's UseNeighbor mode-0 path (VMRelationship.cs) is actually
# reached by original data, and with what stack-object context, before claiming a fidelity bug.
# Mirrors FSO.Files BHAV.Read layout: each instruction = u16 opcode + u8 true + u8 false + 8-byte
# operand (12 bytes). Op 24 operand: [GetSet, RelVar, Param, Mode, Flags, pad...];
# Op 26 operand: [RelVar, Mode, Flags, Local, VarScope(u16), VarData(i16)].
import struct, collections, sys

ID2_5 = b'IFF FILE 2.5:TYPE FOLLOWED BY SIZE JAMIE DOORNBOS & MAXIS 1'
ID2_0 = b'IFF FILE 2.0:TYPE FOLLOWED BY SIZE JAMIE DOORNBOS & MAXIS 1'

def clean_name(b):
    return b.decode('latin1', 'replace').replace(chr(0), '').strip()

def walk_iff(buf, cb):
    """cb(chunk_type, cid, label, body) for each chunk."""
    if len(buf) < 64:
        return
    ident = buf[0:60].replace(bytes([0]), b'').rstrip()
    if ident not in (ID2_0, ID2_5):
        return
    off = 64
    while len(buf) - off >= 8:
        ct = buf[off:off+4].decode('latin1', 'replace'); off += 4
        csize = struct.unpack_from('>I', buf, off)[0]; off += 4
        cid = struct.unpack_from('>H', buf, off)[0]; off += 2
        flags = struct.unpack_from('>H', buf, off)[0]; off += 2
        clabel = clean_name(buf[off:off+64]); off += 64
        dsz = csize - 76
        if dsz < 0 or off + dsz > len(buf):
            break
        cb(ct, cid, clabel, buf[off:off+dsz])
        off += dsz

def far_entries(path):
    """Return list of (name, dlen, dlen2, doff) using 16-byte manifest (FAR1 'FAR!byAZ')."""
    data = open(path, 'rb').read()
    size = len(data)
    if data[:8] != b'FAR!byAZ':
        return []
    man = struct.unpack('<I', data[12:16])[0]
    num = struct.unpack('<I', data[man:man+4])[0]
    off = man + 4
    entries = []
    for i in range(num):
        if off + 16 > size:
            break
        dlen = struct.unpack('<I', data[off:off+4])[0]
        dlen2 = struct.unpack('<I', data[off+4:off+8])[0]
        doff = struct.unpack('<I', data[off+8:off+12])[0]
        nlen = struct.unpack('<H', data[off+12:off+14])[0]
        if not (0 < nlen <= 300) or off + 16 + nlen > size:
            break
        nm = clean_name(data[off+16:off+16+nlen])
        entries.append((nm, dlen, dlen2, doff))
        off += 16 + nlen
    return entries

def scan_bhav(body, label, cluster, mode14):
    if len(body) < 2:
        return
    v2 = struct.unpack_from('<H', body, 0)[0]
    if v2 == 0x8002:
        n = struct.unpack_from('<H', body, 2)[0]
        for i in range(n):
            base = 12 + i * 12
            if base + 12 > len(body):
                break
            op = struct.unpack_from('<H', body, base)[0]
            if op == 24:
                GetSet, RelVar, Par, Mode, Flags = struct.unpack_from('<BBBBB', body, base+4)
                usen = (Flags & 2) == 2
                cluster[(op, usen, Mode, RelVar)][label] += 1
                mode14.append((op, usen, Mode, RelVar, label))
            elif op == 26:
                RelVar, Mode, Flags, Local = struct.unpack_from('<BBBB', body, base+4)
                usen = (Flags & 2) == 2
                cluster[(op, usen, Mode, RelVar)][label] += 1
                mode14.append((op, usen, Mode, RelVar, label))

cluster = collections.defaultdict(collections.Counter)
mode14 = []

def scan_file(path, srcname):
    if path.endswith('.far'):
        for (nm, dlen, dlen2, doff) in far_entries(path):
            if not nm.lower().endswith('.iff'):
                continue
            data = open(path, 'rb').read()
            body = data[doff:doff+dlen]
            def cb(ct, cid, clabel, chunk, src=srcname+'/'+nm):
                if ct == 'BHAV':
                    scan_bhav(chunk, clabel or '(%s cid=%d)' % (ct, cid), cluster, mode14)
            walk_iff(body, cb)
    elif path.endswith('.iff'):
        data = open(path, 'rb').read()
        def cb(ct, cid, clabel, chunk):
            if ct == 'BHAV':
                scan_bhav(chunk, clabel or '(%s cid=%d)' % (ct, cid), cluster, mode14)
        walk_iff(data, cb)

for p in sys.argv[1:]:
    print('SCANNING', p)
    scan_file(p, p)

print()
print('=== relationship-op cluster (op, UseNeighbor, Mode, RelVar) -> #calls', sorted(
    ((k, sum(v.values())) for k, v in cluster.items()), key=lambda x: -x[1], reverse=False))
print()
for (op, usen, mode, rv), cnt in sorted(cluster.items()):
    un = 'UseN' if usen else 'obj '
    print('op=%-2d %s mode=%d rv=%d total=%-4d :: %s' % (op, un, mode, rv, sum(cnt.values()),
        ' | '.join('%s=%d' % (k, v) for k, v in cnt.most_common(8))))

neigh = [t for t in mode14 if t[1]]
print()
print('=== UseNeighbor calls total:', len(neigh))
for (op, usen, mode, rv, label) in neigh:
    print('  op=%-2d mode=%d rv=%d :: %s' % (op, mode, rv, label))
