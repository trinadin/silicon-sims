#!/usr/bin/env python3
"""R153 TASK 2: do UserData/Characters/*.iff store STATIC person-data/interests?

Character files need a resyncing walker: some odd-size chunks are written
without the usual pad byte (CTSS 2000 of User00000.iff: next chunk starts at
off+size, not off+size+1). Walk re-syncs by probing off+-1/+-2 for a known tag.

For every character file we then:
  * list all chunks (BHAV ids 4096 Main, 4097 'init tree', 4098 'load tree',
    4100 'init traits', OBJD 128, CTSS 2000, STR# 200/300/304, BMP_, GLOB, SLOT)
  * decode every BHAV instruction and report person-data writes
    (op 2 expression with LhsScope/RhsScope per FreeSO VMExpression layout;
    op 8 random) - flag any literal 10 destined for person words 46..53
  * dump the OBJD data as LE u16 fields and flag runs of small values that
    could be an interests block (esp. eight consecutive 10s)

Usage: r153-charscan.py [--dir <chars-dir>] ... (default: UserData/Characters;
      also run for TemplateUserData/Characters). Writes r153-chars-dump.txt.
"""
import struct, sys, os, glob

HERE = os.path.dirname(__file__)
ROOT = os.path.normpath(os.path.join(HERE, '..', '..', '..', 'game-data', 'The Sims'))

KNOWN = {b'rsmp', b'OBJD', b'CTSS', b'STR#', b'BHAV', b'BMP_', b'SLOT', b'DGRP',
         b'SPR2', b'SPR#', b'PALT', b'FCNS', b'TPRT', b'TPNN', b'FWAV', b'XSRC',
         b'TTAB', b'TTAs', b'GLOB', b'BCON', b'OBJf', b'CSTRI'}


def walk(d):
    off = 64
    while off + 76 <= len(d):
        tag = d[off:off+4]
        size = struct.unpack_from('>I', d, off+4)[0]
        ok = tag in KNOWN and 76 <= size and off + size <= len(d)
        if not ok:
            for delta in (-1, 1, -2, 2):
                t2 = d[off+delta:off+delta+4]
                if t2 in KNOWN:
                    s2 = struct.unpack_from('>I', d, off+delta+4)[0]
                    if 76 <= s2 <= len(d) and off + delta + s2 <= len(d):
                        off += delta
                        ok = True
                        break
            if not ok:
                yield ('!!!!', off, None, None, None, d[off:off+4])
                return
            continue
        cid, flags = struct.unpack_from('>HH', d, off+8)
        label = d[off+12:off+76].split(b'\0')[0].decode('latin1', 'replace')
        yield (tag.decode('latin1'), off, size, cid, flags, label)
        off += size + (size & 1)


def decode_bhav(d, doff, dsz):
    """FreeSO BHAV.cs replica: sig 0x8002 => count u16 @+2, Type @+4, Args @+5,
    Locals u16 @+6, Version u16 @+8, pad2; instructions 12B from +12
    (opcode u16 LE, truePtr u8, falsePtr u8, operands[8])."""
    h = d[doff:doff+12]
    sig = struct.unpack_from('<H', h, 0)[0]
    if sig == 0x8003:
        count = struct.unpack_from('<I', h, 9)[0]
        ioff, meta = doff + 13, (h[3], h[4], h[5])
    else:
        count = struct.unpack_from('<H', h, 2)[0]
        meta = (h[4], h[5], struct.unpack_from('<H', h, 6)[0])
        ioff = doff + 12
    instrs = []
    for i in range(count):
        o = d[ioff+i*12: ioff+(i+1)*12]
        if len(o) < 12:
            break
        op, tp, fp = struct.unpack_from('<HBB', o, 0)
        instrs.append((i, op, tp, fp, o[4:12]))
    return meta, instrs


SCOPE_NAMES = {0: 'MyAttributes', 1: 'StackObjAttributes', 3: 'MyObject', 4: 'StackObject',
               6: 'Global', 7: 'Literal', 8: 'Temps', 9: 'Parameters', 10: 'StackObjID',
               18: 'MyPersonData', 19: 'StackObjPersonData', 25: 'Local',
               30: 'MyPersonDataByTemp', 31: 'StackObjPersonDataByTemp',
               32: 'NeighborPersonData'}  # FreeSO VMVariableScope.cs values


def main():
    out = open(os.path.join(HERE, 'r153-chars-dump.txt'), 'w')

    def P(s=''):
        print(s); out.write(s + '\n')

    dirs = sys.argv[1:] or [os.path.join(ROOT, 'UserData', 'Characters'),
                            os.path.join(ROOT, 'TemplateUserData', 'Characters')]
    ten_hits = 0
    for cdir in dirs:
        files = sorted(glob.glob(os.path.join(cdir, '*.iff')))
        P('### %s (%d files)' % (cdir, len(files)))
        for p in files:
            d = open(p, 'rb').read()
            P('-- %s (len %d)' % (os.path.basename(p), len(d)))
            for tag, off, size, cid, flags, label in walk(d):
                if tag == '!!!!':
                    P('   WALK-STOP at 0x%x raw=%r' % (off, label[:8]))
                    continue
                if tag == 'BHAV':
                    meta, instrs = decode_bhav(d, off + 76, size - 76)
                    P('   BHAV %d %-12r typ=%s args=%s locs=%s n=%d' % (
                        cid, label, meta[0], meta[1], meta[2], len(instrs)))
                    for (i, op, tp, fp, ob) in instrs:
                        L, R = struct.unpack_from('<hh', ob, 0)
                        note = ''
                        if op == 2:
                            opd, oper, lsc, rsc = ob[4], ob[5], ob[6], ob[7]
                            lname = SCOPE_NAMES.get(lsc, str(lsc))
                            rname = SCOPE_NAMES.get(rsc, str(rsc))
                            if lsc in (18, 19, 30, 31):
                                note = ' <<< PERSON-DATA WRITE idx=%d <- %s %d' % (L, rname, R)
                                if 46 <= L <= 55 and rsc == 7 and R == 10:
                                    note += ' VALUE=10 (INTEREST SENTINEL CANDIDATE)'
                                    ten_hits += 1
                            note = (' expr %s[%d] %s %s[%d] op=%d packed=%#04x%s'
                                    % (lname, L, '<-', rname, R, oper, opd, note))
                        elif op == 8:
                            dst, dsc, rng, rsc = struct.unpack_from('<hhHH', ob, 0)
                            note = ' random dest=[%d]%d range=[%d]%d' % (dsc, dst, rsc, rng)
                        else:
                            note = ' raw=%s' % ob.hex()
                        if 'PERSON-DATA' in note or op in (2, 8) or cid in (4096, 4097, 4098):
                            P('     [%2d] op=%-2d T=%-3d F=%-3d |%s' % (i, op, tp, fp, note))
                elif tag == 'OBJD':
                    body = d[off+76:off+size]
                    fields = struct.unpack_from('<%dh' % (len(body)//2), body, 0)
                    P('   OBJD %d %r datasz=%d u16fields=%d' % (cid, label, len(body), len(fields)))
                    P('      fields=%s' % list(fields))
                else:
                    P('   %s id=%s sz=%s %r' % (tag, cid, size, label))
    P('')
    P('literal-10 person-word writes (46..55) across corpus: %d' % ten_hits)
    out.close()
    print('\nwrote tools/iff-dump/r153/r153-chars-dump.txt')


if __name__ == '__main__':
    main()
