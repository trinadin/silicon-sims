#!/usr/bin/env python3
# iff_objd_raw.py - ENGINE-INDEPENDENT raw OBJD catalog decode over the ORIGINAL Objects.far
# (Round 46 'catalog' IFF-literal session). IFF-literally mirrors the engine's OWN parser:
#   - TS1ObjectProvider.Init builds the buy catalog solely from original OBJD chunks
#     (Price = obj.Price, Category = (sbyte)log2(FunctionFlags) or BuildModeType+7, and the
#     passesCatalogCheck predicate). So the IFF-authored OBJD tables IFF-literally pin the
#     BUY-MODE PRICE/CATEGORY surface - no client-invented currency.
#   - OBJD chunk payload: u32 version LE, then u16 LE field array; the fields we read
#     (positions 0..67) are read UNCONDITIONALLY by FSO OBJD.Read, byte-for-byte the same
#     offsets: StackSize0 BaseGraphicID1 NumGraphics2 .. MasterID8 SubIndex9(int16) ..
#     GUID(12-13) Disabled14 .. Price16 .. RoomFlags37 FunctionFlags38 .. BuildModeType67.
# Prints the IFF-literal catalog canon: total OBJDs, catalog-eligible count, price histogram,
# checksum sum(guid*price) over eligible, and pinned per-object prices.
import struct, collections, math

FAR_OBJECTS = 'game-data/The Sims/GameData/Objects/Objects.far'
FAR_GLOBAL = 'game-data/The Sims/GameData/Global/Global.far'
FIXTURE20 = ['Easel','ChessTable','Beds','Fridges','Stoves','TVs','ShowerC','TubC','TubM',
             'Toilets','Mirrors','ExerciseMachine','BBQ','Bookcases','Computers','Stereos',
             'Bars','HotTub','Recliners','ChairsLR1Tile']

def far_members(path):
    data = open(path,'rb').read()
    assert data[0:8] == b'FAR!byAZ', 'magic: '+path
    man = struct.unpack('<I', data[12:16])[0]
    num = struct.unpack('<I', data[man:man+4])[0]
    out = {}
    off = man + 4
    for i in range(num):
        dlen, dlen2, doff = struct.unpack('<III', data[off:off+12])
        nlen = struct.unpack('<H', data[off+12:off+14])[0]
        nm = data[off+16:off+16+nlen].decode('latin1','replace')
        off += 16 + nlen
        if dlen2 == dlen and 0 < doff < len(data) and dlen < len(data):
            out[nm.upper()] = (nm, data[doff:doff+dlen])
    return out

def decode_objd_chunks(body):
    out = []
    pos = 64
    while pos + 76 <= len(body):
        ct = body[pos:pos+4].decode('latin1','replace')
        sz = struct.unpack_from('>I', body, pos+4)[0]
        cid = struct.unpack_from('>H', body, pos+8)[0]
        lbl = body[pos+12:pos+76].split(bytes([0]))[0].decode('latin1','replace').strip()
        paysz = sz - 76
        payload = body[pos+76:pos+76+paysz] if paysz > 0 else b''
        if ct == 'OBJD' and len(payload) >= 4 + 68*2:
            ver = struct.unpack_from('<I', payload, 0)[0]
            fld = struct.unpack_from('<%dH' % ((len(payload)-4)//2), payload, 4)
            out.append((cid, lbl, ver, fld))
        pos += 76 + paysz
    return out

def s16(v):
    return v - 0x10000 if v >= 0x8000 else v

def passes(ff, bmt, dis, mid, si, ng):
    if not (ff > 0 or bmt > 0): return False
    if dis != 0: return False
    if not (mid != 0 or ng > 0): return False
    if not (mid == 0 or si == -1): return False
    return True

def cat(ff, bmt):
    if ff > 0:
        return int(math.log(ff, 2.0))
    return (bmt + 7)

omem = far_members(FAR_OBJECTS)
gmem = far_members(FAR_GLOBAL)
print('Objects.far members=%d Global.far members=%d' % (len(omem), len(gmem)))

all_objds = 0
missing = []
for name in FIXTURE20:
    key = (name + '.IFF').upper()
    if key not in omem:
        missing.append(name); continue
    nm, body = omem[key]
    chunks = decode_objd_chunks(body)
    print('FIXTURE %-24s OBJD-chunks=%d' % (nm, len(chunks)))
    for (cid, lbl, ver, fld) in chunks:
        all_objds += 1
        guid = (fld[13] << 16) | fld[12]
        price = fld[16]; ff = fld[38]; bmt = fld[67]; dis = fld[14]
        mid = fld[8]; si = s16(fld[9]); ng = fld[2]
        pc = passes(ff, bmt, dis, mid, si, ng)
        print('  OBJD cid=%-4d ver=%d label=%r guid=0x%08x price=%d ff=%d bmt=%d dis=%d mid=%d si=%d ng=%d catalog=%s cat=%d' %
              (cid, ver, lbl, guid, price, ff, bmt, dis, mid, si, ng, pc, cat(ff, bmt)))
print('fixture missing:', missing)

full_eligible = []
for key, (nm, body) in omem.items():
    for (cid, lbl, ver, fld) in decode_objd_chunks(body):
        all_objds += 1
        guid = (fld[13] << 16) | fld[12]
        price = fld[16]
        ff = fld[38]; bmt = fld[67]; dis = fld[14]; mid = fld[8]; si = s16(fld[9]); ng = fld[2]
        if passes(ff, bmt, dis, mid, si, ng):
            full_eligible.append((guid, price, nm, cid, lbl))

print('FULL-CORPUS OBJDs(total across members):', all_objds)
print('FULL-CORPUS catalog-eligible:', len(full_eligible))
pc = collections.Counter(p[1] for p in full_eligible)
print('price histogram (top 25):')
for price, c in pc.most_common(25):
    print('  price=%d count=%d' % (price, c))
cksum = sum(guid * price for (guid, price, f, ci, lb) in full_eligible)
print('checksum sum(guid*price) =', cksum)
print('CSHARP_ELIGIBLE_COUNT %d' % len(full_eligible))
print('CSHARP_CHECKSUM %d' % cksum)
print('CSHARP_PRICE_HIST', sorted(pc.items()))
print('CSHARP_PINNED_20:')
for name in FIXTURE20:
    key = (name + '.IFF').upper()
    if key not in omem: continue
    nm, body = omem[key]
    for (cid, lbl, ver, fld) in decode_objd_chunks(body):
        guid = (fld[13] << 16) | fld[12]
        ff = fld[38]; bmt = fld[67]; dis = fld[14]; mid = fld[8]; si = s16(fld[9]); ng = fld[2]
        if passes(ff, bmt, dis, mid, si, ng):
            print('  %-14s label=%r guid=0x%08x price=%d cat=%d' % (nm, lbl, guid, fld[16], cat(ff, bmt)))
