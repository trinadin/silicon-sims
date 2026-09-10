"""Isolated serialized validation of the audit-fix + NBRS serialization fixes.

Run a focused CAS/recordfmt autotest against this checkout's package using a
private settings/saves directory (-autotest-userdir) so the owner's
Documents/Simitone/config.ini and game-data are untouched. Only one TheSims
process may run at a time; this script never stops another process and fails
if one is already active. Adapted from cas-support's validate.py (uses pgrep
instead of ps, plus a strict recordfmt/CAS verdict check).
"""
from pathlib import Path
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import time

repo = Path(__file__).resolve().parents[3]
evidence = Path(__file__).resolve().parent
if len(sys.argv) < 2:
    raise SystemExit('usage: validate.py <flow|recordfmt|combined|default|focused> [width] [--foreground]')
mode = sys.argv[1]
width = int(sys.argv[2]) if len(sys.argv) > 2 else 1024
height = 600 if width == 800 else 768
assert mode in ('flow', 'recordfmt', 'combined', 'default', 'focused')
config = Path.home() / 'Documents/Simitone/config.ini'
before = config.read_bytes()
private = Path(tempfile.mkdtemp(prefix='cas-serial-'))
settings = before.decode('utf-8-sig')
for key, value in [('GraphicsWidth', width), ('GraphicsHeight', height), ('DPIScaleFactor', 1)]:
    settings = re.sub(r'^' + key + r'=.*$', f'{key}={value}', settings, flags=re.M)
(private / 'config.ini').write_text(settings)


def game_pids():
    try:
        out = subprocess.check_output(['pgrep', '-f', 'TheSims'], text=True, stderr=subprocess.DEVNULL)
        return {int(x) for x in out.split() if x.strip().isdigit()}
    except subprocess.CalledProcessError:
        return set()


if game_pids():
    raise SystemExit('Another TheSims process is active; run later. No process was stopped.')
command = [str(repo / 'dist/The Sims-arm64.app/Contents/MacOS/TheSims'),
           '-ApplePersistenceIgnoreState', 'YES', '-path' + str(repo / 'game-data/The Sims'),
           '-autotest', '5', '-autotest-userdir', str(private), '-autotest-timeout', '1800000']
if mode != 'default':
    command += ['-autotest-opts', {'flow': 'ucasflow', 'recordfmt': 'recordfmt',
                                   'combined': 'ucasflow,recordfmt',
                                   'focused': 'lot,corpus,uicasorig,uiscrap,uivita,uivitaplay'}[mode]]
tag = f'{mode}-{width}'
logpath = evidence / f'{tag}.log'
interruption = None
with logpath.open('w') as log:
    child = subprocess.Popen(command, cwd=repo, stdout=log, stderr=subprocess.STDOUT,
                             env=dict(os.environ, NSUnbufferedIO='YES'))
    started = time.monotonic()
    try:
        if '--foreground' in sys.argv:
            time.sleep(1)
            subprocess.run(['osascript', '-e',
                f'tell application "System Events" to set frontmost of (first process whose unix id is {child.pid}) to true'],
                check=True)
        while child.poll() is None:
            if game_pids() - {child.pid}:
                raise RuntimeError('Another game started; stopping only this test process.')
            if time.monotonic() - started > 2100:
                raise TimeoutError('Test exceeded time budget.')
            time.sleep(1)
    except (RuntimeError, TimeoutError, subprocess.CalledProcessError) as error:
        interruption = str(error)
    finally:
        if child.poll() is None:
            child.terminate()
            try: child.wait(timeout=5)
            except subprocess.TimeoutExpired:
                child.kill(); child.wait()
text = logpath.read_text()
for line in text.splitlines():
    if 'AUTOTEST SUMMARY' in line or 'AUTOTEST recordfmt passed=' in line or 'RESULT ' in line:
        print(line, flush=True)
capture_sizes = sorted(set(re.findall(r'capture (?:02-family-initial-focus|03-person-initial-focus|03-characteredit-preview|03-child-preview|04-preview-suitmemory) (\d+x\d+) ->', text)))
after = config.read_bytes()
(evidence / f'{tag}-isolation.json').write_text(json.dumps({
    'private_directory': str(private), 'owner_config_unchanged': before == after,
    'owner_config_sha256': hashlib.sha256(before).hexdigest(),
    'exit_code': child.returncode, 'interruption': interruption, 'capture_sizes': capture_sizes,
    'recordfmt_verdict': re.search(r'AUTOTEST recordfmt passed=(\d+) failed=(\d+) completed=(\w+) flowFailed=(\d+)',
                                   text).group(0) if re.search(r'AUTOTEST recordfmt ', text) else None,
    'simitone_debug_in_bundle': bool(subprocess.run(
        ['find', str(repo / 'dist/The Sims-arm64.app'), '-name', 'simitone_debug.log'],
        capture_output=True, text=True).stdout.strip()),
}, indent=2) + '\n')
if interruption:
    raise SystemExit(interruption)
assert before == after, 'Owner config changed externally or by the test; it has not been overwritten.'
verdict = re.search(r'AUTOTEST RESULT (PASS|FAIL)', text)
if child.returncode != 0:
    raise SystemExit(f'child exited {child.returncode}; log: {logpath}')
if verdict is None:
    raise SystemExit(f'no AUTOTEST RESULT verdict in {logpath}; unfinished run rejected')
print('AUTOTEST RESULT:', verdict.group(1))
if verdict.group(1) != 'PASS':
    raise SystemExit(f'AUTOTEST RESULT FAIL in {logpath}')
# REL-11: PASS with zero verdicted checks is not a pass.
summary = re.search(r'AUTOTEST SUMMARY passed=(\d+) failed=\d+ skipped=(\d+)', text)
if summary is None:
    raise SystemExit(f'no AUTOTEST SUMMARY counts in {logpath}; verdict rejected')
if int(summary.group(1)) == 0:
    raise SystemExit(f'passed=0: no check verdicted; refusing PASS ({logpath})')
if int(summary.group(2)) != 0:
    raise SystemExit(f'skipped={summary.group(2)} checks did not verdict; refusing PASS ({logpath})')
print('AUTOTEST VALIDATION: PASS')
