namespace Memoria.ScreenReader
{
    public sealed class TolkSpeechBackend : ISpeechBackend
    {
        private readonly Tolk.Tolk _tolk = new Tolk.Tolk(); private bool _loaded;
        public string Name { get { return "Tolk " + (_tolk.DetectScreenReader() ?? "SAPI"); } }
        public bool Initialize() { if (!_loaded) { _tolk.TrySAPI(true); _tolk.Load(); _loaded = true; } return _tolk.IsLoaded() && (_tolk.HasSpeech() || _tolk.HasBraille()); }
        public bool Output(string text, bool interrupt) { return _tolk.Output(text, interrupt); }
        public void Stop() { _tolk.Silence(); } public bool IsSpeaking() { return _tolk.IsSpeaking(); }
        public void Dispose() { if (_loaded) { _tolk.Unload(); _loaded = false; } }
    }
}
