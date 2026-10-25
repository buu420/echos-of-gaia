using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    public sealed class WorldRoutePlanner
    {
        public RoutePlan Plan(NavigationContext context,NavigationTarget target,WorldGeometrySnapshot geometry,Func<bool> cancelled=null)
        {
            var failure=new RoutePlan{TargetId=target.Id,PhysicalId=target.PhysicalId,Generation=context.Generation,Transport=context.Transport,Failure=RouteFailure.TerrainUnavailable,GeometryFingerprint=geometry==null?null:geometry.Fingerprint};
            if(geometry==null || !geometry.Complete || geometry.Surfaces.Length==0)return failure;
            if(geometry.Generation!=context.Generation || target.Generation!=context.Generation){failure.Failure=RouteFailure.StaleContext;return failure;}
            if(geometry.Rules==null){failure.Failure=RouteFailure.UnsupportedTransport;return failure;}
            var start=geometry.Probe(context.Position,geometry.Rules.CurrentTerrainId,new PrivateWorldCache(geometry.NativeCache));
            if(start==null){failure.Failure=RouteFailure.StartOutsideMesh;return failure;}
            RoutePlan best=null;int startCell=WorldGeometrySnapshot.Cell(context.Position);
            foreach(var approach in target.ApproachCandidates??new[]{target.Position}) {
                if(cancelled!=null && cancelled()){failure.Failure=RouteFailure.StaleContext;return failure;}
                if(!approach.IsFinite)continue;
                int goalCell=target.PhysicalIdentity.Kind==TargetKind.WorldLocation?WorldGeometrySnapshot.Cell(approach):-1;
                var goalProbe=new NavigationPoint(approach.X,geometry.Rules.Flying?context.Position.Y:approach.Y,approach.Z);
                // Marker tables carry X/Z, not terrain height. Locate the destination
                // surface from above; subsequent native-height legs still validate it.
                var goal=geometry.Probe(new NavigationPoint(approach.X,Math.Max(context.Position.Y,approach.Y)+400,approach.Z),start.TerrainId,new PrivateWorldCache(geometry.NativeCache));
                if(goal==null){failure.Failure=RouteFailure.TargetOutsideMesh;continue;}
                float ground;if(!geometry.Rules.Flying && goal.TryHeight(goalProbe,out ground))goalProbe=new NavigationPoint(goalProbe.X,ground,goalProbe.Z);
                WorldSurface hit;var points=new List<NavigationPoint>();
                if(geometry.Segment(context.Position,goalProbe,start,new PrivateWorldCache(geometry.NativeCache),startCell,goalCell,out hit,cancelled))points.Add(goalProbe);
                else {
                    var corridor=Search(geometry,start,goal,startCell,goalCell,cancelled);if(corridor==null){failure.Failure=RouteFailure.Disconnected;continue;}
                    var cache=new PrivateWorldCache(geometry.NativeCache);var previous=context.Position;hit=start;bool valid=true;
                    // Every centroid leg is validated with a private evolving copy of the native cache.
                    for(int i=1;i<corridor.Count;i++) {
                        var point=geometry.Find(corridor[i]).Center;if(geometry.Rules.Flying)point=new NavigationPoint(point.X,context.Position.Y,point.Z);
                        WorldSurface next;if(!geometry.Segment(previous,point,hit,cache,startCell,goalCell,out next,cancelled)){valid=false;break;}
                        points.Add(point);previous=point;hit=next;
                    }
                    WorldSurface last;if(!valid || !geometry.Segment(previous,goalProbe,hit,cache,startCell,goalCell,out last,cancelled)){failure.Failure=RouteFailure.TraversalRejected;continue;}
                    points.Add(goalProbe);
                }
                var route=new RoutePlan{Success=true,TargetId=target.Id,PhysicalId=target.PhysicalId,Generation=context.Generation,Transport=context.Transport,GeometryFingerprint=geometry.Fingerprint,ApproachPoint=goalProbe,Waypoints=points.ToArray()};
                var from=context.Position;foreach(var point in points){route.Length+=WorldCoordinates.Distance(from,point);from=point;}
                if(best==null || route.Length<best.Length)best=route;
            }
            return best??failure;
        }
        private static List<int> Search(WorldGeometrySnapshot geometry,WorldSurface start,WorldSurface goal,int startCell,int goalCell,Func<bool> cancelled)
        {
            var open=new Heap();open.Add(start.Id,0);var costs=new Dictionary<int,float>{{start.Id,0}};var parents=new Dictionary<int,int>();var closed=new HashSet<int>();int expanded=0;
            while(open.Count>0 && expanded++<250000) {
                if(cancelled!=null && cancelled())return null;
                int id=open.Pop();if(closed.Contains(id))continue;if(id==goal.Id){var path=new List<int>{id};while(parents.ContainsKey(id)){id=parents[id];path.Add(id);}path.Reverse();return path;}
                closed.Add(id);var from=geometry.Find(id);
                foreach(int nextId in geometry.Neighbors(id)) {
                    var next=geometry.Find(nextId);if(!WorldTerrainRules.CanTraverse(geometry.Rules,from,next))continue;
                    WorldSurface hit;var a=from.Center;var b=next.Center;
                    if(geometry.Rules.Flying){a=new NavigationPoint(a.X,Math.Max(a.Y,b.Y)+50,a.Z);b=new NavigationPoint(b.X,a.Y,b.Z);}
                    if(!geometry.Segment(a,b,from,new PrivateWorldCache(new[]{from.Id}),startCell,goalCell,out hit,cancelled))continue;
                    float tentative=costs[id]+WorldCoordinates.Distance(a,b),old;if(costs.TryGetValue(nextId,out old) && old<=tentative)continue;
                    costs[nextId]=tentative;parents[nextId]=id;closed.Remove(nextId);open.Add(nextId,tentative+WorldCoordinates.Distance(next.Center,goal.Center));
                }
            }return null;
        }
        private sealed class Heap
        {
            private struct Item {public int Id;public float Cost;}
            private readonly List<Item> _items=new List<Item>();public int Count{get{return _items.Count;}}
            public void Add(int id,float cost) {var item=new Item{Id=id,Cost=cost};_items.Add(item);int index=_items.Count-1;while(index>0){int parent=(index-1)/2;if(_items[parent].Cost<=cost)break;_items[index]=_items[parent];index=parent;}_items[index]=item;}
            public int Pop() {int id=_items[0].Id;var last=_items[_items.Count-1];_items.RemoveAt(_items.Count-1);if(_items.Count==0)return id;int index=0;while(index*2+1<_items.Count){int child=index*2+1;if(child+1<_items.Count && _items[child+1].Cost<_items[child].Cost)child++;if(_items[child].Cost>=last.Cost)break;_items[index]=_items[child];index=child;}_items[index]=last;return id;}
        }
    }
}
