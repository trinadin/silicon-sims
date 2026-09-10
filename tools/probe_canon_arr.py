import io
for p in ['Client/Simitone/Simitone.Client/AutotestCatalogCanon.cs', 'tools/iff-dump/r61-bhav-ops-census.cs']:
    lines = open(p, encoding='utf-8').read().split('\n')
    for i, l in enumerate(lines, 1):
        if 'BhavOpcodePairCensus' in l:
            print(p, 'line', i, 'len', len(l))
            if '...' in l:
                j = l.index('...')
                print('   DOTS at char', j, repr(l[j-30:j+40]))
            else:
                print('   no literal ...')
