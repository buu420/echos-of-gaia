using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    public static class ScriptBranches
    {
        public static int[] SwitchTargets(ScriptInstruction item)
        {
            var result=new List<int>();if(item==null || item.Arguments==null)return result.ToArray();
            int count=item.Arguments.Length;
            if(item.Opcode==6) {
                if(count<1 || (count&1)==0)return result.ToArray();
                for(int i=0;i<count;i+=2)if(item.Constant(i).HasValue)result.Add(item.Offset+4+item.Constant(i).Value);
            }else if(item.Opcode==11 || item.Opcode==13) {
                if(count<2)return result.ToArray();int origin=item.Offset+(item.Opcode==11?1:2);
                for(int i=1;i<count;i++)if(item.Constant(i).HasValue)result.Add(origin+item.Constant(i).Value);
            }
            return result.ToArray();
        }
        public static int? SelectSwitch(ScriptInstruction item,int? value)
        {
            if(item==null || !value.HasValue || item.Arguments==null)return null;int count=item.Arguments.Length;
            if(item.Opcode==6) {
                if(count<1 || (count&1)==0 || !item.Constant(0).HasValue)return null;
                int input=value.Value&65535;for(int i=1;i+1<count;i+=2) {
                    if(!item.Constant(i).HasValue || !item.Constant(i+1).HasValue)return null;
                    if(item.Constant(i).Value==input)return item.Offset+4+item.Constant(i+1).Value;
                }return item.Offset+4+item.Constant(0).Value;
            }
            if(item.Opcode!=11 && item.Opcode!=13 || count<2 || !item.Constant(0).HasValue || !item.Constant(1).HasValue)return null;
            int index=value.Value-(short)item.Constant(0).Value;
            int argument=index>=0 && index<count-2?index+2:1;
            return item.Constant(argument).HasValue?(int?)(item.Offset+(item.Opcode==11?1:2)+item.Constant(argument).Value):null;
        }
    }
}
