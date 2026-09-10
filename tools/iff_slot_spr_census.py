import struct, os
# iff_slot_spr_census.py - Round 63 IFF SLOT/SPR#/SPR2 census. Exact mirror of engine SLOT.Read,
# SPR.Read, SPR2.Read: SLOT numSlots u32 at payload+12 (zero u32, version u32, magic 4, numSlots;
# engine Chronological.Count); SPR# v1 u16 at 0 (v1==0 -> BIG endian), spriteCount u32 at +4 (no 1001
# in corpus); SPR2 version u32 at 0 (1000 corpus), spriteCount u32 at +4. Per member basename
# LAST-wins; within member chunks distinct by (type,id); each chunk = ONE instance (engine
# IffFile.List counts chunks). IFF data - object slot tables and sprite-frame surfaces.
def far_members(path):
    data = open(path,'rb').read()
    if data[0:8] != b'FAR!byAZ': return []
    man = struct.unpack('<I', data[12:16])[0]
    num = struct.unpack('<I', data[man:man+4])[0]
    out = []; off = man+4
    for i in range(num):
        dlen,dlen2,doff = struct.unpack('<III', data[off:off+12])
        nlen = struct.unpack('<H', data[off+12:off+14])[0]
        nm = data[off+16:off+16+nlen].decode('latin1','replace')
        off += 16+nlen
        if dlen2==dlen and 0<doff<len(data) and dlen<len(data): out.append((nm, doff, dlen))
    return out
ROOT = 'game-data/The Sims'
fars = sorted([os.path.join(dp, fn) for dp, dns, fns in os.walk(ROOT) for fn in fns if fn.lower().endswith('.far')])
def slot_entries(payload):
    if len(payload) < 16: return 0
    return struct.unpack_from('<I', payload, 12)[0]
def spr_frames(payload):
    if len(payload) < 8: return 0
    v1 = struct.unpack_from('<H', payload, 0)[0]
    if v1 == 0:
        return struct.unpack_from('>I', payload, 4)[0]
    return struct.unpack_from('<I', payload, 4)[0]
def spr2_frames(payload):
    if len(payload) < 12: return 0
    ver = struct.unpack_from('<I', payload, 0)[0]
    if ver == 1000:
        return struct.unpack_from('<I', payload, 4)[0]
    if ver == 1001:
        return struct.unpack_from('<I', payload, 8)[0]
    return 0
best = {}
for fp in fars:
    data = open(fp,'rb').read()
    for nm, doff, dlen in far_members(fp):
        if not nm.lower().endswith('.iff'): continue
        k = nm.replace('\\','/').rsplit('/',1)[-1].lower()
        dat = data[doff:doff+dlen]
        pos = 64
        seen = set()
        sc = se = sp = spc = sp2 = sp2c = 0
        while pos + 76 <= len(dat):
            ct = dat[pos:pos+4].decode('latin1','replace')
            sz = struct.unpack_from('>I', dat, pos+4)[0]
            cid = struct.unpack_from('>H', dat, pos+8)[0]
            paysz = sz - 76
            if paysz < 0: break
            key = (ct, cid)
            if key not in seen:
                seen.add(key)
                if ct == 'SLOT':
                    e = slot_entries(dat[pos+76:pos+76+paysz])
                    if paysz >= 16: sc += 1; se += e
                elif ct == 'SPR#':
                    fr = spr_frames(dat[pos+76:pos+76+paysz])
                    if fr > 0: spc += 1; sp += fr
                elif ct == 'SPR2':
                    fr = spr2_frames(dat[pos+76:pos+76+paysz])
                    if fr > 0: sp2c += 1; sp2 += fr
            pos += 76 + paysz
        if sc or spc or sp2c:
            best[k] = (sc, se, spc, sp, sp2c, sp2)  # LAST-wins per basename
SC = sum(v[0] for v in best.values()); SE = sum(v[1] for v in best.values())
SPC = sum(v[2] for v in best.values()); SP = sum(v[3] for v in best.values())
S2C = sum(v[4] for v in best.values()); S2 = sum(v[5] for v in best.values())
print('SLOT chunks', SC, 'entries', SE)
print('SPR# chunks', SPC, 'frames', SP)
print('SPR2 chunks', S2C, 'frames', S2)
open('tools/iff-dump/r63-slot-spr.txt','w').write('SLOT chunks=%d entries=%d SPR chunks=%d frames=%d SPR2 chunks=%d frames=%d\n' % (SC, SE, SPC, SP, S2C, S2))
lines = ['// Round 63 IFF-LITERAL SLOT/SPR census: tools/iff_slot_spr_census.py over game-data/The Sims.',
         '// SLOT chunk instances + slot entries (engine SLOT.Read numSlots u32 at payload+12); SPR#',
         '// chunk instances + frames (spriteCount u32 at +4, BE if v1==0); SPR2 chunks + frames',
         '// (spriteCount u32 at +4 for v1000, +8 for v1001). Per member basename LAST-wins; chunks',
         '// distinct by (type,id) = one instance each. IFF data - object slot tables and sprite-frame',
         '// surfaces.',
         'public const int SlotChunkCensus = %d;' % SC,
         'public const int SlotEntryCensus = %d;' % SE,
         'public const int SprChunkCensus = %d;' % SPC,
         'public const int SprFrameCensus = %d;' % SP,
         'public const int Spr2ChunkCensus = %d;' % S2C,
         'public const int Spr2FrameCensus = %d;' % S2]
open('tools/iff-dump/r63-slot-spr-census.cs','w').write('\n'.join(lines) + '\n')
print('canon written tools/iff-dump/r63-slot-spr-census.cs')
