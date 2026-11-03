using System;
namespace Memoria.Accessibility.Navigation
{
    public sealed class RouteGuidanceProgress
    {
        private readonly NavigationPoint[] _points;private readonly NavigationPoint _origin;private NavigationPoint _previous;private readonly bool _world;private readonly float _radius,_bodyRadius;private int _index;private bool _correcting;
        public RouteGuidanceProgress(NavigationPoint origin,NavigationPoint[] points,bool world,float radius){_origin=_previous=origin;_points=(NavigationPoint[])points.Clone();_world=world;_bodyRadius=Math.Max(.5f,radius);_radius=Math.Max(0.5f,world?radius:FieldAnalogMovement.ArrivalRadius(radius,1));}
        private NavigationPoint Delta(NavigationPoint a,NavigationPoint b){return _world?WorldCoordinates.Difference(a,b):new NavigationPoint(b.X-a.X,b.Y-a.Y,b.Z-a.Z);}
        public NavigationPoint Next(NavigationPoint position)
        {return Advance(position,_radius,null);}
        public void ObserveWaypoint(NavigationPoint waypoint)
        {
            for(int i=_index;i<_points.Length;i++) {
                var delta=Delta(_points[i],waypoint);
                if(delta.X*delta.X+delta.Y*delta.Y+delta.Z*delta.Z>.0001f)continue;
                _index=i;_previous=i==0?_origin:_points[i-1];return;
            }
        }
        public ManualGuidanceStep Read(NavigationContext context,RoutePlan route,Func<NavigationPoint,bool> clear)
        {
            Advance(context.Position,_bodyRadius,clear);
            var step=ManualRouteGuidance.Read(context,route,_index,_bodyRadius,_correcting,clear);_correcting=step.Correcting;return step;
        }
        private NavigationPoint Advance(NavigationPoint position,float radius,Func<NavigationPoint,bool> clear)
        {
            while(_index<_points.Length-1) {
                var segment=Delta(_previous,_points[_index]);var offset=Delta(_previous,position);var to=Delta(position,_points[_index]);
                float length=segment.X*segment.X+segment.Z*segment.Z,dot=segment.X*offset.X+segment.Z*offset.Z;
                float cross=segment.X*offset.Z-segment.Z*offset.X;
                bool passed=length>0 && dot>=length && cross*cross<=radius*radius*length;
                if(!passed && to.X*to.X+to.Z*to.Z>radius*radius)break;
                if(clear!=null && !clear(_points[_index+1]))break;
                _previous=_points[_index++];
            }return _points[_index];
        }
    }
}
