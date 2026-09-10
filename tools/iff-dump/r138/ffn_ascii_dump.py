#!/usr/bin/env python3
"""R138: independent .ffn decoder + ASCII-art renderer.

Renders sample strings under the PORT's exact interpretation
(Simitone.Client OriginalGlyphFont.FromFFN / Advance / Draw):

  header: u32@20 = charTableOffset, u32@28 = shapeOffset
  char entry (11B): u16 code, u8 w, u8 h, u16 u, u16 v, i8 t0, i8 t1, i8 t2
  shape@shapeOff: u8 recId, u8[3] nextOff, u16 atlasW, u16 atlasH,
                  u32 0, u32 0 (16-byte record header), then 4-bit nibble
                  stream (high first): 0=transparent, 15=ink
  R140 CORRECTION: the stream starts at shapeOff+16 (engine MacGIMEX_read
  0xB9B0 samples at record+16; len == shapeOff+16+W*H/2 exactly). The
  original +12 base of this script read 4 pad bytes as pixels — an 8px
  left-shift whose renders still read as text and masked the truth.
  port placement: draw at (pen_x, line_y + t2); pen_x += w + t0

Usage: ffn_ascii_dump.py <file.ffn> [more.ffn ...]
"""
import struct
import sys

INK = " .:-=+*#%@"  # index by nibble>>1 -> 10 levels


def parse(path):
    d = open(path, "rb").read()
    assert d[:4] == b"FNTF", path
    ver, num = struct.unpack_from("<HH", d, 8)
    dir_off, = struct.unpack_from("<I", d, 20)
    shape_off, = struct.unpack_from("<I", d, 32 - 4)
    ascent, descent = d[18], d[19]
    glyphs = {}
    order = []
    for k in range(num):
        o = dir_off + k * 11
        code, w, h, u, v = struct.unpack_from("<HBBHH", d, o)
        t0, t1, t2 = struct.unpack_from("<bbb", d, o + 8)
        g = dict(code=code, w=w, h=h, u=u, v=v, t0=t0, t1=t1, t2=t2)
        glyphs.setdefault(code, g)
        order.append(g)
    rec_id = d[shape_off]
    aw, ah = struct.unpack_from("<HH", d, shape_off + 4)
    img = shape_off + 16  # R140: engine-proven stream base
    nib_needed = (aw * ah + 1) // 2
    nibbles = []
    for b in d[img:img + nib_needed]:
        nibbles.append(b >> 4)
        nibbles.append(b & 0xF)
    atlas = [[0] * aw for _ in range(ah)]
    i = 0
    for y in range(ah):
        for x in range(aw):
            atlas[y][x] = nibbles[i]
            i += 1
    return dict(path=path, ver=ver, num=num, ascent=ascent, descent=descent,
                glyphs=glyphs, order=order, rec_id=rec_id, aw=aw, ah=ah,
                atlas=atlas, filesize=len(d))


def glyph_art(f, g):
    rows = []
    for y in range(g["v"], g["v"] + g["h"]):
        row = ""
        for x in range(g["u"], g["u"] + g["w"]):
            n = f["atlas"][y][x] if (0 <= y < f["ah"] and 0 <= x < f["aw"]) else -1
            row += ("?" if n < 0 else (INK[n >> 1] if n > 1 else (" " if n == 0 else ".")))
        rows.append(row)
    return rows


def render_string(f, s, scale_note=""):
    """Port placement: y = t2, x += w+t0."""
    cells = []
    pen = 0
    top = 0
    for ch in s:
        g = f["glyphs"].get(ord(ch))
        if g is None or g["w"] <= 1:
            pen += 5 if ch == " " else 0
            continue
        art = glyph_art(f, g)
        cells.append((pen, g["t2"], art))
        pen += g["w"] + g["t0"]
        top = min(top, g["t2"])
    width = pen
    height = max((t2 + len(art)) for (_, t2, art) in cells) if cells else 1
    height = max(height, 8)
    canvas = [[" "] * width for _ in range(height + 4)]
    for (x, t2, art) in cells:
        for dy, row in enumerate(art):
            yy = t2 + dy
            if 0 <= yy < len(canvas):
                for dx, c in enumerate(row):
                    if c not in (" ",):
                        canvas[yy][x + dx] = c
    print("  render('%s') advance_total=%d (port: x+=w+t0, y=t2):" % (s, pen))
    for row in canvas:
        print("  |" + "".join(row))


def dump(path):
    f = parse(path)
    print("=" * 78)
    print("FILE %s  size=%d ver=%d numChars=%d ascent=%d descent=%d recId=0x%02x atlas=%dx%d"
          % (path.split("/")[-1], f["filesize"], f["ver"], f["num"], f["ascent"],
             f["descent"], f["rec_id"], f["aw"], f["ah"]))
    codes = sorted(f["glyphs"].keys())
    print("  char codes: %d glyphs; first=%s last=%s" % (len(codes), codes[:8], codes[-8:]))
    # print the table for a set of interesting chars
    for ch in "AabgHjkmnpqy0139":
        g = f["glyphs"].get(ord(ch))
        if g:
            print("  '%s' code=%3d w=%2d h=%2d u=%3d v=%3d t0=%+d t1=%+d t2=%+d"
                  % (ch, g["code"], g["w"], g["h"], g["u"], g["v"], g["t0"], g["t1"], g["t2"]))
    render_string(f, "Handgloves")
    render_string(f, "The quick brown fox 0123")
    # raw atlas strip: first 3 glyphs as stored
    for g in f["order"][:0]:
        pass
    return f


if __name__ == "__main__":
    for p in sys.argv[1:]:
        dump(p)
