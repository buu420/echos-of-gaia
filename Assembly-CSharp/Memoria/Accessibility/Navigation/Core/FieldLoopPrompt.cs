using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    // Some background objects publish their prompt from a passive event loop
    // instead of owning a Talk/Push handler. Prove only the exact native icon
    // site, using a prospective standing point and detached state. Never read
    // the following key test, run a request, or simulate the interaction.
    public sealed class FieldLoopPrompt
    {
        private readonly Dictionary<int,ScriptInstruction> _code=new Dictionary<int,ScriptInstruction>();
        public FieldLoopPrompt(IList<ScriptInstruction> code)
        {if(code!=null)foreach(var instruction in code)_code[instruction.Offset]=instruction;}
        public bool Presented(int entry,int prompt,int iconOpcode,ScriptSnapshot snapshot,Func<int,int,int?> property)
        {
            if(snapshot==null || !snapshot.UserControl)return false;
            int ip=entry;int? value=null;
            for(int budget=0;budget<128;budget++) {
                ScriptInstruction item;if(!_code.TryGetValue(ip,out item))return false;
                if(item.Opcode==iconOpcode)return ip==prompt;
                if(ip==prompt)return false;
                switch(item.Opcode) {
                    case 5:
                        if(!ExitScriptAnalyzer.TryExpression(item.Expression,snapshot,out value,property))return false;
                        break;
                    case 1:case 2:case 3:
                        if(!item.Argument.HasValue || item.Opcode!=1 && !value.HasValue)return false;
                        if(item.Opcode==1 || (item.Opcode==2?value==0:value!=0)) {
                            ip=item.NextOffset+(item.Opcode==2?item.Argument.Value:(short)item.Argument.Value);continue;
                        }
                        break;
                    case 6:case 11:case 13:
                        var target=ScriptBranches.SelectSwitch(item,value);if(!target.HasValue)return false;
                        ip=target.Value;continue;
                    default:return false;
                }
                ip=item.NextOffset;
            }
            return false;
        }
    }
}
