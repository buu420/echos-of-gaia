using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    public static class OpeningPositionApproaches
    {
        // Twice the maximum owned arrival radius. Reaching a waypoint must
        // still satisfy the native rounded-coordinate wait before it is popped.
        public const float StopMargin=8;
        public static NavigationPoint[] Read(OpeningPositionGoal goal,FieldGeometrySnapshot geometry,NavigationPoint player)
        {return Read(goal,geometry,player,true);}
        public static NavigationPoint[] Read(OpeningPositionGoal goal,FieldGeometrySnapshot geometry,NavigationPoint player,bool nearBoundary)
        {
            if(goal==null || geometry==null || !player.IsFinite)return new NavigationPoint[0];
            var start=geometry.Find(geometry.ActiveTriangle);if(!geometry.Permitted(start) || !start.Contains(player))start=geometry.At(player);
            if(start==null || goal.Satisfied(player,start.Floor))return new NavigationPoint[0];
            var reachable=new HashSet<int>{start.Id};var pending=new Queue<FieldTriangle>();pending.Enqueue(start);
            float minX=float.MaxValue,maxX=float.MinValue,minZ=float.MaxValue,maxZ=float.MinValue;
            while(pending.Count>0) {
                var triangle=pending.Dequeue();
                foreach(var p in triangle.Vertices){minX=Math.Min(minX,p.X);maxX=Math.Max(maxX,p.X);minZ=Math.Min(minZ,p.Z);maxZ=Math.Max(maxZ,p.Z);}
                for(int edge=0;edge<3;edge++) {
                    int id=triangle.Neighbors[edge];NavigationPoint portal;
                    if(reachable.Contains(id) || !geometry.TryPortalPoint(triangle,edge,out portal))continue;
                    reachable.Add(id);pending.Enqueue(geometry.Find(id));
                }
            }
            if(goal.Axis==OpeningGoalAxis.Y)return HeightPoints(goal,geometry,player,reachable,nearBoundary);
            if(goal.Axis==OpeningGoalAxis.X){if(goal.Minimum.HasValue)minX=Math.Max(minX,goal.Minimum.Value+StopMargin);if(goal.Maximum.HasValue)maxX=Math.Min(maxX,goal.Maximum.Value-StopMargin);}
            else {if(goal.Minimum.HasValue)minZ=Math.Max(minZ,goal.Minimum.Value+StopMargin);if(goal.Maximum.HasValue)maxZ=Math.Min(maxZ,goal.Maximum.Value-StopMargin);}
            // Aim just across the wait boundary. Scanning the entire rest of a
            // large rooftop creates hundreds of irrelevant far-away endpoints.
            // The native adapter can explicitly request the full span if the
            // near band has no currently presented safe approach.
            if(nearBoundary) {
                float band=Math.Max(64,geometry.Radius*2);
                if(goal.Axis==OpeningGoalAxis.X){if(player.X<minX)maxX=Math.Min(maxX,minX+band);else if(player.X>maxX)minX=Math.Max(minX,maxX-band);}
                else {if(player.Z<minZ)maxZ=Math.Min(maxZ,minZ+band);else if(player.Z>maxZ)minZ=Math.Max(minZ,maxZ-band);}
            }
            if(minX>=maxX || minZ>=maxZ)return new NavigationPoint[0];
            var polygon=new[]{new NavigationPoint(minX,0,minZ),new NavigationPoint(maxX,0,minZ),new NavigationPoint(maxX,0,maxZ),new NavigationPoint(minX,0,maxZ)};
            var safe=new List<NavigationPoint>();
            foreach(var point in FieldRegionApproaches.Interaction(polygon,geometry)) {
                var support=geometry.At(point);
                if(support!=null && reachable.Contains(support.Id) && goal.Satisfied(point,support.Floor) &&
                    (!goal.MinimumHeight.HasValue || point.Y>=goal.MinimumHeight.Value+StopMargin*HeightScale(support)))safe.Add(point);
            }
            safe.Sort((a,b)=>NavigationPoint.Distance(player,a).CompareTo(NavigationPoint.Distance(player,b)));return safe.ToArray();
        }
        private static NavigationPoint[] HeightPoints(OpeningPositionGoal goal,FieldGeometrySnapshot geometry,NavigationPoint player,HashSet<int> reachable,bool nearBoundary)
        {
            var safe=new List<NavigationPoint>();
            foreach(var triangle in geometry.Triangles)if(reachable.Contains(triangle.Id) && (!goal.SupportFloor.HasValue || goal.SupportFloor==triangle.Floor)) {
                // Arrival tolerance is horizontal. Scale vertical clearance by
                // this floor's gradient so a steep stair cannot stop too low.
                float scale=HeightScale(triangle),margin=StopMargin*scale;
                float minimum=goal.Minimum.HasValue?goal.Minimum.Value+margin:float.MinValue;
                float maximum=goal.Maximum.HasValue?goal.Maximum.Value-margin:float.MaxValue;
                if(nearBoundary){float band=Math.Max(64,geometry.Radius*2)*scale;
                    if(player.Y<minimum)maximum=Math.Min(maximum,minimum+band);
                    else if(player.Y>maximum)minimum=Math.Max(minimum,maximum-band);
                }
                if(minimum>maximum)continue;
                var polygon=ClipHeight(triangle.Vertices,minimum,true);polygon=ClipHeight(polygon,maximum,false);
                if(polygon.Length<3)continue;
                foreach(var point in FieldRegionApproaches.Interaction(polygon,geometry)) {
                    var support=geometry.At(point);if(support==null || !reachable.Contains(support.Id) || !goal.Satisfied(point,support.Floor))continue;
                    float clearance=StopMargin*HeightScale(support);
                    if(goal.Minimum.HasValue && point.Y<goal.Minimum.Value+clearance-.01f || goal.Maximum.HasValue && point.Y>goal.Maximum.Value-clearance+.01f)continue;
                    if(!safe.Contains(point))safe.Add(point);
                }
            }
            safe.Sort((a,b)=>NavigationPoint.Distance(player,a).CompareTo(NavigationPoint.Distance(player,b)));return safe.ToArray();
        }
        private static float HeightScale(FieldTriangle triangle)
        {
            var p=triangle.Vertices[0];
            float x=triangle.Height(new NavigationPoint(p.X+1,p.Y,p.Z))-p.Y,z=triangle.Height(new NavigationPoint(p.X,p.Y,p.Z+1))-p.Y;
            return Math.Max(1,(float)Math.Sqrt(x*x+z*z));
        }
        private static NavigationPoint[] ClipHeight(NavigationPoint[] polygon,float boundary,bool minimum)
        {
            var result=new List<NavigationPoint>();if(polygon==null || polygon.Length==0)return result.ToArray();
            var previous=polygon[polygon.Length-1];bool before=minimum?previous.Y>=boundary:previous.Y<=boundary;
            foreach(var current in polygon) {
                bool inside=minimum?current.Y>=boundary:current.Y<=boundary;
                if(inside!=before){float t=(boundary-previous.Y)/(current.Y-previous.Y);result.Add(new NavigationPoint(previous.X+(current.X-previous.X)*t,boundary,previous.Z+(current.Z-previous.Z)*t));}
                if(inside)result.Add(current);previous=current;before=inside;
            }
            return result.ToArray();
        }
    }
}
