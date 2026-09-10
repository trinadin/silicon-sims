"""Serial CAS validation with exact settings restoration."""
from pathlib import Path
import hashlib
import os
import re
import shutil
import subprocess
import sys

repo = Path(__file__).resolve().parents[3]
evidence = Path(__file__).resolve().parent
mode = sys.argv[1]
assert mode in ('flow800', 'default')
config = Path.home() / 'Documents/Simitone/config.ini'
original = config.read_bytes()
try:
    if mode == 'flow800':
        settings = original.decode('utf-8-sig')
        for key, value in [('GraphicsWidth', 800), ('GraphicsHeight', 600), ('DPIScaleFactor', 1)]:
            settings = re.sub(r'^' + key + r'=.*$', f'{key}={value}', settings, flags=re.M)
        config.write_text(settings)
    command = [str(repo / 'dist/The Sims-arm64.app/Contents/MacOS/TheSims'),
               '-ApplePersistenceIgnoreState', 'YES', '-path' + str(repo / 'game-data/The Sims'),
               '-autotest', '5', '-autotest-timeout', '1800000']
    if mode == 'flow800':
        command += ['-autotest-opts', 'ucasflow']
    logpath = evidence / (mode + '-validated.log')
    with logpath.open('w') as log:
        result = subprocess.run(command, cwd=repo, stdout=log, stderr=subprocess.STDOUT,
                                env=dict(os.environ, NSUnbufferedIO='YES'), timeout=2400)
    text = logpath.read_text()
    for line in text.splitlines():
        if 'AUTOTEST SUMMARY' in line or 'AUTOTEST ucasflow passed=' in line:
            print(line, flush=True)
    assert result.returncode == 0 and 'AUTOTEST RESULT PASS' in text, logpath
    assert 'after Run' in text and 'after Dispose' in text, logpath
    if mode == 'flow800':
        for path in re.findall(r'capture 03-characteredit-preview .*? -> (.+)', text):
            shutil.copy2(path, evidence / 'casflow-800x600.png')
finally:
    config.write_bytes(original)
    assert config.read_bytes() == original
    (evidence / (mode + '-settings.sha256')).write_text(hashlib.sha256(original).hexdigest() + '\nRestored byte-for-byte.\n')
