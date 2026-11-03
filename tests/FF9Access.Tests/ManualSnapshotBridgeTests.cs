using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class ManualSnapshotBridgeTests
    {
        public static void AParallelForwardAimStillUsesTheStableManualRun()
        {
            var u=StableManualSpeechTests.Run();u.Context.Position=TestFixtures.P(30,300);
            u.Movement.ManualStep=ManualRouteGuidance.Read(u.Context,u.Movement.PlannedRoute,1,80,false,p=>true);u.Movement.RoutePoint=u.Movement.ManualStep.Aim;
            var instruction=RouteInstruction.Resolve(u);TestAssert.True(instruction.StableLeg);TestAssert.Equal("up 4.",instruction.Text);TestAssert.Equal(2,instruction.EndWaypointIndex);
        }
        public static void ARejoinPointBetweenPathNodesSpeaksTheCorrectionAndRemainingRun()
        {
            var u=StableManualSpeechTests.Run();u.Context.Position=TestFixtures.P(720,600);
            u.Movement.ManualStep=ManualRouteGuidance.Read(u.Context,u.Movement.PlannedRoute,0,80,false,p=>true);u.Movement.RoutePoint=u.Movement.ManualStep.Aim;
            TestAssert.Equal("Correction: left 3. Then up 3.",RouteInstruction.Resolve(u).Text);
        }
        public static void AutomaticFollowerInstructionsKeepTheirNativeWaypoint()
        {
            var u=StableManualSpeechTests.Run();u.Session.Mode=RouteMode.Moving;u.Session.IsAutomatic=true;
            u.Movement.ManualStep=new ManualGuidanceStep{Aim=TestFixtures.P(720,600),Correcting=true};
            TestAssert.Equal("up 5.",RouteInstruction.Resolve(u).Text);
        }
    }
}
