"""Offline integrity and speech checks for private Fish narration generation."""
import hashlib
import io
import json
import math
import re
import wave
from pathlib import Path

from mastering import reconstructed_peak_dbfs


def sha(data):
    return hashlib.sha256(data).hexdigest()


def atomic_json(path, value):
    path = Path(path)
    temporary = path.with_suffix(path.suffix + '.tmp')
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=1) + '\n', encoding='utf-8')
    temporary.replace(path)


def open_job(root, identity):
    root = Path(root)
    root.mkdir(parents=True, exist_ok=True)
    checkpoint = root / 'job.json'
    if checkpoint.exists():
        if json.loads(checkpoint.read_text(encoding='utf-8')) != identity:
            raise ValueError('Rendering inputs changed; use another research directory')
    elif any(root.iterdir()):
        raise ValueError('Unknown nonempty research directory')
    else:
        atomic_json(checkpoint, identity)


def read_references(profiles):
    result = []
    for filename in profiles:
        filename = Path(filename)
        profile = json.loads(filename.read_text(encoding='utf-8-sig'))
        audio = (filename.parent / profile['reference_wav']).read_bytes()
        text = profile.get('reference_text', '')
        if not text.strip() or sha(audio) != profile.get('reference_sha256'):
            raise ValueError('Human voice reference identity/text mismatch')
        result.append({'audio': audio, 'text': text, 'sha256': sha(audio)})
    if not result:
        raise ValueError('At least one verified human reference is required')
    return result


def wave_bytes(samples):
    import numpy as np
    memory = io.BytesIO()
    with wave.open(memory, 'wb') as output:
        output.setparams((1, 2, 24000, 0, 'NONE', 'not compressed'))
        output.writeframes(np.rint(samples * 32768).astype('<i2').tobytes())
    return memory.getvalue()


def asr_audio(data):
    # A bare array is assumed to be 16 kHz by faster-whisper. Decode the WAV
    # header explicitly so 24 kHz narration never acquires a 1.5x ASR clock.
    from faster_whisper.audio import decode_audio
    return decode_audio(io.BytesIO(data), sampling_rate=16000)


def normalize_wave(data):
    import numpy as np
    with wave.open(io.BytesIO(data), 'rb') as source:
        if source.getparams()[:3] != (1, 2, 24000):
            raise ValueError('Fish output must be 24 kHz mono PCM16')
        raw = source.readframes(source.getnframes())
    if not raw or len(raw) > 2097108:
        raise ValueError('Empty or oversized speech')
    pcm = np.frombuffer(raw, dtype='<i2')
    if np.max(np.abs(pcm.astype('int32'))) >= 32767:
        raise ValueError('Clipped provider output')
    audio = pcm.astype('float64') / 32768
    blocks = [float(np.sqrt(np.mean(audio[i:i+240] ** 2))) for i in range(0, len(audio), 240)]
    active = [i for i, value in enumerate(blocks) if value > 58/32768]
    if not active:
        raise ValueError('Silent provider output')
    left = max(0, active[0]*240-2400)
    right = min(len(audio), (active[-1]+1)*240+4800)
    audio = audio[left:right]
    frames = [float(np.sqrt(np.mean(audio[i:i+1200] ** 2))) for i in range(0, len(audio), 1200)]
    speech = [v for v in frames if v > .1*max(frames)]
    rms = math.sqrt(sum(v*v for v in speech)/len(speech))
    original_peak = reconstructed_peak_dbfs(raw[left*2:right*2])
    gain = min(10**(-18/20)/rms, 10**((-1.9-original_peak)/20))
    output = wave_bytes(audio*gain)
    return output, {'gain_db':20*math.log10(gain), 'speech_rms_dbfs':20*math.log10(rms*gain),
                    'reconstructed_peak_dbfs':original_peak+20*math.log10(gain),
                    'trimmed_lead_seconds':left/24000, 'trimmed_tail_seconds':(len(pcm)-right)/24000}


def rhythm_failures(words, syllables, duration, maximum):
    failures = []
    if not math.isfinite(duration) or duration + .05 > maximum:
        failures.append('window')
    function_words = {'a','an','the','and','or','in','on','to','of','with','from','through','at','by','for','as','its','his','her','their'}
    for index, word in enumerate(words):
        text = re.sub(r"[^a-z']", '', word['word'].lower())
        count = max(1, syllables(text))
        hold = max(0, word['end']-word['start'])/count
        if text in function_words and hold > .45:
            failures.append('held-function-word')
        if hold > (.8 if index == len(words)-1 else .65):
            failures.append('held-word')
    if len(words) >= 5:
        rate = sum(max(1, syllables(w['word'])) for w in words)/max(.01, words[-1]['end']-words[0]['start'])
        if rate < 2.7 or rate > 7.5:
            failures.append('speech-rate')
    return sorted(set(failures))


def next_take_number(directory, cue_id):
    pattern = re.compile(re.escape(cue_id)+r'-take-(\d+)\.(?:wav|json)$')
    indices = [int(match.group(1)) for path in Path(directory).iterdir()
               if (match := pattern.fullmatch(path.name))]
    return max(indices, default=0)+1


def verify_prior_take(cue, raw, metadata, prior_job_sha):
    origin = metadata.get('origin', {})
    if (metadata.get('id') != cue['id'] or metadata.get('text') != cue['text'] or
        metadata.get('job_sha256') != prior_job_sha or not isinstance(origin,dict) or
        origin.get('raw_sha256') != sha(raw)):
        raise ValueError('Prior take does not match its original job and waveform')


def verify_accepted(cue, data, metadata, job_sha, maximum, syllables):
    import numpy as np
    from build_pack import equivalent_words
    if (metadata.get('id') != cue['id'] or metadata.get('text') != cue['text'] or
        metadata.get('wave_sha256') != sha(data) or metadata.get('job_sha256') != job_sha):
        raise ValueError('Accepted take identity changed')
    with wave.open(io.BytesIO(data),'rb') as source:
        duration = source.getnframes()/24000
        if source.getparams()[:3] != (1,2,24000) or data[36:40] != b'data' or len(data)!=44+2*source.getnframes():
            raise ValueError('Accepted take format changed')
    checks = metadata.get('checks',{})
    rhythm = metadata.get('rhythm',{})
    words = rhythm.get('words',[])
    levels = metadata.get('levels',{})
    if (not equivalent_words(cue['text'],checks.get('asr_text','')) or
        set(checks.get('failures',[]))-{'words'} or not words or rhythm.get('failures') or
        not equivalent_words(cue['text'],' '.join(w['word'] for w in words)) or
        not math.isfinite(rhythm.get('duration',math.nan)) or abs(duration-rhythm['duration'])>.000001 or
        rhythm_failures(words,syllables,duration,maximum)):
        raise ValueError('Accepted take failed word, rhythm or window checks')
    for word in words:
        if (not all(math.isfinite(word[key]) for key in ('start','end')) or
            word['start']<0 or word['end']<word['start'] or word['end']>duration+.05):
            raise ValueError('Accepted word clock changed')
    peak = levels.get('reconstructed_peak_dbfs',math.nan)
    samples = np.frombuffer(data[44:],dtype='<i2').astype('int32')
    if not math.isfinite(peak) or peak>-1.8 or not len(samples) or np.max(np.abs(samples))/32768>10**(-1.75/20):
        raise ValueError('Accepted take lost signal headroom')
    if any(not math.isfinite(value) for value in levels.values() if isinstance(value,(int,float))):
        raise ValueError('Nonfinite accepted signal checks')
