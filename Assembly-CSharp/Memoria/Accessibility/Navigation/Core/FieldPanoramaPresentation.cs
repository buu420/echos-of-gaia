namespace Memoria.Accessibility.Navigation
{
    public sealed class FieldCameraPanorama
    {
        public int HalfWidth,HalfHeight,MinX,MaxX,MinY,MaxY;
    }
    public static class FieldPanoramaPresentation
    {
        public static bool IsOpaque(float alpha){return !float.IsNaN(alpha) && !float.IsInfinity(alpha) && alpha>=1;}
        public static bool PresentsPassage(bool plainPassage,bool inCurrentView,bool activeBackdrop,FieldCameraPanorama camera,NavigationPoint projected)
        {return plainPassage && activeBackdrop && (inCurrentView || Contains(camera,projected));}
        // Union of the ordinary native camera's permitted view rectangles.
        // CenterCameraOnPlayer places its center at (panX-halfWidth,
        // halfHeight-panY). This is a read-only presentation test, not a pan.
        public static bool Contains(FieldCameraPanorama camera,NavigationPoint projected)
        {
            if(camera==null || !projected.IsFinite || projected.Z<=0 || camera.HalfWidth<=0 || camera.HalfHeight<=0 ||
                camera.MinX>camera.MaxX || camera.MinY>camera.MaxY || camera.MinX==camera.MaxX && camera.MinY==camera.MaxY)return false;
            return projected.X>=camera.MinX-2f*camera.HalfWidth && projected.X<=camera.MaxX &&
                projected.Y>=-camera.MaxY && projected.Y<=2f*camera.HalfHeight-camera.MinY;
        }
    }
}
