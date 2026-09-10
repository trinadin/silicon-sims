#!/usr/bin/env python3
"""R132: byte-by-byte CodeWarrior end-of-function symbol scan (the R131 recipe)
over 0x288000-0x292500 listing EVERY symbol so SetPerson/TrackPerson bodies get
exact boundaries; plus RT id immediate scan."""
import re, struct, os

BIN = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                   '../../../game-data/The Sims/The Sims Complete')
data = open(BIN, 'rb').read()

IDENT = re.compile(rb'^[A-Za-z_.$][A-Za-z0-9_.$]*$')

LO, HI = 0x288000, 0x292500
print('== ALL end-of-function symbols 0x%x-0x%x ==' % (LO, HI))
i = LO
syms = []
while i + 2 < HI:
    ln = struct.unpack('>H', data[i:i+2])[0]
    if 4 <= ln <= 72 and i + 2 + ln <= len(data):
        s = data[i+2:i+2+ln]
        if IDENT.match(s) and (b'__' in s or s.startswith(b'.')):
            syms.append((i, s.decode()))
            i += 2 + ln
            continue
    i += 1
for a, s in syms:
    print('0x%06x  %s' % (a, s))
print('total', len(syms))

IDS = {4601: 'kTrackingTarget', 4800: 'kHouseBars', 4801: 'kJobBars', 4802: 'kRelBars',
       4822: 'kFameSubpanelBars', 4823: 'kFameSubBarsOff', 4900: 'kHouseSubpanelBars',
       4901: 'kJobSubpanelBars', 4919: 'kHouseSubBarsOff', 4920: 'kJobSubBarsOff',
       1619: 'kInterestBars'}
print()
print('== addi-form id immediates 0x286000-0x292500 (any register) ==')
for off in range(0x286000, 0x292500, 4):
    w = int.from_bytes(data[off:off+4], 'big')
    if (w >> 26) == 14 and (w & 0xFFFF) in IDS and ((w >> 16) & 31) == 0:
        print('0x%06x: %08x  addi r%d,r0,%d (%s)'
              % (off, w, (w >> 21) & 31, w & 0xFFFF, IDS[w & 0xFFFF]))
