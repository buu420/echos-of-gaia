using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class WorldMovementTests
    {
        private sealed class Port : IWorldMovementPort
        {
            public int Starts,Stops,EventBits=0x1234;public WorldSteeringCommand Last;
            public WorldMovementState State=new WorldMovementState{ArrivalRadius=1};
            public bool StartTown(int id,long owner){Starts++;State.NativeAutopilotActive=true;return true;}
            public void StopTown(long owner){Stops++;State.NativeAutopilotActive=false;}
            public void SetMovement(NavigationContext c,WorldSteeringCommand command,long owner){Last=command;}
            public void ClearMovement(long owner){Last=new WorldSteeringCommand();}
            public WorldMovementState Read(NavigationContext c,NavigationTarget t){return State;}
        }
        private static NavigationContext Context(TransportKind kind=TransportKind.Foot,int status=0) {var c=TestFixtures.Context("world:0");c.Transport=new TransportIdentity(kind,7,status);return c;}
        private static NavigationTarget Town() {return new NavigationTarget{Id=new TargetIdentity(TargetKind.WorldLocation,"world:0",8),NativeLocationId=8,Generation=1,Position=TestFixtures.P(40,-20)};}
        private static RoutePlan Route(NavigationContext c,NavigationTarget t){return new RoutePlan{Success=true,TargetId=t.Id,Generation=1,Transport=c.Transport,ApproachPoint=t.Position,Waypoints=new[]{t.Position}};}
        public static void BoatDoesNotStartAirshipAutopilot() {var p=new Port();var c=Context(TransportKind.Boat,8);var m=new OwnedWorldTravel(p);m.Start(c,Town(),Route(c,Town()),2);TestAssert.Equal(0,p.Starts);}
        public static void BothAirshipStatusesUseNativeAutopilot() {foreach(int status in new[]{9,10}){var p=new Port();var c=Context(status==9?TransportKind.HildaGarde:TransportKind.Invincible,status);new OwnedWorldTravel(p).Start(c,Town(),Route(c,Town()),2);TestAssert.Equal(1,p.Starts);}}
        public static void CancelCallsNativeOff() {var p=new Port();var c=Context(TransportKind.HildaGarde,9);var m=new OwnedWorldTravel(p);m.Start(c,Town(),Route(c,Town()),2);m.Release(2,StopReason.Explicit);TestAssert.Equal(1,p.Stops);}
        public static void SyntheticThrottleNeverSetsEventConfirm() {var p=new Port();var c=Context(TransportKind.Boat,8);var m=new OwnedWorldTravel(p);m.Start(c,Town(),Route(c,Town()),2);m.Tick(c,Town());TestAssert.Equal(0x1234,p.EventBits);TestAssert.True(p.Last.Throttle>=0);}
        public static void ManualReverseStopsOwnedTravel() {var p=new Port();var c=Context();var m=new OwnedWorldTravel(p);m.Start(c,Town(),Route(c,Town()),2);m.Release(2,StopReason.ManualMovement);TestAssert.Equal(0L,m.Owner);TestAssert.Equal(0,p.Last.Throttle);}
        public static void TownArrivalDoesNotUseRegionEquality() {var p=new Port();var c=Context(TransportKind.HildaGarde,9);var m=new OwnedWorldTravel(p);m.Start(c,Town(),Route(c,Town()),2);TestAssert.Equal(WorldOutcome.Moving,m.Tick(c,Town()).Outcome);p.State.NativeAutopilotActive=false;TestAssert.Equal(WorldOutcome.Interrupted,m.Tick(c,Town()).Outcome);}
        public static void VehicleArrivalUsesFreshBoardingApproach() {var p=new Port();var c=Context();var t=Town();t.Id=new TargetIdentity(TargetKind.Vehicle,"world:0",10);t.NativeLocationId=-1;var m=new OwnedWorldTravel(p);m.Start(c,t,Route(c,t),2);t.Position=TestFixtures.P(50,-20);TestAssert.Equal(WorldOutcome.TargetMoved,m.Tick(c,t).Outcome);}
        public static void PresentedVehicleInteractionEndsApproachWithoutFurtherMovement()
        {
            var p=new Port();var c=Context();var t=Town();t.Id=new TargetIdentity(TargetKind.Vehicle,"world:0",10);t.NativeLocationId=-1;
            var m=new OwnedWorldTravel(p);m.Start(c,t,Route(c,t),2);
            p.State.PresentedInteractionUid=11;
            TestAssert.Equal(WorldOutcome.Moving,m.Tick(c,t).Outcome);TestAssert.Equal(2L,m.Owner);
            p.State.PresentedInteractionUid=10;
            TestAssert.Equal(WorldOutcome.ApproachReached,m.Tick(c,t).Outcome);TestAssert.Equal(0L,m.Owner);
            TestAssert.Equal(0,p.Last.LX);TestAssert.Equal(0,p.Last.LY);TestAssert.Equal(0,p.Last.Throttle);
            TestAssert.Equal(0x1234,p.EventBits);
        }
        public static void StaleOverlayDoesNotDeliverMovement() {var state=new MovementOverlayState();var c=Context();state.Set(c,new WorldSteeringCommand{LX=70},2);c.Generation=2;WorldSteeringCommand value;TestAssert.False(state.TryRead(c,out value));}
        public static void NativeControlLockRevokesAlreadyPublishedMovement()
        {
            var state=new MovementOverlayState();var c=Context();state.Set(c,new WorldSteeringCommand{LX=70,Throttle=48},2);
            c.CanControl=false;c.GateReason=GateReason.NoControl;WorldSteeringCommand value;
            TestAssert.False(state.TryRead(c,out value));TestAssert.Equal(0,value.LX);TestAssert.Equal(0,value.Throttle);
            var port=new Port();c=Context(TransportKind.HildaGarde,9);var owned=new OwnedWorldTravel(port);
            owned.Start(c,Town(),Route(c,Town()),3);c.CanControl=false;c.GateReason=GateReason.NoControl;
            TestAssert.Equal(WorldOutcome.Interrupted,owned.Tick(c,Town()).Outcome);TestAssert.Equal(1,port.Stops);
        }
        public static void HumanAxesMatchNativeCameraTransform() {var command=WorldSteering.For(Context(),new NavigationPoint(10,0,0),new WorldMovementState{CameraDegrees=0,ControlType=0});TestAssert.True(command.LX<0);TestAssert.Equal(0,command.Throttle);}
    }
}
