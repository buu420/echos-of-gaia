using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    public sealed class ScriptStoryFunction
    {
        public int Identity;
        public int ScriptId=-1,Tag=-1;
        public IList<ScriptInstruction> Code;
        public ScriptSnapshot Snapshot;
    }
    public sealed class ScriptStoryEvidence
    {
        public readonly HashSet<StoryEffectSite> Reached=new HashSet<StoryEffectSite>();
        public readonly HashSet<StoryEffectSite> Effects=new HashSet<StoryEffectSite>();
        public readonly HashSet<int> Milestones=new HashSet<int>();
        public readonly HashSet<int> Destinations=new HashSet<int>();
    }
    public struct StoryEffectSite:IEquatable<StoryEffectSite>
    {
        public int Trigger,Sid,Tag,Offset,Uid;
        public StoryEffectSite(int trigger,int sid,int tag,int offset):this(trigger,sid,tag,offset,-1){}
        public StoryEffectSite(int trigger,int sid,int tag,int offset,int uid){Trigger=trigger;Sid=sid;Tag=tag;Offset=offset;Uid=uid;}
        public bool Equals(StoryEffectSite other){return Trigger==other.Trigger && Sid==other.Sid && Tag==other.Tag && Offset==other.Offset && Uid==other.Uid;}
        public override bool Equals(object other){return other is StoryEffectSite && Equals((StoryEffectSite)other);}
        public override int GetHashCode(){return (((Trigger*397^Sid)*397^Tag)*397^Offset)*397^Uid;}
    }
    // Read a currently selected script path in detached buffers. This never
    // executes native opcodes, requests an event, supplies a choice or input,
    // or obtains an unseen actor position. Unknown conditions stop the proof.
    // objectProperty(uid,property) may answer native OBJ(uid).property reads (EBin.getvobj values,
    // e.g. property 1 = -round(pos.y)); callers supply only the controlled player's prospective
    // position. Null, or a null answer, keeps the read unknown.
    public static class ScriptStoryAnalyzer
    {
        private sealed class ReadState{
            public Func<int,int,int?> Position;public bool UserControl,PendingAsync;public int Disable,Enable,Async=-1;public int[] PositionChanges,Yields;
            public readonly Dictionary<int,ScriptSnapshot> Instances=new Dictionary<int,ScriptSnapshot>();
        }
        public static ScriptStoryEvidence Read(ScriptStoryFunction start,Func<int,int,int,ScriptSnapshot,ScriptStoryFunction> requested,int mapJump,int worldJump,int[] requestOpcodes)
        {return Read(start,requested,mapJump,worldJump,requestOpcodes,null);}
        public static ScriptStoryEvidence Read(ScriptStoryFunction start,Func<int,int,int,ScriptSnapshot,ScriptStoryFunction> requested,int mapJump,int worldJump,int[] requestOpcodes,Func<int,int,int?> objectProperty)
        {return Read(start,requested,mapJump,worldJump,requestOpcodes,objectProperty,-1,-1,null);}
        public static ScriptStoryEvidence Read(ScriptStoryFunction start,Func<int,int,int,ScriptSnapshot,ScriptStoryFunction> requested,int mapJump,int worldJump,int[] requestOpcodes,Func<int,int,int?> objectProperty,int disableControl,int enableControl,int[] positionChanges)
        {return Read(start,requested,mapJump,worldJump,requestOpcodes,objectProperty,disableControl,enableControl,positionChanges,-1,null);}
        public static ScriptStoryEvidence Read(ScriptStoryFunction start,Func<int,int,int,ScriptSnapshot,ScriptStoryFunction> requested,int mapJump,int worldJump,int[] requestOpcodes,Func<int,int,int?> objectProperty,int disableControl,int enableControl,int[] positionChanges,int asynchronousRequest,int[] yieldOpcodes)
        {
            var evidence=new ScriptStoryEvidence();int budget=512;
            var state=new ReadState{Position=objectProperty,UserControl=start!=null && start.Snapshot!=null && start.Snapshot.UserControl,Disable=disableControl,Enable=enableControl,PositionChanges=positionChanges,Async=asynchronousRequest,Yields=yieldOpcodes};
            ReadFunction(start,requested,mapJump,worldJump,requestOpcodes,state,evidence,new HashSet<int>(),start==null?-1:start.Tag,ref budget);
            return evidence;
        }
        private static bool ReadFunction(ScriptStoryFunction function,Func<int,int,int,ScriptSnapshot,ScriptStoryFunction> requested,int mapJump,int worldJump,int[] requestOpcodes,ReadState state,ScriptStoryEvidence evidence,HashSet<int> visiting,int trigger,ref int budget)
        {
            if(function==null || function.Code==null || function.Code.Count==0 || function.Snapshot==null || !visiting.Add(function.Identity))return false;
            try {
                // Native local variables belong to an object instance, shared
                // by all its tags. A second synchronous call must not reload
                // the old live bytes over this detached instance's own writes.
                if(function.ScriptId>=0 && function.Tag>=0) {
                    int owner=(function.Identity/256)*256+function.ScriptId;ScriptSnapshot instance;
                    if(state.Instances.TryGetValue(owner,out instance))function.Snapshot=instance;
                    else state.Instances[owner]=function.Snapshot;
                }
                int self=function.Identity/256;
                Func<int,int,int?> property=(uid,index)=>state.Position==null?(int?)null:state.Position(uid==255 && function.ScriptId>=0 && function.Tag>=0?self:uid,index);
                var code=new Dictionary<int,ScriptInstruction>();foreach(var instruction in function.Code)code[instruction.Offset]=instruction;
                int ip=function.Code[0].Offset;int? value=null;
                while(budget-- >0) {
                    ScriptInstruction item;if(!code.TryGetValue(ip,out item))return false;
                    if(function.ScriptId>=0 && function.Tag>=0)evidence.Reached.Add(new StoryEffectSite(trigger,function.ScriptId,function.Tag,item.Offset,function.Identity/256));
                    // A current prefix stops at native yields and blocking
                    // requests even when no asynchronous work preceded them.
                    // The reached site itself remains evidence of an available
                    // interaction; nothing after the wait has been promised.
                    if(Contains(state.Yields,item.Opcode))return false;
                    // REQ schedules another script and immediately continues this
                    // caller. After a yield, that child can change shared state;
                    // a current-state prefix must stop rather than forecast it.
                    if(state.PendingAsync && (Contains(state.Yields,item.Opcode) || Contains(requestOpcodes,item.Opcode) && item.Opcode!=state.Async))return false;
                    if(item.Opcode==4)return true;
                    if(item.Opcode==5) {
                        int? before=function.Snapshot.Read(0,7,0);
                        function.Snapshot.UserControl=state.UserControl;
                        if(!ExitScriptAnalyzer.TryExpression(item.Expression,function.Snapshot,out value,property))return false;
                        if(function.ScriptId>=0 && function.Tag>=0)evidence.Effects.Add(new StoryEffectSite(trigger,function.ScriptId,function.Tag,item.Offset,function.Identity/256));
                        int? after=function.Snapshot.Read(0,7,0);if(after.HasValue && before.HasValue && after>before)evidence.Milestones.Add(after.Value);
                    }else if(item.Opcode>=1 && item.Opcode<=3) {
                        if(!item.Argument.HasValue || item.Opcode!=1 && !value.HasValue)return false;
                        bool branch=item.Opcode==1 || (item.Opcode==2?value==0:value!=0);
                        if(branch){ip=item.NextOffset+(item.Opcode==2?item.Argument.Value:(short)item.Argument.Value);continue;}
                    }else if(item.Opcode==6 || item.Opcode==11 || item.Opcode==13) {
                        int? branch=ScriptBranches.SelectSwitch(item,value);if(!branch.HasValue)return false;ip=branch.Value;continue;
                    }else if(item.Opcode==mapJump || item.Opcode==worldJump) {
                        int? destination=item.Constant(0);if(!destination.HasValue && !ExitScriptAnalyzer.TryExpression(item.ArgumentExpression(0),function.Snapshot,out destination,property))destination=null;
                        if(destination.HasValue)evidence.Destinations.Add(destination.Value);return destination.HasValue;
                    }else if(Contains(requestOpcodes,item.Opcode)) {
                        int? uid=item.Constant(1),tag=item.Constant(2);
                        if(item.Opcode==state.Async) {
                            if(!item.Constant(0).HasValue || !uid.HasValue || !tag.HasValue)return false;
                            state.PendingAsync=true;ip=item.NextOffset;continue;
                        }
                        if(!uid.HasValue || !tag.HasValue || requested==null)return false;
                        if(!ReadFunction(requested(uid.Value,tag.Value,function.Identity,function.Snapshot),requested,mapJump,worldJump,requestOpcodes,state,evidence,visiting,trigger,ref budget))return false;
                    }else if(item.Opcode==state.Disable)state.UserControl=false;
                    else if(item.Opcode==state.Enable)state.UserControl=true;
                    else if(Contains(state.PositionChanges,item.Opcode))state.Position=null;
                    ip=item.NextOffset;
                }
                return false;
            }finally{visiting.Remove(function.Identity);}
        }
        private static bool Contains(int[] values,int value){if(values!=null)foreach(int item in values)if(item==value)return true;return false;}
    }
}
