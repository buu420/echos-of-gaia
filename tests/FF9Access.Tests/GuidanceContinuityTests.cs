using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class GuidanceContinuityTests
    {
        private static NavigationTarget Exit(float x)
        {return new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldExit,"field:100",15),Position=TestFixtures.P(x,1200),ApproachCandidates=new[]{TestFixtures.P(0,1200),TestFixtures.P(160,1200)}};}
        public static void AStaticExitChangingItsNearestPresentationSampleIsNotAMovingTarget()
        {
            var old=Exit(0);var fresh=Exit(160);var route=new RoutePlan{Success=true,ApproachPoint=TestFixtures.P(0,1200),GeometryFingerprint="same"};
            TestAssert.Equal(MovementOutcome.None,GuidanceRouteValidation.Check(route,old,fresh,"same",80,false,true));
        }
        public static void FailedExitWaitsForRealChangesInsteadOfChasingNearestSample()
        {
            var old=Exit(0);var fresh=Exit(160);
            old.UseApprovedApproaches=fresh.UseApprovedApproaches=true;
            var route=new RoutePlan{Failure=RouteFailure.Disconnected,GeometryFingerprint="same"};
            TestAssert.Equal(MovementOutcome.None,GuidanceRouteValidation.Check(route,old,fresh,"same",80,false,true));
            TestAssert.Equal(MovementOutcome.RouteChanged,GuidanceRouteValidation.Check(route,old,fresh,"new",80,false,true));
            fresh.ApproachCandidates=new[]{TestFixtures.P(160,1200)};
            TestAssert.Equal(MovementOutcome.TargetMoved,GuidanceRouteValidation.Check(route,old,fresh,"same",80,false,true));
        }
        public static void RemovingThePlannedExitApproachStillRequiresReplanning()
        {
            var old=Exit(0);var fresh=Exit(160);fresh.ApproachCandidates=new[]{TestFixtures.P(160,1200)};
            var route=new RoutePlan{Success=true,ApproachPoint=TestFixtures.P(0,1200),GeometryFingerprint="same"};
            TestAssert.Equal(MovementOutcome.TargetMoved,GuidanceRouteValidation.Check(route,old,fresh,"same",80,false,true));
        }
        public static void ActualActorMovementAndBlockedSegmentsRemainInvalidations()
        {
            var old=Exit(0);var fresh=Exit(160);old.Id=fresh.Id=new TargetIdentity(TargetKind.FieldActor,"field:100",4);
            var route=new RoutePlan{Success=true,ApproachPoint=TestFixtures.P(0,1200),GeometryFingerprint="same"};
            TestAssert.Equal(MovementOutcome.TargetMoved,GuidanceRouteValidation.Check(route,old,fresh,"same",80,false,true));
            old=Exit(0);fresh=Exit(0);TestAssert.Equal(MovementOutcome.RouteChanged,GuidanceRouteValidation.Check(route,old,fresh,"same",80,false,false));
        }
    }
}
