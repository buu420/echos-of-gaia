using System;
using System.Linq;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class OpeningBoothTests
    {
        private static OpeningTicketBoothState State()
        {var c=TestFixtures.Context(OpeningTicketBoothEvidence.Map);c.StoryProgress=1000;c.PlayerIdentity=30;return new OpeningTicketBoothState{Context=c,Camera=0,PlayerUid=30,PlayerSid=30,PlayerModel=8,PlayerHash=OpeningTicketBoothEvidence.Player,DirectorHash=OpeningTicketBoothEvidence.Director,RegionSid=28,RegionHash=OpeningTicketBoothEvidence.Script,BackgroundHash=OpeningTicketBoothEvidence.Background,WalkmeshHash=OpeningTicketBoothEvidence.Walkmesh,ArtPresented=true,RegionOnScreen=true,NativeInteractionAllowed=true,Region=OpeningTicketBoothEvidence.Region};}
        public static void CurrentlyPresentedNativeBoothCanSupplyALocalStoryStep()
        {
            var s=State();TestAssert.True(OpeningTicketBoothEvidence.Available(s));
            var obs=new FieldTargetObservation{Id=new TargetIdentity(TargetKind.FieldActor,s.Context.LogicalMapKey,28),Visible=true,HasInteraction=true,Position=TestFixtures.P(25,-500),ApproachCandidates=new[]{TestFixtures.P(25,-500)},InteractionUid=28,InteractionRegion=s.Region};
            OpeningTicketBoothEvidence.Annotate(s.Context,obs);
            var input=new FieldTargetCatalog().Build(s.Context,new[]{obs},new ObservedIdentityStore());
            using(var story=OpeningNativeFixture.Story())OpeningNativeFixture.Catalog(story.RootElement).Append(s.Context,input,103,OpeningNativeFixture.Location);
            var target=input.Targets.Last(t=>t.Category==NavigationCategory.Story);TestAssert.Equal(28,target.PhysicalIdentity.NativeId);TestAssert.Equal(s.Context.LogicalMapKey,target.PhysicalIdentity.MapKey);TestAssert.True(target.Label.Contains("ticket booth"));TestAssert.Equal(28,target.InteractionUid);
        }
        public static void HiddenChangedOrForeignBoothsCannotAcquireTheReviewedLabel()
        {
            Action<OpeningTicketBoothState>[] changes={s=>s.ArtPresented=false,s=>s.RegionOnScreen=false,s=>s.NativeInteractionAllowed=false,s=>s.Camera=1,s=>s.PlayerHash="changed",s=>s.DirectorHash="changed",s=>s.RegionHash="changed",s=>s.BackgroundHash="changed",s=>s.WalkmeshHash="changed",s=>s.PlayerModel=98,s=>s.PlayerUid=17,s=>s.RegionSid=27,s=>s.Context.Foreground=false,s=>s.Context.CanControl=false,s=>s.Context.StoryProgress=999,s=>s.Context.StoryProgress=1190,s=>s.Context.LogicalMapKey="field:104:ROOM",s=>s.Region[0]=TestFixtures.P(-501,-111)};
            foreach(var change in changes){var s=State();change(s);TestAssert.False(OpeningTicketBoothEvidence.Available(s));}
        }
        public static void TicketCompletionReleasesTheBoothObjectiveForTheAlley()
        {
            var s=State();s.Context.StoryProgress=1150;
            var obs=new FieldTargetObservation{Id=new TargetIdentity(TargetKind.FieldActor,s.Context.LogicalMapKey,28),Visible=true,HasInteraction=true,Position=TestFixtures.P(25,-500),ApproachCandidates=new[]{TestFixtures.P(25,-500)}};
            OpeningTicketBoothEvidence.Annotate(s.Context,obs);TestAssert.True(obs.StoryMilestones==null || obs.StoryMilestones.Length==0);
            using(var scripts=OpeningNativeFixture.Scripts())using(var story=OpeningNativeFixture.Story()){
                var input=OpeningNativeFixture.Presented(scripts.RootElement,s.Context,103);input.Targets.Add(new NavigationTarget{Id=obs.Id,Category=NavigationCategory.Objects,Generation=s.Context.Generation,Label=obs.VisibleLabel,Position=obs.Position,IsInteractive=true,ApproachCandidates=obs.ApproachCandidates});
                OpeningNativeFixture.Catalog(story.RootElement).Append(s.Context,input,103,OpeningNativeFixture.Location);TestAssert.Equal(22,input.Targets.Last(t=>t.Category==NavigationCategory.Story).PhysicalIdentity.NativeId);
            }
        }
    }
}
