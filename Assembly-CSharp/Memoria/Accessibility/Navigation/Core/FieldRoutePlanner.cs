using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    public sealed class FieldRoutePlanner
    {
        public RoutePlan Plan(NavigationContext context,NavigationTarget target,FieldGeometrySnapshot geometry)
        {
            var failure=new RoutePlan {TargetId=target.Id,PhysicalId=target.PhysicalId,Generation=context.Generation,Transport=context.Transport,Failure=RouteFailure.GeometryUnavailable,GeometryFingerprint=geometry==null?null:geometry.Fingerprint};
            if(geometry==null || geometry.Triangles.Length==0)return failure;
            if(geometry.Generation!=context.Generation || target.Generation!=context.Generation){failure.Failure=RouteFailure.StaleContext;return failure;}
            var start=geometry.Find(geometry.ActiveTriangle);if(!geometry.Permitted(start) || !start.Contains(context.Position))start=geometry.At(context.Position);
            if(start==null){failure.Failure=RouteFailure.StartOutsideMesh;return failure;}
            NavigationPoint[] candidates=FieldTargetApproaches.Prepare(context,target,geometry);RoutePlan best=null;
            bool viewing=target.UseViewingPoint && !target.IsInteractive && target.PhysicalIdentity.Kind==TargetKind.FieldActor;
            if(candidates.Length==0){failure.Failure=RouteFailure.TargetOutsideMesh;return failure;}
            // Exit samples often share the same support triangle and stair turns.
            // Reuse checks only within this plan, where the geometry and origin are fixed.
            var corridors=new Dictionary<int,int[]>();
            var portals=new Dictionary<int,NavigationPoint?>();
            var segments=new Dictionary<SegmentKey,NavigationPoint[]>();
            bool bestInsideExit=false;
            var exitRegion=target.PhysicalIdentity.Kind==TargetKind.FieldExit?target.InteractionRegion:null;
            float arrival=FieldAnalogMovement.ArrivalRadius(geometry.Radius,1);
            foreach(var approach in candidates) {
                bool insideExit=FieldRegionApproaches.CanStopInside(exitRegion,approach,arrival);
                if(bestInsideExit && !insideExit)continue;
                var end=geometry.At(approach);if(end==null){failure.Failure=RouteFailure.TargetOutsideMesh;continue;}
                int[] corridor;
                if(!corridors.TryGetValue(end.Id,out corridor)) {
                    corridor=Search(geometry,start,end,portals);
                    corridors[end.Id]=corridor;
                }
                if(corridor==null){failure.Failure=RouteFailure.Disconnected;continue;}
                var points=CorridorPoints(geometry,corridor,context.Position,approach,portals);if(points==null){failure.Failure=RouteFailure.NarrowPassage;continue;}
                List<NavigationPoint> steering=null;
                foreach(int id in corridor)if(geometry.UsesReviewedBoundaryClearance(geometry.Find(id))) {
                    steering=new List<NavigationPoint>();break;
                }
                var funnel=Funnel(geometry,corridor,context.Position,approach);
                var smooth=funnel==null?null:Smooth(geometry,context.Position,funnel,segments,steering);
                if(smooth==null) {
                    if(steering!=null)steering.Clear();
                    smooth=Smooth(geometry,context.Position,points,segments,steering);
                }
                if(smooth==null){failure.Failure=RouteFailure.UnsafeSegment;continue;}
                var route=new RoutePlan{Success=true,Failure=RouteFailure.None,TargetId=target.Id,PhysicalId=target.PhysicalId,Generation=context.Generation,Transport=context.Transport,Origin=context.Position,ApproachPoint=approach,Waypoints=smooth.ToArray(),FieldSteeringWaypoints=steering==null?null:steering.ToArray(),Corridor=corridor,GeometryFingerprint=geometry.Fingerprint,ViewingPoint=viewing};
                var from=context.Position;foreach(var point in route.Waypoints){route.Length+=FieldGeometrySnapshot.Horizontal(from,point);from=point;}
                if(viewing)return route; // Viewing candidates are ordered by proximity to the observed object.
                if(best==null || insideExit && !bestInsideExit || route.Length<best.Length) {
                    best=route;bestInsideExit=insideExit;
                }
            }return best??failure;
        }
        private static int[] Search(FieldGeometrySnapshot geometry,FieldTriangle start,FieldTriangle end,Dictionary<int,NavigationPoint?> portals)
        {
            var open=new List<int>{start.Id};var costs=new Dictionary<int,float>{{start.Id,0}};var parent=new Dictionary<int,int>();var closed=new HashSet<int>();
            while(open.Count>0) {
                int current=open[0];float score=costs[current]+FieldGeometrySnapshot.Horizontal(geometry.Find(current).Center,end.Center);
                foreach(int id in open){float value=costs[id]+FieldGeometrySnapshot.Horizontal(geometry.Find(id).Center,end.Center);if(value<score){score=value;current=id;}}
                open.Remove(current);if(current==end.Id){var result=new List<int>{current};while(parent.ContainsKey(current)){current=parent[current];result.Add(current);}result.Reverse();return result.ToArray();}
                closed.Add(current);var triangle=geometry.Find(current);
                for(int edge=0;edge<3;edge++) {
                    var next=geometry.Find(triangle.Neighbors[edge]);if(!geometry.Permitted(next))continue;
                    NavigationPoint crossing;if(!Portal(geometry,triangle,edge,portals,out crossing))continue;
                    float tentative=costs[current]+FieldGeometrySnapshot.Horizontal(triangle.Center,next.Center)*Math.Max(1,next.TraversalCost),existing;
                    if(costs.TryGetValue(next.Id,out existing) && tentative>=existing)continue;
                    costs[next.Id]=tentative;parent[next.Id]=current;closed.Remove(next.Id);if(!open.Contains(next.Id))open.Add(next.Id);
                }
            }return null;
        }
        private static List<NavigationPoint> CorridorPoints(FieldGeometrySnapshot geometry,int[] corridor,NavigationPoint start,NavigationPoint end,Dictionary<int,NavigationPoint?> portals)
        {
            var result=new List<NavigationPoint>();
            // Native radius forces can leave the resting player within clearance
            // of a boundary. Depart inward on the same support before the portal;
            // Smooth still requires monotone clearance and a fully clear endpoint.
            if(!geometry.IsSegmentClear(start,start))result.Add(geometry.Find(corridor[0]).Center);
            for(int i=0;i<corridor.Length;i++) {
                var triangle=geometry.Find(corridor[i]);if(i>0)result.Add(triangle.Center);
                if(i+1<corridor.Length) {
                    int edge=Array.IndexOf(triangle.Neighbors,corridor[i+1]);if(edge<0)return null;
                    // Radius constrains physical boundary clearance along this native portal.
                    NavigationPoint crossing;if(!Portal(geometry,triangle,edge,portals,out crossing))return null;result.Add(crossing);
                }
            }result.Add(end);return result;
        }
        private static List<NavigationPoint> Smooth(FieldGeometrySnapshot geometry,NavigationPoint start,List<NavigationPoint> points,Dictionary<SegmentKey,NavigationPoint[]> segments,List<NavigationPoint> steering)
        {
            var result=new List<NavigationPoint>();int index=0;
            while(index<points.Count) {
                int farthest=-1;NavigationPoint[] surface=null;
                for(int i=points.Count-1;i>=index;i--)if(Segment(geometry,start,points[i],segments,out surface)){farthest=i;break;}
                if(farthest<0)return null;
                result.AddRange(surface);
                start=surface[surface.Length-1];
                // Manual guidance interpolates these surface heights. The
                // native follower steers in XZ and can walk through slope breaks.
                if(steering!=null)steering.Add(start);
                index=farthest+1;
            }
            return result;
        }
        private static bool Portal(FieldGeometrySnapshot geometry,FieldTriangle triangle,int edge,Dictionary<int,NavigationPoint?> cache,out NavigationPoint crossing)
        {
            int key=triangle.Id*3+edge;
            NavigationPoint? point;
            if(!cache.TryGetValue(key,out point)) {
                point=geometry.TryPortalPoint(triangle,edge,out crossing)?(NavigationPoint?)crossing:null;
                cache.Add(key,point);
            }
            crossing=point.GetValueOrDefault();
            return point.HasValue;
        }
        private static bool Segment(FieldGeometrySnapshot geometry,NavigationPoint start,NavigationPoint end,
            Dictionary<SegmentKey,NavigationPoint[]> cache,out NavigationPoint[] surface)
        {
            var key=new SegmentKey(start,end);
            if(!cache.TryGetValue(key,out surface)) {
                if(!geometry.TrySegmentClear(start,end,out surface))surface=null;
                if(cache.Count<4096)cache.Add(key,surface);
            }
            return surface!=null;
        }
        private struct SegmentKey : IEquatable<SegmentKey>
        {
            private readonly NavigationPoint _start,_end;
            public SegmentKey(NavigationPoint start,NavigationPoint end){_start=start;_end=end;}
            public bool Equals(SegmentKey other)
            {
                return _start.X==other._start.X && _start.Y==other._start.Y && _start.Z==other._start.Z &&
                    _end.X==other._end.X && _end.Y==other._end.Y && _end.Z==other._end.Z;
            }
            public override bool Equals(object value){return value is SegmentKey && Equals((SegmentKey)value);}
            public override int GetHashCode()
            {
                unchecked {
                    int hash=_start.X.GetHashCode();
                    hash=hash*397^_start.Y.GetHashCode();
                    hash=hash*397^_start.Z.GetHashCode();
                    hash=hash*397^_end.X.GetHashCode();
                    hash=hash*397^_end.Y.GetHashCode();
                    return hash*397^_end.Z.GetHashCode();
                }
            }
        }
        private static List<NavigationPoint> Funnel(FieldGeometrySnapshot geometry,int[] corridor,NavigationPoint start,NavigationPoint end)
        {
            var lefts=new List<NavigationPoint>{start};var rights=new List<NavigationPoint>{start};
            for(int i=0;i+1<corridor.Length;i++) {
                var current=geometry.Find(corridor[i]);var next=geometry.Find(corridor[i+1]);int edge=Array.IndexOf(current.Neighbors,next.Id);if(edge<0)return null;
                var a=current.Vertices[edge];var b=current.Vertices[(edge+1)%3];
                if(FieldTriangle.Cross(current.Center,next.Center,a)<0){var swap=a;a=b;b=swap;}
                lefts.Add(a);rights.Add(b);
            }
            lefts.Add(end);rights.Add(end);var result=new List<NavigationPoint>();var apex=start;var left=start;var right=start;int apexIndex=0,leftIndex=0,rightIndex=0;
            for(int i=1;i<lefts.Count;i++) {
                var newLeft=lefts[i];var newRight=rights[i];
                if(FieldTriangle.Cross(apex,right,newRight)<=0) {
                    if(FieldGeometrySnapshot.Horizontal(apex,right)<0.001f || FieldTriangle.Cross(apex,left,newRight)>0){right=newRight;rightIndex=i;}
                    else {result.Add(left);apex=left;apexIndex=leftIndex;left=apex;right=apex;leftIndex=apexIndex;rightIndex=apexIndex;i=apexIndex;continue;}
                }
                if(FieldTriangle.Cross(apex,left,newLeft)>=0) {
                    if(FieldGeometrySnapshot.Horizontal(apex,left)<0.001f || FieldTriangle.Cross(apex,right,newLeft)<0){left=newLeft;leftIndex=i;}
                    else {result.Add(right);apex=right;apexIndex=rightIndex;left=apex;right=apex;leftIndex=apexIndex;rightIndex=apexIndex;i=apexIndex;continue;}
                }
            }result.Add(end);return result;
        }
    }
}
