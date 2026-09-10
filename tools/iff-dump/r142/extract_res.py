#!/usr/bin/env python3
"""Extract Res_*.RT / Res_*.h members from UIGraphics.far into r142/art/. R142."""
import os
from far_manifest import parse_far, FAR

OUT = "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/tools/iff-dump/r142/art"

data, entries = parse_far(FAR)
os.makedirs(OUT, exist_ok=True)
for e in entries:
    n = e["name"]
    if n.lower().startswith("res_"):
        out = os.path.join(OUT, n.replace("\\", "_"))
        with open(out, "wb") as f:
            f.write(data[e["dataOffset"]:e["dataOffset"]+e["dataLen"]])
        print(f"extracted {n} -> {out} ({e['dataLen']} bytes)")
