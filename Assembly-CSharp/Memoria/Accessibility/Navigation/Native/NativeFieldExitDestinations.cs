using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    public static partial class NativeFieldTargets
    {
        private static readonly FieldExitDestinationCache _exitDestinationCache=new FieldExitDestinationCache();
        private static int? CurrentPlayerFacing(EventEngine engine,int uid)
        {
            var player=engine.GetControlChar() as Actor;
            if(player==null || player.uid!=uid)return null;
            float angle=player.rotAngle[1];
            return Single.IsNaN(angle) || Single.IsInfinity(angle)?(int?)null:(EventEngineUtils.ConvertFloatAngleToFixedPoint(angle)>>4)&255;
        }
        private static ScriptStoryEvidence ReadStoryEvidence(NavigationContext context,EventEngine engine,Obj obj,int onlyTag,NavigationPoint? prospective,bool currentPrefix=false,bool readFacing=true,int? approachFacing=null)
        {
            var combined=new ScriptStoryEvidence();
            // Only a reviewed Confirm approach may test a possible turn. Live
            // interaction readiness always uses the actual native facing and cue.
            int? facing=currentPrefix && onlyTag==EventEngine.tagTalk && approachFacing.HasValue?approachFacing:
                readFacing && prospective.HasValue && prospective.Value.Equals(context.Position)?CurrentPlayerFacing(engine,context.PlayerIdentity):null;
            foreach(int tag in new[]{EventEngine.tagPush,EventEngine.tagTalk}) {
                if(onlyTag>=0 && tag!=onlyTag)continue;
                var snapshot=new ScriptSnapshot(FF9StateSystem.EventState.gEventGlobal,engine.GetMapVar(),ScriptSnapshot.InstanceBytes(obj.buffer,obj.vofs)){UserControl=true};
                Func<int,int,int,ScriptSnapshot,ScriptStoryFunction> requested=(uid,calledTag,callerIdentity,caller)=> {
                    var called=uid==255?engine.GetObjUID(callerIdentity/256):uid==250?engine.GetControlChar():engine.GetObjUID(uid);
                    if(called==null || called.ebData==null || !NativeStoryTargets.Matches(context,called.sid,Fingerprint(called.ebData)))return null;
                    return new ScriptStoryFunction{Identity=called.uid*256+calledTag,ScriptId=called.sid,Tag=calledTag,Code=Decode(engine,called,calledTag),Snapshot=caller.WithInstance(ScriptSnapshot.InstanceBytes(called.buffer,called.vofs))};
                };
                Func<int,int,int?> property=prospective.HasValue?(Func<int,int,int?>)((uid,index)=>FieldExitDestinations.PlayerProperty(context.PlayerIdentity,prospective.Value,uid,index,facing)):null;
                var start=new ScriptStoryFunction{Identity=obj.uid*256+tag,ScriptId=obj.sid,Tag=tag,Code=Decode(engine,obj,tag),Snapshot=snapshot};
                var moves=new[]{_opcodes.Move,_opcodes.MoveQuad,(int)EBin.event_code_binary.MOVE3,(int)EBin.event_code_binary.MOVE3H,(int)EBin.event_code_binary.POS,(int)EBin.event_code_binary.POS3,(int)EBin.event_code_binary.DPOS3};
                // Reviewed action prefixes end before a choice, a native wait,
                // or a blocking request. Legacy assignment/transition proofs
                // retain their own replay; a prefix never supplies those writes.
                var evidence=currentPrefix?ScriptStoryAnalyzer.Read(start,requested,_opcodes.MapJump,_opcodes.WorldJump,_opcodes.Requests,property,_opcodes.DisableControl,_opcodes.EnableControl,moves,
                    (int)EBin.event_code_binary.REQ,new[]{(int)EBin.event_code_binary.REQSW,(int)EBin.event_code_binary.REQEW,(int)EBin.event_code_binary.WAIT,(int)EBin.event_code_binary.WAITANIM,(int)EBin.event_code_binary.WAITTURN,
                        (int)EBin.event_code_binary.MES,(int)EBin.event_code_binary.MESN,(int)EBin.event_code_binary.MESA,(int)EBin.event_code_binary.MESAN,(int)EBin.event_code_binary.MESB,(int)EBin.event_code_binary.WAITMES,(int)EBin.event_code_binary.CHOOSEPARAM}):
                    ScriptStoryAnalyzer.Read(start,requested,_opcodes.MapJump,_opcodes.WorldJump,_opcodes.Requests,property,_opcodes.DisableControl,_opcodes.EnableControl,moves);
                combined.Effects.UnionWith(evidence.Effects);
                combined.Reached.UnionWith(evidence.Reached);
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
            Func<NavigationPoint,ScriptStoryEvidence> evaluate=point=>ReadStoryEvidence(context,engine,obj,EventEngine.tagPush,point,false,false);
            string scope=context.Generation+":"+context.LogicalMapKey+":"+context.SaveIdentity+":"+context.PlayerIdentity;
            // A requested entry owns separate variables/lifetime. Do not cache
            // it using only the parent entry's memory as its invalidation key.
            var groups=requests?FieldExitDestinations.Read(exit.ApproachCandidates,evaluate):_exitDestinationCache.Read(scope,obj.uid,Fingerprint(obj.ebData),FF9StateSystem.EventState.gEventGlobal,engine.GetMapVar(),ScriptSnapshot.InstanceBytes(obj.buffer,obj.vofs),exit.ApproachCandidates,evaluate);
            var destinations=new List<int>(groups.Keys);destinations.Sort();observation.NextFields=destinations.ToArray();
            var approved=FieldExitDestinations.Select(groups,destinations);
            if(approved.Length==0)return; // The full exit replay handles positioning, facing, requests and choices.
            observation.DestinationApproaches=groups;exit.ApproachCandidates=approved;
            exit.Approach=approved[0];observation.Position=approved[0];exit.Availability=null;
        }
    }
}
