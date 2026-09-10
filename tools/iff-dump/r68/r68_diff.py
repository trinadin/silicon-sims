from PIL import Image
old = Image.open('/Users/nathannoom/Developer/Games/The Sims/simitone-fork/tools/iff-dump/r67/r67-uidump-lot.png').convert('RGB')
new = Image.open('/Users/nathannoom/Developer/Games/The Sims/simitone-fork/tools/iff-dump/r68/r68-uidump-lot.png').convert('RGB')
# compare bottom-strip region (y 625..768) tile-by-tile
changed = 0; total = 0; maxd = 0
for y in range(625, 768, 2):
    for x in range(0, 1024, 2):
        a = old.getpixel((x,y)); b = new.getpixel((x,y))
        d = sum(abs(u-v) for u,v in zip(a,b))
        total += 1
        if d > 24: changed += 1
        maxd = max(maxd, d)
print('bottom-strip changed px:', changed, '/', total, 'maxdelta', maxd)
# save crops for vision: left glimmer region and full bottom strip
new.crop((0, 620, 640, 768)).save('/Users/nathannoom/Developer/Games/The Sims/simitone-fork/tools/iff-dump/r68/r68-strip-left.png')
new.crop((0, 620, 1024, 768)).save('/Users/nathannoom/Developer/Games/The Sims/simitone-fork/tools/iff-dump/r68/r68-strip-full.png')
print('crops saved')
