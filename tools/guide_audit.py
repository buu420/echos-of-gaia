"""Offline guide/native census join. Never imported or loaded by the game.

Generate using the retained, independently researched inputs; --check verifies
the public artifact without requiring copyrighted game assets or private paths.
The latter checks consistency and source drift, not a fresh game extraction.
"""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
OUT = Path('docs/accessibility/guide-coverage.json')
BUNDLE = '1404f0244bf069acb0cab059684c9603589ba9ae070222447b644f9150ca9690'
BASELINE = '41f8e68a9f7f182d49ffb5aada52f79d3f71924d'
TOPICS = {
    'tetra-master', 'jump-rope-mini-game', 'mognet', 'friendly160creatures',
    'ragtime-mouse', 'frog-catching', 'chocobo-hot-and-cold', 'morrids-coffees',
    'kupo-nuts', 'stiltzkin-shopping', 'treno-auction-house', 'stellazzio',
    'weapon-shop-monsters', 'conde-petie-colored-stones', 'oglop-trading',
    'hippaul-racing', 'double-or-nothing', 'daguerreo', 'magical-fingertip',
    'nero-brothers', 'the-master-synthesist', 'the-ultimate-monster',
    'storyline-mini-games',
}
SOURCE_FILES = [
    'docs/accessibility/story-navigation.json',
    'docs/accessibility/story-prerequisites.json',
    'docs/accessibility/reviewed-field-labels.json',
    'docs/accessibility/field-interaction-evidence.json',
    'docs/accessibility/opening-ticket-booth.json',
    'Assembly-CSharp/Global/Quad/Mist/QuadMistAccessibility.cs',
    'Assembly-CSharp/Global/Button/ButtonInputHandler.cs',
    'Assembly-CSharp/Global/Event/EventCollision.cs',
    'Assembly-CSharp/Global/Dialog/Dialog.cs',
    'Assembly-CSharp/Global/Field/Map/Actor/FieldMapActorController.cs',
    'tests/FF9Access.Tests/Fixtures/opening-street-scripts.json',
    'tests/FF9Access.Tests/Fixtures/opening-booth-geometry.json',
    'tests/FF9Access.Tests/Fixtures/story-region-surfaces.json',
] + ['Assembly-CSharp/Memoria/Accessibility/Navigation/' + path for path in [
    'Core/StoryPrerequisiteCatalog.cs', 'Core/StoryPrerequisiteData.cs',
    'Core/OpeningTicketBoothEvidence.cs', 'Core/ExitScriptAnalyzer.cs',
    'Core/FieldPanoramaPresentation.cs', 'Core/NavigationAnnouncements.cs',
    'Core/FieldPanoramaEvidence.cs',
    'Core/DisplayedChoice.cs', 'Core/FieldAreaNames.cs',
    'Core/FieldScenePresentation.cs', 'Core/FieldStoryRegionApproaches.cs',
    'Core/NavigationTargets.cs', 'Core/FieldTargetCatalog.cs', 'Core/FieldTargetApproaches.cs',
    'Core/OpeningRooftopEvidence.cs', 'Core/OpeningPositionApproaches.cs',
    'Core/FieldInputOrientation.cs', 'Core/ManualRouteGuidance.cs',
    'Core/GuidanceRouteValidation.cs', 'Core/NavigationModels.cs',
    'Core/RouteGuidanceProgress.cs', 'Core/RouteInstruction.cs',
    'Core/RoutePlan.cs', 'Core/FieldRoutePlanner.cs', 'Core/WorldRoutePlanner.cs',
    'Native/NativeNavigationContext.cs',
    'Native/NativeFieldAreaNames.cs', 'Native/NativeFieldStoryRegions.cs',
    'Native/NativeOpeningPositionTargets.cs',
    'Core/StoryTargetCatalog.cs', 'Core/NavigationCoordinator.cs',
    'Core/FieldVisibleLabels.cs', 'Core/FieldTouchConfirmation.cs',
    'Core/NumericInputReader.cs', 'Native/NativeStoryPrerequisites.cs',
    'Native/NativeFieldTouchReadiness.cs', 'Native/NativeNumericDialogs.cs',
    'Native/NativeFieldTargets.cs', 'Native/NativeStoryTargets.cs',
    'Native/NativeInteractionProbe.cs', 'Native/NavigationRuntime.cs',
    'Native/NavigationLifecycleBridge.cs',
    'Native/NativeOpeningTicketBooth.cs', 'Native/NativeTicketBoothPresentation.cs',
]]
PREREQUISITE_CHAPTERS = {
    1: ('opening-cross-zone', 'Opening travel uses actual native street exits toward the square. The rendered ticket sign binds its native front hotspot; room104 is the dialogue/completion scene. Unknown routes identify the next area without creating a walking target.'),
    15: ('pinnacle-fragments', 'Current fragment bits precede the final conversation. Only an already presented, interactive fragment can supply a walking target. Unshown fragments have no inferred position.'),
    17: ('fossil-route', 'Current switch bits and the verified current floor component choose the route step. Boarding steps without an existing interactive target remain manual information.'),
    34: ('gulug-lever', 'The presented lever remains the objective until the native completion flag changes. Pull direction and Confirm stay under player control.'),
}
SHA = re.compile(r'^[a-f0-9]{64}$')


def load(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))


def digest(path):
    # Git checkouts may use CRLF; source identity uses canonical LF text.
    data = Path(path).read_bytes().replace(b'\r\n', b'\n')
    return hashlib.sha256(data).hexdigest()


def identity(rows):
    data = json.dumps(rows, separators=(',', ':'), sort_keys=True).encode('utf-8')
    return hashlib.sha256(data).hexdigest()


def require(condition, message):
    if not condition:
        raise ValueError(message)


def validate(report, root=ROOT):
    require(report['schema'] == 1 and report['baselineSourceHead'] == BASELINE, 'Unrecognized audit baseline')
    require(report['eventBundleSha256'] == BUNDLE, 'Different game event bundle')
    require(report['runtimeLoadsGuide'] is False, 'Guide facts must remain offline')
    fields = report['fields']
    by_field = {row['field']: row for row in fields}
    require(len(fields) == len(by_field) == 818, 'All 818 fields must remain represented')
    require(identity([[f['field'], f['scriptSha256'], f['entries']] for f in fields]) == report['nativeFieldIdentity'], 'Native census identity changed')
    for row in fields:
        require(SHA.fullmatch(row['scriptSha256']), 'Unbound native field')
        require(row['entries'] == row['actors'] + row['regions'] + row['otherEntries'], 'Entry accounting mismatch')
        require(row['staticAudit'] == 'decoded-all-entries' and row['liveValidated'] is False, 'Static extraction is not live proof')
        require(row['semanticLabelStatus'] == 'partial' and row['presentationStatus'] == 'native-gates-live-unverified', 'Do not imply all entities are semantically or visibly verified')
    require(sum(f['entries'] for f in fields) == 16067, 'Native entry census incomplete')
    chapters = report['storyChapters']
    require(len(chapters) == 44 and {c['number'] for c in chapters} == set(range(1, 45)), 'All 44 main guide chapters required')
    stages = load(root / 'docs/accessibility/story-navigation.json')['stages']
    checkpoints = {s['milestone'] for s in stages}
    ticket = next(s for s in stages if s['milestone'] == 1150)
    require(ticket.get('approachFields') == [103] and ticket['fields'] == [104], 'Ticket physical approach and dialogue completion must stay distinct')
    require({m for c in chapters for m in c['checkpoints']} == checkpoints and len(checkpoints) == 364, 'All 364 catalog checkpoints required')
    for c in chapters:
        require(c['liveValidated'] is False and c['fullyAccessibleProven'] is False and c['remainingGaps'], 'Story must retain proof limits')
        require(c['guideUrl'].startswith(report['guide']['root'] + '/'), 'Chapter has no exact guide page')
        require(set(c['nativeFields']) <= set(by_field), 'Chapter uses foreign native fields')
    topics = report['optionalTopics']
    require(len(topics) == 23 and {t['id'] for t in topics} == TOPICS, 'All 23 optional guide topics required')
    require(sum(len(t['nativeBindings']) for t in topics) == 87, 'Optional binding evidence lost')
    for t in topics:
        require(t['fullyAccessibleProven'] is False and t['liveValidated'] is False and t['remainingGaps'], 'Script presence cannot establish complete accessibility')
        require(t['guideUrl'].startswith(report['guide']['root'] + '/'), 'Topic has no exact guide page')
        for b in t['nativeBindings']:
            require(b['field'] in by_field and b['fieldSha256'] == by_field[b['field']]['scriptSha256'] and SHA.fullmatch(b['entrySha256']), 'Optional binding has different native provenance')
    require(len(next(t for t in topics if t['id'] == 'chocobo-hot-and-cold')['subactivities']) == 6, 'Chocobo quest parts omitted')
    require(len(next(t for t in topics if t['id'] == 'storyline-mini-games')['subactivities']) == 7, 'Story minigames omitted')
    require({s['path'] for s in report['sourceArtifacts']} == set(SOURCE_FILES), 'Required runtime/source evidence omitted')
    for source in report['sourceArtifacts']:
        require(digest(root / source['path']) == source['sha256'], 'Source changed; refresh audit: ' + source['path'])
    labels = load(root / 'docs/accessibility/reviewed-field-labels.json')['entries']
    require(report['totals']['reviewedAppearanceModels'] == len(labels) == 47, 'Appearance catalog count mismatch')
    require(report['totals']['reviewedInteractionBindings'] == len(load(root / 'docs/accessibility/field-interaction-evidence.json')['bindings']) == 18, 'Actor/region binding count mismatch')
    native = load(root / 'docs/accessibility/story-navigation.json')['fields']
    native_fields = {f['field']: f for f in native}
    captured = load(root / 'tests/FF9Access.Tests/Fixtures/opening-street-scripts.json')
    require(captured['EventBundleSha256'] == BUNDLE and len(captured['Functions']) == 17, 'Opening native function inventory changed')
    for row in captured['Functions']:
        field = native_fields[row['Field']]
        entries = {e['sid']: e for e in field['entries']}
        require(row['FieldSha256'] == field['hash'] and row['EntrySha256'] == entries[row['Sid']]['hash'], 'Opening fixture native identity drift')
    booth = load(root / 'docs/accessibility/opening-ticket-booth.json')
    geometry = load(root / 'tests/FF9Access.Tests/Fixtures/opening-booth-geometry.json')
    require(booth['source']['fieldSha256'] == native_fields[103]['hash'] and booth['source']['eventBundleSha256'] == BUNDLE, 'Booth has different native field evidence')
    require(booth['source']['walkmeshSha256'] == geometry['WalkmeshSha256'] and booth['region'] == geometry['Booth'] and len(geometry['Triangles']) == 151, 'Booth art/region/geometry evidence differs')


def build(research, root=ROOT):
    story = load(research / 'story/main-story-checklist.json')
    optional = load(research / 'optional/optional-checklist.json')
    entities = load(research / 'entities/entities-census.json')
    cache = load(research / 'entities/cache-validation.json')
    require(entities['eventBundleSha256'] == cache['eventBundleSha256'] == BUNDLE and not cache['decodeErrors'], 'Unverified native census')
    require(len(cache['fields']) == 818 and all(f['baseline818HashMatch'] for f in cache['fields']), 'Retained full native census drift')
    require(all(f['cache817AllEntryHashesMatch'] for f in cache['fields'] if f['field'] != 70)
            and next(f for f in cache['fields'] if f['field'] == 70)['retainedCacheEntryCount'] == 0,
            'Retained 817-field Story cache drift (field70 was absent from that older cache)')
    labels = load(root / 'docs/accessibility/reviewed-field-labels.json')['entries']
    models = {l['model'] for l in labels}
    conditional = {l['model'] for l in labels if l.get('visibilityRequirement')}
    bindings = load(root / 'docs/accessibility/field-interaction-evidence.json')['bindings']
    actor_regions = {(b['field'], b['region']) for b in bindings}
    fields = []
    pair_counts = Counter()
    for f in entities['fields']:
        pairs = [(a, m) for a in f['actors'] for m in a['models']]
        counts = Counter()
        for actor, model in pairs:
            state = 'conditional' if model in conditional else 'reviewed' if model in models else 'generic'
            counts[state] += 1
            pair_counts[state] += 1
            if not actor['possiblePlayerEntry']:
                pair_counts['nonplayer-' + state] += 1
        policies = Counter('reviewed-opening-ticket-booth-art-binding' if (f['field'], r['entry']) == (103, 28) else 'reviewed-actor-region-binding' if (f['field'], r['entry']) in actor_regions else r['runtimePresentationPolicy'] for r in f['regions'])
        icon_actionable = sum(r['runtimePresentationPolicy'] == 'current-native-icon-only' and (f['field'], r['entry']) not in actor_regions and (f['field'], r['entry']) != (103, 28) and (r['hasTalkHandler'] or r['hasPushHandler']) for r in f['regions'])
        fields.append(dict(field=f['field'], scriptSha256=f['scriptSha256'], entries=f['entryCount'], actors=len(f['actors']), regions=len(f['regions']), otherEntries=f['otherEntryCount'],
                           actorModelPairs=dict(counts), regionPolicies=dict(policies), iconOnlyActionableRegions=icon_actionable,
                           staticAudit='decoded-all-entries', semanticLabelStatus='partial', presentationStatus='native-gates-live-unverified', liveValidated=False))
    chapters = []
    for c in story['chapters']:
        change = PREREQUISITE_CHAPTERS.get(c['number'])
        gap = c['runtimeAndProofGap'] if not change else change[1] + ' Full scene transitions, puzzle inputs, controller and speech still need live validation.'
        chapters.append(dict(number=c['number'], disc=c['disc'], title=c['guideChapter'], guideUrl=c['sourceURL'],
                             expectedActions=c['expectedRequiredActions'], nativeFields=c['nativeFamilyFields'], checkpoints=c['catalogCheckpoints'],
                             staticStatus='guide-order-and-native-scenes-compared', runtimeCorrection=change[0] if change else None,
                             remainingGaps=[gap], fullyAccessibleProven=False, liveValidated=False))
    topics = []
    for t in optional['topics']:
        resolved = [g for g in t['gaps'] if re.match(r'B0[1-4]|B09', g)]
        gaps = [g for g in t['gaps'] if g not in resolved]
        topics.append(dict(id=t['id'], title=t['title'], guideUrl=t['sourceUrl'], checklist=t['interactionChecklist'], subactivities=t.get('subactivities', []),
                           nativeBindings=[dict(field=b['field'], fieldSha256=b['fieldSha256'], entry=b['entry'], entrySha256=b['entrySha256'], tags=b['tags']) for b in t['nativeStaticBindings']],
                           existingSupport=t['existingSupport'], correctedBaselineFindings=resolved, remainingGaps=gaps,
                           fullyAccessibleProven=False, liveValidated=False))
    return dict(schema=1, auditDate='2026-11-01', baselineSourceHead=BASELINE, eventBundleSha256=BUNDLE,
                guide=story['guide'], additionalGuide='https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/29957',
                runtimeLoadsGuide=False,
                proofBoundary='Static guide/native-source comparison. All 818 fields and all listed guide topics accounted for; no claim of all visible props, full semantic labeling, full accessibility or a completed live playthrough. Native field IDs below are developer evidence, never spoken labels.',
                retainedInputs=[dict(name=p, sha256=digest(research / p)) for p in ['story/main-story-checklist.json', 'optional/optional-checklist.json', 'entities/entities-census.json', 'entities/cache-validation.json']],
                sourceArtifacts=[dict(path=p, sha256=digest(root / p)) for p in SOURCE_FILES],
                nativeFieldIdentity=identity([[f['field'], f['scriptSha256'], f['entries']] for f in fields]),
                totals=dict(fields=len(fields), entries=sum(f['entries'] for f in fields), actors=sum(f['actors'] for f in fields), regions=sum(f['regions'] for f in fields),
                            storyChapters=len(chapters), storyCheckpoints=364, optionalTopics=len(topics), optionalBindings=sum(len(t['nativeBindings']) for t in topics),
                            reviewedAppearanceModels=len(labels), reviewedInteractionBindings=len(bindings), actorModelPairs=dict(pair_counts),
                            iconOnlyActionableRegions=sum(f['iconOnlyActionableRegions'] for f in fields)),
                remainingEntityGaps=[
                    'Most of the 345 constant actor models still lack individually reviewed appearance descriptions. Displayed native names may be learned; generic labels remain elsewhere.',
                    'Non-exit backdrop regions without a verified actor/art presentation binding require the current native icon. A raw script region is not proof of a visible prop.',
                    'Observed generic icon-only regions do not yet have retained offscreen art-visibility evidence. They may disappear when their current bubble ends.',
                    'Viewport anchor checks and projected exit surfaces still require live tests, including partly visible meshes, changing branches and all map entrances.',
                    'Random-encounter battle-only entities are outside this field census. Optional timed, visual and world-clue tasks retain the per-topic gaps below.',
                ], storyChapters=chapters, optionalTopics=topics, fields=fields)


def markdown(report):
    t = report['totals']
    lines = ['# Whole-game guide comparison', '',
        'This is an offline audit, not a complete accessibility or playthrough certification.', '',
        f"The current English event bundle has **{t['fields']} fields and {t['entries']:,} entries**. All were decoded and matched to retained native fingerprints. The comparison accounts for **44 main-guide chapters, 364 existing Story checkpoints and 23 optional topics** (87 native entry bindings). Every field and guide topic has an explicit result in `guide-coverage.json`.", '',
        "The main reference is [bover_87's guide, version 2.01](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/71891); [Andrew_Testa's guide](https://gamefaqs.gamespot.com/ps/197338-final-fantasy-ix/faqs/29957) is the user's additional reference. Short checklists here are original summaries. Guide information is never loaded by the runtime or used to disclose hidden NPC names, rewards or coordinates.", '',
        '## Corrections from this audit', '',
        '- Opening Story uses captured street transitions and names the next local area. The square is the physical approach to the ticket objective; the booth dialogue runs in a separate room. Its reviewed visible sign/hotspot is listed before bubble discovery. Informational steps explain the next area without starting a route to a remote objective.',
        '- Pinnacle Rocks now keeps fragment collection ahead of the final conversation. Only currently presented, interactive fragments can supply walking targets.',
        '- Fossil Roo uses the actual switch bits and current connected floor section. The route changes with reversible switches. Flower/boarding steps without a verified interactive target stay informative and manual.',
        '- Mount Gulug keeps the lever step until its native completion flag changes. The player still chooses the lever action.',
        '- Card speech uses the displayed face grades, includes placed-card defenses and reads initial hand/board focus. Triple Triad follows its displayed layout and hidden-arrow setting.',
        '- Auction bids/current price/funds and Daguerreo quantities read changes from completed, visible native text parsers. Original instructions and clocks remain intact. Only the verified English numeric layouts are supported.',
        '- Exact source UIDs are retained for field touch bubbles. Two additional native inn actor/region bindings and a narrowly verified touch-only confirmation state are recognized.',
        '- Six additional chest/frog appearances and a conditional long-coat body description bring the reviewed catalog to 47 models. A hidden body cannot acquire the coat label.', '',
        '## Remaining limits', '',
        'All rows retain `fullyAccessibleProven: false` and/or `liveValidated: false`. Script presence, guide order and passing fixtures do not establish that every NPC, prop, exit, puzzle or optional activity works in all live states. There are 345 constant actor models; many still use generic labels before an attributed native name appears.', '',
        f"{t['iconOnlyActionableRegions']} region entries have a Talk/Push handler and still require the current native icon rather than a verified backdrop/actor binding. This is a static candidate count, not a count of visible missing props. Generic icon-only observations also lack a durable offscreen art-visibility proof.", '',
        'Chocograph picture descriptions, world cracks/bubbles/tracks/shadows, jump-rope cadence, race progress, shuffle tracking, guard-facing/timed tasks, Blackjack and several puzzle sequences still need dedicated presentation evidence or live validation. Desert Palace and Pandemonium intermediate state-to-target mappings remain unresolved. The optional checklist lists each limitation; optional preparation and sidequests never become mandatory Story steps.', '',
        'This audit did not launch the game or synthesize gameplay/controller input. Full transitions, audible NVDA/controller delivery and complete main/optional playthroughs remain player tests.', '',
        '## Reproduce the checks', '',
        'Run `python tools/guide_audit.py --check` and `python tools/compile_story_prerequisites.py --check`. These validate the committed census accounting, all guide topics/checkpoints and current source fingerprints. They do not re-extract the installed game. A fresh extraction/research input refresh is required when the event-bundle identity changes.', '',
        '## Main-story chapters', '', '| Chapter | Comparison result |', '| --- | --- |']
    for c in report['storyChapters']:
        result = c['remainingGaps'][0].replace('|', '/')
        lines.append(f"| [{c['number']}. {c['title']}]({c['guideUrl']}) | {result} |")
    lines += ['', '## Optional topics', '', '| Topic | Remaining work |', '| --- | --- |']
    for topic in report['optionalTopics']:
        lines.append(f"| [{topic['title']}]({topic['guideUrl']}) | {' '.join(topic['remainingGaps']).replace('|', '/')} |")
    return '\n'.join(lines) + '\n'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--research-root', type=Path)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    if args.research_root:
        report = build(args.research_root)
        validate(report)
        data = json.dumps(report, indent=2, ensure_ascii=True) + '\n'
        if args.check:
            require((ROOT / OUT).read_text(encoding='utf-8') == data, 'Retained research differs from committed report')
        else:
            (ROOT / OUT).write_text(data, encoding='utf-8', newline='\n')
            (ROOT / OUT.with_suffix('.md')).write_text(markdown(report), encoding='utf-8', newline='\n')
    else:
        require(args.check, 'Use --research-root to generate, or --check to validate')
        report = load(ROOT / OUT)
        validate(report)
    require((ROOT / OUT.with_suffix('.md')).read_text(encoding='utf-8') == markdown(report), 'Markdown report differs from machine-readable audit')
    print('PASS guide audit: 818 fields, 44 chapters, 364 checkpoints, 23 optional topics/87 bindings; live proof remains open')


if __name__ == '__main__':
    main()
