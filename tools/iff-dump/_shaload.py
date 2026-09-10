import hashlib
import os
base='tools/iff-dump/uigr-orig/'
for a,b in [('0010_1024x768.bmp','0539_1024x768.bmp'),('0011_800x600.bmp','0540_800x600.bmp'),('0010_1024x768.bmp','0011_800x600.bmp')]:
    h=[]
    for f in (a,b):
        p=os.path.join(base,f)
        d=open(p,'rb').read()
        h.append(hashlib.sha256(d).hexdigest()[:16])
        print(f, len(d), h[-1])
    print(a,'==',b,':',h[0]==h[1])
