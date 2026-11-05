using System;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class ReviewedStoryPrefixTests
    {
        private static StoryStep Step()
        {
            return new StoryStep{Field=202,Sid=9,Uid=9,Hash="baku",Source="reviewed-prefix",Kind=StoryStepKind.Talk,Floor=2100,Ceiling=2101,
                Guards=new[]{new StoryStepGuard{Sid=0,Uid=0,Hash="director",Expression=new byte[]{0x7d,1,0,0x7f}}},
                Effects=new[]{new StoryStepEffect{Sid=9,Uid=9,Tag=3,Offset=355,Hash="baku",Proof="reached"}}};
        }
        private static string Hash(int sid){return sid==9?"baku":sid==0?"director":null;}
        public static void AReachedConversationEntryDoesNotOverrideAnInactiveScenePrerequisite()
        {
            var catalog=new StoryStepCatalog(new[]{Step()});
            foreach(int? guard in new int?[]{0,null})
                TestAssert.Equal(0,catalog.Candidates(202,2100,9,"baku",Hash,g=>guard,s=>true,9).Count);
            TestAssert.Equal(1,catalog.Candidates(202,2100,9,"baku",Hash,g=>1,s=>true,9).Count);
        }
        public static void AChangedGuardOwnerCannotSupplyAReviewedPrefix()
        {
            var catalog=new StoryStepCatalog(new[]{Step()});
            TestAssert.Equal(0,catalog.Candidates(202,2100,9,"baku",sid=>sid==0?"changed":Hash(sid),g=>1,s=>true,9).Count);
        }
        public static void AReviewedStairInstructionReplacesTheGenericInstructionForTheSameWait()
        {
            var generic=new StoryStep{Field=1104,Sid=23,Kind=StoryStepKind.Position,Axis=OpeningGoalAxis.Y,Minimum=-180};
            var reviewed=new StoryStep{Field=1104,Sid=23,Kind=StoryStepKind.Position,Axis=OpeningGoalAxis.Y,Minimum=-180,
                Priority=0,Action="Go up the stairs to the villagers"};
            TestAssert.Equal(reviewed,StoryStepCatalog.SelectPosition(new[]{generic,reviewed}));
            generic.Minimum=-200;
            TestAssert.True(StoryStepCatalog.SelectPosition(new[]{generic,reviewed})==null);
        }
    }
}
