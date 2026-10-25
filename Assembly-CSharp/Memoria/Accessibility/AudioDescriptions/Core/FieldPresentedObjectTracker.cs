using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.AudioDescriptions
{
    public sealed partial class FieldDescriptionTracker
    {
        private readonly FieldPresentedObjectCatalog _presentedCatalog;
        private const int MaxPresentedActors=256,PresentedSamples=3;
        private sealed class PresentedObservation
        {
            internal FieldPresentedObjectDescription Entry;
            internal long LastFrame;
            internal int VisibleSamples,FirstAnim,LastAnim,FrameCount,Progressed,Age;
            internal float OriginX,OriginY,OriginZ,LastX,LastY,LastZ;
        }
        private readonly Dictionary<long,PresentedObservation> _presentedPending=new Dictionary<long,PresentedObservation>();
        private readonly HashSet<string> _presentedSeen=new HashSet<string>(StringComparer.Ordinal);
        public int PresentedPendingCount { get { return _presentedPending.Count; } }
        public bool HasPresentedObjects(int map){return _presentedCatalog.HasMap(map);}
        public bool CanReviewPresentedObject(int map,int sid,int model,string clip){return _presentedCatalog.CanReview(map,sid,model,clip);}
        public bool IsPresentedPending(long actor){return _presentedPending.ContainsKey(actor);}
        public bool NeedsPresentedObjectSample(long actor,int map,int sid,string entrySha256,int model,string clip)
        {
            ObserveActor(actor,model);
            var entry=_presentedCatalog.Find(map,sid,entrySha256,model,clip);
            if(entry==null || _presentedSeen.Contains(entry.CueId)){_presentedPending.Remove(actor);return false;}
            PresentedObservation pending;
            if(_presentedPending.TryGetValue(actor,out pending) && !Object.ReferenceEquals(pending.Entry,entry))_presentedPending.Remove(actor);
            return true;
        }
        public void SamplePresentedObject(FieldPresentedObjectSample sample)
        {
            if(sample==null)return;
            ObserveActor(sample.Actor,sample.Model);
            var entry=_presentedCatalog.Find(sample.Map,sample.Sid,sample.EntrySha256,sample.Model,sample.Clip);
            if(entry==null || _presentedSeen.Contains(entry.CueId) || sample.PresentationFrame<0 || !sample.ClipPresent || !sample.Visible || !sample.ScreenClear)
            {_presentedPending.Remove(sample.Actor);return;}
            bool animated=entry.Mode==FieldPresentedObjectMode.Animated,moving=entry.Mode==FieldPresentedObjectMode.Moving;
            if(animated && (!sample.FullRange || !sample.Forward || !sample.NonJump || sample.FrameCount<2 || sample.FrameCount>=Byte.MaxValue || sample.InFrame!=0 || sample.OutFrame<sample.FrameCount-1 || sample.AnimFrame<0 || sample.AnimFrame>=sample.FrameCount) ||
                moving && (!sample.PositionKnown || !Finite(sample.X,sample.Y,sample.Z) || sample.Speed<0 || sample.Speed>Byte.MaxValue))
            {_presentedPending.Remove(sample.Actor);return;}
            PresentedObservation pending;
            if(!_presentedPending.TryGetValue(sample.Actor,out pending) || !Object.ReferenceEquals(pending.Entry,entry)) {
                _presentedPending.Remove(sample.Actor);
                if(_presentedPending.Count>=MaxPresentedActors)return;
                _presentedPending[sample.Actor]=new PresentedObservation {
                    Entry=entry,LastFrame=sample.PresentationFrame,VisibleSamples=1,FirstAnim=sample.AnimFrame,LastAnim=sample.AnimFrame,FrameCount=sample.FrameCount,
                    OriginX=sample.X,OriginY=sample.Y,OriginZ=sample.Z,LastX=sample.X,LastY=sample.Y,LastZ=sample.Z
                };
                return;
            }
            if(sample.PresentationFrame<pending.LastFrame || animated && (sample.FrameCount!=pending.FrameCount || sample.AnimFrame<pending.LastAnim))
            {_presentedPending.Remove(sample.Actor);return;}
            if(sample.PresentationFrame==pending.LastFrame)return;
            if(moving) {
                double dx=(double)sample.X-pending.LastX,dy=(double)sample.Y-pending.LastY,dz=(double)sample.Z-pending.LastZ;
                double bound=Math.Max(TeleportDistance,4f*sample.Speed);
                if(dx*dx+dy*dy+dz*dz>bound*bound){_presentedPending.Remove(sample.Actor);return;}
                pending.LastX=sample.X;pending.LastY=sample.Y;pending.LastZ=sample.Z;
            }
            pending.LastFrame=sample.PresentationFrame;pending.VisibleSamples++;
            if(animated && sample.AnimFrame!=pending.LastAnim){pending.Progressed++;pending.LastAnim=sample.AnimFrame;}
            if(pending.VisibleSamples<PresentedSamples)return;
            if(animated && (pending.Progressed<ProofSamples || (double)(pending.LastAnim-pending.FirstAnim)/(pending.FrameCount-1)<ProofFraction))return;
            if(moving) {
                double dx=(double)sample.X-pending.OriginX,dy=(double)sample.Y-pending.OriginY,dz=(double)sample.Z-pending.OriginZ;
                if(dx*dx+dy*dy+dz*dz<=.25)return;
            }
            _presentedPending.Remove(sample.Actor);
            if(_presentedSeen.Add(entry.CueId))_emit(new FieldDescriptionEmission(entry.CueId,entry.VoiceText,_generation,sample.Actor,_actors[sample.Actor].Token,"presented object "+entry.Map+":"+entry.Sid+" "+entry.Mode));
        }
        private void InvalidatePresentedObjects(){_presentedPending.Clear();}
        private void ResetPresentedObjects(){_presentedSeen.Clear();}
        private void EndPresentedFrame(HashSet<long> live)
        {
            if(_presentedPending.Count==0)return;
            var drop=new List<long>();
            foreach(var pair in _presentedPending)if(!live.Contains(pair.Key) || ++pair.Value.Age>PendingLimitFrames)drop.Add(pair.Key);
            foreach(long actor in drop)_presentedPending.Remove(actor);
        }
    }
}
