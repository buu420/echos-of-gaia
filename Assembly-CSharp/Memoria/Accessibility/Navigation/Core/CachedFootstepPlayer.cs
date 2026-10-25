using System;
namespace Memoria.Accessibility.Navigation
{
    // Cache a small sample set once. WarmOne is called during world loading;
    // Play never reads files, decodes audio, creates an engine, or starts threads.
    public sealed class CachedFootstepPlayer
    {
        private readonly int[] _banks, _voices = new int[4];
        private readonly Func<int, int> _load;
        private readonly Func<int, float, int> _play;
        private readonly Action<int> _stop;
        private readonly Action<string> _report;
        private int _loaded, _nextSample, _nextVoice;
        private bool _reported, _failed;
        public CachedFootstepPlayer(int sampleCount, Func<int, int> load, Func<int, float, int> play, Action<int> stop, Action<string> report)
        {
            if (sampleCount < 1 || sampleCount > 32 || load == null || play == null || stop == null) throw new ArgumentException("Invalid footstep sample bank");
            _banks = new int[sampleCount]; _load = load; _play = play; _stop = stop; _report = report;
        }
        public void WarmOne()
        {
            if (_failed || _loaded == _banks.Length) return;
            int index = _loaded++;
            try { _banks[index] = _load(index); if (_banks[index] <= 0) Report("Footstep sample could not be loaded"); }
            catch (Exception exception) { Report(exception.Message); }
        }
        public bool Play(float gain)
        {
            if (_failed || gain <= 0 || float.IsNaN(gain) || float.IsInfinity(gain)) return false;
            for (int attempt = 0; attempt < _banks.Length; attempt++)
            {
                int sample = _nextSample; _nextSample = (_nextSample + 1) % _banks.Length;
                if (_banks[sample] <= 0) continue;
                StopSlot(_nextVoice);
                if (_failed) return false;
                try
                {
                    int voice = _play(_banks[sample], Math.Min(4, gain));
                    if (voice <= 0)
                    {
                        // Treat a rejected native voice like a thrown backend fault:
                        // report once and avoid repeated silent playback attempts.
                        _failed = true; Report("World footstep playback was rejected by the audio backend"); Stop(); return false;
                    }
                    _voices[_nextVoice] = voice; _nextVoice = (_nextVoice + 1) % _voices.Length;
                    return true;
                }
                catch (Exception exception) { _failed = true; Report(exception.Message); Stop(); return false; }
            }
            return false;
        }
        public void Stop()
        {
            for (int i = 0; i < _voices.Length; i++) StopSlot(i);
            _nextVoice = 0;
        }
        private void StopSlot(int index)
        {
            int voice = _voices[index]; _voices[index] = 0;
            if (voice <= 0) return;
            try { _stop(voice); }
            catch (Exception exception) { _failed = true; Report(exception.Message); }
        }
        private void Report(string message)
        {
            if (_reported) return; _reported = true;
            try { if (_report != null) _report(message); } catch (Exception) { }
        }
    }
}
