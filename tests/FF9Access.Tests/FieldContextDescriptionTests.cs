using System;
using System.Collections.Generic;
using Memoria.Accessibility.AudioDescriptions;
namespace FF9Access.Tests
{
    internal static class FieldContextDescriptionTests
    {
        // Synthetic test identity and captions belong only to this test assembly.
        private const string Hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        private const string Site = "50:17:" + Hash + ":617";
        private const string Clip = "ANH_MAIN_F0_ZDN_HIZA_1";
        private const string ContextOnly = "TEST_ONLY_ACTUAL_CLIP";
        private static FieldContextPoseDescription Entry(string clip = Clip)
        { return new FieldContextPoseDescription(50, 17, Hash, 617, 98, clip, "test-context-pose", "A reviewed test action."); }
        private static FieldContextPoseCatalog Catalog(string clip = Clip)
        { return new FieldContextPoseCatalog(new[] { Entry(clip) }); }
        private static FieldPoseStart Start(string clip = Clip, string site = Site, int model = 98, bool full = true)
        { return new FieldPoseStart { Actor = 17, Model = model, Clip = clip, Site = site, FullRange = full, Cutscene = true }; }
        private static FieldPoseSample Sample(int frame, string clip = Clip, bool visible = true, bool present = true, bool clear = true)
        { return new FieldPoseSample { Actor = 17, Clip = clip, AnimFrame = frame, FrameCount = 25, Visible = visible, ClipPresent = present, ScreenClear = clear }; }
        private static void Play(FieldDescriptionTracker tracker, string clip = Clip)
        { for (int frame = 0; frame < 25; frame++) tracker.SamplePose(Sample(frame, clip)); }
        private static FieldDescriptionTracker Tracker(List<FieldDescriptionEmission> output, string clip = Clip)
        { return new FieldDescriptionTracker(output.Add, Catalog(clip)); }

        public static void ContextSelectionRequiresExactSiteModelAndActualClip()
        {
            var catalog = Catalog();
            TestAssert.Equal("test-context-pose", catalog.FindPose(98, Clip, Site).CueId);
            string[] sites = { null, "unknown", "51:17:" + Hash + ":617", "50:18:" + Hash + ":617", "50:17:" + new string('a', 64) + ":617", "50:17:" + Hash + ":618", Site + "0" };
            foreach (string site in sites) TestAssert.True(catalog.FindPose(98, Clip, site) == null);
            TestAssert.True(catalog.FindPose(99, Clip, Site) == null);
            TestAssert.True(catalog.FindPose(98, Clip.ToLowerInvariant(), Site) == null);
        }
        public static void ExactContextEmitsOneOverrideAfterTheExistingProofThreshold()
        {
            var output = new List<FieldDescriptionEmission>(); var tracker = Tracker(output);
            TestAssert.Equal("pending", tracker.StartPose(Start()));
            for (int frame = 0; frame < 4; frame++) tracker.SamplePose(Sample(frame));
            TestAssert.Equal(0, output.Count);
            for (int frame = 4; frame < 25; frame++) tracker.SamplePose(Sample(frame));
            TestAssert.Equal(1, output.Count);
            TestAssert.Equal("test-context-pose", output[0].CueId);
            TestAssert.Equal("A reviewed test action.", output[0].VoiceText);
            TestAssert.Equal("repeat", tracker.StartPose(Start()));
            Play(tracker); TestAssert.Equal(1, output.Count);
        }
        public static void WrongContextFallsBackToGenericWordingWithoutContextLeakage()
        {
            var output = new List<FieldDescriptionEmission>(); var tracker = Tracker(output);
            foreach (string site in new[] { "other", "50:17:" + new string('a', 64) + ":617" })
            {
                TestAssert.Equal("pending", tracker.StartPose(Start(site: site))); Play(tracker);
            }
            TestAssert.Equal(2, output.Count);
            foreach (var emission in output)
            {
                TestAssert.Equal("field-main-f0-zdn-hiza-1", emission.CueId);
                TestAssert.Equal("The boy with the tail crouches down.", emission.VoiceText);
            }
            TestAssert.Equal("field-main-f0-zdn-hiza-1", FieldDescriptionCatalog.FindPose(98, Clip, Site).CueId);
        }
        public static void ContextOnlyClipsAreEligibleButNeverGainGlobalWording()
        {
            var catalog = Catalog(ContextOnly); var output = new List<FieldDescriptionEmission>(); var tracker = Tracker(output, ContextOnly);
            TestAssert.True(catalog.CanReviewPose(98, ContextOnly));
            TestAssert.False(catalog.CanReviewPose(99, ContextOnly));
            TestAssert.False(catalog.CanReviewPose(98, ContextOnly.ToLowerInvariant()));
            TestAssert.True(FieldDescriptionCatalog.FindPose(98, ContextOnly) == null);
            TestAssert.True(FieldDescriptionCatalog.CanReviewPose(98, Clip));
            TestAssert.Equal("unreviewed", tracker.StartPose(Start(ContextOnly, "other")));
            TestAssert.Equal("pending", tracker.StartPose(Start(ContextOnly))); Play(tracker, ContextOnly);
            TestAssert.Equal(1, output.Count); TestAssert.Equal("test-context-pose", output[0].CueId);
        }
        public static void ContextProofRejectsHiddenMissingFadedChangedAndReversedSamples()
        {
            Func<FieldPoseSample>[] failures = {
                () => Sample(2, visible: false), () => Sample(2, present: false),
                () => Sample(2, clear: false), () => Sample(2, "different"), () => Sample(0)
            };
            foreach (var failure in failures)
            {
                var output = new List<FieldDescriptionEmission>(); var tracker = Tracker(output);
                tracker.StartPose(Start()); tracker.SamplePose(Sample(0)); tracker.SamplePose(Sample(1)); tracker.SamplePose(failure()); Play(tracker);
                TestAssert.Equal(0, output.Count); TestAssert.Equal(0, tracker.PendingCount);
                // Failed positive proof is not a delivered contextual action.
                TestAssert.Equal("pending", tracker.StartPose(Start())); Play(tracker); TestAssert.Equal(1, output.Count);
            }
        }
        public static void ContextProofStillRejectsPartialAmbientFrozenAndInsufficientProgress()
        {
            var output = new List<FieldDescriptionEmission>(); var tracker = Tracker(output);
            TestAssert.Equal("partial", tracker.StartPose(Start(full: false))); Play(tracker);
            var ambient = Start(); ambient.Cutscene = false;
            TestAssert.Equal("ambient", tracker.StartPose(ambient)); Play(tracker);
            tracker.StartPose(Start()); for (int i = 0; i < 30; i++) tracker.SamplePose(Sample(0));
            tracker.SamplePose(Sample(1)); tracker.SamplePose(Sample(2)); tracker.SamplePose(Sample(3));
            TestAssert.Equal(0, output.Count);
            tracker.SamplePose(Sample(8)); TestAssert.Equal(1, output.Count);
        }
        public static void ContextProofCannotOutliveActorsBoundariesOrPendingTimeout()
        {
            Action<FieldDescriptionTracker>[] interrupt = {
                tracker => tracker.Invalidate("menu"), tracker => tracker.ObserveActor(17, 99),
                tracker => tracker.EndFrame(new long[0]),
                tracker => { for (int i = 0; i <= FieldDescriptionTracker.PendingLimitFrames; i++) tracker.EndFrame(new long[] { 17 }); }
            };
            foreach (var boundary in interrupt)
            {
                var output = new List<FieldDescriptionEmission>(); var tracker = Tracker(output);
                tracker.StartPose(Start()); tracker.SamplePose(Sample(0)); boundary(tracker); Play(tracker);
                TestAssert.Equal(0, output.Count);
            }
            var cues = new List<FieldDescriptionEmission>(); var current = Tracker(cues);
            current.StartPose(Start()); Play(current); TestAssert.True(current.IsCurrent(cues[0]));
            current.EndFrame(new long[0]); current.ObserveActor(17, 98); TestAssert.False(current.IsCurrent(cues[0]));
            current.ResetScene("new visit"); current.StartPose(Start()); Play(current); TestAssert.Equal(2, cues.Count);
            current.Invalidate("focus lost"); TestAssert.False(current.IsCurrent(cues[1]));
            TestAssert.Equal("repeat", current.StartPose(Start()));
        }
        public static void ContextCatalogCopiesInputAndRejectsAmbiguousBindings()
        {
            var input = new[] { Entry() }; var catalog = new FieldContextPoseCatalog(input); input[0] = Entry(ContextOnly);
            TestAssert.Equal("test-context-pose", catalog.FindPose(98, Clip, Site).CueId);
            bool rejected = false;
            try { new FieldContextPoseCatalog(new[] { Entry(), Entry() }); } catch (ArgumentException) { rejected = true; }
            TestAssert.True(rejected);
            foreach (string hash in new[] { "abc", new string('z', 64), null })
            {
                rejected = false;
                try { new FieldContextPoseDescription(50, 17, hash, 617, 98, Clip, "test", "Test."); } catch (ArgumentException) { rejected = true; }
                TestAssert.True(rejected);
            }
        }
        private static FieldContextPoseDescription RangedEntry(int first = 67, int last = 71, int count = 80)
        { return new FieldContextPoseDescription(50, 17, Hash, 617, 98, Clip, "test-reviewed-range", "A reviewed test segment.", first, last, count); }
        private static FieldDescriptionTracker RangedTracker(List<FieldDescriptionEmission> output, int first = 67, int last = 71, int count = 80)
        { return new FieldDescriptionTracker(output.Add, new FieldContextPoseCatalog(new[] { RangedEntry(first, last, count) })); }
        private static FieldPoseStart RangedStart(int first = 67, int last = 71, int count = 80)
        {
            var start = Start(full: false);
            start.InFrame = first; start.OutFrame = last; start.FrameCount = count; start.Forward = true; start.NonJump = true;
            return start;
        }
        private static FieldPoseSample RangedSample(int frame, int count = 80)
        { var sample = Sample(frame); sample.FrameCount = count; return sample; }
        private static void PlayRange(FieldDescriptionTracker tracker, int first = 67, int last = 71, int count = 80)
        { for (int frame = first; frame <= last; frame++) tracker.SamplePose(RangedSample(frame, count)); }

        public static void ExactReviewedForwardPartialRangeCanProveItsOwnCaption()
        {
            var output = new List<FieldDescriptionEmission>(); var tracker = RangedTracker(output);
            TestAssert.Equal("pending", tracker.StartPose(RangedStart()));
            for (int frame = 67; frame <= 69; frame++) tracker.SamplePose(RangedSample(frame));
            TestAssert.Equal(0, output.Count); // Still needs three distinct progressed samples.
            tracker.SamplePose(RangedSample(70));
            TestAssert.Equal(1, output.Count); TestAssert.Equal("test-reviewed-range", output[0].CueId);
            TestAssert.Equal("repeat", tracker.StartPose(RangedStart()));
            PlayRange(tracker); TestAssert.Equal(1, output.Count);
        }
        public static void PartialProofFractionUsesTheReviewedRangeSpan()
        {
            var output = new List<FieldDescriptionEmission>(); var tracker = RangedTracker(output, 20, 60, 100);
            TestAssert.Equal("pending", tracker.StartPose(RangedStart(20, 60, 100)));
            for (int frame = 20; frame < 32; frame++) tracker.SamplePose(RangedSample(frame, 100));
            TestAssert.Equal(0, output.Count);
            tracker.SamplePose(RangedSample(32, 100));
            TestAssert.Equal(1, output.Count); // 12/40 = 30%, not 12/99.
        }
        public static void PartialRangesCannotBypassExactBoundsCountForwardOrJumpProof()
        {
            Action<FieldPoseStart>[] mutations = {
                start => start.InFrame = 66, start => start.OutFrame = 72,
                start => start.FrameCount = 79, start => start.FrameCount = 255,
                start => start.FrameCount = 1, start => start.FrameCount = 0,
                start => start.Forward = false, start => start.NonJump = false,
                start => { start.Forward = false; start.FullRange = true; },
                start => { start.NonJump = false; start.FullRange = true; }
            };
            foreach (var mutation in mutations)
            {
                var output = new List<FieldDescriptionEmission>(); var tracker = RangedTracker(output); var start = RangedStart(); mutation(start);
                TestAssert.False(tracker.StartPose(start) == "pending"); PlayRange(tracker); TestAssert.Equal(0, output.Count);
                TestAssert.Equal("pending", tracker.StartPose(RangedStart())); PlayRange(tracker); TestAssert.Equal(1, output.Count);
            }
        }
        public static void GenericAndUnreviewedContextRangesStaySilent()
        {
            var output = new List<FieldDescriptionEmission>(); var tracker = Tracker(output);
            TestAssert.Equal("partial", tracker.StartPose(RangedStart())); PlayRange(tracker);
            var generic = RangedStart(); generic.Site = "other";
            TestAssert.Equal("partial", tracker.StartPose(generic)); PlayRange(tracker);
            var ranged = RangedTracker(output); TestAssert.Equal("partial", ranged.StartPose(generic)); PlayRange(ranged);
            var noMetadata = Start(full: false);
            TestAssert.False(ranged.StartPose(noMetadata) == "pending"); PlayRange(ranged);
            TestAssert.Equal(0, output.Count);
        }
        public static void PartialSamplesRequireReviewedBoundsCountAndPositivePresentation()
        {
            Func<FieldPoseSample>[] failures = {
                () => RangedSample(66), () => RangedSample(72), () => RangedSample(69, 79), () => RangedSample(69, 255),
                () => { var sample = RangedSample(69); sample.Visible = false; return sample; },
                () => { var sample = RangedSample(69); sample.ClipPresent = false; return sample; },
                () => { var sample = RangedSample(69); sample.ScreenClear = false; return sample; },
                () => { var sample = RangedSample(69); sample.Clip = "different"; return sample; },
                () => RangedSample(67)
            };
            foreach (var failure in failures)
            {
                var output = new List<FieldDescriptionEmission>(); var tracker = RangedTracker(output);
                tracker.StartPose(RangedStart()); tracker.SamplePose(RangedSample(67)); tracker.SamplePose(RangedSample(68)); tracker.SamplePose(failure());
                PlayRange(tracker); TestAssert.Equal(0, output.Count); TestAssert.Equal(0, tracker.PendingCount);
                TestAssert.Equal("pending", tracker.StartPose(RangedStart())); PlayRange(tracker); TestAssert.Equal(1, output.Count);
            }
        }
        public static void PartialProofStillExpiresAndRequiresTheOriginalActorLifetime()
        {
            Action<FieldDescriptionTracker>[] interruptions = {
                tracker => tracker.Invalidate("focus"), tracker => tracker.ObserveActor(17, 99), tracker => tracker.EndFrame(new long[0]),
                tracker => { for (int i = 0; i <= FieldDescriptionTracker.PendingLimitFrames; i++) tracker.EndFrame(new long[] { 17 }); }
            };
            foreach (var interruption in interruptions)
            {
                var output = new List<FieldDescriptionEmission>(); var tracker = RangedTracker(output);
                tracker.StartPose(RangedStart()); tracker.SamplePose(RangedSample(67)); interruption(tracker); PlayRange(tracker);
                TestAssert.Equal(0, output.Count);
            }
            var cues = new List<FieldDescriptionEmission>(); var current = RangedTracker(cues);
            current.StartPose(RangedStart()); PlayRange(current); TestAssert.True(current.IsCurrent(cues[0]));
            current.EndFrame(new long[0]); current.ObserveActor(17, 98); TestAssert.False(current.IsCurrent(cues[0]));
            current.ResetScene("new visit"); current.StartPose(RangedStart()); PlayRange(current); TestAssert.Equal(2, cues.Count);
        }
        public static void ImpossibleReviewedRangesAreRejectedAtCatalogConstruction()
        {
            int[][] ranges = { new[] { -1, 71, 80 }, new[] { 71, 67, 80 }, new[] { 67, 67, 80 }, new[] { 67, 80, 80 }, new[] { 67, 71, 255 }, new[] { 0, 1, 1 } };
            foreach (var range in ranges)
            {
                bool rejected = false;
                try { RangedEntry(range[0], range[1], range[2]); } catch (ArgumentException) { rejected = true; }
                TestAssert.True(rejected);
            }
        }
        public static void ShortReviewedRangesCannotSkipTheMinimumProgressedSamples()
        {
            var output = new List<FieldDescriptionEmission>(); var tracker = RangedTracker(output, 67, 69, 80);
            TestAssert.Equal("pending", tracker.StartPose(RangedStart(67, 69, 80)));
            for (int i = 0; i < 30; i++) tracker.SamplePose(RangedSample(67));
            tracker.SamplePose(RangedSample(68)); tracker.SamplePose(RangedSample(69));
            TestAssert.Equal(0, output.Count);
            tracker.SamplePose(RangedSample(70)); TestAssert.Equal(0, tracker.PendingCount); TestAssert.Equal(0, output.Count);
        }
    }
}
