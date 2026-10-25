using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class FieldMovementTests
    {
        private sealed class Follower : IFieldFollower
        {
            public long Generation {get;set;}=1;public int PlayerIdentity {get;set;}=7;public long Owner {get;set;}public bool HasPath {get;set;}
            public NavigationPoint[] Reversed;public bool Cleared;public NavigationPoint Position {get;set;}public float ArrivalRadius {get{return 1;}}
            public bool Install(long owner,NavigationPoint[] reversed) {if(HasPath)return false;Owner=owner;Reversed=reversed;HasPath=true;return true;}
            public void Clear(long owner) {if(Owner!=owner)return;Cleared=true;HasPath=false;Owner=0;}
        }
        private static RoutePlan Route() {return new RoutePlan{Success=true,Generation=1,Transport=TestFixtures.Context().Transport,TargetId=new TargetIdentity(TargetKind.FieldActor,"field:100",10),ApproachPoint=TestFixtures.P(30,0),Waypoints=new[]{TestFixtures.P(10,0),TestFixtures.P(20,0),TestFixtures.P(30,0)}};}
        private static NavigationTarget Target() {return new NavigationTarget{Id=Route().TargetId,Generation=1};}
        public static void FollowerConsumesCorrectWaypointOrder() {var fake=new Follower();var movement=new OwnedFieldFollower(fake);TestAssert.Equal(MovementStartResult.Started,movement.Start(TestFixtures.Context(),Target(),Route(),2));TestAssert.Near(10,fake.Reversed[fake.Reversed.Length-1].X,0);}
        public static void OwnedWaypointMovesWithNeutralAnalogControls()
        {
            var fake=new Follower();var movement=new OwnedFieldFollower(fake);
            movement.Start(TestFixtures.Context(),Target(),Route(),2);
            float speed=FieldAnalogMovement.Speed(10,30,1,0,0.1f,fake.Owner,fake.HasPath);
            TestAssert.Near(30,speed,0);
            movement.Release(2,StopReason.ManualMovement);
            TestAssert.Near(0,FieldAnalogMovement.Speed(10,30,1,0,0.1f,fake.Owner,fake.HasPath),0);
            TestAssert.Near(20,FieldAnalogMovement.Speed(10,30,0.5f,0.5f,0.1f,fake.Owner,false),0);
        }
        public static void UnownedOrCompletedPathsDoNotBypassAnalogDeadZone()
        {
            TestAssert.Near(0,FieldAnalogMovement.Speed(10,30,1,0,0.1f,0,true),0);
            TestAssert.Near(0,FieldAnalogMovement.Speed(10,30,1,0,0.1f,2,false),0);
        }
        public static void OwnedApproachDoesNotSkipOrOvershootShortWaypoints()
        {
            TestAssert.Near(4,FieldAnalogMovement.ArrivalRadius(96,2),0);
            TestAssert.Near(5,FieldAnalogMovement.StepMagnitude(30,5,2,true),0);
            TestAssert.Near(30,FieldAnalogMovement.StepMagnitude(30,50,2,true),0);
            TestAssert.Near(96,FieldAnalogMovement.ArrivalRadius(96,0),0);
            TestAssert.Near(30,FieldAnalogMovement.StepMagnitude(30,5,0,true),0);
        }
        public static void ManualMovementReleasesOwnedPathOnly() {var fake=new Follower();var movement=new OwnedFieldFollower(fake);movement.Start(TestFixtures.Context(),Target(),Route(),2);fake.Owner=3;movement.Release(2,StopReason.ManualMovement);TestAssert.False(fake.Cleared);TestAssert.True(fake.HasPath);}
        public static void NativeClearIsAnnouncedAsInterrupted() {var fake=new Follower();var movement=new OwnedFieldFollower(fake);movement.Start(TestFixtures.Context(),Target(),Route(),2);fake.HasPath=false;TestAssert.Equal(FieldOutcome.Interrupted,movement.Read(TestFixtures.Context(),Target(),new InteractionReadiness()).Outcome);}
        public static void CompetingNpcDoesNotProduceReady() {var fake=new Follower();var movement=new OwnedFieldFollower(fake);movement.Start(TestFixtures.Context(),Target(),Route(),2);var readiness=new InteractionReadiness{InRange=true,Talkable=true,FacingWithinArc=true,SelectedCollision=false};TestAssert.False(movement.Read(TestFixtures.Context(),Target(),readiness).Outcome==FieldOutcome.ReadyToInteract);}
        public static void TalkabilityAndFacingAreRequired() {var fake=new Follower();var movement=new OwnedFieldFollower(fake);movement.Start(TestFixtures.Context(),Target(),Route(),2);var ready=new InteractionReadiness{InRange=true,SelectedCollision=true,FacingWithinArc=true};TestAssert.False(movement.Read(TestFixtures.Context(),Target(),ready).Outcome==FieldOutcome.ReadyToInteract);ready.Talkable=true;ready.FacingWithinArc=false;TestAssert.Equal(FieldOutcome.TurnNeeded,movement.Read(TestFixtures.Context(),Target(),ready).Outcome);ready.FacingWithinArc=true;TestAssert.Equal(FieldOutcome.ReadyToInteract,movement.Read(TestFixtures.Context(),Target(),ready).Outcome);}
        public static void BattleRebuildNeverUsesOldFollower() {var fake=new Follower();var movement=new OwnedFieldFollower(fake);movement.Start(TestFixtures.Context(),Target(),Route(),2);TestAssert.Equal(FieldOutcome.Interrupted,movement.Read(TestFixtures.Context(generation:2),Target(),new InteractionReadiness()).Outcome);TestAssert.False(fake.HasPath);}
        public static void ExistingNativePathIsNotOverwritten() {var fake=new Follower{HasPath=true,Owner=0};TestAssert.Equal(MovementStartResult.Busy,new OwnedFieldFollower(fake).Start(TestFixtures.Context(),Target(),Route(),2));TestAssert.False(fake.Cleared);}
        public static void ReleasedRouteCannotClaimLaterManualExit(){var fake=new Follower();var movement=new OwnedFieldFollower(fake);var route=Route();route.TargetId=new TargetIdentity(TargetKind.FieldExit,"field:100",10);var target=Target();target.Id=route.TargetId;movement.Start(TestFixtures.Context(),target,route,2);movement.Release(2,StopReason.Explicit);movement.ObserveExit(10);TestAssert.False(movement.Read(TestFixtures.Context(),target,null).Outcome==FieldOutcome.ArrivedExit);}
    }
}
