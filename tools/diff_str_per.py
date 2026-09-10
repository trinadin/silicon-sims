def rd(p):
    d = {}
    for l in open(p):
        try:
            k, v = l.rstrip('\n').split(';')
            d[k] = int(v)
        except: pass
    return d
def diff(a, b, name):
    A, B = rd(a), rd(b)
    miss = {k: v for k, v in B.items() if k not in A}
    diffc = {k: (A[k], B[k]) for k in A if k in B and A[k] != B[k]}
    print(name, 'A', len(A), 'B', len(B), 'missing-in-A', len(miss), 'count-diff', len(diffc))
    for k, v in list(miss.items())[:8]: print('   MISSING', v, k)
    for k, (x, y) in list(diffc.items())[:8]: print('   COUNT', k, x, '->', y)
base = '/var/folders/sg/3fjlktk972j1zkbssc8dfcrw0000gn/T/'
diff(base + 'r58-eng-strsharp.txt', '/tmp/r58-iff-strsharp.txt', 'STR#')
diff(base + 'r58-eng-ttas.txt', '/tmp/r58-iff-ttas.txt', 'TTAs')
