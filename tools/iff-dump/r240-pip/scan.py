#!/usr/bin/env python3
"""Independent read-only opcode35 corpus and operand fixture recovery."""
from pathlib import Path
import collections
import json
import runpy
import struct

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
p = runpy.run_path(str(ROOT/'tools/iff-dump/r239-tutorial/scan.py'))


def decode(raw):
    value = struct.unpack_from('<h', raw)[0]
    size, zoom, flags, caption = raw[2:6]
    return dict(timeout_literal=value, timeout_local_index=value if flags & 32 else None,
                duration_ms=None if flags & 32 else value*1000,
                size_index=size, size_pixels=100 if size == 0 else 200 if size == 1 else 300,
                zoom_index=zoom, open=bool(flags & 1), skip_if_visible=not bool(flags & 4),
                main_camera=bool(flags & 8), auto_snapshot=bool(flags & 16),
                suppress_pre_dispatch_notification=bool(flags & 64), caption_index=caption)


def main():
    sites = []
    for name, data in p['iff_files']():
        for tag, off, size, _, flags, label, start in p['helpers']['walk_chunks'](data):
            if tag != 'BHAV':
                continue
            bhav = p['helpers']['parse_bhav'](data, start)
            if not bhav:
                continue
            cid = struct.unpack_from('>H', data, off+8)[0]
            for i, (op, true, false, *args) in enumerate(bhav['instrs']):
                if op != 35:
                    continue
                raw = struct.pack('<hhhh', *args)
                sites.append(dict(file=name, bhav=cid, label=label, instruction=i,
                                  true=true, false=false, operand=raw.hex(), **decode(raw)))
    (HERE/'corpus.json').write_text(json.dumps(sites, indent=2)+'\n')
    print(json.dumps(dict(sites=len(sites),
                         zooms=dict(collections.Counter(x['zoom_index'] for x in sites)),
                         sizes=dict(collections.Counter(x['size_index'] for x in sites)),
                         local_timeouts=sum(x['timeout_local_index'] is not None for x in sites)), indent=2))
    fixtures = ['0a00010315000000', 'ffff02000100a55a', '0100ff80e5ff0102',
                '0000000000000000', '0200010229000000']
    (HERE/'operand-fixtures.json').write_text(json.dumps(
        [dict(operand=x, **decode(bytes.fromhex(x))) for x in fixtures], indent=2)+'\n')


if __name__ == '__main__':
    main()
