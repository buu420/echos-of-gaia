using System;
using System.Collections.Generic;
using System.Reflection;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    public static class WorldFootstepsTests
    {
        // These exercise audible-step decisions and cached playback, not input presses.
        public static void AcceptedMovementUsesWalkingCadenceWithoutBurstCatchup()
        {
            var tracker = new Tracker(); var context = World();
            TestAssert.False(tracker.Observe(context, true, 0));
            context.Position = new NavigationPoint(1, 0, 0);
            TestAssert.False(tracker.Observe(context, true, 299));
            context.Position = new NavigationPoint(2, 0, 0);
            TestAssert.True(tracker.Observe(context, true, 300));
            context.Position = new NavigationPoint(3, 0, 0);
            TestAssert.False(tracker.Observe(context, true, 301));
            context.Position = new NavigationPoint(4, 0, 0);
            TestAssert.True(tracker.Observe(context, true, 900));
            context.Position = new NavigationPoint(5, 0, 0);
            TestAssert.False(tracker.Observe(context, true, 901));
        }
        public static void StandingBlockedAndVerticalOnlyMovementStaySilent()
        {
            var tracker = new Tracker(); var context = World(); tracker.Observe(context, true, 0);
            TestAssert.False(tracker.Observe(context, true, 400)); TestAssert.False(tracker.Moving);
            context.Position = new NavigationPoint(0, 4, 0);
            TestAssert.False(tracker.Observe(context, true, 500)); TestAssert.False(tracker.Moving);
            context.Position = new NavigationPoint(1, 4, 0);
            TestAssert.True(tracker.Observe(context, true, 600)); TestAssert.True(tracker.Moving);
            TestAssert.False(tracker.Observe(context, true, 700)); TestAssert.False(tracker.Moving);
        }
        public static void MovementAcrossBothWorldSeamsRemainsContinuous()
        {
            var tracker = new Tracker(); var context = World();
            context.Position = new NavigationPoint(1535, 0, -1279); tracker.Observe(context, true, 0);
            context.Position = new NavigationPoint(1, 0, -1);
            TestAssert.True(tracker.Observe(context, true, 300));
        }
        public static void MenusBattleDialogueFocusAndLoadingResetMovement()
        {
            foreach (var gate in new[] { GateReason.Menu, GateReason.Battle, GateReason.Dialogue, GateReason.Background, GateReason.NoControl, GateReason.MapTransition, GateReason.UnknownActor })
            {
                var tracker = new Tracker(); var context = World(); tracker.Observe(context, true, 0);
                context.Position = new NavigationPoint(1, 0, 0); TestAssert.True(tracker.Observe(context, true, 300));
                context.GateReason = gate; context.Position = new NavigationPoint(2, 0, 0);
                TestAssert.False(tracker.Observe(context, true, 600)); TestAssert.False(tracker.Moving);
                context.GateReason = GateReason.None;
                TestAssert.False(tracker.Observe(context, true, 700));
                context.Position = new NavigationPoint(3, 0, 0);
                TestAssert.True(tracker.Observe(context, true, 1000));
            }
        }
        public static void FieldsVehiclesAndAirborneMovementHaveNoWorldFootsteps()
        {
            foreach (var kind in new[] { TransportKind.Unknown, TransportKind.Boat, TransportKind.HildaGarde, TransportKind.Invincible })
            {
                var tracker = new Tracker(); var context = World(); context.Transport = new TransportIdentity(kind, 1, 8);
                tracker.Observe(context, true, 0); context.Position = new NavigationPoint(1, 0, 0);
                TestAssert.False(tracker.Observe(context, true, 300)); TestAssert.False(tracker.Moving);
            }
            var fieldTracker = new Tracker(); var field = World(); field.LogicalMapKey = "field:611";
            fieldTracker.Observe(field, true, 0); field.Position = new NavigationPoint(1, 0, 0);
            TestAssert.False(fieldTracker.Observe(field, true, 300));
            var flyingTracker = new Tracker(); var flying = World(); flyingTracker.Observe(flying, false, 0);
            flying.Position = new NavigationPoint(1, 0, 0); TestAssert.False(flyingTracker.Observe(flying, false, 300));
        }
        public static void GroundChocoboUsesLongerCadenceAndReprimesAfterSwimming()
        {
            var tracker = new Tracker(); var context = World(); context.Transport = new TransportIdentity(TransportKind.Chocobo, 1, 3);
            tracker.Observe(context, true, 0); context.Position = new NavigationPoint(1, 0, 0);
            TestAssert.False(tracker.Observe(context, true, 300));
            context.Position = new NavigationPoint(2, 0, 0); TestAssert.True(tracker.Observe(context, true, 500));
            context.Position = new NavigationPoint(3, 0, 0); TestAssert.False(tracker.Observe(context, false, 1000)); TestAssert.False(tracker.Moving);
            TestAssert.False(tracker.Observe(context, true, 1100));
            context.Position = new NavigationPoint(4, 0, 0); TestAssert.True(tracker.Observe(context, true, 1600));
        }
        public static void IdentityChangesAndTeleportsCannotCreateArrivalFootsteps()
        {
            foreach (Action<NavigationContext> change in new Action<NavigationContext>[] {
                c => c.Generation++, c => c.SaveIdentity = "other", c => c.LogicalMapKey = "world:1",
                c => c.PlayerIdentity++, c => c.Transport = new TransportIdentity(TransportKind.Foot, 2, 1) })
            {
                var tracker = new Tracker(); var context = World(); tracker.Observe(context, true, 0);
                change(context); context.Position = new NavigationPoint(1, 0, 0);
                TestAssert.False(tracker.Observe(context, true, 300));
            }
            var teleportTracker = new Tracker(); var teleport = World(); teleportTracker.Observe(teleport, true, 0);
            teleport.Position = new NavigationPoint(33, 0, 0); TestAssert.False(teleportTracker.Observe(teleport, true, 300)); TestAssert.False(teleportTracker.Moving);
            teleport.Position = new NavigationPoint(34, 0, 0); TestAssert.True(teleportTracker.Observe(teleport, true, 600));
        }
        public static void InvalidCoordinatesClockReversalAndLongGapsReprimeSafely()
        {
            var tracker = new Tracker(); var context = World(); tracker.Observe(context, true, 0);
            context.Position = new NavigationPoint(float.NaN, 0, 0); TestAssert.False(tracker.Observe(context, true, 300));
            context.Position = new NavigationPoint(1, 0, 0); TestAssert.False(tracker.Observe(context, true, 400));
            context.Position = new NavigationPoint(2, 0, 0); TestAssert.False(tracker.Observe(context, true, double.PositiveInfinity));
            TestAssert.False(tracker.Observe(context, true, 500));
            context.Position = new NavigationPoint(3, 0, 0); TestAssert.False(tracker.Observe(context, true, 2000));
            context.Position = new NavigationPoint(4, 0, 0); TestAssert.False(tracker.Observe(context, true, 1999));
        }
        public static void CachedPlaybackWarmsOnceAndNeverLoadsDuringSteps()
        {
            var audio = new AudioFixture(10); var bank = audio.Bank;
            for (int i = 0; i < 12; i++) bank.Warm();
            TestAssert.Equal(10, audio.Loaded.Count);
            for (int i = 0; i < 12; i++) TestAssert.True(bank.Play(.7f));
            TestAssert.Equal(10, audio.Loaded.Count);
            TestAssert.Equal(100, audio.Played[0]); TestAssert.Equal(109, audio.Played[9]); TestAssert.Equal(100, audio.Played[10]);
            TestAssert.Near(.7f, audio.LastGain, .00001f);
        }
        public static void StoppingAffectsOnlyOwnedVoicesAndKeepsCachedSounds()
        {
            var audio = new AudioFixture(2); audio.Bank.Warm(); audio.Bank.Warm();
            for (int i = 0; i < 6; i++) audio.Bank.Play(1);
            TestAssert.Equal(2, audio.Stopped.Count); // Four owned slots bound overlapping steps.
            audio.Bank.Stop(); TestAssert.Equal(6, audio.Stopped.Count);
            foreach (int voice in audio.Stopped) TestAssert.True(voice >= 1000 && voice < 1006);
            audio.Bank.Stop(); TestAssert.Equal(6, audio.Stopped.Count);
            TestAssert.True(audio.Bank.Play(1)); TestAssert.Equal(2, audio.Loaded.Count);
        }
        public static void MissingSoundAndBackendFailuresCannotEscapeIntoNavigation()
        {
            var audio = new AudioFixture(2); audio.FailLoad = true;
            audio.Bank.Warm(); audio.Bank.Warm(); audio.Bank.Warm();
            TestAssert.False(audio.Bank.Play(1)); TestAssert.Equal(1, audio.Reported);
            var playback = new AudioFixture(1); playback.Bank.Warm(); playback.FailPlay = true;
            TestAssert.False(playback.Bank.Play(1)); TestAssert.False(playback.Bank.Play(1)); TestAssert.Equal(1, playback.Reported);
            var stop = new AudioFixture(1); stop.Bank.Warm(); stop.Bank.Play(1); stop.FailStop = true;
            stop.Bank.Stop(); stop.Bank.Stop(); TestAssert.Equal(1, stop.Reported);
        }
        public static void MutedPlaybackDoesNotCreateVoices()
        {
            var audio = new AudioFixture(1); audio.Bank.Warm();
            TestAssert.False(audio.Bank.Play(0)); TestAssert.False(audio.Bank.Play(float.NaN));
            TestAssert.Equal(0, audio.Played.Count);
        }
        public static void NativeVoiceRejectionReportsOnceStopsOwnedVoicesAndDisablesPlayback()
        {
            var audio = new AudioFixture(1); audio.Bank.Warm();
            TestAssert.True(audio.Bank.Play(1)); TestAssert.True(audio.Bank.Play(1));
            audio.RejectPlay = true;
            TestAssert.False(audio.Bank.Play(1)); TestAssert.Equal(1, audio.Reported);
            TestAssert.Equal(2, audio.Stopped.Count); TestAssert.Equal(3, audio.PlayAttempts);
            audio.RejectPlay = false;
            TestAssert.False(audio.Bank.Play(1)); TestAssert.Equal(1, audio.Reported);
            TestAssert.Equal(3, audio.PlayAttempts);
        }
        private static NavigationContext World()
        {
            return new NavigationContext { Generation = 1, LogicalMapKey = "world:0", SaveIdentity = "save", PlayerIdentity = 1,
                Position = new NavigationPoint(0, 0, 0), Transport = new TransportIdentity(TransportKind.Foot, 1, 1),
                CanControl = true, Foreground = true, GateReason = GateReason.None };
        }
        private static Type Require(string name)
        {
            var type = typeof(NavigationContext).Assembly.GetType("Memoria.Accessibility.Navigation." + name);
            if (type == null) throw new Exception("World-map footstep component is missing: " + name);
            return type;
        }
        private sealed class Tracker
        {
            private readonly object _value = Activator.CreateInstance(Require("WorldFootstepCadence"));
            public bool Observe(NavigationContext context, bool grounded, double now) { return (bool)_value.GetType().GetMethod("Observe").Invoke(_value, new object[] { context, grounded, now }); }
            public bool Moving { get { return (bool)_value.GetType().GetProperty("IsMoving").GetValue(_value, null); } }
        }
        private sealed class Bank
        {
            private readonly object _value;
            public Bank(int count, Func<int, int> load, Func<int, float, int> play, Action<int> stop, Action<string> report)
            { _value = Activator.CreateInstance(Require("CachedFootstepPlayer"), new object[] { count, load, play, stop, report }); }
            public void Warm() { _value.GetType().GetMethod("WarmOne").Invoke(_value, null); }
            public bool Play(float gain) { return (bool)_value.GetType().GetMethod("Play").Invoke(_value, new object[] { gain }); }
            public void Stop() { _value.GetType().GetMethod("Stop").Invoke(_value, null); }
        }
        private sealed class AudioFixture
        {
            public readonly List<int> Loaded = new List<int>(), Played = new List<int>(), Stopped = new List<int>();
            public bool FailLoad, FailPlay, FailStop, RejectPlay; public float LastGain; public int Reported, PlayAttempts;
            public readonly Bank Bank;
            public AudioFixture(int count)
            {
                Bank = new Bank(count, i => { Loaded.Add(i); if (FailLoad) throw new Exception("load failure"); return 100 + i; },
                    (id, gain) => { PlayAttempts++; if (FailPlay) throw new Exception("play failure"); if (RejectPlay) return 0; Played.Add(id); LastGain = gain; return 999 + Played.Count; },
                    id => { Stopped.Add(id); if (FailStop) throw new Exception("stop failure"); },
                    message => { Reported++; if (FailLoad) throw new Exception("diagnostic failure"); });
            }
        }
    }
}
