src = open('tools/iff-dump/r55-resource-census.cs').read()
vals = []
for ln in src.splitlines():
    t = ln.strip()
    if t.startswith('{ "') and t.endswith(','): vals.append(t)
print('exts:', len(vals))
NL = chr(10)
new_block = ('        public static readonly Dictionary<string, int> ResourceMountCensus = new Dictionary<string, int>{' + NL
             + NL.join('            ' + v for v in vals) + NL + '        };')
p = 'Client/Simitone/Simitone.Client/AutotestCatalogCanon.cs'
txt = open(p).read()
decl = 'public static readonly Dictionary<string, int> SoundMountCensus = new Dictionary<string, int>{'
start = txt.index(decl)
end = txt.index('        };', start)
old_len = end + len('        };') - start
out = txt[:end+len('        };')] + new_block + txt[end+len('        };'):]
open(p, 'w').write(out)
print('embedded after SoundMountCensus; exts', len(vals))
