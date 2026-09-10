#!/usr/bin/env python3
"""Build id -> constant -> artfile -> dimensions tables for every RT pair in UIGraphics.far.
Parses {TYPE,kId,"path"[,lang]} lines from .RT and `const Sint32 kX = N;` from .h.
Looks up art bytes in the FAR (case-insensitive, / vs \\) and reads BMP/TGA headers.
R142."""
import re, struct, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from far_manifest import parse_far, FAR

HERE = os.path.dirname(os.path.abspath(__file__))
data, entries = parse_far(FAR)
by_lower = {}
for e in entries:
    by_lower.setdefault(e["name"].lower().replace("\\", "/"), e)

def dims(e):
    d = data[e["dataOffset"]:e["dataOffset"]:][:0] or data[e["dataOffset"]:e["dataOffset"]+32]
    if d[:2] == b"BM":
        w, h = struct.unpack_from("<ii", d, 18)  # width@18 file-offset? see note
        # Actually for BMP: width is u32@18 from file start when including 14-byte
        # file header: BITMAPINFOHEADER starts at 14; width at 14+4=18, height 14+8=22.
        return w, h, "BMP"
    if len(d) >= 2 and d[1] in (0,1,2,3,9,10,11) and d[0] <= 8:
        # TGA: no signature unless v2 footer; width u16@12, height u16@14
        dd = data[e["dataOffset"]:e["dataOffset"]+18]
        w, h = struct.unpack_from("<HH", dd, 12)
        return w, h, "TGA"
    return None, None, d[:4].hex()

RE_RT = re.compile(r'\{(BITMAP|RLEBMP|BMPTGA|[A-Z]+)\s*,\s*(\w+)\s*,\s*"([^"]+)"\s*(?:,\s*(\w+))?\s*\}')
RE_H = re.compile(r'const\s+Sint32\s+(\w+)\s*=\s*(-?\d+)\s*;')

def parse_h(path):
    ids = {}
    for m in RE_H.finditer(open(path, encoding="latin-1").read()):
        ids[m.group(1)] = int(m.group(2))
    return ids

def parse_rt(path):
    out = []
    text = open(path, encoding="latin-1").read()
    for m in RE_RT.finditer(text):
        typ, const, art, lang = m.group(1), m.group(2), m.group(3), m.group(4)
        out.append((typ, const, art, lang))
    return out

RT_PAIRS = [
    ("SMCtrlMgrRes", "System control-manager chrome (dialogs, buttons, pie menu)"),
    ("Res_Other", "Misc in-game UI (speech balloons, logos, queue cancel)"),
    ("Res_Nbhd", "Neighborhood / sub-neighborhood screens"),
    ("Res_CPanel", "Live/BUY/BUILD control panel"),
]

results = {}
for stem, desc in RT_PAIRS:
    rt = os.path.join(HERE, "art", stem + ".RT")
    h = os.path.join(HERE, "art", stem + ".h")
    ids = parse_h(h)
    rows = []
    for typ, const, art, lang in parse_rt(rt):
        idval = ids.get(const)
        e = by_lower.get(art.lower())
        w = hh = None; fmt = "MISSING"; sz = None
        if e:
            w, hh, fmt = dims(e)
            sz = e["dataLen"]
        rows.append({"type": typ, "const": const, "id": idval, "art": art,
                     "w": w, "h": hh, "fmt": fmt, "size": sz, "lang": lang})
    results[stem] = {"desc": desc, "rows": rows}

# Report
out = []
for stem, info in results.items():
    out.append(f"\n### {stem} — {info['desc']}")
    out.append(f"{'ID':>6}  {'CONST':<34} {'TYPE':<7} {'ART':<50} {'WxH':<12} {'bytes':>8}  LANG")
    for r in sorted(info["rows"], key=lambda r: (r["id"] if r["id"] is not None else 99999)):
        wh = f"{r['w']}x{r['h']}" if r["w"] is not None else "?"
        out.append(f"{str(r['id']):>6}  {r['const']:<34} {r['type']:<7} {r['art']:<50} {wh:<12} {str(r['size'] or ''):>8}  {r['lang'] or ''}")
    miss = [r for r in info["rows"] if r["fmt"] == "MISSING"]
    if miss:
        out.append(f"  !! {len(miss)} MISSING: " + ", ".join(r["art"] for r in miss))
report = "\n".join(out)
open(os.path.join(HERE, "rt-inventory.txt"), "w").write(report)
print(report)
