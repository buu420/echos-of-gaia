using System;
using System.Collections.Generic;
using System.Linq;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class FieldExitDestinationTests
    {
        public static void ACheckpointCannotMistakeAnotherLivingActorForThePlayer()
        {
            var c=TestFixtures.Context("field:153");c.StoryProgress=1190;c.PlayerIdentity=32;
            var wrong=new StoryCheckpoint{Floor=1190,Ceiling=1400,ScopeFields=new[]{153},PlayerIdentity=3,RequiredScripts=new Dictionary<int,string>{{3,"Zidane"}},Stage=new StoryStage{Milestone=1190,Goal="Zidane stairs"}};
            var right=new StoryCheckpoint{Floor=1190,Ceiling=1400,ScopeFields=new[]{153},PlayerIdentity=32,RequiredScripts=new Dictionary<int,string>{{32,"Steiner"}},Stage=new StoryStage{Milestone=1400,Goal="Steiner search"}};
            var catalog=new CatalogResult();new StoryTargetCatalog(null,null,new[]{wrong,right}).Append(c,catalog,153,null,null,sid=>sid==3?"Zidane":sid==32?"Steiner":null);
            TestAssert.True(catalog.Targets[0].Label.Contains("Steiner search"));
        }
        public static void CurrentChapterRoutingDoesNotUseAFutureCastleShortcut()
        {
            var c=TestFixtures.Context("field:153");c.StoryProgress=1190;
            var exit=new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldExit,c.LogicalMapKey,23),Category=NavigationCategory.Exits,Generation=1,Label="Exit",NextFields=new[]{154},ApproachCandidates=new[]{TestFixtures.P(20,20)}};
            var catalog=new CatalogResult();catalog.Targets.Add(exit);
            var stage=new StoryStage{Milestone=1400,GoalFields=new[]{55},Goal="Alexandria Castle",Instruction="Search the castle for Princess Garnet.",TransitFields=new[]{153,154,158,55},KeepGoalLabel=true};
            var transit=new[]{new StoryTransit{From=153,To=204},new StoryTransit{From=204,To=55},new StoryTransit{From=153,To=154},new StoryTransit{From=154,To=158},new StoryTransit{From=158,To=55}};
            new StoryTargetCatalog(new[]{stage},transit).Append(c,catalog,153,f=>f==55?"Future scene":"Castle hall");
            var story=catalog.Targets.Single(t=>t.Category==NavigationCategory.Story);TestAssert.True(story.PhysicalId.HasValue);TestAssert.Equal(23,story.PhysicalIdentity.NativeId);
            catalog=new CatalogResult();new StoryTargetCatalog(new[]{stage},transit).Append(c,catalog,153,f=>f==55?"Future scene":"Castle hall");
            TestAssert.False(catalog.Targets[0].Label.Contains("Future scene"));
        }
        public static void PositionProofReadsOnlyTheProspectiveControlledPlayer()
        {
            var p=new NavigationPoint(12.6f,450.6f,-20.6f);
            TestAssert.Equal((int?)13,FieldExitDestinations.PlayerProperty(32,p,250,0));
            TestAssert.Equal((int?)-451,FieldExitDestinations.PlayerProperty(32,p,32,1));
            TestAssert.Equal((int?)-21,FieldExitDestinations.PlayerProperty(32,p,250,2));
            TestAssert.False(FieldExitDestinations.PlayerProperty(32,p,17,1).HasValue);
            TestAssert.False(FieldExitDestinations.PlayerProperty(32,p,255,1).HasValue);
            TestAssert.False(FieldExitDestinations.PlayerProperty(32,p,250,3).HasValue);
        }
        public static void UnchangedNativeStateReusesProofAndChangedStateInvalidatesIt()
        {
            var cache=new FieldExitDestinationCache();var points=new[]{TestFixtures.P(10,10),TestFixtures.P(20,20)};var map=new byte[]{0};int calls=0;
            Func<NavigationPoint,ScriptStoryEvidence> read=p=>{calls++;var e=new ScriptStoryEvidence();e.Destinations.Add(map[0]==0?158:153);return e;};
            var first=cache.Read("visit1",8,"script",null,map,null,points,read);TestAssert.Equal(2,calls);
            first[158][0]=TestFixtures.P(9999,9999);
            var same=cache.Read("visit1",8,"script",null,map,null,points,read);TestAssert.Equal(2,calls);TestAssert.Equal(points[0],same[158][0]);
            map[0]=1;var changed=cache.Read("visit1",8,"script",null,map,null,points,read);TestAssert.Equal(4,calls);TestAssert.True(changed.ContainsKey(153));TestAssert.False(changed.ContainsKey(158));
            cache.Read("visit2",8,"script",null,map,null,points,read);TestAssert.Equal(6,calls);
            cache.Read("visit2",8,"different",null,map,null,points,read);TestAssert.Equal(8,calls);
        }
        public static void OnlyProvenFiniteSurfacePointsBelongToADestination()
        {
            var points=new[]{TestFixtures.P(10,10),TestFixtures.P(20,20),new NavigationPoint(float.NaN,0,0)};
            var groups=FieldExitDestinations.Read(points,p=>{var e=new ScriptStoryEvidence();if(p.X==20)e.Destinations.Add(158);return e;});
            TestAssert.Equal(1,groups.Count);TestAssert.Equal(1,groups[158].Length);TestAssert.Equal(points[1],groups[158][0]);
        }
        public static void StoryUsesOnlyTheApproachForItsSelectedDestination()
        {
            var c=TestFixtures.Context("field:154");c.StoryProgress=1190;c.Position=TestFixtures.P(10,10);
            var lower=TestFixtures.P(30,30);var upper=TestFixtures.P(12,12);
            var obs=new FieldTargetObservation{Id=new TargetIdentity(TargetKind.FieldExit,c.LogicalMapKey,8),Position=upper,NextFields=new[]{153,158},
                DestinationApproaches=new Dictionary<int,NavigationPoint[]>{{153,new[]{upper}},{158,new[]{lower}}},
                Exit=new ExitPresentation{Visible=true,ApproachEnabled=true,Approach=upper,ApproachCandidates=new[]{upper,lower}}};
            var catalog=new FieldTargetCatalog().Build(c,new[]{obs},null);
            new StoryTargetCatalog(new[]{new StoryStage{Milestone=1400,GoalFields=new[]{55},Goal="Castle"}},new[]{new StoryTransit{From=154,To=153},new StoryTransit{From=154,To=158},new StoryTransit{From=158,To=55}}).Append(c,catalog,154);
            var story=catalog.Targets.Single(t=>t.Category==NavigationCategory.Story);
            TestAssert.True(story.PhysicalId.HasValue);TestAssert.Equal(1,story.ApproachCandidates.Length);TestAssert.Equal(lower,story.ApproachCandidates[0]);
            var g=new FieldGeometrySnapshot{Generation=1,Radius=1,ActiveTriangle=0,Triangles=new[]{new FieldTriangle{Id=0,Vertices=new[]{TestFixtures.P(0,0),TestFixtures.P(200,0),TestFixtures.P(0,200)}}}};
            var route=new FieldRoutePlanner().Plan(c,story,g);TestAssert.True(route.Success);TestAssert.Equal(lower,route.ApproachPoint);
        }
    }
}
