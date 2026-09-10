#!/usr/bin/env python3
# R86 IFF-literal canon scan (mirrors r85) - engine-independent raw scan of UIGraphics.far
# Resolves the ORIGINAL live-toolbar chrome members (from Res_CPanel.RT ids:
#   kHouseBtn=House.bmp, kOptions=options.bmp, kPause=pause.bmp, kOptionExit=OptExit.bmp,
#   kObjects=objects.bmp, kLiveModeGauge=LiveGadget.bmp) and prints index/bytes/dims.
import struct
SRC = 'game-data/The Sims/UIGraphics/UIGraphics.far'
WANT = {
    'cpanel\\Buttons\\House.bmp': 'kHouseBtn',
    'cpanel\\Buttons\\options.bmp': 'kOptions',
    'cpanel\\Buttons\\pause.bmp': 'kPause',
    'cpanel\\Buttons\\OptExit.bmp': 'kOptionExit',
    'cpanel\\Buttons\\objects.bmp': 'kObjects',
    'cpanel\\Backgrounds\\LiveGadget.bmp': 'kLiveModeGauge',
}
def bmp_dims(b):
    if len(b) < 26 or b[0:2] != b'BM':
        return None
    return (b[18] | (b[19] << 8) | (b[20] << 16) | (b[21] << 24),
            b[22] | (b[23] << 8) | (b[24] << 16) | (b[25] << 24))
data = open(SRC, 'rb').read()
man = struct.unpack('<I', data[12:16])[0]
num = struct.unpack('<I', data[man:man + 4])[0]
off = man + 4
print('R86 toolbar canon - engine-independent raw IFF scan (%s)' % SRC)
for i in range(num):
    dlen, d2, doff, nlen = struct.unpack('<IIII', data[off:off + 16])
    nm = data[off + 16:off + 16 + nlen].decode('latin1', 'replace')
    off += 16 + nlen
    if nm not in WANT:
        continue
    payload = data[doff:doff + dlen]
    dims = bmp_dims(payload)
    print('%s idx=%d bytes=%d %s' % (WANT[nm], i, dlen, '%dx%d' % dims if dims else 'nodims'))
