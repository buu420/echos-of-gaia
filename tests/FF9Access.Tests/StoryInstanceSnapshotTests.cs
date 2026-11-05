using System;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class StoryInstanceSnapshotTests
    {
        private static ScriptInstruction Expr(int offset,byte[] value){return new ScriptInstruction(offset,offset+1,5){Expression=value};}
        private static ScriptInstruction Request(int offset,int uid,int tag){return new ScriptInstruction(offset,offset+1,20){Arguments=new int?[]{0,uid,tag}};}
        private static byte[] SetLocal=new byte[]{0xda,0,0x7d,1,0,44,0x7f};
        private static ScriptInstruction[] CheckLocal(){return new[]{Expr(0,new byte[]{0xda,0,0x7d,1,0,32,0x7f}),new ScriptInstruction(1,2,2){Argument=1},Expr(2,new byte[]{0xdc,0,0x7d,20,0,44,0x7f}),new ScriptInstruction(3,4,4)};}
        private static ScriptStoryEvidence Read(int callerUid,int childUid,bool twice)
        {
            int callerSid=callerUid==128?2:8;
            var start=new ScriptStoryFunction{Identity=callerUid*256+3,ScriptId=callerSid,Tag=3,Snapshot=new ScriptSnapshot(new byte[32],new byte[32],new byte[32]),Code=twice?new[]{Request(0,childUid,10),Request(1,childUid,11),new ScriptInstruction(2,3,4)}:new[]{Expr(0,SetLocal),Request(1,childUid,11),new ScriptInstruction(2,3,4)}};
            Func<int,int,int,ScriptSnapshot,ScriptStoryFunction> requested=(uid,tag,identity,snapshot)=>new ScriptStoryFunction{Identity=uid*256+tag,ScriptId=uid==callerUid?callerSid:2,Tag=tag,Snapshot=snapshot.WithInstance(new byte[32]),Code=tag==10?new[]{Expr(0,SetLocal),new ScriptInstruction(1,2,4)}:CheckLocal()};
            return ScriptStoryAnalyzer.Read(start,requested,43,182,new[]{16,18,20});
        }
        public static void ARequestedTagOnTheSameActorSeesTheCallersLocalWrites()
        {TestAssert.True(Read(130,130,false).Milestones.Contains(20));}
        public static void TwoSynchronousCallsKeepThatActorsLocalState()
        {TestAssert.True(Read(130,129,true).Milestones.Contains(20));}
        public static void TwoInstancesOfOneScriptDoNotShareLocalVariables()
        {TestAssert.False(Read(128,129,false).Milestones.Contains(20));}
    }
}
