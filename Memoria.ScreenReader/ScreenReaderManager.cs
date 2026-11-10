using System;
namespace Memoria.ScreenReader
{
    public sealed class ScreenReaderManager : IDisposable
    {
        private static readonly ScreenReaderManager _instance = new ScreenReaderManager();
        private readonly object _sync = new object();
        private readonly Func<ISpeechBackend> _createBackend;
        private readonly Func<DateTime> _clock;
        private ISpeechBackend _backend;
        private DateTime _nextAttempt;
        private bool _disposed, _enabled = true;
        private long _outputSerial;
        public static ScreenReaderManager Instance { get { return _instance; } }
        private ScreenReaderManager() : this(() => new PrismSpeechBackend(), () => DateTime.UtcNow) { }
        internal ScreenReaderManager(Func<ISpeechBackend> createBackend, Func<DateTime> clock) { _createBackend = createBackend; _clock = clock; }
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
                if (OpenBackend(out failure))
                    SetDiagnostic("Prism: " + _backend.Name);
                else
                    SetDiagnostic("Prism unavailable. " + failure);
            }
        }
        private bool OpenBackend(out string failure)
        {
            ISpeechBackend backend = null;
            try {
                backend = _createBackend();
                if (backend != null && backend.Initialize()) {
                    _backend = backend;
                    SpeechGeneration++;
                    failure = null;
                    return true;
                }
                failure = "No active speech backend";
            }
            catch (Exception exception) { failure = exception.Message; }
            if (backend != null) try { backend.Dispose(); } catch (Exception exception) { failure += "; cleanup: " + exception.Message; }
            return false;
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
            if (!_enabled) return false;
            text = PrepareText(text);
            if (string.IsNullOrEmpty(text)) return false;
            lock (_sync) {
                Initialize(); if (_backend == null || _disposed) return false;
                string failure;
                bool textRejected;
                if (TryOutput(text, interrupt, out failure, out textRejected)) return true;
                if (!textRejected) {
                    DiscardBackend();
                    string reconnectFailure;
                    if (OpenBackend(out reconnectFailure)) {
                        if (TryOutput(text, interrupt, out reconnectFailure, out textRejected)) {
                            SetDiagnostic("Prism: " + _backend.Name + " (reconnected)");
                            return true;
                        }
                        if (!textRejected) DiscardBackend();
                    }
                    failure += "; reconnect: " + reconnectFailure;
                }
                SetDiagnostic("Prism output failed: " + failure);
                if (_backend == null) _nextAttempt = _clock().AddSeconds(1);
                return false;
            }
        }
        private static string PrepareText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            char[] corrected = null;
            // Unity's Mono encodes orphaned UTF-16 surrogates as invalid UTF-8.
            for (int i = 0; i < text.Length; i++) {
                char current = text[i];
                if (char.IsHighSurrogate(current) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1])) {
                    i++;
                } else if (current == '\0' || char.IsSurrogate(current)) {
                    if (corrected == null) corrected = text.ToCharArray();
                    corrected[i] = current == '\0' ? ' ' : '\uFFFD';
                }
            }
            return corrected == null ? text : new string(corrected).Trim();
        }
        private bool TryOutput(string text, bool interrupt, out string failure, out bool textRejected)
        {
            textRejected = false;
            try {
                if (_backend.Output(text, interrupt)) { _outputSerial++; LastAnnouncement = text; SpeechDiagnostics.Write("Output " + _backend.Name + ": " + text); failure = null; return true; }
                var prism = _backend as PrismSpeechBackend;
                textRejected = prism != null && prism.LastError == 13; // PRISM_ERROR_INVALID_UTF8
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
