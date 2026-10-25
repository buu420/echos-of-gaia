using System;
namespace Memoria.Accessibility.Navigation
{
    public struct WorldSteeringCommand {public int LX,LY,Throttle;}
    public enum WorldOutcome {Moving,ApproachReached,Arrived,Interrupted,Unavailable,TargetMoved}
    public sealed class WorldMovementOutcome {public WorldOutcome Outcome;public StopReason Reason;public string Evidence;}
    public sealed class WorldMovementState {public float CameraDegrees,HeadingDegrees,ArrivalRadius=0.5f;public int ControlType,PresentedInteractionUid=-1;public bool NativeAutopilotActive;}
    public interface IWorldMovementPort
    {
        bool StartTown(int nativeLocationId,long owner);void StopTown(long owner);
        void SetMovement(NavigationContext context,WorldSteeringCommand command,long owner);void ClearMovement(long owner);
        WorldMovementState Read(NavigationContext context,NavigationTarget target);
    }
    public sealed class MovementOverlayState
    {
        private long _owner,_generation;private int _player;private string _map,_save;private TransportIdentity _transport;private WorldSteeringCommand _command;
        public void Set(NavigationContext context,WorldSteeringCommand command,long owner) {_owner=owner;_generation=context.Generation;_player=context.PlayerIdentity;_map=context.LogicalMapKey;_save=context.SaveIdentity;_transport=context.Transport;_command=command;}
        public void Clear(long owner){if(_owner==owner)_owner=0;}
        public bool TryRead(NavigationContext context,out WorldSteeringCommand command)
        {
            command=new WorldSteeringCommand();if(_owner==0 || context==null || !context.CanControl || !context.Foreground || context.Generation!=_generation || context.PlayerIdentity!=_player || context.LogicalMapKey!=_map || context.SaveIdentity!=_save || !context.Transport.Equals(_transport))return false;
            command=_command;return true;
        }
    }
    public static class WorldSteering
    {
        public static float Angle(float value){value%=360;if(value>180)value-=360;if(value< -180)value+=360;return value;}
        public static WorldSteeringCommand For(NavigationContext context,NavigationPoint point,WorldMovementState state)
        {
            var delta=WorldCoordinates.Difference(context.Position,point);
            float heading=(float)(Math.Atan2(delta.X,delta.Z)*180/Math.PI)-180;
            if(state.ControlType==0) {
                double local=(heading-state.CameraDegrees)*Math.PI/180;
                return new WorldSteeringCommand{LX=(int)Math.Round(Math.Sin(local)*128),LY=-(int)Math.Round(Math.Cos(local)*128)};
            }
            float error=Angle(heading-state.HeadingDegrees);
            return new WorldSteeringCommand{LX=(int)Math.Max(-128,Math.Min(128,error*4)),LY=0,Throttle=Math.Abs(error)>3?0:48};
        }
    }
    public sealed class OwnedWorldTravel
    {
        private readonly IWorldMovementPort _port;private long _owner;private RoutePlan _route;private NavigationPoint _originalTarget,_lastPosition;
        private bool _nativeTown;private int _waypoint,_stalled;private StopReason _lastStop;
        public long Owner {get{return _owner;}}public bool NativeTown {get{return _nativeTown;}}
        public NavigationPoint? CurrentWaypoint{get{return _owner!=0 && _route!=null?_route.Waypoints[_waypoint]:(NavigationPoint?)null;}}
        public OwnedWorldTravel(IWorldMovementPort port){_port=port;}
        public MovementStartResult Start(NavigationContext context,NavigationTarget target,RoutePlan route,long owner)
        {
            if(owner==0 || !context.CanControl || !context.Foreground)return MovementStartResult.InvalidContext;
            if(route==null || !route.Success || route.Waypoints.Length==0)return MovementStartResult.Unavailable;
            if(route.Generation!=context.Generation || target.Generation!=context.Generation || !route.TargetId.Equals(target.Id) || !route.Transport.Equals(context.Transport))return MovementStartResult.StaleRoute;
            Release(_owner,StopReason.Explicit);
            bool town=context.Transport.IsAirship && target.PhysicalIdentity.Kind==TargetKind.WorldLocation && target.NativeLocationId>=0;
            if(town && !_port.StartTown(target.NativeLocationId,owner))return MovementStartResult.Busy;
            _owner=owner;_route=route;_nativeTown=town;_originalTarget=target.Position;_lastPosition=context.Position;_waypoint=_stalled=0;_lastStop=StopReason.None;return MovementStartResult.Started;
        }
        public void Release(long owner,StopReason reason)
        {
            if(owner==0 || owner!=_owner)return;
            if(_nativeTown)_port.StopTown(owner);_port.ClearMovement(owner);_owner=0;_lastStop=reason;
        }
        public WorldMovementOutcome Tick(NavigationContext context,NavigationTarget target)
        {
            if(_owner==0)return new WorldMovementOutcome{Outcome=WorldOutcome.Interrupted,Reason=_lastStop};
            if(context==null || !context.CanControl || !context.Foreground || _route.Generation!=context.Generation || !_route.Transport.Equals(context.Transport) || target==null || !_route.TargetId.Equals(target.Id))return Stop(WorldOutcome.Interrupted,StopReason.UnknownContext,"World control context changed");
            var state=_port.Read(context,target);
            if(target.PhysicalIdentity.Kind==TargetKind.Vehicle && WorldCoordinates.Distance(_originalTarget,target.Position)>Math.Max(0.5f,state.ArrivalRadius/2))return Stop(WorldOutcome.TargetMoved,StopReason.TargetChanged,"Vehicle moved; a fresh approach route is required");
            if(target.PhysicalIdentity.Kind==TargetKind.Vehicle && state.PresentedInteractionUid==target.PhysicalIdentity.NativeId)return Stop(WorldOutcome.ApproachReached,StopReason.Arrived,"Selected vehicle interaction is presented; use the ordinary game action to board");
            float remaining=WorldCoordinates.Distance(context.Position,_route.ApproachPoint);
            if(remaining<=state.ArrivalRadius) return Stop(_nativeTown?WorldOutcome.Arrived:WorldOutcome.ApproachReached,StopReason.Arrived,"Reachable approach reached; use the ordinary game action to enter, board or land");
            if(_nativeTown) {
                if(!state.NativeAutopilotActive)return Stop(WorldOutcome.Interrupted,StopReason.NativeClear,"Native town autopilot stopped before its destination");
                return new WorldMovementOutcome{Outcome=WorldOutcome.Moving};
            }
            if(WorldCoordinates.Distance(context.Position,_lastPosition)<0.02f)_stalled++;else{_lastPosition=context.Position;_stalled=0;}
            if(_stalled>180)return Stop(WorldOutcome.Interrupted,StopReason.Stalled,"Native movement made no progress");
            while(_waypoint<_route.Waypoints.Length-1 && WorldCoordinates.Distance(context.Position,_route.Waypoints[_waypoint])<=0.5f)_waypoint++;
            _port.SetMovement(context,WorldSteering.For(context,_route.Waypoints[_waypoint],state),_owner);
            return new WorldMovementOutcome{Outcome=WorldOutcome.Moving};
        }
        private WorldMovementOutcome Stop(WorldOutcome outcome,StopReason reason,string evidence){Release(_owner,reason);return new WorldMovementOutcome{Outcome=outcome,Reason=reason,Evidence=evidence};}
    }
}
