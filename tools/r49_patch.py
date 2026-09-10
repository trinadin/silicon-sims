import io
src = open('tools/iff-dump/r49-fixture-canon.cs').read()
block_lines = []
raws = bhavs = 0
for ln in src.splitlines():
    t = ln.strip()
    if not t: continue
    if t.startswith('//'):
        block_lines.append('        ' + t)
    elif t.startswith('public static'):
        block_lines.append('        ' + t)
    elif t == '};':
        block_lines.append('        };')
    elif t.startswith('{ "') and 'L },' in t:
        raws += 1
        block_lines.append('            ' + t)
    elif t.startswith('{ "') and 'new Dictionary<uint' in t:
        bhavs += 1
        block_lines.append('            ' + t)
    else:
        block_lines.append('        ' + t)
print('raw entries:', raws, 'bhav entries:', bhavs)
assert raws == 20 and bhavs == 20, 'count mismatch'
NL = chr(10)
block = NL*2 + NL.join(block_lines)

p = 'Client/Simitone/Simitone.Client/AutotestCatalogCanon.cs'
txt = open(p).read()
anchor = '            { "Recliners.iff", 4487941013400L }, { "ChairsLR1Tile.iff", 2759233843050L }\n        };'
assert txt.count(anchor) == 1, 'anchor %d' % txt.count(anchor)
i = txt.index(anchor) + len(anchor)
out = txt[:i] + block + txt[i:]
open(p, 'w').write(out)
print('patched OK, block lines:', len(block_lines))
