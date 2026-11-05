using System;
using System.Collections.Generic;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class StoryEffectTests
    {
        private static ScriptInstruction Expr(int off,byte[] expression){return new ScriptInstruction(off,off+1,5){Expression=expression};}
        public static void ASharedScriptEffectMustRunOnItsBoundNativeInstance()
        {
            var step=new StoryStep{Kind=StoryStepKind.Talk,Effects=new[]{new StoryStepEffect{Sid=2,Uid=129,Tag=46,Offset=1290,Hash="native"}}};
            var evidence=new ScriptStoryEvidence();evidence.Effects.Add(new StoryEffectSite(3,2,46,1290,128));
            TestAssert.False(StoryStepCatalog.HasEffect(step,evidence,sid=>"native"));
            evidence.Effects.Clear();evidence.Effects.Add(new StoryEffectSite(3,2,46,1290,129));
            TestAssert.True(StoryStepCatalog.HasEffect(step,evidence,sid=>"native"));
            step.Effects[0].Uid=-1;TestAssert.True(StoryStepCatalog.HasEffect(step,evidence,sid=>"native"));
        }
        public static void AReachedAssignmentMustBelongToTheActualTriggerAndBranch()
        {
            byte[] compare={0xdc,0,0x7d,0x68,0x10,24,0x7f}; // counter < 4200
            byte[] write={0xdc,0,0x7d,0x72,0x10,44,0x7f}; // counter = 4210
            var code=new[]{Expr(0,compare),new ScriptInstruction(1,2,2){Argument=2},new ScriptInstruction(2,3,64),new ScriptInstruction(3,4,4),Expr(4,write),new ScriptInstruction(5,6,4)};
            var step=new StoryStep{Kind=StoryStepKind.Talk,Effects=new[]{new StoryStepEffect{Sid=22,Tag=3,Offset=4,Hash="exact"}}};
            foreach(int counter in new[]{4160,4180,4200}) {
                var global=new byte[16];global[0]=(byte)counter;global[1]=(byte)(counter>>8);
                var evidence=ScriptStoryAnalyzer.Read(new ScriptStoryFunction{Identity=22*256+3,ScriptId=22,Tag=3,Code=code,Snapshot=new ScriptSnapshot(global,new byte[64],null)},null,43,182,new[]{16,18,20});
                TestAssert.Equal(counter==4200,StoryStepCatalog.HasEffect(step,evidence,sid=>"exact"));
                TestAssert.False(StoryStepCatalog.HasEffect(step,evidence,sid=>"changed"));
                step.Kind=StoryStepKind.Touch;TestAssert.False(StoryStepCatalog.HasEffect(step,evidence,sid=>"exact"));step.Kind=StoryStepKind.Talk;
            }
        }
        public static void AnUnprovedWaitSiteCannotAdvanceToASibling()
        {
            var step=new StoryStep{Sid=3,Hash="actor",WaitSid=0,WaitHash="director",WaitIps=new[]{918},Completion=new[]{new StoryStepGuard{Sid=0,Hash="director",Expression=new byte[]{0}}}};
            Func<int,string> hashes=sid=>sid==3?"actor":"director";
            TestAssert.False(StoryStepCatalog.IsCurrent(step,9320,hashes,g=>null,s=>null));
            TestAssert.False(StoryStepCatalog.IsCurrent(step,9320,hashes,g=>null,s=>false));
            TestAssert.True(StoryStepCatalog.IsCurrent(step,9320,hashes,g=>null,s=>true));
        }
        public static void AControlChangeIsAppliedOnlyToTheDetachedProof()
        {
            var code=new[]{new ScriptInstruction(0,1,90),Expr(1,new byte[]{0x7a,2,0x7f}),new ScriptInstruction(2,3,2){Argument=1},Expr(3,new byte[]{0xdc,0,0x7d,20,0,44,0x7f}),new ScriptInstruction(4,5,4)};
            var global=new byte[16];global[0]=10;
            var evidence=ScriptStoryAnalyzer.Read(new ScriptStoryFunction{Identity=1,ScriptId=1,Tag=3,Code=code,Snapshot=new ScriptSnapshot(global,null,null)},null,43,182,null,null,90,91,null);
            TestAssert.False(evidence.Milestones.Contains(20));TestAssert.Equal((byte)10,global[0]);
        }
        public static void APositionAfterScriptedMovementIsUnknown()
        {
            var code=new[]{new ScriptInstruction(0,1,92),Expr(1,new byte[]{0x78,250,0,0x7d,5,0,32,0x7f}),new ScriptInstruction(2,3,2){Argument=1},Expr(3,new byte[]{0xdc,0,0x7d,20,0,44,0x7f}),new ScriptInstruction(4,5,4)};
            var evidence=ScriptStoryAnalyzer.Read(new ScriptStoryFunction{Identity=1,ScriptId=1,Tag=3,Code=code,Snapshot=new ScriptSnapshot(new byte[16],null,null)},null,43,182,null,(uid,index)=>5,90,91,new[]{92});
            TestAssert.False(evidence.Milestones.Contains(20));
        }
    }
}
