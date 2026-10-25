using System;
using System.Collections.Generic;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class ScriptStoryTests
    {
        private static ScriptInstruction Expr(int offset,int next,params byte[] bytes){return new ScriptInstruction(offset,next,5,bytes);}
        private static ScriptStoryEvidence Read(ScriptInstruction[] code,ScriptSnapshot snapshot,Func<int,int,int,ScriptSnapshot,ScriptStoryFunction> requested=null)
        {return ScriptStoryAnalyzer.Read(new ScriptStoryFunction{Identity=9,Code=code,Snapshot=snapshot},requested,99,100,new[]{101});}
        public static void CurrentCounterGuardRejectsTheLaterShipShortcut()
        {
            var code=new[]{Expr(0,15,0xdc,0,0x7d,0x63,0x29,27,0xdc,0,0x7d,0xb8,0x29,24,39,0x7f),new ScriptInstruction(15,18,2,1),new ScriptInstruction(18,19,99,2510),new ScriptInstruction(19,20,4)};
            foreach(int counter in new[]{10400,10595,10600,10680}) {
                var evidence=Read(code,new ScriptSnapshot(new[]{(byte)counter,(byte)(counter>>8)},null,null));
                TestAssert.Equal(counter>=10595 && counter<10680?1:0,evidence.Destinations.Count);
            }
        }
        public static void UnknownAssignmentCannotReuseAnOldGuardValue()
        {
            var code=new[]{Expr(0,8,0xc5,0,0x78,255,3,44,0x7f),Expr(8,12,0xc5,0,0x7f),new ScriptInstruction(12,15,2,1),new ScriptInstruction(15,16,99,2510),new ScriptInstruction(16,17,4)};
            byte[] map={1};var evidence=Read(code,new ScriptSnapshot(null,map,null));TestAssert.Equal(0,evidence.Destinations.Count);TestAssert.Equal((byte)1,map[0]);
        }
        public static void UnrelatedUnknownRotationDoesNotHideKnownTransit()
        {
            var code=new[]{Expr(0,8,0xd5,16,0x78,255,3,44,0x7f),new ScriptInstruction(8,9,100,9009)};
            TestAssert.True(Read(code,new ScriptSnapshot(null,new byte[64],null)).Destinations.Contains(9009));
        }
        public static void UnboundRequestStopsProofAndRequestsRemainReadOnly()
        {
            var request=new ScriptInstruction(0,5,101){Arguments=new int?[]{2,7,14}};
            var code=new[]{request,new ScriptInstruction(5,6,99,2510)};
            TestAssert.Equal(0,Read(code,new ScriptSnapshot(null,null,null),(uid,tag,caller,snapshot)=>null).Destinations.Count);
            int calls=0;var evidence=Read(code,new ScriptSnapshot(null,null,null),(uid,tag,caller,snapshot)=>{calls++;TestAssert.Equal(7,uid);TestAssert.Equal(14,tag);TestAssert.Equal(9,caller);return new ScriptStoryFunction{Identity=7*256+14,Code=new[]{new ScriptInstruction(0,1,4)},Snapshot=snapshot.WithInstance(null)};});
            TestAssert.Equal(1,calls);TestAssert.True(evidence.Destinations.Contains(2510));
        }
        public static void UnknownChoiceDoesNotInventItsDestinationAndLoopsAreBounded()
        {
            var code=new[]{Expr(0,4,0x7a,9,0x7f),new ScriptInstruction(4,7,2,1),new ScriptInstruction(7,8,99,2510),new ScriptInstruction(8,9,4)};
            TestAssert.Equal(0,Read(code,new ScriptSnapshot(null,null,null)).Destinations.Count);
            TestAssert.Equal(0,Read(new[]{new ScriptInstruction(0,3,1,65533)},new ScriptSnapshot(null,null,null)).Destinations.Count);
        }
    }
}
