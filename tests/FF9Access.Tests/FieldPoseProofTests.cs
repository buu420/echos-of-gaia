using System;
using System.Collections.Generic;
using Memoria.Accessibility.AudioDescriptions;
namespace FF9Access.Tests
{
    internal static class FieldPoseProofTests
    {
        private const string Hash="0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        private const string Site="502:12:"+Hash+":564",SequenceSite="502:12:"+Hash+":590",Clip="TEST_ONLY_SEMANTIC_POSE";
        private static FieldPoseProof Pipe(){return new FieldPoseProof(.7,590,5,.5f,-1,-1);}
        private static FieldPoseProof Companion(){return new FieldPoseProof(0,-1,-1,0,10,8);}
        private static FieldDescriptionTracker Tracker(List<FieldDescriptionEmission> cues,FieldPoseProof proof)
        {return new FieldDescriptionTracker(cues.Add,new FieldContextPoseCatalog(new[]{new FieldContextPoseDescription(502,12,Hash,564,98,Clip,"test-semantic-pose","A reviewed composite action.",proof)}));}
        private static FieldPoseStart Start()
        {return new FieldPoseStart{Actor=12,Model=98,Clip=Clip,Site=Site,Cutscene=true,FullRange=true,FrameCount=30,PositionKnown=true,X=100,Y=200,Z=300,CompanionActor=10010,CompanionVisible=true};}
        private static FieldPoseSample Sample(int frame,float displacement=1)
        {return new FieldPoseSample{Actor=12,Clip=Clip,AnimFrame=frame,FrameCount=30,ClipPresent=true,Visible=true,ScreenClear=true,PositionKnown=true,X=100+displacement,Y=200,Z=300,CompanionActor=10010,CompanionVisible=true};}
        private static void Play(FieldDescriptionTracker tracker,int first=0,int last=29,float displacement=1)
        {for(int i=first;i<=last;i++)tracker.SamplePose(Sample(i,displacement));}
        private static FieldContextPoseDescription CombinedRange(double minimum=.7)
        {return new FieldContextPoseDescription(307,14,Hash,295,658,Clip,"test-range-semantic-pose","A reviewed late range action.",0,66,80,new FieldPoseProof(minimum,-1,-1,0,-1,-1));}
        private static FieldDescriptionTracker CombinedTracker(List<FieldDescriptionEmission> cues,double minimum=.7)
        {return new FieldDescriptionTracker(cues.Add,new FieldContextPoseCatalog(new[]{CombinedRange(minimum)}));}
        private static FieldPoseStart CombinedStart()
        {return new FieldPoseStart{Actor=14,Model=658,Clip=Clip,Site="307:14:"+Hash+":295",Cutscene=true,FullRange=false,InFrame=0,OutFrame=66,FrameCount=80,Forward=true,NonJump=true};}
        private static FieldPoseSample CombinedSample(int frame)
        {return new FieldPoseSample{Actor=14,Clip=Clip,AnimFrame=frame,FrameCount=80,ClipPresent=true,Visible=true,ScreenClear=true};}
        private static void CombinedPlay(FieldDescriptionTracker tracker,int first=0,int last=66)
        {for(int frame=first;frame<=last;frame++)tracker.SamplePose(CombinedSample(frame));}
        public static void CombinedRangeRequiresLateNativePoseRatherThanEarlyThirtyPercent()
        {
            var cues=new List<FieldDescriptionEmission>();var tracker=CombinedTracker(cues);
            TestAssert.Equal("pending",tracker.StartPose(CombinedStart()));CombinedPlay(tracker,0,20);TestAssert.Equal(0,cues.Count);
            for(int i=0;i<30;i++)tracker.SamplePose(CombinedSample(20));TestAssert.Equal(0,cues.Count);
            CombinedPlay(tracker,21,55);TestAssert.Equal(0,cues.Count);tracker.SamplePose(CombinedSample(56));TestAssert.Equal(1,cues.Count);
            TestAssert.Equal("test-range-semantic-pose",cues[0].CueId);TestAssert.Equal("repeat",tracker.StartPose(CombinedStart()));
        }
        public static void WrongCombinedRangeReverseAndHiddenSamplesPreserveSafetyGuards()
        {
            Action<FieldPoseStart>[] invalid={s=>s.InFrame=1,s=>s.OutFrame=67,s=>s.FrameCount=79,s=>s.Forward=false,s=>s.NonJump=false};
            foreach(var mutate in invalid){var cues=new List<FieldDescriptionEmission>();var tracker=CombinedTracker(cues);var start=CombinedStart();mutate(start);TestAssert.False(tracker.StartPose(start)=="pending");CombinedPlay(tracker);TestAssert.Equal(0,cues.Count);}
            Action<FieldPoseSample>[] hidden={s=>s.Visible=false,s=>s.ScreenClear=false,s=>s.ClipPresent=false,s=>s.FrameCount=79,s=>s.AnimFrame=67};
            foreach(var mutate in hidden){var cues=new List<FieldDescriptionEmission>();var tracker=CombinedTracker(cues);tracker.StartPose(CombinedStart());CombinedPlay(tracker,0,54);var sample=CombinedSample(55);mutate(sample);tracker.SamplePose(sample);CombinedPlay(tracker,56);TestAssert.Equal(0,cues.Count);TestAssert.Equal("pending",tracker.StartPose(CombinedStart()));CombinedPlay(tracker,0,56);TestAssert.Equal(1,cues.Count);}
        }
        public static void UnreachableCombinedRangeMinimumSampleTimeCannotBePublished()
        {
            foreach(double fraction in new[]{.826,.9,.99}){bool rejected=false;try{CombinedRange(fraction);}catch(ArgumentException){rejected=true;}TestAssert.True(rejected);}
            var cues=new List<FieldDescriptionEmission>();var tracker=CombinedTracker(cues,66d/80);tracker.StartPose(CombinedStart());CombinedPlay(tracker,0,65);TestAssert.Equal(0,cues.Count);tracker.SamplePose(CombinedSample(66));TestAssert.Equal(1,cues.Count);
            bool missing=false;try{new FieldContextPoseDescription(307,14,Hash,295,658,Clip,"test",TextForTest,0,66,80,null);}catch(ArgumentException){missing=true;}TestAssert.True(missing);
        }
        private const string TextForTest="A test range action.";
        public static void PreparationFractionAndSemanticFractionAloneCannotEmitCompositeCaption()
        {
            var cues=new List<FieldDescriptionEmission>();var tracker=Tracker(cues,Pipe());
            TestAssert.Equal("pending",tracker.StartPose(Start()));Play(tracker,0,10);TestAssert.Equal(0,cues.Count);
            Play(tracker,11,29);TestAssert.Equal(0,cues.Count);TestAssert.Equal(1,tracker.PendingCount);
        }
        public static void CorrectPhaseMovementAndLateNativeSampleAreAllRequired()
        {
            var cues=new List<FieldDescriptionEmission>();var tracker=Tracker(cues,Pipe());tracker.StartPose(Start());
            tracker.OnSequenceStarted(12,SequenceSite,5);Play(tracker,0,20);TestAssert.Equal(0,cues.Count);
            tracker.SamplePose(Sample(21,.5f));TestAssert.Equal(0,cues.Count);
            tracker.SamplePose(Sample(22,.51f));TestAssert.Equal(1,cues.Count);TestAssert.Equal("test-semantic-pose",cues[0].CueId);
            TestAssert.Equal("repeat",tracker.StartPose(Start()));
        }
        public static void SemanticProgressUsesActualNativeFrameTimeDenominator()
        {
            var cues=new List<FieldDescriptionEmission>();var tracker=Tracker(cues,new FieldPoseProof(.68,-1,-1,0,-1,-1));tracker.StartPose(Start());
            Play(tracker,0,20);TestAssert.Equal(0,cues.Count); // 20/30, not 20/(30-1).
            tracker.SamplePose(Sample(21));TestAssert.Equal(1,cues.Count);
        }
        public static void WrongActorSiteSequenceAndPreAnimationPhaseCannotUnlockPose()
        {
            Action<FieldDescriptionTracker>[] phases={
                t=>t.OnSequenceStarted(13,SequenceSite,5),t=>t.OnSequenceStarted(12,"502:12:"+Hash+":591",5),
                t=>t.OnSequenceStarted(12,"503:12:"+Hash+":590",5),t=>t.OnSequenceStarted(12,"502:13:"+Hash+":590",5),
                t=>t.OnSequenceStarted(12,"502:12:"+new string('a',64)+":590",5),t=>t.OnSequenceStarted(12,SequenceSite,6)
            };
            foreach(var phase in phases){var cues=new List<FieldDescriptionEmission>();var tracker=Tracker(cues,Pipe());tracker.OnSequenceStarted(12,SequenceSite,5);tracker.StartPose(Start());phase(tracker);Play(tracker);TestAssert.Equal(0,cues.Count);}
        }
        public static void MissingOriginOrActualMovementNeverUsesGuessedPlacement()
        {
            var cues=new List<FieldDescriptionEmission>();var tracker=Tracker(cues,Pipe());
            foreach(var mutate in new Action<FieldPoseStart>[]{s=>s.PositionKnown=false,s=>s.X=Single.NaN,s=>s.Y=Single.PositiveInfinity}) {
                var start=Start();mutate(start);TestAssert.False(tracker.StartPose(start)=="pending");
            }
            TestAssert.Equal("pending",tracker.StartPose(Start()));tracker.OnSequenceStarted(12,SequenceSite,5);Play(tracker,0,29,0);TestAssert.Equal(0,cues.Count);
        }
        public static void InvalidPositionSamplesDiscardProofWithoutCommittingDedupe()
        {
            Action<FieldPoseSample>[] mutations={s=>s.PositionKnown=false,s=>s.X=Single.NaN,s=>s.Z=Single.PositiveInfinity};
            foreach(var mutate in mutations){var cues=new List<FieldDescriptionEmission>();var tracker=Tracker(cues,Pipe());tracker.StartPose(Start());tracker.OnSequenceStarted(12,SequenceSite,5);var sample=Sample(0);mutate(sample);tracker.SamplePose(sample);Play(tracker);TestAssert.Equal(0,cues.Count);TestAssert.Equal(0,tracker.PendingCount);TestAssert.Equal("pending",tracker.StartPose(Start()));tracker.OnSequenceStarted(12,SequenceSite,5);Play(tracker);TestAssert.Equal(1,cues.Count);}
        }
        public static void CompanionMustBePositivelyVisibleFromStartThroughProof()
        {
            var cues=new List<FieldDescriptionEmission>();var tracker=Tracker(cues,Companion());var start=Start();start.CompanionVisible=false;
            TestAssert.False(tracker.StartPose(start)=="pending");
            tracker.StartPose(Start());tracker.SamplePose(Sample(0));var hidden=Sample(1);hidden.CompanionVisible=false;tracker.SamplePose(hidden);Play(tracker,2);TestAssert.Equal(0,cues.Count);TestAssert.Equal(0,tracker.PendingCount);
            TestAssert.Equal("pending",tracker.StartPose(Start()));Play(tracker);TestAssert.Equal(1,cues.Count);
        }
        public static void ChangedOrAmbiguousCompanionCannotProveOrRetainQueuedCaption()
        {
            Action<FieldDescriptionTracker,FieldPoseSample>[] mutations={
                (t,s)=>s.CompanionActor=20010,(t,s)=>s.CompanionVisible=false,
                (t,s)=>{t.ObserveActor(10010,9);t.ObserveActor(10010,8);},
                (t,s)=>t.EndFrame(new long[]{12})
            };
            foreach(var mutate in mutations){var cues=new List<FieldDescriptionEmission>();var tracker=Tracker(cues,Companion());tracker.StartPose(Start());tracker.SamplePose(Sample(0));var sample=Sample(1);mutate(tracker,sample);tracker.SamplePose(sample);Play(tracker,2);TestAssert.Equal(0,cues.Count);}
            var output=new List<FieldDescriptionEmission>();var current=Tracker(output,Companion());current.StartPose(Start());Play(current);TestAssert.True(current.IsCurrent(output[0]));current.ObserveActor(10010,9);TestAssert.False(current.IsCurrent(output[0]));
        }
        public static void NewAnimationAndPresentationBoundariesCannotReuseSequenceProof()
        {
            var cues=new List<FieldDescriptionEmission>();var tracker=Tracker(cues,Pipe());tracker.StartPose(Start());tracker.OnSequenceStarted(12,SequenceSite,5);Play(tracker,0,5);
            tracker.StartPose(Start());Play(tracker);TestAssert.Equal(0,cues.Count);
            tracker.Invalidate("pause");tracker.StartPose(Start());Play(tracker);TestAssert.Equal(0,cues.Count);
            tracker.Invalidate("field/focus/battle");tracker.StartPose(Start());tracker.OnSequenceStarted(12,SequenceSite,5);Play(tracker);TestAssert.Equal(1,cues.Count);
        }
        public static void ReplacedSharedSequenceRevokesEarlierPhaseForPendingPose()
        {
            var cues=new List<FieldDescriptionEmission>();var tracker=Tracker(cues,Pipe());tracker.StartPose(Start());tracker.OnSequenceStarted(12,SequenceSite,5);tracker.OnSequenceStarted(12,SequenceSite,6);Play(tracker);TestAssert.Equal(0,cues.Count);
        }
        public static void SemanticWaitCanFinishOnAHeldAlreadyProvenPose()
        {
            var cues=new List<FieldDescriptionEmission>();var tracker=Tracker(cues,Pipe());tracker.StartPose(Start());Play(tracker,0,29,0);TestAssert.Equal(0,cues.Count);
            tracker.OnSequenceStarted(12,SequenceSite,5);tracker.SamplePose(Sample(29,1));TestAssert.Equal(1,cues.Count);
            // A pose frozen from its first sample still has no animation progress.
            cues.Clear();tracker=Tracker(cues,Pipe());tracker.StartPose(Start());tracker.OnSequenceStarted(12,SequenceSite,5);
            for(int i=0;i<30;i++)tracker.SamplePose(Sample(29,1));TestAssert.Equal(0,cues.Count);
        }
        public static void InvalidProofMetadataFailsClosed()
        {
            Func<FieldPoseProof>[] invalid={
                ()=>new FieldPoseProof(Double.NaN,-1,-1,0,-1,-1),()=>new FieldPoseProof(Double.PositiveInfinity,-1,-1,0,-1,-1),()=>new FieldPoseProof(-.1,-1,-1,0,-1,-1),()=>new FieldPoseProof(1,-1,-1,0,-1,-1),
                ()=>new FieldPoseProof(.7,-1,5,0,-1,-1),()=>new FieldPoseProof(.7,590,-1,0,-1,-1),()=>new FieldPoseProof(.7,590,256,0,-1,-1),
                ()=>new FieldPoseProof(.7,-1,-1,Single.NaN,-1,-1),()=>new FieldPoseProof(.7,-1,-1,Single.PositiveInfinity,-1,-1),()=>new FieldPoseProof(.7,-1,-1,-1,-1,-1),()=>new FieldPoseProof(.7,-1,-1,241,-1,-1),
                ()=>new FieldPoseProof(0,-1,-1,0,10,-1),()=>new FieldPoseProof(0,-1,-1,0,-1,8),()=>new FieldPoseProof(0,-1,-1,0,256,8),()=>new FieldPoseProof(0,-1,-1,0,10,65535),()=>new FieldPoseProof(0,-1,-1,0,-1,-1)
            };
            foreach(var create in invalid){bool rejected=false;try{create();}catch(ArgumentException){rejected=true;}TestAssert.True(rejected);}
        }
        public static void NativeAttachmentRequiresActualOwnerUidAndExactBone()
        {
            var proof=new FieldPoseProof(0,-1,-1,0,10,8,55);TestAssert.True(proof.RequiresAttachment);TestAssert.Equal(55,proof.AttachmentBone);
            TestAssert.Equal("bone055",proof.AttachmentBoneName);TestAssert.True(proof.AcceptsCompanionAttachment(9,9,55,true));
            foreach(var relation in new[]{new[]{9,-1,-1},new[]{9,10,55},new[]{9,9,54},new[]{-1,-1,55},new[]{256,256,55}}) {
                TestAssert.False(proof.AcceptsCompanionAttachment(relation[0],relation[1],relation[2],true));
                var cues=new List<FieldDescriptionEmission>();var tracker=Tracker(cues,proof);tracker.StartPose(Start());tracker.SamplePose(Sample(0));var sample=Sample(1);sample.CompanionVisible=proof.AcceptsCompanionAttachment(relation[0],relation[1],relation[2],true);tracker.SamplePose(sample);Play(tracker,2);TestAssert.Equal(0,cues.Count);
            }
            var legacy=Companion();TestAssert.False(legacy.RequiresAttachment);TestAssert.True(legacy.AcceptsCompanionAttachment(-1,-1,-1,false));
            foreach(var create in new Func<FieldPoseProof>[]{()=>new FieldPoseProof(0,-1,-1,0,10,8,-2),()=>new FieldPoseProof(0,-1,-1,0,10,8,256),()=>new FieldPoseProof(.7,-1,-1,0,-1,-1,55)}){
                bool rejected=false;try{create();}catch(ArgumentException){rejected=true;}TestAssert.True(rejected);
            }
        }
        public static void HistoricalAttachmentMetadataCannotReplaceLiveParentProof()
        {
            var proof=new FieldPoseProof(0,-1,-1,0,10,8,55);TestAssert.False(proof.AcceptsCompanionAttachment(9,9,55,false));
            var cues=new List<FieldDescriptionEmission>();var tracker=Tracker(cues,proof);tracker.StartPose(Start());tracker.SamplePose(Sample(0));
            var detached=Sample(1);detached.CompanionVisible=proof.AcceptsCompanionAttachment(9,9,55,false);tracker.SamplePose(detached);Play(tracker,2);TestAssert.Equal(0,cues.Count);
            TestAssert.Equal("pending",tracker.StartPose(Start()));Play(tracker);TestAssert.Equal(1,cues.Count);
        }
    }
}
