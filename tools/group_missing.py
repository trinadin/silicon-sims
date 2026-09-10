from collections import Counter
miss = [l.rstrip('\n') for l in open('tools/iff-dump/r54-missing-xa.txt') if l.strip()]
print('total missing:', len(miss))
pre = Counter(m.split('\\')[0] for m in miss)
for k, v in pre.most_common():
    print('%5d  %s' % (v, k))
smp = Counter(m.split('\\')[1] if '\\' in m else m for m in miss)
print('--- second component ---')
for k, v in smp.most_common(12):
    print('%5d  %s' % (v, k))
