#!/usr/bin/env python3
"""R87 IFF-LITERAL canon: original UIGraphics.far .cur cursors + .ffn glyph-tables.

Engine-independent scan of the ORIGINAL UIGraphics.far (FAR1, byte-verbatim). The member
filenames stored in the FAR are the ORIGINAL game paths (Shared\\cursors\\*.cur, Fonts\\*.ffn),
so the canon keys ARE the runtime mount keys. Emits:

  tools/iff-dump/r87/orig/                    byte-faithful member extracts (evidence)
  tools/iff-dump/r87/r87-resource-canon.json  full canon (path, len, sha256, ico/fntf parse)
  tools/iff-dump/r87/r87-resource-canons.txt  human evidence
  tools/iff-dump/r87/r87-cursor-canon.cs      C# cursor canon (path, byte len, sha256)
  tools/iff-dump/r87/r87-glyph-canon.cs       C# glyph canon (path, byte len, sha256)

Nothing here reads the engine: byte-verbatim FAR1 manifest layout only (mirrors
tools/extract_uigr.py and the TS1 FAR1Provider mount).
"""
import struct, hashlib, os, json

FAR = "game-data/The Sims/UIGraphics/UIGraphics.far"
OUT = "tools/iff-dump/r87"
ORIG = os.path.join(OUT, "orig")


def read_far(path):
    # UIGraphics.far: member names are stored INLINE in the manifest (16-byte entry header +
    # nlen inline name, entry-by-entry). dlen == d2 marks a real data member; data is pointed
    # to by doff. Byte-verbatim extraction only.
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
        out.append((nm.replace("\\", "/"), dlen, doff))
    return data, out


def ico_parse(b, name):
    if len(b) < 22:
        return {"valid": False, "note": "too small"}
    reserved, typ, count = struct.unpack("<HHH", b[0:6])
    if reserved != 0 or typ != 2:
        return {"valid": False, "note": "reserved=%d type=%d" % (reserved, typ)}
    w, h, colors, r2, hx, hy, size, off = struct.unpack("<BBBBhhII", b[6:22])
    return {"valid": True, "count": count, "width": w, "height": h, "colors": colors,
            "hotspot_x": hx, "hotspot_y": hy, "image_size": size, "image_offset": off}


def main():
    os.makedirs(ORIG, exist_ok=True)
    data, es = read_far(FAR)
    curs = sorted((n, d, doff) for (n, d, doff) in es if n.lower().endswith(".cur"))
    ffns = sorted((n, d, doff) for (n, d, doff) in es if n.lower().endswith(".ffn"))

    cur_canon, ffn_canon, evid = [], [], []
    for (n, d, doff) in curs:
        b = data[doff:doff + d]
        sha = hashlib.sha256(b).hexdigest()
        ico = ico_parse(b, n)
        cur_canon.append({"name": n, "len": d, "sha256": sha, "ico": ico})
        evid.append("CUR %s %d %s %s" % (n, d, sha, json.dumps(ico)))
        safe = n.replace("/", "__")
        open(os.path.join(ORIG, "cur_" + safe), "wb").write(b)

    for (n, d, doff) in ffns:
        b = data[doff:doff + d]
        sha = hashlib.sha256(b).hexdigest()
        banner = b[0:4].decode("latin1", "replace") if len(b) >= 8 else ""
        size_le = struct.unpack("<I", b[4:8])[0] if len(b) >= 8 else -1
        size_be = struct.unpack(">I", b[4:8])[0] if len(b) >= 8 else -1
        ffn_canon.append({"name": n, "len": d, "sha256": sha, "banner": banner,
                          "size_le": size_le, "size_be": size_be})
        evid.append("FFN %s %d %s %s %d/%d" % (n, d, sha, banner, size_le, size_be))
        safe = n.replace("/", "__")
        open(os.path.join(ORIG, "ffn_" + safe), "wb").write(b)

    canon = {"cur": cur_canon, "ffn": ffn_canon}
    with open(os.path.join(OUT, "r87-resource-canon.json"), "w") as f:
        json.dump(canon, f, indent=1)
    with open(os.path.join(OUT, "r87-resource-canons.txt"), "w") as f:
        f.write("R87 IFF-LITERAL resource canon: original UIGraphics.far .cur/%d .ffn/%d (byte-verbatim).\n"
                % (len(cur_canon), len(ffn_canon)))
        for line in evid:
            f.write(line + "\n")

    def csharp_rows(rows):
        out = []
        for r in rows:
            # C# canon uses the raw FAR stored filename (backslashes) exactly like GoScreens/LiveChrome
            # so the check can match entry.FarEntry.Filename verbatim.
            csname = r["name"].replace("/", "\\\\")
            out.append("            new R87ResourceCanon(\"%s\", %d, \"%s\")," % (csname, r["len"], r["sha256"]))
        return "\n".join(out)

    with open(os.path.join(OUT, "r87-cursor-canon.cs"), "w") as f:
        f.write("// R87 IFF-LITERAL canon: original UIGraphics.far .cur cursors (%d), byte-verbatim.\n" % len(cur_canon))
        f.write("// (raw FAR stored name, byte length, sha256) = the ORIGINAL game cursor-path member bytes.\n")
        f.write("// IFF-first live mount must load exactly these member bytes. Text render-style residual unchanged.\n")
        f.write("using System;\n\n")
        f.write("public class R87ResourceCanon\n{\n    public string Name; public long Bytes; public string Sha256;\n")
        f.write("    public R87ResourceCanon(string name, long bytes, string sha256) { Name = name; Bytes = bytes; Sha256 = sha256; }\n}\n\n")
        f.write("public static readonly R87ResourceCanon[] R87CursorCanon = new R87ResourceCanon[] {\n")
        f.write(csharp_rows(cur_canon))
        f.write("        };\n")
    with open(os.path.join(OUT, "r87-glyph-canon.cs"), "w") as f:
        f.write("// R87 IFF-LITERAL canon: original UIGraphics.far .ffn glyph-tables (%d), byte-verbatim (FNTF banner).\n" % len(ffn_canon))
        f.write("// (raw FAR stored name, byte length, sha256) = the ORIGINAL glyph-table member bytes mounted 1:1.\n")
        f.write("using System;\n\n")
        f.write("public class R87ResourceCanon\n{\n    public string Name; public long Bytes; public string Sha256;\n")
        f.write("    public R87ResourceCanon(string name, long bytes, string sha256) { Name = name; Bytes = bytes; Sha256 = sha256; }\n}\n\n")
        f.write("public static readonly R87ResourceCanon[] R87GlyphCanon = new R87ResourceCanon[] {\n")
        f.write(csharp_rows(ffn_canon))
        f.write("        };\n")

    # IFF-first cursor map: which original .cur member backs each engine CursorType
    curmap = {}
    for r in cur_canon:
        curmap[r["name"].rsplit("/", 1)[-1].lower()] = r["name"]
    with open(os.path.join(OUT, "r87-cursor-map.cs"), "w") as f:
        f.write("// R87 IFF-first cursor map: CursorType filename (lower) -> original UIGraphics.far member path.\n")
        for k in sorted(curmap):
            f.write("  map[%r] = %r; // %d bytes\n" % (k, curmap[k], [r["len"] for r in cur_canon if r["name"] == curmap[k]][0]))

    print("R87 canon: cur=%d ffn=%d extracted=%d" % (len(cur_canon), len(ffn_canon), len(cur_canon) + len(ffn_canon)))


if __name__ == "__main__":
    main()
