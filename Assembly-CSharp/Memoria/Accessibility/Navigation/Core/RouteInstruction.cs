using System;
using System.Globalization;
namespace Memoria.Accessibility.Navigation
{
    // Counts describe a short distance, never a number of key presses or footfalls.
    // Eight default field walking updates (8 * 30); comparable world distance.
    public sealed class RouteInstruction
    {
        private const float FieldUnitsPerCount=240,WorldUnitsPerCount=4;
        private static readonly string[] Directions={"up","up and right","right","down and right","down","down and left","left","up and left"};
        public string Text,Direction;
        public int EndWaypointIndex=-1;
        public bool HasRoute,AtApproach;

        public static RouteInstruction Resolve(NavigationUpdate update)
        {
            var result=new RouteInstruction();var c=update.Context;var target=update.Selected;
            if(target.PhysicalIdentity.Kind==TargetKind.Story) {result.Direction="story";result.Text=target.Label+" "+target.Availability;return result;}
            if(update.IsPlanning || update.RouteOperation!=RouteOperation.None) {result.Direction="planning";result.Text="Finding a walking route.";return result;}
            var movement=update.Movement;
            result.HasRoute=movement!=null && movement.RouteAvailable && movement.RoutePoint.HasValue;
            var point=result.HasRoute?movement.RoutePoint.Value:target.Position;
            bool world=c.LogicalMapKey!=null && c.LogicalMapKey.StartsWith("world:",StringComparison.Ordinal);
            float scale=world?WorldUnitsPerCount:FieldUnitsPerCount;
            var delta=Difference(c.Position,point,world);float distance=Horizontal(delta);
            var route=result.HasRoute?movement.PlannedRoute:null;
            int index=Find(route,point);float arrivalRadius=world?0.5f:4;
            // A native follower may still expose the just-reached corner for
            // one update. It is a turn, not arrival at the final target.
            while(index>=0 && index<route.Waypoints.Length-1 && distance<=arrivalRadius) {
                point=route.Waypoints[++index];delta=Difference(c.Position,point,world);distance=Horizontal(delta);
            }
            result.EndWaypointIndex=index;
            // Arrival is a geometric approach test, independent of count rounding.
            if(result.HasRoute && index>=0 && index==route.Waypoints.Length-1 && distance<=arrivalRadius) {
                result.AtApproach=true;result.Direction="approach";
                result.Text=route.ViewingPoint?"Viewing point reached.":"Route approach reached.";return result;
            }
            double counted;
            result.Direction=DirectionFor(delta,c,world,out counted);
            if(index>=0 && distance>0.001f) {
                var previous=point;var lastDelta=delta;
                for(int next=index+1;next<route.Waypoints.Length;next++) {
                    var leg=Difference(previous,route.Waypoints[next],world);float length=Horizontal(leg);
                    if(length<=0.001f) {result.EndWaypointIndex=next;previous=route.Waypoints[next];continue;}
                    // Combine collinear points only. Even a sub-count bend remains
                    // a separate leg; speech must not shortcut native obstacles.
                    double cross=(double)lastDelta.X*leg.Z-(double)lastDelta.Z*leg.X;
                    double dot=(double)lastDelta.X*leg.X+(double)lastDelta.Z*leg.Z;
                    double denominator=((double)lastDelta.X*lastDelta.X+(double)lastDelta.Z*lastDelta.Z)*length*length;
                    if(dot<=0 || cross*cross>denominator*0.00000001)break;
                    double amount;string direction=DirectionFor(leg,c,world,out amount);
                    if(direction!=result.Direction)break;
                    counted+=amount;result.EndWaypointIndex=next;previous=route.Waypoints[next];lastDelta=leg;
                }
            }
            int count=(int)Math.Round(counted/scale,MidpointRounding.AwayFromZero);
            string instruction=result.Direction=="here"?"Target is here.":result.Direction+" "+(count==0?"a little":count.ToString(CultureInfo.InvariantCulture))+".";
            result.Text=result.HasRoute?instruction:"Bearing only, "+instruction+" No walking route.";
            return result;
        }
        private static int Find(RoutePlan route,NavigationPoint point)
        {
            if(route==null || !route.Success)return -1;
            for(int i=0;i<route.Waypoints.Length;i++)if(NavigationPoint.Distance(route.Waypoints[i],point)<0.001f)return i;
            return -1;
        }
        private static NavigationPoint Difference(NavigationPoint from,NavigationPoint to,bool world)
        {
            return world?WorldCoordinates.Difference(from,to):new NavigationPoint(to.X-from.X,to.Y-from.Y,to.Z-from.Z);
        }
        private static float Horizontal(NavigationPoint delta){return (float)Math.Sqrt(delta.X*delta.X+delta.Z*delta.Z);}
        private static string DirectionFor(NavigationPoint delta,NavigationContext context,bool world,out double distance)
        {
            distance=Horizontal(delta);
            if(world && context.WorldControlType!=0)return WorldCoordinates.Compass(delta);
            double angle=context.InputOrientationDegrees*Math.PI/180;
            double right=delta.X*Math.Cos(angle)-delta.Z*Math.Sin(angle),up=delta.X*Math.Sin(angle)+delta.Z*Math.Cos(angle);
            double horizontal=Math.Abs(right),vertical=Math.Abs(up),dominant=Math.Max(horizontal,vertical),secondary=Math.Min(horizontal,vertical);
            if(dominant<0.001)return "here";
            // Diagonal intent must survive even when the entire leg is shorter
            // than one count; count rounding must never change the path shape.
            if(secondary>=dominant*0.5) {
                int sector=((int)Math.Round(Math.Atan2(right,up)*4/Math.PI)+8)%8;return Directions[sector];
            }
            distance=dominant;
            return vertical>horizontal?(up>0?"up":"down"):(right>0?"right":"left");
        }
    }
}
