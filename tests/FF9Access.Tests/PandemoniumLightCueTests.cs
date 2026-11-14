using System.Collections.Generic;
using Memoria.Accessibility.AudioDescriptions;
using Memoria.Accessibility.Navigation;

namespace FF9Access.Tests
{
    internal static class PandemoniumLightCueTests
    {
        public static void ACurrentGroupReplacesOldLightsAndClearingIsOnlyAnnouncedOnce()
        {
            var cue = new PandemoniumLightCue();
            var messages = new List<string>();
            cue.Observe(0, 0, Speak);
            cue.Observe(1 << 4, 1 << 3, Speak);
            cue.Observe(1 << 4, 1 << 4, Speak);
            cue.Observe(1 << 5, 1 << 0, Speak);
            cue.Observe(0, 0, Speak);
            cue.Observe(0, 0, Speak);
            TestAssert.Equal(3, messages.Count);
            TestAssert.Equal("Lights up left", messages[0]);
            TestAssert.Equal("Lights right", messages[1]);
            TestAssert.Equal("No lights in view", messages[2]);

            bool Speak(string text) { messages.Add(text); return true; }
        }

        public static void HiddenOrUnusableViewportCoordinatesDoNotProduceDirections()
        {
            var player = new NavigationPoint(.5f, .5f, 1);
            TestAssert.Equal(-1, PandemoniumLightCue.Direction(new NavigationPoint(1.02f, .7f, 1), player, 1.5f));
            TestAssert.Equal(-1, PandemoniumLightCue.Direction(new NavigationPoint(.8f, .7f, -1), player, 1.5f));
            TestAssert.Equal(-1, PandemoniumLightCue.Direction(new NavigationPoint(float.NaN, .7f, 1), player, 1.5f));
            TestAssert.Equal(-1, PandemoniumLightCue.Direction(new NavigationPoint(.7f, .7f, 1), player, 0));
            TestAssert.Equal(3, PandemoniumLightCue.Direction(new NavigationPoint(.3f, .8f, 1), player, 1.5f));
            TestAssert.Equal(0, PandemoniumLightCue.Direction(new NavigationPoint(.8f, .5f, 1), player, 1.5f));
        }

        public static void ARejectedCueDoesNotCarryIntoAResetOrNewGroup()
        {
            var cue = new PandemoniumLightCue();
            var messages = new List<string>();
            cue.Observe(1 << 4, 1 << 2, text => false);
            cue.Observe(1 << 18, (1 << 0) | (1 << 4), Speak);
            cue.Reset();
            cue.Observe(0, 0, Speak);
            cue.Observe(1 << 18, 1 << 8, Speak);
            TestAssert.Equal(2, messages.Count);
            TestAssert.Equal("Lights right, left", messages[0]);
            TestAssert.Equal("Lights nearby", messages[1]);

            bool Speak(string text) { messages.Add(text); return true; }
        }
    }
}
