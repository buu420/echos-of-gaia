"""Publish quantities and review status, without destinations, rewards or scripts."""
import csv
import json
from collections import Counter
from audit_paths import OUTPUT_ROOT, SOURCE_ROOT

audit = json.loads((OUTPUT_ROOT / 'field-navigation-audit.json').read_text(encoding='utf-8'))
exits = json.loads((SOURCE_ROOT / 'docs/accessibility/field-exit-evidence.json').read_text(encoding='utf-8'))
actors = json.loads((SOURCE_ROOT / 'docs/accessibility/field-interaction-evidence.json').read_text(encoding='utf-8'))
surfaces = {(int(r['sceneKey'].split(':')[1]), r['scriptId']) for r in exits['surfaceReviewed']}
excluded = {(int(r['sceneKey'].split(':')[1]), r['scriptId']): r['reason'] for r in exits['excludedReviewed']}
fields = []
for f in audit['fields']:
    nonplayer = [a for a in f['actors'] if not a['isPlayerEntry']]
    unresolved = []
    for x in f['exits']:
        if (f['field'], x['entry']) in surfaces or (f['field'], x['entry']) in excluded:
            continue
        if x['class'] in ('candidate-requested', 'candidate-nowalk', 'unsupported'):
            unresolved.append({'entry': x['entry'], 'reason': x.get('noEdgeReason') or x['class']})
    fields.append({
        'field': f['field'], 'scene': f['scene'], 'scriptSha256': f['scriptSha256'],
        'scriptEntries': f['entries'], 'decodeErrors': len(f['decodeErrors']),
        'geometryAvailable': f['geometry'] is not None,
        'peopleCandidates': sum(a['runtimeCategory'] == 'People' for a in nonplayer),
        'objectCandidates': sum(a['runtimeCategory'] == 'Objects' for a in nonplayer),
        'regionTransitions': len(f['exits']),
        'plainWalkOutCandidates': sum(x['trigger'] == 'touch' and x['plainWalkOut'] for x in f['exits']),
        'animatedDoorCandidates': f['classCounts'].get('animated-door', 0),
        'baselineOffscreenEdgeOmissions': f['classCounts'].get('candidate-offscreen-edge', 0),
        'baselineInteriorOmissions': f['classCounts'].get('candidate-interior', 0),
        'authoredSurfaceReviews': sum(k[0] == f['field'] for k in surfaces),
        'reviewedScriptedOrUnresolvedRegions': sum(k[0] == f['field'] for k in excluded),
        'reviewedTalkBindings': sum(b['field'] == f['field'] for b in actors['bindings']),
        'unresolvedTouchRegions': unresolved,
        'status': 'all script entries audited; live states unverified',
    })
report = {
    'schema': 1, 'scope': 'Every installed English field event asset; static native-script and geometry census',
    'eventBundleSha256': audit['p0data7Sha256'], 'fieldCount': len(fields),
    'geometryFieldCount': sum(f['geometryAvailable'] for f in fields),
    'decodeErrors': sum(f['decodeErrors'] for f in fields),
    'regionTransitionCount': sum(f['regionTransitions'] for f in fields),
    'baselineTransitionClasses': audit['classTotals'],
    'authoredSurfaceReviewCount': len(surfaces), 'reviewedTalkBindingCount': len(actors['bindings']),
    'remainingUnresolvedTouchRegions': sum(len(f['unresolvedTouchRegions']) for f in fields),
    'limitations': [
        'Candidate counts are script entries, not simultaneously visible or interactable targets.',
        'Runtime rendering, camera, permissions, guards and safe approaches decide current targets.',
        'Ordinary native-icon interactions stay manual; scripted jumps are not walking exits.',
        'This census does not exercise every story state, camera, controller or audible cue in gameplay.',
        'World navigation uses the existing native marker, transport and reachable-component readers.',
    ],
    'fields': fields,
}
(OUTPUT_ROOT / 'field-navigation-coverage.json').write_text(json.dumps(report, indent=2)+'\n', encoding='utf-8')
columns = list(fields[0])
with (OUTPUT_ROOT / 'field-navigation-coverage.csv').open('w', newline='', encoding='utf-8') as output:
    writer = csv.DictWriter(output, columns)
    writer.writeheader()
    for f in fields:
        writer.writerow({**f, 'unresolvedTouchRegions': '; '.join(str(x['entry'])+': '+x['reason'] for x in f['unresolvedTouchRegions'])})
print('coverage rows', len(fields), 'unresolved touch regions', report['remainingUnresolvedTouchRegions'])
