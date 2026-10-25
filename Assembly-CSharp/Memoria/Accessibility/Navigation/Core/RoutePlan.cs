namespace Memoria.Accessibility.Navigation
{
    public enum RouteFailure { None, GeometryUnavailable, StaleContext, StartOutsideMesh, TargetOutsideMesh, Disconnected, NarrowPassage, UnsafeSegment, TargetUnavailable, UnsupportedTransport, TerrainUnavailable, TraversalRejected, InvalidContext }
    public sealed class RoutePlan
    {
        public bool Success;public RouteFailure Failure;public TargetIdentity TargetId;public long Generation;public TransportIdentity Transport;public NavigationPoint ApproachPoint;
        public NavigationPoint[] Waypoints=new NavigationPoint[0];public int[] Corridor=new int[0];public string GeometryFingerprint;
        public float Length;
        public bool ViewingPoint;
        public TargetIdentity? PhysicalId;
        public TargetIdentity PhysicalIdentity {get{return PhysicalId??TargetId;}}
    }
}
