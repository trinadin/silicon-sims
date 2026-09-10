#!/usr/bin/env python3
"""Offline original-byte pins and independently derived PIP fade examples.

Reads owned PEF in memory; emits metadata only. No game/GPU/asset writes.
The arithmetic examples are source-derived fixtures, not native execution.
"""
from pathlib import Path
import hashlib
import json
import runpy
import struct

ROOT = Path(__file__).resolve().parents[3]
raw = (ROOT / 'game-data/The Sims/The Sims Complete').read_bytes()
assert hashlib.sha256(raw).hexdigest() == '33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f'
decoder = runpy.run_path(str(ROOT / 'tools/iff-dump/r176/r176-expansion-category-decode.py'))
data = decoder['unpack_pef_data'](raw[0x5c22f0:0x5c22f0+0x6d834])
word = lambda offset: struct.unpack_from('>I', data, offset)[0]
pins = {
    0x502fa0: 0x3ba0014d,  # default duration 333
    0x503018: 0xec220828,  # reverse closing -> opening start = 1-current
    0x503044: 0xc8630000,  # opening -> closing duration float conversion
    0x50305c: 0xec220072,  # duration * current
    0x503110: 0x9b9a00c5,  # force paint flag, only new idle transition
    0x506618: 0x8bdc00c5,  # force paint consumed by Plot
    0x506630: 0x818c00bc,  # GetWantsFadeCapture
    0x506640: 0x418201c4,  # false skips local capture allocation
    0x506424: 0x57a0eefe,  # opacity >> 3
    0x506428: 0x2c00001f,  # fully opaque bucket31
    0x5064a8: 0x80ba00c8,  # intermediate blend needs capture buffer
    0x5064b0: 0x41820038,  # null capture skips output
    0x5063d8: 0x818c00a0,  # actual hide on fade completion
    0x503178: 0x818c009c,  # show at opening start
    0x507b94: 0x38800001,  # child recursion visible bit
    0x507b9c: 0x818c0094,  # GetFlag
    0x507c34: 0x818c0078,  # point in screen coords
    0xcbbd0: 0x818c007c,   # point in local coords
    0xcbcb8: 0x38800200,   # optional color-mask hit testing
    0x50383c: 0x38804000,  # overlaps-scroll flag only
}
for address, expected in pins.items():
    actual = struct.unpack_from('>I', raw, address)[0]
    assert actual == expected, (hex(address), hex(actual), hex(expected))

vtable = word(0x8000-27044)
slots = {0x78:0xcbb50, 0x7c:0xcbc40, 0x94:0xcbed0, 0x9c:0x5029d0,
         0xa0:0x298dc0, 0xb0:0x502e10, 0xb8:0x502770,
         0xbc:0x298c90, 0xd8:0x506200, 0x17c:0x505840}
for slot, expected in slots.items():
    assert word(word(vtable+slot))+0x8e90 == expected
assert word(word(0x8000-0x6b90)) == 0x13
consts = struct.unpack_from('>ffff', raw, word(0x8000-0x4414)+0x8e90)
assert consts == (0.5,255.0,1.0,0.0)
popup = word(0x8000-0x5008)+0x8e90
assert struct.unpack_from('>I', raw, popup+9*4)[0] == 3714

# PEF loader section starts file128. Its initial ImportRun/SmByImport
# relocation operations map data0x54c (TOC -0x7ab4) to import341.
loader = raw[128:128+36356]
header = struct.unpack_from('>14I',loader)
assert header[6:11] == (7,535,1,2376,28260)
ops = struct.unpack_from('>5H',loader,header[9])
assert ops == (0x4add,0x60df,0x4a51,0x6133,0x4ae2)
position = imported_index = 0
imports = {}
for op in ops:
    if op & 0xfe00 == 0x4a00:  # ImportRun
        for _ in range((op & 0x1ff)+1):
            imports[position] = imported_index
            position += 4
            imported_index += 1
    else:  # SmByImport: relocate current word by explicit import
        assert op & 0xfe00 == 0x6000
        imported_index = op & 0x1ff
        imports[position] = imported_index
        position += 4
        imported_index += 1
assert imports[0x54c] == 341
symbol_word = struct.unpack_from('>I',loader,56+7*24+341*4)[0]
name_start = header[10]+(symbol_word & 0xffffff)
assert loader[name_start:loader.index(0,name_start)] == b'Microseconds'
calibration = word(0x8000-0x5d78)+0x8e90
assert struct.unpack_from('>d',raw,calibration+0x18)[0] == 500.0

f32 = lambda value: struct.unpack('>f',struct.pack('>f',value))[0]
def ramp(start, end, duration, elapsed):
    if elapsed >= duration:
        return end
    slope = f32(f32(end-start)/duration)
    return f32(end - f32(duration-elapsed)*slope)

def alpha(value):
    return int(f32(255.0*value+0.5))

examples = []
for opening in [True,False]:
    start,end = (0.,1.) if opening else (1.,0.)
    for elapsed in [0,9,10,100,166,322,323,324,332,333]:
        value = ramp(start,end,333,elapsed)
        ink = alpha(value)
        examples.append(dict(opening=opening,elapsed=elapsed,alpha=ink,
                             normalPipCopiesPixels=ink>>3==31,
                             logicallyVisible=opening or elapsed<333))
assert next(x for x in examples if x['opening'] and x['elapsed']==322)['normalPipCopiesPixels'] is False
assert next(x for x in examples if x['opening'] and x['elapsed']==323)['normalPipCopiesPixels'] is False
assert next(x for x in examples if x['opening'] and x['elapsed']==324)['normalPipCopiesPixels'] is True
assert next(x for x in examples if not x['opening'] and x['elapsed']==9)['normalPipCopiesPixels'] is True
assert next(x for x in examples if not x['opening'] and x['elapsed']==10)['normalPipCopiesPixels'] is False
reversals = [dict(oldDirection='closing',currentValue=.75,newStart=.25,newEnd=1.,duration=83),
             dict(oldDirection='opening',currentValue=.25,newStart=.25,newEnd=0.,duration=83)]
print(json.dumps(dict(bytePins=len(pins),vtablePins=len(slots),checks='PASS',
                     assumptions='normal PIP flags, no capture buffer, full parent redraw',
                     examples=examples,reversals=reversals),indent=2))
