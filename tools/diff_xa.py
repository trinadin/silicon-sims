import sys
iff = set()
for l in open('tools/iff-dump/r54-iff-xa.txt'):
    if l.strip(): iff.add(l.rstrip('\n'))
eng = set()
for l in open('/var/folders/sg/3fjlktk972j1zkbssc8dfcrw0000gn/T/r54-mounted-xa.txt'):
    if l.strip(): eng.add(l.rstrip('\n'))
print('iff', len(iff), 'eng', len(eng))
missing = sorted(iff - eng)
extra = sorted(eng - iff)
print('missing (iff-not-eng):', len(missing))
print('extra (eng-not-iff):', len(extra))
for m in missing[:15]:
    print('  MISS', m)
for m in extra[:5]:
    print('  EXTRA', m)
# case-variant detection
import os, re
miss_low = [m.lower() for m in missing]
add = 0
for m in list(missing):
    if m.lower() in eng:
        print('  CASE-VARIANT (eng has lower):', m)
open('tools/iff-dump/r54-missing-xa.txt', 'w').write('\n'.join(missing) + '\n')
print('missing written tools/iff-dump/r54-missing-xa.txt')
