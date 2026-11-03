using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class StableManualSpeechTests
    {
        internal static NavigationUpdate Run()
        {
            var c=TestFixtures.Context();var target=new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldExit,c.LogicalMapKey,15),Generation=c.Generation,Category=NavigationCategory.Exits,Label="Street exit",Position=TestFixtures.P(0,1200)};
            var route=new RoutePlan{Success=true,Generation=c.Generation,TargetId=target.Id,Transport=c.Transport,ApproachPoint=target.Position,Waypoints=new[]{TestFixtures.P(0,240),TestFixtures.P(0,480),TestFixtures.P(0,1200)}};
            return new NavigationUpdate{Context=c,Selected=target,Session=new RouteDecision{Mode=RouteMode.Guiding},GuidanceEnabled=true,Movement=new MovementObservation{RouteAvailable=true,PlannedRoute=route,RoutePoint=route.Waypoints[0]}};
        }
        public static void SmallSidestepDoesNotAnnounceADifferentRunImmediately()
        {
            var u=Run();var speech=new NavigationAnnouncements();var initial=speech.Format(u,5000,new SpeechState());TestAssert.Equal("up 5.",initial[0].Text);speech.Acknowledge(initial[0],true);
            u.Context.Position=TestFixtures.P(30,0);
            TestAssert.Equal(0,speech.Format(u,5000,new SpeechState{NowMilliseconds=100}).Count);
        }
        public static void SmallSidestepKeepsTheFullDistanceToTheSameTurn()
        {
            var u=Run();u.Context.Position=TestFixtures.P(30,0);
            TestAssert.Equal("up 5.",NavigationAnnouncements.Status(u).Substring(NavigationAnnouncements.Status(u).LastIndexOf("up")));
        }
    }
}
