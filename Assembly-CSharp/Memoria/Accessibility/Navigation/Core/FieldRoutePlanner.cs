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
            foreach(var approach in candidates) {
                var end=geometry.At(approach);if(end==null){failure.Failure=RouteFailure.TargetOutsideMesh;continue;}
                int[] corridor=Search(geometry,start,end);if(corridor==null){failure.Failure=RouteFailure.Disconnected;continue;}
                var points=CorridorPoints(geometry,corridor,context.Position,approach);if(points==null){failure.Failure=RouteFailure.NarrowPassage;continue;}
                var funnel=Funnel(geometry,corridor,context.Position,approach);
                var smooth=funnel==null?null:Smooth(geometry,context.Position,funnel);
                if(smooth==null)smooth=Smooth(geometry,context.Position,points);
                if(smooth==null){failure.Failure=RouteFailure.UnsafeSegment;continue;}
                var route=new RoutePlan{Success=true,Failure=RouteFailure.None,TargetId=target.Id,PhysicalId=target.PhysicalId,Generation=context.Generation,Transport=context.Transport,ApproachPoint=approach,Waypoints=smooth.ToArray(),Corridor=corridor,GeometryFingerprint=geometry.Fingerprint,ViewingPoint=viewing};
                var from=context.Position;foreach(var point in route.Waypoints){route.Length+=FieldGeometrySnapshot.Horizontal(from,point);from=point;}
                if(viewing)return route; // Viewing candidates are ordered by proximity to the observed object.
                if(best==null || route.Length<best.Length)best=route;
            }return best??failure;
        }
        private static int[] Search(FieldGeometrySnapshot geometry,FieldTriangle start,FieldTriangle end)
        {
            var open=new List<int>{start.Id};var costs=new Dictionary<int,float>{{start.Id,0}};var parent=new Dictionary<int,int>();var closed=new HashSet<int>();
            while(open.Count>0) {
                int current=open[0];float score=costs[current]+FieldGeometrySnapshot.Horizontal(geometry.Find(current).Center,end.Center);
                foreach(int id in open){float value=costs[id]+FieldGeometrySnapshot.Horizontal(geometry.Find(id).Center,end.Center);if(value<score){score=value;current=id;}}
                open.Remove(current);if(current==end.Id){var result=new List<int>{current};while(parent.ContainsKey(current)){current=parent[current];result.Add(current);}result.Reverse();return result.ToArray();}
                closed.Add(current);var triangle=geometry.Find(current);
                for(int edge=0;edge<3;edge++) {
                    var next=geometry.Find(triangle.Neighbors[edge]);if(!geometry.Permitted(next))continue;
                    NavigationPoint crossing;if(!geometry.TryPortalPoint(triangle,edge,out crossing))continue;
                    float tentative=costs[current]+FieldGeometrySnapshot.Horizontal(triangle.Center,next.Center)*Math.Max(1,next.TraversalCost),existing;
                    if(costs.TryGetValue(next.Id,out existing) && tentative>=existing)continue;
                    costs[next.Id]=tentative;parent[next.Id]=current;closed.Remove(next.Id);if(!open.Contains(next.Id))open.Add(next.Id);
                }
            }return null;
        }
        private static List<NavigationPoint> CorridorPoints(FieldGeometrySnapshot geometry,int[] corridor,NavigationPoint start,NavigationPoint end)
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
                    NavigationPoint crossing;if(!geometry.TryPortalPoint(triangle,edge,out crossing))return null;result.Add(crossing);
                }
            }result.Add(end);return result;
        }
        private static List<NavigationPoint> Smooth(FieldGeometrySnapshot geometry,NavigationPoint start,List<NavigationPoint> points)
        {
            var result=new List<NavigationPoint>();int index=0;
            while(index<points.Count) {
                int farthest=-1;NavigationPoint[] surface=null;
                for(int i=points.Count-1;i>=index;i--)if(geometry.TrySegmentClear(start,points[i],out surface)){farthest=i;break;}
                if(farthest<0)return null;result.AddRange(surface);start=surface[surface.Length-1];index=farthest+1;
            }
            return result;
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
