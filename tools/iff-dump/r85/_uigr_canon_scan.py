import struct, sys, os
# R85 uichrome IFF-literal canon scan - engine-independent, mirrors _farnames_scan.py
# Reads ONLY the FAR1 manifest + raw member payloads. No engine, no conversion.
SRC='game-data/The Sims/UIGraphics/UIGraphics.far'
data=open(SRC,'rb').read()
man=struct.unpack('<I', data[12:16])[0]
num=struct.unpack('<I', data[man:man+4])[0]
off=man+4

WANT_SUBSTR = ['cpanel\\', 'greenbars', 'redbars', 'variablesans', 'cursors\\live', 'panelback']

def bmp_dims(b):
    # BMP: 'BM' at 0, width LE at 18, height LE at 22 (with sign bit)
    if len(b) < 26 or b[0:2] != b'BM':
        return None
    w = b[18] | (b[19]<<8) | (b[20]<<16) | (b[21]<<24)
    h = b[22] | (b[23]<<8) | (b[24]<<16) | (b[25]<<24)
    if h & 0x80000000: h = h  # keep as-is; print signed? just print raw
    return (w, h)

print('R85 uichrome canon - engine-independent raw IFF scan (%s)' % SRC)
print('member header: index DataLength filename [BMP WxH]')
print('-'*90)
for i in range(num):
    dlen,d2,doff,nlen = struct.unpack('<IIII', data[off:off+16])
    nm=data[off+16:off+16+nlen].decode('latin1','replace')
    off += 16+nlen
    low=nm.lower()
    if not any(sub in low for sub in WANT_SUBSTR):
        continue
    payload = data[doff:doff+dlen] if doff+dlen <= len(data) else b''
    dims = bmp_dims(payload)
    d = ('BMP %dx%d' % dims) if dims else ('hdr=%s' % payload[0:4].hex() if len(payload)>=4 else 'hdr<4')
    print(i, dlen, repr(nm), d)
print('-'*90)
print('total manifest members:', num)
