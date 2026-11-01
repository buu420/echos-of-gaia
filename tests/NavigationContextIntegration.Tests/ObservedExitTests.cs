using System;
using Memoria.Accessibility.Navigation;
internal static class ObservedExitTests
{
    private sealed class Fixture
    {
        internal readonly EventEngine Engine=UnityEngine.Object.FindObjectOfType<EventEngine>();
        internal readonly Quad Exit=new Quad{uid=7,sid=7};
        internal readonly NavigationContext Source;
        internal readonly TargetIdentity Id;
        internal Fixture(){NativeObservedFieldExits.Reset();NavigationSaveIdentity.Current="save";NavigationSaveIdentity.Identities.SwitchSave("save");NavigationLifecycleBridge.Generation=10;FF9StateSystem.Common.FF9.fldMapNo=102;Source=new NavigationContext{Generation=10,LogicalMapKey="field:102:"+FF9StateSystem.Field.SceneName,SaveIdentity="save"};Id=new TargetIdentity(TargetKind.FieldExit,Source.LogicalMapKey,7);Engine.Objects[7]=Exit;NativeObservedFieldExits.Presented(Source,Exit);}
        internal void Trigger(){NativeObservedFieldExits.Triggered(Engine,Engine.Controlled,Exit);}
        internal void Finish(){NativeObservedFieldExits.Detach(false);NativeObservedFieldExits.Attach(false);NavigationLifecycleBridge.Generation=12;FF9StateSystem.Common.FF9.fldMapNo=108;NativeObservedFieldExits.Arrive(new NavigationContext{Generation=12,LogicalMapKey="field:108:shop",SaveIdentity="save",CanControl=true,CurrentLocation="Item Shop"});}
        internal bool Learned(){string label;return NavigationSaveIdentity.Identities.TryGetLabel(Id,NativeFieldTargets.ExitLabelSignature(Exit),out label) && label=="Last led to Item Shop";}
    }
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
    public static void IndependentSharedScriptCannotClaimAnActivePlayerPush(){var f=new Fixture();f.Trigger();NativeObservedFieldExits.Executed(f.Engine,f.Exit,new Obj{uid=100},108);f.Finish();Require(!f.Learned(),"independent shared script used normalized owner to claim a player push");}
    public static void ManualTraversalLearnsExactPresentedExit(){var f=new Fixture();f.Trigger();NativeObservedFieldExits.Executed(f.Engine,f.Exit,f.Exit,108);f.Finish();Require(f.Learned(),"actual direct manual exit was not learned");}
    public static void UnknownReusedOrChangedQuadCannotBeLearned(){var f=new Fixture();f.Engine.Objects[7]=new Quad{uid=7,sid=7};f.Trigger();NativeObservedFieldExits.Executed(f.Engine,f.Exit,f.Exit,108);f.Finish();Require(!f.Learned(),"reused Quad reference accepted");f=new Fixture();f.Trigger();f.Exit.Signature="moved polygon";NativeObservedFieldExits.Executed(f.Engine,f.Exit,f.Exit,108);f.Finish();Require(!f.Learned(),"changed exit geometry accepted");}
    public static void OtherActorAndIndirectOwnerCannotClaimPlayerExit(){var f=new Fixture();NativeObservedFieldExits.Triggered(f.Engine,new Actor{uid=9},f.Exit);NativeObservedFieldExits.Executed(f.Engine,f.Exit,f.Exit,108);f.Finish();Require(!f.Learned(),"NPC trigger accepted");f=new Fixture();f.Trigger();NativeObservedFieldExits.Executed(f.Engine,new Quad{uid=7,sid=7},f.Exit,108);f.Finish();Require(!f.Learned(),"unmatched jump owner accepted");}
    public static void UnpresentedAndUnexecutedTriggersRemainUnknown(){var f=new Fixture();NativeObservedFieldExits.Reset();f.Trigger();NativeObservedFieldExits.Executed(f.Engine,f.Exit,f.Exit,108);f.Finish();Require(!f.Learned(),"unpresented exit accepted");f=new Fixture();f.Trigger();f.Finish();Require(!f.Learned(),"unexecuted trigger became knowledge");}
    public static void FinishedPushCannotNameALaterJumpByTheSameOwner(){var f=new Fixture();f.Trigger();NativeObservedFieldExits.Returned(f.Exit);NativeObservedFieldExits.Executed(f.Engine,f.Exit,f.Exit,108);f.Finish();Require(!f.Learned(),"completed invocation claimed a later jump");}
    public static void NestedReturnPreservesTheOriginalPushButAnotherDepthCannotJump(){var f=new Fixture();f.Trigger();f.Exit.sx=4;NativeObservedFieldExits.Returned(f.Exit);f.Exit.sx=2;NativeObservedFieldExits.Executed(f.Engine,f.Exit,f.Exit,108);f.Finish();Require(f.Learned(),"nested return discarded original push");f=new Fixture();f.Trigger();f.Exit.sx=4;NativeObservedFieldExits.Executed(f.Engine,f.Exit,f.Exit,108);f.Finish();Require(!f.Learned(),"different invocation stack claimed the jump");}
}
