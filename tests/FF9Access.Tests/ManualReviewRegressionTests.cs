using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class ManualBeforeStartTests
    {
        public static void CorrectionCountsTheRemainingRunFromItsActualJoin()
        {
            var u=StableManualSpeechTests.Run();u.Context.Position=TestFixtures.P(600,-600);
            var step=ManualRouteGuidance.Read(u.Context,u.Movement.PlannedRoute,0,80,false,p=>true);
            TestAssert.True(step.Correcting);TestAssert.Near(1200,step.ForwardDistance,.01f);
        }
        public static void UncorrectedSmallBackwardOffsetStillCountsFromThePlayer()
        {
            var u=StableManualSpeechTests.Run();u.Context.Position=TestFixtures.P(0,-30);
            var step=ManualRouteGuidance.Read(u.Context,u.Movement.PlannedRoute,0,80,false,p=>true);
            TestAssert.False(step.Correcting);TestAssert.Near(1230,step.ForwardDistance,.01f);
        }
    }
    internal static class ManualSecondaryTurnTests
    {
        public static void AChangedFollowOnKeyDirectionSpeaksEvenWhenTheCorrectionKeyStaysTheSame()
        {
            var u=StableManualSpeechTests.Run();u.Context.Position=TestFixtures.P(600,-600);u.Context.InputOrientationDegrees=20;
            var speech=new NavigationAnnouncements();var first=speech.Format(u,5000,new SpeechState());speech.Acknowledge(first[0],true);
            u.Context.InputOrientationDegrees=30;var changed=speech.Format(u,5000,new SpeechState{NowMilliseconds=100});
            TestAssert.Equal(1,changed.Count);TestAssert.True(changed[0].Text.Contains("Then up and left"));speech.Acknowledge(changed[0],false);
            TestAssert.Equal(1,speech.Format(u,5000,new SpeechState{NowMilliseconds=200}).Count);
        }
    }
    internal static class ManualBlockedBendTests
    {
        public static void ABlockedNonfinalBendNeverClaimsTheTargetIsHere()
        {
            var c=TestFixtures.Context();c.Position=TestFixtures.P(0,242);
            var route=new RoutePlan{Success=true,Origin=TestFixtures.P(0,0),Waypoints=new[]{TestFixtures.P(0,240),TestFixtures.P(480,240)}};
            var progress=new RouteGuidanceProgress(route.Origin,route.Waypoints,false,80);
            var step=progress.Read(c,route,p=>p.X==0);TestAssert.True(step.NeedReplan);TestAssert.False(step.AtApproach);
        }
    }
    internal static class ManualWrappedRunTests
    {
        public static void LongWorldRunsStopBeforeTheNativeWrappedDirectionReverses()
        {
            var c=TestFixtures.Context("world:0");var route=new RoutePlan{Success=true,Origin=TestFixtures.P(0,0),Waypoints=new[]{TestFixtures.P(600,0),TestFixtures.P(1200,0)}};
            var step=ManualRouteGuidance.Read(c,route,0,.5f,false,p=>WorldCoordinates.Difference(c.Position,p).X>=0);
            TestAssert.False(step.NeedReplan);TestAssert.Equal(0,step.EndWaypointIndex);TestAssert.Near(600,step.ForwardDistance,.01f);
        }
        public static void APlayerOffsetNearTheHalfWorldSpanRejoinsBeforeGivingTheLongRun()
        {
            var c=TestFixtures.Context("world:0");c.Position=TestFixtures.P(1535.5f,0);
            var route=new RoutePlan{Success=true,Origin=TestFixtures.P(0,0),Waypoints=new[]{TestFixtures.P(768,0)}};
            var step=ManualRouteGuidance.Read(c,route,0,.5f,false,p=>WorldCoordinates.Difference(c.Position,p).X>=0);
            TestAssert.True(step.Correcting && !step.NeedReplan);TestAssert.Near(768,step.ForwardDistance,.01f);
        }
    }
}
