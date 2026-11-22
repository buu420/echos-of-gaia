using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class FieldReportedRouteTests
    {
        private sealed class PointConverter:JsonConverter<NavigationPoint>
        {
            public override NavigationPoint Read(ref Utf8JsonReader reader,Type type,JsonSerializerOptions options)
            {using(var value=JsonDocument.ParseValue(ref reader)){var p=value.RootElement;return new NavigationPoint(p.GetProperty("X").GetSingle(),p.GetProperty("Y").GetSingle(),p.GetProperty("Z").GetSingle());}}
            public override void Write(Utf8JsonWriter writer,NavigationPoint value,JsonSerializerOptions options){throw new NotSupportedException();}
        }
        private static FieldGeometrySnapshot Load(int map,float radius)
        {
            var root=new DirectoryInfo(Environment.CurrentDirectory);
            while(root!=null&&!Directory.Exists(Path.Combine(root.FullName,"Assembly-CSharp")))root=root.Parent;
            if(root==null)throw new Exception("Run route regressions from the source checkout");
            using(var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(root.FullName,"tests/FF9Access.Tests/Fixtures/native-route-geometry.json")))) {
                var record=json.RootElement.GetProperty("Maps").EnumerateArray().Single(m=>m.GetProperty("Map").GetInt32()==map);
                TestAssert.Equal(64,record.GetProperty("MeshSHA256").GetString().Length);
                var options=new JsonSerializerOptions{IncludeFields=true};options.Converters.Add(new PointConverter());
                var geometry=JsonSerializer.Deserialize<FieldGeometrySnapshot>(record.GetProperty("Geometry").GetRawText(),options);
                geometry.Radius=radius;
                geometry.Triangles=geometry.Triangles.Select(t=>FieldNativeGeometryContract.CreateTriangle(t.Id,t.Floor,map,t.Native)).ToArray();
                return geometry;
            }
        }
        private static void AssertRoute(int map,float radius,NavigationPoint start,int uid,params uint[] packed)
        {
            var geometry=Load(map,radius);
            var support=geometry.At(start);TestAssert.True(support!=null);
            start=new NavigationPoint(start.X,support.Height(start),start.Z);geometry.ActiveTriangle=support.Id;
            var context=TestFixtures.Context("field:"+map);context.Position=start;
            var polygon=packed.Select(n=>new NavigationPoint((short)n,0,(short)(n>>16))).ToArray();
            var target=new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldExit,context.LogicalMapKey,uid),Generation=1,
                InteractionRegion=polygon,UseApprovedApproaches=true,ApproachCandidates=FieldRegionApproaches.Interaction(polygon,geometry)};
            var route=new FieldRoutePlanner().Plan(context,target,geometry);
            if(!route.Success)throw new Exception("Native map "+map+" route failed: "+route.Failure);
            var previous=start;
            foreach(var waypoint in route.FieldSteeringWaypoints??route.Waypoints) {
                TestAssert.True(geometry.IsSegmentClear(previous,waypoint));previous=waypoint;
            }
            TestAssert.True(FieldRegionApproaches.CanStopInside(polygon,route.ApproachPoint,0));
        }
        public static void IcePathCanTurnAroundTheNativeInnerCorner()
        {
            AssertRoute(305,80,new NavigationPoint(518,-358.6f,-667),4,300022021,276560217,231865857,255655440);
        }
        public static void TrenoStreetCanLeaveTheTowerStairsWithSteinersNativeRadius()
        {
            AssertRoute(915,120,new NavigationPoint(-11001,1177.6f,-6425),9,196597847,210228718,98947968,102946780);
        }
        public static void SlumsCanReachThePubFromTheGate()
        {
            AssertRoute(912,120,new NavigationPoint(6722,5993,-17310),10,3353025667,3359447827,3300138256,3300859391,3307806279);
        }
        public static void TrenoWallReviewRequiresTheOriginalMapAndWallAndNativeSize()
        {
            var geometry=Load(915,120);var triangle=geometry.Find(111);
            TestAssert.True(geometry.UsesReviewedBoundaryClearance(triangle));
            geometry.Radius=121;TestAssert.False(geometry.UsesReviewedBoundaryClearance(triangle));
            TestAssert.Equal(0,FieldNativeGeometryContract.CreateTriangle(111,2,914,triangle.Native).ReviewedBoundaryEdges);
            var old=triangle.Native.CrossingVertices[0];
            triangle.Native.CrossingVertices[0]=new NavigationPoint(old.X+1,old.Y,old.Z);
            TestAssert.Equal(0,FieldNativeGeometryContract.CreateTriangle(111,2,915,triangle.Native).ReviewedBoundaryEdges);
        }
    }
}
