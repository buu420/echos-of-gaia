using Memoria.Accessibility.AudioDescriptions;

namespace FF9Access.Tests
{
    internal static class JumpRopeCueTests
    {
        private const string SquareRope = "388b3f65caf4af5a272110a06db8d71503de69c6643470456ca4537bf515cdf2";
        private const string SteepleRope = "0e747dc56a53988699dd8167fca78000d2521adb0b7d390963a11fd7c59a3b25";

        public static void MatchesTheDisplayedPromptInEachRopeScene()
        {
            TestAssert.Equal(JumpRopePrompt.Pass, JumpRopeCue.Identify(103, 9, 215, SquareRope));
            TestAssert.Equal(JumpRopePrompt.Pass, JumpRopeCue.Identify(2456, 6, 193, SteepleRope));
            TestAssert.Equal(JumpRopePrompt.Start, JumpRopeCue.Identify(103, 30, 1094,
                "0e72508bc17c135bed7a794f53ea81941c31cadc590c6832cae3a3e85e87f45d"));
            TestAssert.Equal(JumpRopePrompt.Start, JumpRopeCue.Identify(2456, 16, 439,
                "7767d40a0ab196cba250ccef9380d659e42a6ee804a37737cd078a562b035706"));
            TestAssert.Equal(JumpRopePrompt.Start, JumpRopeCue.Identify(2456, 21, 443,
                "f02b4f94615b9e40e392d7b545c3d3d1d9d66a6cf016115f3e9f4ddba7055e1f"));
        }

        public static void IgnoresDifferentScenesPromptsAndScriptRevisions()
        {
            TestAssert.Equal(JumpRopePrompt.None, JumpRopeCue.Identify(1853, 9, 215, SquareRope));
            TestAssert.Equal(JumpRopePrompt.None, JumpRopeCue.Identify(103, 9, 218, SquareRope));
            TestAssert.Equal(JumpRopePrompt.None, JumpRopeCue.Identify(103, 9, 215, SteepleRope));
            TestAssert.Equal(JumpRopePrompt.None, JumpRopeCue.Identify(2456, 12, 42, SteepleRope));
        }

        public static void RepeatedPollsPlayOnceAndCatchUpCannotMoveTheCueAhead()
        {
            var cue = new JumpRopeCue();
            var room = new object();
            TestAssert.True(cue.Observe(room, JumpRopePrompt.Start, 0, 10, true));
            TestAssert.False(cue.Observe(room, JumpRopePrompt.Start, 0, 11, true));
            TestAssert.True(cue.Observe(room, JumpRopePrompt.Pass, 1, 30, true));
            TestAssert.False(cue.Observe(room, JumpRopePrompt.Pass, 1, 31, true));
            TestAssert.False(cue.Observe(room, JumpRopePrompt.Pass, 1, 32, true));
            TestAssert.True(cue.Observe(room, JumpRopePrompt.Pass, 2, 52, true));
            TestAssert.False(cue.Observe(room, JumpRopePrompt.Pass, 3, 52, true));
            TestAssert.True(cue.Observe(room, JumpRopePrompt.Pass, 3, 53, true));
        }

        public static void ResumesOnlyForAnOpenPromptAndResetsBetweenAttempts()
        {
            var cue = new JumpRopeCue();
            var room = new object();
            TestAssert.False(cue.Observe(room, JumpRopePrompt.Pass, 20, 500, false));
            TestAssert.True(cue.Observe(room, JumpRopePrompt.Pass, 20, 501, true));
            TestAssert.False(cue.Observe(room, JumpRopePrompt.Pass, 20, 502, false));
            TestAssert.False(cue.Observe(room, JumpRopePrompt.Pass, 20, 503, true));
            TestAssert.True(cue.Observe(room, JumpRopePrompt.Start, 0, 520, true));
            TestAssert.True(cue.Observe(room, JumpRopePrompt.Pass, 1, 540, true));
            TestAssert.True(cue.Observe(new object(), JumpRopePrompt.Pass, 1, 560, true));
        }
    }
}
