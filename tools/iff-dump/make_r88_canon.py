#!/usr/bin/env python3
"""R88 IFF-LITERAL canon: the ORIGINAL loading-screen LOGO + ANIMATION families.

Engine-independent scan of the ORIGINAL UIGraphics.far (FAR1, byte-verbatim). The member
lists are NOT hand-typed: this tool extracts the ORIGINAL resource templates
(Res_Other.h/.RT, Res_Nbhd.h/.RT) from the FAR itself and parses their {BITMAP}/{RLEBMP}
id->path lines, so every R88 pin provably comes from the original template:

  boot (19)  kSimsLogo (Res_Other.h id 9003) -> Other/setup.bmp + 18 language variants
             = the ORIGINAL full-screen boot/logo screen (plumbob + wordmark art).
  logo (7)   kNghSimsLogoEn/SP/FR/GR/JP (Res_Nbhd.h 5421/5427-5430) + kDowntownCredits +
             kStudiotownCredits -> the original "The Sims" logo stamps.
  anim (74)  the ORIGINAL animated frame families (Res_Nbhd.RT):
             Unleashed neighborhood waves kNghFrameDelta1-6_UL + NonUS (12),
             TS1.0 neighborhood frame-deltas kNghFrameDelta1-3 + Loc (6),
             Downtown water kDowntownWater0-5 (6), Magicland water kMagictownWater0-5 (6),
             Magicland fog kMagictownFogA/B/C (3 tga), Magicland balloon 1-16 (16),
             Studiotown car kStudiotownCar0-19 (20), Vacation water kVIWater0-4 (5).

Emits:
  tools/iff-dump/r88/templates/            byte-verbatim template member extracts (evidence)
  tools/iff-dump/r88/orig/                 byte-faithful member extracts (evidence)
  tools/iff-dump/r88/r88-resource-canon.json / .txt
  tools/iff-dump/r88/r88-boot-canon.cs / r88-logo-canon.cs / r88-anim-canon.cs
"""
import struct, hashlib, os, json, re

FAR = "game-data/The Sims/UIGraphics/UIGraphics.far"
OUT = "tools/iff-dump/r88"
ORIG = os.path.join(OUT, "orig")
TPL = os.path.join(OUT, "templates")

ANIM_PREFIXES = ("kNghFrameDelta", "kDowntownWater", "kMagictownWater", "kMagictownFog",
                 "kMagictownBalloon", "kStudiotownCar", "kVIWater")


def read_far(path):
    # UIGraphics.far: member names INLINE in the manifest (16-byte entry header + nlen inline
    # name). dlen == d2 marks a real data member; data at doff. Byte-verbatim only.
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


def tga_dims(b):
    # uncompressed/certain TGA: width @12, height @14 (LE u16)
    if len(b) < 18 or b[2] not in (0, 1, 2, 3, 10, 11):
        return None
    w = struct.unpack("<H", b[12:14])[0]
    h = struct.unpack("<H", b[14:16])[0]
    return (w, h)


def parse_rt(rt_text):
    # {BITMAP,id,"path"} and {RLEBMP,id,"path"[, kRes_LanguageXxx]}
    rows = []
    for m in re.finditer(r"\{(BITMAP|RLEBMP)\s*,\s*(\w+)\s*,\s*\"([^\"]+)\"", rt_text):
        rows.append((m.group(1), m.group(2), m.group(3)))
    return rows


def main():
    os.makedirs(ORIG, exist_ok=True)
    os.makedirs(TPL, exist_ok=True)
    data, es = read_far(FAR)
    bylower = {}
    for (n, d, do) in es:
        bylower[n.lower()] = (n, d, do)

    # 1) extract the original resource templates from the FAR (byte-verbatim evidence)
    tpl_rows = {}
    for tpl_name in ["Res_Other.h", "Res_Other.RT", "Res_Nbhd.h", "Res_Nbhd.RT"]:
        hit = bylower.get(tpl_name.lower())
        if hit is None:
            raise SystemExit("template member missing: " + tpl_name)
        n, d, do = hit
        raw = data[do:do + d]
        open(os.path.join(TPL, tpl_name), "wb").write(raw)
        tpl_rows[tpl_name] = raw.decode("latin1", "replace")

    # 2) derive the member lists from the ORIGINAL templates (not hand-typed)
    other_rows = parse_rt(tpl_rows["Res_Other.RT"])
    nbhd_rows = parse_rt(tpl_rows["Res_Nbhd.RT"])

    def pick(rows, pred):
        out = []
        for (kind, rid, path) in rows:
            if pred(rid):
                out.append((rid, path))
        return out

    boot_ids = pick(other_rows, lambda rid: rid == "kSimsLogo")
    logo_ids = (pick(nbhd_rows, lambda rid: rid.startswith("kNghSimsLogo")) +
                pick(nbhd_rows, lambda rid: rid in ("kDowntownCredits", "kStudiotownCredits")))
    anim_ids = pick(nbhd_rows, lambda rid: rid.startswith(ANIM_PREFIXES))

    def canon_for(sel, label):
        rows, evid, missing = [], [], []
        seen = set()
        for (rid, path) in sel:
            stored = path.replace("/", "\\")
            hit = bylower.get(stored.lower())
            if hit is None:
                missing.append(path)
                continue
            n, d, do = hit
            key = n.lower()
            if key in seen:  # RT repeats (locale lines) still pin the same member once
                continue
            seen.add(key)
            b = data[do:do + d]
            sha = hashlib.sha256(b).hexdigest()
            dims = bmp_dims(b) or tga_dims(b) or (-1, -1)
            rows.append({"rid": rid, "name": n, "len": d, "sha256": sha,
                         "w": dims[0], "h": dims[1]})
            evid.append("%s %s %s %d %s %dx%d" % (label, rid, n, d, sha, dims[0], dims[1]))
            safe = n.replace("/", "__").replace("\\", "__")
            open(os.path.join(ORIG, safe), "wb").write(b)
        return rows, evid, missing

    boot, boot_evid, boot_miss = canon_for(boot_ids, "BOOT")
    logo, logo_evid, logo_miss = canon_for(logo_ids, "LOGO")
    anim, anim_evid, anim_miss = canon_for(anim_ids, "ANIM")

    if boot_miss or logo_miss or anim_miss:
        raise SystemExit("MISSING MEMBERS: %s %s %s" % (boot_miss, logo_miss, anim_miss))

    canon = {"boot": boot, "logo": logo, "anim": anim}
    with open(os.path.join(OUT, "r88-resource-canon.json"), "w") as f:
        json.dump(canon, f, indent=1)
    with open(os.path.join(OUT, "r88-resource-canons.txt"), "w") as f:
        f.write("R88 IFF-LITERAL canon: original loading-screen LOGO + ANIMATION families.\n")
        f.write("Member lists parsed from the ORIGINAL templates Res_Other.RT / Res_Nbhd.RT\n")
        f.write("(themselves extracted byte-verbatim from UIGraphics.far into templates/).\n")
        f.write("boot(kSimsLogo)=%d logo=%d anim=%d\n\n" % (len(boot), len(logo), len(anim)))
        for line in boot_evid + logo_evid + anim_evid:
            f.write(line + "\n")

    def csharp_rows(rows):
        out = []
        for r in rows:
            csname = r["name"].replace("\\", "\\\\")
            out.append("            new R88ResourceCanon(\"%s\", %d, \"%s\", %d, %d),"
                       % (csname, r["len"], r["sha256"], r["w"], r["h"]))
        return "\n".join(out)

    def write_cs(fname, arrname, rows, banner):
        with open(os.path.join(OUT, fname), "w") as f:
            f.write(banner + "\n")
            f.write("// (raw FAR stored name, byte length, sha256, BMP/TGA dims) - IFF-literal mount keys.\n")
            f.write("using System;\n\n")
            f.write("public class R88ResourceCanon\n{\n    public string Name; public int Bytes; public string Sha256; public int W; public int H;\n")
            f.write("    public R88ResourceCanon(string name, int bytes, string sha256, int w, int h) { Name = name; Bytes = bytes; Sha256 = sha256; W = w; H = h; }\n}\n\n")
            f.write("public static readonly R88ResourceCanon[] %s = new R88ResourceCanon[] {\n" % arrname)
            f.write(csharp_rows(rows))
            f.write("\n        };\n")

    write_cs("r88-boot-canon.cs", "R88BootCanon", boot,
             "// R88 IFF-LITERAL canon: kSimsLogo (Res_Other.RT id 9003) - the ORIGINAL full-screen boot/logo\n// screen, Other\\setup.bmp + 18 language variants (%d), byte-verbatim." % len(boot))
    write_cs("r88-logo-canon.cs", "R88LogoCanon", logo,
             "// R88 IFF-LITERAL canon: the ORIGINAL 'The Sims' logo stamps - kNghSimsLogoEn/SP/FR/GR/JP +\n// kDowntownCredits + kStudiotownCredits (%d members), byte-verbatim." % len(logo))
    write_cs("r88-anim-canon.cs", "R88AnimCanon", anim,
             "// R88 IFF-LITERAL canon: the ORIGINAL animated frame families from Res_Nbhd.RT - Unleashed\n// waves + NonUS, TS1.0 frame-deltas + Loc, Downtown/Magicland/Vacation water, Magicland fog +\n// balloon, Studiotown car (%d frames), byte-verbatim." % len(anim))

    print("R88 canon: boot=%d logo=%d anim=%d templates=4 extracted=%d"
          % (len(boot), len(logo), len(anim), len(boot) + len(logo) + len(anim)))


if __name__ == "__main__":
    main()
