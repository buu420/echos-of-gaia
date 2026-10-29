using System;
using System.IO;
namespace Memoria.ScreenReader
{
    public sealed class PrismSpeechBackend : ISpeechBackend
    {
        private PrismNative _native; private readonly string _path;
        public PrismSpeechBackend() : this(Path.Combine(Path.GetDirectoryName(typeof(PrismSpeechBackend).Assembly.Location), "prism.dll")) { }
        public PrismSpeechBackend(string absolutePath) { _path = absolutePath; }
        public string Name { get { return _native == null ? "Prism" : "Prism " + _native.Name; } }
        public int LastError { get { return _native == null ? 0 : _native.LastError; } }
        public bool Initialize() { if (_native == null) _native = new PrismNative(_path); return _native.SelectBackend(); }
        public bool Output(string text, bool interrupt) { return _native != null && _native.Output(text, interrupt); }
        public void Stop() { if (_native != null) _native.Stop(); } public bool IsSpeaking() { return _native != null && _native.IsSpeaking(); }
        public void Dispose() { if (_native != null) { _native.Dispose(); _native = null; } }
    }
}
