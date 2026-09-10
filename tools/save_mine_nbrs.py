#!/usr/bin/env python3
# save_mine_nbrs.py - IFF-factual shipped-save death-persistence miner (Round 80).
# Reproduces the NBRS neighbour parse EXACTLY as FreeSO's NBRS.cs (IffFile.cs is the
# chunk reader) over the shipped UserData1-8/Neighborhood.iff files so the 8-ghost
# shipped-save measurement is re-runnable and byte-factual.
#   UserData (Neighborhood 1): 8 NBRS residents with PersonData[IsGhost(68)]=1
#     (Neighbour ids 19,20,26,27,28,29,30,31; ages 9 and 27; PersonType[32]=0; two at JobLvl 4)
#   UserData2-8 (Deluxe): Grim-Reaper / genie / genie2 / tragicclown / robot in roster
#     (PersonMode=0 -> no persisted PersonData; names are the IFF-factual ship evidence)
# Delta vs engine mount (Content.Neighborhood.Neighbors, run 2): total-IsGhost=10 =
#   the 8 shipped-save ghosts + IFF NPC templates genie2 + campfireghost that the engine
#   adds at runtime via AddMissingNeighbors (PersonData derived from OBJD, not shipped file).
import struct, glob, os, sys


def read_cstr(buf, off, n):
    raw = buf[off:off+n]
    z = raw.find(b'\x00')
    if z >= 0:
        raw = raw[:z]
    return raw.rstrip(b' \t').decode('latin1'), off+n


def scan(path):
    b = open(path, 'rb').read()
    o = 64
    while len(b) - o >= 8:
        ct = b[o:o+4].decode('latin1')
        sz = struct.unpack_from('>I', b, o+4)[0]
        if ct == 'NBRS':
            return b[o+76:o+76+sz-76], sz-76
        o += 76 + (sz-76)
    return None, 0


def parse_nbrs_engine(data):
    """Byte-for-byte NBRS.cs replica (including the Unknown1!=1 short-circuit)."""
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
    pad = u32(); ver = u32(); data[o:o+4].decode('latin1'); o += 4; count = u32()
    sims = []
    for i in range(count):
        u1 = i32()
        if u1 != 1:
            sims.append({'name': None, 'partial': True})
            continue
        nver = i32()
        if nver == 0xA:
            i32()
        name_start = o
        while o < len(data) and data[o] != 0:
            o += 1
        name = data[name_start:o].decode('latin1', 'replace')
        o += 1
        if len(name) % 2 == 0:
            o += 1
        i32(); pm = i32()
        pd = None
        if pm > 0:
            n = 88 if nver == 0xA else 80
            pd = [i16() for _ in range(n)]
            sz = 0xa0 if nver == 0x4 else 0x200
            o += (sz - 2*n)
        nid = i16(); guid = u32(); i32()
        rel_count = i32()
        rels = {}
        for _ in range(rel_count):
            kc = i32(); key = i32(); vc = i32()
            rels[key] = [i32() for _ in range(max(0, vc))]
        sims.append({'name': name, 'nver': nver, 'pm': pm, 'pd': pd, 'id': nid, 'guid': guid, 'rels': rels, 'partial': False})
    return sims, o


def main():
    total_ghosts = 0
    for pth in sorted(glob.glob('game-data/The Sims/UserData*/Neighborhood.iff')):
        d, ds = scan(pth)
        sims, consumed = parse_nbrs_engine(d)
        real = [s for s in sims if not s.get('partial') and s['name']]
        ghosts = [s for s in real if s['pd'] and len(s['pd']) > 68 and s['pd'][68] != 0]
        align = 'ALIGN' if consumed == ds else 'MISALIGN(%d/%d)' % (consumed, ds)
        print('%s NBRS datalen=%d consumed=%d %s real=%d ghosts(IsGhost)=%d' % (
            os.path.basename(os.path.dirname(pth)), ds, consumed, align, len(real), len(ghosts)))
        for s in ghosts:
            print('   GHOST id=%d name=%s age=%d gender=%d jobLvl=%d ptype=%d' % (
                s['id'], s['name'], s['pd'][58], s['pd'][65], s['pd'][57], s['pd'][32]))
        total_ghosts += len(ghosts)
        if os.path.basename(os.path.dirname(pth)) == 'UserData2':
            nps = [s['name'] for s in real if s['name'] in ('grimreaper', 'genie', 'genie2', 'tragicclown', 'robot')]
            print('   shipped NPCs in roster (PersonMode=0):', nps)
    print('TOTAL shipped-save IsGhost residents across UserData1-8 =', total_ghosts)


if __name__ == '__main__':
    main()
