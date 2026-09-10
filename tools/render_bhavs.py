
import json, os
inv = json.load(open('tools/iff-dump/global-bhav-inventory.json'))
lines = ['# Global behavior corpus (extracted from GameData/Global/Global.far, original Complete Collection DVD data)',
         '# Reverse-engineered via tools/extract_globals.py. BHAV chunk labels are Maxis function names.', '']
tot = 0
for r in inv:
    w = r.get('iff')
    if not w: continue
    tot += w['nbhav']
    lines.append('## %s  (%d chunks, %d BHAVs)' % (r['name'], w['chunks'], w['nbhav']))
    lines.append('')
    for b in w['bhavs']:
        lines.append('- ' + b)
    lines.append('')
lines.append('TOTAL BHAVs: %d' % tot)
open('tools/iff-dump/global-bhav-names.md', 'w').write(chr(10).join(lines))
print('wrote', len(lines), 'lines, total BHAVs', tot)
