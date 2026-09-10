#!/usr/bin/env python3
"""Compare the blank buy-grid tile against the original ThumbTemplate frame art."""
import struct, io
import numpy as np
from PIL import Image

ROOT = "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/game-data/The Sims"
R240 = "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/build/ui-audit/r240"

# 1) find the ThumbTemplate art in the FAR
data = open(f"{ROOT}/UIGraphics/UIGraphics.far", "rb").read()
man = struct.unpack("<I", data[12:16])[0]
num = struct.unpack("<I", data[man:man+4])[0]
off = man + 4
frames = {}
for i in range(num):
    dlen, d2, doff, nlen = struct.unpack("<IIII", data[off:off+16])
    nm = data[off+16:off+16+nlen].decode("latin1", "replace")
    off += 16 + nlen
    ln = nm.lower()
    if "thumbtemplate" in ln:
        try:
            im = Image.open(io.BytesIO(data[doff:doff+dlen])).convert("RGB")
            frames[nm] = im
            print(f"{nm}: {im.size}")
        except Exception as e:
            print(f"{nm}: decode fail {e}")

# 2) the capture tile that is blank (default run, row0)
cap = np.array(Image.open(f"{R240}/default/uisurvey-buy.png").convert("RGB"), dtype=np.int32)
# grid geometry: locate by scanning the band for the frame borders; probe known tile boxes
# row0 y=676..721, pitch 47, x start 418 (12 cols) — refine: find the blank one by low inner std
blank_candidates = []
for c in range(12):
    x = 418 + c*47
    inner = cap[680:716, x+6:x+41]
    m = inner.reshape(-1,3).mean(axis=0); s = inner.reshape(-1,3).std(axis=0)
    blank_candidates.append((c, m.round(0), s.round(0)))
for c, m, s in blank_candidates:
    print(f"col {c}: mean={tuple(m)} std={tuple(s)}")

# 3) stat the original frame's inner region for comparison
for nm, im in frames.items():
    a = np.array(im, dtype=np.int32)
    h, w = a.shape[:2]
    cw = w // max(1, h) if w > h else 1
    fw = w // cw if cw else w
    frame0 = a[:, :fw if cw == 1 else a.shape[1]//cw]
    inner = frame0[8:fw-8 if fw < h else h-8, 8:fw-8]
    m = inner.reshape(-1,3).mean(axis=0); s = inner.reshape(-1,3).std(axis=0)
    print(f"{nm} frame0 inner: mean={tuple(m.round(0))} std={tuple(s.round(0))}")
