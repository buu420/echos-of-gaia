using System;
using System.Collections.Generic;
using System.Threading;
namespace Memoria.Accessibility.Navigation
{
    public sealed class WorldReachabilityIndex
    {
        private readonly WorldGeometrySnapshot _source;
        private readonly int[] _labels;
        private readonly Dictionary<int,int> _sparse;
        private sealed class Marker {internal NavigationPoint Position;internal NavigationPoint[] Approaches;}
        private readonly Dictionary<TargetIdentity,Marker> _markers=new Dictionary<TargetIdentity,Marker>();
        internal WorldSurface[] SourceSurfaces{get{return _source.Surfaces;}}
        private WorldReachabilityIndex(WorldGeometrySnapshot geometry)
        {
            _source=geometry.ForPlanning();int maximum=-1;
            foreach(var surface in geometry.Surfaces)maximum=Math.Max(maximum,surface.Id);
            // Dense native IDs keep this index at four bytes per face. Sparse
            // detached fixtures must not request an unbounded array allocation.
            if(maximum>=0 && maximum<geometry.Surfaces.Length*4L)_labels=new int[maximum+1];
            else _sparse=new Dictionary<int,int>();
        }
        private int Label(int id){int value;return _labels!=null?(id>=0 && id<_labels.Length?_labels[id]:-1):_sparse.TryGetValue(id,out value)?value:0;}
        private void Set(int id,int value){if(_labels!=null){if(id>=0 && id<_labels.Length)_labels[id]=value;}else _sparse[id]=value;}
        internal static bool SameProfile(WorldGeometrySnapshot a,WorldGeometrySnapshot b)
        {
            if(a==null || b==null || !System.Object.ReferenceEquals(a.Surfaces,b.Surfaces) || a.Rules==null || b.Rules==null)return false;
            var x=a.Rules;var y=b.Rules;
            return x.LimitHigh==y.LimitHigh && x.LimitLow==y.LimitLow && x.ControlMode==y.ControlMode && x.ControlType==y.ControlType && x.PreventGroundWaterCrossing==y.PreventGroundWaterCrossing && x.Flying==y.Flying;
        }
        public bool Matches(WorldGeometrySnapshot geometry){return geometry!=null && geometry.Complete && SameProfile(_source,geometry);}
        private static bool Terminal(WorldSurface surface){return (surface.TerrainId&0xc000)!=0;}
        private static bool Allowed(WorldGeometrySnapshot geometry,WorldSurface surface)
        {
            if(surface==null || surface.Id<0)return false;
            if(!geometry.Rules.Flying && (surface.NormalY<=0.1f || surface.TerrainId==4078 || surface.TerrainId==4088 || surface.TerrainId==2040))return false;
            if(surface.TerrainId==0x31ee && geometry.Rules.ControlType!=1)return false;
            return WorldTerrainRules.CanTraverse(geometry.Rules,surface,surface);
        }
        public static WorldReachabilityIndex Build(WorldGeometrySnapshot geometry,Func<bool> cancelled=null)
        {
            if(geometry==null || !geometry.Complete || geometry.Rules==null || geometry.Surfaces.Length==0)return null;
            var result=new WorldReachabilityIndex(geometry);var pending=new Queue<int>();int component=0,steps=0;
            foreach(var surface in geometry.Surfaces) {
                if((steps++&127)==0 && cancelled!=null && cancelled())throw new OperationCanceledException();
                if(result.Label(surface.Id)!=0)continue;
                if(!Allowed(geometry,surface) || Terminal(surface)){result.Set(surface.Id,-1);continue;}
                result.Set(surface.Id,++component);pending.Enqueue(surface.Id);
                while(pending.Count!=0) {
                    if((steps++&127)==0 && cancelled!=null && cancelled())throw new OperationCanceledException();
                    var from=geometry.Find(pending.Dequeue());
                    var neighbors=geometry.Neighbors(from.Id);
                    for(int n=0;n<neighbors.Count;n++) {int id=neighbors[n];
                        if(result.Label(id)!=0)continue;var to=geometry.Find(id);
                        if(!Allowed(geometry,to) || Terminal(to)){result.Set(id,-1);continue;}
                        if(!WorldTerrainRules.CanTraverse(geometry.Rules,from,to) || !WorldTerrainRules.CanTraverse(geometry.Rules,to,from))continue;
                        result.Set(id,component);pending.Enqueue(id);
                    }
                }
            }
            if(cancelled!=null && cancelled())throw new OperationCanceledException();return result;
        }
        internal bool CanReach(WorldGeometrySnapshot geometry,WorldSurface surface,int cell,HashSet<int> origin)
        {
            if(surface==null || !Allowed(geometry,surface))return false;int label=Label(surface.Id);
            if(label>0)return origin.Contains(label);
            foreach(int component in ComponentsAt(geometry,surface,cell))if(origin.Contains(component))return true;return false;
        }
        internal NavigationPoint[] MarkerApproaches(WorldGeometrySnapshot geometry,NavigationTarget target)
        {
            Marker marker;
            if(_markers.TryGetValue(target.Id,out marker) && marker.Position.Equals(target.Position))return marker.Approaches;
            int cell=WorldGeometrySnapshot.Cell(target.Position);var nearest=new Dictionary<int,NavigationPoint>();
            foreach(var surface in geometry.MarkerCellSurfaces(target.Position)) {
                int component=Label(surface.Id);if(component<=0 || !Allowed(geometry,surface))continue;
                var point=ClosestPoint(surface,target.Position);
                // Keep the approach inside this face instead of exactly on a
                // building/ground edge where native hit order can be ambiguous.
                var center=surface.Center;var inward=WorldCoordinates.Difference(point,center);
                point=WorldCoordinates.Canonical(new NavigationPoint(point.X+inward.X*0.01f,point.Y+(center.Y-point.Y)*0.01f,point.Z+inward.Z*0.01f));
                if(WorldGeometrySnapshot.Cell(point)!=cell)continue;NavigationPoint before;
                if(!nearest.TryGetValue(component,out before) || WorldCoordinates.Distance(point,target.Position)<WorldCoordinates.Distance(before,target.Position))nearest[component]=point;
            }
            var points=new NavigationPoint[nearest.Count];nearest.Values.CopyTo(points,0);
            _markers[target.Id]=new Marker{Position=target.Position,Approaches=points};return points;
        }
        private static NavigationPoint ClosestPoint(WorldSurface surface,NavigationPoint marker)
        {
            float height;if(surface.TryHeight(marker,out height))return new NavigationPoint(marker.X,height,marker.Z);
            var best=surface.Center;float distance=float.MaxValue;
            for(int edge=0;edge<3;edge++) {
                var a=surface.Vertices[edge];var b=surface.Vertices[(edge+1)%3];
                var ab=WorldCoordinates.Difference(a,b);var ap=WorldCoordinates.Difference(a,marker);float length=ab.X*ab.X+ab.Z*ab.Z;
                float t=length==0?0:Math.Max(0,Math.Min(1,(ap.X*ab.X+ap.Z*ab.Z)/length));
                var point=WorldCoordinates.Canonical(new NavigationPoint(a.X+ab.X*t,a.Y+(b.Y-a.Y)*t,a.Z+ab.Z*t));float candidate=WorldCoordinates.Distance(point,marker);
                if(candidate<distance){distance=candidate;best=point;}
            }return best;
        }
        internal HashSet<int> ComponentsAt(WorldGeometrySnapshot geometry,WorldSurface surface,int cell,bool allowCachedStart=false)
        {
            var result=new HashSet<int>();if(!Matches(geometry) || surface==null)return result;
            if(!Allowed(geometry,surface)) {
                // Native cached raycasts omit ordinary-ray normal/ID exclusions.
                // Only the already accepted starting face can depart this way;
                // excluded faces never become ordinary targets or through roads.
                if(!allowCachedStart || Array.IndexOf(geometry.NativeCache,surface.Id)<0 || !WorldTerrainRules.CanTraverse(geometry.Rules,surface,surface))return result;
                foreach(int id in geometry.Neighbors(surface.Id)) {
                    var next=geometry.Find(id);if(!Allowed(geometry,next) || !WorldTerrainRules.CanTraverse(geometry.Rules,surface,next))continue;
                    foreach(int component in ComponentsAt(geometry,next,cell))result.Add(component);
                }
                return result;
            }
            int label=Label(surface.Id);if(label>0){result.Add(label);return result;}
            // Enter/exit a place through its own native cell. A place must not
            // become a through road joining disconnected ordinary ground.
            if(!Terminal(surface))return result;
            var visited=new HashSet<int>();var pending=new Queue<int>();pending.Enqueue(surface.Id);visited.Add(surface.Id);
            while(pending.Count!=0) {
                var from=geometry.Find(pending.Dequeue());
                var neighbors=geometry.Neighbors(from.Id);
                for(int n=0;n<neighbors.Count;n++) {int id=neighbors[n];
                    var to=geometry.Find(id);if(!Allowed(geometry,to) || !WorldTerrainRules.CanTraverse(geometry.Rules,from,to))continue;
                    label=Label(id);if(label>0){result.Add(label);continue;}
                    if(Terminal(to) && WorldGeometrySnapshot.Cell(to.Center)==cell && visited.Add(id))pending.Enqueue(id);
                }
            }return result;
        }
    }
    public sealed class WorldReachabilityCache
    {
        private sealed class Job {internal WorldGeometrySnapshot Geometry;internal volatile bool Cancelled;}
        private readonly object _sync=new object();private readonly Action<WaitCallback> _schedule;
        private readonly List<WorldReachabilityIndex> _ready=new List<WorldReachabilityIndex>();
        private Job _job;private WorldGeometrySnapshot _failed;private string _failure;
        public WorldReachabilityCache():this(callback=>{if(!ThreadPool.QueueUserWorkItem(callback))throw new InvalidOperationException("World reachability worker unavailable");}){ }
        internal WorldReachabilityCache(Action<WaitCallback> schedule){_schedule=schedule;}
        public string Failure{get{lock(_sync)return _failure;}}
        public bool TryRead(WorldGeometrySnapshot geometry,bool allowStart,out WorldReachabilityIndex index)
        {
            index=null;if(geometry==null || !geometry.Complete || geometry.Rules==null || geometry.Surfaces.Length==0)return false;Job job;
            lock(_sync) {
                for(int i=_ready.Count-1;i>=0;i--)if(!System.Object.ReferenceEquals(_ready[i].SourceSurfaces,geometry.Surfaces))_ready.RemoveAt(i);
                if(_failed!=null && !System.Object.ReferenceEquals(_failed.Surfaces,geometry.Surfaces)){_failed=null;_failure=null;}
                foreach(var ready in _ready)if(ready.Matches(geometry)){index=ready;return true;}
                if(_failed!=null && WorldReachabilityIndex.SameProfile(_failed,geometry))return false;
                if(!allowStart)return false;
                if(_job!=null){if(!WorldReachabilityIndex.SameProfile(_job.Geometry,geometry))_job.Cancelled=true;return false;}
                _failed=null;_failure=null;job=new Job{Geometry=geometry.ForPlanning()};_job=job;
            }
            try{_schedule(delegate(object unused){Run(job);});}
            catch(Exception exception){lock(_sync){if(_job==job){_job=null;_failed=job.Geometry;_failure=exception.Message;}}}
            return false;
        }
        private void Run(Job job)
        {
            WorldReachabilityIndex index=null;string failure=null;
            try{if(!job.Cancelled)index=WorldReachabilityIndex.Build(job.Geometry,()=>job.Cancelled);}
            catch(OperationCanceledException){ }
            catch(Exception exception){failure=exception.Message;}
            lock(_sync) {
                if(_job==job) {
                    if(!job.Cancelled){if(index!=null){if(_ready.Count==2)_ready.RemoveAt(0);_ready.Add(index);}else{_failed=job.Geometry;_failure=failure??"World reachability unavailable";}}
                    _job=null;
                }
            }
        }
        public void Cancel(){lock(_sync){if(_job!=null)_job.Cancelled=true;_failed=null;_failure=null;}}
    }
}
