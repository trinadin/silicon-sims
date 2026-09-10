#!/usr/bin/env python3
# decode_bhav.py (R219) - pretty-print TS1 BHAVs from an extracted IFF with the
# FreeSO expression grammar (VMExpressionOperand: LhsData:i16 RhsData:i16
# IsSigned:u8 Operator:u8 LhsOwner:u8 RhsOwner:u8, LE) and private-call labels.
# Pointer semantics per VMThread.MoveToInstruction: 254=RETURN_TRUE,
# 255=RETURN_FALSE, 253=follow the other (non-253) path.
import struct, sys

SCOPES = {
    0: "MyAttr", 1: "StackObjAttr", 3: "MyObject", 4: "StackObject", 6: "Global",
    7: "Lit", 8: "Temp", 9: "Param", 10: "StackObjID", 11: "TempByTemp",
    13: "StackObjTemp", 14: "MyMotives", 15: "StackObjMotives", 16: "StackObjSlot",
    17: "StackObjMotiveByTemp", 18: "MyPersonData", 19: "StackObjPersonData",
    20: "MySlot", 21: "StackObjDef", 24: "NeighborInStackObj", 25: "Local",
    26: "Tuning", 28: "TreeAdPersonalityVar", 30: "MyPersonDataByTemp",
    31: "StackObjPersonDataByTemp", 33: "JobData", 34: "NeighborhoodData",
}
OPS = {
    0: ">", 1: "<", 2: "==", 3: "+=", 4: "-=", 5: ":=", 6: "*=", 7: "/=",
    8: "HasFlag", 9: "SetFlag", 10: "ClearFlag", 11: "++<", 12: "%=", 13: "&=",
    14: ">=", 15: "<=", 16: "!=", 17: "-->", 18: "|=", 19: "^=", 20: "sqrt:=",
}
MOTIVES = {0:"HappyLife",1:"HappyWeek",2:"HappyDay",3:"Mood",5:"Energy",6:"Comfort",
           7:"Hunger",8:"Hygiene",9:"Bladder",11:"SleepState",13:"Room",14:"Social",15:"Fun"}
PRIMS = {0:"sleep",1:"generic-sims-call",2:"expression",3:"report-metric",4:"grab",5:"drop",
         6:"change-suit",7:"refresh",8:"random-number",9:"burn",11:"get-distance-to",
         12:"get-direction-to",13:"push-interaction",14:"find-best-object-for",15:"breakpoint",
         16:"find-location-for",17:"idle-for-input",18:"remove-object-instance",20:"run-functional-tree",
         21:"show-string",22:"look-towards",23:"play-sound-event",24:"old-relationship",
         25:"transfer-funds",26:"relationship",27:"goto-relative-position",28:"run-tree-by-name",
         29:"set-motive-change",30:"syslog",31:"set-to-next",32:"test-object-type",
         33:"find-5-worst-motives",34:"ui-effect",35:"special-effect",36:"dialog-all-strings",
         37:"test-sim-interacting",38:"dialog",39:"dialog",40:"online-jobs-call",
         41:"set-balloon-headline",42:"create-object-instance",43:"drop-onto",44:"animate-sim",
         45:"goto-routing-slot",46:"snap",47:"reach",48:"stop-all-sounds",
         49:"notify-out-of-idle",50:"change-action-string",51:"ts1-inventory",
         62:"invoke-plugin",63:"get-terrain-info",65:"find-best-action",66:"set-dyn-obj-name",
         67:"tso-inventory"}

def ptr(p):
    if p == 254: return "RETURN_TRUE"
    if p == 255: return "RETURN_FALSE"
    if p == 253: return "other-path"
    return str(p)

def side(owner, data):
    s = SCOPES.get(owner, "scope%d" % owner)
    if owner in (14, 15):
        return "%s[%s]" % (s, MOTIVES.get(data, data))
    return "%s[%d]" % (s, data)

def main():
    path = sys.argv[1]
    ids = [int(x) for x in sys.argv[2:]]
    data = open(path, 'rb').read()
    # first pass: label index
    labels = {}
    off = 64
    while off + 76 <= len(data):
        ctype = data[off:off+4].decode('latin1')
        csize = struct.unpack('>I', data[off+4:off+8])[0]
        cid = struct.unpack('>H', data[off+8:off+10])[0]
        label = data[off+12:off+76].split(bytes([0]))[0].decode('latin1', 'replace')
        if ctype == 'BHAV': labels[cid] = label
        off += csize
    # second pass: print
    off = 64
    while off + 76 <= len(data):
        ctype = data[off:off+4].decode('latin1')
        csize = struct.unpack('>I', data[off+4:off+8])[0]
        cid = struct.unpack('>H', data[off+8:off+10])[0]
        label = data[off+12:off+76].split(bytes([0]))[0].decode('latin1', 'replace')
        paysz = csize - 76
        payload = data[off+76:off+76+paysz]
        if ctype == 'BHAV' and (not ids or cid in ids):
            cnt = struct.unpack('<H', payload[2:4])[0]
            print('### BHAV id=%d label=%r count=%d' % (cid, label, cnt))
            p = 12
            for i in range(cnt):
                op, tp, fp = struct.unpack('<HBB', payload[p:p+4])
                opd = payload[p+4:p+12]
                txt = ''
                if op == 2:
                    lhs, rhs = struct.unpack('<hh', opd[0:4])
                    oper, lo, ro = opd[5], opd[6], opd[7]
                    txt = '%s %s %s' % (side(lo, lhs), OPS.get(oper, 'op%d' % oper), side(ro, rhs))
                elif op >= 4096:
                    txt = 'CALL %d %r' % (op, labels.get(op, '?'))
                else:
                    txt = 'PRIM %s opd=%s' % (PRIMS.get(op, str(op)), opd.hex())
                print('  ins%-2d %-28s t=%-12s f=%s' % (i, txt, ptr(tp), ptr(fp)))
                p += 12
        off += csize

main()
