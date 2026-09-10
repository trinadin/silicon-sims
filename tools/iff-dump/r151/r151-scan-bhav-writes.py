#!/usr/bin/env python3
"""R151: scan ALL character-file BHAVs for person-data (scope 18/19) accesses.
Reports every expression op whose Lhs (write target) uses MyPersonData(18)/
StackObjectPersonData(19), plus any MyPersonData READ operand with index >= 46.
Usage: r151-scan-bhav-writes.py <charDir|iff> ..."""
import struct, sys, os, collections

def chunks(d):
    off = 64
    while off + 76 <= len(d):
        tag = d[off:off+4]
        size, cid, flags = struct.unpack_from('>IHH', d, off+4)
        if size < 76 or off+size > len(d): return
        label = d[off+12:off+76].split(b'\0')[0].decode(errors='replace')
        yield tag.decode(errors='replace'), cid, label, off+76, size-76
        off += size

SCOPES = {18:"MyPD", 19:"StackPD"}
def scan(path, out):
    d = open(path,'rb').read()
    if d[:8] != b'IFF FILE': return
    for tag, cid, label, doff, dsz in chunks(d):
        if tag != 'BHAV': continue
        h = d[doff:doff+13]
        sig = struct.unpack_from('<H', h, 0)[0]
        if sig == 0x8003:
            count = h[9] | (h[10]<<8) | (h[11]<<16) | (h[12]<<24); ioff = doff+13
        else:
            count = struct.unpack_from('<H', h, 2)[0]; ioff = doff+12
        for i in range(count):
            o = d[ioff+i*12: ioff+(i+1)*12]
            if len(o) < 12: break
            op = struct.unpack_from('<H', o, 0)[0]
            body = o[4:12]
            if op == 2:
                L, R = struct.unpack_from('<hh', body, 0)
                lsc, rsc = body[6], body[7]
                if lsc in (18,19):
                    out[cid,label,path].append((i, f"WRITE [{SCOPES[lsc]}]{L}  (R=[{rsc}]{R})"))
                elif lsc not in (18,19) and rsc == 18 and 46 <= R <= 60:
                    out[cid,label,path].append((i, f"read-side R=[MyPD]{R}"))

out = collections.defaultdict(list)
args = sys.argv[1:]
targets = []
for a in args:
    if os.path.isdir(a):
        for f in sorted(os.listdir(a)):
            if f.endswith('.iff'): targets.append(os.path.join(a,f))
    else: targets.append(a)
for p in targets: scan(p, out)
print(f"scanned {len(targets)} files")
for (cid,label,path), hits in sorted(out.items()):
    for i, s in hits:
        print(f"{os.path.basename(path)} BHAV {cid} '{label}' [{i}] {s}")
