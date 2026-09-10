from PIL import Image
im = Image.open('/Users/nathannoom/Developer/Games/The Sims/simitone-fork/tools/iff-dump/r68/r68-uidump-lot.png').convert('RGB')
w, h = im.size
print('size', im.size)
# bottom strip rows, column bands
rows = range(h-130, h, 6)
band = w // 16
for r in rows:
    out = []
    for c in range(16):
        acc=[0,0,0]; n=0
        for x in range(c*band, (c+1)*band):
            px=im.getpixel((x, r))
            acc[0]+=px[0]; acc[1]+=px[1]; acc[2]+=px[2]; n+=1
        acc=[a//max(n,1) for a in acc]
        out.append('#%02X%02X%02X' % tuple(acc))
    print(r, ' '.join(out))
# presence counts of IFF palette families
counts={'navy#000052':0,'steel#6F6F9C':0,'glower':0,'teal#0052':0}
for y in range(h-130, h):
    for x in range(0, w, 4):
        r,g,b = im.getpixel((x,y))
        if abs(r-0)<30 and abs(g-0x52)<30 and abs(b-0x52)<30: counts['navy#000052']+=1
        elif abs(r-0x6F)<40 and abs(g-0x6F)<40 and abs(b-0x9C)<40: counts['steel#6F6F9C']+=1
        elif r>0x40 and g>0x40 and b>0x60 and r>g: counts['glower']+=1
        elif r<0x20 and 0x30<g<0x70 and 0x30<b<0x80 and g>=b-10 and g<=b+10: counts['teal#0052']+=1
print('IFF-family counts (bottom strip):', counts)
