using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class ManualArrivalTests
    {
        public static void MatchingHorizontalCoordinatesOnAnotherFloorDoNotClaimArrival()
        {
            var c=TestFixtures.Context();c.Position=new NavigationPoint(0,100,240);
            var route=new RoutePlan{Success=true,Origin=new NavigationPoint(0,-100,0),Waypoints=new[]{new NavigationPoint(0,-100,240)}};
            var step=ManualRouteGuidance.Read(c,route,0,80,false,p=>false);
            TestAssert.True(step.NeedReplan && !step.AtApproach);
        }
    }
}
