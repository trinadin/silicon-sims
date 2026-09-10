import struct
def clean(b): return b.decode('latin1','replace').replace(chr(0),'').strip()
data = open('game-data/The Sims/ExpansionShared/ExpansionShared.far','rb').read()
man = struct.unpack('<I', data[12:16])[0]; num = struct.unpack('<I', data[man:man+4])[0]
off = man+4
for i in range(num):
    dlen = struct.unpack_from('<I', data, off)[0]; dlen2 = struct.unpack_from('<I', data, off+4)[0]; doff = struct.unpack_from('<I', data, off+8)[0]
    nlen = struct.unpack_from('<H', data, off+12)[0]; nm = clean(data[off+16:off+16+nlen]); off += 16+nlen
    if nm.lower() != 'work.iff': continue
    body = data[doff:doff+dlen]
    open('/tmp/work_extracted.iff','wb').write(body)
    pos = 64; types = {}
    while pos + 76 <= len(body):
        ct = body[pos:pos+4].decode('latin1','replace'); sz = struct.unpack_from('>I', body, pos+4)[0]-76; pos += 76
        if sz < 0 or pos+sz > len(body): break
        types[ct] = types.get(ct,0)+1
        pos += sz
    print('work.iff dlen', dlen, 'chunks', types)
