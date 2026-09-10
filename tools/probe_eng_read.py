import struct, os, collections
# Probe: emulate engine IffFile.Read (identifier 64 bytes consumed, then while io.HasBytes(2)
# AddChunk; chunk = 76-byte header + chunkDataSize payload). Count registered-chunk instances per
# member, LAST-wins per basename. Compare total against engine MtChunkInv.
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
REG = ['STR#','CTSS','PALT','OBJD','DGRP','SPR#','SPR2','BHAV','TPRP','SLOT','GLOB','glob','BCON','TTAB','OBJf','TTAs','FWAV','BMP_','PIFF','TRCN','objt','Arry','ObjM','WALm','FLRm','CARR','NBRS','FAMI','NGBH','FAMs','THMB','SIMI','TATT','HOUS','TREE','FCNS','FSOR','FSOM','MTEX','FSOV','PNG_','XXXX','POSI','TMPL','pers','FAMh','CATS','Optn','rsmp']
def member_hist(dat):
    h = collections.Counter()
    pos = 64
    while pos + 2 <= len(dat):
        if pos + 76 > len(dat):
            break
        ct = dat[pos:pos+4].decode('latin1','replace')
        sz = struct.unpack_from('>I', dat, pos+4)[0]
        paysz = sz - 76
        if paysz < 0: break
        if ct in REG:
            h[ct] += 1
        pos += 76 + paysz
    return h
best = {}
for fp in fars:
    data = open(fp,'rb').read()
    for nm, doff, dlen in far_members(fp):
        if not nm.lower().endswith('.iff'): continue
        k = nm.replace('\\','/').rsplit('/',1)[-1].lower()
        h = member_hist(data[doff:doff+dlen])
        if h: best[k] = h
tot = collections.Counter()
for h in best.values(): tot.update(h)
print(dict(sorted(tot.items())))
