
import struct
for path in ['game-data/The Sims/GameData/Global/Global.far', 'game-data/The Sims/GameData/Objects/Objects.far']:
    with open(path, 'rb') as f:
        hdr = f.read(8); ver = struct.unpack('<I', f.read(4))[0]
        man = struct.unpack('<I', f.read(4))[0]
        f.seek(man)
        num = struct.unpack('<I', f.read(4))[0]
        print(chr(10) + 'FAR', path, 'ver', ver, 'entries', num)
        for i in range(min(num, 60)):
            rec = f.read(14)
            if len(rec) < 14: break
            dlen, dlen2, doff = struct.unpack('<iii', rec[0:12])
            nlen = struct.unpack('<h', rec[12:14])[0]
            nm = f.read(nlen).decode('latin1', 'replace')
            print('  %-34s dlen=%-8d off=%-8d' % (nm, dlen, doff))
