iff = {}
for l in open('/tmp/r57-iff-ctss.txt'):
    k, v = l.rstrip('\n').split(';')
    iff[k.lower()] = int(v)
eng = {}
for l in open('/var/folders/sg/3fjlktk972j1zkbssc8dfcrw0000gn/T/r57-eng-ctss.txt'):
    k, v = l.rstrip('\n').split(';')
    eng[k.lower()] = int(v)
miss = {k: v for k, v in iff.items() if k not in eng}
extra = {k: v for k, v in eng.items() if k not in iff}
diffc = {k: (iff[k], eng[k]) for k in iff if k in eng and iff[k] != eng[k]}
print('iff:', len(iff), 'eng:', len(eng), 'missing:', len(miss), 'extra:', len(extra), 'count-diff:', len(diffc))
for k, v in list(miss.items())[:10]: print('  MISS', v, k)
for k, (a, b) in list(diffc.items())[:10]: print('  COUNT', k, a, b)
