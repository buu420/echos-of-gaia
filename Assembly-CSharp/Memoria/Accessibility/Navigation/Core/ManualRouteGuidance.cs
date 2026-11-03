using System;
namespace Memoria.Accessibility.Navigation
{
    public sealed class ManualGuidanceStep
    {
        public NavigationPoint Aim,LegEnd,ForwardVector;
        public float ForwardDistance;public int EndWaypointIndex;
        public bool Correcting,NeedReplan,AtApproach;
    }
    public static class ManualRouteGuidance
    {
        public static ManualGuidanceStep Read(NavigationContext context,RoutePlan route,int index,float bodyRadius,bool wasCorrecting,Func<NavigationPoint,bool> clear)
        {
            var result=new ManualGuidanceStep{NeedReplan=true};
            if(context==null || !context.Position.IsFinite || route==null || !route.Success || !route.Origin.IsFinite || route.Waypoints==null ||
                index<0 || index>=route.Waypoints.Length || !route.Waypoints[index].IsFinite || float.IsNaN(bodyRadius) || float.IsInfinity(bodyRadius))return result;
            bool world=context.LogicalMapKey!=null && context.LogicalMapKey.StartsWith("world:");
            float arrival=world?.5f:4,scale=world?4:240,tolerance=Math.Max(arrival,bodyRadius);
            var start=index==0?route.Origin:route.Waypoints[index-1];var first=Delta(start,route.Waypoints[index],world);float firstLength=Length(first);
            if(!start.IsFinite || float.IsInfinity(firstLength) || float.IsNaN(firstLength))return result;
            if(firstLength<=.001f){start=context.Position;first=Delta(start,route.Waypoints[index],world);firstLength=Length(first);}
            int end=index;float total=firstLength;var previous=route.Waypoints[index];var last=first;var accumulated=first;
            for(int next=index+1;next<route.Waypoints.Length;next++) {
                if(!route.Waypoints[next].IsFinite)return result;
                var leg=Delta(previous,route.Waypoints[next],world);float length=Length(leg);
                if(length<=.001f){end=next;previous=route.Waypoints[next];continue;}
                double cross=(double)last.X*leg.Z-(double)last.Z*leg.X,dot=(double)last.X*leg.X+(double)last.Z*leg.Z;
                double denominator=((double)last.X*last.X+(double)last.Z*last.Z)*length*length;
                if(dot<=0 || cross*cross>denominator*.00000001)break;
                var combined=new NavigationPoint(accumulated.X+leg.X,0,accumulated.Z+leg.Z);
                // Native world segment checks use the shortest wrapped delta.
                // Keep each spoken run within that same direction.
                if(world && !SameHorizontal(Delta(start,route.Waypoints[next],true),combined))break;
                total+=length;end=next;previous=route.Waypoints[next];last=leg;
                accumulated=combined;
            }
            result.LegEnd=route.Waypoints[end];result.EndWaypointIndex=end;
            if(end==route.Waypoints.Length-1 && Length(Delta(context.Position,result.LegEnd,world))<=arrival) {
                if(clear!=null && !clear(result.LegEnd))return result;
                result.Aim=result.LegEnd;result.AtApproach=true;result.NeedReplan=false;return result;
            }
            if(firstLength<=.001f)return result;
            var unit=new NavigationPoint(first.X/firstLength,0,first.Z/firstLength);var offset=Delta(start,context.Position,world);
            float along=offset.X*unit.X+offset.Z*unit.Z,remaining=Math.Max(0,total-along);
            float joinedAlong=Math.Max(0,Math.Min(total,along));
            var join=PointOnRun(start,route.Waypoints,index,end,joinedAlong,world);
            float drift=Length(Delta(context.Position,join,world));
            result.ForwardVector=unit;result.ForwardDistance=remaining;
            var forward=Canonical(new NavigationPoint(context.Position.X+unit.X*remaining,result.LegEnd.Y,context.Position.Z+unit.Z*remaining),world);
            bool close=remaining<=tolerance;
            float returnTolerance=close?arrival:Math.Max(arrival,bodyRadius);
            bool correcting=wasCorrecting?drift>returnTolerance:drift>Math.Max(tolerance,scale*.5f) || close && drift>arrival;
            bool wrappedForward=world && !SameHorizontal(Delta(context.Position,forward,true),new NavigationPoint(unit.X*remaining,0,unit.Z*remaining));
            if(wrappedForward) {
                if(drift<=.001f)return result;
                correcting=true;
            }
            bool forwardClear=true;
            if(!correcting && clear!=null){forwardClear=clear(forward);if(!forwardClear)correcting=drift>arrival;}
            result.Correcting=correcting;result.Aim=correcting?join:forward;
            // Correction returns to the clamped join, so the follow-on count
            // starts there rather than counting the correction a second time.
            if(correcting)result.ForwardDistance=total-joinedAlong;
            result.NeedReplan=correcting?clear!=null && !clear(join):!forwardClear || remaining<=.001f;
            return result;
        }
        private static NavigationPoint PointOnRun(NavigationPoint start,NavigationPoint[] points,int index,int end,float distance,bool world)
        {
            var previous=start;
            for(int i=index;i<=end;i++) {
                var delta=Delta(previous,points[i],world);float length=Length(delta);
                if(distance<=length || i==end) {
                    float t=length<=.001f?0:Math.Max(0,Math.Min(1,distance/length));
                    return Canonical(new NavigationPoint(previous.X+delta.X*t,previous.Y+delta.Y*t,previous.Z+delta.Z*t),world);
                }
                distance-=length;previous=points[i];
            }return points[end];
        }
        private static NavigationPoint Canonical(NavigationPoint p,bool world){return world?WorldCoordinates.Canonical(p):p;}
        private static NavigationPoint Delta(NavigationPoint a,NavigationPoint b,bool world){return world?WorldCoordinates.Difference(a,b):new NavigationPoint(b.X-a.X,b.Y-a.Y,b.Z-a.Z);}
        private static bool SameHorizontal(NavigationPoint a,NavigationPoint b){return Math.Abs(a.X-b.X)<=.01f && Math.Abs(a.Z-b.Z)<=.01f;}
        private static float Length(NavigationPoint p){return (float)Math.Sqrt(p.X*p.X+p.Z*p.Z);}
    }
}
