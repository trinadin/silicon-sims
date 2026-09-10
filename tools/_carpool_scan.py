import struct, sys, os, glob

ID2_5 = bytes('IFF FILE 2.5:TYPE FOLLOWED BY SIZE JAMIE DOORNBOS & MAXIS 1', 'ascii')
ID2_0 = bytes('IFF FILE 2.0:TYPE FOLLOWED BY SIZE JAMIE DOORNBOS & MAXIS 1', 'ascii')

def read_cstr(buf, off, n):
    raw = buf[off:off+n].replace(bytes([0]), b'')
    return raw.rstrip(bytes([32, 9])), off + n

def walk_iff(buf):
    if len(buf) < 64: return []
    ident, off = read_cstr(buf, 0, 60)
    if ident not in (ID2_0, ID2_5): return []
    off += 4
    out = []
    while len(buf) - off >= 8:
        ct = buf[off:off+4].decode('latin1', 'replace'); off += 4
        csize = struct.unpack_from('>I', buf, off)[0]; off += 4
        cid = struct.unpack_from('>H', buf, off)[0]; off += 2
        off += 2
        clabel, off = read_cstr(buf, off, 64)
        data_sz = csize - 76
        if data_sz < 0 or off + data_sz > len(buf): break
        if ct == 'BHAV':
            out.append(('BHAV', cid, clabel.decode('latin1', 'replace'), data_sz))
        elif ct == 'OBJD':
            name = ''
            try:
                d = buf[off:off+data_sz]
                if len(d) >= 104:
                    name = d[72:72+32].split(b'\x00')[0].decode('latin1', 'replace')
            except:
                pass
            out.append(('OBJD', cid, name, data_sz))
        off += data_sz
    return out

def scan_iff_body(name, body):
    rows = walk_iff(body)
    objd_names = [r for r in rows if r[0] == 'OBJD' and ('car' in r[2].lower() or 'portal' in r[2].lower() or 'pool' in r[2].lower())]
    bhavs = [r for r in rows if r[0] == 'BHAV' and ('car' in r[2].lower() or 'work' in r[2].lower() or 'school' in r[2].lower() or 'job' in r[2].lower() or 'portal' in r[2].lower())]
    if objd_names or bhavs or 'car' in os.path.basename(name).lower():
        print('== %s ==' % name)
        for t, cid, label, sz in objd_names:
            print('   OBJD id=%d name=%r' % (cid, label))
        for t, cid, label, sz in bhavs:
            print('   BHAV id=%d label=%r' % (cid, label))

def far_scan(path):
    with open(path, 'rb') as f:
        hdr = f.read(8)
        ver = struct.unpack('<I', f.read(4))[0]
        if hdr != b'FAR!byAZ' or ver != 1: return
        man = struct.unpack('<I', f.read(4))[0]
        f.seek(man)
        num = struct.unpack('<I', f.read(4))[0]
        for i in range(num):
            rec = f.read(14)
            if len(rec) < 14: break
            (dlen, dlen2, doff) = struct.unpack('<iii', rec[0:12])
            nlen = struct.unpack('<h', rec[12:14])[0]
            if nlen < 0 or nlen > 1024: break
            name = f.read(nlen).rstrip(bytes([0])).decode('latin1', 'replace')
            if dlen > 0 and doff >= 0:
                p = f.tell()
                f.seek(doff)
                body = f.read(min(dlen, 64 * 1024 * 1024))
                f.seek(p)
                scan_iff_body(name, body)

targets = sys.argv[1:] or [
    'game-data/The Sims/GameData/Objects/Objects.far',
    'game-data/The Sims/GameData/Global/Global.far',
]
for p in targets:
    print('### FAR ' + p)
    far_scan(p)
