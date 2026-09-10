import json, collections
summary = json.load(open('/Users/nathannoom/Developer/The Sims/simitone-fork/tools/iff-dump/iff-summary.json'))
typetot = collections.Counter()
filecount = 0
for f in summary:
    if 'types' not in f or not f['types']: continue
    filecount += 1
    for t, n in f['types'].items():
        typetot[t] += n
print('files with types:', filecount)
for t, n in typetot.most_common(60):
    print('%6d %s' % (n, t))
