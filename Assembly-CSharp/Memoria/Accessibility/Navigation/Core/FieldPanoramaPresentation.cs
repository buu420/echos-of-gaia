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
        {return Contains(camera,projected,0,false);}
        // The same twelve native-pixel edge allowance used to bind rendered
        // background tiles. A cropped walk-off's safe trigger can lie just past
        // the bottom edge while the ordinary path visibly leaves the picture.
        public static bool ContainsBackdropEdge(FieldCameraPanorama camera,NavigationPoint projected)
        {return Contains(camera,projected,12,true);}
        private static bool Contains(FieldCameraPanorama camera,NavigationPoint projected,float edge,bool fixedCamera)
        {
            if(camera==null || !projected.IsFinite || projected.Z<=0 || camera.HalfWidth<=0 || camera.HalfHeight<=0 ||
                camera.MinX>camera.MaxX || camera.MinY>camera.MaxY || !fixedCamera && camera.MinX==camera.MaxX && camera.MinY==camera.MaxY)return false;
            return projected.X>=camera.MinX-2f*camera.HalfWidth-edge && projected.X<=camera.MaxX+edge &&
                projected.Y>=-camera.MaxY-edge && projected.Y<=2f*camera.HalfHeight-camera.MinY+edge;
        }
    }
}
