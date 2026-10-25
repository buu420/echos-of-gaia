using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    public enum ScriptExitOutcome { Unknown, NoJumpNow, JumpNow }
    public sealed class ScriptInstruction
    {
        public int Offset, NextOffset, Opcode; public int? Argument; public byte[] Expression;
        public int?[] Arguments; public byte[][] ArgumentExpressions;
        public ScriptInstruction(int offset,int next,int opcode,int? argument=null) { Offset=offset; NextOffset=next; Opcode=opcode; Argument=argument; }
        public ScriptInstruction(int offset,int next,int opcode,byte[] expression) : this(offset,next,opcode) { Expression=expression; }
        public int? Constant(int index) { return Arguments!=null && index>=0 && index<Arguments.Length?Arguments[index]:index==0?Argument:null; }
        public byte[] ArgumentExpression(int index) { return ArgumentExpressions!=null && index>=0 && index<ArgumentExpressions.Length?ArgumentExpressions[index]:index==0?Expression:null; }
    }
    public sealed class ScriptSnapshot
    {
        private readonly byte[][] _buffers;
        private readonly HashSet<int>[] _unknown={new HashSet<int>(),new HashSet<int>(),new HashSet<int>()};
        public bool UserControl=true;
        public ScriptSnapshot(byte[] global,byte[] map,byte[] instance) { _buffers=new[]{Clone(global),Clone(map),Clone(instance)}; }
        private static byte[] Clone(byte[] value) { return value==null ? new byte[0] : (byte[])value.Clone(); }
        public static byte[] InstanceBytes(byte[] buffer,int wordOffset)
        {
            if(buffer==null || wordOffset<0 || wordOffset>buffer.Length/4)return new byte[0];
            int offset=wordOffset*4;var result=new byte[buffer.Length-offset];Array.Copy(buffer,offset,result,0,result.Length);return result;
        }
        public ScriptSnapshot WithInstance(byte[] instance)
        {
            var child=new ScriptSnapshot(null,null,instance){UserControl=UserControl};child._buffers[0]=_buffers[0];child._buffers[1]=_buffers[1];child._unknown[0]=_unknown[0];child._unknown[1]=_unknown[1];return child;
        }
        internal int? Read(int source,int type,int index)
        {
            if(source<0 || source>2 || type<0 || type>7 || index<0) return null; var b=_buffers[source];
            int offset=type<2 ? index>>3 : index, size=type<2?1:type<4?3:type<6?1:2;
            if(offset<0 || offset+size>b.Length) return null;
            for(int i=0;i<size;i++)if(_unknown[source].Contains(offset+i))return null;
            if(type<2) return (b[offset]>>(index&7))&1;
            if(type==4) return (sbyte)b[offset]; if(type==5) return b[offset];
            if(type==6) return b[offset]|((sbyte)b[offset+1]<<8); if(type==7) return b[offset]|(b[offset+1]<<8);
            // Match the engine's signed high byte even for UInt24.
            return b[offset]|(b[offset+1]<<8)|((sbyte)b[offset+2]<<16);
        }
        internal bool Write(int source,int type,int index,int value)
        {
            if(!InBounds(source,type,index))return false;var b=_buffers[source]; int offset=type<2?index>>3:index;
            if(type<2) b[offset]=(byte)((b[offset]&~(1<<(index&7)))|((value&1)<<(index&7)));
            else { int size=type<4?3:type<6?1:2; for(int i=0;i<size;i++){b[offset+i]=(byte)(value>>(8*i));_unknown[source].Remove(offset+i);} }
            return true;
        }
        private bool InBounds(int source,int type,int index){if(source<0 || source>2 || type<0 || type>7 || index<0)return false;int offset=type<2?index>>3:index,size=type<2?1:type<4?3:type<6?1:2;return offset>=0 && offset<=_buffers[source].Length-size;}
        internal bool Invalidate(int source,int type,int index){if(!InBounds(source,type,index))return false;int offset=type<2?index>>3:index,size=type<2?1:type<4?3:type<6?1:2;for(int i=0;i<size;i++)_unknown[source].Add(offset+i);return true;}
    }
    public static class ExitScriptAnalyzer
    {
        private struct Value { public int? Number; public int Source,Type,Index; public bool Variable; }
        public static ScriptExitOutcome Evaluate(IList<ScriptInstruction> code,ScriptSnapshot snapshot,int mapJump,int worldJump,int moveQuad,int jumpPosition)
        {
            if(code.Count==0)return ScriptExitOutcome.Unknown;
            var offsets=new Dictionary<int,ScriptInstruction>(); foreach(var item in code) offsets[item.Offset]=item;
            int ip=code[0].Offset; int? value=null;
            for(int step=0;step<512;step++) {
                ScriptInstruction item; if(!offsets.TryGetValue(ip,out item)) return ScriptExitOutcome.Unknown;
                if(item.Opcode==mapJump || item.Opcode==worldJump) return ScriptExitOutcome.JumpNow;
                if(item.Opcode==4)return ScriptExitOutcome.NoJumpNow;
                if(item.Opcode==5) { value=Expression(item.Expression,snapshot); if(!value.HasValue)return ScriptExitOutcome.Unknown; }
                else if(item.Opcode>=1 && item.Opcode<=3) {
                    if(!item.Argument.HasValue || (item.Opcode!=1 && !value.HasValue)) return ScriptExitOutcome.Unknown;
                    bool branch=item.Opcode==1 || (item.Opcode==2 ? value==0 : value!=0);
                    if(branch) { ip=item.NextOffset+(item.Opcode==2?item.Argument.Value:(short)item.Argument.Value); continue; }
                } else if(item.Opcode==6 || item.Opcode==11 || item.Opcode==13) {
                    int? branch=ScriptBranches.SelectSwitch(item,value);if(!branch.HasValue)return ScriptExitOutcome.Unknown;ip=branch.Value;continue;
                } else if(item.Opcode!=0 && item.Opcode!=moveQuad && item.Opcode!=jumpPosition) return ScriptExitOutcome.Unknown;
                ip=item.NextOffset;
            }
            return ScriptExitOutcome.Unknown;
        }
        internal static int? Expression(byte[] bytes,ScriptSnapshot snapshot)
        {int? value;return TryExpression(bytes,snapshot,out value)?value:null;}
        internal static bool TryExpression(byte[] bytes,ScriptSnapshot snapshot,out int? value,Func<int,int,int?> objectProperty=null)
        {
            value=null;if(bytes==null)return false; var stack=new Stack<Value>();
            try { for(int p=0;p<bytes.Length;) {
                int op=bytes[p++];
                if(op==0x7f){if(stack.Count!=1)return false;value=stack.Pop().Number;return true;}
                if(op>=0xc0) { int index=bytes[p++]; if(op>=0xe0) index|=bytes[p++]<<8; int source=op&3,type=(op&0x1c)>>2; stack.Push(new Value{Number=snapshot.Read(source,type,index),Variable=true,Source=source,Type=type,Index=index}); continue; }
                if(op==0x7d || op==0x7e) { int number=bytes[p++]|(bytes[p++]<<8); if(op==0x7e)number|=(bytes[p++]<<16)|(bytes[p++]<<24); else number=(short)number; stack.Push(new Value{Number=number}); continue; }
                if(op==0x7a) { int code=bytes[p++]; stack.Push(new Value{Number=code==2 ? (snapshot.UserControl?1:0) : (int?)null}); continue; }
                // Object properties remain unknown unless a narrow caller
                // supplies an observed value. System-list values are unknown.
                // Assignments must invalidate an unreadable shadow destination.
                if(op==0x78){int uid=bytes[p++],property=bytes[p++];stack.Push(new Value{Number=objectProperty==null?null:objectProperty(uid,property)});continue;}
                if(op==0x79){p++;if(p>bytes.Length)return false;stack.Push(new Value());continue;}
                if(op>=4 && op<=7){var a=stack.Pop();if(!a.Variable)return false;int? next=a.Number.HasValue?(int?)(a.Number.Value+(op==4 || op==6?1:-1)):null;if(next.HasValue){if(!snapshot.Write(a.Source,a.Type,a.Index,next.Value))return false;}else if(!snapshot.Invalidate(a.Source,a.Type,a.Index))return false;stack.Push(new Value{Number=op<6?a.Number:next});continue;}
                if(op==12 || op==13 || op==14 || op==15 || op==16 || op>=70 && op<=73) { var a=stack.Pop(); if(!a.Number.HasValue){stack.Push(new Value());continue;} int x=a.Number.Value; stack.Push(new Value{Number=op==12?x:op==13?-x:op==16?~x:op==70?(sbyte)x:op==71?(byte)x:op==72?(short)x:op==73?(ushort)x:x==0?1:0}); continue; }
                var right=stack.Pop(); var left=stack.Pop();
                if(op==44) { if(!left.Variable)return false;if(right.Number.HasValue){if(!snapshot.Write(left.Source,left.Type,left.Index,right.Number.Value))return false;}else if(!snapshot.Invalidate(left.Source,left.Type,left.Index))return false;stack.Push(new Value{Number=right.Number}); continue; }
                if(!left.Number.HasValue || !right.Number.HasValue){if(op<17 || op>40)return false;stack.Push(new Value());continue;} int aValue=left.Number.Value,bValue=right.Number.Value,result;
                switch(op) {
                    case 17:result=aValue*bValue;break;case 18:if(bValue==0)return false;result=aValue/bValue;break;case 19:if(bValue==0)return false;result=aValue%bValue;break;
                    case 20:result=aValue+bValue;break;case 21:result=aValue-bValue;break;case 22:result=aValue<<bValue;break;case 23:result=aValue>>bValue;break;
                    case 24:case 28:result=aValue<bValue?1:0;break;case 25:case 29:result=aValue>bValue?1:0;break;case 26:case 30:result=aValue<=bValue?1:0;break;case 27:case 31:result=aValue>=bValue?1:0;break;
                    case 32:case 34:result=aValue==bValue?1:0;break;case 33:case 35:result=aValue!=bValue?1:0;break;
                    case 36:result=aValue&bValue;break;case 37:result=aValue^bValue;break;case 38:result=aValue|bValue;break;case 39:result=aValue!=0&&bValue!=0?1:0;break;case 40:result=aValue!=0||bValue!=0?1:0;break;default:return false;
                } stack.Push(new Value{Number=result});
            } } catch (Exception e) { if(!(e is IndexOutOfRangeException) && !(e is InvalidOperationException) && !(e is OverflowException))throw; }
            return false;
        }
    }
}
