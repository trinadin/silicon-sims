#!/usr/bin/env python3
"""R153 TASK 1: full NBRS decode across every staged neighborhood file.

Semantics are the byte-exact replica of FreeSO's authoritative parser
(FreeSO/TSOClient/tso.files/Formats/IFF/Chunks/NBRS.cs, Neighbour(io)),
cross-validated against the engine disassembly
tools/iff-dump/r151/r151-dis-loadinterestdata.txt (LoadInterestData
walks (index,value) pairs and sthx's them into person +0x58c+2*index;
record halfword +0 -> person word 31; person key match at record +8).

NBRS chunk data (LITTLE-ENDIAN inside a big-endian IFF chunk header):
  u32 pad | u32 version | cstr4 magic 'SRBN'/'NBRS' | u32 count
  count x record:
    i32 Unknown1  (must be 1; else 4-byte null slot, no further fields)
    i32 NVersion  (0x4: pd block = 0xa0 bytes = 80 shorts;
                   0xA: unknown3 i32 follows, pd block = 0x200 bytes
                   of which the first 88 shorts are PersonData)
    [0xA only] i32 Unknown3
    sz name + NUL, +pad to even total
    i32 MysteryZero, i32 PersonMode (0/5/9; <=0 => NO pd block)
    PersonMode>0: pd shorts as above
    i16 NeighbourID, u32 GUID, i32 (usually -1)
    i32 relCount x { i32 1, i32 key, i32 n, n x i32 }

Usage: r153-nbrs-scan.py [--full]   (writes r153-nbrs-dump.txt)
"""
import struct, sys, os, glob

ROOT = os.path.join(os.path.dirname(__file__), '..', '..', '..', 'game-data', 'The Sims')

FILES = [
    'UserData/Neighborhood.iff',
    'UserData2/Neighborhood.iff', 'UserData3/Neighborhood.iff',
    'UserData4/Neighborhood.iff', 'UserData5/Neighborhood.iff',
    'UserData6/Neighborhood.iff', 'UserData7/Neighborhood.iff',
    'UserData8/Neighborhood.iff',
    'TemplateUserData/Neighborhood.iff',
    'TemplateCommunity/NeighborhoodDesc.iff',
    'TemplateDowntown/DTDesc.iff',
    'TemplateVacation/VIDesc.iff',
    'TemplateStudiotown/STDesc.iff',
    'TemplateMagictown/MTDesc.iff',
]

INTEREST_BASE = range(46, 54)   # words 46..53 — the engine's all-10 sentinel gate
INTEREST_ALL  = range(46, 56)   # words 46..55 — full init-traits tree run
EXP_WORDS     = (13, 14, 16, 20, 26)  # Exercise/Food/Parties/Style/Hollywood


def chunks(d):
    """Old-format TS1 IFF chunk walker: BE header (tag,size,id,flags,label64)."""
    off = 64
    while off + 76 <= len(d):
        tag = d[off:off+4].decode('latin1')
        size = struct.unpack_from('>I', d, off+4)[0]
        if size < 76 or off + size > len(d):
            break
        yield tag, off, size
        off += size + (size & 1)


def parse_nbrs(data):
    """Byte-exact NBRS.cs replica. Returns (header, records, consumed)."""
    o = 0
    def u32():
        nonlocal o
        v = struct.unpack_from('<I', data, o)[0]; o += 4; return v
    def i32():
        nonlocal o
        v = struct.unpack_from('<i', data, o)[0]; o += 4; return v
    def i16():
        nonlocal o
        v = struct.unpack_from('<h', data, o)[0]; o += 2; return v
    hdr = dict(pad=u32(), version=u32(), magic=data[o:o+4].decode('latin1'))
    o += 4
    hdr['count'] = u32()
    recs = []
    for i in range(hdr['count']):
        if o >= len(data):
            break
        r = dict(idx=i, file_off=o)
        r['u1'] = i32()
        if r['u1'] != 1:
            r['null_slot'] = True
            recs.append(r)
            continue
        r['nver'] = i32()
        if r['nver'] == 0xA:
            r['unknown3'] = i32()
        start = o
        while o < len(data) and data[o] != 0:
            o += 1
        r['name'] = data[start:o].decode('latin1', 'replace')
        o += 1
        if len(r['name']) % 2 == 0:
            o += 1
        r['mystery0'] = i32()
        r['pm'] = i32()
        r['pd'] = None
        if r['pm'] > 0:
            size = 0xa0 if r['nver'] == 0x4 else 0x200
            nshort = min(88, size // 2)
            r['pd'] = list(struct.unpack_from('<%dh' % nshort, data, o))
            r['pd_file_off'] = o
            o += size
        r['nid'] = i16()
        r['guid'] = u32()
        r['neg1'] = i32()
        nrel = i32()
        r['rels'] = {}
        for _ in range(max(0, nrel)):
            i32()  # keyCount (1)
            key = i32()
            vc = i32()
            r['rels'][key] = [i32() for _ in range(max(0, vc))]
        recs.append(r)
    return hdr, recs, o


def fmt_words(words):
    return '[' + ','.join('%d' % w for w in words) + ']'


def main():
    full = '--full' in sys.argv
    out = open(os.path.join(os.path.dirname(__file__), 'r153-nbrs-dump.txt'), 'w')

    def P(s=''):
        print(s)
        out.write(s + '\n')

    total_real = total_flagged = total_touch = 0
    for rel in FILES:
        p = os.path.normpath(os.path.join(ROOT, rel))
        if not os.path.exists(p):
            P('== %s: MISSING' % rel); continue
        d = open(p, 'rb').read()
        found = False
        for tag, coff, size in chunks(d):
            if tag != 'NBRS':
                continue
            found = True
            data = d[coff+76:coff+size]
            hdr, recs, consumed = parse_nbrs(data)
            P('== %s  (NBRS chunk @0x%x datasz=%d)' % (rel, coff, len(data)))
            P('   header: pad=%d version=%#x magic=%r count=%d  consumed=%d %s' % (
                hdr['pad'], hdr['version'], hdr['magic'], hdr['count'], consumed,
                'ALIGN' if consumed == len(data) else 'MISALIGN(%d/%d)' % (consumed, len(data))))
            real = [r for r in recs if not r.get('null_slot')]
            nulls = [r for r in recs if r.get('null_slot')]
            flagged = []      # all-10 on words 46..53
            touching = []     # any nonzero in 46..55
            ten_partial = []  # some 10s in 46..53 but not all-10
            for r in real:
                pd = r['pd'] or []
                if len(pd) >= 56:
                    base = [pd[i] for i in INTEREST_BASE]
                    if all(v == 10 for v in base):
                        flagged.append(r)
                    elif any(v == 10 for v in base):
                        ten_partial.append(r)
                    if any(pd[i] != 0 for i in INTEREST_ALL):
                        touching.append(r)
            P('   records: %d parsed (%d null slots Unknown1!=1, %d real); '
              'PersonMode>0 with pd: %d' % (
                  len(recs), len(nulls), len(real),
                  sum(1 for r in real if r['pd'])))
            P('   INTEREST GATE (words 46..53 all ==10): %d record(s)%s' % (
                len(flagged), '  <<< FOUND' if flagged else '  (none)'))
            P('   partial-10s in 46..53 (not all eight): %d' % len(ten_partial))
            P('   records with ANY nonzero word in 46..55: %d' % len(touching))
            total_real += len(real)
            total_flagged += len(flagged)
            total_touch += len(touching)
            for r in real:
                pd = r['pd']
                if pd is None:
                    if full:
                        P('   [%2d] id=%-4d %-14r pm=%d nver=%#x NO-PD-BLOCK guid=%08x rels=%d'
                          % (r['idx'], r['nid'], r['name'], r['pm'], r['nver'], r['guid'], len(r['rels'])))
                    continue
                interest = pd[46:56]
                nz = {i: v for i, v in enumerate(pd) if v != 0}
                mark = ''
                if all(pd[i] == 10 for i in INTEREST_BASE):
                    mark = '  <<< ALL-10 GATE'
                P('   [%2d] id=%-4d %-14r pm=%d nver=%#x guid=%08x pd@0x%x' % (
                    r['idx'], r['nid'], r['name'], r['pm'], r['nver'], r['guid'],
                    r['pd_file_off']))
                P('        pd[46..55]=%s%s' % (fmt_words(interest), mark))
                P('        pd[13,14,16,20,26]=%s  nonzero-words(%d)=%s' % (
                    fmt_words([pd[i] if len(pd) > i else 0 for i in EXP_WORDS]),
                    len(nz), nz if full else ('...' if len(nz) > 12 else nz)))
            if nulls and full:
                P('   null slots at record idx: %s' % [r['idx'] for r in nulls])
        if not found:
            P('== %s: no NBRS chunk (chunks: %s)' % (
                rel, [c[0] for c in chunks(d)]))
    P('')
    P('TOTALS: real records=%d  all-10-on-46..53=%d  touching-46..55=%d'
      % (total_real, total_flagged, total_touch))
    out.close()
    print('\nwrote tools/iff-dump/r153/r153-nbrs-dump.txt')


if __name__ == '__main__':
    main()
