using System;
namespace Memoria.Accessibility.Navigation
{
    public sealed class RouteGuidanceProgress
    {
        private readonly NavigationPoint[] _points;private NavigationPoint _previous;private readonly bool _world;private readonly float _radius;private int _index;
        public RouteGuidanceProgress(NavigationPoint origin,NavigationPoint[] points,bool world,float radius){_previous=origin;_points=(NavigationPoint[])points.Clone();_world=world;_radius=Math.Max(0.5f,world?radius:FieldAnalogMovement.ArrivalRadius(radius,1));}
        private NavigationPoint Delta(NavigationPoint a,NavigationPoint b){return _world?WorldCoordinates.Difference(a,b):new NavigationPoint(b.X-a.X,b.Y-a.Y,b.Z-a.Z);}
        public NavigationPoint Next(NavigationPoint position)
        {
            while(_index<_points.Length-1) {
                var segment=Delta(_previous,_points[_index]);var offset=Delta(_previous,position);var to=Delta(position,_points[_index]);
                float length=segment.X*segment.X+segment.Z*segment.Z,dot=segment.X*offset.X+segment.Z*offset.Z;
                float cross=segment.X*offset.Z-segment.Z*offset.X;
                bool passed=length>0 && dot>=length && cross*cross<=_radius*_radius*length;
                if(!passed && to.X*to.X+to.Z*to.Z>_radius*_radius)break;
                _previous=_points[_index++];
            }return _points[_index];
        }
    }
}
