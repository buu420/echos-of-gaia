using System;
using System.Collections.Generic;
using System.Linq;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class StoryPhaseRouteTests
    {
        // Gizamaluke's Grotto: both required actions occur before counter
        // 3720. The wounded soldier supplies the bell, then the door consumes
        // it and opens the route. An incomplete door is not a walking shortcut.
        private static readonly byte[] DoorClosed={0xe4,0xf7,0x0e,0x7d,0,0,0x20,0x7f};
        private static readonly byte[] NoBell={0xe0,0,0,0x7f}; // identity key for the test reader
        private static readonly byte[] HasBell={0xe0,1,0,0x7f};
        private static StoryTargetCatalog Catalog(Func<byte[],bool?> read)
        {
            var directors=new Dictionary<int,string>{{701,"entry"},{702,"soldier-room"}};
            var legs=new[]{new StoryTransit{From=701,To=702,ActionUid=3}};
            var bell=new StoryStage{Milestone=3720,GoalFields=new[]{702},ActionUids=new[]{5},Instruction="Get a bell from the wounded soldier",ReviewedLinks=legs};
            var ring=new StoryStage{Milestone=3720,GoalFields=new[]{702},ActionUids=new[]{8},Instruction="Ring the bell at the sealed door",ReviewedLinks=legs};
            var plan=new StoryPhasePlan(new[]{new StoryPhase{Floor=3710,Ceiling=3720,Directors=directors,Guards=new[]{DoorClosed,NoBell},Stage=bell},
                new StoryPhase{Floor=3710,Ceiling=3720,Directors=directors,Guards=new[]{DoorClosed,HasBell},Stage=ring}});
            var normal=new StoryStage{Milestone=3720,GoalFields=new[]{703},Goal="Grotto"};
            var catalog=new StoryTargetCatalog(new[]{normal},new[]{new StoryTransit{From=701,To=703},new StoryTransit{From=702,To=703}});
            catalog.SetPhaseSelector((progress,field,scripts)=>plan.Select(progress,field,scripts,read));return catalog;
        }
        private static NavigationTarget Target(NavigationContext c,int uid,TargetKind kind,params int[] fields)
        {return new NavigationTarget{Id=new TargetIdentity(kind,c.LogicalMapKey,uid),Category=kind==TargetKind.FieldExit?NavigationCategory.Exits:NavigationCategory.Objects,
            Label="Target "+uid,Generation=c.Generation,IsInteractive=true,ApproachCandidates=new[]{TestFixtures.P(uid,0)},NextFields=fields,StoryStepMilestone=kind==TargetKind.FieldActor?3720:Int32.MinValue};}
        private static NavigationTarget Story(StoryTargetCatalog c,NavigationContext context,int field,string director,params NavigationTarget[] targets)
        {
            var list=new CatalogResult();foreach(var target in targets)list.Targets.Add(target);
            c.Append(context,list,field,f=>"Room "+f,null,sid=>sid==0?director:null);
            return list.Targets.Single(t=>t.Category==NavigationCategory.Story);
        }
        public static void TheBellPhaseUsesItsReviewedDoorwayEvenIfAnotherDoorHasTheSameRoomNumber()
        {
            var c=TestFixtures.Context("field:701:SCENE");c.StoryProgress=3710;
            var catalog=Catalog(g=>g==DoorClosed || g==NoBell);
            var story=Story(catalog,c,701,"entry",Target(c,4,TargetKind.FieldExit,702),Target(c,9,TargetKind.FieldExit,703),Target(c,3,TargetKind.FieldExit,702));
            TestAssert.Equal(3,story.PhysicalIdentity.NativeId);TestAssert.True(story.Label.Contains("wounded soldier"));
        }
        public static void GettingTheBellChangesTheTargetWithoutChangingTheScenarioCounter()
        {
            bool bell=false,closed=true;
            var c=TestFixtures.Context("field:702:SCENE");c.StoryProgress=3710;
            var catalog=Catalog(g=>g==DoorClosed?closed:g==HasBell?bell:!bell);
            var targets=new[]{Target(c,5,TargetKind.FieldActor),Target(c,8,TargetKind.FieldActor),Target(c,7,TargetKind.FieldExit,703)};
            var story=Story(catalog,c,702,"soldier-room",targets);TestAssert.Equal(5,story.PhysicalIdentity.NativeId);
            bell=true;story=Story(catalog,c,702,"soldier-room",targets);TestAssert.Equal(8,story.PhysicalIdentity.NativeId);
            closed=false;targets[0].StoryStepMilestone=targets[1].StoryStepMilestone=Int32.MinValue;
            story=Story(catalog,c,702,"soldier-room",targets);TestAssert.Equal(7,story.PhysicalIdentity.NativeId);
        }
        public static void UnknownPhaseStateOrChangedDirectorDoesNotAdvanceTheStory()
        {
            var c=TestFixtures.Context("field:702:SCENE");c.StoryProgress=3710;
            var targets=new[]{Target(c,5,TargetKind.FieldActor),Target(c,8,TargetKind.FieldActor),Target(c,7,TargetKind.FieldExit,703)};
            TestAssert.False(Story(Catalog(g=>g==DoorClosed?(bool?)null:true),c,702,"soldier-room",targets).PhysicalId.HasValue);
            TestAssert.False(Story(Catalog(g=>true),c,702,"changed",targets).PhysicalId.HasValue);
        }
        public static void APhaseNeverInventsAnApproachWhenItsRealActionIsMissing()
        {
            var c=TestFixtures.Context("field:702:SCENE");c.StoryProgress=3710;
            var story=Story(Catalog(g=>g==DoorClosed || g==NoBell),c,702,"soldier-room",Target(c,6,TargetKind.FieldActor));
            TestAssert.False(story.PhysicalId.HasValue);TestAssert.Equal(0,story.ApproachCandidates.Length);
        }
        public static void AReusedPhysicalIdCannotReplaceTheReviewedNativeActionProof()
        {
            var c=TestFixtures.Context("field:702:SCENE");c.StoryProgress=3710;
            var other=Target(c,5,TargetKind.FieldActor);other.StoryStepMilestone=Int32.MinValue;
            TestAssert.False(Story(Catalog(g=>g==DoorClosed || g==NoBell),c,702,"soldier-room",other).PhysicalId.HasValue);
        }
        public static void ARequiredActionFromALaterPhaseCannotRenameACurrentObject()
        {
            var c=TestFixtures.Context("field:702:SCENE");c.StoryProgress=3710;
            var catalog=Catalog(g=>g==DoorClosed || g==NoBell);
            TestAssert.True(catalog.AcceptsStep(c,702,3720,null,s=>s==0?"soldier-room":null,5));
            TestAssert.False(catalog.AcceptsStep(c,702,3720,null,s=>s==0?"soldier-room":null,8));
            TestAssert.False(catalog.AcceptsStep(c,701,3720,null,s=>s==0?"entry":null,5));
        }
        public static void DaliLiftUsesTheCurrentFloorRatherThanTheLastRoomEntrance()
        {
            var hashes=new Dictionary<int,string>{{404,"lift"},{406,"tunnel"}};
            var upper=new StoryStage{Milestone=2710,GoalFields=new[]{404},ActionUids=new[]{4}};
            var lower=new StoryStage{Milestone=2710,GoalFields=new[]{406},ActionUids=new int[0]};
            var plan=new StoryPhasePlan(new[]{new StoryPhase{Floor=2700,Ceiling=2710,Directors=hashes,ScopeFields=new[]{404},Stage=upper,
                    Local=new StoryPhaseLocal{Field=404,Walkmesh="native",ActiveTriangles=new[]{7,8}}},
                new StoryPhase{Floor=2700,Ceiling=2710,Directors=hashes,ScopeFields=new[]{404},Stage=lower,
                    Local=new StoryPhaseLocal{Field=404,Walkmesh="native",ActiveTriangles=new[]{13,14}}}});
            CheckFloor(8,upper);CheckFloor(13,lower);
            TestAssert.True(plan.Select(2700,404,s=>"lift",g=>true,local=>null).InformationOnly);
            TestAssert.True(plan.Select(2700,404,s=>"lift",g=>true,local=>false).InformationOnly);
            TestAssert.True(plan.Select(2700,406,s=>"tunnel",g=>true,local=>true)==null);
            void CheckFloor(int active,StoryStage expected){TestAssert.True(ReferenceEquals(expected,plan.Select(2700,404,s=>"lift",g=>true,local=>Array.IndexOf(local.ActiveTriangles,active)>=0)));}
        }
    }
}
