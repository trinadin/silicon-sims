#!/usr/bin/env python3
"""R151: old-format TS1 IFF walker + BHAV person-data scan.
Chunk header: tag[4] (byte-reversed), size u32 LE (includes 76-byte header),
id u16 LE, flags u16, label 64 bytes, data = size-76. Next chunk at off+size.
BHAV instruction: opcode u16 LE, truePtr u8, falsePtr u8, operands[8].
op 2 (expression): Lhs i16, Rhs i16, byte4=signed/op hi/lo?, byte5=operator,
byte6=LhsScope, byte7=RhsScope  (FreeSO VMExpressionOperand layout)
op 8 (random): dest i16, destScope u16, range i16, rangeScope u16.
Usage: r151-iffscan.py <iff> [--bhav ID] [--scan-person-writes]"""
import struct, sys

def chunks(d):
    off = 64
    while off + 76 <= len(d):
        raw = d[off:off+4]
        tag = raw[::-1].decode(errors='replace')
        size, cid, flags = struct.unpack_from('<IHH', d, off+4)
        if size < 76 or off + size > len(d): break
        label = d[off+12:off+76].split(b'\0')[0].decode(errors='replace')
        yield tag, cid, flags, label, off+76, size-76
        off += size + (size & 1)

def scopes(v):
    # VMVariableScope enum (FreeSO): 0 Literal? ... we print raw
    return v

def decode_instr(op, o):
    # returns human string; o = 8 operand bytes (little endian fields)
    L = struct.unpack_from('<h', o, 0)[0]
    R = struct.unpack_from('<h', o, 2)[0]
    if op == 2:
        opd = o[4]; oper = o[5]; lsc = o[6]; rsc = o[7]
        return f"expr L=[{lsc}]{L} R=[{rsc}]{R} op={oper} packed={opd:#04x}"
    if op == 8:
        dst, dsc, rng, rsc = struct.unpack_from('<hhHH', o, 0)
        return f"random dest=[{dsc}]{dst} range=[{rsc}]{rng}"
    return f"op{op} raw={o.hex()}"

def main():
    p = sys.argv[1]
    d = open(p,'rb').read()
    want = None
    scan = False
    for a in sys.argv[2:]:
        if a == '--scan-person-writes': scan = True
        elif a.startswith("--bhav"): want = int(a.split("=")[1])
    for tag, cid, flags, label, doff, dsz in chunks(d):
        if tag != 'BHAV': continue
        if want is not None and cid != want: continue
        h = d[doff:doff+13]
        sig = struct.unpack_from('<H', h, 0)[0]
        if sig == 0x8003:
            typ, args, locs = h[3], h[4], h[5]
            count = h[9] | (h[10]<<8) | (h[11]<<16) | (h[12]<<24)
            ioff = doff + 13
            ilen = 12
        else:
            count = struct.unpack_from('<H', h, 3)[0]
            typ, args = h[5], h[6]
            locs = struct.unpack_from('<H', h, 7)[0]
            ioff = doff + 12
            ilen = 12
        print(f"BHAV {cid} '{label}' sig={sig:#x} count={count} type={typ} args={args} locals={locs}")
        for i in range(count):
            o = d[ioff+i*ilen: ioff+(i+1)*ilen]
            if len(o) < 12: break
            op, tp, fp = struct.unpack_from('<HBB', o, 0)
            ob = o[4:12]
            print(f"  [{i}] op={op} T={tp} F={fp} | {decode_instr(op, ob)}")

if __name__ == '__main__':
    main()
