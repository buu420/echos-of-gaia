using System;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class StoryRequestSchedulingTests
    {
        private static ScriptInstruction Write(int offset){return new ScriptInstruction(offset,offset+1,5){Expression=new byte[]{0xdc,0,0x7d,20,0,44,0x7f}};}
        private static ScriptStoryEvidence Read(bool yield)
        {
            var request=new ScriptInstruction(0,1,16){Arguments=new int?[]{6,129,46}};
            var start=new ScriptStoryFunction{Identity=130*256+3,ScriptId=8,Tag=3,Snapshot=new ScriptSnapshot(new byte[32],new byte[32],new byte[32]),Code=yield?new[]{request,new ScriptInstruction(1,2,34),Write(2),new ScriptInstruction(3,4,4)}:new[]{request,Write(1),new ScriptInstruction(2,3,4)}};
            Func<int,int,int,ScriptSnapshot,ScriptStoryFunction> child=(uid,tag,caller,snapshot)=>new ScriptStoryFunction{Identity=uid*256+tag,ScriptId=2,Tag=tag,Snapshot=snapshot.WithInstance(null),Code=new[]{new ScriptInstruction(10,11,5){Expression=new byte[]{0x7a,9,0x7f}},new ScriptInstruction(11,12,2){Argument=1},new ScriptInstruction(12,13,4)}};
            return ScriptStoryAnalyzer.Read(start,child,43,182,new[]{16,18,20},null,35,36,null,16,new[]{34,66});
        }
        public static void AnAsyncRequestCannotBlockTheCallersImmediatePhaseWrite()
        {
            // Native REQ returns 0 and continues even when its child is busy or
            // waits on unreadable animation/choice state. It does not join it.
            var evidence=Read(false);TestAssert.True(evidence.Milestones.Contains(20));
            TestAssert.True(evidence.Reached.Contains(new StoryEffectSite(3,8,3,0,130)));
        }
        public static void AYieldAfterAsyncWorkEndsTheCurrentStateProof()
        {
            // Once the caller yields, another script can change the shared
            // variables. Do not predict its next-frame branch from stale bytes.
            TestAssert.False(Read(true).Milestones.Contains(20));
        }
        public static void ACurrentPrefixDoesNotForecastPastANativeWaitWithoutAnAsyncRequest()
        {
            var start=new ScriptStoryFunction{Identity=9*256+3,ScriptId=9,Tag=3,Snapshot=new ScriptSnapshot(new byte[32],null,null),
                Code=new[]{new ScriptInstruction(0,1,34),Write(1),new ScriptInstruction(2,3,4)}};
            var evidence=ScriptStoryAnalyzer.Read(start,null,43,182,new[]{16,18,20},null,35,36,null,16,new[]{34,18,20});
            TestAssert.True(evidence.Reached.Contains(new StoryEffectSite(3,9,3,0,9)));
            TestAssert.False(evidence.Milestones.Contains(20));
        }
        public static void ACurrentPrefixDoesNotJoinAWaitForAcceptanceOrCompletion()
        {
            foreach(int opcode in new[]{18,20}) {
                var start=new ScriptStoryFunction{Identity=9*256+3,ScriptId=9,Tag=3,Snapshot=new ScriptSnapshot(new byte[32],null,null),
                    Code=new[]{new ScriptInstruction(0,1,opcode){Arguments=new int?[]{2,13,10}},Write(1),new ScriptInstruction(2,3,4)}};
                Func<int,int,int,ScriptSnapshot,ScriptStoryFunction> child=(uid,tag,caller,snapshot)=>new ScriptStoryFunction{Identity=uid*256+tag,ScriptId=13,Tag=tag,Snapshot=snapshot.WithInstance(null),Code=new[]{Write(10),new ScriptInstruction(11,12,4)}};
                var evidence=ScriptStoryAnalyzer.Read(start,child,43,182,new[]{16,18,20},null,35,36,null,16,new[]{34,18,20});
                TestAssert.True(evidence.Reached.Contains(new StoryEffectSite(3,9,3,0,9)));
                TestAssert.False(evidence.Milestones.Contains(20));
            }
        }
    }
}
