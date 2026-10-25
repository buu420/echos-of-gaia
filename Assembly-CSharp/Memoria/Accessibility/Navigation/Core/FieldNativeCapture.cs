namespace Memoria.Accessibility.Navigation
{
    // Populated only on a bounded diagnostic write, on the game thread. These are
    // public-field values, never Unity references or live collection wrappers.
    public sealed class FieldNativeCapture
    {
        public int Schema=1,Map,Camera,Frame,ActiveTriangle,LastTriangle,ActiveFloor,LastFloor;
        public int ControlUid,ActorUid,ActorSid,AttributeMask,BlockedAttributes;
        public NavigationPoint CurrentPosition,LastPosition,BgiOriginalPosition,BgiCurrentPosition;
        public FieldNativeCaptureTriangle[] Triangles;
        public FieldNativeCaptureFloor[] Floors;
    }
    public sealed class FieldNativeCaptureFloor
    {
        public int Index,Flags;public NavigationPoint OriginalPosition,CurrentPosition;
    }
    public sealed class FieldNativeCaptureTriangle
    {
        public int Index,RuntimeFloor,NativeFloor,RuntimeFlags,NativeFlags,NormalIndex,NormalInverseY,Plane,ThetaX,ThetaZ;
        public NavigationPoint[] OriginalVertices,RawVertices;
        public NavigationPoint Normal,NativeCenter;
        public int[] RuntimeNeighbors,NativeNeighbors,VertexIndices,EdgeIndices,EdgeClones,EdgeFlags;
    }
}
