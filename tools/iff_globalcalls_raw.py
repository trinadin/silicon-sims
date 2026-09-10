#!/usr/bin/env python3
# iff_globalcalls_raw.py - ENGINE-INDEPENDENT raw census of the ORIGINAL global-call surface.
# TS1 BHAV opcode layout: op < 256 = VM primitive; 256 <= op < 4096 = GLOBAL call (target id in
# Global.iff, e.g. #281 'Wait For Notify'); op >= 4096 = private (4096..8191) / semi-global (8192+).
# This pins WHICH Global.iff subroutines the ORIGINAL brain (PersonGlobals) and the 20 fixture
# objects invoke, and at what aggregate frequency - the IFF-authored global-dispatch surface.
# Decode mirrors dump_iff_bhavs.py (IFF chunk header 76 bytes; BHAV ver 0x8002; per instr u16 op
# + u8 t + u8 f + 8-byte operand).
import struct, collections

FAR_GLOBAL = 'game-data/The Sims/GameData/Global/Global.far'
FAR_OBJECTS = 'game-data/The Sims/GameData/Objects/Objects.far'
FIXTURE20 = ['Easel','ChessTable','Beds','Fridges','Stoves','TVs','ShowerC','TubC','TubM',
             'Toilets','Mirrors','ExerciseMachine','BBQ','Bookcases','Computers','Stereos',
             'Bars','HotTub','Recliners','ChairsLR1Tile']

def far_members(path):
    data = open(path, 'rb').read()
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
            out[nm] = data[doff:doff+dlen]
    return out

def bhav_ids_and_global_calls(body):
    ids = set()
    labels = {}
    hist = collections.Counter()
    pos = 64
    while pos + 76 <= len(body):
        ct = body[pos:pos+4].decode('latin1','replace')
        sz = struct.unpack_from('>I', body, pos+4)[0] - 76
        cid = struct.unpack_from('>H', body, pos+8)[0]
        lbl = body[pos+12:pos+76].split(bytes([0]))[0].decode('latin1','replace').strip()
        pos += 76
        if ct == 'BHAV' and sz > 12 and pos + sz <= len(body):
            b = body[pos:pos+sz]
            ver = struct.unpack_from('<H', b, 0)[0]
            cnt = struct.unpack_from('<H', b, 2)[0]
            ids.add(cid)
            if lbl: labels[cid] = lbl
            for k in range(cnt):
                s = 12 + k*12
                if s + 12 > len(b): break
                op = struct.unpack_from('<H', b, s)[0]
                if 256 <= op < 4096:
                    hist[op] += 1
        pos += sz
    return ids, hist, labels

global_members = far_members(FAR_GLOBAL)
objects_members = far_members(FAR_OBJECTS)

global_iff = global_members.get('global.iff') or global_members.get('Global.iff')
if global_iff is None:
    raise SystemExit('Global.iff not found (case?): ' + repr(list(global_members)[:20]))
g_ids, _, g_labels = bhav_ids_and_global_calls(global_iff)

def nm(op):
    return g_labels.get(op, '?')

person = None
for k in global_members:
    if k.lower() == 'personglobals.iff': person = global_members[k]
if person is None:
    raise SystemExit('PersonGlobals.iff not found in Global.far')
_, person_hist, _ = bhav_ids_and_global_calls(person)

per_obj = {}
missing = []
for name in FIXTURE20:
    body = None
    for k in objects_members:
        if k.lower() == (name + '.iff').lower():
            body = objects_members[k]; break
    if body is None:
        missing.append(name); continue
    _, h, _ = bhav_ids_and_global_calls(body)
    per_obj[name] = h

agg = collections.Counter()
agg.update(person_hist)
for h in per_obj.values():
    agg.update(h)

print('Global.iff BHAV-id count (name map):', len(g_ids))
print('PersonGlobals global-calls:', sum(person_hist.values()), 'distinct:', len(person_hist))
for name, h in sorted(per_obj.items()):
    print('  %s global-calls=%d distinct=%d' % (name, sum(h.values()), len(h)))
print('fixture objects with no member:', missing)
print('AGGREGATE global-call surface: total=%d distinct=%d' % (sum(agg.values()), len(agg)))
print('resolved (target is a Global.iff BHAV id):', sum(1 for op in agg if op in g_ids))
print('unresolved/dangling targets:', [op for op in agg if op not in g_ids])
print('unnamed targets (empty label):', [op for op in agg if op in g_ids and not nm(op)])
print('TOP global-call targets (name, id, count):')
for op, c in agg.most_common(20):
    print('  %-28s #%d x%d' % (nm(op), op, c))
total_checksum = 0
for op, c in sorted(agg.items()):
    total_checksum += op * c
print('checksum sum(id*count) =', total_checksum)
print('CSHARP_DICT_START')
for op, c in sorted(agg.items()):
    print('  { %d, %dL },' % (op, c))
print('CSHARP_DICT_END')
print('histogram (sorted by id):')
for op, c in sorted(agg.items()):
    print('  op=%d count=%d name=%r' % (op, c, nm(op)))
