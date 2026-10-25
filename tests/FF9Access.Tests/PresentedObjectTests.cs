using System;
using System.Collections.Generic;
using Memoria.Accessibility.AudioDescriptions;
namespace FF9Access.Tests
{
    internal static class PresentedObjectTests
    {
        private const string Hash="0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",Clip="TEST_REVIEWED_OBJECT",Cue="test-presented-object",Text="A reviewed visible object.";
        private static FieldPresentedObjectDescription Entry(FieldPresentedObjectMode mode=FieldPresentedObjectMode.Static,int sid=3,string cue=Cue,string text=Text)
        {return new FieldPresentedObjectDescription(400,sid,Hash,5468,Clip,cue,text,mode);}
        private static FieldPresentedObjectCatalog Catalog(FieldPresentedObjectMode mode=FieldPresentedObjectMode.Static)
        {return new FieldPresentedObjectCatalog(new[]{Entry(mode)});}
        private static FieldDescriptionTracker Tracker(List<FieldDescriptionEmission> cues,FieldPresentedObjectMode mode=FieldPresentedObjectMode.Static)
        {return new FieldDescriptionTracker(cues.Add,new FieldContextPoseCatalog(new FieldContextPoseDescription[0]),Catalog(mode));}
        private static FieldPresentedObjectSample Sample(long frame,int anim=0,long actor=1,float x=100)
        {return new FieldPresentedObjectSample{Actor=actor,PresentationFrame=frame,Map=400,Sid=3,Model=5468,EntrySha256=Hash,Clip=Clip,ClipPresent=true,Visible=true,ScreenClear=true,FrameCount=25,AnimFrame=anim,InFrame=0,OutFrame=254,FullRange=true,Forward=true,NonJump=true,PositionKnown=true,X=x,Y=200,Z=300,Speed=1};}
        private static void StaticPlay(FieldDescriptionTracker tracker,long actor=1,long first=0)
        {for(long i=first;i<first+3;i++)tracker.SamplePresentedObject(Sample(i,actor:actor));}
        public static void ActualVisibleStaticObjectCanEarnWithoutAnimationOrCutscene()
        {
            var cues=new List<FieldDescriptionEmission>();var tracker=Tracker(cues);
            var first=Sample(0);first.FrameCount=1;first.FullRange=false;first.Forward=false;tracker.SamplePresentedObject(first);
            tracker.SamplePresentedObject(Sample(1));TestAssert.Equal(0,cues.Count);tracker.SamplePresentedObject(Sample(2));TestAssert.Equal(1,cues.Count);
            TestAssert.Equal(Cue,cues[0].CueId);TestAssert.Equal(Text,cues[0].VoiceText);TestAssert.True(tracker.IsCurrent(cues[0]));
        }
        public static void CatalogIdentityIsExactAndCandidateFilterDoesNotGrantCaption()
        {
            var catalog=Catalog();TestAssert.True(catalog.HasMap(400));TestAssert.False(catalog.HasMap(401));TestAssert.True(catalog.CanReview(400,3,5468,Clip));
            TestAssert.True(catalog.Find(400,3,Hash,5468,Clip)!=null);
            TestAssert.True(catalog.Find(401,3,Hash,5468,Clip)==null);TestAssert.True(catalog.Find(400,4,Hash,5468,Clip)==null);
            TestAssert.True(catalog.Find(400,3,new string('a',64),5468,Clip)==null);TestAssert.True(catalog.Find(400,3,Hash,5469,Clip)==null);TestAssert.True(catalog.Find(400,3,Hash,5468,Clip.ToLowerInvariant())==null);
            Action<FieldPresentedObjectSample>[] wrong={s=>s.Map=401,s=>s.Sid=4,s=>s.EntrySha256=new string('a',64),s=>s.Model=5469,s=>s.Clip="other"};
            foreach(var mutate in wrong){var cues=new List<FieldDescriptionEmission>();var tracker=Tracker(cues);for(int i=0;i<10;i++){var s=Sample(i);mutate(s);tracker.SamplePresentedObject(s);}TestAssert.Equal(0,cues.Count);StaticPlay(tracker,first:10);TestAssert.Equal(1,cues.Count);}
        }
        public static void InitializationFadeOffscreenAndMissingClipDoNotConsumeVisibleCue()
        {
            Action<FieldPresentedObjectSample>[] hidden={s=>s.Visible=false,s=>s.ScreenClear=false,s=>s.ClipPresent=false};
            foreach(var mutate in hidden){var cues=new List<FieldDescriptionEmission>();var tracker=Tracker(cues);for(int i=0;i<10;i++){var sample=Sample(i);mutate(sample);tracker.SamplePresentedObject(sample);}TestAssert.Equal(0,cues.Count);TestAssert.Equal(0,tracker.PresentedPendingCount);StaticPlay(tracker,first:10);TestAssert.Equal(1,cues.Count);}
        }
        public static void VisibilityInterruptionResetsUnprovenSamplesAndCanRetry()
        {
            var cues=new List<FieldDescriptionEmission>();var tracker=Tracker(cues);tracker.SamplePresentedObject(Sample(0));tracker.SamplePresentedObject(Sample(1));
            var hidden=Sample(2);hidden.Visible=false;tracker.SamplePresentedObject(hidden);tracker.SamplePresentedObject(Sample(3));tracker.SamplePresentedObject(Sample(4));TestAssert.Equal(0,cues.Count);
            tracker.SamplePresentedObject(Sample(5));TestAssert.Equal(1,cues.Count);
        }
        public static void StaticProofRequiresDistinctPresentationFrames()
        {
            var cues=new List<FieldDescriptionEmission>();var tracker=Tracker(cues);for(int i=0;i<30;i++)tracker.SamplePresentedObject(Sample(0));TestAssert.Equal(0,cues.Count);
            tracker.SamplePresentedObject(Sample(1));TestAssert.Equal(0,cues.Count);tracker.SamplePresentedObject(Sample(2));TestAssert.Equal(1,cues.Count);
        }
        public static void AnimatedObjectNeedsAdvancingFullForwardNativeCycle()
        {
            var cues=new List<FieldDescriptionEmission>();var tracker=Tracker(cues,FieldPresentedObjectMode.Animated);
            for(int i=0;i<30;i++)tracker.SamplePresentedObject(Sample(i));TestAssert.Equal(0,cues.Count);
            tracker.Invalidate("retry");tracker.SamplePresentedObject(Sample(30,0));tracker.SamplePresentedObject(Sample(31,1));tracker.SamplePresentedObject(Sample(32,2));TestAssert.Equal(0,cues.Count);
            tracker.SamplePresentedObject(Sample(33,8));TestAssert.Equal(1,cues.Count);
        }
        public static void AnimatedPartialReverseJumpMissingCountAndChangedLengthStaySilent()
        {
            Action<FieldPresentedObjectSample>[] invalid={s=>s.FullRange=false,s=>s.Forward=false,s=>s.NonJump=false,s=>s.InFrame=1,s=>s.OutFrame=15,s=>s.FrameCount=255,s=>s.FrameCount=1,s=>s.AnimFrame=25};
            foreach(var mutate in invalid){var cues=new List<FieldDescriptionEmission>();var tracker=Tracker(cues,FieldPresentedObjectMode.Animated);for(int i=0;i<12;i++){var sample=Sample(i,i);mutate(sample);tracker.SamplePresentedObject(sample);}TestAssert.Equal(0,cues.Count);}
            var output=new List<FieldDescriptionEmission>();var current=Tracker(output,FieldPresentedObjectMode.Animated);current.SamplePresentedObject(Sample(0,0));current.SamplePresentedObject(Sample(1,1));var changed=Sample(2,2);changed.FrameCount=24;current.SamplePresentedObject(changed);current.SamplePresentedObject(Sample(3,8));TestAssert.Equal(0,output.Count);
        }
        public static void AnimatedWrapDiscardsPriorCycleAndCanEarnInLaterCycle()
        {
            var cues=new List<FieldDescriptionEmission>();var tracker=Tracker(cues,FieldPresentedObjectMode.Animated);tracker.SamplePresentedObject(Sample(0,22));tracker.SamplePresentedObject(Sample(1,23));tracker.SamplePresentedObject(Sample(2,0));TestAssert.Equal(0,cues.Count);
            for(int i=1;i<=9;i++)tracker.SamplePresentedObject(Sample(i+2,i));TestAssert.Equal(1,cues.Count);
        }
        public static void MovingObjectRequiresObservedFiniteDisplacementBeyondHalfAUnit()
        {
            var cues=new List<FieldDescriptionEmission>();var tracker=Tracker(cues,FieldPresentedObjectMode.Moving);
            tracker.SamplePresentedObject(Sample(0,x:10000));tracker.SamplePresentedObject(Sample(1,x:10000));tracker.SamplePresentedObject(Sample(2,x:10000));TestAssert.Equal(0,cues.Count);
            tracker.SamplePresentedObject(Sample(3,x:10000.5f));TestAssert.Equal(0,cues.Count);tracker.SamplePresentedObject(Sample(4,x:10000.6f));TestAssert.Equal(1,cues.Count);
        }
        public static void MovingObjectDoesNotCountSpawnTeleportOrInvalidCoordinates()
        {
            var cues=new List<FieldDescriptionEmission>();var tracker=Tracker(cues,FieldPresentedObjectMode.Moving);tracker.SamplePresentedObject(Sample(0,x:0));tracker.SamplePresentedObject(Sample(1,x:10000));
            for(int i=2;i<5;i++)tracker.SamplePresentedObject(Sample(i,x:10000));TestAssert.Equal(0,cues.Count);
            tracker.SamplePresentedObject(Sample(5,x:10001));TestAssert.Equal(1,cues.Count);
            foreach(var mutate in new Action<FieldPresentedObjectSample>[]{s=>s.PositionKnown=false,s=>s.X=Single.NaN,s=>s.Y=Single.PositiveInfinity,s=>s.Speed=-1}){
                cues=new List<FieldDescriptionEmission>();tracker=Tracker(cues,FieldPresentedObjectMode.Moving);for(int i=0;i<10;i++){var sample=Sample(i,x:100+i);mutate(sample);tracker.SamplePresentedObject(sample);}TestAssert.Equal(0,cues.Count);
            }
        }
        public static void ActualMovementCanBeInYOrZWithoutAnInventedDestination()
        {
            var cues=new List<FieldDescriptionEmission>();var tracker=Tracker(cues,FieldPresentedObjectMode.Moving);
            for(int i=0;i<3;i++){var sample=Sample(i);sample.Z+=i*.3f;tracker.SamplePresentedObject(sample);}TestAssert.Equal(1,cues.Count);
        }
        public static void ClonesShareCueDedupeButNeverSharePresentationSamples()
        {
            var cues=new List<FieldDescriptionEmission>();var tracker=Tracker(cues);tracker.SamplePresentedObject(Sample(0,actor:128));tracker.SamplePresentedObject(Sample(1,actor:129));tracker.SamplePresentedObject(Sample(2,actor:130));TestAssert.Equal(0,cues.Count);
            StaticPlay(tracker,actor:129,first:3);TestAssert.Equal(1,cues.Count);StaticPlay(tracker,actor:128,first:6);TestAssert.Equal(1,cues.Count);
            tracker.Invalidate("menu/reentry presentation");StaticPlay(tracker,actor:131,first:9);TestAssert.Equal(1,cues.Count);
            tracker.ResetScene("new field visit");StaticPlay(tracker,actor:131,first:12);TestAssert.Equal(2,cues.Count);
        }
        public static void BoundariesAndActorReplacementCancelPendingAndQueuedValidity()
        {
            Action<FieldDescriptionTracker>[] boundaries={t=>t.Invalidate("pause/focus/menu/battle"),t=>t.ObserveActor(1,5469),t=>t.EndFrame(new long[0])};
            foreach(var boundary in boundaries){var cues=new List<FieldDescriptionEmission>();var tracker=Tracker(cues);tracker.SamplePresentedObject(Sample(0));tracker.SamplePresentedObject(Sample(1));boundary(tracker);tracker.SamplePresentedObject(Sample(2));TestAssert.Equal(0,cues.Count);tracker.SamplePresentedObject(Sample(3));tracker.SamplePresentedObject(Sample(4));TestAssert.Equal(1,cues.Count);TestAssert.True(tracker.IsCurrent(cues[0]));boundary(tracker);TestAssert.False(tracker.IsCurrent(cues[0]));}
        }
        public static void CatalogCopiesInputAndRejectsAmbiguousOrInvalidDeclarations()
        {
            var rows=new[]{Entry()};var catalog=new FieldPresentedObjectCatalog(rows);rows[0]=Entry(sid:5);TestAssert.True(catalog.Find(400,3,Hash,5468,Clip)!=null);
            Func<FieldPresentedObjectCatalog>[] invalid={()=>new FieldPresentedObjectCatalog(new[]{Entry(),Entry()}),()=>new FieldPresentedObjectCatalog(new[]{Entry(),Entry(sid:5,text:"Contradictory caption.")}),()=>new FieldPresentedObjectCatalog(new FieldPresentedObjectDescription[]{null})};
            foreach(var create in invalid){bool rejected=false;try{create();}catch(ArgumentException){rejected=true;}TestAssert.True(rejected);}
            bool badHash=false;try{new FieldPresentedObjectDescription(400,3,"bad",5468,Clip,Cue,Text,FieldPresentedObjectMode.Static);}catch(ArgumentException){badHash=true;}TestAssert.True(badHash);
        }
        public static void PendingObjectsStayBoundedAndExpireWithoutConsumingCue()
        {
            var cues=new List<FieldDescriptionEmission>();var tracker=Tracker(cues);
            for(long actor=1;actor<=300;actor++)tracker.SamplePresentedObject(Sample(0,actor:actor));TestAssert.True(tracker.PresentedPendingCount<=256);
            tracker.EndFrame(new long[0]);TestAssert.Equal(0,tracker.PresentedPendingCount);
            tracker.SamplePresentedObject(Sample(1));tracker.SamplePresentedObject(Sample(2));
            for(int i=0;i<=FieldDescriptionTracker.PendingLimitFrames;i++)tracker.EndFrame(new long[]{1});TestAssert.Equal(0,tracker.PresentedPendingCount);
            StaticPlay(tracker,first:3);TestAssert.Equal(1,cues.Count);
        }
        public static void ChangedIdentityAndRewoundPresentationFramesDiscardPartialProof()
        {
            var cues=new List<FieldDescriptionEmission>();var tracker=Tracker(cues);tracker.SamplePresentedObject(Sample(10));tracker.SamplePresentedObject(Sample(11));tracker.SamplePresentedObject(Sample(9));
            tracker.SamplePresentedObject(Sample(12));tracker.SamplePresentedObject(Sample(13));TestAssert.Equal(0,cues.Count);
            var changed=Sample(14);changed.EntrySha256=new string('a',64);tracker.SamplePresentedObject(changed);TestAssert.Equal(0,tracker.PresentedPendingCount);
            StaticPlay(tracker,first:15);TestAssert.Equal(1,cues.Count);
        }
        public static void CapacityCannotPreserveProofAcrossAnotherReviewedClip()
        {
            var other=new FieldPresentedObjectDescription(400,3,Hash,5468,"OTHER_REVIEWED_OBJECT","test-other-object","Another visible object.",FieldPresentedObjectMode.Static);
            var cues=new List<FieldDescriptionEmission>();var tracker=new FieldDescriptionTracker(cues.Add,new FieldContextPoseCatalog(new FieldContextPoseDescription[0]),new FieldPresentedObjectCatalog(new[]{Entry(),other}));
            for(long actor=1;actor<=256;actor++)tracker.SamplePresentedObject(Sample(0,actor:actor));tracker.SamplePresentedObject(Sample(1));
            var changed=Sample(2);changed.Clip=other.Clip;tracker.SamplePresentedObject(changed);tracker.SamplePresentedObject(Sample(3));TestAssert.Equal(0,cues.Count);
            tracker.SamplePresentedObject(Sample(4));TestAssert.Equal(0,cues.Count);tracker.SamplePresentedObject(Sample(5));TestAssert.Equal(1,cues.Count);
        }
        public static void DeliveredCuePreflightSkipsSharedClonesAndDropsTheirPendingProof()
        {
            var cues=new List<FieldDescriptionEmission>();var catalog=new FieldPresentedObjectCatalog(new[]{Entry(),Entry(sid:5),Entry(sid:7)});
            var tracker=new FieldDescriptionTracker(cues.Add,new FieldContextPoseCatalog(new FieldContextPoseDescription[0]),catalog);
            tracker.SamplePresentedObject(Sample(0,actor:128));var other=Sample(0,actor:129);other.Sid=5;tracker.SamplePresentedObject(other);
            TestAssert.Equal(2,tracker.PresentedPendingCount);StaticPlay(tracker,actor:1,first:1);TestAssert.Equal(1,cues.Count);
            TestAssert.False(tracker.NeedsPresentedObjectSample(128,400,3,Hash,5468,Clip));TestAssert.False(tracker.IsPresentedPending(128));
            TestAssert.False(tracker.NeedsPresentedObjectSample(129,400,5,Hash,5468,Clip));TestAssert.False(tracker.IsPresentedPending(129));
            TestAssert.False(tracker.NeedsPresentedObjectSample(130,400,7,Hash,5468,Clip));TestAssert.False(tracker.NeedsPresentedObjectSample(1,400,3,Hash,5468,Clip));
            TestAssert.Equal(0,tracker.PresentedPendingCount);TestAssert.Equal(1,cues.Count);
            tracker.Invalidate("focus/menu");TestAssert.False(tracker.NeedsPresentedObjectSample(128,400,3,Hash,5468,Clip));
            tracker.ResetScene("next visit");TestAssert.True(tracker.NeedsPresentedObjectSample(128,400,3,Hash,5468,Clip));
        }
        public static void PreflightWrongIdentityDiscardsPendingButUnseenHiddenCueCanRetry()
        {
            var cues=new List<FieldDescriptionEmission>();var tracker=Tracker(cues);TestAssert.True(tracker.NeedsPresentedObjectSample(1,400,3,Hash,5468,Clip));
            tracker.SamplePresentedObject(Sample(0));tracker.SamplePresentedObject(Sample(1));TestAssert.True(tracker.IsPresentedPending(1));
            TestAssert.False(tracker.NeedsPresentedObjectSample(1,400,3,new string('a',64),5468,Clip));TestAssert.False(tracker.IsPresentedPending(1));
            TestAssert.True(tracker.NeedsPresentedObjectSample(1,400,3,Hash,5468,Clip));var hidden=Sample(2);hidden.Visible=false;tracker.SamplePresentedObject(hidden);
            TestAssert.True(tracker.NeedsPresentedObjectSample(1,400,3,Hash,5468,Clip));StaticPlay(tracker,first:3);TestAssert.Equal(1,cues.Count);
            TestAssert.False(tracker.NeedsPresentedObjectSample(1,400,3,Hash,5469,Clip));TestAssert.False(tracker.IsCurrent(cues[0]));
        }
        public static void PreflightNewReviewedIdentityCannotReuseObsoletePendingSamples()
        {
            var other=new FieldPresentedObjectDescription(400,3,Hash,5468,"OTHER_REVIEWED_OBJECT","other-object","Another visible object.",FieldPresentedObjectMode.Static);
            var cues=new List<FieldDescriptionEmission>();var tracker=new FieldDescriptionTracker(cues.Add,new FieldContextPoseCatalog(new FieldContextPoseDescription[0]),new FieldPresentedObjectCatalog(new[]{Entry(),other}));
            tracker.SamplePresentedObject(Sample(0));tracker.SamplePresentedObject(Sample(1));TestAssert.True(tracker.IsPresentedPending(1));
            TestAssert.True(tracker.NeedsPresentedObjectSample(1,400,3,Hash,5468,other.Clip));TestAssert.False(tracker.IsPresentedPending(1));
            tracker.SamplePresentedObject(Sample(2));TestAssert.Equal(0,cues.Count);
        }
    }
}
