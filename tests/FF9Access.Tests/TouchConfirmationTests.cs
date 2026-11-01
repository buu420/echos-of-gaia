using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class TouchConfirmationTests
    {
        private static FieldTouchConfirmation State(){return new FieldTouchConfirmation{Field=2108,Uid=6,Sid=6,Hash=FieldTouchConfirmation.SourceHash,CanControl=true,ShownUid=6,SelectedTouch=true,Contains=true,NativeFacingAllowed=true,OriginalBytes=true,Running=true,Level=7,Ip=-1,Depth=0,Wait=0,CallbackSid=4,CallbackModel=5478,CallbackHash=FieldTouchConfirmation.CallbackSourceHash,CallbackRunning=true,CallbackOriginalBytes=true,CallbackLevel=7,CallbackHasTag=true};}
        public static void IdleAndNativeQueuedTouchAreBothSafeManualConfirmPoints()
        {var s=State();TestAssert.True(s.Ready);s.Level=1;s.Ip=30;s.Depth=2;s.SavedIp=-1;s.SavedStatus=0xff0700;TestAssert.True(s.Ready);}
        public static void UnrelatedSourceOrCallbackCannotInheritTheCue()
        {var s=State();s.Hash="changed";TestAssert.False(s.Ready);s=State();s.CallbackHash="changed";TestAssert.False(s.Ready);s=State();s.CallbackLevel=4;TestAssert.False(s.Ready);s=State();s.CallbackModel=98;TestAssert.False(s.Ready);}
        public static void HiddenOutOfRangeOrFacingAwayDoesNotClaimReady()
        {var s=State();s.ShownUid=-1;TestAssert.False(s.Ready);s=State();s.SelectedTouch=false;TestAssert.False(s.Ready);s=State();s.Contains=false;TestAssert.False(s.Ready);s=State();s.NativeFacingAllowed=false;TestAssert.False(s.Ready);}
        public static void AdditionalBuffersAndUnverifiedStacksAreNotOriginalQueuedInput()
        {var s=State();s.OriginalBytes=false;TestAssert.False(s.Ready);s=State();s.Level=1;s.Ip=30;s.Depth=2;s.SavedIp=-1;s.SavedStatus=0;TestAssert.False(s.Ready);s.SavedStatus=0xff0700;s.Depth=4;TestAssert.False(s.Ready);}
        public static void WaitingOrMovedInstructionPointerCannotClaimIdleReadiness()
        {var s=State();s.Wait=255;TestAssert.False(s.Ready);s=State();s.Level=1;s.Ip=31;s.Depth=2;s.SavedIp=-1;s.SavedStatus=0xff0700;TestAssert.False(s.Ready);}
        public static void FrameControlAndFieldIdentityRemainMandatory()
        {var s=State();s.CanControl=false;TestAssert.False(s.Ready);s=State();s.Field=2109;TestAssert.False(s.Ready);s=State();s.Uid=7;TestAssert.False(s.Ready);}
    }
}
