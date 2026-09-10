
C = 'Client/Simitone/Simitone.Client/AutotestCatalogCanon.cs'
R = 'tools/iff-dump/r61-bhav-ops-census.cs'
rc = open(R, encoding='utf-8').read().split('\n')
# take lines from the array declaration through '};'
start = next(i for i, l in enumerate(rc) if 'BhavOpcodePairCensus = new int[]' in l)
end = next(i for i, l in enumerate(rc) if l.strip() == '};')
arr_lines = rc[start:end + 1]
cc = open(C, encoding='utf-8').read().split('\n')
idx = next(i for i, l in enumerate(cc) if 'BhavOpcodePairCensus' in l and 'new int[]' in l)
print('replacing canon idx', idx + 1, 'OLD len', len(cc[idx]), 'with', len(arr_lines), 'lines')
cc[idx:idx + 1] = arr_lines
open(C, 'w', encoding='utf-8').write('\n'.join(cc))
check = open(C, encoding='utf-8').read()
print('dots in file:', '...' in check)
print('pairs decls:', check.count('BhavOpcodePairCensus = new int[]'))
import re
m = re.search(r'BhavOpcodePairCensus = new int\[\] \{(.*?)\};', check, re.S)
vals = re.findall(r'\d+', m.group(1))
print('pairs ints:', len(vals), '(expect 1426 = 1 distinct line? no: 713*2=1426)')
