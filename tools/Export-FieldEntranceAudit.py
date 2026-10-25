"""Instruction-aware, read-only audit. Candidate counts never establish visibility."""
import argparse, hashlib, json, re, sys
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('--game-root', required=True)
parser.add_argument('--output', required=True)
parser.add_argument('--unitypy-root', required=True)
args = parser.parse_args()
sys.path.insert(0, args.unitypy_root)
import UnityPy
source = Path(__file__).resolve().parents[1]
decoder = (source/'Assembly-CSharp/Global/Event/Engine/EventEngineUtils.cs').read_text(encoding='utf-8-sig')
counts = [int(x) for x in re.findall(r'-?\d+', re.search(r'static SByte\[\] opArgCount.*?\{(.*?)\};', decoder, re.S).group(1))]
sizes = []
size_text = re.search(r'static Byte\[\]\[\] opArgSize.*?\{(.*?)\n\s*\};',decoder,re.S).group(1)
for match in re.finditer(r'null|new Byte\[\]\{([^}]*)\}',size_text): sizes.append(None if match.group(0)=='null' else [int(v) for v in re.findall(r'\d+',match.group(1))])
enum_text = re.search(r'public enum event_code_binary\s*\{(.*?)\n\s*\}',(source/'Assembly-CSharp/Global/EBin.cs').read_text(encoding='utf-8-sig'),re.S).group(1)
names={}; number=0
for token in enum_text.split(','):
    token=re.sub(r'//[^\n]*','',token).strip()
    if not token: continue
    parts=token.split('='); name=parts[0].strip()
    if len(parts)>1: number=int(parts[1].strip(),0)
    names[name]=number; number+=1

def u16(b,p): return b[p]|b[p+1]<<8
def expression(b,p):
    start=p
    for _ in range(1024):
        op=b[p]; p+=1
        if op==0x7f: return p
        if op>=0xc0 or op in (0x29,0x5f,0x78,0x79,0x7a,0x7d,0x7e): p+=4 if op==0x7e else 2 if op>=0xe0 or op in (0x78,0x7d) else 1
        if p>=len(b): raise ValueError('Truncated expression')
    raise ValueError('Unbounded expression')

def decode(b,start,end):
    p=start; result=[]
    while p<end:
        offset=p; op=b[p]; p+=1
        if op==255: op=256|b[p]; p+=1
        count=counts[op]; flags=b[p] if op>=16 and count else 0
        if op>=16 and count: p+=1
        if op==5: flags=1
        if count<0:
            count=b[p];p+=1
            if op==13:count|=b[p]<<8;p+=1
            if op==6:count=1+2*count
            elif op in (11,13):count=2+count
        values=[]
        for i in range(count):
            if flags&(1<<i): p=expression(b,p); values.append(None)
            else:
                size=4 if op==41 else 2 if op in (6,11,13) else sizes[op][i] if sizes[op] else 0
                values.append(int.from_bytes(b[p:p+size],'little'));p+=size
        if p>end or p<=offset:raise ValueError('Instruction crosses function boundary')
        result.append((offset,op,values))
    return result

bundle=Path(args.game_root)/'StreamingAssets/p0data7.bin'
env=UnityPy.load(str(bundle)); records=[]; failures=[]; fields=0
for path,obj in env.container.items():
    if '/eventbinary/field/us/' not in path.lower():continue
    try:
        asset=obj.read(); raw=asset.m_Script
        if isinstance(raw,str):raw=raw.encode('utf-8','surrogateescape')
        if raw[:2]!=b'EV':continue
        fields+=1; entry_count=raw[3]; table=128; regions=set(); functions=[]
        for entry in range(entry_count):
            start=table+u16(raw,table+8*entry); length=u16(raw,table+8*entry+2); b=raw[start:start+length]
            if len(b)<4:continue
            tags=[(u16(b,2+4*i),2+u16(b,4+4*i)) for i in range(b[1])]
            for tag,offset in tags:
                end=min([x for _,x in tags if x>offset]+[len(b)])
                codes=decode(b,offset,end); functions.append((entry,tag,b,codes))
                for _,op,values in codes:
                    # NEW2 arg1 is the script entry; arg2 is its runtime UID.
                    # Candidate entryId indexes scripts, so UID cannot classify it.
                    if op==names['NEW2'] and len(values)>=2 and values[0] is not None:regions.add(values[0])
        for entry,tag,b,codes in functions:
            jumps=[offset for offset,op,_ in codes if op in (names['MAPJUMP'],names['WMAPJUMP'])]
            if not jumps:continue
            walkoff=any(op==names['MJPOS'] for _,op,_ in codes) and any(op==names['MOVQ'] for _,op,_ in codes)
            records.append({'asset':path,'entryId':entry,'tag':tag,'regionEntry':entry in regions,'sourceFingerprint':hashlib.sha256(b).hexdigest(),'jumpOffsets':jumps,'walkOffCandidate':walkoff,'evidenceStatus':'unresolved','reason':'Runtime branch state and current-camera presentation are required; polling and interior-door candidates require review.'})
    except Exception as exc:
        import traceback
        failures.append({'asset':path,'error':str(exc),'trace':traceback.format_exc()})
report={'schema':1,'decoderSourceSHA256':hashlib.sha256(decoder.encode()).hexdigest(),'bundleSHA256':hashlib.sha256(bundle.read_bytes()).hexdigest(),'language':'US','unitypyVersion':UnityPy.__version__,'fieldScripts':fields,'candidates':records,'failures':failures,'note':'Jump arguments and future destinations are intentionally omitted. Static candidates are not eligible player targets.'}
Path(args.output).write_text(json.dumps(report,indent=2),encoding='utf-8')
print(f'PASS entrance audit: {fields} scripts, {len(records)} candidate functions, {len(failures)} unresolved decode failures')
if failures:sys.exit(1)
