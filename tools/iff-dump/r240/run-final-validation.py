#!/usr/bin/env python3
"""Verify the frozen owner-local package serially; never share live config."""
from pathlib import Path
import os
import subprocess
import re
import shutil
import hashlib

repo = Path(__file__).resolve().parents[3]
output = repo / 'build/ui-audit/r240'
output.mkdir(parents=True, exist_ok=True)
binary = repo / 'dist/The Sims-arm64.app/Contents/MacOS/TheSims'
environment = dict(os.environ, NSUnbufferedIO='YES')

def run(name, args, timeout):
    path = output / (name + '.log')
    with path.open('w') as stream:
        result = subprocess.run(args, cwd=repo, env=environment, stdout=stream,
                                stderr=subprocess.STDOUT, timeout=timeout)
    content = path.read_text()
    assert result.returncode == 0, path
    assert 'AUTOTEST RESULT PASS' in content, path
    assert 'after Run' in content and 'after Dispose' in content, path
    if name == 'default-final':
        assert 'AUTOTEST_WRAPPER_EXIT=0' in content, path
    print(name + ': ' + next(line for line in content.splitlines()
          if 'AUTOTEST SUMMARY' in line), flush=True)

run('focused-final', [str(binary), '-ApplePersistenceIgnoreState', 'YES',
    '-path' + str(repo / 'game-data/The Sims'), '-autotest', '5',
    '-autotest-opts', 'lot,corpus,uicasorig,uitutorial,uicapture,uipip,uiclip',
    '-autotest-timeout', '180000'], 240)
run('default-final', ['./tools/run-autotest.sh'], 2400)
# Preserve the current default images before the size runs overwrite them.
paths = list(dict.fromkeys(re.findall(r'AUTOTEST uisurvey dump .*? -> (.+)',
    (output / 'default-final.log').read_text())))
captures = output / 'default'
captures.mkdir(exist_ok=True)
for path in paths:
    shutil.copy2(path, captures / Path(path).name)
for path in (Path.home() / 'Documents/Simitone/ui-audit/r240').glob('pip-*.png'):
    shutil.copy2(path, captures / path.name)
manifest = ''.join(hashlib.sha256(p.read_bytes()).hexdigest() + '  ' + p.name + '\n'
    for p in sorted(captures.glob('*.png')))
(repo / 'tools/iff-dump/r240/default-images.sha256').write_text(manifest)
subprocess.run(['python3', 'tools/iff-dump/r240/run-ui-sizes.py'], cwd=repo,
               env=environment, check=True, timeout=900)
