"""Offline narration loudness mastering. Never changes game or device volume."""
import array
import math
import subprocess
import sys

RATE = 24000
PEAK_DBFS = -1.5


def pcm_metrics(data):
    if not data or len(data) % 2:
        raise ValueError('Expected nonempty little-endian PCM16 samples')
    samples = array.array('h')
    samples.frombytes(data)
    if sys.byteorder != 'little':
        samples.byteswap()
    peak = max(abs(value) for value in samples)
    rms = math.sqrt(sum(float(value) ** 2 for value in samples) / len(samples))
    return {
        'samples': len(samples),
        'peak_dbfs': 20 * math.log10(peak / 32768) if peak else -math.inf,
        'rms_dbfs': 20 * math.log10(rms / 32768) if rms else -math.inf,
        'clipped_samples': sum(abs(value) >= 32767 for value in samples),
    }


def reconstructed_peak_dbfs(data, rate=192000):
    decoded = subprocess.run([
        'ffmpeg', '-v', 'error', '-nostdin', '-f', 's16le', '-ar', str(RATE), '-ac', '1',
        '-i', 'pipe:0', '-af', f'aresample={rate}', '-f', 'f64le', '-threads', '1', 'pipe:1',
    ], input=data, capture_output=True, check=True).stdout
    samples = array.array('d'); samples.frombytes(decoded)
    if sys.byteorder != 'little': samples.byteswap()
    peak = max(abs(value) for value in samples)
    return 20 * math.log10(peak) if peak else -math.inf


def master_pcm(data, opening=False):
    before = pcm_metrics(data)
    if before['peak_dbfs'] < -60:
        raise ValueError('Silent narration cannot be mastered')
    if not opening:
        return data
    # Raise the existing opening takes by 10 dB before peak protection.
    # Short utterances do not give EBU's long-term estimator a stable basis;
    # a bounded gain preserves their existing relative speech levels. This is
    # rendering only: no live processing or extra battle-time work.
    ceiling = 10 ** (-1.8 / 20)
    # Oversampling protects peaks reconstructed by 44.1/48 kHz output devices.
    # Keep a little extra headroom for the final 24 kHz conversion. Makeup is
    # disabled and latency compensated; no speech timing or pitch is changed.
    result = subprocess.run([
        'ffmpeg', '-v', 'error', '-nostdin', '-f', 's16le', '-ar', str(RATE),
        '-ac', '1', '-i', 'pipe:0', '-af',
        f'volume=10dB,aresample=192000,alimiter=limit={ceiling:.9f}:attack=1:release=20:level=false:latency=true,aresample={RATE}',
        '-ac', '1', '-ar', str(RATE), '-f', 's16le', '-threads', '1', 'pipe:1',
    ], input=data, capture_output=True, check=True).stdout
    if len(result) != len(data):
        raise ValueError('Mastering changed narration duration')
    after = pcm_metrics(result)
    if after['clipped_samples'] or after['peak_dbfs'] > PEAK_DBFS + .15:
        raise ValueError(f"Mastered narration exceeds peak headroom: {after['peak_dbfs']:.3f} dBFS")
    if reconstructed_peak_dbfs(result) > -1.0:
        raise ValueError('Mastered narration exceeds reconstructed peak headroom')
    return result
