using System;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class NavigationAuditTests
    {
        private static NavigationPoint P(float x,float z,float y=0){return new NavigationPoint(x,y,z);}
        private static FieldGeometrySnapshot Floor(){return new FieldGeometrySnapshot{Generation=1,ActiveTriangle=0,Radius=80,Triangles=new[]{new FieldTriangle{Id=0,Vertices=new[]{P(0,0),P(1000,0),P(0,1000)},Neighbors=new[]{-1,1,-1}},new FieldTriangle{Id=1,Vertices=new[]{P(1000,0),P(1000,1000),P(0,1000)},Neighbors=new[]{-1,-1,0}}}};}
        public static void RaisedTalkableActorUsesRealFloorWithinNativeHeightRange()
        {
            var context=TestFixtures.Context("field:raised");context.Position=P(200,200);
            var target=new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldActor,context.LogicalMapKey,9),Generation=1,IsInteractive=true,Position=P(500,500,200)};
            target.ApproachCandidates=FieldActorApproaches.Create(context.Position,target.Position,160,540);
            var geometry=Floor();var route=new FieldRoutePlanner().Plan(context,target,geometry);
            TestAssert.True(route.Success);TestAssert.Near(0,route.ApproachPoint.Y,0.01f);
            var last=context.Position;foreach(var point in route.Waypoints){TestAssert.True(geometry.IsSegmentClear(last,point));last=point;}
            target.Position=P(500,500,400);target.ApproachCandidates=FieldActorApproaches.Create(context.Position,target.Position,160,540);
            TestAssert.False(new FieldRoutePlanner().Plan(context,target,geometry).Success);
        }
        public static void PreviouslySeenExitRemainsAtObservedApproachAfterScroll()
        {
            var context=TestFixtures.Context();var catalog=new FieldTargetCatalog();
            var observation=new FieldTargetObservation{Id=new TargetIdentity(TargetKind.FieldExit,context.LogicalMapKey,20),Position=P(100,100),NativePresentationIdentity=123,Exit=new ExitPresentation{Visible=true,ApproachEnabled=true,Approach=P(100,100),Evidence=VisibilityEvidence.VisibleWalkmeshEdge}};
            var first=catalog.Build(context,new[]{observation},null);TestAssert.Equal(1,first.Targets.Count);
            observation.OutOfView=true;observation.Exit=null;observation.Position=P(900,900);
            var remembered=catalog.Build(context,new[]{observation},null);TestAssert.Equal(1,remembered.Targets.Count);
            TestAssert.Equal(VisibilityEvidence.ObservedFieldLandmark,remembered.Targets[0].Evidence);TestAssert.Near(100,remembered.Targets[0].ApproachCandidates[0].X,0);
            observation.NativePresentationIdentity=124;TestAssert.Equal(0,catalog.Build(context,new[]{observation},null).Targets.Count);
        }
        public static void CachedNativeWorldStartKeepsReachableDestinations()
        {
            var context=TestFixtures.Context("world:1");context.Position=P(4,-4,-50);
            var faces=new[]{new WorldSurface{Id=0,BlockId=0,MeshIndex=0,TriangleIndex=0,NormalY=(float)(1/Math.Sqrt(1+12.5*12.5)),Vertices=new[]{P(0,0),P(16,0,-200),P(0,-16)}},new WorldSurface{Id=1,BlockId=0,MeshIndex=0,TriangleIndex=1,NormalY=(float)(1/Math.Sqrt(1+6.25*6.25*2)),Vertices=new[]{P(16,0,-200),P(16,-16,-100),P(0,-16)}}};
            var geometry=new WorldGeometrySnapshot(1,"cached",faces,new TransportRules{LimitLow=1,Step=0.5f},true,new[]{0});
            var target=new NavigationTarget{Id=new TargetIdentity(TargetKind.RegionPoint,context.LogicalMapKey,1),Generation=1,Category=NavigationCategory.Regions,Position=P(12,-12,-100),Evidence=VisibilityEvidence.ObservedRegion};
            TestAssert.True(new WorldRoutePlanner().Plan(context,target,geometry).Success);
            var input=new CatalogResult();input.Targets.Add(target);
            TestAssert.Equal(1,WorldTargetCatalog.Filter(context,input,geometry,WorldReachabilityIndex.Build(geometry)).Targets.Count);
            var uncached=geometry.ForContext(1,"uncached",geometry.Rules,null);
            TestAssert.Equal(0,WorldTargetCatalog.Filter(context,input,uncached,WorldReachabilityIndex.Build(uncached)).Targets.Count);
        }
    }
}
