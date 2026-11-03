using System;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class QuietManualRefreshTests
    {
        public static void ValidationRefreshDoesNotAnnounceARerouteBeforeOneExists()
        {
            var n=new NavigationCoordinator();var c=TestFixtures.Context();var catalog=IntegrationTests.Catalog();
            n.Tick(c,new[]{NavigationCommand.StartGuidance},catalog,null);
            var u=n.Tick(c,new NavigationCommand[0],catalog,new MovementObservation{Outcome=MovementOutcome.RouteChanged});
            TestAssert.Equal(RouteOperation.PlanGuidance,u.RouteOperation);
            TestAssert.False(String.Join(" ",u.Messages).Contains("Route changed"));
        }
        public static void QuietRefreshKeepsTheDeliveredRunWithoutPlanningChatter()
        {
            var u=StableManualSpeechTests.Run();var speech=new NavigationAnnouncements();var first=speech.Format(u,5000,new SpeechState());speech.Acknowledge(first[0],true);
            u.RefreshGuidance=true;u.RouteOperation=RouteOperation.PlanGuidance;
            TestAssert.Equal(0,speech.Format(u,5000,new SpeechState{NowMilliseconds=100}).Count);
            u.RefreshGuidance=false;u.RouteOperation=RouteOperation.None;u.Movement.IsPlanning=u.Movement.QuietPlanning=true;
            TestAssert.Equal(0,speech.Format(u,5000,new SpeechState{NowMilliseconds=200}).Count);
            u.Movement.IsPlanning=u.Movement.QuietPlanning=false;
            TestAssert.Equal(0,speech.Format(u,5000,new SpeechState{NowMilliseconds=300}).Count);
        }
        public static void ExplicitStatusStillExplainsAQuietPlanningWait()
        {
            var u=StableManualSpeechTests.Run();u.IsPlanning=true;u.Movement.IsPlanning=u.Movement.QuietPlanning=true;u.RequestStatus=true;
            var result=new NavigationAnnouncements().Format(u,5000,new SpeechState());
            TestAssert.True(result[0].Text.Contains("Finding a walking route"));
        }
    }
}
