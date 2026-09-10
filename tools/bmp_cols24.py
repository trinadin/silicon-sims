import struct, os, sys, collections
def bmp_cols(path):
    d = open(path,'rb').read()
    if d[:2]!=b'BM': return None
    off = struct.unpack('<I', d[10:14])[0]
    bpp = struct.unpack('<H', d[28:30])[0]
    w = struct.unpack('<i', d[18:22])[0]
    h = struct.unpack('<i', d[22:26])[0]
    cnt = collections.Counter()
    if bpp == 24:
        rowsize = ((w*24+31)//32)*4
        for y in range(abs(h)):
            row = off + y*rowsize
            if row + rowsize > len(d): break
            rb = d[row:row+rowsize]
            for x in range(w):
                b,g,r = rb[x*3:x*3+3]
                cnt[(r,g,b)] += 1
        return cnt, (w,abs(h),bpp)
    return None
for f in sys.argv[1:]:
    r = bmp_cols(f)
    if not r: print(os.path.basename(f), 'unparsed'); continue
    cnt, dim = r
    print(os.path.basename(f), dim, 'colors', len(cnt), '; top:', ', '.join('#%02X%02X%02X x%d' % (c[0],c[1],c[2],n) for c,n in cnt.most_common(14)))