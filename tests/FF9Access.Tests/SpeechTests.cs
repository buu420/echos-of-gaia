using System;
using Memoria.ScreenReader;
namespace FF9Access.Tests
{
    internal static class SpeechTests
    {
        private sealed class Backend : ISpeechBackend
        {
            public bool Available = true, Accept = true, ThrowOnOutput, LastInterrupt; public int Initializations, Outputs, Disposals, Stops; public string Text;
            public string Name { get { return "test backend"; } }
            public bool Initialize() { Initializations++; return Available; }
            public bool Output(string text, bool interrupt) { Outputs++; LastInterrupt = interrupt; if (ThrowOnOutput) throw new InvalidOperationException("Native output rejected"); if (Accept) Text = text; return Accept; }
            public void Stop() { Stops++; } public bool IsSpeaking() { return false; } public void Dispose() { Disposals++; }
        }
        public static void InitializedPrimaryRejectionDeliversThroughCompatibilityOnce()
        {
            var primary = new Backend { Accept = false }; var fallback = new Backend();
            using (var manager = new ScreenReaderManager(() => primary, () => fallback, () => DateTime.UtcNow)) {
                TestAssert.True(manager.TrySpeak("Item"));
                TestAssert.Equal("Item", manager.LastAnnouncement); TestAssert.Equal(1L, manager.OutputSerial);
                TestAssert.Equal(1, primary.Outputs); TestAssert.Equal(1, primary.Disposals);
                TestAssert.Equal(1, fallback.Initializations); TestAssert.Equal(1, fallback.Outputs); TestAssert.False(fallback.LastInterrupt);
                TestAssert.True(manager.BackendDiagnostic.Contains("fallback"));
                long generation = manager.SpeechGeneration;
                TestAssert.True(manager.TrySpeak("Ability"));
                TestAssert.Equal("Ability", fallback.Text); TestAssert.Equal(2L, manager.OutputSerial);
                TestAssert.Equal(1, primary.Initializations); TestAssert.Equal(1, fallback.Initializations);
                TestAssert.Equal(generation, manager.SpeechGeneration);
            }
            TestAssert.Equal(1, primary.Disposals); TestAssert.Equal(1, fallback.Disposals);
        }
        public static void ThrowingPrimaryDeliversThroughCompatibility()
        {
            var primary = new Backend { ThrowOnOutput = true }; var fallback = new Backend();
            using (var manager = new ScreenReaderManager(() => primary, () => fallback, () => DateTime.UtcNow)) {
                TestAssert.True(manager.TrySpeak("Status", true));
                TestAssert.Equal("Status", fallback.Text); TestAssert.Equal(1L, manager.OutputSerial);
                TestAssert.Equal(1, primary.Disposals); TestAssert.Equal(1, fallback.Outputs); TestAssert.True(fallback.LastInterrupt);
                TestAssert.True(manager.BackendDiagnostic.Contains("Native output rejected"));
            }
        }
        public static void BothOutputFailuresKeepAcceptanceAndRespectCooldown()
        {
            DateTime clock = new DateTime(2026, 1, 1);
            var primary = new Backend(); var fallback = new Backend { Accept = false };
            using (var manager = new ScreenReaderManager(() => primary, () => fallback, () => clock)) {
                TestAssert.True(manager.TrySpeak("Accepted")); primary.Accept = false;
                TestAssert.False(manager.TrySpeak("Rejected"));
                TestAssert.Equal("Accepted", manager.LastAnnouncement); TestAssert.Equal(1L, manager.OutputSerial);
                TestAssert.Equal(1, primary.Disposals); TestAssert.Equal(1, fallback.Disposals);
                TestAssert.False(manager.IsInitialized);
                clock = clock.AddMilliseconds(500); TestAssert.False(manager.TrySpeak("Too soon"));
                TestAssert.Equal(2, primary.Outputs); TestAssert.Equal(1, fallback.Outputs);
                clock = clock.AddMilliseconds(500); fallback.Accept = true;
                TestAssert.True(manager.TrySpeak("Recovered")); TestAssert.Equal("Recovered", manager.LastAnnouncement);
                TestAssert.Equal(2L, manager.OutputSerial); TestAssert.Equal(2, fallback.Initializations);
            }
        }
        public static void HealthyPrimaryDoesNotInitializeCompatibility()
        {
            var primary = new Backend(); var fallback = new Backend();
            using (var manager = new ScreenReaderManager(() => primary, () => fallback, () => DateTime.UtcNow)) {
                TestAssert.True(manager.TrySpeak("Equip"));
                TestAssert.Equal(1, primary.Outputs); TestAssert.Equal(0, fallback.Initializations);
                TestAssert.Equal(0, fallback.Outputs); TestAssert.Equal(0, fallback.Disposals);
            }
        }
        public static void RepeatedInitializeOwnsOneBackend()
        {
            var backend = new Backend(); var manager = new ScreenReaderManager(() => backend, () => new Backend(), () => DateTime.UtcNow);
            manager.Initialize(); manager.Initialize(); TestAssert.Equal(1, backend.Initializations); manager.Dispose(); manager.Dispose(); TestAssert.Equal(1, backend.Disposals);
        }
        public static void AcceptedOutputSerialDistinguishesIdenticalAnnouncements()
        {
            DateTime clock=new DateTime(2026,1,1);var backend=new Backend();using(var manager=new ScreenReaderManager(()=>backend,() => new Backend { Accept = false },()=>clock)) {
                TestAssert.Equal(0L,manager.OutputSerial);TestAssert.True(manager.TrySpeak("The figure nods."));long owned=manager.OutputSerial;
                TestAssert.True(manager.TrySpeak("The figure nods."));TestAssert.True(manager.OutputSerial>owned);long last=manager.OutputSerial;
                backend.Accept=false;TestAssert.False(manager.TrySpeak("Rejected"));TestAssert.Equal(last,manager.OutputSerial);
                manager.Enabled=false;TestAssert.False(manager.TrySpeak("Disabled"));TestAssert.Equal(last,manager.OutputSerial);
            }
        }
        public static void TrySpeakReportsActualAcceptanceIncludingIdenticalRejectedText()
        {
            DateTime clock=new DateTime(2026,1,1);var backend=new Backend();
            var manager=new ScreenReaderManager(()=>backend,() => new Backend { Accept = false },()=>clock);
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
            var backend = new Backend(); var manager = new ScreenReaderManager(() => backend, () => new Backend { Accept = false }, () => DateTime.UtcNow);
            manager.Initialize(); manager.Speak("Alexandrie — entrée"); TestAssert.Equal("Alexandrie — entrée", manager.LastAnnouncement);
            backend.Accept = false; manager.Speak("Rejected"); TestAssert.Equal("Alexandrie — entrée", manager.LastAnnouncement); manager.Dispose();
        }
        public static void FailedOutputDoesNotClearEnabledPreference()
        {
            var backend = new Backend { Accept = false }; var manager = new ScreenReaderManager(() => backend, () => new Backend { Accept = false }, () => DateTime.UtcNow);
            manager.Initialize(); manager.Speak("Unavailable"); TestAssert.True(manager.Enabled); TestAssert.True(manager.BackendDiagnostic.Contains("failed")); manager.Dispose();
        }
    }
}
