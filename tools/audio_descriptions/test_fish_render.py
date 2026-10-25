import importlib.util
import hashlib
import io
import json
import math
import tempfile
import unittest
import wave
from pathlib import Path
from types import ModuleType, SimpleNamespace
from unittest.mock import patch

import render_fish as renderer

SPEC = importlib.util.spec_from_file_location('fish_render_support', Path(__file__).with_name('fish_render_support.py'))
support = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(support)


class FishRenderTests(unittest.TestCase):
    def baseline(self, root):
        source = root/'baseline'; source.mkdir()
        memory = io.BytesIO()
        with wave.open(memory, 'wb') as output:
            output.setparams((1, 2, 24000, 0, 'NONE', 'not compressed'))
            output.writeframes(b'\x00\x10'*24000)
        data = memory.getvalue()
        (source/'old-cue.wav').write_bytes(data)
        row = {'id':'old-cue', 'text':'A bird flies.', 'file':'old-cue.wav',
               'bytes':len(data), 'duration':1, 'sha256':hashlib.sha256(data).hexdigest()}
        films = [{'movie':f'FMV{i:03}', 'duration':5, 'sourceSHA256':'0'*64,
                  'sourceBytes':100, 'cues':[{'id':'old-cue', 'start':0, 'end':4}]} for i in range(42)]
        (source/'manifest.json').write_text(json.dumps({'schema':1, 'clips':[row], 'films':films}), encoding='utf-8')
        return source

    def test_append_input_selects_only_new_text_without_touching_the_baseline(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); source = self.baseline(root)
            before = {p.name:p.read_bytes() for p in source.iterdir()}
            script = root/'append.json'
            script.write_bytes(b'{"cues":[{"id":"new-cue","text":"A bird flies."},{"id":"second-cue","text":"She waves."}]}')
            catalog, script_hash = renderer.load_render_input(source, script)
            self.assertEqual(catalog['clips'], [{'id':'new-cue','text':'A bird flies.'},
                                                {'id':'second-cue','text':'She waves.'}])
            self.assertEqual(catalog['films'], [])
            self.assertEqual(script_hash, hashlib.sha256(script.read_bytes()).hexdigest())
            self.assertEqual({p.name:p.read_bytes() for p in source.iterdir()}, before)

    def test_append_rejects_old_duplicate_invalid_and_private_input(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); source = self.baseline(root); script = root/'append.json'
            bad = [
                {'cues':[{'id':'old-cue','text':'She waves.'}]},
                {'cues':[{'id':'new-cue','text':'She waves.'}]*2},
                {'cues':[{'id':'../escape','text':'She waves.'}]},
                {'cues':[{'id':1,'text':'She waves.'}]},
                {'cues':[]}, {'cues':{}}, [],
            ]
            bad.extend({'cues':[{'id':'new-cue','text':text}]} for text in (None, 7, [], '', '   ', '\n', 'She\x00waves.', '...'))
            bad.extend({'cues':[dict(id='new-cue', text='She waves.', **{key:'private'})]}
                       for key in ('reference_wav','reference_text','reference_sha256','audio','start','end'))
            bad.append({'cues':[{'id':'new-cue','text':'She waves.'}], 'references':[]})
            for value in bad:
                with self.subTest(value=value):
                    script.write_text(json.dumps(value), encoding='utf-8')
                    with self.assertRaises(ValueError):
                        renderer.load_render_input(source, script)
            script.write_text('{"cues":[{"id":"new-cue","text":"She waves.","text":"He waves."}]}', encoding='utf-8')
            with self.assertRaises(ValueError): renderer.load_render_input(source, script)

    def test_append_still_validates_every_baseline_recording(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); source = self.baseline(root); script = root/'append.json'
            script.write_text('{"cues":[{"id":"new-cue","text":"She waves."}]}', encoding='utf-8')
            (source/'old-cue.wav').write_bytes(b'changed baseline')
            with self.assertRaises(ValueError): renderer.load_render_input(source, script)

    def test_legacy_input_preserves_movie_windows_and_field_maximums(self):
        with tempfile.TemporaryDirectory() as temporary:
            source = self.baseline(Path(temporary))
            catalog, script_hash = renderer.load_render_input(source)
            self.assertIsNone(script_hash)
            self.assertEqual(catalog['clips'][0]['id'], 'old-cue')
            self.assertEqual(len(catalog['films']), 42)
            self.assertEqual(renderer.maximum_duration([{'id':'movie','duration':2}], {'movie':4}), 4)
            self.assertAlmostEqual(renderer.maximum_duration([{'id':'field','duration':2}], {}), 2.85)

    def test_new_field_window_uses_wave_budget_without_an_fmv_clock(self):
        self.assertAlmostEqual(renderer.maximum_duration([{'id':'new-cue','text':'She waves.'}], {}),
                               43.68975)

    def test_append_cli_binds_script_bytes_and_baseline_before_loading_provider(self):
        class ProviderBoundary(Exception): pass
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); source = self.baseline(root); script = root/'append.json'
            script.write_bytes(b'{"cues":[{"id":"new-cue","text":"She waves."}]}')
            reference = root/'reference.wav'; reference.write_bytes(b'private human recording')
            profile = root/'profile.json'
            profile.write_text(json.dumps({'reference_wav':reference.name,'reference_text':'Hello.',
                                          'reference_sha256':hashlib.sha256(reference.read_bytes()).hexdigest()}))
            output = root/'job'
            args = ['render_fish', '--source-pack', str(source), '--append-script', str(script),
                    '--out',str(output),'--reference',str(profile),'--asr-cache',str(root/'asr')]
            original_import = __import__
            def offline_import(name, *args, **kwargs):
                if name == 'fishaudio': raise ProviderBoundary()
                return original_import(name, *args, **kwargs)
            with patch('sys.argv',args), patch('builtins.__import__',side_effect=offline_import):
                with self.assertRaises(ProviderBoundary): renderer.main()
                checkpoint = (output/'job.json').read_bytes()
                identity = json.loads(checkpoint)
                self.assertEqual(identity['append_script_sha256'],hashlib.sha256(script.read_bytes()).hexdigest())
                self.assertEqual(identity['catalog_sha256'],hashlib.sha256((source/'manifest.json').read_bytes()).hexdigest())
                script.write_bytes(script.read_bytes()+b'\n')
                with self.assertRaises(ValueError): renderer.main()
                self.assertEqual((output/'job.json').read_bytes(),checkpoint)
                script.write_bytes(script.read_bytes().rstrip(b'\n'))
                manifest = source/'manifest.json'
                manifest.write_bytes(manifest.read_bytes()+b'\n')
                with self.assertRaises(ValueError): renderer.main()
                self.assertEqual((output/'job.json').read_bytes(),checkpoint)
            self.assertEqual(list((output/'accepted').iterdir()), [])

    def test_append_pipeline_requests_only_new_text_and_retains_duplicate_cue_ids(self):
        # Only the external speech/ASR boundaries are substituted. Selection,
        # waveform normalization, word/rhythm verification, evidence and resume
        # all use the real renderer against an independent WAV fixture.
        import array
        raw = array.array('h', (round(4000*math.sin(2*math.pi*220*n/24000)) for n in range(12000)))
        memory = io.BytesIO()
        with wave.open(memory, 'wb') as output:
            output.setparams((1,2,24000,0,'NONE','not compressed'))
            output.writeframes(raw.tobytes())
        requests = []
        class Client:
            def __init__(self, **kwargs): pass
            def __enter__(self): return self
            def __exit__(self, *args): pass
            @property
            def tts(self): return self
            def convert(self, **kwargs):
                requests.append(kwargs)
                return memory.getvalue()
        class Asr:
            def __init__(self, *args, **kwargs): pass
            def transcribe(self, *args, **kwargs):
                words = [SimpleNamespace(word='She',start=.05,end=.2),
                         SimpleNamespace(word='waves.',start=.2,end=.45)]
                return iter([SimpleNamespace(text='She waves.',words=words)]), SimpleNamespace(duration=.5)
        sdk, sdk_types, asr = ModuleType('fishaudio'), ModuleType('fishaudio.types'), ModuleType('faster_whisper')
        sdk.FishAudio = Client
        sdk_types.ReferenceAudio = sdk_types.TTSConfig = lambda **kwargs: SimpleNamespace(**kwargs)
        asr.WhisperModel = Asr
        # The real decode helper still supplies Whisper's correct 16 kHz clock.
        import faster_whisper.audio
        asr.audio = faster_whisper.audio
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); source = self.baseline(root); script = root/'append.json'
            before = {p.name:p.read_bytes() for p in source.iterdir()}
            script.write_text('{"cues":[{"id":"new-cue","text":"She waves."},'
                              '{"id":"same-text-cue","text":"She waves."}]}', encoding='utf-8')
            (root/'reference.wav').write_bytes(b'private human recording')
            profile = root/'profile.json'
            profile.write_text(json.dumps({'reference_wav':'reference.wav','reference_text':'Hello.',
                                          'reference_sha256':hashlib.sha256(b'private human recording').hexdigest()}))
            output = root/'job'
            args = ['render_fish','--source-pack',str(source),'--append-script',str(script),
                    '--out',str(output),'--reference',str(profile),'--asr-cache',str(root/'asr')]
            with patch('sys.argv',args), patch.dict('sys.modules',{'fishaudio':sdk,'fishaudio.types':sdk_types,'faster_whisper':asr}):
                renderer.main()
                checkpoint = (output/'job.json').read_bytes()
                renderer.main()
                # Reusing raw takes in another job must bind the exact script
                # bytes too, even when the decoded cue rows are unchanged.
                script.write_bytes(script.read_bytes()+b'\n')
                reuse_args = list(args)
                reuse_args[reuse_args.index('--out')+1] = str(root/'different-job')
                reuse_args.extend(['--reuse-takes',str(output)])
                with patch('sys.argv',reuse_args):
                    with self.assertRaises(ValueError): renderer.main()
            self.assertEqual(len(requests),1)
            self.assertEqual(requests[0]['text'],'[engaged, conversational delivery] She waves.')
            self.assertEqual(requests[0]['model'],'s2.1-pro-free')
            self.assertEqual({p.name for p in (output/'accepted').glob('*.wav')}, {'new-cue.wav','same-text-cue.wav'})
            self.assertEqual(json.loads((output/'status.json').read_text())['accepted'],2)
            self.assertEqual((output/'job.json').read_bytes(),checkpoint)
            self.assertEqual({p.name:p.read_bytes() for p in source.iterdir()},before)

    def test_raw_reuse_rejects_a_foreign_job_even_when_wave_hash_matches(self):
        raw=b'raw provider output'
        meta={'id':'test','text':'A bird flies.','job_sha256':'different-job',
              'origin':{'raw_sha256':support.sha(raw)}}
        with self.assertRaises(ValueError):
            support.verify_prior_take({'id':'test','text':'A bird flies.'},raw,meta,'expected-job')

    def test_accepted_cache_rejects_a_stale_wrong_transcript(self):
        import numpy as np
        data=support.wave_bytes(.1*np.sin(2*np.pi*220*np.arange(24000)/24000))
        cue={'id':'test','text':'A bird flies.'}
        meta={'id':'test','text':cue['text'],'wave_sha256':support.sha(data),'job_sha256':'job',
              'checks':{'exact':False,'failures':['words'],'asr_text':'Wrong stale transcript.'},
              'rhythm':{'failures':[],'duration':1,'words':[{'word':'A','start':0,'end':.2}]},
              'levels':{'reconstructed_peak_dbfs':-20}}
        with self.assertRaises(ValueError):
            support.verify_accepted(cue,data,meta,'job',2,lambda _:1)

    def test_orphan_wave_cannot_be_overwritten_by_the_next_attempt(self):
        with tempfile.TemporaryDirectory() as temporary:
            root=Path(temporary)
            orphan=root/'test-take-005.wav'; orphan.write_bytes(b'preserved raw take')
            (root/'test-take-002.json').write_text('{}')
            self.assertEqual(support.next_take_number(root,'test'),6)
            self.assertEqual(orphan.read_bytes(),b'preserved raw take')

    def test_asr_input_keeps_the_original_duration_at_whispers_16khz_clock(self):
        memory=io.BytesIO()
        with wave.open(memory,'wb') as output:
            output.setparams((1,2,24000,0,'NONE','not compressed'))
            output.writeframes(b'\0\0'*24000)
        decoded=support.asr_audio(memory.getvalue())
        self.assertEqual(len(decoded),16000)

    def test_long_function_word_is_rejected_even_when_whole_line_fits(self):
        words = [{'word':'emerges','start':0,'end':.6}, {'word':'through','start':.6,'end':1.18}, {'word':'mist.','start':1.18,'end':1.5}]
        self.assertIn('held-function-word', support.rhythm_failures(words, lambda _:1, 1.6, 5))
        words[1]['end'] = .94
        self.assertNotIn('held-function-word', support.rhythm_failures(words, lambda _:1, 1.6, 5))
        self.assertIn('window', support.rhythm_failures(words, lambda _:1, 5, 5))

    def test_normalization_retains_samples_and_reconstructed_headroom(self):
        import array
        raw = array.array('h', (round(4000*math.sin(2*math.pi*220*n/24000)) for n in range(24000)))
        memory = io.BytesIO()
        with wave.open(memory,'wb') as output:
            output.setparams((1,2,24000,0,'NONE','not compressed'))
            output.writeframes(raw.tobytes())
        normalized, report = support.normalize_wave(memory.getvalue())
        with wave.open(io.BytesIO(normalized),'rb') as source:
            self.assertEqual(source.getparams()[:3], (1,2,24000))
            self.assertEqual(source.getnframes(),24000)
        self.assertLessEqual(report['reconstructed_peak_dbfs'], -1.85)
        self.assertGreater(report['gain_db'],0)
        with self.assertRaises(ValueError):
            support.normalize_wave(memory.getvalue()[:44]+b'\0'*48000)

    def test_resume_refuses_changed_inputs_and_does_not_overwrite_evidence(self):
        with tempfile.TemporaryDirectory() as temporary:
            root=Path(temporary)
            support.open_job(root, {'catalog':'a','references':['voice-a']})
            original=(root/'job.json').read_bytes()
            support.open_job(root, {'catalog':'a','references':['voice-a']})
            with self.assertRaises(ValueError):
                support.open_job(root, {'catalog':'a','references':['voice-b']})
            self.assertEqual((root/'job.json').read_bytes(), original)

    def test_reference_hash_is_checked_before_any_provider_request(self):
        with tempfile.TemporaryDirectory() as temporary:
            root=Path(temporary)
            (root/'reference.wav').write_bytes(b'original human recording')
            (root/'profile.json').write_text(json.dumps({'reference_wav':'reference.wav','reference_text':'Hello.','reference_sha256':'0'*64}))
            with self.assertRaises(ValueError):
                support.read_references([root/'profile.json'])


if __name__=='__main__': unittest.main()
