import struct, os, collections
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
REG = set(['STR#','CTSS','PALT','OBJD','DGRP','SPR#','SPR2','BHAV','TPRP','SLOT','GLOB','glob','BCON','TTAB','OBJf','TTAs','FWAV','BMP_','PIFF','TRCN','objt','Arry','ObjM','WALm','FLRm','CARR','NBRS','FAMI','NGBH','FAMs','THMB','SIMI','TATT','HOUS','TREE','FCNS','FSOR','FSOM','MTEX','FSOV','PNG_','XXXX','POSI','TMPL','pers','FAMh','CATS','Optn','rsmp'])
def member_counts(dat):
    h = collections.Counter()
    pos = 64
    while pos + 76 <= len(dat):
        ct = dat[pos:pos+4].decode('latin1','replace')
        sz = struct.unpack_from('>I', dat, pos+4)[0]
        paysz = sz - 76
        if paysz < 0: break
        if ct in REG: h[ct] += 1
        pos += 76 + paysz
    return h
best = {}
for fp in fars:
    data = open(fp,'rb').read()
    for nm, doff, dlen in far_members(fp):
        if not nm.lower().endswith('.iff'): continue
        k = nm.replace('\\','/').rsplit('/',1)[-1].lower()
        h = member_counts(data[doff:doff+dlen])
        if h: best[k] = h
eng = {}
dump = open('/var/folders/sg/3fjlktk972j1zkbssc8dfcrw0000gn/T/r65-inv.txt').read().split('\n')
for line in dump:
    if ' | ' not in line: continue
    nm, rest = line.split(' | ', 1)
    rest = rest.replace('GLOB: ', '').replace('GLOBEXC ', '')
    d = {}
    for tok in rest.strip().split(','):
        if not tok or '=' not in tok: continue
        k, v = tok.split('=')
        try: d[k] = int(v)
        except: pass
    eng[nm.lower()] = d
print('engine members', len(eng), 'iff members', len(best))
engTot = collections.Counter()
for d in eng.values(): engTot.update(d)
mis = 0
top = []
for nm in sorted(set(best) | set(eng)):
    e, b = eng.get(nm), best.get(nm)
    if e == b: continue
    mis += 1
    if len(top) < 16:
        diff = {k: ('eng', (e or {}).get(k,0), 'iff', (b or {}).get(k,0)) for k in set(e or {}) | set(b or {}) if (e or {}).get(k,0) != (b or {}).get(k,0)}
        top.append((nm, diff))
for nm, diff in top:
    print('DIFF', nm, diff)
print('mismatched', mis)
print('engine total per type:')
print(dict(sorted(engTot.items())))
print('IFF total per type:')
print(dict(sorted(collections.Counter() for _ in [])) if False else dict(sorted(sum((collections.Counter(v) for v in best.values()), collections.Counter()).items())))
