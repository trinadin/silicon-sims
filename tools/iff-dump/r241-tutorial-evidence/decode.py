#!/usr/bin/env python3
"""Decode instruction metadata from the owner's original; never copy assets."""
from pathlib import Path
import runpy
ROOT = Path(__file__).resolve().parents[3]
helper = runpy.run_path(str(ROOT / 'tools/iff-dump/r145/cdis.py'))
helper['md'].skipdata = True
for name, start, end in [('icon',0x25dd80,0x25f030),('dialog',0xcc1c0,0xcde20)]:
    (Path(__file__).parent/(name+'.txt')).write_text(helper['dis'](start,end)+'\n')
