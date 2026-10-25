using System;
namespace Memoria.ScreenReader
{
    public sealed class ScreenReaderManager : IDisposable
    {
        private static readonly ScreenReaderManager _instance = new ScreenReaderManager();
        private readonly object _sync = new object();
        private readonly Func<ISpeechBackend> _preferred, _fallback;
        private readonly Func<DateTime> _clock;
        private ISpeechBackend _backend;
        private DateTime _nextAttempt;
        private bool _disposed, _enabled = true;
        private long _outputSerial;
        public static ScreenReaderManager Instance { get { return _instance; } }
        private ScreenReaderManager() : this(() => new PrismSpeechBackend(), () => new TolkSpeechBackend(), () => DateTime.UtcNow) { }
        internal ScreenReaderManager(Func<ISpeechBackend> preferred, Func<ISpeechBackend> fallback, Func<DateTime> clock) { _preferred = preferred; _fallback = fallback; _clock = clock; }
        public string LastAnnouncement { get; private set; }
        public string BackendDiagnostic { get; private set; }
        public long SpeechGeneration { get; private set; }
        public long OutputSerial { get { lock (_sync) return _outputSerial; } }
        public string DiagnosticWriteFailure { get { return SpeechDiagnostics.LastWriteFailure; } }
        public string DiagnosticLogPath { get { return SpeechDiagnostics.LogPath; } }
        public bool Enabled { get { return _enabled; } set { _enabled = value; } }
        public bool IsInitialized { get { return _backend != null; } }
        public void Initialize()
        {
            lock (_sync) {
                if (_disposed || _backend != null || _clock() < _nextAttempt) return;
                _nextAttempt = _clock().AddSeconds(5);
                string failure;
                _backend = TryCreate(_preferred, out failure);
                if (_backend != null) SetDiagnostic("Prism: " + _backend.Name);
                else {
                    string fallbackFailure;
                    _backend = TryCreate(_fallback, out fallbackFailure);
                    SetDiagnostic(_backend == null ? "Speech unavailable. " + failure + "; " + fallbackFailure : "Speech compatibility fallback: " + _backend.Name + ". Prism unavailable: " + failure);
                }
                if (_backend != null) SpeechGeneration++;
            }
        }
        private static ISpeechBackend TryCreate(Func<ISpeechBackend> factory, out string failure)
        {
            ISpeechBackend backend = null;
            try { backend = factory(); if (backend != null && backend.Initialize()) { failure = null; return backend; } failure = "No active speech backend"; }
            catch (Exception exception) { failure = exception.Message; }
            if (backend != null) try { backend.Dispose(); } catch (Exception exception) { failure += "; cleanup: " + exception.Message; }
            return null;
        }
        private void SetDiagnostic(string message) { BackendDiagnostic = message; SpeechDiagnostics.Write(message); }
        // Existing scene callers detach here. Only process quit owns native disposal.
        public void Shutdown() { }
        public void Speak(string text, bool interrupt = false)
        {
            TrySpeak(text,interrupt);
        }
        public bool TrySpeak(string text,bool interrupt=false)
        {
            if (!_enabled || string.IsNullOrEmpty(text)) return false;
            lock (_sync) {
                Initialize(); if (_backend == null || _disposed) return false;
                try {
                    if (_backend.Output(text, interrupt)) { _outputSerial++; LastAnnouncement = text; SpeechDiagnostics.Write("Output " + _backend.Name + ": " + text); return true; }
                    SetDiagnostic("Speech output failed: " + _backend.Name);
                } catch (Exception exception) { SetDiagnostic("Speech output failed: " + exception.Message); }
                try { _backend.Dispose(); } catch (Exception exception) { SpeechDiagnostics.Write("Speech cleanup: " + exception.Message); }
                _backend = null; _nextAttempt = _clock().AddSeconds(1);
                return false;
            }
        }
        public void SpeakInterrupt(string text) { Speak(text, true); }
        public void Silence() { lock (_sync) if (_backend != null) try { _backend.Stop(); } catch (Exception e) { SetDiagnostic("Speech stop failed: " + e.Message); } }
        public bool IsSpeaking() { lock (_sync) { if (_backend == null) return false; try { return _backend.IsSpeaking(); } catch (Exception e) { SetDiagnostic("Speech status unavailable: " + e.Message); return false; } } }
        public bool IsScreenReaderActive() { Initialize(); return _backend != null; }
        public string GetScreenReaderName() { return _backend == null ? null : _backend.Name; }
        public void Dispose()
        {
            lock (_sync) {
                if (_disposed) return; _disposed = true;
                if (_backend != null) try { _backend.Dispose(); } catch (Exception e) { SetDiagnostic("Speech final cleanup failed: " + e.Message); }
                _backend = null;
            }
        }
    }
}
