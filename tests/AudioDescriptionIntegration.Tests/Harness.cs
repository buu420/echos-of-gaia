using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Assets.Sources.Graphics.Movie;
using Memoria.Accessibility.AudioDescriptions;
using Memoria.Prime;
using Memoria.ScreenReader;
namespace AudioDescriptionIntegration.Tests
{
    // One isolated game "install" per scenario process: real runtime, fake devices.
    internal static class World
    {
        internal const string Crouch = "field-main-f0-zdn-hiza-1", CrouchText = "The boy with the tail crouches down.";
        internal const string Nod = "field-main-f0-grn-yes", NodText = "The long-haired girl nods.";
        internal static string Root, MoviePath;
        internal static void Install(bool fieldClips = true, bool movieClips = true, bool movieMatches = true, bool initialize = true)
        {
            Root = Path.Combine(Path.GetTempPath(), "ff9ad-harness-" + Guid.NewGuid().ToString("N"));
            string data = Path.Combine(Root, "x64", "FF9_Data"), audio = Path.Combine(Root, "accessibility", "audio-descriptions");
            Directory.CreateDirectory(data); Directory.CreateDirectory(audio);
            UnityEngine.Application.dataPath = data; UnityEngine.Time.realtimeSinceStartup = 10f;
            string fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures");
            foreach (var wav in Directory.GetFiles(fixtures, "*.wav"))
            {
                bool field = Path.GetFileName(wav).StartsWith("field-");
                if (field ? fieldClips : movieClips) File.Copy(wav, Path.Combine(audio, Path.GetFileName(wav)));
            }
            MoviePath = Path.Combine(Root, "FMV001.movie");
            var movie = File.ReadAllBytes(Path.Combine(fixtures, "FMV001.movie")); if (!movieMatches) movie[100] ^= 0xFF;
            File.WriteAllBytes(MoviePath, movie);
            if (initialize) { NativeAudioDescriptions.Initialize(); WaitForBank("FieldBank", -1, fieldClips ? Crouch : null); }
        }
        internal static void Cleanup()
        {
            if(Root==null)return;
            string full=Path.GetFullPath(Root), temp=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            if(!full.StartsWith(temp,StringComparison.OrdinalIgnoreCase) || !System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(full),"^ff9ad-harness-[a-f0-9]{32}$"))
                throw new InvalidOperationException("Unsafe test cleanup path");
            try{if(Directory.Exists(full))Directory.Delete(full,true);}catch(IOException){ }
        }
        private static DescriptionAudioBank Bank(string name) { return (DescriptionAudioBank)typeof(NativeAudioDescriptions).GetField(name, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null); }
        // Bank work is genuinely asynchronous (ThreadPool); wait for its published outcome only.
        internal static void WaitForBank(string name, long owner, string id)
        {
            var bank = Bank(name); var stop = DateTime.UtcNow.AddSeconds(10); byte[] clip;
            while (DateTime.UtcNow < stop)
            {
                if (id != null ? bank.TryGet(owner, id, out clip) : bank.Failure(owner) != null) return;
                if (id != null && bank.Failure(owner) != null) throw new Exception("Bank failed: " + bank.Failure(owner));
                Thread.Sleep(10);
            }
            throw new Exception("Bank did not settle: " + name);
        }
        internal static long MovieOwner() { var film = typeof(NativeAudioDescriptions).GetField("_film", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null); return film == null ? 0 : (long)film.GetType().GetField("Owner", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(film); }
        internal static void Tick(int frames = 1, float seconds = 1f / 30f) { for (int i = 0; i < frames; i++) { UnityEngine.Time.realtimeSinceStartup += seconds; GameLoopManager.RaiseUpdateEvent(); } }
        internal static Func<bool> Always = () => true;
        internal static int Spoken(string fragment) { return FakeSpeech.Outputs.Count(o => o.Key.Contains(fragment))+NativeDescriptionSpeech.Outputs.Count(o=>o.Contains(fragment)); }
        internal static string Transcript() { return string.Join(" || ", FakeSpeech.Outputs.Select(o => (o.Value ? "[i] " : "[q] ") + o.Key).Concat(NativeDescriptionSpeech.Outputs.Select(o=>"[owned] "+o))); }
        internal static int Played { get { return DescriptionWaveOutput.Played.Count; } }
        internal static bool Playing { get { return DescriptionWaveOutput.Last != null && DescriptionWaveOutput.Last.Playing; } }
        internal static void Present(MovieMaterial movie, double seconds, bool advancing = true) { movie.PresentedTime = seconds; movie.Advancing = advancing; NativeAudioDescriptions.MoviePresented(movie); }
        internal static void Check(bool condition, string message) { if (!condition) throw new Exception(message + " | speech: " + Transcript() + " | waves played: " + Played); }
    }
}
