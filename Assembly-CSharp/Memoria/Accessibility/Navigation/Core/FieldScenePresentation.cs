namespace Memoria.Accessibility.Navigation
{
    public static class FieldScenePresentation
    {
        public static bool Actor(bool shown,bool renderer,bool inCurrentView,FieldCameraPanorama panorama,NavigationPoint projected)
        {
            return shown && renderer && projected.IsFinite && projected.Z>0 && (inCurrentView || FieldPanoramaPresentation.Contains(panorama,projected));
        }
        public static bool Passage(bool plain,bool activeBackdrop,FieldCameraPanorama panorama,NavigationPoint projected)
        {return plain && activeBackdrop && FieldPanoramaPresentation.ContainsBackdropEdge(panorama,projected);}
    }
}
