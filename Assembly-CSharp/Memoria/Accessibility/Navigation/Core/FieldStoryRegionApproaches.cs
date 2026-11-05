using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    public enum StoryRegionTrigger { Confirm,Walk }
    // What one prospective point proves on one trigger path: the current generic step
    // (Int32.MinValue when none) and the binding's counter milestones.
    public sealed class StoryRegionProof
    {
        public readonly int Step;public readonly int[] Milestones;
        public StoryRegionProof(int step,int[] milestones)
        {
            Step=step;Milestones=milestones==null?new int[0]:(int[])milestones.Clone();Array.Sort(Milestones);
        }
        public bool Proves{get{return Step!=Int32.MinValue || Milestones.Length!=0;}}
        public bool SameAs(StoryRegionProof other)
        {
            if(other==null || other.Step!=Step || other.Milestones.Length!=Milestones.Length)return false;
            for(int i=0;i<Milestones.Length;i++)if(other.Milestones[i]!=Milestones[i])return false;
            return true;
        }
    }
    public static class FieldStoryRegionApproaches
    {
        public static NavigationPoint[] Read(bool currentStory,bool triggerAllowed,NavigationPoint[] candidates,Func<NavigationPoint,bool> nativeFirst,Func<NavigationPoint,bool> presented,NavigationPoint player)
        {
            var safe=new List<NavigationPoint>();
            if(!currentStory || !triggerAllowed || candidates==null || nativeFirst==null || presented==null || !player.IsFinite)return safe.ToArray();
            foreach(var point in candidates)if(point.IsFinite && nativeFirst(point) && presented(point))safe.Add(point);
            safe.Sort((a,b)=>NavigationPoint.Distance(player,a).CompareTo(NavigationPoint.Distance(player,b)));return safe.ToArray();
        }
        // Native trigger paths a story region may offer now. Each needs its own native gate:
        // a current Confirm step needs the Talk gate (CheckQuadTalk/IsQuadTalkable) and is never
        // authorized by the Push gate. A walk-in is proven only by the Push (tag 2) handler.
        public static StoryRegionTrigger[] Triggers(bool confirmStep,bool confirmGate,bool pushGate)
        {
            var paths=new List<StoryRegionTrigger>();
            if(confirmStep && confirmGate)paths.Add(StoryRegionTrigger.Confirm);
            if(pushGate)paths.Add(StoryRegionTrigger.Walk);
            return paths.ToArray();
        }
        // The script tag the native request runs for this path (EventCollision: Request tag 3 or 2).
        public static int Tag(StoryRegionTrigger path){return path==StoryRegionTrigger.Confirm?3:2;}
        // TreadQuad mode 4 (first Talk region) for Confirm, mode 2 (first Push region) for a walk-in.
        public static bool Selects(StoryRegionTrigger path,object firstTalk,object firstPush,object region)
        {return region!=null && ReferenceEquals(path==StoryRegionTrigger.Confirm?firstTalk:firstPush,region);}
        // Indices of the approach points (nearest first) that prove one current action. The current
        // position's proof decides when it has one; otherwise the nearest proving point does. Points
        // proving another step or milestone set are a different future action and are never mixed.
        public static int[] Proven(IList<StoryRegionProof> proofs,StoryRegionProof current,int maximum)
        {
            var kept=new List<int>();if(proofs==null || maximum<=0)return kept.ToArray();
            StoryRegionProof offered=current!=null && current.Proves?current:null;
            if(offered==null)foreach(var proof in proofs)if(proof!=null && proof.Proves){offered=proof;break;}
            if(offered==null)return kept.ToArray();
            for(int i=0;i<proofs.Count && kept.Count<maximum;i++)if(proofs[i]!=null && proofs[i].SameAs(offered))kept.Add(i);
            return kept.ToArray();
        }
    }
}
namespace Memoria.Accessibility.Navigation
{
    // Per-point proofs of one region trigger path. Reused only while the scope, the detached memory
    // the proof reads (including requested directors, wait owners and guards) and the approach
    // surface are unchanged, and for at most maxAge frames.
    public sealed class StoryRegionProofCache<T> where T:class
    {
        private sealed class Entry{public string Scope;public object Surface;public byte[][] Memory;public int Frame;public readonly Dictionary<NavigationPoint,T> Points=new Dictionary<NavigationPoint,T>();}
        private readonly Dictionary<string,Entry> _entries=new Dictionary<string,Entry>();
        private readonly int _maxAge;
        public StoryRegionProofCache(int maxAge){_maxAge=maxAge;}
        public Func<NavigationPoint,T> Begin(string identity,string scope,object surface,byte[][] memory,int frame,Func<NavigationPoint,T> evaluate)
        {
            Entry entry;
            if(!_entries.TryGetValue(identity,out entry) || entry.Scope!=scope || !ReferenceEquals(entry.Surface,surface) || !Same(entry.Memory,memory) || frame<entry.Frame || frame-entry.Frame>=_maxAge) {
                if(_entries.Count>=64)_entries.Clear();
                entry=new Entry{Scope=scope,Surface=surface,Memory=Copy(memory),Frame=frame};_entries[identity]=entry;
            }
            return point=>{T value;if(!entry.Points.TryGetValue(point,out value)){value=evaluate(point);if(entry.Points.Count<256)entry.Points[point]=value;}return value;};
        }
        private static byte[][] Copy(byte[][] memory)
        {
            if(memory==null)return null;var result=new byte[memory.Length][];
            for(int i=0;i<memory.Length;i++)result[i]=memory[i]==null?null:(byte[])memory[i].Clone();
            return result;
        }
        private static bool Same(byte[][] a,byte[][] b)
        {
            if(a==null || b==null)return a==b;if(a.Length!=b.Length)return false;
            for(int i=0;i<a.Length;i++) {
                if(a[i]==null || b[i]==null){if(a[i]!=b[i])return false;continue;}
                if(a[i].Length!=b[i].Length)return false;for(int j=0;j<a[i].Length;j++)if(a[i][j]!=b[i][j])return false;
            }
            return true;
        }
    }
}
