
import struct
path = 'game-data/The Sims/GameData/Global/Global.far'
with open(path, 'rb') as f:
    data = f.read()
print('size', len(data))
print('hdr', data[:16].hex())
man = struct.unpack('<I', data[12:16])[0]
print('man', man)
num = struct.unpack('<I', data[man:man+4])[0]
print('num', num)
off = man + 4
for i in range(4):
    print('entry', i, 'rec_hex', data[off:off+16].hex())
    dlen = struct.unpack('<i', data[off:off+4])[0]
    dlen2 = struct.unpack('<i', data[off+4:off+8])[0]
    doff = struct.unpack('<i', data[off+8:off+12])[0]
    nlen = struct.unpack('<h', data[off+12:off+14])[0]
    print('   dlen', dlen, 'dlen2', dlen2, 'doff', doff, 'nlen', nlen)
    nm = data[off+14:off+14+nlen].decode('latin1')
    print('   name', repr(nm))
    off += 14 + nlen
