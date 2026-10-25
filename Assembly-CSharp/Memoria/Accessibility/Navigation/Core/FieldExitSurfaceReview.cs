using System;
namespace Memoria.Accessibility.Navigation
{
    public sealed class FieldExitSurfaceReview
    {
        public string Scene,Script,Background,Walkmesh,Cue;
        public int ScriptId,Camera;
        public NavigationPoint[] Region;
        public int[] OverlayIds;
        public bool Matches(string scene,int scriptId,int camera,string script,string background,string walkmesh,NavigationPoint[] region)
        {
            if(Scene!=scene || ScriptId!=scriptId || Camera!=camera || String.IsNullOrEmpty(Script) || Script!=script || String.IsNullOrEmpty(Background) || Background!=background || String.IsNullOrEmpty(Walkmesh) || Walkmesh!=walkmesh || Region==null || region==null || Region.Length<3 || Region.Length!=region.Length)return false;
            for(int i=0;i<Region.Length;i++)if(!Region[i].IsFinite || !region[i].IsFinite || Region[i].X!=region[i].X || Region[i].Z!=region[i].Z)return false;
            return true;
        }
    }
}
