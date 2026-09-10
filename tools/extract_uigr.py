import struct, os, re
SRC='game-data/The Sims/UIGraphics/UIGraphics.far'
OUT='tools/iff-dump/uigr-orig'
with open(SRC,'rb') as f: data=f.read()
man=struct.unpack('<I', data[12:16])[0]
num=struct.unpack('<I', data[man:man+4])[0]
os.makedirs(OUT, exist_ok=True)
off=man+4
ok=0
for i in range(num):
    if off+16>len(data): break
    dlen,d2,doff,nlen = struct.unpack('<IIII', data[off:off+16])
    nm=data[off+16:off+16+nlen].decode('latin1','replace')
    off += 16+nlen
    if dlen!=d2 or not (0<doff<len(data) and doff+dlen<=len(data)):
        continue
    real = nm.replace(chr(92), '_').split('/')[-1].split('_')[-1]
    path = os.path.join(OUT, '%04d_%s' % (i, real.replace(' ', '_')))
    with open(path,'wb') as g:
        g.write(data[doff:doff+dlen]); ok+=1
print('extracted', ok, 'of', num)