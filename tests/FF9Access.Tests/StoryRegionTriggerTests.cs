using System;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    // Finding7: each native trigger path needs its own gate, and only points proving one current step remain.
    internal static class StoryRegionTriggerTests
    {
        public static void FailedConfirmGateNeverFallsBackToPush()
        {
            // Field 757 / SID 5: Push shows the icon, Talk advances the story. Talk is not permitted now.
            var paths=FieldStoryRegionApproaches.Triggers(true,false,true);
            TestAssert.Equal(1,paths.Length);TestAssert.Equal(StoryRegionTrigger.Walk,paths[0]);
            TestAssert.Equal(EventEngineTags.Push,FieldStoryRegionApproaches.Tag(paths[0]));
            TestAssert.Equal(0,FieldStoryRegionApproaches.Triggers(true,false,false).Length);
        }
        public static void ConfirmStepUsesTalkTagAndWalkStaysAvailable()
        {
            var paths=FieldStoryRegionApproaches.Triggers(true,true,true);
            TestAssert.Equal(2,paths.Length);TestAssert.Equal(StoryRegionTrigger.Confirm,paths[0]);TestAssert.Equal(StoryRegionTrigger.Walk,paths[1]);
            TestAssert.Equal(EventEngineTags.Talk,FieldStoryRegionApproaches.Tag(paths[0]));
            paths=FieldStoryRegionApproaches.Triggers(true,true,false);
            TestAssert.Equal(1,paths.Length);TestAssert.Equal(StoryRegionTrigger.Confirm,paths[0]);
        }
        public static void TalkPermissionWithoutConfirmStepOffersNoConfirmPath()
        {
            TestAssert.Equal(0,FieldStoryRegionApproaches.Triggers(false,true,false).Length);
            var paths=FieldStoryRegionApproaches.Triggers(false,true,true);
            TestAssert.Equal(1,paths.Length);TestAssert.Equal(StoryRegionTrigger.Walk,paths[0]);
        }
        public static void OnlyPointsProvingTheOfferedStepRemain()
        {
            var proofs=new[]{Proof(Int32.MinValue),Proof(7),Proof(9),Proof(7),Proof(Int32.MinValue)};
            // The current-position annotation offers step 7: endpoints for 9 prove a different action.
            var kept=FieldStoryRegionApproaches.Proven(proofs,Proof(7),16);
            TestAssert.Equal(2,kept.Length);TestAssert.Equal(1,kept[0]);TestAssert.Equal(3,kept[1]);
            // No current proof: the nearest proving point decides, never a union.
            kept=FieldStoryRegionApproaches.Proven(proofs,Proof(Int32.MinValue),16);
            TestAssert.Equal(2,kept.Length);TestAssert.Equal(1,kept[0]);TestAssert.Equal(3,kept[1]);
            kept=FieldStoryRegionApproaches.Proven(proofs,Proof(Int32.MinValue),1);
            TestAssert.Equal(1,kept.Length);
            // The current step is proven here, but by no prospective point: nothing is offered.
            TestAssert.Equal(0,FieldStoryRegionApproaches.Proven(proofs,Proof(4),16).Length);
            TestAssert.Equal(0,FieldStoryRegionApproaches.Proven(new[]{Proof(Int32.MinValue)},Proof(Int32.MinValue),16).Length);
        }
        public static void LegacyPositionalMilestonesAreGroupedNotMixed()
        {
            var proofs=new[]{Proof(Int32.MinValue,3850),Proof(Int32.MinValue,3860),Proof(Int32.MinValue,3850)};
            var kept=FieldStoryRegionApproaches.Proven(proofs,Proof(Int32.MinValue),16);
            TestAssert.Equal(2,kept.Length);TestAssert.Equal(0,kept[0]);TestAssert.Equal(2,kept[1]);
            // A step proof and a step-plus-different-legacy proof are different endpoints.
            proofs=new[]{Proof(5,3850),Proof(5),Proof(5,3850)};
            kept=FieldStoryRegionApproaches.Proven(proofs,Proof(5,3850),16);
            TestAssert.Equal(2,kept.Length);TestAssert.Equal(0,kept[0]);TestAssert.Equal(2,kept[1]);
            // Scene-advance steps (-1) are proofs too.
            TestAssert.Equal(1,FieldStoryRegionApproaches.Proven(new[]{Proof(-1)},Proof(Int32.MinValue),16).Length);
        }
        private static StoryRegionProof Proof(int step,params int[] milestones){return new StoryRegionProof(step,milestones);}
        private static class EventEngineTags{public const int Push=2,Talk=3;}
    }
}
namespace FF9Access.Tests
{
    internal static class StoryRegionProofCacheTests
    {
        public static void AWaitOwnerChangeInvalidatesProofWithoutARegionOrMapChange()
        {
            var cache=new StoryRegionProofCache<string>(30);var surface=new object();var point=TestFixtures.P(2,3);int evaluations=0;
            // Region/global/map bytes stay fixed; the separate requested owner's
            // native wait IP or instance flag changes within the cache lifetime.
            var memory=new[]{new byte[]{1},new byte[]{2},new byte[]{3},BitConverter.GetBytes(1415),new byte[]{0}};
            Func<NavigationPoint,string> evaluate=p=>"proof"+(++evaluations);
            TestAssert.Equal("proof1",cache.Begin("region","scope",surface,memory,100,evaluate)(point));
            memory[3]=BitConverter.GetBytes(1444);
            TestAssert.Equal("proof2",cache.Begin("region","scope",surface,memory,101,evaluate)(point));
            memory[4][0]=1;
            TestAssert.Equal("proof3",cache.Begin("region","scope",surface,memory,101,evaluate)(point));
        }
        public static void ProofsAreReusedOnlyForUnchangedStateSurfaceAndAge()
        {
            var cache=new Memoria.Accessibility.Navigation.StoryRegionProofCache<string>(30);int evaluations=0;
            System.Func<Memoria.Accessibility.Navigation.NavigationPoint,string> evaluate=p=>{evaluations++;return "proof"+p.X;};
            var surface=new object();var memory=new[]{new byte[]{1,2},new byte[]{3}};var point=TestFixtures.P(5,5);
            TestAssert.Equal("proof5",cache.Begin("5:Confirm","scope",surface,memory,100,evaluate)(point));
            memory[0][0]=1;TestAssert.Equal("proof5",cache.Begin("5:Confirm","scope",surface,memory,110,evaluate)(point));TestAssert.Equal(1,evaluations);
            memory[0][0]=9;cache.Begin("5:Confirm","scope",surface,memory,111,evaluate)(point);TestAssert.Equal(2,evaluations);
            cache.Begin("5:Confirm","scope",new object(),memory,112,evaluate)(point);TestAssert.Equal(3,evaluations);
            cache.Begin("5:Confirm","other",new object(),memory,113,evaluate)(point);TestAssert.Equal(4,evaluations);
            var stable=new object();cache.Begin("5:Confirm","other",stable,memory,114,evaluate)(point);TestAssert.Equal(5,evaluations);
            cache.Begin("5:Confirm","other",stable,memory,143,evaluate)(point);TestAssert.Equal(5,evaluations);
            cache.Begin("5:Confirm","other",stable,memory,144,evaluate)(point);TestAssert.Equal(6,evaluations);
            cache.Begin("5:Walk","other",stable,memory,144,evaluate)(point);TestAssert.Equal(7,evaluations);
        }
    }
}
