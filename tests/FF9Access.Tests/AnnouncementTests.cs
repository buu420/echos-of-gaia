using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class AnnouncementsTests
    {
        public static void GuidanceDoesNotPointBackToConsumedWaypoints(){var p=new RouteGuidanceProgress(TestFixtures.P(0,0),new[]{TestFixtures.P(10,0),TestFixtures.P(20,0),TestFixtures.P(30,0)},false,1);TestAssert.Near(30,p.Next(TestFixtures.P(25,0)).X,0);}
        public static void RegionChangeDeduplicates(){var n=new NavigationCoordinator();var c=TestFixtures.Context("world:0");c.CurrentRegion="Eunorus Plains";var u=n.Tick(c,new NavigationCommand[0],new CatalogResult(),null);var a=new NavigationAnnouncements();var offered=a.Format(u,1000,new SpeechState());TestAssert.Equal(1,offered.Count);a.Acknowledge(offered[0],true);TestAssert.Equal(0,a.Format(u,1000,new SpeechState{NowMilliseconds=20}).Count);}
        public static void DialogueIsNotInterruptedByRepeatedGuidance(){var u=new NavigationCoordinator().Tick(TestFixtures.Context(),new[]{NavigationCommand.StartGuidance},IntegrationTests.Catalog(),null);TestAssert.Equal(0,new NavigationAnnouncements().Format(u,1000,new SpeechState{DialogueActive=true}).Count);}
        public static void RepeatUsesFacadeLastAnnouncement(){var u=new NavigationCoordinator().Tick(TestFixtures.Context(),new[]{NavigationCommand.Repeat},IntegrationTests.Catalog(),null);var a=new NavigationAnnouncements().Format(u,1000,new SpeechState{LastAnnouncement="Actual accepted dialogue."});TestAssert.Equal("Actual accepted dialogue.",a[0].Text);}
        public static void BusySpeechDoesNotConsumeGuidanceInterval(){var n=new NavigationCoordinator();n.Tick(TestFixtures.Context(),new[]{NavigationCommand.StartGuidance},IntegrationTests.Catalog(),null);var u=n.Tick(TestFixtures.Context(),new NavigationCommand[0],IntegrationTests.Catalog(),null);var a=new NavigationAnnouncements();TestAssert.Equal(0,a.Format(u,1000,new SpeechState{Busy=true,NowMilliseconds=1000}).Count);TestAssert.Equal(1,a.Format(u,1000,new SpeechState{NowMilliseconds=1100}).Count);}
        public static void GuidanceUsesActualFieldTwist(){var c=TestFixtures.Context();c.InputOrientationDegrees=90;var coordinator=new NavigationCoordinator();var u=coordinator.Tick(c,new[]{NavigationCommand.StartGuidance},IntegrationTests.Catalog(),null);var a=new NavigationAnnouncements();a.Format(u,1000,new SpeechState());u=coordinator.Tick(c,new NavigationCommand[0],IntegrationTests.Catalog(),null);var text=a.Format(u,500,new SpeechState{NowMilliseconds=1000})[0].Text;TestAssert.True(text.Contains("up"));}
    }
}
