import struct, hashlib, re

FAR = 'game-data/The Sims/UIGraphics/UIGraphics.far'
data = open(FAR, 'rb').read()
man = struct.unpack('<I', data[12:16])[0]
num = struct.unpack('<I', data[man:man + 4])[0]
off = man + 4
truth = {}
for i in range(num):
    if off + 16 > len(data):
        break
    dlen, d2, doff, nlen = struct.unpack('<IIII', data[off:off + 16])
    nm = data[off + 16:off + 16 + nlen].decode('latin1', 'replace')
    off += 16 + nlen
    if dlen != d2 or doff + dlen > len(data):
        continue
    base = nm.replace('\\', '/').split('/')[-1].lower()
    if base not in truth:
        raw = data[doff:doff + dlen]
        if len(raw) >= 26:
            w, h = struct.unpack('<ii', raw[18:26])
        else:
            w = h = -1
        truth[base] = (dlen, w, h, hashlib.sha256(raw).hexdigest())

src = open('Client/Simitone/Simitone.Client/AutotestRunner.cs').read()
pat = re.compile(r'new\[\] \{ "(cpanel\\\\[^"]+)", "(\d+)", "(\d+)", "(\d+)", "([0-9a-f]{64})" \}')
pins = pat.findall(src)
print(len(pins), 'pins found')
bad = 0
for name, ln, w, h, s in pins:
    base = name.split('\\')[-1].lower()
    t = truth.get(base)
    if t is None:
        print(base, 'MISSING FROM FAR')
        bad += 1
        continue
    errs = []
    if t[0] != int(ln):
        errs.append('len %s->%d' % (ln, t[0]))
    if t[1] != int(w):
        errs.append('w %s->%d' % (w, t[1]))
    if t[2] != int(h):
        errs.append('h %s->%d' % (h, t[2]))
    if t[3] != s:
        errs.append('sha %s..->%s..' % (s[:10], t[3][:10]))
    if errs:
        bad += 1
        print(base, 'FIX:', ', '.join(errs))
        print('  TRUE: len=%d %dx%d sha256=%s' % t)
print('bad:', bad)
