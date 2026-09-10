
import struct
path = 'game-data/The Sims/GameData/Global/Global.far'
with open(path, 'rb') as f:
    hdr = f.read(8); ver = struct.unpack('<I', f.read(4))[0]; man = struct.unpack('<I', f.read(4))[0]
    print('hdr', hdr, 'ver', ver, 'man', man)
    f.seek(man)
    num = struct.unpack('<I', f.read(4))[0]
    print('numfiles', num)
    print('manifest bytes (first 96):', f.read(96).hex())
