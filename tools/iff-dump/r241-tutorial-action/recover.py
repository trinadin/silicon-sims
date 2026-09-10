#!/usr/bin/env python3
"""Read-only R241 tutorial action evidence from the owner's executable and FAR."""
from pathlib import Path
import hashlib
import json
import runpy
import struct

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
pef = runpy.run_path(str(ROOT / 'tools/iff-dump/r176/r176-expansion-category-decode.py'))
iff = runpy.run_path(str(ROOT / 'tools/iff-dump/r154/r154-callers-scan.py'))
engine = pef['ENGINE'].read_bytes()
assert hashlib.sha256(engine).hexdigest() == pef['ENGINE_SHA256']
data = pef['unpack_pef_data'](engine[0x5c22f0:0x62fb24])
word = pef['word']
name_base = word(data, 0x8000 - 0x5a30)
name_at = name_base + 0x72
name = data[name_at:data.index(0, name_at)].decode('ascii')
assert name == 'show situation info'
table = word(data, 0x8000 - 0x5998)
case = word(data, table + 49*4) + 0x8e90
assert case == 0xf0634
# Independent key-instruction pins: owner lookup, private Behaviors, and
# distinct parameter/scheduler writes in the two notify cases.
pins = {
    0xe2bfc: 0x806300ac,
    0xe2c08: 0x8083011c,
    0xe2c18: 0x8084000c,
    0xf0670: 0x38000015,
    0xf06d0: 0xb3a30000,
    0xf06ec: 0x5400001e,
    0xf06fc: 0x2c000011,
    0xf0710: 0xb3a30000,
    0xf144c: 0x7c033b2e,
    0xf14a0: 0x38600001,
}
for address, expected in pins.items():
    actual = word(engine, address)
    assert actual == expected, (hex(address), hex(actual), hex(expected))
far = ROOT/'game-data/The Sims/GameData/Objects/Objects.far'
with far.open('rb') as stream:
    tutorial = next(payload for name, payload in iff['far_members'](str(far), stream)
                    if name.lower().endswith('tutorial.iff'))
rows = []
for tag, offset, size, _, flags, label, start in iff['walk_chunks'](tutorial):
    cid, = struct.unpack_from('>H', tutorial, offset+8)
    if tag == 'BHAV' and cid in (4099, 4146):
        parsed = iff['parse_bhav'](tutorial, start)
        for index, (opcode, true, false, *args) in enumerate(parsed['instrs']):
            rows.append(dict(bhav=cid, label=label, index=index, opcode=opcode,
                             true=true, false=false, operand=struct.pack('<hhhh', *args).hex()))
assert [(r['opcode'], r['true'], r['operand']) for r in rows if r['bhav']==4099] == [
    (2, 2, '0100010000050007'), (49, 254, '0000000000000000'), (2, 1, '00000b0000050a03')]
result = dict(engine_sha256=pef['ENGINE_SHA256'], tutorial_sha256=hashlib.sha256(tutorial).hexdigest(),
              name_data_address=hex(name_at), named_tree=name,
              opcode49_jump_table=hex(table), opcode49_file_address=hex(case),
              pinned_words={hex(k):hex(v) for k,v in pins.items()}, instructions=rows)
(HERE/'metadata.json').write_text(json.dumps(result, indent=2)+'\n')
print(json.dumps(dict(named_tree=name, opcode49_file_address=hex(case), instructions=len(rows))))
