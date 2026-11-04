using UnityEngine;
namespace Memoria.Accessibility.Navigation
{
    public static class NativeFieldMovement
    {
        private static OwnedFieldFollower _owned;
        private static NavigationTarget _target;
        private static long _generation;
        private static FieldMovementOutcome _interruption;
        private static RoutePlan _route;
        public static event System.Action ExitReached;
        public static NavigationPoint? CurrentWaypoint()
        {
            if(_owned==null || _owned.Owner==0)return null;
            var engine=Object.FindObjectOfType<EventEngine>();var actor=engine==null?null:engine.GetControlChar() as Actor;
            var controller=actor==null?null:actor.fieldMapActorController;
            if(controller==null || controller.AccessibilityPathOwner!=_owned.Owner)return null;
            var p=controller.hasTarget?controller.moveTarget:controller.movePaths.Count>0?controller.movePaths[controller.movePaths.Count-1]:controller.curPos;
            return new NavigationPoint(p.x,p.y,p.z);
        }
        static NativeFieldMovement() { NavigationLifecycleBridge.Releasing += ReleaseCurrent; }
        public static MovementStartResult TryStart(NavigationContext context, NavigationTarget target, RoutePlan route, long ownershipId)
        {
            if (context == null || target == null || !context.CanControl) return MovementStartResult.InvalidContext;
            var engine = Object.FindObjectOfType<EventEngine>();
            var player = engine == null ? null : engine.GetControlChar() as Actor;
            if (player == null || player.uid != context.PlayerIdentity || player.fieldMapActorController == null || !player.fieldMapActorController.isPlayer) return MovementStartResult.Unavailable;
            ReleaseCurrent(StopReason.Explicit);
            var owned = new OwnedFieldFollower(new NativeFollower(player.fieldMapActorController, context.Generation));
            var result = owned.Start(context, target, route, ownershipId);
            if (result == MovementStartResult.Started) { _owned = owned; _target = target; _route=route;_generation = context.Generation; _interruption = null; }
            return result;
        }
        public static void Release(long ownershipId, StopReason reason) { if (_owned != null) _owned.Release(ownershipId, reason); }
        private static void ReleaseCurrent(StopReason reason) { if (_owned != null) _owned.Release(_owned.Owner, reason); }
        public static FieldMovementOutcome ReadOutcome(NavigationContext context, NavigationTarget target)
        {
            if (_interruption != null) { var result = _interruption; _interruption = null; return result; }
            if (_owned == null) return new FieldMovementOutcome { Outcome = FieldOutcome.Unavailable };
            var outcome = _owned.Read(context, target, NativeInteractionProbe.Read(context, target));
            if (outcome.Outcome != FieldOutcome.Moving) ReleaseCurrent(outcome.Reason == StopReason.None ? StopReason.Arrived : outcome.Reason);
            return outcome;
        }
        public static void CheckLiveGate(FieldMapActorController controller)
        {
            if (controller.AccessibilityPathOwner == 0) return;
            var context = NativeNavigationContext.Read();
            if (!context.CanControl || !context.Foreground || _generation != context.Generation || controller.originalActor.uid != context.PlayerIdentity || controller.originalActor.state != EventEngine.stateRunning)
                ReleaseCurrent(StopReason.UnknownContext);
            else {
                var geometry=NativeFieldGeometry.Read(context);
                if(geometry==null || _route==null || geometry.Fingerprint!=_route.GeometryFingerprint){ReleaseCurrent(StopReason.PathInvalid);_interruption=new FieldMovementOutcome{Outcome=FieldOutcome.Interrupted,Reason=StopReason.PathInvalid,Evidence="Current field walkmesh changed"};}
            }
        }
        public static void OnManualClick(FieldMapActorController controller)
        {
            if (controller.AccessibilityPathOwner != 0) {
                ReleaseCurrent(StopReason.ManualMovement);
                _interruption = new FieldMovementOutcome { Outcome = FieldOutcome.Interrupted, Reason = StopReason.ManualMovement, Evidence = "Player selected a native mouse movement destination" };
            }
        }
        public static void OnNativeClear(FieldMapActorController controller){OnNativeClear(controller,FieldPathClearCause.Unknown);}
        public static void OnNativeClear(FieldMapActorController controller,FieldPathClearCause cause)
        {
            if (controller.AccessibilityPathOwner != 0) {
                FieldMovementCapture state=null;
                try { state=CaptureMovement(controller,cause); }
                catch(System.Exception){/* The native stop must survive a failed observation. */}
                var reason=cause==FieldPathClearCause.StillFrames?StopReason.Stalled:StopReason.NativeClear;
                ReleaseCurrent(reason);
                _interruption = new FieldMovementOutcome { Outcome = FieldOutcome.Interrupted, Reason = reason, Evidence = "Native path clear: "+cause };
                // Disk/geometry diagnostics must never delay ownership release or
                // throw past the game's existing cancellation operation.
                try { Memoria.Prime.Log.Message("[FF9Access] Owned field path cleared: {0}; owner={1}; still={2}",cause,state==null?0:state.Owner,controller.stillCount); }
                catch(System.Exception){/* The independent snapshot may still succeed. */}
                try { if(state!=null)NativeFieldGeometry.CaptureInterruption(NativeNavigationContext.Read(),_target,_route,state); }
                catch(System.Exception){/* Optional logging cannot prevent native cancellation. */}
            }
        }
        private static FieldMovementCapture CaptureMovement(FieldMapActorController controller,FieldPathClearCause cause)
        {
            var state=new FieldMovementCapture{Cause=cause,Owner=controller.AccessibilityPathOwner,HasTarget=controller.hasTarget,StillCount=controller.stillCount,
                RemainingWaypoints=controller.movePaths.Count,Position=Point(controller.curPos),LastPosition=Point(controller.lastPos),
                Waypoint=Point(controller.hasTarget?controller.moveTarget:controller.movePaths.Count>0?controller.movePaths[controller.movePaths.Count-1]:controller.curPos),
                ActiveTriangle=controller.activeTri,LastTriangle=controller.lastTri,Speed=controller.speed,BodyRadius=controller.radius,NativeActorSpeed=controller.originalActor.speed,Running=controller.originalActor.speed==60};
            var previous=controller.originalActor.coll;
            if(previous!=null){state.PreviousCollisionUid=previous.uid;state.PreviousCollisionPosition=new NavigationPoint(previous.pos[0],previous.pos[1],previous.pos[2]);state.PreviousCollisionRadius=4*previous.collRad;state.PreviousCollisionDistance=controller.originalActor.colldist;}
            if(controller.walkMesh!=null) {
                try {
                    float distance;var collision=controller.walkMesh.Collision(controller,0,out distance);
                    state.CollisionProbeAvailable=true;
                    if(collision!=null){state.CollisionUid=collision.uid;state.CollisionPosition=new NavigationPoint(collision.pos[0],collision.pos[1],collision.pos[2]);state.CollisionRadius=4*collision.collRad;state.CollisionDistance=distance;}
                }catch(System.Exception){/* Keep the waypoint even if collision observation fails. */}
            }
            return state;
        }
        private static NavigationPoint Point(Vector3 value){return new NavigationPoint(value.x,value.y,value.z);}
        public static void NotifyExitTransition(int nativeExecutingUid) { if (_owned != null) {bool previous=_owned.ExitConfirmed;_owned.ObserveExit(nativeExecutingUid);if(!previous && _owned.ExitConfirmed && ExitReached!=null)ExitReached();} }
        private sealed class NativeFollower : IFieldFollower
        {
            private readonly FieldMapActorController _controller;
            private readonly long _generation;
            public NativeFollower(FieldMapActorController controller, long generation) { _controller = controller; _generation = generation; }
            public long Generation { get { return _generation; } }
            public int PlayerIdentity { get { return _controller == null ? -1 : _controller.originalActor.uid; } }
            public long Owner { get { return _controller == null ? 0 : _controller.AccessibilityPathOwner; } }
            public bool HasPath { get { return _controller != null && (_controller.hasTarget || _controller.movePaths.Count > 0); } }
            public NavigationPoint Position { get { return _controller == null ? new NavigationPoint(float.NaN, 0, 0) : Point(_controller.curPos); } }
            public float ArrivalRadius { get { return _controller == null ? 0 : FieldAnalogMovement.ArrivalRadius(_controller.radius,_controller.AccessibilityPathOwner); } }
            public bool Install(long owner, NavigationPoint[] reversed)
            {
                if (_controller == null || HasPath || _controller.AccessibilityPathOwner != 0 || _generation != NavigationLifecycleBridge.Generation) return false;
                _controller.ClearMoveTarget();
                _controller.movePaths.Clear();
                foreach (var point in reversed) _controller.movePaths.Add(new Vector3(point.X, point.Y, point.Z));
                _controller.totalPathCount = reversed.Length;
                _controller.totalPathLengthSq = 0;
                Vector3 previous = _controller.curPos;
                for (int i = reversed.Length - 1; i >= 0; i--) { Vector3 next = _controller.movePaths[i]; _controller.totalPathLengthSq += (previous - next).sqrMagnitude; previous = next; }
                _controller.stillCount = 0; _controller.stillPos = _controller.curPos;
                _controller.AccessibilityPathOwner = owner;
                return true;
            }
            public void Clear(long owner)
            {
                if (_controller == null || _controller.AccessibilityPathOwner != owner) return;
                _controller.movePaths.Clear(); _controller.ClearMoveTarget();
                _controller.totalPathCount = 0; _controller.totalPathLengthSq = 0;
                _controller.AccessibilityPathOwner = 0;
            }
            private static NavigationPoint Point(Vector3 value) { return new NavigationPoint(value.x, value.y, value.z); }
        }
    }
}
