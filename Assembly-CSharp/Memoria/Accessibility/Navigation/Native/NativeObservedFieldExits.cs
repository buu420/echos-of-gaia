using System;
using System.Collections.Generic;
using Memoria.Prime;
namespace Memoria.Accessibility.Navigation
{
    public static class NativeObservedFieldExits
    {
        private sealed class Source
        {internal Quad Quad;internal TargetIdentity Id;internal string Scope,Save;internal long Generation;internal int RequestDepth;}
        private static readonly Dictionary<Quad,Source> _presented=new Dictionary<Quad,Source>();
        private static readonly ObservedExitJourney _journey=new ObservedExitJourney();
        private static Source _candidate;
        private static long _generation=-1;
        public static void Reset(){_candidate=null;_journey.Reset();_presented.Clear();_generation=-1;}
        public static void Presented(NavigationContext context,Quad quad)
        {Presented(context,quad,NativeFieldTargets.ExitLabelSignature(quad));}
        public static void Presented(NavigationContext context,Quad quad,string scope)
        {
            if(_generation!=context.Generation){_presented.Clear();_generation=context.Generation;}
            if(String.IsNullOrEmpty(scope))return;
            Source previous;if(_presented.TryGetValue(quad,out previous) && previous.Scope==scope && previous.Save==context.SaveIdentity && previous.Id.MapKey==context.LogicalMapKey)return;
            _presented[quad]=new Source{Quad=quad,Id=new TargetIdentity(TargetKind.FieldExit,context.LogicalMapKey,quad.uid),Scope=scope,Save=context.SaveIdentity,Generation=context.Generation};
        }
        public static void Triggered(EventEngine engine,PosObj player,Obj source)
        {
            try {
                _candidate=null;_journey.Reset();var quad=source as Quad;Source seen;
                if(engine==null || engine.gMode!=1 || player==null || player!=engine.GetControlChar() || quad==null || !_presented.TryGetValue(quad,out seen) || !Matches(engine,seen))return;
                seen.RequestDepth=quad.sx;_candidate=seen;_journey.Capture(seen.Id,seen.Scope,seen.Generation,seen.Save);
            }catch(Exception error){Reset();Log.Message("[FF9Access] Exit observation unavailable: {0}",error);}
        }
        public static void Executed(EventEngine engine,Obj owner,Obj executing,int destination)
        {
            try {
                var seen=_candidate;_candidate=null;
                if(seen==null || owner!=seen.Quad || executing!=seen.Quad || owner.sx!=seen.RequestDepth || owner.level!=1 || !Matches(engine,seen)){_journey.Reset();return;}
                _journey.Executed(destination);
            }catch(Exception error){Reset();Log.Message("[FF9Access] Exit observation unavailable: {0}",error);}
        }
        private static bool Matches(EventEngine engine,Source source)
        {return engine!=null && engine.gMode==1 && source.Generation==NavigationLifecycleBridge.Generation && source.Save==NavigationSaveIdentity.Current && source.Id.MapKey=="field:"+FF9StateSystem.Common.FF9.fldMapNo+":"+FF9StateSystem.Field.SceneName && engine.GetObjUID(source.Quad.uid)==source.Quad && source.Scope==NativeFieldTargets.ExitLabelSignature(source.Quad);}
        public static void Returned(Obj owner)
        {if(_candidate!=null && owner==_candidate.Quad && owner.sx<=_candidate.RequestDepth){_candidate=null;_journey.Reset();}}
        public static void Detach(bool battle){_candidate=null;_presented.Clear();_journey.Detach(battle);}
        public static void Attach(bool world){_candidate=null;_presented.Clear();_journey.Attach(world);}
        public static void Arrive(NavigationContext context)
        {
            TargetIdentity source;string scope,label;
            if(_journey.TryComplete(context,FF9StateSystem.Common.FF9.fldMapNo,out source,out scope,out label)) {
                NavigationSaveIdentity.Identities.Record(source,label,scope);
                Log.Message("[FF9Access] Exit destination observed: source={0}; uid={1}; label={2}",source.MapKey,source.NativeId,label);
            }
        }
    }
}
