#!/usr/bin/env python3
"""R251 mood-law implementation round — pins the NEW instruction words and the
curve facts this round relied on.

(1) The CalcHappy table-split gate polarity (how children vs adults select the
    STR#502 vs STR#504 weight table), reconciled from the RAW hex (this round),
    which had been mis-decoded twice previously.
(2) The CalcHappy table-order array (rodata): Hunger..Room.
(3) The STR#502/504 curve counts (verified = 7 each, so the Room has no curve).

Run:  python3 tools/iff-dump/r251-mood-cadence/moodlaw-verify.py
"""
import struct, hashlib, sys, os

BIN = os.path.join(os.path.dirname(__file__), "..", "..", "..", "game-data",
                   "The Sims", "The Sims Complete")
SHA = "33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f"


def open_bin():
    with open(BIN, "rb") as f:
        return f.read()


def u32(d, off):
    return struct.unpack(">I", d[off:off + 4])[0]


def s16(d, off):
    return struct.unpack(">h", d[off:off + 2])[0]


def main():
    d = open_bin()

    # --- 0. pin the source image SHA256 --------------------------------
    digest = hashlib.sha256(d).hexdigest()
    if digest != SHA:
        print("FAIL: binary sha256", digest, "!=", SHA)
        sys.exit(1)
    print("PASS: binary sha256", digest)

    # --- 1. CalcHappy table-split gate (file offsets) --------------------
    # r251-disasm-calchappy.txt. The polarity here was the #1 error class;
    # the raw words are the ground truth. r5==1 ONLY for person[1536] in
    # [1,17]; r5==0 for ==0 or >=18. r5!=0 -> STR#504 (child), r5==0 -> STR#502.
    gate = {
        0x10b9c8: 0xa8830600,  # lha   r4, 0x600(r3)     person[1536]
        0x10b9d0: 0x7c800735,  # extsh. r0, r4
        0x10b9d4: 0xfca03090,  # fmr   f5, f6
        0x10b9d8: 0x41820010,  # beq   0x10b9e8          ==0 -> adult path (r5=0)
        0x10b9dc: 0x2c040012,  # cmpwi r4, 0x12
        0x10b9e0: 0x40800008,  # bge   0x10b9e8          >=18 -> adult path (r5=0)
        0x10b9e4: 0x38a00001,  # li    r5, 1             [1,17] -> child (r5=1)
        0x10b9e8: 0x54a0063f,  # clrlwi. r0, r5, 0x18
        0x10b9ec: 0x4182000c,  # beq   0x10b9f8          r5==0 -> adult table
        0x10b9f0: 0x8082a704,  # lwz   r4, -0x58fc(r2)   child -> STR#504
        0x10b9f8: 0x8082a700,  # lwz   r4, -0x5900(r2)   adult -> STR#502
    }
    for off, exp in gate.items():
        got = u32(d, off)
        if got != exp:
            print("FAIL: word 0x%x expected 0x%08x got 0x%08x" % (off, exp, got))
            sys.exit(1)
    print("PASS: CalcHappy table-split gate (%d words) — child[1,17]->STR#504, adult->STR#502" % len(gate))

    # --- 2. CalcHappy table-order array (rodata, senior) ----------------
    # 8 x 32-bit at 0x5a45b8: Hunger,Energy,Comfort,Fun,Hygiene,Social,Bladder,Room.
    order_off = 0x5a45b8
    expected = [7, 5, 6, 15, 8, 14, 9, 13]
    got = [u32(d, order_off + 4 * i) for i in range(8)]
    if got != expected:
        print("FAIL: table-order array", got, "!=", expected)
        sys.exit(1)
    print("PASS: CalcHappy table-order array", got)

    # --- 3. STR#502 / STR#504 curve counts (Global.far -> global.iff) ---
    far = os.path.join(os.path.dirname(__file__), "..", "..", "..",
                       "game-data", "The Sims", "GameData", "Global", "Global.far")
    f = open(far, "rb").read()
    if f[:8] != b"FAR!byAZ":
        print("FAIL: not a FAR1 archive")
        sys.exit(1)
    ver, manoff = struct.unpack("<II", f[8:16])
    n = struct.unpack("<I", f[manoff:manoff + 4])[0]
    off = manoff + 4
    entries = {}
    for _ in range(n):
        dl1, dl2, do = struct.unpack("<iii", f[off:off + 12]); off += 12
        fnlen = struct.unpack("<i", f[off:off + 4])[0]; off += 4
        fn = f[off:off + fnlen].decode("latin1"); off += fnlen
        entries[fn] = do
    g = None
    for k, v in entries.items():
        if k.lower() == "global.iff":
            g = f[v:]
            break
    if g is None:
        print("FAIL: global.iff not found in Global.far")
        sys.exit(1)

    def str_chunks(data):
        # header: 60-byte id string + 4-byte rsmpOffset, then chunks
        ch = {}
        off = 64
        while off + 8 <= len(data):
            typ = data[off:off + 4].decode("latin1")
            csize = struct.unpack(">I", data[off + 4:off + 8])[0]
            if typ == "STR#":
                cid = struct.unpack(">H", data[off + 8:off + 10])[0]
                ch.setdefault(cid, []).append((off, csize))
            off += csize
        return ch

    def str_count(data, cid):
        for (o, cs) in str_chunks(data)[cid]:
            dat = data[o + 76:o + cs]
            fmt = struct.unpack("<h", dat[0:2])[0]
            cnt = struct.unpack("<H", dat[2:4])[0]
            # format -3 (language-coded): per string a byte lang + 2 null-terminated strings
            return fmt, cnt
        return None, None

    for cid in (502, 504):
        fmt, cnt = str_count(g, cid)
        if cnt != 7:
            print("FAIL: STR#%d count %s != 7" % (cid, cnt))
            sys.exit(1)
        if fmt != -3:
            print("FAIL: STR#%d format %s != -3" % (cid, fmt))
            sys.exit(1)
        print("PASS: STR#%d has %d curves (the 8th/Room has no curve -> scalar weight)" % (cid, cnt))

    print("ALL CHECKS PASS")
    return 0


if __name__ == "__main__":
    sys.exit(main())
