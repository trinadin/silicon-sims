#!/usr/bin/env python3
"""R125 canon: the ORIGINAL live-mode object hover tooltips.

A) STR# 159 'ObjectTTs' byte-verbatim dump from GameData/UIText.iff (9 English
   reason entries; reuses the r122 decoder). The gate pins label/180-raw/
   full-chunk sha/all 9 entries via regen_gate_shas.py (never hand-typed).

B) TTAB classification scan across EVERY catalog master on disk (all FARs,
   case-insensitive extension — the r124 EP5 lesson): parses each master's
   TreeTableID TTAB (version 8 plain + version 9 bit-packed per a python
   mirror of FreeSO's IffFieldEncode, MSB-first) and classifies the entry set
   the way the R125 port's ObjectTooltipReason does:
     pets-only  = every entry carries TS1AllowCats(0x200)|TS1AllowDogs(0x400)
     cats-only  = pets-only with only 0x200 of the pair
     dogs-only  = pets-only with only 0x400
     kids-only  = every entry TS1NoAdult(0x40), no pet bits
     adults-only= every entry TS1NoChild(0x10), no pet bits
   Produces the anchor objects the gate drives LIVE (through the engine's own
   TTAB decoder) and the corpus-wide evidence for the classification.

Writes r125-objtts-strs.txt + r125-ttab-scan.txt. FAILS LOUDLY if STR# 159
is absent/mislabeled or no pets-only anchor exists.
"""
import struct, hashlib, os, sys, glob, importlib.util

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
GAMEDATA = os.path.join(ROOT, 'game-data/The Sims')

spec = importlib.util.spec_from_file_location('r122mod', os.path.join(HERE, '..', 'r122', 'make_r122_buycat_canon.py'))
r122 = importlib.util.module_from_spec(spec)
sys.modules['r122mod'] = r122
spec.loader.exec_module(r122)

spec2 = importlib.util.spec_from_file_location('r124mod', os.path.join(HERE, '..', 'r124', 'make_r124_descpanel_canon.py'))
r124 = importlib.util.module_from_spec(spec2)
sys.modules['r124mod'] = r124
spec2.loader.exec_module(r124)
_parse_objd = r124.parse_objd

NO_CHILD, NO_ADULT, ALLOW_CATS, ALLOW_DOGS = 0x10, 0x40, 0x200, 0x400
PET_MASK = ALLOW_CATS | ALLOW_DOGS


class BitReader:
    """IffFieldEncode mirror (MSB-first, sign-extended, 0-bit = literal zero)."""
    def __init__(self, data, pos):
        self.d = data; self.p = pos; self.bit = 0
        self.cur = data[pos]
    def rbit(self):
        r = (self.cur >> (7 - self.bit)) & 1
        self.bit += 1
        if self.bit > 7:
            self.bit = 0; self.p += 1
            self.cur = self.d[self.p] if self.p < len(self.d) else 0
        return r
    def rbits(self, n):
        t = 0
        for _ in range(n): t = (t << 1) | self.rbit()
        return t
    def field(self, widths):
        if self.rbit() == 0: return 0
        w = widths[self.rbits(2)]
        v = self.rbits(w)
        if v & (1 << (w - 1)): v -= (1 << w)
        return v
    def u16(self): return self.field((5, 8, 13, 16)) & 0xFFFF
    def u32(self): return self.field((6, 11, 21, 32)) & 0xFFFFFFFF


def parse_ttab(body):
    """Mirror of TTAB.Read (FreeSO TTAB.cs:41-93). Returns list of flag u32s."""
    if len(body) < 4: return None
    (count,) = struct.unpack('<H', body[0:2])
    if count == 0: return []
    (version,) = struct.unpack('<H', body[2:4])
    if version <= 3: return None
    pos = 4
    if 9 <= version <= 10:
        code = body[4]; pos = 5
        if code != 1:
            return None   # plain v9/10 not in the TS1 corpus; refuse loudly downstream
        rd = None
    else:
        rd = None
    flags = []
    if 9 <= version <= 10:
        br = BitReader(body, pos)
        for _ in range(count):
            br.u16(); br.u16()                       # action, test
            mc = br.u32()
            f = br.u32(); br.u32()                   # flags, ttaindex
            br.u32(); br.u32(); br.u32(); br.u32()   # attcode, attval(f32 bits), autonomy, joining
            for _ in range(mc):
                br.u16(); br.u16(); br.u16()
            flags.append(f)
    else:
        p = pos
        def ru16():
            nonlocal p
            v = struct.unpack('<H', body[p:p+2])[0]; p += 2; return v
        def ru32():
            nonlocal p
            v = struct.unpack('<I', body[p:p+4])[0]; p += 4; return v
        def ri32():
            nonlocal p
            v = struct.unpack('<i', body[p:p+4])[0]; p += 4; return v
        for _ in range(count):
            ru16(); ru16()
            mc = ru32()
            f = ru32(); ru32()
            if version > 6:
                ru32()                                # attenuation code
            ru32()                                    # attenuation value bits
            ru32(); ri32()
            for _ in range(mc):
                if version > 6: ru16()
                ru16()
                if version > 6: ru16()
            flags.append(f)
    return flags


def classify(flags):
    if flags is None or len(flags) == 0: return 'none'
    if all(f & PET_MASK for f in flags):
        c, d = any(f & ALLOW_CATS for f in flags), any(f & ALLOW_DOGS for f in flags)
        if c and d: return 'pets-only'
        if d: return 'dogs-only'
        return 'cats-only'
    if all(f & NO_ADULT for f in flags) and not any(f & PET_MASK for f in flags):
        return 'kids-only'
    if all(f & NO_CHILD for f in flags) and not any(f & PET_MASK for f in flags):
        return 'adults-only'
    return 'mixed'


def main():
    # ---- A) STR# 159 ----
    uitext = os.path.join(GAMEDATA, 'GameData/UIText.iff')
    data = open(uitext, 'rb').read()
    chunks = r122.walk_chunks(data)
    hits = [c for c in chunks if c['cc'] == b'STR#' and c['id'] == 159]
    if len(hits) != 1 or hits[0]['label'] != 'ObjectTTs':
        print('FATAL: STR# 159 not found/mislabeled'); sys.exit(1)
    out = ['R125 STR# 159 \'ObjectTTs\' — the original live-mode object hover reasons',
           'Source: %s' % uitext]
    t = r122.dump_table(hits[0], data, out)
    if t['declared'] != 180 or len(t['eng']) != 9:
        print('FATAL: STR# 159 counts %d/%d' % (t['declared'], len(t['eng']))); sys.exit(1)
    with open(os.path.join(HERE, 'r125-objtts-strs.txt'), 'w') as f:
        f.write('\n'.join(out) + '\n')

    # ---- B) TTAB classification scan ----
    scan = ['R125 TTAB classification scan — every catalog master, all FARs (case-insensitive)',
            'Parser: TTAB.cs mirror (v8 plain LE / v9 bit-packed IffFieldEncode mirror).',
            'Classes as the port classifies (ObjectTooltipReason): pets/dogs/cats-only,',
            'kids-only (all TS1NoAdult 0x40), adults-only (all TS1NoChild 0x10), mixed, none.',
            '']
    buckets = {'pets-only': [], 'dogs-only': [], 'cats-only': [], 'kids-only': [],
               'adults-only': [], 'none': [], 'mixed': []}
    n_masters = 0
    fars = sorted(f for f in glob.glob(os.path.join(GAMEDATA, '**', '*'), recursive=True)
                  if f.lower().endswith('.far'))
    for far in fars:
        if 'sound' in far.lower() or 'graphic' in far.lower(): continue
        fd = open(far, 'rb').read()
        base = os.path.basename(far)
        for nm, dlen, doff in _walk(fd):
            if not nm.lower().endswith('.iff'): continue
            raw = fd[doff:doff + dlen]
            if not raw.startswith(b'IFF'): continue
            try: chunks = r122.walk_chunks(raw)
            except SystemExit: continue
            ctss, ttabs = {}, {}
            for c in chunks:
                if c['cc'] == b'CTSS':
                    try:
                        _, ents, _ = r122.decode_str_body(raw[c['off'] + 76:c['off'] + c['size']])
                        ctss[c['id']] = [v for l, v, _ in ents if l == 1]
                    except Exception: pass
                elif c['cc'] == b'TTAB':
                    ttabs[c['id']] = raw[c['off'] + 76:c['off'] + c['size']]
            for c in chunks:
                if c['cc'] != b'OBJD': continue
                o = _parse_objd(raw[c['off'] + 76:c['off'] + c['size']])
                if o is None: continue
                f = o['fields']
                if len(f) < 90 or f[8] or f[9]: continue     # masters only
                n_masters += 1
                tt = ttabs.get(f[5])
                flags = parse_ttab(tt) if tt is not None else ([] if f[5] == 0xFFFF else None)
                ent = ctss.get(f[39], [])
                name = ent[0].decode('mac_roman', 'replace') if ent else nm
                cls = classify(flags)
                buckets[cls].append((name, base + '!' + nm,
                                     [hex(x) for x in flags] if flags else flags))
    scan.append('masters scanned: %d' % n_masters)
    for cls in ('pets-only', 'dogs-only', 'cats-only', 'kids-only', 'adults-only'):
        scan.append('')
        scan.append('==== %s: %d masters ====' % (cls, len(buckets[cls])))
        for name, src, flags in buckets[cls][:26]:
            scan.append('  %-34s %-30s flags=%s' % (name[:34], src[:30], flags))
        if len(buckets[cls]) > 26:
            scan.append('  ... and %d more' % (len(buckets[cls]) - 26))
    scan.append('')
    scan.append('none(no TTAB): %d   mixed/everything-else: %d'
                % (len(buckets['none']), len(buckets['mixed'])))
    if not buckets['pets-only']:
        print('FATAL: no pets-only anchor found — the gate needs one'); sys.exit(1)
    with open(os.path.join(HERE, 'r125-ttab-scan.txt'), 'w') as f:
        f.write('\n'.join(scan) + '\n')
    print('masters=%d pets=%d cats=%d dogs=%d kids=%d adults=%d none=%d mixed=%d; wrote outputs'
          % (n_masters, len(buckets['pets-only']), len(buckets['cats-only']),
             len(buckets['dogs-only']), len(buckets['kids-only']), len(buckets['adults-only']),
             len(buckets['none']), len(buckets['mixed'])))


def _walk(fd):
    man = struct.unpack('<I', fd[12:16])[0]
    num = struct.unpack('<I', fd[man:man + 4])[0]
    off = man + 4
    for _ in range(num):
        if off + 16 > len(fd): break
        dlen, d2, doff, nlen = struct.unpack('<IIII', fd[off:off + 16])
        nm = fd[off + 16:off + 16 + nlen].decode('latin1', 'replace')
        off += 16 + nlen
        if dlen != d2 or doff + dlen > len(fd): continue
        yield nm, dlen, doff


if __name__ == '__main__':
    main()
