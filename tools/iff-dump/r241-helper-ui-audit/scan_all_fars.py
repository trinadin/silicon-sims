#!/usr/bin/env python3
"""Scan every FAR in the corpus for CreateACharBack variants; stat their vita-window color."""
import struct, io, glob, os
from PIL import Image
import numpy as np

ROOT = "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/game-data/The Sims"

def far_members(path):
    try:
        data = open(path, "rb").read()
    except Exception as e:
        return
    if data[:8] != b"FAR!byAZ":
        return
    man = struct.unpack("<I", data[12:16])[0]
    num = struct.unpack("<I", data[man:man+4])[0]
    off = man + 4
    for i in range(num):
        dlen, d2, doff, nlen = struct.unpack("<IIII", data[off:off+16])
        nm = data[off+16:off+16+nlen].decode("latin1", "replace")
        off += 16 + nlen
        yield nm, data[doff:doff+dlen]

WANT = "createacharback"
for far in glob.glob(f"{ROOT}/**/*.far", recursive=True) + glob.glob(f"{ROOT}/**/*.FAR", recursive=True):
    try:
        for nm, blob in far_members(far):
            if WANT in nm.lower():
                try:
                    im = Image.open(io.BytesIO(blob)).convert("RGB")
                    a = np.array(im, dtype=np.int32)
                    h, w = a.shape[:2]
                    tag = ""
                    if (w, h) == (800, 600):
                        r = a[155:355, 628:708].reshape(-1, 3)
                        m = r.mean(axis=0)
                        tag = f" vita-window=({m[0]:.0f},{m[1]:.0f},{m[2]:.0f})"
                    print(f"{os.path.relpath(far, ROOT)} :: {nm} {w}x{h}{tag}")
                except Exception as e:
                    print(f"{os.path.relpath(far, ROOT)} :: {nm} {len(blob)}B decode-fail {e}")
    except Exception as e:
        print(f"{far}: scan EXC {e}")
