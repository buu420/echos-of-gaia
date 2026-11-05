using System;
using System.Collections.Generic;
using System.Text;
namespace Memoria.Accessibility.Navigation
{
    public enum FieldExitPathKind { Unknown, NoJump, Determined, ChoiceSingle, ChoiceMenu }
    // Where a native exit path can lead now. Determined: one place on every path (button waits and
    // conversation choices that do not change the outcome). ChoiceSingle: a shown choice either
    // leads to the one place or declines. ChoiceMenu: the player's choice decides (different places,
    // or a choice carried into the next field), so no single place may be named.
    public sealed class FieldExitPathEvidence
    {
        public FieldExitPathKind Kind;
        public readonly HashSet<int> Destinations=new HashSet<int>();
        // The entrance after the jump's own detached script writes. Null means
        // its reachable branches did not agree on one entrance.
        public readonly Dictionary<int,int?> ArrivalEntrances=new Dictionary<int,int?>();
        public bool CanDecline,UsesChoice;
        // A path that runs now shows dialogue (MES windows) or writes the scenario counter (global UInt16 0).
        public bool Dialogue,StoryCounter;
        // Buttons (EventInput mask) that a branch decided only by input waits for, e.g. Confirm at a ledge.
        public uint Buttons;
        // Where an unproven path stopped ("uid:tag@offset"), for the exit ledger and diagnostics.
        public string Stop;
    }
    // Native request and dialogue semantics for indirect exits.
    public sealed class FieldExitPathOptions
    {
        // Opcodes that show a dialogue window (MES, MESN, MESA, MESAN, MESB).
        public int[] DialogueOpcodes;
        // requestable(uid,level,caller uid): true when the entry runs the request now (EventEngine.Request:
        // level below its current level), false when it is busy, null when it is missing or unreadable.
        public Func<int,int,int,bool?> Requestable;
        // The asynchronous REQ opcode: a busy target simply does not run. REQSW/REQEW wait instead.
        public int AsyncRequest=-1;
        // The controlled player's uid: its facing (OBJ.3) is chosen by the player's own movement, like input.
        public int PlayerUid=-1;
        // MJPOS computes SYS(10/11); it does not move the player. For a walking
        // approach, the direction toward that point is a possible entry facing.
        public int CalculateExitPosition=-1;
        public NavigationPoint? ExitPosition;
        public int? EntryFacing;
        public Func<int,int,int> Angle2;
    }
    // Detached replay of an exit's touch path, as ScriptStoryAnalyzer, except that a branch decided only
    // by player input (B_KEYON button reads, the message signal) or by a shown choice (GetChoose) is
    // replayed once per alternative; nothing is chosen for the player. B_PARTYCHK is read from the live
    // party through partyCheck. Native mutations (B_PARTYADD) and any other unreadable value stop the
    // proof. Backward alternatives of an input wait are not replayed (waiting changes nothing).
    public static class FieldExitPaths
    {
        private const int MaxRuns=48,MaxDecisions=8,Budget=4096;
        private const int TaintInput=1,TaintChoice=2;
        private struct Value { public int? Number; public int Source,Type,Index,Taint; public bool Variable; }
        private sealed class Run
        {
            public readonly List<int> Prefix;public readonly List<int> Taken=new List<int>();public readonly List<int> Alternatives=new List<int>();
            public readonly StringBuilder Writes=new StringBuilder();
            public int? Destination,Entrance;public bool Done,UsedChoice,Overflow,Dialogue,Counter;public uint Buttons,PendingButtons;public string Stop;
            public Run(List<int> prefix){Prefix=prefix;}
            public int Decide(int count,bool choice)
            {
                if(choice)UsedChoice=true;
                int at=Taken.Count;if(at>=MaxDecisions){Overflow=true;return -1;}
                int pick=at<Prefix.Count?Prefix[at]:0;if(pick>=count){Overflow=true;return -1;}
                Taken.Add(pick);Alternatives.Add(count);return pick;
            }
        }
        private sealed class State{
            public Func<int,int,int?> Position;public Func<int,bool?> Party;public bool UserControl;public int Disable,Enable,SelfUid=-1;public int[] PositionChanges;
            public bool HasExitPosition;
            public readonly Dictionary<int,ScriptSnapshot> Instances=new Dictionary<int,ScriptSnapshot>();
            public FieldExitPathOptions Options;
        }
        public static FieldExitPathEvidence Read(Func<ScriptStoryFunction> start,Func<int,int,int,ScriptSnapshot,ScriptStoryFunction> requested,int mapJump,int worldJump,int[] requestOpcodes,
            Func<int,int,int?> objectProperty,Func<int,bool?> partyCheck,int disableControl,int enableControl,int[] positionChanges)
        {return Read(start,requested,mapJump,worldJump,requestOpcodes,objectProperty,partyCheck,disableControl,enableControl,positionChanges,null);}
        public static FieldExitPathEvidence Read(Func<ScriptStoryFunction> start,Func<int,int,int,ScriptSnapshot,ScriptStoryFunction> requested,int mapJump,int worldJump,int[] requestOpcodes,
            Func<int,int,int?> objectProperty,Func<int,bool?> partyCheck,int disableControl,int enableControl,int[] positionChanges,FieldExitPathOptions options)
        {
            var evidence=new FieldExitPathEvidence();var runs=new List<Run>();
            var pending=new Queue<List<int>>();pending.Enqueue(new List<int>());
            while(pending.Count>0) {
                if(runs.Count>=MaxRuns){evidence.Kind=FieldExitPathKind.Unknown;return evidence;}
                var run=new Run(pending.Dequeue());var function=start==null?null:start();
                if(function==null){evidence.Kind=FieldExitPathKind.Unknown;return evidence;}
                var state=new State{Position=objectProperty,Party=partyCheck,UserControl=function.Snapshot!=null && function.Snapshot.UserControl,Disable=disableControl,Enable=enableControl,PositionChanges=positionChanges,Options=options};
                int budget=Budget;
                if(!Function(function,requested,mapJump,worldJump,requestOpcodes,state,run,new HashSet<int>(),ref budget) || run.Overflow){evidence.Kind=FieldExitPathKind.Unknown;evidence.Stop=run.Stop??(run.Overflow?"decisions":"budget");return evidence;}
                runs.Add(run);
                // Every decision this run met beyond its prefix spawns its untaken alternatives.
                for(int k=run.Prefix.Count;k<run.Taken.Count;k++)for(int alt=1;alt<run.Alternatives[k];alt++) {
                    var next=new List<int>();for(int i=0;i<k;i++)next.Add(run.Taken[i]);next.Add(alt);pending.Enqueue(next);
                }
            }
            string signature=null;bool sameWrites=true;int declines=0;
            foreach(var run in runs) {
                if(run.UsedChoice)evidence.UsesChoice=true;
                if(run.Dialogue)evidence.Dialogue=true;if(run.Counter)evidence.StoryCounter=true;evidence.Buttons|=run.Buttons;
                if(!run.Destination.HasValue){declines++;continue;}
                int destination=run.Destination.Value;evidence.Destinations.Add(destination);
                if(FieldAreaNames.SpiralArea(destination,0)!=0) {
                    int? earlier;
                    if(!evidence.ArrivalEntrances.TryGetValue(destination,out earlier))evidence.ArrivalEntrances.Add(destination,run.Entrance);
                    else if(earlier!=run.Entrance)evidence.ArrivalEntrances[destination]=null;
                }
                string writes=run.Writes.ToString();if(signature==null)signature=writes;else if(signature!=writes)sameWrites=false;
            }
            evidence.CanDecline=declines!=0;
            if(evidence.Destinations.Count==0)evidence.Kind=FieldExitPathKind.NoJump;
            else if(evidence.Destinations.Count>1 || evidence.UsesChoice && !sameWrites)evidence.Kind=FieldExitPathKind.ChoiceMenu;
            else if(runs.Count==1 || declines==0)evidence.Kind=FieldExitPathKind.Determined;
            else evidence.Kind=FieldExitPathKind.ChoiceSingle;
            return evidence;
        }
        private static bool Function(ScriptStoryFunction function,Func<int,int,int,ScriptSnapshot,ScriptStoryFunction> requested,int mapJump,int worldJump,int[] requestOpcodes,State state,Run run,HashSet<int> visiting,ref int budget)
        {
            if(function==null || function.Code==null || function.Code.Count==0 || function.Snapshot==null || !visiting.Add(function.Identity))return Fail(run,function,-1);
            int previousSelf=state.SelfUid;
            try {
                state.SelfUid=function.ScriptId>=0 && function.Tag>=0?function.Identity/256:-1;
                if(state.SelfUid>=0) {
                    int owner=state.SelfUid*256+function.ScriptId;ScriptSnapshot instance;
                    if(state.Instances.TryGetValue(owner,out instance))function.Snapshot=instance;
                    else state.Instances[owner]=function.Snapshot;
                }
                var code=new Dictionary<int,ScriptInstruction>();foreach(var instruction in function.Code)code[instruction.Offset]=instruction;
                int ip=function.Code[0].Offset;Value value=new Value();bool hasValue=false;
                while(budget-- >0) {
                    ScriptInstruction item;if(!code.TryGetValue(ip,out item))return Fail(run,function,ip);
                    if(item.Opcode==4)return true;
                    if(item.Opcode==5) {
                        function.Snapshot.UserControl=state.UserControl;
                        if(!Evaluate(item.Expression,function.Snapshot,state,run,out value))return Fail(run,function,ip);
                        hasValue=true;
                    }else if(item.Opcode>=1 && item.Opcode<=3) {
                        if(!item.Argument.HasValue)return Fail(run,function,ip);
                        int target=item.NextOffset+(item.Opcode==2?item.Argument.Value:(short)item.Argument.Value);
                        if(item.Opcode==1){ip=target;continue;}
                        if(!hasValue)return Fail(run,function,ip);
                        if(value.Number.HasValue){bool branch=item.Opcode==2?value.Number==0:value.Number!=0;ip=branch?target:item.NextOffset;continue;}
                        int? next=Fork(new[]{target,item.NextOffset},item.Offset,value.Taint,run);if(!next.HasValue)return Fail(run,function,ip);ip=next.Value;continue;
                    }else if(item.Opcode==6 || item.Opcode==11 || item.Opcode==13) {
                        if(!hasValue)return Fail(run,function,ip);
                        if(value.Number.HasValue){int? branch=ScriptBranches.SelectSwitch(item,value.Number);if(!branch.HasValue)return Fail(run,function,ip);ip=branch.Value;continue;}
                        var targets=new List<int>();foreach(int t in ScriptBranches.SwitchTargets(item))if(!targets.Contains(t))targets.Add(t);
                        int? next=Fork(targets.ToArray(),item.Offset,value.Taint,run);if(!next.HasValue)return Fail(run,function,ip);ip=next.Value;continue;
                    }else if(item.Opcode==mapJump || item.Opcode==worldJump) {
                        int? destination=item.Constant(0);
                        if(!destination.HasValue){Value computed;if(!Evaluate(item.ArgumentExpression(0),function.Snapshot,state,run,out computed) || !computed.Number.HasValue)return Fail(run,function,ip);destination=computed.Number;}
                        run.Destination=destination;
                        if(item.Opcode==mapJump && FieldAreaNames.SpiralArea(destination.Value,0)!=0)
                            run.Entrance=function.Snapshot.Read(0,7,2);
                        run.Done=true;return true;
                    }else if(Contains(requestOpcodes,item.Opcode)) {
                        int? uid=item.Constant(1),tag=item.Constant(2);
                        if(!uid.HasValue || !tag.HasValue || requested==null)return Fail(run,function,ip);
                        if(state.Options!=null && state.Options.Requestable!=null) {
                            int? level=item.Constant(0);if(!level.HasValue)return Fail(run,function,ip);
                            bool? runs=state.Options.Requestable(uid.Value,level.Value,function.Identity/256);
                            if(runs==null)return Fail(run,function,ip);
                            // A busy entry does not run an asynchronous REQ; a waiting request cannot be proven now.
                            if(runs==false){if(item.Opcode!=state.Options.AsyncRequest)return Fail(run,function,ip);ip=item.NextOffset;continue;}
                        }
                        if(!Function(requested(uid.Value,tag.Value,function.Identity,function.Snapshot),requested,mapJump,worldJump,requestOpcodes,state,run,visiting,ref budget))return Fail(run,function,ip);
                        if(run.Done)return true;
                    }else if(state.Options!=null && Contains(state.Options.DialogueOpcodes,item.Opcode)){run.Dialogue=true;ip=item.NextOffset;continue;}
                    else if(item.Opcode==state.Disable)state.UserControl=false;
                    else if(item.Opcode==state.Enable)state.UserControl=true;
                    else if(state.Options!=null && item.Opcode==state.Options.CalculateExitPosition)
                        state.HasExitPosition=state.Position!=null && state.Options.ExitPosition.HasValue;
                    else if(Contains(state.PositionChanges,item.Opcode))state.Position=null;
                    ip=item.NextOffset;
                }
                return Fail(run,function,ip);
            }finally{state.SelfUid=previousSelf;visiting.Remove(function.Identity);}
        }
        private static bool Fail(Run run,ScriptStoryFunction function,int ip)
        {if(run.Stop==null)run.Stop=(function==null?"missing function":(function.Identity/256)+":"+function.Tag)+"@"+ip;return false;}
        // Only input/choice-decided branches fork, and only forward: a backward edge of a wait repeats it.
        private static int? Fork(int[] targets,int origin,int taint,Run run)
        {
            if((taint&(TaintInput|TaintChoice))==0)return null;
            var forward=new List<int>();foreach(int t in targets)if(t>origin && !forward.Contains(t))forward.Add(t);
            if(forward.Count==0)return null;
            if((taint&TaintChoice)==0)run.Buttons|=run.PendingButtons;
            int pick=run.Decide(forward.Count,(taint&TaintChoice)!=0);return pick<0?(int?)null:forward[pick];
        }
        private static bool Contains(int[] values,int value){if(values!=null)foreach(int item in values)if(item==value)return true;return false;}
        // ExitScriptAnalyzer.TryExpression semantics, with the reason a value is unknown kept as a taint.
        private static bool Evaluate(byte[] bytes,ScriptSnapshot snapshot,State state,Run run,out Value value)
        {
            value=new Value();if(bytes==null)return false;var stack=new Stack<Value>();
            try { for(int p=0;p<bytes.Length;) {
                int op=bytes[p++];
                if(op==0x7f){if(stack.Count!=1)return false;value=stack.Pop();return true;}
                if(op>=0xc0){int index=bytes[p++];if(op>=0xe0)index|=bytes[p++]<<8;int source=op&3,type=(op&0x1c)>>2;stack.Push(new Value{Number=snapshot.Read(source,type,index),Variable=true,Source=source,Type=type,Index=index});continue;}
                if(op==0x7d || op==0x7e){int number=bytes[p++]|(bytes[p++]<<8);if(op==0x7e)number|=(bytes[p++]<<16)|(bytes[p++]<<24);else number=(short)number;stack.Push(new Value{Number=number});continue;}
                if(op==0x7a){
                    int code=bytes[p++];int? number=code==2?(snapshot.UserControl?1:0):(int?)null;
                    if(state.HasExitPosition && (code==10 || code==11))number=(int)(code==10?state.Options.ExitPosition.Value.X:state.Options.ExitPosition.Value.Z);
                    stack.Push(new Value{Number=number,Taint=code==9?TaintChoice:code==8?TaintInput:0});continue;
                }
                if(op==0x78) {
                    int uid=bytes[p++],property=bytes[p++];if(uid==255 && state.SelfUid>=0)uid=state.SelfUid;int? number=state.Position==null?null:state.Position(uid,property);
                    bool facing=property==3 && (uid==250 || state.Options!=null && uid==state.Options.PlayerUid);
                    if(number==null && facing && state.Position!=null && state.HasExitPosition)number=state.Options.EntryFacing;
                    stack.Push(new Value{Number=number,Taint=number==null && facing?TaintInput:0});continue;
                }
                if(op==0x79){p++;if(p>bytes.Length)return false;stack.Push(new Value());continue;}
                if(op==102){
                    Value z=stack.Pop(),x=stack.Pop();
                    int? angle=z.Number.HasValue && x.Number.HasValue && state.Options!=null && state.Options.Angle2!=null?
                        (int?)state.Options.Angle2(x.Number.Value,z.Number.Value):null;
                    stack.Push(new Value{Number=angle,Taint=z.Taint|x.Taint});continue;
                }
                // B_KEYON / B_KEYON2: the buttons held now are player input.
                if(op==79 || op==90){var mask=stack.Pop();if(mask.Number.HasValue)run.PendingButtons|=(uint)mask.Number.Value;stack.Push(new Value{Taint=mask.Taint|TaintInput});continue;}
                // B_PARTYCHK: a pure read of the live party.
                if(op==107){var who=stack.Pop();bool? member=who.Number.HasValue && state.Party!=null?state.Party(who.Number.Value):null;stack.Push(new Value{Number=member.HasValue?(int?)(member.Value?1:0):null,Taint=who.Taint});continue;}
                if(op>=4 && op<=7){var a=stack.Pop();if(!a.Variable)return false;int? next=a.Number.HasValue?(int?)(a.Number.Value+(op==4 || op==6?1:-1)):null;if(!Store(snapshot,run,a,next))return false;stack.Push(new Value{Number=op<6?a.Number:next,Taint=a.Taint});continue;}
                if(op==12 || op==13 || op==14 || op==15 || op==16 || op>=70 && op<=73){var a=stack.Pop();if(!a.Number.HasValue){stack.Push(new Value{Taint=a.Taint});continue;}int x=a.Number.Value;stack.Push(new Value{Number=op==12?x:op==13?-x:op==16?~x:op==70?(sbyte)x:op==71?(byte)x:op==72?(short)x:op==73?(ushort)x:x==0?1:0,Taint=a.Taint});continue;}
                var right=stack.Pop();var left=stack.Pop();int taint=left.Taint|right.Taint;
                if(op==44){if(!left.Variable)return false;if(!Store(snapshot,run,left,right.Number))return false;stack.Push(new Value{Number=right.Number,Taint=right.Taint});continue;}
                if(!left.Number.HasValue || !right.Number.HasValue){if(op<17 || op>40)return false;stack.Push(new Value{Taint=taint});continue;}
                int aValue=left.Number.Value,bValue=right.Number.Value,result;
                switch(op) {
                    case 17:result=aValue*bValue;break;case 18:if(bValue==0)return false;result=aValue/bValue;break;case 19:if(bValue==0)return false;result=aValue%bValue;break;
                    case 20:result=aValue+bValue;break;case 21:result=aValue-bValue;break;case 22:result=aValue<<bValue;break;case 23:result=aValue>>bValue;break;
                    case 24:case 28:result=aValue<bValue?1:0;break;case 25:case 29:result=aValue>bValue?1:0;break;case 26:case 30:result=aValue<=bValue?1:0;break;case 27:case 31:result=aValue>=bValue?1:0;break;
                    case 32:case 34:result=aValue==bValue?1:0;break;case 33:case 35:result=aValue!=bValue?1:0;break;
                    case 36:result=aValue&bValue;break;case 37:result=aValue^bValue;break;case 38:result=aValue|bValue;break;case 39:result=aValue!=0&&bValue!=0?1:0;break;case 40:result=aValue!=0||bValue!=0?1:0;break;default:return false;
                }
                stack.Push(new Value{Number=result,Taint=taint});
            } } catch(Exception e){if(!(e is IndexOutOfRangeException) && !(e is InvalidOperationException) && !(e is OverflowException))throw;}
            return false;
        }
        private static bool Store(ScriptSnapshot snapshot,Run run,Value target,int? number)
        {
            bool ok=number.HasValue?snapshot.Write(target.Source,target.Type,target.Index,number.Value):snapshot.Invalidate(target.Source,target.Type,target.Index);
            if(ok && target.Source==0 && target.Type==7 && target.Index==0)run.Counter=true;
            if(ok)run.Writes.Append(target.Source).Append(':').Append(target.Type).Append(':').Append(target.Index).Append('=').Append(number.HasValue?number.Value.ToString():"?").Append(';');
            return ok;
        }
    }
}
