using System;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    public static class WorldGeometryContentTests
    {
        private static WorldSurface Surface(int id,int mesh=0,int triangle=0)
        {
            return new WorldSurface{Id=id,BlockId=0,MeshIndex=mesh,TriangleIndex=triangle,TerrainId=4,NormalY=1,
                Vertices=new[]{new NavigationPoint(0,0,0),new NavigationPoint(10,0,0),new NavigationPoint(0,0,-10)}};
        }
        public static void ReloadRequiresExactOrderedContentRatherThanCountsOrNewIds()
        {
            var content=new WorldGeometryContent(new WorldGeometrySnapshot(1,"old",new[]{Surface(17),Surface(18,1)},null,true));
            TestAssert.True(content.MatchesBlock(0,new[]{Surface(99),Surface(100,1)}));
            TestAssert.False(content.MatchesBlock(0,new[]{Surface(99)}));
            TestAssert.False(content.MatchesBlock(0,new[]{Surface(99,1),Surface(100)}));
            var changed=Surface(99);changed.Vertices[2]=new NavigationPoint(0,0.000001f,-10);
            TestAssert.False(content.MatchesBlock(0,new[]{changed,Surface(100,1)}));
            changed=Surface(99);changed.TerrainId=8;TestAssert.False(content.MatchesBlock(0,new[]{changed,Surface(100,1)}));
            changed=Surface(99);changed.NormalY=0.99f;TestAssert.False(content.MatchesBlock(0,new[]{changed,Surface(100,1)}));
            changed=Surface(99);changed.TriangleIndex=1;TestAssert.False(content.MatchesBlock(0,new[]{changed,Surface(100,1)}));
            TestAssert.False(content.MatchesBlock(1,new[]{Surface(99),Surface(100,1)}));
            TestAssert.True(content.MatchesBlock(479,new WorldSurface[0]));
        }
        public static void MissingPartialOrMalformedContentNeverValidatesReload()
        {
            TestAssert.False(new WorldGeometryContent(null).MatchesBlock(0,new WorldSurface[0]));
            TestAssert.False(new WorldGeometryContent(new WorldGeometrySnapshot(1,null,new[]{Surface(17)},null,false)).MatchesBlock(0,new[]{Surface(17)}));
            var content=new WorldGeometryContent(new WorldGeometrySnapshot(1,null,new[]{Surface(17)},null,true));
            TestAssert.False(content.MatchesBlock(0,null));TestAssert.False(content.MatchesBlock(-1,new WorldSurface[0]));
            TestAssert.False(content.MatchesBlock(480,new WorldSurface[0]));
        }
    }
}
