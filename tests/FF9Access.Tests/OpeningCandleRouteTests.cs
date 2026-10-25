using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    // Exact installed BGI plus reviewed phase-1 flags/POS/RADIUS, not a live capture.
    internal static class OpeningCandleRouteTests
    {
        private static NavigationPoint Point(JsonElement p){return new NavigationPoint(p.GetProperty("X").GetSingle(),p.GetProperty("Y").GetSingle(),p.GetProperty("Z").GetSingle());}
        private static NavigationPoint[] Points(JsonElement p){return p.EnumerateArray().Select(Point).ToArray();}
        private static int[] Ints(JsonElement p){return p.EnumerateArray().Select(v=>v.GetInt32()).ToArray();}
        private static bool Inside(NavigationPoint[] polygon,NavigationPoint point)
        {
            for(int i=0;i<polygon.Length;i++)if(new FieldTriangle{Vertices=new[]{polygon[i],polygon[(i+1)%polygon.Length],polygon[(i+2)%polygon.Length]}}.Contains(point))return true;
            return false;
        }
        private static FieldGeometrySnapshot Geometry()
        {
            var directory=new DirectoryInfo(Environment.CurrentDirectory);while(directory!=null && !Directory.Exists(Path.Combine(directory.FullName,"Assembly-CSharp")))directory=directory.Parent;
            if(directory==null)throw new Exception("Repository root not found");
            using(var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory.FullName,"tests/FF9Access.Tests/Fixtures/field50-candle-native.json")))){
                TestAssert.Equal(OpeningCandleEvidence.Walkmesh,json.RootElement.GetProperty("Source").GetProperty("SHA256").GetString());
                var g=json.RootElement.GetProperty("Geometry");
                var result=new FieldGeometrySnapshot{Generation=1,Radius=g.GetProperty("Radius").GetSingle(),AttributeMask=255,BlockedAttributes=128};
                result.Triangles=g.GetProperty("Triangles").EnumerateArray().Select(v=>{
                    var n=v.GetProperty("Native");
                    return FieldNativeGeometryContract.CreateTriangle(v.GetProperty("Id").GetInt32(),v.GetProperty("Floor").GetInt32(),50,new FieldNativeTriangleData{
                        OriginalVertices=Points(n.GetProperty("OriginalVertices")),CrossingVertices=Points(n.GetProperty("CrossingVertices")),FloorDelta=Point(n.GetProperty("FloorDelta")),
                        RuntimeNeighbors=Ints(n.GetProperty("RuntimeNeighbors")),NativeNeighbors=Ints(n.GetProperty("NativeNeighbors")),EdgeClones=Ints(n.GetProperty("EdgeClones")),
                        RuntimeFlags=n.GetProperty("RuntimeFlags").GetInt32(),NativeFlags=n.GetProperty("NativeFlags").GetInt32(),NormalIndex=n.GetProperty("NormalIndex").GetInt32(),
                        Normal=Point(n.GetProperty("Normal")),NormalInverseY=n.GetProperty("NormalInverseY").GetInt32(),Plane=n.GetProperty("Plane").GetInt32(),
                        ThetaX=n.GetProperty("ThetaX").GetInt32(),ThetaZ=n.GetProperty("ThetaZ").GetInt32(),Projection=n.GetProperty("Projection").GetInt32()});
                }).ToArray();
                result.ActiveTriangle=result.At(new NavigationPoint(25,0,-664)).Id;
                result.Fingerprint=FieldNativeGeometryContract.Fingerprint(OpeningCandleEvidence.Map,result);return result;
            }
        }
        public static void InitialControlledPositionRoutesToTheRealCandleTouchRegions()
        {
            var geometry=Geometry();TestAssert.Equal(203,geometry.Triangles.Length);TestAssert.Equal(80f,geometry.Radius);
            var context=new NavigationContext{LogicalMapKey=OpeningCandleEvidence.Map,Generation=1,PlayerIdentity=17,CanControl=true,Position=new NavigationPoint(25,0,-664)};
            foreach(var area in new[]{OpeningStoryCheckpoints.CandleArea}.Concat(OpeningStoryCheckpoints.CandleRegions.Select(r=>r.Polygon))){
                var target=new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldActor,context.LogicalMapKey,4),Generation=1,Position=new NavigationPoint(0,0,350),IsInteractive=true,InteractionUid=4,InteractionRegion=area};
                var route=new FieldRoutePlanner().Plan(context,target,geometry);TestAssert.True(route.Success);
                TestAssert.True(OpeningStoryCheckpoints.CandleRegions.Any(r=>Inside(r.Polygon,route.ApproachPoint)));
                var from=context.Position;foreach(var point in route.Waypoints){TestAssert.True(geometry.IsSegmentClear(from,point));from=point;}
            }
        }
        public static void WarmCandlePresentationDoesNotRegenerateMegabytesOfApproachSamples()
        {
            var geometry=Geometry();var cache=new OpeningCandleApproachCache();var first=cache.Read(geometry);TestAssert.True(first.Length>0);
            long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<30;i++)cache.Read(geometry);
            TestAssert.True(GC.GetAllocatedBytesForCurrentThread()-before<16384);
            geometry.Radius=1000;geometry.Fingerprint=FieldNativeGeometryContract.Fingerprint(OpeningCandleEvidence.Map,geometry);
            TestAssert.Equal(0,cache.Read(geometry).Length);
        }
        public static void FinalApproachContainsTheNativeFollowersWholeStoppingTolerance()
        {
            var geometry=Geometry();var context=new NavigationContext{LogicalMapKey=OpeningCandleEvidence.Map,Generation=1,PlayerIdentity=17,CanControl=true,Position=new NavigationPoint(25,0,-664)};
            var target=new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldActor,context.LogicalMapKey,4),Generation=1,Position=new NavigationPoint(0,0,350),IsInteractive=true,InteractionUid=4,InteractionRegion=OpeningCandleEvidence.ApproachArea};
            var route=new FieldRoutePlanner().Plan(context,target,geometry);TestAssert.True(route.Success);
            float tolerance=FieldAnalogMovement.ArrivalRadius(geometry.Radius,1);
            for(int i=0;i<32;i++){
                double angle=2*Math.PI*i/32;
                var possibleStop=new NavigationPoint(route.ApproachPoint.X+tolerance*(float)Math.Cos(angle),route.ApproachPoint.Y,route.ApproachPoint.Z+tolerance*(float)Math.Sin(angle));
                TestAssert.True(OpeningStoryCheckpoints.CandleRegions.Any(r=>Inside(r.Polygon,possibleStop)));
            }
        }
    }
}
