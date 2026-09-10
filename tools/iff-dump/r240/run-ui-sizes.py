#!/usr/bin/env python3
"""Run owner-local native-window UI captures; restore config bytes on exit."""
from pathlib import Path
import hashlib
import re
import shutil
import subprocess

repo = Path(__file__).resolve().parents[3]
config = Path.home() / 'Documents/Simitone/config.ini'
binary = repo / 'dist/The Sims-arm64.app/Contents/MacOS/TheSims'
original = config.read_bytes()
output = repo / 'build/ui-audit/r240'
output.mkdir(parents=True, exist_ok=True)
(output / 'config-before-size-audit.ini').write_bytes(original)
try:
    for width, height, dpi in [(800, 600, 1), (1280, 800, 1), (800, 600, 2)]:
        tag = f'{width}x{height}' + ('@2x' if dpi == 2 else '')
        settings = original.decode('utf-8-sig')
        settings = re.sub(r'^GraphicsWidth=.*$', f'GraphicsWidth={width}', settings, flags=re.M)
        settings = re.sub(r'^GraphicsHeight=.*$', f'GraphicsHeight={height}', settings, flags=re.M)
        settings = re.sub(r'^DPIScaleFactor=.*$', f'DPIScaleFactor={dpi}', settings, flags=re.M)
        config.write_text(settings)
        log_path = output / f'size-{tag}.log'
        args = [str(binary), '-ApplePersistenceIgnoreState', 'YES',
                '-path' + str(repo / 'game-data/The Sims'), '-autotest', '5',
                '-autotest-opts', 'lot,corpus,uisurvey,uiaudit,uismall,uisyschrome,uicasorig,uidialog,uitt,uiscrap,uitutorial,uicapture,uipip,uiclip',
                '-autotest-timeout', '180000']
        with log_path.open('w') as stream:
            result = subprocess.run(args, cwd=repo, stdout=stream,
                                    stderr=subprocess.STDOUT, timeout=240)
        log = log_path.read_text()
        assert result.returncode == 0 and 'AUTOTEST RESULT PASS' in log, log_path
        assert 'after Run' in log and 'after Dispose' in log, log_path
        paths = list(dict.fromkeys(re.findall(r'AUTOTEST uisurvey dump .*? -> (.+)', log)))
        target = output / tag
        target.mkdir(exist_ok=True)
        for path in paths:
            shutil.copy2(path, target / Path(path).name)
        pip_images = Path.home() / 'Documents/Simitone/ui-audit/r240'
        for pip_image in pip_images.glob('pip-*.png'):
            shutil.copy2(pip_image, target / pip_image.name)
        manifest = ''.join(hashlib.sha256(p.read_bytes()).hexdigest()
                           + '  ' + p.name + '\n' for p in sorted(target.glob('*.png')))
        (repo / f'tools/iff-dump/r240/{tag}-images.sha256').write_text(manifest)
        print(f'{tag}: PASS; {len(paths)} fresh UI captures plus {len(list(pip_images.glob("pip-*.png")))} PIP renders', flush=True)
finally:
    config.write_bytes(original)
    assert config.read_bytes() == original
    print('Original config restored byte-for-byte.', flush=True)
