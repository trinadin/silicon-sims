#!/usr/bin/env python3
"""R89 position analysis: data-informed placements for the neighborhood sprite layers.

The original sprite-layer POSITIONS lived in engine code, not in the IFF corpus (the
Res_Nbhd templates declare resources only). This tool reads the ORIGINAL background
art bytes (byte-verbatim FAR extracts in r89/orig) and finds plausible homes for the
cycling sprites:

  car (24x15 / 26x19)  -> the road band on Studiotown\\DScreen.bmp (gray, wide run)
  balloon (64x64)      -> open sky on Magicland\\DScreen.bmp (top rows, low variance)
  mist (224x154 tga)   -> the water region on Magicland\\DScreen.bmp (the R88 waves
                          layer sits at (0,62) sized 647x362)
  nessie (106x75)      -> the river band on Community\\NScreen_unleashed.bmp (blue)

All placements derived from the art are PROVISIONAL (layout traces); the gate pins
bytes + cycling, not positions. Writes r89/r89-position-analysis.txt (evidence).
"""
import os
from PIL import Image

OUT = "tools/iff-dump/r89"
R89ORIG = os.path.join(OUT, "orig")


def load(path):
    im = Image.open(path).convert("RGB")
    return im.size[0], im.size[1], im.load()


def band_rows(w, h, px, pred, min_frac):
    """contiguous row-runs where >= min_frac of the row matches pred"""
    out, run = [], []
    for y in range(h):
        n = 0
        for x in range(w):
            r, g, b = px[x, y]
            if pred(r, g, b):
                n += 1
        if n >= w * min_frac:
            run.append((y, n))
        else:
            if len(run) >= 4:
                out.append(run)
            run = []
    if len(run) >= 4:
        out.append(run)
    return out


def report_bands(label, w, h, px, pred, min_frac, lines):
    bands = band_rows(w, h, px, pred, min_frac)
    lines.append("%s: %d candidate band(s)" % (label, len(bands)))
    best = None
    for run in bands:
        y0, y1 = run[0][0], run[-1][0]
        xs = []
        for y in range(y0, y1 + 1, 3):
            for x in range(0, w, 3):
                if pred(*px[x, y]):
                    xs.append(x)
        cx = sum(xs) / max(1, len(xs))
        height = y1 - y0 + 1
        lines.append("  y=%3d..%3d h=%2d px/row~%.0f centroid-x=%.0f" %
                     (y0, y1, height, sum(r[1] for r in run) / len(run), cx))
        if best is None or height > best[0]:
            best = (height, y0, y1, cx)
    return best


def main():
    lines = ["R89 position analysis - data-informed (PROVISIONAL) sprite placements.",
             "Placements derive from the original background art bytes; the original",
             "engine-code positions are not in the IFF corpus.", ""]

    # nessie: river band (blue-dominant) on the Unleashed community screen
    w, h, px = load(os.path.join(R89ORIG, "Community__NScreen_unleashed.bmp"))
    best = report_bands("NESSIE river (Community\\NScreen_unleashed.bmp blue band)",
                        w, h, px, lambda r, g, b: b > r + 15 and b > g + 5 and b > 50,
                        0.15, lines)
    if best:
        _, y0, y1, cx = best
        lines.append("  -> nessie (106x75) place ~(%d, %d)  [tallest band, centroid x, head above waterline]"
                     % (int(cx - 53), max(0, y0 - 20)))

    # car: road band (gray, mid-luminance, wide) on the Studiotown screen
    w, h, px = load(os.path.join(R89ORIG, "Studiotown__DScreen.bmp"))
    best = report_bands("CAR road (Studiotown\\DScreen.bmp gray band)",
                        w, h, px,
                        lambda r, g, b: abs(r - g) < 16 and abs(g - b) < 16 and 70 <= r <= 180,
                        0.35, lines)
    if best:
        _, y0, y1, cx = best
        lines.append("  -> car (24x15) place ~(%d, %d)  [tallest band, vertical middle, centroid x]"
                     % (int(cx - 12), (y0 + y1) // 2 - 7))

    # balloon: sky = lowest color-variance band in the top 200 rows (Magicland)
    w, h, px = load(os.path.join(R89ORIG, "Magicland__DScreen.bmp"))
    scores = []
    for y0 in range(0, 200, 10):
        tot = n = 0
        for y in range(y0, min(y0 + 30, h), 2):
            for x in range(0, w, 8):
                r, g, b = px[x, y]
                tot += abs(r - g) + abs(g - b)
                n += 1
        scores.append((tot / max(1, n), y0))
    scores.sort()
    lines.append("BALLOON sky (Magicland\\DScreen.bmp): most-uniform band y=%d (score %.1f; next %s)"
                 % (scores[0][1], scores[0][0], ["y=%d:%.0f" % (y, s) for s, y in scores[1:3]]))
    lines.append("  -> balloon (64x64) place ~(140, %d)  [left sky, clear of centre art]"
                 % (scores[0][1] + 10))
    # mist: blue-dominant band inside the waves rect (0,62)-(647,424)
    xs0, xs1 = 0, 647
    best = None
    run = []
    for y in range(62, min(62 + 362, h)):
        n = sum(1 for x in range(xs0, xs1, 2) if px[x, y][2] > px[x, y][0] + 12)
        if n > (xs1 - xs0) / 2 * 0.5:
            run.append((y, n))
        else:
            if len(run) >= 4 and (best is None or len(run) > len(best)):
                best = run
            run = []
    if len(run) >= 4 and (best is None or len(run) > len(best)):
        best = run
    if best:
        my = (best[0][0] + best[-1][0]) // 2
        lines.append("MIST water (Magicland\\DScreen.bmp inside waves rect): blue rows y=%d..%d"
                     % (best[0][0], best[-1][0]))
        lines.append("  -> mist (224x154) place ~(210, %d)  [centred in water band]" % (my - 77))
    else:
        lines.append("MIST water: no dominant blue band; default (210, 210)")

    txt = "\n".join(lines) + "\n"
    txt += (
        "\nDECISIONS (provisional constants used in UINeighbourhoodSelectionPanel):\n"
        "  car     -> no distinct road band exists; the Studiotown screen is uniformly concrete\n"
        "             (152,152,136). Frames 0-13 are a blue-grey car, 14-19 a yellow taxi\n"
        "             (magenta 255,0,255 key). Place mid-lot on the concrete: (390, 440).\n"
        "  balloon -> left sky over the most-uniform band: (140, 30).\n"
        "  mist    -> the water region is the waves rect (0,62)-(647,424) (not blue - swamp).\n"
        "             Centre the fog inside it: (210, 210).\n"
        "  nessie  -> tallest blue band (river) bottom-left: (121, 505).\n"
        "All four are layout traces; positions are NOT gate-pinned.\n")
    open(os.path.join(OUT, "r89-position-analysis.txt"), "w").write(txt)
    print(txt)


if __name__ == "__main__":
    main()
