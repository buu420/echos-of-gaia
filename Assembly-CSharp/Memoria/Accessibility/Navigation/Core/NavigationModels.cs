using System;
namespace Memoria.Accessibility.Navigation
{
    public struct NavigationPoint
    {
        public readonly float X, Y, Z;
        public NavigationPoint(float x, float y, float z) { X = x; Y = y; Z = z; }
        public bool IsFinite { get { return !(float.IsNaN(X) || float.IsNaN(Y) || float.IsNaN(Z) || float.IsInfinity(X) || float.IsInfinity(Y) || float.IsInfinity(Z)); } }
        public static float Distance(NavigationPoint a, NavigationPoint b) { float x = a.X-b.X, y = a.Y-b.Y, z = a.Z-b.Z; return (float)Math.Sqrt(x*x+y*y+z*z); }
    }
    public enum TargetKind { FieldActor, FieldExit, WorldLocation, RegionPoint, Vehicle, Story }
    public struct TargetIdentity : IEquatable<TargetIdentity>
    {
        public readonly TargetKind Kind;
        public readonly string MapKey;
        public readonly int NativeId;
        public TargetIdentity(TargetKind kind, string mapKey, int nativeId) { Kind = kind; MapKey = mapKey ?? ""; NativeId = nativeId; }
        public bool Equals(TargetIdentity other) { return Kind == other.Kind && NativeId == other.NativeId && MapKey == other.MapKey; }
        public override bool Equals(object other) { return other is TargetIdentity && Equals((TargetIdentity)other); }
        public override int GetHashCode() { return ((int)Kind * 397 ^ NativeId) * 397 ^ (MapKey ?? "").GetHashCode(); }
        public override string ToString() { return Kind + ":" + MapKey + ":" + NativeId; }
    }
    public enum TransportKind { Unknown, Foot, Chocobo, Boat, HildaGarde, Invincible }
    public struct TransportIdentity : IEquatable<TransportIdentity>
    {
        public readonly TransportKind Kind;
        public readonly int ActorId, NativeStatus;
        public TransportIdentity(TransportKind kind, int actorId, int nativeStatus) { Kind = kind; ActorId = actorId; NativeStatus = nativeStatus; }
        public bool IsAirship { get { return (Kind == TransportKind.HildaGarde || Kind == TransportKind.Invincible) && (NativeStatus == 9 || NativeStatus == 10); } }
        public bool Equals(TransportIdentity other) { return Kind == other.Kind && ActorId == other.ActorId && NativeStatus == other.NativeStatus; }
        public override bool Equals(object other) { return other is TransportIdentity && Equals((TransportIdentity)other); }
        public override int GetHashCode() { return ((int)Kind * 397 ^ ActorId) * 397 ^ NativeStatus; }
    }
    public enum GateReason { None, UnknownActor, NoControl, Menu, Dialogue, Battle, Background, Disconnected, UnknownInput, MapTransition }
    public enum BattleTransition { None, RandomStarted, ScriptedStarted, RandomReturned, UncertainReturn }
    public enum RouteMode { Idle, Guiding, Moving, Paused, Arrived, Unavailable }
    public enum StopReason { None, Explicit, ManualMovement, Confirm, Menu, Dialogue, FocusLost, Disconnected, UnknownContext, MapChanged, TransportChanged, TargetChanged, TargetLost, Battle, PathInvalid, Stalled, Arrived, NativeClear, SaveChanged }
    public enum NavigationSignal { None, StartGuidance, ToggleGuidance, StartAutomatic, Stop, ManualMovement, Confirm, OpenMenu, TargetChanged, TargetLost, SceneDetach, Arrived, RouteFailed }
    public sealed class NavigationContext
    {
        public long Generation;
        public string LogicalMapKey, SaveIdentity;
        public int PlayerIdentity = -1;
        public NavigationPoint Position;
        public TransportIdentity Transport;
        public bool CanControl, Foreground, CanInspect;
        public bool MinigameInspection;
        public string CurrentLocation, CurrentRegion;
        public int StoryProgress=-1;
        public float InputOrientationDegrees;
        public int WorldControlType=-1;
        public GateReason GateReason;
        public BattleTransition BattleTransition;
    }
    public sealed class RouteDecision
    {
        public RouteMode Mode;
        public bool IsAutomatic, ReleaseMovement, RequireReplan;
        public StopReason Reason;
    }
}
