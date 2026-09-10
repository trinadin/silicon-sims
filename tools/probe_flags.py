import struct, os
from collections import Counter
miss = [l.rstrip('\n') for l in open('tools/iff-dump/r54-missing-xa.txt') if l.strip()]
targets = ['game-data/The Sims/SoundData/SoundData.FAR', 'game-data/The Sims/ExpansionShared/Sound/Sound.far']
flags = Counter()
anom = []
for base in targets:
    data = open(base,'rb').read()
    man = struct.unpack('<I', data[12:16])[0]
    num = struct.unpack('<I', data[man:man+4])[0]
    off = man+4
    for i in range(num):
        dlen,dlen2,doff = struct.unpack('<III', data[off:off+12])
        nlen = struct.unpack('<H', data[off+12:off+14])[0]
        nm = data[off+16:off+16+nlen].decode('latin1','replace')
        off += 16+nlen
        if nm not in miss: continue
        if dlen != dlen2:
            flags['dlen!=dlen2'] += 1
            anom.append((nm, dlen, dlen2, doff, os.path.basename(base)))
        elif not (0 < doff and doff < len(data)):
            flags['bad-offset'] += 1
            anom.append((nm, dlen, dlen2, doff, os.path.basename(base)))
print(flags)
for a in anom[:6]: print(a)
# sample regular member for comparison
