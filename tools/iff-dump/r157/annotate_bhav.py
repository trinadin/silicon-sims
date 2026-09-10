#!/usr/bin/env python3
# annotate_bhav.py — full pseudo-code listing of a TS1 BHAV (v0x8002, 8-byte operands)
# using the port's OWN schemas (FreeSO VMExpressionOperand / VMSetToNextOperand /
# VMVariableScope / VMExpressionOperator — r157). Usage: annotate_bhav.py <iff> <id>
import struct, sys

SCOPES = {0:'MyAttr',1:'StkObjAttr',2:'TgtAttr',3:'MyObj',4:'StkObj',5:'TgtObj',6:'Global',
 7:'Lit',8:'Temp',9:'Param',10:'StkObjID',11:'TempByTemp',12:'TreeAdRange',13:'StkObjTemp',
 14:'MyMotives',15:'StkObjMotives',16:'StkObjSlot',17:'StkObjMotiveByTemp',18:'MyPD',
 19:'StkObjPD',20:'MySlot',21:'StkObjDef',22:'StkObjAttrByParam',23:'RoomByTemp0',
 24:'NeighborInStkObj',25:'Local',26:'Tuning',27:'DynSpriteFlag',28:'TreeAdPersonalityVar',
 29:'TreeAdMin',30:'MyPDByTemp',31:'StkObjPDByTemp',32:'NeighborPD',33:'JobData',
 34:'NeighborhoodData',35:'StkObjFunction',36:'MyTypeAttr',37:'StkObjTypeAttr',38:'NeighborsObjDef'}
OPS = {0:'>',1:'<',2:'==',3:'+=',4:'-=',5:'=',6:'*=',7:'/=',8:'flagSet',9:'setFlag',10:'clearFlag',
 11:'++&<',12:'%=',13:'&=',14:'>=',15:'<=',16:'!=',17:'--&>',18:'|=',19:'^=',20:'sqrt->'}
PRIMS = {0:'sleep',2:'expression',4:'grab',5:'drop',6:'changeSuit',7:'refresh',8:'random',
 11:'getDistanceTo',12:'getDirectionTo',13:'pushInteraction',14:'findBestObjectForFunction',
 15:'breakPoint',16:'findLocationFor',17:'idleForInput',18:'removeObjectInstance',
 20:'runFunctionalTree',21:'showString',22:'lookTowards',23:'playSound',24:'relationship',
 26:'relationship',27:'gotoRelativePosition',28:'runTreeByName',29:'setMotiveChange',
 30:'sysLog/gosubFoundAction',31:'setToNext',32:'testObjectType',35:'specialEffect',
 36:'dialogPrivateStrings',37:'testSimInteractingWith',38:'dialogGlobalStrings',
 39:'dialogSemiGlobalStrings',41:'setBalloonHeadline',42:'createObjectInstance',43:'dropOnto',
 44:'animateSim',45:'gotoRoutingSlot',46:'snap',47:'reach',48:'stopAllSounds',
 49:'notifyOutOfIdle',50:'changeActionString',62:'invokePlugin',63:'getTerrainInfo',
 65:'findBestAction',67:'inventoryOps'}
SEARCH = {0:'Object',1:'Person',2:'NonPerson',3:'MultiPartTile',4:'ObjectOfType',5:'NeighborId',
 6:'ObjCategorySP0',7:'NeighborOfType',8:'ObjectOnSameTile',9:'ObjAdjacentToLocal',
 10:'Career',11:'ClosestHouse',12:'FamilyMember'}

def rd_i16(b,o): return struct.unpack_from('<h',b,o)[0]

def main():
    path, want = sys.argv[1], int(sys.argv[2])
    data = open(path,'rb').read()
    off = 64
    while off+2 <= len(data):
        ctype = data[off:off+4].decode('latin1')
        csize = struct.unpack('>I', data[off+4:off+8])[0]
        cid = struct.unpack('>H', data[off+8:off+10])[0]
        label = data[off+12:off+76].split(bytes([0]))[0].decode('latin1','replace')
        paysz = csize-76
        if ctype=='BHAV' and cid==want:
            payload = data[off+76:off+76+paysz]
            cnt = struct.unpack('<H', payload[2:4])[0]
            print('### BHAV %d %r (%d instructions)' % (cid,label,cnt))
            p = 12
            for i in range(cnt):
                op,tp,fp = struct.unpack('<HBB', payload[p:p+4])
                o = payload[p+4:p+12]
                dest = 'T%d/F%d'%(tp,fp)
                if op == 2:
                    lhs,rhs = rd_i16(o,0), rd_i16(o,2)
                    oper,lsco,rsco = o[5],o[6],o[7]  # [lhs2][rhs2][IsSigned][Operator][LhsOwner][RhsOwner]
                    print('ins%-2d %-8s %s[%d] %s %s[%d]' % (i,dest,
                        SCOPES.get(lsco,'s%d'%lsco),lhs,OPS.get(oper,'op%d'%oper),
                        SCOPES.get(rsco,'s%d'%rsco),rhs))
                elif op == 31:
                    guid = struct.unpack('<I',o[0:4])[0]
                    flags,tsco,local,tdata = o[4],o[5],o[6],o[7]
                    st = SEARCH.get(flags&0x7f, str(flags&0x7f))
                    print('ins%-2d %-8s setToNext %s guid=0x%08x -> %s[%d] (flags=0x%02x)' %
                        (i,dest,st,guid,SCOPES.get(tsco,'s%d'%tsco),tdata,flags))
                elif op >= 256:
                    where = 'semi-global' if op>=8192 else ('own-iff' if op>=4096 else 'global.iff')
                    print('ins%-2d %-8s CALL %d (%s) opd=%s' % (i,dest,op,where,o.hex()))
                else:
                    print('ins%-2d %-8s PRIM %s (%d) opd=%s' % (i,dest,PRIMS.get(op,'?'),op,o.hex()))
                p += 12
            return 0
        off += 76 + paysz
    print('BHAV %d not found' % want); return 1

sys.exit(main())
