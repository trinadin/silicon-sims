def rd(p):
    d = {}
    for l in open(p):
        try:
            k, v = l.rstrip('\n').split(';')
            d[k.lower().replace('\\','/').split('/')[-1]] = int(v)
        except Exception:
            pass
    return d
def diff(a, b, name):
    A, B = rd(a), rd(b)
    diffc = {k: (A[k], B[k]) for k in A if k in B and A[k] != B[k]}
    miss = {k: v for k, v in B.items() if k not in A}
    print(name, 'A-eng', len(A), 'B-iff', len(B), 'missing-in-eng', len(miss), 'count-diff', len(diffc))
    for k, (x, y) in list(diffc.items())[:6]: print('   COUNT', k, x, '->', y)
base = '/var/folders/sg/3fjlktk972j1zkbssc8dfcrw0000gn/T/'
diff(base + 'r58-eng-strsharp.txt', '/tmp/r58-iff-strsharp.txt', 'STR#')
diff(base + 'r58-eng-ttas.txt', '/tmp/r58-iff-ttas.txt', 'TTAs')
