#!/usr/bin/env python3
"""R153 TASK 3: creation templates + GameData FARs — any writer of the all-10
interest state (person words 46..53), or person-word blocks, in staged data.

Targets (all extracted in-memory, no package deps):
  * GameData/Global/Global.far   -> PersonGlobals.iff, Global.iff (all members)
  * GameData/Objects/Objects.far -> People/*.iff (13 NPC/person files,
                                     incl. People/TemplatePerson.iff)
  * ExpansionPack5/ExpansionPack5.FAR -> templatecat.iff, templatedog.iff
  * TemplateNPCs/*.iff (loose)
  * GameData/*.iff loose (Behavior/Live/UIText/fame/magic/...)

Detection:
  1. Every BHAV-shaped blob (loose BHAV chunks AND trees embedded in 'XXXX'
     wrapper chunks, found by scanning chunk bodies for sig 0x8000..0x8003)
     is decoded; every expression whose LHS scope is MyPersonData(18)/
     StackObjectPersonData(19)/...ByTemp(30/31) is reported; literal (scope 7)
     RHS == 10 destined for words 46..53 is THE sentinel candidate.
  2. Chunk labels + STR# id-0 strings are grepped for creation-flow names
     (editperson, personed, cas, template, newperson, createperson...).
  (Static arrays are covered separately: the whole-tree 8x10-short byte sweep
   in r153 found only font/WAV coincidences - see survey doc.)

Usage: r153-gamedata-scan.py   (writes r153-gamedata-dump.txt)
"""
import os, re, struct, sys

HERE = os.path.dirname(__file__)
ROOT = os.path.normpath(os.path.join(HERE, '..', '..', '..', 'game-data', 'The Sims'))

PD_SCOPES = {18, 19, 30, 31}
# FreeSO VMExpressionOperator: writes vs tests (r153 verified vs VMExpression.cs)
WRITE_OPS = {3: '+=', 4: '-=', 5: '=', 6: '*=', 7: '/=', 9: 'setFlag', 10: 'clearFlag',
             12: '%=', 13: '&=', 18: '|=', 19: '^=', 20: 'sqrt='}
TEST_OPS = {0: '>', 1: '<', 2: '==', 8: 'flagSet', 11: '++&<', 14: '>=', 15: '<=',
            16: '!=', 17: '--&>'}
KW = re.compile(r'(editperson|personed|newperson|createperson|makeperson|createasim|\bcas\b|template)', re.I)

KNOWN = {b'rsmp', b'OBJD', b'CTSS', b'STR#', b'BHAV', b'BMP_', b'SLOT', b'DGRP',
         b'SPR2', b'SPR#', b'PALT', b'FCNS', b'TPRT', b'TPNN', b'FWAV', b'XSRC',
         b'TTAB', b'TTAs', b'GLOB', b'BCON', b'OBJf', b'CSTRI', b'OBJT', b'XXXX',
         b'Arry', b'SIMI', b'HOUS', b'FAMI', b'THMB', b'objt', b'ObjM', b'NGBH',
         b'NBRS', b'FLRm', b'WALm', b'EXPi', b'FAMs', b'FAMh', b'TPRT', b'TREE'}


def far_members(path):
    d = open(path, 'rb').read()
    if d[:8] != b'FAR!byAZ':
        return
    man = struct.unpack('<I', d[12:16])[0]
    num = struct.unpack('<I', d[man:man+4])[0]
    off = man + 4
    for i in range(num):
        dlen, dlen2, doff = struct.unpack('<III', d[off:off+12])
        nlen = struct.unpack('<I', d[off+12:off+16])[0]
        nm = d[off+16:off+16+nlen].decode('latin1', 'replace')
        yield nm, d[doff:doff+dlen]
        off += 16 + nlen


def walk_chunks(d):
    """Resyncing walker; yields (tag, off, size, cid, flags, label, dataStart)."""
    off = 64
    while off + 76 <= len(d):
        tag = d[off:off+4]
        size = struct.unpack_from('>I', d, off+4)[0]
        ok = tag in KNOWN and 76 <= size and off + size <= len(d)
        if not ok:
            for delta in (-1, 1, -2, 2):
                if d[off+delta:off+delta+4] in KNOWN:
                    s2 = struct.unpack_from('>I', d, off+delta+4)[0]
                    if 76 <= s2 and off + delta + s2 <= len(d):
                        off += delta
                        ok = True
                        break
            if not ok:
                return
            continue
        cid, flags = struct.unpack_from('<HH', d, off+8)
        label = d[off+12:off+76].split(b'\0')[0].decode('latin1', 'replace')
        yield tag.decode('latin1'), off, size, cid, flags, label, off + 76
        off += size + (size & 1)


def parse_tree_at(d, off):
    """Parse a BHAV blob at off. Returns (header, instrs) or None."""
    if off + 12 > len(d):
        return None
    sig = struct.unpack_from('<H', d, off)[0]
    if sig == 0x8003:
        if off + 13 > len(d):
            return None
        count = struct.unpack_from('<I', d, off + 9)[0]
        ioff, hdr = off + 13, (d[off+3], d[off+4], d[off+5])
    elif sig in (0x8000, 0x8001, 0x8002):
        count = struct.unpack_from('<H', d, off + 2)[0]
        hdr = (d[off+4], d[off+5], struct.unpack_from('<H', d, off + 6)[0])
        ioff = off + 12
    else:
        return None
    if count > 20000 or ioff + count * 12 > len(d):
        return None
    instrs = []
    for i in range(count):
        o = d[ioff+i*12: ioff+(i+1)*12]
        op, tp, fp = struct.unpack_from('<HBB', o, 0)
        instrs.append((i, op, tp, fp, o[4:12]))
    return hdr, instrs


def scan_iff(name, data, P, state):
    if data[:8] != b'IFF FILE':
        return
    for tag, off, size, cid, flags, label, dstart in walk_chunks(data):
        if KW.search(label) or KW.search(name):
            state['kw'].add('%s: %s %d %r' % (name, tag, cid, label))
        body = data[dstart:off+size]
        if tag == 'BHAV':
            t = parse_tree_at(data, dstart)
            if t:
                report_tree(name, 'BHAV', cid, label, t, P, state)
            continue
        if tag in ('STR#', 'CTSS'):
            continue
        # embedded trees (XXXX wrappers etc.): scan body for 0x8002 headers
        for m in re.finditer(b'\x02\x80', body):
            t = parse_tree_at(body, m.start())
            if t and t[1]:
                report_tree(name, '%s@+0x%x' % (tag, m.start()), cid, label, t, P, state, quiet=True)


def report_tree(name, where, cid, label, t, P, state, quiet=False):
    hdr, instrs = t
    hits = []
    for (i, op, tp, fp, ob) in instrs:
        if op != 2:
            continue
        L, R = struct.unpack_from('<hh', ob, 0)
        lsc, rsc, oper = ob[6], ob[7], ob[5]
        if lsc in PD_SCOPES:
            if oper in WRITE_OPS:
                marker = ''
                if rsc == 7:  # Literal RHS
                    if 46 <= L <= 55 and R == 10:
                        marker = '  <<< SENTINEL-10 WRITE TO INTEREST WORD'
                        state['sentinels'] += 1
                    elif 46 <= L <= 55:
                        marker = '  (interest-word literal write)'
                state['literals'].add('%s %s %d %r [%d] PD[%d] %s %d%s' % (
                    name, where, cid, label, i, L, WRITE_OPS[oper], R, marker))
                hits.append('     [%d] PD[%d] %s %d (scope%d)%s' % (
                    i, L, WRITE_OPS[oper], R, rsc, marker))
            elif oper in TEST_OPS:
                if rsc == 7 and 46 <= L <= 55 and R == 10:
                    state['gates'].add('%s %s %d %r [%d] PD[%d] %s 10' % (
                        name, where, cid, label, i, L, TEST_OPS[oper]))
                    hits.append('     [%d] GATE PD[%d] %s 10' % (i, L, TEST_OPS[oper]))
    if hits and not quiet:
        P('  %s %s %d %r:' % (name, where, cid, label))
        for h in hits:
            P(h)


def main():
    out = open(os.path.join(HERE, 'r153-gamedata-dump.txt'), 'w')

    def P(s=''):
        print(s); out.write(s + '\n')

    state = {'sentinels': 0, 'literals': set(), 'kw': set(), 'gates': set()}

    targets = []  # (display-name, data)
    # Global.far - all members
    for nm, data in far_members(os.path.join(ROOT, 'GameData/Global/Global.far')):
        targets.append(('Global.far!' + nm, data))
    # Objects.far - People/* only
    for nm, data in far_members(os.path.join(ROOT, 'GameData/Objects/Objects.far')):
        if nm.lower().startswith('people'):
            targets.append(('Objects.far!' + nm, data))
    # ExpansionPack5 templates
    for nm, data in far_members(os.path.join(ROOT, 'ExpansionPack5/ExpansionPack5.FAR')):
        if 'template' in nm.lower():
            targets.append(('EP5!' + nm, data))
    # TemplateNPCs loose
    npcs = os.path.join(ROOT, 'TemplateNPCs')
    for fn in sorted(os.listdir(npcs)):
        targets.append(('TemplateNPCs/' + fn, open(os.path.join(npcs, fn), 'rb').read()))
    # GameData loose IFFs
    for fn in sorted(os.listdir(os.path.join(ROOT, 'GameData'))):
        if fn.lower().endswith('.iff'):
            targets.append(('GameData/' + fn, open(os.path.join(ROOT, 'GameData', fn), 'rb').read()))

    P('scanning %d targets' % len(targets))
    for name, data in targets:
        scan_iff(name, data, P, state)

    P('')
    P('== creation-keyword label hits (%d):' % len(state['kw']))
    for s in sorted(state['kw']):
        P('   ' + s)
    P('')
    P('== literal person-data writes across corpus (%d):' % len(state['literals']))
    for s in sorted(state['literals']):
        P('   ' + s)
    P('')
    P('== gates testing interest words vs 10 (%d):' % len(state['gates']))
    for s in sorted(state['gates']):
        P('   ' + s)
    P('')
    P('SENTINEL-10 WRITES to interest words 46..55: %d' % state['sentinels'])
    out.close()
    print('\nwrote tools/iff-dump/r153/r153-gamedata-dump.txt')


if __name__ == '__main__':
    main()
