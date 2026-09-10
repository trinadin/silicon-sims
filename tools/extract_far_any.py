import struct, os, sys

SRC = sys.argv[1]
OUT = sys.argv[2]

with open(SRC, 'rb') as f:
    data = f.read()

assert data[0:8] == b'FAR!byAZ', 'magic mismatch: '+SRC
man = struct.unpack('<I', data[12:16])[0]
num = struct.unpack('<I', data[man:man+4])[0]
print('manifest_offset=%d num_files=%d filesize=%d' % (man, num, len(data)))

chosen = None
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
        entries.append((nm, dlen, dlen2, doff))
        off += hlen + nlen
    ok = sum(1 for (nm, dl, d2, df) in entries if dl == d2 and 0 < df < len(data) and dl < len(data))
    print('  hlen=%d entries=%d bad=%d plausible=%d' % (hlen, len(entries), bad, ok))
    if len(entries) == num and bad == 0 and ok >= num * 0.5:
        chosen = (hlen, entries); break
if chosen is None:
    raise SystemExit('no clean parse for ' + SRC)

os.makedirs(OUT, exist_ok=True)
(n_comp) = (0,)
for (nm, dlen, dlen2, doff) in entries:
    base = os.path.basename(nm)
    if dlen2 != dlen:
        print('SKIP-COMPRESSED ' + base + ' dlen=%d dlen2=%d' % (dlen, dlen2))
        n_comp = (n_comp[0]+1,)
        continue
    if not (0 < doff < len(data) and dlen <= len(data) - doff):
        print('SKIP-BADOFF ' + base); continue
    body = data[doff:doff+dlen]
    out = os.path.join(OUT, base)
    with open(out, 'wb') as g:
        g.write(body)
print('compressed skipped: %d' % n_comp[0])
