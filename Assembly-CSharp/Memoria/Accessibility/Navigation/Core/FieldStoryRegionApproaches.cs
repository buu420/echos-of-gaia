using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    public static class FieldStoryRegionApproaches
    {
        public static NavigationPoint[] Read(bool currentStory,bool triggerAllowed,NavigationPoint[] candidates,Func<NavigationPoint,bool> nativeFirst,Func<NavigationPoint,bool> presented,NavigationPoint player)
        {
            var safe=new List<NavigationPoint>();
            if(!currentStory || !triggerAllowed || candidates==null || nativeFirst==null || presented==null || !player.IsFinite)return safe.ToArray();
            foreach(var point in candidates)if(point.IsFinite && nativeFirst(point) && presented(point))safe.Add(point);
            safe.Sort((a,b)=>NavigationPoint.Distance(player,a).CompareTo(NavigationPoint.Distance(player,b)));return safe.ToArray();
        }
    }
}
