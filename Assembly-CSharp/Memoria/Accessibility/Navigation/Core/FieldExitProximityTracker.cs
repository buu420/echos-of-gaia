using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    public sealed class FieldExitProximityCue
    {
        public TargetIdentity Identity;public float Gain;
    }
    // Blind Soldier's 80/400 native-unit envelope and 3200 ms pulse. A caller
    // must prove a clear native floor segment; distance alone is not enough.
    public sealed class FieldExitProximityTracker
    {
        private sealed class Candidate {public TargetIdentity Id;public NavigationPoint Point;public float Distance;}
        private readonly Dictionary<TargetIdentity,double> _next=new Dictionary<TargetIdentity,double>();
        private string _map,_save;private long _generation;private int _player;private double _time;
        public bool HasAudibleExit {get;private set;}
        public static float VolumeGain(bool enabled,int ownVolume,bool nativeMuted,int nativeVolume)
        {return !enabled || nativeMuted?0:Math.Max(0,Math.Min(200,ownVolume))/100f*Math.Max(0,Math.Min(100,nativeVolume))/100f;}
        public static bool CanPlay(NavigationContext context)
        {return context!=null && context.Foreground && context.CanControl && !context.MinigameInspection && context.GateReason==GateReason.None && context.Position.IsFinite && context.LogicalMapKey!=null && context.LogicalMapKey.StartsWith("field:",StringComparison.Ordinal);}
        public FieldExitProximityCue Update(NavigationContext context,IList<NavigationTarget> targets,double milliseconds,Func<NavigationPoint,bool> clearSegment)
        {
            if(!CanPlay(context) || targets==null || clearSegment==null || Double.IsNaN(milliseconds) || Double.IsInfinity(milliseconds)){Reset();return null;}
            if(_map!=context.LogicalMapKey || _generation!=context.Generation || _save!=context.SaveIdentity || _player!=context.PlayerIdentity || milliseconds<_time)Reset();
            _map=context.LogicalMapKey;_generation=context.Generation;_save=context.SaveIdentity;_player=context.PlayerIdentity;_time=milliseconds;
            var candidates=new List<Candidate>();
            foreach(var target in targets)if(target!=null && target.Category==NavigationCategory.Exits && target.Id.Kind==TargetKind.FieldExit && target.Id.MapKey==_map && target.Generation==_generation && target.IsInteractive && target.ApproachCandidates!=null) {
                foreach(var point in target.ApproachCandidates)if(point.IsFinite) {
                    float distance=NavigationPoint.Distance(context.Position,point);
                    if(distance<400)candidates.Add(new Candidate{Id=target.Id,Point=point,Distance=distance});
                }
            }
            candidates.Sort((a,b)=>a.Distance.CompareTo(b.Distance));
            Candidate nearest=null;foreach(var candidate in candidates)if(clearSegment(candidate.Point)){nearest=candidate;break;}
            HasAudibleExit=nearest!=null;
            if(nearest==null){_next.Clear();return null;}
            // The single recording indicates the nearest clear exit. Listing
            // still provides every exit's identity; overlapping copies add no cue.
            var stale=new List<TargetIdentity>();foreach(var pair in _next)if(!candidates.Exists(c=>c.Id.Equals(pair.Key)))stale.Add(pair.Key);foreach(var id in stale)_next.Remove(id);
            double next;if(_next.TryGetValue(nearest.Id,out next) && milliseconds<next)return null;
            _next[nearest.Id]=milliseconds+3200;
            return new FieldExitProximityCue{Identity=nearest.Id,Gain=nearest.Distance<=80?1:(400-nearest.Distance)/320};
        }
        public void Reset(){_next.Clear();_map=_save=null;_generation=0;_player=-1;_time=0;HasAudibleExit=false;}
    }
}
