#!/usr/bin/env python3
"""Reproduce the R239 camera/album evidence from the owner's local executable."""
from pathlib import Path
import hashlib
import json
import runpy
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
ranges = {
    'frame-snapshot': (0x1c4e10, 0x1c51c4),
    'camera-state': (0x20ca20, 0x20cd40),
    'album-storage': (0x247d60, 0x248c18),
    'album-pages': (0x249520, 0x24a874),
    'family-album': (0x75fd0, 0x76310),
    'family-stream': (0x765a0, 0x767e0),
    'family-load': (0x767e0, 0x76a60),
    'pip-dispatch': (0x2130c0, 0x2135a8),
    'pip-window': (0x298c90, 0x29a5a0),
    'mouse-track': (0x1d0310, 0x1d0680),
    'frame-paint-a': (0x1d09a0, 0x1d0d70),
    'frame-paint-b': (0x1d48c0, 0x1d4cf0),
    'frame-paint-c': (0x1d5ab0, 0x1d60e0),
    'frame-outline': (0x1c6ef0, 0x1c7250),
    'key-down': (0x217590, 0x217e24),
}
for name, (start, end) in ranges.items():
    result = subprocess.run([sys.executable, str(ROOT/'tools/iff-dump/r145/capdis2.py'), hex(start), hex(end)],
                            cwd=ROOT, check=True, capture_output=True)
    (OUT/(name+'.txt')).write_bytes(result.stdout)
helper = runpy.run_path(str(ROOT/'tools/iff-dump/r176/r176-expansion-category-decode.py'))
binary = helper['ENGINE'].read_bytes()
data = helper['unpack_pef_data'](binary[helper['DATA_CONTAINER_OFFSET']:helper['DATA_CONTAINER_OFFSET']+helper['DATA_CONTAINER_LENGTH']])
strings = {}
for slot, offsets in [(-0x5c04, [0,12]), (-0x5254, [0,2,7,0x12]), (-0x5510, [11])]:
    pointer = helper['word'](data, 0x8000+slot)
    strings[hex(slot)] = {'pointer': hex(pointer), 'strings': {
        hex(offset): data[pointer+offset:data.index(b'\0',pointer+offset)].decode('mac_roman') for offset in offsets}}
(OUT/'source-metadata.json').write_text(json.dumps({
    'owned_binary_sha256': hashlib.sha256(binary).hexdigest(), 'toc': strings,
    'symbols': 'tools/iff-dump/r153/r153-symbols.txt',
    'ranges': {name: [hex(start),hex(end)] for name,(start,end) in ranges.items()}
},indent=2)+'\n')
