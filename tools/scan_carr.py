import struct
pathg = 'game-data/The Sims/GameData/Global/Global.far'
def clean_name(b): return b.decode('latin1','replace').replace(chr(0),'').strip()
data = open(pathg,'rb').read(); man = struct.unpack('<I', data[12:16])[0]; num = struct.unpack('<I', data[man:man+4])[0]
off = man+4
found = {}
for i in range(num):
    dlen = struct.unpack_from('<I', data, off)[0]; dlen2 = struct.unpack_from('<I', data, off+4)[0]; doff = struct.unpack_from('<I', data, off+8)[0]
    nlen = struct.unpack_from('<H', data, off+12)[0]; nm = clean_name(data[off+16:off+16+nlen]); off += 16+nlen
    if not nm.lower().endswith('.iff') or dlen2 != dlen or doff >= len(data): continue
    body = data[doff:doff+dlen]; pos = 64
    while pos + 76 <= len(body):
        ct = body[pos:pos+4].decode('latin1','replace'); sz = struct.unpack_from('>I', body, pos+4)[0]-76; pos += 76
        if sz < 0 or pos+sz > len(body): break
        if ct == 'CARR':
            found.setdefault(nm, 0)
            found[nm] += 1
        pos += sz
print('CARR chunk files:', found if found else 'NONE')
