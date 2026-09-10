#!/usr/bin/env python3
"""R103: PEF packed-section unpacker. Encoding per Apple's PEF spec
(RTArch-94): opcode byte = 3-bit op (top) + 5-bit count; count==0 means the
count follows as a base-128 big-endian varint (high bit set = continue).
Ops: 0 Zero(count) | 1 blockCopy(count literal bytes) | 2 repeatedBlock(
blockSize, repeatCount-1, blockSize bytes) | 3 interleaveRepeatBlockWith-
BlockCopy(commonSize, customSize, repeatCount, data) | 4 same but the common
pattern is ZEROS.
Usage: pef_unpack.py <bin> <off> <packedLen> <out> [expectUnpacked]
"""
import struct, sys

def varint(d, i):
    v = 0
    while True:
        b = d[i]; i += 1
        v = ((v << 7) | (b & 0x7F)) & 0xFFFFFFFF
        if not (b & 0x80):
            return v, i

def unpack(d):
    out = bytearray()
    i = 0
    n = len(d)
    while i < n:
        opb = d[i]; i += 1
        op = opb >> 5
        cnt = opb & 0x1F
        if cnt == 0:
            cnt, i = varint(d, i)
        if op == 0:
            out += b"\x00" * cnt
        elif op == 1:
            out += d[i:i+cnt]; i += cnt
        elif op == 2:
            bs, i = varint(d, i)
            rc, i = varint(d, i)
            blk = d[i:i+bs]; i += bs
            out += blk * (rc + 1)
        elif op == 3 or op == 4:
            common, i = varint(d, i)
            custom, i = varint(d, i)
            rc, i = varint(d, i)
            common_blk = b"\x00" * common if op == 4 else d[i:i+common]
            if op == 3: i += common
            out += common_blk
            for k in range(rc):
                out += d[i:i+custom]; i += custom
                out += common_blk
        else:
            raise ValueError(f"reserved op {op} at stream {i-1:#x} (out={len(out):#x})")
    return bytes(out)

if __name__ == "__main__":
    src, off, length, dst = sys.argv[1], int(sys.argv[2],0), int(sys.argv[3],0), sys.argv[4]
    expect = int(sys.argv[5],0) if len(sys.argv) > 5 else None
    data = open(src,"rb").read()[off:off+length]
    out = unpack(data)
    open(dst,"wb").write(out)
    ok = "" if expect is None else (" MATCH" if len(out) == expect else f" MISMATCH vs {expect:#x}")
    print(f"packed {length:#x} -> unpacked {len(out):#x}{ok}")
