using System;
using System.Linq;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class StoryTransitStateTests
    {
        private static NavigationContext Context(){return new NavigationContext{LogicalMapKey="field:1",Generation=1,StoryProgress=10};}
        private static CatalogResult Input(NavigationContext context){var result=new CatalogResult();result.Targets.Add(new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldExit,context.LogicalMapKey,2),Category=NavigationCategory.Exits,Generation=1,NextFields=new[]{2},Label="Door"});return result;}
        private static bool HasRoute(StoryTargetCatalog catalog,NavigationContext context){var input=Input(context);catalog.Append(context,input,1);return input.Targets.Any(t=>t.Category==NavigationCategory.Story && t.PhysicalId.HasValue);}
        public static void PortableFlagsMustBeKnownAndAreInvalidatedWithinTheSameCounter()
        {
            bool? flag=null;var edge=new StoryTransit{From=2,To=3,Scope="global",Guards=new[]{new byte[]{1}}};
            var catalog=new StoryTargetCatalog(new[]{new StoryStage{Milestone=20,GoalFields=new[]{3}}},new[]{edge});
            catalog.SetTransitEvaluator(e=>flag);var context=Context();
            TestAssert.False(HasRoute(catalog,context));flag=false;catalog.InvalidateState();TestAssert.False(HasRoute(catalog,context));
            flag=true;catalog.InvalidateState();TestAssert.True(HasRoute(catalog,context));
            flag=false;catalog.InvalidateState();TestAssert.False(HasRoute(catalog,context));
        }
        public static void RemoteRoomMemoryCannotAuthorizeAnIntermediateHop()
        {
            var catalog=new StoryTargetCatalog(new[]{new StoryStage{Milestone=20,GoalFields=new[]{3}}},new[]{new StoryTransit{From=2,To=3,Scope="room"}});
            catalog.SetTransitEvaluator(e=>true);TestAssert.False(HasRoute(catalog,Context()));
            // The current exit itself can still be used when the next room is the goal:
            // its destination is supplied by the live physical catalog, not remote memory.
            catalog=new StoryTargetCatalog(new[]{new StoryStage{Milestone=20,GoalFields=new[]{2}}},new[]{new StoryTransit{From=1,To=2,Scope="room"}});
            TestAssert.True(HasRoute(catalog,Context()));
        }
        public static void ReverseTravelCannotUseTheCurrentRoomAsAShortcut()
        {
            var context=Context();var input=Input(context);
            input.Targets.Add(new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldExit,context.LogicalMapKey,4),Category=NavigationCategory.Exits,Generation=1,NextFields=new[]{4},Label="Correct"});
            var catalog=new StoryTargetCatalog(new[]{new StoryStage{Milestone=20,GoalFields=new[]{5}}},new[]{new StoryTransit{From=2,To=1},new StoryTransit{From=1,To=4},new StoryTransit{From=4,To=5}});
            catalog.Append(context,input,1);
            TestAssert.Equal(4,input.Targets.Single(t=>t.Category==NavigationCategory.Story).PhysicalIdentity.NativeId);
        }
    }
}
