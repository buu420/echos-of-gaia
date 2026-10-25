using System;
using System.Collections.Generic;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class FieldRoutesTests
    {
        internal static FieldGeometrySnapshot Grid(bool obstacle)
        {
            var triangles=new List<FieldTriangle>();
            for(int z=0;z<2;z++)for(int x=0;x<3;x++) {
                if(obstacle && x==1 && z==0)continue;
                var a=TestFixtures.P(x*20,z*20);var b=TestFixtures.P((x+1)*20,z*20);var c=TestFixtures.P(x*20,(z+1)*20);var d=TestFixtures.P((x+1)*20,(z+1)*20);
                triangles.Add(new FieldTriangle{Id=triangles.Count,Vertices=new[]{a,b,c}});triangles.Add(new FieldTriangle{Id=triangles.Count,Vertices=new[]{b,d,c}});
            }
            Connect(triangles);
            return new FieldGeometrySnapshot {Generation=1,Radius=1,Triangles=triangles.ToArray()};
        }
        private static void Connect(List<FieldTriangle> triangles)
        {
            foreach(var a in triangles)for(int edge=0;edge<3;edge++)foreach(var b in triangles)if(a!=b)for(int other=0;other<3;other++) {
                NavigationPoint a0=a.Vertices[edge],a1=a.Vertices[(edge+1)%3],b0=b.Vertices[other],b1=b.Vertices[(other+1)%3];
                if((Same(a0,b0)&&Same(a1,b1)) || (Same(a0,b1)&&Same(a1,b0)))a.Neighbors[edge]=b.Id;
            }
        }
        private static bool Same(NavigationPoint a,NavigationPoint b) { return NavigationPoint.Distance(a,b)<0.01f; }
        private static RoutePlan Route(FieldGeometrySnapshot geometry) { var context=TestFixtures.Context();context.Position=TestFixtures.P(10,10);var target=new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldExit,context.LogicalMapKey,1),Generation=1,Position=TestFixtures.P(50,10)};return new FieldRoutePlanner().Plan(context,target,geometry); }
        public static void DetoursAroundObstacle() { var geometry=Grid(true);var route=Route(geometry);TestAssert.True(route.Success);TestAssert.True(route.Waypoints.Length>1);var from=TestFixtures.P(10,10);foreach(var point in route.Waypoints){TestAssert.True(geometry.IsSegmentClear(from,point));from=point;} }
        public static void DisabledBgiTriangleIsRejected() { var geometry=Grid(false);foreach(var tri in geometry.Triangles)if(tri.Center.X>40)tri.Enabled=false;TestAssert.False(Route(geometry).Success); }
        public static void DisconnectedFloorHasNoRoute() { var geometry=Grid(false);foreach(var tri in geometry.Triangles)for(int i=0;i<3;i++)tri.Neighbors[i]=-1;TestAssert.Equal(RouteFailure.Disconnected,Route(geometry).Failure); }
        public static void RadiusRejectsNarrowCorridor() { var geometry=Grid(true);geometry.Radius=12;TestAssert.False(Route(geometry).Success); }
        public static void FineInteriorTriangulationDoesNotBlockWideOpenFloor()
        {
            var geometry=Grid(false);geometry.Radius=12;
            var context=TestFixtures.Context();context.Position=TestFixtures.P(15,20);
            var target=new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldActor,context.LogicalMapKey,2),Generation=1,Position=TestFixtures.P(45,20)};
            TestAssert.True(geometry.IsSegmentClear(context.Position,target.Position));
            var route=new FieldRoutePlanner().Plan(context,target,geometry);
            TestAssert.True(route.Success);
            var from=context.Position;foreach(var point in route.Waypoints){TestAssert.True(geometry.IsSegmentClear(from,point));from=point;}
        }
        public static void UnrelatedOverheadBoundaryDoesNotBlockSupportedFloor()
        {
            var geometry=Grid(false);var triangles=new List<FieldTriangle>(geometry.Triangles);
            triangles.Add(new FieldTriangle{Id=20,Floor=1,Vertices=new[]{new NavigationPoint(30,64,10),new NavigationPoint(30,64,30),new NavigationPoint(35,64,20)}});
            geometry.Triangles=triangles.ToArray();
            TestAssert.True(geometry.IsSegmentClear(TestFixtures.P(15,20),TestFixtures.P(45,20)));
        }
        public static void SegmentCannotChangeToDisconnectedOverlappingFloor()
        {
            var geometry=Grid(false);var triangles=new List<FieldTriangle>(geometry.Triangles);
            foreach(var source in geometry.Triangles) {
                var raised=new FieldTriangle{Id=source.Id+20,Floor=1,Neighbors=new[]{source.Neighbors[0]<0?-1:source.Neighbors[0]+20,source.Neighbors[1]<0?-1:source.Neighbors[1]+20,source.Neighbors[2]<0?-1:source.Neighbors[2]+20}};
                raised.Vertices=Array.ConvertAll(source.Vertices,p=>new NavigationPoint(p.X,200,p.Z));triangles.Add(raised);
            }
            geometry.Triangles=triangles.ToArray();geometry.Radius=12;
            TestAssert.False(geometry.IsSegmentClear(TestFixtures.P(15,20),new NavigationPoint(45,200,20)));
        }
        public static void SegmentCannotSkipDisabledSliverBetweenSamples()
        {
            var geometry=Grid(false);
            foreach(var tri in geometry.Triangles)for(int edge=0;edge<3;edge++)tri.Neighbors[edge]=-1;
            geometry.Radius=0;
            TestAssert.False(geometry.IsSegmentClear(TestFixtures.P(15,20),TestFixtures.P(45,20)));
        }
        public static void ContinuousSlopeCrossesShortInteriorPortals()
        {
            var geometry=Grid(false);geometry.Radius=12;
            foreach(var tri in geometry.Triangles)for(int i=0;i<3;i++){var p=tri.Vertices[i];tri.Vertices[i]=new NavigationPoint(p.X,p.X/4,p.Z);}
            var context=TestFixtures.Context();context.Position=new NavigationPoint(15,3.75f,20);
            var target=new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldActor,context.LogicalMapKey,2),Generation=1,Position=new NavigationPoint(45,11.25f,20)};
            TestAssert.True(geometry.IsSegmentClear(context.Position,target.Position));
            TestAssert.True(new FieldRoutePlanner().Plan(context,target,geometry).Success);
        }
        public static void ProjectedPortalWithHeightGapIsNotWelded()
        {
            var geometry=Grid(false);
            foreach(var tri in geometry.Triangles)if(tri.Center.X>20)for(int i=0;i<3;i++){var p=tri.Vertices[i];tri.Vertices[i]=new NavigationPoint(p.X,100,p.Z);}
            TestAssert.False(geometry.IsSegmentClear(TestFixtures.P(15,10),new NavigationPoint(45,100,10)));
        }
        public static void ConnectedOverlappingFloorsKeepTheOffAxisRamp()
        {
            var geometry=Grid(false);var triangles=new List<FieldTriangle>(geometry.Triangles);
            foreach(var source in geometry.Triangles)triangles.Add(new FieldTriangle{Id=source.Id+20,Floor=1,Vertices=Array.ConvertAll(source.Vertices,p=>new NavigationPoint(p.X,200,p.Z))});
            triangles.Add(new FieldTriangle{Id=40,Vertices=new[]{new NavigationPoint(60,0,0),new NavigationPoint(120,100,0),new NavigationPoint(60,0,20)}});
            triangles.Add(new FieldTriangle{Id=41,Vertices=new[]{new NavigationPoint(120,100,0),new NavigationPoint(120,100,20),new NavigationPoint(60,0,20)}});
            triangles.Add(new FieldTriangle{Id=42,Vertices=new[]{new NavigationPoint(120,100,0),new NavigationPoint(60,200,0),new NavigationPoint(120,100,20)}});
            triangles.Add(new FieldTriangle{Id=43,Vertices=new[]{new NavigationPoint(60,200,0),new NavigationPoint(60,200,20),new NavigationPoint(120,100,20)}});
            Connect(triangles);geometry.Triangles=triangles.ToArray();
            var context=TestFixtures.Context();context.Position=TestFixtures.P(15,10);
            var target=new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldActor,context.LogicalMapKey,2),Generation=1,Position=new NavigationPoint(45,200,10)};
            TestAssert.False(geometry.IsSegmentClear(context.Position,target.Position));
            var route=new FieldRoutePlanner().Plan(context,target,geometry);TestAssert.True(route.Success);
            bool ramp=false;var from=context.Position;
            foreach(var point in route.Waypoints){ramp|=point.X>60;TestAssert.True(geometry.IsSegmentClear(from,point));from=point;}
            TestAssert.True(ramp);
        }
        public static void CapsuleRecursionStaysWithinTheLegIntervalThatReachesEachFloor()
        {
            var triangles=new List<FieldTriangle>();
            NavigationPoint[][] vertices={
                new[]{new NavigationPoint(0,0,0),new NavigationPoint(100,0,0),new NavigationPoint(0,0,40)},
                new[]{new NavigationPoint(100,0,0),new NavigationPoint(100,0,40),new NavigationPoint(0,0,40)},
                new[]{new NavigationPoint(90,.5f,0),new NavigationPoint(100,.5f,0),new NavigationPoint(90,.5f,40)},
                new[]{new NavigationPoint(100,.5f,0),new NavigationPoint(100,.5f,40),new NavigationPoint(90,.5f,40)},
                new[]{new NavigationPoint(100,0,0),new NavigationPoint(100.5f,.25f,0),new NavigationPoint(100,0,40)},
                new[]{new NavigationPoint(100.5f,.25f,0),new NavigationPoint(100.5f,.25f,40),new NavigationPoint(100,0,40)},
                new[]{new NavigationPoint(100.5f,.25f,0),new NavigationPoint(100,.5f,0),new NavigationPoint(100.5f,.25f,40)},
                new[]{new NavigationPoint(100,.5f,0),new NavigationPoint(100,.5f,40),new NavigationPoint(100.5f,.25f,40)}
            };
            foreach(var value in vertices)triangles.Add(new FieldTriangle{Id=triangles.Count,Vertices=value});Connect(triangles);
            var geometry=new FieldGeometrySnapshot{Generation=1,Radius=1,Triangles=triangles.ToArray()};
            TestAssert.True(geometry.IsSegmentClear(TestFixtures.P(80,10),TestFixtures.P(99.6f,10)));
        }
        public static void StationaryPointOnLegalSharedEdgeRetainsActiveSupport()
        {
            var geometry=Grid(false);geometry.ActiveTriangle=1;
            TestAssert.True(geometry.IsSegmentClear(TestFixtures.P(10,10),TestFixtures.P(10,10)));
        }
        public static void WiderBypassRemainsAvailableWhenTheShortestOpeningIsTooNarrow()
        {
            var triangles=new List<FieldTriangle>();
            for(int z=0;z<6;z++)for(int x=0;x<5;x++) {
                if(x==2&&(z==0||z==2||z==3))continue;
                var a=TestFixtures.P(x*20,z*20);var b=TestFixtures.P((x+1)*20,z*20);var c=TestFixtures.P(x*20,(z+1)*20);var d=TestFixtures.P((x+1)*20,(z+1)*20);
                triangles.Add(new FieldTriangle{Id=triangles.Count,Vertices=new[]{a,b,c}});triangles.Add(new FieldTriangle{Id=triangles.Count,Vertices=new[]{b,d,c}});
            }Connect(triangles);
            var geometry=new FieldGeometrySnapshot{Generation=1,Radius=12,Triangles=triangles.ToArray()};
            var context=TestFixtures.Context();context.Position=TestFixtures.P(20,30);
            var target=new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldActor,context.LogicalMapKey,2),Generation=1,Position=TestFixtures.P(80,30)};
            TestAssert.True(geometry.IsSegmentClear(context.Position,TestFixtures.P(20,100)));
            TestAssert.True(geometry.IsSegmentClear(TestFixtures.P(20,100),TestFixtures.P(80,100)));
            TestAssert.True(geometry.IsSegmentClear(TestFixtures.P(80,100),target.Position));
            var route=new FieldRoutePlanner().Plan(context,target,geometry);TestAssert.True(route.Success);
            var from=context.Position;foreach(var point in route.Waypoints){TestAssert.True(geometry.IsSegmentClear(from,point));from=point;}
        }
        public static void OrdinaryFieldCoordinatesDoNotLoseSharedPortalCrossings()
        {
            var geometry=Grid(false);
            foreach(var tri in geometry.Triangles)for(int i=0;i<3;i++){var p=tri.Vertices[i];tri.Vertices[i]=new NavigationPoint(p.X+2000,p.Y,p.Z+2000);}
            var from=TestFixtures.P(2053.974f,2005.4596f);var to=TestFixtures.P(2049.5823f,2036.9845f);
            geometry.Radius=0;TestAssert.True(geometry.IsSegmentClear(from,to));
            geometry.Radius=1;TestAssert.True(geometry.IsSegmentClear(from,to));
            TestAssert.True(geometry.At(TestFixtures.P(2050,1999.5f))==null);
            var inside=TestFixtures.P(2010.6968f,2029.316f);TestAssert.Equal(7,geometry.At(inside).Id);
            TestAssert.True(geometry.IsSegmentClear(TestFixtures.P(2014.9408f,2026.8832f),inside));
        }
        public static void CostRelaxationFindsBetterPath() { var geometry=Grid(false);geometry.Triangles[2].TraversalCost=20;geometry.Triangles[3].TraversalCost=20;var route=Route(geometry);TestAssert.True(route.Success);TestAssert.True(route.Corridor.Length>4); }
        public static void NoRouteNeverCreatesStraightAutoPath() { var geometry=Grid(true);foreach(var tri in geometry.Triangles)if(tri.Center.Z>20)tri.Enabled=false;var route=Route(geometry);TestAssert.False(route.Success);TestAssert.Equal(0,route.Waypoints.Length); }
        public static void StaleGenerationRejectsRoute() { var geometry=Grid(false);geometry.Generation=2;TestAssert.Equal(RouteFailure.StaleContext,Route(geometry).Failure); }
        public static void NativePlayerAttributesPermitNpcOnlyRestriction()
        {
            var geometry=Grid(false);geometry.AttributeMask=0xff;
            foreach(var tri in geometry.Triangles)if(tri.Center.X>40)tri.Attributes=0x40;
            TestAssert.True(Route(geometry).Success);
            foreach(var tri in geometry.Triangles)if(tri.Center.X>40)tri.Attributes=0x80;
            TestAssert.False(Route(geometry).Success);
            geometry.BlockedAttributes=0x40;
            TestAssert.True(Route(geometry).Success);
            foreach(var tri in geometry.Triangles)if(tri.Center.X>40)tri.Attributes=0x40;
            TestAssert.False(Route(geometry).Success);
        }
        public static void ActorBeyondMeshBoundaryHasReachableTalkApproach()
        {
            var geometry=Grid(false);var context=TestFixtures.Context();context.Position=TestFixtures.P(10,10);
            var actor=new FieldTargetObservation{Id=new TargetIdentity(TargetKind.FieldActor,context.LogicalMapKey,10),Position=TestFixtures.P(65,10),Visible=true,IsPerson=true,HasInteraction=true};
            actor.ApproachCandidates=FieldActorApproaches.Create(context.Position,actor.Position,8,20);
            var catalog=new FieldTargetCatalog().Build(context,new[]{actor},null);
            var route=new FieldRoutePlanner().Plan(context,catalog.Targets[0],geometry);
            TestAssert.True(route.Success);TestAssert.True(route.ApproachPoint.X<60);
            float distance=NavigationPoint.Distance(route.ApproachPoint,actor.Position);
            TestAssert.True(distance>8 && distance<20);
        }
    }
}
