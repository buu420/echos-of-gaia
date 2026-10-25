using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    public enum FieldSpatialCueKind { Exit, Npc, Interactable }
    public sealed class FieldSpatialCue
    {
        public readonly FieldSpatialCueKind Kind;
        public readonly TargetIdentity Identity;
        public readonly NavigationPoint Position,Direction;
        public readonly float Gain;
        public readonly long PulseSerial;
        public FieldSpatialCue(FieldSpatialCueKind kind,TargetIdentity identity,NavigationPoint position,NavigationPoint direction,float gain,long pulseSerial)
        {Kind=kind;Identity=identity;Position=position;Direction=direction;Gain=gain;PulseSerial=pulseSerial;}
    }
    public sealed class FieldSpatialCueTracker
    {
        private sealed class Candidate {internal NavigationTarget Target;internal NavigationPoint Approach;internal float Distance;internal int Kind;}
        private readonly Dictionary<TargetIdentity,double> _next=new Dictionary<TargetIdentity,double>();
        private readonly FieldSpatialCue[] _current=new FieldSpatialCue[3];
        private string _map,_save;private long _generation,_serial;private int _player=-1;private double _time,_nextStart;
        public FieldSpatialCue[] Update(NavigationContext context,IList<NavigationTarget> targets,double milliseconds,Func<FieldGeometrySnapshot> readGeometry)
        {
            if(readGeometry==null)throw new ArgumentNullException("readGeometry");FieldGeometrySnapshot geometry=null;bool read=false;
            Func<FieldGeometrySnapshot> current=()=>{if(!read){read=true;geometry=readGeometry();}return geometry!=null && context!=null && geometry.Generation==context.Generation?geometry:null;};
            return Update(context,targets,milliseconds,point=>current()!=null && geometry.IsSegmentClear(context.Position,point),target=>{
                if(!MightBeNear(context,target))return new NavigationPoint[0];var support=current();return support==null?new NavigationPoint[0]:FieldTargetApproaches.Prepare(context,target,support);
            });
        }
        public FieldSpatialCue[] Update(NavigationContext context,IList<NavigationTarget> targets,double milliseconds,Func<NavigationPoint,bool> clearSegment)
        {return Update(context,targets,milliseconds,clearSegment,target=>target.ApproachCandidates);}
        private FieldSpatialCue[] Update(NavigationContext context,IList<NavigationTarget> targets,double milliseconds,Func<NavigationPoint,bool> clearSegment,Func<NavigationTarget,NavigationPoint[]> prepare)
        {
            if(!FieldExitProximityTracker.CanPlay(context) || targets==null || clearSegment==null || Double.IsNaN(milliseconds) || Double.IsInfinity(milliseconds)) {Reset();return new FieldSpatialCue[0];}
            if(_map!=context.LogicalMapKey || _generation!=context.Generation || _save!=context.SaveIdentity || _player!=context.PlayerIdentity || milliseconds<_time)Reset();
            _map=context.LogicalMapKey;_generation=context.Generation;_save=context.SaveIdentity;_player=context.PlayerIdentity;_time=milliseconds;
            var candidates=new List<Candidate>();var present=new HashSet<TargetIdentity>();
            foreach(var target in targets) {
                int kind=Kind(target,context);if(kind<0)continue;var approaches=prepare(target);if(approaches==null)continue;
                foreach(var approach in approaches)if(approach.IsFinite) {
                    float distance=NavigationPoint.Distance(context.Position,approach);
                    if(distance<400) {candidates.Add(new Candidate{Target=target,Approach=approach,Distance=distance,Kind=kind});present.Add(target.Id);}
                }
            }
            candidates.Sort((a,b)=>{int distance=a.Distance.CompareTo(b.Distance);return distance!=0?distance:a.Target.Id.NativeId.CompareTo(b.Target.Id.NativeId);});
            var stale=new List<TargetIdentity>();foreach(var pair in _next)if(!present.Contains(pair.Key))stale.Add(pair.Key);foreach(var id in stale)_next.Remove(id);
            var nearest=new Candidate[3];
            foreach(var candidate in candidates)if(nearest[candidate.Kind]==null && clearSegment(candidate.Approach))nearest[candidate.Kind]=candidate;
            int pulse=-1;float pulseDistance=Single.MaxValue;
            if(milliseconds>=_nextStart)for(int kind=0;kind<3;kind++) {
                var candidate=nearest[kind];if(candidate==null)continue;double next;
                if((!_next.TryGetValue(candidate.Target.Id,out next) || milliseconds>=next) && candidate.Distance<pulseDistance){pulse=kind;pulseDistance=candidate.Distance;}
            }
            var result=new List<FieldSpatialCue>();
            for(int kind=0;kind<3;kind++) {
                var candidate=nearest[kind];if(candidate==null){_current[kind]=null;continue;}
                var position=kind==(int)FieldSpatialCueKind.Exit?candidate.Approach:candidate.Target.Position;NavigationPoint direction;
                if(!TryDirection(context,position,out direction)){_current[kind]=null;continue;}
                long serial=_current[kind]!=null && _current[kind].Identity.Equals(candidate.Target.Id)?_current[kind].PulseSerial:0;
                if(pulse==kind){serial=++_serial;_next[candidate.Target.Id]=milliseconds+3200;_nextStart=milliseconds+250;}
                var cue=new FieldSpatialCue((FieldSpatialCueKind)kind,candidate.Target.Id,position,direction,candidate.Distance<=80?1:(400-candidate.Distance)/320,serial);
                _current[kind]=cue;result.Add(cue);
            }
            if(result.Count==0)_nextStart=0;
            return result.ToArray();
        }
        private static bool MightBeNear(NavigationContext context,NavigationTarget target)
        {
            var points=target.InteractionUid>=0?target.InteractionRegion:target.ApproachCandidates??new[]{target.Position};if(points==null || points.Length==0)return false;
            double minX=Double.PositiveInfinity,minZ=minX,maxX=Double.NegativeInfinity,maxZ=maxX;
            foreach(var point in points)if(point.IsFinite){minX=Math.Min(minX,point.X);minZ=Math.Min(minZ,point.Z);maxX=Math.Max(maxX,point.X);maxZ=Math.Max(maxZ,point.Z);}
            double x=Math.Max(minX-context.Position.X,Math.Max(0,context.Position.X-maxX)),z=Math.Max(minZ-context.Position.Z,Math.Max(0,context.Position.Z-maxZ));return x*x+z*z<400*400;
        }
        private static int Kind(NavigationTarget target,NavigationContext context)
        {
            if(target==null || target.Generation!=context.Generation || target.Id.MapKey!=context.LogicalMapKey || !target.Position.IsFinite || !target.IsInteractive || target.UseViewingPoint)return -1;
            if(target.Evidence==VisibilityEvidence.None || target.Evidence==VisibilityEvidence.ObservedFieldLandmark)return -1;
            if(target.Category==NavigationCategory.Exits && target.Id.Kind==TargetKind.FieldExit)return (int)FieldSpatialCueKind.Exit;
            if(target.Evidence==VisibilityEvidence.ObservedRegion)return -1;
            if(target.Id.Kind!=TargetKind.FieldActor)return -1;
            if(target.Category==NavigationCategory.People)return (int)FieldSpatialCueKind.Npc;
            return target.Category==NavigationCategory.Objects || target.Category==NavigationCategory.Items?(int)FieldSpatialCueKind.Interactable:-1;
        }
        public static bool TryDirection(NavigationContext context,NavigationPoint source,out NavigationPoint direction)
        {
            direction=new NavigationPoint();
            if(context==null || !context.Position.IsFinite || !source.IsFinite || Single.IsNaN(context.InputOrientationDegrees) || Single.IsInfinity(context.InputOrientationDegrees))return false;
            double x=(double)source.X-context.Position.X,y=(double)source.Y-context.Position.Y,z=(double)source.Z-context.Position.Z,angle=context.InputOrientationDegrees*Math.PI/180;
            double right=x*Math.Cos(angle)-z*Math.Sin(angle),forward=x*Math.Sin(angle)+z*Math.Cos(angle),length=Math.Sqrt(x*x+y*y+z*z);
            direction=length<.001?new NavigationPoint(0,0,-1):new NavigationPoint((float)(right/length),(float)(y/length),(float)(-forward/length));
            return direction.IsFinite;
        }
        public void Reset(){_next.Clear();for(int i=0;i<3;i++)_current[i]=null;_map=_save=null;_generation=0;_player=-1;_time=_nextStart=0;}
    }
}
