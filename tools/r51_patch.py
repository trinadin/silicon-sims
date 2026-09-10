src = open('tools/iff-dump/r50-corpus-bhavs-canon.cs').read()
capture = False
block_lines = []
for ln in src.splitlines():
    t = ln.strip()
    if 'CatalogRawChecksum' in t and t.startswith('public static'):
        capture = True
        block_lines.append('        ' + t); continue
    if capture:
        if t == '};':
            block_lines.append('        };')
            capture = False
        else:
            block_lines.append('            ' + t)
NL = chr(10)
block = NL + NL.join(block_lines)
print('raw block lines:', len(block_lines))

p = 'Client/Simitone/Simitone.Client/AutotestCatalogCanon.cs'
txt = open(p).read()
close_seq = '        };' + NL + '    }' + NL + '}'
m = txt.rfind(close_seq)
assert m > 0
out = txt[:m] + '        };' + block + NL + '    }' + NL + '}'
open(p, 'w').write(out)
print('patched OK')
