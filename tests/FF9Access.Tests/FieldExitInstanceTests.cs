using System;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class FieldExitInstanceTests
    {
        private static ScriptInstruction Request(int offset,int uid,int tag){return new ScriptInstruction(offset,offset+1,20){Arguments=new int?[]{2,uid,tag}};}
        private static ScriptInstruction Expr(int offset,params byte[] bytes){return new ScriptInstruction(offset,offset+1,5){Expression=bytes};}
        private static ScriptInstruction Jump(int offset,int field){return new ScriptInstruction(offset,offset+1,43){Arguments=new int?[]{field}};}
        private static ScriptStoryFunction Function(int sid,int uid,int tag,ScriptSnapshot snapshot,params ScriptInstruction[] code)
        {return new ScriptStoryFunction{ScriptId=sid,Identity=uid*256+tag,Tag=tag,Snapshot=snapshot,Code=code};}
        private static FieldExitPathEvidence LocalCalls(int secondUid)
        {
            Func<ScriptStoryFunction> start=()=>Function(9,9,2,new ScriptSnapshot(new byte[32],new byte[32],new byte[32]),Request(0,128,10),Request(1,secondUid,11),new ScriptInstruction(2,3,4));
            Func<int,int,int,ScriptSnapshot,ScriptStoryFunction> called=(uid,tag,caller,snapshot)=>tag==10?
                Function(8,uid,tag,snapshot.WithInstance(new byte[32]),Expr(10,0xd6,0,0x7d,1,0,44,0x7f),new ScriptInstruction(11,12,4)):
                Function(8,uid,tag,snapshot.WithInstance(new byte[32]),Expr(20,0xd6,0,0x7f),new ScriptInstruction(21,22,2){Argument=1},Jump(22,105),Jump(23,106));
            return FieldExitPaths.Read(start,called,43,182,new[]{16,18,20},null,null,35,36,null);
        }
        public static void ARepeatedCallToOneDoorDirectorKeepsItsOwnLocalWrites()
        {
            var evidence=LocalCalls(128);TestAssert.Equal(FieldExitPathKind.Determined,evidence.Kind);
            TestAssert.True(evidence.Destinations.SetEquals(new[]{105}));
        }
        public static void SiblingDoorInstancesDoNotShareTheirLocalWrites()
        {
            var evidence=LocalCalls(129);TestAssert.Equal(FieldExitPathKind.Determined,evidence.Kind);
            TestAssert.True(evidence.Destinations.SetEquals(new[]{106}));
        }
        public static void AControlledPlayersSelfReadUsesItsActualUid()
        {
            Func<ScriptStoryFunction> start=()=>Function(13,13,2,new ScriptSnapshot(new byte[32],null,null),Expr(0,0x78,255,0,0x7d,10,0,32,0x7f),new ScriptInstruction(1,2,2){Argument=1},Jump(2,105),Jump(3,106));
            var evidence=FieldExitPaths.Read(start,null,43,182,new[]{16,18,20},(uid,property)=>uid==13 && property==0?(int?)10:null,null,35,36,null);
            TestAssert.Equal(FieldExitPathKind.Determined,evidence.Kind);TestAssert.True(evidence.Destinations.SetEquals(new[]{105}));
        }
        public static void AnotherActorsSelfReadStillDoesNotRevealItsUnseenPosition()
        {
            Func<ScriptStoryFunction> start=()=>Function(12,12,2,new ScriptSnapshot(new byte[32],null,null),Expr(0,0x78,255,0,0x7d,10,0,32,0x7f),new ScriptInstruction(1,2,2){Argument=1},Jump(2,105),Jump(3,106));
            var evidence=FieldExitPaths.Read(start,null,43,182,new[]{16,18,20},(uid,property)=>uid==13 && property==0?(int?)10:null,null,35,36,null);
            TestAssert.Equal(FieldExitPathKind.Unknown,evidence.Kind);
        }
        public static void FacingIsOnlyKnownForTheControlledPlayerAndAnActualReading()
        {
            var at=new NavigationPoint(3,4,5);
            TestAssert.Equal((int?)192,FieldExitDestinations.PlayerProperty(13,at,250,3,192));
            TestAssert.Equal((int?)192,FieldExitDestinations.PlayerProperty(13,at,13,3,192));
            TestAssert.Equal((int?)null,FieldExitDestinations.PlayerProperty(13,at,12,3,192));
            TestAssert.Equal((int?)null,FieldExitDestinations.PlayerProperty(13,at,250,3));
        }
    }
}
