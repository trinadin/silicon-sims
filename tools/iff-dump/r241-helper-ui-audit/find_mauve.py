#!/usr/bin/env python3
"""Scan every BMP member of UIGraphics.far for a texture whose colors match the
capture's mauve hole signature (mean ~ (109,91,117), std < 35)."""
import struct, io
import numpy as np
from PIL import Image

ROOT = "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/game-data/The Sims"
data = open(f"{ROOT}/UIGraphics/UIGraphics.far", "rb").read()
man = struct.unpack("<I", data[12:16])[0]
num = struct.unpack("<I", data[man:man+4])[0]
off = man + 4
target = np.array([109, 91, 117])
hits = []
members = []
for i in range(num):
    dlen, d2, doff, nlen = struct.unpack("<IIII", data[off:off+16])
    nm = data[off+16:off+16+nlen].decode("latin1", "replace")
    off += 16 + nlen
    members.append((nm, doff, dlen))
print(f"members: {len(members)}")
for nm, doff, dlen in members:
    blob = data[doff:doff+dlen]
    try:
        im = Image.open(io.BytesIO(blob)).convert("RGB")
    except Exception:
        continue
    a = np.array(im, dtype=np.int32)
    if a.shape[0] < 8 or a.shape[1] < 8:
        continue
    m = a.reshape(-1, 3).mean(axis=0)
    s = a.reshape(-1, 3).std(axis=0)
    if np.abs(m - target).max() < 18 and s.mean() < 40:
        hits.append((nm, a.shape[1], a.shape[0], tuple(m.round(0)), tuple(s.round(0))))
for h in hits:
    print("MATCH", h)
print(f"total matches: {len(hits)}")
