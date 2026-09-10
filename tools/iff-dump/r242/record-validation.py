"""Verify final owner-local artifacts and retain derived validation metadata."""
from pathlib import Path
import hashlib
import json
import re
import shutil

repo = Path(__file__).resolve().parents[3]
here = Path(__file__).resolve().parent
captures = repo / 'build/ui-audit/r242'
rows = []
for name, expected, count in [('focused-final', 16, 27), ('default-final', 138, 69),
                               ('800x600', 23, 80), ('1280x800', 23, 80),
                               ('800x600@2x', 23, 80)]:
    size_run = name[0].isdigit()
    log_path = here / (('size-' if size_run else '') + name + '.log')
    if size_run:
        shutil.copy2(captures / log_path.name, log_path)
    log = log_path.read_text()
    assert f'AUTOTEST SUMMARY passed={expected} failed=0 skipped=0' in log, name
    assert 'AUTOTEST RESULT PASS' in log and 'after Run' in log and 'after Dispose' in log, name
    if name == 'default-final':
        assert 'AUTOTEST_WRAPPER_EXIT=0' in log
    manifest = (here / (name + '-images.sha256')).read_text().splitlines()
    assert len(manifest) == count, (name, len(manifest))
    for line in manifest:
        expected_hash, filename = line.split('  ', 1)
        assert hashlib.sha256((captures / name / filename).read_bytes()).hexdigest() == expected_hash, filename
    row = {'run': name, 'passed': expected, 'failed': 0, 'captures': count,
           'log': log_path.name, 'manifest_verified': True}
    if size_run:
        match = re.search(r'logical=(\d+x\d+) dpi=([\d.]+)', log)
        assert match, name
        row.update(logical=match[1], effective_dpi=float(match[2]))
        viewport = re.search(r'ui-fit: viewport=(\d+x\d+)', log)
        if viewport:
            row['physical_viewport'] = viewport[1]
    rows.append(row)
before = (captures / 'config-before-size-audit.ini').read_bytes()
after = (Path.home() / 'Documents/Simitone/config.ini').read_bytes()
assert before == after
(here / 'config-restoration.json').write_text(json.dumps({
    'restored_byte_for_byte': True, 'sha256': hashlib.sha256(after).hexdigest()
}, indent=2) + '\n')
(here / 'validation.json').write_text(json.dumps(rows, indent=2) + '\n')
print(json.dumps(rows, indent=2))
