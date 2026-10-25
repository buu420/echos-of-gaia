"""Bank membership and append preservation contract (no provider calls)."""
import copy
import importlib.util
import json
import os
import tempfile
import unittest
import wave
from pathlib import Path
from unittest.mock import patch

spec = importlib.util.spec_from_file_location('build_pack_banks', Path(__file__).with_name('build_pack.py'))
pack = importlib.util.module_from_spec(spec)
spec.loader.exec_module(pack)


class FieldBankTests(unittest.TestCase):
    def metadata(self, count=513):
        return {f'field-{i}': {'bytes': 46} for i in range(count)}

    def test_explicit_banks_remove_members_from_legacy_generic_budget(self):
        clips = self.metadata()
        pack.check_bank_budgets([], clips, [{'id': 'chapter-one', 'fields': [50, 51], 'clips': ['field-512']}])

    def test_bad_membership_cannot_select_a_movie_or_share_a_map_or_clip(self):
        good = {'id': 'chapter-one', 'fields': [50], 'clips': ['field-0']}
        cases = [
            [dict(good, clips=['missing'])],
            [dict(good, fields=[50, 50])],
            [dict(good, fields=[True])],
            [dict(good, fields=['50'])],
            [dict(good, fields=[-1])],
            [dict(good, fields=[2147483648])],
            [dict(good, fields=[])],
            [dict(good, clips=[])],
            [dict(good, clips=['field-0', 'field-0'])],
            [good, dict(good, id='chapter-two', fields=[51])],
            [good, dict(good, id='chapter-two', clips=['field-1'])],
            [good, dict(good, fields=[51], clips=['field-1'])],
        ]
        for banks in cases:
            with self.subTest(banks=banks), self.assertRaises(ValueError):
                pack.check_bank_budgets([], self.metadata(2), banks)
        with self.assertRaises(ValueError):
            pack.check_bank_budgets([{'cues': [{'id': 'field-0'}]}], self.metadata(2), [good])

    def test_global_limit_is_independent_of_each_runtime_bank(self):
        clips = self.metadata(4097)
        banks = [{'id': f'group-{n}', 'fields': [n], 'clips': [f'field-{i}' for i in range(n*512, min((n+1)*512, 4097))]} for n in range(9)]
        with self.assertRaises(ValueError):
            pack.check_bank_budgets([], clips, banks)
        del clips['field-4096']; banks.pop()
        pack.check_bank_budgets([], clips, banks)
        with self.assertRaises(ValueError):
            pack.check_bank_budgets([], self.metadata(), [{'id': 'too-many', 'fields': [1], 'clips': list(self.metadata())}])
        with self.assertRaises(ValueError):
            pack.check_bank_budgets([], {f'field-{i}': {'bytes': 2097152} for i in range(17)},
                                    [{'id': 'too-big', 'fields': [1], 'clips': [f'field-{i}' for i in range(17)]}])

    def fixture(self, root):
        source = root/'baseline'; source.mkdir()
        data_path = source/'old-movie.wav'
        with wave.open(str(data_path), 'wb') as out:
            out.setparams((1, 2, 24000, 0, 'NONE', 'not compressed')); out.writeframes(b'\x00\x10'*4800)
        data = data_path.read_bytes()
        rows = [{'id': name, 'file': name+'.wav', 'text': 'A bird flies.', 'bytes': len(data),
                 'sha256': pack.digest(data), 'duration': .2} for name in ('old-movie', 'old-field')]
        (source/'old-field.wav').write_bytes(data)
        films = [{'movie': f'FMV{i:03}', 'duration': 5, 'sourceSHA256': '0'*64, 'sourceBytes': 100,
                  'cues': [{'id': 'old-movie', 'start': 0, 'end': 4}]} for i in range(42)]
        manifest = {'schema': 1, 'voice': 'Approved narrator', 'format': '24000 Hz mono canonical PCM16 WAV',
                    'films': films, 'clips': rows, 'openingMastering': {'version': 1, 'gain': 1.5}}
        (source/'manifest.json').write_text(json.dumps(manifest))
        voice = root/'voice'; voice.mkdir()
        (voice/'new-field.wav').write_bytes(data)
        (voice/'new-field.json').write_text(json.dumps({'id': 'new-field', 'text': 'A bird flies.',
            'wave_sha256': pack.digest(data), 'checks': {'exact': True, 'failures': [], 'asr_text': 'A bird flies.'}}))
        return source, voice, manifest, data

    def test_append_cli_keeps_old_audio_movie_rows_mastering_and_private_inputs(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp); source, voice, before, data = self.fixture(root)
            script = root/'append.json'; script.write_text(json.dumps({'cues': [{'cueId': 'new-field', 'voiceText': 'A bird flies.'}]}))
            banks = root/'banks.json'; banks.write_text(json.dumps([{'id': 'chapter-one', 'fields': [50], 'clips': ['new-field']}]))
            output, catalog, qa = root/'public', root/'catalog.json', root/'qa.json'
            baseline = (source/'manifest.json').read_bytes()
            argv = ['build_pack', '--append-fields', str(script), '--replace-from', str(source), '--voice-dir', str(voice),
                    '--out', str(output), '--catalog', str(catalog), '--qa', str(qa), '--field-banks', str(banks)]
            with patch('sys.argv', argv): pack.main()
            result = pack.validate_pack(output)
            self.assertEqual(result['films'], before['films'])
            self.assertEqual(result['clips'][:2], before['clips'])
            self.assertEqual(result['voice'], before['voice'])
            self.assertEqual(result['openingMastering'], before['openingMastering'])
            self.assertEqual((source/'manifest.json').read_bytes(), baseline)
            for name in ('old-movie.wav', 'old-field.wav'):
                self.assertEqual((output/name).read_bytes(), data)
            self.assertEqual(catalog.read_bytes(), (output/'manifest.json').read_bytes())
            self.assertEqual(set(p.name for p in output.iterdir()), {'manifest.json', 'old-movie.wav', 'old-field.wav', 'new-field.wav'})
            self.assertEqual([row['id'] for row in json.loads(qa.read_text())], ['new-field'])

    def test_public_validator_accepts_more_than_512_clips_split_into_bounded_banks(self):
        with tempfile.TemporaryDirectory() as temp:
            source, _, manifest, data = self.fixture(Path(temp))
            members = []
            for n in range(512):
                cue_id = f'new-{n}'
                row = dict(manifest['clips'][1], id=cue_id, file=cue_id+'.wav')
                manifest['clips'].append(row); members.append(cue_id)
                (source/row['file']).write_bytes(data)
            manifest['fieldBanks'] = [{'id': 'group', 'fields': [1], 'clips': members}]
            (source/'manifest.json').write_text(json.dumps(manifest))
            self.assertEqual(len(pack.validate_pack(source)['clips']), 514)
            manifest['fieldBanks'][0]['clips'].append('old-field')
            (source/'manifest.json').write_text(json.dumps(manifest))
            with self.assertRaises(ValueError): pack.validate_pack(source)

    def test_bank_metadata_cannot_carry_private_properties_or_invalid_clip_sizes(self):
        with self.assertRaises(ValueError):
            pack.check_bank_budgets([], self.metadata(1), [{'id': 'group', 'fields': [1], 'clips': ['field-0'], 'reference': 'private.wav'}])
        for size in (0, -100, 2097153, True):
            with self.subTest(size=size), self.assertRaises(ValueError):
                pack.check_bank_budgets([], {'field-0': {'bytes': size}})

    def test_append_cli_rejects_private_qa_and_immutable_aliases_before_output(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp); source, voice, _, _ = self.fixture(root)
            script = root/'append.json'; script.write_text(json.dumps({'cues': [{'cueId': 'new-field', 'voiceText': 'A bird flies.'}]}))
            banks = root/'banks.json'; banks.write_text(json.dumps([{'id': 'group', 'fields': [1], 'clips': ['new-field']}]))
            alias = root/'linked-catalog.json'; os.link(voice/'new-field.json', alias)
            baseline = (voice/'new-field.json').read_bytes()
            cases = [(root/'catalog.json', source/'qa.json'), (root/'catalog.json', voice/'qa.json'),
                     (alias, root/'qa.json'), (script, root/'qa.json'),
                     (root/'catalog.json', root/'catalog.json')]
            for n, (catalog, qa) in enumerate(cases):
                output = root/f'output-{n}'
                argv = ['build_pack', '--append-fields', str(script), '--replace-from', str(source), '--voice-dir', str(voice),
                        '--out', str(output), '--catalog', str(catalog), '--qa', str(qa), '--field-banks', str(banks)]
                with self.subTest(n=n), patch('sys.argv', argv), patch('sys.stderr'), self.assertRaises(SystemExit):
                    pack.main()
                self.assertFalse(output.exists())
            self.assertEqual((voice/'new-field.json').read_bytes(), baseline)

    def test_append_rejects_existing_ids_duplicate_ids_or_unbanked_new_cues_before_writing(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp); source, voice, _, _ = self.fixture(root)
            good = {'cueId': 'new-field', 'voiceText': 'A bird flies.'}
            cases = [([{'cueId': 'old-field', 'voiceText': 'A bird flies.'}], []), ([good, good], []), ([good], [])]
            for n, (cues, banks) in enumerate(cases):
                with self.subTest(n=n), self.assertRaises(ValueError):
                    pack.append_fields(source, {'cues': cues}, [voice], root/f'out-{n}', banks)
                self.assertFalse((root/f'out-{n}').exists())


if __name__ == '__main__': unittest.main()
