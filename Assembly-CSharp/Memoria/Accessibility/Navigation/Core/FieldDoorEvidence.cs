using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    public sealed class FieldDoorOpcodes
    {
        public int MapJump,WorldJump,JumpPosition,MoveQuad,Move;
        public int[] Requests,Animations,InteractionBarriers;
        public int[] PlainPassive;public int DisableControl=-1,EnableControl=-1,PreloadHint=-1;
    }
    public sealed class DoorTileObservation
    {
        public int Overlay,Camera,Flags;public bool HierarchyActive,HasRenderer;
        public float MinX,MinY,MaxX,MaxY;
    }
    public static class FieldDoorEvidence
    {
        private struct PathState {public int Offset,Animation;public bool Position,Walk;}
        // The normal trigger can start well before the remote mesh boundary.
        // Presentation belongs to its reachable surface, not that remote edge.
        public static bool TryTriggerSurface(bool provenEntrance,bool hasTriggerSurface,NavigationPoint[] candidates,Func<NavigationPoint,bool> presented,Func<NavigationPoint,bool> nativeFirst,NavigationPoint player,out NavigationPoint approach)
        {
            approach=new NavigationPoint();if(!provenEntrance || !hasTriggerSurface || candidates==null || presented==null || nativeFirst==null)return false;
            var ordered=new List<NavigationPoint>(candidates);
            ordered.Sort((a,b)=>NavigationPoint.Distance(player,a).CompareTo(NavigationPoint.Distance(player,b)));
            foreach(var point in ordered)if(point.IsFinite && nativeFirst(point) && presented(point)){approach=point;return true;}
            return false;
        }
        public static int[] Animations(IList<ScriptInstruction> code,Func<int,IList<ScriptInstruction>> playerFunction,FieldDoorOpcodes opcodes)
        { bool walk;return ReadPaths(code,playerFunction,opcodes,out walk); }
        public static bool HasWalkOut(IList<ScriptInstruction> code,Func<int,IList<ScriptInstruction>> playerFunction,FieldDoorOpcodes opcodes)
        { bool walk;ReadPaths(code,playerFunction,opcodes,out walk);return walk; }
        public static bool HasPlainWalkOut(IList<ScriptInstruction> code,Func<int,IList<ScriptInstruction>> playerFunction,FieldDoorOpcodes opcodes)
        {
            if(code==null)return false;
            foreach(var item in code)if(Has(opcodes.Animations,item.Opcode))return false;
            return HasWalkOut(code,playerFunction,opcodes);
        }
        // Mixed handlers need proof of the current plain branch. A possible
        // plain branch must never make a currently animated door into a walk-off.
        public static bool CurrentPlainWalkOut(IList<ScriptInstruction> code,ScriptSnapshot snapshot,FieldDoorOpcodes opcodes,Func<int,int,int?> objectProperty)
        {
            if(code==null || code.Count==0 || snapshot==null || opcodes==null)return false;
            var offsets=new Dictionary<int,ScriptInstruction>();foreach(var item in code)offsets[item.Offset]=item;
            int ip=code[0].Offset;int? value=null;bool position=false,walk=false;
            for(int step=0;step<512;step++) {
                ScriptInstruction item;if(!offsets.TryGetValue(ip,out item))return false;
                int op=item.Opcode;
                if(op==opcodes.MapJump || op==opcodes.WorldJump)return position && walk;
                if(op==4 || Has(opcodes.Animations,op) || Has(opcodes.Requests,op) || Has(opcodes.InteractionBarriers,op) || ReadsInput(item))return false;
                if(op==opcodes.PreloadHint && item.Constant(0)!=5 && item.Constant(0)!=17 && item.Constant(0)!=145)return false;
                if(op==5) {
                    var expression=item.ArgumentExpression(0);
                    if(!ExitScriptAnalyzer.TryExpression(expression,snapshot,out value,walk?null:objectProperty)) {
                        // An unused unsupported calculation is harmless, but a
                        // failed assignment cannot leave a stale shadow value.
                        if(expression==null || FieldInteractionGuard.Assigns(expression))return false;
                        value=null;
                    }
                } else if(op>=1 && op<=3) {
                    int? argument=item.Constant(0);if(!argument.HasValue || op!=1 && !value.HasValue)return false;
                    bool branch=op==1 || (op==2?value==0:value!=0);
                    if(branch){ip=item.NextOffset+(op==2?argument.Value:(short)argument.Value);continue;}
                } else if(op==6 || op==11 || op==13) {
                    int? branch=ScriptBranches.SelectSwitch(item,value);if(!branch.HasValue)return false;ip=branch.Value;continue;
                } else if(op==opcodes.JumpPosition)position=true;
                else if(op==opcodes.MoveQuad)walk=true;
                else if(op!=0 && !Has(opcodes.PlainPassive,op))return false;
                if(op==opcodes.DisableControl)snapshot.UserControl=false;
                if(op==opcodes.EnableControl)snapshot.UserControl=true;
                ip=item.NextOffset;
            }
            return false;
        }
        private static int[] ReadPaths(IList<ScriptInstruction> code,Func<int,IList<ScriptInstruction>> playerFunction,FieldDoorOpcodes opcodes,out bool walk)
        {
            walk=false;var animations=new HashSet<int>();if(code==null || code.Count==0)return new int[0];
            var offsets=new Dictionary<int,ScriptInstruction>();foreach(var item in code)offsets[item.Offset]=item;
            var pending=new Queue<PathState>();pending.Enqueue(new PathState{Offset=code[0].Offset,Animation=-1});var visited=new HashSet<string>();
            while(pending.Count>0) {
                var state=pending.Dequeue();string key=state.Offset+":"+state.Position+":"+state.Walk+":"+state.Animation;
                if(!visited.Add(key))continue;
                if(visited.Count>8192){walk=false;return new int[0];}
                ScriptInstruction item;if(!offsets.TryGetValue(state.Offset,out item))continue;
                if(item.Opcode==opcodes.MapJump || item.Opcode==opcodes.WorldJump) {
                    if(state.Position && state.Walk){walk=true;if(state.Animation>=0)animations.Add(state.Animation);}continue;
                }
                if(item.Opcode==4 || Has(opcodes.InteractionBarriers,item.Opcode) || ReadsInput(item))continue;
                if(item.Opcode==opcodes.JumpPosition)state.Position=true;
                if(item.Opcode==opcodes.MoveQuad)state.Walk=true;
                if(Has(opcodes.Requests,item.Opcode) && item.Constant(0).HasValue && item.Constant(1)==250 && item.Constant(2).HasValue && playerFunction!=null)
                    state.Walk|=PlayerWalks(playerFunction(item.Constant(2).Value),opcodes);
                if(Has(opcodes.Animations,item.Opcode))state.Animation=item.Constant(0)??-1;
                if(item.Opcode>=1 && item.Opcode<=3) {
                    if(!item.Constant(0).HasValue)continue;
                    var branch=state;branch.Offset=item.NextOffset+(item.Opcode==2?item.Constant(0).Value:(short)item.Constant(0).Value);pending.Enqueue(branch);
                    if(item.Opcode==1)continue;
                }else if(item.Opcode==6 || item.Opcode==11 || item.Opcode==13) {
                    foreach(int offset in ScriptBranches.SwitchTargets(item)){var branch=state;branch.Offset=offset;pending.Enqueue(branch);}continue;
                }
                state.Offset=item.NextOffset;pending.Enqueue(state);
            }
            var result=new List<int>(animations);result.Sort();return result.ToArray();
        }
        private static bool PlayerWalks(IList<ScriptInstruction> code,FieldDoorOpcodes opcodes)
        {
            if(code==null || code.Count==0)return false;
            var offsets=new Dictionary<int,ScriptInstruction>();foreach(var item in code)offsets[item.Offset]=item;
            var pending=new Queue<int>();pending.Enqueue(code[0].Offset);var visited=new HashSet<int>();
            while(pending.Count>0 && visited.Count<1024) {
                int offset=pending.Dequeue();if(!visited.Add(offset))continue;ScriptInstruction item;if(!offsets.TryGetValue(offset,out item))continue;
                if(item.Opcode==opcodes.Move && Sysvar(item.ArgumentExpression(0),10) && Sysvar(item.ArgumentExpression(1),11))return true;
                if(item.Opcode==4 || Has(opcodes.InteractionBarriers,item.Opcode) || ReadsInput(item))continue;
                if(item.Opcode>=1 && item.Opcode<=3) {
                    if(!item.Constant(0).HasValue)continue;pending.Enqueue(item.NextOffset+(item.Opcode==2?item.Constant(0).Value:(short)item.Constant(0).Value));if(item.Opcode==1)continue;
                }else if(item.Opcode==6 || item.Opcode==11 || item.Opcode==13) {foreach(int branch in ScriptBranches.SwitchTargets(item))pending.Enqueue(branch);continue;}
                pending.Enqueue(item.NextOffset);
            }return false;
        }
        private static bool Sysvar(byte[] expression,int id) {return expression!=null && expression.Length==3 && expression[0]==0x7a && expression[1]==id && expression[2]==0x7f;}
        private static bool Has(int[] values,int value) {if(values!=null)foreach(int item in values)if(item==value)return true;return false;}
        private static bool ReadsInput(ScriptInstruction item)
        {
            int count=item.ArgumentExpressions==null?1:item.ArgumentExpressions.Length;
            for(int i=0;i<count;i++) {
                var expression=item.ArgumentExpression(i);if(expression==null)continue;
                for(int p=0;p<expression.Length;) {
                    int op=expression[p++];if(op==79 || (op>=88 && op<=92))return true;
                    int bytes=op==0x7e?4:op>=0xe0 || op==0x78 || op==0x7d?2:op>=0xc0 || op==0x29 || op==0x5f || op==0x79 || op==0x7a?1:0;
                    p+=bytes;if(p>expression.Length)return true;
                }
            }return false;
        }
        public static bool TryBind(int animationCamera,int currentCamera,IList<DoorTileObservation> frames,NavigationPoint projected,bool onScreen,out int overlay)
        {
            overlay=-1;if(animationCamera!=currentCamera || !onScreen || !projected.IsFinite || frames==null)return false;
            DoorTileObservation displayed=null;
            foreach(var frame in frames)if(frame!=null && (frame.Flags&2)!=0 && frame.HierarchyActive) {
                if(displayed!=null && displayed.Overlay!=frame.Overlay)return false;displayed=frame;
            }
            if(displayed==null || !displayed.HasRenderer || displayed.Camera!=currentCamera || (displayed.Flags&(1|8|128))!=0)return false;
            if(!Finite(displayed.MinX)||!Finite(displayed.MinY)||!Finite(displayed.MaxX)||!Finite(displayed.MaxY)||displayed.MaxX<=displayed.MinX || displayed.MaxY<=displayed.MinY)return false;
            if(projected.X<displayed.MinX-12 || projected.X>displayed.MaxX+12 || projected.Y<displayed.MinY-12 || projected.Y>displayed.MaxY+12)return false;
            overlay=displayed.Overlay;return true;
        }
        public static bool InViewport(NavigationPoint screen,float width,float height)
        { return screen.IsFinite && Finite(width) && Finite(height) && width>0 && height>0 && screen.Z>=0 && screen.X>=0 && screen.X<=width && screen.Y>=0 && screen.Y<=height; }
        private static bool Finite(float value) {return !float.IsNaN(value)&&!float.IsInfinity(value);}
    }
}
