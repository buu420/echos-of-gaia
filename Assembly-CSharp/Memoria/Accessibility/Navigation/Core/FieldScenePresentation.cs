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
        public static bool Door(int animationCamera,int currentCamera,System.Collections.Generic.IList<DoorTileObservation> frames,
            bool inCurrentView,bool activeBackdrop,FieldCameraPanorama panorama,NavigationPoint panoramaPoint,NavigationPoint tilePoint,out int overlay)
        {
            overlay=-1;
            if(!inCurrentView && !Passage(true,activeBackdrop,panorama,panoramaPoint))return false;
            return FieldDoorEvidence.TryBind(animationCamera,currentCamera,frames,tilePoint,true,out overlay);
        }
        public static bool ReviewedWalkOut(bool plain,bool inCurrentView,bool currentCamera,bool activeBackdrop,FieldCameraPanorama panorama,NavigationPoint projected)
        {
            return currentCamera && projected.IsFinite && projected.Z>0 &&
                FieldPanoramaPresentation.PresentsPassage(plain,inCurrentView,activeBackdrop,panorama,projected);
        }
    }
}
