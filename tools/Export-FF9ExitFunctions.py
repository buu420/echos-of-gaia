"""Export native exit functions and possible destinations for exit naming (read-only).

For every field region (entry type 1) whose touch handler (tag 2), or an entry it requests, reaches a
MAPJUMP/WMAPJUMP, this records:
  * every destination any branch can reach (the static upper bound; the runtime still proves the
    current one per approach with FieldExitPaths);
  * whether a branch is decided by a shown choice (sysvar 9, ETb.GetChoose) and the native prompt
    texts of the path (installed English field text, [STRT] entry order as FF9TextTool.ExtractSentense);
  * a reviewed function label for choice exits, assigned only from the native prompt wording
    (air cab, trolley and lift menus, airship boarding, inn rest). A choice exit without a matching prompt
    is reported as unreviewed rather than named.
Outputs Core/FieldExitFunctions.Generated.cs and docs/accessibility/field-exit-functions.json.
"""
import argparse, hashlib, json, re, sys
from pathlib import Path
from story_native_graph import NativeEventGraph

ROOT = Path(__file__).resolve().parents[1]
p = argparse.ArgumentParser()
p.add_argument('--inventory', required=True)
p.add_argument('--game', default=r'D:\SteamLibrary\steamapps\common\FINAL FANTASY IX')
p.add_argument('--unitypy', help='optional directory containing the UnityPy package')
p.add_argument('--output', default=str(ROOT / 'Assembly-CSharp/Memoria/Accessibility/Navigation/Core/FieldExitFunctions.Generated.cs'))
p.add_argument('--ledger', default=str(ROOT / 'docs/accessibility/field-exit-functions.json'))
a = p.parse_args()
if a.unitypy:
    sys.path.insert(0, a.unitypy)
import UnityPy

inventory = Path(a.inventory).read_bytes()
fields = json.loads(inventory)
utils = (ROOT / 'Assembly-CSharp/Global/Event/Engine/EventEngineUtils.cs').read_text(encoding='utf-8')
block = utils[utils.index('eventIDToMESID'):]
block = block[:block.index('};')]
zone_of = {int(x): int(y) for x, y in re.findall(r'\{\s*(\d+)\s*,\s*(\d+)\s*\}', block)}

# Resolve the actual English Resources path, then follow FF9TextTool's TXID
# and STRT message indexing. The same numeric TextAsset name exists in many languages.
data_dir=Path(a.game)/'x64/FF9_Data'
index={}
for obj in UnityPy.load(str(data_dir/'mainData')).objects:
    if obj.type.name!='ResourceManager':continue
    for key,pointer in obj.read_typetree()['m_Container']:
        match=re.fullmatch(r'embeddedasset/text/us/field/(\d+)\.mes',key.lower())
        if match:index[pointer['m_PathID']]=int(match.group(1))
env = UnityPy.load(str(data_dir/'resources.assets'))
english = {}
for obj in env.objects:
    if obj.type.name!='TextAsset' or obj.path_id not in index:continue
    value = obj.parse_as_object()
    data = value.m_Script
    raw = data if isinstance(data, str) else bytes(data).decode('utf-8', 'replace')
    messages={};ident=0
    for part in re.split(r'(\[TXID=\d+\])',raw):
        if part.startswith('[TXID=') and part.endswith(']'):
            ident=int(part[6:-1]);continue
        for sentence in part.split('[STRT=')[1:]:
            messages[ident]='[STRT='+sentence;ident+=1
    english[index[obj.path_id]]=messages


def text(zone, index):
    message=english.get(zone,{}).get(index)
    return re.sub(r'\s+', ' ', re.sub(r'\[[^\]]*\]', ' ', message)).strip() if message else None


def decides_choice(code, at, item):
    nxt = at.get(item['next'])
    if item['op'] != 'EXPR' or nxt is None or nxt['op'] not in ('rsv02', 'rsv03', 'rsv06', 'rsv0b', 'rsv0d'):
        return False
    return any(isinstance(arg, dict) and any(op == 122 and h[:2] == '09' for op, h in arg.get('expr', [])) for arg in item['args'])


def walk(graph, node, seen, out):
    if node in seen or len(seen) > 256:
        return
    seen.add(node)
    code = graph.functions.get(node,())
    at = {i['off']: i for i in code}
    for item in code:
        op = item['op']
        if op in ('MAPJUMP', 'WMAPJUMP') and item['args'] and isinstance(item['args'][0], int):
            out['destinations'].add(item['args'][0])
        if decides_choice(code, at, item):
            out['choice'] = True
        if op in ('MES', 'MESN', 'MESA', 'MESAN', 'MESB') and item['args'] and isinstance(item['args'][-1], int):
            out['texts'].append(item['args'][-1])
    for edge in graph.calls(node):
        walk(graph,edge.target,seen,out)


RULES = [
    (lambda t: 'Air Cab' in t and 'Fly to' in t, 'Air cab'),
    (lambda t: 'Trolley' in t and 'Ride to' in t, 'Castle trolley'),
    (lambda t: 'Lindblum Castle Lift' in t and 'ride' in t, 'Castle lift'),
    (lambda t: 'Board the airship' in t, 'Airship boarding'),
    (lambda t: re.search(r'\bRest\b', t) and 'Not yet' in t, 'Inn, asks whether to rest'),
]
# Region-specific native choices whose wording identifies their ordinary
# function. A walk-in cutscene may move the party, but isn't a doorway.
NAMED_CHOICE = {
    (1606,24):(1602,'Momatose','Rest with Momatose'),
    (1606,25):(1602,'Momatose','Rest with Momatose'),
    (1903,15):(2054,'Card Game Usher','Card tournament entrance'),
    (2714,17):(2715,'Danger ahead','Warp maze exit'),
    (2955,24):(2954,'Fat Chocobo',"Fat Chocobo's departure prompt"),
    (756,5):(752,'balcony','Balcony passage'),
    (757,6):(752,'balcony','Balcony passage'),
    (403,11):(405,'smoke','Underground storage passage'),
}
SCENE_TRANSITIONS = {(2161,6):('68c53400d4de025e36cf86dfe4682ada49e90b1cabbb7e98c81a0ea6df0bd324',2162)}
# Confirm regions that operate an ordinary route (lift, ladder, portal, ship
# departure). Story conversations that start a new scene are deliberately not
# admitted here. Each row still needs its exact live script, first Talk region,
# presented surface, walkmesh approach and proven current outcome.
CONFIRM_ROUTES = {
    (603,3):{601}, (706,13):'world', (914,8):{950}, (950,6):{914},
    (1017,5):{1050}, (1018,4):{1100}, (1050,3):{1017}, (1100,3):{1018},
    (1652,14):{1751}, (1751,5):{1652,1759}, (1755,12):{1750}, (1759,9):{1751},
    (1953,7):'world', (2151,3):{2153}, (2153,3):{2151}, (2173,8):{2105,2855,*range(9000,9013)},
    (2207,4):{2211}, (2207,5):{2222}, (2211,11):{2207}, (2222,4):{2207,2211},
    (2255,2):{2260}, (2350,23):{2351},
    **{(2504,s):{2505} for s in (2,3,10,11)}, (2505,2):{2504},
    **{(2506,s):{2507} for s in (2,3,4,6)}, (2507,6):{2506}, (2507,7):{2506}, (2508,5):{2502},
    (2605,10):{2606}, (2606,3):{2605}, (2651,8):{2652}, (2652,8):{2651},
    (2702,20):{2703}, (2703,8):{2704},
    (2712,16):{2713}, **{(2713,s):{2714} for s in (4,5,6)}, (2713,7):{2712},
    **{(2714,s):{2713} for s in (14,15,16)},
    (2750,1):'world', (2751,2):'world', (2901,7):'world',
    (2917,2):{2918}, (2918,5):{2917}, (2926,7):{2925},
    (2950,19):{2950,2951,2952}, (2952,22):'world', (2954,21):{2955}, (2955,16):{2954},
}
CONFIRM_MENU_NAMES = {(2173,8):'Harbor boarding', (2950,19):'Chocobo excavation choice'}
rows = []
confirm_rows = []
for fk in sorted(fields, key=int):
    graph=NativeEventGraph(fields[fk]['entries'])
    for sid, entry in fields[fk]['entries'].items():
        if entry['type'] != 1 or '2' not in entry['functions']:
            continue
        out = {'destinations': set(), 'choice': False, 'texts': []}
        source=(int(fk),int(sid))
        seen=set()
        for uid in graph.instances.get(source[1],{-1}):
            walk(graph,(source[1],uid,2),seen,out)
        if source in CONFIRM_ROUTES:
            confirm={'destinations':set(), 'choice':False, 'texts':[]}
            confirm_seen=set()
            for uid in graph.instances.get(source[1],set()):
                walk(graph,(source[1],uid,3),confirm_seen,confirm)
            expected=CONFIRM_ROUTES[source]
            if expected=='world':expected=set(range(9000,9013))
            if confirm['destinations']!=expected or not any(i['op']=='FICON' for i in entry['functions']['2']):
                raise ValueError(f'Confirm route identity mismatch: {source}: {confirm["destinations"]}')
            confirm_rows.append({'field':source[0], 'sid':source[1], 'hash':entry['sha256'],
                                 'destinations':sorted(expected), 'choice':confirm['choice'], 'function':CONFIRM_MENU_NAMES.get(source)})
        if not out['destinations']:continue
        scene=SCENE_TRANSITIONS.get(source)
        if scene and entry['sha256']==scene[0] and out['destinations']=={scene[1]}:
            continue
        zone = zone_of.get(int(fk))
        prompts = [(i, text(zone, i)) for i in dict.fromkeys(out['texts'])]
        label = None
        if out['choice']:
            named=NAMED_CHOICE.get(source)
            if named and out['destinations']=={named[0]} and any(t and named[1] in t for _,t in prompts):
                label=named[2]
            for test, name in RULES:
                if label is None and any(t and test(t) for _, t in prompts):
                    label = name
                    break
        rows.append({'field': int(fk), 'sid': int(sid), 'hash': entry['sha256'], 'destinations': sorted(out['destinations']),
                     'choice': out['choice'], 'function': label,
                     'prompts': [{'id': i, 'text': t[:160] if t else None} for i, t in prompts if out['choice']]})

lines = ['// <auto-generated> tools/Export-FF9ExitFunctions.py from the installed field scripts and text. Do not edit.',
         'using System.Collections.Generic;',
         'namespace Memoria.Accessibility.Navigation',
         '{',
         '    public static partial class FieldExitFunctions',
         '    {',
         '        // "field:sid:script" -> every destination any native branch of the exit can reach.',
         '        private static readonly Dictionary<string,int[]> _possible=new Dictionary<string,int[]>{']
for r in rows:
    lines.append('            {"%d:%d:%s",new[]{%s}},' % (r['field'], r['sid'], r['hash'], ','.join(str(d) for d in r['destinations'])))
lines += ['        };', '        // Reviewed function of a choice exit, from its native prompt wording.',
          '        private static readonly Dictionary<string,string> _functions=new Dictionary<string,string>{']
for r in rows:
    if r['function']:
        lines.append('            {"%d:%d:%s","%s"},' % (r['field'], r['sid'], r['hash'], r['function']))
lines += ['        };', '        private static readonly Dictionary<string,int[]> _confirmPossible=new Dictionary<string,int[]>{']
if len(confirm_rows)!=len(CONFIRM_ROUTES):raise ValueError('Missing native Confirm region')
for r in confirm_rows:
    lines.append('            {"%d:%d:%s",new[]{%s}},' % (r['field'],r['sid'],r['hash'],','.join(str(d) for d in r['destinations'])))
lines += ['        };','        private static readonly Dictionary<string,string> _confirmFunctions=new Dictionary<string,string>{']
for r in confirm_rows:
    if r['function']:
        lines.append('            {"%d:%d:%s","%s"},' % (r['field'],r['sid'],r['hash'],r['function']))
lines += ['        };', '    }', '}', '']
Path(a.output).write_text('\n'.join(lines), encoding='utf-8')
choice = [r for r in rows if r['choice']]
ledger = {'schema': 1, 'inventorySHA256': hashlib.sha256(inventory).hexdigest(), 'exits': len(rows),
          'singlePossibleDestination': sum(1 for r in rows if len(r['destinations']) == 1),
          'choiceExits': len(choice), 'reviewedChoiceExits': sum(1 for r in choice if r['function']),
          'unreviewedChoiceExits': [{'field': r['field'], 'sid': r['sid'], 'prompts': r['prompts']} for r in choice if not r['function']],
          'rows': rows,'confirmExits':len(confirm_rows),'confirmRows':confirm_rows}
Path(a.ledger).write_text(json.dumps(ledger, indent=1, ensure_ascii=False), encoding='utf-8')
print('walk exits',len(rows),'single',ledger['singlePossibleDestination'],'choice',len(choice),'reviewed',ledger['reviewedChoiceExits'],'Confirm exits',len(confirm_rows))
for r in ledger['unreviewedChoiceExits']:
    print('UNREVIEWED', r['field'], r['sid'], [(x['id'], (x['text'] or '')[:70]) for x in r['prompts'] if x['text'] and ('?' in x['text'] or 'Yes' in x['text'])][:3])
