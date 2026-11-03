using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class EquivalentManualRunTests
    {
        private static void Accept(NavigationAnnouncements speech,NavigationUpdate u)
        {foreach(var a in speech.Format(u,5000,new SpeechState()))speech.Acknowledge(a,true);}
        public static void EquivalentRouteObjectsDoNotRestartUnchangedDirections()
        {
            var u=StableManualSpeechTests.Run();var speech=new NavigationAnnouncements();Accept(speech,u);
            var old=u.Movement.PlannedRoute;u.Movement.PlannedRoute=new RoutePlan{Success=true,Origin=old.Origin,TargetId=old.TargetId,Generation=old.Generation,Transport=old.Transport,ApproachPoint=old.ApproachPoint,Waypoints=(NavigationPoint[])old.Waypoints.Clone()};
            TestAssert.Equal(0,speech.Format(u,5000,new SpeechState{NowMilliseconds=100}).Count);
        }
        public static void DifferentNodeDensityOnTheSameStraightRunStaysQuiet()
        {
            var u=StableManualSpeechTests.Run();var speech=new NavigationAnnouncements();Accept(speech,u);
            u.Movement.PlannedRoute.Waypoints=new[]{TestFixtures.P(0,600),TestFixtures.P(0,1200)};u.Movement.RoutePoint=TestFixtures.P(0,600);
            TestAssert.Equal(0,speech.Format(u,5000,new SpeechState{NowMilliseconds=100}).Count);
        }
        public static void ARefreshedEndpointWithTheSameUsefulInstructionDoesNotRepeatIt()
        {
            var u=StableManualSpeechTests.Run();var speech=new NavigationAnnouncements();Accept(speech,u);
            u.Movement.PlannedRoute.Waypoints=new[]{TestFixtures.P(0,240),TestFixtures.P(0,480),TestFixtures.P(0,1230)};
            TestAssert.Equal(0,speech.Format(u,5000,new SpeechState{NowMilliseconds=100}).Count);
        }
        public static void ARealNewTurnSpeaksImmediately()
        {
            var u=StableManualSpeechTests.Run();var speech=new NavigationAnnouncements();Accept(speech,u);
            u.Movement.PlannedRoute.Waypoints=new[]{TestFixtures.P(0,600),TestFixtures.P(480,600),TestFixtures.P(480,1200)};u.Movement.RoutePoint=TestFixtures.P(0,600);
            TestAssert.Equal("up 3.",speech.Format(u,5000,new SpeechState{NowMilliseconds=100})[0].Text);
        }
        public static void AnActualControlRotationUpdatesTheKeysWithoutClaimingAReroute()
        {
            var u=StableManualSpeechTests.Run();var speech=new NavigationAnnouncements();Accept(speech,u);u.Context.InputOrientationDegrees=90;
            TestAssert.Equal("left 5.",speech.Format(u,5000,new SpeechState{NowMilliseconds=100})[0].Text);
        }
        public static void DriftCorrectionReturnsToCurrentProgressAndRemainsRetryable()
        {
            var u=StableManualSpeechTests.Run();var speech=new NavigationAnnouncements();Accept(speech,u);u.Context.Position=TestFixtures.P(720,600);
            var offered=speech.Format(u,5000,new SpeechState{NowMilliseconds=100});TestAssert.Equal("Correction: left 3. Then up 3.",offered[0].Text);speech.Acknowledge(offered[0],false);
            TestAssert.Equal("Correction: left 3. Then up 3.",speech.Format(u,5000,new SpeechState{NowMilliseconds=200})[0].Text);
        }
    }
}
