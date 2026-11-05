using System;
using System.Collections.Generic;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    // Native exit paths whose branches wait on player input or a shown choice (FieldExitPaths).
    internal static class FieldExitPathTests
    {
        private const int MapJump=43,WorldJump=182,Req=20,Expr=5,Ifn=2,Jmp=1,Ret=4,Switch=11;
        private static readonly int[] Requests={Req};
        // Expression bytes (EBin encoding): constant, variable, system variable, operators.
        private static byte[] C(int v){return new[]{(byte)0x7d,(byte)(v&255),(byte)((v>>8)&255)};}
        private static byte[] MapByte(int index){return new[]{(byte)0xd5,(byte)index};}
        private static byte[] Choice(){return new byte[]{0x7a,9};}
        private static byte[] E(params byte[][] parts){var all=new List<byte>();foreach(var p in parts)all.AddRange(p);all.Add(0x7f);return all.ToArray();}
        private static byte[] Op(int op){return new[]{(byte)op};}
        private sealed class Code
        {
            public readonly List<ScriptInstruction> Items=new List<ScriptInstruction>();int _at=10;
            public int Add(int opcode,byte[] expression=null,params int?[] args)
            {
                int size=expression!=null?1+expression.Length:1+2*Math.Max(args.Length,0);
                var item=new ScriptInstruction(_at,_at+size,opcode){Expression=expression,Arguments=args.Length==0?null:args};
                if(args.Length!=0)item.Argument=args[0];
                Items.Add(item);_at+=size;return item.Offset;
            }
            public ScriptInstruction Last{get{return Items[Items.Count-1];}}
            // Point a branch instruction at an offset (relative to its next instruction).
            public void Target(ScriptInstruction branch,int offset){branch.Argument=offset-branch.NextOffset;branch.Arguments=new int?[]{branch.Argument};}
        }
        private static FieldExitPathEvidence Read(Code code,Func<int,bool?> party=null,Dictionary<int,Code> requested=null,byte[] map=null)
        {
            Func<ScriptStoryFunction> start=()=>new ScriptStoryFunction{Identity=1,ScriptId=1,Tag=2,Code=code.Items,Snapshot=new ScriptSnapshot(new byte[64],map??new byte[64],new byte[16]){UserControl=true}};
            Func<int,int,int,ScriptSnapshot,ScriptStoryFunction> request=(uid,tag,caller,snapshot)=>{Code called;return requested!=null && requested.TryGetValue(uid,out called)?new ScriptStoryFunction{Identity=uid*256+tag,ScriptId=uid,Tag=tag,Code=called.Items,Snapshot=snapshot.WithInstance(new byte[16])}:null;};
            return FieldExitPaths.Read(start,request,MapJump,WorldJump,Requests,null,party,-1,-1,null);
        }
        // if(choice==0) jump 563 else return.
        public static void AConfirmationPromptLeadsToOnePlaceOrDeclines()
        {
            var code=new Code();code.Add(Expr,E(Choice(),C(0),Op(32)));code.Add(Ifn,null,0);var branch=code.Last;
            code.Add(MapJump,null,2715);int decline=code.Add(Ret);code.Target(branch,decline);
            var evidence=Read(code);
            TestAssert.Equal(FieldExitPathKind.ChoiceSingle,evidence.Kind);TestAssert.Equal(1,evidence.Destinations.Count);TestAssert.True(evidence.Destinations.Contains(2715));TestAssert.True(evidence.CanDecline);
        }
        public static void IifaSelfJumpRetainsItsNativeArrivalEntrance()
        {
            var code=new Code();code.Add(Expr,E(new byte[]{0xd8,2},C(6),Op(44)));code.Add(MapJump,null,1651);
            var evidence=Read(code);
            TestAssert.Equal(FieldExitPathKind.Determined,evidence.Kind);
            TestAssert.Equal((int?)6,evidence.ArrivalEntrances[1651]);
        }
        // switch(choice){0:map5=1;1:map5=2;2:map5=4} then the same jump: the choice is carried to the next field.
        public static void AMenuThatCarriesItsChoiceIsNotASingleDestination()
        {
            var code=new Code();code.Add(Expr,E(Choice()));code.Add(Switch,null,0,0,0,0);var sw=code.Last;
            int a=code.Add(Expr,E(MapByte(5),C(1),Op(44)));code.Add(Jmp,null,0);var ja=code.Last;
            int b=code.Add(Expr,E(MapByte(5),C(2),Op(44)));code.Add(Jmp,null,0);var jb=code.Last;
            int c=code.Add(Expr,E(MapByte(5),C(4),Op(44)));int jump=code.Add(MapJump,null,563);
            code.Target(ja,jump);code.Target(jb,jump);
            // Switch (opcode 11): [base, default, case0, case1, case2] relative to Offset+1.
            sw.Arguments=new int?[]{0,c-(sw.Offset+1),a-(sw.Offset+1),b-(sw.Offset+1),c-(sw.Offset+1)};
            var evidence=Read(code);
            TestAssert.Equal(FieldExitPathKind.ChoiceMenu,evidence.Kind);TestAssert.True(evidence.Destinations.Contains(563));
        }
        // A conversation choice that only changes what is said still always leaves to one place.
        public static void AConversationChoiceThatAlwaysLeavesToOnePlaceIsDetermined()
        {
            var code=new Code();code.Add(Expr,E(Choice(),C(0),Op(32)));code.Add(Ifn,null,0);var branch=code.Last;
            code.Add(Jmp,null,0);var skip=code.Last;int other=code.Add(Expr,E(C(1)));int jump=code.Add(MapJump,null,2162);
            code.Target(branch,other);code.Target(skip,jump);
            var evidence=Read(code);
            TestAssert.Equal(FieldExitPathKind.Determined,evidence.Kind);TestAssert.True(evidence.Destinations.Contains(2162));TestAssert.False(evidence.CanDecline);
        }
        // while(!KEYON(0x0108)) loop; then jump: a help window waiting for Confirm does not change the place.
        public static void AButtonWaitDoesNotHideTheDestination()
        {
            var code=new Code();int loop=code.Add(Expr,E(C(0x108),Op(79),Op(14)));code.Add(3,null,0);var back=code.Last;
            back.Argument=(short)(loop-back.NextOffset);back.Arguments=new int?[]{back.Argument};
            code.Add(MapJump,null,1452);
            var evidence=Read(code);
            TestAssert.Equal(FieldExitPathKind.Determined,evidence.Kind);TestAssert.True(evidence.Destinations.Contains(1452));
        }
        // if(B_PARTYCHK(5)) jump 656 else jump 655: read from the live party, never both.
        public static void APartyCheckIsReadFromTheLiveParty()
        {
            var code=new Code();code.Add(Expr,E(C(5),Op(107)));code.Add(Ifn,null,0);var branch=code.Last;
            code.Add(MapJump,null,656);int other=code.Add(MapJump,null,655);code.Target(branch,other);
            var present=Read(code,x=>x==5);TestAssert.Equal(FieldExitPathKind.Determined,present.Kind);TestAssert.Equal(1,present.Destinations.Count);TestAssert.True(present.Destinations.Contains(656));
            var absent=Read(code,x=>false);TestAssert.True(absent.Destinations.Contains(655));TestAssert.Equal(1,absent.Destinations.Count);
            TestAssert.Equal(FieldExitPathKind.Unknown,Read(code).Kind);
        }
        // An unreadable state branch (another actor's position) is unknown, not forked into all places.
        public static void AnUnreadableStateBranchIsUnknownNotEveryPlace()
        {
            var code=new Code();code.Add(Expr,E(new byte[]{0x78,7,0},C(100),Op(25)));code.Add(Ifn,null,0);var branch=code.Last;
            code.Add(MapJump,null,1424);int other=code.Add(MapJump,null,1425);code.Target(branch,other);
            var evidence=Read(code);TestAssert.Equal(FieldExitPathKind.Unknown,evidence.Kind);TestAssert.Equal(0,evidence.Destinations.Count);
        }
        // A party join (B_PARTYADD) is a native mutation and is never evaluated.
        public static void APartyMutationStopsTheProof()
        {
            var code=new Code();code.Add(Expr,E(C(5),Op(109)));code.Add(Ifn,null,0);var branch=code.Last;
            code.Add(MapJump,null,656);int other=code.Add(Ret);code.Target(branch,other);
            TestAssert.Equal(FieldExitPathKind.Unknown,Read(code,x=>true).Kind);
        }
        public static void AnAngledMemoriaDoorReadsTheProjectedEdgeBeforeMoving()
        {
            const int Mjpos=164;
            var point=new NavigationPoint(0,0,100);
            var jump=FieldExitDestinations.JumpPoint(point,-320,0,320,0);
            TestAssert.Equal(0f,jump.X);TestAssert.Equal(0f,jump.Z);
            var code=new Code();code.Add(Mjpos);
            byte[] angleMemory={0xd9,6};
            // Same relative-angle expression as 2916 region 2: native SYS10/11
            // minus the player's position, then ANGLE2, then compare facing.
            code.Add(Expr,E(angleMemory,new byte[]{0x78,250,3},new byte[]{0x7a,10},new byte[]{0x78,250,0},Op(21),
                new byte[]{0x7a,11},new byte[]{0x78,250,2},Op(21),Op(102),Op(21),C(255),Op(36),Op(44)));
            code.Add(Expr,E(angleMemory,C(48),Op(24),angleMemory,C(208),Op(25),Op(40)));
            code.Add(Ifn,null,0);var branch=code.Last;code.Add(MapJump,null,2917);int refuse=code.Add(Ret);code.Target(branch,refuse);
            FieldExitPathEvidence Read(int heading)=>FieldExitPaths.Read(
                ()=>new ScriptStoryFunction{Identity=2*256+2,ScriptId=2,Tag=2,Code=code.Items,Snapshot=new ScriptSnapshot(new byte[32],new byte[64],null)},
                null,MapJump,WorldJump,Requests,(uid,property)=>FieldExitDestinations.PlayerProperty(4,point,uid,property),null,-1,-1,new[]{Mjpos},
                new FieldExitPathOptions{CalculateExitPosition=Mjpos,ExitPosition=jump,EntryFacing=heading,Angle2=(x,z)=>{TestAssert.Equal(0,x);TestAssert.Equal(-100,z);return 0;}});
            TestAssert.Equal(FieldExitPathKind.Determined,Read(0).Kind);
            TestAssert.Equal(FieldExitPathKind.NoJump,Read(128).Kind);
        }
        // Current state still decides branches exactly as the native path would.
        public static void CurrentStateBranchesAreEvaluatedNotForked()
        {
            var code=new Code();code.Add(Expr,E(MapByte(3),C(1),Op(32)));code.Add(Ifn,null,0);var branch=code.Last;
            code.Add(MapJump,null,100);int other=code.Add(MapJump,null,101);code.Target(branch,other);
            var map=new byte[64];map[3]=1;
            var set=Read(code,null,null,map);TestAssert.Equal(FieldExitPathKind.Determined,set.Kind);TestAssert.True(set.Destinations.Contains(100));TestAssert.Equal(1,set.Destinations.Count);
            var clear=Read(code);TestAssert.True(clear.Destinations.Contains(101));TestAssert.Equal(1,clear.Destinations.Count);
        }
        // A choice made inside a requested entry is followed there.
        public static void ChoicesInsideRequestedEntriesAreFollowed()
        {
            var called=new Code();called.Add(Expr,E(Choice(),C(0),Op(32)));called.Add(Ifn,null,0);var branch=called.Last;
            called.Add(WorldJump,null,9003);int no=called.Add(Ret);called.Target(branch,no);
            var code=new Code();code.Add(Req,null,0,14,22);code.Add(Ret);
            var evidence=Read(code,null,new Dictionary<int,Code>{{14,called}});
            TestAssert.Equal(FieldExitPathKind.ChoiceSingle,evidence.Kind);TestAssert.True(evidence.Destinations.Contains(9003));
        }
    }
}
