import struct, os

SRC = 'game-data/The Sims/GameData/Global/Global.far'
OUT = '/tmp/iff-raw'

with open(SRC, 'rb') as f:
    data = f.read()

assert data[0:8] == b'FAR!byAZ', 'magic mismatch'
man = struct.unpack('<I', data[12:16])[0]
num = struct.unpack('<I', data[man:man+4])[0]
print('manifest_offset=%d num_files=%d filesize=%d' % (man, num, len(data)))

for hlen in (16, 14):
    off = man + 4
    bad = 0
    entries = []
    for i in range(num):
        if off + hlen > len(data):
            bad += 1; break
        dlen = struct.unpack('<I', data[off:off+4])[0]
        dlen2 = struct.unpack('<I', data[off+4:off+8])[0]
        doff = struct.unpack('<I', data[off+8:off+12])[0]
        nlen = struct.unpack('<I', data[off+12:off+16])[0] if hlen == 16 else struct.unpack('<H', data[off+12:off+14])[0]
        if not (0 < nlen <= 300):
            bad += 1; break
        nm = data[off+hlen:off+hlen+nlen].decode('latin1', 'replace')
        if not (nm.endswith('.iff') or nm.endswith('.far') or nm.endswith('.txt') or '.' in nm):
            bad += 1
        entries.append((nm, dlen, dlen2, doff))
        off += hlen + nlen
    ok = sum(1 for (nm, dl, d2, df) in entries if nm.lower().endswith('.iff') and dl == d2 and 0 < df < len(data))
    print('hlen=%d entries=%d bad=%d iff-ok=%d' % (hlen, len(entries), bad, ok))
    if len(entries) == num and bad == 0:
        break
else:
    raise SystemExit('no clean parse')

os.makedirs(OUT, exist_ok=True)
for (nm, dlen, dlen2, doff) in entries:
    base = os.path.basename(nm)
    if base.lower().endswith('.iff') and dlen2 == dlen and 0 < doff < len(data):
        body = data[doff:doff+dlen]
        if len(body) == dlen:
            with open(os.path.join(OUT, base), 'wb') as g:
                g.write(body)
            print('extracted %-28s len=%d' % (base, len(body)))
        else:
            print('skip %s incomplete %d != %d' % (base, len(body), dlen))
    else:
        print('skip %-28s dlen=%d dlen2=%d doff=%d' % (base, dlen, dlen2, doff))
