using System;
using System.Collections.Generic;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class WorldGeometryIndexTests
    {
        public static void SharedEdgesPreserveQuantizationNonmanifoldAndWrappedHeightLayers()
        {
            var surfaces=new[]{
                new WorldSurface{Id=17,Vertices=new[]{new NavigationPoint(0,0,0),new NavigationPoint(10,0,0),new NavigationPoint(0,0,-10)}},
                new WorldSurface{Id=21,Vertices=new[]{new NavigationPoint(10.0009f,0,0),new NavigationPoint(0.0009f,0,0),new NavigationPoint(10,0,-10)}},
                new WorldSurface{Id=42,Vertices=new[]{new NavigationPoint(0,0,0),new NavigationPoint(10,0,0),new NavigationPoint(5,0,10)}},
                new WorldSurface{Id=51,Vertices=new[]{new NavigationPoint(0,0.01f,0),new NavigationPoint(10,0.01f,0),new NavigationPoint(0,0.01f,-10)}},
                new WorldSurface{Id=71,Vertices=new[]{new NavigationPoint(1536,0,0),new NavigationPoint(1546,0,0),new NavigationPoint(1540,0,-10)}}
            };
            var geometry=new WorldGeometrySnapshot(1,"joined edges",surfaces,new TransportRules(),true);
            foreach(int id in new[]{17,21,42,71}) {
                var neighbors=geometry.Neighbors(id);TestAssert.Equal(3,neighbors.Count);
                foreach(int other in new[]{17,21,42,71})if(other!=id)TestAssert.True(neighbors.Contains(other));
                TestAssert.True(!neighbors.Contains(51));
            }
            TestAssert.Equal(0,geometry.Neighbors(51).Count);
        }
        public static void RoundedCanonicalSeamStillFindsTheNativeSurface()
        {
            var surface=new WorldSurface{Id=17,BlockId=0,MeshIndex=0,TerrainId=0,NormalY=1,Vertices=new[]{new NavigationPoint(0,0,0),new NavigationPoint(10,0,0),new NavigationPoint(0,0,-10)}};
            var geometry=new WorldGeometrySnapshot(1,"seam-rounding",new[]{surface},new TransportRules{LimitLow=1,Step=0.5f},true);
            foreach(float z in new[]{0.00001f,0.00002f,0f,-0.00001f}) {
                var point=new NavigationPoint(1,0,z);var expected=Linear(geometry,point,0,new PrivateWorldCache(null));
                TestAssert.True(expected!=null);TestAssert.Equal(17,expected.Id);
                var actual=geometry.Probe(point,0,new PrivateWorldCache(null));
                TestAssert.True(actual!=null);TestAssert.Equal(17,actual.Id);
            }
        }
        private static WorldSurface Linear(WorldGeometrySnapshot geometry,NavigationPoint position,int previous,PrivateWorldCache cache)
        {
            var p=WorldCoordinates.Canonical(position);int block=(int)(-p.Z/64)%20*24+(int)(p.X/64);float height,origin=position.Y+(geometry.Rules.Flying?400:2.34375f);
            int topography=WorldTerrainRules.Topography(previous);
            if((topography!=49 && topography!=52) || geometry.Rules.ControlType==1)for(int i=0;i<10;i++) {
                var s=geometry.Find(cache.At(i));if(s!=null && s.BlockId==block && (s.TerrainId!=0x31ee || geometry.Rules.ControlType==1) && s.TryHeight(position,out height) && height<=origin)return s;
            }
            int rejected=-1;
            foreach(var s in geometry.Surfaces) {
                if(s.BlockId!=block || s.MeshIndex==rejected)continue;
                bool excluded=s.TerrainId==4078 || s.TerrainId==4088 || s.TerrainId==2040 || s.NormalY<=0.1f;
                if(excluded && !geometry.Rules.Flying || !s.TryHeight(position,out height) || height>origin)continue;
                if(s.TerrainId==0x31ee && geometry.Rules.ControlType!=1){rejected=s.MeshIndex;continue;}
                cache.Add(s.Id);return s;
            }
            return null;
        }
        private static WorldSurface Triangle(int id,int mesh,int terrain,float x,float z,float y=0)
        {
            return new WorldSurface{Id=id,BlockId=0,MeshIndex=mesh,TriangleIndex=id,TerrainId=terrain,NormalY=1,
                Vertices=new[]{new NavigationPoint(x,y,z),new NavigationPoint(x+10,y,z),new NavigationPoint(x,y,z-10)}};
        }
        public static void LookupMatchesNativeOrderLayersSpecialMeshRejectionAndCache()
        {
            var all=new List<WorldSurface>{Triangle(10000,0,0x31ee,8,-8),Triangle(10001,0,0,8,-8),Triangle(10002,1,0,8,-8,-3),Triangle(10003,2,0,8,-8)};
            all.AddRange(WorldGeometryPerformanceTests.DenseSurfaces(32));
            foreach(bool flying in new[]{false,true})foreach(int control in new[]{0,1}) {
                var geometry=new WorldGeometrySnapshot(1,"ordered index",all.ToArray(),new TransportRules{LimitLow=1,ControlType=control,Flying=flying},true);
                foreach(int seed in new[]{-1,10003,10002})foreach(int previous in new[]{0,52*4})for(int z=0;z<64;z+=3)for(int x=0;x<64;x+=3) {
                    var a=new PrivateWorldCache(new[]{seed});var b=new PrivateWorldCache(new[]{seed});var point=TestFixtures.P(x+0.125f,-z-0.125f);
                    var expected=Linear(geometry,point,previous,a);var actual=geometry.Probe(point,previous,b);
                    TestAssert.Equal(expected==null?-1:expected.Id,actual==null?-1:actual.Id);
                    for(int i=0;i<10;i++)TestAssert.Equal(a.At(i),b.At(i));
                }
            }
        }
        public static void LookupPreservesToleranceAtCellBoundariesAndBothPeriodicSeams()
        {
            var surfaces=new[]{Triangle(0,0,0,8,0),Triangle(1,1,0,1534,-1),Triangle(2,2,0,1,-1278)};
            var geometry=new WorldGeometrySnapshot(1,"periodic tolerance",surfaces,new TransportRules{LimitLow=1},true);
            foreach(var position in new[]{TestFixtures.P(7.99997f,-1),TestFixtures.P(1,-2),TestFixtures.P(2,-1)}) {
                var expected=Linear(geometry,position,0,new PrivateWorldCache(null));var actual=geometry.Probe(position,0,new PrivateWorldCache(null));
                TestAssert.True(expected!=null);TestAssert.Equal(expected.Id,actual==null?-1:actual.Id);
            }
        }
    }
}
