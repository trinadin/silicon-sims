#!/usr/bin/env python3
"""Reproduce native CAS state/filter metadata from the owner's local PEF."""
from pathlib import Path
import hashlib
import json
import runpy
import struct
import subprocess
import sys
root=Path(__file__).resolve().parents[3]
out=Path(__file__).resolve().parent
helper=runpy.run_path(str(root/'tools/iff-dump/r176/r176-expansion-category-decode.py'))
raw=helper['ENGINE'].read_bytes()
assert hashlib.sha256(raw).hexdigest()==helper['ENGINE_SHA256']
ranges={
 'character-command':(0x2cd3a0,0x2ce040),
 'character-ctor':(0x2cfc00,0x2cfe20),
 'character-initial-selection':(0x2cf960,0x2cf9e0),
 'sanify-suits':(0x2cc840,0x2cc9b0),
 'family-filter-install':(0x2d15d0,0x2d1640),
 'filter-setter':(0x52ee60,0x52eea0),
 'filter-character':(0x5336b0,0x5339e0),
 'filter-paste':(0x52f920,0x52fa50),
}
for name,(start,end) in ranges.items():
 result=subprocess.run([sys.executable,str(root/'tools/iff-dump/r145/capdis2.py'),hex(start),hex(end)],check=True,capture_output=True,cwd=root)
 (out/(name+'.txt')).write_bytes(result.stdout)
data=helper['unpack_pef_data'](raw[helper['DATA_CONTAINER_OFFSET']:helper['DATA_CONTAINER_OFFSET']+helper['DATA_CONTAINER_LENGTH']])
slot=-0x4358
pointer=helper['word'](data,0x8000+slot)
# PEF section0 header begins at40; containerOffset is its sixth u32.
code_file_start=struct.unpack_from('>I',raw,40+20)[0]
assert code_file_start==0x8e90
start=code_file_start+pointer
chars=raw[start:start+20]
assert chars==b'''\\/|*?:<>"'%()&;@!#,.'''
(out/'filter-bytes.json').write_text(json.dumps({
 'sha256':hashlib.sha256(raw).hexdigest(), 'toc_slot':hex(slot),
 'code_relative_pointer':hex(pointer),'code_file_start':hex(code_file_start),'file_offset':hex(start),
 '20_forbidden_ASCII_bytes':list(chars),'characters':chars.decode('ascii'),
 'ranges':{key:[hex(a),hex(b)] for key,(a,b) in ranges.items()}
},indent=2)+'\n')
