"""Build the public narration pack from reviewed text and private generation evidence.

Only generated PCM recordings and their public catalog leave the voice workspace.
Requires FFmpeg on PATH; no service credentials or network calls at build/runtime.
"""
import argparse
import array
import copy
import hashlib
import io
import json
import math
import re
import subprocess
import sys
import wave
from pathlib import Path

RATE = 24000


def digest(data): return hashlib.sha256(data).hexdigest()


def read_json(path): return json.loads(Path(path).read_text(encoding='utf-8-sig'))


def equivalent_words(expected, heard):
    # These recognizer spellings have identical spoken forms. Do not accept
    # synonyms, omissions or other substitutions as verified narration.
    def words(text):
        text = text.lower().replace("'", '').replace('\u2019', '')
        text = re.sub(r'final fantasy (?:ix|9)\b', 'final fantasy nine', text)
        return [{'blonde': 'blond', 'night': 'knight', 'canons': 'cannons'}.get(w, w) for w in re.findall(r'[a-z]+|\d+', text)]
    return words(expected) == words(heard)


def prepare_clip(cue, voice_dirs, overrides):
    cue_id, text = cue['id'], cue['text']
    if not re.fullmatch(r'[a-z0-9][a-z0-9-]*', cue_id) or not text.strip():
        raise ValueError('Invalid cue identity/text: ' + cue_id)
    selected = None
    for directory in voice_dirs:
        metadata = directory / (cue_id + '.json')
        recording = directory / (cue_id + '.wav')
        if metadata.is_file() and recording.is_file():
            meta = read_json(metadata)
            if meta.get('id') == cue_id and meta.get('text') == text:
                selected = meta, recording
                break
    if selected is None: raise ValueError('No matching generated recording: ' + cue_id)
    meta, recording = selected
    raw = recording.read_bytes()
    if len(raw) > 2097152 or meta.get('wave_sha256') != digest(raw):
        raise ValueError('Changed or oversized generated recording: ' + cue_id)
    checks = meta.get('checks', {})
    review = overrides.get(cue_id)
    if review and (review.get('wave_sha256') != digest(raw) or not review.get('reason')):
        raise ValueError('Stale or unexplained recording review: ' + cue_id)
    equivalent = (set(checks.get('failures', [])) <= {'words'} and
                  equivalent_words(text, checks.get('asr_text', '')))
    if (not checks.get('exact') or checks.get('failures')) and not equivalent and not review:
        raise ValueError('Recording word/audio check needs explicit hash-bound review: ' + cue_id)
    decoded = subprocess.run(['ffmpeg', '-v', 'error', '-nostdin', '-i', str(recording),
                              '-ac', '1', '-ar', str(RATE), '-f', 's16le', '-threads', '1', 'pipe:1'],
                             capture_output=True, check=True).stdout
    samples = array.array('h'); samples.frombytes(decoded)
    if sys.byteorder != 'little': samples.byteswap()
    if not samples: raise ValueError('Empty recording: ' + cue_id)
    # Remove outer silence only. Keep 100 ms before and 200 ms after the last
    # low-threshold (roughly -55 dBFS) 10 ms energy block; no phoneme stretching,
    # interior edits, tempo/pitch changes or voice metadata in the public pack.
    active = []
    for start in range(0, len(samples), 240):
        block = samples[start:start + 240]
        if math.sqrt(sum(float(x) * x for x in block) / len(block)) > 58:
            active.append(start)
    if not active: raise ValueError('Silent recording: ' + cue_id)
    first = max(0, active[0] - 2400)
    last = min(len(samples), active[-1] + 240 + 4800)
    kept = samples[first:last]
    if max(abs(x) for x in kept) >= 32767: raise ValueError('Clipped recording: ' + cue_id)
    if sys.byteorder != 'little': kept.byteswap()
    memory = io.BytesIO()
    with wave.open(memory, 'wb') as out:
        out.setparams((1, 2, RATE, 0, 'NONE', 'not compressed')); out.writeframes(kept.tobytes())
    data = memory.getvalue()
    if len(data) < 46 or len(data) > 2097152: raise ValueError('Invalid final clip size: ' + cue_id)
    row = {'id': cue_id, 'text': text, 'file': cue_id + '.wav', 'sha256': digest(data),
           'bytes': len(data), 'duration': len(kept) / RATE}
    qa = {'id': cue_id, 'source_sha256': digest(raw), 'packed_sha256': row['sha256'],
          'asr': checks.get('asr_text'), 'word_check_exact': bool(checks.get('exact')),
          'word_check_equivalent': equivalent,
          'review': review.get('reason') if review else None,
          'trimmed_lead_seconds': first / RATE, 'trimmed_tail_seconds': (len(samples) - last) / RATE}
    return row, data, qa


def check_timeline(film, clips):
    previous = 0
    if not re.fullmatch(r'FMV\d{3}[A-D]?', film['movie']) or not film['cues']:
        raise ValueError('Invalid or empty movie: ' + film['movie'])
    for cue in film['cues']:
        start, end = cue['start'], cue['end']
        if not all(math.isfinite(x) for x in (start, end, film['duration'])) or start < previous or end > film['duration'] + .00001:
            raise ValueError('Invalid/overlapping/outside movie window: ' + cue['id'])
        if clips[cue['id']]['duration'] + .05 > end - start:
            raise ValueError('Voice does not fit the observed scene: ' + cue['id'])
        previous = end


def check_bank_budgets(films, clips, field_banks=None):
    if len(clips) > 4096:
        raise ValueError('Public description pack exceeds 4096 clips')
    if any(type(row.get('bytes')) is not int or row['bytes'] < 46 or row['bytes'] > 2097152 for row in clips.values()):
        raise ValueError('Invalid description recording size')
    movie_ids = set()
    banks = []
    for film in films:
        ids = {cue['id'] for cue in film['cues']}
        movie_ids.update(ids)
        banks.append(ids)
    if field_banks is None: field_banks = []
    if not isinstance(field_banks, list): raise ValueError('fieldBanks must be an array')
    bank_ids, fields, assigned = set(), set(), set()
    for bank in field_banks:
        if not isinstance(bank, dict) or set(bank) != {'id', 'fields', 'clips'} or not isinstance(bank.get('id'), str) or not re.fullmatch(r'[a-z0-9][a-z0-9-]*', bank['id']) or bank['id'] in bank_ids:
            raise ValueError('Invalid or duplicate field bank identity')
        bank_ids.add(bank['id'])
        maps, members = bank.get('fields'), bank.get('clips')
        if not isinstance(maps, list) or not maps or not isinstance(members, list) or not members:
            raise ValueError('Field banks require fields and clips arrays')
        for field in maps:
            if type(field) is not int or field < 0 or field > 2147483647 or field in fields:
                raise ValueError('Invalid or duplicate field bank map ownership')
            fields.add(field)
        for cue_id in members:
            if not isinstance(cue_id, str) or cue_id not in clips or cue_id in movie_ids or cue_id in assigned:
                raise ValueError('Invalid or duplicate field bank clip ownership')
            assigned.add(cue_id)
        banks.append(set(members))
    banks.append(set(clips) - movie_ids - assigned)
    for ids in banks:
        if len(ids) > 512 or sum(clips[i]['bytes'] for i in ids) > 33554432:
            raise ValueError('Runtime description bank exceeds its memory budget')


def validate_pack(directory):
    manifest = read_json(directory / 'manifest.json')
    if manifest.get('schema') != 1: raise ValueError('Unsupported pack schema')
    if 'fieldBanks' in manifest and not isinstance(manifest['fieldBanks'], list): raise ValueError('fieldBanks must be an array')
    clips = {}
    for row in manifest['clips']:
        if row['id'] in clips or not re.fullmatch(r'[a-z0-9][a-z0-9-]*', row['id']) or row['file'] != row['id'] + '.wav':
            raise ValueError('Invalid or duplicate packed identity')
        data = (directory / row['file']).read_bytes()
        if len(data) != row['bytes'] or digest(data) != row['sha256']:
            raise ValueError('Packed recording changed: ' + row['id'])
        with wave.open(io.BytesIO(data)) as source:
            if source.getparams()[:3] != (1, 2, RATE) or data[36:40] != b'data' or len(data) != 44 + 2 * source.getnframes():
                raise ValueError('Noncanonical packed WAV: ' + row['id'])
            if abs(source.getnframes() / RATE - row['duration']) > .000001:
                raise ValueError('Packed duration changed: ' + row['id'])
        clips[row['id']] = row
    expected = {'manifest.json'} | {row['file'] for row in clips.values()}
    if {p.name for p in directory.iterdir()} != expected:
        raise ValueError('Unlisted/private file in public pack')
    films = set()
    for film in manifest['films']:
        if film['movie'] in films or not re.fullmatch(r'[0-9a-f]{64}', film['sourceSHA256']) or film['sourceBytes'] <= 0:
            raise ValueError('Invalid/duplicate movie visual identity')
        check_timeline(film, clips); films.add(film['movie'])
    if len(films) != 42 or len(clips) > 4096: raise ValueError('Incomplete movie coverage or oversized pack')
    check_bank_budgets(manifest['films'], clips, manifest.get('fieldBanks'))
    return manifest


def replace_pack(source, voice_dirs, output, voice):
    """Replace recordings while preserving the reviewed native content exactly."""
    source, output = Path(source).resolve(), Path(output).resolve()
    if source == output or source in output.parents or output in source.parents:
        raise ValueError('Replace recordings in an independent directory')
    if output.exists() and any(output.iterdir()):
        raise ValueError('Replacement output must be empty')
    if not voice.strip() or len(voice) > 200:
        raise ValueError('A concise, truthful public provider description is required')
    original = validate_pack(source)
    revised, waves, reports = copy.deepcopy(original), {}, []
    revised['voice'] = voice
    # The old marker describes old audio. Mastering the new candidate sets it anew.
    revised.pop('openingMastering', None)
    revised['clips'] = []
    for cue in original['clips']:
        row, data, report = prepare_clip(cue, voice_dirs, {})
        revised['clips'].append(row)
        waves[row['file']] = data
        reports.append(report)
    clips = {row['id']:row for row in revised['clips']}
    for film in revised['films']:
        check_timeline(film, clips)
    check_bank_budgets(revised['films'], clips, revised.get('fieldBanks'))
    output.mkdir(parents=True, exist_ok=True)
    for name, data in waves.items():
        (output/name).write_bytes(data)
    (output/'manifest.json').write_text(json.dumps(revised,ensure_ascii=False,indent=1)+'\n',encoding='utf-8')
    validate_pack(output)
    return revised, reports


def append_fields(source, script, voice_dirs, output, field_banks, reviews=None):
    """Append reviewed fields without remastering or rewriting any old WAV."""
    source, output = Path(source).resolve(), Path(output).resolve()
    voice_dirs = [Path(p).resolve() for p in voice_dirs]
    for protected in [source] + voice_dirs:
        if output == protected or protected in output.parents or output in protected.parents:
            raise ValueError('Append into an independent public directory')
    if output.exists() and any(output.iterdir()): raise ValueError('Append output must be empty')
    original = validate_pack(source)
    revised, waves, reports = copy.deepcopy(original), {}, []
    if not isinstance(script, dict) or not isinstance(script.get('cues'), list) or not script['cues']:
        raise ValueError('Append script requires nonempty cues')
    if isinstance(field_banks, dict): field_banks = field_banks.get('fieldBanks')
    if not isinstance(field_banks, list): raise ValueError('field-banks requires an array')
    existing = {row['id'] for row in original['clips']}
    added, cues = set(), []
    for cue in script['cues']:
        if not isinstance(cue, dict) or not isinstance(cue.get('cueId'), str) or not re.fullmatch(r'[a-z0-9][a-z0-9-]*', cue['cueId']) or not isinstance(cue.get('voiceText'), str) or not cue['voiceText'].strip():
            raise ValueError('Invalid appended cue identity/text')
        cue_id = cue['cueId']
        if cue_id in existing or cue_id in added: raise ValueError('Duplicate or existing appended cue: ' + cue_id)
        added.add(cue_id); cues.append({'id': cue_id, 'text': cue['voiceText']})
    members = []
    for bank in field_banks:
        if not isinstance(bank, dict) or not isinstance(bank.get('clips'), list): raise ValueError('Invalid appended field bank')
        members.extend(bank['clips'])
    if any(not isinstance(i, str) for i in members) or set(members) != added:
        raise ValueError('Every appended cue must belong to a new bank; old memberships are immutable')
    revised['fieldBanks'] = copy.deepcopy(original.get('fieldBanks', [])) + copy.deepcopy(field_banks)
    # Validate structural closure and limits before decoding generated recordings.
    preliminary = {row['id']: row for row in original['clips']}
    preliminary.update({cue['id']: {'bytes': 46} for cue in cues})
    check_bank_budgets(revised['films'], preliminary, revised['fieldBanks'])
    for cue in cues:
        row, data, report = prepare_clip(cue, voice_dirs, reviews or {})
        revised['clips'].append(row); waves[row['file']] = data; reports.append(report)
    check_bank_budgets(revised['films'], {row['id']: row for row in revised['clips']}, revised['fieldBanks'])
    # Re-read each immutable input before writing: a concurrent source edit must
    # not silently turn preservation into a hash-only assertion about old metadata.
    for row in original['clips']:
        data = (source / row['file']).read_bytes()
        if len(data) != row['bytes'] or digest(data) != row['sha256']: raise ValueError('Source recording changed while appending: ' + row['id'])
        waves[row['file']] = data
    output.mkdir(parents=True, exist_ok=True)
    for name, data in waves.items(): (output / name).write_bytes(data)
    (output / 'manifest.json').write_text(json.dumps(revised, ensure_ascii=False, indent=1) + '\n', encoding='utf-8')
    validate_pack(output)
    return revised, reports


def check_append_artifacts(args, parser):
    """Refuse aliases into immutable inputs and keep QA outside public artifacts."""
    qa, catalog = args.qa.resolve(), args.catalog.resolve()
    protected_dirs = [args.replace_from.resolve(), args.out.resolve()] + [p.resolve() for p in args.voice_dir]
    protected_files = [args.append_fields.resolve(), args.field_banks.resolve()]
    if args.reviews: protected_files.append(args.reviews.resolve())
    if args.qa.exists() or args.qa.is_symlink(): parser.error('Preserve existing private QA; choose a new report path')
    for target in (qa, catalog):
        if any(target == p or p in target.parents for p in protected_dirs) or target in protected_files:
            parser.error('Keep catalog and private QA outside public output and immutable inputs')
        if target.exists() and any(p.exists() and target.samefile(p) for p in protected_files + [args.replace_from / 'manifest.json']):
            parser.error('Artifact aliases an immutable input')
    if qa == catalog: parser.error('Catalog and private QA require separate paths')
    if args.catalog.is_symlink(): parser.error('Catalog output cannot be a link')
    # Hard links can alias any source recording, including voice metadata.
    if catalog.exists():
        for directory in [args.replace_from] + args.voice_dir:
            if any(p.is_file() and catalog.samefile(p) for p in directory.iterdir()):
                parser.error('Catalog output aliases an immutable input')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--script', type=Path)
    parser.add_argument('--fields', type=Path)
    parser.add_argument('--append-fields', type=Path)
    parser.add_argument('--field-banks', type=Path)
    parser.add_argument('--inventory', type=Path)
    parser.add_argument('--voice-dir', action='append', type=Path, default=[])
    parser.add_argument('--reviews', type=Path)
    parser.add_argument('--out', type=Path)
    parser.add_argument('--catalog', type=Path)
    parser.add_argument('--qa', type=Path)
    parser.add_argument('--validate', type=Path)
    parser.add_argument('--replace-from', type=Path)
    parser.add_argument('--voice-description')
    args = parser.parse_args()
    if args.append_fields and args.validate: parser.error('Append mode cannot be combined with validation')
    if args.validate:
        manifest = validate_pack(args.validate)
        print(f"PASS public descriptions: {len(manifest['films'])} films, {len(manifest['clips'])} hash-pinned PCM16 clips")
        return
    if args.append_fields:
        if not all((args.replace_from, args.voice_dir, args.out, args.catalog, args.qa, args.field_banks)):
            parser.error('Appending requires replace-from, voice-dir, out, catalog, qa and field-banks')
        if args.validate or args.script or args.fields or args.inventory or args.voice_description:
            parser.error('Append mode cannot be combined with build, validation or voice replacement')
        check_append_artifacts(args, parser)
        manifest, reports = append_fields(args.replace_from, read_json(args.append_fields), args.voice_dir, args.out,
                                         read_json(args.field_banks), read_json(args.reviews) if args.reviews else {})
        args.catalog.parent.mkdir(parents=True, exist_ok=True)
        args.catalog.write_bytes((args.out / 'manifest.json').read_bytes())
        args.qa.parent.mkdir(parents=True, exist_ok=True)
        with args.qa.open('x', encoding='utf-8') as report: report.write(json.dumps(reports, indent=1) + '\n')
        print(f"PASS appended {len(reports)} field recordings; old audio, films and opening mastering preserved")
        return
    if args.replace_from:
        if not all((args.voice_dir,args.out,args.qa,args.voice_description)):
            parser.error('Replacing requires voice-dir, out, qa and an explicit voice-description')
        if args.qa.exists() or args.qa.is_symlink():
            parser.error('Preserve existing private QA; choose a new report path')
        for protected in (args.replace_from.resolve(),args.out.resolve(),*(p.resolve() for p in args.voice_dir)):
            if args.qa.resolve()==protected or protected in args.qa.resolve().parents:
                parser.error('Keep private QA outside source, voice inputs and public output directories')
        manifest,reports=replace_pack(args.replace_from,args.voice_dir,args.out,args.voice_description)
        args.qa.parent.mkdir(parents=True,exist_ok=True)
        # Exclusive creation also refuses a link/file introduced after preflight.
        with args.qa.open('x',encoding='utf-8') as report:
            report.write(json.dumps(reports,indent=1)+'\n')
        print(f"PASS replaced {len(manifest['clips'])} recordings; native content and timelines preserved")
        return
    if not all((args.script, args.fields, args.inventory, args.voice_dir, args.out, args.catalog, args.qa)):
        parser.error('Building requires script, fields, inventory, voice-dir, out, catalog and qa')
    script, inventory = read_json(args.script), {r['key']: r for r in read_json(args.inventory)}
    cues = {r['cueId']: {'id': r['cueId'], 'text': r['voiceText']} for r in read_json(args.fields)['cues']}
    for film in script['films']:
        for cue in film['cues']:
            if cue['id'] in cues: raise ValueError('Duplicate script identity: ' + cue['id'])
            cues[cue['id']] = cue
    reviews = read_json(args.reviews) if args.reviews else {}
    clips, waves, qa = {}, {}, []
    for cue in cues.values():
        row, data, report = prepare_clip(cue, args.voice_dir, reviews)
        clips[row['id']] = row; waves[row['file']] = data; qa.append(report)
    movies = []
    for film in script['films']:
        native = inventory[film['movie']]
        check_timeline(film, clips)
        movies.append({'movie': film['movie'], 'duration': native['duration'],
                       'sourceSHA256': native['sha256'], 'sourceBytes': native['bytes'],
                       'cues': [{k: c[k] for k in ('id', 'start', 'end')} for c in film['cues']]})
    manifest = {'schema': 1, 'voice': 'Approved personal narrator, generated locally with Qwen3-TTS',
                'format': '24000 Hz mono canonical PCM16 WAV', 'clips': list(clips.values()), 'films': movies}
    if args.field_banks:
        banks = read_json(args.field_banks)
        manifest['fieldBanks'] = banks.get('fieldBanks') if isinstance(banks, dict) else banks
    check_bank_budgets(movies, clips, manifest.get('fieldBanks'))
    if args.out.exists() and any(args.out.iterdir()):
        raise ValueError('Build into an empty directory; never replace a reviewed pack in place')
    args.out.mkdir(parents=True, exist_ok=True)
    for name, data in waves.items(): (args.out / name).write_bytes(data)
    public = json.dumps(manifest, ensure_ascii=False, indent=1) + '\n'
    (args.out / 'manifest.json').write_text(public, encoding='utf-8')
    validate_pack(args.out)
    args.catalog.parent.mkdir(parents=True, exist_ok=True); args.catalog.write_text(public, encoding='utf-8')
    args.qa.parent.mkdir(parents=True, exist_ok=True); args.qa.write_text(json.dumps(qa, indent=1) + '\n', encoding='utf-8')
    print(f'PASS prepared {len(clips)} verified narration recordings for {len(movies)} films and field observations')


if __name__ == '__main__': main()
