using System;
using System.Diagnostics;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class WorldGeometryPerformanceTests
    {
        internal static WorldSurface[] DenseSurfaces(int side)
        {
            var result=new WorldSurface[side*side*2];float size=64f/side;int id=0;
            for(int z=0;z<side;z++)for(int x=0;x<side;x++)for(int half=0;half<2;half++) {
                float left=x*size,right=(x+1)*size,top=-z*size,bottom=-(z+1)*size;
                result[id]=new WorldSurface{Id=id,BlockId=0,MeshIndex=0,TriangleIndex=id,TerrainId=0,NormalY=1,
                    Vertices=half==0?new[]{TestFixtures.P(left,top),TestFixtures.P(right,top),TestFixtures.P(left,bottom)}:new[]{TestFixtures.P(right,top),TestFixtures.P(right,bottom),TestFixtures.P(left,bottom)}};id++;
            }
            return result;
        }
        public static void DenseWorldLookupDoesNotScanTheWholeBlockForEverySample()
        {
            var build=Stopwatch.StartNew();var geometry=new WorldGeometrySnapshot(1,"dense native order",DenseSurfaces(256),new TransportRules{LimitLow=1,Step=0.5f},true);build.Stop();
            var position=TestFixtures.P(63.9f,-63.9f);var expected=geometry.Probe(position,0,null);TestAssert.True(expected!=null);
            var lookup=Stopwatch.StartNew();
            for(int i=0;i<2000;i++)TestAssert.Equal(expected.Id,geometry.Probe(position,0,null).Id);
            lookup.Stop();Console.WriteLine("World probe benchmark: 131072 triangles; build="+build.ElapsedMilliseconds+" ms; 2000 uncached probes="+lookup.ElapsedMilliseconds+" ms");
            if(lookup.ElapsedMilliseconds>=500)throw new InvalidOperationException("Dense world probes exceeded the 500 ms regression ceiling: "+lookup.ElapsedMilliseconds+" ms");
        }
        public static void WorldPreparationDoesNotAllocateFormattedEdgesForEveryTriangle()
        {
            var surfaces=DenseSurfaces(256);
            long before=GC.GetAllocatedBytesForCurrentThread();var timer=Stopwatch.StartNew();
            var geometry=new WorldGeometrySnapshot(1,"allocation ceiling",surfaces,new TransportRules{LimitLow=1},true);
            timer.Stop();long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
            Console.WriteLine("World geometry allocation benchmark: 131072 faces; allocated="+allocated+" bytes; build="+timer.ElapsedMilliseconds+" ms");
            TestAssert.True(geometry.Find(131071)!=null);
            if(allocated>128L*1024*1024)throw new InvalidOperationException("World preparation exceeded its 128 MiB allocation ceiling: "+allocated+" bytes");
        }
    }
}
