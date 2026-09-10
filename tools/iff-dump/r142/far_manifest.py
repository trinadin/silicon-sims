#!/usr/bin/env python3
"""Parse UIGraphics.far manifest and list all members. R142."""
import struct, sys, json

FAR = "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/game-data/The Sims/UIGraphics/UIGraphics.far"

def parse_far(path):
    with open(path, "rb") as f:
        data = f.read()
    manifest_off = struct.unpack_from("<I", data, 12)[0]
    count = struct.unpack_from("<I", data, manifest_off)[0]
    entries = []
    p = manifest_off + 4
    for i in range(count):
        dlen, d2, doff, nlen = struct.unpack_from("<IIII", data, p)
        p += 16
        name = data[p:p+nlen].decode("latin-1")
        p += nlen
        entries.append({"name": name, "dataLen": dlen, "d2": d2, "dataOffset": doff})
    return data, entries

if __name__ == "__main__":
    data, entries = parse_far(FAR)
    print(f"total members: {len(entries)}")
    with open("/Users/nathannoom/Developer/Games/The Sims/simitone-fork/tools/iff-dump/r142/manifest.txt", "w") as f:
        for e in sorted(entries, key=lambda x: x["name"].lower()):
            f.write(f"{e['name']}\t{e['dataLen']}\t{e['dataOffset']}\n")
    # Res_ files
    res = [e for e in entries if e["name"].lower().startswith("res_")]
    print(f"Res_* members: {len(res)}")
    for e in sorted(res, key=lambda x: x["name"].lower()):
        print(f"  {e['name']}  len={e['dataLen']}")
