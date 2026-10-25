using System;
using Memoria.ScreenReader;
namespace FF9Access.Tests
{
    internal static class SpeechTests
    {
        private sealed class Backend : ISpeechBackend
        {
            public bool Available = true, Accept = true; public int Initializations, Outputs, Disposals, Stops; public string Text;
            public string Name { get { return "test backend"; } }
            public bool Initialize() { Initializations++; return Available; }
            public bool Output(string text, bool interrupt) { Outputs++; if (Accept) Text = text; return Accept; }
            public void Stop() { Stops++; } public bool IsSpeaking() { return false; } public void Dispose() { Disposals++; }
        }
        public static void RepeatedInitializeOwnsOneBackend()
        {
            var backend = new Backend(); var manager = new ScreenReaderManager(() => backend, () => new Backend(), () => DateTime.UtcNow);
            manager.Initialize(); manager.Initialize(); TestAssert.Equal(1, backend.Initializations); manager.Dispose(); manager.Dispose(); TestAssert.Equal(1, backend.Disposals);
        }
        public static void AcceptedOutputSerialDistinguishesIdenticalAnnouncements()
        {
            DateTime clock=new DateTime(2026,1,1);var backend=new Backend();using(var manager=new ScreenReaderManager(()=>backend,()=>new Backend(),()=>clock)) {
                TestAssert.Equal(0L,manager.OutputSerial);TestAssert.True(manager.TrySpeak("The figure nods."));long owned=manager.OutputSerial;
                TestAssert.True(manager.TrySpeak("The figure nods."));TestAssert.True(manager.OutputSerial>owned);long last=manager.OutputSerial;
                backend.Accept=false;TestAssert.False(manager.TrySpeak("Rejected"));TestAssert.Equal(last,manager.OutputSerial);
                manager.Enabled=false;TestAssert.False(manager.TrySpeak("Disabled"));TestAssert.Equal(last,manager.OutputSerial);
            }
        }
        public static void TrySpeakReportsActualAcceptanceIncludingIdenticalRejectedText()
        {
            DateTime clock=new DateTime(2026,1,1);var backend=new Backend();
            var manager=new ScreenReaderManager(()=>backend,()=>new Backend(),()=>clock);
            TestAssert.True(manager.TrySpeak("Far."));TestAssert.Equal("Far.",manager.LastAnnouncement);
            backend.Accept=false;TestAssert.False(manager.TrySpeak("Far."));TestAssert.Equal("Far.",manager.LastAnnouncement);
            backend.Accept=true;clock=clock.AddSeconds(2);TestAssert.True(manager.TrySpeak("Far."));
            manager.Enabled=false;TestAssert.False(manager.TrySpeak("Quiet"));manager.Dispose();TestAssert.False(manager.TrySpeak("Closed"));
        }
        public static void SceneShutdownDoesNotFreeSpeech()
        {
            var backend = new Backend(); var manager = new ScreenReaderManager(() => backend, () => new Backend(), () => DateTime.UtcNow);
            manager.Initialize(); manager.Shutdown(); manager.Speak("Return to field"); TestAssert.Equal(0, backend.Disposals); TestAssert.Equal(1, backend.Outputs); manager.Dispose();
        }
        public static void PrismFailureUsesOneReportedFallback()
        {
            var primary = new Backend { Available = false }; var fallback = new Backend();
            var manager = new ScreenReaderManager(() => primary, () => fallback, () => DateTime.UtcNow);
            manager.Initialize(); manager.Speak("Town"); TestAssert.Equal(1, primary.Disposals); TestAssert.Equal(0, primary.Outputs); TestAssert.Equal(1, fallback.Outputs); TestAssert.True(manager.BackendDiagnostic.Contains("fallback")); manager.Dispose();
        }
        public static void RecoverAfterScreenReaderStarts()
        {
            DateTime clock = new DateTime(2026, 1, 1); bool available = false;
            var manager = new ScreenReaderManager(() => new Backend { Available = available }, () => new Backend { Available = false }, () => clock);
            manager.Initialize(); TestAssert.False(manager.IsInitialized); available = true; clock = clock.AddSeconds(6); manager.Speak("Ready");
            TestAssert.True(manager.IsInitialized); TestAssert.Equal("Ready", manager.LastAnnouncement); manager.Dispose();
        }
        public static void DisabledPreferenceSurvivesRecovery()
        {
            var manager = new ScreenReaderManager(() => new Backend(), () => new Backend(), () => DateTime.UtcNow); manager.Enabled = false;
            manager.Initialize(); manager.Shutdown(); manager.Initialize(); manager.Speak("Quiet"); TestAssert.False(manager.Enabled); TestAssert.True(string.IsNullOrEmpty(manager.LastAnnouncement)); manager.Dispose();
        }
        public static void Utf8AndRepeatPreserveActualAcceptedText()
        {
            var backend = new Backend(); var manager = new ScreenReaderManager(() => backend, () => new Backend(), () => DateTime.UtcNow);
            manager.Initialize(); manager.Speak("Alexandrie — entrée"); TestAssert.Equal("Alexandrie — entrée", manager.LastAnnouncement);
            backend.Accept = false; manager.Speak("Rejected"); TestAssert.Equal("Alexandrie — entrée", manager.LastAnnouncement); manager.Dispose();
        }
        public static void FailedOutputDoesNotClearEnabledPreference()
        {
            var backend = new Backend { Accept = false }; var manager = new ScreenReaderManager(() => backend, () => new Backend(), () => DateTime.UtcNow);
            manager.Initialize(); manager.Speak("Unavailable"); TestAssert.True(manager.Enabled); TestAssert.True(manager.BackendDiagnostic.Contains("failed")); manager.Dispose();
        }
    }
}
