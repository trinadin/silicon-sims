src = open('tools/iff-dump/r54-snd-census.cs').read()
vals = []
for ln in src.splitlines():
    t = ln.strip()
    if t.startswith('{ "'): vals.append(t)
assert len(vals) == 4, vals
NL = chr(10)
new_block = ('        public static readonly Dictionary<string, int> SoundMountCensus = new Dictionary<string, int>{' + NL
             + NL.join('            ' + v for v in vals) + NL + '        };')
p = 'Client/Simitone/Simitone.Client/AutotestCatalogCanon.cs'
txt = open(p).read()
decl = 'public static readonly Dictionary<string, int> SoundMountCensus = new Dictionary<string, int>{'
start = txt.index(decl)
end = txt.index('        };', start)
old_len = end + len('        };') - start
out = txt[:start] + new_block + txt[start+old_len:]
open(p, 'w').write(out)
print('replaced old', old_len, 'new', len(new_block))
print([v for v in vals])
