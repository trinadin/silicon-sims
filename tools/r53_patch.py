src = open('tools/iff-dump/r53-order-canon.cs').read()
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
n = sum(1 for l in block_lines if '0x' in l and ',' in l)
print('entry lines:', n)
assert n == 1114
NL = chr(10)
block = NL*2 + NL.join(block_lines)

p = 'Client/Simitone/Simitone.Client/AutotestCatalogCanon.cs'
txt = open(p).read()
close_seq = '        };' + NL + '    }' + NL + '}'
m = txt.rfind(close_seq)
assert m > 0
out = txt[:m] + '        };' + block + NL + '    }' + NL + '}'
open(p, 'w').write(out)
print('patched OK')
