using UnityEngine;
namespace Memoria.Accessibility.Navigation
{
    public static class NativeWorldMovement
    {
        private static readonly NativePort _port=new NativePort();
        private static readonly OwnedWorldTravel _owned=new OwnedWorldTravel(_port);
        private static NavigationTarget _target;private static RoutePlan _route;
        private static WorldMovementOutcome _outcome;
        public static NavigationPoint? CurrentWaypoint{get{return _owned.CurrentWaypoint;}}
        static NativeWorldMovement(){NavigationLifecycleBridge.Releasing+=ReleaseCurrent;}
        public static MovementStartResult TryStart(NavigationContext context,NavigationTarget target,RoutePlan route,long ownershipId)
        {
            if(ff9.w_moveActorPtr==null || ff9.w_moveCHRControlPtr==null)return MovementStartResult.Unavailable;
            var result=_owned.Start(context,target,route,ownershipId);
            if(result==MovementStartResult.Started){_target=target;_route=route;_outcome=new WorldMovementOutcome{Outcome=WorldOutcome.Moving};}
            return result;
        }
        public static void Release(long ownershipId,StopReason reason){_owned.Release(ownershipId,reason);}
        private static void ReleaseCurrent(StopReason reason){Release(_owned.Owner,reason);}
        public static WorldMovementOutcome ReadOutcome(NavigationContext context,NavigationTarget target){return _outcome??new WorldMovementOutcome{Outcome=WorldOutcome.Unavailable};}
        public static void BeforeMovement()
        {
            NativeNavigationInput.EnsureUpdated();if(_owned.Owner==0)return;
            var context=NativeNavigationContext.Read();
            if(!context.CanControl || ff9.w_frameMenuon || ff9.w_naviMode==2 || !ff9.w_blockReady || ff9.w_cameraInter) {ReleaseCurrent(StopReason.UnknownContext);_outcome=new WorldMovementOutcome{Outcome=WorldOutcome.Interrupted,Reason=StopReason.UnknownContext};return;}
            if(_route.GeometryFingerprint!=NativeWorldGeometry.CurrentFingerprint()){ReleaseCurrent(StopReason.PathInvalid);_outcome=new WorldMovementOutcome{Outcome=WorldOutcome.Interrupted,Reason=StopReason.PathInvalid,Evidence="World terrain form changed"};return;}
            NavigationTarget fresh=_target;
            if(_target.PhysicalIdentity.Kind==TargetKind.Vehicle) {
                var engine=Object.FindObjectOfType<EventEngine>();var actor=engine==null?null:engine.GetObjUID(_target.PhysicalIdentity.NativeId) as Actor;
                if(actor==null || actor.wmActor==null){ReleaseCurrent(StopReason.TargetLost);_outcome=new WorldMovementOutcome{Outcome=WorldOutcome.Unavailable,Reason=StopReason.TargetLost};return;}
                var p=actor.wmActor.RealPosition;fresh=new NavigationTarget{Id=_target.Id,PhysicalId=_target.PhysicalId,Generation=context.Generation,Position=new NavigationPoint(p.x,p.y,p.z)};
            }else if(_target.PhysicalIdentity.Kind==TargetKind.WorldLocation && !WMUIData.LocationAvailable(_target.NativeLocationId)) {ReleaseCurrent(StopReason.TargetLost);_outcome=new WorldMovementOutcome{Outcome=WorldOutcome.Unavailable,Reason=StopReason.TargetLost};return;}
            _outcome=_owned.Tick(context,fresh);
            if(_outcome.Outcome==WorldOutcome.Moving && !_owned.NativeTown)ValidateNextNativeStep(context,fresh);
        }
        private static void ValidateNextNativeStep(NavigationContext context,NavigationTarget target)
        {
            WorldSteeringCommand command;if(!WorldMovementOverlay.TryRead(out command))return;
            var geometry=NativeWorldGeometry.Read(context,target);var control=ff9.w_moveCHRControlPtr;
            if(geometry==null || !geometry.Complete){RejectStep();return;}
            float heading;
            if(control.type==0)heading=(-ff9.ratan2(-command.LY,command.LX)+90)+ff9.w_cameraSysDataCamera.rotation;
            else heading=ff9.w_moveActorPtr.rot[1]+ff9.PsxRot(control.speed_rotation*command.LX>>7);
            float speed=control.type==0?Mathf.Abs(ff9.S(control.speed_move)):Mathf.Max(Mathf.Abs(ff9.w_moveCHRControl_XZSpeed),Mathf.Abs(ff9.S(control.speed_move)))*(ff9.w_moveDoping?2:1);
            var next=WorldCoordinates.Canonical(new NavigationPoint(context.Position.X+ff9.rsin(heading+180)*speed,context.Position.Y,context.Position.Z+ff9.rcos(heading+180)*speed));
            var cache=new PrivateWorldCache(geometry.NativeCache);var hit=geometry.Probe(context.Position,geometry.Rules.CurrentTerrainId,cache);WorldSurface end;
            int startCell=WorldGeometrySnapshot.Cell(context.Position),goalCell=target.PhysicalIdentity.Kind==TargetKind.WorldLocation?WorldGeometrySnapshot.Cell(_route.ApproachPoint):-1;
            if(!geometry.Segment(context.Position,next,hit,cache,startCell,goalCell,out end))RejectStep();
        }
        private static void RejectStep(){ReleaseCurrent(StopReason.PathInvalid);_outcome=new WorldMovementOutcome{Outcome=WorldOutcome.Interrupted,Reason=StopReason.PathInvalid,Evidence="Next native movement step does not pass the current terrain probe"};}
        private sealed class NativePort : IWorldMovementPort
        {
            private long _autopilotOwner;private int _location,_map;
            public bool StartTown(int location,long owner)
            {
                var context=NativeNavigationContext.Read();
                if(!context.CanControl || !context.Transport.IsAirship || !WMUIData.LocationAvailable(location) || ff9.w_moveAutoPilot!=0 || ff9.w_naviMapno!=WMUIData.ActiveMapNo)return false;
                _location=location;_map=ff9.w_naviMapno;_autopilotOwner=owner;
                ff9.w_frameAutoid=location;ff9.w_movementAutoPilotON();return true;
            }
            public void StopTown(long owner)
            {
                if(owner!=_autopilotOwner || owner==0)return;
                if(ff9.w_frameAutoid==_location && ff9.w_naviMapno==_map)ff9.w_movementAutoPilotOFF();
                _autopilotOwner=0;
            }
            public void SetMovement(NavigationContext context,WorldSteeringCommand command,long owner){WorldMovementOverlay.Set(context,command,owner);}
            public void ClearMovement(long owner){WorldMovementOverlay.Clear(owner);}
            public WorldMovementState Read(NavigationContext context,NavigationTarget target)
            {
                return new WorldMovementState{CameraDegrees=ff9.w_cameraSysDataCamera.rotation,HeadingDegrees=ff9.w_moveActorPtr.rot[1],ControlType=ff9.w_moveCHRControlPtr.type,ArrivalRadius=context.Transport.IsAirship && target.PhysicalIdentity.Kind==TargetKind.WorldLocation?ff9.S(50)*32:0.5f,PresentedInteractionUid=EIcon.NavigationPresentedUid,NativeAutopilotActive=_autopilotOwner!=0 && ff9.w_moveAutoPilot!=0 && ff9.w_frameAutoid==_location && ff9.w_naviMapno==_map};
            }
        }
    }
}
