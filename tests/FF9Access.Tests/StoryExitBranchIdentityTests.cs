using System;
using System.Linq;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class StoryExitBranchIdentityTests
    {
        public static void TwoDestinationsOfOneRegionStayDistinctInStory()
        {
            var context=TestFixtures.Context("field:154");context.StoryProgress=1190;
            var physical=new TargetIdentity(TargetKind.FieldExit,context.LogicalMapKey,9);
            var input=new CatalogResult();
            foreach(int next in new[]{159,162})input.Targets.Add(new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldExit,context.LogicalMapKey,9+256*(next+1)),PhysicalId=physical,Generation=context.Generation,Category=NavigationCategory.Exits,Label="Exit to room "+next,NextFields=new[]{next},ApproachCandidates=new[]{TestFixtures.P(next,0)}});
            var catalog=new StoryTargetCatalog(new[]{new StoryStage{Milestone=1400,GoalFields=new[]{159,162}}},new StoryTransit[0]);catalog.Append(context,input,154);
            var story=input.Targets.Where(t=>t.Category==NavigationCategory.Story).ToArray();
            TestAssert.Equal(2,story.Length);TestAssert.Equal(2,story.Select(t=>t.Id).Distinct().Count());
            TestAssert.True(story.All(t=>t.PhysicalIdentity.Equals(physical)));
            TestAssert.False(story[0].ApproachCandidates[0].Equals(story[1].ApproachCandidates[0]));
            TestAssert.Equal(story[0].Id,StoryTargetCatalog.Wrap(context,1400,input.Targets[0],story[0].Label).Id);
        }
    }
}
