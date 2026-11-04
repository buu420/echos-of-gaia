namespace Memoria.Accessibility.Navigation
{
    public enum FieldPathClearCause { Unknown,StillFrames,PhysicalMovement,Interaction,ScriptedControl }

    // Detached diagnostic state, sampled only when native code clears our path.
    // A previous collision can be stale; keep it distinct from a current probe.
    public sealed class FieldMovementCapture
    {
        public FieldPathClearCause Cause;
        public long Owner;
        public bool HasTarget,Running,CollisionProbeAvailable;
        public int StillCount,RemainingWaypoints,ActiveTriangle,LastTriangle,NativeActorSpeed;
        public NavigationPoint Position,LastPosition,Waypoint;
        public float Speed,BodyRadius;
        public int CollisionUid=-1,PreviousCollisionUid=-1;
        public NavigationPoint CollisionPosition,PreviousCollisionPosition;
        public float CollisionRadius,CollisionDistance,PreviousCollisionRadius,PreviousCollisionDistance;
    }
}
