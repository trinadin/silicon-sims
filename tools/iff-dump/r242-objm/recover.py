"""Derived metadata only; read original OBJT identity before resolving BHAV."""
from pathlib import Path
import hashlib,json,runpy,struct
ROOT=Path(__file__).resolve().parents[3]; HERE=Path(__file__).resolve().parent
h=runpy.run_path(str(ROOT/'tools/iff-dump/r154/r154-callers-scan.py'))
p=runpy.run_path(str(ROOT/'tools/iff-dump/r241-helper-objm/parse_objm_frames.py'))
b=(ROOT/'game-data/The Sims/UserData/Houses/House56.iff').read_bytes(); digest=hashlib.sha256(b).hexdigest()
chunks={tag.lower():b[start:off+size] for tag,off,size,_,flags,label,start in h['walk_chunks'](b)}
o=p['Objm'](chunks['objm']); i=o.ids.index(292); f=p['BitReader'](o.data,o.objects[i][0]*8)
p['parse_to_stack'](f); frames=p['parse_stack'](f)[2]
b=chunks['objt']; version=struct.unpack_from('<I',b,4)[0]; pos=12; entries=[]
while pos<len(b):
    guid=struct.unpack_from('<I',b,pos)[0]
    if not guid: pos+=4; entries.append({'guid':0}); continue
    tid=struct.unpack_from('<H',b,pos+12)[0]; start=pos+16; end=b.index(0,start)
    name=b[start:end].decode('latin1'); pos=end+1+(len(name)%2==0)+(4 if version>2 else 0)
    entries.append(dict(guid=hex(guid),id=tid,name=name))
owner=entries[145]; guid=int(owner['guid'],16)
far=ROOT/'game-data/The Sims/GameData/Objects/Objects.far'
with far.open('rb') as stream:
    social=next(payload for name,payload in h['far_members'](str(far),stream) if name.lower().endswith('social2.iff'))
# OBJD GUID follows version uint32 and twelve ushort fields: payload+28.
objds=[]; instruction=None
for tag,off,size,_,flags,label,start in h['walk_chunks'](social):
    cid=struct.unpack_from('>H',social,off+8)[0]
    if tag=='OBJD': objds.append(dict(id=cid,label=label,guid=hex(struct.unpack_from('<I',social,start+28)[0])))
    if tag=='BHAV' and cid==4110:
        parsed=h['parse_bhav'](social,start); instruction=parsed['instrs'][1]
actual=[]
for archive in (ROOT/'game-data/The Sims').rglob('*'):
    if not archive.is_file() or archive.suffix.lower()!='.far': continue
    with archive.open('rb') as stream:
        for filename,payload in h['far_members'](str(archive),stream):
            if struct.pack('<I',guid) not in payload: continue
            chunks2=list(h['walk_chunks'](payload))
            if not any(t=='OBJD' and struct.unpack_from('<I',payload,st+28)[0]==guid for t,of,sz,ci,fl,lb,st in chunks2): continue
            for t,of,sz,ci,fl,lb,st in chunks2:
                if t=='BHAV' and struct.unpack_from('>H',payload,of+8)[0]==4110:
                    actual.append(dict(source=str(archive.relative_to(ROOT/'game-data/The Sims'))+'!'+filename,
                                       label=lb,node1=h['parse_bhav'](payload,st)['instrs'][1]))
assert owner['guid']=='0xbedd7b26' and len(actual)==1 and actual[0]['node1'][0]==27
assert instruction[0]==35 and all(int(x['guid'],16)!=guid for x in objds)
engine=(ROOT/'game-data/The Sims/The Sims Complete').read_bytes()
assert hashlib.sha256(engine).hexdigest()=='33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f'
pins={0x1533a4:0x389b0008,0x1533ac:0x4bfcb435,0xfac94:0x80040008,
      0xfacb8:0x901a0008,0xfacd0:0x901a0008}
for address,expected in pins.items(): assert struct.unpack_from('>I',engine,address)[0]==expected
result=dict(pinned_executable_words={hex(k):hex(v) for k,v in pins.items()},actual_owner_routine=actual,house56_sha256=digest,object292_frames=frames,code_owner146=owner,
            social2_objds=objds,bhav4110_node1=instruction)
(HERE/'house56-identity.json').write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps(result,indent=2))
