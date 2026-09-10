"""Record matching owner-local package assemblies and unchanged original input."""
from pathlib import Path
import hashlib
import json

repo = Path(__file__).resolve().parents[3]
here = Path(__file__).resolve().parent
digest = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
assemblies = ['TheSims.dll', 'Simitone.Client.dll', 'FSO.SimAntics.dll',
              'FSO.Content.dll', 'FSO.Files.dll', 'FSO.SimAntics.JIT.dll',
              'FSO.LotView.dll', 'FSO.Common.dll', 'FSO.UI.dll']
rows = []
for name in assemblies:
    published = digest(repo / 'publish/osx-arm64' / name)
    packaged = digest(repo / 'dist/The Sims-arm64.app/Contents/MacOS' / name)
    assert published == packaged, name
    rows.append({'assembly': name, 'publish': published, 'package': packaged})
(here / 'package-assemblies.json').write_text(json.dumps(rows, indent=2) + '\n')
original = digest(repo / 'game-data/The Sims/The Sims Complete')
assert original == '33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f'
(here / 'original-input.json').write_text(json.dumps({
    'path': 'game-data/The Sims/The Sims Complete', 'sha256': original,
    'matchesR240': True}, indent=2) + '\n')
print(f'{len(rows)} publish/package assemblies match; original executable unchanged.')
