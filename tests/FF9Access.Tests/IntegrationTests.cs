using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class IntegrationTests
    {
        internal static CatalogResult Catalog(){var c=new CatalogResult();for(int i=0;i<2;i++)c.Targets.Add(new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldExit,"field:100",10+i),Label="Exit "+(i==0?"A":"B"),Category=NavigationCategory.Exits,Generation=1,Position=TestFixtures.P(100+i*10,0)});return c;}
        private static NavigationUpdate Tick(NavigationCoordinator n,params NavigationCommand[] commands){return n.Tick(TestFixtures.Context(),commands,Catalog(),null);}
        public static void ManualTakeoverKeepsOptionalPassiveGuidance(){var n=new NavigationCoordinator();Tick(n,NavigationCommand.ToggleAutomatic);var u=Tick(n,NavigationCommand.ManualMovement);TestAssert.Equal(RouteMode.Guiding,u.Session.Mode);TestAssert.True(n.Session.GuidanceEnabled);TestAssert.True(u.Session.ReleaseMovement);}
        public static void PStopsBothModes(){var n=new NavigationCoordinator();Tick(n,NavigationCommand.ToggleAutomatic);var u=Tick(n,NavigationCommand.ToggleAutomatic);TestAssert.Equal(RouteMode.Idle,u.Session.Mode);TestAssert.False(u.Session.IsAutomatic);TestAssert.False(n.Session.GuidanceEnabled);}
        public static void IResumesGuidanceOnly(){var n=new NavigationCoordinator();Tick(n,NavigationCommand.ToggleAutomatic);var c=TestFixtures.Context();c.Foreground=false;n.Tick(c,new NavigationCommand[0],Catalog(),null);var u=Tick(n,NavigationCommand.ToggleGuidance);TestAssert.Equal(RouteMode.Guiding,u.Session.Mode);TestAssert.False(u.Session.IsAutomatic);}
        public static void TargetChangeStopsOldTravel(){var n=new NavigationCoordinator();Tick(n,NavigationCommand.ToggleAutomatic);var u=Tick(n,NavigationCommand.NextTarget);TestAssert.True(u.Session.ReleaseMovement);TestAssert.Equal(11,u.Selected.Id.NativeId);TestAssert.Equal(RouteMode.Idle,u.Session.Mode);}
        public static void ChangedTargetCannotInheritThePreviousRouteBearing()
        {
            var n=new NavigationCoordinator();var c=TestFixtures.Context();var catalog=Catalog();n.Tick(c,new NavigationCommand[0],catalog,null);
            var route=new RoutePlan{Success=true,Generation=c.Generation,Transport=c.Transport,TargetId=catalog.Targets[0].Id};
            var movement=new MovementObservation{RouteAvailable=true,RoutePoint=TestFixtures.P(0,-100),PlannedRoute=route};
            var update=n.Tick(c,new[]{NavigationCommand.NextTarget},catalog,movement);
            TestAssert.False(update.Movement.RouteAvailable);TestAssert.False(update.Movement.RoutePoint.HasValue);
            TestAssert.True(NavigationAnnouncements.Status(update).Contains("Bearing only, right"));
        }
        public static void ChangedTransportCannotInheritThePreviousRouteBearing()
        {
            var n=new NavigationCoordinator();var c=TestFixtures.Context("world:0");var catalog=new CatalogResult();
            var target=new NavigationTarget{Id=new TargetIdentity(TargetKind.WorldLocation,"world:0",0),Generation=1,Category=NavigationCategory.Towns,Label="Alexandria Harbor",Position=TestFixtures.P(100,100)};catalog.Targets.Add(target);
            n.Tick(c,new NavigationCommand[0],catalog,null);
            var route=new RoutePlan{Success=true,Generation=c.Generation,Transport=c.Transport,TargetId=target.Id};
            c.Transport=new TransportIdentity(TransportKind.Boat,6,8);c.PlayerIdentity=6;
            var update=n.Tick(c,new[]{NavigationCommand.Status},catalog,new MovementObservation{RouteAvailable=true,RoutePoint=TestFixtures.P(-100,0),PlannedRoute=route});
            TestAssert.False(update.Movement.RouteAvailable);TestAssert.False(update.Movement.RoutePoint.HasValue);
            TestAssert.True(NavigationAnnouncements.Status(update).Contains("Bearing only, northeast"));
        }
        public static void MatchingRouteRetainsItsValidatedWaypointBearing()
        {
            var n=new NavigationCoordinator();var c=TestFixtures.Context();var catalog=Catalog();n.Tick(c,new NavigationCommand[0],catalog,null);
            var route=new RoutePlan{Success=true,Generation=c.Generation,Transport=c.Transport,TargetId=catalog.Targets[0].Id};
            var update=n.Tick(c,new[]{NavigationCommand.Status},catalog,new MovementObservation{RouteAvailable=true,RoutePoint=TestFixtures.P(0,100),PlannedRoute=route});
            TestAssert.True(update.Movement.RouteAvailable);TestAssert.True(NavigationAnnouncements.Status(update).Contains("up a little."));TestAssert.False(NavigationAnnouncements.Status(update).Contains("Bearing only"));
        }
        public static void ITogglesSpeechIndependentlyDuringTravel(){var n=new NavigationCoordinator();Tick(n,NavigationCommand.ToggleAutomatic);var u=Tick(n,NavigationCommand.ToggleGuidance);TestAssert.True(u.Session.IsAutomatic);TestAssert.False(n.Session.GuidanceEnabled);TestAssert.False(u.Session.ReleaseMovement);}
        public static void ArrivalAndFailureAreHandledAlongsideInformationalCommands()
        {
            foreach(var command in new[]{NavigationCommand.Status,NavigationCommand.Repeat,NavigationCommand.DecreaseInterval,NavigationCommand.IncreaseInterval}) {
                foreach(var outcome in new[]{MovementOutcome.ApproachReached,MovementOutcome.ReadyToInteract,MovementOutcome.Unavailable,MovementOutcome.Interrupted}) {
                    var n=new NavigationCoordinator();Tick(n,NavigationCommand.StartAutomatic);
                    var update=n.Tick(TestFixtures.Context(),new[]{command},Catalog(),new MovementObservation{Outcome=outcome});
                    bool arrived=outcome==MovementOutcome.ApproachReached || outcome==MovementOutcome.ReadyToInteract;
                    TestAssert.Equal(arrived?RouteMode.Arrived:RouteMode.Guiding,update.Session.Mode);
                    TestAssert.True(update.Session.ReleaseMovement);TestAssert.True(update.Messages.Count>0);
                    var following=Tick(n);TestAssert.Equal(update.Session.Mode,following.Session.Mode);
                    TestAssert.Equal(RouteOperation.None,following.RouteOperation);
                }
            }
        }
        public static void TargetAndMovementCommandsTakePriorityOverAnOldArrival()
        {
            foreach(var command in new[]{NavigationCommand.NextTarget,NavigationCommand.Stop,NavigationCommand.ManualMovement,NavigationCommand.Confirm,NavigationCommand.ToggleAutomatic,NavigationCommand.ToggleGuidance}) {
                var n=new NavigationCoordinator();Tick(n,NavigationCommand.StartAutomatic);
                var update=n.Tick(TestFixtures.Context(),new[]{command},Catalog(),new MovementObservation{Outcome=MovementOutcome.ApproachReached});
                TestAssert.False(update.Session.Mode==RouteMode.Arrived);
                foreach(string message in update.Messages)TestAssert.False(message.StartsWith("Approach reached"));
            }
        }
        public static void LostTargetDoesNotSilentlySwitch(){var n=new NavigationCoordinator();Tick(n,NavigationCommand.ToggleAutomatic);var c=Catalog();c.Targets.RemoveAt(0);var u=n.Tick(TestFixtures.Context(),new NavigationCommand[0],c,null);TestAssert.True(u.Selected==null);TestAssert.True(u.Session.ReleaseMovement);TestAssert.False(u.Session.IsAutomatic);}
        public static void UnavailableCatalogSpeaksReason(){var c=TestFixtures.Context();c.CanControl=false;c.PlayerIdentity=-1;c.GateReason=GateReason.UnknownActor;var u=new NavigationCoordinator().Tick(c,new[]{NavigationCommand.Status},new CatalogResult(),null);var a=new NavigationAnnouncements().Format(u,1000,new SpeechState());TestAssert.Equal("Navigation unavailable: player control not established.",a[0].Text);}
        public static void PassiveRouteChangesRequestGuidanceWithoutStartingMovement()
        {
            foreach(var outcome in new[]{MovementOutcome.TargetMoved,MovementOutcome.RouteChanged}) {
                var n=new NavigationCoordinator();Tick(n,NavigationCommand.StartGuidance);
                var u=n.Tick(TestFixtures.Context(),new NavigationCommand[0],Catalog(),new MovementObservation{Outcome=outcome});
                TestAssert.Equal(RouteOperation.PlanGuidance,u.RouteOperation);
                TestAssert.False(u.Session.IsAutomatic);TestAssert.Equal(RouteMode.Guiding,u.Session.Mode);
            }
        }
        public static void GuidanceAfterRandomBattleUsesFreshRoute()
        {
            var n=new NavigationCoordinator();Tick(n,NavigationCommand.StartGuidance);
            var c=TestFixtures.Context();c.CanControl=false;c.GateReason=GateReason.Battle;c.BattleTransition=BattleTransition.RandomStarted;
            n.Session.Update(c,NavigationSignal.SceneDetach);
            c=TestFixtures.Context(generation:3);c.BattleTransition=BattleTransition.RandomReturned;
            var u=n.Tick(c,new NavigationCommand[0],Catalog(),null);
            TestAssert.Equal(RouteOperation.PlanGuidance,u.RouteOperation);TestAssert.False(u.Session.IsAutomatic);
        }
        public static void FailedFieldGuidanceDoesNotReplanUnchangedGeometry()
        {
            foreach(bool automatic in new[]{false,true}) {
                var geometry=FieldRoutesTests.Grid(false);geometry.Fingerprint="disconnected";
                foreach(var triangle in geometry.Triangles)for(int edge=0;edge<3;edge++)triangle.Neighbors[edge]=-1;
                var context=TestFixtures.Context();context.Position=TestFixtures.P(10,10);
                var target=Catalog().Targets[0];target.Position=TestFixtures.P(50,10);
                var catalog=new CatalogResult();catalog.Targets.Add(target);
                var coordinator=new NavigationCoordinator();coordinator.Tick(context,new[]{automatic?NavigationCommand.StartAutomatic:NavigationCommand.StartGuidance},catalog,null);
                var failed=new FieldRoutePlanner().Plan(context,target,geometry);TestAssert.False(failed.Success);
                coordinator.Tick(context,new NavigationCommand[0],catalog,new MovementObservation{Outcome=MovementOutcome.Unavailable});
                for(int frame=0;frame<60;frame++) {
                    var outcome=GuidanceRouteValidation.Check(failed,target,target,geometry.Fingerprint,geometry.Radius,false,true);
                    var update=coordinator.Tick(context,new NavigationCommand[0],catalog,new MovementObservation{Outcome=outcome});
                    TestAssert.Equal(RouteOperation.None,update.RouteOperation);TestAssert.Equal(0,update.Messages.Count);
                }
                TestAssert.Equal(MovementOutcome.RouteChanged,GuidanceRouteValidation.Check(failed,target,target,"floor changed",geometry.Radius,false,true));
            }
        }
        public static void MissingAndIncompleteGeometryWaitForMeaningfulChange()
        {
            var context=TestFixtures.Context("world:0");var target=Catalog().Targets[0];
            var missing=new WorldRoutePlanner().Plan(context,target,null);
            TestAssert.Equal(MovementOutcome.None,GuidanceRouteValidation.Check(missing,target,target,null,0.5f,true,true));
            var incomplete=new WorldGeometrySnapshot(1,"loading",new WorldSurface[0],null,false);
            var failed=new WorldRoutePlanner().Plan(context,target,incomplete);
            for(int frame=0;frame<60;frame++)TestAssert.Equal(MovementOutcome.None,GuidanceRouteValidation.Check(failed,target,target,"loading",0.5f,true,true));
            TestAssert.Equal(MovementOutcome.RouteChanged,GuidanceRouteValidation.Check(failed,target,target,"ready",0.5f,true,true));
        }
        public static void FieldGuidancePreservesNearCornerWithLargePlayerRadius()
        {
            var corner=TestFixtures.P(50,0);var end=TestFixtures.P(50,50);
            var progress=new RouteGuidanceProgress(TestFixtures.P(0,0),new[]{corner,end},false,96);
            TestAssert.Equal(corner,progress.Next(TestFixtures.P(10,0)));
            TestAssert.Equal(end,progress.Next(TestFixtures.P(50,1)));
        }
    }
}
