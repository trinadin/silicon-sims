#!/usr/bin/env python3
"""Extract all non-cpanel, non-font art from UIGraphics.far into r142/art/.
(cpanel was r141's domain; fonts are not chrome.) R142."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from far_manifest import parse_far, FAR

OUT = "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/tools/iff-dump/r142/art"
data, entries = parse_far(FAR)
os.makedirs(OUT, exist_ok=True)
n = 0
for e in entries:
    name = e["name"]
    low = name.lower()
    if low.startswith("cpanel\\") or low.startswith("fonts\\") or low.startswith("res_") or low.startswith("smctrlmgrres"):
        continue
    out = os.path.join(OUT, name.replace("\\", "_"))
    with open(out, "wb") as f:
        f.write(data[e["dataOffset"]:e["dataOffset"]+e["dataLen"]])
    n += 1
print(f"extracted {n} files to {OUT}")
