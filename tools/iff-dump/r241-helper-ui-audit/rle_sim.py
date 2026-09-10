#!/usr/bin/env python3
"""Faithful Python simulation of FreeSO BmpRLE8.TryDecode on the real member.
Compare against PIL (faithful RLE8) decode; identify corrupted regions and test
whether the simulation reproduces the capture's mauve window."""
import struct, io
import numpy as np
from PIL import Image

ROOT = "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/game-data/The Sims"
path = f"{ROOT}/UIGraphics/UIGraphics.far"
data = open(path, "rb").read()
man = struct.unpack("<I", data[12:16])[0]
num = struct.unpack("<I", data[man:man+4])[0]
off = man + 4
blob = None
for i in range(num):
    dlen, d2, doff, nlen = struct.unpack("<IIII", data[off:off+16])
    nm = data[off+16:off+16+nlen].decode("latin1", "replace")
    off += 16 + nlen
    if nm.lower() == "cpanel\\backgrounds\\createacharback.bmp":
        blob = data[doff:doff+dlen]
        break
assert blob

bfType = blob[0:2]
bfSize = struct.unpack("<I", blob[2:6])[0]
res1 = struct.unpack("<H", blob[6:8])[0]
res2 = struct.unpack("<H", blob[8:10])[0]
bfOffBits = struct.unpack("<I", blob[10:14])[0]
print(f"type={bfType} bfSize={bfSize} res=({res1},{res2}) bfOffBits={bfOffBits} memberLen={len(blob)}")

size, w, h, planes, bpp, compression, imageSize, xppm, yppm, clrUsed, clrImp = struct.unpack("<IiiHHIIiiII", blob[14:54])
print(f"DIB {w}x{h} bpp={bpp} compression={compression} clrUsed={clrUsed}")
pal_off = 14 + size
pal = np.zeros((256, 4), dtype=np.uint8)
for i in range(256):
    if i < clrUsed:
        b, g, r, _ = blob[pal_off+i*4: pal_off+i*4+4]
        pal[i] = (r, g, b, 255)

def decode_rle8(buf, off_bits, w, h, pad_law):
    """pad_law: 'bmp' = correct (pad if n odd); 'fsoparity' = pad if stream pos p odd (BmpRLE8.cs law)."""
    pixels = bytearray(w*h)
    row, col = h-1, 0
    p = off_bits
    end = len(buf)
    while p + 1 < end:
        count = buf[p]; value = buf[p+1]; p += 2
        if count != 0:
            for i in range(count):
                if col >= w or row < 0 or row >= h: break
                pixels[row*w+col] = value; col += 1
        else:
            if value == 0: col = 0; row -= 1
            elif value == 1: break
            elif value == 2:
                dx, dy = buf[p], buf[p+1]; p += 2
                col += dx; row -= dy
            else:
                n = value
                take = min(n, w - col)
                start_idx = row*w+col
                if take > 0 and 0 <= row < h and start_idx >= 0:
                    avail = min(take, len(buf)-p, w*h-start_idx)
                    if avail > 0:
                        pixels[start_idx:start_idx+avail] = buf[p:p+avail]
                col += max(0, take)
                p += n
                if pad_law == "fsoparity":
                    if (p & 1) == 1: p += 1
                else:
                    if n & 1: p += 1
        if row < 0 or row >= h: break
    return np.frombuffer(bytes(pixels), dtype=np.uint8).reshape(h, w)

sim = decode_rle8(blob, bfOffBits, w, h, "fsoparity")
pil = np.array(Image.open(io.BytesIO(blob)).convert("RGB"), dtype=np.int32)

sim_rgb = pal[sim][:, :, :3].astype(np.int32)
diff = np.abs(sim_rgb - pil).mean(axis=2)
print(f"sim-vs-faithful mean diff = {diff.mean():.1f}")
bad = diff > 25
print(f"pixels differing >25: {bad.mean()*100:.1f}%")
ys, xs = np.where(bad)
if len(ys):
    print(f"differing region bbox: x {xs.min()}..{xs.max()} y {ys.min()}..{ys.max()}")

# Does the simulated (BmpRLE8-law) palette render reproduce the capture's hole?
CAP = "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/build/ui-audit/r240/default/uisurvey-casorig-cac.png"
cap = np.array(Image.open(CAP).convert("RGB"), dtype=np.int32)[:600, :800]
hole_sim = sim_rgb[155:355, 628:708].reshape(-1, 3).mean(axis=0)
hole_cap = cap[155:355, 628:708].reshape(-1, 3).mean(axis=0)
hole_pil = pil[155:355, 628:708].reshape(-1, 3).mean(axis=0)
print(f"hole sim(BmpRLE8 law) = ({hole_sim[0]:.0f},{hole_sim[1]:.0f},{hole_sim[2]:.0f})")
print(f"hole capture          = ({hole_cap[0]:.0f},{hole_cap[1]:.0f},{hole_cap[2]:.0f})")
print(f"hole faithful(PIL)    = ({hole_pil[0]:.0f},{hole_pil[1]:.0f},{hole_pil[2]:.0f})")
d1 = np.abs(sim_rgb.astype(int) - cap[:, :, :]).mean()
d2 = np.abs(pil - cap).mean()
print(f"whole-art: |sim-capture|={d1:.1f}  |faithful-capture|={d2:.1f}")
