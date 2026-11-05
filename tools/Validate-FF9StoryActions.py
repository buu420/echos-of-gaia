"""Check reviewed Story bindings using the installed decoded event inventory."""
import argparse
import hashlib
import json
from pathlib import Path
from story_action_validation import validate

parser = argparse.ArgumentParser()
parser.add_argument('--inventory', required=True)
docs=Path(__file__).resolve().parents[1] / 'docs/accessibility'
parser.add_argument('--chains', action='append', help='Reviewed action pack (repeatable)')
parser.add_argument('--loop-prompts', default=str(docs / 'field-loop-prompts.json'))
args = parser.parse_args()
raw = Path(args.inventory).read_bytes()
fields,identity=json.loads(raw),hashlib.sha256(raw).hexdigest()
loops=json.loads(Path(args.loop_prompts).read_text(encoding='utf-8-sig'))
packs=args.chains or [docs/'story-action-chains.json',docs/'story-palace-actions.json']
errors=[];actions=phases=0
for pack in packs:
    chains=json.loads(Path(pack).read_text(encoding='utf-8-sig'))
    errors.extend(validate(chains,fields,identity,loops))
    actions+=len(chains['steps'])+len(chains.get('loopActions',[]))
    phases+=len(chains.get('phases',[]))
for error in errors:
    print('FAIL', error)
if errors:
    raise SystemExit(1)
print('PASS reviewed Story action provenance:', actions, 'actions;', phases, 'phases; native state/route checks remain separate')
