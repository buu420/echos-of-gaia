using System;
namespace Memoria.Accessibility.Navigation
{
    public static class WorldCoordinates
    {
        public const float Width=1536, Depth=1280;
        private static float Delta(float value,float period) { value%=period; if(value>period/2)value-=period;if(value< -period/2)value+=period;return value; }
        public static NavigationPoint Difference(NavigationPoint from,NavigationPoint to) { return new NavigationPoint(Delta(to.X-from.X,Width),to.Y-from.Y,Delta(to.Z-from.Z,Depth)); }
        public static float Distance(NavigationPoint from,NavigationPoint to) { var delta=Difference(from,to);return (float)Math.Sqrt(delta.X*delta.X+delta.Z*delta.Z); }
        public static string Compass(NavigationPoint delta) { if(Math.Abs(delta.X)+Math.Abs(delta.Z)<0.001f)return "here"; string[] names={"north","northeast","east","southeast","south","southwest","west","northwest"}; double degrees=Math.Atan2(delta.X,delta.Z)*180/Math.PI;return names[((int)Math.Round(degrees/45)+8)%8]; }
        public static NavigationPoint Canonical(NavigationPoint point) { float x=point.X%Width,z=point.Z%Depth;if(x<0)x+=Width;if(z>0)z-=Depth;return new NavigationPoint(x,point.Y,z); }
    }
}
