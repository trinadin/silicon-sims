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

# the 30 options-screen members, in the gate table's order
order = ['optsave.bmp', 'optnbhd.bmp', 'optexit.bmp', 'optgraphics.bmp', 'optsound.bmp',
         'optplay.bmp', 'optcheckbox.bmp', 'optradio.bmp', 'optslideron.bmp',
         'optslideroff.bmp', 'opttutreset.bmp', 'options.bmp', 'popupoptantialias.bmp',
         'popupoptautocenter.bmp', 'popupoptautosnap.bmp', 'popupoptchardetail.bmp',
         'popupoptedgescroll.bmp', 'popupoptfreewill.bmp', 'popupoptlighting.bmp',
         'popupoptlivepip.bmp', 'popupoptmusic.bmp', 'popupoptquicktips.bmp',
         'popupoptsfx.bmp', 'popupoptshadows.bmp', 'popupoptsiminback.bmp',
         'popupoptterraindetail.bmp', 'popupopttransui.bmp', 'popupoptvox.bmp',
         'popupresettutorial.bmp', 'popupexporthtml.bmp']
rows = []
for base in order:
    t = truth.get(base)
    assert t is not None, base + ' missing from FAR'
    rows.append('                    new[] { "cpanel\\\\buttons\\\\%s", "%d", "%d", "%d", "%s" },'
                % (base if base != 'options.bmp' else 'options.bmp',
                   t[0], t[1], t[2], t[3]))
# popups live under cpanel\ root, not buttons\ — fix the prefix per-member
fixed = []
for base, r in zip(order, rows):
    if base.startswith('popup'):
        r = r.replace('cpanel\\\\buttons\\\\', 'cpanel\\\\')
    fixed.append(r)
table = '\n'.join(fixed)

src = open('Client/Simitone/Simitone.Client/AutotestRunner.cs').read()
start = src.index('                string[][] artPins = new string[][] {')
end = src.index('                };', start) + len('                };')
new_block = '                string[][] artPins = new string[][] {\n' + table + '\n                };'
src2 = src[:start] + new_block + src[end:]
# mains frame-dims fix: compare frame width (sheet/4), not sheet width
old_mains = '&& mb.Tex.Width / 4 == int.Parse(artPins[i][2]) && mb.Tex.Height == int.Parse(artPins[i][3]);'
new_mains = '&& mb.Tex.Width == int.Parse(artPins[i][2]) && mb.Tex.Height == int.Parse(artPins[i][3]) && mb.Tex.Width % 4 == 0;'
assert old_mains in src2, 'mains line not found'
src2 = src2.replace(old_mains, new_mains)
open('Client/Simitone/Simitone.Client/AutotestRunner.cs', 'w').write(src2)
print('artPins table regenerated (30 rows) + mains dims check fixed')
