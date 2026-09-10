src = open('tools/iff-dump/r54-snd-census.cs').read()
block_lines = []
for ln in src.splitlines():
    t = ln.strip()
    if not t: continue
    if t.startswith('//'):
        block_lines.append('        ' + t)
    elif t.startswith('public static'):
        block_lines.append('        ' + t)
    elif t == '};':
        block_lines.append('        };')
    else:
        block_lines.append('            ' + t)
NL = chr(10)
block = NL*2 + NL.join(block_lines)
p = 'Client/Simitone/Simitone.Client/AutotestCatalogCanon.cs'
txt = open(p).read()
close_seq = '        };' + NL + '    }' + NL + '}'
m = txt.rfind(close_seq)
assert m > 0
out = txt[:m] + '        };' + block + NL + '    }' + NL + '}'
open(p, 'w').write(out)
print('embedded OK')
