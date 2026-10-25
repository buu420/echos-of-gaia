using System;
using System.IO;
using Global.Sound.SaXAudio;
using Memoria.Prime;
using Memoria.ScreenReader;
using UnityEngine;
namespace Memoria.Accessibility.Navigation
{
    public static class NativeWorldFootsteps
    {
        private static readonly WorldFootstepCadence _cadence = new WorldFootstepCadence();
        private static CachedFootstepPlayer _player;
        private static string _soundsDirectory;
        private static int _bus, _warmed;
        private static bool _playing, _quitting, _reported, _announceFailure, _tracedPlay;
        public static long PlayedSteps { get; private set; }
        static NativeWorldFootsteps()
        {
            // Like SoundLib, avoid calling the audio DLL during process teardown.
            GameLoopManager.Quit += delegate { _quitting = true; _playing = false; _cadence.Reset(); };
        }
        // Render frames can stop footsteps immediately, but must never sample
        // interpolated positions or stop a voice between accepted movement ticks.
        public static void UpdateGates(NavigationContext context)
        {
            if (_quitting) return;
            try
            {
                if (context == null || context.LogicalMapKey == null || !context.LogicalMapKey.StartsWith("world:", StringComparison.Ordinal) ||
                    !context.Foreground || !context.CanControl || context.GateReason != GateReason.None ||
                    !Configuration.Accessibility.WorldMapFootsteps) Suspend();
            }
            catch (Exception exception) { Suspend(); Report(exception.Message); }
        }
        // Called only after the native world movement/collision update, before smoothing.
        public static void Tick(NavigationContext context)
        {
            if (_quitting) return;
            try
            {
                if (context == null || context.LogicalMapKey == null || !context.LogicalMapKey.StartsWith("world:", StringComparison.Ordinal) ||
                    !context.Foreground || context.GateReason == GateReason.Battle || context.GateReason == GateReason.MapTransition ||
                    !Configuration.Accessibility.WorldMapFootsteps)
                { Suspend(); return; }
                var backend = ISdLibAPIProxy.Instance as SdLibAPIWithSaXAudio;
                if (backend == null) { Suspend(); Report("The selected audio backend does not provide the cached footstep interface."); AnnounceFailure(context); return; }
                if (backend.BusSoundEffect <= 0) { Suspend(); return; }
                _bus = backend.BusSoundEffect;
                if (_player == null)
                {
                    var architectureDirectory = Directory.GetParent(Application.dataPath);
                    if (architectureDirectory == null || architectureDirectory.Parent == null) throw new InvalidOperationException("World footstep installation path is unavailable");
                    _soundsDirectory = Path.Combine(architectureDirectory.Parent.FullName, "accessibility/footsteps");
                    _player = new CachedFootstepPlayer(10, LoadSample, PlaySample, StopSample, Report);
                }
                // One small sample per world frame, normally during native loading.
                // The completed bank stays cached across battle returns.
                _player.WarmOne();
                bool step = _cadence.Observe(context, IsGrounded(context), Time.realtimeSinceStartup * 1000d);
                float gain = SoundLib.SoundEffectIsMute ? 0 : Configuration.Accessibility.WorldMapFootstepVolume / 100f * Math.Max(0, Math.Min(100, Configuration.Audio.SoundVolume)) / 100f;
                if (!_cadence.IsMoving || gain <= 0) StopPlaying();
                else if (step && _player.Play(gain))
                {
                    _playing = true; PlayedSteps++;
                    if (!_tracedPlay) { _tracedPlay = true; NavigationRuntime.Trace("World footsteps started; transport=" + context.Transport.Kind + "; gain=" + gain); }
                }
                AnnounceFailure(context);
            }
            catch (Exception exception) { Suspend(); Report(exception.Message); }
        }
        private static bool IsGrounded(NavigationContext context)
        {
            if (context.Transport.Kind != TransportKind.Foot && context.Transport.Kind != TransportKind.Chocobo) return false;
            var actor = ff9.w_moveActorPtr; int control = ff9.w_moveCHRControl_No;
            if (actor == null || actor.originalActor == null || actor.originalActor.uid != context.PlayerIdentity ||
                actor.originalActor.index != context.Transport.NativeStatus || ff9.w_moveCHRControl == null || ff9.w_moveCHRStatus == null ||
                control < 0 || control >= ff9.w_moveCHRControl.Length || actor.originalActor.index < 0 || actor.originalActor.index >= ff9.w_moveCHRStatus.Length) return false;
            var rules = ff9.w_moveCHRControl[control]; var status = ff9.w_moveCHRStatus[actor.originalActor.index];
            // Native type 0 is ground travel; 1 is flight and 2 is swimming/boat.
            return rules != null && status != null && rules.type == 0 && !rules.flg_fly && status.flg_fly == 0 &&
                !WorldTerrainRules.InMask(0x23ED0000, 0, status.id);
        }
        private static int LoadSample(int index)
        {
            string file = Path.Combine(_soundsDirectory, (5020 + index) + ".ogg");
            if (!File.Exists(file)) throw new FileNotFoundException("World footstep sample is missing", file);
            int bank = SaXAudio.BankLoadOggFile(file);
            if (bank <= 0) throw new InvalidOperationException("World footstep sample could not be decoded");
            if (++_warmed == 10) NavigationRuntime.Trace("World footsteps cached; samples=10; backend=SaXAudio; cadence=300/500 ms");
            return bank;
        }
        private static int PlaySample(int bank, float gain)
        {
            int voice = SaXAudio.CreateVoice(bank, _bus, true);
            if (voice <= 0) return 0;
            try
            {
                SaXAudio.SetLooping(voice, false); SaXAudio.SetVolume(voice, gain, 0, false);
                if (SaXAudio.Start(voice)) return voice;
                StopSample(voice); return 0;
            }
            catch { StopSample(voice); throw; }
        }
        private static void StopSample(int voice) { if (SaXAudio.VoiceExist(voice)) SaXAudio.Stop(voice, 0.01f); }
        private static void StopPlaying()
        {
            if (!_playing) return;
            _playing = false; if (_player != null) _player.Stop();
            NavigationRuntime.Trace("World footsteps stopped; accepted steps=" + PlayedSteps);
        }
        public static void Suspend()
        {
            try { _cadence.Reset(); if (!_quitting) StopPlaying(); }
            catch (Exception exception) { Report(exception.Message); }
        }
        private static void Report(string message)
        {
            if (_reported) return; _reported = true; _announceFailure = true;
            try { Log.Message("[FF9Access] World footsteps unavailable: {0}", message); NavigationRuntime.Trace("World footsteps unavailable: " + message); } catch (Exception) { }
        }
        private static void AnnounceFailure(NavigationContext context)
        {
            if (!_announceFailure || !context.CanControl || context.GateReason != GateReason.None) return;
            _announceFailure = false; ScreenReaderManager.Instance.Speak("World map footsteps are unavailable. See the accessibility log.", false);
        }
    }
}
