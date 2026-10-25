using System;
using FF9Access.Tests;
using Memoria.Accessibility.Navigation;
public static class WorldRoutesTests
{
    private static TransportRules Foot() { return new TransportRules { LimitHigh=0x0010667F,LimitLow=0xD8FF3CFF,Step=0.5f }; }
    private static WorldSurface Surface(int id,int terrain,float x=0,float z=0,float y=0)
    { return new WorldSurface { Id=id,BlockId=0,MeshIndex=0,TriangleIndex=id,TerrainId=terrain,NormalY=1,Vertices=new[]{new NavigationPoint(x,y,z),new NavigationPoint(x+10,y,z),new NavigationPoint(x,y,z-10)} }; }
    private static NavigationContext Context() {var c=TestFixtures.Context("world:0");c.Position=new NavigationPoint(1,0,-1);return c;}
    private static NavigationTarget Target() {return new NavigationTarget{Id=new TargetIdentity(TargetKind.RegionPoint,"world:0",1),Generation=1,Position=new NavigationPoint(2,0,-2)};}
    public static void WalkingCannotCrossOcean() {TestAssert.False(WorldTerrainRules.CanTraverse(Foot(),Surface(0,0),Surface(1,57*4)));}
    public static void BoatCannotCrossLand() {var boat=new TransportRules{ControlMode=7,ControlType=2,LimitHigh=0x02600000};TestAssert.False(WorldTerrainRules.CanTraverse(boat,Surface(0,57*4),Surface(1,0)));}
    public static void ChocoboCapabilitiesUseNativeMasks() {var chocobo=new TransportRules{LimitHigh=1u<<4,LimitLow=1u<<7};TestAssert.True(WorldTerrainRules.CanTraverse(chocobo,Surface(0,7*4),Surface(1,36*4)));TestAssert.False(WorldTerrainRules.CanTraverse(chocobo,Surface(0,7*4),Surface(1,35*4)));}
    public static void WrappedPortalsJoinWorldSeams()
    {
        var a=Surface(0,0,1526,-10);var b=Surface(1,0,0,-10);
        a.Vertices=new[]{TestFixtures.P(1526,-10),TestFixtures.P(1536,-10),TestFixtures.P(1536,-20)};
        b.Vertices=new[]{TestFixtures.P(0,-10),TestFixtures.P(10,-10),TestFixtures.P(0,-20)};
        var g=new WorldGeometrySnapshot(1,"forms-a",new[]{a,b},Foot(),true);
        TestAssert.True(g.Neighbors(a.Id).Contains(b.Id));
    }
    public static void NarrowBeachMatchesNativePointProbe()
    {
        var g=new WorldGeometrySnapshot(1,"a",new[]{Surface(0,0)},Foot(),true);
        var r=new WorldRoutePlanner().Plan(Context(),Target(),g);TestAssert.True(r.Success);
    }
    public static void MissingBlockDoesNotInventRoute() {var g=new WorldGeometrySnapshot(1,"a",new[]{Surface(0,0)},Foot(),false);TestAssert.Equal(RouteFailure.TerrainUnavailable,new WorldRoutePlanner().Plan(Context(),Target(),g).Failure);}
    public static void FormChangeInvalidatesRoute() {var g=new WorldGeometrySnapshot(2,"b",new[]{Surface(0,0)},Foot(),true);TestAssert.Equal(RouteFailure.StaleContext,new WorldRoutePlanner().Plan(Context(),Target(),g).Failure);}
    public static void PrivateReadDoesNotMutateSceneCache()
    {
        var cache=new[]{0};var g=new WorldGeometrySnapshot(1,"a",new[]{Surface(0,0)},Foot(),true,cache);
        new WorldRoutePlanner().Plan(Context(),Target(),g);TestAssert.Equal(0,cache[0]);TestAssert.Equal(0,g.NativeCache[0]);
    }
    public static void ProbePreservesOrderedSurfaceAndBridgeBypass()
    {
        var a=Surface(0,0,y:-3);var b=Surface(1,4,y:0);
        var g=new WorldGeometrySnapshot(1,"a",new[]{a,b},Foot(),true,new[]{1});
        TestAssert.Equal(1,g.Probe(Context().Position,0,new PrivateWorldCache(g.NativeCache)).Id);
        TestAssert.Equal(0,g.Probe(Context().Position,52*4,new PrivateWorldCache(g.NativeCache)).Id);
    }
    public static void NativeRayDistanceIsNotInvented()
    {
        var g=new WorldGeometrySnapshot(1,"a",new[]{Surface(0,0,y:-20)},Foot(),true);
        TestAssert.Equal(0,g.Probe(Context().Position,0,new PrivateWorldCache(g.NativeCache)).Id);
    }
    public static void NearestReachableObservedPointIsSelected()
    {
        var target=Target();target.ApproachCandidates=new[]{new NavigationPoint(12,0,-1),target.Position};
        var g=new WorldGeometrySnapshot(1,"a",new[]{Surface(0,0)},Foot(),true);
        var r=new WorldRoutePlanner().Plan(Context(),target,g);TestAssert.True(r.Success);TestAssert.Near(2,r.ApproachPoint.X,0);
    }
    public static void TransportInvalidDirectLegDetoursThroughValidTerrain()
    {
        var field=FieldRoutesTests.Grid(false);var surfaces=new WorldSurface[field.Triangles.Length];
        for(int i=0;i<surfaces.Length;i++) {var tri=field.Triangles[i];var points=new NavigationPoint[3];for(int v=0;v<3;v++)points[v]=new NavigationPoint(tri.Vertices[v].X,0,-tri.Vertices[v].Z);
            surfaces[i]=new WorldSurface{Id=i,BlockId=0,TriangleIndex=i,TerrainId=tri.Center.X>20 && tri.Center.X<40 && tri.Center.Z<20?57*4:0,NormalY=1,Vertices=points};}
        var geometry=new WorldGeometrySnapshot(1,"a",surfaces,Foot(),true);var context=Context();context.Position=TestFixtures.P(10,-10);var target=Target();target.Position=TestFixtures.P(50,-10);
        var route=new WorldRoutePlanner().Plan(context,target,geometry);TestAssert.True(route.Success);TestAssert.True(route.Waypoints.Length>1);
        var from=context.Position;var hit=geometry.Probe(from,0,new PrivateWorldCache(null));var cache=new PrivateWorldCache(null);
        foreach(var point in route.Waypoints){WorldSurface next;TestAssert.True(geometry.Segment(from,point,hit,cache,WorldGeometrySnapshot.Cell(context.Position),-1,out next));from=point;hit=next;}
    }
    public static void PassiveGuidanceRechecksFreshPositionAgainstTerrain()
    {
        var field=FieldRoutesTests.Grid(false);var surfaces=new WorldSurface[field.Triangles.Length];
        for(int i=0;i<surfaces.Length;i++) {var triangle=field.Triangles[i];var points=new NavigationPoint[3];for(int v=0;v<3;v++)points[v]=new NavigationPoint(triangle.Vertices[v].X,0,-triangle.Vertices[v].Z);
            surfaces[i]=new WorldSurface{Id=i,BlockId=0,TriangleIndex=i,TerrainId=triangle.Center.X>20 && triangle.Center.X<40 && triangle.Center.Z<20?57*4:0,NormalY=1,Vertices=points};}
        var geometry=new WorldGeometrySnapshot(1,"same terrain",surfaces,Foot(),true);var context=Context();var target=Target();target.Position=TestFixtures.P(50,-10);
        context.Position=TestFixtures.P(10,-30);
        TestAssert.True(GuidanceRouteValidation.WorldLeg(geometry,context,TestFixtures.P(50,-30),target,target.Position));
        context.Position=TestFixtures.P(10,-10);
        TestAssert.False(GuidanceRouteValidation.WorldLeg(geometry,context,TestFixtures.P(50,-10),target,target.Position));
        TestAssert.False(GuidanceRouteValidation.WorldLeg(null,context,target.Position,target,target.Position));
        TestAssert.Equal("same terrain",geometry.Fingerprint);
    }
}
