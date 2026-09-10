
import os, struct, json, sys

NL = chr(10)
ID2_5 = bytes('IFF FILE 2.5:TYPE FOLLOWED BY SIZE JAMIE DOORNBOS & MAXIS 1', 'ascii')
ID2_0 = bytes('IFF FILE 2.0:TYPE FOLLOWED BY SIZE JAMIE DOORNBOS & MAXIS 1', 'ascii')

def read_cstr(buf, off, n):
    raw = buf[off:off+n].replace(bytes([0]), b'')
    return raw.rstrip(bytes([32, 9])), off + n

def walk_iff(buf):
    if len(buf) < 64: return None
    ident, off = read_cstr(buf, 0, 60)
    if ident not in (ID2_0, ID2_5): return None
    rsmp = struct.unpack_from('>I', buf, off)[0]; off += 4
    types = {}; nbhav = 0; n = 0; bhav_names = []
    while len(buf) - off >= 2:
        ct = buf[off:off+4].decode('latin1', 'replace'); off += 4
        csize = struct.unpack_from('>I', buf, off)[0]; off += 4
        cid = struct.unpack_from('>H', buf, off)[0]; off += 2
        off += 2
        clabel, off = read_cstr(buf, off, 64)
        data_sz = csize - 76
        if data_sz < 0 or off + data_sz > len(buf): break
        types[ct] = types.get(ct, 0) + 1
        if ct == 'BHAV':
            nbhav += 1
            bhav_names.append(str(cid) + ':' + clabel.decode('latin1','replace'))
        n += 1
        off += data_sz
    return {'chunks': n, 'nbhav': nbhav, 'types': types, 'bhavs': bhav_names[:40]}

def far(path, cap):
    with open(path, 'rb') as f:
        hdr = f.read(8)
        ver = struct.unpack('<I', f.read(4))[0]
        if hdr != b'FAR!byAZ' or ver != 1:
            return {'file': path, 'error': 'hdr=%r ver=%d' % (hdr, ver)}
        man = struct.unpack('<I', f.read(4))[0]
        f.seek(man)
        num = struct.unpack('<I', f.read(4))[0]
        entries = []
        for i in range(num):
            rec = f.read(14)
            if len(rec) < 14: break
            (dlen, dlen2, doff) = struct.unpack('<iii', rec[0:12])
            nlen = struct.unpack('<h', rec[12:14])[0]
            if nlen < 0 or nlen > 1024: break
            name = f.read(nlen).rstrip(bytes([0]))  # FAR1 filenames are NOT aligned
            e = {'name': name.decode('latin1','replace'), 'dlen': dlen, 'off': doff}
            if dlen > 0 and doff >= 0:
                p = f.tell()
                f.seek(doff)
                body = f.read(min(dlen, cap))
                f.seek(p)
                w = walk_iff(body)
                if w is not None:
                    e['iff'] = w
            entries.append(e)
    return {'file': path, 'num': num, 'entries': entries}

TARGETS = sys.argv[1:] if len(sys.argv) > 1 else ['game-data/The Sims/GameData/Global/Global.far',
    'game-data/The Sims/GameData/Objects/Objects.far']
print(NL)
summary = []
for p in TARGETS:
    r = far(p, 8 * 1024 * 1024)
    print('FAR ' + r['file'] + ' entries=' + str(r['num']))
    if 'error' in r:
        print('  ', r['error']); continue
    niff = 0; nbhav = 0
    for e in r['entries'][:140]:
        w = e.get('iff')
        if w:
            niff += 1; nbhav += w['nbhav']
            print('  %-42s chunks=%-5d BHAV=%-4d types=%s' % (e['name'], w['chunks'], w['nbhav'], ' '.join('%s:%d' % x for x in sorted(w['types'].items(), key=lambda kv: -kv[1])[:6])))
            if 'Global' in p and w['bhavs']:
                print('     bhavs=' + ' '.join(w['bhavs'][:10]))
        elif e['dlen'] > 0:
            print('  %-42s (non-IFF dlen=%d)' % (e['name'], e['dlen']))
        else:
            print('  %-42s' % (e['name'],))
    if r['num'] > 140:
        print('  ... (+%d more)' % (r['num'] - 140))
    print('  IFF files=%d total BHAVs=%d' % (niff, nbhav))
    summary.append({'file': r['file'], 'num': r['num'], 'niff': niff, 'nbhav': nbhav})
    print(NL)
