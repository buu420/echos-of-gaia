using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    public sealed class ScriptStoryFunction
    {
        public int Identity;
        public IList<ScriptInstruction> Code;
        public ScriptSnapshot Snapshot;
    }
    public sealed class ScriptStoryEvidence
    {
        public readonly HashSet<int> Milestones=new HashSet<int>();
        public readonly HashSet<int> Destinations=new HashSet<int>();
    }
    // Read a currently selected script path in detached buffers. This never
    // executes native opcodes, requests an event, supplies a choice or input,
    // or obtains an unseen actor position. Unknown conditions stop the proof.
    public static class ScriptStoryAnalyzer
    {
        public static ScriptStoryEvidence Read(ScriptStoryFunction start,Func<int,int,int,ScriptSnapshot,ScriptStoryFunction> requested,int mapJump,int worldJump,int[] requestOpcodes)
        {
            var evidence=new ScriptStoryEvidence();int budget=512;
            ReadFunction(start,requested,mapJump,worldJump,requestOpcodes,evidence,new HashSet<int>(),ref budget);
            return evidence;
        }
        private static bool ReadFunction(ScriptStoryFunction function,Func<int,int,int,ScriptSnapshot,ScriptStoryFunction> requested,int mapJump,int worldJump,int[] requestOpcodes,ScriptStoryEvidence evidence,HashSet<int> visiting,ref int budget)
        {
            if(function==null || function.Code==null || function.Code.Count==0 || function.Snapshot==null || !visiting.Add(function.Identity))return false;
            try {
                var code=new Dictionary<int,ScriptInstruction>();foreach(var instruction in function.Code)code[instruction.Offset]=instruction;
                int ip=function.Code[0].Offset;int? value=null;
                while(budget-- >0) {
                    ScriptInstruction item;if(!code.TryGetValue(ip,out item))return false;
                    if(item.Opcode==4)return true;
                    if(item.Opcode==5) {
                        int? before=function.Snapshot.Read(0,7,0);
                        if(!ExitScriptAnalyzer.TryExpression(item.Expression,function.Snapshot,out value))return false;
                        int? after=function.Snapshot.Read(0,7,0);if(after.HasValue && before.HasValue && after>before)evidence.Milestones.Add(after.Value);
                    }else if(item.Opcode>=1 && item.Opcode<=3) {
                        if(!item.Argument.HasValue || item.Opcode!=1 && !value.HasValue)return false;
                        bool branch=item.Opcode==1 || (item.Opcode==2?value==0:value!=0);
                        if(branch){ip=item.NextOffset+(item.Opcode==2?item.Argument.Value:(short)item.Argument.Value);continue;}
                    }else if(item.Opcode==6 || item.Opcode==11 || item.Opcode==13) {
                        int? branch=ScriptBranches.SelectSwitch(item,value);if(!branch.HasValue)return false;ip=branch.Value;continue;
                    }else if(item.Opcode==mapJump || item.Opcode==worldJump) {
                        var destination=item.Constant(0)??ExitScriptAnalyzer.Expression(item.ArgumentExpression(0),function.Snapshot);
                        if(destination.HasValue)evidence.Destinations.Add(destination.Value);return destination.HasValue;
                    }else if(Contains(requestOpcodes,item.Opcode)) {
                        int? uid=item.Constant(1),tag=item.Constant(2);
                        if(!uid.HasValue || !tag.HasValue || requested==null)return false;
                        if(!ReadFunction(requested(uid.Value,tag.Value,function.Identity,function.Snapshot),requested,mapJump,worldJump,requestOpcodes,evidence,visiting,ref budget))return false;
                    }
                    ip=item.NextOffset;
                }
                return false;
            }finally{visiting.Remove(function.Identity);}
        }
        private static bool Contains(int[] values,int value){if(values!=null)foreach(int item in values)if(item==value)return true;return false;}
    }
}
