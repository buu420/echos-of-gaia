using System;
using System.Collections.Generic;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    // Indirect exits (the jump is in a requested director): only a reviewed service menu or a plain
    // walk-out on the path that runs now is admitted; a Story scene stays a Story event.
    internal static class FieldExitAdmissionTests
    {
        private const int MapJump=43,WorldJump=182,ReqAsync=16,ReqWait=20,Mes=0x1a,Expr=5,Ifn=2,Jmp=1,Ret=4,Switch=11;
        private static readonly int[] Requests={ReqAsync,ReqWait};
        private static byte[] C(int v){return new[]{(byte)0x7d,(byte)(v&255),(byte)((v>>8)&255)};}
        private static byte[] E(params byte[][] parts){var all=new List<byte>();foreach(var p in parts)all.AddRange(p);all.Add(0x7f);return all.ToArray();}
        private static byte[] Counter(){return new byte[]{0xdc,0};}
        private static byte[] MapByte(int index){return new[]{(byte)0xd5,(byte)index};}
        private static ScriptInstruction I(int offset,int next,int opcode,params int?[] args){var item=new ScriptInstruction(offset,next,opcode){Arguments=args.Length==0?null:args};if(args.Length!=0)item.Argument=args[0];return item;}
        private static ScriptInstruction X(int offset,int next,byte[] expression){return new ScriptInstruction(offset,next,Expr,expression);}
        private static FieldExitPathEvidence Read(List<ScriptInstruction> region,Dictionary<int,List<ScriptInstruction>> entries,Func<int,int,int,bool?> requestable)
        {
            Func<ScriptStoryFunction> start=()=>new ScriptStoryFunction{Identity=12*256+2,ScriptId=12,Tag=2,Code=region,Snapshot=new ScriptSnapshot(new byte[64],new byte[64],new byte[16])};
            Func<int,int,int,ScriptSnapshot,ScriptStoryFunction> request=(uid,tag,caller,snapshot)=>{List<ScriptInstruction> code;return entries.TryGetValue(uid,out code)?new ScriptStoryFunction{Identity=uid*256+tag,ScriptId=uid,Tag=tag,Code=code,Snapshot=snapshot.WithInstance(new byte[16])}:null;};
            return FieldExitPaths.Read(start,request,MapJump,WorldJump,Requests,null,null,-1,-1,null,new FieldExitPathOptions{DialogueOpcodes=new[]{Mes},AsyncRequest=ReqAsync,Requestable=requestable});
        }
        // Region: request director entry 0 tag 26 at level 4, then return.
        private static List<ScriptInstruction> Region(int opcode){return new List<ScriptInstruction>{I(10,15,opcode,4,0,26),I(15,16,Ret)};}
        private static Dictionary<int,List<ScriptInstruction>> Director(params ScriptInstruction[] code){return new Dictionary<int,List<ScriptInstruction>>{{0,new List<ScriptInstruction>(code)}};}
        public static void APlainDirectorWalkOutIsAnOrdinaryExit()
        {
            var evidence=Read(Region(ReqWait),Director(I(30,33,MapJump,752)),(uid,level,caller)=>true);
            TestAssert.Equal(FieldExitPathKind.Determined,evidence.Kind);TestAssert.False(evidence.Dialogue);TestAssert.False(evidence.StoryCounter);
            TestAssert.Equal(FieldExitAdmission.Walk,FieldExitAdmissions.Admit(null,evidence));
        }
        public static void AStoryWalkInWithDialogueOrACounterWriteIsNotAnExit()
        {
            var scene=Read(Region(ReqWait),Director(I(30,36,Mes,0,0,12),I(36,39,MapJump,207)),(uid,level,caller)=>true);
            TestAssert.True(scene.Dialogue);TestAssert.Equal(FieldExitAdmission.None,FieldExitAdmissions.Admit(null,scene));
            var story=Read(Region(ReqWait),Director(X(30,38,E(Counter(),C(2650),new byte[]{44})),I(38,41,MapJump,2502)),(uid,level,caller)=>true);
            TestAssert.True(story.StoryCounter);TestAssert.Equal(FieldExitAdmission.None,FieldExitAdmissions.Admit(null,story));
            // A reviewed service whose current path writes the scenario counter is a Story event too.
            TestAssert.Equal(FieldExitAdmission.None,FieldExitAdmissions.Admit("Air cab",story));
        }
        public static void ABusyDirectorRejectsAWaitingRequestAndSkipsAnAsyncOne()
        {
            var director=Director(I(30,33,MapJump,752));
            TestAssert.Equal(FieldExitPathKind.Unknown,Read(Region(ReqWait),director,(uid,level,caller)=>false).Kind);
            TestAssert.Equal(FieldExitPathKind.Unknown,Read(Region(ReqWait),director,(uid,level,caller)=>null).Kind);
            // REQ to a busy entry does not run it: nothing jumps now.
            var skipped=Read(Region(ReqAsync),director,(uid,level,caller)=>false);
            TestAssert.Equal(FieldExitPathKind.NoJump,skipped.Kind);TestAssert.Equal(FieldExitAdmission.None,FieldExitAdmissions.Admit(null,skipped));
        }
        public static void AGuardThatIsFalseNowAdmitsNothing()
        {
            // if(map[1]==1) request the director; the guard is false in current memory.
            var region=new List<ScriptInstruction>{X(10,18,E(MapByte(1),C(1),new byte[]{32})),I(18,21,Ifn,5),I(21,26,ReqWait,4,0,26),I(26,27,Ret)};
            var evidence=Read(region,Director(I(30,33,MapJump,752)),(uid,level,caller)=>true);
            TestAssert.Equal(FieldExitPathKind.NoJump,evidence.Kind);TestAssert.Equal(FieldExitAdmission.None,FieldExitAdmissions.Admit("Air cab",evidence));
        }
        public static void AReviewedServiceMenuIsAdmittedAsAService()
        {
            // choice 0 -> map[0x39]=1, choice 1 -> map[0x39]=2; the cab (entry 14) switches on map[0x39].
            var region=new List<ScriptInstruction>{
                X(10,14,E(new byte[]{0x7a,9})),I(14,20,Switch,0,40-15,20-15,40-15),
                X(20,28,E(MapByte(0x39),C(1),new byte[]{44})),I(28,31,Jmp,48-31),
                X(40,48,E(MapByte(0x39),C(2),new byte[]{44})),
                I(48,53,ReqWait,4,14,28),I(53,54,Ret)};
            var cab=new Dictionary<int,List<ScriptInstruction>>{{14,new List<ScriptInstruction>{X(60,64,E(MapByte(0x39))),I(64,70,Switch,1,80-65,70-65,80-65),I(70,73,MapJump,563),I(80,83,MapJump,572)}}};
            var evidence=Read(region,cab,(uid,level,caller)=>true);
            TestAssert.Equal(FieldExitPathKind.ChoiceMenu,evidence.Kind);TestAssert.True(evidence.Destinations.Contains(563));TestAssert.True(evidence.Destinations.Contains(572));
            TestAssert.Equal(FieldExitAdmission.Service,FieldExitAdmissions.Admit("Air cab",evidence));
            // The same menu without a reviewed function is not admitted as an indirect exit.
            TestAssert.Equal(FieldExitAdmission.None,FieldExitAdmissions.Admit(null,evidence));
        }
        public static void AConfirmationPromptWalkOutIsAnExit()
        {
            var evidence=new FieldExitPathEvidence{Kind=FieldExitPathKind.ChoiceSingle,Dialogue=true};evidence.Destinations.Add(752);
            TestAssert.Equal(FieldExitAdmission.Walk,FieldExitAdmissions.Admit(null,evidence));
            TestAssert.Equal(FieldExitAdmission.None,FieldExitAdmissions.Admit(null,new FieldExitPathEvidence{Kind=FieldExitPathKind.Unknown}));
        }
    }
}
