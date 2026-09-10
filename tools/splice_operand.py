
R = 'tools/iff-dump/r64-bhav-operands.cs'
C = 'Client/Simitone/Simitone.Client/AutotestCatalogCanon.cs'
rc = open(R, encoding='utf-8').read().split('\n')
arr = next(l for l in rc if 'BhavOperandByteCensus = new int[]' in l)
cc = open(C, encoding='utf-8').read().split('\n')
# replace existing BhavOperandByteCensus line in place
idx = next(i for i, l in enumerate(cc) if 'BhavOperandByteCensus = new int[]' in l)
cc[idx] = '        ' + arr
open(C, 'w', encoding='utf-8').write('\n'.join(cc))
import re
s = open(C, encoding='utf-8').read()
m = re.search(r'BhavOperandByteCensus = new int\[\] \{(.*?)\};', s, re.S)
vals = re.findall(r'\d+', m.group(1))
print('pairs ints:', len(vals), 'sum', sum(map(int, vals)), 'idx', idx + 1)
