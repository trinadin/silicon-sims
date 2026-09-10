import struct, os, sys, glob

ID2_5 = bytes('IFF FILE 2.5:TYPE FOLLOWED BY SIZE JAMIE DOORNBOS & MAXIS 1', 'ascii')
ID2_0 = bytes('IFF FILE 2.0:TYPE FOLLOWED BY SIZE JAMIE DOORNBOS & MAXIS 1', 'ascii')

def clean(b):
    return b.decode('latin1', 'replace').replace(chr(0), '').strip()

WATCH = {8192, 8298, 8325, 8358, 8194, 8195, 8203, 8211, 8330}

def scan_iff(path):
    with open(path, 'rb') as f:
        buf = f.read()
    if len(buf) < 64:
        return None
    ident = buf[0:60].replace(bytes([0]), b'').rstrip()
    if ident not in (ID2_0, ID2_5):
        return None
    off = 64
    n = len(buf)
    names = {}
    bhavs = []
    while off + 76 <= n:
        ct = buf[off:off+4].decode('latin1', 'replace')
        csize = struct.unpack_from('>I', buf, off+4)[0]
        cid = struct.unpack_from('>H', buf, off+8)[0]
        label = clean(buf[off+12:off+76])
        total = max(76, csize)
        body = buf[off+76:off+total]
        if ct == 'BHAV':
            names[cid] = label
            ver = struct.unpack('<H', body[0:2])[0] if len(body) >= 8 else 0
            count = None
            ins_off = None
            if ver == 0x8002:
                count = struct.unpack('<H', body[2:4])[0]; ins_off = 8
            elif ver == 0x8003:
                count = struct.unpack('<I', body[6:10])[0]; ins_off = 10
            if count is not None:
                calls = []
                prims = []
                io = ins_off
                for i in range(count):
                    if io + 12 > len(body):
                        break
                    op = struct.unpack('<H', body[io:io+2])[0]
                    if op >= 256 and op in WATCH:
                        calls.append((i, op))
                    elif op == 19:
                        prims.append(i)
                    io += 12
                if calls or prims:
                    bhavs.append((label, cid, calls, prims))
        off += total
    return names, bhavs

def main():
    roots = sys.argv[1:]
    stats = {}
    for root in roots:
        for p in glob.glob(os.path.join(root, '**', '*.iff'), recursive=True):
            res = scan_iff(p)
            if not res:
                continue
            names, bhavs = res
            for (label, cid, calls, prims) in bhavs:
                rel = p
                for (i, raw) in calls:
                    tgt = names.get(raw, '?') if raw in names else ('SEMI-' + str(raw))
                    key = (raw, tgt)
                    stats[key] = stats.get(key, 0) + 1
                    print('%s :: %s(%d) ins%d CALL %d (%s)' % (rel, label, cid, i, raw, tgt))
                for i in prims:
                    print('%s :: %s(%d) ins%d OP19' % (rel, label, cid, i))
    print('--- which iff files DEFINE id 8298 ---')
    for root in roots:
        for p in glob.glob(os.path.join(root, '**', '*.iff'), recursive=True):
            res = scan_iff(p)
            if not res: continue
            names, bhavs = res
            if 8298 in names:
                print('%s defines 8298 as %r' % (p, names[8298]))

main()
