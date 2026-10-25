"""Render private, resumable Fish takes; credentials and references never enter a public pack."""
import argparse
import concurrent.futures as futures
import io
import json
import os
import re
import sys
import time
import wave
from pathlib import Path

from build_pack import equivalent_words, validate_pack
from fish_render_support import (asr_audio, atomic_json, next_take_number, normalize_wave, open_job,
                                 read_references, rhythm_failures, sha, verify_accepted, verify_prior_take)

MODEL = 's2.1-pro-free'
DIRECTION = '[engaged, conversational delivery] '


def load_render_input(source, append_script=None):
    """Validate the immutable baseline, then select only explicitly new cues."""
    catalog = validate_pack(source)
    if append_script is None:
        return catalog, None

    def unique_keys(pairs):
        value = {}
        for key, item in pairs:
            if key in value:
                raise ValueError('Duplicate append script JSON key')
            value[key] = item
        return value

    data = Path(append_script).read_bytes()
    script = json.loads(data.decode('utf-8-sig'), object_pairs_hook=unique_keys)
    if (not isinstance(script, dict) or set(script) != {'cues'} or
        not isinstance(script['cues'], list) or not script['cues']):
        raise ValueError('Append script must contain only a nonempty cues array')
    seen = {row['id'] for row in catalog['clips']}
    for cue in script['cues']:
        # A closed text-only schema keeps private references and timing fields
        # out of the input, rather than trying to blacklist reference names.
        if not isinstance(cue, dict) or set(cue) != {'id', 'text'}:
            raise ValueError('Append cues must contain only id and text')
        cue_id, text = cue['id'], cue['text']
        if (not isinstance(cue_id, str) or not re.fullmatch(r'[a-z0-9][a-z0-9-]*', cue_id) or
            cue_id in seen):
            raise ValueError('Invalid, duplicate or existing append cue identity')
        if (not isinstance(text, str) or not text.strip() or not any(c.isalnum() for c in text) or
            not text.isprintable()):
            raise ValueError('Append cue text must be printable spoken text')
        seen.add(cue_id)
    # Field cues have no FMV clock or placeholder recording/duration. The
    # original public pack remains untouched and is never a rendering target.
    return dict(catalog, clips=script['cues'], films=[]), sha(data)


def maximum_duration(rows, windows):
    # New field cues are bounded by the canonical 2 MiB WAV budget; existing
    # movie windows and legacy field duration guards keep their prior behavior.
    return min(windows[row['id']] if row['id'] in windows else
               row['duration']*1.35+.15 if 'duration' in row else
               (2097152-44)/48000 for row in rows)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-pack', type=Path, required=True)
    parser.add_argument('--append-script', type=Path,
                        help='Text-only JSON {cues:[{id,text}]} containing new field cue IDs')
    parser.add_argument('--out', type=Path, required=True)
    parser.add_argument('--reference', type=Path, action='append', required=True)
    parser.add_argument('--asr-cache', type=Path, required=True)
    parser.add_argument('--sdk-path', type=Path)
    parser.add_argument('--seed-selection', type=Path)
    parser.add_argument('--reuse-takes', type=Path)
    parser.add_argument('--limit', type=int)
    args = parser.parse_args()
    source, root = args.source_pack.resolve(), args.out.resolve()
    if source == root or source in root.parents or root in source.parents:
        raise ValueError('Render into a separate research directory')
    catalog, append_script_sha = load_render_input(source, args.append_script)
    refs = read_references(args.reference)
    identity = {'schema':1, 'model':MODEL, 'direction':DIRECTION, 'temperature':.65, 'top_p':.8,
                'catalog_sha256':sha((source/'manifest.json').read_bytes()),
                'references':[{'sha256':r['sha256'],'text_sha256':sha(r['text'].encode())} for r in refs],
                'seed_selection_sha256':sha(args.seed_selection.read_bytes()) if args.seed_selection else None,
                'reuse_job_sha256':sha((args.reuse_takes/'job.json').read_bytes()) if args.reuse_takes else None,
                'renderer_sha256':sha(Path(__file__).read_bytes()),
                'support_sha256':sha(Path(__file__).with_name('fish_render_support.py').read_bytes())}
    if append_script_sha is not None:
        identity['append_script_sha256'] = append_script_sha
    open_job(root, identity)
    raw_dir, accepted = root/'takes', root/'accepted'
    raw_dir.mkdir(exist_ok=True)
    accepted.mkdir(exist_ok=True)
    if args.sdk_path:
        sys.path.append(str(args.sdk_path))
    from fishaudio import FishAudio
    from fishaudio.types import ReferenceAudio, TTSConfig
    from faster_whisper import WhisperModel
    import cmudict
    import numpy as np
    import torch
    # Retain Windows DLL-directory handles for the entire local ASR lifetime.
    dll_handles = []
    if os.name == 'nt':
        runtime = Path(torch.__file__).parent/'lib'
        dll_handles.append(os.add_dll_directory(str(runtime)))
        os.environ['PATH'] = str(runtime)+os.pathsep+os.environ['PATH']
    asr = WhisperModel('medium.en',device='cuda',compute_type='float16',
                       download_root=str(args.asr_cache),local_files_only=True)
    dictionary = cmudict.dict()
    def syllables(word):
        word = re.sub(r"[^a-z']",'',word.lower())
        if word in dictionary:
            return max(1,sum(any(c.isdigit() for c in p) for p in dictionary[word][0]))
        return max(1,len(re.findall('[aeiouy]+',word))-(word.endswith('e') and not word.endswith(('le','ye'))))
    windows = {c['id']:c['end']-c['start'] for f in catalog['films'] for c in f['cues']}
    by_text = {}
    for row in catalog['clips']:
        by_text.setdefault(row['text'],[]).append(row)
    unique = [rows[0] for rows in by_text.values()]
    if args.limit:
        unique = unique[:args.limit]
    reference_audio = [ReferenceAudio(audio=r['audio'],text=r['text']) for r in refs]
    seeds = {}
    if args.seed_selection:
        for row in json.loads(args.seed_selection.read_text())['takes']:
            data = Path(row['source']).read_bytes()
            if sha(data) != row['sha256']:
                raise ValueError('Accepted audition waveform changed')
            seeds[row['text']] = data
    records, failures, attempts = {}, {}, {}
    def maximum(cue):
        return maximum_duration(by_text[cue['text']], windows)
    def status():
        count = sum(len(by_text[text]) for text in records)
        value = {'accepted':count,'total':len(catalog['clips']),'unique_accepted':len(records),
                 'unique_total':len(by_text),'failed':len(failures),'requests':sum(attempts.values()),
                 'complete':len(records)==len(by_text)}
        atomic_json(root/'status.json',value)
        print(json.dumps(value),flush=True)
    def inspect(cue, data, origin):
        normalized, levels = normalize_wave(data)
        waveform = np.frombuffer(normalized[44:],dtype='<i2').astype('float32')/32768
        segments,info = asr.transcribe(asr_audio(normalized),language='en',word_timestamps=True,vad_filter=True,
                                  beam_size=5,condition_on_previous_text=False)
        if abs(info.duration-len(waveform)/24000)>.002:
            raise ValueError('ASR clock does not match the original WAV')
        segments = list(segments)
        heard = ' '.join(s.text.strip() for s in segments)
        words = [{'word':w.word.strip(),'start':w.start,'end':w.end} for s in segments for w in s.words or []]
        flags = rhythm_failures(words,syllables,len(waveform)/24000,maximum(cue))
        equivalent = equivalent_words(cue['text'],heard)
        if not equivalent:
            flags.append('words')
        exact = re.findall(r'[a-z]+|\d+',cue['text'].lower().replace("'",'')) == re.findall(r'[a-z]+|\d+',heard.lower().replace("'",''))
        check_failures = list(flags)
        if equivalent and not exact:
            check_failures.append('words')
        result = {'id':cue['id'],'text':cue['text'],'wave_sha256':sha(normalized),
                  'origin':origin,'job_sha256':sha(json.dumps(identity,sort_keys=True).encode()),
                  'checks':{'exact':exact,'failures':sorted(set(check_failures)),'asr_text':heard},
                  'rhythm':{'words':words,'failures':flags,'duration':len(waveform)/24000,'maximum':maximum(cue)},
                  'levels':levels,'model':MODEL}
        if not flags:
            # Fresh malformed evidence is a rejected take, handled by the
            # caller's per-take retry path; cached/seed evidence stays fail-fast.
            verify_accepted(cue,normalized,result,sha(json.dumps(identity,sort_keys=True).encode()),maximum(cue),syllables)
        return normalized, result, not flags
    def accept(cue, data, result):
        verify_accepted(cue,data,result,sha(json.dumps(identity,sort_keys=True).encode()),maximum(cue),syllables)
        (accepted/(cue['id']+'.wav')).write_bytes(data)
        atomic_json(accepted/(cue['id']+'.json'),result)
        records[cue['text']] = result
    for cue in unique:
        metadata = accepted/(cue['id']+'.json')
        if metadata.exists():
            result = json.loads(metadata.read_text())
            data = (accepted/(cue['id']+'.wav')).read_bytes()
            verify_accepted(cue,data,result,sha(json.dumps(identity,sort_keys=True).encode()),maximum(cue),syllables)
            records[cue['text']] = result
        elif cue['text'] in seeds:
            data,result,ok = inspect(cue,seeds[cue['text']],'accepted-listening-sample')
            if not ok:
                raise ValueError('Accepted audition failed current safety checks')
            accept(cue,data,result)
        # Orphan WAVs survive interruption; never allocate their suffix again.
        attempts[cue['id']] = next_take_number(raw_dir,cue['id'])-1
    if args.reuse_takes:
        prior = json.loads((args.reuse_takes/'job.json').read_text())
        for key in ('model','direction','temperature','top_p','catalog_sha256','references','append_script_sha256'):
            if prior.get(key) != identity.get(key):
                raise ValueError('Prior raw takes came from different generation inputs')
        for cue in unique:
            if cue['text'] in records: continue
            for filename in sorted((args.reuse_takes/'takes').glob(cue['id']+'-take-*.json')):
                meta = json.loads(filename.read_text())
                if meta.get('text') != cue['text'] or not isinstance(meta.get('origin'),dict): continue
                raw = filename.with_suffix('.wav').read_bytes()
                verify_prior_take(cue,raw,meta,sha(json.dumps(prior,sort_keys=True).encode()))
                data,result,ok = inspect(cue,raw,{'rechecked_raw_sha256':sha(raw)})
                if ok:
                    accept(cue,data,result)
                    break
    def request(cue, attempt):
        # Each worker owns its HTTP client. Never return exception bodies/headers.
        prefix = DIRECTION if attempt < 3 else '[engaged, fluent conversational delivery] '
        if attempt >= 5:
            prefix = '[brisk, fluent conversational delivery] '
        for retry in range(4):
            try:
                with FishAudio(timeout=90) as client:
                    data = client.tts.convert(text=prefix+cue['text'],references=reference_audio,
                        model=MODEL,format='wav',latency='normal',
                        config=TTSConfig(sample_rate=24000,temperature=.65 if attempt!=4 else .8,top_p=.8))
                return {'data':data,'attempt':attempt}
            except Exception as error:
                code = getattr(error,'status',None)
                transient = code==429 or isinstance(code,int) and code>=500 or type(error).__name__ in ('ReadTimeout','ConnectTimeout','ConnectError')
                if not transient or retry==3:
                    return {'error_type':type(error).__name__,'status':code,'attempt':attempt}
                time.sleep(min(20,2**(retry+1)))
    pending_cues = iter(c for c in unique if c['text'] not in records)
    active = {}
    last_status = 0
    def submit(pool,cue):
        attempts[cue['id']] += 1
        active[pool.submit(request,cue,attempts[cue['id']])] = cue
    with futures.ThreadPoolExecutor(max_workers=4) as pool:
        for _ in range(4):
            cue = next(pending_cues,None)
            if cue is not None: submit(pool,cue)
        while active:
            done,_ = futures.wait(active,timeout=1,return_when=futures.FIRST_COMPLETED)
            for future in done:
                cue = active.pop(future)
                response = future.result()
                attempt = response['attempt']
                prefix = raw_dir/(cue['id']+f'-take-{attempt:03d}')
                if 'data' in response:
                    raw = response.pop('data')
                    prefix.with_suffix('.wav').write_bytes(raw)
                    try:
                        data,result,ok = inspect(cue,raw,{'attempt':attempt,'raw_sha256':sha(raw)})
                    except (ValueError,wave.Error,EOFError) as error:
                        data,result,ok = None,{'error_type':type(error).__name__},False
                    atomic_json(prefix.with_suffix('.json'),result)
                    if ok:
                        accept(cue,data,result)
                    elif attempt < 6:
                        submit(pool,cue)
                        continue
                    else:
                        failures[cue['id']] = result
                else:
                    atomic_json(prefix.with_suffix('.json'),response)
                    failures[cue['id']] = response
                    if response.get('status') in (401,402,403,422):
                        status()
                        raise RuntimeError('Provider rejected authentication, access or request format; no more work scheduled')
                next_cue = next(pending_cues,None)
                if next_cue is not None: submit(pool,next_cue)
            if time.monotonic()-last_status>15:
                status()
                last_status = time.monotonic()
    # Duplicate captions share verified speech, but retain each native cue identity.
    for text,result in records.items():
        source_id = result['id']
        data = (accepted/(source_id+'.wav')).read_bytes()
        for cue in by_text[text]:
            if cue['id'] == source_id: continue
            (accepted/(cue['id']+'.wav')).write_bytes(data)
            atomic_json(accepted/(cue['id']+'.json'),dict(result,id=cue['id'],duplicate_of=source_id))
    atomic_json(root/'failures.json',failures)
    status()
    if failures:
        raise RuntimeError('Some takes failed; retained privately for targeted repair')


if __name__ == '__main__':
    main()
