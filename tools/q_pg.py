import struct
def clean(b): return b.decode('latin1','replace').replace(chr(0),'').strip()
data = open('game-data/The Sims/GameData/Global/Global.far','rb').read()
man = struct.unpack('<I', data[12:16])[0]; num = struct.unpack('<I', data[man:man+4])[0]
off = man+4
for i in range(num):
    dlen = struct.unpack_from('<I', data, off)[0]; dlen2 = struct.unpack_from('<I', data, off+4)[0]; doff = struct.unpack_from('<I', data, off+8)[0]
    nlen = struct.unpack_from('<H', data, off+12)[0]; nm = clean(data[off+16:off+16+nlen]); off += 16+nlen
    if nm.lower() == 'personglobals.iff':
        open('/tmp/PersonGlobals.iff','wb').write(data[doff:doff+dlen])
        print('extracted', nm, dlen)
