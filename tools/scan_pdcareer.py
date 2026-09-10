#!/usr/bin/env python3
# scan_pdcareer.py - find BHAVs that touch PersonData career slots 56/57/63
# (JobType / JobPromotionLevel / JobPerformance) or the JobData scope (33) across the original
# corpus. Operand decode matches tools/bhav_probe (op==2 EXPR: lhs=o[0:2] sub=o[5] ls=o[6] rs=o[7]).
# Usage: scan_pdcareer.py <far-or-iff>...
import struct, sys

MYPD = 18
STACKPD = 19
JOBDATA = 33
CAREER = {56, 57, 58, 59, 63}
WRITE = {3, 4, 5, 6, 7, 9, 10, 11, 12, 13, 20}
OPS = ["GreaterThan","LessThan","Equals","PlusEquals","MinusEquals","Assign","MulEquals","DivEquals","IsFlagSet","SetFlag","ClearFlag","IncAndLessThan","ModEquals","AndEquals","GEQ","LEQ","NotEqual","DecAndGT","Push","Pop","SqrtRHS"]


def clean(b):
    return b.decode('latin1','replace').replace(chr(0),'').strip()

def iter_iff_bhavs(body):
    n = len(body)
    off = 64
    while off + 76 <= n:
        ct = body[off:off+4].decode('latin1','replace')
        csize = struct.unpack('>I', body[off+4:off+8])[0]
        cid = struct.unpack('>H', body[off+8:off+10])[0]
        label = clean(body[off+12:off+76])
        paysz = csize - 76
        payload = body[off+76:off+76+paysz]
        if ct == 'BHAV':
            ver = struct.unpack('<H', payload[0:2])[0] if len(payload) >= 8 else 0
            if ver == 0x8002 and len(payload) >= 6:
                cnt = struct.unpack('<H', payload[2:4])[0]
                ins = []
                p = 12
                for _ in range(cnt):
                    if p + 12 > len(payload):
                        break
                    op, tp, fp = struct.unpack('<HBB', payload[p:p+4])
                    opd = payload[p+4:p+12]
                    ins.append((op, opd))
                    p += 12
                yield cid, label, ins
        off += 76 + paysz

def scan_bhav(src, bhav_id, label, ins):
    for i, (op, opd) in enumerate(ins):
        if op != 2 or len(opd) != 8:
            continue
        lhs = opd[0] | (opd[1] << 8)
        sub = opd[5]
        ls = opd[6]
        rs = opd[7]
        opn = OPS[sub] if sub < len(OPS) else ('?'+str(sub))
        if ls == JOBDATA or rs == JOBDATA:
            rhs = (opd[2] | (opd[3] << 8))
            print('  JOBDATA lhs=%d ls=%d op=%s rhs=%d rs=%d  BHAV %d %r  [%s]' % (lhs, ls, opn, rhs, rs, bhav_id, label, src))
        elif ls in (MYPD, STACKPD) and (lhs & 0x7FFF) in CAREER:
            rhs = (opd[2] | (opd[3] << 8))
            kind = 'WRITE' if (sub in WRITE) else ('READ' if sub in (0,1,2,14,15,16) else '?')
            print('  pd[%d] ins%d op=%s %s rhs=%d (scope=%d)  BHAV %d %r  [%s]' % (lhs & 0x7FFF, i, kind, opn, rhs, ls, bhav_id, label, src))

def scan_iff_bytes(body, srcname):
    try:
        for bhav_id, label, ins in iter_iff_bhavs(body):
            scan_bhav(srcname, bhav_id, label, ins)
    except Exception as e:
        print('  !! parse error %s: %s' % (srcname, e))

def far_members(data):
    if data[0:8] != b'FAR!byAZ':
        return None
    man = struct.unpack('<I', data[12:16])[0]
    num = struct.unpack('<I', data[man:man+4])[0]
    for hlen in (16, 14):
        off = man + 4
        entries = []
        bad = 0
        for _ in range(num):
            if off + hlen > len(data):
                bad += 1; break
            dlen = struct.unpack('<I', data[off:off+4])[0]
            dlen2 = struct.unpack('<I', data[off+4:off+8])[0]
            doff = struct.unpack('<I', data[off+8:off+12])[0]
            nlen = struct.unpack('<I', data[off+12:off+16])[0] if hlen == 16 else struct.unpack('<H', data[off+12:off+14])[0]
            if not (0 < nlen <= 300):
                bad += 1; break
            nm = data[off+hlen:off+hlen+nlen].decode('latin1','replace')
            entries.append((nm, dlen, dlen2, doff))
            off += hlen + nlen
        if len(entries) == num and bad == 0:
            return [e for e in entries if e[1] == e[2] and 0 < e[3] < len(data)]
    return None

def main():
    for src in sys.argv[1:]:
        data = open(src, 'rb').read()
        if data[0:8] == b'FAR!byAZ':
            mems = far_members(data)
            if mems is None:
                print('== %s : no clean FAR parse' % src); continue
            print('## %s (%d iff members)' % (src, len(mems)))
            for (nm, dlen, dlen2, doff) in mems:
                if not nm.lower().endswith('.iff'):
                    continue
                body = data[doff:doff+dlen]
                scan_iff_bytes(body, nm)
        else:
            scan_iff_bytes(data, src)

if __name__ == '__main__':
    main()
