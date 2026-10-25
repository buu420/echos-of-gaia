using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class StoryNavigationTests
    {
        private sealed class Follower:IFieldFollower
        {
            public long Generation{get{return 1;}}public int PlayerIdentity{get{return 7;}}public long Owner{get;private set;}public bool HasPath{get{return Owner!=0;}}
            public NavigationPoint Position{get{return TestFixtures.P(200,200);}}public float ArrivalRadius{get{return 10;}}
            public bool Install(long owner,NavigationPoint[] path){Owner=owner;return true;}public void Clear(long owner){if(Owner==owner)Owner=0;}
        }
        public static void StoryExitUsesThePhysicalNativeTransitionIdentity()
        {
            var context=TestFixtures.Context();context.Position=TestFixtures.P(200,200);
            var physical=Exit(20,101);physical.ApproachCandidates=new[]{TestFixtures.P(400,400)};
            var selected=StoryTargetCatalog.Wrap(context,1150,physical,"Continue to the next story room");
            var geometry=new FieldGeometrySnapshot{Generation=1,ActiveTriangle=0,Radius=40,Triangles=new[]{new FieldTriangle{Id=0,Vertices=new[]{TestFixtures.P(0,0),TestFixtures.P(1000,0),TestFixtures.P(0,1000)}}}};
            var route=new FieldRoutePlanner().Plan(context,selected,geometry);TestAssert.True(route.Success);TestAssert.True(route.TargetId.Equals(selected.Id));TestAssert.True(route.PhysicalIdentity.Equals(physical.Id));
            var follower=new OwnedFieldFollower(new Follower());TestAssert.Equal(MovementStartResult.Started,follower.Start(context,selected,route,9));
            follower.ObserveExit(21);TestAssert.False(follower.ExitConfirmed);follower.ObserveExit(20);TestAssert.True(follower.ExitConfirmed);
            TestAssert.Equal(FieldOutcome.ArrivedExit,follower.Read(context,selected,null).Outcome);
        }
        public static void WorldStoryUsesPresentedTownOrAvailableAirship()
        {
            var context=TestFixtures.Context("world:1");context.StoryProgress=9400;
            var catalog=new StoryTargetCatalog(new[]{new StoryStage{Milestone=9410,GoalFields=new[]{1450},Goal="Ipsen's Castle"}},null);
            var town=new NavigationTarget{Id=new TargetIdentity(TargetKind.WorldLocation,context.LogicalMapKey,20),Generation=1,Category=NavigationCategory.Towns,Label="Ipsen's Castle",NativeLocationId=20,Position=TestFixtures.P(20,20),Evidence=VisibilityEvidence.NativeMarker};
            var input=Targets(town);catalog.Append(context,input,-1);TestAssert.True(input.Targets[1].PhysicalIdentity.Equals(town.Id));TestAssert.Equal(20,input.Targets[1].NativeLocationId);
            var ship=new NavigationTarget{Id=new TargetIdentity(TargetKind.Vehicle,context.LogicalMapKey,9),Generation=1,Category=NavigationCategory.Vehicles,Label="Hilda Garde III",Position=TestFixtures.P(5,5),Evidence=VisibilityEvidence.NativeMarker};
            input=Targets(ship);catalog.Append(context,input,-1);TestAssert.True(input.Targets[1].PhysicalIdentity.Equals(ship.Id));TestAssert.True(input.Targets[1].Label.Contains("travel toward"));
        }
        public static void FlightCheckpointNeverSubstitutesTheBoat()
        {
            var context=TestFixtures.Context("world:1");context.StoryProgress=10400;
            var catalog=new StoryTargetCatalog(new[]{new StoryStage{Milestone=10500,Goal="Ipsen's Castle",GoalFields=new[]{2500},RequiredVehicle="Hilda Garde III"}},null);
            var boat=new NavigationTarget{Id=new TargetIdentity(TargetKind.Vehicle,context.LogicalMapKey,8),Generation=1,Category=NavigationCategory.Vehicles,Label="Blue Narciss"};
            var ship=new NavigationTarget{Id=new TargetIdentity(TargetKind.Vehicle,context.LogicalMapKey,9),Generation=1,Category=NavigationCategory.Vehicles,Label="Hilda Garde III"};
            var input=Targets(boat,ship);catalog.Append(context,input,-1);TestAssert.Equal(3,input.Targets.Count);TestAssert.True(input.Targets[2].PhysicalIdentity.Equals(ship.Id));
            input=Targets(boat);catalog.Append(context,input,-1);TestAssert.True(input.Targets[1].PhysicalId==null);
        }
        public static void NativeWorldIdentitySurvivesLocalizedOrOverriddenLabels()
        {
            var context=TestFixtures.Context("world:1");context.StoryProgress=10600;
            var catalog=new StoryTargetCatalog(new[]{new StoryStage{Milestone=10620,Goal="Water Shrine",WorldLocations=new[]{43}}},null);
            var shrine=new NavigationTarget{Id=new TargetIdentity(TargetKind.WorldLocation,context.LogicalMapKey,43),NativeLocationId=43,Generation=1,Category=NavigationCategory.Towns,Label="Wasserschrein"};
            var impostor=new NavigationTarget{Id=new TargetIdentity(TargetKind.WorldLocation,context.LogicalMapKey,39),NativeLocationId=39,Generation=1,Category=NavigationCategory.Towns,Label="Water Shrine"};
            var input=Targets(impostor,shrine);catalog.Append(context,input,-1);TestAssert.Equal(3,input.Targets.Count);TestAssert.True(input.Targets[2].PhysicalIdentity.Equals(shrine.Id));
        }
        public static void ShrineDepartureDoesNotRouteIntoItsCinematicShipRoom()
        {
            var context=TestFixtures.Context("field:2850");context.StoryProgress=10620;
            var catalog=new StoryTargetCatalog(new[]{new StoryStage{Milestone=10640,Goal="Hilda Garde III",GoalFields=new[]{2851},DepartureFields=new[]{2850},WorldPlaces=new[]{"Fire Shrine"}}},new[]{new StoryTransit{From=2850,To=2851},new StoryTransit{From=2851,To=2850}});
            var room=new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldExit,context.LogicalMapKey,9),Generation=1,Category=NavigationCategory.Exits,NextFields=new[]{2851},ApproachCandidates=new[]{TestFixtures.P(3,3)}};
            var pilot=new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldActor,context.LogicalMapKey,1),Generation=1,Category=NavigationCategory.People,IsInteractive=true,StoryMilestones=new[]{10640},StoryAction="Talk to the pilot to return to the world map",ApproachCandidates=new[]{TestFixtures.P(2,2)}};
            var input=Targets(room,pilot);catalog.Append(context,input,2850);TestAssert.Equal(3,input.Targets.Count);TestAssert.True(input.Targets[2].PhysicalIdentity.Equals(pilot.Id));TestAssert.True(input.Targets[2].Label.Contains("Fire Shrine"));
            input=Targets(room);catalog.Append(context,input,2850);TestAssert.True(input.Targets[1].PhysicalId==null);TestAssert.True(input.Targets[1].Label.Contains("Fire Shrine"));
        }
        private static StoryTargetCatalog Catalog(){return new StoryTargetCatalog(new[]{new StoryStage{Milestone=1000,GoalFields=new[]{50},Goal="the cargo room"},new StoryStage{Milestone=1150,GoalFields=new[]{104},Goal="the ticket booth"},new StoryStage{Milestone=1160,GoalFields=new[]{108},Goal="the rooftops"}},new[]{new StoryTransit{From=100,To=101},new StoryTransit{From=101,To=103},new StoryTransit{From=103,To=104},new StoryTransit{From=100,To=107},new StoryTransit{From=107,To=100}});}
        private static NavigationTarget Exit(int id,int next){return new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldExit,"field:100",id),Generation=1,Category=NavigationCategory.Exits,Label="Exit "+id,Evidence=VisibilityEvidence.VisibleWalkmeshEdge,ApproachCandidates=new[]{TestFixtures.P(100,100)},NextFields=new[]{next}};}
        private static CatalogResult Targets(params NavigationTarget[] targets){var result=new CatalogResult();foreach(var target in targets)result.Targets.Add(target);return result;}
        public static void MainStoryChoosesRequiredTransitWithoutOptionalDetour()
        {
            var context=TestFixtures.Context();context.StoryProgress=1000;
            var input=Targets(Exit(10,101),Exit(11,107));Catalog().Append(context,input,100);
            NavigationTarget story=null;int count=0;foreach(var t in input.Targets)if(t.Category==NavigationCategory.Story){story=t;count++;}
            TestAssert.Equal(1,count);TestAssert.Equal(10,story.PhysicalIdentity.NativeId);TestAssert.True(story.Label.Contains("ticket booth"));TestAssert.Equal(3,input.Targets.Count);
        }
        public static void GraphKnownOriginStillOffersItsCurrentWorldDeparture()
        {
            var context=TestFixtures.Context();context.StoryProgress=10400;
            var catalog=new StoryTargetCatalog(new[]{new StoryStage{Milestone=10500,Goal="Ipsen's Castle",GoalFields=new[]{2500}}},new[]{new StoryTransit{From=100,To=101},new StoryTransit{From=101,To=2500}});
            var input=Targets(Exit(9,9009));catalog.Append(context,input,100);TestAssert.True(input.Targets[1].PhysicalIdentity.Equals(input.Targets[0].Id));
        }
        public static void StorySelectionFollowsProgressAndMapChangesWithoutStartingMovement()
        {
            var context=TestFixtures.Context();context.StoryProgress=1000;var catalog=new CatalogResult();Catalog().Append(context,catalog,100);
            var coordinator=new NavigationCoordinator();coordinator.Tick(context,new[]{NavigationCommand.PreviousCategory},catalog,null);
            context.StoryProgress=1150;catalog=new CatalogResult();Catalog().Append(context,catalog,100);
            var update=coordinator.Tick(context,new NavigationCommand[0],catalog,null);TestAssert.Equal(NavigationCategory.Story,update.Category);TestAssert.True(update.Selected.Label.Contains("rooftops"));TestAssert.Equal(RouteOperation.None,update.RouteOperation);
            context.LogicalMapKey="world:1";context.Generation++;catalog=new CatalogResult();Catalog().Append(context,catalog,-1);
            update=coordinator.Tick(context,new NavigationCommand[0],catalog,null);TestAssert.Equal(NavigationCategory.Story,update.Category);TestAssert.True(update.Selected!=null);TestAssert.Equal(RouteOperation.None,update.RouteOperation);
        }
        public static void StoryAndOrdinaryCategoriesHaveDistinctSelectionIdentities()
        {
            var context=TestFixtures.Context();context.StoryProgress=1000;var input=Targets(Exit(10,101));Catalog().Append(context,input,100);
            var normal=input.Targets[0];var story=input.Targets[1];TestAssert.False(normal.Id.Equals(story.Id));TestAssert.True(story.PhysicalIdentity.Equals(normal.Id));TestAssert.True(input.Find(story.Id)==story);
        }
        public static void CurrentProgressAdvancesAndUnsupportedStepsStayHonest()
        {
            var context=TestFixtures.Context();context.StoryProgress=1150;var input=Targets(Exit(10,101));Catalog().Append(context,input,100);
            var story=input.Targets[input.Targets.Count-1];TestAssert.True(story.Label.Contains("rooftops"));TestAssert.True(story.PhysicalId==null);TestAssert.Equal(0,story.ApproachCandidates.Length);
        }
        public static void LocalObservedRequiredInteractionBeatsAnExit()
        {
            var context=TestFixtures.Context();context.StoryProgress=1000;
            var person=new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldActor,context.LogicalMapKey,7),Generation=1,Category=NavigationCategory.People,Label="Person A",IsInteractive=true,Evidence=VisibilityEvidence.RenderedActor,StoryMilestones=new[]{1150},ApproachCandidates=new[]{TestFixtures.P(10,10)}};
            var input=Targets(Exit(10,101),person);Catalog().Append(context,input,100);
            var story=input.Targets[input.Targets.Count-1];TestAssert.True(story.PhysicalIdentity.Equals(person.Id));TestAssert.True(story.IsInteractive);
        }
        public static void NeverCreatesAnUnseenStoryActorOrHiddenReward()
        {
            var context=TestFixtures.Context();context.StoryProgress=1000;var input=new CatalogResult();Catalog().Append(context,input,104);
            TestAssert.Equal(1,input.Targets.Count);TestAssert.True(input.Targets[0].PhysicalId==null);TestAssert.Equal(0,input.Targets[0].ApproachCandidates.Length);
        }
        public static void InformationalStoryNeverSpeaksAnInventedBearing()
        {
            var context=TestFixtures.Context();context.StoryProgress=1150;var input=new CatalogResult();Catalog().Append(context,input,100);
            var coordinator=new NavigationCoordinator();var update=coordinator.Tick(context,new[]{NavigationCommand.PreviousCategory,NavigationCommand.StartGuidance},input,null);
            var speech=new NavigationAnnouncements();speech.Format(update,1000,new SpeechState());update.Messages.Clear();
            string text=speech.Format(update,1000,new SpeechState{NowMilliseconds=2000})[0].Text;
            TestAssert.True(text.Contains("rooftops"));TestAssert.False(text.Contains("bearing"));TestAssert.False(text.Contains("units"));
            text=NavigationAnnouncements.Status(update);TestAssert.False(text.Contains("bearing"));TestAssert.False(text.Contains("units"));
        }
        public static void StoryPreservesLastSeenAndInteractionRegionSnapshots()
        {
            var context=TestFixtures.Context();context.StoryProgress=1000;
            var person=new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldActor,context.LogicalMapKey,7),Generation=1,Category=NavigationCategory.People,Label="Mene",Evidence=VisibilityEvidence.ObservedFieldLandmark,StoryMilestones=new[]{1150},InteractionUid=19,InteractionRegion=new[]{TestFixtures.P(5,5),TestFixtures.P(10,5),TestFixtures.P(10,10)},ApproachCandidates=new[]{TestFixtures.P(10,10)}};
            var input=Targets(person);Catalog().Append(context,input,104);var story=input.Targets[1];person.InteractionRegion[0]=TestFixtures.P(900,900);
            TestAssert.Near(5,story.InteractionRegion[0].X,0);TestAssert.Equal(VisibilityEvidence.ObservedFieldLandmark,story.Evidence);TestAssert.Equal(19,story.InteractionUid);
        }
    }
}
