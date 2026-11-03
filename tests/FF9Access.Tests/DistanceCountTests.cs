using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class DistanceCountTests
    {
        private static NavigationUpdate Run(float distance,bool world=false)
        {
            var u=StableManualSpeechTests.Run();
            if(world){u.Context.LogicalMapKey="world:0";u.Context.WorldControlType=0;}
            var goal=TestFixtures.P(0,distance);u.Selected.Position=goal;
            u.Movement.PlannedRoute.Waypoints=new[]{goal};u.Movement.PlannedRoute.ApproachPoint=goal;u.Movement.RoutePoint=goal;
            return u;
        }
        public static void FieldDistanceDoesNotShrinkWhenOnlyTheControlAngleChanges()
        {
            var u=Run(600);TestAssert.Equal("up 3.",RouteInstruction.Resolve(u).Text);
            u.Context.InputOrientationDegrees=10;TestAssert.Equal("up 3.",RouteInstruction.Resolve(u).Text);
            u.Context.InputOrientationDegrees=45;TestAssert.Equal("up and left 3.",RouteInstruction.Resolve(u).Text);
        }
        public static void WorldDistanceDoesNotShrinkWhenOnlyTheCameraAngleChanges()
        {
            var u=Run(10,true);TestAssert.Equal("up 3.",RouteInstruction.Resolve(u).Text);
            u.Context.InputOrientationDegrees=10;TestAssert.Equal("up 3.",RouteInstruction.Resolve(u).Text);
        }
        public static void CorrectionAndFollowingRunKeepTheirGeometricCounts()
        {
            var u=Run(1200);u.Context.Position=TestFixtures.P(600,300);u.Context.InputOrientationDegrees=10;
            TestAssert.Equal("Correction: left 3. Then up 4.",RouteInstruction.Resolve(u).Text);
        }
        public static void ARouteUnavailableBearingUsesTheSameDistanceMeasure()
        {
            var u=Run(600);u.Movement.RouteAvailable=false;u.Context.InputOrientationDegrees=10;
            TestAssert.Equal("Bearing only, up 3. No walking route.",RouteInstruction.Resolve(u).Text);
        }
    }
}
