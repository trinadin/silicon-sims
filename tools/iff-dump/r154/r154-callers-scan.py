#!/usr/bin/env python3
"""R154 TASK 1+2: full-corpus caller scan for BHAV 9761 ('Convert Interests to
0-1000') and BHAV 39200 ('Add Hot Date Interests').

Sweeps EVERY file under game-data/The Sims: 974 loose IFFs + all 65 FAR
archives (FAR!byAZ, manifest-driven per-member reads; nested FARs supported).

Per IFF (loose or in a FAR):
  * BHAV chunks (and BHAV-shaped blobs embedded in other chunks, e.g. XXXX
    wrappers): every instruction whose opcode == 9761/39200 is a CALL HIT.
    Calls are encoded AS the instruction opcode (VMThread.ExecuteInstruction:
    opcode>=256 -> ExecuteSubRoutine; >=8192 -> SemiGlobal file, 4096..8191 ->
    local file, <4096 -> global.iff).
  * TTAB chunks: interaction ActionFunction/TestFunction entry points
    (parsed per FreeSO TTAB.cs; version<=3 or field-encoded bodies get a raw
    u16 fallback scan).
  * STR# chunks: parsed and indexed for run_tree_by_name (opcode 28) string
    resolution; every opcode-28 instruction in the corpus is resolved to its
    text (local -> semiglobal -> global scope chain, per VMRunTreeByName.cs)
    and matched against the two routine names.
  * GLOB chunks: records each file's semi-global filename (call resolution).
  * All other chunks: raw LE-u16 scan for 9761/39200 (name->id tables etc.),
    media chunk types skipped; plus raw ASCII search for the routine names.

Also builds r154-bhav-index.json: {container -> {bhavid -> {label, args,
locals, instrs(bool list of opcodes only? no: full instr tuples)}}} for the
caller-of-caller recursion in r154-caller-graph.py.

Usage: r154-callers-scan.py   (writes r154-callers-dump.txt + r154-bhav-index.json)
"""
import json
import os
import re
import struct
import sys

HERE = os.path.dirname(__file__)
ROOT = os.path.normpath(os.path.join(HERE, '..', '..', '..', 'game-data', 'The Sims'))

TARGETS = {8486: "Convert Interests to 0-1000", 8345: "Add Hot Date Interests"}
TARGET_NAMES = [b'convert interests', b'add hot date']

ID2_5 = b'IFF FILE 2.5:TYPE FOLLOWED BY SIZE JAMIE DOORNBOS & MAXIS 1'
ID2_0 = b'IFF FILE 2.0:TYPE FOLLOWED BY SIZE JAMIE DOORNBOS & MAXIS 1'

KNOWN_TAGS = {b'rsmp', b'OBJD', b'CTSS', b'STR#', b'BHAV', b'BMP_', b'SLOT', b'DGRP',
              b'SPR2', b'SPR#', b'PALT', b'FCNS', b'TPRT', b'TPNN', b'FWAV', b'XSRC',
              b'TTAB', b'TTAs', b'GLOB', b'BCON', b'OBJf', b'CSTRI', b'OBJT', b'XXXX',
              b'Arry', b'SIMI', b'HOUS', b'FAMI', b'THMB', b'objt', b'ObjM', b'NGBH',
              b'NBRS', b'FLRm', b'WALm', b'EXPi', b'FAMs', b'FAMh', b'TREE', b'FCNS',
              b'PRPT', b'TabT', b'TTP ', b'STRn', b'PLOt', b'CHDF', b'CHNM', b'CAT',
              b'FCAS', b'FWAV', b'LANE', b'TTAb', b'TPRP', b'Optn'}
MEDIA_TAGS = {b'BMP_', b'DGRP', b'SPR2', b'SPR#', b'PALT', b'FWAV', b'XSRC', b'THMB'}

# ---------- corpus walkers ----------

def far_members(path, fh):
    """Yield (name, data) for each member of a FAR!byAZ archive.
    `fh` is an open file handle; manifest-driven, per-member reads."""
    fh.seek(0)
    magic = fh.read(8)
    if magic != b'FAR!byAZ':
        return
    fh.seek(12)
    man = struct.unpack('<I', fh.read(4))[0]
    fh.seek(man)
    num = struct.unpack('<I', fh.read(4))[0]
    for _ in range(num):
        hdr = fh.read(16)
        if len(hdr) < 16:
            return
        dlen, dlen2, doff = struct.unpack('<III', hdr[:12])
        nlen = struct.unpack('<I', hdr[12:16])[0]
        nm = fh.read(nlen).decode('latin1', 'replace')
        next_pos = fh.tell()  # manifest position for the NEXT entry
        base = os.path.basename(nm).lower()
        if base.endswith(('.wav', '.mp3', '.bmp', '.jpg', '.gif', '.xa', '.png')):
            continue
        fh.seek(doff)
        data = fh.read(dlen)
        fh.seek(next_pos)
        yield nm, data


def walk_chunks(d):
    """Resyncing chunk walker (r153 law). Yields (tag, off, size, cid, flags,
    label, dataStart)."""
    off = 64
    n = len(d)
    while off + 76 <= n:
        tag = d[off:off+4]
        size = struct.unpack_from('>I', d, off+4)[0]
        ok = tag in KNOWN_TAGS and 76 <= size and off + size <= n
        if not ok:
            found = False
            for delta in (-1, 1, -2, 2):
                if d[off+delta:off+delta+4] in KNOWN_TAGS:
                    s2 = struct.unpack_from('>I', d, off+delta+4)[0]
                    if 76 <= s2 and off + delta + s2 <= n:
                        off += delta
                        found = True
                        break
            if not found:
                return
            continue
        cid, flags = struct.unpack_from('<HH', d, off+8)
        label = d[off+12:off+76].split(b'\0')[0].decode('latin1', 'replace')
        yield tag.decode('latin1'), off, size, cid, flags, label, off + 76
        off += size + (size & 1)


# ---------- BHAV parsing ----------

def parse_bhav(d, off):
    """Parse a BHAV blob at `off` (chunk data start). Returns dict or None."""
    if off + 12 > len(d):
        return None
    sig = struct.unpack_from('<H', d, off)[0]
    r = {'sig': sig, 'type': 0, 'args': 0, 'locals': 0, 'cacheable': 0}
    if sig == 0x8003:
        if off + 13 > len(d):
            return None
        r['type'] = d[off+2]
        r['args'] = d[off+3]
        r['locals'] = d[off+4]
        r['version'] = struct.unpack_from('<H', d, off+8)[0]
        count = struct.unpack_from('<I', d, off+9)[0]
        ioff = off + 13
    elif sig in (0x8000, 0x8001, 0x8002):
        count = struct.unpack_from('<H', d, off+2)[0]
        if sig == 0x8002:
            r['type'] = d[off+4]
            r['args'] = d[off+5]
            r['locals'] = struct.unpack_from('<H', d, off+6)[0]
            r['version'] = struct.unpack_from('<H', d, off+8)[0]
        ioff = off + 12
    else:
        return None
    if count > 20000 or ioff + count * 12 > len(d):
        return None
    instrs = []
    for i in range(count):
        o = d[ioff+i*12: ioff+(i+1)*12]
        op, tp, fp = struct.unpack_from('<HBB', o, 0)
        a = struct.unpack_from('<hhhh', o, 4)
        instrs.append((op, tp, fp, a[0], a[1], a[2], a[3]))
    r['instrs'] = instrs
    return r


# ---------- STR# parsing ----------

def parse_str(d):
    """Parse STR# chunk data. Returns list of strings (language set 0) or None."""
    if len(d) < 4:
        return None
    fmt = struct.unpack_from('<h', d, 0)[0]
    if fmt not in (0, -1):
        return None
    num = struct.unpack_from('<H', d, 2)[0]
    if num > 65535:
        return None
    off = 4
    out = []
    try:
        for _ in range(num):
            ln = d[off]
            off += 1
            if fmt == -1:
                # cstr: length byte then NUL-terminated
                s = d[off:off+ln]
                off += ln
                out.append(s.decode('latin1', 'replace').rstrip('\x00'))
                if d[off-1:off] != b'\x00':
                    # some writers omit NUL if odd? resync defensively
                    pass
            else:
                out.append(d[off:off+ln].decode('latin1', 'replace'))
                off += ln
    except IndexError:
        return out or None
    return out


# ---------- TTAB parsing ----------

def parse_ttab(d):
    """Parse TTAB per FreeSO TTAB.cs. Returns (interactions, fullParse) where
    interactions = list of (actionFunc, testFunc, ttaIndex, flags)."""
    if len(d) < 4:
        return None, False
    count, version = struct.unpack_from('<HH', d, 0)
    if count == 0 or count > 500:
        return None, False
    if version <= 3:
        return None, False  # FreeSO refuses; old format
    off = 4
    field_encoded = False
    if 9 <= version <= 10:
        cc = d[off] if off < len(d) else 0
        off += 1
        field_encoded = (cc == 1)
    if field_encoded:
        return None, False
    ints = []
    try:
        for _ in range(count):
            action, test = struct.unpack_from('<HH', d, off)
            off += 4
            mc, flags, tta = struct.unpack_from('<III', d, off)
            off += 12
            if mc > 32:
                return None, False
            if version > 6:
                off += 4  # attenuation code
            off += 4     # attenuation float
            off += 4     # autonomy threshold
            off += 4     # joining index
            off += mc * (6 if version > 6 else 4)
            if version > 9:
                off += 4  # flags2 (TSO)
            if off > len(d):
                return None, False
            ints.append((action, test, tta, flags))
    except struct.error:
        return None, False
    return ints, True


def u16_hits(d, target):
    """All offsets of LE-u16 target in d."""
    pat = struct.pack('<H', target)
    out = []
    i = d.find(pat)
    while i != -1 and len(out) < 64:
        out.append(i)
        i = d.find(pat, i+1)
    return out


def hexctx(d, i, before=6, after=10):
    lo = max(0, i-before)
    return d[lo:i+after].hex(' ')


# ---------- main scan ----------

def main():
    outp = os.path.join(HERE, 'r154-callers-dump.txt')
    out = open(outp, 'w')

    def P(s=''):
        print(s)
        out.write(s + '\n')

    # container index: name(lower) -> {'iff': display, 'glob': name, 'bhavs': {id: bhav}}
    index = {}
    call_hits = []       # (file, chunkdesc, bhavid, label, instridx, instr)
    ttab_hits = []       # (file, chunkdesc, kind, actionFunc, testFunc, ttaIndex)
    embedded_hits = []   # non-BHAV chunk raw u16 hits
    name_hits = []       # ASCII routine-name hits (file, chunk, context)
    byname_calls = []    # every opcode-28 instruction (file, bhav, idx, operand)

    files_scanned = 0
    iff_scanned = 0
    bhavs_scanned = 0

    def scan_iff(display, d):
        nonlocal iff_scanned, bhavs_scanned
        if d[:8] != b'IFF FILE':
            return
        iff_scanned += 1
        fname = os.path.basename(display).lower()
        cinfo = index.setdefault(fname, {'display': display, 'glob': None, 'bhavs': {}})
        seen_chunks = set()
        for tag, off, size, cid, flags, label, dstart in walk_chunks(d):
            body = d[dstart:off+size]
            seen_chunks.add((tag, cid))
            if tag == 'BHAV':
                b = parse_bhav(d, dstart)
                if not b:
                    continue
                bhavs_scanned += 1
                cinfo['bhavs'].setdefault(cid, b)
                for i, ins in enumerate(b['instrs']):
                    if ins[0] in TARGETS:
                        call_hits.append((display, 'BHAV %d %r' % (cid, label), cid, label, i, ins, b))
                    if ins[0] == 28:
                        byname_calls.append((display, cid, label, i, ins))
            elif tag == 'GLOB':
                # semi-global filename: pascal string (len byte < 48) or cstr
                if body and body[0] < 48:
                    g = body[1:1 + body[0]].decode('latin1', 'replace').strip()
                else:
                    g = body.split(b'\0')[0].decode('latin1', 'replace').strip()
                if g:
                    cinfo['glob'] = g
            elif tag == 'TTAB':
                ints, full = parse_ttab(body)
                if full:
                    for (act, tst, tta, fl) in ints:
                        if act in TARGETS or tst in TARGETS:
                            ttab_hits.append((display, 'TTAB %d %r' % (cid, label), 'parsed', act, tst, tta, fl))
                else:
                    for t in TARGETS:
                        for h in u16_hits(body, t):
                            ttab_hits.append((display, 'TTAB %d %r' % (cid, label), 'RAW@+%d ctx[%s]' % (h, hexctx(body, h)), t, None, None, None))
            else:
                # embedded BHAV-shaped blobs (XXXX wrappers, TREE tables, etc.)
                for m in re.finditer(b'[\x00\x01\x02\x03]\x80', body):
                    sig = struct.unpack_from('<H', body, m.start())[0]
                    if sig not in (0x8000, 0x8001, 0x8002, 0x8003):
                        continue
                    b = parse_bhav(body, m.start())
                    if b and len(b['instrs']) >= 2:
                        for i, ins in enumerate(b['instrs']):
                            if ins[0] in TARGETS:
                                embedded_hits.append((display, '%s %d %r @+0x%x BHAVsig-0x%04x blob instr#%d' % (tag, cid, label, m.start(), sig, i), ins))
                # raw u16 scan (skip media chunks and BHAV/TTAB/STR handled above)
                if tag not in MEDIA_TAGS:
                    for t in TARGETS:
                        for h in u16_hits(body, t):
                            embedded_hits.append((display, '%s %d %r @+%d' % (tag, cid, label, h), (t, hexctx(body, h))))
            # ASCII routine-name search on every chunk (incl. STR#/TTAs)
            low = body.lower()
            for nm in TARGET_NAMES:
                p = low.find(nm)
                if p != -1:
                    name_hits.append((display, '%s %d %r' % (tag, cid, label), nm.decode(), body[max(0, p-8):p+48]))
                # also scan the 64-byte chunk label
            if any(nm.decode() in label.lower() for nm in TARGET_NAMES):
                name_hits.append((display, '%s %d LABEL' % (tag, cid), 'chunk-label', label))
        # index STR# chunks for by-name resolution
        strs = {}
        for tag, off, size, cid, flags, label, dstart in walk_chunks(d):
            if tag == 'STR#':
                parsed = parse_str(d[dstart:off+size])
                if parsed is not None:
                    strs[cid] = parsed
        cinfo['strs'] = {k: v[:400] for k, v in strs.items()}

    # ---- corpus enumeration ----
    todo = []
    for dirpath, _dirnames, filenames in os.walk(ROOT):
        for fn in sorted(filenames):
            todo.append(os.path.join(dirpath, fn))

    P('r154 caller scan — corpus: %d files under %s' % (len(todo), ROOT))
    P('targets: ' + ', '.join('%d=%r' % (k, v) for k, v in TARGETS.items()))
    P('')

    for path in todo:
        files_scanned += 1
        base = os.path.basename(path).lower()
        rel = os.path.relpath(path, ROOT)
        try:
            if base.endswith('.far'):
                with open(path, 'rb') as fh:
                    for nm, data in far_members(path, fh):
                        lnm = nm.lower()
                        if lnm.endswith('.far'):
                            # nested FAR: walk in-memory
                            import io as _io
                            fh2 = _io.BytesIO(data)
                            for nm2, data2 in far_members(nm, fh2):
                                if os.path.basename(nm2).lower().endswith('.iff'):
                                    scan_iff('%s!%s!%s' % (rel, nm, nm2), data2)
                        elif lnm.endswith('.iff'):
                            scan_iff('%s!%s' % (rel, nm), data)
            elif base.endswith(('.iff', '.fam')):
                with open(path, 'rb') as fh:
                    scan_iff(rel, fh.read())
        except Exception as e:
            P('!! ERROR scanning %s: %r' % (rel, e))

    # ---- report ----
    P('files walked: %d, IFFs scanned: %d, BHAV chunks: %d' % (files_scanned, iff_scanned, bhavs_scanned))
    P('containers indexed: %d' % len(index))
    P('')

    P('=== A. DIRECT CALL HITS (instruction opcode == target) : %d ===' % len(call_hits))
    for (display, cdesc, cid, label, i, ins, b) in call_hits:
        P('%s :: %s :: instr[%d] opcode=%d (CALL to %d %r) args=%s true=%d false=%d' % (
            display, cdesc, i, ins[0], ins[0], TARGETS[ins[0]], ins[3:], ins[1], ins[2]))
        lo = max(0, i-5)
        for j in range(lo, i):
            P('    ctx[%d] op=%d T=%d F=%d args=%s' % (j, b['instrs'][j][0], b['instrs'][j][1], b['instrs'][j][2], b['instrs'][j][3:]))
    P('')

    P('=== B. TTAB ENTRY-POINT HITS : %d ===' % len(ttab_hits))
    for h in ttab_hits:
        P('  %s' % (h,))
    P('')

    P('=== C. EMBEDDED/RAW u16 HITS in non-BHAV chunks : %d ===' % len(embedded_hits))
    seen = set()
    for (display, cdesc, ins) in embedded_hits:
        key = (display, cdesc)
        if key in seen:
            continue
        seen.add(key)
        P('  %s :: %s :: %s' % (display, cdesc, ins))
    P('')

    P('=== D. ROUTINE-NAME ASCII HITS : %d ===' % len(name_hits))
    for (display, cdesc, nm, ctx) in name_hits:
        P('  %s :: %s :: %r :: ctx=%r' % (display, cdesc, nm, ctx))
    P('')

    P('=== E. run_tree_by_name (opcode 28) resolution vs routine names ===')
    P('(full table in r154-byname-dump.txt; matches printed here)')
    matched = 0
    for (display, cid, label, i, ins) in byname_calls:
        table, scope = ins[3] & 0xFFFF, ins[4] & 0xFF
        sid, dest = ins[5] & 0xFF, ins[6] & 0xFF
        txt = resolve_byname(display, cid, table, scope, sid, index)
        if txt and any(nm.decode('latin1') in txt.lower() for nm in TARGET_NAMES):
            matched += 1
            P('  MATCH %s :: BHAV %d %r instr[%d] scope=%d table=%d id=%d dest=%d -> %r' % (
                display, cid, label, i, scope, table, sid, dest, txt))
    P('  matches: %d / %d run_tree_by_name instructions corpus-wide' % (matched, len(byname_calls)))
    P('')

    # save index + byname table for later scripts
    slim = {}
    for fname, cinfo in index.items():
        slim[fname] = {
            'display': cinfo['display'],
            'glob': cinfo.get('glob'),
            'bhavs': {str(cid): {'label': '', 'args': b['args'], 'locals': b['locals'],
                                 'ninstr': len(b['instrs']),
                                 'instrs': b['instrs']}
                      for cid, b in cinfo.get('bhavs', {}).items()},
            'strs': cinfo.get('strs', {}),
        }
    with open(os.path.join(HERE, 'r154-bhav-index.json'), 'w') as f:
        json.dump(slim, f)

    # full byname table
    with open(os.path.join(HERE, 'r154-byname-dump.txt'), 'w') as bf:
        bf.write('every run_tree_by_name (opcode 28) instruction, resolved\n')
        for (display, cid, label, i, ins) in sorted(byname_calls):
            table, scope = ins[3] & 0xFFFF, ins[4] & 0xFF
            sid, dest = ins[5] & 0xFF, ins[6] & 0xFF
            txt = resolve_byname(display, cid, table, scope, sid, index)
            bf.write('%s :: BHAV %d %r instr[%d] scope=%d table=%d id=%d dest=%d -> %r\n' % (
                display, cid, label, i, scope, table, sid, dest, txt))
    out.close()
    print('\nwrote', outp)


def resolve_byname(display, bhav_cid, table, scope, sid, index):
    """Follow VMRunTreeByName.cs: scope 1 = global STR table; else local file,
    with semi-global fallback when the running routine id >= 8192."""
    fname = os.path.basename(display).lower()
    cinfo = index.get(fname)
    if not cinfo:
        return None
    if scope == 1:
        g = index.get('global.iff')
        if g and sid:
            ss = g.get('strs', {}).get(table)
            if ss and 0 < sid <= len(ss):
                return ss[sid-1]
        return None
    txt = None
    if bhav_cid >= 8192 and cinfo.get('glob'):
        gname = os.path.basename(cinfo['glob']).lower()
        if not gname.endswith('.iff'):
            gname += '.iff'
        g = index.get(gname)
        if g:
            ss = g.get('strs', {}).get(table)
            if ss and 0 < sid <= len(ss):
                txt = ss[sid-1]
    if txt is None:
        ss = cinfo.get('strs', {}).get(table)
        if ss and 0 < sid <= len(ss):
            txt = ss[sid-1]
    return txt


if __name__ == '__main__':
    main()
