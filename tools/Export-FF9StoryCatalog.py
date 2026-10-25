"""Compile original navigation metadata from a read-only native field inventory.

The input is the decoded installed event inventory, not walkthrough prose.
No game files are modified. Functions that are automatic or unresolved remain
recorded in the ledger; their writes do not invent an interaction position.
"""
import argparse, json, hashlib
from pathlib import Path

p=argparse.ArgumentParser()
p.add_argument('--inventory',required=True)
p.add_argument('--writes',required=True)
p.add_argument('--output',required=True)
p.add_argument('--ledger',required=True)
p.add_argument('--review',default=str(Path(__file__).resolve().parents[1]/'docs/accessibility/story-main-steps.json'))
p.add_argument('--world-markers',default=str(Path(__file__).resolve().parents[1]/'docs/accessibility/story-world-markers.json'))
a=p.parse_args()
inventory=Path(a.inventory).read_bytes()
fields=json.loads(inventory)
writes=json.loads(Path(a.writes).read_text(encoding='utf-8'))
review=json.loads(Path(a.review).read_text(encoding='utf-8'))
world_markers=json.loads(Path(a.world_markers).read_text(encoding='utf-8'))
def normal(text):return ''.join(c for c in text.lower() if c.isalnum())
def gameplay(fid):return fields[str(fid)]['event'].split('_')[1]!='ENDING'
def chapter(milestone):return next((c for c in review['chapters'] if c[0]<milestone<=c[1]),None)
areas={
 'ALEX1':'Alexandria','ALEX2':'Alexandria','ALEX3':'Alexandria','ALEX4':'Alexandria','ALEX5':'Alexandria',
 'DOWNSHIP':'the crashed Prima Vista','EFOREST1':'Evil Forest','ICE':'Ice Cavern','DALI':'Dali','CARGO':'the cargo ship',
 'LIND1':'Lindblum','LIND2':'Lindblum','LIND3':'Lindblum','GIZ':"Gizamaluke's Grotto",'BURMECIA':'Burmecia',
 'GATE':'South Gate','TOT':"Tot's residence",'TRENO1':'Treno','TRENO2':'Treno','GARGAN':'Gargan Roo',
 'CLEYRA1':"Cleyra's Trunk",'CLEYRA2':'Cleyra','CLEYRA3':'Cleyra','ROSE':'the Red Rose','PINA':'Pinnacle Rocks',
 'FOSSIL':'Fossil Roo','PATA':'Conde Petie','MAGE':'Black Mage Village','MAGE2':'Black Mage Village',
 'SARI1':'Madain Sari','SARI2':'Madain Sari','EVA1':'Iifa Tree','EVA2':'Iifa Tree','DAG':'Daguerreo',
 'SUNA':'Desert Palace','HILDA3':'Hilda Garde III','OEIL':'Oeilvert','EST':'Esto Gaza','GULUGU':'Mount Gulug',
 'IPSEN':"Ipsen's Castle",'SHRINE':'the shrines','TERA':'Terra','BAL':'Bran Bal','PANDEMO':'Pandemonium',
 'INV':'the Invincible','LAST':'Memoria','ENDING':'the ending', 'CHOCO':"Chocobo's Forest",'KUINA':"Qu's Marsh",'KUWAN':"Quan's Dwelling",'MOGNET':'Mognet Central'}

stage_fields={}
direct={}
for w in writes:
 if not gameplay(w['field']) or not 0<w['value']<=12000: continue
 c=chapter(w['value'])
 if c is not None and w['value'] not in review['excludeMilestones'] and w['event'].split('_')[1] in c[3]:stage_fields.setdefault(w['value'],set()).add(w['field'])
 direct.setdefault((w['field'],str(w['entry']),str(w['tag'])),set()).add(w['value'])

transit=set()
compiled_fields=[]
unresolved=[]
for fid,f in sorted(fields.items(),key=lambda x:int(x[0])):
 field=int(fid)
 if not gameplay(field):continue
 entries=f['entries']; functions={(sid,tag):code for sid,e in entries.items() for tag,code in e['functions'].items()}
 calls={}
 for key,code in functions.items():
  calls[key]=set()
  for i in code:
   v=i['args']
   if i['op']=='MAPJUMP' and v and isinstance(v[0],int) and str(v[0]) in fields and gameplay(v[0]):transit.add((field,v[0]))
   if i['op'] in ('REQ','REQSW','REQEW') and len(v)>=3 and isinstance(v[1],int) and isinstance(v[2],int):
    sid=key[0] if v[1]==255 else str(v[1])
    if (sid,str(v[2])) in functions:calls[key].add((sid,str(v[2])))
 def reachable(start):
  pending=[start]; seen=set(); result=set()
  while pending:
   key=pending.pop()
   if key in seen:continue
   seen.add(key)
   if len(seen)>1024: raise ValueError('Unbounded static request graph')
   result.update(direct.get((field,key[0],key[1]),()))
   pending.extend(calls.get(key,()))
  return result
 def destinations_from(start):
  pending=[start];seen=set();result=set()
  while pending:
   key=pending.pop()
   if key in seen:continue
   seen.add(key)
   for i in functions.get(key,()):
    if i['op'] in ('MAPJUMP','WMAPJUMP') and i['args'] and isinstance(i['args'][0],int):result.add(i['args'][0])
   pending.extend(calls.get(key,()))
  return result
 annotations=[]
 for sid,e in entries.items():
  code=[i for tag in ('2','3') for i in e['functions'].get(tag,())]
  destinations=sorted(set().union(*(destinations_from((sid,tag)) for tag in ('2','3'))))
  for dest in destinations:
   if str(dest) in fields and gameplay(dest):transit.add((field,dest))
  milestones=set()
  for tag in ('2','3'):
   if (sid,tag) in functions:milestones.update(reachable((sid,tag)))
  pickup=any(i['op'] in ('ITEMADD','KEYITEMADD','ITEM','ADDITEM') for i in code)
  # Retain all hashes so a requested helper cannot silently bypass provenance.
  # The milestone/destination arrays are analysis hints, never runtime proof.
  annotations.append(dict(sid=int(sid),hash=e['sha256'],milestones=sorted(milestones),destinations=destinations,pickup=pickup))
 for w in writes:
  if w['field']==field and str(w['tag']) not in ('2','3'):
   unresolved.append(dict(field=field,entry=w['entry'],tag=w['tag'],milestone=w['value'],classification='automatic or indirect; position requires ordinary presented target'))
 compiled_fields.append(dict(field=field,scene=f['scene'],event=f['event'],hash=f['hash'],entries=annotations))

stages=[]
for milestone,goals in sorted(stage_fields.items()):
 goals=set(review['fieldOverrides'].get(str(milestone),goals))
 if not goals or any(str(fid) not in fields or not gameplay(fid) for fid in goals):raise ValueError('Invalid reviewed story checkpoint')
 names=sorted({'Conde Petie Mountain Path' if fields[str(fid)]['event'].startswith('EVT_PATA_M_') else areas.get(fields[str(fid)]['event'].split('_')[1],'the next story scene') for fid in goals})
 if goals.issubset({2855,2856}):names=['Blue Narciss']
 vehicle='Invincible' if milestone>=11100 else 'Hilda Garde III' if milestone>10400 else 'Blue Narciss' if milestone>=9400 else ''
 world=['Memoria','Iifa Tree'] if names==['Memoria'] else ['Cleyra'] if names==["Cleyra's Trunk"] else names
 world=review['worldOverrides'].get(str(milestone),world)
 marker_ids=[]
 for place in world:
  marker_ids.extend(m['id'] for m in world_markers['markers'] if normal(m['name'])==normal(place))
 marker_ids=review['worldMarkerOverrides'].get(str(milestone),marker_ids)
 departures=[2850,2854] if milestone in (10620,10640,10660,10670,10830) else []
 stages.append(dict(milestone=milestone,fields=sorted(goals),goal=' or '.join(names),instruction=review['instructions'].get(str(milestone),''),worldPlaces=world,worldLocations=marker_ids,departureFields=departures,vehicle=vehicle,chapter=chapter(milestone)[2]))
listeners=[]
for b in review['listenerBindings']:
 entries=fields[str(b['field'])]['entries']
 listeners.append(dict(b,hash=entries[str(b['sid'])]['sha256'],playerHash=entries[str(b['playerSid'])]['sha256']))
result=dict(schema=1,source='Installed FFIX event scripts; reviewed main-story chapter membership; original navigation wording',inventorySHA256=hashlib.sha256(inventory).hexdigest(),reviewSHA256=hashlib.sha256(Path(a.review).read_bytes()).hexdigest(),worldTextSHA256=world_markers['textSHA256'],guide=review['guide'],stages=stages,transit=[dict(source=x,destination=y) for x,y in sorted(transit)],fields=compiled_fields,listeners=listeners)
Path(a.output).parent.mkdir(parents=True,exist_ok=True)
Path(a.output).write_text(json.dumps(result,separators=(',',':')),encoding='utf-8')
ledger=dict(fields=len(compiled_fields),stages=len(stages),chapters=len(review['chapters']),transit=len(transit),entryHashes=sum(len(f['entries']) for f in compiled_fields),automaticOrIndirect=unresolved,unknownAreas=sorted({f['event'].split('_')[1] for f in compiled_fields if f['event'].split('_')[1] not in areas}),policy='Counter floors are scene checkpoints only. Runtime local proof follows current native guards. No hidden reward values or unobserved positions; ordinary presentation and native geometry required')
Path(a.ledger).write_text(json.dumps(ledger,indent=2),encoding='utf-8')
print('PASS native Story metadata:',ledger['fields'],'fields;',ledger['stages'],'scene checkpoints;',ledger['chapters'],'reviewed chapters;',ledger['transit'],'potential transitions;',ledger['entryHashes'],'entry hashes; unknown chapter names=',ledger['unknownAreas'])
