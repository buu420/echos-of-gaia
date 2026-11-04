using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    public static partial class NativeFieldTargets
    {
        private static readonly FieldExitDestinationCache _exitDestinationCache=new FieldExitDestinationCache();
        private static ScriptStoryEvidence ReadStoryEvidence(NavigationContext context,EventEngine engine,Obj obj,int onlyTag,NavigationPoint? prospective)
        {
            var combined=new ScriptStoryEvidence();
            foreach(int tag in new[]{EventEngine.tagPush,EventEngine.tagTalk}) {
                if(onlyTag>=0 && tag!=onlyTag)continue;
                var snapshot=new ScriptSnapshot(FF9StateSystem.EventState.gEventGlobal,engine.GetMapVar(),ScriptSnapshot.InstanceBytes(obj.buffer,obj.vofs)){UserControl=true};
                Func<int,int,int,ScriptSnapshot,ScriptStoryFunction> requested=(uid,calledTag,callerIdentity,caller)=> {
                    var called=uid==255?engine.GetObjUID(callerIdentity/256):uid==250?engine.GetControlChar():engine.GetObjUID(uid);
                    if(called==null || called.ebData==null || !NativeStoryTargets.Matches(context,called.sid,Fingerprint(called.ebData)))return null;
                    return new ScriptStoryFunction{Identity=called.uid*256+calledTag,Code=Decode(engine,called,calledTag),Snapshot=caller.WithInstance(ScriptSnapshot.InstanceBytes(called.buffer,called.vofs))};
                };
                Func<int,int,int?> property=prospective.HasValue?(Func<int,int,int?>)((uid,index)=>FieldExitDestinations.PlayerProperty(context.PlayerIdentity,prospective.Value,uid,index)):null;
                var evidence=ScriptStoryAnalyzer.Read(new ScriptStoryFunction{Identity=obj.uid*256+tag,Code=Decode(engine,obj,tag),Snapshot=snapshot},requested,_opcodes.MapJump,_opcodes.WorldJump,_opcodes.Requests,property);
                combined.Milestones.UnionWith(evidence.Milestones);combined.Destinations.UnionWith(evidence.Destinations);
            }return combined;
        }
        private static void AnnotateExitSurfaces(NavigationContext context,EventEngine engine,Obj obj,FieldTargetObservation observation)
        {
            var exit=observation.Exit;
            if(observation.Id.Kind!=TargetKind.FieldExit || exit==null || !exit.ApproachEnabled || exit.ApproachCandidates==null || !NativeStoryTargets.Matches(context,obj.sid,Fingerprint(obj.ebData)))return;
            var code=Decode(engine,obj,EventEngine.tagPush);bool positionRead=false,requests=false;
            foreach(var instruction in code) {
                if(instruction.Expression!=null && Array.IndexOf(instruction.Expression,(byte)0x78)>=0)positionRead=true;
                if(instruction.ArgumentExpressions!=null)foreach(var expression in instruction.ArgumentExpressions)if(expression!=null && Array.IndexOf(expression,(byte)0x78)>=0)positionRead=true;
                foreach(int request in _opcodes.Requests)if(instruction.Opcode==request)requests=true;
            }
            // Ordinary non-position exits keep their existing analysis. This
            // additional proof is only needed for prospective player branches.
            if(!positionRead)return;
            Func<NavigationPoint,ScriptStoryEvidence> evaluate=point=>ReadStoryEvidence(context,engine,obj,EventEngine.tagPush,point);
            string scope=context.Generation+":"+context.LogicalMapKey+":"+context.SaveIdentity+":"+context.PlayerIdentity;
            // A requested entry owns separate variables/lifetime. Do not cache
            // it using only the parent entry's memory as its invalidation key.
            var groups=requests?FieldExitDestinations.Read(exit.ApproachCandidates,evaluate):_exitDestinationCache.Read(scope,obj.uid,Fingerprint(obj.ebData),FF9StateSystem.EventState.gEventGlobal,engine.GetMapVar(),ScriptSnapshot.InstanceBytes(obj.buffer,obj.vofs),exit.ApproachCandidates,evaluate);
            observation.DestinationApproaches=groups;
            var destinations=new List<int>(groups.Keys);destinations.Sort();observation.NextFields=destinations.ToArray();
            var approved=FieldExitDestinations.Select(groups,destinations);exit.ApproachCandidates=approved;
            if(approved.Length==0){exit.ApproachEnabled=false;exit.Availability="no currently verified walking approach";}
            else {exit.Approach=approved[0];observation.Position=approved[0];exit.Availability=null;}
        }
    }
}
