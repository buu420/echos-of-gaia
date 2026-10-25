using System;
using System.Collections.Generic;
using Memoria.Accessibility.AudioDescriptions;
namespace FF9Access.Tests
{
    internal static class FieldDescriptionMotionTests
    {
        private sealed class Walker
        {
            internal float X, Y, Z, Yaw; internal FieldLocomotion Gait = FieldLocomotion.Walk;
            internal bool Rendered = true, OnScreen, NearEdge, Jumping, Turning, Cutscene = true, Clear = true; internal int Speed = 30;
            internal FieldMotionSample Sample() { return new FieldMotionSample { Actor = 17, Model = 98, X = X, Y = Y, Z = Z, Yaw = Yaw, Locomotion = Gait, Rendered = Rendered, OnScreen = OnScreen, NearEdge = NearEdge, Jumping = Jumping, Turning = Turning, Cutscene = Cutscene, ScreenClear = Clear, Speed = Speed }; }
        }
        private static FieldDescriptionTracker Tracker(List<FieldDescriptionEmission> output) { return new FieldDescriptionTracker(output.Add); }
        private static void Step(FieldDescriptionTracker t, Walker w, int frames, float dx)
        { for (int i = 0; i < frames; i++) { w.X += dx; t.ObserveMotion(w.Sample()); t.EndFrame(new long[] { 17 }); } }

        public static void WalkingInFromTheScreenEdgeIsDescribedOnce()
        {
            var output = new List<FieldDescriptionEmission>(); var t = Tracker(output); var w = new Walker();
            Step(t, w, 5, 30); w.OnScreen = true; w.NearEdge = true; Step(t, w, 3, 30); w.NearEdge = false; Step(t, w, 40, 30);
            TestAssert.Equal(1, output.Count); TestAssert.Equal("field-motion-main-f0-zdn-walk-into-view", output[0].CueId);
            TestAssert.Equal("The boy with the tail walks into view.", output[0].VoiceText);
        }
        public static void RunningOutAtTheEdgeIsDescribed()
        {
            var output = new List<FieldDescriptionEmission>(); var t = Tracker(output); var w = new Walker { OnScreen = true, Gait = FieldLocomotion.Run, Speed = 60 };
            Step(t, w, 20, 60); w.NearEdge = true; Step(t, w, 2, 60); w.OnScreen = false; w.NearEdge = false; Step(t, w, 5, 60);
            TestAssert.Equal(1, output.Count); TestAssert.Equal("field-motion-main-f0-zdn-run-out-of-view", output[0].CueId);
        }
        public static void AppearingMidScreenOrBeingHiddenIsNotWalkingIntoOrOutOfView()
        {
            var output = new List<FieldDescriptionEmission>(); var t = Tracker(output); var w = new Walker();
            Step(t, w, 5, 30); w.OnScreen = true; Step(t, w, 30, 30);
            w.Rendered = false; w.OnScreen = false; Step(t, w, 10, 30);
            TestAssert.Equal(0, output.Count);
        }
        public static void TeleportsAndStillPosesAreNotMotion()
        {
            var output = new List<FieldDescriptionEmission>(); var t = Tracker(output); var w = new Walker();
            Step(t, w, 5, 30); w.OnScreen = true; w.NearEdge = true; Step(t, w, 1, 30); Step(t, w, 1, 4000); w.NearEdge = false; Step(t, w, 40, 30);
            var s = new Walker { OnScreen = true, Gait = FieldLocomotion.Still }; Step(t, s, 40, 0);
            TestAssert.Equal(0, output.Count);
        }
        public static void PlayerFreeWalkingAndFadedScreensAreSuppressed()
        {
            var output = new List<FieldDescriptionEmission>(); var t = Tracker(output);
            var free = new Walker { Cutscene = false }; Step(t, free, 5, 30); free.OnScreen = true; free.NearEdge = true; Step(t, free, 3, 30); Step(t, free, 30, 30);
            var faded = new Walker { Clear = false }; Step(t, faded, 5, 30); faded.OnScreen = true; faded.NearEdge = true; Step(t, faded, 3, 30); Step(t, faded, 30, 30);
            TestAssert.Equal(0, output.Count);
        }
        public static void ObservedJumpsUseActualHeightChange()
        {
            var output = new List<FieldDescriptionEmission>(); var t = Tracker(output); var w = new Walker { OnScreen = true, Gait = FieldLocomotion.Other };
            t.ObserveMotion(w.Sample()); w.Jumping = true;
            for (int i = 1; i <= 8; i++) { w.Y -= 50; w.X += 200; t.ObserveMotion(w.Sample()); }
            w.Jumping = false; t.ObserveMotion(w.Sample());
            TestAssert.Equal(1, output.Count); TestAssert.Equal("The boy with the tail jumps down.", output[0].VoiceText);
            var hidden = new Walker { OnScreen = true, Gait = FieldLocomotion.Other }; t.ObserveMotion(hidden.Sample()); hidden.Jumping = true; t.ObserveMotion(hidden.Sample());
            hidden.OnScreen = false; hidden.Y += 400; t.ObserveMotion(hidden.Sample()); hidden.OnScreen = true; hidden.Jumping = false; t.ObserveMotion(hidden.Sample());
            TestAssert.Equal(1, output.Count);
        }
        public static void OnlyLargeVisibleTurnsAreDescribed()
        {
            var output = new List<FieldDescriptionEmission>(); var t = Tracker(output); var w = new Walker { OnScreen = true, Gait = FieldLocomotion.Other, Yaw = 10 };
            t.ObserveMotion(w.Sample()); w.Turning = true; for (int i = 0; i < 6; i++) { w.Yaw += 15; t.ObserveMotion(w.Sample()); } w.Turning = false; t.ObserveMotion(w.Sample());
            TestAssert.Equal(0, output.Count);
            w.Turning = true; for (int i = 0; i < 6; i++) { w.Yaw += 30; t.ObserveMotion(w.Sample()); } w.Turning = false; t.ObserveMotion(w.Sample());
            TestAssert.Equal(1, output.Count); TestAssert.Equal("field-motion-main-f0-zdn-turn-around", output[0].CueId);
        }
        public static void FirstObservationMidJumpDoesNotInventTakeoffHeight()
        {
            var output=new List<FieldDescriptionEmission>();var t=Tracker(output);
            var w=new Walker{OnScreen=true,Gait=FieldLocomotion.Other,Jumping=true,Y=300};
            t.ObserveMotion(w.Sample());w.Y=160;t.ObserveMotion(w.Sample());w.Y=0;w.Jumping=false;t.ObserveMotion(w.Sample());
            TestAssert.Equal(0,output.Count);
        }
        public static void PauseDuringJumpRequiresAFreshGroundedTakeoff()
        {
            var output=new List<FieldDescriptionEmission>();var t=Tracker(output);
            var w=new Walker{OnScreen=true,Gait=FieldLocomotion.Other};
            t.ObserveMotion(w.Sample());w.Jumping=true;w.Y=40;t.ObserveMotion(w.Sample());t.Invalidate("pause");
            w.Y=300;t.ObserveMotion(w.Sample());w.Y=160;t.ObserveMotion(w.Sample());w.Y=0;w.Jumping=false;t.ObserveMotion(w.Sample());
            TestAssert.Equal(0,output.Count);
            w.Jumping=true;w.Y=100;t.ObserveMotion(w.Sample());w.Y=300;t.ObserveMotion(w.Sample());w.Jumping=false;w.Y=0;t.ObserveMotion(w.Sample());
            TestAssert.Equal(1,output.Count);TestAssert.Equal("The boy with the tail jumps.",output[0].VoiceText);
        }
        public static void RepeatedSynchronousFramesAreOneEpisodeAndUnknownModelsAreIgnored()
        {
            var output = new List<FieldDescriptionEmission>(); var t = Tracker(output); var w = new Walker();
            Step(t, w, 5, 30); w.OnScreen = true; w.NearEdge = true; Step(t, w, 3, 30); w.NearEdge = false; Step(t, w, 200, 30);
            TestAssert.Equal(1, output.Count);
            var stranger = new FieldMotionSample { Actor = 40, Model = 4000, Rendered = true, OnScreen = true, NearEdge = true, Cutscene = true, ScreenClear = true, Locomotion = FieldLocomotion.Walk };
            for (int i = 0; i < 30; i++) { stranger.X += 30; t.ObserveMotion(stranger); }
            TestAssert.Equal(1, output.Count);
        }
    }
}
