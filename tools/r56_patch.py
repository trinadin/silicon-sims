p = 'Client/Simitone/Simitone.Client/AutotestCatalogCanon.cs'
txt = open(p).read()
# cut trailing class close and re-add with consts before it
close_seq = '        };' + chr(10) + '    }' + chr(10) + '}'
idx = txt.rfind(close_seq)
assert idx > 0
block = (chr(10) + '        // Round 56 IFF-LITERAL OBJD-corpus census (tools/iff_objd_census.py over game-data/The Sims,' + chr(10)
         + '        // every IFF OBJD chunk in non-globals .iff; IFF data - 5125 = 5123 v138 + 2 v136).' + chr(10)
         + '        public const int ObjdCorpusCensus = 5125;')
out = txt[:idx] + block + chr(10) + '        };' + chr(10) + '    }' + chr(10) + '}'
open(p, 'w').write(out)
print('embedded ObjdCorpusCensus=5125')
