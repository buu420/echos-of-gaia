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
        private static FieldGeometrySnapshot Load(int map,float radius,string fixture="native-route-geometry.json")
        {
            var root=new DirectoryInfo(Environment.CurrentDirectory);
            while(root!=null&&!Directory.Exists(Path.Combine(root.FullName,"Assembly-CSharp")))root=root.Parent;
            if(root==null)throw new Exception("Run route regressions from the source checkout");
            using(var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(root.FullName,"tests/FF9Access.Tests/Fixtures",fixture)))) {
                var record=json.RootElement.GetProperty("Maps").EnumerateArray().Single(m=>m.GetProperty("Map").GetInt32()==map);
                TestAssert.Equal(64,record.GetProperty("MeshSHA256").GetString().Length);
                var options=new JsonSerializerOptions{IncludeFields=true};options.Converters.Add(new PointConverter());
                var geometry=JsonSerializer.Deserialize<FieldGeometrySnapshot>(record.GetProperty("Geometry").GetRawText(),options);
                geometry.Radius=radius;
                geometry.Triangles=geometry.Triangles.Select(t=>FieldNativeGeometryContract.CreateTriangle(t.Id,t.Floor,map,t.Native)).ToArray();
                if (map == 1009) CleyraTrunkRoutes.ReviewGeometry(CleyraTrunkRoutes.MapKey,geometry);
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
        private static NavigationContext CleyraEntrance(FieldGeometrySnapshot geometry)
        {
            var context = TestFixtures.Context("field:1009:FBG_N18_GTRE_MAP369_GT_DUN_6");
            geometry.ActiveTriangle = 70;
            var entrance = new NavigationPoint(381,192.1202f,-52);
            context.Position = new NavigationPoint(entrance.X,geometry.Find(70).Height(entrance),entrance.Z);
            context.PlayerIdentity = 5;
            context.StoryProgress = 4660;
            return context;
        }

        public static void CleyraArea7StoryReachesTheBridgeFromTheReportedEntrance()
        {
            var geometry = Load(1009,80,"field1009-cleyra-geometry.json");
            var context = CleyraEntrance(geometry);
            var region = new uint[]{267978627,252381388,143066357,154141563}
                .Select(value => new NavigationPoint((short)value,0,(short)(value>>16))).ToArray();
            var exit = new NavigationTarget {
                Id = new TargetIdentity(TargetKind.FieldExit,context.LogicalMapKey,3), Generation = context.Generation,
                NextFields = new[]{1010}, InteractionRegion = region, UseApprovedApproaches = true,
                ApproachCandidates = FieldRegionApproaches.Interaction(region,geometry)
            };
            var story = StoryTargetCatalog.Wrap(context,4700,exit,"Go to Cleyra, Bridge");
            var route = new FieldRoutePlanner().Plan(context,story,geometry);
            if (!route.Success) throw new Exception("Area 7 Story route failed: " + route.Failure);
            TestAssert.Equal(exit.Id,story.PhysicalIdentity);
            TestAssert.True(FieldRegionApproaches.CanStopInside(region,route.ApproachPoint,4));
        }

        public static void CleyraArea7ChestRouteStopsWithinItsNativePushRange()
        {
            var geometry = Load(1009,80,"field1009-cleyra-geometry.json");
            var context = CleyraEntrance(geometry);
            var chest = new NavigationPoint(717,2355.52f,4415);
            var target = new NavigationTarget {
                Id = new TargetIdentity(TargetKind.FieldActor,context.LogicalMapKey,2), Generation = context.Generation,
                IsInteractive = true, Position = chest,
                ApproachCandidates = FieldActorApproaches.Create(context.Position,chest,256,346)
            };
            var route = new FieldRoutePlanner().Plan(context,target,geometry);
            if (!route.Success) throw new Exception("Area 7 chest route failed: " + route.Failure);
            TestAssert.True(FieldGeometrySnapshot.Horizontal(route.ApproachPoint,chest) >= 256);
            TestAssert.True(FieldGeometrySnapshot.Horizontal(route.ApproachPoint,chest) < 342);
            TestAssert.True(Math.Abs(route.ApproachPoint.Y-chest.Y) < 400);
            TestAssert.Equal(40f,route.FieldIntermediateArrivalRadius);
        }

        public static void CleyraArea7ReviewRequiresTheUnchangedWalkmesh()
        {
            var geometry = Load(1009,81,"field1009-cleyra-geometry.json");
            TestAssert.False(geometry.HasReviewedCleyraTrunk);
            geometry.Radius = 80;
            geometry.Find(87).Native.CrossingVertices[1] = new NavigationPoint(225,-2190,4294);
            CleyraTrunkRoutes.ReviewGeometry(CleyraTrunkRoutes.MapKey,geometry);
            TestAssert.False(geometry.HasReviewedCleyraTrunk);
            TestAssert.False(geometry.UsesReviewedBoundaryClearance(geometry.Find(87)));
        }

        public static void LindblumInnSaveMoogleIsReachableFromBothReportedPositions()
        {
            var geometry=Load(554,80,"field554-inn-geometry.json");
            var context=TestFixtures.Context("field:554:FBG_N11_LDBM_MAP161_LB_IN2_0");
            context.PlayerIdentity=12;
            context.StoryProgress=3110;
            var moogle=new NavigationPoint(-204,0,1075);
            var origins=new[]{new NavigationPoint(-703,0,-1001),new NavigationPoint(810,0,1616)};
            foreach(var origin in origins) {
                context.Position=origin;
                geometry.ActiveTriangle=geometry.At(origin).Id;
                var target=new NavigationTarget {
                    Id=new TargetIdentity(TargetKind.FieldActor,context.LogicalMapKey,7),Generation=context.Generation,
                    IsInteractive=true,Position=moogle,
                    ApproachCandidates=FieldActorApproaches.Create(origin,moogle,152,338)
                };
                var route=new FieldRoutePlanner().Plan(context,target,geometry);
                if (!route.Success) throw new Exception("Lindblum Inn Area 2 moogle route: "+route.Failure);
                float distance=FieldGeometrySnapshot.Horizontal(route.ApproachPoint,moogle);
                TestAssert.True(distance>=152 && distance+FieldAnalogMovement.ArrivalRadius(geometry.Radius,1)<338);
                TestAssert.True(Math.Abs(route.ApproachPoint.Y-moogle.Y)<400);
                var last=origin;
                foreach(var waypoint in route.FieldSteeringWaypoints??route.Waypoints) {
                    TestAssert.True(geometry.IsSegmentClear(last,waypoint));
                    last=waypoint;
                }
            }
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
