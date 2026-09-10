#!/usr/bin/env python3
"""Read-only original-resource metadata scan; writes no game assets."""
from pathlib import Path
import json
import runpy
import struct

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
DATA = ROOT / 'game-data/The Sims'
helpers = runpy.run_path(str(ROOT / 'tools/iff-dump/r154/r154-callers-scan.py'))


def iff_files():
    for path in sorted(DATA.rglob('*')):
        if not path.is_file():
            continue
        if path.suffix.lower() in ('.iff', '.fam'):
            yield str(path.relative_to(DATA)), path.read_bytes()
        elif path.suffix.lower() == '.far':
            with path.open('rb') as f:
                for name, data in helpers['far_members'](str(path), f):
                    if data.startswith(b'IFF FILE'):
                        yield str(path.relative_to(DATA)) + '!' + name, data


class Fields:
    def __init__(self, data, pos):
        self.data, self.bit = data, pos * 8

    def bits(self, n):
        value = 0
        for _ in range(n):
            value = (value << 1) | ((self.data[self.bit // 8] >> (7-self.bit % 8)) & 1)
            self.bit += 1
        return value

    def short(self):
        if not self.bits(1):
            return 0
        width = (5, 8, 13, 16)[self.bits(2)]
        value = self.bits(width)
        return value - (1 << width) if value & (1 << (width-1)) else value


def objm_owner(data):
    # Original DoStream writes owner short immediately after final ReconMark.
    version, magic = struct.unpack_from('<II', data, 4)
    assert magic == 0x4f626a4d and data[12] == 1
    fields = Fields(data, 13)
    ids = []
    while True:
        oid = fields.short()
        if oid == 0:
            break
        ids.append(oid)
        fields.short()
    pos = (fields.bit+7)//8
    for _ in ids:
        skip, = struct.unpack_from('<i', data, pos)
        pos = 12 + skip
    pos += 4  # final ReconMark has no successor but still occupies four bytes
    return dict(version=version, objects=len(ids), trailer_offset=pos,
                trailer_prefix=data[pos:pos+8].hex(), owner=Fields(data,pos).short())


def main():
    hits, trailers, tutorial = [], [], []
    count = 0
    for name, data in iff_files():
        count += 1
        for tag, off, size, _wrong_cid, flags, label, start in helpers['walk_chunks'](data):
            # IFF chunk IDs are big endian; BHAV operands are little endian.
            cid, = struct.unpack_from('>H', data, off+8)
            if tag == 'BHAV':
                bhav = helpers['parse_bhav'](data, start)
                if not bhav:
                    continue
                instrs = []
                for index, (op, t, f, *args) in enumerate(bhav['instrs']):
                    record = dict(file=name, bhav=cid, label=label, index=index,
                                  opcode=op, true=t, false=f,
                                  operand=struct.pack('<hhhh', *args).hex())
                    instrs.append(record)
                    if op == 10:
                        hits.append(record)
                if name.endswith('!Tutorial.iff'):
                    tutorial.extend(instrs)
            elif tag == 'ObjM':
                try:
                    trailers.append(dict(file=name, chunk=cid, **objm_owner(data[start:off+size])))
                except (AssertionError, IndexError, struct.error) as e:
                    trailers.append(dict(file=name, chunk=cid, error=repr(e)))
    result = dict(iff_files=count, opcode10_sites=hits, objm_trailers=trailers)
    (HERE/'corpus.json').write_text(json.dumps(result, indent=2)+'\n')
    (HERE/'tutorial-corpus.txt').write_text('\n'.join(
        f"BHAV {r['bhav']} ({r['label']}) [{r['index']}] opcode={r['opcode']:04x} "
        f"T={r['true']} F={r['false']} operand={r['operand']}" for r in tutorial)+'\n')
    print(json.dumps(dict(iff_files=count, opcode10_sites=len(hits),
                         objm_trailers=len(trailers), nonzero_owners=[r for r in trailers if r.get('owner')],
                         trailer_errors=[r for r in trailers if 'error' in r]), indent=2))


if __name__ == '__main__':
    main()
