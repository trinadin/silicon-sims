import struct
SRC='game-data/The Sims/UIGraphics/UIGraphics.far'
data=open(SRC,'rb').read()
man=struct.unpack('<I', data[12:16])[0]
num=struct.unpack('<I', data[man:man+4])[0]
off=man+4
for i in range(num):
    dlen,d2,doff,nlen = struct.unpack('<IIII', data[off:off+16])
    nm=data[off+16:off+16+nlen].decode('latin1','replace')
    off += 16+nlen
    if 'load' in nm.lower() or 'bus' in nm.lower():
        print(i, dlen, repr(nm))
