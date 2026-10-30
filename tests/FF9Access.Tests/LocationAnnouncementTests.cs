using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class LocationAnnouncementTests
    {
        private static NavigationUpdate Update(string map="field:2950",string name="Chocobo's Forest")
        {
            var context=TestFixtures.Context(map);context.CurrentLocation=context.CurrentRegion=name;
            return new NavigationCoordinator().Tick(context,new NavigationCommand[0],new CatalogResult(),null);
        }
        public static void RejectedLocationSpeechIsRetried()
        {
            var announcer=new NavigationAnnouncements();var update=Update();var first=announcer.Format(update,1000,new SpeechState());
            TestAssert.Equal(1,first.Count);announcer.Acknowledge(first[0],false);
            var retry=announcer.Format(update,1000,new SpeechState{NowMilliseconds=16});
            TestAssert.Equal(1,retry.Count);TestAssert.Equal("Chocobo's Forest",retry[0].Text);
        }
        public static void AcceptedLocationIsQuietUntilMapOrRegionChanges()
        {
            var announcer=new NavigationAnnouncements();var update=Update();var first=announcer.Format(update,1000,new SpeechState());
            announcer.Acknowledge(first[0],true);TestAssert.Equal(0,announcer.Format(update,1000,new SpeechState()).Count);
            TestAssert.Equal(1,announcer.Format(Update("field:2951","Chocobo's Lagoon"),1000,new SpeechState()).Count);
        }
        public static void BusyDialogueAndNarrationWaitBeforeReadingTheNewMap()
        {
            foreach(var state in new[]{new SpeechState{Busy=true},new SpeechState{DialogueActive=true},new SpeechState{NarrationActive=true}}) {
                var announcer=new NavigationAnnouncements();var update=Update();TestAssert.Equal(0,announcer.Format(update,1000,state).Count);
                TestAssert.Equal(1,announcer.Format(update,1000,new SpeechState()).Count);
            }
        }
        public static void LateNativeNameIsSpokenWithoutANumericFallback()
        {
            var announcer=new NavigationAnnouncements();TestAssert.Equal(0,announcer.Format(Update(name:""),1000,new SpeechState()).Count);
            TestAssert.Equal("Chocobo's Forest",announcer.Format(Update(),1000,new SpeechState())[0].Text);
        }
    }
}
