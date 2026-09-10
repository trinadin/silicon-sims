
import os, sys, struct, json, glob

ID2_5 = bytes('IFF FILE 2.5:TYPE FOLLOWED BY SIZE JAMIE DOORNBOS & MAXIS 1', 'ascii')
ID2_0 = bytes('IFF FILE 2.0:TYPE FOLLOWED BY SIZE JAMIE DOORNBOS & MAXIS 1', 'ascii')

NL = chr(10)
STRIPCH = bytes([32, 9, 0])

def read_cstr(buf, off, n):
    # Mirrors FreeSO: identifier read strips ALL nulls (IFFFile.Read CString .Replace("")) 
    raw = buf[off:off+n]
    raw = raw.replace(bytes([0]), b'')
    return raw.rstrip(bytes([32, 9])), off + n

def parse_bhav(body):
    if len(body) < 12:
        return None
    ver = struct.unpack_from('<H', body, 0)[0]
    if ver in (0x8000, 0x8001):
        return {'ftype': hex(ver), 'instructions': struct.unpack_from('<H', body, 2)[0]}
    if ver == 0x8002:
        return {'ftype': '0x8002', 'instructions': struct.unpack_from('<H', body, 2)[0],
                'type': body[4], 'args': body[5], 'locals': struct.unpack_from('<H', body, 6)[0],
                'bhavversion': struct.unpack_from('<H', body, 8)[0]}
    if ver == 0x8003:
        return {'ftype': '0x8003', 'instructions': struct.unpack_from('<I', body, 9)[0],
                'type': body[2], 'args': body[3], 'locals': body[4],
                'bhavversion': struct.unpack_from('<H', body, 7)[0]}
    return {'ftype': '?', 'unrecognized': hex(ver)}

def inspect(path):
    with open(path, 'rb') as f:
        buf = f.read()
    size = len(buf)
    if size < 64:
        return {'file': path, 'bytes': size, 'not_iff': 'too small'}
    ident, off = read_cstr(buf, 0, 60)
    if ident not in (ID2_0, ID2_5):
        return {'file': path, 'bytes': size, 'not_iff': ident[:40].decode('latin1', 'replace')}
    rsmp = struct.unpack_from('>I', buf, off)[0]; off += 4
    types = {}
    bhavs = []
    chunk_count = 0
    bad = None
    while size - off >= 2:
        ct = buf[off:off+4].decode('latin1', 'replace'); off += 4
        csize = struct.unpack_from('>I', buf, off)[0]; off += 4
        cid = struct.unpack_from('>H', buf, off)[0]; off += 2
        cflags = struct.unpack_from('>H', buf, off)[0]; off += 2
        clabel, off = read_cstr(buf, off, 64)
        data_sz = csize - 76
        if data_sz < 0:
            bad = 'negative data size ' + str(csize); break
        if off + data_sz > size:
            bad = 'chunk overruns file'; break
        types[ct] = types.get(ct, 0) + 1
        chunk_count += 1
        if ct == 'BHAV':
            bhavs.append({'id': cid, 'label': clabel.decode('latin1', 'replace'), 'hdr': parse_bhav(buf[off:off+data_sz])})
        off += data_sz
    return {'file': path, 'bytes': size, 'chunks': chunk_count,
            'leftover': size - off, 'bad': bad, 'types': types, 'bhavs': bhavs[:60], 'nbhav': len(bhavs)}

def main():
    roots = []
    for arg in sys.argv[1:]:
        if os.path.isdir(arg):
            roots += glob.glob(arg + '/**/*.iff', recursive=True)
            roots += glob.glob(arg + '/**/*.package', recursive=True)
        else:
            roots.append(arg)
    summary = []
    for p in sorted(roots):
        r = inspect(p)
        summary.append(r)
        if r is None:
            continue
        if 'not_iff' in r:
            print(NL + 'SKIP ' + r['file'] + ' ' + str(r['not_iff']))
            continue
        top = sorted(r['types'].items(), key=lambda kv: -kv[1])[:8] if r['types'] else []
        print(NL + 'FILE ' + r['file'])
        print('  bytes=%d chunks=%d leftover=%d bad=%s' % (r['bytes'], r['chunks'], r['leftover'], r['bad']))
        print('  BHAV count=%d' % r['nbhav'])
        print('  sample=' + json.dumps(r['bhavs'][:4], ensure_ascii=False))
        print('  types= ' + ' '.join('%s:%d' % (k, v) for k, v in top))
    os.makedirs('tools/iff-dump', exist_ok=True)
    with open('tools/iff-dump/iff-summary.json', 'w') as f:
        json.dump(summary, f, ensure_ascii=False, indent=1)

if __name__ == '__main__':
    main()
