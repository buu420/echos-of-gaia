using System;
using System.Collections.Generic;
// Narrow stand-ins for the Unity/game surface the central runtime touches. They hold state the
// scenarios set explicitly; they contain no description logic.
namespace UnityEngine
{
    public static class Application { public static string dataPath = "C:/FF9AccessHarness/x64/FF9_Data"; }
    public static class Time { public static float realtimeSinceStartup; }
    public static class Random { }
    public sealed class GameObject { public bool activeInHierarchy = true; }
    public struct Vector4 { public float x, y, z, w; public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; } public static Vector4 zero { get { return new Vector4(); } } }
}
namespace Assets.Scripts.Common
{
    public sealed class SceneDirector { public static readonly SceneDirector Instance = new SceneDirector(); public bool IsFading { get; set; } public static bool IsScreenObscured; }
}
namespace Assets.Sources.Graphics.Movie
{
    public sealed class MovieMaterial
    {
        public string movieKey;
        public double PresentedTime = -1; public bool Advancing = true; public bool FirstFrame = true; public float Alpha = 1f;
        internal double DescriptionPresentedTime { get { return PresentedTime; } }
        internal bool DescriptionAdvancing { get { return Advancing; } }
        public bool GetFirstFrame { get { return FirstFrame; } }
        public float Transparency { get { return Alpha; } }
    }
}
namespace Memoria
{
    public sealed partial class Configuration
    {
        public static class Accessibility { public static bool AudioDescriptions = true; public static int AudioDescriptionVolume = 100; }
    }
}
namespace Memoria.Prime
{
    public sealed class Log
    {
        public static readonly List<string> Lines = new List<string>();
        public static void Message(string format, params object[] args) { lock (Lines) Lines.Add(args == null || args.Length == 0 ? format : string.Format(format, args)); }
        public static void Warning(string format, params object[] args) { Message(format, args); }
        public static void Error(string format, params object[] args) { Message(format, args); }
        public static void Error(Exception exception) { Message(exception.Message); }
        public static void Error(Exception exception, string format, params object[] args) { Message(format, args); }
    }
}
public sealed class Dialog
{
    public UnityEngine.GameObject gameObject = new UnityEngine.GameObject();
    public object CurrentParser = new object();
    public void NextPhrase() { CurrentParser = new object(); }
}
public sealed class UIManager { public static readonly UIManager Instance = new UIManager(); public bool IsPause; }
public static class HonoInputManager { public static bool Active = true; public static bool ApplicationIsActivated() { return Active; } }
namespace Memoria.Accessibility.AudioDescriptions
{
    // Same public surface as the real field observer; the scenarios raise its events.
    public static class NativeFieldDescriptions
    {
        public static void ObservePresentationBoundary() { }
        public static event Action<string, string, Func<bool>> Cue;
        public static event Action SceneClosed;
        public static int Subscribers { get { return Cue == null ? 0 : Cue.GetInvocationList().Length; } }
        public static void Raise(string id, string text, Func<bool> current) { var h = Cue; if (h != null) h(id, text, current); }
        public static void Close() { var h = SceneClosed; if (h != null) h(); }
    }
    // The owned waveOut device. Records what would have been played; never touches real audio.
    public sealed class NativeDescriptionSpeech : IDisposable
    {
        public static bool Available=true,ThrowOnStatus;public static readonly List<string> Outputs=new List<string>();public static NativeDescriptionSpeech Last;
        private bool _playing;
        public bool Playing {get{if(_playing && ThrowOnStatus)throw new InvalidOperationException("fake speech status error");return _playing;}private set{_playing=value;}}public bool Paused {get;private set;}
        public NativeDescriptionSpeech(){Last=this;}
        public bool TrySpeak(string text,int volume){if(!Available)return false;Outputs.Add(text);Playing=true;Paused=false;return true;}
        public void Pause(){if(Playing)Paused=true;}public void Resume(){Paused=false;}
        public void Stop(){Playing=false;Paused=false;}public void Dispose(){Stop();}
        public static void Finish(){if(Last!=null)Last.Stop();}
    }
    public sealed class DescriptionWaveOutput : IDisposable
    {
        public static bool ThrowOnPlay, ThrowOnPause, ThrowOnResume; public static readonly List<byte[]> Played = new List<byte[]>(); public static DescriptionWaveOutput Last;
        private bool _playing, _paused;
        public bool Playing { get { return _playing; } }
        public bool Paused { get { return _paused; } }
        public DescriptionWaveOutput() { Last = this; }
        public bool TryPlay(byte[] wave) { if (ThrowOnPlay) throw new InvalidOperationException("fake waveOut failure"); if (_playing) return false; Played.Add(wave); _playing = true; _paused = false; return true; }
        public void Pause() { if(ThrowOnPause)throw new InvalidOperationException("fake pause failure"); if (_playing) _paused = true; }
        public void Resume() { if(ThrowOnResume)throw new InvalidOperationException("fake resume failure"); _paused = false; }
        public void Stop() { _playing = false; _paused = false; }
        public void Dispose() { Stop(); }
        public static void Finish() { if (Last != null) Last._playing = false; }
        // RIFF/PCM header parse used by the bank's duration check.
        public static double Duration(byte[] wave) { int byteRate = BitConverter.ToInt32(wave, 28), data = BitConverter.ToInt32(wave, 40); return (double)data / byteRate; }
    }
}
namespace Memoria.ScreenReader
{
    public sealed class FakeSpeech
    {
        public static readonly List<KeyValuePair<string, bool>> Outputs = new List<KeyValuePair<string, bool>>();
        public static bool Accept = true; public static int Silences;
        public static string Last { get { return Outputs.Count == 0 ? null : Outputs[Outputs.Count - 1].Key; } }
    }
    public sealed class PrismSpeechBackend : ISpeechBackend
    {
        public string Name { get { return "FakePrism"; } }
        public bool Initialize() { return true; }
        public bool Output(string text, bool interrupt) { if (!FakeSpeech.Accept) return false; FakeSpeech.Outputs.Add(new KeyValuePair<string, bool>(text, interrupt)); return true; }
        public void Stop() { FakeSpeech.Silences++; }
        public bool IsSpeaking() { return false; }
        public void Dispose() { }
    }
    public sealed class TolkSpeechBackend : ISpeechBackend
    {
        public string Name { get { return "FakeTolk"; } }
        public bool Initialize() { return false; }
        public bool Output(string text, bool interrupt) { return false; }
        public void Stop() { } public bool IsSpeaking() { return false; } public void Dispose() { }
    }
    internal static class SpeechDiagnostics
    {
        public static string LastWriteFailure { get; private set; }
        public static string LogPath { get; private set; }
        public static void Write(string message) { }
    }
}
