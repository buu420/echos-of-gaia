using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    public sealed class TransportRules
    {
        public uint LimitHigh,LimitLow;
        public int ControlMode,ControlType,CurrentTerrainId;
        public bool PreventGroundWaterCrossing,Flying;
        public float Step=0.5f;
    }
    public sealed class WorldSurface
    {
        public int Id,BlockId,MeshIndex,TriangleIndex,TerrainId;
        public float NormalY;
        public NavigationPoint[] Vertices;
        public NavigationPoint Center {get{return WorldCoordinates.Canonical(new NavigationPoint((Vertices[0].X+Vertices[1].X+Vertices[2].X)/3,(Vertices[0].Y+Vertices[1].Y+Vertices[2].Y)/3,(Vertices[0].Z+Vertices[1].Z+Vertices[2].Z)/3));}}
        public bool TryHeight(NavigationPoint point,out float height)
        {
            var a=Vertices[0];var b=Vertices[1];var c=Vertices[2];var d=WorldCoordinates.Difference(a,point);
            point=new NavigationPoint(a.X+d.X,point.Y,a.Z+d.Z);
            float cross=FieldTriangle.Cross(a,b,c);height=0;
            if(Math.Abs(cross)<0.00000001f)return false;
            float u=FieldTriangle.Cross(point,b,c)/cross,v=FieldTriangle.Cross(a,point,c)/cross,w=1-u-v;
            if(u< -0.00001f || v< -0.00001f || w< -0.00001f)return false;
            height=a.Y*u+b.Y*v+c.Y*w;return true;
        }
    }
    public sealed class PrivateWorldCache
    {
        private readonly int[] _entries=new int[10];private int _number;
        public PrivateWorldCache(int[] seed) {for(int i=0;i<10;i++)_entries[i]=seed!=null && i<seed.Length?seed[i]:-1;}
        public int At(int order) {return _entries[(_number+order)%10];}
        public void Add(int id) {_number=(_number+1)%10;_entries[_number]=id;}
    }
    public sealed class WorldGeometrySnapshot
    {
        private struct VertexKey : IEquatable<VertexKey>,IComparable<VertexKey>
        {
            private readonly double _x,_y,_z;
            internal VertexKey(NavigationPoint point){point=WorldCoordinates.Canonical(point);_x=Math.Round(point.X*256);_y=Math.Round(point.Y*256);_z=Math.Round(point.Z*256);}
            public bool Equals(VertexKey other){return _x.Equals(other._x) && _y.Equals(other._y) && _z.Equals(other._z);}
            public override bool Equals(object value){return value is VertexKey && Equals((VertexKey)value);}
            public override int GetHashCode(){return (_x.GetHashCode()*397^_y.GetHashCode())*397^_z.GetHashCode();}
            public int CompareTo(VertexKey other){int result=_x.CompareTo(other._x);if(result==0)result=_y.CompareTo(other._y);return result==0?_z.CompareTo(other._z):result;}
        }
        private struct EdgeKey : IEquatable<EdgeKey>
        {
            private readonly VertexKey _first,_second;
            internal EdgeKey(VertexKey first,VertexKey second){bool ordered=first.CompareTo(second)<=0;_first=ordered?first:second;_second=ordered?second:first;}
            public bool Equals(EdgeKey other){return _first.Equals(other._first) && _second.Equals(other._second);}
            public override bool Equals(object value){return value is EdgeKey && Equals((EdgeKey)value);}
            public override int GetHashCode(){return _first.GetHashCode()*397^_second.GetHashCode();}
        }
        // Most native edges have one or two faces. Keep those IDs in the value
        // itself rather than allocating a List and formatted coordinate strings.
        private struct EdgeFaces
        {
            internal int Count,First,Second;internal List<int> Additional;
            internal void Add(int id){if(Count==0)First=id;else if(Count==1)Second=id;else{if(Additional==null)Additional=new List<int>();Additional.Add(id);}Count++;}
            internal int At(int index){return index==0?First:index==1?Second:Additional[index-2];}
        }
        public readonly long Generation;public readonly string Fingerprint;public readonly bool Complete;
        public readonly TransportRules Rules;public readonly WorldSurface[] Surfaces;public readonly int[] NativeCache;
        private readonly Dictionary<int,WorldSurface> _byId=new Dictionary<int,WorldSurface>();
        private readonly Dictionary<int,List<WorldSurface>> _blocks=new Dictionary<int,List<WorldSurface>>();
        private readonly Dictionary<int,Dictionary<int,List<WorldSurface>>> _cells=new Dictionary<int,Dictionary<int,List<WorldSurface>>>();
        private readonly HashSet<int> _linearBlocks=new HashSet<int>();
        private readonly Dictionary<int,List<int>> _neighbors=new Dictionary<int,List<int>>();
        public WorldGeometrySnapshot(long generation,string fingerprint,WorldSurface[] surfaces,TransportRules rules,bool complete,int[] nativeCache=null)
            :this(generation,fingerprint,surfaces,rules,complete,nativeCache,null) { }
        internal WorldGeometrySnapshot(long generation,string fingerprint,WorldSurface[] surfaces,TransportRules rules,bool complete,int[] nativeCache,Func<bool> cancelled)
        {
            Generation=generation;Fingerprint=fingerprint;Complete=complete;
            Rules=CopyRules(rules);
            NativeCache=nativeCache==null?new int[0]:(int[])nativeCache.Clone();
            bool consumed=false;Surfaces=BuildSurfaces(()=>{if(consumed)return null;consumed=true;return surfaces;},true,cancelled);
        }
        // Only the preparation worker calls this constructor. Its provider
        // blocks for detached batches and returns null after native loading ends.
        internal WorldGeometrySnapshot(Func<WorldSurface[]> nextBatch,Func<bool> cancelled)
        {
            Generation=0;Fingerprint=null;Complete=true;Rules=null;NativeCache=new int[0];
            Surfaces=BuildSurfaces(nextBatch,false,cancelled);
        }
        private WorldSurface[] BuildSurfaces(Func<WorldSurface[]> nextBatch,bool clone,Func<bool> cancelled)
        {
            var values=new List<WorldSurface>();
            var edges=new Dictionary<EdgeKey,EdgeFaces>();
            WorldSurface[] surfaces;
            while((surfaces=nextBatch())!=null)for(int i=0;i<surfaces.Length;i++) {
                if((i&127)==0 && cancelled!=null && cancelled())throw new OperationCanceledException();
                var source=surfaces[i];var s=clone?new WorldSurface{Id=source.Id,BlockId=source.BlockId,MeshIndex=source.MeshIndex,TriangleIndex=source.TriangleIndex,TerrainId=source.TerrainId,NormalY=source.NormalY,Vertices=(NavigationPoint[])source.Vertices.Clone()}:source;
                values.Add(s);_byId[s.Id]=s;_neighbors[s.Id]=new List<int>();
                List<WorldSurface> block;if(!_blocks.TryGetValue(s.BlockId,out block))_blocks[s.BlockId]=block=new List<WorldSurface>();block.Add(s);
                IndexSurface(s);
                // A triangle's three endpoints are shared by its three edges.
                // Canonicalize and round each endpoint once, preserving exact keys.
                var first=new VertexKey(s.Vertices[0]);var second=new VertexKey(s.Vertices[1]);var third=new VertexKey(s.Vertices[2]);
                for(int edge=0;edge<3;edge++) {var key=edge==0?new EdgeKey(first,second):edge==1?new EdgeKey(second,third):new EdgeKey(third,first);
                    EdgeFaces joined;edges.TryGetValue(key,out joined);joined.Add(s.Id);edges[key]=joined;
                }
            }
            int count=0;
            foreach(var joined in edges.Values) {
                if((count++&127)==0 && cancelled!=null && cancelled())throw new OperationCanceledException();
                for(int i=0;i<joined.Count;i++)for(int j=0;j<joined.Count;j++){int a=joined.At(i),b=joined.At(j);if(a!=b && !_neighbors[a].Contains(b))_neighbors[a].Add(b);}
            }
            if(cancelled!=null && cancelled())throw new OperationCanceledException();
            return values.ToArray();
        }
        private static TransportRules CopyRules(TransportRules rules)
        {
            return rules==null?null:new TransportRules{LimitHigh=rules.LimitHigh,LimitLow=rules.LimitLow,ControlMode=rules.ControlMode,ControlType=rules.ControlType,CurrentTerrainId=rules.CurrentTerrainId,PreventGroundWaterCrossing=rules.PreventGroundWaterCrossing,Flying=rules.Flying,Step=rules.Step};
        }
        private WorldGeometrySnapshot(WorldGeometrySnapshot source,long generation,string fingerprint,TransportRules rules,int[] nativeCache)
        {
            Generation=generation;Fingerprint=fingerprint;Complete=source.Complete;
            Rules=CopyRules(rules);
            NativeCache=nativeCache==null?new int[0]:(int[])nativeCache.Clone();
            // These geometry/index collections are built once and never changed by
            // the native reader. Only live rules and cache need per-job ownership.
            Surfaces=source.Surfaces;_byId=source._byId;_blocks=source._blocks;_neighbors=source._neighbors;_cells=source._cells;_linearBlocks=source._linearBlocks;
        }
        private const int CellSize=8,Columns=192,Rows=160;
        private static int WrapCell(int value,int period){value%=period;return value<0?value+period:value;}
        private static int SpatialCell(NavigationPoint point){point=WorldCoordinates.Canonical(point);return WrapCell((int)(-point.Z/CellSize),Rows)*Columns+WrapCell((int)(point.X/CellSize),Columns);}
        private void IndexSurface(WorldSurface surface)
        {
            if(_linearBlocks.Contains(surface.BlockId))return;
            float minX=float.MaxValue,maxX=float.MinValue,minZ=float.MaxValue,maxZ=float.MinValue;
            foreach(var point in surface.Vertices){minX=Math.Min(minX,point.X);maxX=Math.Max(maxX,point.X);minZ=Math.Min(minZ,point.Z);maxZ=Math.Max(maxZ,point.Z);}
            // TryHeight permits small negative barycentric weights. Include that
            // fringe, both adjacent cells at boundaries, and wrapped end cells.
            float margin=0.0001f+Math.Max(maxX-minX,maxZ-minZ)*0.00004f;
            if(float.IsNaN(margin) || float.IsInfinity(margin) || Math.Abs(minX)>WorldCoordinates.Width*3 || Math.Abs(maxX)>WorldCoordinates.Width*3 || Math.Abs(minZ)>WorldCoordinates.Depth*3 || Math.Abs(maxZ)>WorldCoordinates.Depth*3){_linearBlocks.Add(surface.BlockId);_cells.Remove(surface.BlockId);return;}
            int firstX=(int)Math.Floor((minX-margin)/CellSize),lastX=(int)Math.Floor((maxX+margin)/CellSize),firstZ=(int)Math.Floor((-maxZ-margin)/CellSize),lastZ=(int)Math.Floor((-minZ+margin)/CellSize);
            if((long)(lastX-firstX+1)*(lastZ-firstZ+1)>256){_linearBlocks.Add(surface.BlockId);_cells.Remove(surface.BlockId);return;}
            Dictionary<int,List<WorldSurface>> cells;if(!_cells.TryGetValue(surface.BlockId,out cells))_cells[surface.BlockId]=cells=new Dictionary<int,List<WorldSurface>>();
            for(int z=firstZ;z<=lastZ;z++)for(int x=firstX;x<=lastX;x++) {
                int key=WrapCell(z,Rows)*Columns+WrapCell(x,Columns);List<WorldSurface> values;
                if(!cells.TryGetValue(key,out values))cells[key]=values=new List<WorldSurface>();values.Add(surface);
            }
        }
        public WorldGeometrySnapshot ForPlanning(){return ForContext(Generation,Fingerprint,Rules,NativeCache);}
        public WorldGeometrySnapshot ForContext(long generation,string fingerprint,TransportRules rules,int[] nativeCache){return new WorldGeometrySnapshot(this,generation,fingerprint,rules,nativeCache);}
        public WorldSurface Find(int id) {WorldSurface value;return _byId.TryGetValue(id,out value)?value:null;}
        public IList<int> Neighbors(int id) {List<int> value;return _neighbors.TryGetValue(id,out value)?value:new List<int>();}
        public static int Cell(NavigationPoint point) {point=WorldCoordinates.Canonical(point);return (int)(-point.Z/32)*48+(int)(point.X/32);}
        private static int Block(NavigationPoint point) {point=WorldCoordinates.Canonical(point);return ((int)(-point.Z/64)%20)*24+(int)(point.X/64);}
        public WorldSurface Probe(NavigationPoint position,int previousId,PrivateWorldCache cache)
        {
            if(Rules==null || !position.IsFinite)return null;
            int blockId=Block(position),topography=WorldTerrainRules.Topography(previousId);
            float origin=position.Y+(Rules.Flying?400:2.34375f),height;
            bool useCache=(topography!=49 && topography!=52) || Rules.ControlType==1;
            if(useCache && cache!=null)for(int i=0;i<10;i++) {var s=Find(cache.At(i));if(s!=null && s.BlockId==blockId && (s.TerrainId!=0x31ee || Rules.ControlType==1) && s.TryHeight(position,out height) && height<=origin)return s;}
            List<WorldSurface> block;
            if(_linearBlocks.Contains(blockId)){if(!_blocks.TryGetValue(blockId,out block))return null;}
            else {Dictionary<int,List<WorldSurface>> cells;if(!_cells.TryGetValue(blockId,out cells) || !cells.TryGetValue(SpatialCell(position),out block))return null;}
            int rejectedMesh=-1;
            foreach(var s in block) {
                if(s.MeshIndex==rejectedMesh)continue;
                bool excluded=s.TerrainId==4078 || s.TerrainId==4088 || s.TerrainId==2040 || s.NormalY<=0.1f;
                if(excluded && !Rules.Flying || !s.TryHeight(position,out height) || height>origin)continue;
                // Native WMBlock rejects the mesh after its first hit on this special ID.
                if(s.TerrainId==0x31ee && Rules.ControlType!=1){rejectedMesh=s.MeshIndex;continue;}
                if(cache!=null)cache.Add(s.Id);return s;
            }
            return null;
        }
        internal IList<WorldSurface> MarkerCellSurfaces(NavigationPoint marker)
        {
            int nativeCell=Cell(marker);float x=(nativeCell%48)*32,z=-(nativeCell/48)*32;
            int blockId=Block(new NavigationPoint(x+16,0,z-16));List<WorldSurface> block;
            if(_linearBlocks.Contains(blockId))return _blocks.TryGetValue(blockId,out block)?block:new List<WorldSurface>();
            var result=new List<WorldSurface>();var seen=new HashSet<int>();Dictionary<int,List<WorldSurface>> cells;
            if(!_cells.TryGetValue(blockId,out cells))return result;
            for(int row=0;row<4;row++)for(int column=0;column<4;column++) {
                int cell=SpatialCell(new NavigationPoint(x+column*8+4,0,z-row*8-4));
                if(cells.TryGetValue(cell,out block))foreach(var surface in block)if(seen.Add(surface.Id))result.Add(surface);
            }return result;
        }
        public bool Segment(NavigationPoint from,NavigationPoint to,WorldSurface starting,PrivateWorldCache cache,int startCell,int goalCell,out WorldSurface end,Func<bool> cancelled=null)
        {
            end=starting;if(starting==null || Rules==null)return false;
            var delta=WorldCoordinates.Difference(from,to);float distance=WorldCoordinates.Distance(from,to);
            int count=Math.Max(1,(int)Math.Ceiling(distance/Math.Max(1f/256,Math.Min(0.5f,Rules.Step))));
            float y=from.Y;
            for(int step=1;step<=count;step++) {
                if(cancelled!=null && cancelled())return false;
                var point=WorldCoordinates.Canonical(new NavigationPoint(from.X+delta.X*step/count,y,from.Z+delta.Z*step/count));
                var next=Probe(point,WorldTerrainRules.Remap(Rules,end.TerrainId),cache);
                if(next==null || !WorldTerrainRules.CanTraverse(Rules,end,next))return false;
                int cell=Cell(point);if((next.TerrainId&0xc000)!=0 && cell!=startCell && cell!=goalCell)return false;
                float height;if(!next.TryHeight(point,out height))return false;
                if(!Rules.Flying)y=height;end=next;
            }
            return true;
        }
    }
}
