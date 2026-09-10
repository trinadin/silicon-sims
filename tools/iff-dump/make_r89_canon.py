#!/usr/bin/env python3
"""R89 IFF-LITERAL canon: the ORIGINAL neighborhood SCREEN members (Res_Nbhd.RT).

Engine-independent scan of the ORIGINAL UIGraphics.far (FAR1, byte-verbatim). As R88,
the member list is NOT hand-typed: this tool re-extracts the ORIGINAL template pair
(Res_Nbhd.h/.RT) from the FAR and parses its {BITMAP|RLEBMP,id,"path"} lines.

  screen (11)  the ORIGINAL neighborhood-screen backdrops + sub-layers + nessie:
               kNbhdBck (Nbhd\\NScreen.BMP), kNbhdBck_UL (Community\\NScreen_unleashed.bmp),
               kDowntownBackground (Downtown\\DScreen.bmp),
               kStudiotownBackground (Studiotown\\DScreen.bmp),
               kMagiclandBackground (Magicland\\DScreen.bmp),
               kVacationIsland (VIsland\\VIsland.bmp),
               kNghVacationPort (VIsland\\VIsland_port.bmp),
               kNghVacationTrees (VIsland\\VIsland_trees.bmp),
               kNess1-3 (Community\\ness01-03.bmp - the ORIGINAL nessie cameo family,
               RT-declared under Community/, i.e. the Unleashed Old-Town screen).

The 74 animated frame families these screens cycle are already pinned by R88
(uianim, make_r88_canon.py); R89 completes the LIVE cycling (see
UINeighbourhoodSelectionPanel + AutotestRunner 'uinbhd').

Emits:
  tools/iff-dump/r89/orig/                 byte-faithful member extracts (evidence)
  tools/iff-dump/r89/r89-resource-canon.json / .txt
  tools/iff-dump/r89/r89-screen-canon.cs   (rows typed R88ResourceCanon - shared row shape)
"""
import struct, hashlib, os, json, re

FAR = "game-data/The Sims/UIGraphics/UIGraphics.far"
OUT = "tools/iff-dump/r89"
ORIG = os.path.join(OUT, "orig")

SCREEN_IDS = ("kNbhdBck", "kNbhdBck_UL", "kDowntownBackground", "kStudiotownBackground",
              "kMagiclandBackground", "kVacationIsland", "kNghVacationPort",
              "kNghVacationTrees", "kNess1", "kNess2", "kNess3")


def read_far(path):
    data = open(path, "rb").read()
    man = struct.unpack("<I", data[12:16])[0]
    num = struct.unpack("<I", data[man:man + 4])[0]
    out = []
    off = man + 4
    for i in range(num):
        if off + 16 > len(data):
            break
        dlen, d2, doff, nlen = struct.unpack("<IIII", data[off:off + 16])
        nm = data[off + 16:off + 16 + nlen].decode("latin1", "replace")
        off += 16 + nlen
        if dlen != d2 or not (0 < doff < len(data) and doff + dlen <= len(data)):
            continue
        out.append((nm, dlen, doff))
    return data, out


def bmp_dims(b):
    if len(b) < 26 or b[0:2] != b"BM":
        return None
    w = struct.unpack("<i", b[18:22])[0]
    h = struct.unpack("<i", b[22:26])[0]
    return (w, abs(h))


def parse_rt(rt_text):
    rows = []
    for m in re.finditer(r"\{(BITMAP|RLEBMP)\s*,\s*(\w+)\s*,\s*\"([^\"]+)\"", rt_text):
        rows.append((m.group(1), m.group(2), m.group(3)))
    return rows


def main():
    os.makedirs(ORIG, exist_ok=True)
    data, es = read_far(FAR)
    bylower = {}
    for (n, d, do) in es:
        bylower[n.lower()] = (n, d, do)

    hit = bylower.get("res_nbhd.rt")
    if hit is None:
        raise SystemExit("template member missing: Res_Nbhd.RT")
    n, d, do = hit
    rt_text = data[do:do + d].decode("latin1", "replace")
    rows = parse_rt(rt_text)

    sel = [(rid, path) for (kind, rid, path) in rows if rid in SCREEN_IDS]
    if len(sel) != len(SCREEN_IDS):
        got = [r for (r, _) in sel]
        raise SystemExit("RT ids unresolved: %s" % [i for i in SCREEN_IDS if i not in got])

    out_rows, evid, missing, seen = [], [], [], set()
    for (rid, path) in sel:
        stored = path.replace("/", "\\")
        hit = bylower.get(stored.lower())
        if hit is None:
            missing.append(path)
            continue
        n, d, do = hit
        if n.lower() in seen:
            continue
        seen.add(n.lower())
        b = data[do:do + d]
        sha = hashlib.sha256(b).hexdigest()
        dims = bmp_dims(b) or (-1, -1)
        out_rows.append({"rid": rid, "name": n, "len": d, "sha256": sha,
                         "w": dims[0], "h": dims[1]})
        evid.append("SCREEN %s %s %d %s %dx%d" % (rid, n, d, sha, dims[0], dims[1]))
        safe = n.replace("/", "__").replace("\\", "__")
        open(os.path.join(ORIG, safe), "wb").write(b)

    if missing:
        raise SystemExit("MISSING MEMBERS: %s" % missing)

    with open(os.path.join(OUT, "r89-resource-canon.json"), "w") as f:
        json.dump({"screen": out_rows}, f, indent=1)
    with open(os.path.join(OUT, "r89-resource-canons.txt"), "w") as f:
        f.write("R89 IFF-LITERAL canon: original neighborhood SCREEN members (Res_Nbhd.RT).\n")
        f.write("Backdrops + vacation sub-layers + the kNess1-3 nessie family (%d members).\n" % len(out_rows))
        f.write("The cycled frame families themselves are pinned by R88 (uianim, 74).\n\n")
        for line in evid:
            f.write(line + "\n")

    with open(os.path.join(OUT, "r89-screen-canon.cs"), "w") as f:
        f.write("// R89 IFF-LITERAL canon: the ORIGINAL neighborhood SCREEN members from Res_Nbhd.RT -\n")
        f.write("// backdrops (kNbhdBck/_UL, kDowntown/kStudiotown/kMagiclandBackground, kVacationIsland),\n")
        f.write("// vacation sub-layers (kNghVacationPort/Trees) and the nessie family kNess1-3 (%d\n" % len(out_rows))
        f.write("// members), byte-verbatim. Rows reuse the R88ResourceCanon row shape.\n")
        f.write("using System;\n\n")
        f.write("public class R88ResourceCanon\n{\n    public string Name; public int Bytes; public string Sha256; public int W; public int H;\n")
        f.write("    public R88ResourceCanon(string name, int bytes, string sha256, int w, int h) { Name = name; Bytes = bytes; Sha256 = sha256; W = w; H = h; }\n}\n\n")
        f.write("public static readonly R88ResourceCanon[] R89ScreenCanon = new R88ResourceCanon[] {\n")
        for r in out_rows:
            f.write("            new R88ResourceCanon(\"%s\", %d, \"%s\", %d, %d),\n"
                    % (r["name"].replace("\\", "\\\\"), r["len"], r["sha256"], r["w"], r["h"]))
        f.write("        };\n")

    print("R89 canon: screen=%d extracted=%d" % (len(out_rows), len(out_rows)))


if __name__ == "__main__":
    main()
