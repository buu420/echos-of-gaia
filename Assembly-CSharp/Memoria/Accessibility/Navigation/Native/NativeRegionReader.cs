using System;
namespace Memoria.Accessibility.Navigation
{
    public static class NativeRegionReader
    {
        public static bool TryRead(out int regionId,out string label)
        {
            regionId=-1;label=null;var actor=ff9.w_moveActorPtr;
            if(actor==null || actor.originalActor==null || ff9.w_moveCHRStatus==null)return false;
            int index=actor.originalActor.index;if(index<0 || index>=ff9.w_moveCHRStatus.Length)return false;
            int id=ff9.w_moveCHRStatus[index].id;regionId=ff9.m_GetIDArea(id);
            if(!RegionObservations.ShouldDisplay(regionId,ff9.m_GetIDTopograph(id)))return false;
            label=ff9.w_worldLocationName();return !string.IsNullOrEmpty(label);
        }
    }
}
