using System;
namespace Memoria.Accessibility.Navigation
{
    public enum MovementStartResult { Started,InvalidContext,StaleRoute,Busy,Unavailable }
    public enum FieldOutcome { Moving,ReadyToInteract,ArrivedExit,TurnNeeded,ApproachReached,Interrupted,Unavailable }
    public sealed class InteractionReadiness {public bool InRange,SelectedCollision,Talkable,FacingWithinArc;public int FacingError;public string Evidence;}
    public sealed class FieldMovementOutcome {public FieldOutcome Outcome;public StopReason Reason;public string Evidence;}
    public interface IFieldFollower
    {
        long Generation {get;}int PlayerIdentity {get;}long Owner {get;}bool HasPath {get;}NavigationPoint Position {get;}float ArrivalRadius {get;}
        bool Install(long owner,NavigationPoint[] reversed);void Clear(long owner);
    }
    public sealed class OwnedFieldFollower
    {
        private readonly IFieldFollower _follower;private RoutePlan _route;private long _owner;private bool _exitTransition;
        public OwnedFieldFollower(IFieldFollower follower) {_follower=follower;}
        public long Owner {get{return _owner;}}
        public bool ExitConfirmed {get{return _exitTransition;}}
        public MovementStartResult Start(NavigationContext context,NavigationTarget target,RoutePlan route,long owner)
        {
            if(owner==0 || !context.CanControl || !context.Foreground || context.PlayerIdentity!=_follower.PlayerIdentity)return MovementStartResult.InvalidContext;
            if(route==null || !route.Success || route.Waypoints.Length==0)return MovementStartResult.Unavailable;
            if(route.Generation!=context.Generation || _follower.Generation!=context.Generation || !route.TargetId.Equals(target.Id) || !route.Transport.Equals(context.Transport))return MovementStartResult.StaleRoute;
            if(_owner!=0)Release(_owner,StopReason.Explicit);
            var reversed=(NavigationPoint[])route.Waypoints.Clone();Array.Reverse(reversed);
            if(!_follower.Install(owner,reversed))return MovementStartResult.Busy;
            _owner=owner;_route=route;_exitTransition=false;return MovementStartResult.Started;
        }
        public void ObserveExit(int nativeExecutingUid) {if(_owner!=0 && _route!=null && _route.PhysicalIdentity.Kind==TargetKind.FieldExit && _route.PhysicalIdentity.NativeId==nativeExecutingUid)_exitTransition=true;}
        public void Release(long owner,StopReason reason) {if(owner!=_owner || owner==0)return;_follower.Clear(owner);_owner=0;}
        public FieldMovementOutcome Read(NavigationContext context,NavigationTarget target,InteractionReadiness readiness)
        {
            if(_route==null)return new FieldMovementOutcome{Outcome=FieldOutcome.Unavailable};
            if(_exitTransition)return new FieldMovementOutcome{Outcome=FieldOutcome.ArrivedExit,Evidence="Selected native exit script requested the scene transition"};
            if(target==null || !_route.TargetId.Equals(target.Id) || _route.Generation!=context.Generation || !context.CanControl || !context.Foreground || !_route.Transport.Equals(context.Transport)) {Release(_owner,StopReason.UnknownContext);return new FieldMovementOutcome{Outcome=FieldOutcome.Interrupted,Reason=StopReason.UnknownContext};}
            if(readiness!=null && readiness.SelectedCollision && readiness.InRange && readiness.Talkable) {
                if(readiness.FacingWithinArc)return new FieldMovementOutcome{Outcome=FieldOutcome.ReadyToInteract,Evidence=readiness.Evidence};
                return new FieldMovementOutcome{Outcome=FieldOutcome.TurnNeeded,Evidence=readiness.Evidence};
            }
            if(_owner==0 || _follower.Owner!=_owner || !_follower.HasPath) {
                bool approach=FieldGeometrySnapshot.Horizontal(_follower.Position,_route.ApproachPoint)<=_follower.ArrivalRadius;
                return new FieldMovementOutcome{Outcome=approach?FieldOutcome.ApproachReached:FieldOutcome.Interrupted,Reason=approach?StopReason.Arrived:StopReason.NativeClear,Evidence=approach?"Native follower reached its final approach radius":"Native follower cleared before the approach"};
            }
            return new FieldMovementOutcome{Outcome=FieldOutcome.Moving};
        }
    }
}
