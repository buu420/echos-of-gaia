using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    public sealed class WorldGeometryContent
    {
        public readonly WorldGeometrySnapshot Geometry;
        private readonly WorldSurface[][] _blocks=new WorldSurface[480][];
        public WorldGeometryContent(WorldGeometrySnapshot geometry)
        {
            Geometry=geometry;if(geometry==null || !geometry.Complete)return;
            var blocks=new List<WorldSurface>[480];
            foreach(var surface in geometry.Surfaces){if(surface==null || surface.BlockId<0 || surface.BlockId>=480)return;
                var block=blocks[surface.BlockId];if(block==null)blocks[surface.BlockId]=block=new List<WorldSurface>();block.Add(surface);}
            for(int i=0;i<480;i++)_blocks[i]=blocks[i]==null?new WorldSurface[0]:blocks[i].ToArray();
        }
        public bool MatchesBlock(int block,WorldSurface[] surfaces)
        {
            if(block<0 || block>=480 || surfaces==null || _blocks[block]==null || _blocks[block].Length!=surfaces.Length)return false;
            var retained=_blocks[block];
            for(int i=0;i<retained.Length;i++){
                var a=retained[i];var b=surfaces[i];
                if(b==null || a.BlockId!=b.BlockId || a.MeshIndex!=b.MeshIndex || a.TriangleIndex!=b.TriangleIndex || a.TerrainId!=b.TerrainId || a.NormalY!=b.NormalY || b.Vertices==null || b.Vertices.Length!=3 || a.Vertices==null || a.Vertices.Length!=3)return false;
                for(int j=0;j<3;j++){var p=a.Vertices[j];var q=b.Vertices[j];if(!p.IsFinite || !q.IsFinite || p.X!=q.X || p.Y!=q.Y || p.Z!=q.Z)return false;}
            }
            return true;
        }
    }
}
