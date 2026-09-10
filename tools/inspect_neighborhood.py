import struct

ID2_5 = b'IFF FILE 2.5:TYPE FOLLOWED BY SIZE JAMIE DOORNBOS & MAXIS 1'
ID2_0 = b'IFF FILE 2.0:TYPE FOLLOWED BY SIZE JAMIE DOORNBOS & MAXIS 1'

def read_cstr(buf, off, n):
    raw = buf[off:off+n].replace(b'\x00', b'')
    return raw.rstrip(b' \t'), off + n

path = '/Users/nathannoom/Documents/Simitone/UserData/Neighborhood.iff'
with open(path, 'rb') as f:
    buf = f.read()
ident, off = read_cstr(buf, 0, 60)
print('ident:', ident)
rsmp = struct.unpack_from('>I', buf, off)[0]; off += 4
print('rsmp:', rsmp)
types = {}
chunks = []
size = len(buf)
while size - off >= 2:
    ct = buf[off:off+4].decode('latin1'); off += 4
    csize = struct.unpack_from('>I', buf, off)[0]; off += 4
    cid = struct.unpack_from('>H', buf, off)[0]; off += 2
    cflags = struct.unpack_from('>H', buf, off)[0]; off += 2
    clabel, off = read_cstr(buf, off, 64)
    data_sz = csize - 76
    types[ct] = types.get(ct, 0) + 1
    chunks.append((ct, cid, clabel.decode('latin1','replace'), data_sz))
    off += data_sz
print('total chunks:', len(chunks), 'leftover:', size-off)
print('types:', types)
for ct in ['FAMI','CATS','HOUS']:
    fam = [c for c in chunks if c[0]==ct]
    print(ct, len(fam), fam[:10])
