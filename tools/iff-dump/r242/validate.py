"""Serial package validation; preserve owner settings exactly, including failure."""
from pathlib import Path
import argparse
import hashlib
import json
import os
import re
import shutil
import subprocess

args = argparse.ArgumentParser()
args.add_argument('phase', choices=['focused', 'default'])
args.add_argument('--name')
args = args.parse_args()
repo = Path(__file__).resolve().parents[3]
evidence = Path(__file__).resolve().parent
output = repo / 'build/ui-audit/r242' / (args.name or args.phase)
output.mkdir(parents=True, exist_ok=True)
config = Path.home() / 'Documents/Simitone/config.ini'
original = config.read_bytes()
binary = repo / 'dist/The Sims-arm64.app/Contents/MacOS/TheSims'
command = ['./tools/run-autotest.sh'] if args.phase == 'default' else [
    str(binary), '-ApplePersistenceIgnoreState', 'YES',
    '-path' + str(repo / 'game-data/The Sims'), '-autotest', '5',
    '-autotest-opts', 'lot,corpus,uitutorial,uidialog,uipip,uiclip,uicasorig',
    '-autotest-timeout', '180000']
log = evidence / ((args.name or args.phase) + '.log')
try:
    with log.open('w') as stream:
        result = subprocess.run(command, cwd=repo, stdout=stream, stderr=subprocess.STDOUT,
                                env=dict(os.environ, NSUnbufferedIO='YES'),
                                timeout=2400 if args.phase == 'default' else 240)
    content = log.read_text()
    print(next((line for line in content.splitlines() if 'AUTOTEST SUMMARY' in line),
               content[-3000:]), flush=True)
    assert result.returncode == 0 and 'AUTOTEST RESULT PASS' in content, log
    assert 'after Run' in content and 'after Dispose' in content, log
    paths = list(dict.fromkeys(re.findall(r'AUTOTEST uisurvey dump .*? -> (.+)', content)))
    paths += list((Path.home() / 'Documents/Simitone/ui-audit/r241').glob('tutorial-*.png'))
    paths += list((Path.home() / 'Documents/Simitone/ui-audit/r240').glob('pip-*.png'))
    paths += list((Path.home() / 'Documents/Simitone/ui-audit/r242').glob('cas-*.png'))
    for path in paths:
        shutil.copy2(path, output / Path(path).name)
    (evidence / ((args.name or args.phase) + '-images.sha256')).write_text(''.join(
        hashlib.sha256(p.read_bytes()).hexdigest() + '  ' + p.name + '\n'
        for p in sorted(output.glob('*.png'))))
finally:
    config.write_bytes(original)
    restored = config.read_bytes()
    (evidence / ((args.name or args.phase) + '-config.json')).write_text(json.dumps({
        'before': hashlib.sha256(original).hexdigest(),
        'after': hashlib.sha256(restored).hexdigest(),
        'equal': original == restored}, indent=2) + '\n')
