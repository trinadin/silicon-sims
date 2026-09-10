#!/usr/bin/env python3
# iff_carr_raw.py - ENGINE-INDEPENDENT IFF canon decode of the original career table.
# Reads raw work.iff CARR chunks, replicating IffFieldEncode (FSO.Files.Utils) for Int32 with
# widths {6,11,21,32}: bitstream LSB-first; ReadField: if bit0==0 -> 0; code=next2; width=w32[code];
# value=next width bits; sign-extend. Layout per CARR.cs: u32 pad, u32 version, u32 MjbO, u8 comp,
# null-terminated name(+align), then field-encoded: numLevels, then per level 10 MinRequired + 7
# MotiveDelta + Salary + StartTime + EndTime + CarType. IFF JobData[data] == this table:
#   data0=numLevels, 1=Salary, 2..8=MinRequired[0..6] (the 7 skill reqs IFF 'test promotion' checks),
#   12=StartTime 13=EndTime 21=CarType 22=const0 14..20=MotiveDelta[0..6] (CARR.GetJobData switch).
import struct, sys

W32 = (6, 11, 21, 32)

def clean(b):
    return b.decode('latin1','replace').replace(chr(0),'').strip()

class BitStream:
    def __init__(self, data):
        self.data = data; self.pos = 0; self.cur = 0; self.n = 0
    def bit(self):
        if self.n == 0:
            self.cur = self.data[self.pos]; self.pos += 1; self.n = 8
        self.n -= 1
        return (self.cur >> self.n) & 1
    def bits(self, k):
        v = 0
        for _ in range(k):
            v = (v << 1) | self.bit()
        return v
    def readint(self):
        if self.bit() == 0:
            return 0
        code = self.bits(2)
        width = W32[code]
        value = self.bits(width)
        value |= -(value & (1 << (width - 1)))
        return value

def decode_levels(payload):
    # after 76-byte IFF chunk header, CARR payload starts with pad/version/MjbO/comp/name
    off = 0
    pad, ver, mjbo = struct.unpack_from('<III', payload, off); off += 12
    comp = payload[off]; off += 1
    end = payload.index(b'\x00', off)
    name = payload[off:end].decode('latin1', 'replace')
    off = end + 1
    if (end - 12 - 1) % 2 == 1:
        off += 1
    bs = BitStream(payload[off:])
    levels = []
    num = bs.readint()
    for _ in range(max(0, num)):
        req = [bs.readint() for _ in range(10)]
        delta = [bs.readint() for _ in range(7)]
        sal = bs.readint()
        st = bs.readint()
        et = bs.readint()
        car = bs.readint()
        levels.append({'req': req, 'delta': delta, 'sal': sal, 'st': st, 'et': et, 'car': car})
        if len(levels) >= 1:
            break  # strings per level interleave after CarType; level-0 canon only
    return name, levels

def main():
    src = sys.argv[1]
    data = open(src, 'rb').read()
    off = 64
    out = []
    while off + 76 <= len(data):
        ct = data[off:off+4].decode('latin1','replace')
        csize = struct.unpack('>I', data[off+4:off+8])[0]
        cid = struct.unpack('>H', data[off+8:off+10])[0]
        label = clean(data[off+12:off+76])
        paysz = csize - 76
        payload = data[off+76:off+76+paysz]
        if ct == 'CARR':
            try:
                name, levels = decode_levels(payload)
            except Exception as e:
                print('CARR id=%d label=%r parse error %s' % (cid, label, e)); off += 76 + paysz; continue
            for li, lv in enumerate(levels):
                req = lv['req']
                # IFF JobData[data] map: data2..8 = req[0..6]; promotion-critical in IFF order:
                # req[0]~SkillEfficiency req[1]~Cooking req[2]~Mechanical req[3]~Charisma
                # req[4]~Body req[5]~Logic req[6]~Creativity (IFF 'test promotion' checks)
                print('CARR id=%-3d lvl=%d name=%s levels=%d | req[0..6]=%s | delta[0..6]=%s | salary=%d start=%d end=%d car=%d' % (
                    cid, li, name, len(levels), req[0:7], lv['delta'], lv['sal'], lv['st'], lv['et'], lv['car']))
        off += 76 + paysz

if __name__ == '__main__':
    main()
