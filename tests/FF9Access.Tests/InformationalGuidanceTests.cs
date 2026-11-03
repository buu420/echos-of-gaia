using System;
using System.Linq;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class InformationalGuidanceTests
    {
        private static NavigationUpdate Information()
        {
            var c=TestFixtures.Context("field:100:STREET");
            return new NavigationUpdate{Context=c,Session=new RouteDecision{Mode=RouteMode.Guiding},GuidanceEnabled=true,
                Selected=new NavigationTarget{Id=new TargetIdentity(TargetKind.Story,c.LogicalMapKey,1150),Generation=c.Generation,Category=NavigationCategory.Story,
                    Label="Show the ticket at the ticket booth.",Availability="Next area: Main Street. No walking approach is currently presented.",Position=c.Position,ApproachCandidates=new NavigationPoint[0]}};
        }
        private static string Speak(NavigationAnnouncements speech,NavigationUpdate update,double now=0,bool accepted=true)
        {
            var result=speech.Format(update,1000,new SpeechState{NowMilliseconds=now});
            foreach(var announcement in result)speech.Acknowledge(announcement,accepted);
            return String.Join(" ",result.Select(a=>a.Text));
        }
        public static void AcceptedStoryLimitationDoesNotRepeatOnTheGuidanceTimer()
        {
            var speech=new NavigationAnnouncements();var u=Information();
            TestAssert.True(Speak(speech,u).Contains("Next area"));
            for(int time=1000;time<=10000;time+=1000)TestAssert.Equal("",Speak(speech,u,time));
        }
        public static void AChangedLimitationIsReadOnceWithoutAnotherNavigationCommand()
        {
            var speech=new NavigationAnnouncements();var u=Information();Speak(speech,u);
            u.Selected.Availability="The ticket booth is in this area. Its approach is temporarily unavailable.";
            TestAssert.True(Speak(speech,u,30).Contains("in this area"));TestAssert.Equal("",Speak(speech,u,5000));
        }
        public static void RejectedLimitationDeliveryIsRetried()
        {
            var speech=new NavigationAnnouncements();var u=Information();Speak(speech,u,0,false);
            TestAssert.True(Speak(speech,u,30).Contains("Next area"));TestAssert.Equal("",Speak(speech,u,5000));
        }
        public static void DeliberateStatusAndRepeatStillReadAnUnchangedLimitation()
        {
            var speech=new NavigationAnnouncements();var u=Information();string last=Speak(speech,u);
            u.RequestStatus=true;TestAssert.True(Speak(speech,u,5000).Contains("Next area"));u.RequestStatus=false;
            u.RequestRepeat=true;var result=speech.Format(u,1000,new SpeechState{NowMilliseconds=6000,LastAnnouncement=last});
            TestAssert.Equal(last,result.Single().Text);
        }
        public static void GuidanceCommandDoesNotRepeatItsInformationOnTheNextTick()
        {
            var speech=new NavigationAnnouncements();var u=Information();u.Messages.Add("Guidance on. "+u.Selected.Availability);
            TestAssert.True(Speak(speech,u).Contains("Next area"));u.Messages.Clear();
            TestAssert.Equal("",Speak(speech,u,30));TestAssert.Equal("",Speak(speech,u,5000));
        }
    }
}
