#!/usr/bin/env python3
"""R154: proper scan of FIELD-ENCODED (IffFieldEncode) TTAB chunks.

The r154 corpus scan's TTAB pass parsed only plain (TTABNormal) TTABs; 1,355
version-9 TTABs use Maxis' field-encoded bitstream (FreeSO IffFieldEncode.cs:
MSB-first bits; field = '0' bit -> 0, else 2-bit width code -> width in
{5,8,13,16} (16-bit) / {6,11,21,32} (32-bit), value = width bits, sign-
extended from MSB). This script decodes every encoded TTAB per TTAB.cs and
checks ActionFunction/TestFunction against 9761/39200, reporting the census
of semiglobal ids (>=8192) they DO use.

Body layout (chunk data):
  u16 count, u16 version, u8 compressionCode(=1 -> encoded),
  then bitstream from the NEXT byte (that byte is the IffFieldEncode seed):
  per interaction:
    u16 action, u16 test, u32 motiveCount, u32 flags, u32 ttaIndex,
    [v>6] u32 attenuationCode, f32 attenuation, u32 autonomy, i32 joining,
    motiveCount x ([v>6] i16 min, i16 delta, u16 personality),
    [v>9] u32 flags2

Writes r154-ttab-encoded-scan.txt.
"""
import os
import struct
from collections import Counter

HERE = os.path.dirname(__file__)

TARGETS = {8486, 8345}
WIDTHS = (5, 8, 13, 16)
WIDTHS32 = (6, 11, 21, 32)


class BitReader:
    """IffFieldEncode mirror (TTAB path only: no strings, no Interrupt)."""

    def __init__(self, data, start):
        self.d = data
        self.pos = start          # byte position of curByte
        self.cur = data[start] if start < len(data) else 0
        self.bit = 0
        self.end = False

    def _bit(self):
        if self.pos >= len(self.d):
            self.end = True
            raise EOFError()
        b = (self.cur >> (7 - self.bit)) & 1
        self.bit += 1
        if self.bit > 7:
            self.bit = 0
            self.pos += 1
            self.cur = self.d[self.pos] if self.pos < len(self.d) else 0
        return b

    def bits(self, n):
        v = 0
        for _ in range(n):
            v = (v << 1) | self._bit()
        return v

    def field(self, widths):
        if self._bit() == 0:
            return 0
        w = widths[self.bits(2)]
        v = self.bits(w)
        if v & (1 << (w - 1)):
            v -= (1 << w)  # sign-extend
        return v

    def u16(self):
        return self.field(WIDTHS) & 0xFFFF

    def i16(self):
        v = self.field(WIDTHS) & 0xFFFF
        return v - 0x10000 if v >= 0x8000 else v

    def u32(self):
        return self.field(WIDTHS32) & 0xFFFFFFFF

    def i32(self):
        v = self.field(WIDTHS32) & 0xFFFFFFFF
        return v - (1 << 32) if v >= (1 << 31) else v


def parse_encoded(body):
    """Returns list of (action, test, ttaIndex, flags) or ('ERR', reason)."""
    count, version = struct.unpack_from('<HH', body, 0)
    cc = body[4]
    if cc != 1:
        return 'ERR', 'cc=%d' % cc
    br = BitReader(body, 5)
    out = []
    try:
        for _ in range(count):
            action = br.u16()
            test = br.u16()
            mc = br.u32()
            flags = br.u32()
            tta = br.u32()
            if version > 6:
                br.u32()  # attenuation code
            br.u32()      # attenuation float (bit pattern)
            br.u32()      # autonomy
            br.i32()      # joining
            if mc > 64:
                return 'ERR', 'mc=%d' % mc
            for _ in range(mc):
                if version > 6:
                    br.i16(); br.i16()
                br.u16()
            if version > 9:
                br.u32()
            out.append((action, test, tta, flags))
    except EOFError:
        return 'ERR', 'stream-end after %d' % len(out)
    return 'OK', out


def main():
    import importlib.util
    spec = importlib.util.spec_from_file_location('m', os.path.join(HERE, 'r154-callers-scan.py'))
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)

    out = open(os.path.join(HERE, 'r154-ttab-encoded-scan.txt'), 'w')

    def P(s=''):
        print(s)
        out.write(s + '\n')

    stats = Counter()
    ids_used = Counter()
    hits = []
    for dirpath, _, fns in os.walk(m.ROOT):
        for fn in fns:
            b = fn.lower()
            if not (b.endswith('.iff') or b.endswith('.fam') or b.endswith('.far')):
                continue
            p = os.path.join(dirpath, fn)
            try:
                fh = open(p, 'rb')
                items = []
                if b.endswith('.far'):
                    items = [('%s!%s' % (os.path.relpath(p, m.ROOT), nm), d)
                             for nm, d in m.far_members(p, fh)
                             if nm.lower().endswith(('.iff', '.far'))]
                else:
                    items = [(os.path.relpath(p, m.ROOT), open(p, 'rb').read())]
                fh.close()
            except OSError:
                continue
            for disp, d in items:
                if d[:8] != b'IFF FILE':
                    continue
                for tag, off, size, cid, flags, label, dstart in m.walk_chunks(d):
                    if tag != 'TTAB':
                        continue
                    body = d[dstart:off+size]
                    if len(body) < 6:
                        continue
                    count, version = struct.unpack_from('<HH', body, 0)
                    cc = body[4] if len(body) > 4 else 0
                    if 9 <= version <= 10 and cc == 1:
                        st, res = parse_encoded(body)
                        if st == 'OK':
                            stats['parsed-ok'] += 1
                            for (act, tst, tta, fl) in res:
                                ids_used[act] += 1
                                ids_used[tst] += 1
                                if act in TARGETS or tst in TARGETS:
                                    hits.append((disp, cid, label, act, tst, tta))
                        else:
                            stats['fail:' + res] += 1
    P('field-encoded TTABs: parsed %d' % stats['parsed-ok'])
    for k, v in sorted(stats.items()):
        if k != 'parsed-ok':
            P('  FAIL %s: %d' % (k, v))
    P('')
    P('semiglobal ids (>=8192) referenced by encoded TTABs: %s'
      % sorted(i for i in ids_used if i >= 8192))
    P('')
    P('TARGET HITS: %d' % len(hits))
    for h in hits:
        P('  %s TTAB %d %r action=%d test=%d tta=%d' % h)
    out.close()
    print('wrote', out.name)


if __name__ == '__main__':
    main()
