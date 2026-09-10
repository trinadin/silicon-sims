from PIL import Image
im = Image.open('/Users/nathannoom/Developer/Games/The Sims/simitone-fork/tools/iff-dump/r67/r67-uidump-lot.png').convert('RGB')
w, h = im.size
print('size', im.size)
# classify bottom region rows -> column bands
rows = range(h-140, h)
cols = 16
band = w // cols
for r in rows:
    if r % 7: continue
    out = []
    for c in range(cols):
        # mean of the band
        acc = [0,0,0]
        n = 0
        for x in range(c*band, (c+1)*band):
            px = im.getpixel((x, r))
            acc[0]+=px[0]; acc[1]+=px[1]; acc[2]+=px[2]; n+=1
        acc = [a//n for a in acc]
        out.append('#%02X%02X%02X' % tuple(acc))
    print(r, ' '.join(out))
