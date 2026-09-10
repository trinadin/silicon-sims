import struct, os
base = next(os.path.join(dp, fn) for dp, dns, fns in os.walk('game-data/The Sims') for fn in fns if fn.lower() == 'sound.far')
print('sound far:', base)
data = open(base,'rb').read()
man = struct.unpack('<I', data[12:16])[0]
num = struct.unpack('<I', data[man:man+4])[0]
seen = {}
off = man+4
for i in range(num):
    dlen,dlen2,doff = struct.unpack('<III', data[off:off+12])
    nlen = struct.unpack('<H', data[off+12:off+14])[0]
    nm = data[off+16:off+16+nlen].decode('latin1','replace')
    off += 16+nlen
    seen.setdefault(nm, 0)
    seen[nm] += 1
dups = {k:v for k,v in seen.items() if v>1}
print('member rows:', num, 'distinct:', len(seen), 'dup-names:', len(dups))
for k,v in list(dups.items())[:6]: print('  DUP x%d' % v, k)
birdy = sorted(k for k in seen if 'birdy' in k.lower())
print('birdy distinct:', len(birdy), 'sampled:', birdy[:4])
xa = [k for k in seen if k.lower().endswith('.xa')]
print('.xa distinct in Sound.far:', len(xa))
