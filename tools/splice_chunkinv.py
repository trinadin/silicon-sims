
R = 'tools/iff-dump/r65-chunk-inventory.cs'
C = 'Client/Simitone/Simitone.Client/AutotestCatalogCanon.cs'
rc = open(R, encoding='utf-8').read().split('\n')
arr = next(l for l in rc if 'ChunkTypeCensus = new string[]' in l)
cc = open(C, encoding='utf-8').read().split('\n')
idx = next(i for i, l in enumerate(cc) if 'BhavOperandByteCensus = new int[]' in l)
lines = ['', '        // Round 65 IFF-LITERAL REGISTERED-CHUNK INVENTORY census (tools/iff_chunks_census.py',
         '        // over game-data/The Sims): per mounted member (basename LAST-wins), every chunk whose',
         '        // raw 4-char type is in CHUNK_TYPES is instantiated by IffFile.AddChunk and enumerated',
         '        // by ListAll(); unregistered types are skipped. Raw spellings kept. IFF data.',
         '        ' + arr]
cc[idx:idx] = lines
open(C, 'w', encoding='utf-8').write('\n'.join(cc))
print('spliced at', idx + 1)
