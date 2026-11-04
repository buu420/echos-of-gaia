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
            if(goal==null || geometry==null || !player.IsFinite || goal.Satisfied(player))return new NavigationPoint[0];
            var start=geometry.Find(geometry.ActiveTriangle);if(!geometry.Permitted(start) || !start.Contains(player))start=geometry.At(player);
            if(start==null)return new NavigationPoint[0];
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
                if(support!=null && reachable.Contains(support.Id) && goal.Satisfied(point))safe.Add(point);
            }
            safe.Sort((a,b)=>NavigationPoint.Distance(player,a).CompareTo(NavigationPoint.Distance(player,b)));return safe.ToArray();
        }
    }
}
