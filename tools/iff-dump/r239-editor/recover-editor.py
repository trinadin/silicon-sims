#!/usr/bin/env python3
"""Read original PPC editor contracts; emit address-labelled metadata only."""
from pathlib import Path
import hashlib
import capstone
root=Path(__file__).resolve().parents[3]
raw=(root/'game-data/The Sims/The Sims Complete').read_bytes()
assert hashlib.sha256(raw).hexdigest()=='33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f'
symbols={}
for line in (root/'tools/iff-dump/r141/symbol-index.txt').read_text().splitlines():
 parts=line.split()
 if len(parts)>2:
  try:symbols[int(parts[0],16)&~3]=parts[2]
  except ValueError:pass
md=capstone.Cs(capstone.CS_ARCH_PPC,capstone.CS_MODE_32|capstone.CS_MODE_BIG_ENDIAN)
md.skipdata=True
for name,start,end in [('IndexToLineNum',0x5301a0,0x5302a0),('MoveWord',0x532760,0x532954),('MoveCursor',0x532990,0x532dac),('CharacterStride',0x532de0,0x532ef0),('DoubleClickAndKeys',0x5332d0,0x533680)]:
 print('\n'+name)
 for i in md.disasm(raw[start:end],start):
  target=int(i.op_str,16) if i.mnemonic in ['b','bl'] and i.op_str.startswith('0x') else -1
  note=' ; '+symbols[target] if target in symbols else ''
  print(f'{i.address:08x}: {i.mnemonic:8} {i.op_str}{note}'.rstrip())
