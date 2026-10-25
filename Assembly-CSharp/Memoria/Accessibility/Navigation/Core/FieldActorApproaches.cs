using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    public static class FieldActorApproaches
    {
        public static NavigationPoint[] Create(NavigationPoint player,NavigationPoint center,float collisionRadius,float talkRadius)
        {
            if(!player.IsFinite || !center.IsFinite || float.IsNaN(collisionRadius) || float.IsNaN(talkRadius))return new NavigationPoint[0];
            float clearance=Math.Max(0,collisionRadius);
            float radius=clearance+4;
            if(talkRadius>clearance && radius>=talkRadius)radius=clearance+(talkRadius-clearance)*0.5f;
            var points=new NavigationPoint[17];
            double initial=Math.Atan2(player.Z-center.Z,player.X-center.X);
            for(int i=0;i<points.Length;i++) {
                double angle=i==0?initial:(i-1)*Math.PI/8;
                points[i]=new NavigationPoint(center.X+(float)Math.Cos(angle)*radius,center.Y,center.Z+(float)Math.Sin(angle)*radius);
            }
            return points;
        }
        public static NavigationPoint[] OnSurfaces(NavigationPoint[] candidates,NavigationPoint actor,FieldGeometrySnapshot geometry)
        {
            var result=new List<NavigationPoint>();
            if(candidates==null || geometry==null)return result.ToArray();
            foreach(var candidate in candidates)foreach(var triangle in geometry.Triangles) {
                if(!candidate.IsFinite || !geometry.Permitted(triangle) || !triangle.Contains(candidate))continue;
                float height=triangle.Height(candidate);
                // Match WalkMesh.Collision's strict vertical talk/body interval.
                if(Math.Abs(height-actor.Y)>=400)continue;
                var point=new NavigationPoint(candidate.X,height,candidate.Z);bool duplicate=false;
                foreach(var old in result)if(NavigationPoint.Distance(old,point)<0.01f){duplicate=true;break;}
                if(!duplicate)result.Add(point);
            }
            return result.ToArray();
        }
    }
}
