import json
inv = json.load(open('tools/iff-dump/global-bhav-inventory.json'))
for r in inv:
    w = r.get('iff')
    if not w: continue
    hits = [x for x in w['bhavs'] if 'mood' in x.lower() or 'motive' in x.lower() or 'perform' in x.lower()]
    for h in hits[:40]: print(r['name'], '::', h)
