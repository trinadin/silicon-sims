import struct, os, sys, collections
def bmp_palette(path):
    d = open(path,'rb').read()
    if d[:2]!=b'BM': return None
    off = struct.unpack('<I', d[10:14])[0]
    bpp = struct.unpack('<H', d[28:30])[0]
    w = struct.unpack('<i', d[18:22])[0]
    h = struct.unpack('<i', d[22:26])[0]
    if bpp > 8: return None
    ncolors = 1 << bpp
    pal = []
    for i in range(ncolors):
        b,g,r,res = struct.unpack('<BBBB', d[54+4*i:58+4*i])
        pal.append((r,g,b))
    rowsize = ((w*bpp+31)//32)*4
    cnt = collections.Counter()
    for y in range(abs(h)):
        row = off + y*rowsize
        if row + rowsize > len(d): break
        rowbytes = d[row:row+rowsize]
        for x in range(w):
            if bpp==8:
                idx = rowbytes[x]
            elif bpp==4:
                idx = (rowbytes[x//2]>>(((x%2)*4)))&0xF
            else:
                idx = (rowbytes[x//8]>>(7-(x%8)))&1
            if idx < ncolors: cnt[pal[idx]]+=1
    return pal, cnt, (w,abs(h),bpp)
for f in sys.argv[1:]:
    r = bmp_palette(f)
    if not r: print(os.path.basename(f), 'no-pal-bmp'); continue
    pal, cnt, dim = r
    top = cnt.most_common(12)
    print(os.path.basename(f), dim, 'pixels', len(cnt), '; top:', ', '.join('#%02X%02X%02X x%d' % (c[0],c[1],c[2],n) for c,n in top))