import struct, os, io
from PIL import Image
SRC='game-data/The Sims/UIGraphics/UIGraphics.far'
OUTPNG='Client/Simitone/Simitone.Client/Content/uigraphics/live'
WANT = {
  'cpanel\\Buttons\\Mood.bmp': 'btn_mood.png',
  'cpanel\\Buttons\\Job.bmp': 'btn_job.png',
  'cpanel\\Buttons\\Personality.bmp': 'btn_personality.png',
  'cpanel\\Buttons\\Relationship.BMP': 'btn_relationship.png',
  'cpanel\\Buttons\\people.bmp': 'btn_people.png',
  'cpanel\\Greenbars.BMP': 'bars_green.png',
  'cpanel\\Redbars.bmp': 'bars_red.png',
}
data=open(SRC,'rb').read()
man=struct.unpack('<I', data[12:16])[0]
num=struct.unpack('<I', data[man:man+4])[0]
off=man+4
os.makedirs(OUTPNG, exist_ok=True)
for i in range(num):
    dlen,d2,doff,nlen = struct.unpack('<IIII', data[off:off+16])
    nm=data[off+16:off+16+nlen].decode('latin1','replace')
    off += 16+nlen
    if nm not in WANT: continue
    payload=data[doff:doff+dlen]
    im=Image.open(io.BytesIO(payload)).convert('RGBA')
    px=im.load()
    for y in range(im.height):
        for x in range(im.width):
            r,g,b,a=px[x,y]
            if (r,g,b)==(255,0,255): px[x,y]=(0,0,0,0)
    out=os.path.join(OUTPNG, WANT[nm])
    im.save(out, 'PNG')
    print('wrote', out, im.width, 'x', im.height)
