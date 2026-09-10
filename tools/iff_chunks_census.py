import struct, os, collections
# iff_chunks_census.py - Round 65 IFF REGISTERED-CHUNK INVENTORY census. Exact mirror of engine
# IffFile.AddChunk/ListAll: per mounted member (basename LAST-wins), every chunk whose raw 4-char
# type is in CHUNK_TYPES (the full engine registry: typed classes + IffUnknownChunk for XXXX/POSI/
# TMPL/pers/FAMh/CATS/Optn/rsmp) is instantiated and enumerated by ListAll(). Raw spellings kept
# ('glob' vs 'GLOB' both registered to GLOB). Unregistered types are skipped by the engine. IFF
# data - the document-level chunk inventory the engine parses.
REG = ['STR#', 'CTSS', 'PALT', 'OBJD', 'DGRP', 'SPR#', 'SPR2', 'BHAV', 'TPRP', 'SLOT', 'GLOB', 'glob',
       'BCON', 'TTAB', 'OBJf', 'TTAs', 'FWAV', 'BMP_', 'PIFF', 'TRCN', 'objt', 'Arry', 'ObjM', 'WALm',
       'FLRm', 'CARR', 'NBRS', 'FAMI', 'NGBH', 'FAMs', 'THMB', 'SIMI', 'TATT', 'HOUS', 'TREE', 'FCNS',
       'FSOR', 'FSOM', 'MTEX', 'FSOV', 'PNG_', 'XXXX', 'POSI', 'TMPL', 'pers', 'FAMh', 'CATS', 'Optn', 'rsmp']
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
def member_hist(dat):
    h = collections.Counter()
    pos = 64
    while pos + 76 <= len(dat):
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
        if h:
            best[k] = h
raw_tot = collections.Counter()
for h in best.values():
    raw_tot.update(h)
print('raw:', dict(sorted(raw_tot.items())))
open('tools/iff-dump/r65-chunks.txt','w').write(str(sorted(raw_tot.items())) + '\n')
pairs = ['"%s=%d"' % (k, v) for k, v in sorted(raw_tot.items())]
lines = ['// Round 65 IFF-LITERAL REGISTERED-CHUNK INVENTORY census: tools/iff_chunks_census.py over',
         '// game-data/The Sims. Per mounted member (basename LAST-wins), every chunk whose raw 4-char',
         '// type is in the full engine CHUNK_TYPES registry (typed + IffUnknownChunk) is instantiated',
         '// and enumerated by ListAll(); unregistered types are skipped. Raw spellings kept. IFF data.',
         'public static readonly string[] ChunkTypeCensus = new string[] { ' + ', '.join(pairs) + ' };']
open('tools/iff-dump/r65-chunk-inventory.cs','w').write('\n'.join(lines) + '\n')
print('canon written tools/iff-dump/r65-chunk-inventory.cs, entries', len(pairs))
