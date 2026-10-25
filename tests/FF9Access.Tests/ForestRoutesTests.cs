using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class ForestRoutesTests
    {
        private static NavigationPoint Point(JsonElement value) { return new NavigationPoint(value.GetProperty("X").GetSingle(),value.GetProperty("Y").GetSingle(),value.GetProperty("Z").GetSingle()); }
        private static NavigationPoint[] Points(JsonElement value) { return value.EnumerateArray().Select(Point).ToArray(); }
        private static int[] Integers(JsonElement value) { return value.EnumerateArray().Select(v=>v.GetInt32()).ToArray(); }
        private sealed class Capture
        {
            public NavigationContext Context; public FieldGeometrySnapshot Geometry; public NavigationTarget Mene,Object; public NavigationPoint[] Region;
        }
        private static Capture Load(string resource="Field2950.Forest.json")
        {
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
            using(var json=JsonDocument.Parse(stream)) {
                var root=json.RootElement;var c=root.GetProperty("Context");var g=root.GetProperty("Geometry");var transport=c.GetProperty("Transport");
                var result=new Capture{Context=new NavigationContext{CanControl=true,CanInspect=true,Foreground=true,Generation=c.GetProperty("Generation").GetInt64(),LogicalMapKey=c.GetProperty("LogicalMapKey").GetString(),Position=Point(c.GetProperty("Position")),PlayerIdentity=c.GetProperty("PlayerIdentity").GetInt32(),Transport=new TransportIdentity((TransportKind)transport.GetProperty("Kind").GetInt32(),transport.GetProperty("ActorId").GetInt32(),transport.GetProperty("NativeStatus").GetInt32())},Region=Points(root.GetProperty("MeneInteractionRegion"))};
                result.Geometry=new FieldGeometrySnapshot{Generation=result.Context.Generation,ActiveTriangle=g.GetProperty("ActiveTriangle").GetInt32(),Radius=g.GetProperty("Radius").GetSingle(),AttributeMask=g.GetProperty("AttributeMask").GetByte(),BlockedAttributes=g.GetProperty("BlockedAttributes").GetByte(),Fingerprint=g.GetProperty("Fingerprint").GetString()};
                result.Geometry.Triangles=g.GetProperty("Triangles").EnumerateArray().Select(v=>{
                    var n=v.GetProperty("Native");
                    return new FieldTriangle{Id=v.GetProperty("Id").GetInt32(),Floor=v.GetProperty("Floor").GetInt32(),Enabled=v.GetProperty("Enabled").GetBoolean(),RuntimeEnabled=v.GetProperty("RuntimeEnabled").GetBoolean(),Attributes=v.GetProperty("Attributes").GetByte(),TraversalCost=v.GetProperty("TraversalCost").GetSingle(),Neighbors=Integers(v.GetProperty("Neighbors")),Vertices=Points(v.GetProperty("Vertices")),RadiusVertices=Points(v.GetProperty("RadiusVertices")),Native=new FieldNativeTriangleData{OriginalVertices=Points(n.GetProperty("OriginalVertices")),CrossingVertices=Points(n.GetProperty("CrossingVertices")),FloorDelta=Point(n.GetProperty("FloorDelta")),RuntimeNeighbors=Integers(n.GetProperty("RuntimeNeighbors")),NativeNeighbors=Integers(n.GetProperty("NativeNeighbors")),EdgeClones=Integers(n.GetProperty("EdgeClones")),RuntimeFlags=n.GetProperty("RuntimeFlags").GetInt32(),NativeFlags=n.GetProperty("NativeFlags").GetInt32(),Projection=n.GetProperty("Projection").GetInt32()}};
                }).ToArray();
                Func<JsonElement,NavigationTarget> target=t=>new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldActor,result.Context.LogicalMapKey,t.GetProperty("Id").GetProperty("NativeId").GetInt32()),Generation=result.Context.Generation,Position=Point(t.GetProperty("Position")),ApproachCandidates=Points(t.GetProperty("ApproachCandidates")),IsInteractive=t.GetProperty("IsInteractive").GetBoolean(),Label=t.GetProperty("Label").GetString()};
                result.Mene=target(root.GetProperty("Target"));result.Object=target(root.GetProperty("ObjectTarget"));return result;
            }
        }
        public static void MeneUsesNativeGroundTalkRegionWhileHisRaisedMeshStaysDisconnected()
        {
            var capture=Load();var target=capture.Mene;var original=new FieldRoutePlanner().Plan(capture.Context,target,capture.Geometry);
            TestAssert.False(original.Success);TestAssert.Equal(RouteFailure.Disconnected,original.Failure);
            target.InteractionUid=19;target.InteractionRegion=capture.Region;
            var route=new FieldRoutePlanner().Plan(capture.Context,target,capture.Geometry);
            if(!route.Success)throw new Exception("Mene route failed: "+route.Failure);AssertSafe(capture,route);
            TestAssert.True(route.ApproachPoint.X>=473&&route.ApproachPoint.X<=1004);
            TestAssert.True(route.ApproachPoint.Z>=-4676&&route.ApproachPoint.Z<=-4208);
            TestAssert.True(route.ApproachPoint.Y<400);
        }
        public static void ForestExitUsesItsTriggerSurfaceRatherThanTheFarMapEdge()
        {
            var capture=Load();
            // Installed field2950 entry20 QUAD, SHA256 41e565a9...1bd833d.
            var region=new[]{new NavigationPoint(-1235,0,-6218),new NavigationPoint(378,0,-6288),new NavigationPoint(363,0,-5304),new NavigationPoint(-985,0,-5281)};
            var candidates=FieldRegionApproaches.Interaction(region,capture.Geometry);
            TestAssert.True(candidates.Length>0);
            var exit=new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldExit,capture.Context.LogicalMapKey,20),Category=NavigationCategory.Exits,Generation=capture.Context.Generation,Position=candidates[0],IsInteractive=true,ApproachCandidates=candidates};
            var route=new FieldRoutePlanner().Plan(capture.Context,exit,capture.Geometry);
            if(!route.Success)throw new Exception("Forest exit route failed: "+route.Failure);
            AssertSafe(capture,route);
            TestAssert.True(route.ApproachPoint.Z< -5281 && route.ApproachPoint.Z> -6218);
            // The native camera can show the trigger before its remote boundary.
            // Native projection: negative Unity Y is restored before GTE, then
            // pixel Y = camera-height/2 + projected view Y / depth.
            double x=route.ApproachPoint.X,y=-route.ApproachPoint.Y,z=route.ApproachPoint.Z;
            double depth=26*x/4096+3263*y/4096+2476*z/4096+11975;
            double cameraY=-32*x/4096+2311*y/4096-3045*z/4096-1097;
            double pixel=216+cameraY*640/Math.Abs(depth);
            TestAssert.True(pixel<432 && pixel>=208);
        }
        public static void ObjectWithoutInteractionUsesReachableViewingPoint()
        {
            var capture=Load();var target=capture.Object;target.UseViewingPoint=true;
            TestAssert.False(target.IsInteractive);
            var route=new FieldRoutePlanner().Plan(capture.Context,target,capture.Geometry);
            if(!route.Success)throw new Exception("Object viewing route failed: "+route.Failure);AssertSafe(capture,route);
            TestAssert.True(route.ViewingPoint);
            TestAssert.True(route.ApproachPoint.Y<400);
            TestAssert.True(FieldGeometrySnapshot.Horizontal(route.ApproachPoint,target.Position)<1000);
        }
        public static void ObjectViewingRouteCanStartAfterTalkingToMene()
        {
            var capture=Load("Field2950.AfterMene.json");var target=capture.Object;target.UseViewingPoint=true;
            TestAssert.False(capture.Geometry.IsSegmentClear(capture.Context.Position,capture.Context.Position));
            var route=new FieldRoutePlanner().Plan(capture.Context,target,capture.Geometry);
            if(!route.Success)throw new Exception("Post-Mene viewing route failed: "+route.Failure);
            AssertSafe(capture,route);TestAssert.True(route.ViewingPoint);
        }
        public static void BoundaryOverlapAllowsOnlyDepartureAwayToFullClearance()
        {
            var geometry=new FieldGeometrySnapshot{Radius=10,ActiveTriangle=0,Triangles=new[]{new FieldTriangle{Id=0,Vertices=new[]{new NavigationPoint(0,0,0),new NavigationPoint(100,0,0),new NavigationPoint(0,0,100)}}}};
            var start=new NavigationPoint(20,0,5);
            TestAssert.False(geometry.IsSegmentClear(start,start));
            TestAssert.True(geometry.IsSegmentClear(start,new NavigationPoint(20,0,20)));
            TestAssert.False(geometry.IsSegmentClear(start,new NavigationPoint(20,0,2)));
            TestAssert.False(geometry.IsSegmentClear(start,new NavigationPoint(30,0,5)));
            TestAssert.False(geometry.IsSegmentClear(start,new NavigationPoint(20,0,-20)));
            TestAssert.False(geometry.IsSegmentClear(start,new NavigationPoint(80,0,80)));
        }
        public static void NativeRadiusVisitFlagsDoNotChangeTheRouteFingerprint()
        {
            var capture=Load();string original=FieldNativeGeometryContract.Fingerprint(capture.Context.LogicalMapKey,capture.Geometry);
            foreach(int id in new[]{39,100}){var triangle=capture.Geometry.Find(id);triangle.Native.NativeFlags|=128;triangle.Native.RuntimeFlags|=128;}
            TestAssert.Equal(original,FieldNativeGeometryContract.Fingerprint(capture.Context.LogicalMapKey,capture.Geometry));
            capture.Geometry.Find(100).Native.NativeFlags|=0x8000;
            TestAssert.False(original==FieldNativeGeometryContract.Fingerprint(capture.Context.LogicalMapKey,capture.Geometry));
        }
        public static void BoundRegionSurvivesCameraScrollWithoutUnseenUpdates()
        {
            var capture=Load();var catalog=new FieldTargetCatalog();var actor=new FieldTargetObservation{Id=capture.Mene.Id,Position=capture.Mene.Position,Visible=true,IsPerson=true,HasInteraction=true,NativePresentationIdentity=42,InteractionUid=19,InteractionRegion=(NavigationPoint[])capture.Region.Clone()};
            var original=catalog.Build(capture.Context,new[]{actor},null).Find(actor.Id);
            actor.Visible=false;actor.OutOfView=true;actor.InteractionUid=99;actor.InteractionRegion[0]=new NavigationPoint(9999,0,9999);
            var remembered=catalog.Build(capture.Context,new[]{actor},null).Find(actor.Id);
            TestAssert.Equal(19,remembered.InteractionUid);TestAssert.Equal(original.InteractionRegion[0],remembered.InteractionRegion[0]);
            TestAssert.True(remembered.Availability.Contains("last seen"));
            actor.NativePresentationIdentity++;TestAssert.Equal(0,catalog.Build(capture.Context,new[]{actor},null).Targets.Count);
        }
        public static void ChangedInteractionRegionInvalidatesDirectionsEvenWhenActorDidNotMove()
        {
            var capture=Load();capture.Mene.InteractionUid=19;capture.Mene.InteractionRegion=capture.Region;
            var fresh=new NavigationTarget{Id=capture.Mene.Id,Position=capture.Mene.Position,InteractionUid=19,InteractionRegion=(NavigationPoint[])capture.Region.Clone()};fresh.InteractionRegion[0]=new NavigationPoint(9000,0,9000);
            var route=new FieldRoutePlanner().Plan(capture.Context,capture.Mene,capture.Geometry);
            TestAssert.Equal(MovementOutcome.TargetMoved,GuidanceRouteValidation.Check(route,capture.Mene,fresh,capture.Geometry.Fingerprint,80,false,true));
        }
        public static void ViewingArrivalDoesNotPromptForAnUnavailableInteraction()
        {
            var capture=Load();var target=capture.Object;target.Category=NavigationCategory.Objects;
            var catalog=new CatalogResult();catalog.Targets.Add(target);var coordinator=new NavigationCoordinator();
            coordinator.Tick(capture.Context,new[]{NavigationCommand.NextCategory,NavigationCommand.NextCategory,NavigationCommand.StartAutomatic},catalog,null);
            var update=coordinator.Tick(capture.Context,new NavigationCommand[0],catalog,new MovementObservation{Outcome=MovementOutcome.ApproachReached,PlannedRoute=new RoutePlan{ViewingPoint=true}});
            TestAssert.True(update.Messages.Any(m=>m.Contains("viewing point")&&m.Contains("no interaction")));
            TestAssert.False(update.Messages.Any(m=>m.Contains("Confirm")||m.Contains("ordinary game action")));
        }
        public static void ForestBindingsRejectChangedScriptsModelsScenesAndLoweredMene()
        {
            var capture=Load();var p=capture.Mene.Position;
            TestAssert.True(ForestNavigationEvidence.MeneRegion(ForestNavigationEvidence.Map,9,220,ForestNavigationEvidence.MeneScript,19,ForestNavigationEvidence.TalkRegionScript,p));
            TestAssert.False(ForestNavigationEvidence.MeneRegion("field:2951",9,220,ForestNavigationEvidence.MeneScript,19,ForestNavigationEvidence.TalkRegionScript,p));
            TestAssert.False(ForestNavigationEvidence.MeneRegion(ForestNavigationEvidence.Map,9,220,"changed",19,ForestNavigationEvidence.TalkRegionScript,p));
            TestAssert.False(ForestNavigationEvidence.MeneRegion(ForestNavigationEvidence.Map,9,220,ForestNavigationEvidence.MeneScript,19,"changed",p));
            TestAssert.False(ForestNavigationEvidence.MeneRegion(ForestNavigationEvidence.Map,9,221,ForestNavigationEvidence.MeneScript,19,ForestNavigationEvidence.TalkRegionScript,p));
            TestAssert.False(ForestNavigationEvidence.MeneRegion(ForestNavigationEvidence.Map,9,220,ForestNavigationEvidence.MeneScript,19,ForestNavigationEvidence.TalkRegionScript,new NavigationPoint(p.X,0,p.Z)));
            TestAssert.True(ForestNavigationEvidence.NoninteractiveObject(ForestNavigationEvidence.Map,11,241,ForestNavigationEvidence.ObjectScript,false));
            TestAssert.False(ForestNavigationEvidence.NoninteractiveObject(ForestNavigationEvidence.Map,11,241,ForestNavigationEvidence.ObjectScript,true));
        }
        private static void AssertSafe(Capture capture,RoutePlan route)
        {
            TestAssert.True(route.Waypoints.Length>0);var from=capture.Context.Position;
            foreach(var point in route.Waypoints){TestAssert.True(capture.Geometry.IsSegmentClear(from,point));from=point;}
            TestAssert.Near(0,NavigationPoint.Distance(from,route.ApproachPoint),0.02f);
            TestAssert.Near(80,capture.Geometry.Radius,0);
        }
    }
}
