using System.Collections.Generic;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class FieldDoorEvidenceTests
    {
        private static readonly FieldDoorOpcodes Codes=new FieldDoorOpcodes { MapJump=99,WorldJump=100,JumpPosition=101,MoveQuad=102,Move=103,Requests=new[]{105,106,107},Animations=new[]{108,109},InteractionBarriers=new[]{110,111} };
        private static ScriptInstruction I(int index,int opcode,params int?[] args) { return new ScriptInstruction(index,index+1,opcode){Arguments=args}; }
        private static ScriptInstruction PlayerWalk(int opcode=103) { return new ScriptInstruction(0,1,opcode){ArgumentExpressions=new[]{new byte[]{0x7a,10,0x7f},new byte[]{0x7a,11,0x7f}}}; }
        private static IList<ScriptInstruction> Player(int tag) {return tag==12?new[]{PlayerWalk()}:new ScriptInstruction[0];}
        private static DoorTileObservation Tile(int id=4) {return new DoorTileObservation{Overlay=id,Camera=0,Flags=2,HierarchyActive=true,HasRenderer=true,MinX=-160,MaxX=-96,MinY=-32,MaxY=80};}
        public static void NativeSwitchWalkOutRetainsEverySafeBranch()
        {
            var table=new ScriptInstruction(10,20,11){Arguments=new int?[]{17,39,19}};
            var code=new[]{I(0,101),new ScriptInstruction(1,10,102),table,I(30,100),I(50,4)};
            TestAssert.True(FieldDoorEvidence.HasPlainWalkOut(code,Player,Codes));
            var sparse=new ScriptInstruction(10,20,6){Arguments=new int?[]{36,83,16}};
            code=new[]{I(0,101),new ScriptInstruction(1,10,102),sparse,I(30,100),I(50,4)};
            TestAssert.True(FieldDoorEvidence.HasPlainWalkOut(code,Player,Codes));
        }
        public static void OwnRequestedPlayerWalkBindsDoorAnimation()
        {
            var code=new[]{I(0,101),I(1,107,2,250,12),I(2,108,0,0,4),I(3,99)};
            TestAssert.True(FieldDoorEvidence.HasWalkOut(code,Player,Codes));
            var animations=FieldDoorEvidence.Animations(code,Player,Codes);TestAssert.Equal(1,animations.Length);TestAssert.Equal(0,animations[0]);
        }
        public static void NativeMoveQuadNeedsNoRequestedFunction()
        {
            var code=new[]{I(0,101),I(1,102),I(2,108,3),I(3,99)};
            TestAssert.True(FieldDoorEvidence.HasWalkOut(code,Player,Codes));TestAssert.Equal(3,FieldDoorEvidence.Animations(code,Player,Codes)[0]);
        }
        public static void UnknownOrOtherActorRequestDoesNotBindAnExit()
        {
            foreach(int? uid in new int?[]{null,7}) {var code=new[]{I(0,101),I(1,105,2,uid,12),I(2,108,0),I(3,99)};TestAssert.False(FieldDoorEvidence.HasWalkOut(code,Player,Codes));TestAssert.Equal(0,FieldDoorEvidence.Animations(code,Player,Codes).Length);}
        }
        public static void WalkFunctionMustUseExactNativeJumpCoordinates()
        {
            var code=new[]{I(0,101),I(1,105,2,250,12),I(2,108,0),I(3,99)};
            var other=PlayerWalk();other.ArgumentExpressions[1]=new byte[]{0x7a,13,0x7f};
            TestAssert.Equal(0,FieldDoorEvidence.Animations(code,t=>new[]{other},Codes).Length);
        }
        public static void ExpressionAnimationAndPostJumpTileAreUnresolved()
        {
            TestAssert.Equal(0,FieldDoorEvidence.Animations(new[]{I(0,101),I(1,102),I(2,108,null),I(3,99)},Player,Codes).Length);
            TestAssert.Equal(0,FieldDoorEvidence.Animations(new[]{I(0,101),I(1,102),I(2,99),I(3,108,0)},Player,Codes).Length);
        }
        public static void UnresolvedAnimatedDoorCannotDowngradeToPlainEdgeEvidence()
        {
            TestAssert.False(FieldDoorEvidence.HasPlainWalkOut(new[]{I(0,101),I(1,102),I(2,108,null),I(3,99)},Player,Codes));
            TestAssert.False(FieldDoorEvidence.HasPlainWalkOut(new[]{I(0,101),I(1,102),I(2,99),I(3,108,0)},Player,Codes));
            TestAssert.True(FieldDoorEvidence.HasPlainWalkOut(new[]{I(0,101),I(1,102),I(2,99)},Player,Codes));
        }
        public static void UnreachableAnimationOrDialogueOutcomeDoesNotBecomeDoorEvidence()
        {
            var code=new[]{I(0,1,2),I(1,101),I(2,108,0),I(3,102),I(4,99)};
            TestAssert.Equal(0,FieldDoorEvidence.Animations(code,Player,Codes).Length);
            TestAssert.Equal(0,FieldDoorEvidence.Animations(new[]{I(0,101),I(1,102),I(2,110),I(3,108,0),I(4,99)},Player,Codes).Length);
        }
        public static void ActualRenderedFrameBindsWithinNativePixelMargin()
        {
            int overlay;TestAssert.True(FieldDoorEvidence.TryBind(0,0,new[]{Tile()},new NavigationPoint(-100,-10,0),true,out overlay));TestAssert.Equal(4,overlay);
            TestAssert.True(FieldDoorEvidence.TryBind(0,0,new[]{Tile()},new NavigationPoint(-84,0,0),true,out overlay));
            TestAssert.False(FieldDoorEvidence.TryBind(0,0,new[]{Tile()},new NavigationPoint(-83.9f,0,0),true,out overlay));
        }
        public static void HiddenMissingWrongCameraAndAlteredPlacementHaveNoCue()
        {
            int overlay;TestAssert.False(FieldDoorEvidence.TryBind(1,0,new[]{Tile()},new NavigationPoint(-100,0,0),true,out overlay));
            foreach(int flag in new[]{0,3,10,130}) {var tile=Tile();tile.Flags=flag;TestAssert.False(FieldDoorEvidence.TryBind(0,0,new[]{tile},new NavigationPoint(-100,0,0),true,out overlay));}
            var missing=Tile();missing.HasRenderer=false;TestAssert.False(FieldDoorEvidence.TryBind(0,0,new[]{missing},new NavigationPoint(-100,0,0),true,out overlay));
            var inactive=Tile();inactive.HierarchyActive=false;TestAssert.False(FieldDoorEvidence.TryBind(0,0,new[]{inactive},new NavigationPoint(-100,0,0),true,out overlay));
        }
        public static void AmbiguousFramesOffscreenAndInvalidBoundsHaveNoCue()
        {
            int overlay;TestAssert.False(FieldDoorEvidence.TryBind(0,0,new[]{Tile(4),Tile(5)},new NavigationPoint(-100,0,0),true,out overlay));
            TestAssert.False(FieldDoorEvidence.TryBind(0,0,new[]{Tile()},new NavigationPoint(-100,0,0),false,out overlay));
            var tile=Tile();tile.MaxX=tile.MinX;TestAssert.False(FieldDoorEvidence.TryBind(0,0,new[]{tile},new NavigationPoint(-100,0,0),true,out overlay));
            tile=Tile();tile.MinY=float.NaN;TestAssert.False(FieldDoorEvidence.TryBind(0,0,new[]{tile},new NavigationPoint(-100,0,0),true,out overlay));
        }
        public static void EntranceEvidenceRequiresTheCurrentViewport()
        {
            TestAssert.False(FieldDoorEvidence.InViewport(new NavigationPoint(-1,100,1),1280,720));
            TestAssert.False(FieldDoorEvidence.InViewport(new NavigationPoint(1281,100,1),1280,720));
            TestAssert.False(FieldDoorEvidence.InViewport(new NavigationPoint(100,-1,1),1280,720));
            TestAssert.False(FieldDoorEvidence.InViewport(new NavigationPoint(100,721,1),1280,720));
            TestAssert.True(FieldDoorEvidence.InViewport(new NavigationPoint(0,0,1),1280,720));
            TestAssert.True(FieldDoorEvidence.InViewport(new NavigationPoint(1280,720,1),1280,720));
            TestAssert.False(FieldDoorEvidence.InViewport(new NavigationPoint(100,100,-1),1280,720));
        }
    }
}
