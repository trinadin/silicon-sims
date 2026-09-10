#!/usr/bin/env python3
# r45_check_raw.py - CROSS-CHECK that the raw decoder reproduces the established engine-verified
# R43/R44 canon totals (PersonGlobals 6124, 20 fixture objects 7767). Safe-guards the Round 45
# 'globalcalls' IFF-literal census: the decode must NEVER gate on BHAV version - the ORIGINAL data
# carries a few non-0x8002 BHAVs (PersonGlobals 1, ShowerC 1, Stoves 1) whose instructions the
# engine parses. Any drift from 6124/7767 means the decoder diverges from engine IFF-truth.
import struct
FAR_GLOBAL = 'game-data/The Sims/GameData/Global/Global.far'
FAR_OBJECTS = 'game-data/The Sims/GameData/Objects/Objects.far'
FIXTURE20 = ['Easel','ChessTable','Beds','Fridges','Stoves','TVs','ShowerC','TubC','TubM',
             'Toilets','Mirrors','ExerciseMachine','BBQ','Bookcases','Computers','Stereos',
             'Bars','HotTub','Recliners','ChairsLR1Tile']
def far_members(path):
    data = open(path,'rb').read()
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

def instr_count(body):
    tot = 0; w281 = 0
    pos = 64
    while pos + 76 <= len(body):
        ct = body[pos:pos+4].decode('latin1','replace')
        sz = struct.unpack_from('>I', body, pos+4)[0] - 76
        pos += 76
        if ct == 'BHAV' and sz > 12 and pos + sz <= len(body):
            b = body[pos:pos+sz]
            cnt = struct.unpack_from('<H', b, 2)[0]
            for k in range(cnt):
                s = 12 + k*12
                if s + 12 > len(b): break
                op = struct.unpack_from('<H', b, s)[0]
                tot += 1
                if op == 281: w281 += 1
        pos += sz
    return tot, w281

gmem = far_members(FAR_GLOBAL)
pk = [k for k in gmem if k == 'PERSONGLOBALS.IFF'][0]
person, pbody = gmem[pk]
ptot, pw281 = instr_count(pbody)
print('%s %-24s instr=%d (engine R43 canon 6124) w281=%d' % ("GLOBAL", person, ptot, pw281))

omem = far_members(FAR_OBJECTS)
obj_tot = 0
for name in FIXTURE20:
    key = (name + '.IFF').upper()
    if key in omem:
        nm, body = omem[key]
        tot, w281 = instr_count(body)
        obj_tot += tot
        print('%s %-24s instr=%d w281=%d' % ("OBJECTS", nm, tot, w281))
print('OBJECTS aggregate instr=%d (engine R44 canon 7767)' % obj_tot)
