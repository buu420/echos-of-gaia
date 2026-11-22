using System;
using System.Collections.Generic;
using System.Linq;
namespace Memoria.Accessibility.Navigation
{
    public sealed class FieldRoutePlanner
    {
        public RoutePlan Plan(NavigationContext context,NavigationTarget target,FieldGeometrySnapshot geometry)
        {
            return Plan(context, target, geometry, null);
        }
        internal RoutePlan Plan(NavigationContext context,NavigationTarget target,FieldGeometrySnapshot geometry,Func<bool> cancelled)
        {
            var failure=new RoutePlan {TargetId=target.Id,PhysicalId=target.PhysicalId,Generation=context.Generation,Transport=context.Transport,Failure=RouteFailure.GeometryUnavailable,GeometryFingerprint=geometry==null?null:geometry.Fingerprint};
            if(geometry==null || geometry.Triangles.Length==0)return failure;
            if(geometry.Generation!=context.Generation || target.Generation!=context.Generation){failure.Failure=RouteFailure.StaleContext;return failure;}
            var start=geometry.Find(geometry.ActiveTriangle);if(!geometry.Permitted(start) || !start.Contains(context.Position))start=geometry.At(context.Position);
            if(start==null){failure.Failure=RouteFailure.StartOutsideMesh;return failure;}
            var candidates=FieldTargetApproaches.Prepare(context,target,geometry);
            if(candidates.Length==0){failure.Failure=RouteFailure.TargetOutsideMesh;return failure;}
            var route=PlanFrom(context,target,geometry,start,context.Position,candidates,failure,cancelled);
            if(route.Failure==RouteFailure.UnsafeSegment) {
                var alternative=PlanAcrossPortals(context,target,geometry,start,candidates,cancelled);
                if(alternative!=null)route=alternative;
            }
            if (route.Success) {
                route.FieldIntermediateArrivalRadius = CleyraTrunkRoutes.IntermediateRadius(target, geometry, route);
                return route;
            }
            if (route.Failure != RouteFailure.UnsafeSegment || !target.IsInteractive || target.PhysicalIdentity.Kind != TargetKind.FieldActor ||
                !FieldActorApproaches.IsCabinVivi(context,target.PhysicalIdentity.NativeId)) return route;

            // The shortest corridor can cut the cabin's inside corner. Both
            // legs through the center aisle still need full native clearance.
            var aisle=geometry.Find(62);
            if (!geometry.Permitted(aisle) || aisle.Id == start.Id) return route;
            var via=aisle.Center;
            var first=PlanFrom(context,target,geometry,start,context.Position,new[]{via},new RoutePlan(),cancelled);
            if (!first.Success) return route;
            var last=PlanFrom(context,target,geometry,aisle,via,candidates,new RoutePlan(),cancelled);
            if (!last.Success) return route;
            last.Origin=context.Position;
            last.Length+=first.Length;
            if (first.FieldSteeringWaypoints != null || last.FieldSteeringWaypoints != null)
                last.FieldSteeringWaypoints=(first.FieldSteeringWaypoints??first.Waypoints).Concat(last.FieldSteeringWaypoints??last.Waypoints).ToArray();
            last.Waypoints=first.Waypoints.Concat(last.Waypoints).ToArray();
            last.Corridor=first.Corridor.Concat(last.Corridor.Skip(1)).ToArray();
            return last;
        }

        private sealed class PassageNode
        {
            internal int From,To,Parent=-1;internal NavigationPoint Point;
            internal float Cost=float.MaxValue;internal bool Queued,Closed;internal NavigationPoint[] Leg;
        }
        private static RoutePlan PlanAcrossPortals(NavigationContext context,NavigationTarget target,FieldGeometrySnapshot geometry,
            FieldTriangle start,NavigationPoint[] candidates,Func<bool> cancelled)
        {
            // A triangle-only shortest path can choose an impossible turn even
            // though another crossing is clear. Refine only a failed route, on
            // the detached worker geometry, with verified crossings and legs.
            if(geometry.Triangles.Length>2048)return null;
            var nodes=new List<PassageNode>{new PassageNode{From=start.Id,To=start.Id,Point=context.Position,Cost=0,Queued=true}};
            var outgoing=new Dictionary<int,List<int>>();
            foreach(var tri in geometry.Triangles) {
                if(cancelled!=null && cancelled())return null;
                if(!geometry.Permitted(tri))continue;
                var list=new List<int>();outgoing.Add(tri.Id,list);
                for(int edge=0;edge<3;edge++) {
                    NavigationPoint middle;if(!geometry.TryPortalPoint(tri,edge,out middle))continue;
                    var crossings=new List<NavigationPoint>{middle};var a=tri.Vertices[edge];var b=tri.Vertices[(edge+1)%3];
                    for(int step=1;step<4;step++) {
                        float t=step*.25f;var p=new NavigationPoint(a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t,a.Z+(b.Z-a.Z)*t);
                        bool duplicate=false;foreach(var old in crossings)if(NavigationPoint.Distance(p,old)<.1f){duplicate=true;break;}
                        if(!duplicate && geometry.IsSegmentClear(p,p))crossings.Add(p);
                    }
                    foreach(var point in crossings) {
                        list.Add(nodes.Count);nodes.Add(new PassageNode{From=tri.Id,To=tri.Neighbors[edge],Point=point});
                    }
                }
            }
            var segments=new Dictionary<SegmentKey,NavigationPoint[]>();var supports=new Dictionary<SegmentKey,FieldTriangle>();
            var turns=new Dictionary<SegmentKey,NavigationPoint[]>();var samples=new Dictionary<int,NavigationPoint[]>();
            var goals=new Dictionary<int,List<NavigationPoint>>();
            bool preferInside=false;var region=ArrivalRegion(target);
            float arrival=FieldAnalogMovement.ArrivalRadius(geometry.Radius,1);
            foreach(var point in candidates)if(FieldRegionApproaches.CanStopInside(region,point,arrival)){preferInside=true;break;}
            foreach(var point in candidates) {
                if(preferInside && !FieldRegionApproaches.CanStopInside(region,point,arrival))continue;
                var support=Support(geometry,point,supports);if(support==null)continue;
                List<NavigationPoint> list;if(!goals.TryGetValue(support.Id,out list))goals.Add(support.Id,list=new List<NavigationPoint>());
                list.Add(point);
            }
            var pending=new List<int>{0};float best=float.MaxValue;int bestNode=-1;NavigationPoint[] finalLeg=null;
            while(pending.Count!=0) {
                if(cancelled!=null && cancelled())return null;
                int current=pending[0];foreach(int id in pending)if(nodes[id].Cost<nodes[current].Cost)current=id;
                pending.Remove(current);var at=nodes[current];at.Queued=false;if(at.Closed)continue;at.Closed=true;
                if(at.Cost>=best)break;
                List<NavigationPoint> destinations;
                if(goals.TryGetValue(at.To,out destinations))foreach(var point in destinations) {
                    var leg=PassageLeg(geometry,at.To,at.Point,point,segments,supports,turns,samples,cancelled);if(leg==null)continue;
                    float cost=at.Cost+LegLength(at.Point,leg);
                    if(cost<best){best=cost;bestNode=current;finalLeg=leg;}
                }
                List<int> next;if(!outgoing.TryGetValue(at.To,out next))continue;
                foreach(int id in next) {
                    if(cancelled!=null && cancelled())return null;
                    var to=nodes[id];if(to.Closed)continue;
                    if(at.Cost+FieldGeometrySnapshot.Horizontal(at.Point,to.Point)>=Math.Min(to.Cost,best))continue;
                    var leg=PassageLeg(geometry,at.To,at.Point,to.Point,segments,supports,turns,samples,cancelled);if(leg==null)continue;
                    float cost=at.Cost+LegLength(at.Point,leg);if(cost>=to.Cost)continue;
                    to.Cost=cost;to.Parent=current;to.Leg=leg;if(!to.Queued){to.Queued=true;pending.Add(id);}
                }
            }
            if(bestNode<0)return null;
            var chain=new List<int>();for(int id=bestNode;id>=0;id=nodes[id].Parent)chain.Add(id);chain.Reverse();
            var points=new List<NavigationPoint>();var corridor=new List<int>{start.Id};bool reviewed=false;
            foreach(int id in chain) {
                var node=nodes[id];if(node.Leg!=null)points.AddRange(node.Leg);
                if(corridor[corridor.Count-1]!=node.To)corridor.Add(node.To);
                reviewed|=geometry.UsesReviewedBoundaryClearance(geometry.Find(node.From)) || geometry.UsesReviewedBoundaryClearance(geometry.Find(node.To));
            }
            points.AddRange(finalLeg);var steering=reviewed?new List<NavigationPoint>():null;
            var smooth=Smooth(geometry,context.Position,points,segments,supports,steering);if(smooth==null)return null;
            return new RoutePlan{Success=true,Failure=RouteFailure.None,TargetId=target.Id,PhysicalId=target.PhysicalId,
                Generation=context.Generation,Transport=context.Transport,Origin=context.Position,GeometryFingerprint=geometry.Fingerprint,
                ApproachPoint=finalLeg[finalLeg.Length-1],Waypoints=smooth.ToArray(),FieldSteeringWaypoints=steering==null?null:steering.ToArray(),
                Corridor=corridor.ToArray(),Length=LegLength(context.Position,smooth.ToArray()),
                ViewingPoint=target.UseViewingPoint && !target.IsInteractive && target.PhysicalIdentity.Kind==TargetKind.FieldActor};
        }
        private static NavigationPoint[] PassageLeg(FieldGeometrySnapshot geometry,int triangle,NavigationPoint from,NavigationPoint to,
            Dictionary<SegmentKey,NavigationPoint[]> segments,Dictionary<SegmentKey,FieldTriangle> supports,
            Dictionary<SegmentKey,NavigationPoint[]> turns,Dictionary<int,NavigationPoint[]> samples,Func<bool> cancelled)
        {
            NavigationPoint[] leg;if(Segment(geometry,from,to,segments,supports,out leg))return leg;
            var key=new SegmentKey(from,to);if(turns.TryGetValue(key,out leg))return leg;
            NavigationPoint[] candidates;if(!samples.TryGetValue(triangle,out candidates))samples.Add(triangle,candidates=ClearTurnSamples(geometry,geometry.Find(triangle),cancelled));
            leg=TurnPath(geometry,from,to,candidates,segments,supports,cancelled);if(turns.Count<4096)turns.Add(key,leg);return leg;
        }
        private static float LegLength(NavigationPoint from,NavigationPoint[] points)
        {
            float length=0;foreach(var point in points){length+=FieldGeometrySnapshot.Horizontal(from,point);from=point;}return length;
        }

        private static RoutePlan PlanFrom(NavigationContext context,NavigationTarget target,FieldGeometrySnapshot geometry,
            FieldTriangle start,NavigationPoint origin,NavigationPoint[] candidates,RoutePlan failure,Func<bool> cancelled)
        {
            RoutePlan best=null;
            bool viewing=target.UseViewingPoint && !target.IsInteractive && target.PhysicalIdentity.Kind==TargetKind.FieldActor;
            // Entrance and Confirm-region samples often share support triangles and turns.
            // Reuse checks only within this plan, where the geometry and origin are fixed.
            var corridors=new Dictionary<int,int[]>();
            var portals=new Dictionary<int,NavigationPoint?>();
            var segments=new Dictionary<SegmentKey,NavigationPoint[]>();
            var supports = new Dictionary<SegmentKey,FieldTriangle>();
            var turnSamples = new Dictionary<int,NavigationPoint[]>();
            var turnPaths = new Dictionary<SegmentKey,NavigationPoint[]>();
            bool? departureClear = null;
            bool bestInsideExit=false;
            var exitRegion=ArrivalRegion(target);
            float arrival=FieldAnalogMovement.ArrivalRadius(geometry.Radius,1);
            foreach(var approach in candidates) {
                if (cancelled != null && cancelled()) return failure;
                bool insideExit=FieldRegionApproaches.CanStopInside(exitRegion,approach,arrival);
                if(bestInsideExit && !insideExit)continue;
                var end=Support(geometry,approach,supports);if(end==null){failure.Failure=RouteFailure.TargetOutsideMesh;continue;}
                int[] corridor;
                if(!corridors.TryGetValue(end.Id,out corridor)) {
                    corridor=Search(geometry,start,end,portals,cancelled);
                    corridors[end.Id]=corridor;
                }
                if(corridor==null){failure.Failure=RouteFailure.Disconnected;continue;}
                if (!departureClear.HasValue) departureClear = geometry.IsSegmentClear(origin, origin);
                var points=CorridorPoints(geometry,corridor,approach,departureClear.Value,portals);if(points==null){failure.Failure=RouteFailure.NarrowPassage;continue;}
                List<NavigationPoint> steering=null;
                foreach(int id in corridor)if(geometry.UsesReviewedBoundaryClearance(geometry.Find(id))) {
                    steering=new List<NavigationPoint>();break;
                }
                var funnel=Funnel(geometry,corridor,origin,approach);
                var smooth=funnel==null?null:Smooth(geometry,origin,funnel,segments,supports,steering);
                if(smooth==null) {
                    if(steering!=null)steering.Clear();
                    smooth=Smooth(geometry,origin,points,segments,supports,steering);
                }
                if(smooth==null) {
                    // A triangle center is not necessarily clear of its walls.
                    // Walk the verified portals and, where a corner needs a turn,
                    // choose a clear point on that native floor instead.
                    var passage=PassagePoints(geometry,corridor,origin,approach,portals,turnSamples,turnPaths,segments,supports,cancelled);
                    if(passage!=null) {
                        if(steering!=null)steering.Clear();
                        smooth=Smooth(geometry,origin,passage,segments,supports,steering);
                    }
                }
                if(smooth==null){failure.Failure=RouteFailure.UnsafeSegment;continue;}
                var route=new RoutePlan{Success=true,Failure=RouteFailure.None,TargetId=target.Id,PhysicalId=target.PhysicalId,Generation=context.Generation,Transport=context.Transport,Origin=origin,ApproachPoint=approach,Waypoints=smooth.ToArray(),FieldSteeringWaypoints=steering==null?null:steering.ToArray(),Corridor=corridor,GeometryFingerprint=geometry.Fingerprint,ViewingPoint=viewing};
                var from=origin;foreach(var point in route.Waypoints){route.Length+=FieldGeometrySnapshot.Horizontal(from,point);from=point;}
                if(viewing)return route; // Viewing candidates are ordered by proximity to the observed object.
                if(best==null || insideExit && !bestInsideExit || route.Length<best.Length) {
                    best=route;bestInsideExit=insideExit;
                }
            }return best??failure;
        }
        private static NavigationPoint[] ArrivalRegion(NavigationTarget target)
        {
            return target.PhysicalIdentity.Kind==TargetKind.FieldExit || target.IsInteractive && target.InteractionUid>=0?target.InteractionRegion:null;
        }
        private static List<NavigationPoint> PassagePoints(FieldGeometrySnapshot geometry,int[] corridor,NavigationPoint origin,NavigationPoint end,
            Dictionary<int,NavigationPoint?> portals,Dictionary<int,NavigationPoint[]> turnSamples,
            Dictionary<SegmentKey,NavigationPoint[]> turnPaths,Dictionary<SegmentKey,NavigationPoint[]> segments,Dictionary<SegmentKey,FieldTriangle> supports,Func<bool> cancelled)
        {
            var result=new List<NavigationPoint>();var from=origin;
            for(int i=0;i<corridor.Length;i++) {
                if(cancelled!=null && cancelled())return null;
                var tri=geometry.Find(corridor[i]);var to=end;
                if(i+1<corridor.Length) {
                    int edge=Array.IndexOf(tri.Neighbors,corridor[i+1]);
                    if(edge<0 || !Portal(geometry,tri,edge,portals,out to))return null;
                }
                NavigationPoint[] direct;
                if(Segment(geometry,from,to,segments,supports,out direct))result.AddRange(direct);
                else {
                    NavigationPoint[] candidates;
                    if(!turnSamples.TryGetValue(tri.Id,out candidates))
                        turnSamples.Add(tri.Id,candidates=ClearTurnSamples(geometry,tri,cancelled));
                    var key=new SegmentKey(from,to);NavigationPoint[] turn;
                    if(!turnPaths.TryGetValue(key,out turn)) {
                        turn=TurnPath(geometry,from,to,candidates,segments,supports,cancelled);
                        turnPaths.Add(key,turn);
                    }
                    if(turn==null)return null;
                    result.AddRange(turn);
                }
                from=to;
            }
            return result;
        }
        private static NavigationPoint[] TurnPath(FieldGeometrySnapshot geometry,NavigationPoint from,NavigationPoint to,NavigationPoint[] samples,
            Dictionary<SegmentKey,NavigationPoint[]> segments,Dictionary<SegmentKey,FieldTriangle> supports,Func<bool> cancelled)
        {
            var points=new NavigationPoint[samples.Length+2];Array.Copy(samples,points,samples.Length);
            int end=samples.Length,start=end+1;points[end]=to;points[start]=from;
            var parent=new int[points.Length];for(int i=0;i<parent.Length;i++)parent[i]=-1;
            var queue=new Queue<int>();queue.Enqueue(start);parent[start]=start;
            while(queue.Count!=0) {
                if(cancelled!=null && cancelled())return null;
                int current=queue.Dequeue();
                // Prefer finishing the turn before adding another intermediate point.
                for(int visit=end;visit>=0;visit--) {
                    if(cancelled!=null && cancelled())return null;
                    if(parent[visit]>=0)continue;NavigationPoint[] leg;
                    if(!Segment(geometry,points[current],points[visit],segments,supports,out leg))continue;
                    parent[visit]=current;
                    if(visit==end) {
                        var indices=new List<int>{end};while(indices[indices.Count-1]!=start)indices.Add(parent[indices[indices.Count-1]]);
                        indices.Reverse();var route=new List<NavigationPoint>();
                        for(int i=1;i<indices.Count;i++) {
                            Segment(geometry,points[indices[i-1]],points[indices[i]],segments,supports,out leg);route.AddRange(leg);
                        }
                        return route.ToArray();
                    }
                    queue.Enqueue(visit);
                }
            }
            return null;
        }
        private static NavigationPoint[] ClearTurnSamples(FieldGeometrySnapshot geometry,FieldTriangle triangle,Func<bool> cancelled)
        {
            var result=new List<NavigationPoint>();var vertices=triangle.Vertices;
            // Small floor triangles can have a clear strip away from their
            // centroid. This bounded grid is used only for a failed route turn.
            const int divisions=32;
            for(int a=0;a<=divisions;a++)for(int b=0;b<=divisions-a;b++) {
                if(cancelled!=null && cancelled())return new NavigationPoint[0];
                float u=(float)a/divisions,v=(float)b/divisions,w=1-u-v;
                var point=new NavigationPoint(vertices[0].X*u+vertices[1].X*v+vertices[2].X*w,
                    vertices[0].Y*u+vertices[1].Y*v+vertices[2].Y*w,vertices[0].Z*u+vertices[1].Z*v+vertices[2].Z*w);
                if(triangle.Contains(point) && geometry.IsSegmentClear(point,point))result.Add(point);
            }
            return result.ToArray();
        }
        private static int[] Search(FieldGeometrySnapshot geometry,FieldTriangle start,FieldTriangle end,Dictionary<int,NavigationPoint?> portals,Func<bool> cancelled)
        {
            var open=new List<int>{start.Id};var costs=new Dictionary<int,float>{{start.Id,0}};var parent=new Dictionary<int,int>();var closed=new HashSet<int>();
            while(open.Count>0) {
                if (cancelled != null && cancelled()) return null;
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
        private static List<NavigationPoint> CorridorPoints(FieldGeometrySnapshot geometry,int[] corridor,NavigationPoint end,bool departureClear,Dictionary<int,NavigationPoint?> portals)
        {
            var result=new List<NavigationPoint>();
            // Native radius forces can leave the resting player within clearance
            // of a boundary. Depart inward on the same support before the portal;
            // Smooth still requires monotone clearance and a fully clear endpoint.
            if(!departureClear)result.Add(geometry.Find(corridor[0]).Center);
            for(int i=0;i<corridor.Length;i++) {
                var triangle=geometry.Find(corridor[i]);if(i>0)result.Add(triangle.Center);
                if(i+1<corridor.Length) {
                    int edge=Array.IndexOf(triangle.Neighbors,corridor[i+1]);if(edge<0)return null;
                    // Radius constrains physical boundary clearance along this native portal.
                    NavigationPoint crossing;if(!Portal(geometry,triangle,edge,portals,out crossing))return null;result.Add(crossing);
                }
            }result.Add(end);return result;
        }
        private static List<NavigationPoint> Smooth(FieldGeometrySnapshot geometry,NavigationPoint start,List<NavigationPoint> points,Dictionary<SegmentKey,NavigationPoint[]> segments,Dictionary<SegmentKey,FieldTriangle> supports,List<NavigationPoint> steering)
        {
            var result=new List<NavigationPoint>();int index=0;
            while(index<points.Count) {
                int farthest=-1;NavigationPoint[] surface=null;
                for(int i=points.Count-1;i>=index;i--)if(Segment(geometry,start,points[i],segments,supports,out surface)){farthest=i;break;}
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
            Dictionary<SegmentKey,NavigationPoint[]> cache,Dictionary<SegmentKey,FieldTriangle> supports,out NavigationPoint[] surface)
        {
            var key=new SegmentKey(start,end);
            if(!cache.TryGetValue(key,out surface)) {
                if(!geometry.TrySegmentClear(start,end,Support(geometry,start,supports),Support(geometry,end,supports),out surface))surface=null;
                if(cache.Count<4096)cache.Add(key,surface);
            }
            return surface!=null;
        }
        private static FieldTriangle Support(FieldGeometrySnapshot geometry, NavigationPoint point, Dictionary<SegmentKey,FieldTriangle> cache)
        {
            var key = new SegmentKey(point, point);
            FieldTriangle triangle;
            if (!cache.TryGetValue(key, out triangle))
            {
                triangle = geometry.At(point);
                if (cache.Count < 4096) cache.Add(key, triangle);
            }
            return triangle;
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
