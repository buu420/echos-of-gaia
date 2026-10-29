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
        private bool _disposed, _enabled = true, _usingFallback;
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
                _usingFallback = false;
                if (_backend != null) SetDiagnostic("Prism: " + _backend.Name);
                else {
                    string fallbackFailure;
                    _backend = TryCreate(_fallback, out fallbackFailure);
                    _usingFallback = true;
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
                string failure;
                if (TryOutput(text, interrupt, out failure)) return true;
                bool tryFallback = !_usingFallback;
                DiscardBackend();
                // Availability does not prove delivery. A preferred backend can initialize
                // successfully while every output fails; do not keep reselecting it and
                // leave the same menu selection unread when compatibility output works.
                if (tryFallback) {
                    string fallbackFailure;
                    _backend = TryCreate(_fallback, out fallbackFailure);
                    _usingFallback = true;
                    if (_backend != null) {
                        SpeechGeneration++;
                        SetDiagnostic("Speech compatibility fallback: " + _backend.Name + ". Prism output failed: " + failure);
                        if (TryOutput(text, interrupt, out fallbackFailure)) return true;
                        DiscardBackend();
                    }
                    failure += "; compatibility failed: " + fallbackFailure;
                }
                SetDiagnostic("Speech output failed: " + failure);
                _nextAttempt = _clock().AddSeconds(1);
                return false;
            }
        }
        private bool TryOutput(string text, bool interrupt, out string failure)
        {
            try {
                if (_backend.Output(text, interrupt)) { _outputSerial++; LastAnnouncement = text; SpeechDiagnostics.Write("Output " + _backend.Name + ": " + text); failure = null; return true; }
                var prism = _backend as PrismSpeechBackend;
                failure = _backend.Name + (prism == null ? "" : " (Prism error " + prism.LastError + ")");
            } catch (Exception exception) { failure = _backend.Name + ": " + exception.Message; }
            SpeechDiagnostics.Write("Speech output failed: " + failure);
            return false;
        }
        private void DiscardBackend()
        {
            try { _backend.Dispose(); } catch (Exception exception) { SpeechDiagnostics.Write("Speech cleanup: " + exception.Message); }
            _backend = null;
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
