src = open('tools/iff-dump/r53-order-canon.cs').read()
guids = []
for ln in src.splitlines():
    t = ln.strip()
    if t.startswith('0x') and t.endswith(','):
        guids.append('            ' + t)
assert len(guids) == 1114, len(guids)
NL = chr(10)
new_block = ('        public static readonly uint[] CatalogOrderArray = new uint[] {' + NL
             + NL.join(guids) + NL + '        };')

p = 'Client/Simitone/Simitone.Client/AutotestCatalogCanon.cs'
txt = open(p).read()
decl = 'public static readonly uint[] CatalogOrderArray = new uint[] {'
start = txt.index(decl)
end = txt.index('        };', start)
old_len = end + len('        };') - start
new_len = len(new_block)
out = txt[:start] + new_block + txt[start + old_len:]
open(p, 'w').write(out)
print('replaced block: old', old_len, 'new', new_len)
