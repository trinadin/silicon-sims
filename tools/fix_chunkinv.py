
R = 'tools/iff-dump/r65-chunk-inventory.cs'
C = 'Client/Simitone/Simitone.Client/AutotestCatalogCanon.cs'
rc = open(R, encoding='utf-8').read().split('\n')
arr = next(l for l in rc if 'ChunkTypeCensus = new string[]' in l)
cc = open(C, encoding='utf-8').read().split('\n')
idx = next(i for i, l in enumerate(cc) if 'ChunkTypeCensus = new string[]' in l)
cc[idx] = '        ' + arr
open(C, 'w', encoding='utf-8').write('\n'.join(cc))
import re
s = open(C, encoding='utf-8').read()
m = re.search(r'ChunkTypeCensus = new string\[\] \{(.*?)\};', s, re.S)
vals = re.findall(r'\d+', m.group(1))
print('entries:', len(vals), 'sum:', sum(int(x) for x in vals))
