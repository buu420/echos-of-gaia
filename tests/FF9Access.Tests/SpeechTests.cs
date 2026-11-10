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
        public static void ReconnectDeliversRejectedAnnouncementOnce()
        {
            var disconnected = new Backend { Accept = false }; var reconnected = new Backend();
            int instances = 0;
            using (var manager = new ScreenReaderManager(() => ++instances == 1 ? disconnected : reconnected, () => DateTime.UtcNow)) {
                TestAssert.True(manager.TrySpeak("Item"));
                TestAssert.Equal("Item", manager.LastAnnouncement); TestAssert.Equal(1L, manager.OutputSerial);
                TestAssert.Equal(1, disconnected.Outputs); TestAssert.Equal(1, disconnected.Disposals);
                TestAssert.Equal(1, reconnected.Initializations); TestAssert.Equal(1, reconnected.Outputs); TestAssert.False(reconnected.LastInterrupt);
                TestAssert.True(manager.BackendDiagnostic.Contains("reconnected"));
                long generation = manager.SpeechGeneration;
                TestAssert.True(manager.TrySpeak("Ability"));
                TestAssert.Equal("Ability", reconnected.Text); TestAssert.Equal(2L, manager.OutputSerial);
                TestAssert.Equal(2, instances);
                TestAssert.Equal(generation, manager.SpeechGeneration);
            }
            TestAssert.Equal(1, disconnected.Disposals); TestAssert.Equal(1, reconnected.Disposals);
        }
        public static void ReconnectPreservesInterruptAfterException()
        {
            var disconnected = new Backend { ThrowOnOutput = true }; var reconnected = new Backend();
            int instances = 0;
            using (var manager = new ScreenReaderManager(() => ++instances == 1 ? disconnected : reconnected, () => DateTime.UtcNow)) {
                TestAssert.True(manager.TrySpeak("Status", true));
                TestAssert.Equal("Status", reconnected.Text); TestAssert.Equal(1L, manager.OutputSerial);
                TestAssert.Equal(1, disconnected.Disposals); TestAssert.Equal(1, reconnected.Outputs); TestAssert.True(reconnected.LastInterrupt);
            }
        }
        public static void FailedReconnectKeepsAcceptedTextAndRespectsCooldown()
        {
            DateTime clock = new DateTime(2026, 1, 1);
            var original = new Backend(); var retry = new Backend { Accept = false }; var recovered = new Backend();
            var backends = new[] { original, retry, recovered };
            int next = 0;
            using (var manager = new ScreenReaderManager(() => backends[next++], () => clock)) {
                TestAssert.True(manager.TrySpeak("Accepted")); original.Accept = false;
                TestAssert.False(manager.TrySpeak("Rejected"));
                TestAssert.Equal("Accepted", manager.LastAnnouncement); TestAssert.Equal(1L, manager.OutputSerial);
                TestAssert.Equal(1, original.Disposals); TestAssert.Equal(1, retry.Disposals);
                TestAssert.False(manager.IsInitialized);
                clock = clock.AddMilliseconds(500); TestAssert.False(manager.TrySpeak("Too soon"));
                TestAssert.Equal(2, original.Outputs); TestAssert.Equal(1, retry.Outputs); TestAssert.Equal(2, next);
                clock = clock.AddMilliseconds(500);
                TestAssert.True(manager.TrySpeak("Recovered")); TestAssert.Equal("Recovered", manager.LastAnnouncement);
                TestAssert.Equal(2L, manager.OutputSerial); TestAssert.Equal(1, recovered.Initializations);
            }
        }
        public static void HealthyPrismKeepsOneBackend()
        {
            var backend = new Backend();
            int instances = 0;
            using (var manager = new ScreenReaderManager(() => { instances++; return backend; }, () => DateTime.UtcNow)) {
                TestAssert.True(manager.TrySpeak("Equip"));
                TestAssert.Equal(1, backend.Outputs); TestAssert.Equal(1, instances); TestAssert.Equal(0, backend.Disposals);
            }
        }
        public static void RepeatedInitializeOwnsOneBackend()
        {
            var backend = new Backend(); var manager = new ScreenReaderManager(() => backend, () => DateTime.UtcNow);
            manager.Initialize(); manager.Initialize(); TestAssert.Equal(1, backend.Initializations); manager.Dispose(); manager.Dispose(); TestAssert.Equal(1, backend.Disposals);
        }
        public static void AcceptedOutputSerialDistinguishesIdenticalAnnouncements()
        {
            DateTime clock=new DateTime(2026,1,1);var backend=new Backend();using(var manager=new ScreenReaderManager(()=>backend,()=>clock)) {
                TestAssert.Equal(0L,manager.OutputSerial);TestAssert.True(manager.TrySpeak("The figure nods."));long owned=manager.OutputSerial;
                TestAssert.True(manager.TrySpeak("The figure nods."));TestAssert.True(manager.OutputSerial>owned);long last=manager.OutputSerial;
                backend.Accept=false;TestAssert.False(manager.TrySpeak("Rejected"));TestAssert.Equal(last,manager.OutputSerial);
                manager.Enabled=false;TestAssert.False(manager.TrySpeak("Disabled"));TestAssert.Equal(last,manager.OutputSerial);
            }
        }
        public static void TrySpeakReportsActualAcceptanceIncludingIdenticalRejectedText()
        {
            DateTime clock=new DateTime(2026,1,1);var backend=new Backend();
            var manager=new ScreenReaderManager(()=>backend,()=>clock);
            TestAssert.True(manager.TrySpeak("Far."));TestAssert.Equal("Far.",manager.LastAnnouncement);
            backend.Accept=false;TestAssert.False(manager.TrySpeak("Far."));TestAssert.Equal("Far.",manager.LastAnnouncement);
            backend.Accept=true;clock=clock.AddSeconds(2);TestAssert.True(manager.TrySpeak("Far."));
            manager.Enabled=false;TestAssert.False(manager.TrySpeak("Quiet"));manager.Dispose();TestAssert.False(manager.TrySpeak("Closed"));
        }
        public static void SceneShutdownDoesNotFreeSpeech()
        {
            var backend = new Backend(); var manager = new ScreenReaderManager(() => backend, () => DateTime.UtcNow);
            manager.Initialize(); manager.Shutdown(); manager.Speak("Return to field"); TestAssert.Equal(0, backend.Disposals); TestAssert.Equal(1, backend.Outputs); manager.Dispose();
        }
        public static void PrismStartupFailureReportsUnavailableAndBacksOff()
        {
            var backend = new Backend { Available = false };
            using (var manager = new ScreenReaderManager(() => backend, () => DateTime.UtcNow)) {
                manager.Initialize();
                TestAssert.False(manager.TrySpeak("Town"));
                TestAssert.Equal(1, backend.Initializations); TestAssert.Equal(1, backend.Disposals);
                TestAssert.Equal(0, backend.Outputs); TestAssert.True(manager.BackendDiagnostic.Contains("Prism unavailable"));
            }
        }
        public static void RecoverAfterScreenReaderStarts()
        {
            DateTime clock = new DateTime(2026, 1, 1); bool available = false;
            var manager = new ScreenReaderManager(() => new Backend { Available = available }, () => clock);
            manager.Initialize(); TestAssert.False(manager.IsInitialized); available = true; clock = clock.AddSeconds(6); manager.Speak("Ready");
            TestAssert.True(manager.IsInitialized); TestAssert.Equal("Ready", manager.LastAnnouncement); manager.Dispose();
        }
        public static void DisabledPreferenceSurvivesRecovery()
        {
            var manager = new ScreenReaderManager(() => new Backend(), () => DateTime.UtcNow); manager.Enabled = false;
            manager.Initialize(); manager.Shutdown(); manager.Initialize(); manager.Speak("Quiet"); TestAssert.False(manager.Enabled); TestAssert.True(string.IsNullOrEmpty(manager.LastAnnouncement)); manager.Dispose();
        }
        public static void Utf8AndRepeatPreserveActualAcceptedText()
        {
            var backend = new Backend(); var manager = new ScreenReaderManager(() => backend, () => DateTime.UtcNow);
            manager.Initialize(); manager.Speak("Alexandrie — entrée"); TestAssert.Equal("Alexandrie — entrée", manager.LastAnnouncement);
            backend.Accept = false; manager.Speak("Rejected"); TestAssert.Equal("Alexandrie — entrée", manager.LastAnnouncement); manager.Dispose();
        }
        public static void FailedOutputDoesNotClearEnabledPreference()
        {
            var backend = new Backend { Accept = false }; var manager = new ScreenReaderManager(() => backend, () => DateTime.UtcNow);
            manager.Initialize(); manager.Speak("Unavailable"); TestAssert.True(manager.Enabled); TestAssert.True(manager.BackendDiagnostic.Contains("failed")); manager.Dispose();
        }
        public static void MalformedTextPreservesTheReadableCharacters()
        {
            var backend = new Backend();
            using (var manager = new ScreenReaderManager(() => backend, () => DateTime.UtcNow)) {
                TestAssert.False(manager.TrySpeak("\0 \0"));
                TestAssert.Equal(0, backend.Initializations);
                TestAssert.True(manager.TrySpeak("\0Entrée\0\uD800\uD83D\uDE03\uDC00", true));
                TestAssert.Equal("Entrée \uFFFD\uD83D\uDE03\uFFFD", backend.Text);
                TestAssert.Equal(backend.Text, manager.LastAnnouncement);
                TestAssert.Equal(1, backend.Outputs); TestAssert.True(backend.LastInterrupt);
            }
        }
    }
}
