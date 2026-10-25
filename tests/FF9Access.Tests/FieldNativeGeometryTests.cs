using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class FieldNativeGeometryTests
    {
        public static void SupportAddsPositiveFloorYWhileOrdinaryRadiusRetainsCachedOriginals()
        {
            var input=Data(new[]{P(0,-20,0),P(100,-20,0),P(0,-20,100)});
            input.FloorDelta=P(30,7,40);
            var triangle=FieldNativeGeometryContract.CreateTriangle(0,0,611,input);
            TestAssert.Near(-13,triangle.Height(P(20,0,20)),0.001f);
            TestAssert.Equal(P(0,-20,0),triangle.RadiusVertices[0]);
            TestAssert.Equal(P(0,-13,0),triangle.Vertices[0]);
            var geometry=new FieldGeometrySnapshot{Triangles=new[]{triangle},Radius=5};
            TestAssert.True(geometry.IsSegmentClear(P(30,-13,1),P(40,-13,1)));
        }
        public static void AirGardenRadiusAddsAllAxesAfterOriginalYInversion()
        {
            var input=Data(new[]{P(0,-20,0),P(100,-20,0),P(0,-20,100)});input.FloorDelta=P(30,7,40);
            var triangle=FieldNativeGeometryContract.CreateTriangle(0,0,2952,input);
            TestAssert.Equal(P(30,-13,40),triangle.RadiusVertices[0]);
            TestAssert.Equal(P(0,-13,0),triangle.Vertices[0]);
        }
        public static void TraversalRequiresMatchingNeighborsValidCloneAndBothContinuities()
        {
            var geometry=Pair();var first=geometry.Triangles[0];NavigationPoint portal;
            TestAssert.True(geometry.TryPortalPoint(first,1,out portal));
            first.Native.NativeNeighbors[1]=-1;TestAssert.False(geometry.TryPortalPoint(first,1,out portal));
            TestAssert.False(geometry.IsNativeOuterBoundary(first,1));
            first.Native.NativeNeighbors[1]=1;first.Native.EdgeClones[1]=-1;TestAssert.False(geometry.TryPortalPoint(first,1,out portal));
            first.Native.EdgeClones[1]=2;TestAssert.False(geometry.TryPortalPoint(first,1,out portal));
            first.Native.EdgeClones[1]=0;first.Native.CrossingVertices[1]=P(101,0,0);TestAssert.False(geometry.TryPortalPoint(first,1,out portal));
            first.Native.CrossingVertices[1]=P(100,0,0);first.Native.Projection=1;TestAssert.False(geometry.TryPortalPoint(first,1,out portal));
            TestAssert.True(geometry.IsNativeOuterBoundary(first,0));
            first.Native.NativeNeighbors[0]=1;TestAssert.False(geometry.IsNativeOuterBoundary(first,0));
        }
        public static void FingerprintChangesForIndependentPermissionAndGeometryInputs()
        {
            var geometry=Pair();var triangle=geometry.Triangles[0];
            string previous=FieldNativeGeometryContract.Fingerprint("611",geometry);
            triangle.Neighbors[1]=-1;string changed=FieldNativeGeometryContract.Fingerprint("611",geometry);TestAssert.False(previous==changed);previous=changed;
            triangle.Native.NativeNeighbors[1]=-1;changed=FieldNativeGeometryContract.Fingerprint("611",geometry);TestAssert.False(previous==changed);previous=changed;
            triangle.Native.EdgeClones[1]=-1;changed=FieldNativeGeometryContract.Fingerprint("611",geometry);TestAssert.False(previous==changed);previous=changed;
            triangle.Native.FloorDelta=P(0,1,0);changed=FieldNativeGeometryContract.Fingerprint("611",geometry);TestAssert.False(previous==changed);previous=changed;
            triangle.Native.OriginalVertices[0]=P(0,1,0);changed=FieldNativeGeometryContract.Fingerprint("611",geometry);TestAssert.False(previous==changed);previous=changed;
            triangle.Native.Normal=P(1,2,3);changed=FieldNativeGeometryContract.Fingerprint("611",geometry);TestAssert.False(previous==changed);previous=changed;
            geometry.AttributeMask=0x80;changed=FieldNativeGeometryContract.Fingerprint("611",geometry);TestAssert.False(previous==changed);
        }
        public static void UnrelatedFloorBoundaryDoesNotContaminateLocalRadius()
        {
            var geometry=Pair();geometry.Radius=5;
            var other=new FieldTriangle{Id=2,Floor=9,Vertices=new[]{P(25,0,30),P(40,0,30),P(25,0,40)}};
            geometry.Triangles=new[]{geometry.Triangles[0],geometry.Triangles[1],other};
            TestAssert.True(geometry.IsSegmentClear(P(20,0,30),P(40,0,30)));
        }
        public static void MalformedNativeContractIsUnavailable()
        {
            var data=Data(new[]{P(0,0,0),P(100,0,0),P(0,0,100)});data.RuntimeNeighbors=new[]{-1};
            TestAssert.Equal<FieldTriangle>(null,FieldNativeGeometryContract.CreateTriangle(0,0,611,data));
        }
        public static void RadiusRecursesWithoutSupportContinuityOrRuntimeEnabledSelection()
        {
            // The player is within radius of edge 0. Native permission follows the
            // runtime neighbor to its BGI flags, even if that support is displaced.
            var a=Data(new[]{P(0,0,0),P(100,0,0),P(0,0,100)});a.RuntimeNeighbors[0]=1;
            // BGI adjacency and clone stay -1: neither participates in radius permission.
            var b=Data(new[]{P(1000,50,1000),P(1100,50,1000),P(1000,50,1100)});b.RuntimeFlags=0;
            var first=FieldNativeGeometryContract.CreateTriangle(0,0,611,a);
            var other=FieldNativeGeometryContract.CreateTriangle(1,1,611,b);
            var geometry=new FieldGeometrySnapshot{Triangles=new[]{first,other},Radius=10,ActiveTriangle=0};
            TestAssert.True(geometry.IsSegmentClear(P(30,0,1),P(40,0,1)));
            other.Enabled=false;
            TestAssert.False(geometry.IsSegmentClear(P(30,0,1),P(40,0,1)));
            other.Enabled=true;other.Attributes=0x80;geometry.AttributeMask=0x80;
            TestAssert.False(geometry.IsSegmentClear(P(30,0,1),P(40,0,1)));
            geometry.BlockedAttributes=0x40;
            TestAssert.True(geometry.IsSegmentClear(P(30,0,1),P(40,0,1)));
        }
        public static void CrossingProjectionUsesNativeFixedPointThresholds()
        {
            TestAssert.Equal(0,FieldNativeGeometryContract.Projection(-1,P(100,0,0)));
            TestAssert.Equal(0,FieldNativeGeometryContract.Projection(0,P(100,20725,0)));
            TestAssert.Equal(1,FieldNativeGeometryContract.Projection(0,P(100,20724,0)));
            TestAssert.Equal(2,FieldNativeGeometryContract.Projection(0,P(0,0,100)));
        }
        private static NavigationPoint P(float x,float y,float z){return new NavigationPoint(x,y,z);}
        private static FieldNativeTriangleData Data(NavigationPoint[] vertices)
        {
            return new FieldNativeTriangleData{OriginalVertices=vertices,CrossingVertices=new[]{P(vertices[0].X,-vertices[0].Y,vertices[0].Z),P(vertices[1].X,-vertices[1].Y,vertices[1].Z),P(vertices[2].X,-vertices[2].Y,vertices[2].Z)},RuntimeNeighbors=new[]{-1,-1,-1},NativeNeighbors=new[]{-1,-1,-1},EdgeClones=new[]{-1,-1,-1},RuntimeFlags=1,NativeFlags=1};
        }
        private static FieldGeometrySnapshot Pair()
        {
            var a=Data(new[]{P(0,0,0),P(100,0,0),P(0,0,100)});var b=Data(new[]{P(100,0,0),P(100,0,100),P(0,0,100)});
            a.RuntimeNeighbors[1]=a.NativeNeighbors[1]=1;a.EdgeClones[1]=0;
            b.RuntimeNeighbors[2]=b.NativeNeighbors[2]=0;b.EdgeClones[2]=2;
            return new FieldGeometrySnapshot{Triangles=new[]{FieldNativeGeometryContract.CreateTriangle(0,0,611,a),FieldNativeGeometryContract.CreateTriangle(1,0,611,b)},Radius=0,ActiveTriangle=0};
        }
    }
}
