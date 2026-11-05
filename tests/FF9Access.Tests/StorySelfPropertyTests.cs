using System;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class StorySelfPropertyTests
    {
        public static void SelfPositionUsesTheExecutingInstanceWithoutReadingOtherActors()
        {
            foreach(int uid in new[]{128,129}) {
                var start=new ScriptStoryFunction{Identity=uid*256+10,ScriptId=2,Tag=10,Snapshot=new ScriptSnapshot(null,null,null),Code=new[]{
                    new ScriptInstruction(0,1,5){Expression=new byte[]{0x78,255,1,0x7d,0,0,24,0x7f}},new ScriptInstruction(1,2,2){Argument=1},new ScriptInstruction(2,3,43){Argument=153},new ScriptInstruction(3,4,4)}};
                var evidence=ScriptStoryAnalyzer.Read(start,null,43,182,new[]{16,18,20},(id,property)=>FieldExitDestinations.PlayerProperty(129,new NavigationPoint(0,500,0),id,property));
                TestAssert.Equal(uid==129?1:0,evidence.Destinations.Count);
            }
        }
    }
}
