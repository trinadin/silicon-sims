#!/usr/bin/env python3
"""R124 catalog item DESCRIPTION panel canon. Four sections:

A) STR# 159 'ObjectTTs' + STR# 160 'CatalogRatings' — byte-verbatim dumps from
   GameData/UIText.iff (reuses the r122 decoder verbatim). 159 turned out to be
   LIVE-MODE object hover tooltips ("There are no actions available"...) — NOT
   catalog — so it is archived here for a future live-tooltip round; 160 is the
   description panel's rating/skill/restriction caption table (7 'X: %d' motive
   formats, 7 '+ Skill' lines, 6 usage-restriction flags).

B) Description plaque art pins: cpanel\\Backgrounds\\PopupInfo.BMP (kCatalogPopupBack
   = the item-info popup plaque, 559x127 RLE8) + PopupInfoTiles.bmp
   (kCatalogPopupBackTiles, 36x36). Byte-verbatim extracts + sha256 + an RLE8
   structural decode (palette census, per-column/row distinct-color profile,
   ASCII downsample) to derive how the plaque composes (frame vs uniform fill).

C) EMPIRICAL OBJD MiscFlags scan (field 89) across EVERY object IFF on disk
   (Objects.far + 7 expansion FARs + Deluxe/Shared/Downloads + loose UserObjects
   .iffs): master objects only (SubIndex==0), with their CTSS[0] catalog name.
   Histogram of each set bit + example names — the restriction-flag bit map is
   DERIVED FROM THIS DATA (same method as R122's RoomFlags), never guessed.
   Also samples CTSS[0]/[1]/[2] + OBJD Price for anchor objects to settle
   whether CTSS[2] is the original price string.

D) The derived-bit conclusion block (written after reading section C output;
   the scan file is regenerated verbatim each run — edit CONCLUSIONS below only
   with data in hand).

Writes r124-descpanel-strs.txt, r124-art-canon.txt, r124-objdflags-scan.txt,
art/*.bmp extracts. FAILS LOUDLY (exit 1) if any pinned member is missing.
"""
import struct, hashlib, sys, os, glob

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
UITEXT = os.path.join(ROOT, 'game-data/The Sims/GameData/UIText.iff')
UIFAR = os.path.join(ROOT, 'game-data/The Sims/UIGraphics/UIGraphics.far')
GAMEDATA = os.path.join(ROOT, 'game-data/The Sims')

ART_MEMBERS = [
    ('cpanel\\Backgrounds\\PopupInfo.BMP',      'kCatalogPopupBack — item description plaque'),
    ('cpanel\\Backgrounds\\PopupInfoTiles.bmp', 'kCatalogPopupBackTiles — plaque tile fill'),
]

# anchor objects for the CTSS price sample (name substrings, case-insensitive)
ANCHORS = ['stove', 'refrigerator', 'bed', 'toilet', 'chess', 'pool table',
           'piano', 'aquarium', 'scratching', 'dog', 'pet', 'toy']


def walk_far_manifest(data):
    """Manifest-only FAR1 walk (no byte slicing): yields (name, dlen, doff)."""
    if len(data) < 20 or data[:8] != b'FAR!byAZ':
        return
    man = struct.unpack('<I', data[12:16])[0]
    if man + 4 > len(data):
        return
    num = struct.unpack('<I', data[man:man + 4])[0]
    off = man + 4
    for _ in range(num):
        if off + 16 > len(data):
            break
        dlen, d2, doff, nlen = struct.unpack('<IIII', data[off:off + 16])
        nm = data[off + 16:off + 16 + nlen].decode('latin1', 'replace')
        off += 16 + nlen
        if dlen != d2 or doff + dlen > len(data):
            continue
        yield nm, dlen, doff


def rle8_ascii(raw, cols=88, rows=18):
    """Decode an 8-bit RLE8 BMP; return (palette, pixels, ascii_rows, facts)."""
    if raw[:2] != b'BM':
        return None, None, ['NOT A BMP (magic %r)' % raw[:2]], {}
    data_off = struct.unpack('<I', raw[10:14])[0]
    hdr_size = struct.unpack('<I', raw[14:18])[0]
    w, h = struct.unpack('<ii', raw[18:26])
    bpp = struct.unpack('<H', raw[28:30])[0]
    comp = struct.unpack('<I', raw[30:34])[0]
    clr_used = struct.unpack('<I', raw[46:50])[0] or 256
    pal_off = 14 + hdr_size
    pal = [struct.unpack('<BBBB', raw[pal_off + 4 * i: pal_off + 4 * i + 4]) for i in range(clr_used)]
    facts = {'w': w, 'h': h, 'bpp': bpp, 'comp': comp, 'colors': clr_used,
             'dataoff': data_off, 'payload': len(raw) - data_off}
    if comp != 1 or bpp != 8:
        facts['note'] = 'not RLE8 (comp=%d bpp=%d) — no pixel decode' % (comp, bpp)
        return pal, None, [], facts
    px = [[0] * w for _ in range(abs(h))]
    top_down = h < 0
    H = abs(h)
    pos = data_off
    x = y = 0
    while pos < len(raw):
        a, b = raw[pos], raw[pos + 1]
        pos += 2
        if a == 0:
            if b == 0:
                y += 1; x = 0; continue
            if b == 1:
                break
            if b == 2:
                dx, dy = raw[pos], raw[pos + 1]; pos += 2
                x += dx; y += dy; continue
            for i in range(b):
                if x < w:
                    row = px[y if top_down else H - 1 - y]
                    row[x] = raw[pos + i]
                x += 1
            pos += b + (b & 1)  # b literal bytes, padded to a 16-bit boundary
        else:
            for i in range(a):
                if x < w and y < H:
                    row = px[y if top_down else H - 1 - y]
                    row[x] = b
                x += 1
    # luminance-ordered char map over USED palette indices
    used = sorted({v for row in px for v in row})
    lum = {i: 0.3 * pal[i][0] + 0.6 * pal[i][1] + 0.1 * pal[i][2] for i in range(len(pal))}
    order = sorted(used, key=lambda i: -lum[i])
    chars = ' .:-=+*#%@'
    cmap = {i: chars[min(9, order.index(i) * 10 // max(1, len(order)))] for i in used}
    ascii_rows = []
    for r in range(rows):
        sy = int(r * H / rows)
        line = ''.join(cmap.get(px[sy][int(c * w / cols)], '?') for c in range(cols))
        ascii_rows.append(line)
    # per-column / per-row distinct-color census (border detection)
    colsets = [len({px[r][c] for r in range(0, H, max(1, H // 24))}) for c in range(w)]
    rowsets = [len(set(row)) for row in px]
    facts['col_uniform_span'] = next((c for c in range(w) if colsets[c] <= 1), -1)
    facts['row_uniform_span'] = next((r for r in range(H) if rowsets[r] <= 1), -1)
    facts['used_indices'] = len(used)
    facts['col_profile'] = ' '.join(str(colsets[c]) for c in range(0, w, max(1, w // 40)))
    return pal, px, ascii_rows, facts


def parse_objd(body):
    """[u32 version] then LE u16 fields — the field COUNT IS NOT IN THE FILE;
    FreeSO OBJD.cs derives it from the version (136->80, 138->106, 139->96,
    140/141->97, 142->105 named-field ushorts, then numFields-2 into RawData)
    and fields start at body+4. Mirrors the engine reader exactly."""
    if len(body) < 12:
        return None
    ver, = struct.unpack('<I', body[0:4])
    num = {136: 80, 138: 106, 139: 96, 140: 97, 141: 97, 142: 105}.get(ver, 80)
    n = num - 2
    if n < 40 or 4 + 2 * n > len(body):
        return None
    f = struct.unpack('<%dH' % n, body[4:4 + 2 * n])
    return {'ver': ver, 'fields': f}


def ctss_entries(chunk_body):
    """lang-1 values of a CTSS/STR# body (format -3 or 0)."""
    try:
        import importlib.util
        spec = importlib.util.spec_from_file_location(
            'r122mod', os.path.join(HERE, '..', 'r122', 'make_r122_buycat_canon.py'))
        m = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(m)
        fc, ents, _ = m.decode_str_body(chunk_body)
        return [v.decode('mac_roman', 'replace') for l, v, _ in ents if l == 1 or l is None]
    except Exception:
        return []


_STR_MOD = None
def str_mod():
    global _STR_MOD
    if _STR_MOD is None:
        import importlib.util
        spec = importlib.util.spec_from_file_location(
            'r122mod', os.path.join(HERE, '..', 'r122', 'make_r122_buycat_canon.py'))
        _STR_MOD = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(_STR_MOD)
    return _STR_MOD


def main():
    out = open(os.path.join(HERE, 'r124-objdflags-scan.txt'), 'w')
    def say(s=''):
        out.write(s + '\n')

    # ---------- A) STR# 159/160 ------------------------------------------------
    m = str_mod()
    data = open(UITEXT, 'rb').read()
    chunks = m.walk_chunks(data)
    strs_out = []
    strs_out.append('R124 description-panel STR# canon — UIText.iff STR# 159 + STR# 160')
    strs_out.append('Source: %s (%d bytes; full-file sha256 %s)'
                    % (UITEXT, len(data), hashlib.sha256(data).hexdigest()))
    for tid, expect in ((159, 'ObjectTTs'), (160, 'CatalogRatings')):
        hits = [c for c in chunks if c['cc'] == b'STR#' and c['id'] == tid]
        if len(hits) != 1:
            print('FATAL: STR# %d found %d times' % (tid, len(hits))); sys.exit(1)
        m.dump_table(hits[0], data, strs_out)
        if hits[0]['label'] != expect:
            print('FATAL: STR# %d label %r != %r' % (tid, hits[0]['label'], expect)); sys.exit(1)
    with open(os.path.join(HERE, 'r124-descpanel-strs.txt'), 'w') as f:
        f.write('\n'.join(strs_out) + '\n')

    # ---------- B) plaque art ---------------------------------------------------
    uidata = open(UIFAR, 'rb').read()
    truth = {}
    for nm, dlen, doff in walk_far_manifest(uidata):
        raw = uidata[doff:doff + dlen]
        w = h = -1
        if len(raw) >= 26:
            w, h = struct.unpack('<ii', raw[18:26])
        truth[nm.lower()] = (nm, dlen, w, h, hashlib.sha256(raw).hexdigest(), raw)
    art_out = ['R124 description plaque art canon (UIGraphics.far)',
               'FAR: %s (%d bytes; full-file sha256 %s)'
               % (UIFAR, len(uidata), hashlib.sha256(uidata).hexdigest()), '']
    for name, note in ART_MEMBERS:
        t = truth.get(name.lower())
        if t is None:
            print('FATAL: missing art member %s' % name); sys.exit(1)
        nm, dlen, w, h, sha, raw = t
        safe = name.lower().replace('\\', '_').replace('/', '_')
        with open(os.path.join(HERE, 'art', safe), 'wb') as f:
            f.write(raw)
        art_out.append('=== %s ===' % nm)
        art_out.append('note: %s' % note)
        art_out.append('len %d  dims %dx%d  sha256 %s' % (dlen, w, h, sha))
        pal, px, ascii_rows, facts = rle8_ascii(raw)
        art_out.append('facts: %s' % facts)
        for line in ascii_rows:
            art_out.append('  |%s|' % line)
        art_out.append('')
    with open(os.path.join(HERE, 'r124-art-canon.txt'), 'w') as f:
        f.write('\n'.join(art_out) + '\n')

    # ---------- C) OBJD MiscFlags empirical scan --------------------------------
    say('R124 OBJD MiscFlags (field 89) EMPIRICAL SCAN — every object IFF on disk')
    say('Master objects only (SubIndex==0). CTSS name via CatalogStringsID (field 39).')
    say('')
    fars = sorted(f for f in glob.glob(os.path.join(GAMEDATA, '**', '*'), recursive=True)
                  if f.lower().endswith('.far'))   # case-sensitive FS: ExpansionPack5.FAR is uppercase
    fars = [f for f in fars if 'sound' not in f.lower() and 'graphic' not in f.lower()]
    loose = sorted(glob.glob(os.path.join(GAMEDATA, 'GameData', 'UserObjects', '*.iff')))
    say('FARs scanned: %d + %d loose UserObjects iffs' % (len(fars), len(loose)))
    masters = []
    skipped = iff_count = objd_count = 0
    def scan_iff(raw, src):
        nonlocal iff_count, objd_count, skipped
        if not raw.startswith(b'IFF'):
            skipped += 1; return
        iff_count += 1
        try:
            chunks = str_mod().walk_chunks(raw)
        except Exception:
            skipped += 1; return
        ctss = {}
        for c in chunks:
            if c['cc'] == b'CTSS':
                body = raw[c['off'] + 76: c['off'] + c['size']]
                ctss[c['id']] = ctss_entries(body)
        for c in chunks:
            if c['cc'] != b'OBJD':
                continue
            objd_count += 1
            o = parse_objd(raw[c['off'] + 76: c['off'] + c['size']])
            if o is None:
                continue
            f = o['fields']
            if len(f) < 90 or f[8] != 0 or f[9] != 0:   # MasterID/SubIndex != 0 -> slave
                continue
            name = ''
            ent = ctss.get(f[39], [])
            if ent:
                name = ent[0] if ent else ''
            masters.append({'src': src, 'name': name, 'ver': o['ver'],
                            'misc': f[89], 'fields': f,
                            'ratings': f[80:87], 'skills': f[87],
                            'price': f[16], 'ctss': ent})
    for far in fars:
        fd = open(far, 'rb').read()
        base = os.path.basename(far)
        for nm, dlen, doff in walk_far_manifest(fd):
            if not nm.lower().endswith('.iff'):
                continue
            scan_iff(fd[doff:doff + dlen], base + '!' + nm)
    for p in loose:
        scan_iff(open(p, 'rb').read(), 'UserObjects/' + os.path.basename(p))
    say('members: %d IFFs, %d OBJD chunks, %d masters, %d skipped (non-IFF/undecodable)'
        % (iff_count, objd_count, len(masters), skipped))
    say('')
    say('==== MiscFlags (field 89) bit histogram (masters) ====')
    for bit in range(16):
        hits = [x for x in masters if x['misc'] & (1 << bit)]
        if not hits:
            continue
        say('bit %d (0x%04X): %d masters: %s' % (bit, 1 << bit, len(hits),
                                                 '; '.join((x['name'] or x['src'])[:30] for x in hits[:12])))
    say('')
    say('==== CTSS English layout (empirical: catalog CTSS = 2 entries x N languages) ====')
    from collections import Counter
    say('lang-1 entry-count histogram: %s' % dict(Counter(len(x['ctss']) for x in masters)))
    say('==== CTSS[0]/[1] + OBJD price samples (anchor names) ====')
    seen = 0
    for x in masters:
        ln = x['name'].lower()
        if not any(a in ln for a in ANCHORS) or len(x['ctss']) < 2:
            continue
        say('%-28s | price=%-5d | desc=%r'
            % (x['name'][:28], x['price'], x['ctss'][1][:44]))
        seen += 1
        if seen >= 26:
            break
    say('')
    say('==== WIDE histogram: every OBJD field 78..103, sparse-bit name lists ====')
    say('fields: 78 BHAV_Repair, 79 WallStyleSpriteID, 80-86 Ratings, 87 RatingSkillFlags,')
    say('        88 NumTypeAttributes, 89 MiscFlags, 90-91 TypeAttrGUID, 92 FunctionSubsort,')
    say('        93 DTSubsort, 94 KeepBuying, 95 VacationSubsort, 96 ResetLotAction,')
    say('        97 CommunitySubsort, 98 DreamFlags, 99 RenderFlags, 100 VitaboyFlags,')
    say('        101 STSubsort, 102 MTSubsort  (v138 masters)')
    v138 = [x for x in masters if x['ver'] == 138 and x['name']]
    say('v138 named masters: %d' % len(v138))
    for fi in range(78, 104):
        vals = [x['fields'][fi] for x in v138 if len(x['fields']) > fi]
        nz = sum(1 for v in vals if v)
        if nz == 0:
            say('field %2d: all zero' % fi)
            continue
        say('field %2d: %d/%d nonzero' % (fi, nz, len(vals)))
        for bit in range(16):
            hits = [x for x in v138 if len(x['fields']) > fi and x['fields'][fi] & (1 << bit)]
            if not hits:
                continue
            say('  bit %2d (0x%04X): %d masters: %s'
                % (bit, 1 << bit, len(hits), '; '.join(x['name'][:22] for x in hits[:10])))
        if fi == 87:  # skill flags: also show nonzero values with names
            for v in sorted({x['fields'][87] for x in v138 if x['fields'][87]})[:8]:
                ex = [x['name'] for x in v138 if x['fields'][87] == v][:4]
                say('  skillflags value 0x%04x: %s' % (v, '; '.join(ex)))

    say('')
    say('==== R124 DERIVED CONCLUSIONS (data-derived; see r124-descpanel-canon.md) ====')
    say('1. Ratings: OBJD fields 80..86 are SIGNED VALUES in exactly STR# 160 [0..6] caption')
    say('   order (Hunger,Comfort,Hygiene,Bladder,Energy,Fun,Room); coffee = 0xFFF8 = -8 proof.')
    say('2. Skills: field 87 bits 0..6 = STR# 160 [7..13], data-validated (chess=Logic 0x0004,')
    say('   guitar/easel=Creativity 0x0010, mirrors=Charisma 0x0020, computers=Study 0x0040).')
    say('3. NEGATIVE SPACE (STR# 160 [14..19] adults/kids/group/pets/dogs/cats): NO corpus data')
    say('   encodes usage restrictions - MiscFlags(89) all-zero corpus-wide except 2 FX objects;')
    say('   the only pets-clustered bits are f92 bit5 = the PETS SUBSORT (catalog placement,')
    say('   doghouse carries it without being pets-only) and f98 = pet DREAM flags (doghouse 0,')
    say('   chew toys 0); CTSS = exactly 2 English entries (name+description, no price string,')
    say('   no rating tags); CTSS comments = Maxis authoring notes; per-object STR# = behavior/')
    say('   animation strings. The port must NOT invent flag display.')
    say('4. Price: OBJD field 16 only (CTSS carries no price entry).')
    say('5. SCAN FIX: ExpansionPack5.FAR (Unleashed, ALL pet objects) is UPPERCASE - the first')
    say('   glob (*.far, case-sensitive FS) silently missed it; scan now matches case-insensitively.')
    out.close()
    print('masters=%d (iff=%d objd=%d skipped=%d); wrote r124-objdflags-scan.txt, '
          'r124-descpanel-strs.txt, r124-art-canon.txt, art extracts'
          % (len(masters), iff_count, objd_count, skipped))


if __name__ == '__main__':
    main()
