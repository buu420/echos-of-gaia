using System.Linq;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    // An arrival notice may only promise an interaction when the native readiness test reported
    // one (MovementOutcome.ReadyToInteract / TurnNeeded). A plain approach is a position, not a prompt.
    internal static class ArrivalNoticeTests
    {
        private static NavigationTarget Target(bool interactive,NavigationCategory category=NavigationCategory.People,string label="Alexandrian Soldier")
        {return new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldActor,"field:100",4),Category=category,Label=label,Generation=1,IsInteractive=interactive,ApproachCandidates=new[]{TestFixtures.P(1,1)}};}
        public static void ApproachWithoutNativeReadinessNeverClaimsAnAction()
        {
            foreach(var target in new[]{Target(true),Target(false),Target(false,NavigationCategory.Story,"Walk to the next story position")}) {
                string text=ArrivalNotice.ApproachReached(target,false);
                TestAssert.False(text.Contains("ordinary game action") || text.Contains("Press Confirm"));
                TestAssert.True(text.StartsWith("Reached "+target.Label));
            }
            TestAssert.True(ArrivalNotice.ApproachReached(Target(true),false).Contains("not offering an interaction"));
            TestAssert.False(ArrivalNotice.ApproachReached(Target(false,NavigationCategory.Story),false).Contains("interaction"));
            TestAssert.True(ArrivalNotice.ApproachReached(Target(false,NavigationCategory.Objects,"Chest"),true).Contains("viewing point"));
        }
        public static void CoordinatorUsesTheTruthfulApproachNotice()
        {
            var catalog=new CatalogResult();var target=Target(true,NavigationCategory.Exits);catalog.Targets.Add(target);
            var n=new NavigationCoordinator();n.Tick(TestFixtures.Context(),new[]{NavigationCommand.StartAutomatic},catalog,null);
            var update=n.Tick(TestFixtures.Context(),new NavigationCommand[0],catalog,new MovementObservation{Outcome=MovementOutcome.ApproachReached});
            TestAssert.True(update.Messages.Any(m=>m.StartsWith("Reached Alexandrian Soldier")));
            TestAssert.False(update.Messages.Any(m=>m.Contains("ordinary game action")));
        }
    }
}
