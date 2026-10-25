using System.Collections.Generic;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class FieldTargetsTests
    {
        public static void RetainedPositionUsesCurrentStoryGuards()
        {
            var catalog=new FieldTargetCatalog();var item=Actor(9);item.StoryMilestones=new[]{1150};item.NextFields=new[]{2510};Build(catalog,item);
            item.Visible=false;item.OutOfView=true;item.Position=TestFixtures.P(500,700);item.StoryMilestones=new int[0];item.NextFields=new int[0];
            var current=Build(catalog,item).Find(item.Id);TestAssert.Equal(9f,current.Position.X);TestAssert.Equal(0,current.StoryMilestones.Length);TestAssert.Equal(0,current.NextFields.Length);
        }
        public static void VisibleNoninteractiveActorUsesViewingApproachInEveryField()
        {
            var item=Actor(9);item.HasInteraction=false;var target=Build(new FieldTargetCatalog(),item).Find(item.Id);
            TestAssert.True(target.UseViewingPoint);TestAssert.False(target.IsInteractive);
        }
        public static void LastSeenPersonLosingInteractionDoesNotKeepAnOldConfirmClaim()
        {
            var catalog=new FieldTargetCatalog();var item=Actor(9);item.HasInteraction=true;Build(catalog,item);
            item.Visible=false;item.OutOfView=true;item.HasInteraction=false;
            var target=Build(catalog,item).Find(item.Id);TestAssert.False(target.IsInteractive);TestAssert.True(target.UseViewingPoint);TestAssert.True(target.Availability.Contains("interaction unavailable"));
        }
        public static void RevokedRememberedTalkingRegionCannotSteerAViewingRoute()
        {
            var context=TestFixtures.Context();context.Position=new NavigationPoint(10,0,10);
            var catalog=new FieldTargetCatalog();var item=Actor(9);item.Position=new NavigationPoint(50,0,50);item.HasInteraction=true;item.InteractionUid=19;
            item.InteractionRegion=new[]{new NavigationPoint(600,0,20),new NavigationPoint(650,0,20),new NavigationPoint(650,0,60),new NavigationPoint(600,0,60)};
            catalog.Build(context,new[]{item},null);item.Visible=false;item.OutOfView=true;item.HasInteraction=false;item.InteractionUid=-1;
            var target=catalog.Build(context,new[]{item},null).Find(item.Id);
            var geometry=new FieldGeometrySnapshot{Generation=context.Generation,Radius=0,ActiveTriangle=0,Triangles=new[]{new FieldTriangle{Id=0,Vertices=new[]{new NavigationPoint(0,0,0),new NavigationPoint(1000,0,0),new NavigationPoint(0,0,1000)}}}};
            var route=new FieldRoutePlanner().Plan(context,target,geometry);TestAssert.True(route.Success);TestAssert.True(route.ViewingPoint);TestAssert.True(NavigationPoint.Distance(route.ApproachPoint,target.Position)<100);
        }
        public static void LastSeenExitUsesFreshApproachPermissionsWithoutLosingItsLabel()
        {
            var catalog=new FieldTargetCatalog();var item=Door();Build(catalog,item);
            item.Exit.Visible=false;item.OutOfView=true;item.Exit.ApproachEnabled=false;item.Exit.Availability="current entrance trigger unavailable";
            var target=Build(catalog,item).Find(item.Id);TestAssert.True(target!=null);TestAssert.Equal(0,target.ApproachCandidates.Length);TestAssert.True(target.Availability.Contains("unavailable"));
        }
        public static void LearnedPickupUpdatesLabelAndSelectedCategory()
        {
            var item=Actor(8);item.IsPerson=false;item.HasInteraction=true;var catalog=new FieldTargetCatalog();var coordinator=new NavigationCoordinator();var context=TestFixtures.Context();
            var update=coordinator.Tick(context,new[]{NavigationCommand.NextCategory,NavigationCommand.NextCategory},Build(catalog,item),null);TestAssert.Equal(NavigationCategory.Objects,update.Category);
            item.IsItem=true;update=coordinator.Tick(context,new NavigationCommand[0],Build(catalog,item),null);TestAssert.Equal(NavigationCategory.Items,update.Category);TestAssert.True(update.Selected.Label.StartsWith("Item "));
            TestAssert.Equal(RouteOperation.None,update.RouteOperation);
        }
        private static FieldTargetObservation Actor(int id, bool visible = true) { return new FieldTargetObservation { Id = new TargetIdentity(TargetKind.FieldActor, "field:100", id), Position = TestFixtures.P(id, 0), Visible = visible, IsPerson = true }; }
        private static FieldTargetObservation Door(int id = 0) { return new FieldTargetObservation { Id = new TargetIdentity(TargetKind.FieldExit, "field:100", id), Position = TestFixtures.P(4, 0), Exit = new ExitPresentation { Visible = true, ApproachEnabled = true, Cue = "Reviewed background doorway", SourceFingerprint = "capture" } }; }
        private static CatalogResult Build(FieldTargetCatalog catalog, params FieldTargetObservation[] values) { return catalog.Build(TestFixtures.Context(), values, new ObservedIdentityStore(null)); }
        public static void HiddenActorIsNotSelectable() { var item = Actor(1, false); TestAssert.False(Build(new FieldTargetCatalog(), item).Contains(item.Id)); }
        public static void SeenActorOutsideViewportKeepsOnlyItsObservedPosition()
        {
            var catalog=new FieldTargetCatalog();var item=Actor(9);item.HasInteraction=true;
            item.ApproachCandidates=new[]{TestFixtures.P(5,2)};
            var first=Build(catalog,item).Find(item.Id);
            item.Visible=false;item.OutOfView=true;item.Position=TestFixtures.P(500,700);
            item.ApproachCandidates[0]=TestFixtures.P(900,900);
            var remembered=Build(catalog,item).Find(item.Id);
            TestAssert.True(remembered!=null);
            TestAssert.Equal(first.Position.X,remembered.Position.X);
            TestAssert.Equal(5f,remembered.ApproachCandidates[0].X);
            TestAssert.True(remembered.Availability.Contains("last seen"));
        }
        public static void NeverSeenActorOutsideViewportIsNotDiscovered()
        {
            var item=Actor(9,false);item.OutOfView=true;
            TestAssert.Equal(0,Build(new FieldTargetCatalog(),item).Targets.Count);
        }
        public static void HiddenPreviouslySeenActorIsRemoved()
        {
            var catalog=new FieldTargetCatalog();var item=Actor(9);Build(catalog,item);
            item.Visible=false;item.OutOfView=false;
            TestAssert.Equal(0,Build(catalog,item).Targets.Count);
            item.OutOfView=true;TestAssert.Equal(0,Build(catalog,item).Targets.Count);
        }
        public static void DeletedPreviouslySeenActorIsRemoved()
        {
            var catalog=new FieldTargetCatalog();var item=Actor(9);Build(catalog,item);
            TestAssert.Equal(0,Build(catalog).Targets.Count);
            item.Visible=false;item.OutOfView=true;
            TestAssert.Equal(0,Build(catalog,item).Targets.Count);
        }
        public static void ReusedActorIdDoesNotInheritAnUnseenLandmark()
        {
            var catalog=new FieldTargetCatalog();var item=Actor(9);item.NativePresentationIdentity=41;Build(catalog,item);
            item.Visible=false;item.OutOfView=true;item.NativePresentationIdentity=42;
            TestAssert.Equal(0,Build(catalog,item).Targets.Count);
        }
        public static void SeenActorsAreNotCarriedAcrossSceneOrSaveOrPlayerChanges()
        {
            foreach(int change in new[]{0,1,2}) {
                var catalog=new FieldTargetCatalog();var item=Actor(9);var context=TestFixtures.Context();
                catalog.Build(context,new[]{item},null);item.Visible=false;item.OutOfView=true;
                if(change==0)context.Generation++;else if(change==1)context.SaveIdentity="save-b";else context.PlayerIdentity++;
                TestAssert.Equal(0,catalog.Build(context,new[]{item},null).Targets.Count);
            }
        }
        public static void ReappearingActorRefreshesTheObservedPosition()
        {
            var catalog=new FieldTargetCatalog();var item=Actor(9);Build(catalog,item);
            item.Visible=false;item.OutOfView=true;item.Position=TestFixtures.P(500,700);Build(catalog,item);
            item.Visible=true;item.OutOfView=false;
            TestAssert.Equal(500f,Build(catalog,item).Find(item.Id).Position.X);
        }
        public static void LeavingAndReturningToTheMapDoesNotReuseOldLandmarks()
        {
            var catalog=new FieldTargetCatalog();var item=Actor(9);Build(catalog,item);
            catalog.Build(TestFixtures.Context("field:101"),new FieldTargetObservation[0],null);
            item.Visible=false;item.OutOfView=true;
            TestAssert.Equal(0,Build(catalog,item).Targets.Count);
        }
        public static void RememberedLandmarkSelectionIsAnnouncedAsLastSeen()
        {
            var catalog=new FieldTargetCatalog();var item=Actor(9);var coordinator=new NavigationCoordinator();
            var context=TestFixtures.Context();
            coordinator.Tick(context,new[]{NavigationCommand.NextCategory},Build(catalog,item),null);
            item.Visible=false;item.OutOfView=true;
            var update=coordinator.Tick(context,new[]{NavigationCommand.NextTarget},Build(catalog,item),null);
            TestAssert.True(update.Messages[0].Contains("last seen"));
        }
        public static void ActualAttributedIconExposesBackgroundInteraction()
        {
            var item=Actor(3,false);item.ShowsIcon=true;item.IsPerson=false;item.HasInteraction=true;
            var target=Build(new FieldTargetCatalog(),item).Find(item.Id);
            TestAssert.Equal("Object A",target.Label);TestAssert.Equal(VisibilityEvidence.NativeInteractionIcon,target.Evidence);
        }
        public static void VisibleUnnamedActorGetsStableLabel() { var catalog = new FieldTargetCatalog(); var item = Actor(8); string label = Build(catalog, item).Find(item.Id).Label; item.Position = TestFixtures.P(100, 0); TestAssert.Equal(label, Build(catalog, Actor(3), item).Find(item.Id).Label); }
        public static void DoorWithoutIconRemainsAvailable() { var item = Door(); item.ShowsIcon = false; TestAssert.Equal("Exit A", Build(new FieldTargetCatalog(), item).Find(item.Id).Label); }
        public static void ConditionalHiddenJumpIsExcluded() { var item = Door(); item.Exit.Visible = false; item.ContainsJump = true; TestAssert.False(Build(new FieldTargetCatalog(), item).Contains(item.Id)); }
        public static void FutureItemAndBattleOutcomesStayUnknown() { var item = Actor(2); item.IsPerson = false; item.UnobservedOutcome = "Potion and battle"; TestAssert.Equal("Object A", Build(new FieldTargetCatalog(), item).Find(item.Id).Label); }
        public static void DistanceChangeDoesNotRenameExit() { var catalog = new FieldTargetCatalog(); var first = Door(); var second = Door(1); second.Position = TestFixtures.P(50, 0); var before = Build(catalog, first, second); first.Position = TestFixtures.P(500, 0); var after = Build(catalog, first, second); TestAssert.Equal(before.Find(first.Id).Label, after.Find(first.Id).Label); }
        public static void VisibleClosedDoorRemainsInformative() { var item = Door(); item.Exit.Availability = "closed for now"; TestAssert.True(Build(new FieldTargetCatalog(), item).Find(item.Id).Availability.Contains("closed")); }
        public static void VisibleEntranceWithoutSafeApproachRemainsInformative()
        {
            var item=Door();item.Exit.ApproachEnabled=false;item.Exit.Availability="safe approach unavailable";
            var target=Build(new FieldTargetCatalog(),item).Find(item.Id);
            TestAssert.True(target!=null);TestAssert.Equal(0,target.ApproachCandidates.Length);
            TestAssert.True(target.Availability.Contains("unavailable"));
            var geometry=new FieldGeometrySnapshot{Generation=1,Triangles=new[]{new FieldTriangle{Id=0,Vertices=new[]{TestFixtures.P(-10,-10),TestFixtures.P(10,-10),TestFixtures.P(0,10)}}}};
            TestAssert.False(new FieldRoutePlanner().Plan(TestFixtures.Context(),target,geometry).Success);
        }
        public static void DisplayedAttributedDialoguePromotesOnlyObservedName()
        {
            string name;TestAssert.True(DisplayedSpeakerLabel.TryRead("Moguo\n“Can I help you, kupo?”",out name));
            var identities=new ObservedIdentityStore(null);var item=Actor(10);identities.Record(item.Id,name);
            var catalog=new FieldTargetCatalog().Build(TestFixtures.Context(),new[]{item},identities);
            TestAssert.Equal("Moguo",catalog.Targets[0].Label);
            TestAssert.False(DisplayedSpeakerLabel.TryRead("Received\nPotion",out name));
            TestAssert.False(DisplayedSpeakerLabel.TryRead("Choose an option.\nSave",out name));
            TestAssert.False(DisplayedSpeakerLabel.TryRead("An ordinary unlabelled speech line",out name));
        }
    }
}
