def rd(p):
    d = {}
    for l in open(p):
        try:
            k, v = l.rstrip('\n').split(';')
            d[k.lower()] = int(v)
        except Exception:
            pass
    return d
base = '/var/folders/sg/3fjlktk972j1zkbssc8dfcrw0000gn/T/'
s_eng = rd(base + 'r58-eng-strsharp.txt'); t_eng = rd(base + 'r58-eng-ttas.txt')
s_iff = rd('/tmp/r58-iff-strsharp.txt'); t_iff = rd('/tmp/r58-iff-ttas.txt')
print('STR# eng sum', sum(s_eng.values()), 'iff sum', sum(s_iff.values()))
print('TTAs eng sum', sum(t_eng.values()), 'iff sum', sum(t_iff.values()))
for name, E, F in (('STR#', s_eng, s_iff), ('TTAs', t_eng, t_iff)):
    miss = {k: v for k, v in F.items() if k not in E}
    if miss:
        print(name, 'members in IFF not engine:', len(miss))
        for k, v in sorted(miss.items())[:14]: print('   ', v, k)
