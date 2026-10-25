using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class CapturedFieldRoutesTests
    {
        private static NavigationPoint Point(JsonElement value) {return new NavigationPoint(value.GetProperty("X").GetSingle(),value.GetProperty("Y").GetSingle(),value.GetProperty("Z").GetSingle());}
        public static void GuestRoomChestRouteUsesNativeConnectionsAndKeepsWalls()
        {
            CheckRoute("Field611.ObjectB.json");
        }
        public static void GuestRoomLowerFloorChestKeepsTheConnectedRampRoute()
        {
            var route=CheckRoute("Field611.ObjectA.json");
            TestAssert.True(route.Waypoints.Length>=2);
            TestAssert.True(route.Waypoints.Any(p=>p.Y>route.ApproachPoint.Y+500));
            TestAssert.True(System.Math.Abs(route.Waypoints.Last().Y-4)<0.01f);
        }
        private static RoutePlan CheckRoute(string resource)
        {
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
            using(var json=JsonDocument.Parse(stream)) {
                var root=json.RootElement;var c=root.GetProperty("Context");var t=root.GetProperty("Target");var g=root.GetProperty("Geometry");
                var transport=c.GetProperty("Transport");string map=c.GetProperty("LogicalMapKey").GetString();
                var context=new NavigationContext{Generation=c.GetProperty("Generation").GetInt64(),LogicalMapKey=map,Position=Point(c.GetProperty("Position")),PlayerIdentity=c.GetProperty("PlayerIdentity").GetInt32(),Transport=new TransportIdentity((TransportKind)transport.GetProperty("Kind").GetInt32(),transport.GetProperty("ActorId").GetInt32(),transport.GetProperty("NativeStatus").GetInt32())};
                var identity=t.GetProperty("Id");
                var target=new NavigationTarget{Generation=t.GetProperty("Generation").GetInt64(),Id=new TargetIdentity((TargetKind)identity.GetProperty("Kind").GetInt32(),map,identity.GetProperty("NativeId").GetInt32()),Position=Point(t.GetProperty("Position")),ApproachCandidates=t.GetProperty("ApproachCandidates").EnumerateArray().Select(Point).ToArray()};
                var geometry=new FieldGeometrySnapshot{Generation=g.GetProperty("Generation").GetInt64(),ActiveTriangle=g.GetProperty("ActiveTriangle").GetInt32(),Radius=g.GetProperty("Radius").GetSingle(),AttributeMask=g.GetProperty("AttributeMask").GetByte(),BlockedAttributes=g.GetProperty("BlockedAttributes").GetByte(),Fingerprint=g.GetProperty("Fingerprint").GetString()};
                geometry.Triangles=g.GetProperty("Triangles").EnumerateArray().Select(v=>new FieldTriangle{Id=v.GetProperty("Id").GetInt32(),Floor=v.GetProperty("Floor").GetInt32(),Enabled=v.GetProperty("Enabled").GetBoolean(),RuntimeEnabled=v.GetProperty("RuntimeEnabled").GetBoolean(),Attributes=v.GetProperty("Attributes").GetByte(),TraversalCost=v.GetProperty("TraversalCost").GetSingle(),Neighbors=v.GetProperty("Neighbors").EnumerateArray().Select(n=>n.GetInt32()).ToArray(),Vertices=v.GetProperty("Vertices").EnumerateArray().Select(Point).ToArray()}).ToArray();
                var route=new FieldRoutePlanner().Plan(context,target,geometry);TestAssert.True(route.Success);TestAssert.True(route.Waypoints.Length>=1);
                var from=context.Position;foreach(var point in route.Waypoints){TestAssert.True(geometry.IsSegmentClear(from,point));from=point;}
                TestAssert.True(FieldGeometrySnapshot.Horizontal(from,route.ApproachPoint)<0.01f);
                return route;
            }
        }
    }
}
