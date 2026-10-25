import hashlib
import importlib.util
import json
import os
import tempfile
import unittest
from unittest.mock import patch
import wave
from pathlib import Path

SPEC = importlib.util.spec_from_file_location('build_pack', Path(__file__).with_name('build_pack.py'))
pack = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(pack)


class PackTests(unittest.TestCase):
    def test_replacement_preserves_native_metadata_and_source_bytes(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp); voice=self.fixture(root)
            original=root/'original'; original.mkdir()
            row,data,_=pack.prepare_clip({'id':'test-cue','text':'A bird flies.'},[voice],{})
            (original/row['file']).write_bytes(data)
            films=[{'movie':f'FMV{i:03}','duration':5,'sourceSHA256':'0'*64,'sourceBytes':100,
                    'cues':[{'id':'test-cue','start':0,'end':4}]} for i in range(42)]
            manifest={'schema':1,'voice':'Previous provider','format':'24000 Hz mono canonical PCM16 WAV',
                      'clips':[row],'films':films,'openingMastering':{'version':1}}
            source=json.dumps(manifest).encode(); (original/'manifest.json').write_bytes(source)
            output=root/'replacement'
            result,_=pack.replace_pack(original,[voice],output,'Personal narrator, Fish Audio S2.1 Pro')
            self.assertEqual(result['films'],films)
            self.assertEqual(result['voice'],'Personal narrator, Fish Audio S2.1 Pro')
            self.assertNotIn('openingMastering',result)
            self.assertEqual((original/'manifest.json').read_bytes(),source)
            self.assertEqual((original/row['file']).read_bytes(),data)
            pack.validate_pack(output)
            with patch('sys.argv',['build_pack','--replace-from',str(original),'--voice-dir',str(voice),
                       '--out',str(root/'cli-output'),'--qa',str(root/'cli-qa.json'),
                       '--voice-description','Personal narrator, Fish Audio S2.1 Pro']):
                pack.main()
            self.assertEqual(pack.validate_pack(root/'cli-output')['voice'],result['voice'])
            with patch('sys.argv',['build_pack','--replace-from',str(original),'--voice-dir',str(voice),
                       '--out',str(root/'unsafe-cli-output'),'--qa',str(voice/'private-qa.json'),
                       '--voice-description','Fish']), patch('sys.stderr'):
                with self.assertRaises(SystemExit): pack.main()
            self.assertFalse((voice/'private-qa.json').exists())
            linked_qa=root/'linked-private-qa.json'
            os.link(original/'manifest.json',linked_qa)
            with patch('sys.argv',['build_pack','--replace-from',str(original),'--voice-dir',str(voice),
                       '--out',str(root/'hardlink-output'),'--qa',str(linked_qa),
                       '--voice-description','Fish']), patch('sys.stderr'):
                with self.assertRaises(SystemExit): pack.main()
            self.assertEqual((original/'manifest.json').read_bytes(),source)
            self.assertFalse((root/'hardlink-output').exists())
            with self.assertRaises(ValueError): pack.replace_pack(original,[voice],original,'Fish')
            meta=json.loads((voice/'test-cue.json').read_text())
            meta['checks']['failures']=['timing']; (voice/'test-cue.json').write_text(json.dumps(meta))
            with self.assertRaises(ValueError): pack.replace_pack(original,[voice],root/'invalid','Fish')
            self.assertFalse((root/'invalid').exists())

    def fixture(self, root):
        voice = root / 'voice'; voice.mkdir()
        target = voice / 'test-cue.wav'
        with wave.open(str(target), 'wb') as out:
            out.setparams((1, 2, 24000, 0, 'NONE', 'not compressed'))
            out.writeframes(b'\x00\x00' * 2400 + b'\x00\x10' * 4800 + b'\x00\x00' * 7200)
        data = target.read_bytes()
        (voice / 'test-cue.json').write_text(json.dumps({
            'id': 'test-cue', 'text': 'A bird flies.', 'wave_sha256': hashlib.sha256(data).hexdigest(),
            'checks': {'asr_text': 'A bird flies.', 'exact': True, 'failures': []}}))
        return voice

    def test_canonical_conversion_preserves_speech_and_pins_output(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp); voice = self.fixture(root)
            row, data, qa = pack.prepare_clip({'id': 'test-cue', 'text': 'A bird flies.'}, [voice], {})
            self.assertEqual(data[36:40], b'data')
            self.assertEqual(row['bytes'], len(data))
            self.assertEqual(row['sha256'], hashlib.sha256(data).hexdigest())
            self.assertLessEqual(row['duration'], .6)
            self.assertIn(b'\x00\x10' * 4800, data)
            self.assertGreater(qa['trimmed_tail_seconds'], 0)

    def test_wrong_text_or_changed_generation_cannot_ship(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp); voice = self.fixture(root)
            with self.assertRaises(ValueError):
                pack.prepare_clip({'id': 'test-cue', 'text': 'A monster flies.'}, [voice], {})
            target = voice / 'test-cue.wav'; target.write_bytes(target.read_bytes() + b'changed')
            with self.assertRaises(ValueError):
                pack.prepare_clip({'id': 'test-cue', 'text': 'A bird flies.'}, [voice], {})

    def test_failed_word_check_requires_exact_hash_review(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp); voice = self.fixture(root)
            target = voice / 'test-cue.json'; meta = json.loads(target.read_text())
            meta['checks'] = {'exact': False, 'failures': ['words'], 'asr_text': 'A bird.'}
            target.write_text(json.dumps(meta))
            with self.assertRaises(ValueError):
                pack.prepare_clip({'id': 'test-cue', 'text': 'A bird flies.'}, [voice], {})
            overrides = {'test-cue': {'wave_sha256': meta['wave_sha256'], 'reason': 'reviewed exact source'}}
            pack.prepare_clip({'id': 'test-cue', 'text': 'A bird flies.'}, [voice], overrides)
            overrides['test-cue']['wave_sha256'] = '0' * 64
            with self.assertRaises(ValueError):
                pack.prepare_clip({'id': 'test-cue', 'text': 'A bird flies.'}, [voice], overrides)

    def test_movie_windows_reject_overlap_and_voice_spilling_into_next_shot(self):
        clips = {'one': {'duration': 2.1}, 'two': {'duration': 1.0}}
        with self.assertRaises(ValueError):
            pack.check_timeline({'movie': 'FMV000', 'duration': 10, 'cues': [{'id': 'one', 'start': 0, 'end': 2}]}, clips)
        with self.assertRaises(ValueError):
            pack.check_timeline({'movie': 'FMV000', 'duration': 10, 'cues': [{'id': 'one', 'start': 0, 'end': 3}, {'id': 'two', 'start': 2, 'end': 4}]}, clips)
        with self.assertRaises(ValueError):
            pack.check_timeline({'movie': 'FMV000', 'duration': 10, 'cues': [{'id': 'two', 'start': 9.5, 'end': 10.5}]}, clips)

    def test_known_recognizer_spelling_equivalence_is_not_an_audio_word_change(self):
        self.assertTrue(pack.equivalent_words('Final Fantasy Nine.', 'Final Fantasy IX'))
        self.assertTrue(pack.equivalent_words('The blond boy and knight.', 'The blonde boy and night.'))
        self.assertTrue(pack.equivalent_words('The dark-haired girl.', 'The dark haired girl.'))
        self.assertTrue(pack.equivalent_words('Cannons flash.', 'Canons flash.'))
        self.assertFalse(pack.equivalent_words('A knight arrives.', 'A king arrives.'))
        self.assertFalse(pack.equivalent_words('An illuminated theater.', 'and illuminated theater.'))

    def test_each_runtime_bank_must_fit_its_actual_memory_budget(self):
        clips = {str(i): {'bytes': 2097152} for i in range(17)}
        with self.assertRaises(ValueError):
            pack.check_bank_budgets([], clips)
        with self.assertRaises(ValueError):
            pack.check_bank_budgets([{'cues': [{'id': i} for i in clips]}], clips)
        # Movie banks replace each other; their combined size is not resident.
        pack.check_bank_budgets([{'cues': [{'id': i}]} for i in clips], clips)


if __name__ == '__main__': unittest.main()
