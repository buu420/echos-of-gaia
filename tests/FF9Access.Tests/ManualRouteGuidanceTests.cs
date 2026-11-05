using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class ManualRouteGuidanceTests
    {
        private static RoutePlan Route(params NavigationPoint[] points)
        {return new RoutePlan{Success=true,Origin=TestFixtures.P(0,0),Waypoints=points};}
        private static NavigationContext Context(float x=0,float z=0){var c=TestFixtures.Context();c.Position=TestFixtures.P(x,z);return c;}
        public static void ThreeCountsOfDriftRejoinAtCurrentProgressInsteadOfAnOldNode()
        {
            var route=Route(TestFixtures.P(0,240),TestFixtures.P(0,480),TestFixtures.P(0,1200));var c=Context(720,600);
            var step=ManualRouteGuidance.Read(c,route,0,80,false,p=>true);
            TestAssert.True(step.Correcting && !step.NeedReplan);TestAssert.Near(0,step.Aim.X,0.01f);TestAssert.Near(600,step.Aim.Z,0.01f);TestAssert.Near(600,step.ForwardDistance,.01f);TestAssert.Equal(2,step.EndWaypointIndex);
        }
        public static void SmallDriftKeepsTheSameGeometricRun()
        {
            var route=Route(TestFixtures.P(0,240),TestFixtures.P(0,480),TestFixtures.P(0,1200));
            var step=ManualRouteGuidance.Read(Context(30,300),route,1,80,false,p=>true);
            TestAssert.False(step.Correcting || step.NeedReplan);TestAssert.Near(900,step.ForwardDistance,.01f);TestAssert.Equal(2,step.EndWaypointIndex);
        }
        public static void BlockedRejoinRequestsANativeReplanInsteadOfUnsafeDirections()
        {
            var step=ManualRouteGuidance.Read(Context(720,600),Route(TestFixtures.P(0,1200)),0,80,false,p=>false);
            TestAssert.True(step.NeedReplan);
        }
        public static void HumanCornerToleranceRequiresAClearNextSegment()
        {
            var route=Route(TestFixtures.P(0,240),TestFixtures.P(480,240));var c=Context(0,230);
            var progress=new RouteGuidanceProgress(route.Origin,route.Waypoints,false,80);
            var step=progress.Read(c,route,p=>p.X<=0);TestAssert.Equal(0,step.EndWaypointIndex);
            step=progress.Read(c,route,p=>true);TestAssert.Equal(1,step.EndWaypointIndex);
        }
        public static void FinalApproachStillNeedsTheActualEndpoint()
        {
            var route=Route(TestFixtures.P(0,240));var c=Context(30,240);
            var step=ManualRouteGuidance.Read(c,route,0,80,false,p=>true);TestAssert.False(step.AtApproach);TestAssert.True(step.Correcting);
            c.Position=TestFixtures.P(0,240);step=ManualRouteGuidance.Read(c,route,0,80,true,p=>true);TestAssert.True(step.AtApproach);
        }
        public static void WorldWrapRetainsTheForwardRunAndLocalCorrection()
        {
            var c=Context(1535,0);c.LogicalMapKey="world:0";var route=Route(TestFixtures.P(2,0),TestFixtures.P(8,0));route.Origin=TestFixtures.P(1534,0);
            var step=ManualRouteGuidance.Read(c,route,0,.5f,false,p=>true);TestAssert.Near(9,step.ForwardDistance,.01f);TestAssert.False(step.Correcting);
        }
        public static void CorrectionHysteresisAvoidsFlappingAtTheDriftThreshold()
        {
            var route=Route(TestFixtures.P(0,1200));
            TestAssert.True(ManualRouteGuidance.Read(Context(130,600),route,0,80,false,p=>true).Correcting);
            TestAssert.True(ManualRouteGuidance.Read(Context(110,600),route,0,80,true,p=>true).Correcting);
            TestAssert.False(ManualRouteGuidance.Read(Context(79,600),route,0,80,true,p=>true).Correcting);
        }
        public static void ABlockedTinyDriftKeepsTheVerifiedEndOfTheRun()
        {
            var end=TestFixtures.P(0,1200);
            var step=ManualRouteGuidance.Read(Context(2,600),Route(end),0,80,false,p=>p.Equals(end));
            TestAssert.False(step.Correcting || step.NeedReplan);
            TestAssert.Equal(end,step.Aim);
        }
        public static void ABlockedParallelLineRequestsASmallSafeCorrection()
        {
            var step=ManualRouteGuidance.Read(Context(30,600),Route(TestFixtures.P(0,1200)),0,80,false,p=>p.X==0);
            TestAssert.True(step.Correcting && !step.NeedReplan);TestAssert.Near(0,step.Aim.X,.01f);
        }
        public static void InvalidOriginPositionAndRadiusNeverProduceAnActionableStep()
        {
            var route=Route(TestFixtures.P(0,1200));route.Origin=new NavigationPoint(float.NaN,0,0);
            TestAssert.True(ManualRouteGuidance.Read(Context(),route,0,80,false,p=>true).NeedReplan);
            route=Route(TestFixtures.P(0,1200));var c=Context();c.Position=new NavigationPoint(float.PositiveInfinity,0,0);
            TestAssert.True(ManualRouteGuidance.Read(c,route,0,80,false,p=>true).NeedReplan);
            TestAssert.True(ManualRouteGuidance.Read(Context(),route,0,float.NaN,false,p=>true).NeedReplan);
        }
        public static void NativeFollowerProgressCarriesIntoManualGuidanceWithoutBacktracking()
        {
            var route=Route(TestFixtures.P(0,240),TestFixtures.P(480,240),TestFixtures.P(480,1200));
            var progress=new RouteGuidanceProgress(route.Origin,route.Waypoints,false,80);progress.ObserveWaypoint(route.Waypoints[2]);
            var step=progress.Read(Context(480,600),route,p=>true);
            TestAssert.False(step.Correcting);TestAssert.Near(600,step.ForwardDistance,.01f);TestAssert.Equal(2,step.EndWaypointIndex);
        }
    }
}
