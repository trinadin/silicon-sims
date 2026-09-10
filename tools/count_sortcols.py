
import re
src = open('tools/iff-dump/r47-sort-canon.cs').read()
cols = {0:'Room',1:'Subsort',2:'Downtown',3:'Vacation',4:'Community',5:'Studiotown',6:'Magictown'}
counts = {}
total = 0
for m in re.finditer(r'\{ 0x[0-9a-fA-F]+, new int\[\] \{ ([^}]+) \} \}', src):
    vals = [int(x) for x in m.group(1).split(',')]
    total += 1
    for i, v in enumerate(vals):
        if v != 0: counts[i] = counts.get(i, 0) + 1
print('canon entries:', total)
for i in sorted(cols):
    print('  %s nonzero: %d' % (cols[i], counts.get(i, 0)))
