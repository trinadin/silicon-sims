#!/usr/bin/env python3
"""R122 buy-catalog ART canon: byte-verbatim pin table for every UIGraphics.far
member the R122 port mounts, extracted from the archive with the proven r85
FAR1 walk (manifest offset = u32@12, count = u32@manifest, entries of four
u32 {dlen,d2,doff,nlen} + name; guard dlen==d2 and doff+dlen<=filelen; BMP dims
= i32@18/@22 when len(raw)>=26). Members are keyed by FULL lowercase path (the
catalog subsort sheets reuse basenames across 8 family directories, so a
basename key would be ambiguous).

Writes r122-art-canon.txt next to this script and FAILS LOUDLY (exit 1) if any
member is missing from the archive.
"""
import struct, hashlib, os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
FAR = os.path.join(ROOT, 'game-data/The Sims/UIGraphics/UIGraphics.far')

# (manifest-exact name, note) — every member the R122 port mounts.
CATALOG = 'cpanel\\Catalog\\'
SS = 'cpanel\\Catalog\\SubSortIcons\\'
MEMBERS = [
    # main sort plaques: 8 functions (STR# 150 [8..15] / kBuyF* order) then 8 rooms ([0..7] / kBuyR*)
    (CATALOG + 'BuyFSeating.BMP',     'function plaque 0 Seating'),
    (CATALOG + 'BuyFSurfaces.bmp',    'function plaque 1 Surfaces'),
    (CATALOG + 'BuyFDecorative.bmp',  'function plaque 2 Decorative'),
    (CATALOG + 'BuyFElectronics.bmp', 'function plaque 3 Electronics'),
    (CATALOG + 'BuyFAppliances.bmp',  'function plaque 4 Appliances'),
    (CATALOG + 'BuyFPlumbing.bmp',    'function plaque 5 Plumbing'),
    (CATALOG + 'BuyFLighting.bmp',    'function plaque 6 Lighting'),
    (CATALOG + 'BuyFMisc.bmp',        'function plaque 7 Miscellaneous'),
    (CATALOG + 'BuyRLiving.BMP',      'room plaque 0 Living Room'),
    (CATALOG + 'BuyRDining.bmp',      'room plaque 1 Dining Room'),
    (CATALOG + 'BuyRBedroom.bmp',     'room plaque 2 Bedroom'),
    (CATALOG + 'BuyRStudy.bmp',       'room plaque 3 Study'),
    (CATALOG + 'BuyRKitchen.bmp',     'room plaque 4 Kitchen'),
    (CATALOG + 'BuyRBathroom.bmp',    'room plaque 5 Bathroom'),
    (CATALOG + 'BuyROutside.bmp',     'room plaque 6 Outside'),
    (CATALOG + 'BuyRMisc.bmp',        'room plaque 7 Miscellaneous'),
    # room-mode subsort sheets (the 8 FUNCTION sorts shown inside a room)
    (SS + 'RoomSubsort\\BuySubSortRoomSeating.BMP',     'room subsort Seating (4-frame 36x36)'),
    (SS + 'RoomSubsort\\BuySubSortRoomSurfaces.BMP',    'room subsort Surfaces'),
    (SS + 'RoomSubsort\\BuySubSortRoomDecorative.BMP',  'room subsort Decorative'),
    (SS + 'RoomSubsort\\BuySubSortRoomElectronics.bmp', 'room subsort Electronics'),
    (SS + 'RoomSubsort\\BuySubSortRoomAppliances.BMP',  'room subsort Appliances'),
    (SS + 'RoomSubsort\\BuySubSortRoomPlumbing.BMP',    'room subsort Plumbing'),
    (SS + 'RoomSubsort\\BuySubSortRoomLighting.BMP',    'room subsort Lighting'),
    (SS + 'RoomSubsort\\BuySubSortRoomMisc.bmp',        'room subsort Miscellaneous'),
]
# function-mode subsort sheets: 8 families x slots One..Four (STR# 200-207 e0-e3)
for fam in ('Seating', 'Surfaces', 'Decorative', 'Electronics',
            'Appliances', 'Plumbing', 'Lighting', 'Miscellaneous'):
    for slot in ('One.BMP', 'Two.bmp', 'Three.BMP', 'Four.BMP'):
        MEMBERS.append((SS + fam + '\\BuySubSort' + slot,
                        fam + ' subsort slot ' + slot + ' (4-frame 36x36)'))
MEMBERS += [
    (SS + 'Miscellaneous\\BuySubSortPets.BMP',  'Pets subsort (STR# 210 [3])'),
    (SS + 'Miscellaneous\\BuySubSortMagic.BMP', 'Magic subsort (STR# 210 [4])'),
    (SS + 'BuySubSortOther.bmp',                'Other subsort (STR# 210 [1], single)'),
    (SS + 'BuySubSortAll.bmp',                  'All subsort (STR# 210 [2], single)'),
    # expansion-lot main-sort plaques for the subsort row (res_cpanel.RT mapping;
    # Studiotown/Magictown alias the Downtown art per the RT)
    (CATALOG + 'BuyDDining.BMP',   'Downtown Dining / ST+MT food alias'),
    (CATALOG + 'BuyDShops.bmp',    'Downtown Shops / ST+MT shops alias'),
    (CATALOG + 'BuyDOutdoor.bmp',  'Downtown Outdoors'),
    (CATALOG + 'BuyDStreet.bmp',   'Downtown Street'),
    (CATALOG + 'BuyCOne.BMP',      'Community One (Food)'),
    (CATALOG + 'BuyCTwo.bmp',      'Community Two (Shops)'),
    (CATALOG + 'BuyCThree.bmp',    'Community Three (Outdoors)'),
    (CATALOG + 'BuyCFour.bmp',     'Community Four (Street)'),
    (CATALOG + 'BuyVOne.BMP',      'Vacation One (Lodging)'),
    (CATALOG + 'BuyVTwo.bmp',      'Vacation Two (Shops)'),
    (CATALOG + 'BuyVThree.bmp',    'Vacation Three (Recreation)'),
    (CATALOG + 'BuyVFour.bmp',     'Vacation Four (Amenities)'),
    (CATALOG + 'BuySTStudio.bmp',  'Studiotown Studio (RT)'),
    (CATALOG + 'BuySTSpa.bmp',     'Studiotown Spa (RT)'),
    (CATALOG + 'BuyMTmagic.bmp',   'Magictown MagiCo (RT)'),
    (CATALOG + 'BuyMOutdoor.bmp',  'Magictown Outdoors (RT)'),
    # paging arrows (kCatalogPrevPage / kCatalogNextPage)
    ('cpanel\\Buttons\\ScrollLeft.BMP',  'catalog Previous Page arrow (STR# 154 [0])'),
    ('cpanel\\Buttons\\ScrollRight.BMP', 'catalog Next Page arrow (STR# 154 [1])'),
    # item thumb templates
    (CATALOG + 'ThumbTemplate.BMP',      '4-frame 45x45 thumb sheet (selected = frame 1)'),
    (CATALOG + 'ThumbTemplate1Frame.BMP','single 45x45 thumb frame'),
]


def far_truth(data):
    man = struct.unpack('<I', data[12:16])[0]
    num = struct.unpack('<I', data[man:man + 4])[0]
    off = man + 4
    truth = {}
    for _ in range(num):
        if off + 16 > len(data):
            break
        dlen, d2, doff, nlen = struct.unpack('<IIII', data[off:off + 16])
        nm = data[off + 16:off + 16 + nlen].decode('latin1', 'replace')
        off += 16 + nlen
        if dlen != d2 or doff + dlen > len(data):
            continue
        raw = data[doff:doff + dlen]
        w = h = -1
        if len(raw) >= 26:
            w, h = struct.unpack('<ii', raw[18:26])
        truth[nm.lower()] = (nm, dlen, w, h, hashlib.sha256(raw).hexdigest())
    return truth


def main():
    data = open(FAR, 'rb').read()
    truth = far_truth(data)
    lines = ['R122 buy-catalog ART canon — every UIGraphics.far member the port mounts',
             'FAR: %s (%d bytes, full-file sha256 %s)' % (FAR, len(data), hashlib.sha256(data).hexdigest()),
             'key = full lowercase manifest path (basenames repeat across subsort family dirs)',
             '',
             '%-64s %8s %9s %7s  %s' % ('member (lowercase)', 'len', 'WxH', 'sha256', 'note')]
    missing = []
    out_rows = []
    for name, note in MEMBERS:
        t = truth.get(name.lower())
        if t is None:
            missing.append(name)
            continue
        exact, dlen, w, h, sha = t
        lines.append('%-64s %8d %4dx%-4d %s  %s  [manifest: %s]'
                     % (name.lower(), dlen, w, h, sha, note, exact))
        out_rows.append((name.lower(), dlen, w, h, sha))
    txt = '\n'.join(lines) + '\n'
    with open(os.path.join(HERE, 'r122-art-canon.txt'), 'w') as f:
        f.write(txt)
    print(txt)
    print('SUMMARY: %d members pinned, %d missing' % (len(out_rows), len(missing)))
    if missing:
        print('MISSING FROM FAR:', *missing, sep='\n  ')
        sys.exit(1)


if __name__ == '__main__':
    main()
