#!/usr/bin/env python3
"""Inspect the on-disk BMP encoding of CreateACharBack.BMP (palette + compression)."""
import struct

ROOT = "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/game-data/The Sims"
path = f"{ROOT}/UIGraphics/UIGraphics.far"
data = open(path, "rb").read()
man = struct.unpack("<I", data[12:16])[0]
num = struct.unpack("<I", data[man:man+4])[0]
off = man + 4
for i in range(num):
    dlen, d2, doff, nlen = struct.unpack("<IIII", data[off:off+16])
    nm = data[off+16:off+16+nlen].decode("latin1", "replace")
    off += 16 + nlen
    if nm.lower() == "cpanel\\backgrounds\\createacharback.bmp":
        blob = data[doff:doff+dlen]
        break

print(f"member size={len(blob)}")
hdr = struct.unpack("<IiiHHIIiiII", blob[:40])
bfSize, res, res2, bfOffBits = hdr[0], hdr[1], hdr[2], hdr[3]
size, w, h, planes, bpp, compression, imageSize, xppm, yppm, clrUsed, clrImportant = struct.unpack("<IiiHHIIiiII", blob[14:54])
print(f"bfOffBits={bfOffBits} DIB size={size} {w}x{h} planes={planes} bpp={bpp} compression={compression} imageSize={imageSize} clrUsed={clrUsed} clrImportant={clrImportant}")
# compression: 0=BI_RGB, 1=BI_RLE8
npal = clrUsed if clrUsed else (1 << bpp)
print(f"palette entries={npal}")
pal_off = 14 + size
pal = []
for i in range(npal):
    b, g, r, resv = blob[pal_off+i*4: pal_off+i*4+4]
    pal.append((r, g, b))
# what indices does the window region use? decode RLE8 if compression==1
if compression == 1:
    print("BI_RLE8 confirmed; pixel data at", bfOffBits)
    px = bfOffBits
    width, height = w, h
    rows = {}
    y = height - 1  # bottom-up
    x = 0
    idxhist = {}
    end = len(blob)
    while px < end and y >= 0:
        c0 = blob[px]; c1 = blob[px+1]; px += 2
        if c0 > 0:
            for k in range(c0):
                if x < width:
                    idxhist.setdefault(blob[px+k] if False else 0, None)
                # record index
                rows.setdefault(y, {})[x+k] = None
            x += c0
        else:
            mode = c1
            if mode == 0:
                y -= 1; x = 0
            elif mode == 1:
                break
            elif mode == 2:
                dx, dy = struct.unpack("<BB", blob[px:px+2]); px += 2
                x += dx; y -= dy
            else:
                n = mode
                for k in range(n):
                    if x+k < width:
                        idx = blob[px+k]
                        idxhist[idx] = idxhist.get(idx, 0) + 1
                px += n + ((n % 4) and (4 - n % 4) or 0)
                x += n
    print("distinct absolute-mode indices:", len(idxhist))
    top = sorted(idxhist.items(), key=lambda t: -t[1])[:12]
    print("top absolute indices (idx,count):", top)
    print("their palette colors:", [(i, pal[i]) for i, _ in top if i < len(pal)])
    # indices used by window region can't be isolated without full decode; dump sample rows
