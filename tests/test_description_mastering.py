"""Signal-level regression checks for the offline narration mastering stage."""
import array
import importlib.util
import math
import shutil
import sys
import hashlib
import json
import tempfile
import subprocess
import unittest
import wave
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'tools' / 'audio_descriptions'))
from mastering import master_pcm, pcm_metrics


class NarrationMasteringTests(unittest.TestCase):
    def tone(self, level, seconds=2):
        return array.array('h', (round(level * math.sin(2 * math.pi * 220 * n / 24000))
                                 for n in range(round(seconds * 24000)))).tobytes()

    def test_opening_is_substantially_louder_without_changing_timing(self):
        quiet = self.tone(3000)
        ordinary = master_pcm(quiet, opening=False)
        opening = master_pcm(quiet, opening=True)
        self.assertEqual(len(quiet), len(ordinary))
        self.assertEqual(len(quiet), len(opening))
        self.assertGreater(pcm_metrics(opening)['rms_dbfs'] - pcm_metrics(ordinary)['rms_dbfs'], 4.5)

    def test_loud_input_retains_headroom_and_finite_signal(self):
        result = master_pcm(self.tone(32760), opening=True)
        metrics = pcm_metrics(result)
        self.assertLessEqual(metrics['peak_dbfs'], -1.35)
        self.assertEqual(metrics['clipped_samples'], 0)
        self.assertGreater(metrics['rms_dbfs'], -20)

    def test_resampling_speech_retains_peak_headroom(self):
        source = ROOT / 'tests' / 'fixtures' / 'audio' / 'original-ff9-fmv000-004.wav'
        data = source.read_bytes()[44:]
        result = master_pcm(data, opening=True)
        self.assertEqual(len(result), len(data))
        self.assertLessEqual(pcm_metrics(result)['peak_dbfs'], -1.35)
        for rate in (44100, 48000, 192000):
            decoded = subprocess.run(['ffmpeg', '-v', 'error', '-f', 's16le', '-ar', '24000', '-ac', '1',
                                      '-i', 'pipe:0', '-af', f'aresample={rate}', '-f', 'f64le', 'pipe:1'],
                                     input=result, capture_output=True, check=True).stdout
            samples = array.array('d'); samples.frombytes(decoded)
            if sys.byteorder != 'little': samples.byteswap()
            self.assertLess(20 * math.log10(max(abs(value) for value in samples)), -1.0)

    def test_short_opening_lines_get_the_same_audible_level_increase(self):
        source = ROOT / 'tests' / 'fixtures' / 'audio' / 'original-ff9-fmv001-004.wav'
        data = source.read_bytes()[44:]
        result = master_pcm(data, opening=True)
        self.assertGreater(pcm_metrics(result)['rms_dbfs'] - pcm_metrics(data)['rms_dbfs'], 5.5)

    def test_silent_and_malformed_recordings_are_rejected(self):
        for data in (b'', b'\0', b'\0' * 96000):
            with self.assertRaises(ValueError):
                master_pcm(data, opening=True)

    def test_pack_revision_preserves_originals_and_other_recordings(self):
        from master_pack import master_pack
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source = root / 'source'
            source.mkdir()
            clips = []
            for identity in ('opening', 'ordinary'):
                path = source / (identity + '.wav')
                with wave.open(str(path), 'wb') as output:
                    output.setparams((1, 2, 24000, 0, 'NONE', 'not compressed'))
                    output.writeframes(self.tone(3000))
                data = path.read_bytes()
                clips.append({'id': identity, 'text': 'A visible landscape.', 'file': path.name,
                              'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest(), 'duration': 2.0})
            films = [{'movie': f'FMV{i:03}', 'duration': 5.0, 'sourceSHA256': '0' * 64,
                      'sourceBytes': 100, 'cues': [{'id': 'opening' if i < 2 else 'ordinary',
                                                 'start': 0.0, 'end': 4.0}]} for i in range(42)]
            manifest = {'schema': 1, 'voice': 'Original narrator', 'clips': clips, 'films': films}
            (source / 'manifest.json').write_text(json.dumps(manifest))
            output = root / 'output'
            result, report = master_pack(source, output)
            self.assertEqual(result['voice'], manifest['voice'])
            self.assertEqual(result['films'], films)
            self.assertEqual(result['clips'][0]['duration'], 2.0)
            self.assertNotEqual(result['clips'][0]['sha256'], clips[0]['sha256'])
            self.assertEqual((output / 'ordinary.wav').read_bytes(), (source / 'ordinary.wav').read_bytes())
            self.assertEqual(hashlib.sha256((source / 'opening.wav').read_bytes()).hexdigest(), clips[0]['sha256'])
            self.assertEqual(len(report), 1)
            with self.assertRaises(ValueError):
                master_pack(output, root / 'processed-twice')


if __name__ == '__main__':
    unittest.main()
