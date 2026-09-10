import io
src = open('tools/iff-dump/r48-names-canon.cs').read()
entries = 0
head = []
body = []
for ln in src.splitlines():
    t = ln.strip()
    if t.startswith('//'):
        head.append('        ' + t)
    elif t == 'public static readonly Dictionary<uint, string> CatalogNames = new Dictionary<uint, string>{':
        head.append('        ' + t)
    elif t == '};':
        pass
    elif t.startswith('{ 0x'):
        entries += 1
        body.append('            ' + t)
    else:
        head.append('        ' + t)
assert entries == 1114, 'unexpected %d' % entries

p = 'Client/Simitone/Simitone.Client/AutotestCatalogCanon.cs'
txt = open(p).read()
anchor = '        public static readonly Dictionary<string, int> FixtureEligible'
assert txt.count(anchor) == 1, 'anchor count %d' % txt.count(anchor)
i = txt.index(anchor)
NL = chr(10)
block = NL.join(head) + NL + NL.join(body) + NL + '        };' + NL + NL
out = txt[:i] + block + txt[i:]
open(p, 'w').write(out)
print('patched OK; embedded entries:', entries)
