
import struct, json, glob

ID2_5 = bytes('IFF FILE 2.5:TYPE FOLLOWED BY SIZE JAMIE DOORNBOS & MAXIS 1', 'ascii')
ID2_0 = bytes('IFF FILE 2.0:TYPE FOLLOWED BY SIZE JAMIE DOORNBOS & MAXIS 1', 'ascii')

OK = set('abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789._\ ()-')

def clean_name(b):
    s = b.decode('latin1', 'replace')
    return s.replace(chr(0), '').strip()

def plausible(s):
    return 0 < len(s) <= 80 and all(c in OK for c in s)

def walk_iff(buf):
    if len(buf) < 64: return None
    ident = buf[0:60].replace(bytes([0]), b'').rstrip()
    if ident not in (ID2_0, ID2_5): return None
    off = 64
    types = {}; nbhav = 0; bhavs = []
    while len(buf) - off >= 2:
        ct = buf[off:off+4].decode('latin1', 'replace'); off += 4
        csize = struct.unpack_from('>I', buf, off)[0]; off += 4
        off += 2
        flags = struct.unpack_from('>H', buf, off)[0]; off += 2
        clabel = clean_name(buf[off:off+64]); off += 64
        data_sz = csize - 76
        if data_sz < 0: break
        types[ct] = types.get(ct, 0) + 1
        if ct == 'BHAV':
            nbhav += 1
            bhavs.append(clabel)
        off += data_sz
    return {'chunks': sum(types.values()), 'nbhav': nbhav, 'types': types, 'bhavs': bhavs}

def try_hdr(path, hlen):
    with open(path, 'rb') as f:
        data = f.read()
    size = len(data)
    man = struct.unpack('<I', data[12:16])[0]
    num = struct.unpack('<I', data[man:man+4])[0]
    off = man + 4
    entries = []
    bad = 0
    for i in range(num):
        if off + hlen > size:
            bad += 1; break
        dlen = struct.unpack('<I', data[off:off+4])[0]
        dlen2 = struct.unpack('<I', data[off+4:off+8])[0]
        doff = struct.unpack('<I', data[off+8:off+12])[0]
        nlen = struct.unpack('<H', data[off+12:off+14])[0]
        if not (0 < nlen <= 300):
            bad += 1; break
        if not (0 < dlen <= size):
            bad += 1; break
        nm = clean_name(data[off+hlen:off+hlen+nlen])
        if not plausible(nm):
            bad += 1
        entries.append((nm, dlen, dlen2, doff))
        off += hlen + nlen
    return entries, bad, num

for hlen in (14, 16):
    entries, bad, num = try_hdr('game-data/The Sims/GameData/Global/Global.far', hlen)
    wok = 0
    for (nm, dlen, dlen2, doff) in entries:
        if nm.lower().endswith('.iff') and dlen == dlen2 and doff < 50_000_000:
            with open('game-data/The Sims/GameData/Global/Global.far', 'rb') as f:
                f.seek(doff); body = f.read(dlen)
            if walk_iff(body): wok += 1
    print('hlen=%d entries_parsed=%d/24 bad_names=%d iffs_that_walk=%d' % (hlen, len(entries), bad, wok))
    if len(entries) == 24 and wok >= 20:
        chosen = (hlen, entries)

(hlen, entries) = chosen
print('USING header length', hlen)
out = []
for (nm, dlen, dlen2, doff) in entries:
    rec = {'name': nm, 'dlen': dlen, 'compressed': dlen2 != dlen}
    if nm.lower().endswith('.iff') and dlen2 == dlen and 0 < doff < 50_000_000 and dlen < 50_000_000:
        with open('game-data/The Sims/GameData/Global/Global.far', 'rb') as f:
            f.seek(doff); body = f.read(dlen)
        w = walk_iff(body)
        if w:
            rec['iff'] = w
            print('  %-28s chunks=%-5d BHAV=%-5d types=%s' % (nm, w['chunks'], w['nbhav'], ' '.join('%s:%d' % x for x in sorted(w['types'].items(), key=lambda kv: -kv[1])[:6])))
            print('      ' + ' '.join(w['bhavs'][:12]))
        else:
            print('  %-28s (iff but unwalkable/compressed)' % nm)
    else:
        print('  %-28s (compressed or non-iff)' % nm)
    out.append(rec)
import os
os.makedirs('tools/iff-dump', exist_ok=True)
with open('tools/iff-dump/global-bhav-inventory.json', 'w') as f:
    json.dump(out, f, ensure_ascii=False, indent=1)
print('saved global-bhav-inventory.json')
